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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// What one <c>requestAnimationFrame</c> tick of the browser host does, and in what order. There is no way
	/// to run an animation frame in a test process, which is exactly why
	/// <see cref="BrowserFrameTick"/> is a class over delegates rather than a private method on the window:
	/// the ordering, the UI-thread identity claim, the re-entrancy guard and the "one bad frame" containment
	/// all run here, on every desktop OS, with no browser anywhere.
	/// </summary>
	// These tests drive UiThread's process wide statics from their own threads, so nothing else may be
	// driving UiThread meanwhile - they share the key the windowed automation tests use.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class BrowserFrameTickTests
	{
		[Test]
		[Timeout(30_000)]
		public async Task BrowserEventsAreDeliveredBeforeIdleActions(CancellationToken cancellationToken)
		{
			UiThread.ResetForTests();

			try
			{
				var order = new List<string>();

				var tick = new BrowserFrameTick(
					drainBrowserEvents: () => order.Add("browser events"),
					canPaint: () => false,
					paintFrame: () => order.Add("paint"));

				// The reason the order matters: this is the shape of every "the click did something" path -
				// a widget handles the mouse down and queues the work it triggered. Draining idle first would
				// run that work a frame before the click that asked for it.
				UiThread.RunOnIdle(() => order.Add("idle action"));

				tick.Tick();

				await Assert.That(order).IsEquivalentTo(new[] { "browser events", "idle action" });
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task EachDrainClaimsTheTickingThreadAsTheUiThread(CancellationToken cancellationToken)
		{
			UiThread.ResetForTests();

			try
			{
				// A test helper draining the queue from somewhere else: legal, common, and enough to latch the
				// id onto a thread that is not the one that ticks. See UiThread.MarkCurrentThreadAsUiThread for
				// what a host that loses its own identity then does to itself.
				RunOnItsOwnThread(() => UiThread.InvokePendingActions());

				bool queuedActionSawTheUiThread = false;
				UiThread.RunOnIdle(() => queuedActionSawTheUiThread = UiThread.IsUiThread);

				var tick = new BrowserFrameTick(() => { }, () => false, () => { });

				RunOnItsOwnThread(tick.Tick);

				await Assert.That(queuedActionSawTheUiThread).IsTrue()
					.Because("the thread that ticks is the UI thread, whoever happened to drain the queue before it");
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task TheTickDoesNotInstallTheMainLoopSynchronizationContext(CancellationToken cancellationToken)
		{
			UiThread.ResetForTests();

			try
			{
				var tick = new BrowserFrameTick(() => { }, () => false, () => { });

				SynchronizationContext contextAfterTick = null;

				// On its own thread so the answer is about what the tick did, not about what some earlier test
				// left installed on a pooled one.
				RunOnItsOwnThread(() =>
				{
					tick.Tick();
					contextAfterTick = SynchronizationContext.Current;
				});

				await Assert.That(contextAfterTick is MainLoopSynchronizationContext).IsFalse()
					.Because("installing it would post every await continuation through the idle queue, so a chain "
						+ "of N suspending awaits would take N animation frames to unwind - and wasm already "
						+ "resumes continuations on this one thread without it");
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task AFramePaintsOnlyWhenItWasAskedForAndThereIsSomethingToPaintInto()
		{
			UiThread.ResetForTests();

			try
			{
				int paints = 0;
				bool canPaint = true;

				var tick = new BrowserFrameTick(() => { }, () => canPaint, () => paints++);

				// A window that has been shown but never invalidated still owes the page its first frame.
				tick.Tick();
				await Assert.That(paints).IsEqualTo(1);

				// Nothing asked for another one, and a browser tick is not a reason to redraw - the loop runs
				// sixty times a second whether or not anything changed.
				tick.Tick();
				await Assert.That(paints).IsEqualTo(1);

				// Asked for while nothing can paint - the state a host is in before its render device has
				// finished coming up. The request is still owed rather than dropped.
				canPaint = false;
				tick.Invalidate();
				tick.Tick();
				await Assert.That(paints).IsEqualTo(1);
				await Assert.That(tick.NeedsRedraw).IsTrue();

				canPaint = true;
				tick.Tick();
				await Assert.That(paints).IsEqualTo(2);
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task AThrowingPaintCostsOneFrameAndIsReported()
		{
			UiThread.ResetForTests();

			var reported = new List<Exception>();
			void Report(Exception exception) => reported.Add(exception);

			UiThread.UnhandledException += Report;

			try
			{
				int paintAttempts = 0;

				var tick = new BrowserFrameTick(
					() => { },
					() => true,
					() =>
					{
						paintAttempts++;
						throw new InvalidOperationException("a widget's draw threw");
					});

				tick.Tick();

				await Assert.That(paintAttempts).IsEqualTo(1);
				await Assert.That(reported.Count).IsEqualTo(1)
					.Because("this is the channel the automation harness listens on, so the test whose draw threw "
						+ "still fails - it just fails alone");

				// Cleared BEFORE the paint, so a draw that throws every time does not re-arm itself: without
				// this the loop would fail sixty times a second and bury the first report.
				await Assert.That(tick.NeedsRedraw).IsFalse();

				// And the loop is still running, which is the half the spike's frame loop gave up on: a dead
				// loop leaves a window on screen with no input, no idle queue and no way to close.
				tick.Tick();
				await Assert.That(tick.TickCount).IsEqualTo(2L);
				await Assert.That(paintAttempts).IsEqualTo(1);

				// It repeats only as often as something asks for a repaint.
				tick.Invalidate();
				tick.Tick();
				await Assert.That(paintAttempts).IsEqualTo(2);
				await Assert.That(reported.Count).IsEqualTo(2);
			}
			finally
			{
				UiThread.UnhandledException -= Report;
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task AThrowingEventDrainDoesNotCostTheRestOfTheTick()
		{
			UiThread.ResetForTests();

			var reported = new List<Exception>();
			void Report(Exception exception) => reported.Add(exception);

			UiThread.UnhandledException += Report;

			try
			{
				bool idleRan = false;
				int paints = 0;

				var tick = new BrowserFrameTick(
					() => throw new InvalidOperationException("an unreadable event"),
					() => true,
					() => paints++);

				UiThread.RunOnIdle(() => idleRan = true);

				tick.Tick();

				await Assert.That(reported.Count).IsEqualTo(1);
				await Assert.That(idleRan).IsTrue();
				await Assert.That(paints).IsEqualTo(1);
			}
			finally
			{
				UiThread.UnhandledException -= Report;
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task ANestedTickDoesNotReEnterTheIdleDrain()
		{
			UiThread.ResetForTests();

			try
			{
				var order = new List<string>();

				BrowserFrameTick tick = null;
				tick = new BrowserFrameTick(() => order.Add("browser events"), () => false, () => { });

				// What a modal dialog does: an idle action that runs a loop of its own, which ticks this window
				// again from underneath the drain it is already inside.
				UiThread.RunOnIdle(() =>
				{
					order.Add("outer action");
					UiThread.RunOnIdle(() => order.Add("action queued by the outer one"));

					tick.Tick();
				});

				tick.Tick();

				await Assert.That(order).IsEquivalentTo(new[]
				{
					"browser events",
					"outer action",
					"browser events",
				})
					.Because("the nested tick still delivers browser events - the dialog has to stay responsive - "
						+ "but its idle drain is guarded, exactly as MacSystemWindow.InvokeIdleActions is");

				// The guarded drain did not lose the queued action; it is simply owed to a later tick.
				tick.Tick();
				await Assert.That(order).Contains("action queued by the outer one");
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task APhaseThatBlocksTheOnlyThreadIsCalledOutOnce()
		{
			UiThread.ResetForTests();

			var output = new StringWriter();
			var errors = new StringWriter();
			TextWriter previousOut = Console.Out;
			TextWriter previousError = Console.Error;
			Console.SetOut(output);
			Console.SetError(errors);

			try
			{
				// The R3 mitigation: a browser has one thread, so a phase that blocks is not slow - it is the
				// whole application stopped. Half a second is well past any honest frame and short enough that
				// a blocking wait cannot hide under it.
				var tick = new BrowserFrameTick(
					drainBrowserEvents: () => Thread.Sleep(600),
					canPaint: () => false,
					paintFrame: () => { });

				tick.Tick();
				tick.Tick();

				string reported = output.ToString();

				await Assert.That(reported).Contains("browser events")
					.Because("the line has to name the phase, or it says only that something was slow");
				await Assert.That(reported).Contains("held the only thread");

				await Assert.That(reported.Split("held the only thread").Length - 1).IsEqualTo(1)
					.Because("a phase that is slow every frame would otherwise bury everything else in the console");

				// The bug this pins: Blazor wires the runtime's stderr straight to its
				// "An unhandled error has occurred" strip, so a slow-frame notice written to Console.Error
				// tells a browser user the application broke - while nothing threw and the work completed.
				// Dropping a Text primitive onto the bed takes about a second of glyph work on the one
				// browser thread, which is exactly how a user met that strip.
				await Assert.That(errors.ToString()).IsEmpty()
					.Because("a slow phase is a diagnostic, not a failure, and stderr is the browser host's fatal channel");
			}
			finally
			{
				Console.SetOut(previousOut);
				Console.SetError(previousError);
				UiThread.ResetForTests();
			}
		}

		private static void RunOnItsOwnThread(ThreadStart work)
		{
			var thread = new Thread(work)
			{
				IsBackground = true,
				Name = "Browser frame tick test thread",
			};

			thread.Start();
			thread.Join();
		}
	}
}
