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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins the contract of <see cref="MainLoopSynchronizationContext"/>: await resumes on the main loop,
	/// Post lands in the one existing UiThread queue, and Send is inline on the loop / bounded off it.
	/// </summary>
	// These tests stand up their own pump thread and reset UiThread's process wide statics, so nothing else
	// may be driving UiThread meanwhile - they share the key the windowed automation tests use.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class MainLoopSynchronizationContextTests
	{
		/// <summary>
		/// A stand in for a platform host's idle pump: one thread that installs the context and then calls
		/// InvokePendingActions in a loop, exactly the way WinformsSystemWindow and AutomationRunner do.
		/// </summary>
		private sealed class TestPump : IDisposable
		{
			private readonly Thread thread;
			private readonly ManualResetEventSlim ready = new ManualResetEventSlim(false);
			private volatile bool stopRequested;

			public TestPump()
			{
				// Hand ourselves a clean queue and an unlatched ui thread id so the pump thread below becomes
				// UiThread's ui thread.
				UiThread.ResetForTests();

				thread = new Thread(() =>
				{
					MainLoopSynchronizationContext.InstallOnPumpThread();

					// Latches UiThread.IsUiThread onto this thread before anyone can observe it.
					UiThread.InvokePendingActions();

					ThreadId = Environment.CurrentManagedThreadId;
					ready.Set();

					while (!stopRequested)
					{
						UiThread.InvokePendingActions();
						Thread.Sleep(1);
					}

					UiThread.InvokePendingActions();
				})
				{
					IsBackground = true,
					Name = "MainLoopSynchronizationContext test pump"
				};

				thread.Start();
				ready.Wait();
			}

			public int ThreadId { get; private set; }

			/// <summary>
			/// Runs <paramref name="work"/> on the pump thread under the installed context and completes when
			/// the whole async chain - including every continuation - has finished.
			/// </summary>
			public Task RunOnPump(Func<Task> work)
			{
				var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

				UiThread.RunOnIdle(async () =>
				{
					try
					{
						await work();
						completion.TrySetResult(true);
					}
					catch (Exception workException)
					{
						completion.TrySetException(workException);
					}
				});

				return completion.Task;
			}

			public void Dispose()
			{
				stopRequested = true;

				// Bounded: a pump thread that will not come back is a real failure, and an unbounded Join
				// would report it as a hung test run rather than as itself.
				bool stopped = thread.Join(TimeSpan.FromSeconds(10));

				// The pump thread is gone; leave no latched ui thread id or queued work behind.
				UiThread.ResetForTests();
				ready.Dispose();

				if (!stopped)
				{
					throw new TimeoutException(
						"The test pump thread did not stop within 10s - queued work is still blocking it.");
				}
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task AwaitResumesOnThePumpThread(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			int resumedOnThread = 0;
			int startedOnThread = 0;

			await pump.RunOnPump(async () =>
			{
				startedOnThread = Environment.CurrentManagedThreadId;
				await Task.Delay(10);
				resumedOnThread = Environment.CurrentManagedThreadId;
			});

			await Assert.That(startedOnThread).IsEqualTo(pump.ThreadId)
				.Because("the work is started by the pump");

			await Assert.That(resumedOnThread).IsEqualTo(pump.ThreadId)
				.Because("with the context installed, an await resumes on the main loop rather than the thread pool");
		}

		[Test]
		[Timeout(30_000)]
		public async Task PostFromBackgroundThreadRunsOnThePump(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			int ranOnThread = 0;
			object observedState = null;
			var ran = new ManualResetEventSlim(false);
			var marker = new object();

			await Task.Run(() =>
			{
				MainLoopSynchronizationContext.Instance.Post(
					state =>
					{
						ranOnThread = Environment.CurrentManagedThreadId;
						observedState = state;
						ran.Set();
					},
					marker);
			});

			await Assert.That(ran.Wait(TimeSpan.FromSeconds(10))).IsTrue()
				.Because("Post must land in the UiThread queue the pump drains");

			await Assert.That(ranOnThread).IsEqualTo(pump.ThreadId);
			await Assert.That(observedState).IsSameReferenceAs(marker);
		}

		[Test]
		[Timeout(30_000)]
		public async Task SendFromThePumpThreadRunsInline(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			bool ranBeforeSendReturned = false;
			int ranOnThread = 0;

			await pump.RunOnPump(() =>
			{
				bool ran = false;

				MainLoopSynchronizationContext.Instance.Send(
					_ =>
					{
						ran = true;
						ranOnThread = Environment.CurrentManagedThreadId;
					},
					null);

				ranBeforeSendReturned = ran;

				return Task.CompletedTask;
			});

			await Assert.That(ranBeforeSendReturned).IsTrue()
				.Because("Send on the main loop thread must run inline, not queue - queuing would deadlock the loop");

			await Assert.That(ranOnThread).IsEqualTo(pump.ThreadId);
		}

		[Test]
		[Timeout(30_000)]
		public async Task SendFromBackgroundThreadCompletesOnThePumpAndReportsTheLegacyShape(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			var diagnostics = new List<string>();
			void CollectDiagnostic(string message)
			{
				lock (diagnostics)
				{
					diagnostics.Add(message);
				}
			}

			MainLoopSynchronizationContext.BlockingSendObserved += CollectDiagnostic;
			try
			{
				int ranOnThread = 0;

				await Task.Run(() =>
				{
					MainLoopSynchronizationContext.Instance.Send(
						_ => ranOnThread = Environment.CurrentManagedThreadId,
						null);
				});

				await Assert.That(ranOnThread).IsEqualTo(pump.ThreadId)
					.Because("a Send from off the loop still has to run its work on the loop");

				await Assert.That(diagnostics.Count).IsEqualTo(1);
				await Assert.That(diagnostics[0]).Contains(nameof(MainLoopSynchronizationContext))
					.Because("the diagnostic has to name the context so the blocking marshal is attributable");
			}
			finally
			{
				MainLoopSynchronizationContext.BlockingSendObserved -= CollectDiagnostic;
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task SendFromBackgroundThreadThrowsWhenNothingEverPumps(CancellationToken cancellationToken)
		{
			// No pump at all: the queued work can never run, so the bounded wait has to give up loudly
			// rather than park the caller forever.
			UiThread.ResetForTests();

			var previousTimeout = MainLoopSynchronizationContext.SendFromOtherThreadTimeout;
			MainLoopSynchronizationContext.SendFromOtherThreadTimeout = TimeSpan.FromMilliseconds(250);

			try
			{
				// UiThread.IsUiThread is false here because ResetForTests unlatched the id and nothing has
				// pumped since, which is exactly the "no main loop" case.
				var thrown = await Assert.That(
					() => MainLoopSynchronizationContext.Instance.Send(_ => { }, null))
					.Throws<TimeoutException>();

				await Assert.That(thrown.Message).Contains(nameof(MainLoopSynchronizationContext));
			}
			finally
			{
				MainLoopSynchronizationContext.SendFromOtherThreadTimeout = previousTimeout;
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task PostAndRunOnIdleShareOneFifoQueue(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			var order = new List<string>();
			var done = new ManualResetEventSlim(false);

			await pump.RunOnPump(() =>
			{
				// Queued from the pump thread, mid pump. Everything queued here - RunOnIdle or Post - runs
				// on the NEXT pump, in the order it was queued: one queue, one FIFO, no priority.
				UiThread.RunOnIdle(() => order.Add("idle 1"));
				MainLoopSynchronizationContext.Instance.Post(_ => order.Add("post 1"), null);
				UiThread.RunOnIdle(() => order.Add("idle 2"));
				MainLoopSynchronizationContext.Instance.Post(
					_ =>
					{
						order.Add("post 2");
						done.Set();
					},
					null);

				// Nothing has run yet - this delegate is still the one the pump is running.
				order.Add("queued them all");

				return Task.CompletedTask;
			});

			await Assert.That(done.Wait(TimeSpan.FromSeconds(10))).IsTrue();

			await Assert.That(order).IsEquivalentTo(new List<string>
			{
				"queued them all",
				"idle 1",
				"post 1",
				"idle 2",
				"post 2"
			});
		}

		/// <summary>
		/// The shape <see cref="UiThread.DrainForNestedPump"/> exists for: a loop that is itself running
		/// inside a pumped action and cannot finish until a suspended await resumes - which, with this
		/// context installed, can only happen when something drains the queue. That is exactly the Mac and
		/// X11 CaptureScreenshot spin, whose ordinary guarded drain is a no-op while an idle action is on
		/// the stack.
		/// </summary>
		[Test]
		[Timeout(30_000)]
		public async Task NestedDrainAdvancesAnAwaitThatOnlyTheQueueCanComplete(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			bool captureFinished = false;
			int continuationThread = 0;
			bool finishedInsideTheNestedLoop = false;

			await pump.RunOnPump(() =>
			{
				// Stands in for CaptureThenPresent: suspends on work only a later pump can complete.
				var frameReadback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

				async Task CaptureAsync()
				{
					await frameReadback.Task;
					continuationThread = Environment.CurrentManagedThreadId;
					captureFinished = true;
				}

				_ = CaptureAsync();

				// The readback signal arrives through the queue, the way an idle driven frame delivers it.
				UiThread.RunOnIdle(() => frameReadback.TrySetResult());

				// The capture spin. Nothing else can drain the queue while this runs: the pump thread is
				// inside this very delegate.
				for (int spin = 0; spin < 200 && !captureFinished; spin++)
				{
					UiThread.DrainForNestedPump();
					Thread.Sleep(1);
				}

				finishedInsideTheNestedLoop = captureFinished;

				return Task.CompletedTask;
			});

			await Assert.That(finishedInsideTheNestedLoop).IsTrue()
				.Because("a nested drain is the only thing that can run the continuation the loop is waiting for");

			await Assert.That(continuationThread).IsEqualTo(pump.ThreadId)
				.Because("draining from the pump thread must still resume the continuation on the main loop");
		}

		[Test]
		[Timeout(30_000)]
		public async Task InstallForScopeInstallsAndRestores(CancellationToken cancellationToken)
		{
			// No awaits inside the scope, deliberately: with the context installed and no pump on this
			// thread, an await in here would post its continuation to a queue nobody drains.
			var before = SynchronizationContext.Current;
			SynchronizationContext insideScope;
			SynchronizationContext insideNestedScope;
			SynchronizationContext afterNestedScope;

			using (MainLoopSynchronizationContext.InstallForScope())
			{
				insideScope = SynchronizationContext.Current;

				using (MainLoopSynchronizationContext.InstallForScope())
				{
					insideNestedScope = SynchronizationContext.Current;
				}

				afterNestedScope = SynchronizationContext.Current;
			}

			var afterScope = SynchronizationContext.Current;

			await Assert.That(insideScope).IsSameReferenceAs(MainLoopSynchronizationContext.Instance);
			await Assert.That(insideNestedScope).IsSameReferenceAs(MainLoopSynchronizationContext.Instance);

			await Assert.That(afterNestedScope).IsSameReferenceAs(MainLoopSynchronizationContext.Instance)
				.Because("a nested scope restores what IT found, which is the outer scope's context");

			await Assert.That(afterScope).IsSameReferenceAs(before)
				.Because("a borrowed thread must be handed back exactly as it was found");
		}

		[Test]
		[Timeout(30_000)]
		public async Task InstallForScopeRestoresWhenTheScopeBodyThrows(CancellationToken cancellationToken)
		{
			var before = SynchronizationContext.Current;

			try
			{
				using (MainLoopSynchronizationContext.InstallForScope())
				{
					throw new InvalidOperationException("the scope body failed");
				}
			}
			catch (InvalidOperationException)
			{
			}

			await Assert.That(SynchronizationContext.Current).IsSameReferenceAs(before)
				.Because("a test that fails must not leave the context latched on the harness thread");
		}

		[Test]
		[Timeout(30_000)]
		public async Task CreateCopyReturnsTheOneInstance(CancellationToken cancellationToken)
		{
			// The framework copies the current context freely (every ExecutionContext capture may). There is
			// one main loop, so a copy that was a different object would just be a second name for it.
			await Assert.That(MainLoopSynchronizationContext.Instance.CreateCopy())
				.IsSameReferenceAs(MainLoopSynchronizationContext.Instance);
		}

		[Test]
		[Timeout(30_000)]
		public async Task PostRejectsANullCallback(CancellationToken cancellationToken)
		{
			// The BCL contract, and the useful one: failing here names the caller, where failing on the pump
			// a tick later would name nobody.
			await Assert.That(() => MainLoopSynchronizationContext.Instance.Post(null, null))
				.Throws<ArgumentNullException>();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ThrowFromSentWork()
		{
			throw new InvalidOperationException("the sent work failed");
		}

		[Test]
		[Timeout(30_000)]
		public async Task SendFromBackgroundThreadRethrowsWithTheOriginalStack(CancellationToken cancellationToken)
		{
			using var pump = new TestPump();

			Exception caught = null;

			await Task.Run(() =>
			{
				try
				{
					MainLoopSynchronizationContext.Instance.Send(_ => ThrowFromSentWork(), null);
				}
				catch (Exception sendException)
				{
					caught = sendException;
				}
			});

			await Assert.That(caught).IsTypeOf<InvalidOperationException>()
				.Because("work that failed on the loop has to fail the Send that asked for it");

			await Assert.That(caught.StackTrace).Contains(nameof(ThrowFromSentWork))
				.Because("the rethrow must preserve the frame that actually threw, not start a new stack here");
		}

		[Test]
		[Timeout(30_000)]
		public async Task SendThatTimedOutDoesNotRunItsWorkLater(CancellationToken cancellationToken)
		{
			// Nothing pumps, so the Send gives up; then the queue is drained by hand to stand in for a loop
			// that recovers afterwards. The abandoned work must not run: the caller has already thrown and
			// may well have retried, and a late second application would be silent corruption.
			UiThread.ResetForTests();

			var previousTimeout = MainLoopSynchronizationContext.SendFromOtherThreadTimeout;
			MainLoopSynchronizationContext.SendFromOtherThreadTimeout = TimeSpan.FromMilliseconds(250);

			try
			{
				bool ran = false;

				await Assert.That(
					() => MainLoopSynchronizationContext.Instance.Send(_ => ran = true, null))
					.Throws<TimeoutException>();

				UiThread.InvokePendingActions();

				await Assert.That(ran).IsFalse()
					.Because("a timed out Send's work is abandoned, not merely late");
			}
			finally
			{
				MainLoopSynchronizationContext.SendFromOtherThreadTimeout = previousTimeout;
				UiThread.ResetForTests();
			}
		}
	}
}
