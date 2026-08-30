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
	/// </remarks>
	public static class ThreadStackDump
	{
		/// <summary>
		/// Frames past this per thread are elided. A runaway recursion would otherwise bury the one stack the
		/// reader came for under tens of thousands of identical lines.
		/// </summary>
		private const int MaxFramesPerThread = 80;

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
		/// Builds the all-thread stack report. Throws if the capture fails; callers on a failure path should
		/// use <see cref="WriteToConsole"/>, which cannot throw.
		/// </summary>
		/// <param name="reason">Why the dump was taken - printed at the top so a log reader knows what latched it.</param>
		/// <returns>The report text, one section per managed thread.</returns>
		public static string Capture(string reason)
		{
			// A distinct file per capture: two watchdogs latching at once must not fight over one path.
			string dumpPath = Path.Combine(Path.GetTempPath(), $"agg-threadstacks-{Environment.ProcessId}-{Guid.NewGuid():N}.dmp");
			var timer = Stopwatch.StartNew();

			try
			{
				// DumpType.Normal is thread stacks plus the minimum the stack walker needs. WithHeap or Full
				// would answer questions nobody is asking here and cost orders of magnitude more time and disk.
				new DiagnosticsClient(Environment.ProcessId).WriteDump(DumpType.Normal, dumpPath, logDumpGeneration: false);

				long dumpBytes = new FileInfo(dumpPath).Length;
				long writeMilliseconds = timer.ElapsedMilliseconds;

				var report = new StringBuilder();
				report.AppendLine("======================= ALL MANAGED THREAD STACKS =======================");
				report.AppendLine($"reason: {reason}");
				report.AppendLine($"process {Environment.ProcessId} at {DateTime.Now:HH:mm:ss.fff}, dump {dumpBytes / (1024 * 1024)} MB written in {writeMilliseconds} ms");

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
