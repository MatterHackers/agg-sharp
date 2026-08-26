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
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins the contract of <see cref="UiThread.SwitchToUiThreadAsync"/>: awaiting it puts the rest of the
	/// method on the main loop, costs nothing when it is already there, and leaves failures on the caller's
	/// task rather than on the pump.
	/// </summary>
	// These tests stand up their own pump thread and reset UiThread's process wide statics, so nothing else
	// may be driving UiThread meanwhile - they share the key the windowed automation tests use.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class SwitchToUiThreadTests
	{
		[Test]
		[Timeout(30_000)]
		public async Task SwitchFromAPoolThreadResumesOnThePumpThread(CancellationToken cancellationToken)
		{
			using var pump = new UiThreadTestPump();

			int startedOnThread = 0;
			int afterSwitchThread = 0;

			// The shape every converted site has: work that began on a pool task (Tasks.Execute -> Task.Run,
			// so no context was captured) and has to touch widgets when it is done.
			await Task.Run(async () =>
			{
				startedOnThread = Environment.CurrentManagedThreadId;

				await UiThread.SwitchToUiThreadAsync();

				afterSwitchThread = Environment.CurrentManagedThreadId;
			});

			await Assert.That(startedOnThread).IsNotEqualTo(pump.ThreadId)
				.Because("the work has to start off the loop or this proves nothing");

			await Assert.That(afterSwitchThread).IsEqualTo(pump.ThreadId)
				.Because("everything after the switch runs on the thread pumping the ui queue");
		}

		[Test]
		[Timeout(30_000)]
		public async Task SwitchOnTheUiThreadCompletesWithoutWaitingForAPump(CancellationToken cancellationToken)
		{
			using var pump = new UiThreadTestPump();

			bool completedWithoutPumping = false;
			int afterSwitchThread = 0;

			await pump.RunOnPump(() =>
			{
				async Task SwitchAsync()
				{
					await UiThread.SwitchToUiThreadAsync();
					afterSwitchThread = Environment.CurrentManagedThreadId;
				}

				var switching = SwitchAsync();

				// Nothing can drain the queue while this delegate runs - the pump thread is inside it - so a
				// completed task here means the await never left the thread.
				completedWithoutPumping = switching.IsCompleted;

				return switching;
			});

			await Assert.That(completedWithoutPumping).IsTrue()
				.Because("already on the ui thread, the switch must continue inline rather than cost a pump hop");

			await Assert.That(afterSwitchThread).IsEqualTo(pump.ThreadId);
		}

		[Test]
		[Timeout(30_000)]
		public async Task AFailureAfterTheSwitchFailsTheCallersTask(CancellationToken cancellationToken)
		{
			using var pump = new UiThreadTestPump();

			// The point of the awaitable over RunOnIdle: the continuation still belongs to this task, so a
			// throw is the caller's to catch instead of an async void escaping onto the pump.
			var failing = Task.Run(async () =>
			{
				await UiThread.SwitchToUiThreadAsync();

				throw new InvalidOperationException("the work after the switch failed");
			});

			var thrown = await Assert.That(async () => await failing).Throws<InvalidOperationException>();

			await Assert.That(thrown.Message).IsEqualTo("the work after the switch failed");

			// The pump survived it - had the exception been raised on the queue instead, the loop would be
			// gone and this could not run at all.
			bool pumpStillRuns = false;
			await pump.RunOnPump(() =>
			{
				pumpStillRuns = true;
				return Task.CompletedTask;
			});

			await Assert.That(pumpStillRuns).IsTrue();
		}

		[Test]
		[Timeout(30_000)]
		public async Task LaterAwaitsAfterTheSwitchAlsoResumeOnThePumpThread(CancellationToken cancellationToken)
		{
			using var pump = new UiThreadTestPump();

			int afterSwitchThread = 0;
			int afterDelayThread = 0;
			int afterYieldThread = 0;

			await Task.Run(async () =>
			{
				await UiThread.SwitchToUiThreadAsync();
				afterSwitchThread = Environment.CurrentManagedThreadId;

				// Once the switch lands, MainLoopSynchronizationContext is the current context, so ordinary
				// awaits from here on come back to the loop by themselves - the whole point of switching once
				// at the top rather than marshalling every ui touch.
				await Task.Delay(10);
				afterDelayThread = Environment.CurrentManagedThreadId;

				await Task.Yield();
				afterYieldThread = Environment.CurrentManagedThreadId;
			});

			await Assert.That(afterSwitchThread).IsEqualTo(pump.ThreadId);
			await Assert.That(afterDelayThread).IsEqualTo(pump.ThreadId);
			await Assert.That(afterYieldThread).IsEqualTo(pump.ThreadId);
		}

		[Test]
		[Timeout(30_000)]
		public async Task SwitchWithNothingPumpingNeverResumes(CancellationToken cancellationToken)
		{
			// The documented hazard: the continuation is queued on the idle queue, so a host that never pumps
			// it (a headless test with no window) leaves the caller parked forever. Code that can run headless
			// has to keep its own guard rather than rely on the switch.
			UiThread.ResetForTests();

			try
			{
				async Task SwitchAsync()
				{
					await UiThread.SwitchToUiThreadAsync();
				}

				// ResetForTests unlatched the ui thread id and nothing has pumped since, so IsUiThread is
				// false here - exactly the "no main loop" case.
				var switching = SwitchAsync();

				await Assert.That(await Task.WhenAny(switching, Task.Delay(250))).IsNotSameReferenceAs(switching)
					.Because("with nothing draining the queue the continuation can never run");

				// Pumping by hand is all it takes to release it, which is what makes this a hazard of the host
				// rather than of the primitive.
				UiThread.InvokePendingActions();

				await Assert.That(await Task.WhenAny(switching, Task.Delay(TimeSpan.FromSeconds(10)))).IsSameReferenceAs(switching)
					.Because("one drain of the queue resumes it");
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}
	}
}
