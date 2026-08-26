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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// What a widget that throws while drawing may cost. One frame, and a report - never the window's
	/// event loop.
	/// </summary>
	/// <remarks>
	/// A draw handler in MatterCAD threw a NullReferenceException (a progress overlay whose AfterDraw was
	/// subscribed before the bar it draws existed). The throw unwound the host's paint and, with it, the
	/// loop pumping that window: the window stayed on screen with nothing servicing it, the main thread
	/// never returned to run marshalled work, and every later test in that process blocked or timed out
	/// waiting for an application gate that could not be released. One bad frame cost a whole test shard
	/// fourteen minutes and three failures. The draw bug is worth fixing where it lives; the loop surviving
	/// it is worth pinning here, because the next such bug will be somebody else's.
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PaintExceptionContainmentTests
	{
		[Test]
		[Timeout(60_000)]
		public async Task AThrowingDrawIsReportedAndTheEventLoopKeepsRunning(CancellationToken cancellationToken)
		{
			var reported = new List<Exception>();
			void CollectReport(Exception reportedException)
			{
				lock (reported)
				{
					reported.Add(reportedException);
				}
			}

			var window = new SystemWindow(300, 200) { BackgroundColor = Color.White };
			var thrower = new ThrowOnDrawWidget();
			window.AddChild(thrower);

			UiThread.UnhandledException += CollectReport;

			// Shown from a worker, the way every windowed test does it: ShowAsSystemWindow blocks for the
			// window's lifetime, and on macOS the loop it starts runs on the process main thread.
			var shown = Task.Run(() => window.ShowAsSystemWindow(), cancellationToken);

			try
			{
				// Wait for the throw to have happened, not for a length of time.
				await WaitFor(() => thrower.ThrowCount > 0, TimeSpan.FromSeconds(20), cancellationToken);

				await Assert.That(thrower.ThrowCount).IsGreaterThan(0)
					.Because("the widget under test has to have been drawn, or nothing has been proved");

				// The loop is alive if it still runs queued work. This is the part the failure took away:
				// with the paint unwinding the loop, nothing ever drained the idle queue again.
				bool idleRan = false;
				UiThread.RunOnIdle(() => idleRan = true);

				await WaitFor(() => idleRan, TimeSpan.FromSeconds(20), cancellationToken);

				await Assert.That(idleRan).IsTrue()
					.Because("a paint that threw must leave the window's event loop pumping - queued work still has to run");

				await Assert.That(reported.Count).IsGreaterThan(0)
					.Because("the exception has to be reported, not swallowed: it is what fails the test that caused it");
			}
			finally
			{
				// And the window still closes - a loop that survived the throw is the only thing that can
				// service this.
				window.CloseOnIdle();

				var closed = await Task.WhenAny(shown, Task.Delay(TimeSpan.FromSeconds(20), cancellationToken));

				UiThread.UnhandledException -= CollectReport;

				await Assert.That(ReferenceEquals(closed, shown)).IsTrue()
					.Because("the window has to close after a throwing paint; a dead loop leaves it on screen forever");
			}
		}

		/// <summary>Waits for a condition, polling off the UI thread; throws nothing, the assert does that.</summary>
		private static async Task WaitFor(Func<bool> condition, TimeSpan limit, CancellationToken cancellationToken)
		{
			var watch = Stopwatch.StartNew();
			while (!condition() && watch.Elapsed < limit)
			{
				await Task.Delay(10, cancellationToken);
			}
		}

		/// <summary>A widget whose draw always throws - the shape of the bug this pins.</summary>
		private class ThrowOnDrawWidget : GuiWidget
		{
			public ThrowOnDrawWidget()
			{
				this.Name = "throwOnDraw";
				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Stretch;
			}

			/// <summary>How many times this widget has thrown out of a draw.</summary>
			public int ThrowCount => Volatile.Read(ref this.throwCount);

			private int throwCount;

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);

				Interlocked.Increment(ref this.throwCount);

				throw new InvalidOperationException("A widget threw while drawing, deliberately (PaintExceptionContainmentTests).");
			}
		}
	}
}
