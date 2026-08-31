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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
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
