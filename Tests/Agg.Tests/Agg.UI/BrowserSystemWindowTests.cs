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
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The browser host with the browser taken out: <see cref="BrowserSystemWindow"/> reaches the DOM only
	/// through <see cref="IBrowserWindowInterop"/> and <see cref="IBrowserFrameLoop"/>, so its sizing, its
	/// input queue and its screenshot contract can all be driven from a desktop test with two fakes.
	/// </summary>
	// Shows an agg window and drives UiThread through the tick, so it takes the same key the windowed
	// automation tests use.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class BrowserSystemWindowTests
	{
		[Test]
		[Timeout(30_000)]
		public async Task ShowingAWindowSizesAggFromTheCanvasAndStartsTheLoopWithoutBlocking()
		{
			var interop = new FakeWindowInterop { BindResult = new BrowserBackingSize(800, 600, 2) };
			var frameLoop = new FakeFrameLoop();

			var platformWindow = new BrowserSystemWindow(interop, frameLoop);
			var systemWindow = new SystemWindow(100, 100) { Title = "agg in a page" };

			try
			{
				platformWindow.ShowSystemWindow(systemWindow);

				// Reaching this line at all is the contract that separates this host from every other one:
				// ShowSystemWindow starts the animation frame loop and RETURNS, because Blazor's RunAsync keeps
				// the runtime alive and blocking the one thread would stop the very frames it was shown to draw.
				await Assert.That(frameLoop.StartCount).IsEqualTo(1);
				await Assert.That(frameLoop.IsRunning).IsTrue();

				await Assert.That(interop.AttachCount).IsEqualTo(1);
				await Assert.That(interop.FocusCount).IsEqualTo(1);
				await Assert.That(interop.Title).IsEqualTo("agg in a page");

				// The canvas's backing store is agg's coordinate space, so the window is laid out in device
				// pixels and told the scale they are at.
				await Assert.That(platformWindow.Backing).IsEqualTo(new BrowserBackingSize(800, 600, 2));
				await Assert.That(systemWindow.Width).IsEqualTo(800.0);
				await Assert.That(systemWindow.Height).IsEqualTo(600.0);
				await Assert.That(systemWindow.DisplayScale).IsEqualTo(2.0);
			}
			finally
			{
				platformWindow.CloseSystemWindow(systemWindow);
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task AResizeQueuedByJsIsAppliedOnTheNextTick()
		{
			var interop = new FakeWindowInterop { BindResult = new BrowserBackingSize(800, 600, 2) };
			var frameLoop = new FakeFrameLoop();

			var platformWindow = new BrowserSystemWindow(interop, frameLoop);
			var systemWindow = new SystemWindow(100, 100);

			try
			{
				platformWindow.ShowSystemWindow(systemWindow);

				// What the ResizeObserver reports: exact integer device pixels, and the ratio they are at. A
				// pane collapsed to nothing and a nonsense ratio are both survivable - see BrowserBacking.
				platformWindow.EnqueueBackingSize(1024, 0, 0);

				// Queued, not applied: agg is laid out from the tick like everything else.
				await Assert.That(systemWindow.Width).IsEqualTo(800.0);

				frameLoop.Tick();

				await Assert.That(platformWindow.Backing).IsEqualTo(new BrowserBackingSize(1024, 1, 1));
				await Assert.That(systemWindow.Width).IsEqualTo(1024.0);
				await Assert.That(systemWindow.Height).IsEqualTo(1.0);
				await Assert.That(systemWindow.DisplayScale).IsEqualTo(1.0);
			}
			finally
			{
				platformWindow.CloseSystemWindow(systemWindow);
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task AQueuedPointerEventReachesTheWidgetTreeInAggCoordinates()
		{
			var interop = new FakeWindowInterop { BindResult = new BrowserBackingSize(800, 600, 2) };
			var frameLoop = new FakeFrameLoop();

			var platformWindow = new BrowserSystemWindow(interop, frameLoop);
			var systemWindow = new SystemWindow(100, 100);

			MouseEventArgs delivered = null;
			systemWindow.MouseDown += (s, e) => delivered = e;

			try
			{
				platformWindow.ShowSystemWindow(systemWindow);

				// offsetX/offsetY are CSS pixels from the canvas's top left; agg wants device pixels from its
				// bottom left. Both steps happen when the event is queued, against the size that was current
				// when the pointer was there.
				platformWindow.EnqueuePointerEvent(
					"pointerdown", 100, 50, button: 0, buttons: 1, detail: 1,
					ctrlKey: false, shiftKey: true, altKey: false, metaKey: false);

				await Assert.That(platformWindow.PendingInputCount).IsEqualTo(1);
				await Assert.That(delivered).IsNull();

				// And the modifier state is live between ticks, because ModifierKeys has no other source in a
				// browser - there is no equivalent of +[NSEvent modifierFlags].
				await Assert.That(platformWindow.ModifierKeys).IsEqualTo(Keys.Shift);

				frameLoop.Tick();

				await Assert.That(delivered).IsNotNull();
				await Assert.That(delivered.Button).IsEqualTo(MouseButtons.Left);
				await Assert.That(delivered.X).IsEqualTo(200.0);
				await Assert.That(delivered.Y).IsEqualTo(500.0);
				await Assert.That(delivered.Clicks).IsEqualTo(1);
			}
			finally
			{
				platformWindow.CloseSystemWindow(systemWindow);
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task ClosingTheWindowStopsTheLoopAndReleasesTheCanvas()
		{
			var interop = new FakeWindowInterop();
			var frameLoop = new FakeFrameLoop();

			var platformWindow = new BrowserSystemWindow(interop, frameLoop);
			var systemWindow = new SystemWindow(100, 100);

			try
			{
				platformWindow.ShowSystemWindow(systemWindow);
				platformWindow.CloseSystemWindow(systemWindow);

				await Assert.That(frameLoop.StopCount).IsEqualTo(1);

				// A closed window that kept its listeners would go on swallowing the page's keystrokes.
				await Assert.That(interop.DetachCount).IsEqualTo(1);
				await Assert.That(BrowserSystemWindow.Current).IsNull();
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		[Test]
		[Timeout(30_000)]
		public async Task ACaptureThatNeverGetsAFrameGivesUpQuietly()
		{
			TimeSpan restoreTimeout = BrowserSystemWindow.CaptureTimeout;
			BrowserSystemWindow.CaptureTimeout = TimeSpan.FromMilliseconds(50);

			string path = Path.Combine(
				Path.GetTempPath(), $"browser-capture-{Guid.NewGuid():N}.png");

			var platformWindow = new BrowserSystemWindow(new FakeWindowInterop(), new FakeFrameLoop());

			try
			{
				// No render device, so no frame ever consumes the request - which is the state the host is in
				// until W4, and the state a hidden tab (which receives no animation frames) is in for good.
				// IPlatformWindow.CaptureScreenshotAsync's remarks allow exactly this: complete, write nothing,
				// and leave the caller to check.
				await platformWindow.CaptureScreenshotAsync(path);

				await Assert.That(File.Exists(path)).IsFalse();

				// And the give-up cleaned up after itself: a second request is accepted rather than being
				// refused by the "one capture in flight" guard forever.
				await platformWindow.CaptureScreenshotAsync(path);

				await Assert.That(File.Exists(path)).IsFalse();
			}
			finally
			{
				BrowserSystemWindow.CaptureTimeout = restoreTimeout;
			}
		}

		[Test]
		public async Task BackingMetricsAreClampedIntoSomethingAggCanBeSizedBy()
		{
			// JS hands over exact integer device pixels, so the rounding here is a safety net rather than the
			// policy - what these pin is that nothing downstream ever sees a zero-sized surface or a zero scale.
			await Assert.That(BrowserBacking.ClampPixelExtent(1920)).IsEqualTo(1920u);
			await Assert.That(BrowserBacking.ClampPixelExtent(1920.5)).IsEqualTo(1921u);
			await Assert.That(BrowserBacking.ClampPixelExtent(0)).IsEqualTo(1u);
			await Assert.That(BrowserBacking.ClampPixelExtent(-4)).IsEqualTo(1u);
			await Assert.That(BrowserBacking.ClampPixelExtent(double.NaN)).IsEqualTo(1u);

			await Assert.That(BrowserBacking.ClampDevicePixelRatio(2)).IsEqualTo(2.0);
			await Assert.That(BrowserBacking.ClampDevicePixelRatio(0)).IsEqualTo(1.0);
			await Assert.That(BrowserBacking.ClampDevicePixelRatio(-1)).IsEqualTo(1.0);
			await Assert.That(BrowserBacking.ClampDevicePixelRatio(double.NaN)).IsEqualTo(1.0);

			await Assert.That(BrowserBacking.FromDeviceMetrics(3840, 2160, 2))
				.IsEqualTo(new BrowserBackingSize(3840, 2160, 2));
		}

		/// <summary>The DOM, replaced by a recorder.</summary>
		private sealed class FakeWindowInterop : IBrowserWindowInterop
		{
			public BrowserBackingSize BindResult { get; set; } = new BrowserBackingSize(400, 300, 1);

			public int BindCount { get; private set; }

			public int AttachCount { get; private set; }

			public int DetachCount { get; private set; }

			public int FocusCount { get; private set; }

			public string Title { get; private set; }

			public string CssCursor { get; private set; }

			public BrowserBackingSize BindCanvas(string canvasSelector)
			{
				this.BindCount++;
				return this.BindResult;
			}

			public void AttachInput(string canvasSelector) => this.AttachCount++;

			public void DetachInput(string canvasSelector) => this.DetachCount++;

			public void SetCursor(string canvasSelector, string cssCursor) => this.CssCursor = cssCursor;

			public void SetDocumentTitle(string title) => this.Title = title;

			public void Focus(string canvasSelector) => this.FocusCount++;
		}

		/// <summary>requestAnimationFrame, replaced by a method the test calls when it wants a frame.</summary>
		private sealed class FakeFrameLoop : IBrowserFrameLoop
		{
			private Action onFrame;

			public int StartCount { get; private set; }

			public int StopCount { get; private set; }

			/// <summary>Whether the window has handed over a tick and not taken it back.</summary>
			public bool IsRunning => this.onFrame != null;

			/// <summary>Runs one frame, the way requestAnimationFrame would.</summary>
			public void Tick()
			{
				if (this.onFrame == null)
				{
					throw new InvalidOperationException("No frame loop is running.");
				}

				this.onFrame();
			}

			public void Start(Action onFrame)
			{
				this.onFrame = onFrame;
				this.StartCount++;
			}

			public void Stop()
			{
				this.onFrame = null;
				this.StopCount++;
			}
		}
	}
}
