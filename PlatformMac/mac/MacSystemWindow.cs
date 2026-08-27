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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Mac;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

using static MatterHackers.Agg.Platform.Mac.AppKitConstants;
using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The macOS window host: an <c>NSWindow</c> whose content view is layer-<em>hosting</em> over a
	/// <c>CAMetalLayer</c>, a <see cref="MacWebGpuLayer"/> for the swapchain, a
	/// <see cref="Graphics2DGpu"/> over its GL facade for widget paint, and one present per frame. The
	/// structural counterpart of <c>WinformsSystemWindow</c> + <c>WebGpuSystemWindow</c> on Windows, with
	/// AppKit reached through raw <c>objc_msgSend</c> - no MonoMac, no SDL, no GLFW.
	///
	/// <para>
	/// <b>Coordinates and DPI.</b> agg has no logical/physical split: every coordinate it deals in is a
	/// device pixel. AppKit deals in points. The whole conversion therefore lives on this seam, and the
	/// rule is one line long - <em>agg pixels = AppKit points x backingScaleFactor</em>. Concretely:
	/// <list type="bullet">
	/// <item>the NSWindow's content rect is the requested agg size <em>divided</em> by the scale, so that
	/// <c>SystemWindow.Width/Height</c> are honored exactly (which is also what a DPI-aware WinForms host
	/// does: <c>ClientSize = 640x480</c> is 640x480 real pixels there too);</item>
	/// <item>the swapchain, the layer's <c>drawableSize</c>, the viewport, the scissor, the
	/// <see cref="Graphics2DGpu"/> and <see cref="SystemWindow.LocalBounds"/> all come from
	/// <c>convertRectToBacking:</c>, i.e. real pixels;</item>
	/// <item>mouse coordinates from NSEvent are points and are multiplied by the scale on the way in.</item>
	/// </list>
	/// <c>GuiWidget.DeviceScale</c> is deliberately <em>not</em> touched: it is a user text-size
	/// preference, not a DPI factor.
	/// </para>
	///
	/// <para>
	/// <b>No Y flip.</b> The Windows event sink flips mouse Y because Win32 is top-left origin. A
	/// non-flipped NSView already has a bottom-left, y-up coordinate system, which is exactly agg's, so
	/// flipping here would be a double flip. Nothing calls <c>setFlipped:</c>.
	/// </para>
	///
	/// <para>
	/// <b>The loop is ours.</b> Rather than <c>[NSApp run]</c>, the first window drives a pumped loop:
	/// drain <c>nextEventMatchingMask:</c> to nil, dispatch, then paint any window that asked to be
	/// repainted. That is what lets a frame be scheduled by <see cref="Invalidate"/> the way WM_PAINT does
	/// on Windows. RunOnIdle is pumped by a real 10ms <c>NSTimer</c> rather than by this loop, because a
	/// timer keeps firing inside AppKit's nested tracking loops (window drag, live resize, menu tracking)
	/// where the pump below is frozen.
	/// </para>
	///
	/// <para>
	/// <b>The main thread, always.</b> AppKit permits window creation - and in practice every UI call -
	/// only on the process main thread; creating an NSWindow anywhere else raises
	/// <c>NSInternalInconsistencyException</c>, which as an uncaught Objective-C exception aborts the
	/// process rather than failing a call. An application satisfies that for free, because <c>Main</c> is
	/// the thread that shows the window. A test process does not: the test engine owns <c>Main</c> and runs
	/// test bodies on thread pool workers. So every AppKit call below goes through
	/// <see cref="MainThreadDispatcher"/>, which runs the work inline when the caller is already on the
	/// main thread (the application case, and every call from inside the pump) and marshals it when it is
	/// not (the test case). Windows needs none of this - a WinForms Form is legal on any thread that pumps
	/// messages - which is why <c>WinformsSystemWindow</c> has no equivalent.
	/// </para>
	/// </summary>
	public class MacSystemWindow : IPlatformWindow
	{
		/// <summary>
		/// How many pump iterations <see cref="CaptureScreenshot"/> spins waiting for a capture whose
		/// read-back suspended. Bounded so a window that never repaints cannot hang the caller; the native
		/// read-back path completes inline and never reaches the loop.
		/// </summary>
		private const int ScreenshotPumpSpins = 200;

		/// <summary>
		/// How long a capture waits for work it handed to another thread: the queued capture
		/// <see cref="CaptureScreenshotAsync"/> awaits. Bounded for the same reason
		/// <see cref="ScreenshotPumpSpins"/> is: a window that never repaints must not hang the caller.
		/// Either entry point's marshalling hop waits on <see cref="ScreenshotMarshalTimeout"/> instead, so
		/// this bound stays the governing one.
		/// </summary>
		private static readonly TimeSpan ScreenshotAsyncTimeout = TimeSpan.FromSeconds(10);

		/// <summary>
		/// How long either entry point waits on the marshalling hop. Deliberately looser than
		/// <see cref="ScreenshotAsyncTimeout"/>: the queued capture's own bound is the governing one, and if
		/// this outer wait expired first the caller would return while the inner capture still owned
		/// <c>pendingScreenshotPath</c> - the give-up and its cleanup would never be observed here. The slack
		/// gives the inner wait time to expire, clean up and Set() before this one stops looking.
		/// </summary>
		private static readonly TimeSpan ScreenshotMarshalTimeout = ScreenshotAsyncTimeout + TimeSpan.FromSeconds(2);

		private static readonly object StaticInitLock = new object();

		/// <summary>Every constructed window that has not closed yet, in creation order.</summary>
		private static readonly List<MacSystemWindow> LiveWindows = new List<MacSystemWindow>();

		/// <summary>Maps a runtime-created delegate instance back to the window that owns it.</summary>
		private static readonly Dictionary<IntPtr, MacSystemWindow> DelegateOwners = new Dictionary<IntPtr, MacSystemWindow>();

		/// <summary>Maps a content view back to the window that owns it, for the cursor-rect callback.</summary>
		private static readonly Dictionary<IntPtr, MacSystemWindow> ViewOwners = new Dictionary<IntPtr, MacSystemWindow>();

		/// <summary>NSCursor class-method selector name to the retained shared cursor it vends.</summary>
		private static readonly Dictionary<string, IntPtr> ResolvedCursors = new Dictionary<string, IntPtr>();

		// --- Unattended smoke runs -------------------------------------------------------------------
		// Read once, from the environment, because the point is to drive an *unmodified* demo: no demo has
		// to know it is being smoke tested, and with the variables unset none of this does anything. Kept
		// byte for byte compatible with WinformsSystemWindow's version so one AGG_SMOKE_* invocation drives
		// either platform.
		private static readonly int SmokeFrameTarget = ParseSmokeFrames();
		private static readonly string SmokeScreenshotPath = Environment.GetEnvironmentVariable("AGG_SMOKE_SCREENSHOT");

		private static System.Threading.Timer smokeExitWatchdog;

		/// <summary>
		/// Set <c>AGG_LOG_GESTURE=1</c> to print every scroll and magnify event as it arrives - its type,
		/// phase, momentum phase, scrolling delta, whether the deltas are precise, and the magnification.
		/// No test can synthesise a trackpad gesture, so when a gesture misbehaves this log is the only way
		/// to see what AppKit actually sent rather than what we assume it sent.
		/// </summary>
		private static readonly bool LogGestureEvents = Environment.GetEnvironmentVariable("AGG_LOG_GESTURE") == "1";

		private static IntPtr nsApp;
		private static IntPtr distantPast;
		private static IntPtr defaultRunLoopMode;
		private static IntPtr delegateClass;
		private static IntPtr contentViewClass;
		private static bool appBootstrapped;

		/// <summary>
		/// Whether a window is currently running <see cref="RunEventLoop"/>. This, rather than a
		/// "first window" latch, is what decides whether a window being shown owns the loop: a latch has to
		/// be reset between runs (which is what <c>WinformsSystemWindow.ResetFirstWindowFlag</c> exists for)
		/// and gets the answer wrong for any window shown before the application's main one.
		/// </summary>
		private static volatile bool runLoopActive;

		private static bool processingOnIdle;

		private IntPtr window;
		private IntPtr view;
		private IntPtr metalLayer;

		/// <summary>The cursor agg last asked for, re-asserted from -resetCursorRects. Not owned.</summary>
		private IntPtr currentCursor;

		private IntPtr windowDelegate;
		private IntPtr idleTimer;

		private MacWebGpuLayer webGpuLayer;
		private SystemWindow aggSystemWindow;

		private double backingScale = 1;
		private uint pixelWidth = 1;
		private uint pixelHeight = 1;

		private string caption = string.Empty;
		private Vector2 minimumSize;

		private bool needsRedraw = true;
		private bool viewPortHasBeenSet;
		private bool isInsidePaint;
		private bool hasClosed;

		/// <summary>Set while an AppKit-initiated close is running, so the agg close does not re-enter it.</summary>
		private bool platformAlreadyClosing;

		/// <summary>Which buttons this view owns for the duration of a drag; see <see cref="OutOfViewMouseCapture"/>.</summary>
		private readonly OutOfViewMouseCapture mouseCapture = new OutOfViewMouseCapture();

		/// <summary>What <see cref="SetModifierKeys"/> was last told; see <see cref="ModifierKeys"/>.</summary>
		private Keys overrideModifierKeys = Keys.None;

		/// <summary>
		/// The modifier flags word carried by the last NSEventTypeFlagsChanged, so the next one can tell
		/// which flags moved rather than only what they now are. Zero - nothing held - is the correct
		/// starting value for a window that has not seen a flags change yet.
		/// </summary>
		private ulong lastModifierFlags;

		/// <summary>
		/// The modifier down-state keys this window last wrote into <see cref="Keyboard"/>, so losing focus
		/// can release exactly those and leave whatever anyone else put there alone. See
		/// <see cref="ReleaseAppliedModifierKeys"/> for why that narrowing matters.
		/// </summary>
		private IReadOnlySet<Keys> appliedModifierKeys = NoModifierKeys;

		private bool modifiersOverridden;

		/// <summary>
		/// True between the Began and the Ended of a pinch. macOS decides a two-finger movement is a scroll
		/// before it decides it is a magnification, so the start of a pinch can arrive as a scroll or two;
		/// once the pinch is running, any scroll that overlaps it is the same two fingers reported twice and
		/// zooming on both would double-count them.
		/// </summary>
		private bool magnifyGestureInFlight;

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

		/// <summary>The same signal as <see cref="screenshotComplete"/> for an awaiting requester. Only one of
		/// the two is ever non-null: <see cref="ThrowIfCapturePending"/> holds both entry points to one
		/// in-flight request.</summary>
		private TaskCompletionSource screenshotCompletion;

		public MacSystemWindow()
		{
			BootstrapApplication();

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
		/// Whether every agg window in the process shares this one native window, dialogs included.
		/// </summary>
		/// <remarks>
		/// What an application shell like MatterCAD runs on. <see cref="SingleWindowProvider"/> wraps
		/// everything shown after the first window in a <c>WindowWidget</c>, draws it inside the window
		/// already on screen, and then hands that wrapper to this same <see cref="IPlatformWindow"/>.
		/// Without this flag the second call reads as "the window you are already showing asked to be
		/// raised" and the dialog is never drawn. <c>WinformsSystemWindow</c> carries the identical flag
		/// for the identical reason; a provider that gives every window its own native window (agg's own
		/// <see cref="WebGpuMacWindowProvider"/>) leaves it alone.
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
		public MacWebGpuLayer WebGpuLayer => this.webGpuLayer;

		/// <summary>The provider that created this window, set by the provider itself.</summary>
		public ISystemWindowProvider WindowProvider { get; set; }

		/// <summary>The <c>CAMetalLayer*</c> the swapchain is built over. Diagnostics and tests.</summary>
		public IntPtr MetalLayerHandle => this.metalLayer;

		/// <summary>The <c>NSWindow*</c>. Diagnostics and tests.</summary>
		public IntPtr WindowHandle => this.window;

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

		public string Caption
		{
			get => this.caption;

			set
			{
				this.caption = value ?? string.Empty;
				if (this.window != IntPtr.Zero)
				{
					MainThreadDispatcher.Invoke(() => Send_v_r(this.window, Sel("setTitle:"), NSString(this.caption)));
				}
			}
		}

		/// <summary>
		/// The height of the native title bar, in agg pixels. Zero until the window exists.
		/// </summary>
		public int TitleBarHeight
		{
			get
			{
				if (this.window == IntPtr.Zero)
				{
					return 0;
				}

				// Read on the main thread: the automation runner asks for this from its test thread on every
				// screen-to-window coordinate conversion, and -[NSWindow frame] is AppKit like everything else.
				return MainThreadDispatcher.Invoke(() =>
				{
					CGRect frame = Send_R(this.window, Sel("frame"));
					CGRect content = Send_R(this.view, Sel("frame"));
					return (int)Math.Round((frame.Size.Height - content.Size.Height) * this.backingScale);
				});
			}
		}

		/// <summary>
		/// The window's top-left corner in desktop space. Desktop space here is device pixels with the
		/// origin at the top-left of the primary screen - Windows' convention, and the one agg's callers
		/// assume - so it is flipped and scaled out of AppKit's bottom-left, point-based screen space.
		/// </summary>
		public Point2D DesktopPosition
		{
			get
			{
				if (this.window == IntPtr.Zero)
				{
					return new Point2D(0, 0);
				}

				return MainThreadDispatcher.Invoke(() =>
				{
					CGRect frame = Send_R(this.window, Sel("frame"));
					double scale = DesktopScale();
					double topLeftY = PrimaryScreenHeightInPoints() - (frame.Origin.Y + frame.Size.Height);

					return new Point2D((int)Math.Round(frame.Origin.X * scale), (int)Math.Round(topLeftY * scale));
				});
			}

			set
			{
				if (this.window == IntPtr.Zero)
				{
					return;
				}

				MainThreadDispatcher.Invoke(() =>
				{
					double scale = DesktopScale();
					CGRect frame = Send_R(this.window, Sel("frame"));
					double left = value.x / scale;
					double top = value.y / scale;
					double bottom = PrimaryScreenHeightInPoints() - top - frame.Size.Height;

					Send_v_P(this.window, Sel("setFrameOrigin:"), new CGPoint(left, bottom));
				});
			}
		}

		public Vector2 MinimumSize
		{
			get => this.minimumSize;

			set
			{
				this.minimumSize = value;
				if (this.window != IntPtr.Zero)
				{
					double scale = this.backingScale;
					MainThreadDispatcher.Invoke(
						() => Send_v_S(this.window, Sel("setContentMinSize:"), new CGSize(value.X / scale, value.Y / scale)));
				}
			}
		}

		/// <summary>The modifier keys held right now, translated from AppKit's flags to agg's.</summary>
		/// <remarks>
		/// Reports whatever <see cref="SetModifierKeys"/> was last told, once it has been told anything.
		/// A simulated Ctrl-click has no real key held, so reading AppKit here would report None and every
		/// modifier-sensitive interaction in an automated run would behave as an unmodified one.
		/// </remarks>
		public Keys ModifierKeys
			=> this.modifiersOverridden
				? this.overrideModifierKeys
				: MainThreadDispatcher.Invoke(() => TranslateModifiers(Send_Q(Class("NSEvent"), Sel("modifierFlags"))));

		/// <summary>
		/// Declares which modifier keys a synthetic input event is holding, so <see cref="ModifierKeys"/>
		/// reports them instead of the (empty) real keyboard state.
		/// </summary>
		/// <remarks>
		/// Found by name and by reflection from <c>AggInputMethods.TrySetModifierKeys</c>, which is why it
		/// is internal and not on <see cref="IPlatformWindow"/>. <c>WinformsSystemWindow</c> has the same
		/// method for the same caller. Once called, the real keyboard is never read again - an automated
		/// run has no user at the keyboard, so there is nothing to fall back to.
		/// </remarks>
		internal void SetModifierKeys(Keys modifiers)
		{
			this.overrideModifierKeys = modifiers;
			this.modifiersOverridden = true;
		}

		public void BringToFront()
		{
			if (this.window != IntPtr.Zero)
			{
				MainThreadDispatcher.Invoke(() => Send_v_r(this.window, Sel("orderFront:"), IntPtr.Zero));
			}
		}

		public void Activate()
		{
			if (this.window != IntPtr.Zero)
			{
				MainThreadDispatcher.Invoke(() =>
				{
					Send_v_r(this.window, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
					Send_v_B(nsApp, Sel("activateIgnoringOtherApps:"), YES);
				});
			}
		}

		/// <summary>
		/// Schedules a repaint. There is no WM_PAINT here, so this is a flag the pumped loop reads rather
		/// than a message; the rectangle is ignored because the whole frame is redrawn either way.
		/// </summary>
		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			this.needsRedraw = true;
		}

		/// <summary>Closes the platform window, tearing the NSWindow down.</summary>
		public void Close()
		{
			MainThreadDispatcher.Invoke(this.CloseNativeWindow);
		}

		public void SetCursor(Cursors cursorToSet)
		{
			string selectorName = cursorToSet switch
			{
				Cursors.Hand => "pointingHandCursor",
				Cursors.IBeam => "IBeamCursor",
				Cursors.Cross => "crosshairCursor",
				Cursors.No => "operationNotAllowedCursor",
				Cursors.SizeNS => "resizeUpDownCursor",
				Cursors.SizeWE => "resizeLeftRightCursor",
				Cursors.HSplit => "resizeUpDownCursor",
				Cursors.VSplit => "resizeLeftRightCursor",
				Cursors.UpArrow => "resizeUpCursor",

				// The diagonal resize cursors macOS draws at its own window corners exist, but only as
				// private class methods on NSCursor - so they are probed for rather than assumed (see
				// ResolveCursor). Without them a window-widget corner grip, which is the one place agg
				// asks for them, would hover as a plain arrow.
				Cursors.SizeNWSE => "_windowResizeNorthWestSouthEastCursor",
				Cursors.SizeNESW => "_windowResizeNorthEastSouthWestCursor",

				// No move-in-any-direction cursor exists here; the open hand is what macOS itself shows
				// for "this can be dragged around", which is what SizeAll means to agg.
				Cursors.SizeAll => "openHandCursor",

				// The eight pan directions have no macOS equivalent, private or otherwise, so they fall
				// back to the arrow rather than being faked with something misleading.
				_ => "arrowCursor",
			};

			MainThreadDispatcher.Invoke(() =>
			{
				IntPtr cursor = ResolveCursor(selectorName);
				if (cursor == IntPtr.Zero || cursor == this.currentCursor)
				{
					// Nothing to do for a cursor that is already showing, and doing it anyway is not free:
					// rebuilding the cursor rect posts a mouseExited/mouseEntered pair. agg calls this from
					// every OnMouseEnter, so re-asserting the same arrow would keep the pointer looking to
					// AppKit like it was leaving and re-entering the view. See PointerReallyLeftContentView.
					return;
				}

				// Set it now so the change is immediate, and remember it so -resetCursorRects can keep
				// re-asserting it: [NSCursor set] alone lasts only until the pointer next crosses one of
				// the window frame's cursor rects, which puts the arrow back.
				this.currentCursor = cursor;
				Send_v(cursor, Sel("set"));

				if (this.window != IntPtr.Zero && this.view != IntPtr.Zero)
				{
					Send_v_r(this.window, Sel("invalidateCursorRectsForView:"), this.view);
				}
			});
		}

		/// <summary>
		/// The shared NSCursor for a class-method selector name, or the arrow when that selector does not
		/// exist on this macOS. Private selectors have to be probed with <c>respondsToSelector:</c> before
		/// being sent - Apple can withdraw one in any release, and an unrecognized selector is a crash, not
		/// a nil. Results are cached because the probe is per-selector and never changes within a process.
		/// </summary>
		private static IntPtr ResolveCursor(string selectorName)
		{
			lock (ResolvedCursors)
			{
				if (ResolvedCursors.TryGetValue(selectorName, out IntPtr cached))
				{
					return cached;
				}

				IntPtr cursorClass = Class("NSCursor");
				IntPtr selector = Sel(selectorName);

				IntPtr cursor = RespondsToSelector(cursorClass, selector)
					? Send_r(cursorClass, selector)
					: IntPtr.Zero;

				if (cursor == IntPtr.Zero)
				{
					cursor = Send_r(cursorClass, Sel("arrowCursor"));
				}

				if (cursor == IntPtr.Zero)
				{
					// NSCursor vends nil to a process with no window server connection, so this is not a
					// permanent answer and must not be cached - it comes right once the app is up.
					return IntPtr.Zero;
				}

				// NSCursor's class methods vend process-lifetime singletons, but some of them hand back an
				// autoreleased object and this pointer outlives the pump's autorelease pool.
				cursor = Retain(cursor);

				ResolvedCursors[selectorName] = cursor;
				return cursor;
			}
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
		/// Connects a <see cref="SystemWindow"/> to this platform window, creates the NSWindow and its
		/// wgpu device, shows it, and - unless a window is already running the loop - runs the event loop
		/// until that window closes. The blocking shape is deliberate: it is what <c>Application.Run</c>
		/// does on Windows, and every agg demo's <c>Main</c> depends on <c>ShowAsSystemWindow</c> not
		/// returning until the app is done.
		/// </summary>
		/// <remarks>
		/// The whole body runs on the main thread (see the class remarks), so a caller on a thread pool
		/// worker - which is what a test is - blocks here for exactly as long as it would have on Windows.
		/// </remarks>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			MainThreadDispatcher.Invoke(() => this.ShowSystemWindowOnMainThread(systemWindow));
		}

		private void ShowSystemWindowOnMainThread(SystemWindow systemWindow)
		{
			if (systemWindow.PlatformWindow == this)
			{
				// In single window mode the provider points a window at this one before showing it, so
				// "already mine" means "start drawing this instead", not "raise what is already up".
				if (SingleWindowMode && this.window != IntPtr.Zero)
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

			// Also seeds SystemWindow.DisplayScale, since aggSystemWindow is already attached. On a 1x
			// display that matches the default and says nothing; on a Retina one it queues a single
			// DisplayScaleChanged for the first idle tick. Suppressing that one raise would mean giving the
			// application a window whose scale it was never told about, so the startup rebuild is the
			// cheaper of the two - and it happens before anything is on screen.
			this.SyncSizeFromBacking();

			// Before the window is shown, so the bar is already up when the app becomes frontmost. An agg
			// application that describes no menu bar gets none, which is every application but MatterCAD.
			// Note this fires for whichever window is being shown, and the menu bar is per application, not
			// per window: a second window with a MenuBar of its own would replace the bar for good - closing
			// it restores nothing. MatterCAD sets the property on the root window only, which is the one
			// arrangement that needs no restore.
			if (systemWindow.MenuBar != null)
			{
				MacMenuBar.Install(systemWindow.MenuBar);
			}

			Send_v_r(this.window, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
			Send_v_B(nsApp, Sel("activateIgnoringOtherApps:"), YES);

			// Activation and window ordering are asynchronous: isKeyWindow and, more importantly for the
			// renderer, occlusionState are still stale immediately after the calls above. wgpu's Metal
			// backend refuses to vend a drawable for a window it believes is occluded, so painting before
			// this settles throws the whole first second of frames away. A few pumps is all it takes.
			for (int settle = 0; settle < 10; settle++)
			{
				PumpEvents();
				Thread.Sleep(10);
			}

			this.needsRedraw = true;

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
			// AppKit is already closing us (the user hit the red button); letting the agg close drive a
			// second close would re-enter windowWillClose:.
			if (this.platformAlreadyClosing)
			{
				return;
			}

			// In single window mode a dialog lives inside this window, so closing one is only a matter of
			// going back to drawing whatever the provider now has on top. Only the shell - the window the
			// provider is left holding - takes the native window down with it.
			if (SingleWindowMode
				&& this.window != IntPtr.Zero
				&& this.WindowProvider?.TopWindow != null
				&& this.WindowProvider.TopWindow != systemWindow)
			{
				MainThreadDispatcher.Invoke(() => this.SetActiveAggWindow(this.WindowProvider.TopWindow));
				return;
			}

			MainThreadDispatcher.Invoke(this.CloseNativeWindow);
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

				// Backing pixels, matching SyncSizeFromBacking: this window's agg coordinate space is the
				// drawable, and a swapped-in window that kept its own size would be drawn at the wrong one.
				// SetBoundsFromPlatform rather than LocalBounds so a minimum sized for another display cannot
				// lay the window out larger than the drawable it is about to be drawn into.
				systemWindow.SetBoundsFromPlatform(this.pixelWidth, this.pixelHeight);

				// Same reason: a window built while the shell was on a 1x display is about to be drawn on
				// whatever display the shell is on now, and nothing else will ever tell it so - SyncSizeFromBacking
				// only ever pushes the scale into whichever window was active at the time.
				systemWindow.SetDisplayScale(this.backingScale);
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
				// A thin blocking boundary over the async form, kept for callers whose whole chain is
				// synchronous (failure diagnostics). There is no Control.Invoke here, so the request is
				// marshalled the only way this host has: through the idle queue the 10ms NSTimer drains on
				// the UI thread - and the queued work is the awaitable capture, which waits on the
				// completion the capturing frame posts. Its continuation comes back to the idle pump via
				// MainLoopSynchronizationContext, so nothing here has to spin the nested pump.
				var done = new ManualResetEventSlim(false);

				UiThread.RunOnIdle(async () =>
				{
					try
					{
						// CaptureOnPumpThreadAsync, not the public entry point: this delegate is running on
						// the pump because the pump is what runs it, and re-asking UiThread.IsUiThread here
						// would let a wrong answer send the request round the queue again instead of
						// capturing. See CaptureOnPumpThreadAsync.
						await this.CaptureOnPumpThreadAsync(path);
					}
					catch (Exception ex)
					{
						// A capture that ran and failed faults the task. This lambda is async void to the
						// pump, so letting that escape would take the process down over a diagnostics
						// screenshot - the synchronous path this replaced never did that.
						Console.Error.WriteLine($"MacSystemWindow screenshot failed on the marshalled path: {ex.Message}");
					}
					finally
					{
						done.Set();
					}
				});

				// Disposed only when the queued work is known to be finished with it: after a give-up the
				// idle queue still holds a delegate that will Set() this, and Set on a disposed event throws
				// on the UI thread. An abandoned event is collectable; a crashed UI thread is not.
				if (done.Wait(ScreenshotMarshalTimeout))
				{
					done.Dispose();
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

			// The pump below runs arbitrary queued work, which is free to ask for a screenshot of its own.
			// One pending path and one completion signal is all the frame machinery has, so a second
			// request would steal this one's frame and leave this caller pumping for a capture that will
			// never happen.
			this.ThrowIfCapturePending(path);

			var completed = new ManualResetEventSlim(false);

			this.pendingScreenshotPath = path;
			this.screenshotComplete = completed;

			try
			{
				this.PaintFrame();

				// The native read-back completes inside the paint (wgpu's buffer map is polled to
				// completion there), so this is normally already set. It is only not set if the await in
				// CaptureThenPresent genuinely suspended, in which case its continuation is queued to the
				// idle pump by MainLoopSynchronizationContext - hence pumping rather than blocking the UI
				// thread, which would stop the very frames and continuations it is waiting on.
				//
				// DrainForNestedPump, not InvokeIdleActions: this loop can run underneath an idle action
				// already (a synchronous UI-thread caller reached from queued work), and the guarded drain
				// is a no-op while that is true - which would leave this spinning for a continuation only a
				// drain can run.
				for (int spin = 0; spin < ScreenshotPumpSpins && !completed.IsSet; spin++)
				{
					PumpEvents();
					UiThread.DrainForNestedPump();
				}
			}
			finally
			{
				this.pendingScreenshotPath = null;
				this.screenshotComplete = null;

				// Only dispose once the capturing frame is done with it. If the pump gave up while the
				// capture was still in flight, CaptureThenPresent still holds this same instance and will
				// Set() it - and that Set would throw ObjectDisposedException inside an async void, which
				// is a process kill. Leaving it to the GC costs nothing: this event is only ever polled
				// through IsSet, so it never allocates a wait handle to leak.
				if (completed.IsSet)
				{
					completed.Dispose();
				}
			}
		}

		/// <summary>
		/// Refuses a capture while another one is still in flight. The frame machinery holds exactly one
		/// pending path and one completion signal, so overlapping requests cannot both be served, and
		/// failing loudly beats one caller silently receiving the other's frame - or no frame at all.
		/// </summary>
		private void ThrowIfCapturePending(string path)
		{
			if (this.pendingScreenshotPath != null
				|| this.screenshotComplete != null
				|| this.screenshotCompletion != null)
			{
				throw new InvalidOperationException(
					$"A screenshot capture is already pending (to '{this.pendingScreenshotPath}'); only one capture can be in flight at a time. Requested '{path}'.");
			}
		}

		/// <summary>
		/// The awaitable form of <see cref="CaptureScreenshot"/>. Queues the same request onto the same
		/// frame machinery, but waits on the completion the capturing frame posts instead of pumping, so
		/// the caller's thread is free while the frame runs.
		/// </summary>
		/// <param name="path">Where to write the PNG.</param>
		public async Task CaptureScreenshotAsync(string path)
		{
			if (this.webGpuLayer == null || this.webGpuLayer.IsDisposed)
			{
				return;
			}

			if (!UiThread.IsUiThread)
			{
				// Same marshalling as the synchronous path - the idle queue is this host's only channel to
				// the UI thread - except the result comes back as a task rather than a blocking wait.
				var marshalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

				UiThread.RunOnIdle(async () =>
				{
					try
					{
						await this.CaptureOnPumpThreadAsync(path);
						marshalled.TrySetResult();
					}
					catch (Exception ex)
					{
						marshalled.TrySetException(ex);
					}
				});

				try
				{
					// Bounded like the synchronous twin's done.Wait: an idle queue that never runs (a window
					// with no frames) must not hang the caller forever. On the looser marshal bound, so the
					// inner capture's own timeout governs and gets to clean up before this one gives up.
					await marshalled.Task.WaitAsync(ScreenshotMarshalTimeout);
				}
				catch (TimeoutException)
				{
					// The same quiet give-up the synchronous path makes. A capture that actually ran and
					// failed faults instead of landing here.
				}

				return;
			}

			await this.CaptureOnPumpThreadAsync(path);
		}

		/// <summary>
		/// The capture itself, from the thread that pumps the idle queue: queue the request, force a frame,
		/// and wait for the frame that consumes it.
		/// </summary>
		/// <remarks>
		/// Split out from <see cref="CaptureScreenshotAsync"/> so that the work queued by the marshalling
		/// branches above - which run on the pump BECAUSE the pump is what runs them - can capture directly
		/// instead of asking <see cref="UiThread.IsUiThread"/> a second time. That second question used to be
		/// answerable "no" while running on the pump (the id can be latched onto another thread; see
		/// <see cref="UiThread.MarkCurrentThreadAsUiThread"/>), and the queued work then marshalled the
		/// request onto the queue it was already being run from - forever, until the caller's timeout expired
		/// with no file written and no error raised. Nothing on this path asks that question, so no answer to
		/// it can turn a capture into a loop.
		/// </remarks>
		/// <param name="path">Where to write the PNG.</param>
		private async Task CaptureOnPumpThreadAsync(string path)
		{
			if (this.webGpuLayer == null || this.webGpuLayer.IsDisposed)
			{
				return;
			}

			if (this.isInsidePaint)
			{
				// Same as the synchronous path: this frame is about to consume the request, and forcing
				// another paint from here would re-enter the frame.
				this.pendingScreenshotPath = path;
				return;
			}

			this.ThrowIfCapturePending(path);

			var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			this.pendingScreenshotPath = path;
			this.screenshotCompletion = completion;

			try
			{
				// The read-back usually finishes inside this synchronous frame, leaving nothing to await.
				this.PaintFrame();

				// A capture that ran and failed faults here, on purpose: the contract is that the file
				// exists once this completes, so a swallowed failure would be a lie.
				await completion.Task.WaitAsync(ScreenshotAsyncTimeout);
			}
			catch (TimeoutException)
			{
				// Matches the synchronous path's bounded pump, which also gives up quietly rather than
				// hanging a caller whose window never painted.
			}
			finally
			{
				// Only clear what still belongs to this request. The continuation that gets here resumes on
				// the main loop (MainLoopSynchronizationContext), but on a LATER pump - so this cleanup can
				// still run well after the request was given up on, by which point the fields may already
				// have been claimed by the next request.
				if (ReferenceEquals(this.screenshotCompletion, completion))
				{
					this.pendingScreenshotPath = null;
					this.screenshotCompletion = null;
				}
			}
		}

		// -----------------------------------------------------------------------------------------
		// Application bootstrap
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Brings NSApplication up from a plain console process (no .app bundle, no Info.plist). Idempotent.
		/// </summary>
		/// <remarks>
		/// On the main thread, always: <c>+[NSApplication sharedApplication]</c> installs the application
		/// object and its event machinery, and AppKit is explicit that this must happen on the main thread.
		/// The window constructor calls this, and a test constructs its window from a worker.
		/// </remarks>
		private static void BootstrapApplication()
		{
			MainThreadDispatcher.Invoke(BootstrapApplicationOnMainThread);
		}

		private static void BootstrapApplicationOnMainThread()
		{
			lock (StaticInitLock)
			{
				if (appBootstrapped)
				{
					return;
				}

				EnsureFrameworksLoaded();

				nsApp = Send_r(Class("NSApplication"), Sel("sharedApplication"));
				if (nsApp == IntPtr.Zero)
				{
					throw new InvalidOperationException("+[NSApplication sharedApplication] returned nil.");
				}

				// Regular, not Accessory: a Prohibited/Accessory app cannot become frontmost, and a
				// non-bundled process defaults to Prohibited.
				Send_B_q(nsApp, Sel("setActivationPolicy:"), NSApplicationActivationPolicyRegular);

				// finishLaunching does the work [NSApp run] would normally do on entry (posts
				// NSApplicationWillFinishLaunching, unstalls the launch). Skip it and a pumped app can end
				// up unable to become frontmost.
				Send_v(nsApp, Sel("finishLaunching"));

				distantPast = Retain(Send_r(Class("NSDate"), Sel("distantPast")));
				defaultRunLoopMode = Retain(NSString("kCFRunLoopDefaultMode"));

				RegisterWindowDelegateClass();
				RegisterContentViewClass();

				appBootstrapped = true;
			}
		}

		/// <summary>
		/// Defines <c>AggMacWindowDelegate</c> at runtime. AppKit only reports a window close through the
		/// <c>NSWindowDelegate</c> protocol, and a protocol cannot be implemented from managed code without
		/// a real Objective-C class - so one is built with <c>objc_allocateClassPair</c> and given
		/// <c>[UnmanagedCallersOnly]</c> statics as its method implementations.
		/// <para>
		/// The instance carries no state; it is mapped back to its owning window through
		/// <see cref="DelegateOwners"/>. An ivar would work too, but a dictionary keyed on the instance
		/// pointer needs no <c>class_addIvar</c> layout arithmetic and there are never more than a handful
		/// of windows.
		/// </para>
		/// </summary>
		private static unsafe void RegisterWindowDelegateClass()
		{
			IntPtr cls = objc_allocateClassPair(Class("NSObject"), "AggMacWindowDelegate", 0);
			if (cls == IntPtr.Zero)
			{
				throw new InvalidOperationException(
					"objc_allocateClassPair(\"AggMacWindowDelegate\") returned nil - the name is already registered.");
			}

			// Type encodings: 'c' is BOOL (a signed char), 'v' is void, '@' is id, ':' is SEL.
			AddMethod(cls, "windowShouldClose:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>)&OnWindowShouldClose, "c@:@");
			AddMethod(cls, "windowWillClose:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnWindowWillClose, "v@:@");
			AddMethod(cls, "windowDidResize:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnWindowDidResize, "v@:@");
			AddMethod(cls, "windowDidChangeBackingProperties:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnWindowDidResize, "v@:@");
			AddMethod(cls, "windowDidChangeScreen:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnWindowDidResize, "v@:@");
			AddMethod(cls, "windowDidResignKey:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnWindowDidResignKey, "v@:@");
			AddMethod(cls, "windowDidBecomeKey:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnWindowDidBecomeKey, "v@:@");
			AddMethod(cls, "aggIdleTimer:", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnIdleTimer, "v@:@");

			objc_registerClassPair(cls);
			delegateClass = cls;
		}

		/// <summary>
		/// Defines <c>AggMacContentView</c>, an NSView subclass whose only addition is a cursor rect over
		/// its whole bounds.
		/// <para>
		/// A plain <c>[NSCursor set]</c> is not durable: the window frame installs its own cursor rects, so
		/// the moment the pointer crosses one of them - the title bar, a resize edge - AppKit puts the
		/// arrow back and whatever agg had chosen is lost. Cursor rects are the mechanism AppKit actually
		/// consults, so owning one over the content view is what makes agg's choice stick while hovering.
		/// </para>
		/// </summary>
		private static unsafe void RegisterContentViewClass()
		{
			IntPtr cls = objc_allocateClassPair(Class("NSView"), "AggMacContentView", 0);
			if (cls == IntPtr.Zero)
			{
				throw new InvalidOperationException(
					"objc_allocateClassPair(\"AggMacContentView\") returned nil - the name is already registered.");
			}

			AddMethod(cls, "resetCursorRects", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, void>)&OnResetCursorRects, "v@:");

			objc_registerClassPair(cls);
			contentViewClass = cls;
		}

		[UnmanagedCallersOnly]
		private static void OnResetCursorRects(IntPtr self, IntPtr cmd)
		{
			// An exception must never cross back into Objective-C: there is no managed frame above this to
			// catch it and the runtime tears the process down.
			try
			{
				MacSystemWindow owner;
				lock (StaticInitLock)
				{
					ViewOwners.TryGetValue(self, out owner);
				}

				IntPtr cursor = owner?.currentCursor ?? IntPtr.Zero;
				if (cursor != IntPtr.Zero)
				{
					Send_v_R_r(self, Sel("addCursorRect:cursor:"), Send_R(self, Sel("bounds")), cursor);
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacSystemWindow.OnResetCursorRects threw {ex}");
			}
		}

		private static void AddMethod(IntPtr cls, string selectorName, IntPtr implementation, string typeEncoding)
		{
			if (class_addMethod(cls, Sel(selectorName), implementation, typeEncoding) == NO)
			{
				string className = Marshal.PtrToStringUTF8(class_getName(cls)) ?? "(?)";
				throw new InvalidOperationException($"class_addMethod failed for -[{className} {selectorName}].");
			}
		}

		private static MacSystemWindow OwnerOf(IntPtr delegateInstance)
		{
			lock (StaticInitLock)
			{
				return DelegateOwners.TryGetValue(delegateInstance, out var owner) ? owner : null;
			}
		}

		[UnmanagedCallersOnly]
		private static byte OnWindowShouldClose(IntPtr self, IntPtr cmd, IntPtr sender)
		{
			// An exception must never cross back into Objective-C: there is no managed frame above this to
			// catch it and the runtime tears the process down.
			try
			{
				return OwnerOf(self)?.HandleShouldClose() ?? YES;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacSystemWindow windowShouldClose: threw {ex}");
				return YES;
			}
		}

		[UnmanagedCallersOnly]
		private static void OnWindowWillClose(IntPtr self, IntPtr cmd, IntPtr notification)
		{
			try
			{
				OwnerOf(self)?.HandleWillClose();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacSystemWindow windowWillClose: threw {ex}");
			}
		}

		[UnmanagedCallersOnly]
		private static void OnWindowDidResize(IntPtr self, IntPtr cmd, IntPtr notification)
		{
			try
			{
				OwnerOf(self)?.HandleDidResize();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacSystemWindow windowDidResize: threw {ex}");
			}
		}

		[UnmanagedCallersOnly]
		private static void OnWindowDidResignKey(IntPtr self, IntPtr cmd, IntPtr notification)
		{
			try
			{
				OwnerOf(self)?.HandleDidResignKey();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacSystemWindow windowDidResignKey: threw {ex}");
			}
		}

		[UnmanagedCallersOnly]
		private static void OnWindowDidBecomeKey(IntPtr self, IntPtr cmd, IntPtr notification)
		{
			try
			{
				OwnerOf(self)?.HandleDidBecomeKey();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"MacSystemWindow windowDidBecomeKey: threw {ex}");
			}
		}

		[UnmanagedCallersOnly]
		private static void OnIdleTimer(IntPtr self, IntPtr cmd, IntPtr timer)
		{
			try
			{
				InvokeIdleActions();
			}
			catch (Exception ex)
			{
				UiThread.ReportUnhandledException(ex);
				Console.Error.WriteLine($"MacSystemWindow idle tick threw {ex}");
			}
		}

		/// <summary>
		/// Drains the RunOnIdle queue. Guarded because an idle action can run a nested loop (a modal
		/// dialog) and re-enter this. A nested loop that must instead let awaited continuations run -
		/// <see cref="CaptureScreenshot"/>'s spin - calls <see cref="UiThread.DrainForNestedPump"/>, which
		/// deliberately skips this guard.
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
				MainLoopSynchronizationContext.InstallOnPumpThread();
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

		private void CreateNativeWindow(SystemWindow systemWindow)
		{
			// The screen's scale, because there is no window yet to ask. Once the window is on screen its
			// own backingScaleFactor is authoritative and SyncSizeFromBacking picks that up.
			this.backingScale = PrimaryScreenScale();

			// agg asked for a size in device pixels; AppKit wants points.
			double contentWidth = Math.Max(1, systemWindow.Width / this.backingScale);
			double contentHeight = Math.Max(1, systemWindow.Height / this.backingScale);

			ulong styleMask = NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable;
			if (systemWindow.Resizable)
			{
				styleMask |= NSWindowStyleMaskResizable;
			}

			var contentRect = new CGRect(0, 0, contentWidth, contentHeight);

			this.window = Send_r_R_Q_Q_B(
				Alloc(Class("NSWindow")),
				Sel("initWithContentRect:styleMask:backing:defer:"),
				contentRect,
				styleMask,
				NSBackingStoreBuffered,
				NO);

			if (this.window == IntPtr.Zero)
			{
				throw new InvalidOperationException("-[NSWindow initWithContentRect:styleMask:backing:defer:] returned nil.");
			}

			// Otherwise AppKit releases the NSWindow the moment it closes and every pointer held here
			// becomes a use-after-free during teardown.
			Send_v_B(this.window, Sel("setReleasedWhenClosed:"), NO);
			Send_v_r(this.window, Sel("setTitle:"), NSString(this.caption.Length > 0 ? this.caption : systemWindow.Title ?? string.Empty));

			// Without this the window never delivers NSEventTypeMouseMoved, so agg sees no hover at all.
			Send_v_B(this.window, Sel("setAcceptsMouseMovedEvents:"), YES);

			if (this.minimumSize != Vector2.Zero)
			{
				Send_v_S(this.window, Sel("setContentMinSize:"), new CGSize(this.minimumSize.X / this.backingScale, this.minimumSize.Y / this.backingScale));
			}

			// AggMacContentView rather than a bare NSView, purely so the cursor agg picks survives the
			// window frame's own cursor rects - see RegisterContentViewClass.
			this.view = Send_r_R(Alloc(contentViewClass), Sel("initWithFrame:"), contentRect);
			lock (StaticInitLock)
			{
				ViewOwners[this.view] = this;
			}

			this.metalLayer = Retain(Send_r(Class("CAMetalLayer"), Sel("layer")));
			if (this.metalLayer == IntPtr.Zero)
			{
				throw new InvalidOperationException("+[CAMetalLayer layer] returned nil.");
			}

			IntPtr mtlDevice = MTLCreateSystemDefaultDevice();
			if (mtlDevice == IntPtr.Zero)
			{
				throw new InvalidOperationException("MTLCreateSystemDefaultDevice returned nil - no Metal device on this machine.");
			}

			Send_v_r(this.metalLayer, Sel("setDevice:"), mtlDevice);
			Send_v_R(this.metalLayer, Sel("setFrame:"), contentRect);

			// ORDER MATTERS: setLayer: then setWantsLayer:YES makes this a layer-HOSTING view (we own the
			// layer). The reverse order makes it layer-BACKED - AppKit allocates its own CALayer and this
			// CAMetalLayer is silently discarded, which shows up as a window that never draws anything.
			Send_v_r(this.view, Sel("setLayer:"), this.metalLayer);
			Send_v_B(this.view, Sel("setWantsLayer:"), YES);

			if (Send_r(this.view, Sel("layer")) != this.metalLayer)
			{
				throw new InvalidOperationException(
					"[view layer] is not the CAMetalLayer that was set - the view became layer-backed rather than layer-hosting.");
			}

			Send_v_r(this.window, Sel("setContentView:"), this.view);

			this.windowDelegate = New("AggMacWindowDelegate");
			lock (StaticInitLock)
			{
				DelegateOwners[this.windowDelegate] = this;
			}

			// NSWindow does not retain its delegate; this instance is kept alive by our own +1 from init.
			Send_v_r(this.window, Sel("setDelegate:"), this.windowDelegate);

			this.MeasureBacking();

			this.webGpuLayer = new MacWebGpuLayer(this.metalLayer, this.pixelWidth, this.pixelHeight);

			this.StartIdleTimer();

			if (systemWindow.Maximized)
			{
				Send_v_r(this.window, Sel("zoom:"), IntPtr.Zero);
			}
			else if (systemWindow.InitialDesktopPosition == new Point2D(-1, -1))
			{
				Send_v(this.window, Sel("center"));
			}
			else
			{
				this.DesktopPosition = systemWindow.InitialDesktopPosition;
			}
		}

		/// <summary>
		/// Starts the 10ms RunOnIdle pump. A real <c>NSTimer</c> rather than a tick in the event loop
		/// because AppKit runs nested tracking loops for window drags, live resizes and menus, and this
		/// class's own loop is frozen for their duration - a timer in the common run loop modes keeps
		/// firing throughout. Without this pump nothing queued by <c>RunOnIdle</c> ever runs, which shows
		/// up as a window that comes up completely blank (widget layout is queued work).
		/// </summary>
		private void StartIdleTimer()
		{
			this.idleTimer = Retain(Send_r_d_r_r_r_B(
				Class("NSTimer"),
				Sel("scheduledTimerWithTimeInterval:target:selector:userInfo:repeats:"),
				0.01,
				this.windowDelegate,
				Sel("aggIdleTimer:"),
				IntPtr.Zero,
				YES));

			// scheduledTimer... adds the timer in the default mode only; adding it again in the common
			// modes is what makes it survive a modal tracking loop.
			IntPtr runLoop = Send_r(Class("NSRunLoop"), Sel("currentRunLoop"));
			Send_v_r_r(runLoop, Sel("addTimer:forMode:"), this.idleTimer, NSString("kCFRunLoopCommonModes"));
		}

		/// <summary>
		/// Reads the view's size in real pixels out of AppKit. <c>convertRectToBacking:</c> is the only
		/// correct source: it is the view's bounds multiplied by whatever the scale of the screen the
		/// window is currently on happens to be.
		/// </summary>
		private void MeasureBacking()
		{
			this.backingScale = Send_d(this.window, Sel("backingScaleFactor"));
			if (this.backingScale <= 0)
			{
				this.backingScale = 1;
			}

			CGRect bounds = Send_R(this.view, Sel("bounds"));
			CGRect backing = Send_R_R(this.view, Sel("convertRectToBacking:"), bounds);

			this.pixelWidth = (uint)Math.Max(1, Math.Round(backing.Size.Width));
			this.pixelHeight = (uint)Math.Max(1, Math.Round(backing.Size.Height));
		}

		/// <summary>
		/// How much room the screen this window is on has for a window, in device pixels.
		/// </summary>
		/// <remarks>
		/// <c>visibleFrame</c> rather than <c>frame</c>: it already has the menu bar and the Dock taken out
		/// of it, which is the honest answer to "how big can this window be". It is in points, so it is
		/// multiplied by <see cref="backingScale"/> to reach agg's device pixels - the window's own backing
		/// scale is the scale of the screen <c>-[NSWindow screen]</c> just returned, since AppKit derives
		/// both from the display the window mostly covers.
		/// <para>
		/// Zero when the window has no screen at all (dragged off the desktop, minimised), which
		/// <see cref="SystemWindow.SetDisplayUsableSize"/> reads as "nothing measured" and discards.
		/// </para>
		/// </remarks>
		private Vector2 MeasureUsableScreenSize()
		{
			IntPtr screen = this.window == IntPtr.Zero ? IntPtr.Zero : Send_r(this.window, Sel("screen"));
			if (screen == IntPtr.Zero)
			{
				return Vector2.Zero;
			}

			CGRect visible = Send_R(screen, Sel("visibleFrame"));

			return new Vector2(visible.Size.Width * this.backingScale, visible.Size.Height * this.backingScale);
		}

		/// <summary>
		/// Handles <c>windowDidResize:</c> - and, through the same registration,
		/// <c>windowDidChangeBackingProperties:</c> and <c>windowDidChangeScreen:</c>. Re-sizes everything
		/// that follows the backing store, then, <em>while the user is dragging an edge</em>, paints the
		/// frame right here rather than leaving it to the pump.
		/// <para>
		/// That synchronous paint is the whole point of this method. A live resize runs inside one of
		/// AppKit's nested tracking loops, and for its duration <see cref="RunEventLoop"/> - the only caller
		/// that paints on its own schedule - is frozen (see the class remarks). So <see cref="SyncSizeFromBacking"/>
		/// would resize the swapchain and set <see cref="needsRedraw"/>, and then nobody would draw until the
		/// mouse came up; the CAMetalLayer meanwhile stretches its last drawable, which is exactly the smeared
		/// content the user sees. Painting from the notification is the Mac equivalent of servicing WM_PAINT
		/// from inside Win32's modal resize loop, which is what makes the Windows host live-update.
		/// </para>
		/// <para>
		/// Only during a live resize, though: outside one the pump is running and will pick up
		/// <see cref="needsRedraw"/> on its next pass, and painting eagerly there would draw during
		/// <see cref="ShowSystemWindowOnMainThread"/>'s show/settle sequence, which sizes the window before
		/// the first pump.
		/// </para>
		/// </summary>
		private void HandleDidResize()
		{
			this.SyncSizeFromBacking();

			// -[NSView inLiveResize] is only YES inside AppKit's resize tracking loop, so this is also the
			// test for "the pump cannot get to it".
			bool inLiveResize = this.view != IntPtr.Zero && Send_B(this.view, Sel("inLiveResize")) != NO;

			if (ShouldPaintSynchronouslyForResize(
				inLiveResize,
				this.isInsidePaint,
				this.hasClosed,
				this.webGpuLayer?.IsWebGpuInitialized ?? false))
			{
				// OnWindowDidResize's catch exists to keep an exception from unwinding through ObjC, but it
				// only logs. Without this, a paint that throws during a live resize - which outside a resize
				// would have propagated out of the run loop - would vanish. Report it the way the idle tick does.
				try
				{
					this.PaintFrame();
				}
				catch (Exception ex)
				{
					UiThread.ReportUnhandledException(ex);
					Console.Error.WriteLine($"MacSystemWindow live-resize paint threw {ex}");
				}
			}
		}

		/// <summary>
		/// Decides whether a resize notification has to paint the frame itself. Factored out of
		/// <see cref="HandleDidResize"/> because a live resize cannot be synthesised in a test - AppKit only
		/// raises <c>inLiveResize</c> for a real drag - but this decision can be.
		/// </summary>
		/// <param name="inLiveResize">The view's <c>inLiveResize</c>: the pump is frozen exactly when this is true.</param>
		/// <param name="isInsidePaint">True when a paint is already on the stack; painting again would re-enter the frame.</param>
		/// <param name="hasClosed">True once the window is gone, which resize notifications can still outlive.</param>
		/// <param name="webGpuInitialized">False before there is a swapchain to draw into - the first resizes land there.</param>
		internal static bool ShouldPaintSynchronouslyForResize(
			bool inLiveResize,
			bool isInsidePaint,
			bool hasClosed,
			bool webGpuInitialized)
		{
			return inLiveResize && webGpuInitialized && !isInsidePaint && !hasClosed;
		}

		/// <summary>
		/// Re-reads the backing size and pushes it everywhere it has to go: the layer (whose
		/// <c>contentsScale</c> and <c>drawableSize</c> do <em>not</em> follow the window's scale on their
		/// own), the swapchain, and the agg window's bounds.
		/// </summary>
		private void SyncSizeFromBacking()
		{
			if (this.window == IntPtr.Zero || this.hasClosed)
			{
				return;
			}

			uint previousWidth = this.pixelWidth;
			uint previousHeight = this.pixelHeight;

			this.MeasureBacking();

			CGRect bounds = Send_R(this.view, Sel("bounds"));
			Send_v_R(this.metalLayer, Sel("setFrame:"), bounds);
			Send_v_d(this.metalLayer, Sel("setContentsScale:"), this.backingScale);
			Send_v_S(this.metalLayer, Sel("setDrawableSize:"), new CGSize(this.pixelWidth, this.pixelHeight));

			if (this.webGpuLayer != null
				&& this.webGpuLayer.IsWebGpuInitialized
				&& (previousWidth != this.pixelWidth || previousHeight != this.pixelHeight))
			{
				this.webGpuLayer.Resize(this.pixelWidth, this.pixelHeight);
			}

			this.viewPortHasBeenSet = false;

			if (this.aggSystemWindow != null)
			{
				// The drawable is this big whatever the application's minimum says. Assigning LocalBounds would
				// let a minimum computed for the display we just left inflate the layout past the drawable, and
				// agg being y-up that clips off the top - the toolbars vanish under the title bar.
				this.aggSystemWindow.SetBoundsFromPlatform(this.pixelWidth, this.pixelHeight);

				// windowDidChangeScreen: and windowDidChangeBackingProperties: both land here, so this is
				// where a drag from a Retina display to a standard one becomes visible to the application.
				// SetDisplayScale only stores the value here - it raises its event from the idle queue -
				// which is what makes this safe to call from inside AppKit's drag tracking loop, where this
				// window's own pump is frozen and a subscriber that rebuilt the UI would stall the drag.
				this.aggSystemWindow.SetDisplayScale(this.backingScale);

				// The other half of "which display am I on": how much room it has. A second display is often
				// a different size rather than a different scale, and an application that sized itself
				// against the primary monitor would hold a minimum the display it is on cannot satisfy.
				this.aggSystemWindow.SetDisplayUsableSize(this.MeasureUsableScreenSize());

				this.aggSystemWindow.Invalidate();
			}

			this.needsRedraw = true;
		}

		// -----------------------------------------------------------------------------------------
		// The event loop
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Drives AppKit ourselves, the way a toolkit with its own frame scheduler must:
		/// <c>distantPast</c> makes <c>nextEventMatchingMask:</c> return immediately when the queue is
		/// empty, so this never blocks inside AppKit and can paint between batches of events.
		/// </summary>
		private static void RunEventLoop()
		{
			runLoopActive = true;

			try
			{
				while (runLoopActive)
				{
					PumpEvents();

					// This loop owns the main thread for as long as a window is up, so anything another
					// thread asked the main thread to do has to come through here or it never runs at all.
					MainThreadDispatcher.DrainPending();

					// The NSTimer normally drives this, but the timer only fires while the run loop is being
					// serviced; calling it here as well means a frame is never held up waiting for a tick.
					InvokeIdleActions();

					bool paintedSomething = false;

					MacSystemWindow[] windows;
					lock (StaticInitLock)
					{
						windows = LiveWindows.ToArray();
					}

					foreach (var macWindow in windows)
					{
						if (macWindow.needsRedraw && !macWindow.hasClosed)
						{
							// A throw from a widget's draw costs this frame and nothing more. Letting it
							// unwind cost the loop: the window stayed on screen with nothing pumping it, the
							// main thread never came back to run marshalled work, and every later caller -
							// in a test host, every later test - blocked forever on a window that could not
							// even be closed. One bad frame is not allowed to end the application's UI.
							//
							// Reported, not swallowed: this is the channel the automation harness listens on
							// (see UiThread.ReportUnhandledException), so the test whose draw threw still
							// fails, and loudly - it just fails alone. PaintFrame has already cleared
							// needsRedraw, so a repeatedly throwing draw does not spin the loop; it repeats
							// only as often as something asks for a repaint.
							try
							{
								macWindow.PaintFrame();
							}
							catch (Exception paintException)
							{
								Console.Error.WriteLine($"MacSystemWindow paint threw, frame abandoned: {paintException}");
								UiThread.ReportUnhandledException(paintException);
							}

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
						// Nothing to draw and nothing queued. Without this the loop is a 100% CPU spin; 4ms
						// is short enough that input latency stays under a frame - and waiting on the
						// dispatcher rather than sleeping means a marshalled call is picked up immediately
						// instead of after the rest of the interval.
						MainThreadDispatcher.WaitForWork(4);
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

		/// <summary>Drains the AppKit event queue to nil, dispatching each event to agg and to AppKit.</summary>
		private static void PumpEvents()
		{
			// Without a pool per pass, every autoreleased AppKit temporary leaks for the life of the process.
			IntPtr pool = New("NSAutoreleasePool");

			try
			{
				IntPtr nsEvent;
				while ((nsEvent = Send_r_Q_r_r_B(
					nsApp,
					Sel("nextEventMatchingMask:untilDate:inMode:dequeue:"),
					NSEventMaskAny,
					distantPast,
					defaultRunLoopMode,
					YES)) != IntPtr.Zero)
				{
					bool consumed = DispatchEvent(nsEvent);

					if (!consumed)
					{
						Send_v_r(nsApp, Sel("sendEvent:"), nsEvent);
					}
				}

				Send_v(nsApp, Sel("updateWindows"));
			}
			finally
			{
				Send_v(pool, Sel("drain"));
			}
		}

		/// <summary>
		/// Hands an event to the agg window it belongs to.
		/// </summary>
		/// <returns>
		/// True when AppKit must <em>not</em> also see the event. Every key event is swallowed, Command
		/// chords included: the content view is a plain NSView with no key handling, so letting a keystroke
		/// walk the responder chain ends at NSBeep - which is what used to make every Cmd-shortcut the
		/// application itself handled beep on top of doing its work.
		/// <para/>
		/// Keys are therefore dispatched managed first and only managed. A menu bar's shortcuts are not lost
		/// to that: a Command chord the managed window left unhandled is offered to the installed main menu
		/// by <see cref="MacMenuBar.PerformKeyEquivalent"/> from <see cref="HandleKeyDown"/>, which is a
		/// deliberate call and not the responder chain. Nothing is handed back to AppKit either way, so
		/// AppKit itself can never act on a chord a second time - but see <see cref="HandleKeyDown"/> for
		/// the invariant the menu bar's own shortcuts have to keep to for the same to be true of them.
		/// <para/>
		/// Every mouse event is passed on because title-bar dragging, live resize and the close button are
		/// all AppKit's to handle.
		/// </returns>
		private static bool DispatchEvent(IntPtr nsEvent)
		{
			long type = Send_q(nsEvent, Sel("type"));
			IntPtr eventWindow = Send_r(nsEvent, Sel("window"));

			MacSystemWindow target = null;
			lock (StaticInitLock)
			{
				foreach (var macWindow in LiveWindows)
				{
					if (macWindow.window == eventWindow)
					{
						target = macWindow;
						break;
					}
				}
			}

			if (target == null || target.hasClosed || target.aggSystemWindow == null)
			{
				return false;
			}

			// Parallel automation tests turn this off so a real mouse or keyboard cannot perturb a run.
			if (!IPlatformWindow.EnablePlatformWindowInput)
			{
				return false;
			}

			try
			{
				return target.HandleEvent(nsEvent, type);
			}
			catch (Exception ex)
			{
				UiThread.ReportUnhandledException(ex);
				Console.Error.WriteLine($"MacSystemWindow input handler threw {ex}");
				return false;
			}
		}

		private bool HandleEvent(IntPtr nsEvent, long type)
		{
			switch (type)
			{
				case NSEventTypeLeftMouseDown:
				case NSEventTypeRightMouseDown:
				case NSEventTypeOtherMouseDown:
					if (this.TryMakeMouseArgs(nsEvent, type, out var downArgs))
					{
						this.aggSystemWindow.OnMouseDown(downArgs);
					}

					return false;

				case NSEventTypeLeftMouseUp:
				case NSEventTypeRightMouseUp:
				case NSEventTypeOtherMouseUp:
					if (this.TryMakeMouseArgs(nsEvent, type, out var upArgs))
					{
						this.aggSystemWindow.OnMouseUp(upArgs);
					}

					return false;

				case NSEventTypeMouseMoved:
				case NSEventTypeLeftMouseDragged:
				case NSEventTypeRightMouseDragged:
				case NSEventTypeOtherMouseDragged:
					if (this.TryMakeMouseArgs(nsEvent, type, out var moveArgs))
					{
						this.aggSystemWindow.OnMouseMove(moveArgs);
					}

					return false;

				case NSEventTypeMouseExited:
					if (this.PointerReallyLeftContentView(nsEvent))
					{
						// Same sentinel the Windows sink uses for "the pointer is nowhere near me".
						this.aggSystemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
					}

					return false;

				case NSEventTypeScrollWheel:
					this.LogGestureEvent(nsEvent, type);
					this.TrackScrollGesturePhase(nsEvent);

					if (!this.magnifyGestureInFlight
						&& this.TryMakeMouseArgs(nsEvent, type, out var wheelArgs))
					{
						this.aggSystemWindow.OnMouseWheel(wheelArgs);
					}

					return false;

				case NSEventTypeMagnify:
					this.LogGestureEvent(nsEvent, type);

					// A pinch is its own event type, not a modified scroll, so without this case it reaches
					// nothing at all - which is what made pinch to zoom do nothing on a trackpad. It goes out
					// through the wheel path because a wheel is what every agg consumer already reads as zoom,
					// and fingers moving apart (a positive magnification) means zoom in, which is the same
					// direction a wheel pushed forward means.
					this.TrackMagnifyGesturePhase(nsEvent);

					if (this.TryMakeMouseArgs(nsEvent, type, out var magnifyArgs))
					{
						this.aggSystemWindow.OnMouseWheel(magnifyArgs);
					}

					return false;

				case NSEventTypeKeyDown:
					return this.HandleKeyDown(nsEvent);

				case NSEventTypeKeyUp:
					return this.HandleKeyUp(nsEvent);

				case NSEventTypeFlagsChanged:
					return this.HandleFlagsChanged(nsEvent);

				default:
					return false;
			}
		}

		/// <summary>
		/// Turns a modifier-only press or release into agg's <see cref="Keyboard"/> down state.
		/// </summary>
		/// <remarks>
		/// macOS delivers a bare modifier <em>only</em> as flagsChanged - there is no keyDown for holding
		/// Command or Control on its own - so without this agg never learns a modifier is held and every
		/// chorded drag gesture (the 3D view's rotate, pan and zoom) is dead on this platform.
		/// <para/>
		/// The down/up bookkeeping is derived from the whole flags word rather than tracked per physical
		/// key, and that is load bearing. Command and physical Control both map onto agg's Control, as do
		/// left and right Shift onto Shift; if each physical key were tracked on its own then releasing
		/// Command while Control was still held would clear the Control state even though the user is
		/// still holding a Control. The flags word is the truth, so it is what the state is computed from.
		/// </remarks>
		/// <returns>Always false: AppKit needs to see modifier changes for menu key equivalents and
		/// cursor tracking, so this one is never swallowed.</returns>
		private bool HandleFlagsChanged(IntPtr nsEvent)
		{
			ulong flags = Send_Q(nsEvent, Sel("modifierFlags"));

			this.appliedModifierKeys = ApplyModifierFlagsToKeyboard(flags);

			// AppKit suppresses keyUp: while Command is held, so every ordinary key pressed during a
			// Command chord is still latched down in Keyboard. Command going away is the moment those ups
			// are known never to arrive, so that is when the latched keys are released.
			bool commandWasHeld = (this.lastModifierFlags & NSEventModifierFlagCommand) != 0;
			bool commandIsHeld = (flags & NSEventModifierFlagCommand) != 0;
			if (commandWasHeld && !commandIsHeld)
			{
				Keyboard.ClearNonModifierKeys();
			}

			this.lastModifierFlags = flags;

			return false;
		}

		/// <summary>
		/// Releases the modifiers this window put down, because it is no longer the key window and can no
		/// longer be told they were let go of.
		/// </summary>
		/// <remarks>
		/// macOS delivers flagsChanged only to the key window, so a modifier released while another
		/// application is frontmost is never reported to us and stays latched down forever. Cmd-Tab is the
		/// everyday case - it *begins* with Command held - and the symptom is that coming back to the
		/// application leaves the 3D view convinced Control is held, so a plain left drag rotates the
		/// camera instead of selecting.
		/// <para/>
		/// Deliberately unguarded, unlike <see cref="HandleDidBecomeKey"/>. This is the exact inverse of
		/// <see cref="HandleFlagsChanged"/> - it undoes what that put down and can touch nothing else - so
		/// there is no synthetic state for it to damage and nothing to guard against. Guarding it while
		/// leaving its counterpart unguarded is what latched Control forever in the first place: the window
		/// went on writing real modifier state into <see cref="Keyboard"/> while permanently losing the
		/// focus-loss release that compensates for it.
		/// </remarks>
		private void HandleDidResignKey()
		{
			this.lastModifierFlags = ReleaseAppliedModifierKeys(this.appliedModifierKeys);
			this.appliedModifierKeys = NoModifierKeys;
		}

		/// <summary>
		/// Re-derives the held modifiers from the live flags word now that the window is key again.
		/// </summary>
		/// <remarks>
		/// The counterpart to <see cref="HandleDidResignKey"/>, and just as necessary: a user genuinely can
		/// be holding a modifier at the moment focus returns - releasing Cmd-Tab commonly leaves Command
		/// down for a beat over the newly frontmost window - and every flags change that happened while we
		/// were unfocused was delivered somewhere else. Without this the first drag back would be wrong in
		/// the opposite direction, with a held modifier the window never heard about.
		/// <para/>
		/// The one guarded handler of the three, because it is the only one that <em>polls</em> the real
		/// keyboard rather than reacting to an event about it. Both conditions say the same thing from
		/// different directions: EnablePlatformWindowInput off means a run has asked that the real machine
		/// not perturb it, and <see cref="SetModifierKeys"/>' contract is that once a synthetic event has
		/// declared what it is holding the real keyboard is never read again. Answering "nothing is held"
		/// from a machine with no user at it would overwrite the synthetic state with a lie.
		/// </remarks>
		private void HandleDidBecomeKey()
		{
			if (!IPlatformWindow.EnablePlatformWindowInput || this.modifiersOverridden)
			{
				return;
			}

			// +[NSEvent modifierFlags] is the global "what is held right now", not a property of any event,
			// which is exactly what is wanted when no event told us. Same call ModifierKeys makes.
			ulong flags = Send_Q(Class("NSEvent"), Sel("modifierFlags"));

			this.appliedModifierKeys = ApplyModifierFlagsToKeyboard(flags);

			this.lastModifierFlags = flags;
		}

		/// <summary>
		/// Puts the modifier down state a flags word implies into <see cref="Keyboard"/>, and reports the
		/// keys it left held so <see cref="ReleaseAppliedModifierKeys"/> can undo exactly those.
		/// </summary>
		/// <remarks>
		/// Shared by <see cref="HandleFlagsChanged"/> and <see cref="HandleDidBecomeKey"/>, which have to
		/// agree exactly on what a flags word means; two copies would drift.
		/// <para/>
		/// Every modifier is written on every call, including the ones being released. There is no "has
		/// this changed?" test here on purpose: <c>Keyboard.SetKeyDownState</c> is idempotent and raises
		/// StateChanged only on a real change, so the redundant writes cost nothing, and a test here could
		/// only compare the physical spelling (ControlKey) while automation latches the fanned-out one
		/// (Control) - it would conclude "no change" and leave the very latch this call exists to correct.
		/// </remarks>
		internal static IReadOnlySet<Keys> ApplyModifierFlagsToKeyboard(ulong flags)
		{
			IReadOnlySet<Keys> shouldBeDown = ModifierDownStateKeys(flags);
			foreach (Keys modifierKey in ModifierStateKeys)
			{
				Keyboard.SetKeyDownState(modifierKey, shouldBeDown.Contains(modifierKey));
			}

			return shouldBeDown;
		}

		/// <summary>
		/// Releases the modifier keys this window put into the down state, and reports the modifier-flags
		/// word that now describes what it is holding - nothing.
		/// </summary>
		/// <remarks>
		/// Narrow on purpose, where a <c>Keyboard.Clear()</c> would not be. <see cref="Keyboard"/> is
		/// process-wide and other callers write to it directly - an automation test sets Shift down and
		/// then shift-clicks - so a blunt clear turns any incidental focus change into a dropped
		/// selection with no visible cause. Releasing only what this window applied cannot reach anything
		/// it did not put there, which is what lets the focus handlers run unguarded.
		/// <para/>
		/// The return exists so that releasing the keys and forgetting the remembered flags word cannot be
		/// done separately. <see cref="lastModifierFlags"/> is what the next flags change computes its
		/// transitions against; leaving a stale word there would make the Command-dropped detection in
		/// <see cref="HandleFlagsChanged"/> fire (or fail to fire) on nothing the user did.
		/// </remarks>
		internal static ulong ReleaseAppliedModifierKeys(IReadOnlySet<Keys> appliedModifierKeys)
		{
			foreach (Keys modifierKey in appliedModifierKeys)
			{
				Keyboard.SetKeyDownState(modifierKey, false);
			}

			return 0;
		}

		private bool HandleKeyDown(IntPtr nsEvent)
		{
			ulong flags = Send_Q(nsEvent, Sel("modifierFlags"));
			var keyEvent = MakeKeyEventArgs(nsEvent, flags);

			this.aggSystemWindow.OnKeyDown(keyEvent);
			Keyboard.SetKeyDownState(keyEvent.KeyCode, true);

			// A Command chord is a shortcut, never text, so it stops at the key down - typing Cmd-S must
			// not also insert an "s" into whatever has focus.
			bool commandHeld = (flags & NSEventModifierFlagCommand) != 0;
			if (commandHeld)
			{
				if (!keyEvent.Handled)
				{
					// Nothing in the application claimed it, so the menu bar gets its turn. For an event that
					// reaches one of our windows this is the only way a menu shortcut can fire, since the
					// event is never given to AppKit; one that belongs to a window we do not own - a native
					// open panel - is passed on and AppKit searches the menus itself, which ends in the same
					// place.
					//
					// The Handled test is not by itself a guarantee against acting on a chord twice, and must
					// not be read as one. It is read here, synchronously, the moment OnKeyDown returns - but a
					// KeyDown handler is free to be async, and MatterCAD's is: its Cmd-C, Cmd-X and Cmd-S arms
					// await real work and only then set Handled, so when the await yields this sees Handled
					// false for a chord the application is in the middle of servicing. What actually keeps
					// those chords safe is that no MenuItemRole maps to c, x or s, so the forward matches
					// nothing. That is the invariant to keep: a role's chord may not overlap a chord the
					// managed side handles asynchronously, or the two will both fire. Cmd-Q is the shape that
					// is fine either way - handled synchronously, and the Quit item's action goes through the
					// same root window Close() regardless.
					//
					// The result is deliberately unused: the event is swallowed whether or not a menu item
					// took it, so there is nothing to decide.
					_ = MacMenuBar.PerformKeyEquivalent(nsEvent);
				}

				return true;
			}

			if (!keyEvent.SuppressKeyPress)
			{
				// -[NSEvent characters] is the layout-, dead-key- and modifier-resolved text, which is
				// exactly what OnKeyPress wants; keyCode above is a raw hardware position and is not.
				string typed = FromNSString(Send_r(nsEvent, Sel("characters")));
				if (!string.IsNullOrEmpty(typed))
				{
					foreach (char character in typed)
					{
						// Function keys and arrows arrive as private-use-area characters; they were already
						// delivered as a key down and are not text.
						if (character >= 0xF700 && character <= 0xF8FF)
						{
							continue;
						}

						this.aggSystemWindow.OnKeyPress(new KeyPressEventArgs(character));
					}
				}
			}

			// Swallowed so the responder chain cannot end at NSBeep.
			return true;
		}

		private bool HandleKeyUp(IntPtr nsEvent)
		{
			var keyEvent = MakeKeyEventArgs(nsEvent, Send_Q(nsEvent, Sel("modifierFlags")));

			// Only process the key up if we saw the key down, matching the Windows sink.
			if (Keyboard.IsKeyDown(keyEvent.KeyCode))
			{
				this.aggSystemWindow.OnKeyUp(keyEvent);
				Keyboard.SetKeyDownState(keyEvent.KeyCode, false);
			}

			// Swallowed for the same reason a key down is, Command chords included.
			return true;
		}

		/// <summary>
		/// Reads the parts of a key NSEvent that decide which agg key it is, and composes the event args.
		/// </summary>
		private KeyEventArgs MakeKeyEventArgs(IntPtr nsEvent, ulong flags)
		{
			return MakeKeyEventArgs(
				Send_u(nsEvent, Sel("keyCode")),
				FromNSString(Send_r(nsEvent, Sel("charactersIgnoringModifiers"))),
				flags);
		}

		/// <summary>
		/// Composes the agg key event a keyDown or keyUp carries, from the three parts of the NSEvent that
		/// determine it.
		/// </summary>
		/// <remarks>
		/// Pure - no ObjC calls, no state - so the whole key translation can be exercised without a window,
		/// in the same spirit as <see cref="ModifierDownStateKeys"/>.
		/// </remarks>
		internal static KeyEventArgs MakeKeyEventArgs(ushort virtualKey, string charactersIgnoringModifiers, ulong flags)
		{
			Keys keyCode = TranslateKeyCode(virtualKey);

			if (keyCode == Keys.None)
			{
				keyCode = TranslateCharacterKey(charactersIgnoringModifiers);
			}

			return new KeyEventArgs(keyCode | TranslateModifiers(flags));
		}

		/// <summary>
		/// Maps the layout-resolved text of a key onto agg's <see cref="Keys"/>, for the letters and digits
		/// <see cref="TranslateKeyCode"/> deliberately does not name.
		/// </summary>
		/// <remarks>
		/// A virtual key code is a hardware position - 0x01 is "where S sits on a US layout" and is a
		/// different letter on an AZERTY one - so the key code table cannot answer "which letter is this".
		/// Shortcuts are all spelled as key codes (Ctrl+S, Ctrl+Z, Ctrl+A), so without this every one of
		/// them arrived as a bare Control modifier with <see cref="Keys.None"/> attached and matched
		/// nothing: Cmd-S did not save, it only beeped.
		/// <para/>
		/// <c>-[NSEvent charactersIgnoringModifiers]</c> is the source because it resolves the layout while
		/// factoring Command and Option back out - Option-S is "s" here and the dead-key text only in
		/// <c>characters</c>. Shift is <em>not</em> factored out, so each shifted spelling has to be mapped
		/// onto the same key its unshifted spelling gives, which is what WinForms reports either way.
		/// </remarks>
		internal static Keys TranslateCharacterKey(string charactersIgnoringModifiers)
		{
			if (string.IsNullOrEmpty(charactersIgnoringModifiers))
			{
				return Keys.None;
			}

			char character = char.ToUpperInvariant(charactersIgnoringModifiers[0]);

			if (character >= 'A' && character <= 'Z')
			{
				return Keys.A + (character - 'A');
			}

			if (character >= '0' && character <= '9')
			{
				return Keys.D0 + (character - '0');
			}

			switch (character)
			{
				// The zoom shortcuts, with the shifted spelling of each key alongside the unshifted one.
				case '=':
				case '+':
					return Keys.Oemplus;

				case '-':
				case '_':
					return Keys.OemMinus;

				default:
					return Keys.None;
			}
		}

		/// <summary>
		/// Which of <see cref="PointerEventKind"/>'s four kinds an NSEvent type is, so the shared capture
		/// rule in <see cref="OutOfViewMouseCapture"/> never has to know AppKit's numbering.
		/// </summary>
		internal static PointerEventKind PointerEventKindFor(long type)
		{
			switch (type)
			{
				case NSEventTypeLeftMouseDown:
				case NSEventTypeRightMouseDown:
				case NSEventTypeOtherMouseDown:
					return PointerEventKind.Down;

				case NSEventTypeLeftMouseUp:
				case NSEventTypeRightMouseUp:
				case NSEventTypeOtherMouseUp:
					return PointerEventKind.Up;

				case NSEventTypeLeftMouseDragged:
				case NSEventTypeRightMouseDragged:
				case NSEventTypeOtherMouseDragged:
					return PointerEventKind.Drag;

				default:
					return PointerEventKind.Other;
			}
		}

		/// <summary>
		/// The content view's bounds as agg sees them. The origin is dropped rather than carried across: an
		/// NSView's bounds origin is zero unless something shifts it, and this test has always been against
		/// the size alone.
		/// </summary>
		private static RectangleDouble ToAggBounds(CGRect bounds)
			=> new RectangleDouble(0, 0, bounds.Size.Width, bounds.Size.Height);

		/// <summary>See <see cref="OutOfViewMouseCapture.IsInsideBounds"/>; this is the AppKit-typed adapter.</summary>
		internal static bool IsInsideBounds(CGPoint inView, CGRect bounds)
			=> OutOfViewMouseCapture.IsInsideBounds(new Vector2(inView.X, inView.Y), ToAggBounds(bounds));

		/// <summary>
		/// Whether a mouseExited event means the pointer actually left the content view.
		/// </summary>
		/// <remarks>
		/// The event type on its own does not mean that, which is the trap: a mouseExited is a tracking
		/// notification, and every <c>invalidateCursorRectsForView:</c> that <see cref="SetCursor"/> issues
		/// posts one for a pointer that never moved. See
		/// <see cref="OutOfViewMouseCapture.IsRealPointerExit"/>, which holds the whole story and the rule
		/// this reads the AppKit numbers for.
		/// </remarks>
		private bool PointerReallyLeftContentView(IntPtr nsEvent)
		{
			CGPoint inWindow = Send_P(nsEvent, Sel("locationInWindow"));
			CGPoint inView = Send_P_P_r(this.view, Sel("convertPoint:fromView:"), inWindow, IntPtr.Zero);

			return IsRealPointerExit(inView, Send_R(this.view, Sel("bounds")), this.mouseCapture.HasCapturedButtons);
		}

		/// <summary>See <see cref="OutOfViewMouseCapture.IsRealPointerExit"/>; this is the AppKit-typed adapter.</summary>
		internal static bool IsRealPointerExit(CGPoint inView, CGRect bounds, bool dragInFlight)
			=> OutOfViewMouseCapture.IsRealPointerExit(new Vector2(inView.X, inView.Y), ToAggBounds(bounds), dragInFlight);

		/// <summary>
		/// Converts an NSEvent's location into agg's coordinate space.
		/// </summary>
		/// <remarks>
		/// Two things happen here and one deliberately does not. <c>locationInWindow</c> is in window
		/// points, so it is converted into the view and then multiplied by <c>backingScaleFactor</c> to
		/// reach agg's device pixels. What does <em>not</em> happen is a Y flip: a non-flipped NSView is
		/// already bottom-left origin with Y increasing upwards, which is agg's convention. The Windows
		/// sink flips only because Win32 is top-left.
		/// </remarks>
		/// <returns>False when agg must not see the event at all: it happened outside the content view (the
		/// title bar, say) and no button held by this view makes it ours. See
		/// <see cref="OutOfViewMouseCapture"/> for why a drag is the exception.</returns>
		private bool TryMakeMouseArgs(IntPtr nsEvent, long type, out MouseEventArgs args)
		{
			args = null;

			CGPoint inWindow = Send_P(nsEvent, Sel("locationInWindow"));
			CGPoint inView = Send_P_P_r(this.view, Sel("convertPoint:fromView:"), inWindow, IntPtr.Zero);
			CGRect bounds = Send_R(this.view, Sel("bounds"));

			bool insideView = IsInsideBounds(inView, bounds);

			MouseButtons button = type switch
			{
				NSEventTypeLeftMouseDown or NSEventTypeLeftMouseUp or NSEventTypeLeftMouseDragged => MouseButtons.Left,
				NSEventTypeRightMouseDown or NSEventTypeRightMouseUp or NSEventTypeRightMouseDragged => MouseButtons.Right,
				NSEventTypeOtherMouseDown or NSEventTypeOtherMouseUp or NSEventTypeOtherMouseDragged => MouseButtons.Middle,
				_ => MouseButtons.None,
			};

			if (!this.mouseCapture.ShouldDeliver(PointerEventKindFor(type), button, insideView))
			{
				return false;
			}

			// Deliberately not clamped to the bounds: a drag that ran past the window edge should reach the
			// widget with where the pointer really is, the same coordinates WinForms reports while it holds
			// the capture, so that dragging out and back does not look like a jump to the edge and stop.
			double x = inView.X * this.backingScale;
			double y = inView.Y * this.backingScale;

			int clicks = 0;
			if (type == NSEventTypeLeftMouseDown || type == NSEventTypeLeftMouseUp
				|| type == NSEventTypeRightMouseDown || type == NSEventTypeRightMouseUp
				|| type == NSEventTypeOtherMouseDown || type == NSEventTypeOtherMouseUp)
			{
				clicks = (int)Send_q(nsEvent, Sel("clickCount"));
			}

			int wheelDelta = 0;
			if (type == NSEventTypeMagnify)
			{
				wheelDelta = WheelDeltaMath.MagnificationToWheelDelta(Send_d(nsEvent, Sel("magnification")));
			}

			args = new MouseEventArgs(button, clicks, x, y, wheelDelta);

			if (type == NSEventTypeScrollWheel)
			{
				WheelDeltaMath.ApplyScrollingDeltas(
					args,
					Send_d(nsEvent, Sel("scrollingDeltaX")),
					Send_d(nsEvent, Sel("scrollingDeltaY")),
					Send_B(nsEvent, Sel("hasPreciseScrollingDeltas")) != NO,
					this.backingScale);
			}

			return true;
		}

		/// <summary>
		/// Remembers whether a pinch is running, from the phase on each magnify event. See
		/// <see cref="magnifyGestureInFlight"/> for what the answer is used for.
		/// </summary>
		private void TrackMagnifyGesturePhase(IntPtr nsEvent)
		{
			ulong phase = Send_Q(nsEvent, Sel("phase"));

			if ((phase & (NSEventPhaseEnded | NSEventPhaseCancelled)) != 0)
			{
				this.magnifyGestureInFlight = false;
			}
			else
			{
				// Began, Changed, or - on a device that reports no phase at all - anything else that still
				// carries a magnification. Treating the phaseless case as "in flight" is the safe way round:
				// the worst it costs is a scroll dropped while pinching.
				this.magnifyGestureInFlight = true;
			}
		}

		/// <summary>
		/// Clears <see cref="magnifyGestureInFlight"/> when a scroll event proves no pinch can be running.
		/// </summary>
		/// <remarks>
		/// A scroll that is beginning a gesture of its own, or one from a device that has no gestures at all
		/// (a real mouse wheel reports no phase), cannot be part of a pinch. Without this the latch is only
		/// ever cleared by the pinch's own Ended, and a pinch that never delivers one - the window loses focus
		/// mid-gesture, say - would leave this window unable to scroll for the rest of its life.
		/// </remarks>
		private void TrackScrollGesturePhase(IntPtr nsEvent)
		{
			ulong phase = Send_Q(nsEvent, Sel("phase"));

			if (phase == NSEventPhaseNone || (phase & NSEventPhaseBegan) != 0)
			{
				this.magnifyGestureInFlight = false;
			}
		}

		/// <summary>
		/// Prints one scroll or magnify event when <c>AGG_LOG_GESTURE=1</c>; see
		/// <see cref="LogGestureEvents"/>. Each property is only asked of the event types that document it -
		/// an unanswered selector would raise an Objective-C exception, which aborts the process rather than
		/// failing a call.
		/// </summary>
		private void LogGestureEvent(IntPtr nsEvent, long type)
		{
			if (!LogGestureEvents)
			{
				return;
			}

			ulong phase = Send_Q(nsEvent, Sel("phase"));

			if (type == NSEventTypeMagnify)
			{
				Console.WriteLine($"AGG_LOG_GESTURE magnify phase=0x{phase:x} magnification={Send_d(nsEvent, Sel("magnification")):0.#####}");
			}
			else
			{
				ulong momentumPhase = Send_Q(nsEvent, Sel("momentumPhase"));
				double scrollingDeltaX = Send_d(nsEvent, Sel("scrollingDeltaX"));
				double scrollingDeltaY = Send_d(nsEvent, Sel("scrollingDeltaY"));
				bool precise = Send_B(nsEvent, Sel("hasPreciseScrollingDeltas")) != NO;

				Console.WriteLine($"AGG_LOG_GESTURE scroll phase=0x{phase:x} momentumPhase=0x{momentumPhase:x} scrollingDeltaX={scrollingDeltaX:0.#####} scrollingDeltaY={scrollingDeltaY:0.#####} precise={precise} magnifyInFlight={this.magnifyGestureInFlight}");
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
							// floating on an empty background. Kept identical to WinformsSystemWindow.
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
			this.CaptureThenPresent(screenshotPath, this.screenshotComplete, this.screenshotCompletion);
		}

		/// <summary>
		/// Saves the frame and then presents it. <c>async void</c> on purpose: this is the end of a frame
		/// and there is nobody to hand a Task to. The native read-back completes before its ValueTask is
		/// returned, so the present still happens inline, while the frame is alive.
		/// </summary>
		/// <param name="path">Where to write the PNG.</param>
		/// <param name="completed">Signalled once the file is written, for a synchronous requester. Null when
		/// the capture was requested by the smoke-run path, which does not wait.</param>
		/// <param name="completion">The same signal for an awaiting <see cref="CaptureScreenshotAsync"/>
		/// caller. Null unless the request came from there. A failed capture faults it rather than
		/// completing it, so the async contract's "the file exists once the task completes" holds.</param>
		private async void CaptureThenPresent(string path, ManualResetEventSlim completed, TaskCompletionSource completion)
		{
			Exception failure = null;

			try
			{
				await this.webGpuLayer.SaveCurrentFrameAsync(path);
			}
			catch (Exception ex)
			{
				// The synchronous caller has no channel for this - releasing its pump is all that can be
				// done for it - so it keeps the long-standing behaviour of a stderr note and a quiet give up.
				failure = ex;
				Console.Error.WriteLine($"MacSystemWindow screenshot failed: {ex.Message}");
			}
			finally
			{
				completed?.Set();

				// TrySet, not Set: the awaiting caller may already have timed out and moved on.
				if (failure != null)
				{
					completion?.TrySetException(failure);
				}
				else
				{
					completion?.TrySetResult();
				}
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

		/// <summary>Answers AppKit's "may I close?" by asking the agg window, and starts the agg close.</summary>
		private byte HandleShouldClose()
		{
			bool mayClose = PlatformCloseArbitration.HandlePlatformCloseRequest(
				SingleWindowMode,
				this.WindowProvider,
				this.aggSystemWindow,
				closing => this.platformAlreadyClosing = closing);

			return mayClose ? YES : NO;
		}

		/// <summary>Tears everything down once AppKit has committed to closing the window.</summary>
		private void HandleWillClose()
		{
			if (this.hasClosed)
			{
				return;
			}

			this.hasClosed = true;

			if (this.idleTimer != IntPtr.Zero)
			{
				Send_v(this.idleTimer, Sel("invalidate"));
				Release(this.idleTimer);
				this.idleTimer = IntPtr.Zero;
			}

			this.webGpuLayer?.Dispose();
			this.webGpuLayer = null;

			// Break the delegate link before the objects go away, or a late notification would find a
			// half-dead window.
			Send_v_r(this.window, Sel("setDelegate:"), IntPtr.Zero);

			bool wasLast;
			lock (StaticInitLock)
			{
				DelegateOwners.Remove(this.windowDelegate);

				// The view itself outlives this (see the note below), so drop the back-pointer or a late
				// -resetCursorRects would find a closed window.
				ViewOwners.Remove(this.view);

				LiveWindows.Remove(this);
				wasLast = LiveWindows.Count == 0;
			}

			Release(this.windowDelegate);
			this.windowDelegate = IntPtr.Zero;

			Release(this.metalLayer);
			this.metalLayer = IntPtr.Zero;

			// The NSWindow and its content view are deliberately NOT released here. This runs from inside
			// -[NSWindow close], so the window is still executing on the stack above us; dropping its last
			// reference would dealloc it mid-call. setReleasedWhenClosed:NO means AppKit will not free it
			// either, so a closed window costs one leaked NSWindow + NSView. Bounded and small (windows are
			// not opened in a loop), and the alternative - an autorelease with no pool guaranteed to be in
			// scope - leaks the same memory and prints a Cocoa warning while doing it.
			this.aggSystemWindow = null;

			if (wasLast)
			{
				runLoopActive = false;
			}
		}

		private void CloseNativeWindow()
		{
			if (this.hasClosed || this.window == IntPtr.Zero)
			{
				return;
			}

			// setReleasedWhenClosed:NO was set at construction, so -close only orders the window out and
			// fires windowWillClose:; the NSWindow itself stays valid for the teardown below.
			Send_v_r(this.window, Sel("orderOut:"), IntPtr.Zero);
			Send_v(this.window, Sel("close"));

			// -close is documented to send windowWillClose:, but a window that was never ordered in does
			// not always, and a half-torn-down window would keep the run loop alive forever.
			this.HandleWillClose();
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
				var windowToClose = this.ShellAggWindow();

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
		/// The agg window whose close ends the application: the shell, not whatever is currently on top.
		/// See <see cref="PlatformCloseArbitration.ShellWindowForClose"/> for why
		/// <see cref="aggSystemWindow"/> is not that window in single window mode.
		/// </summary>
		private SystemWindow ShellAggWindow()
		{
			return PlatformCloseArbitration.ShellWindowForClose(SingleWindowMode, this.WindowProvider, this.aggSystemWindow);
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

		// -----------------------------------------------------------------------------------------
		// Small AppKit helpers
		// -----------------------------------------------------------------------------------------

		/// <summary>The primary screen's height in points - the origin AppKit measures window frames from.</summary>
		private static double PrimaryScreenHeightInPoints()
		{
			IntPtr screens = Send_r(Class("NSScreen"), Sel("screens"));
			IntPtr primary = screens == IntPtr.Zero || Send_Q(screens, Sel("count")) == 0
				? IntPtr.Zero
				: Send_r_Q(screens, Sel("objectAtIndex:"), 0);
			if (primary == IntPtr.Zero)
			{
				primary = Send_r(Class("NSScreen"), Sel("mainScreen"));
			}

			return primary == IntPtr.Zero ? 0 : Send_R(primary, Sel("frame")).Size.Height;
		}

		private static double PrimaryScreenScale()
		{
			IntPtr screen = Send_r(Class("NSScreen"), Sel("mainScreen"));
			double scale = screen == IntPtr.Zero ? 1 : Send_d(screen, Sel("backingScaleFactor"));
			return scale > 0 ? scale : 1;
		}

		/// <summary>
		/// The scale desktop coordinates are expressed in. agg's desktop space is device pixels, and the
		/// primary screen's scale is the only one that can be used for a space shared by every window -
		/// which does mean a second display running at a different scale reports positions that are off by
		/// the ratio. Nothing in agg positions windows precisely enough for that to matter yet.
		/// </summary>
		private static double DesktopScale() => PrimaryScreenScale();

		/// <summary>The complete set of down-state keys <see cref="ModifierDownStateKeys"/> can report, so
		/// a flags change can set and clear all of them from one loop.</summary>
		private static readonly Keys[] ModifierStateKeys = { Keys.ShiftKey, Keys.ControlKey, Keys.Menu };

		/// <summary>Holding nothing - the starting value for <see cref="appliedModifierKeys"/>.</summary>
		private static readonly IReadOnlySet<Keys> NoModifierKeys = new HashSet<Keys>();

		/// <summary>
		/// Maps a raw AppKit modifier-flags word onto the agg down-state keys it implies.
		/// </summary>
		/// <remarks>
		/// Deliberately pure - no ObjC calls, no state - because it is the whole of the modifier
		/// translation and is worth testing without a window.
		/// <para/>
		/// Note the two-to-one mapping: Command <em>and</em> physical Control both produce
		/// <see cref="Keys.ControlKey"/>. Every agg shortcut and every 3D view gesture is spelled
		/// "Control+X"; on a Mac the key a user reaches for is usually Command, but Control is right there
		/// as well and users press it, so both are honoured.
		/// <para/>
		/// The answer is a set and not an OR'd <see cref="Keys"/> value because ShiftKey (16), ControlKey
		/// (17) and Menu (18) are consecutive integers rather than disjoint bits - OR-ing them would
		/// produce unrelated key codes. The modifier <em>flags</em> Shift/Control/Alt that
		/// <see cref="TranslateModifiers"/> returns are disjoint bits and do combine.
		/// </remarks>
		internal static IReadOnlySet<Keys> ModifierDownStateKeys(ulong flags)
		{
			var downKeys = new HashSet<Keys>();

			if ((flags & NSEventModifierFlagShift) != 0)
			{
				downKeys.Add(Keys.ShiftKey);
			}

			if ((flags & (NSEventModifierFlagCommand | NSEventModifierFlagControl)) != 0)
			{
				downKeys.Add(Keys.ControlKey);
			}

			if ((flags & NSEventModifierFlagOption) != 0)
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
		/// <c>Keyboard.IsKeyDown(Keys.Control)</c> says and what <c>ModifierKeys</c> says have to agree, or
		/// a gesture that checks one and a shortcut that checks the other disagree about the same keyboard.
		/// </remarks>
		internal static Keys TranslateModifiers(ulong flags)
		{
			Keys modifiers = Keys.None;

			foreach (Keys downKey in ModifierDownStateKeys(flags))
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
		/// Maps a Carbon virtual key code to agg's <see cref="Keys"/>. Only the keys agg actually reacts to
		/// are listed; everything else resolves through <c>-[NSEvent characters]</c> as a key press, which
		/// is layout correct in a way a key code table can never be.
		/// </summary>
		private static Keys TranslateKeyCode(ushort virtualKey)
		{
			switch (virtualKey)
			{
				case VkReturn: return Keys.Enter;
				case VkTab: return Keys.Tab;
				case VkSpace: return Keys.Space;
				case VkDelete: return Keys.Back;
				case VkForwardDelete: return Keys.Delete;
				case VkEscape: return Keys.Escape;
				case VkCommand: return Keys.ControlKey;
				case VkShift:
				case VkRightShift: return Keys.ShiftKey;
				case VkCapsLock: return Keys.CapsLock;
				case VkOption:
				case VkRightOption: return Keys.Menu;
				case VkControl:
				case VkRightControl: return Keys.ControlKey;
				case VkHome: return Keys.Home;
				case VkEnd: return Keys.End;
				case VkPageUp: return Keys.PageUp;
				case VkPageDown: return Keys.PageDown;
				case VkLeftArrow: return Keys.Left;
				case VkRightArrow: return Keys.Right;
				case VkUpArrow: return Keys.Up;
				case VkDownArrow: return Keys.Down;
				case VkF1: return Keys.F1;
				case VkF2: return Keys.F2;
				case VkF3: return Keys.F3;
				case VkF4: return Keys.F4;
				case VkF5: return Keys.F5;
				case VkF6: return Keys.F6;
				case VkF7: return Keys.F7;
				case VkF8: return Keys.F8;
				case VkF9: return Keys.F9;
				case VkF10: return Keys.F10;
				case VkF11: return Keys.F11;
				case VkF12: return Keys.F12;
				default: return Keys.None;
			}
		}
	}
}
