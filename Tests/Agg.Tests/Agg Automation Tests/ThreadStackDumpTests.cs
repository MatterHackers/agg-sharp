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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using Microsoft.Diagnostics.Runtime;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Proves the close watchdog's stack dump actually produces stacks. It is only ever exercised for real on
	/// a build server, on a failure path, minutes into a hang - so if it were broken there, nobody would find
	/// out until the one run that needed it printed an apology instead of an answer.
	/// </summary>
	/// <remarks>
	/// Not in parallel: the capture asks the runtime to write a minidump of this process, which briefly
	/// suspends every thread in it. That is harmless to this test and unkind to a timing sensitive one
	/// running beside it.
	/// </remarks>
	[NotInParallel(nameof(ThreadStackDumpTests))]
	public class ThreadStackDumpTests
	{
		[Test]
		public async Task CaptureNamesTheCallingThreadsOwnFrames()
		{
			string report = CaptureFromADistinctivelyNamedFrame();

			// The frame below this one on the calling thread's stack. If the walker works at all, this is the
			// one line it cannot miss - it was on the stack while the dump was being written.
			await Assert.That(report).Contains(nameof(CaptureFromADistinctivelyNamedFrame));

			await Assert.That(report).Contains("ALL MANAGED THREAD STACKS");
			await Assert.That(report).Contains("proving the dump helper works");
		}

		/// <summary>
		/// In full mac suite runs a capture once came back whole but with the capturing thread (and a few
		/// others) walked to a single frame or spliced with another thread's frames - its register contexts did
		/// not match its stacks. The capturing thread is the one thread known to be inside ThreadStackDump while
		/// the dump is written, so a dump that cannot walk it back there is rejected, which makes
		/// <see cref="ThreadStackDump.Capture"/> take a new one instead of returning a report missing the caller.
		/// </summary>
		[Test]
		public async Task ADumpThatCannotWalkItsCapturingThreadIsRejected()
		{
			var blocked = new ManualResetEventSlim(false);
			var started = new ManualResetEventSlim(false);
			var bystander = new Thread(() =>
			{
				started.Set();
				blocked.Wait();
			})
			{
				IsBackground = true,
			};

			bystander.Start();
			string dumpPath = Path.Combine(Path.GetTempPath(), $"agg-threadstacks-test-{Guid.NewGuid():N}.dmp");

			try
			{
				started.Wait();
				ThreadStackDump.WriteDumpOfThisProcess(dumpPath);
				int writer = Environment.CurrentManagedThreadId;

				string report = ThreadStackDump.ReportFromDump("the writer is walked", dumpPath, 0, Stopwatch.StartNew(), writer);
				await Assert.That(report).Contains("END THREAD STACKS");

				// The bystander never entered ThreadStackDump: named as the capturer, it reads as a dump whose
				// contexts are wrong for the thread that took it.
				InvalidOperationException rejected = null;
				try
				{
					ThreadStackDump.ReportFromDump("the capturer is not walked", dumpPath, 0, Stopwatch.StartNew(), bystander.ManagedThreadId);
				}
				catch (InvalidOperationException ex)
				{
					rejected = ex;
				}

				await Assert.That(rejected).IsNotNull();
				await Assert.That(rejected.Message).Contains($"managed {bystander.ManagedThreadId}");
			}
			finally
			{
				blocked.Set();
				bystander.Join();
				File.Delete(dumpPath);
			}
		}

		/// <summary>
		/// On macOS the runtime's dump writer, <c>createdump</c>, prints five "[createdump] ..." status lines
		/// per capture. Launched by the runtime's diagnostic server it inherits the host's stdout, so every
		/// capture - three from this class and one from the vetoed-close watchdog test - landed in the
		/// suite's console output, where they read like a crash. The writer has to hand its log back to
		/// the caller instead of printing it.
		/// </summary>
		[Test]
		public async Task WritingADumpCapturesTheDumpWritersLogInsteadOfPrintingIt()
		{
			if (!OperatingSystem.IsMacOS())
			{
				// Only the mac leg runs createdump itself; elsewhere the runtime's own path is kept.
				return;
			}

			string dumpPath = Path.Combine(Path.GetTempPath(), $"agg-threadstacks-test-{Guid.NewGuid():N}.dmp");

			try
			{
				string log = ThreadStackDump.WriteDumpOfThisProcess(dumpPath);

				await Assert.That(log).Contains("[createdump]");
				await Assert.That(new FileInfo(dumpPath).Length).IsGreaterThan(0);
			}
			finally
			{
				File.Delete(dumpPath);
			}
		}

		[Test]
		public async Task RegisteredThreadsAreLabelledInTheDump()
		{
			// The watchdog's whole value rests on this mapping: the dump only knows thread ids, so a role
			// registered from a live thread has to come back out attached to the right one.
			var blocked = new ManualResetEventSlim(false);
			int registeredManagedThreadId = 0;
			var registered = new ManualResetEventSlim(false);

			var thread = new Thread(() =>
			{
				ThreadStackDump.RegisterCurrentThread("<<< UI THREAD (message pump)");
				registeredManagedThreadId = Thread.CurrentThread.ManagedThreadId;
				registered.Set();
				blocked.Wait();
			})
			{
				Name = "PretendUiThread",
				IsBackground = true,
			};

			thread.Start();

			try
			{
				registered.Wait();

				string report = ThreadStackDump.Capture("proving registered threads are labelled");

				await Assert.That(report).Contains($"managed={registeredManagedThreadId} <<< UI THREAD (message pump)");
				await Assert.That(report).Contains("name=\"PretendUiThread\"");
			}
			finally
			{
				blocked.Set();
				thread.Join();
			}
		}

		/// <summary>
		/// The dump is taken of a process that is still running, so the thread list can change underneath it -
		/// on macOS that surfaces as createdump failing to read an exited thread's registers, or as ClrMD's mac
		/// core reader throwing "An item with the same key has already been added" on two thread contexts that
		/// resolve to one thread id. Both live in the captured file, so a re-capture is the only thing that can
		/// clear them, and this test is what says the second capture actually happens.
		/// </summary>
		[Test]
		public async Task ACaptureThatLosesTheDumpRaceIsRetakenNotReparsed()
		{
			int attempts = 0;
			int waits = 0;

			string report = ThreadStackDump.CaptureWithRetries(
				"proving a lost dump race is retried",
				() =>
				{
					if (++attempts == 1)
					{
						// The real shape of the failure this guards against.
						throw new ArgumentException("An item with the same key has already been added. Key: 624794");
					}

					return "second capture\n";
				},
				() => waits++);

			await Assert.That(attempts).IsEqualTo(2);
			await Assert.That(waits).IsEqualTo(1);
			await Assert.That(report).Contains("second capture");

			// The report has to admit the retry, or a run that took two tries reads as one that took none.
			await Assert.That(report).Contains($"attempt 1 of {ThreadStackDump.CaptureAttempts} failed and was re-taken");
			await Assert.That(report).Contains("Key: 624794");
		}

		/// <summary>
		/// The retry is bounded, and running out of attempts still throws - <see cref="ThreadStackDump.Capture"/>
		/// promises that, and <see cref="ThreadStackDump.WriteToConsole"/> is the layer that turns it into a note
		/// instead of replacing the failure it was called to explain.
		/// </summary>
		[Test]
		public async Task ACaptureThatNeverSucceedsGivesUpAndReportsEveryAttempt()
		{
			int attempts = 0;

			AggregateException thrown = null;

			try
			{
				ThreadStackDump.CaptureWithRetries(
					"proving the retry is bounded",
					() => throw new InvalidOperationException($"capture {++attempts} failed"),
					() => { });
			}
			catch (AggregateException ex)
			{
				thrown = ex;
			}

			await Assert.That(thrown).IsNotNull();
			await Assert.That(attempts).IsEqualTo(ThreadStackDump.CaptureAttempts);
			await Assert.That(thrown.InnerExceptions.Count).IsEqualTo(ThreadStackDump.CaptureAttempts);
			await Assert.That(thrown.Message).Contains("proving the retry is bounded");
			await Assert.That(thrown.Message).Contains($"capture {ThreadStackDump.CaptureAttempts} failed");
		}

		/// <summary>
		/// A dump cut off inside a thread command is left for ClrMD to judge: reading the stack pointers stops at
		/// the truncated command instead of throwing past the end of the file.
		/// </summary>
		[Test]
		public async Task ATruncatedThreadCommandIsNotRead()
		{
			using var stream = new MemoryStream();
			var writer = new BinaryWriter(stream);
			writer.Write(0xFEEDFACFu); // MH_MAGIC_64
			writer.Write(0x0100000Cu); // CPU_TYPE_ARM64
			writer.Write(0u); // cpusubtype
			writer.Write(4u); // filetype MH_CORE
			writer.Write(1u); // ncmds
			writer.Write(0u); // sizeofcmds
			writer.Write(0u); // flags
			writer.Write(0u); // reserved
			writer.Write(4u); // LC_THREAD
			writer.Write(400u); // cmdsize, running far past the end of the stream
			writer.Write(6u); // ARM_THREAD_STATE64
			writer.Write(68u); // count
			writer.Flush();

			var threads = ThreadStackDump.ReadThreadStackPointers(stream);

			await Assert.That(threads.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Two thread contexts on one stack pointer made ClrMD's mac core reader throw "An item with the same key
		/// has already been added" - and in one full suite run it did so on all five re-captures, so the retry
		/// alone could not save the report. This builds that dump on purpose, from a real one, and requires a
		/// report from it anyway.
		/// </summary>
		[Test]
		public async Task ADumpWithTwoThreadsOnOneStackPointerStillReports()
		{
			if (!OperatingSystem.IsMacOS())
			{
				// Only the mac reader keys contexts by stack pointer; other platforms' dumps are not Mach-O.
				return;
			}

			string dumpPath = Path.Combine(Path.GetTempPath(), $"agg-threadstacks-test-{Guid.NewGuid():N}.dmp");

			try
			{
				ThreadStackDump.WriteDumpOfThisProcess(dumpPath);

				// A live capture can already hold repeats of its own: createdump writes an all-zero register
				// context for a thread whose state it could not read, and two of those share stack pointer 0.
				// Seen in full serial suite runs (and ClrMD rejects such a dump untouched), so the report is
				// expected to drop those plus the one this test adds - an exact count of 1 was only true of
				// a quiet process.
				int naturalRepeats;
				using (var stream = new FileStream(dumpPath, FileMode.Open, FileAccess.ReadWrite))
				{
					var threads = ThreadStackDump.ReadThreadStackPointers(stream);
					await Assert.That(threads.Count).IsGreaterThan(3);

					// Make that capture state certain rather than luck: zero the last two threads' register
					// contexts the way createdump does, so every run has repeats before this test adds its own.
					var writer = new BinaryWriter(stream);
					ZeroRegisterState(stream, writer, threads[^1].CommandOffset);
					ZeroRegisterState(stream, writer, threads[^2].CommandOffset);
					writer.Flush();
					threads = ThreadStackDump.ReadThreadStackPointers(stream);

					naturalRepeats = threads.Count - threads.Select(t => t.StackPointer).Distinct().Count();
					await Assert.That(naturalRepeats).IsGreaterThanOrEqualTo(1);

					// Give a thread command the first one's stack pointer, in place. The target has to own a real
					// stack pointer no other context shares, so the rewrite adds exactly one repeat.
					var source = threads[0];
					var target = threads.Skip(1).First(t => t.StackPointer != 0
						&& threads.Count(other => other.StackPointer == t.StackPointer) == 1
						&& t.StackPointer != source.StackPointer);
					int targetIndex = threads.IndexOf(target);

					foreach (long position in StackPointerValuePositions(stream, target.CommandOffset, target.StackPointer))
					{
						stream.Position = position;
						writer.Write(source.StackPointer);
					}

					writer.Flush();

					// The production reader must now see the repeat, or the rest of this test proves nothing.
					var rewritten = ThreadStackDump.ReadThreadStackPointers(stream);
					await Assert.That(rewritten[targetIndex].StackPointer).IsEqualTo(source.StackPointer);
				}

				// The reproduction: unrepaired, ClrMD cannot open this dump at all.
				Exception unrepaired = null;
				try
				{
					DataTarget.LoadDump(dumpPath).Dispose();
				}
				catch (ArgumentException ex)
				{
					unrepaired = ex;
				}

				await Assert.That(unrepaired).IsNotNull();
				await Assert.That(unrepaired.Message).Contains("same key");

				string report = ThreadStackDump.ReportFromDump("proving a repeated stack pointer is survivable", dumpPath, 0, Stopwatch.StartNew());

				await Assert.That(report).Contains("END THREAD STACKS");
				await Assert.That(report).Contains($"({naturalRepeats + 1} thread register context(s) repeated an earlier thread's stack pointer");
				await Assert.That(report).Contains("--- thread os=");
			}
			finally
			{
				File.Delete(dumpPath);
			}
		}

		/// <summary>
		/// Overwrites the first register state of the thread command at <paramref name="commandOffset"/> with
		/// zeros, keeping its flavor and count - what createdump writes for a thread whose state it could not read.
		/// </summary>
		private static void ZeroRegisterState(FileStream stream, BinaryWriter writer, long commandOffset)
		{
			var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
			stream.Position = commandOffset + 12;
			uint countInWords = reader.ReadUInt32();

			stream.Position = commandOffset + 16;
			writer.Write(new byte[countInWords * 4]);
		}

		/// <summary>
		/// Every register slot inside the thread command at <paramref name="commandOffset"/> that holds the stack
		/// pointer's value - found by value so the test does not restate the production code's register layout.
		/// </summary>
		/// <remarks>
		/// All of them, not the first: another register can hold the same value (on ARM64 fp, before sp in the
		/// state, often equals sp), and rewriting only that one left sp unchanged. Rewriting the extra copies is
		/// harmless, as this thread's context is the one the repair drops.
		/// </remarks>
		private static List<long> StackPointerValuePositions(FileStream stream, long commandOffset, ulong stackPointer)
		{
			var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
			stream.Position = commandOffset + 4;
			uint commandSize = reader.ReadUInt32();

			var positions = new List<long>();
			for (long position = commandOffset + 16; position + 8 <= commandOffset + commandSize; position += 8)
			{
				stream.Position = position;
				if (reader.ReadUInt64() == stackPointer)
				{
					positions.Add(position);
				}
			}

			if (positions.Count == 0)
			{
				throw new InvalidOperationException("stack pointer not found in its own thread command");
			}

			return positions;
		}

		/// <summary>
		/// Exists only so the assertion above has a frame name to look for that no other code could produce.
		/// Inlining would erase it, which is the one thing that would make this test lie.
		/// </summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static string CaptureFromADistinctivelyNamedFrame()
		{
			return ThreadStackDump.Capture("proving the dump helper works");
		}
	}
}
