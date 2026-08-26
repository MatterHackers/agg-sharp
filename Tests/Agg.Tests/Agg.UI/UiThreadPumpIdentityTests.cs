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

using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Who <see cref="UiThread.IsUiThread"/> says the UI thread is. The pump owns that answer: everything
	/// that decides "run this here or marshal it" reads it, and a host whose own pump is told it is not the
	/// UI thread will marshal work to itself forever.
	/// </summary>
	/// <remarks>
	/// This is not hypothetical. A screenshot capture on the mac host queued itself onto the idle pump, and
	/// the queued work asked <see cref="UiThread.IsUiThread"/> again before capturing; with the id latched
	/// onto a test's worker thread the pump answered "no" and re-queued the request, over and over, until
	/// the caller's timeout expired with no file written.
	/// </remarks>
	// These tests reset UiThread's process wide statics and pump from their own threads, so nothing else may
	// be driving UiThread meanwhile - they share the key the windowed automation tests use.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class UiThreadPumpIdentityTests
	{
		[Test]
		[Timeout(30_000)]
		public async Task ThePumpClaimsTheUiThreadEvenAfterAnotherThreadDrainedTheQueue(CancellationToken cancellationToken)
		{
			// The automation harness resets this between tests, so "whoever drained first" is a race that
			// restarts on every test - which is exactly the state this reproduces.
			UiThread.ResetForTests();

			try
			{
				// A test helper draining the queue from a worker: legal, common, and enough to latch the id
				// onto a thread that is not the pump.
				RunOnItsOwnThread(() => UiThread.InvokePendingActions());

				int pumpThreadId = 0;
				bool pumpSeesItselfAsTheUiThread = false;

				RunOnItsOwnThread(() =>
				{
					// What every platform host calls from the thread that pumps the idle queue.
					MainLoopSynchronizationContext.InstallOnPumpThread();
					UiThread.InvokePendingActions();

					pumpThreadId = Thread.CurrentThread.ManagedThreadId;
					pumpSeesItselfAsTheUiThread = UiThread.IsUiThread;
				});

				await Assert.That(pumpSeesItselfAsTheUiThread).IsTrue()
					.Because("the thread that pumps the idle queue is the UI thread, whoever happened to drain the queue before it");

				// And it keeps it: a later drain from somewhere else does not move the answer, or the host
				// would lose its own identity again on the next test helper that pumps.
				bool workerClaimedIt = false;
				RunOnItsOwnThread(() =>
				{
					UiThread.InvokePendingActions();
					workerClaimedIt = UiThread.IsUiThread;
				});

				await Assert.That(workerClaimedIt).IsFalse()
					.Because($"the pump (thread {pumpThreadId}) has declared itself; a worker draining the queue afterwards is not the UI thread");
			}
			finally
			{
				// The threads above are gone; leave no id latched onto a dead one.
				UiThread.ResetForTests();
			}
		}

		private static void RunOnItsOwnThread(ThreadStart work)
		{
			var thread = new Thread(work)
			{
				IsBackground = true,
				Name = "UiThread identity test thread",
			};

			thread.Start();
			thread.Join();
		}
	}
}
