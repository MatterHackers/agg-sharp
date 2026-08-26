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
				thread.Join();

				// The pump thread is gone; leave no latched ui thread id or queued work behind.
				UiThread.ResetForTests();
				ready.Dispose();
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
	}
}
