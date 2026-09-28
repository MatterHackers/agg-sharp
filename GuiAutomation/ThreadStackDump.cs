/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Runtime;

namespace MatterHackers.GuiAutomation
{
	/// <summary>
	/// Captures the managed call stack of every thread in this process, as text, so that a hang which only
	/// reproduces on a build server can name the frame it is stuck in. A hung UI thread prints nothing by
	/// definition, and a CI log that says only "the window did not close" is the same as no log at all.
	/// </summary>
	/// <remarks>
	/// Mechanism: ask the runtime's own diagnostic IPC server to write a *minidump* of this process
	/// (<see cref="DiagnosticsClient.WriteDump(DumpType, string, bool)"/>), then walk that dump with ClrMD.
	/// The alternatives were both worse:
	/// <list type="bullet">
	/// <item>ClrMD's <c>DataTarget.AttachToProcess</c> refuses the current process outright - it throws
	/// "Attaching to the current process is not supported".</item>
	/// <item>ClrMD's <c>DataTarget.CreateSnapshotAndAttach</c> is the sanctioned self-inspection route, but
	/// off Windows it is implemented as a *full* core dump: measured at 6.1 GB and 137 seconds for a hello
	/// world process, which is not something a watchdog can spend. The same measurement for the minidump
	/// below was 24 MB and 142 ms.</item>
	/// </list>
	/// Nothing here needs a debugger, an installed tool or elevated permissions - the diagnostic server is on
	/// by default in every runtime this ships against - so it works unchanged in Release on a CI runner. The
	/// single code path is also deliberate: the mac/Linux developer machine runs exactly what Windows CI runs,
	/// which is the only way <c>ThreadStackDumpTests</c> can vouch for the CI behaviour.
	/// <para>
	/// Capturing is retried, because a dump of a *live* process is a race that sometimes loses - see
	/// <see cref="CaptureAttempts"/>.
	/// </para>
	/// </remarks>
	public static class ThreadStackDump
	{
		/// <summary>
		/// Frames past this per thread are elided. A runaway recursion would otherwise bury the one stack the
		/// reader came for under tens of thousands of identical lines.
		/// </summary>
		private const int MaxFramesPerThread = 80;

		/// <summary>
		/// How many complete captures to try before giving up. A dump is taken of a process that is still
		/// running, so the thread list it describes can change underneath the capture, and both halves of the
		/// capture can lose that race:
		/// <list type="bullet">
		/// <item>the runtime's <c>createdump</c> reads each thread's register state after enumerating threads,
		/// so a thread that exits in between fails the read outright - on macOS that reads as
		/// "thread_get_state(0) FAILED (ipc/send) invalid destination port".</item>
		/// <item>ClrMD's mac core reader keys thread contexts by looking each context's stack pointer up in a
		/// stack-pointer-to-thread-id map (<c>MachOCoreDump</c>'s constructor, unchanged from 3.0 through
		/// <c>main</c>, so 4.x is no escape). Two contexts whose stack pointers collapse to one id - which is
		/// what a dump of a mutating thread list can contain - make its <c>Dictionary.Add</c> throw
		/// "An item with the same key has already been added". That one did not always clear on a retry, so
		/// <see cref="DropThreadContextsWithRepeatedStackPointers"/> now repairs the file before it is read.</item>
		/// </list>
		/// Both are properties of the *captured file*, not of the parse, so a retry has to write a whole new dump
		/// rather than re-read the one that failed.
		/// <para>
		/// Five rather than two because the losses come in bursts, so attempts are nowhere near independent.
		/// Measured over 250 captures taken against 24 threads each creating and joining a thread in a tight
		/// loop, on a 10 core mac also running 16 spinners: unretried, 35 captures failed; retried, 52 first
		/// attempts failed but only 7 survived three attempts and none survived five.
		/// </para>
		/// </summary>
		internal const int CaptureAttempts = 5;

		/// <summary>
		/// Pause between capture attempts. The race is lost in bursts, so an immediate retry is likelier to
		/// land in the same burst; this is short enough to be free next to the minutes a watchdog waited to
		/// get here.
		/// </summary>
		private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromMilliseconds(250);

		/// <summary>
		/// What each registered thread is *for*, keyed by managed thread id. A dump can read a thread's ids and
		/// its runtime state flags, but not its <see cref="Thread.Name"/>, and there is no way to enumerate
		/// other threads' <see cref="Thread"/> objects from inside the process - so anything that wants to be
		/// recognisable in the dump has to say so from its own thread while it is still running.
		/// </summary>
		private static readonly ConcurrentDictionary<int, string> threadRoles = new ConcurrentDictionary<int, string>();

		/// <summary>
		/// Labels the calling thread so it can be picked out of a later stack dump by role rather than by id.
		/// </summary>
		/// <param name="role">Short description of what this thread does, e.g. "UI THREAD (message pump)".</param>
		public static void RegisterCurrentThread(string role)
		{
			var thread = Thread.CurrentThread;
			string name = string.IsNullOrEmpty(thread.Name) ? "(unnamed)" : thread.Name;

			threadRoles[thread.ManagedThreadId] = $"{role} name=\"{name}\" threadPool={thread.IsThreadPoolThread} background={thread.IsBackground}";
		}

		/// <summary>
		/// Builds the all-thread stack report. Throws if every capture attempt fails; callers on a failure path
		/// should use <see cref="WriteToConsole"/>, which cannot throw.
		/// </summary>
		/// <param name="reason">Why the dump was taken - printed at the top so a log reader knows what latched it.</param>
		/// <returns>The report text, one section per managed thread.</returns>
		public static string Capture(string reason)
		{
			return CaptureWithRetries(reason, () => CaptureOnce(reason), () => Thread.Sleep(DelayBetweenAttempts));
		}

		/// <summary>
		/// Runs <paramref name="captureAttempt"/> until it produces a report or <see cref="CaptureAttempts"/> is
		/// exhausted, waiting via <paramref name="waitBetweenAttempts"/> in between.
		/// </summary>
		/// <remarks>
		/// Split out from <see cref="Capture"/> so the retry policy itself is testable without having to lose a
		/// real dump race on purpose. The delegate must take a *fresh* dump every time - see
		/// <see cref="CaptureAttempts"/> for why re-parsing the same file would fix nothing.
		/// </remarks>
		/// <param name="reason">Why the dump was taken - repeated in the give-up exception.</param>
		/// <param name="captureAttempt">Takes one complete dump and returns its report; may throw.</param>
		/// <param name="waitBetweenAttempts">Called after a failed attempt, before the next one.</param>
		/// <returns>The report text from the first attempt that succeeded.</returns>
		internal static string CaptureWithRetries(string reason, Func<string> captureAttempt, Action waitBetweenAttempts)
		{
			List<Exception> failures = null;

			for (int attempt = 1; attempt <= CaptureAttempts; attempt++)
			{
				try
				{
					string report = captureAttempt();

					// Say so when an earlier attempt lost the race, otherwise a report that took several tries
					// looks exactly like one that took none, and the next person to debug this starts over.
					return failures == null ? report : DescribeFailedAttempts(failures) + report;
				}
				catch (Exception ex)
				{
					failures ??= new List<Exception>();
					failures.Add(ex);

					if (attempt < CaptureAttempts)
					{
						waitBetweenAttempts();
					}
				}
			}

			throw new AggregateException($"Thread stack capture failed on all {CaptureAttempts} attempts ({reason}).", failures);
		}

		private static string CaptureOnce(string reason)
		{
			// A distinct file per capture: two watchdogs latching at once must not fight over one path, and a
			// retry must not read back the file whose contents just failed to parse.
			string dumpPath = Path.Combine(Path.GetTempPath(), $"agg-threadstacks-{Environment.ProcessId}-{Guid.NewGuid():N}.dmp");
			var timer = Stopwatch.StartNew();

			try
			{
				WriteDumpOfThisProcess(dumpPath);

				return ReportFromDump(reason, dumpPath, timer.ElapsedMilliseconds, timer);
			}
			finally
			{
				try
				{
					File.Delete(dumpPath);
				}
				catch
				{
					// A leaked temp file is not worth failing a diagnostic over; the OS will clear it.
				}
			}
		}

		/// <summary>
		/// Writes a minidump of this process to <paramref name="dumpPath"/>.
		/// </summary>
		/// <returns>The dump writer's own log where it has one (macOS), otherwise an empty string.</returns>
		internal static string WriteDumpOfThisProcess(string dumpPath)
		{
			// DumpType.Normal is thread stacks plus the minimum the stack walker needs. WithHeap or Full
			// would answer questions nobody is asking here and cost orders of magnitude more time and disk.
			if (OperatingSystem.IsMacOS())
			{
				return RunCreateDump(dumpPath);
			}

			new DiagnosticsClient(Environment.ProcessId).WriteDump(DumpType.Normal, dumpPath, logDumpGeneration: false);
			return string.Empty;
		}

		/// <summary>
		/// Runs the runtime's <c>createdump</c> against this process with its output redirected, which is what
		/// <see cref="DiagnosticsClient.WriteDump(DumpType, string, bool)"/> does on this OS minus the noise.
		/// </summary>
		/// <remarks>
		/// Launched by the runtime's diagnostic server, createdump inherits the host's stdout and prints five
		/// "[createdump] ..." status lines per capture whatever <c>logDumpGeneration</c> says - the runtime
		/// passes no log file on that path and ignores <c>DOTNET_CreateDumpLogToFile</c> there. In a test run
		/// those lines landed in the suite's output and read as a crash. Same tool, same arguments, so the
		/// dump is identical. macOS only: on Linux, Yama's ptrace scope stops a process we start from
		/// attaching to us unless the runtime's PR_SET_PTRACER dance is repeated, and Windows is untested.
		/// </remarks>
		private static string RunCreateDump(string dumpPath)
		{
			string createDump = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "createdump");
			var startInfo = new ProcessStartInfo(createDump)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			startInfo.ArgumentList.Add("--normal");
			startInfo.ArgumentList.Add("--name");
			startInfo.ArgumentList.Add(dumpPath);
			startInfo.ArgumentList.Add(Environment.ProcessId.ToString());

			using (var process = new Process { StartInfo = startInfo })
			{
				// Drain stderr on its own callback while stdout is read here, so neither pipe can fill and
				// stall the writer. WaitForExit() (no timeout) also waits for the callback's end of stream.
				var error = new StringBuilder();
				process.ErrorDataReceived += (s, e) =>
				{
					if (e.Data != null)
					{
						lock (error)
						{
							error.AppendLine(e.Data);
						}
					}
				};

				process.Start();
				process.BeginErrorReadLine();
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();

				string log;
				lock (error)
				{
					log = output + error;
				}

				if (process.ExitCode != 0)
				{
					throw new InvalidOperationException($"createdump exited with {process.ExitCode}: {log.Trim()}");
				}

				return log;
			}
		}

		/// <summary>
		/// Walks an already written dump and builds the all-thread report from it. May modify the file - see
		/// <see cref="DropThreadContextsWithRepeatedStackPointers"/>.
		/// </summary>
		/// <param name="reason">Why the dump was taken.</param>
		/// <param name="dumpPath">The dump to read; owned by the caller, who deletes it.</param>
		/// <param name="writeMilliseconds">How long writing the dump took, for the header line.</param>
		/// <param name="timer">Started when the capture began; its total goes in the footer.</param>
		internal static string ReportFromDump(string reason, string dumpPath, long writeMilliseconds, Stopwatch timer)
		{
			int droppedContexts = DropThreadContextsWithRepeatedStackPointers(dumpPath);
			long dumpBytes = new FileInfo(dumpPath).Length;

			var report = new StringBuilder();
			report.AppendLine("======================= ALL MANAGED THREAD STACKS =======================");
			report.AppendLine($"reason: {reason}");
			report.AppendLine($"process {Environment.ProcessId} at {DateTime.Now:HH:mm:ss.fff}, dump {dumpBytes / (1024 * 1024)} MB written in {writeMilliseconds} ms");

			if (droppedContexts > 0)
			{
				report.AppendLine($"({droppedContexts} thread register context(s) repeated an earlier thread's stack pointer and were dropped; the thread that owns that stack pointer keeps the first one, others may show no frames)");
			}

			using (var dataTarget = DataTarget.LoadDump(dumpPath))
			{
				int runtimeCount = 0;

				foreach (var clrInfo in dataTarget.ClrVersions)
				{
					runtimeCount++;
					AppendRuntimeThreads(report, clrInfo.CreateRuntime());
				}

				if (runtimeCount == 0)
				{
					report.AppendLine("(no CLR found in the dump - nothing to walk)");
				}
			}

			report.AppendLine($"===================== END THREAD STACKS ({timer.ElapsedMilliseconds} ms) =====================");

			return report.ToString();
		}

		private const uint MachOMagic64 = 0xFEEDFACF;
		private const uint MachOCpuTypeX86_64 = 0x01000007;
		private const uint MachOCpuTypeArm64 = 0x0100000C;
		private const uint MachOLoadCommandThread = 0x4;

		/// <summary>
		/// Written over the command type of a dropped LC_THREAD. ClrMD's load command loop only acts on
		/// LC_SEGMENT_64 and LC_THREAD and skips anything else by its size, and 0 is no Mach-O command.
		/// </summary>
		private const uint MachOIgnoredLoadCommand = 0;

		/// <summary>
		/// Offset of each LC_THREAD in a mac core dump, with the stack pointer ClrMD will read from it.
		/// Empty for anything that is not a 64-bit Mach-O of a CPU ClrMD reads (Windows minidumps, Linux ELF cores).
		/// </summary>
		/// <remarks>
		/// Mirrors ClrMD 3.1's <c>MachOCoreDump</c> constructor: a thread command whose flavor is not the CPU's
		/// general register set leaves its context zeroed, so its stack pointer reads as 0.
		/// </remarks>
		internal static List<(long CommandOffset, ulong StackPointer)> ReadThreadStackPointers(Stream stream)
		{
			var threads = new List<(long, ulong)>();
			var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

			if (stream.Length < 32)
			{
				return threads;
			}

			stream.Position = 0;
			uint magic = reader.ReadUInt32();
			uint cpuType = reader.ReadUInt32();

			// thread_command is cmd, cmdsize, flavor, count, then the register state.
			const int stateOffset = 16;
			uint generalRegistersFlavor;
			int stackPointerOffset;

			if (magic != MachOMagic64)
			{
				return threads;
			}
			else if (cpuType == MachOCpuTypeArm64)
			{
				// ARM_THREAD_STATE64: x0-x28, fp, lr, then sp.
				generalRegistersFlavor = 6;
				stackPointerOffset = 29 * 8 + 2 * 8;
			}
			else if (cpuType == MachOCpuTypeX86_64)
			{
				// x86_THREAD_STATE64: rax, rbx, rcx, rdx, rdi, rsi, rbp, then rsp.
				generalRegistersFlavor = 4;
				stackPointerOffset = 7 * 8;
			}
			else
			{
				return threads;
			}

			stream.Position = 16;
			uint commandCount = reader.ReadUInt32();
			long commandStart = 32;

			for (uint i = 0; i < commandCount && commandStart + 8 <= stream.Length; i++)
			{
				stream.Position = commandStart;
				uint command = reader.ReadUInt32();
				uint commandSize = reader.ReadUInt32();

				if (commandSize < 8)
				{
					// Corrupt; ClrMD will say so better than a guess here would.
					break;
				}

				if (commandStart + commandSize > stream.Length)
				{
					// Truncated: the command runs past the end of the file. Reading its registers would throw, so
					// stop here and leave the dump for ClrMD to judge.
					break;
				}

				if (command == MachOLoadCommandThread)
				{
					ulong stackPointer = 0;

					if (commandSize >= stateOffset + stackPointerOffset + 8 && reader.ReadUInt32() == generalRegistersFlavor)
					{
						stream.Position = commandStart + stateOffset + stackPointerOffset;
						stackPointer = reader.ReadUInt64();
					}

					threads.Add((commandStart, stackPointer));
				}

				commandStart += commandSize;
			}

			return threads;
		}

		/// <summary>
		/// Keeps ClrMD's mac core reader from rejecting a whole dump because two thread contexts share a stack
		/// pointer: every thread command after the first with a given stack pointer is turned into a command
		/// ClrMD skips. Returns how many were dropped. Does nothing to a dump that is not a Mach-O core.
		/// </summary>
		/// <remarks>
		/// ClrMD's <c>MachOCoreDump</c> constructor maps each context's stack pointer to a thread id through
		/// createdump's THREADINFO table and then <c>Dictionary.Add</c>s the context under that id, so two
		/// contexts with one stack pointer throw "An item with the same key has already been added" and the
		/// dump cannot be opened at all. That state is not always a passing race: one full agg suite run lost
		/// all five attempts with the same key, so whatever left two contexts on one stack pointer outlived
		/// every re-capture. The common source, caught in full serial agg suite runs: createdump writes an
		/// all-zero general register context (only cpsr set) for a thread whose state it could not read, and
		/// two such threads share stack pointer 0 - ClrMD rejects that dump untouched. At most one of the two
		/// contexts can be that thread's real registers, and one
		/// thread with no frames beats a report with no threads. Keeping the first is what a non-throwing
		/// add in ClrMD would have done.
		/// </remarks>
		internal static int DropThreadContextsWithRepeatedStackPointers(string dumpPath)
		{
			using var stream = new FileStream(dumpPath, FileMode.Open, FileAccess.ReadWrite);
			var seenStackPointers = new HashSet<ulong>();
			var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
			int dropped = 0;

			foreach (var (commandOffset, stackPointer) in ReadThreadStackPointers(stream))
			{
				if (!seenStackPointers.Add(stackPointer))
				{
					stream.Position = commandOffset;
					writer.Write(MachOIgnoredLoadCommand);
					dropped++;
				}
			}

			writer.Flush();

			return dropped;
		}

		/// <summary>
		/// Writes the all-thread stack report to the console (which is what a TRX captures), degrading to a
		/// one line note if the capture itself fails.
		/// </summary>
		/// <remarks>
		/// Never throws. This is only ever called from a path that is already reporting a failure, and a
		/// diagnostic that replaces the failure it was meant to explain is worse than no diagnostic.
		/// </remarks>
		/// <param name="reason">Why the dump was taken.</param>
		public static void WriteToConsole(string reason)
		{
			try
			{
				Console.WriteLine(Capture(reason));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"THREAD STACK DUMP FAILED ({reason}): {ex.GetType().Name}: {ex.Message}");
			}
		}

		/// <summary>
		/// One line per lost race, printed above the report that finally worked.
		/// </summary>
		private static string DescribeFailedAttempts(List<Exception> failures)
		{
			var note = new StringBuilder();

			for (int i = 0; i < failures.Count; i++)
			{
				note.AppendLine($"(thread stack capture attempt {i + 1} of {CaptureAttempts} failed and was re-taken: {failures[i].GetType().Name}: {failures[i].Message})");
			}

			return note.ToString();
		}

		private static void AppendRuntimeThreads(StringBuilder report, ClrRuntime runtime)
		{
			foreach (var clrThread in runtime.Threads)
			{
				if (!clrThread.IsAlive)
				{
					continue;
				}

				string role = threadRoles.TryGetValue(clrThread.ManagedThreadId, out string registered)
					? registered
					: DescribeUnregisteredThread(clrThread);

				report.AppendLine();
				report.AppendLine($"--- thread os={clrThread.OSThreadId} managed={clrThread.ManagedThreadId} {role}");

				int frameCount = 0;

				foreach (var frame in clrThread.EnumerateStackTrace())
				{
					if (frameCount++ >= MaxFramesPerThread)
					{
						report.AppendLine($"    ... more than {MaxFramesPerThread} frames, rest elided");
						break;
					}

					report.AppendLine($"    {frame}");
				}

				if (frameCount == 0)
				{
					report.AppendLine("    (no managed frames - thread is in native code with no managed caller)");
				}
			}
		}

		/// <summary>
		/// Best available description of a thread that never called <see cref="RegisterCurrentThread"/>: the
		/// dump has no managed thread name, so the runtime's own state flags are all there is to go on.
		/// </summary>
		private static string DescribeUnregisteredThread(ClrThread clrThread)
		{
			bool isThreadPoolThread = clrThread.State.HasFlag(ClrThreadState.TS_TPWorkerThread)
				|| clrThread.State.HasFlag(ClrThreadState.TS_CompletionPortThread);

			string kind = clrThread.IsFinalizer ? "finalizer"
				: clrThread.IsGc ? "gc"
				: isThreadPoolThread ? "worker"
				: "unregistered";

			return $"({kind}) name=(unknown - not registered) threadPool={isThreadPoolThread} background={clrThread.State.HasFlag(ClrThreadState.TS_Background)}";
		}
	}
}
