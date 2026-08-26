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
using System.Diagnostics;
using System.Text;
using System.Threading;
using MatterHackers.Agg.Platform.Linux;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The Linux/X11 window host: a plain <c>InputOutput</c> X11 window, a <see cref="X11WebGpuLayer"/> for
	/// the Vulkan swapchain over it, a <see cref="Graphics2DGpu"/> over its GL facade for widget paint, and
	/// one present per frame. The structural counterpart of <c>MacSystemWindow</c> on macOS and of
	/// <c>WinformsSystemWindow</c> + <c>WebGpuSystemWindow</c> on Windows, with X11 reached through raw
	/// P/Invoke into <c>libX11.so.6</c> - no GTK, no SDL, no GLFW.
	///
	/// <para>
	/// <b>Coordinates and DPI: agg pixels = X pixels.</b> Unlike AppKit (points x backingScaleFactor) and
	/// unlike a DPI-aware Win32 window, X11 has no logical coordinate system at all. A window's size, an
	/// event's position and the drawable are all the same pixels, so the conversion this seam has to perform
	/// is the identity: <see cref="SystemWindow.Width"/> is the X11 width, and the swapchain is that size.
	/// What X11 <em>does</em> have is a user scaling preference, which desktop environments record as the
	/// <c>Xft.dpi</c> X resource; that is read here purely to be reported through
	/// <see cref="SystemWindow.SetDisplayScale"/> - it never scales a coordinate. As on macOS,
	/// <c>GuiWidget.DeviceScale</c> is deliberately not touched: it is a user text-size preference, not a
	/// DPI factor.
	/// </para>
	///
	/// <para>
	/// <b>Y flip.</b> X11's origin is top-left with Y increasing downwards, which is Win32's convention and
	/// not agg's. Mouse Y therefore has to be flipped on the way in, exactly as <c>WinformsEventSink</c>
	/// does; that lives with the rest of the input translation (step 3b) and is called out here so the
	/// absence of a flip in <c>MacSystemWindow</c> is not copied by mistake.
	/// </para>
	///
	/// <para>
	/// <b>The loop is ours, and it is also the idle timer.</b> <see cref="RunEventLoop"/> drains the X queue,
	/// drains the RunOnIdle queue, paints whatever asked to be repainted, and then - having nothing to do -
	/// sleeps in <c>poll(2)</c> on the X connection for at most <see cref="IdlePumpMilliseconds"/>. On macOS
	/// that idle drain has to be a real <c>NSTimer</c>, because AppKit runs nested tracking loops (window
	/// drag, live resize, menu tracking) in which the host's own pump is frozen and queued layout would never
	/// run - a window with no idle pump comes up blank. X11 has no such nested native loop: a resize is a
	/// stream of <c>ConfigureNotify</c> events delivered to this same queue and a menu is drawn by the
	/// application, so this loop is never frozen and it can be the idle timer itself. The <=4ms poll timeout
	/// is what makes it one.
	/// </para>
	///
	/// <para>
	/// <b>One thread, not "the main thread".</b> Xlib is thread-safe only after <c>XInitThreads</c>, which is
	/// deliberately not called (see <see cref="Xlib"/>); in exchange every Xlib call must come from the one
	/// thread that pumps the connection. That is a weaker rule than AppKit's, which is why
	/// <c>MainThreadDispatcher.MainThreadRequired</c> is false off macOS and why nothing here is wrapped in
	/// <c>MainThreadDispatcher.Invoke</c>. <see cref="RunEventLoop"/> still calls
	/// <c>MainThreadDispatcher.DrainPending</c>, which costs nothing when unhosted and keeps test hosting
	/// working.
	/// </para>
	/// </summary>
	public class X11SystemWindow : IPlatformWindow
	{
		/// <summary>
		/// How many pump iterations <see cref="CaptureScreenshot"/> spins waiting for a capture whose
		/// read-back suspended. Bounded so a window that never repaints cannot hang the caller; the native
		/// read-back path completes inline and never reaches the loop.
		/// </summary>
		private const int ScreenshotPumpSpins = 200;

		/// <summary>
		/// How long <see cref="RunEventLoop"/> is willing to sleep with nothing to do. Doubles as the idle
		/// tick: see the class remarks for why X11 needs no separate timer. Short enough that input latency
		/// stays well under a frame, long enough that an idle window is not a CPU spin.
		/// </summary>
		private const int IdlePumpMilliseconds = 4;

		/// <summary>
		/// How long after a size change another one still counts as part of the same resize burst.
		/// </summary>
		/// <remarks>
		/// A window manager delivers a drag of a window edge as a stream of <c>ConfigureNotify</c> events at
		/// roughly pointer-report rate - 5 to 16ms apart on everything measured - so 50ms is several times
		/// the gap this has to bridge while staying far below the pause between two deliberate resizes. The
		/// original 250ms did not discriminate: it is long enough that the configures a window emits while
		/// it is being mapped and settled all fall inside one window, which is exactly the case that must
		/// <em>not</em> paint synchronously. Widening it further trades the same way, which is why the show
		/// gate in <see cref="ShouldPaintSynchronouslyForResize"/> carries that case instead of the timer.
		/// </remarks>
		private const int ResizeBurstMilliseconds = 50;

		/// <summary>The DPI X11 and every toolkit on it treat as unscaled.</summary>
		private const double BaselineDpi = 96.0;

		/// <summary>
		/// How long after a press another press on the same button at the same spot still counts as part of
		/// the same click. 500ms is Win32's <c>GetDoubleClickTime</c> default and X11's own convention -
		/// there is no server-side setting to ask, so every toolkit hard-codes something near it.
		/// </summary>
		private const ulong DoubleClickMilliseconds = 500;

		/// <summary>
		/// How far the pointer may drift between two presses and still be the same click, in pixels. A hand
		/// resting on a mouse moves it a pixel or two between clicks of a real double click; more than this
		/// and the user meant two clicks in two places.
		/// </summary>
		private const int DoubleClickSlopPixels = 4;

		/// <summary>
		/// The wheel units one detent is worth. Win32's v120 convention, which every agg consumer was
		/// written against (MatterCAD's trackball zooms by <c>WheelDelta / 120</c> steps). X11 has no
		/// magnitude to carry: a detent is a button press, so it is exactly one notch and nothing else.
		/// </summary>
		private const int WheelDeltaPerDetent = 120;

		/// <summary>The complete set of down-state keys <see cref="ModifierDownStateKeys"/> can report, so
		/// one loop can set and clear all of them.</summary>
		private static readonly Keys[] ModifierStateKeys = { Keys.ShiftKey, Keys.ControlKey, Keys.Menu };

		/// <summary>Holding nothing - the starting value for <see cref="appliedModifierKeys"/>.</summary>
		private static readonly IReadOnlySet<Keys> NoModifierKeys = new HashSet<Keys>();

		private static readonly object StaticInitLock = new object();

		/// <summary>Every constructed window that has not closed yet, in creation order.</summary>
		private static readonly List<X11SystemWindow> LiveWindows = new List<X11SystemWindow>();

		/// <summary>Font-cursor shape id to the cursor made from it. Cursors are per-display and immutable,
		/// so one per shape for the life of the process is all that is ever needed.</summary>
		private static readonly Dictionary<uint, ulong> ResolvedCursors = new Dictionary<uint, ulong>();

		// --- Unattended smoke runs -------------------------------------------------------------------
		// Read once, from the environment, because the point is to drive an *unmodified* demo: no demo has
		// to know it is being smoke tested, and with the variables unset none of this does anything. Kept
		// byte for byte compatible with the WinForms and mac hosts' versions so one AGG_SMOKE_* invocation
		// drives any of the three.
		private static readonly int SmokeFrameTarget = ParseSmokeFrames();
		private static readonly string SmokeScreenshotPath = Environment.GetEnvironmentVariable("AGG_SMOKE_SCREENSHOT");

		private static System.Threading.Timer smokeExitWatchdog;

		/// <summary>
		/// The one connection to the X server every window in this process shares, opened lazily by the
		/// first window. One display rather than one per window because the event queue is per connection:
		/// two connections would need two pumps, and the second window's events would be invisible to the
		/// loop the first window is running.
		/// </summary>
		private static IntPtr display;

		private static int screenNumber;
		private static ulong rootWindow;
		private static bool displayBootstrapped;

		/// <summary>The managed thread that opened <see cref="display"/>, and so the only one allowed to
		/// use it. -1 until <see cref="BootstrapDisplay"/> has run.</summary>
		private static int displayThreadId = -1;

		// The interned atoms. All are per-display, so they are interned once alongside it.
		private static ulong wmProtocolsAtom;
		private static ulong wmDeleteWindowAtom;
		private static ulong netWmNameAtom;
		private static ulong utf8StringAtom;
		private static ulong netWmStateAtom;
		private static ulong netWmStateMaximizedHorzAtom;
		private static ulong netWmStateMaximizedVertAtom;
		private static ulong netFrameExtentsAtom;

		/// <summary>
		/// The installed protocol-error handler, held in a static field for exactly one reason: nothing on
		/// the native side roots a managed delegate, and a collected one turns the next BadWindow into a
		/// jump to freed memory. Same for <see cref="ioErrorHandler"/>.
		/// </summary>
		private static Xlib.XErrorHandler protocolErrorHandler;

		private static Xlib.XIOErrorHandler ioErrorHandler;

		/// <summary>
		/// Whether a window is currently running <see cref="RunEventLoop"/>. This, rather than a
		/// "first window" latch, is what decides whether a window being shown owns the loop: a latch has to
		/// be reset between runs and gets the answer wrong for any window shown before the application's
		/// main one. Same reasoning as <c>MacSystemWindow</c>'s.
		/// </summary>
		private static volatile bool runLoopActive;

		private static bool processingOnIdle;

		private ulong window;

		/// <summary>The cursor currently defined on the window, so re-asserting the same one is free.</summary>
		private ulong currentCursor;

		private X11WebGpuLayer webGpuLayer;
		private SystemWindow aggSystemWindow;

		/// <summary>
		/// The user's display scaling, reported to the application and never applied to a coordinate. See
		/// the class remarks for why those are two different things on X11.
		/// </summary>
		private double displayScale = 1;

		private uint pixelWidth = 1;
		private uint pixelHeight = 1;

		private string caption = string.Empty;
		private Vector2 minimumSize;

		private bool needsRedraw = true;
		private bool viewPortHasBeenSet;
		private bool isInsidePaint;
		private bool hasClosed;

		/// <summary>Set while an X11-initiated close is running, so the agg close does not re-enter it.</summary>
		private bool platformAlreadyClosing;

		/// <summary>
		/// True once <see cref="ShowSystemWindow"/> has finished mapping and settling this window. Until
		/// then the configures arriving are the show sequence's own, not a user resizing anything - see
		/// <see cref="ShouldPaintSynchronouslyForResize"/>.
		/// </summary>
		private bool showCompleted;

		/// <summary>
		/// When the last size change arrived, on <see cref="Stopwatch"/>'s monotonic clock. See
		/// <see cref="ResizeBurstMilliseconds"/> and <see cref="ShouldPaintSynchronouslyForResize"/>.
		/// </summary>
		private long lastResizeTimestamp = long.MinValue;

		/// <summary>Which buttons this window owns for the duration of a drag; see <see cref="OutOfViewMouseCapture"/>.</summary>
		private readonly OutOfViewMouseCapture mouseCapture = new OutOfViewMouseCapture();

		/// <summary>Turns a stream of button presses into single, double and triple clicks.</summary>
		private readonly ClickCounter clickCounter = new ClickCounter();

		/// <summary>What <see cref="SetModifierKeys"/> was last told; see <see cref="ModifierKeys"/>.</summary>
		private Keys overrideModifierKeys = Keys.None;

		private bool modifiersOverridden;

		/// <summary>
		/// The modifier keys this window put into <see cref="Keyboard"/>'s down state, so focus loss can
		/// release exactly those and nothing else. See <see cref="ReleaseAppliedModifierKeys"/>.
		/// </summary>
		private IReadOnlySet<Keys> appliedModifierKeys = NoModifierKeys;

		/// <summary>
		/// The modifier half of the last input event's <c>state</c> word, corrected for the event itself
		/// when that event was a modifier key. What <see cref="ModifierKeys"/> answers from.
		/// </summary>
		private uint lastModifierState;

		/// <summary>Whether <see cref="Xlib.XGrabPointer"/> is currently held for a drag.</summary>
		private bool pointerGrabbed;

		private int drawCount;
		private bool smokeRunFinished;

		/// <summary>
		/// A screenshot asked for but not taken yet. The read-back has to happen at the end of a frame,
		/// so a request made at any other time waits here for one.
		/// </summary>
		private string pendingScreenshotPath;

		/// <summary>Signalled by the paint that performs a queued capture, so the requester can return only
		/// once the file is on disk.</summary>
		private ManualResetEventSlim screenshotComplete;

		public X11SystemWindow()
		{
			BootstrapDisplay();

			lock (StaticInitLock)
			{
				LiveWindows.Add(this);
			}
		}

		/// <summary>
		/// How many frames a smoke run draws before it screenshots and closes itself
		/// (<c>AGG_SMOKE_FRAMES</c>), or 0 when the window should behave normally.
		/// </summary>
		public static int SmokeFrames => SmokeFrameTarget;

		/// <summary>
		/// The shared connection, or <see cref="IntPtr.Zero"/> when no window has opened one yet. What
		/// <see cref="X11Selection"/> hangs the clipboard off, and what tells it there is no X11 to talk to
		/// - a headless test process, where the clipboard falls back to in-process behaviour.
		/// </summary>
		internal static IntPtr SharedDisplay => display;

		/// <summary>The root window of the default screen, for a child that belongs to no window of ours.</summary>
		internal static ulong SharedRootWindow => rootWindow;

		/// <summary>
		/// Whether the calling thread is the one that owns the X connection. False when there is no
		/// connection at all, so it doubles as "is there an X11 to talk to from here".
		/// </summary>
		internal static bool OnDisplayThread
			=> display != IntPtr.Zero && Environment.CurrentManagedThreadId == displayThreadId;

		/// <summary>
		/// Whether a connection is open at all, from whatever thread. What separates "this process is
		/// headless, so the in-process clipboard is the whole story" from "there is a real X clipboard and
		/// this caller merely has to get onto the right thread to reach it" - two cases
		/// <see cref="OnDisplayThread"/> alone cannot tell apart.
		/// </summary>
		internal static bool HasDisplay => display != IntPtr.Zero;

		/// <summary>
		/// Whether every agg window in the process shares this one native window, dialogs included.
		/// </summary>
		/// <remarks>
		/// What an application shell like MatterCAD runs on. <see cref="SingleWindowProvider"/> wraps
		/// everything shown after the first window in a <c>WindowWidget</c>, draws it inside the window
		/// already on screen, and then hands that wrapper to this same <see cref="IPlatformWindow"/>.
		/// Without this flag the second call reads as "the window you are already showing asked to be
		/// raised" and the dialog is never drawn. The WinForms and mac hosts carry the identical flag for
		/// the identical reason; a provider that gives every window its own native window (agg's own
		/// <see cref="WebGpuX11WindowProvider"/>) leaves it alone.
		/// </remarks>
		public static bool SingleWindowMode { get; set; }

		/// <summary>
		/// The single consumption point of <see cref="SystemWindow.UseGpu"/>, which is seeded from
		/// RootSystemWindow.DefaultUseGpu by the FORCE_SOFTWARE_RENDERING command-line flag.
		/// </summary>
		public static bool ShouldUseSoftwareAdapter(SystemWindow systemWindow) => systemWindow?.UseGpu == false;

		/// <summary>The SystemWindow this platform window is currently showing.</summary>
		public SystemWindow AggSystemWindow => this.aggSystemWindow;

		/// <summary>The wgpu host that owns the device and swapchain.</summary>
		public X11WebGpuLayer WebGpuLayer => this.webGpuLayer;

		/// <summary>The provider that created this window, set by the provider itself.</summary>
		public ISystemWindowProvider WindowProvider { get; set; }

		/// <summary>
		/// The X11 window XID, widened into an <see cref="IntPtr"/> so this type's public surface matches
		/// the other hosts' <c>WindowHandle</c>. Diagnostics and tests. An XID is a 32-bit server-side id
		/// carried in an <c>unsigned long</c>, so nothing is lost.
		/// </summary>
		public IntPtr WindowHandle => (IntPtr)this.window;

		/// <summary>The shared <c>Display*</c>, or zero before the first window opened it. Diagnostics.</summary>
		public IntPtr DisplayHandle => display;

		/// <summary>What the renderer has to complain about, or null when it is happy.</summary>
		public string RenderErrorReport => this.webGpuLayer?.LastError;

		/// <summary>Which backend, and how many frames actually reached the screen.</summary>
		public string RenderStatusReport
		{
			get
			{
				var layer = this.webGpuLayer;
				if (layer?.Device == null)
				{
					return "webgpu not initialized";
				}

				return $"{layer.BackendType} {layer.Device.AdapterName}, presented {layer.Surface?.PresentedFrameCount ?? 0}";
			}
		}

		/// <summary>
		/// The window title. Written to both <c>WM_NAME</c> and <c>_NET_WM_NAME</c>: the first is ICCCM and
		/// is Latin-1 only, the second is the EWMH UTF-8 replacement every modern window manager prefers.
		/// Setting only one gets a title that is either mojibake or missing depending on which manager the
		/// user runs.
		/// </summary>
		public string Caption
		{
			get => this.caption;

			set
			{
				this.caption = value ?? string.Empty;
				if (this.window != X11.None)
				{
					this.ApplyCaption();
				}
			}
		}

		/// <summary>
		/// The height of the window manager's title bar, in agg pixels (= X pixels), or zero when there is
		/// none to measure.
		/// </summary>
		/// <remarks>
		/// From <c>_NET_FRAME_EXTENTS</c>, which is the only way to ask: the decorations belong to the
		/// window manager, live on a frame window this client does not own, and are not part of this
		/// window's geometry at all. A manager that does not publish the property - and a bare X server with
		/// no manager running, which is what an Xvfb smoke run is - has no title bar, so zero is the honest
		/// answer rather than a fallback.
		/// </remarks>
		public int TitleBarHeight => this.ReadFrameExtents(out _, out int top) ? top : 0;

		/// <summary>
		/// The window's top-left corner in desktop space: device pixels with the origin at the top-left of
		/// the screen, which is X11's own convention and needs no conversion.
		/// </summary>
		/// <remarks>
		/// The position is the <em>frame's</em>, not this window's. Under a reparenting window manager -
		/// which is nearly all of them - this window is a child of a frame window that carries the
		/// decorations, so <c>XTranslateCoordinates</c> reports a point inset by the frame's border and
		/// title bar. Subtracting <c>_NET_FRAME_EXTENTS</c> undoes that, which is what makes the getter the
		/// inverse of the setter: ICCCM says a <c>XMoveWindow</c> on a reparented top-level is a request to
		/// place the frame, not the client.
		/// </remarks>
		public Point2D DesktopPosition
		{
			get
			{
				if (this.window == X11.None || display == IntPtr.Zero)
				{
					return new Point2D(0, 0);
				}

				Xlib.XTranslateCoordinates(display, this.window, rootWindow, 0, 0, out int x, out int y, out _);

				if (this.ReadFrameExtents(out int left, out int top))
				{
					x -= left;
					y -= top;
				}

				return new Point2D(x, y);
			}

			set
			{
				if (this.window == X11.None || display == IntPtr.Zero)
				{
					return;
				}

				Xlib.XMoveWindow(display, this.window, value.x, value.y);
				Xlib.XFlush(display);
			}
		}

		/// <summary>
		/// The smallest the window may be made, in agg pixels. Published to the window manager as the
		/// <c>PMinSize</c> half of <c>WM_NORMAL_HINTS</c>; a manager is free to ignore it, and a bare X
		/// server with no manager always does.
		/// </summary>
		public Vector2 MinimumSize
		{
			get => this.minimumSize;

			set
			{
				this.minimumSize = value;
				if (this.window != X11.None)
				{
					this.ApplySizeHints(null);
				}
			}
		}

		/// <summary>The modifier keys held right now.</summary>
		/// <remarks>
		/// Reports whatever <see cref="SetModifierKeys"/> was last told, once it has been told anything - a
		/// simulated Ctrl-click has no real key held, so reading the real keyboard would report None and
		/// every modifier-sensitive interaction in an automated run would behave as an unmodified one.
		/// <para/>
		/// Otherwise it reports the modifier half of the last input event's <c>state</c> word rather than
		/// asking the server. That is a deliberate difference from the mac host, which polls
		/// <c>+[NSEvent modifierFlags]</c> here: the X11 equivalent is <c>XQueryPointer</c>, and every Xlib
		/// call has to come from the thread that owns the display (see <see cref="Xlib"/>), while this
		/// property is read from wherever a widget happens to be. The remembered word costs nothing, is
		/// thread safe, and is stale only for the moment between a modifier being pressed and the next event
		/// - and a bare modifier press is itself an event, so that window is empty in practice.
		/// <see cref="HandleFocusGained"/>, which does run on the pump thread, is where the live state is
		/// read instead.
		/// </remarks>
		public Keys ModifierKeys => this.modifiersOverridden
			? this.overrideModifierKeys
			: TranslateModifiers(this.lastModifierState);

		/// <summary>
		/// Declares which modifier keys a synthetic input event is holding, so <see cref="ModifierKeys"/>
		/// reports them instead of the (empty) real keyboard state.
		/// </summary>
		/// <remarks>
		/// Found by name and by reflection from <c>AggInputMethods.TrySetModifierKeys</c>, which is why it
		/// is internal and not on <see cref="IPlatformWindow"/> - the name and the visibility are both part
		/// of the contract and cannot be changed without silently dropping the modifiers off every synthetic
		/// click in the automation suite. Both other hosts have the same method for the same caller. Once
		/// called, the real keyboard is never read again: an automated run has no user at the keyboard, so
		/// there is nothing to fall back to.
		/// </remarks>
		internal void SetModifierKeys(Keys modifiers)
		{
			this.overrideModifierKeys = modifiers;
			this.modifiersOverridden = true;
		}

		/// <summary>Raises the window above its siblings, without taking focus from another application.</summary>
		public void BringToFront()
		{
			if (this.window != X11.None && display != IntPtr.Zero)
			{
				Xlib.XRaiseWindow(display, this.window);
				Xlib.XFlush(display);
			}
		}

		/// <summary>Raises the window and gives it the keyboard.</summary>
		/// <remarks>
		/// <c>RevertToParent</c> so that when this window goes away the focus falls back to whatever
		/// contains it rather than to <c>None</c> - focus on None makes the keyboard dead for every client
		/// until something claims it.
		/// </remarks>
		public void Activate()
		{
			if (this.window != X11.None && display != IntPtr.Zero)
			{
				Xlib.XRaiseWindow(display, this.window);
				Xlib.XSetInputFocus(display, this.window, X11.RevertToParent, X11.CurrentTime);
				Xlib.XFlush(display);
			}
		}

		/// <summary>
		/// Schedules a repaint. X11 has an Expose event but no WM_PAINT-style "please redraw" the client can
		/// post to itself, so this is a flag the pumped loop reads; the rectangle is ignored because the
		/// whole frame is redrawn either way.
		/// </summary>
		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			this.needsRedraw = true;
		}

		/// <summary>
		/// Asks for this window to close, the same way the window manager's close button does: by sending
		/// itself a <c>WM_DELETE_WINDOW</c> client message.
		/// </summary>
		/// <remarks>
		/// Routing through the protocol rather than calling the teardown directly is what keeps one close
		/// path: whether the user pressed the frame's X or the application called this, the same
		/// <see cref="HandlePlatformCloseRequest"/> runs and the same <c>OnShouldClose</c> is asked. The
		/// message is only delivered by the pump, though, so a close with no loop running would otherwise
		/// never happen - hence the inline fallback.
		/// </remarks>
		public void Close()
		{
			if (this.hasClosed || this.window == X11.None || display == IntPtr.Zero)
			{
				return;
			}

			if (runLoopActive)
			{
				this.SendCloseRequestToSelf();
				return;
			}

			this.HandleCloseRequest();
		}

		/// <summary>
		/// Points the window's cursor at one of the standard "cursor" font shapes.
		/// </summary>
		/// <remarks>
		/// X11 needs nothing like AppKit's cursor rects: <c>XDefineCursor</c> is a property of the window
		/// itself, so the server shows this cursor for as long as the pointer is over it and nothing else
		/// can put it back. The eight pan directions and the "no move" cursors have no cursorfont
		/// equivalent, so they fall back to the arrow rather than being faked with something misleading.
		/// </remarks>
		public void SetCursor(Cursors cursorToSet)
		{
			uint shape = cursorToSet switch
			{
				Cursors.IBeam => X11.XC_xterm,
				Cursors.Hand => X11.XC_hand2,
				Cursors.Cross => X11.XC_crosshair,
				Cursors.Help => X11.XC_question_arrow,
				Cursors.WaitCursor => X11.XC_watch,
				Cursors.SizeAll => X11.XC_fleur,

				// A split bar is dragged along one axis, which is the same gesture - and in every toolkit
				// the same cursor - as a window edge on that axis.
				Cursors.SizeWE or Cursors.VSplit => X11.XC_sb_h_double_arrow,
				Cursors.SizeNS or Cursors.HSplit => X11.XC_sb_v_double_arrow,

				// cursorfont has no free-floating diagonal arrows, only the four named window corners. The
				// bottom pair point the right way for the one place agg asks: a window-widget corner grip.
				Cursors.SizeNWSE => X11.XC_bottom_right_corner,
				Cursors.SizeNESW => X11.XC_bottom_left_corner,

				_ => X11.XC_arrow,
			};

			if (this.window == X11.None || display == IntPtr.Zero)
			{
				return;
			}

			ulong cursor = ResolveCursor(shape);
			if (cursor == X11.None || cursor == this.currentCursor)
			{
				return;
			}

			this.currentCursor = cursor;
			Xlib.XDefineCursor(display, this.window, cursor);
			Xlib.XFlush(display);
		}

		public Graphics2D NewGraphics2D()
		{
			if (this.webGpuLayer?.Gl == null)
			{
				// Without this the caller gets a bare NullReferenceException out of Graphics2DGpu and no
				// hint at all that the real problem is a window painting before its wgpu device exists.
				throw new InvalidOperationException(
					"The WebGPU device is not initialized, so this window cannot make a Graphics2D. "
					+ "InitializeWebGpu runs from ShowSystemWindow; reaching a paint before that happened "
					+ "means the window was never shown or its initialization threw.");
			}

			if (!this.viewPortHasBeenSet)
			{
				this.SetAndClearViewPort();
			}

			Graphics2D graphics2D = new Graphics2DGpu(
				this.webGpuLayer.Gl,
				(int)this.pixelWidth,
				(int)this.pixelHeight,
				GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		/// <summary>
		/// Connects a <see cref="SystemWindow"/> to this platform window, creates the X11 window and its
		/// wgpu device, maps it, and - unless a window is already running the loop - runs the event loop
		/// until that window closes. The blocking shape is deliberate: it is what <c>Application.Run</c>
		/// does on Windows, and every agg demo's <c>Main</c> depends on <c>ShowAsSystemWindow</c> not
		/// returning until the app is done.
		/// </summary>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			if (systemWindow.PlatformWindow == this)
			{
				// In single window mode the provider points a window at this one before showing it, so
				// "already mine" means "start drawing this instead", not "raise what is already up".
				if (SingleWindowMode && this.window != X11.None)
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

			this.CreateNativeWindow(systemWindow);

			this.webGpuLayer.UseSoftwareAdapter = ShouldUseSoftwareAdapter(systemWindow);

			// The swapchain can drop a frame for something that clears itself; this is how it asks the
			// pumped loop for another paint instead of leaving the window on its last presented frame.
			this.webGpuLayer.RequestRedraw = () => this.needsRedraw = true;
			this.webGpuLayer.InitializeWebGpu();

			// Also seeds SystemWindow.DisplayScale, since aggSystemWindow is already attached. On an
			// unscaled desktop that matches the default and says nothing; on a scaled one it queues a single
			// DisplayScaleChanged for the first idle tick, which happens before anything is on screen.
			this.SyncSizeFromWindow();

			// Mapping is a request, not a state change: the window is not on screen until the server (and,
			// when there is one, the window manager) has processed it and sent back the MapNotify and the
			// first ConfigureNotify. Pumping the queue here lets those be handled - and the initial geometry
			// picked up - before the first frame is drawn into a swapchain sized from a guess.
			for (int settle = 0; settle < 10; settle++)
			{
				PumpEvents();
				Thread.Sleep(10);
			}

			this.needsRedraw = true;

			// From here on a configure is somebody resizing the window, not this method sizing it - which is
			// what lets a resize burst paint itself. See ShouldPaintSynchronouslyForResize.
			this.showCompleted = true;

			// Whoever finds no loop running owns it. A window shown from inside the loop (a dialog, a second
			// window) finds one and returns, which is the non-blocking Show every platform gives it.
			if (!runLoopActive)
			{
				RunEventLoop();
			}
		}

		/// <summary>
		/// Tears this platform window down in response to the agg window closing. Called by the provider
		/// from <see cref="SystemWindow.OnClosed"/>.
		/// </summary>
		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// X11 is already closing us (the user hit the frame's close button); letting the agg close drive
			// a second close would re-enter the teardown.
			if (this.platformAlreadyClosing)
			{
				return;
			}

			// In single window mode a dialog lives inside this window, so closing one is only a matter of
			// going back to drawing whatever the provider now has on top. Only the shell - the window the
			// provider is left holding - takes the native window down with it.
			if (SingleWindowMode
				&& this.window != X11.None
				&& this.WindowProvider?.TopWindow != null
				&& this.WindowProvider.TopWindow != systemWindow)
			{
				this.SetActiveAggWindow(this.WindowProvider.TopWindow);
				return;
			}

			this.DestroyNativeWindow();
		}

		/// <summary>
		/// Makes <paramref name="systemWindow"/> the window this native window draws and routes input to.
		/// Creates nothing native - only single window mode reaches it, see <see cref="SingleWindowMode"/>.
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

				// SetBoundsFromPlatform rather than LocalBounds so a minimum sized for another display cannot
				// lay the window out larger than the drawable it is about to be drawn into.
				systemWindow.SetBoundsFromPlatform(this.pixelWidth, this.pixelHeight);
				systemWindow.SetDisplayScale(this.displayScale);
				systemWindow.SetDisplayUsableSize(this.MeasureUsableScreenSize());
			}

			systemWindow.Invalidate();
			this.needsRedraw = true;
		}

		/// <summary>
		/// Reads the frame back through wgpu. No <c>System.Drawing</c> anywhere on the path: the pixels go
		/// through agg's own <c>ImageBuffer</c>/<c>ImageIO</c>.
		/// </summary>
		/// <remarks>
		/// The read-back can only happen at the end of a frame - that is the only moment a swapchain
		/// texture exists and is still readable - so the request is queued and a paint forced, and this
		/// call does not return until that paint has written the file. Callers (failure diagnostics, the
		/// automation harness) treat <c>CaptureScreenshot</c> as "the PNG exists when I get control back",
		/// which is the contract every other <c>IPlatformWindow</c> gives them.
		/// </remarks>
		/// <param name="path">Where to write the PNG.</param>
		public void CaptureScreenshot(string path)
		{
			if (this.webGpuLayer == null || this.webGpuLayer.IsDisposed)
			{
				return;
			}

			if (!UiThread.IsUiThread)
			{
				// Every Xlib call has to come from the thread that owns the display, so the request is
				// marshalled the only way this host has: through the idle queue the event loop drains.
				using (var done = new ManualResetEventSlim(false))
				{
					UiThread.RunOnIdle(() =>
					{
						try
						{
							this.CaptureScreenshot(path);
						}
						finally
						{
							done.Set();
						}
					});

					done.Wait(TimeSpan.FromSeconds(10));
				}

				return;
			}

			if (this.isInsidePaint)
			{
				// The smoke-run path asks from inside the paint, just before the present that would consume
				// the request. Forcing another paint from here would re-enter the frame; queuing is enough,
				// because this frame is about to reach PresentOrCapture anyway.
				this.pendingScreenshotPath = path;
				return;
			}

			this.pendingScreenshotPath = path;
			this.screenshotComplete = new ManualResetEventSlim(false);

			try
			{
				this.PaintFrame();

				// The native read-back completes inside the paint (wgpu's buffer map is polled to
				// completion there), so this is normally already set. It is only not set if the await in
				// CaptureThenPresent genuinely suspended, in which case its continuation is queued to the
				// idle pump - hence pumping rather than blocking, which would deadlock.
				for (int spin = 0; spin < ScreenshotPumpSpins && !this.screenshotComplete.IsSet; spin++)
				{
					PumpEvents();
					InvokeIdleActions();
				}
			}
			finally
			{
				this.pendingScreenshotPath = null;
				this.screenshotComplete.Dispose();
				this.screenshotComplete = null;
			}
		}

		// -----------------------------------------------------------------------------------------
		// Display bootstrap
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Opens the shared connection and makes the process safe to run X11 on. Idempotent.
		/// </summary>
		/// <remarks>
		/// The order matters. The locale is set <em>first</em>, because <c>XOpenDisplay</c> captures the
		/// locale in effect when it runs and <c>XLookupString</c> answers in that locale's encoding - a
		/// process that never calls <c>setlocale</c> starts in the "C" locale, where the encoding is ASCII
		/// and every non-ASCII key silently produces nothing. The error handlers are installed before
		/// anything can fail, because both Xlib defaults end in <c>exit</c>.
		/// </remarks>
		private static void BootstrapDisplay()
		{
			lock (StaticInitLock)
			{
				if (displayBootstrapped)
				{
					return;
				}

				// "" means "take the locale from the environment", which is what makes XLookupString produce
				// UTF-8 on any modern desktop. XSupportsLocale then says whether Xlib has an X locale
				// database entry for it; if it has not, its behaviour in that locale is undefined, so the
				// only safe move is to stay in the portable "C" one.
				Xlib.SetLocale(Xlib.LC_ALL, string.Empty);
				if (Xlib.XSupportsLocale() == 0)
				{
					Console.Error.WriteLine(
						"X11SystemWindow: Xlib does not support this locale; falling back to \"C\". Non-ASCII keys may not type.");
					Xlib.SetLocale(Xlib.LC_ALL, "C");
				}
				else
				{
					// "" here means "take XMODIFIERS from the environment", which is where a running input
					// method advertises itself.
					//
					// Advertised to, and then not used: no XIM is opened and no XIC is created, so the input
					// method is not actually connected to and key translation goes through XLookupString
					// alone. That is enough for every direct key on every Latin layout, and is not enough for
					// the three things an input context owns - dead keys and Compose sequences (typing
					// ' then e for e-acute), CJK candidate selection, and on-the-spot preedit. Those need
					// XOpenIM/XCreateIC and a Xutf8LookupString on the KeyPress path, which is a follow-up.
					// The call stays because it costs nothing and has to happen here, before XOpenDisplay,
					// for that follow-up to have anything to open.
					Xlib.XSetLocaleModifiers(string.Empty);
				}

				InstallErrorHandlers();

				display = Xlib.XOpenDisplay(null);
				if (display == IntPtr.Zero)
				{
					throw new InvalidOperationException(
						"XOpenDisplay returned NULL: there is no X server to talk to. "
						+ $"DISPLAY is '{Environment.GetEnvironmentVariable("DISPLAY") ?? "(unset)"}'.");
				}

				screenNumber = Xlib.XDefaultScreen(display);
				rootWindow = Xlib.XRootWindow(display, screenNumber);

				// Xlib is single-threaded here (see Xlib's remarks), and this is the thread that owns the
				// connection: whoever opened it is whoever goes on to run the pump. Recorded so a caller
				// that can arrive from anywhere - the clipboard - can tell whether it is allowed to speak
				// X11 at all, rather than corrupting the connection to find out.
				displayThreadId = Environment.CurrentManagedThreadId;

				wmProtocolsAtom = Xlib.XInternAtom(display, "WM_PROTOCOLS", 0);
				wmDeleteWindowAtom = Xlib.XInternAtom(display, "WM_DELETE_WINDOW", 0);
				netWmNameAtom = Xlib.XInternAtom(display, "_NET_WM_NAME", 0);
				utf8StringAtom = Xlib.XInternAtom(display, "UTF8_STRING", 0);
				netWmStateAtom = Xlib.XInternAtom(display, "_NET_WM_STATE", 0);
				netWmStateMaximizedHorzAtom = Xlib.XInternAtom(display, "_NET_WM_STATE_MAXIMIZED_HORZ", 0);
				netWmStateMaximizedVertAtom = Xlib.XInternAtom(display, "_NET_WM_STATE_MAXIMIZED_VERT", 0);
				netFrameExtentsAtom = Xlib.XInternAtom(display, "_NET_FRAME_EXTENTS", 0);

				displayBootstrapped = true;
			}
		}

		/// <summary>
		/// Replaces both of Xlib's fatal default handlers. Xlib does not return protocol errors from the
		/// call that caused them - requests are asynchronous, so by the time the server objects the call has
		/// long since returned - and its defaults print and then <c>exit</c>. An application that installs
		/// nothing therefore dies on the first BadWindow with no managed stack and no chance to report.
		/// </summary>
		private static unsafe void InstallErrorHandlers()
		{
			// Rooted in static fields: nothing on the native side keeps a managed delegate alive, and a
			// collected one turns the next error into a jump into freed memory.
			protocolErrorHandler = (handlerDisplay, error) =>
			{
				// Never throws. This runs on a native frame with no managed caller above it, so an exception
				// crossing back tears the process down with no diagnostic - the same rule the mac host's
				// [UnmanagedCallersOnly] IMPs follow.
				try
				{
					Console.Error.WriteLine(
						$"X11 protocol error: code={error->ErrorCode} request={error->RequestCode}.{error->MinorCode} "
						+ $"resource=0x{error->ResourceId:x} serial={error->Serial}");
				}
				catch
				{
				}

				// Xlib ignores the value; zero is the convention.
				return 0;
			};

			ioErrorHandler = handlerDisplay =>
			{
				// The connection is gone: the server died, or the session ended under us. Every Xlib call
				// from here on is undefined behaviour, which rules out an orderly teardown - there is no
				// way to destroy a window on a display that no longer exists. Xlib also requires this
				// handler NOT to return; if it does, Xlib calls exit() itself with no message at all.
				//
				// So the process ends here, and it ends by Environment.Exit rather than FailFast: this is a
				// lost connection, not a corrupted process, and a crash dump plus a Watson report would say
				// nothing that the line below does not. Clearing the latch first means that if a finalizer
				// or an AppDomain handler does get a turn, it does not find a loop that believes it is
				// still running.
				try
				{
					runLoopActive = false;
					Console.Error.WriteLine("X11 I/O error: the connection to the X server was lost. Exiting.");
				}
				catch
				{
				}

				Environment.Exit(1);

				return 0;
			};

			Xlib.XSetErrorHandler(protocolErrorHandler);
			Xlib.XSetIOErrorHandler(ioErrorHandler);
		}

		/// <summary>
		/// Drains the RunOnIdle queue. Guarded because an idle action can run a nested loop (a modal
		/// dialog, or <see cref="CaptureScreenshot"/>'s pump) and re-enter this.
		/// </summary>
		private static void InvokeIdleActions()
		{
			lock (StaticInitLock)
			{
				if (processingOnIdle)
				{
					return;
				}

				processingOnIdle = true;
			}

			try
			{
				UiThread.InvokePendingActions();
			}
			finally
			{
				lock (StaticInitLock)
				{
					processingOnIdle = false;
				}
			}
		}

		// -----------------------------------------------------------------------------------------
		// Native window construction
		// -----------------------------------------------------------------------------------------

		private unsafe void CreateNativeWindow(SystemWindow systemWindow)
		{
			this.displayScale = ReadDisplayScale();

			// No division by a scale factor here, unlike the mac host: on X11 the window's size in pixels IS
			// the agg size. See the class remarks.
			uint width = (uint)Math.Max(1, systemWindow.Width);
			uint height = (uint)Math.Max(1, systemWindow.Height);

			var attributes = default(XSetWindowAttributes);
			attributes.BackgroundPixel = Xlib.XBlackPixel(display, screenNumber);
			attributes.EventMask =
				X11.ExposureMask
				| X11.StructureNotifyMask
				| X11.KeyPressMask
				| X11.KeyReleaseMask
				| X11.ButtonPressMask
				| X11.ButtonReleaseMask
				| X11.PointerMotionMask
				| X11.EnterWindowMask
				| X11.LeaveWindowMask
				| X11.FocusChangeMask;

			// Deliberately no PropertyChangeMask. The only property this host reads is _NET_FRAME_EXTENTS,
			// which the window manager publishes onto this window - but TitleBarHeight and DesktopPosition
			// read it on demand with XGetWindowProperty, so there is nothing for a PropertyNotify to do
			// except cost a round trip per property any client on the desktop happens to change.

			this.window = Xlib.XCreateWindow(
				display,
				rootWindow,
				0,
				0,
				width,
				height,
				0,
				X11.CopyFromParent,
				X11.InputOutput,

				// CopyFromParent for the visual as well as the depth: the root's visual is the server's
				// default, which is the one wgpu's Vulkan surface expects and the only one guaranteed to
				// have a colormap already.
				Xlib.XDefaultVisual(display, screenNumber),
				X11.CWEventMask | X11.CWBackPixel,
				&attributes);

			if (this.window == X11.None)
			{
				throw new InvalidOperationException("XCreateWindow returned None - the X server refused to create the window.");
			}

			// Without this the window manager has no way to ask, so its close button kills the connection
			// outright instead - which reaches this process as an I/O error and no Closed handler at all.
			ulong[] protocols = { wmDeleteWindowAtom };
			Xlib.XSetWMProtocols(display, this.window, protocols, protocols.Length);

			if (string.IsNullOrEmpty(this.caption))
			{
				this.caption = systemWindow.Title ?? string.Empty;
			}

			this.ApplyCaption();
			this.ApplySizeHints(systemWindow);

			// Before the map, not after: see RequestMaximizeBeforeMap. A window manager reads the state
			// property when it adopts the window, and after that it owns it.
			if (systemWindow.Maximized)
			{
				this.RequestMaximizeBeforeMap();
			}

			Xlib.XMapWindow(display, this.window);

			// XSync rather than XFlush: the geometry read straight afterwards has to be the one the server
			// settled on, and a flush only pushes the requests out without waiting for them.
			Xlib.XSync(display, 0);

			this.MeasureWindow();

			this.webGpuLayer = new X11WebGpuLayer(display, this.window, this.pixelWidth, this.pixelHeight);
		}

		/// <summary>
		/// Publishes the title under both conventions. See <see cref="Caption"/> for why one is not enough.
		/// </summary>
		private unsafe void ApplyCaption()
		{
			Xlib.XStoreName(display, this.window, this.caption);

			byte[] utf8 = Encoding.UTF8.GetBytes(this.caption);
			fixed (byte* bytes = utf8)
			{
				Xlib.XChangeProperty(
					display,
					this.window,
					netWmNameAtom,
					utf8StringAtom,
					8,
					X11.PropModeReplace,
					bytes,

					// A zero-length property is legal and is how an empty title is spelled; the fixed
					// pointer of an empty array is null, which XChangeProperty accepts for a zero count.
					utf8.Length);
			}
		}

		/// <summary>
		/// Publishes <c>WM_NORMAL_HINTS</c>: the minimum size always, and the initial geometry when the
		/// application asked for a specific one.
		/// </summary>
		/// <param name="systemWindow">
		/// The window being created, or null when this is a later minimum-size change - in which case there
		/// is no initial position left to state.
		/// </param>
		private unsafe void ApplySizeHints(SystemWindow systemWindow)
		{
			var hints = default(XSizeHints);

			if (this.minimumSize != Vector2.Zero)
			{
				hints.Flags |= X11.PMinSize;
				hints.MinWidth = (int)Math.Max(1, this.minimumSize.X);
				hints.MinHeight = (int)Math.Max(1, this.minimumSize.Y);
			}

			if (systemWindow != null)
			{
				// PSize is "the program picked this size", as opposed to USSize which claims the user did.
				hints.Flags |= X11.PSize;
				hints.Width = (int)Math.Max(1, systemWindow.Width);
				hints.Height = (int)Math.Max(1, systemWindow.Height);

				// (-1, -1) is agg's "no preference", which means let the window manager place it.
				if (systemWindow.InitialDesktopPosition != new Point2D(-1, -1))
				{
					hints.Flags |= X11.PPosition;
					hints.X = systemWindow.InitialDesktopPosition.x;
					hints.Y = systemWindow.InitialDesktopPosition.y;

					Xlib.XMoveWindow(display, this.window, hints.X, hints.Y);
				}
			}

			if (hints.Flags == 0)
			{
				return;
			}

			Xlib.XSetWMNormalHints(display, this.window, &hints);
		}

		/// <summary>
		/// Asks for the window to come up maximized, by setting <c>_NET_WM_STATE</c> on it while it is still
		/// unmapped.
		/// </summary>
		/// <remarks>
		/// EWMH splits this into two mechanisms and the split is by map state, not by preference. Before the
		/// window is mapped the property <em>is</em> the request: the manager reads it when it takes the
		/// window over, and that is the only way to ask for an initial state. Once mapped the manager owns
		/// the property and a client that writes it is ignored - from then on the request has to be a
		/// <c>_NET_WM_STATE</c> ClientMessage to the root window, where the manager is the one selecting for
		/// substructure events. Nothing here needs the second form (agg has no "maximize now" call), so only
		/// the pre-map one is implemented.
		/// <para/>
		/// The property is a list of atoms in "format 32", which on LP64 means Xlib expects an array of C
		/// <c>long</c> - 8 bytes per element - even though only 32 bits per element reach the wire.
		/// </remarks>
		private unsafe void RequestMaximizeBeforeMap()
		{
			long* states = stackalloc long[2];
			states[0] = (long)netWmStateMaximizedVertAtom;
			states[1] = (long)netWmStateMaximizedHorzAtom;

			Xlib.XChangeProperty(
				display,
				this.window,
				netWmStateAtom,
				X11.XA_ATOM,
				32,
				X11.PropModeReplace,
				(byte*)states,
				2);
		}

		/// <summary>Sends this window the same close request the window manager's close button sends.</summary>
		private unsafe void SendCloseRequestToSelf()
		{
			var message = default(XEvent);
			ref XClientMessageEvent clientMessage = ref message.As<XClientMessageEvent>();

			clientMessage.Type = X11.ClientMessage;
			clientMessage.Display = display;
			clientMessage.Window = this.window;
			clientMessage.MessageType = wmProtocolsAtom;
			clientMessage.Format = 32;
			clientMessage.Data[0] = (long)wmDeleteWindowAtom;
			clientMessage.Data[1] = (long)X11.CurrentTime;

			// No mask: an event sent with NoEventMask goes to the client that created the window, which for
			// this window is us. Propagate is false for the same reason.
			Xlib.XSendEvent(display, this.window, 0, X11.NoEventMask, ref message);
			Xlib.XFlush(display);
		}

		/// <summary>Re-reads the window's size from the server into <see cref="pixelWidth"/>/<see cref="pixelHeight"/>.</summary>
		private void MeasureWindow()
		{
			if (Xlib.XGetWindowAttributes(display, this.window, out XWindowAttributes attributes) == 0)
			{
				return;
			}

			this.pixelWidth = (uint)Math.Max(1, attributes.Width);
			this.pixelHeight = (uint)Math.Max(1, attributes.Height);
		}

		/// <summary>
		/// How much room the screen has for a window, in device pixels.
		/// </summary>
		/// <remarks>
		/// The whole screen. X11 has no notion of a work area at all - the space a panel or a dock occupies
		/// is a window-manager convention published as <c>_NET_WORKAREA</c>, which is frequently absent and
		/// is meaningless on a multi-head setup where it describes the union of the displays. The screen
		/// size is the honest answer this host can give; a manager-aware refinement belongs with the rest
		/// of the multi-monitor work, which nothing in agg needs yet.
		/// </remarks>
		private Vector2 MeasureUsableScreenSize()
		{
			if (display == IntPtr.Zero)
			{
				return Vector2.Zero;
			}

			int width = Xlib.XDisplayWidth(display, screenNumber);
			int height = Xlib.XDisplayHeight(display, screenNumber);

			return width > 0 && height > 0 ? new Vector2(width, height) : Vector2.Zero;
		}

		/// <summary>
		/// The user's display scaling, from <c>Xft.dpi</c> over 96. Read per window rather than cached,
		/// because a user who changes their scaling writes a new value into the resource database and every
		/// running client is expected to notice.
		/// </summary>
		private static double ReadDisplayScale()
		{
			if (display != IntPtr.Zero && Xlib.TryReadXftDpi(display, out double dpi) && dpi > 0)
			{
				return dpi / BaselineDpi;
			}

			return 1;
		}

		/// <summary>
		/// Reads the window manager's frame thickness from <c>_NET_FRAME_EXTENTS</c>.
		/// </summary>
		/// <returns>False when no manager published the property, which is also the bare-X-server case.</returns>
		private bool ReadFrameExtents(out int left, out int top)
		{
			left = 0;
			top = 0;

			if (this.window == X11.None || display == IntPtr.Zero)
			{
				return false;
			}

			// The property is four CARDINALs: left, right, top, bottom. Lengths here are in 32-bit units,
			// but a "format 32" property is unpacked into C longs, so each item is 8 bytes wide on LP64.
			int status = Xlib.XGetWindowProperty(
				display,
				this.window,
				netFrameExtentsAtom,
				0,
				4,
				0,
				X11.XA_CARDINAL,
				out _,
				out int actualFormat,
				out ulong itemCount,
				out _,
				out IntPtr property);

			if (status != 0 || property == IntPtr.Zero)
			{
				return false;
			}

			try
			{
				if (actualFormat != 32 || itemCount < 4)
				{
					return false;
				}

				left = (int)System.Runtime.InteropServices.Marshal.ReadInt64(property, 0);
				top = (int)System.Runtime.InteropServices.Marshal.ReadInt64(property, 2 * sizeof(long));

				return true;
			}
			finally
			{
				// Xlib allocated this even when it found nothing, which is the leak everyone writes once.
				Xlib.XFree(property);
			}
		}

		/// <summary>The cursor for a font shape, made once per shape and kept for the life of the process.</summary>
		private static ulong ResolveCursor(uint shape)
		{
			lock (ResolvedCursors)
			{
				if (ResolvedCursors.TryGetValue(shape, out ulong cached))
				{
					return cached;
				}

				ulong cursor = Xlib.XCreateFontCursor(display, shape);
				if (cursor != X11.None)
				{
					ResolvedCursors[shape] = cursor;
				}

				return cursor;
			}
		}

		// -----------------------------------------------------------------------------------------
		// Resize
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Decides whether a <c>ConfigureNotify</c> has to paint the frame itself rather than leaving it to
		/// the pump.
		/// </summary>
		/// <remarks>
		/// The same decision <c>MacSystemWindow</c> makes, reached from the other direction. There it is
		/// forced: a live resize runs inside AppKit's nested tracking loop, the host's pump is frozen for
		/// its duration, and without a synchronous paint nothing draws until the mouse comes up. X11 has no
		/// nested loop, so the pump would get there on its own - but not before the server has already
		/// resized the window under the last presented frame, which reads as the same smear. Painting from
		/// inside the burst keeps the drawable and the window the same size at every step.
		/// <para/>
		/// Two things have to be true, and they are not the same test. The burst is what says a drag is in
		/// progress rather than one deliberate resize the pump will pick up on its very next pass. The show
		/// gate is what keeps the mapping and settling sequence out of it: those configures arrive back to
		/// back, so they look exactly like a burst to a timer, and painting there would draw before
		/// <see cref="ShowSystemWindow"/> has finished bringing the window up. A threshold cannot separate
		/// the two - the show sequence's configures are as close together as a drag's - which is why the
		/// caller passes both.
		/// <para/>
		/// Factored out and parameterised for the same reason the mac one is - a resize burst cannot be
		/// synthesised in a unit test, but this decision can.
		/// </remarks>
		/// <param name="inResizeBurst">Whether another size change arrived within <see cref="ResizeBurstMilliseconds"/>.</param>
		/// <param name="showCompleted">False until <see cref="ShowSystemWindow"/> has mapped and settled the window.</param>
		/// <param name="isInsidePaint">True when a paint is already on the stack; painting again would re-enter the frame.</param>
		/// <param name="hasClosed">True once the window is gone, which configure events can still outlive.</param>
		/// <param name="webGpuInitialized">False before there is a swapchain to draw into - the first resizes land there.</param>
		internal static bool ShouldPaintSynchronouslyForResize(
			bool inResizeBurst,
			bool showCompleted,
			bool isInsidePaint,
			bool hasClosed,
			bool webGpuInitialized)
		{
			return inResizeBurst && showCompleted && webGpuInitialized && !isInsidePaint && !hasClosed;
		}

		/// <summary>
		/// Pushes the window's current size everywhere it has to go: the swapchain and the agg window's
		/// bounds, scale and usable size.
		/// </summary>
		private void SyncSizeFromWindow()
		{
			if (this.window == X11.None || this.hasClosed)
			{
				return;
			}

			uint previousWidth = this.pixelWidth;
			uint previousHeight = this.pixelHeight;

			this.MeasureWindow();

			if (this.webGpuLayer != null
				&& this.webGpuLayer.IsWebGpuInitialized
				&& (previousWidth != this.pixelWidth || previousHeight != this.pixelHeight))
			{
				this.webGpuLayer.Resize(this.pixelWidth, this.pixelHeight);
			}

			this.viewPortHasBeenSet = false;

			if (this.aggSystemWindow != null)
			{
				// The drawable is this big whatever the application's minimum says. Assigning LocalBounds
				// would let a minimum computed elsewhere inflate the layout past the drawable, and agg being
				// y-up that clips off the top - the toolbars vanish under the title bar.
				this.aggSystemWindow.SetBoundsFromPlatform(this.pixelWidth, this.pixelHeight);

				// SetDisplayScale only stores the value; it raises its event from the idle queue, so this is
				// safe to call from inside a resize burst where a subscriber that rebuilt the UI would stall.
				this.aggSystemWindow.SetDisplayScale(this.displayScale);
				this.aggSystemWindow.SetDisplayUsableSize(this.MeasureUsableScreenSize());

				this.aggSystemWindow.Invalidate();
			}

			this.needsRedraw = true;
		}

		// -----------------------------------------------------------------------------------------
		// The event loop
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Drives X11 ourselves, the way a toolkit with its own frame scheduler must: the queue is drained
		/// without ever blocking in <c>XNextEvent</c>, so the loop can paint between batches of events, and
		/// the wait for the next one is a <c>poll</c> with a timeout rather than a block. See the class
		/// remarks for why that timeout is also the idle tick.
		/// </summary>
		private static void RunEventLoop()
		{
			runLoopActive = true;

			try
			{
				while (runLoopActive)
				{
					PumpEvents();

					// This loop owns its thread for as long as a window is up, so anything another thread
					// asked it to do has to come through here or it never runs at all. A no-op when nothing
					// hosted the dispatcher, which off macOS is the normal case.
					MainThreadDispatcher.DrainPending();

					InvokeIdleActions();

					bool paintedSomething = false;

					X11SystemWindow[] windows;
					lock (StaticInitLock)
					{
						windows = LiveWindows.ToArray();
					}

					foreach (var x11Window in windows)
					{
						if (x11Window.needsRedraw && !x11Window.hasClosed)
						{
							x11Window.PaintFrame();
							paintedSomething = true;
						}
					}

					lock (StaticInitLock)
					{
						if (LiveWindows.Count == 0)
						{
							runLoopActive = false;
						}
					}

					if (!paintedSomething && runLoopActive)
					{
						// Nothing to draw and nothing queued. Without this the loop is a 100% CPU spin.
						WaitForEvents(IdlePumpMilliseconds);
					}
				}
			}
			finally
			{
				// A paint that throws must not leave the process believing a loop is still running, or the
				// next window shown would return immediately and never be pumped.
				runLoopActive = false;
			}
		}

		/// <summary>Drains the X event queue, dispatching each event to the window it belongs to.</summary>
		private static void PumpEvents()
		{
			if (display == IntPtr.Zero)
			{
				return;
			}

			// Input that arrived while a clipboard round trip was outstanding was held back rather than
			// dispatched into half-finished widget code (see X11Selection's remarks). It goes first, ahead
			// of anything still on the X queue, so a keystroke typed during a paste still lands before one
			// typed after it.
			X11Selection.DispatchDeferredInput();

			// XPending flushes the output buffer and then reports what has already been decoded, so this is
			// both "send my requests" and "is there anything for me". XNextEvent blocks, which is why it is
			// only ever reached with a non-zero count in hand.
			while (Xlib.XPending(display) > 0)
			{
				Xlib.XNextEvent(display, out XEvent nextEvent);
				DispatchEvent(ref nextEvent);
			}
		}

		/// <summary>
		/// Sleeps until the X connection has something to say or <paramref name="milliseconds"/> elapse.
		/// </summary>
		/// <remarks>
		/// <c>poll</c> on the connection's file descriptor rather than a plain sleep, so an event that
		/// arrives one millisecond in is acted on then rather than at the end of the interval. The queue is
		/// re-checked first because Xlib may already hold a decoded event with nothing left on the socket to
		/// wake the poll - which would be a wait for something that has already happened.
		/// </remarks>
		internal static unsafe void WaitForEvents(int milliseconds)
		{
			if (display == IntPtr.Zero)
			{
				Thread.Sleep(milliseconds);
				return;
			}

			if (Xlib.XPending(display) > 0)
			{
				return;
			}

			var pollFd = new PollFd
			{
				Fd = Xlib.XConnectionNumber(display),
				Events = Xlib.POLLIN,
			};

			Xlib.Poll(&pollFd, 1, milliseconds);
		}

		/// <summary>
		/// Routes one X event to the window it names, with the same two guards the mac host's dispatch has:
		/// input is dropped when a parallel automation run has turned real input off, and nothing is allowed
		/// to throw out of here.
		/// </summary>
		/// <remarks>
		/// Internal rather than private because a clipboard read is a round trip through the X server on
		/// this same single thread: <see cref="X11Selection"/> runs a nested pump while it waits for the
		/// answer, and hands everything that is not that answer back here so the application does not go
		/// deaf for the length of a paste.
		/// </remarks>
		internal static void DispatchEvent(ref XEvent nextEvent)
		{
			// A few events are about the display rather than about a window, and the per-window lookup below
			// would drop them: they carry a window field that is meaningless (MappingNotify names whatever
			// window happened to have the focus, which is frequently not one of ours and is sometimes None).
			// They have to be handled before the lookup, not after it.
			if (HandleDisplayWideEvent(ref nextEvent))
			{
				return;
			}

			ulong eventWindow = WindowOf(ref nextEvent);

			X11SystemWindow target = null;
			lock (StaticInitLock)
			{
				foreach (var x11Window in LiveWindows)
				{
					if (x11Window.window == eventWindow && eventWindow != X11.None)
					{
						target = x11Window;
						break;
					}
				}
			}

			if (target == null || target.hasClosed)
			{
				return;
			}

			try
			{
				target.HandleEvent(ref nextEvent);
			}
			catch (Exception ex)
			{
				UiThread.ReportUnhandledException(ex);
				Console.Error.WriteLine($"X11SystemWindow event handler threw {ex}");
			}
		}

		/// <summary>
		/// Handles the events that belong to the connection rather than to any one window.
		/// </summary>
		/// <returns>True when the event was handled here and must not be routed to a window.</returns>
		private static bool HandleDisplayWideEvent(ref XEvent nextEvent)
		{
			// The selection events belong to X11Selection's hidden window, which is deliberately not one of
			// LiveWindows - so the lookup below would drop the SelectionRequest that *is* the clipboard.
			if (X11Selection.TryHandleEvent(ref nextEvent))
			{
				return true;
			}

			if (nextEvent.Type != X11.MappingNotify)
			{
				return false;
			}

			try
			{
				// Xlib caches the keyboard mapping, and until it is told the layout changed every keysym
				// lookup answers from the old one - so a user who switches layout keeps typing the previous
				// one until the process restarts. The cache is per display, which is why this is not a
				// window's business.
				Xlib.XRefreshKeyboardMapping(ref nextEvent.As<XMappingEvent>());
			}
			catch (Exception ex)
			{
				UiThread.ReportUnhandledException(ex);
				Console.Error.WriteLine($"X11SystemWindow MappingNotify handler threw {ex}");
			}

			return true;
		}

		/// <summary>
		/// The window an event is about. Every arm of <see cref="XEvent"/> that this host handles carries a
		/// <c>window</c> field, but not at the same offset - <see cref="XConfigureEvent"/> and
		/// <see cref="XDestroyWindowEvent"/> have an <c>event</c> field ahead of it, because a structure
		/// event can be selected on the parent. The one wanted here is always the subject window.
		/// </summary>
		private static ulong WindowOf(ref XEvent nextEvent)
		{
			switch (nextEvent.Type)
			{
				case X11.ConfigureNotify:
					return nextEvent.As<XConfigureEvent>().Window;

				case X11.DestroyNotify:
					return nextEvent.As<XDestroyWindowEvent>().Window;

				case X11.KeyPress:
				case X11.KeyRelease:
					return nextEvent.As<XKeyEvent>().Window;

				case X11.ButtonPress:
				case X11.ButtonRelease:
					return nextEvent.As<XButtonEvent>().Window;

				case X11.MotionNotify:
					return nextEvent.As<XMotionEvent>().Window;

				case X11.EnterNotify:
				case X11.LeaveNotify:
					return nextEvent.As<XCrossingEvent>().Window;

				case X11.FocusIn:
				case X11.FocusOut:
					return nextEvent.As<XFocusChangeEvent>().Window;

				case X11.Expose:
					return nextEvent.As<XExposeEvent>().Window;

				case X11.ClientMessage:
					return nextEvent.As<XClientMessageEvent>().Window;

				default:
					// MappingNotify was already taken by HandleDisplayWideEvent; everything else left here
					// is something this host does not handle at all.
					return X11.None;
			}
		}

		private unsafe void HandleEvent(ref XEvent nextEvent)
		{
			switch (nextEvent.Type)
			{
				case X11.Expose:
					// The whole frame is redrawn either way, so the damage rectangle is not read. Count is
					// how many more Expose events for this same damage are still queued; they will all set
					// the same flag, which is harmless.
					this.needsRedraw = true;
					return;

				case X11.ConfigureNotify:
					this.HandleConfigureNotify(ref nextEvent.As<XConfigureEvent>());
					return;

				case X11.ClientMessage:
					this.HandleClientMessage(ref nextEvent.As<XClientMessageEvent>());
					return;

				case X11.DestroyNotify:
					this.HandleDestroyNotify();
					return;

				case X11.FocusIn:
					this.HandleFocusGained(ref nextEvent.As<XFocusChangeEvent>());
					return;

				case X11.FocusOut:
					this.HandleFocusLost(ref nextEvent.As<XFocusChangeEvent>());
					return;

				case X11.KeyPress:
				case X11.KeyRelease:
				case X11.ButtonPress:
				case X11.ButtonRelease:
				case X11.MotionNotify:
				case X11.EnterNotify:
				case X11.LeaveNotify:
					// Parallel automation tests turn this off so a real mouse or keyboard cannot perturb a
					// run. Only the input arms are gated: a window still has to resize, repaint and close.
					if (!IPlatformWindow.EnablePlatformWindowInput || this.aggSystemWindow == null)
					{
						return;
					}

					this.HandleInputEvent(ref nextEvent);
					return;

				default:
					return;
			}
		}

		/// <summary>
		/// Handles a geometry change. A move alone changes nothing this host cares about, so the size is
		/// what is tested - the window manager sends a ConfigureNotify for a restack too.
		/// </summary>
		private void HandleConfigureNotify(ref XConfigureEvent configureEvent)
		{
			uint newWidth = (uint)Math.Max(1, configureEvent.Width);
			uint newHeight = (uint)Math.Max(1, configureEvent.Height);

			if (newWidth == this.pixelWidth && newHeight == this.pixelHeight)
			{
				return;
			}

			// Read before the timestamp is moved on, so this asks "was there a size change just before this
			// one" - which is what makes the first configure of a burst leave the paint to the pump.
			long now = Stopwatch.GetTimestamp();
			bool inResizeBurst = this.lastResizeTimestamp != long.MinValue
				&& (now - this.lastResizeTimestamp) < (Stopwatch.Frequency * ResizeBurstMilliseconds / 1000);

			this.lastResizeTimestamp = now;

			this.SyncSizeFromWindow();

			if (ShouldPaintSynchronouslyForResize(
				inResizeBurst,
				this.showCompleted,
				this.isInsidePaint,
				this.hasClosed,
				this.webGpuLayer?.IsWebGpuInitialized ?? false))
			{
				// DispatchEvent's catch would swallow a paint failure into a log line; report it the way
				// the loop's own paint would have.
				try
				{
					this.PaintFrame();
				}
				catch (Exception ex)
				{
					UiThread.ReportUnhandledException(ex);
					Console.Error.WriteLine($"X11SystemWindow resize paint threw {ex}");
				}
			}
		}

		private unsafe void HandleClientMessage(ref XClientMessageEvent clientMessage)
		{
			if (clientMessage.MessageType != wmProtocolsAtom || clientMessage.Format != 32)
			{
				return;
			}

			if ((ulong)clientMessage.Data[0] != wmDeleteWindowAtom)
			{
				return;
			}

			this.HandleCloseRequest();
		}

		/// <summary>
		/// Runs a close request - the window manager's close button, or <see cref="Close"/> - against the
		/// application, and destroys the native window if it is allowed to.
		/// </summary>
		private void HandleCloseRequest()
		{
			bool mayClose = HandlePlatformCloseRequest(
				SingleWindowMode,
				this.WindowProvider,
				this.aggSystemWindow,
				closing => this.platformAlreadyClosing = closing);

			if (mayClose)
			{
				this.DestroyNativeWindow();
			}
		}

		// -----------------------------------------------------------------------------------------
		// Input translation
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Re-derives the held modifiers from the live keyboard now that this window has the focus again.
		/// </summary>
		/// <remarks>
		/// The counterpart to <see cref="HandleFocusLost"/>. Every modifier change that happened while
		/// another window had the keyboard was delivered to that window, and a user genuinely can be holding
		/// a modifier at the moment focus returns - releasing an Alt-Tab commonly leaves Alt down for a beat
		/// over the newly focused window. Without this the first drag back would be wrong in the opposite
		/// direction, with a held modifier this window never heard about.
		/// <para/>
		/// The one guarded handler of the pair, because it is the only one that <em>polls</em> the real
		/// keyboard rather than reacting to an event about it - the same reasoning as
		/// <c>MacSystemWindow.HandleDidBecomeKey</c>. Both conditions say the same thing from different
		/// directions: <c>EnablePlatformWindowInput</c> off means a run has asked that the real machine not
		/// perturb it, and <see cref="SetModifierKeys"/>' contract is that once a synthetic event has
		/// declared what it is holding the real keyboard is never read again. Answering "nothing is held"
		/// from a machine with no user at it would overwrite the synthetic state with a lie.
		/// </remarks>
		private void HandleFocusGained(ref XFocusChangeEvent focusEvent)
		{
			if (!IsRealFocusChange(focusEvent.Mode, focusEvent.Detail))
			{
				return;
			}

			if (!IPlatformWindow.EnablePlatformWindowInput || this.modifiersOverridden)
			{
				return;
			}

			if (display == IntPtr.Zero || this.window == X11.None)
			{
				return;
			}

			// The live "what is held right now", which is what is wanted when no event told us. Its return
			// value is deliberately ignored: it reports false only when the pointer is on another screen,
			// which invalidates the coordinates and not the modifier mask. Same call, same moment and same
			// purpose as the mac host's +[NSEvent modifierFlags].
			Xlib.XQueryPointer(
				display,
				this.window,
				out _,
				out _,
				out _,
				out _,
				out _,
				out _,
				out uint mask);

			this.lastModifierState = mask & X11.AllModifierMask;
			this.appliedModifierKeys = ApplyModifierFlagsToKeyboard(this.lastModifierState);
		}

		/// <summary>
		/// Releases the modifiers this window put down, because it no longer has the keyboard and can no
		/// longer be told they were let go of.
		/// </summary>
		/// <remarks>
		/// X11 delivers a key event only to the focus window, so a modifier released while another
		/// application is focused is never reported here and stays latched down forever. Alt-Tab is the
		/// everyday case - it <em>begins</em> with Alt held - and the symptom is that coming back to the
		/// application leaves the 3D view convinced a modifier is down, so a plain left drag pans instead of
		/// selecting.
		/// <para/>
		/// Deliberately unguarded, unlike <see cref="HandleFocusGained"/>: this is the exact inverse of what
		/// this window applied and can touch nothing else, so there is no synthetic state for it to damage.
		/// Never <c>Keyboard.Clear()</c> - see <see cref="ReleaseAppliedModifierKeys"/>.
		/// </remarks>
		private void HandleFocusLost(ref XFocusChangeEvent focusEvent)
		{
			if (!IsRealFocusChange(focusEvent.Mode, focusEvent.Detail))
			{
				return;
			}

			this.lastModifierState = ReleaseAppliedModifierKeys(this.appliedModifierKeys);
			this.appliedModifierKeys = NoModifierKeys;

			// The keyboard is gone, and with it any hope of hearing the release that ends a drag in flight -
			// a button up delivered to whoever has the input now is a button this window would hold captured
			// forever, and a pointer grab held by a window that is not even focused is a desktop that feels
			// broken. Both go here rather than waiting for an up that is not coming.
			this.ReleasePointerGrab();
			this.mouseCapture.ClearCapturedButtons();
		}

		/// <summary>
		/// Whether a focus event means this window really gained or lost the keyboard.
		/// </summary>
		/// <remarks>
		/// Two different impostors have to be turned away, and they are spelled in two different fields.
		/// <para/>
		/// <b>The mode</b> catches a grab: taking or dropping one synthesises a FocusOut/FocusIn pair "as if
		/// the focus warped", the same way it synthesises crossing events. Acting on those releases the held
		/// modifiers in the middle of a gesture that is still running - the keyboard half of the bug
		/// <see cref="IsRealPointerExit"/> exists for on the pointer side.
		/// <para/>
		/// <b>The detail</b> catches focus-follows-mouse. On a desktop where the focus is PointerRoot, every
		/// crossing of every window produces a FocusIn/FocusOut pair with detail Pointer or PointerRoot -
		/// they say "the keyboard goes wherever the pointer is", not "this window lost it". A drag that
		/// leaves the window (which is normal, and is what the pointer grab exists to support) would
		/// otherwise release every modifier mid-gesture, so the user's Ctrl-drag becomes a plain drag halfway
		/// through. NotifyDetailNone rides along above the same threshold: it is the focus becoming None,
		/// which is a transient the window manager passes through and not a window this one lost to.
		/// </remarks>
		private static bool IsRealFocusChange(int mode, int detail)
			=> (mode == X11.NotifyNormal || mode == X11.NotifyWhileGrabbed)
				&& detail < X11.NotifyPointer;

		/// <summary>
		/// Translates a pointer or keyboard event into agg's events: the button and keysym mapping, the Y
		/// flip, the wheel conventions and the out-of-view drag capture.
		/// </summary>
		private unsafe void HandleInputEvent(ref XEvent nextEvent)
		{
			switch (nextEvent.Type)
			{
				case X11.ButtonPress:
					this.HandleButton(ref nextEvent.As<XButtonEvent>(), pressed: true);
					return;

				case X11.ButtonRelease:
					this.HandleButton(ref nextEvent.As<XButtonEvent>(), pressed: false);
					return;

				case X11.MotionNotify:
					this.HandleMotion(ref nextEvent.As<XMotionEvent>());
					return;

				case X11.EnterNotify:
				case X11.LeaveNotify:
					this.HandleCrossing(ref nextEvent.As<XCrossingEvent>());
					return;

				case X11.KeyPress:
					this.HandleKeyPress(ref nextEvent.As<XKeyEvent>());
					return;

				case X11.KeyRelease:
					this.HandleKeyRelease(ref nextEvent.As<XKeyEvent>());
					return;

				default:
					return;
			}
		}

		/// <summary>
		/// Turns a <c>ButtonPress</c> or <c>ButtonRelease</c> into a mouse down, a mouse up or a wheel event.
		/// </summary>
		private void HandleButton(ref XButtonEvent buttonEvent, bool pressed)
		{
			this.lastModifierState = buttonEvent.State & X11.AllModifierMask;

			bool insideWindow = IsInsideBounds(buttonEvent.X, buttonEvent.Y, this.pixelWidth, this.pixelHeight);

			// The state word is the state *before* the event, so a press's own button is not in it yet and has
			// to be put there or the reconcile would immediately drop the button just captured.
			uint heldButtons = buttonEvent.State;
			if (pressed)
			{
				heldButtons |= ButtonStateMaskForButtonNumber(buttonEvent.Button);
			}

			this.mouseCapture.ReconcileWithButtonState(heldButtons);

			try
			{
				// 4 to 7 are not buttons at all. X11 has no wheel event, so a detent is a press/release pair on
				// a synthetic button - which is why a wheel-only device still reports "buttons". Only the press
				// carries the notch (delivering the release as well would double every scroll), and none of
				// these is a button a drag can capture.
				if (buttonEvent.Button >= X11.Button4 && buttonEvent.Button <= X11.Button7)
				{
					if (pressed && insideWindow)
					{
						var wheelArgs = new MouseEventArgs(
							MouseButtons.None,
							0,
							buttonEvent.X,
							FlipY(buttonEvent.Y, this.pixelHeight),
							0);

						ApplyButtonWheelDeltas(wheelArgs, buttonEvent.Button);

						this.aggSystemWindow.OnMouseWheel(wheelArgs);
					}

					return;
				}

				MouseButtons button = TranslateButton(buttonEvent.Button);
				if (button == MouseButtons.None)
				{
					// Buttons 8 and 9 are the thumb "back" and "forward" buttons on most mice. agg has no
					// MouseButtons for them, and reporting them as some other button would be worse than not
					// reporting them at all.
					return;
				}

				if (!this.mouseCapture.ShouldDeliver(
					pressed ? X11.ButtonPress : X11.ButtonRelease,
					button,
					insideWindow))
				{
					return;
				}

				// A mouse up reports the click count of the press it ends. That is AppKit's behaviour, which
				// this host matches deliberately: WinForms reports 1 on every mouse up and puts the 2 only on
				// the second mouse down. Carrying it on the up as well is what lets a widget act on a double
				// click at the end of the gesture rather than the start.
				int clicks = pressed
					? this.clickCounter.CountPress(buttonEvent.Button, buttonEvent.Time, buttonEvent.X, buttonEvent.Y)
					: this.clickCounter.LastClickCount;

				// Deliberately not clamped to the window: a drag that ran past the edge should reach the widget
				// with where the pointer really is, so that dragging out and back does not look like a jump to
				// the edge and stop.
				var args = new MouseEventArgs(
					button,
					clicks,
					buttonEvent.X,
					FlipY(buttonEvent.Y, this.pixelHeight),
					0);

				if (pressed)
				{
					this.aggSystemWindow.OnMouseDown(args);
				}
				else
				{
					this.aggSystemWindow.OnMouseUp(args);
				}
			}
			finally
			{
				// In a finally, and that is the whole point of the try. This runs after ShouldDeliver, which is
				// what owns the captured-button set, so it cannot disagree with the filter about whether a drag
				// is still in flight - but the delivery between them is arbitrary application code. A widget
				// that throws out of its OnMouseUp would otherwise leave this process holding the X pointer
				// grab with no button down and no further up coming, which is a desktop the user cannot click
				// their way out of.
				this.SyncPointerGrab(buttonEvent.Time);
			}
		}

		private void HandleMotion(ref XMotionEvent motionEvent)
		{
			this.lastModifierState = motionEvent.State & X11.AllModifierMask;

			// A motion event's state is current, so this is the cheapest and most frequent chance to notice a
			// button release that never reached us.
			this.mouseCapture.ReconcileWithButtonState(motionEvent.State);

			try
			{
				bool insideWindow = IsInsideBounds(motionEvent.X, motionEvent.Y, this.pixelWidth, this.pixelHeight);
				MouseButtons button = TranslateButtonState(motionEvent.State);

				if (!this.mouseCapture.ShouldDeliver(X11.MotionNotify, button, insideWindow))
				{
					return;
				}

				this.aggSystemWindow.OnMouseMove(new MouseEventArgs(
					button,
					0,
					motionEvent.X,
					FlipY(motionEvent.Y, this.pixelHeight),
					0));
			}
			finally
			{
				this.SyncPointerGrab(motionEvent.Time);
			}
		}

		/// <summary>
		/// Turns an <c>EnterNotify</c> into a move, and a <c>LeaveNotify</c> into the pointer-gone sentinel -
		/// but only when the geometry proves the pointer really left. See <see cref="IsRealPointerExit"/>.
		/// </summary>
		private void HandleCrossing(ref XCrossingEvent crossingEvent)
		{
			this.lastModifierState = crossingEvent.State & X11.AllModifierMask;

			this.mouseCapture.ReconcileWithButtonState(crossingEvent.State);

			try
			{
				if (crossingEvent.Type == X11.EnterNotify)
				{
					// Filtered on the mode for the same reason a leave is: a grab manufactures an enter the
					// pointer never made, and turning that into a move would report the pointer arriving
					// somewhere it has been sitting all along - which for a widget mid-drag reads as a jump.
					if (crossingEvent.Mode != X11.NotifyNormal)
					{
						return;
					}

					bool insideWindow = IsInsideBounds(
						crossingEvent.X,
						crossingEvent.Y,
						this.pixelWidth,
						this.pixelHeight);

					MouseButtons button = TranslateButtonState(crossingEvent.State);

					// Through the same filter as a move, because that is what it becomes. An enter carrying a
					// button whose press this window never saw is somebody else's drag arriving here, and it is
					// no more ours than the motion events behind it.
					if (!this.mouseCapture.ShouldDeliver(X11.MotionNotify, button, insideWindow))
					{
						return;
					}

					// agg has no enter event of its own: a move at the entry point is what makes the widget
					// under the pointer light up, which is what WinForms' MouseEnter ends up doing too.
					this.aggSystemWindow.OnMouseMove(new MouseEventArgs(
						button,
						0,
						crossingEvent.X,
						FlipY(crossingEvent.Y, this.pixelHeight),
						0));

					return;
				}

				if (IsRealPointerExit(
					crossingEvent.X,
					crossingEvent.Y,
					this.pixelWidth,
					this.pixelHeight,
					this.mouseCapture.HasCapturedButtons,
					crossingEvent.Mode))
				{
					// The same sentinel the Windows sink and the mac host use for "the pointer is nowhere near me".
					this.aggSystemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
				}
			}
			finally
			{
				this.SyncPointerGrab(crossingEvent.Time);
			}
		}

		private unsafe void HandleKeyPress(ref XKeyEvent keyEvent)
		{
			// XLookupString answers twice over: the keysym (the symbol the key produces under the active
			// layout, which is what agg's Keys maps onto) and the bytes the key types, in the process
			// locale's encoding - which BootstrapDisplay's setlocale is what makes UTF-8. 32 bytes is far
			// more than one key can produce without an input method.
			byte* typedBytes = stackalloc byte[32];
			int byteCount = Xlib.XLookupString(ref keyEvent, typedBytes, 32, out ulong keysym, IntPtr.Zero);

			this.TrackModifierState(keyEvent.State, keysym, pressed: true);

			// From the corrected state and not from keyEvent.State, because for a bare modifier the two
			// disagree: X11's state word is the state before its own event, so pressing Shift arrives with no
			// ShiftMask set and the args would say Shift is not held at the exact moment it was pressed. Every
			// other key is unaffected - TrackModifierState only corrects a modifier keysym - so this costs
			// nothing and makes a Shift down report Shift.
			var keyArgs = MakeKeyEventArgs(keysym, this.lastModifierState);

			this.aggSystemWindow.OnKeyDown(keyArgs);
			Keyboard.SetKeyDownState(keyArgs.KeyCode, true);

			// A Control chord is a shortcut, never text: typing Ctrl+S must not also insert an "s" into
			// whatever has focus. (XLookupString would hand back the C0 control character for it, 0x13, which
			// is worse than nothing.) The mac host makes the same cut on Command.
			if (keyArgs.SuppressKeyPress || (keyEvent.State & X11.ControlMask) != 0)
			{
				return;
			}

			if (byteCount <= 0)
			{
				return;
			}

			// Every character is forwarded, control characters included, because that is exactly what the
			// Windows sink does - WM_CHAR delivers \b, \t and \r and WinformsEventSink passes them straight
			// through. InternalTextEditWidget is where the filtering lives (it ignores everything under 32
			// except \r and \t), so a host that filtered here would be second-guessing the widget and would
			// silently differ from Windows. Nothing is skipped for being in a private-use range either: that
			// is an AppKit quirk, and on X11 a named key produces no text at all.
			string typed = Encoding.UTF8.GetString(typedBytes, byteCount);
			foreach (char character in typed)
			{
				this.aggSystemWindow.OnKeyPress(new KeyPressEventArgs(character));
			}
		}

		private unsafe void HandleKeyRelease(ref XKeyEvent keyEvent)
		{
			if (IsAutoRepeatRelease(ref keyEvent))
			{
				return;
			}

			// The text this produces is thrown away - a release types nothing - but the keysym has to come
			// from the same lookup the press used or a shifted key would resolve differently on the way up.
			byte* typedBytes = stackalloc byte[32];
			Xlib.XLookupString(ref keyEvent, typedBytes, 32, out ulong keysym, IntPtr.Zero);

			// Computed into a local rather than read back off lastModifierState, because the state has to be
			// corrected for this event before the args are built but written into the field only after the
			// down-state read below. Releasing Shift must report Shift as no longer held, the mirror of the
			// press correction in HandleKeyPress.
			uint stateAfterThisKey = StateAfterModifierKey(keyEvent.State, keysym, pressed: false);

			var keyArgs = MakeKeyEventArgs(keysym, stateAfterThisKey);

			// Read before TrackModifierState, and that ordering is the whole of a bug worth naming. Only if
			// we saw the down, matching the Windows sink and the mac host: a dialog that closed on a key down
			// hands us back the key up for it, and a widget told about an up it never saw a down for will act
			// on it. But for a bare modifier the key being released and the modifier state being updated are
			// the same event on X11 - unlike AppKit, where flagsChanged is separate from keyUp - so
			// TrackModifierState clears exactly the down state this test reads. Asking afterwards means every
			// modifier release answers "it was not down" and no OnKeyUp for Shift, Control or Alt is ever
			// delivered. Observed: Shift+Tab produced a ShiftKey down with no matching up.
			bool sawTheKeyDown = Keyboard.IsKeyDown(keyArgs.KeyCode);

			this.TrackModifierState(keyEvent.State, keysym, pressed: false);

			if (sawTheKeyDown)
			{
				this.aggSystemWindow.OnKeyUp(keyArgs);
				Keyboard.SetKeyDownState(keyArgs.KeyCode, false);
			}
		}

		/// <summary>
		/// Whether a <c>KeyRelease</c> is the first half of an autorepeat rather than the user letting go.
		/// </summary>
		/// <remarks>
		/// X11 spells a held key as a KeyRelease immediately followed by a KeyPress carrying the same
		/// keycode and the <em>same timestamp</em> - the server has no "this is a repeat" flag, and the one
		/// way to ask for one (<c>XkbSetDetectableAutoRepeat</c>) is a per-connection server setting this
		/// host would then own on behalf of every library sharing the display. Peeking at the next event
		/// instead costs nothing: the pair is already decoded in Xlib's queue by the time the release is
		/// handled, because they arrive in one batch.
		/// <para/>
		/// Swallowing the release is what turns a held key into repeated KeyDown/KeyPress, which is what
		/// Windows and macOS both deliver. Letting it through makes a held arrow key a stream of down/up
		/// pairs, with <see cref="Keyboard"/>'s down state flickering off between each one - so anything that
		/// asks "is this key held" during a repeat gets the wrong answer half the time.
		/// <para/>
		/// <b>Known limits, and why they are acceptable.</b> A peek of exactly one event can be wrong in two
		/// directions, and both fail the same safe way. The repeat's KeyPress can be pushed back a slot by an
		/// unrelated event the server interleaved between the pair (a MotionNotify from a mouse being moved
		/// during the repeat is the realistic one), and the pair can be split across two reads when the
		/// release is the last event decoded and its KeyPress has not arrived from the socket yet. Either way
		/// the release is not recognised, is delivered, and the repeat's press follows it - so the symptom is
		/// a spurious up/down pair inside a repeat, which reads as a key being retyped. It is never a stuck
		/// key: the release is delivered rather than lost, so the down state cannot be left latched. Fixing
		/// it properly means draining and reordering the queue, or owning
		/// <c>XkbSetDetectableAutoRepeat</c> for the whole process; neither is worth it for a glitch that
		/// costs a repeated character.
		/// </remarks>
		private static unsafe bool IsAutoRepeatRelease(ref XKeyEvent releaseEvent)
		{
			// XPeekEvent blocks when the queue is empty, so the count has to be in hand first.
			if (display == IntPtr.Zero || Xlib.XPending(display) <= 0)
			{
				return false;
			}

			Xlib.XPeekEvent(display, out XEvent nextEvent);

			if (nextEvent.Type != X11.KeyPress)
			{
				return false;
			}

			ref XKeyEvent nextKey = ref nextEvent.As<XKeyEvent>();

			return nextKey.Window == releaseEvent.Window
				&& nextKey.Keycode == releaseEvent.Keycode
				&& nextKey.Time == releaseEvent.Time;
		}

		/// <summary>
		/// Remembers what the keyboard is holding after this key event, and - for a bare modifier - writes it
		/// into <see cref="Keyboard"/>.
		/// </summary>
		/// <remarks>
		/// Restricted to modifier keysyms on purpose. Writing the modifier down state on every key press
		/// would overwrite what an automation run put there directly (it sets Shift down and then sends a
		/// key), which is the same reason the mac host only writes it from flagsChanged.
		/// </remarks>
		private void TrackModifierState(uint state, ulong keysym, bool pressed)
		{
			this.lastModifierState = StateAfterModifierKey(state, keysym, pressed);

			if (ModifierMaskForKeySym(keysym) != 0)
			{
				this.appliedModifierKeys = ApplyModifierFlagsToKeyboard(this.lastModifierState);
			}
		}

		// -----------------------------------------------------------------------------------------
		// Pointer grab
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Takes or releases the X pointer grab so that it matches whether a drag this window owns is in
		/// flight.
		/// </summary>
		/// <remarks>
		/// <b>Why both this and <see cref="OutOfViewMouseCapture"/>.</b> They solve the two halves of one
		/// problem and neither is enough alone. Without the grab, X11 simply does not deliver motion or a
		/// button release that happens outside the window - the events go to whatever window the pointer is
		/// over, and a drag that leaves the window goes silent mid-gesture and its up never arrives, leaving
		/// the widget convinced its button is still held. The grab is what makes those events exist here at
		/// all. But a grab with <c>owner_events</c> true also hands this window events that are <em>not</em>
		/// its business, and it does not distinguish a drag that started inside from a press that did not, so
		/// the filter is still what decides which of the delivered events agg should see. AppKit needs only
		/// the filter because it routes a drag to the window that saw the down for free; X11 has no such
		/// rule, which is why this half exists here and not there.
		/// <para/>
		/// <c>owner_events</c> is true so that events over this window keep being reported in this window's
		/// coordinates rather than being forced through the grab window - which is the same window here, but
		/// the setting is also what keeps the grab from swallowing events other windows of this client
		/// should get. Both modes are Async: a Sync grab freezes the device after every event until
		/// <c>XAllowEvents</c> lets the next one through, which in a single-threaded pump is a deadlock
		/// waiting to happen.
		/// </remarks>
		/// <param name="time">The timestamp of the event asking for this, so a stale request is refused by
		/// the server rather than grabbing on something the user has since finished doing.</param>
		private void SyncPointerGrab(ulong time)
		{
			if (display == IntPtr.Zero || this.window == X11.None)
			{
				return;
			}

			if (this.mouseCapture.HasCapturedButtons)
			{
				if (!this.pointerGrabbed)
				{
					int result = Xlib.XGrabPointer(
						display,
						this.window,
						X11.True,
						(uint)(X11.ButtonPressMask | X11.ButtonReleaseMask | X11.PointerMotionMask),
						X11.GrabModeAsync,
						X11.GrabModeAsync,
						X11.None,
						X11.None,
						time);

					// A refusal (another client already holds the pointer) is not fatal: the drag still works
					// inside the window, it just goes quiet if the pointer leaves. Recording the failure is
					// what keeps the release from ungrabbing a grab somebody else owns.
					this.pointerGrabbed = result == X11.GrabSuccess;
				}

				return;
			}

			this.ReleasePointerGrab();
		}

		/// <summary>
		/// Drops the pointer grab if this window holds one. Flushed rather than left in the output buffer:
		/// until the ungrab reaches the server every other client's pointer input is still being routed here,
		/// so a delay of even one pump pass is a desktop that feels stuck.
		/// </summary>
		private void ReleasePointerGrab()
		{
			if (!this.pointerGrabbed)
			{
				return;
			}

			this.pointerGrabbed = false;

			if (display != IntPtr.Zero)
			{
				Xlib.XUngrabPointer(display, X11.CurrentTime);
				Xlib.XFlush(display);
			}
		}

		// -----------------------------------------------------------------------------------------
		// The pure parts of the translation - no Xlib, so they can be tested without a server
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Converts an X11 event's Y into agg's.
		/// </summary>
		/// <remarks>
		/// <b>The one conversion X11 needs and macOS does not.</b> X11's origin is the top-left with Y
		/// increasing downwards, which is Win32's convention and not agg's - a non-flipped NSView is already
		/// bottom-left, which is why <c>MacSystemWindow</c> has no flip at all and copying its absence here
		/// would put every click on the wrong half of the window.
		/// <para/>
		/// <c>height - y</c> and not <c>height - 1 - y</c>, which is <c>WinformsEventSink</c>'s convention
		/// exactly (<c>(int)widgetToSendTo.Height - y</c>). The off-by-one is only apparent: agg's bounds are
		/// a closed interval, so a window of height H spans y = 0 through y = H rather than H-1, and this is
		/// the mapping the rest of the stack is built around. <c>AutomationRunner</c> converts the other way
		/// with the same <c>Height - y</c>, so a synthetic click round-trips exactly; subtracting one here
		/// would land every automated click one pixel low.
		/// </remarks>
		internal static double FlipY(int eventY, uint pixelHeight) => (double)pixelHeight - eventY;

		/// <summary>Maps an X11 button number onto agg's <see cref="MouseButtons"/>.</summary>
		/// <returns><see cref="MouseButtons.None"/> for a button agg has no name for.</returns>
		internal static MouseButtons TranslateButton(uint button) => button switch
		{
			X11.Button1 => MouseButtons.Left,

			// X11 numbers the buttons by physical position, so 2 is the middle one and 3 is the right one.
			// Win32 and AppKit both number them by role instead, which is why this pair looks transposed.
			X11.Button2 => MouseButtons.Middle,
			X11.Button3 => MouseButtons.Right,
			_ => MouseButtons.None,
		};

		/// <summary>The state-word bit that is set while an X11 button number is held.</summary>
		/// <returns>Zero for a button with no bit - the wheel's 6 and 7, and the thumb buttons.</returns>
		internal static uint ButtonStateMaskForButtonNumber(uint button) => button switch
		{
			X11.Button1 => X11.Button1Mask,
			X11.Button2 => X11.Button2Mask,
			X11.Button3 => X11.Button3Mask,
			_ => 0,
		};

		/// <summary>The state-word bit that is set while an agg button is held.</summary>
		internal static uint ButtonStateMaskFor(MouseButtons button) => button switch
		{
			MouseButtons.Left => X11.Button1Mask,
			MouseButtons.Middle => X11.Button2Mask,
			MouseButtons.Right => X11.Button3Mask,
			_ => 0,
		};

		/// <summary>
		/// The button a motion or crossing event is carrying, from the button half of its state word.
		/// </summary>
		/// <remarks>
		/// A state word can name several buttons at once, but <see cref="MouseEventArgs.Button"/> is one
		/// value and not a flag set, so one has to win. Left first, then right, then middle: that is the
		/// order of how likely a widget is to be mid-gesture on it, and it matches what WinForms reports for
		/// a move while more than one button is down.
		/// </remarks>
		internal static MouseButtons TranslateButtonState(uint state)
		{
			if ((state & X11.Button1Mask) != 0)
			{
				return MouseButtons.Left;
			}

			if ((state & X11.Button3Mask) != 0)
			{
				return MouseButtons.Right;
			}

			if ((state & X11.Button2Mask) != 0)
			{
				return MouseButtons.Middle;
			}

			return MouseButtons.None;
		}

		/// <summary>
		/// Fills a wheel event's axes from the synthetic button the detent arrived on.
		/// </summary>
		/// <remarks>
		/// Buttons 4 and 5 are the wheel forward and back; 6 and 7 are the horizontal pair a tilt wheel (or a
		/// touchpad driver emulating one) sends. The vertical sign is agg's existing convention - forward is
		/// positive, which every consumer reads as zoom in or scroll up. The horizontal sign follows
		/// <see cref="MouseEventArgs.WheelDeltaX"/>'s: positive means the content should move right,
		/// revealing what is off the left edge, which is what a leftward tilt (button 6) asks for.
		/// <para/>
		/// Never a precise scroll. A detent carries no distance at all - it is one click - so the consumer
		/// picks its own step, exactly as on Windows. That is also why no DPI is applied here: this is the
		/// one place a precise scroll would need it, and X11 has no precise scroll to give. (A high
		/// resolution wheel reports through XInput2 valuators, which this host does not use.)
		/// </remarks>
		internal static void ApplyButtonWheelDeltas(MouseEventArgs args, uint button)
		{
			switch (button)
			{
				case X11.Button4:
					args.WheelDelta = WheelDeltaPerDetent;
					break;

				case X11.Button5:
					args.WheelDelta = -WheelDeltaPerDetent;
					break;

				case X11.Button6:
					args.WheelDeltaX = WheelDeltaPerDetent;
					break;

				case X11.Button7:
					args.WheelDeltaX = -WheelDeltaPerDetent;
					break;
			}

			args.WheelDeltaIsPreciseScroll = false;
		}

		/// <summary>
		/// Whether a point in window coordinates lies within the window.
		/// </summary>
		/// <remarks>
		/// Exclusive on the far edges, unlike the mac host's inclusive version. That is not a change of mind
		/// but a change of units: AppKit reports a point in a continuous coordinate space where the bounds
		/// width <em>is</em> the right edge, while an X11 coordinate is an integer pixel index and a window
		/// of width W has columns 0 through W-1. A pointer leaving to the right reports exactly W, so an
		/// inclusive test would call a real exit "inside" and the pointer-gone sentinel would never fire.
		/// </remarks>
		internal static bool IsInsideBounds(int x, int y, uint width, uint height)
			=> x >= 0 && y >= 0 && x < (int)width && y < (int)height;

		/// <summary>
		/// Whether a <c>LeaveNotify</c> means the pointer actually left the window.
		/// </summary>
		/// <remarks>
		/// The event type on its own does not mean that, which is the trap this exists for - the same trap
		/// the mac host hits with cursor-rect rebuilds, reached by a different route. On X11 the artifact is
		/// a grab: taking or dropping a pointer grab synthesises a LeaveNotify/EnterNotify pair "as if the
		/// pointer warped", so a host that believes every leave fires the pointer-gone sentinel every time a
		/// drag begins. The <c>mode</c> field is what tells those apart, and the geometry is what catches the
		/// rest.
		/// <para/>
		/// A drag holding a captured button is exempt as well: it owns the pointer wherever it has gone, and
		/// its button release is what ends it. Getting this wrong is what makes MatterCAD's 3D view snap a
		/// dragged part back to where the drag started.
		/// </remarks>
		internal static bool IsRealPointerExit(int x, int y, uint width, uint height, bool dragInFlight, int mode)
			=> !dragInFlight
				&& mode == X11.NotifyNormal
				&& !IsInsideBounds(x, y, width, height);

		/// <summary>
		/// Composes the agg key event a <c>KeyPress</c> or <c>KeyRelease</c> carries, from the two parts of
		/// the X event that determine it.
		/// </summary>
		/// <remarks>
		/// Pure - no Xlib calls, no state - so the whole key translation can be exercised without a server,
		/// in the same spirit as <c>MacSystemWindow.MakeKeyEventArgs</c>.
		/// </remarks>
		/// <param name="keysym">What <c>XLookupString</c> resolved the keycode to under the active layout.</param>
		/// <param name="state">The event's <c>state</c> word; only its modifier half is read.</param>
		internal static KeyEventArgs MakeKeyEventArgs(ulong keysym, uint state)
			=> new KeyEventArgs(TranslateKeySym(keysym) | TranslateModifiers(state));

		/// <summary>
		/// Maps an X11 keysym onto agg's <see cref="Keys"/>.
		/// </summary>
		/// <remarks>
		/// A keysym and not a keycode, because a keycode is a hardware position - "where S sits on a US
		/// layout" is another letter on an AZERTY one - and every agg shortcut is spelled as a key
		/// (Ctrl+S, Ctrl+Z). The keysym is what the active layout says that position produces, which is the
		/// thing worth matching on.
		/// <para/>
		/// Case is folded, which is what makes Shift+Z and z the same <see cref="Keys.Z"/>: the keysym for a
		/// shifted letter is the uppercase one, and WinForms reports the same key code either way.
		/// </remarks>
		/// <returns><see cref="Keys.None"/> for a keysym agg has no key for - a dead key, a media key, a
		/// letter outside Latin-1. The modifiers still ride along on the event; see
		/// <see cref="MakeKeyEventArgs"/>.</returns>
		internal static Keys TranslateKeySym(ulong keysym)
		{
			// Latin-1 needs no table at all: keysyms 0x20 to 0xFF are exactly their ISO 8859-1 code points,
			// which is what lets every letter, digit and punctuation key be answered by looking at the
			// character it is.
			if (keysym >= X11.XK_space && keysym <= 0x00FF)
			{
				return TranslateLatin1KeySym((char)keysym);
			}

			// Both of these are contiguous blocks in keysymdef.h, so they are ranges rather than 22 cases.
			if (keysym >= X11.XK_F1 && keysym <= X11.XK_F12)
			{
				return Keys.F1 + (int)(keysym - X11.XK_F1);
			}

			if (keysym >= X11.XK_KP_0 && keysym <= X11.XK_KP_9)
			{
				return Keys.NumPad0 + (int)(keysym - X11.XK_KP_0);
			}

			return keysym switch
			{
				X11.XK_BackSpace => Keys.Back,

				// XK_ISO_Left_Tab is not an obscure corner: it is what an ordinary Shift+Tab produces, and a
				// host that does not name it loses back-tab navigation entirely.
				X11.XK_Tab or X11.XK_ISO_Left_Tab or X11.XK_KP_Tab => Keys.Tab,
				X11.XK_Return or X11.XK_KP_Enter => Keys.Enter,
				X11.XK_Escape => Keys.Escape,
				X11.XK_Delete or X11.XK_KP_Delete => Keys.Delete,
				X11.XK_Insert or X11.XK_KP_Insert => Keys.Insert,
				X11.XK_Home or X11.XK_KP_Home => Keys.Home,
				X11.XK_End or X11.XK_KP_End => Keys.End,
				X11.XK_Page_Up or X11.XK_KP_Page_Up => Keys.PageUp,
				X11.XK_Page_Down or X11.XK_KP_Page_Down => Keys.PageDown,
				X11.XK_Left or X11.XK_KP_Left => Keys.Left,
				X11.XK_Up or X11.XK_KP_Up => Keys.Up,
				X11.XK_Right or X11.XK_KP_Right => Keys.Right,
				X11.XK_Down or X11.XK_KP_Down => Keys.Down,

				// Keypad 5 with Num Lock off. VK_CLEAR is what Win32 calls the same key.
				X11.XK_Begin or X11.XK_KP_Begin => Keys.Clear,

				X11.XK_KP_Space => Keys.Space,
				X11.XK_KP_Multiply => Keys.Multiply,
				X11.XK_KP_Add => Keys.Add,
				X11.XK_KP_Separator => Keys.Separator,
				X11.XK_KP_Subtract => Keys.Subtract,
				X11.XK_KP_Decimal => Keys.Decimal,
				X11.XK_KP_Divide => Keys.Divide,

				// agg has no keypad-equals; the main-row one is the nearest thing that means the same.
				X11.XK_KP_Equal => Keys.Oemplus,

				X11.XK_Pause => Keys.Pause,
				X11.XK_Scroll_Lock => Keys.Scroll,
				X11.XK_Num_Lock => Keys.NumLock,
				X11.XK_Caps_Lock => Keys.CapsLock,
				X11.XK_Print => Keys.PrintScreen,

				// The context-menu key, which Win32 calls VK_APPS. Not agg's Keys.Menu, which is Alt.
				X11.XK_Menu => Keys.Apps,

				// A bare modifier is a real KeyPress/KeyRelease on X11, unlike AppKit's separate
				// FlagsChanged, so these have to resolve to the physical key rather than to None.
				X11.XK_Shift_L or X11.XK_Shift_R => Keys.ShiftKey,
				X11.XK_Control_L or X11.XK_Control_R => Keys.ControlKey,

				// Meta alongside Alt because a keyboard mapped the traditional way (and every Sun-derived
				// layout) puts Meta where a PC keyboard puts Alt.
				X11.XK_Alt_L or X11.XK_Alt_R or X11.XK_Meta_L or X11.XK_Meta_R => Keys.Menu,

				X11.XK_Super_L => Keys.LWin,
				X11.XK_Super_R => Keys.RWin,

				_ => Keys.None,
			};
		}

		/// <summary>
		/// Maps a keysym that is its own Latin-1 character - every letter, digit, space and punctuation key -
		/// onto agg's <see cref="Keys"/>.
		/// </summary>
		/// <remarks>
		/// Each punctuation key is listed under both of its spellings, unshifted and shifted, because the
		/// keysym Shift produces is the shifted symbol while WinForms reports one Oem key either way. Without
		/// the second spelling Ctrl+Shift+= would be a different key from Ctrl+=.
		/// </remarks>
		private static Keys TranslateLatin1KeySym(char keysymCharacter)
		{
			char upper = char.ToUpperInvariant(keysymCharacter);

			if (upper >= 'A' && upper <= 'Z')
			{
				return Keys.A + (upper - 'A');
			}

			if (upper >= '0' && upper <= '9')
			{
				return Keys.D0 + (upper - '0');
			}

			switch (keysymCharacter)
			{
				case ' ':
					return Keys.Space;

				case ';':
				case ':':
					return Keys.OemSemicolon;

				// The zoom shortcuts: Ctrl+= and Ctrl++ are one key, as are Ctrl+- and Ctrl+_.
				case '=':
				case '+':
					return Keys.Oemplus;

				case ',':
				case '<':
					return Keys.Oemcomma;

				case '-':
				case '_':
					return Keys.OemMinus;

				case '.':
				case '>':
					return Keys.OemPeriod;

				case '/':
				case '?':
					return Keys.OemQuestion;

				case '`':
				case '~':
					return Keys.Oemtilde;

				case '[':
				case '{':
					return Keys.OemOpenBrackets;

				case '\\':
				case '|':
					return Keys.OemPipe;

				case ']':
				case '}':
					return Keys.OemCloseBrackets;

				case '\'':
				case '"':
					return Keys.OemQuotes;

				default:
					return Keys.None;
			}
		}

		/// <summary>
		/// The X modifier mask a keysym is the key for, or zero when it is not a modifier.
		/// </summary>
		/// <remarks>
		/// Mod1 is Alt and Mod4 is Super only by convention - X11 itself only knows Mod1 through Mod5, and
		/// which physical key sits on which is a property of the keymap. Every desktop in use follows this
		/// convention, and reading the modifier map to find out for certain would be a round trip per key.
		/// </remarks>
		internal static uint ModifierMaskForKeySym(ulong keysym) => keysym switch
		{
			X11.XK_Shift_L or X11.XK_Shift_R => X11.ShiftMask,
			X11.XK_Control_L or X11.XK_Control_R => X11.ControlMask,
			X11.XK_Alt_L or X11.XK_Alt_R or X11.XK_Meta_L or X11.XK_Meta_R => X11.Mod1Mask,
			X11.XK_Super_L or X11.XK_Super_R => X11.Mod4Mask,
			_ => 0,
		};

		/// <summary>
		/// The modifier state after a key event, given the state word the event carries.
		/// </summary>
		/// <remarks>
		/// X11's <c>state</c> is the state <em>before</em> the event, which for an ordinary key is exactly
		/// what is wanted (Shift+A reports Shift held) but for the modifier keys themselves is always one
		/// event behind: the KeyPress of Shift carries no ShiftMask and its KeyRelease carries one. Applying
		/// that word straight to <see cref="Keyboard"/> would leave every modifier reported inverted for as
		/// long as it is held.
		/// <para/>
		/// Caps Lock is deliberately not corrected. It toggles rather than latches, so neither press nor
		/// release means what this function would compute - and agg has no modifier for it anyway, so
		/// <see cref="ModifierDownStateKeys"/> ignores LockMask entirely.
		/// </remarks>
		internal static uint StateAfterModifierKey(uint state, ulong keysym, bool pressed)
		{
			uint modifierState = state & X11.AllModifierMask;
			uint mask = ModifierMaskForKeySym(keysym);

			if (mask == 0)
			{
				return modifierState;
			}

			return pressed ? modifierState | mask : modifierState & ~mask;
		}

		/// <summary>
		/// Maps an X11 modifier state word onto the agg down-state keys it implies.
		/// </summary>
		/// <remarks>
		/// The answer is a set and not an OR'd <see cref="Keys"/> value because ShiftKey (16), ControlKey
		/// (17) and Menu (18) are consecutive integers rather than disjoint bits - OR-ing them would produce
		/// unrelated key codes. The modifier <em>flags</em> <see cref="TranslateModifiers"/> returns are
		/// disjoint bits and do combine.
		/// <para/>
		/// Lock (Caps Lock), Mod2 (Num Lock), Mod4 (Super) and Mod5 (AltGr) are all deliberately absent: agg
		/// has no modifier for any of them, and mistaking one for a modifier it does have would make a user
		/// with Caps Lock on unable to click on anything normally.
		/// </remarks>
		internal static IReadOnlySet<Keys> ModifierDownStateKeys(uint state)
		{
			var downKeys = new HashSet<Keys>();

			if ((state & X11.ShiftMask) != 0)
			{
				downKeys.Add(Keys.ShiftKey);
			}

			if ((state & X11.ControlMask) != 0)
			{
				downKeys.Add(Keys.ControlKey);
			}

			if ((state & X11.Mod1Mask) != 0)
			{
				downKeys.Add(Keys.Menu);
			}

			return downKeys;
		}

		/// <summary>
		/// The modifier bits agg carries on a <see cref="KeyEventArgs"/> and reports from
		/// <see cref="ModifierKeys"/>.
		/// </summary>
		/// <remarks>
		/// Expressed in terms of <see cref="ModifierDownStateKeys"/> so the two cannot drift apart: what
		/// <c>Keyboard.IsKeyDown(Keys.Control)</c> says and what <see cref="ModifierKeys"/> says have to
		/// agree, or a gesture that checks one and a shortcut that checks the other disagree about the same
		/// keyboard.
		/// </remarks>
		internal static Keys TranslateModifiers(uint state)
		{
			Keys modifiers = Keys.None;

			foreach (Keys downKey in ModifierDownStateKeys(state))
			{
				// Unlike the down-state keys these are disjoint bits, so they OR cleanly.
				modifiers |= downKey switch
				{
					Keys.ShiftKey => Keys.Shift,
					Keys.ControlKey => Keys.Control,
					Keys.Menu => Keys.Alt,
					_ => Keys.None,
				};
			}

			return modifiers;
		}

		/// <summary>
		/// Puts the modifier down state a state word implies into <see cref="Keyboard"/>, and reports the
		/// keys it left held so <see cref="ReleaseAppliedModifierKeys"/> can undo exactly those.
		/// </summary>
		/// <remarks>
		/// Every modifier is written on every call, including the ones being released. There is no "has this
		/// changed?" test here on purpose: <c>Keyboard.SetKeyDownState</c> is idempotent and raises
		/// StateChanged only on a real change, so the redundant writes cost nothing, and a test here could
		/// only compare the physical spelling (ControlKey) while automation latches the fanned-out one
		/// (Control) - it would conclude "no change" and leave the very latch this call exists to correct.
		/// </remarks>
		internal static IReadOnlySet<Keys> ApplyModifierFlagsToKeyboard(uint state)
		{
			IReadOnlySet<Keys> shouldBeDown = ModifierDownStateKeys(state);
			foreach (Keys modifierKey in ModifierStateKeys)
			{
				Keyboard.SetKeyDownState(modifierKey, shouldBeDown.Contains(modifierKey));
			}

			return shouldBeDown;
		}

		/// <summary>
		/// Releases the modifier keys this window put into the down state, and reports the state word that
		/// now describes what it is holding - nothing.
		/// </summary>
		/// <remarks>
		/// Narrow on purpose, where a <c>Keyboard.Clear()</c> would not be. <see cref="Keyboard"/> is
		/// process-wide and other callers write to it directly - an automation test sets Shift down and then
		/// shift-clicks - so a blunt clear turns any incidental focus change into a dropped selection with no
		/// visible cause. Releasing only what this window applied cannot reach anything it did not put there,
		/// which is what lets <see cref="HandleFocusLost"/> run unguarded.
		/// </remarks>
		internal static uint ReleaseAppliedModifierKeys(IReadOnlySet<Keys> appliedModifierKeys)
		{
			foreach (Keys modifierKey in appliedModifierKeys)
			{
				Keyboard.SetKeyDownState(modifierKey, false);
			}

			return 0;
		}

		/// <summary>
		/// Remembers which buttons went down inside the window, so a drag that wanders outside it still
		/// delivers its moves and, critically, its button release.
		/// </summary>
		/// <remarks>
		/// A straight port of <c>MacSystemWindow.OutOfViewMouseCapture</c>, which is itself WinForms' implicit
		/// capture written out by hand: a button is "ours" only if its press landed inside, and drags and the
		/// matching release are then delivered wherever the pointer has gone. On X11 it works alongside
		/// <see cref="SyncPointerGrab"/> rather than instead of it - see that method for which half does what.
		/// <para/>
		/// A press that landed outside is never captured, which is what keeps a title-bar drag (whose press
		/// agg never saw) from delivering a phantom release. Plain hover moves outside the window are still
		/// dropped: with no button held they really are nobody's business.
		/// </remarks>
		internal sealed class OutOfViewMouseCapture
		{
			// Not a bit set: MouseButtons is not [Flags], and more than one button can be held at once.
			private readonly HashSet<MouseButtons> capturedButtons = new HashSet<MouseButtons>();

			/// <summary>
			/// Whether a drag this window owns is in flight, and so the pointer is its business wherever it is.
			/// </summary>
			internal bool HasCapturedButtons => this.capturedButtons.Count > 0;

			/// <summary>
			/// Drops any captured button the server no longer reports as held.
			/// </summary>
			/// <remarks>
			/// The recovery path, and X11 is the platform that needs one. The captured set is only ever
			/// emptied by the button release that ends the drag, and there are ways for that release never to
			/// arrive: the grab was refused because another client already held the pointer, or was broken by
			/// a window manager taking one of its own mid-drag, or the button came up over another screen. A
			/// button stuck in this set is a window that keeps claiming every move on the desktop is part of a
			/// drag that ended minutes ago, and nothing else would ever clear it.
			/// <para/>
			/// Every mouse event carries the truth alongside the question, which is what makes this cheap:
			/// the state word says which buttons are physically down right now, so the set can simply be
			/// intersected with it on the way past. Note the caller has to correct for a press reporting the
			/// state <em>before</em> itself, or this would drop the button being captured on the very event
			/// that captures it.
			/// </remarks>
			/// <param name="heldButtonMask">The button half of a state word, corrected for the event carrying it.</param>
			internal void ReconcileWithButtonState(uint heldButtonMask)
			{
				if (this.capturedButtons.Count == 0)
				{
					return;
				}

				this.capturedButtons.RemoveWhere(
					captured => (heldButtonMask & ButtonStateMaskFor(captured)) == 0);
			}

			/// <summary>
			/// Forgets every captured button, for the case where no release is coming at all - see
			/// <see cref="HandleFocusLost"/>.
			/// </summary>
			internal void ClearCapturedButtons() => this.capturedButtons.Clear();

			/// <summary>
			/// Decides whether an event should reach agg, and updates the captured-button set.
			/// </summary>
			/// <param name="eventType">The X11 event type - ButtonPress, ButtonRelease or MotionNotify.</param>
			/// <param name="button">The agg button the event carries, or None for a hover.</param>
			/// <param name="insideWindow">Whether the event's point lies within the window.</param>
			internal bool ShouldDeliver(int eventType, MouseButtons button, bool insideWindow)
			{
				switch (eventType)
				{
					case X11.ButtonPress:
						if (!insideWindow)
						{
							return false;
						}

						this.capturedButtons.Add(button);
						return true;

					case X11.ButtonRelease:
						// Removed whether or not it is delivered, so a button can never stay captured.
						bool wasCaptured = this.capturedButtons.Remove(button);
						return insideWindow || wasCaptured;

					case X11.MotionNotify:
						// X11 has one motion event for both hover and drag - the button is what tells them
						// apart, where AppKit has a separate event type. A hover outside is nobody's business;
						// a drag outside is ours if its press was.
						return insideWindow
							|| (button != MouseButtons.None && this.capturedButtons.Contains(button));

					default:
						return insideWindow;
				}
			}
		}

		/// <summary>
		/// Turns a stream of button presses into single, double and triple clicks.
		/// </summary>
		/// <remarks>
		/// X11 has no click count. Win32 puts one on every WM_LBUTTONDBLCLK and AppKit puts one on every
		/// event, but on X11 a double click is just two presses and every toolkit counts them itself, from
		/// the timestamps the server does provide. Both thresholds have to be tested, not just the time: a
		/// double click at two different places is two clicks, and using only the clock makes a fast user
		/// clicking down a list select the wrong thing.
		/// <para/>
		/// The timestamps are the server's own clock (milliseconds since the server started), not this
		/// process's - which is what makes the interval right even when the pump was stalled between the two
		/// presses.
		/// </remarks>
		internal sealed class ClickCounter
		{
			private uint lastButton;
			private ulong lastTime;
			private int lastX;
			private int lastY;
			private int clicks;

			/// <summary>
			/// The count the last press produced, which is what the matching release reports. Zero before any
			/// press.
			/// </summary>
			internal int LastClickCount => this.clicks;

			/// <summary>Counts a press and reports what click it is - 1, 2, 3 and up.</summary>
			/// <param name="button">The X11 button number.</param>
			/// <param name="time">The event's server timestamp, in milliseconds.</param>
			/// <param name="x">The press position in window coordinates, unflipped - only the distance
			/// between two of them is read, and that is the same either way round.</param>
			/// <param name="y">See <paramref name="x"/>.</param>
			internal int CountPress(uint button, ulong time, int x, int y)
			{
				bool continuesTheLastClick = this.clicks > 0
					&& button == this.lastButton

					// The server's clock is milliseconds in a 32-bit field, so it wraps every 49.7 days. The
					// ordering test is what keeps a wrap from being read as an enormous interval (harmless) or,
					// on unsigned subtraction, an enormous one that underflows back into range (not).
					&& time >= this.lastTime
					&& (time - this.lastTime) <= DoubleClickMilliseconds
					&& Math.Abs(x - this.lastX) <= DoubleClickSlopPixels
					&& Math.Abs(y - this.lastY) <= DoubleClickSlopPixels;

				this.clicks = continuesTheLastClick ? this.clicks + 1 : 1;

				this.lastButton = button;
				this.lastTime = time;
				this.lastX = x;
				this.lastY = y;

				return this.clicks;
			}
		}

		// -----------------------------------------------------------------------------------------
		// Painting
		// -----------------------------------------------------------------------------------------

		private void PaintFrame()
		{
			this.needsRedraw = false;

			if (this.aggSystemWindow == null
				|| this.aggSystemWindow.HasBeenClosed
				|| this.webGpuLayer == null
				|| this.webGpuLayer.IsDisposed)
			{
				return;
			}

			// An unattended run must fail loudly rather than sitting there: a paint that throws takes the
			// repaint pump with it, so the run would otherwise just hang.
			if (SmokeFrameTarget > 0)
			{
				try
				{
					this.DrawAndPresent();
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"AGG_SMOKE paint failed on frame {this.drawCount}: {ex}");
					Environment.ExitCode = 1;
					this.smokeRunFinished = true;
					UiThread.RunOnIdle(this.FinishSmokeRun);
				}

				return;
			}

			this.DrawAndPresent();
		}

		private void DrawAndPresent()
		{
			MatterHackers.RenderCore.FrameProfiler.BeginFrame();

			if (this.pixelWidth > 0 && this.pixelHeight > 0)
			{
				this.drawCount++;
				this.isInsidePaint = true;

				try
				{
					Graphics2D graphics2D;
					using (MatterHackers.RenderCore.FrameProfiler.Time("NewGraphics2D+Acquire"))
					{
						graphics2D = this.NewGraphics2D();
					}

					using (MatterHackers.RenderCore.FrameProfiler.Time("WidgetTreeDraw"))
					{
						if (SingleWindowMode && this.WindowProvider != null)
						{
							// Every window this provider hosts is drawn into this one frame: the shell first,
							// then - for each dialog stacked on it - a scrim over the whole frame and the
							// dialog on top of that. Drawing only the active window would leave a dialog
							// floating on an empty background. Kept identical to the other two hosts.
							var openWindows = this.WindowProvider.OpenWindows;
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

					// A widget that rasterized into Graphics2D.DestImage drew into a CPU buffer, not into
					// the frame. On a GPU surface that buffer is a layer this uploads and draws over the
					// frame now, after every widget has had its turn.
					if (graphics2D is Graphics2DGpu gpuGraphics && gpuGraphics.HasCpuLayer)
					{
						MatterHackers.RenderCore.FrameProfiler.Count("CompositeCpuLayer");
						using (MatterHackers.RenderCore.FrameProfiler.Time("CompositeCpuLayer"))
						{
							gpuGraphics.CompositeCpuLayer();
						}
					}

					// Before the present, because a GPU window can only read a frame back while the frame's
					// texture is still the one being drawn into.
					this.CheckSmokeRunProgress();
				}
				finally
				{
					this.isInsidePaint = false;
				}

				using (MatterHackers.RenderCore.FrameProfiler.Time("Present"))
				{
					this.PresentOrCapture();
				}
			}

			MatterHackers.RenderCore.FrameProfiler.EndFrame();

			// A demo that has nothing to animate would paint once and wait forever for input that a smoke
			// run never sends, so the run pumps its own frames.
			if (SmokeFrameTarget > 0 && !this.smokeRunFinished)
			{
				this.needsRedraw = true;
			}
		}

		/// <summary>
		/// Presents the frame. Any screenshot requested for this frame is read back first: after the
		/// present the texture is the swapchain's again.
		/// </summary>
		private void PresentOrCapture()
		{
			this.viewPortHasBeenSet = false;

			string screenshotPath = this.pendingScreenshotPath;
			if (screenshotPath == null)
			{
				this.webGpuLayer.Present();
				return;
			}

			this.pendingScreenshotPath = null;
			this.CaptureThenPresent(screenshotPath, this.screenshotComplete);
		}

		/// <summary>
		/// Saves the frame and then presents it. <c>async void</c> on purpose: this is the end of a frame
		/// and there is nobody to hand a Task to. The native read-back completes before its ValueTask is
		/// returned, so the present still happens inline, while the frame is alive.
		/// </summary>
		private async void CaptureThenPresent(string path, ManualResetEventSlim completed)
		{
			try
			{
				await this.webGpuLayer.SaveCurrentFrameAsync(path);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"X11SystemWindow screenshot failed: {ex.Message}");
			}
			finally
			{
				completed?.Set();
			}

			this.webGpuLayer.Present();
		}

		private void SetAndClearViewPort()
		{
			this.webGpuLayer.BeginFrame();

			var gl = this.webGpuLayer.Gl?.GpuContext;
			if (gl == null)
			{
				return;
			}

			gl.Viewport(0, 0, (int)this.pixelWidth, (int)this.pixelHeight);
			this.viewPortHasBeenSet = true;

			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();

			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
			gl.Scissor(0, 0, (int)this.pixelWidth, (int)this.pixelHeight);

			this.NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}

		// -----------------------------------------------------------------------------------------
		// Closing
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Runs a native close request - the window manager's close button, the session ending - against the
		/// application rather than against whatever window happens to be on top, and reports whether the
		/// platform may go ahead and tear its window down.
		/// </summary>
		/// <param name="singleWindowMode">See <see cref="SingleWindowMode"/>.</param>
		/// <param name="provider">The provider holding the open windows, if there is one.</param>
		/// <param name="activeWindow">The window currently being drawn and given events.</param>
		/// <param name="setPlatformClosing">
		/// Sets (and, if the close does not take, clears) the host's "the platform is already closing" flag.
		/// </param>
		/// <remarks>
		/// Static and parameterised because the decision it makes - which window is asked, and whether the
		/// native window may go away - is the whole bug, and none of it needs X11 to exercise. Identical to
		/// <c>MacSystemWindow</c>'s and <c>WinformsSystemWindow</c>'s: the three hosts have to agree here or
		/// closing the application means something different per platform.
		/// </remarks>
		internal static bool HandlePlatformCloseRequest(
			bool singleWindowMode,
			ISystemWindowProvider provider,
			SystemWindow activeWindow,
			Action<bool> setPlatformClosing)
		{
			// The user closed the application, not the dialog drawn inside it. Asking the dialog runs none of
			// the shell's ShouldClose/Closed handlers - window bounds persistence, save on exit - and the
			// native window is torn down immediately afterwards regardless, so that work is simply lost.
			var shellWindow = ShellWindowForClose(singleWindowMode, provider, activeWindow);

			if (shellWindow == null || shellWindow.HasBeenClosed)
			{
				return true;
			}

			// Only the shell decides whether the application may close: an open dialog does not veto here.
			// In single window mode a dialog is a widget drawn inside this window, so its titlebar button is
			// the only close that belongs to it - the frame's close button has always meant "close the
			// application", and applications that want to refuse mid-dialog do it in their own ShouldClose.
			var shouldClose = new ShouldCloseEventArgs();
			shellWindow.OnShouldClose(shouldClose);

			if (shouldClose.Cancel)
			{
				return false;
			}

			// The agg close runs first so widgets get their Closed events while the window is still alive. It
			// calls back through the provider into CloseSystemWindow, which the flag makes a no-op - the
			// platform is already in the middle of closing us.
			setPlatformClosing?.Invoke(true);
			shellWindow.Close();

			if (!shellWindow.HasBeenClosed)
			{
				// Close asks OnShouldClose a second time and an application may cancel on that one (having
				// just put up its "save first?" dialog on the first ask). Letting the platform destroy the
				// window anyway is exactly the "closed with no Closed events" bug, so the shell that is still
				// open keeps its native window.
				setPlatformClosing?.Invoke(false);
				return false;
			}

			return true;
		}

		/// <summary>
		/// The agg window whose close ends the application: the shell, not whatever is currently on top.
		/// </summary>
		/// <remarks>
		/// In single window mode the active window is the one being drawn and given the events, which the
		/// provider re-points at every dialog that opens. Closing that only dismisses the dialog - the shell
		/// stays up, the event loop keeps running, and the process never exits. The provider keeps the shell
		/// first in <see cref="ISystemWindowProvider.OpenWindows"/> and takes the dialogs above it down with
		/// it, so closing that one window is the whole application closing.
		/// </remarks>
		internal static SystemWindow ShellWindowForClose(
			bool singleWindowMode,
			ISystemWindowProvider provider,
			SystemWindow activeWindow)
		{
			if (singleWindowMode && provider != null)
			{
				var openWindows = provider.OpenWindows;

				if (openWindows.Count > 0)
				{
					return openWindows[0];
				}
			}

			return activeWindow;
		}

		/// <summary>
		/// Asks the server to destroy the window. The teardown itself waits for the resulting
		/// <c>DestroyNotify</c>, so that a window destroyed by anyone - us, the window manager, the server
		/// shutting down - unwinds through exactly one path.
		/// </summary>
		private void DestroyNativeWindow()
		{
			if (this.hasClosed || this.window == X11.None || display == IntPtr.Zero)
			{
				return;
			}

			// Before anything else. Destroying the grab window releases the grab as a side effect, but only
			// once the server processes the destroy - and a grab still held while this process tears down is
			// a desktop with an unresponsive pointer, which is the one failure a user cannot click their way
			// out of.
			this.ReleasePointerGrab();

			// The swapchain goes first, while the window it was made over still exists. Vulkan's teardown
			// talks to the X server about the drawable - destroying the surface and its images are real X
			// requests - so releasing the window first turns every one of them into a BadDrawable, on every
			// close. The mac host has the same ordering for the same reason (it disposes the layer inside
			// windowWillClose:, while the NSWindow is still alive).
			this.DisposeWebGpuLayer();

			Xlib.XDestroyWindow(display, this.window);
			Xlib.XFlush(display);

			// XDestroyWindow's DestroyNotify only comes back through the queue, and the queue is only pumped
			// by a running loop. A close from outside one - or the last close, which ends the loop - would
			// otherwise leave the window half torn down forever.
			if (!runLoopActive)
			{
				this.HandleDestroyNotify();
			}
		}

		/// <summary>
		/// Releases the wgpu device and its swapchain. Separate from <see cref="HandleDestroyNotify"/> so
		/// that the ordinary close path can run it before the window is destroyed; the teardown still calls
		/// it as a fallback, for the window that went away without this host asking (the window manager
		/// killed the client, the session ended), where there is no drawable left to be tidy about.
		/// </summary>
		private void DisposeWebGpuLayer()
		{
			this.webGpuLayer?.Dispose();
			this.webGpuLayer = null;
		}

		/// <summary>Tears everything down once the window is gone from the server.</summary>
		private void HandleDestroyNotify()
		{
			if (this.hasClosed)
			{
				return;
			}

			this.hasClosed = true;

			this.DisposeWebGpuLayer();

			this.window = X11.None;
			this.aggSystemWindow = null;

			bool wasLast;
			lock (StaticInitLock)
			{
				LiveWindows.Remove(this);
				wasLast = LiveWindows.Count == 0;
			}

			if (wasLast)
			{
				runLoopActive = false;
			}
		}

		// -----------------------------------------------------------------------------------------
		// Smoke runs
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Counts frames for an <c>AGG_SMOKE_FRAMES</c> run and, on the target frame, asks for the
		/// screenshot and schedules the close. Called from inside the paint, after the widgets have drawn
		/// and before the present, which is the only moment both a finished frame and its pixels exist.
		/// </summary>
		private void CheckSmokeRunProgress()
		{
			if (SmokeFrameTarget <= 0 || this.smokeRunFinished || this.drawCount < SmokeFrameTarget)
			{
				return;
			}

			this.smokeRunFinished = true;

			if (!string.IsNullOrEmpty(SmokeScreenshotPath))
			{
				try
				{
					this.CaptureScreenshot(SmokeScreenshotPath);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"AGG_SMOKE screenshot failed: {ex}");
					Environment.ExitCode = 1;
				}
			}

			// Closing from inside a paint would tear the window down mid-frame (and before the
			// screenshot's present has run), so the close waits for this frame to finish.
			UiThread.RunOnIdle(this.FinishSmokeRun);
		}

		private void FinishSmokeRun()
		{
			string report = this.RenderErrorReport;
			if (!string.IsNullOrEmpty(report))
			{
				Console.Error.WriteLine($"AGG_SMOKE render error: {report}");
				Environment.ExitCode = 1;
			}

			string status = this.RenderStatusReport;
			string detail = $"{this.drawCount} frames on {this.GetType().Name}"
				+ (string.IsNullOrEmpty(status) ? string.Empty : $" [{status}]");

			if (Environment.ExitCode != 0)
			{
				Console.WriteLine($"AGG_SMOKE FAILED: {detail}");
			}
			else
			{
				Console.WriteLine($"AGG_SMOKE ok: {detail}");
			}

			// Armed before the close, not after: a close that throws or blocks is exactly the case the
			// watchdog exists for.
			StartSmokeExitWatchdog();

			try
			{
				// Closing the agg window is what tears the platform window down with it; the platform's own
				// close is only the fallback for a window that was never attached to one.
				var windowToClose = ShellWindowForClose(SingleWindowMode, this.WindowProvider, this.aggSystemWindow);

				if (windowToClose != null)
				{
					windowToClose.Close();
				}
				else
				{
					this.Close();
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"AGG_SMOKE: close threw {ex.GetType().Name}: {ex}");
			}
		}

		/// <summary>
		/// Guarantees a smoke run terminates. Closing the window ends the event loop, but a teardown that
		/// throws part way or a demo that left a foreground thread running would keep the process alive
		/// forever, and an unattended run that never returns is indistinguishable from a hang in the
		/// renderer. Firing is itself a failure and is reported as one.
		/// </summary>
		private static void StartSmokeExitWatchdog()
		{
			var watchdog = new System.Threading.Timer(
				_ =>
				{
					Console.Error.WriteLine("AGG_SMOKE: the process did not exit on its own after closing; forcing exit.");
					Console.WriteLine("AGG_SMOKE FAILED: the exit watchdog had to force the process down.");
					Environment.Exit(Environment.ExitCode != 0 ? Environment.ExitCode : 1);
				},
				null,
				TimeSpan.FromSeconds(5),
				System.Threading.Timeout.InfiniteTimeSpan);

			// Nothing else holds this; keeping the reference alive is the only thing standing between the
			// timer and the collector.
			smokeExitWatchdog = watchdog;
		}

		private static int ParseSmokeFrames()
		{
			return int.TryParse(Environment.GetEnvironmentVariable("AGG_SMOKE_FRAMES"), out int frames) && frames > 0
				? frames
				: 0;
		}
	}
}
