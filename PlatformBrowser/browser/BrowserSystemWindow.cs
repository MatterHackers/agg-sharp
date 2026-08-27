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
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// agg's platform window for a browser page: one <c>&lt;canvas&gt;</c>, driven by
	/// <c>requestAnimationFrame</c>.
	/// </summary>
	/// <remarks>
	/// <para><b>Modeled on the mac host, which is the closest desktop analogue.</b> Both hand-roll their own
	/// loop rather than being called back by a toolkit, both treat invalidation as a boolean rather than a
	/// message, and both redraw the whole frame. The differences are all consequences of the page: there is no
	/// window to move, size, title, raise or destroy, there is exactly one thread, and the loop belongs to the
	/// browser.</para>
	///
	/// <para><b>ShowSystemWindow returns.</b> On every desktop host it blocks in the event loop until the
	/// application is done, because that is what <c>Application.Run</c> does and what every demo's
	/// <c>Main</c> expects. Here it starts the animation frame loop and comes straight back: the runtime is
	/// kept alive by Blazor's <c>RunAsync</c>, and blocking the one thread would stop the very frames the
	/// window was shown to draw.</para>
	///
	/// <para><b>One window at a time.</b> Multi-window is out of scope for the browser v1, and it is not a
	/// gap: <see cref="SingleWindowMode"/> is how an application shell puts dialogs on screen, and they are
	/// drawn inside this one canvas by the provider. A second window shown takes the loop over.</para>
	///
	/// <para><b>No lock anywhere.</b> DOM listeners and animation frame callbacks are both dispatched from
	/// the browser's single thread, and .NET on wasm has no shared-memory threading at all - there is no
	/// second thread to race with, so the input queue below is a plain list and the flags are plain fields.
	/// The desktop hosts' locks exist because their tests drive them from thread pool workers; nothing can do
	/// that here.</para>
	/// </remarks>
	public partial class BrowserSystemWindow : IPlatformWindow
	{
		/// <summary>The three keys <c>Keyboard</c> tracks a modifier's down state under.</summary>
		private static readonly Keys[] ModifierStateKeys = { Keys.ShiftKey, Keys.ControlKey, Keys.Menu };

		private readonly IBrowserWindowInterop interop;
		private readonly IBrowserFrameLoop frameLoop;
		private readonly BrowserFrameTick frameTick;

		private readonly BrowserModifierState modifierState = new BrowserModifierState();

		/// <summary>
		/// Which buttons this canvas owns for the duration of a drag; see <see cref="OutOfViewMouseCapture"/>.
		/// </summary>
		/// <remarks>
		/// On top of, not instead of, <c>setPointerCapture</c> - <c>input.js</c> takes the native capture on
		/// every pointerdown, and that is what keeps the events arriving at all once the pointer has left the
		/// canvas. This decides which of them agg is told about, and it is the hedge for the case the native
		/// capture cannot cover: Safari drops a capture on some gestures without warning, and a stray up
		/// arriving from a press agg never saw (a press that started on the page's chrome) must not reach a
		/// widget as the end of a drag it never began.
		/// </remarks>
		private readonly OutOfViewMouseCapture mouseCapture = new OutOfViewMouseCapture();

		/// <summary>
		/// Events that arrived since the last tick, in arrival order. Not synchronized, deliberately - see the
		/// class remarks.
		/// </summary>
		private readonly List<BrowserInputEvent> inputQueue = new List<BrowserInputEvent>();

		private SystemWindow aggSystemWindow;
		private BrowserBackingSize backing;
		private string caption = string.Empty;
		private Vector2 minimumSize;
		private string currentCssCursor;
		private bool hasClosed;
		private bool isInsidePaint;

		/// <summary>
		/// The WebGPU device, swapchain and compat context for this canvas, or null until the asynchronous
		/// bring-up has finished (or after a device loss). See <see cref="RenderLayerReady"/>.
		/// </summary>
		private BrowserWebGpuLayer renderLayer;

		/// <summary>
		/// Whether a render layer bring-up is in flight or has finished, so the fire-and-forget start is not
		/// begun twice - a second device would take the canvas's WebGPU context away from the first.
		/// </summary>
		private bool renderLayerInitializationStarted;

		/// <summary>
		/// Whether this frame has had its viewport and projection set. Same field, and the same one-shot
		/// lifetime, as the mac host's: it is set by the first <see cref="NewGraphics2D"/> of a frame and
		/// cleared when the frame ends.
		/// </summary>
		private bool viewPortHasBeenSet;

		/// <summary>
		/// The button the last pointerdown carried. Only <c>pointercancel</c> reads it; see
		/// <see cref="EnqueuePointerEvent"/>.
		/// </summary>
		private MouseButtons lastPressedButton = MouseButtons.None;

		/// <summary>
		/// A screenshot asked for but not taken yet. The read-back can only happen at the end of a frame, so
		/// the request waits here for one; see <see cref="CaptureScreenshotAsync"/>.
		/// </summary>
		private string pendingScreenshotPath;

		/// <summary>Signalled by the frame that performs a queued capture. See <see cref="CaptureScreenshotAsync"/>.</summary>
		private TaskCompletionSource screenshotCompletion;

		/// <param name="interop">The DOM seam. <see cref="CreateForBrowser"/> supplies the real one.</param>
		/// <param name="frameLoop">The animation frame loop.</param>
		public BrowserSystemWindow(IBrowserWindowInterop interop, IBrowserFrameLoop frameLoop)
		{
			this.interop = interop ?? throw new ArgumentNullException(nameof(interop));
			this.frameLoop = frameLoop ?? throw new ArgumentNullException(nameof(frameLoop));

			this.frameTick = new BrowserFrameTick(this.DrainBrowserEvents, this.CanPaint, this.PaintFrame);
		}

		/// <summary>
		/// The window the JS event handlers deliver to, or null when none is showing. One window at a time;
		/// see the class remarks.
		/// </summary>
		public static BrowserSystemWindow Current { get; private set; }

		/// <summary>
		/// Whether every agg window in the process shares this one canvas, dialogs included - what an
		/// application shell runs on. Identical in meaning to <c>MacSystemWindow.SingleWindowMode</c>: the
		/// provider wraps everything shown after the first window in a <c>WindowWidget</c>, draws it inside the
		/// canvas already up, and hands that wrapper back to this same window.
		/// </summary>
		public static bool SingleWindowMode { get; set; }

		/// <summary>
		/// The CSS selector for the canvas agg draws into. The host page owns the element; a head that names
		/// it something else sets this before showing a window.
		/// </summary>
		public static string CanvasSelector { get; set; } = "#agg-canvas";

		/// <summary>
		/// How long <see cref="CaptureScreenshotAsync"/> waits for the frame that would serve it before giving
		/// up quietly. Settable so a test does not have to wait out the real bound.
		/// </summary>
		public static TimeSpan CaptureTimeout { get; set; } = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Where this window says things the user has to be told, since a page has no message box and - when
		/// this is called - no canvas that can paint one. A head points it at whatever it shows status in;
		/// unset means the console is the only channel, which every message also uses.
		/// </summary>
		/// <remarks>
		/// The one message this carries today is "this browser has no WebGPU", which is fatal: there is no
		/// software fallback below WebGPU anywhere in agg, so a page without it shows nothing at all.
		/// </remarks>
		public static Action<string> ReportStatus { get; set; }

		/// <summary>The SystemWindow this platform window is currently drawing.</summary>
		public SystemWindow AggSystemWindow => this.aggSystemWindow;

		/// <summary>The provider that created this window, set by the provider itself.</summary>
		public ISystemWindowProvider WindowProvider { get; set; }

		/// <summary>The tick this window's frames run through. Diagnostics, and the smoke-run frame count.</summary>
		public BrowserFrameTick FrameTick => this.frameTick;

		/// <summary>The canvas's backing store as agg is sized from it.</summary>
		public BrowserBackingSize Backing => this.backing;

		/// <summary>
		/// Whether the browser render device exists, so a frame can actually be drawn.
		/// </summary>
		/// <remarks>
		/// Computed from the layer rather than set, because the answer is the device's own lifetime and
		/// nothing else: false while the adapter and device promises are in flight at start-up, true once
		/// they have settled, and false again from the moment a lost device is noticed until its replacement
		/// is up. It is load bearing, not a diagnostic - the tick paints nothing while it is false, which is
		/// what keeps <see cref="NewGraphics2D"/>'s descriptive throw from being reported sixty times a
		/// second during bring-up. The window still ticks throughout, so idle work, input and layout all run;
		/// only the drawing waits.
		/// </remarks>
		public bool RenderLayerReady => this.renderLayer?.IsWebGpuInitialized == true;

		/// <summary>The render layer for this canvas, or null before bring-up finishes. Diagnostics.</summary>
		public BrowserWebGpuLayer RenderLayer => this.renderLayer;

		/// <summary>The page's title. There is no window frame in a page, so a caption is <c>document.title</c>.</summary>
		public string Caption
		{
			get => this.caption;

			set
			{
				this.caption = value ?? string.Empty;
				this.interop.SetDocumentTitle(this.caption);
			}
		}

		/// <summary>
		/// Always zero: the canvas has no title bar. The automation runner subtracts this when converting
		/// screen coordinates to window ones, and in a page the two spaces have the same origin.
		/// </summary>
		public int TitleBarHeight => 0;

		/// <summary>
		/// Always the origin, and setting it does nothing. A page cannot be told where to put itself on the
		/// user's desktop, and the canvas is the whole of agg's world - so the honest answer is that this
		/// window is at 0,0 of the only space it can see.
		/// </summary>
		public Point2D DesktopPosition
		{
			get => new Point2D(0, 0);
			set { }
		}

		/// <summary>
		/// Remembered but not enforced: nothing can stop a browser window being resized below it. Applications
		/// read this back, so it is stored rather than dropped.
		/// </summary>
		public Vector2 MinimumSize
		{
			get => this.minimumSize;
			set => this.minimumSize = value;
		}

		/// <summary>
		/// The modifier keys held right now, as of the last event to say so. The browser has no equivalent of
		/// <c>+[NSEvent modifierFlags]</c> - see <see cref="BrowserModifierState"/>.
		/// </summary>
		public Keys ModifierKeys => this.modifierState.ModifierKeys;

		/// <summary>
		/// Nothing to do: there is one canvas, and it is as far front as anything in the page gets. Raising a
		/// dialog above the shell is the provider's business, and it draws them into this same canvas.
		/// </summary>
		public void BringToFront()
		{
		}

		/// <summary>
		/// Nothing to do, for the same reason as <see cref="BringToFront"/>, plus one browser rule: a page
		/// cannot make itself frontmost, and the canvas keeps DOM focus across a dialog because the dialog is
		/// drawn inside it. The canvas is focused when it is bound and on every pointer down, which is where
		/// keyboard focus actually comes from here.
		/// </summary>
		public void Activate()
		{
		}

		/// <summary>
		/// Schedules a repaint. The rectangle is ignored - the whole frame is redrawn - exactly as on the mac
		/// and X11 hosts.
		/// </summary>
		public void Invalidate(RectangleDouble rectToInvalidate) => this.frameTick.Invalidate();

		/// <summary>
		/// Asks this window to close, the way a desktop host's frame close button does: by closing the agg
		/// window, which comes back through <see cref="CloseSystemWindow"/> to stop the loop.
		/// </summary>
		/// <remarks>
		/// Routing through agg rather than tearing down directly is what keeps one close path, so an
		/// application's ShouldClose/Closed handlers run whoever asked. There is no native window to destroy
		/// afterwards; the canvas outlives the application, and the page is what actually goes away.
		/// </remarks>
		public void Close()
		{
			if (this.hasClosed)
			{
				return;
			}

			this.aggSystemWindow?.Close();
		}

		/// <summary>Points the canvas's CSS <c>cursor</c> at the agg cursor asked for.</summary>
		public void SetCursor(Cursors cursorToSet)
		{
			string cssCursor = BrowserCursorMap.ToCssCursor(cursorToSet);

			if (cssCursor == this.currentCssCursor)
			{
				// agg asks on every OnMouseEnter, and a style write per hovered widget is interop traffic for
				// no change at all. The mac host skips the repeat for a sharper reason (re-asserting a cursor
				// there posts a spurious exit/enter pair); here it is simply waste.
				return;
			}

			this.currentCssCursor = cssCursor;
			this.interop.SetCursor(CanvasSelector, cssCursor);
		}

		/// <summary>
		/// The 2D surface for a frame, over the canvas's swapchain texture. Acquires that texture if this is
		/// the frame's first call. Throws while there is no device; see <see cref="RenderLayerReady"/>.
		/// </summary>
		public Graphics2D NewGraphics2D()
		{
			BrowserWebGpuLayer layer = this.renderLayer;

			// Descriptive on purpose, and the same shape the mac and X11 hosts use: without it the caller gets
			// a bare NullReferenceException out of Graphics2DGpu and no hint that the real problem is a window
			// painting before its device exists.
			if (layer?.Gl == null)
			{
				throw new InvalidOperationException(
					"The browser WebGPU device is not initialized, so this window cannot make a Graphics2D. "
					+ "It is created asynchronously from ShowSystemWindow (the adapter and device are Promises), "
					+ "so a paint can legitimately arrive before it exists - which is what RenderLayerReady "
					+ "gates. Reaching here means that gate was bypassed, or the device was lost.");
			}

			if (!this.viewPortHasBeenSet)
			{
				this.SetAndClearViewPort();
			}

			// Re-read rather than reusing the local: SetAndClearViewPort begins the frame, and a frame that
			// begins on a lost device tears the layer down instead of acquiring anything.
			if (layer.Gl == null)
			{
				throw new InvalidOperationException(
					"The browser WebGPU device was lost while this frame was starting, so this window cannot "
					+ "make a Graphics2D. A new device is being created; painting resumes when it is ready.");
			}

			Graphics2D graphics2D = new Graphics2DGpu(
				layer.Gl,
				(int)this.backing.PixelWidth,
				(int)this.backing.PixelHeight,
				GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		/// <summary>
		/// Connects a <see cref="SystemWindow"/> to the canvas, subscribes input, sizes agg from the backing
		/// store, starts the animation frame loop - and returns.
		/// </summary>
		/// <remarks>
		/// The non-blocking shape is the one real difference from every other host (see the class remarks).
		/// Nothing is imported from JS here either: the modules have to be in place already, which is
		/// <see cref="BrowserHostBootstrap.InitializeAsync"/>'s job, awaited by the head before it ever reaches
		/// application startup. A synchronous method cannot await an import, and doing it lazily would mean the
		/// first frames ran against a module that had not loaded.
		/// </remarks>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			if (systemWindow.PlatformWindow == this)
			{
				// In single window mode the provider points a window at this one before showing it, so
				// "already mine" means "start drawing this instead", not "raise what is already up".
				if (SingleWindowMode)
				{
					this.SetActiveAggWindow(systemWindow);
					return;
				}

				this.BringToFront();
				return;
			}

			this.aggSystemWindow = systemWindow;
			systemWindow.PlatformWindow = this;
			systemWindow.AnchorAll();

			Current = this;

			// Bind before attaching input: binding is what makes the canvas focusable and stops the page
			// scrolling under a touch drag, and it reports the size everything below is laid out against.
			this.ApplyBackingSize(this.interop.BindCanvas(CanvasSelector));
			this.interop.AttachInput(CanvasSelector);
			this.interop.SetDocumentTitle(this.caption.Length > 0 ? this.caption : systemWindow.Title ?? string.Empty);
			this.interop.Focus(CanvasSelector);

			// Started here and not awaited: this method is synchronous by contract (see the class remarks)
			// and the device is a pair of Promises that cannot settle while it is on the stack. The loop below
			// runs from the first frame regardless - draining input, laying out and running idle work - and
			// RenderLayerReady is what keeps it from trying to paint before the device arrives.
			this.StartRenderLayerInitialization();

			this.frameTick.Invalidate();

			this.frameLoop.Start(this.frameTick.Tick);
		}

		/// <summary>
		/// Begins (or, after a device loss, begins again) the asynchronous bring-up of the render layer.
		/// Returns immediately; <see cref="RenderLayerReady"/> turns true when it has finished.
		/// </summary>
		private void StartRenderLayerInitialization()
		{
			if (this.hasClosed || this.renderLayerInitializationStarted)
			{
				return;
			}

			this.renderLayerInitializationStarted = true;

			// Fire and forget, and contained: InitializeRenderLayerAsync catches everything it can fail on,
			// so there is no faulted task for anyone to observe. There is nobody to hand a Task to - the
			// caller is a synchronous IPlatformWindow method or an idle action - and awaiting it here would
			// only move the problem.
			_ = this.InitializeRenderLayerAsync();
		}

		/// <summary>
		/// Creates the WebGPU device and swapchain for the canvas and, once they exist, hands them to this
		/// window and asks for the first real frame.
		/// </summary>
		private async Task InitializeRenderLayerAsync()
		{
			var layer = new BrowserWebGpuLayer(CanvasSelector, this.backing.PixelWidth, this.backing.PixelHeight)
			{
				// A frame the swapchain could not hand a texture out for is not a frame anyone asked to
				// repeat, so the layer needs a way to ask for the next one.
				RequestRedraw = this.frameTick.Invalidate,

				DeviceLost = this.HandleRenderLayerDeviceLost,
			};

			try
			{
				await layer.InitializeWebGpuAsync();
			}
			catch (Exception initializationFailure)
			{
				layer.Dispose();

				// renderLayerInitializationStarted is deliberately left true, so nothing tries again: a
				// browser that has no WebGPU will not grow one, and a retry loop would be an error message a
				// second. A device *loss* is the case that does re-arm it; see HandleRenderLayerDeviceLost.
				ReportRenderLayerFailure(initializationFailure);
				return;
			}

			if (this.hasClosed)
			{
				layer.Dispose();
				return;
			}

			this.renderLayer = layer;

			// The canvas may have been resized while the promises were in flight - the resize events are
			// drained by ticks that ran throughout - and the layer was created at the size measured before.
			layer.Resize(this.backing.PixelWidth, this.backing.PixelHeight);

			// Nothing has been drawn yet and the tick's redraw flag may well have been consumed by a frame
			// that could not paint, so the first real frame has to be asked for explicitly.
			this.aggSystemWindow?.Invalidate();
			this.frameTick.Invalidate();
		}

		/// <summary>
		/// Puts a failed bring-up in front of the user and in the console. There is no fallback renderer to
		/// drop to - WebGPU is the only path to screen - so this is the whole of the application on this
		/// browser.
		/// </summary>
		private static void ReportRenderLayerFailure(Exception initializationFailure)
		{
			// Wording is deliberately plain and product-owned; the exception carries the detail a developer
			// needs and the console keeps it.
			const string userMessage = "This browser does not support WebGPU, which MatterCAD requires.";

			Console.Error.WriteLine($"{userMessage} The render layer could not be created: {initializationFailure}");

			ReportStatus?.Invoke(userMessage);
		}

		/// <summary>
		/// Drops the lost layer and starts a new one down the same path as start-up.
		/// </summary>
		/// <remarks>
		/// Queued rather than run here: this is called from inside the dying layer's own BeginFrame, and
		/// disposing it from its own call stack is a trap that is easy to fall into and hard to see. The
		/// frame in flight is abandoned by NewGraphics2D's throw, and the not-ready gate keeps every frame
		/// after it from trying, so all the queued work has to do is build the replacement.
		/// </remarks>
		private void HandleRenderLayerDeviceLost()
		{
			UiThread.RunOnIdle(() =>
			{
				BrowserWebGpuLayer lost = this.renderLayer;
				this.renderLayer = null;
				lost?.Dispose();

				this.renderLayerInitializationStarted = false;
				this.StartRenderLayerInitialization();
			});
		}

		/// <summary>
		/// Tears this platform window down in response to the agg window closing. Called by the provider from
		/// <see cref="SystemWindow.OnClosed"/>.
		/// </summary>
		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// In single window mode a dialog lives inside this canvas, so closing one is only a matter of going
			// back to drawing whatever the provider now has on top. Only the shell - the window the provider is
			// left holding - takes the loop down with it.
			if (SingleWindowMode
				&& !this.hasClosed
				&& this.WindowProvider?.TopWindow != null
				&& this.WindowProvider.TopWindow != systemWindow)
			{
				this.SetActiveAggWindow(this.WindowProvider.TopWindow);
				return;
			}

			if (this.hasClosed)
			{
				return;
			}

			this.hasClosed = true;

			// Stop first: a tick that ran after the listeners were detached would drain an empty queue and try
			// to paint a closed window, and one that ran after this returned would be doing it forever.
			this.frameLoop.Stop();
			this.interop.DetachInput(CanvasSelector);

			this.inputQueue.Clear();
			this.mouseCapture.ClearCapturedButtons();

			// After the loop has stopped, so nothing is mid-frame: the layer owns the device, the swapchain
			// and every texture cached against them, and the canvas's WebGPU context stays claimed until it
			// goes. A bring-up still in flight sees hasClosed and disposes its own device.
			this.renderLayer?.Dispose();
			this.renderLayer = null;

			if (Current == this)
			{
				Current = null;
			}
		}

		/// <summary>
		/// Makes <paramref name="systemWindow"/> the window this canvas draws and routes input to. Only single
		/// window mode reaches it; see <see cref="SingleWindowMode"/>.
		/// </summary>
		private void SetActiveAggWindow(SystemWindow systemWindow)
		{
			if (systemWindow == null || this.hasClosed)
			{
				return;
			}

			if (this.aggSystemWindow != systemWindow)
			{
				this.aggSystemWindow = systemWindow;
				systemWindow.PlatformWindow = this;
				systemWindow.AnchorAll();

				// Device pixels, matching ApplyBackingSize: this window's agg coordinate space is the canvas
				// backing store, and a swapped-in window that kept its own size would be drawn at the wrong one.
				systemWindow.SetBoundsFromPlatform(this.backing.PixelWidth, this.backing.PixelHeight);
				systemWindow.SetDisplayScale(this.backing.DevicePixelRatio);
				systemWindow.SetDisplayUsableSize(this.UsableSize);
			}

			systemWindow.Invalidate();
			this.frameTick.Invalidate();
		}

		/// <summary>
		/// How much room the application has. In a page the canvas is the whole of it - there is no desktop
		/// around it and no other window to share it with - so this is the backing size, in the device pixels
		/// every other size agg holds is in.
		/// </summary>
		private Vector2 UsableSize => new Vector2(this.backing.PixelWidth, this.backing.PixelHeight);

		/// <summary>
		/// The canvas in agg's coordinate space, which is what "inside the view" is measured against.
		/// </summary>
		/// <remarks>
		/// A closed interval, matching <see cref="BrowserPointer.ToAggPosition"/>'s <c>height - y</c>: a click
		/// on the last row of pixels reports exactly the height and still belongs to the canvas. X11 needs the
		/// exclusive version and keeps its own; see <see cref="OutOfViewMouseCapture.IsInsideBounds"/>.
		/// </remarks>
		private RectangleDouble SurfaceBounds
			=> new RectangleDouble(0, 0, this.backing.PixelWidth, this.backing.PixelHeight);

		// -----------------------------------------------------------------------------------------
		// Input, queued by the JS listeners and delivered at the start of a tick
		// -----------------------------------------------------------------------------------------

		/// <summary>How many events are waiting for the next tick. Diagnostics and tests.</summary>
		public int PendingInputCount => this.inputQueue.Count;

		/// <summary>
		/// Translates and queues a DOM pointer event.
		/// </summary>
		/// <param name="type">The DOM event type: pointerdown, pointerup, pointercancel, pointermove or
		/// pointerleave.</param>
		/// <param name="offsetX">The event's <c>offsetX</c> - CSS pixels from the canvas's padding box.</param>
		/// <param name="offsetY">The event's <c>offsetY</c>.</param>
		/// <param name="button">The event's <c>button</c>; -1 on a move, which reports no button change.</param>
		/// <param name="buttons">The event's <c>buttons</c> mask, which is where a drag's held button comes
		/// from.</param>
		/// <param name="detail">The event's <c>detail</c> - the click count the browser itself timed.</param>
		public void EnqueuePointerEvent(
			string type,
			double offsetX,
			double offsetY,
			int button,
			int buttons,
			int detail,
			bool ctrlKey,
			bool shiftKey,
			bool altKey,
			bool metaKey)
		{
			if (!this.ShouldAcceptInput())
			{
				return;
			}

			this.modifierState.Update(ctrlKey, shiftKey, altKey, metaKey);

			// A move carries button -1 because nothing changed on it, so its button has to come from the mask -
			// whose bits are not the index's numbering. See BrowserPointer.HeldButton.
			MouseButtons aggButton = button < 0
				? BrowserPointer.HeldButton(buttons)
				: BrowserPointer.TranslateButton(button);

			if (type == "pointercancel")
			{
				// A cancel carries button -1 and buttons 0 by specification: the browser is taking the pointer
				// away without saying which button it is taking, and no pointerup is coming afterwards. So the
				// drag is ended with the button that started it - which is the one agg's MouseEventArgs can
				// carry anyway - and that is what lets the arbiter release its capture rather than holding it
				// for a release that will never arrive.
				aggButton = this.lastPressedButton;
			}

			MouseEventArgs mouseEvent = BrowserPointer.MakeMouseEventArgs(
				aggButton,
				detail,
				offsetX,
				offsetY,
				this.backing.DevicePixelRatio,
				this.backing.PixelHeight);

			bool insideView = OutOfViewMouseCapture.IsInsideBounds(
				new Vector2(mouseEvent.X, mouseEvent.Y), this.SurfaceBounds);

			if (type == "pointerleave" || type == "pointerout")
			{
				// A drag owns the pointer wherever it has gone, and its own mouse up is what ends it - telling
				// agg the pointer is nowhere near mid-drag reads to a widget as the drag being abandoned. Same
				// rule, and the same helper, as the mac host applies to a mouseExited.
				if (!OutOfViewMouseCapture.IsRealPointerExit(
					new Vector2(mouseEvent.X, mouseEvent.Y), this.SurfaceBounds, this.mouseCapture.HasCapturedButtons))
				{
					return;
				}

				// The same sentinel the Windows sink and the mac host use for "the pointer is nowhere near me".
				this.Enqueue(BrowserInputEvent.Mouse(
					BrowserInputEventKind.MouseMove,
					new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0),
					this.modifierState.DownStateKeys));
				return;
			}

			// The arbiter updates the captured set as a side effect, so this runs exactly once per event and
			// before anything else can decide the event is uninteresting.
			if (!BrowserPointer.ShouldDeliver(this.mouseCapture, type, buttons, aggButton, insideView))
			{
				return;
			}

			if (type == "pointerdown")
			{
				this.lastPressedButton = aggButton;
			}

			BrowserInputEventKind kind;
			switch (BrowserPointer.PointerEventKindFor(type, buttons))
			{
				case PointerEventKind.Down:
					kind = BrowserInputEventKind.MouseDown;
					break;

				case PointerEventKind.Up:
					kind = BrowserInputEventKind.MouseUp;
					break;

				default:
					kind = BrowserInputEventKind.MouseMove;
					break;
			}

			this.Enqueue(BrowserInputEvent.Mouse(kind, mouseEvent, this.modifierState.DownStateKeys));
		}

		/// <summary>
		/// Translates and queues a DOM wheel event - a wheel, a two-finger scroll, or a pinch (which arrives
		/// as a ctrl+wheel; see <see cref="BrowserWheel"/>).
		/// </summary>
		public void EnqueueWheelEvent(
			double offsetX,
			double offsetY,
			double deltaX,
			double deltaY,
			int deltaMode,
			bool ctrlKey,
			bool shiftKey,
			bool altKey,
			bool metaKey)
		{
			if (!this.ShouldAcceptInput())
			{
				return;
			}

			this.modifierState.Update(ctrlKey, shiftKey, altKey, metaKey);

			MouseEventArgs wheelEvent = BrowserPointer.MakeMouseEventArgs(
				MouseButtons.None,
				0,
				offsetX,
				offsetY,
				this.backing.DevicePixelRatio,
				this.backing.PixelHeight);

			BrowserWheel.ApplyWheelEvent(wheelEvent, deltaX, deltaY, deltaMode, ctrlKey, this.backing.DevicePixelRatio);

			this.Enqueue(BrowserInputEvent.Mouse(
				BrowserInputEventKind.MouseWheel, wheelEvent, this.modifierState.DownStateKeys));
		}

		/// <summary>
		/// Translates and queues a DOM keyboard event.
		/// </summary>
		/// <param name="type">keydown or keyup.</param>
		/// <param name="code">The event's <c>code</c> - the physical key, which is what agg's shortcuts are
		/// spelled in. See <see cref="BrowserKeyboard"/> for why not <c>key</c>.</param>
		/// <param name="key">The event's <c>key</c> - what the layout says was typed, which is where the text
		/// of a key press comes from.</param>
		public void EnqueueKeyEvent(
			string type,
			string code,
			string key,
			bool ctrlKey,
			bool shiftKey,
			bool altKey,
			bool metaKey)
		{
			if (!this.ShouldAcceptInput())
			{
				return;
			}

			this.modifierState.Update(ctrlKey, shiftKey, altKey, metaKey);

			KeyEventArgs keyEvent = BrowserKeyboard.MakeKeyEventArgs(code, ctrlKey, shiftKey, altKey, metaKey);

			if (type == "keyup")
			{
				this.Enqueue(BrowserInputEvent.Key(
					BrowserInputEventKind.KeyUp, keyEvent, null, this.modifierState.DownStateKeys));
				return;
			}

			// A Control or Command chord is a shortcut, never text: typing Ctrl-S must not also insert an "s"
			// into whatever has focus. Same rule the mac host applies to a Command chord, and the browser folds
			// Command into Control anyway (see BrowserKeyboard.ModifierDownStateKeys).
			bool chord = ctrlKey || metaKey;

			string typedText = !chord && BrowserKeyboard.IsPrintableKey(key) ? key : null;

			this.Enqueue(BrowserInputEvent.Key(
				BrowserInputEventKind.KeyDown, keyEvent, typedText, this.modifierState.DownStateKeys));
		}

		/// <summary>
		/// Queues the page losing focus. Whatever was held has to be let go of: a modifier released while the
		/// page was not looking sends no event at all, and would otherwise be reported as held forever.
		/// </summary>
		public void EnqueueFocusLost()
		{
			// Cleared here rather than at delivery so ModifierKeys stops lying immediately; the queued event is
			// what releases agg's process-wide Keyboard state, in order with the rest of the input.
			IReadOnlySet<Keys> heldWhenFocusLeft = this.modifierState.DownStateKeys;
			this.modifierState.Clear();

			// And the same for the buttons. A window that has lost the input entirely has no release left to
			// wait for - the pointer capture went with the focus - so a button left captured here would make
			// every later move on the page look like the continuation of a drag that is over.
			this.mouseCapture.ClearCapturedButtons();
			this.lastPressedButton = MouseButtons.None;

			this.Enqueue(BrowserInputEvent.FocusLost(heldWhenFocusLeft));
		}

		/// <summary>
		/// Queues a canvas resize. JS hands over exact integer device pixels (it reads
		/// <c>devicePixelContentBoxSize</c> and owns the one rounding); see <see cref="BrowserBacking"/>.
		/// </summary>
		public void EnqueueBackingSize(double devicePixelWidth, double devicePixelHeight, double devicePixelRatio)
			=> this.Enqueue(BrowserInputEvent.BackingSizeChanged(
				BrowserBacking.FromDeviceMetrics(devicePixelWidth, devicePixelHeight, devicePixelRatio)));

		/// <summary>
		/// Whether real input should reach agg at all. Parallel automation runs turn
		/// <see cref="IPlatformWindow.EnablePlatformWindowInput"/> off so a real mouse or keyboard cannot
		/// perturb them; every host makes the same check at its event seam.
		/// </summary>
		private bool ShouldAcceptInput()
			=> IPlatformWindow.EnablePlatformWindowInput && !this.hasClosed && this.aggSystemWindow != null;

		private void Enqueue(BrowserInputEvent inputEvent) => this.inputQueue.Add(inputEvent);

		/// <summary>
		/// Delivers everything that arrived since the last tick, in arrival order.
		/// </summary>
		private void DrainBrowserEvents()
		{
			if (this.inputQueue.Count == 0)
			{
				return;
			}

			// A private copy, for the reason UiThread.InvokePendingActions takes one: delivering an event runs
			// widget code, which is free to open a dialog whose nested pump ticks this window again - and that
			// nested drain would otherwise clear and refill the very list being walked here. Events queued
			// while this drain runs belong to the next one.
			BrowserInputEvent[] draining = this.inputQueue.ToArray();
			this.inputQueue.Clear();

			foreach (BrowserInputEvent inputEvent in draining)
			{
				this.Deliver(inputEvent);
			}
		}

		private void Deliver(BrowserInputEvent inputEvent)
		{
			if (this.hasClosed)
			{
				return;
			}

			if (inputEvent.Kind == BrowserInputEventKind.BackingSizeChanged)
			{
				this.ApplyBackingSize(inputEvent.BackingSize);
				return;
			}

			SystemWindow window = this.aggSystemWindow;
			if (window == null || window.HasBeenClosed)
			{
				return;
			}

			ApplyModifierDownState(inputEvent.ModifierDownKeys);

			switch (inputEvent.Kind)
			{
				case BrowserInputEventKind.MouseDown:
					window.OnMouseDown(inputEvent.MouseEvent);
					break;

				case BrowserInputEventKind.MouseUp:
					window.OnMouseUp(inputEvent.MouseEvent);
					break;

				case BrowserInputEventKind.MouseMove:
					window.OnMouseMove(inputEvent.MouseEvent);
					break;

				case BrowserInputEventKind.MouseWheel:
					window.OnMouseWheel(inputEvent.MouseEvent);
					break;

				case BrowserInputEventKind.KeyDown:
					window.OnKeyDown(inputEvent.KeyEvent);
					Keyboard.SetKeyDownState(inputEvent.KeyEvent.KeyCode, true);

					// Read after OnKeyDown, the way the mac host reads it: a handler that consumed the key
					// decides here whether the text of it is also delivered.
					if (inputEvent.TypedText != null && !inputEvent.KeyEvent.SuppressKeyPress)
					{
						foreach (char character in inputEvent.TypedText)
						{
							window.OnKeyPress(new KeyPressEventArgs(character));
						}
					}

					break;

				case BrowserInputEventKind.KeyUp:
					// Only process the key up if we saw the key down, matching the Windows sink and the mac host.
					if (Keyboard.IsKeyDown(inputEvent.KeyEvent.KeyCode))
					{
						window.OnKeyUp(inputEvent.KeyEvent);
						Keyboard.SetKeyDownState(inputEvent.KeyEvent.KeyCode, false);
					}

					break;

				case BrowserInputEventKind.FocusLost:
					// Narrow on purpose, where a Keyboard.Clear() would not be: Keyboard is process-wide and
					// other callers write to it directly (an automation test sets Shift down and then
					// shift-clicks), so releasing only what this window applied cannot reach anything it did
					// not put there. Same rule as MacSystemWindow.ReleaseAppliedModifierKeys.
					foreach (Keys modifierKey in inputEvent.ModifierDownKeys)
					{
						Keyboard.SetKeyDownState(modifierKey, false);
					}

					break;
			}
		}

		/// <summary>
		/// Puts the modifier down state an event reported into <see cref="Keyboard"/>.
		/// </summary>
		/// <remarks>
		/// Every modifier is written on every call, including the ones being released: SetKeyDownState is
		/// idempotent and only raises StateChanged on a real change, and the browser tells us what is held on
		/// every input event rather than only on key events - which is how a modifier pressed while the pointer
		/// is moving is noticed at all.
		/// </remarks>
		private static void ApplyModifierDownState(IReadOnlySet<Keys> modifierDownKeys)
		{
			if (modifierDownKeys == null)
			{
				return;
			}

			foreach (Keys modifierKey in ModifierStateKeys)
			{
				Keyboard.SetKeyDownState(modifierKey, modifierDownKeys.Contains(modifierKey));
			}
		}

		// -----------------------------------------------------------------------------------------
		// Sizing and painting
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Pushes a new backing size everywhere it has to go: agg's bounds, its display scale, and its idea of
		/// how much room it has. The browser host's <c>SyncSizeFromBacking</c>.
		/// </summary>
		/// <remarks>
		/// The canvas's own <c>width</c>/<c>height</c> attributes are set by JS, in the same place the size is
		/// measured, so there is exactly one rounding of a fractional CSS layout into whole device pixels and
		/// the backing store cannot disagree with what agg was told. What is here is what a swapchain resize
		/// (W4) and the widget tree need.
		/// </remarks>
		public void ApplyBackingSize(BrowserBackingSize newBacking)
		{
			if (this.hasClosed || newBacking.Equals(this.backing))
			{
				return;
			}

			this.backing = newBacking;

			// Before the widget tree is told, for the same reason the mac host reconfigures before laying
			// out: the swapchain is what the next frame draws into, and a layout pass can paint.
			this.renderLayer?.Resize(newBacking.PixelWidth, newBacking.PixelHeight);

			if (this.aggSystemWindow != null)
			{
				// The canvas is this big whatever the application's minimum says. Assigning LocalBounds would
				// let a minimum computed for a larger viewport inflate the layout past the drawable, and agg
				// being y-up that clips off the top.
				this.aggSystemWindow.SetBoundsFromPlatform(newBacking.PixelWidth, newBacking.PixelHeight);

				// A window dragged to a display with a different devicePixelRatio changes scale without
				// necessarily changing CSS size; SetDisplayScale only stores the value and raises its event
				// from the idle queue, so this is safe to call from inside a resize burst.
				this.aggSystemWindow.SetDisplayScale(newBacking.DevicePixelRatio);
				this.aggSystemWindow.SetDisplayUsableSize(this.UsableSize);
				this.aggSystemWindow.Invalidate();
			}

			this.frameTick.Invalidate();
		}

		/// <summary>
		/// Whether there is anything to paint into. See <see cref="RenderLayerReady"/> for why a window with no
		/// device ticks happily and draws nothing.
		/// </summary>
		private bool CanPaint()
			=> this.RenderLayerReady
				&& !this.hasClosed
				&& !this.isInsidePaint
				&& this.aggSystemWindow != null
				&& !this.aggSystemWindow.HasBeenClosed
				&& this.backing.PixelWidth > 0
				&& this.backing.PixelHeight > 0;

		/// <summary>
		/// Draws and ends one frame - <c>MacSystemWindow.DrawAndPresent</c>, minus its smoke-run pumping.
		/// <see cref="BrowserFrameTick"/> has already cleared the redraw flag and will contain and report
		/// whatever this throws - one bad frame, not a dead loop.
		/// </summary>
		/// <remarks>
		/// <para><b>Synchronous all the way through <see cref="BrowserWebGpuLayer.EndFrame"/>, deliberately.</b>
		/// The frame is presented by the page when the animation frame callback returns, so anything awaited in
		/// here would resume <i>after</i> that implicit present - drawing into a texture that is no longer the
		/// canvas's. The one thing that legitimately outlives the frame is a read-back, and its copy is
		/// recorded in-frame for exactly this reason (see <c>WebGpuRenderDevice.ReadTextureAsync</c>).</para>
		/// <para><b>The finally is the load-bearing part.</b> An acquired swapchain texture must not survive
		/// this method however it ends; see <see cref="BrowserWebGpuLayer"/>'s class remarks.</para>
		/// </remarks>
		private void PaintFrame()
		{
			FrameProfiler.BeginFrame();

			this.isInsidePaint = true;

			try
			{
				Graphics2D graphics2D;
				using (FrameProfiler.Time("NewGraphics2D+Acquire"))
				{
					graphics2D = this.NewGraphics2D();
				}

				using (FrameProfiler.Time("WidgetTreeDraw"))
				{
					if (SingleWindowMode && this.WindowProvider != null)
					{
						// Every window this provider hosts is drawn into this one frame: the shell first, then -
						// for each dialog stacked on it - a scrim over the whole frame and the dialog on top of
						// that. Drawing only the active window would leave a dialog floating on an empty
						// background. Kept identical to the mac and Windows hosts.
						IReadOnlyList<SystemWindow> openWindows = this.WindowProvider.OpenWindows;
						for (int i = 0; i < openWindows.Count; i++)
						{
							graphics2D.FillRectangle(openWindows[0].LocalBounds, new Color(Color.Black, 160));
							openWindows[i].OnDraw(graphics2D);
						}
					}
					else
					{
						// OnDrawBackground before OnDraw, the way a parent calls into a child in GuiWidget.
						this.aggSystemWindow.OnDrawBackground(graphics2D);
						this.aggSystemWindow.OnDraw(graphics2D);
					}
				}

				// A widget that rasterized into Graphics2D.DestImage drew into a CPU buffer, not into the
				// frame. On a GPU surface that buffer is a layer this uploads and draws over the frame now,
				// after every widget has had its turn.
				if (graphics2D is Graphics2DGpu gpuGraphics && gpuGraphics.HasCpuLayer)
				{
					FrameProfiler.Count("CompositeCpuLayer");
					using (FrameProfiler.Time("CompositeCpuLayer"))
					{
						gpuGraphics.CompositeCpuLayer();
					}
				}

				// W4 S5: any pendingScreenshotPath is read back here - after every widget has drawn and
				// before EndFrame, the only window in which the frame's texture exists and is still
				// readable. Until then a capture request reaches its timeout; see CaptureScreenshotAsync.
			}
			finally
			{
				this.isInsidePaint = false;
				this.viewPortHasBeenSet = false;

				// Unconditional, and the reason this method has a finally at all: the swapchain texture this
				// frame acquired is only valid inside this animation frame task, and one that escapes is
				// handed back to every later frame by WebGpuSurfaceTarget's cache. So the frame is presented
				// if it can be and abandoned if it cannot, but it always ends here.
				this.renderLayer?.EndFrame();

				FrameProfiler.EndFrame();
			}
		}

		/// <summary>
		/// Begins the frame and puts the compat context into the state a frame starts in: full-canvas
		/// viewport and scissor, identity transforms, and a cleared frame. The mac host's, unchanged apart
		/// from taking its size from the canvas backing store.
		/// </summary>
		/// <remarks>
		/// The recursion through <see cref="NewGraphics2D"/> at the end is not accidental: clearing is a draw
		/// like any other, and <see cref="viewPortHasBeenSet"/> is set before it so that call does not come
		/// straight back here.
		/// </remarks>
		private void SetAndClearViewPort()
		{
			this.renderLayer.BeginFrame();

			IGpuContext gl = this.renderLayer.Gl?.GpuContext;
			if (gl == null)
			{
				return;
			}

			gl.Viewport(0, 0, (int)this.backing.PixelWidth, (int)this.backing.PixelHeight);
			this.viewPortHasBeenSet = true;

			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();

			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
			gl.Scissor(0, 0, (int)this.backing.PixelWidth, (int)this.backing.PixelHeight);

			this.NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}

		// -----------------------------------------------------------------------------------------
		// Screenshots
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Gives up, quietly, and says so on stderr.
		/// </summary>
		/// <remarks>
		/// This host cannot honour the synchronous contract and never will: the file only exists once a frame
		/// has been drawn and read back, both of which need the one thread this call would have to block. So it
		/// takes the give-up <see cref="IPlatformWindow.CaptureScreenshot"/>'s remarks allow rather than
		/// deadlocking the page - callers that must have the file check for it, and callers that can await use
		/// <see cref="CaptureScreenshotAsync"/>, which is why that overload exists.
		/// </remarks>
		public void CaptureScreenshot(string path)
		{
			Console.Error.WriteLine(
				$"BrowserSystemWindow cannot capture '{path}' synchronously - a frame cannot be drawn while this "
				+ "call holds the only thread. Use CaptureScreenshotAsync. No file was written.");
		}

		/// <summary>
		/// Queues a screenshot request for the end of a frame and waits for the frame that serves it.
		/// </summary>
		/// <remarks>
		/// The machinery is <c>MacSystemWindow</c>'s, minus its marshalling: there is one thread here, so there
		/// is no "am I on the UI thread?" question to get wrong. What is not here yet is the read-back itself -
		/// the frames are real now, but nothing in <see cref="PaintFrame"/> serves the pending path (W4 S5) -
		/// so today every capture reaches the timeout and gives up quietly, which is exactly what
		/// <see cref="IPlatformWindow.CaptureScreenshotAsync"/>'s remarks promise a host that cannot produce a
		/// frame. That path stays the real one afterwards, not a placeholder: a browser tab that is hidden
		/// stops receiving animation frames, so a capture that never gets one is a permanent case.
		/// </remarks>
		/// <param name="path">Where to write the PNG.</param>
		public async Task CaptureScreenshotAsync(string path)
		{
			if (this.hasClosed)
			{
				return;
			}

			if (this.pendingScreenshotPath != null || this.screenshotCompletion != null)
			{
				// One pending path and one completion is all the frame machinery has, so a second request would
				// steal this one's frame. Failing loudly beats one caller silently receiving the other's frame.
				throw new InvalidOperationException(
					$"A screenshot capture is already pending (to '{this.pendingScreenshotPath}'); only one capture "
					+ $"can be in flight at a time. Requested '{path}'.");
			}

			var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			this.pendingScreenshotPath = path;
			this.screenshotCompletion = completion;

			try
			{
				// Ask for the frame that will serve the request. There is nothing to force here the way the
				// desktop hosts force a paint: a frame only happens when the browser schedules one.
				this.frameTick.Invalidate();

				// A capture that ran and failed faults here, on purpose: the contract is that the file exists
				// once this completes, so a swallowed failure would be a lie.
				await completion.Task.WaitAsync(CaptureTimeout);
			}
			catch (TimeoutException)
			{
				// The quiet give-up the interface allows - a window that never painted must not hang a caller.
			}
			finally
			{
				// Only clear what still belongs to this request: a continuation can resume well after the
				// request was given up on, by which point the fields may already be the next request's.
				if (ReferenceEquals(this.screenshotCompletion, completion))
				{
					this.pendingScreenshotPath = null;
					this.screenshotCompletion = null;
				}
			}
		}
	}
}
