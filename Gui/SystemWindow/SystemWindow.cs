/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
using System.Linq;
using Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class SystemWindow : GuiWidget
	{
		public static bool EnableAllowDrop = true;

		private string _title = "";

		public bool AlwaysOnTopOfMain { get; set; }

		public bool CenterInParent { get; set; } = true;

		public bool IsModal { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this window is an application's top level shell rather than a
		/// dialog or a child window. A single window provider hosts exactly one shell and draws every other
		/// SystemWindow inside it as a titled child window - correct for a dialog, but for a second shell it
		/// renders a whole second application inside the first. Providers use this to refuse that instead.
		/// </summary>
		public bool IsApplicationShell { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this window renders on the GPU. GPU rendering is the default for
		/// every window; set false (e.g. via RootSystemWindow.DefaultUseGpu / the FORCE_SOFTWARE_RENDERING switch) to
		/// request wgpu's software (fallback) adapter when hosted in a WebGpuSystemWindow.
		/// </summary>
		public bool UseGpu { get; set; } = true;

		public int StencilBufferDepth { get; set; }

		public ToolTipManager ToolTipManager { get; private set; }

		public string Title
		{
			get => _title;
			set
			{
				_title = value;

				if (this.PlatformWindow != null)
				{
					this.PlatformWindow.Caption = _title;
				}
			}
		}

		public enum PixelTypes
		{
			Depth24 = 24,
			Depth32 = 32,
			DepthFloat = 128
		}

		public PixelTypes PixelType { get; set; } = PixelTypes.Depth32;

		public int BitDepth => (int)this.PixelType;

		/// <summary>
		/// Gets how many device pixels one point is worth on the monitor this window is currently on: 2 on a
		/// Retina display, 1 on a standard one, 1.5 on a 150% Windows display. 1 until a platform host says
		/// otherwise, which is also what a headless or non-DPI-aware host leaves it at.
		/// </summary>
		/// <remarks>
		/// This is a property of the <em>monitor</em>, not of the window's own coordinates - agg still deals
		/// exclusively in device pixels, and the hosts already scale the window's bounds. It is published so
		/// an application can size text and chrome for the display it is actually on, and it changes when the
		/// user drags the window to a display with a different scale.
		/// </remarks>
		public double DisplayScale { get; private set; } = 1;

		/// <summary>
		/// Raised on the UI thread after <see cref="DisplayScale"/> has changed to a different value,
		/// typically because the window was dragged onto a monitor with a different DPI. Applications
		/// rebuild their UI at the new scale from here.
		/// </summary>
		/// <remarks>
		/// Always raised asynchronously, through the idle queue - see <see cref="SetDisplayScale"/> for why
		/// the platform hosts cannot afford to have subscriber code run where they discover the change.
		/// </remarks>
		public event EventHandler DisplayScaleChanged;

		/// <summary>Guards the coalescing state below, which any thread's SetDisplayScale can touch.</summary>
		private readonly object displayScaleLock = new object();

		/// <summary>True while a raise is sitting in the idle queue, so a second change does not queue another.</summary>
		private bool displayScaleRaisePending;

		/// <summary>
		/// True once a platform host has reported a scale at all. Until then <see cref="DisplayScale"/> is the
		/// assumed default rather than anything measured, which is why the first report is always news.
		/// </summary>
		private bool hasReceivedHostDisplayScale;

		/// <summary>True once <see cref="DisplayScaleChanged"/> has been raised, so the value below means something.</summary>
		private bool hasRaisedDisplayScaleChanged;

		/// <summary>
		/// The value <see cref="DisplayScaleChanged"/> was last raised for. Compared against rather than
		/// against the value at queue time, so a window that leaves a 2x display and comes back before the
		/// queue drains says nothing at all.
		/// </summary>
		private double lastRaisedDisplayScale;

		/// <summary>
		/// Tells this window what the monitor it is on now scales by. Called by the platform hosts - the mac
		/// host from <c>windowDidChangeScreen:</c>/<c>windowDidChangeBackingProperties:</c>, the WinForms host
		/// from <c>DpiChanged</c>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The property is updated synchronously, but the event is always raised from the idle queue, and at
		/// most one raise is ever pending. Both halves of that matter. The hosts learn about a scale change
		/// from inside a native callback that runs in a window-drag tracking loop, where the host's own event
		/// pump is frozen; a subscriber that rebuilds the whole UI from there would run for the length of the
		/// rebuild in the middle of the drag, and on the mac would do it inside an Objective-C frame that
		/// must not see a managed exception. Deferring hands that work back to the normal idle pump (which
		/// keeps ticking inside tracking loops, because it is a timer). Coalescing then keeps a drag that
		/// crosses a display boundary several times from queueing a rebuild per crossing - only where the
		/// window ends up matters.
		/// </para>
		/// <para>
		/// The FIRST report always raises, even when it matches the 1 this window started at. That default was
		/// an assumption, not a measurement, and an application that guessed differently has no other way to
		/// hear the truth: a Retina primary with a 1x second monitor makes an app compute 2 at startup from the
		/// primary, and the window restored onto the second monitor reports exactly 1 - which, treated as "no
		/// change", would leave the UI at 2 forever, since every later return to that monitor is genuinely no
		/// change too. Subscribers are expected to be idempotent about a scale they already agree with.
		/// </para>
		/// <para>
		/// A value that is not a usable multiplier is clamped to 1 rather than rejected: a monitor hot-plug
		/// can be caught mid-transition reporting 0, and a UI laid out at scale 0 is a UI with nothing in it.
		/// </para>
		/// </remarks>
		/// <param name="displayScale">Device pixels per point; anything not finite and positive becomes 1.</param>
		public void SetDisplayScale(double displayScale)
		{
			if (double.IsNaN(displayScale) || double.IsInfinity(displayScale) || displayScale <= 0)
			{
				displayScale = 1;
			}

			lock (displayScaleLock)
			{
				bool isFirstHostReport = !this.hasReceivedHostDisplayScale;
				this.hasReceivedHostDisplayScale = true;

				if (!isFirstHostReport && displayScale == this.DisplayScale)
				{
					return;
				}

				this.DisplayScale = displayScale;

				if (this.displayScaleRaisePending)
				{
					// The queued raise reads DisplayScale when it runs, so it already covers this change.
					return;
				}

				this.displayScaleRaisePending = true;
			}

			UiThread.RunOnIdle(this.RaisePendingDisplayScaleChanged);
		}

		private void RaisePendingDisplayScaleChanged()
		{
			lock (displayScaleLock)
			{
				this.displayScaleRaisePending = false;

				// Only a raise that has already happened can make another one redundant - the first one has
				// nothing to be redundant with, whatever the value.
				if (this.hasRaisedDisplayScaleChanged
					&& this.DisplayScale == this.lastRaisedDisplayScale)
				{
					return;
				}

				this.hasRaisedDisplayScaleChanged = true;
				this.lastRaisedDisplayScale = this.DisplayScale;
			}

			if (this.HasBeenClosed)
			{
				// A window can close between the change and the pump; its subscribers would be rebuilding a
				// UI that no longer exists.
				return;
			}

			// Nothing about the scale is in the args: a handler that wants the value reads the property,
			// which is authoritative and current even if another change landed while this was queued.
			this.DisplayScaleChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Gets how much room the monitor this window is currently on actually has, in device pixels: the
		/// screen minus what the OS permanently reserves on it (the mac menu bar and Dock, the Windows
		/// taskbar). <see cref="Vector2.Zero"/> until a platform host measures it, which is also what a
		/// headless host leaves it at.
		/// </summary>
		/// <remarks>
		/// The companion to <see cref="DisplayScale"/>, and reported by the same hosts at the same moments,
		/// because the two answer the same question for different applications: a window that moves to
		/// another display may find a different scale, a different amount of room, or both. Only
		/// <c>AggContext.DesktopSize</c> existed before, and that describes the PRIMARY monitor - so an
		/// application sizing itself against it on a second, smaller display sizes itself against a screen
		/// it is not on.
		/// <para>
		/// No change event: the consumers are size computations that already re-run when something else
		/// (the display scale, a text size preference) tells them to, and they read this then.
		/// </para>
		/// </remarks>
		public Vector2 DisplayUsableSize { get; private set; }

		/// <summary>
		/// Tells this window how big the usable area of the monitor it is on now is, in device pixels.
		/// Called by the platform hosts wherever they report <see cref="SetDisplayScale"/>.
		/// </summary>
		/// <remarks>
		/// A size that is not usable is ignored rather than stored: a window dragged off screen has no
		/// screen to measure, and a monitor hot-plug can be caught mid-transition reporting nothing. Keeping
		/// the last good measurement is strictly better than replacing it with "unknown", which sends the
		/// application back to guessing from the primary display - the very thing this exists to stop.
		/// </remarks>
		/// <param name="sizeInPixels">The usable screen area in device pixels; ignored unless both axes are finite and positive.</param>
		public void SetDisplayUsableSize(Vector2 sizeInPixels)
		{
			if (double.IsNaN(sizeInPixels.X) || double.IsInfinity(sizeInPixels.X) || sizeInPixels.X <= 0
				|| double.IsNaN(sizeInPixels.Y) || double.IsInfinity(sizeInPixels.Y) || sizeInPixels.Y <= 0)
			{
				return;
			}

			this.DisplayUsableSize = sizeInPixels;
		}

		public override void OnClosed(EventArgs e)
		{
			this.ToolTipManager.Dispose();

			_openWindows.Remove(this);

			base.OnClosed(e);

			// Invoke Close on our PlatformWindow and release our reference when complete
			systemWindowProvider?.CloseSystemWindow(this);
			this.PlatformWindow = null;
		}

		private static readonly List<SystemWindow> _openWindows = new List<SystemWindow>();

		public static IEnumerable<SystemWindow> AllOpenSystemWindows { get; } = _openWindows.Where(w => w.PlatformWindow != null);

		public SystemWindow(double width, double height)
			: base(width, height, SizeLimitsToSet.None)
		{
			// ToolTipManager construction only initializes fields; it is activated (event
			// subscription + UiThread interval) once the window is shown or first receives
			// mouse input, so no callbacks can observe a partially-constructed window.
			ToolTipManager = new ToolTipManager(this);

			// non-virtual initialization path; the virtual BackgroundColor setter must not be
			// dispatched while derived windows are still constructing
			SetBackgroundColorWithoutDispatch(new Color("#444444"));
		}

		public override void OnMinimumSizeChanged(EventArgs e)
		{
			if (PlatformWindow != null)
			{
				PlatformWindow.MinimumSize = this.MinimumSize;
			}
		}

		/// <summary>True while <see cref="SetBoundsFromPlatform"/> is assigning, so the size clamps stand down.</summary>
		private bool applyingPlatformBounds;

		/// <summary>
		/// True once a platform host has measured this window's drawing surface and said how big it is. Until
		/// then (a headless test, a window built before it is shown) the window sizes itself like any widget.
		/// </summary>
		private bool platformHasReportedSurfaceSize;

		/// <summary>
		/// Called by a platform host to state the measured size, in device pixels, of the surface this window
		/// draws into - the mac host's backing drawable, the WinForms host's client area.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This deliberately bypasses <see cref="GuiWidget.MinimumSize"/>, which assigning
		/// <see cref="GuiWidget.LocalBounds"/> would enforce. A minimum can only ever grow the widget tree, and
		/// the tree has nowhere to grow into: the surface is exactly this big. agg is y-up, so the overflow
		/// clips off the <em>top</em> of the window, which is where an application's toolbars live.
		/// </para>
		/// <para>
		/// That is not hypothetical. Drag the window from a Retina display to a standard one and its surface
		/// loses half its pixels in a single step, while <see cref="GuiWidget.MinimumSize"/> is still the value
		/// the application computed in device pixels for the display it just left - routinely larger than the
		/// whole new surface. The application lowers the minimum when it handles
		/// <see cref="DisplayScaleChanged"/>, but that runs from the idle queue, and lowering a minimum has
		/// never shrunk bounds back, so the clipped layout survived until the user resized the window by hand.
		/// </para>
		/// <para>
		/// Nothing is lost by standing the clamp down: the minimum a user can drag a window to is enforced by
		/// the native window itself (<c>setContentMinSize:</c> on the mac, <c>Form.MinimumSize</c> on Windows),
		/// which agg keeps up to date from <see cref="OnMinimumSizeChanged"/>. agg's own copy of the minimum
		/// only ever needed to size the layout, and a size the host measured beats a size the application
		/// predicted.
		/// </para>
		/// <para>
		/// Unlike <see cref="SetDisplayScale"/> above, this is UI-thread-only - the two fields it touches are
		/// neither volatile nor locked. That is all it needs: every host calls this from its own UI callback
		/// (the mac host's drawable-resize, the WinForms host's resize/paint), and the assignment below runs
		/// layout synchronously, which is a UI-thread-only operation regardless. The save/restore of the flag
		/// is for re-entrancy on that one thread, not for other threads: laying out can raise the window's
		/// MinimumSize, which reaches <c>Form.MinimumSize</c> and can come straight back in here.
		/// </para>
		/// </remarks>
		/// <param name="width">Surface width in device pixels.</param>
		/// <param name="height">Surface height in device pixels.</param>
		public void SetBoundsFromPlatform(double width, double height)
		{
			this.platformHasReportedSurfaceSize = true;

			bool wasApplyingPlatformBounds = this.applyingPlatformBounds;
			this.applyingPlatformBounds = true;
			try
			{
				this.LocalBounds = new RectangleDouble(0, 0, width, height);
			}
			finally
			{
				// Restored, not cleared: a nested call must not tell the outer one's assignment - still on the
				// stack below us - that the clamps are back on halfway through.
				this.applyingPlatformBounds = wasApplyingPlatformBounds;
			}
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Only the platform path is exempt - see <see cref="SetBoundsFromPlatform"/>. Every other assignment,
		/// application code included, keeps the ordinary widget contract.
		/// </remarks>
		protected override RectangleDouble ClampToSizeLimits(RectangleDouble value)
		{
			return this.applyingPlatformBounds ? value : base.ClampToSizeLimits(value);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Refused once a host has reported a surface size, which keeps the invariant that this window's bounds
		/// are the surface's size. Otherwise the application raising its minimum - which is exactly what it does
		/// from <see cref="DisplayScaleChanged"/>, after the surface has already shrunk - would inflate the
		/// layout back off the top of the surface. The new minimum still reaches the native window through
		/// <see cref="OnMinimumSizeChanged"/>, so it still stops the user dragging the window smaller, and the
		/// resize that follows arrives here as a surface size.
		/// </remarks>
		protected override void GrowBoundsToMinimumSize()
		{
			if (this.platformHasReportedSurfaceSize)
			{
				return;
			}

			base.GrowBoundsToMinimumSize();
		}

		private Vector2 lastMousePosition;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			lastMousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// Mouse input can only arrive after construction is complete, so this is a safe
			// activation point. Covers windows that receive events without ever going through
			// ShowAsSystemWindow (e.g. headless tests). No-op after the first call.
			ToolTipManager.Initialize();

			lastMousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);

			base.OnMouseMove(mouseEvent);

			SetToolTipText(mouseEvent);
		}

		private void SetToolTipText(MouseEventArgs mouseEvent)
		{
			var screenSpaceMouse = this.TransformToScreenSpace(lastMousePosition);

			GuiWidget lastChild = this;
			// look down our tree to find the first widget under the mouse
			var items = new Stack<GuiWidget>(new[] { this });
			while (items.Count > 0)
			{
				var item = items.Pop();

				foreach (var child in item.Children.Reverse())
				{
					var screenSpaceChildBounds = child.TransformToScreenSpace(child.LocalBounds);

					// Selectable rather than CanSelect on purpose. CanSelect also requires Enabled, which
					// would make the walk match mouse routing, but a disabled control is exactly the one
					// whose tooltip the user needs (MatterCAD's greyed Undo button says what it would undo).
					// This walk answers "what is drawn on top here", not "what would get the click".
					if (screenSpaceChildBounds.Contains(screenSpaceMouse)
						&& child.Visible
						&& child.Selectable)
					{
						items.Clear();
						items.Push(child);
						lastChild = child;
						break;
					}
				}
			}

			// Always report the hovered widget, even when it has no tooltip of its own. Reporting only
			// widgets that have tooltip text leaves the previously hovered widget armed, so its tooltip
			// can appear (and linger) over a widget that is now covering it - the tooltip manager uses
			// pure containment tests and cannot tell that the old widget is occluded.
			SetHoveredWidget(lastChild);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			lastMousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
			base.OnMouseUp(mouseEvent);
		}

		public override void BringToFront()
		{
			if (this == AllOpenSystemWindows.First())
			{
				PlatformWindow.Activate();
			}
			else
			{
				Parent?.BringToFront();
			}
		}

		public override Graphics2D NewGraphics2D()
		{
			return this.PlatformWindow.NewGraphics2D();
		}

		private static ISystemWindowProvider systemWindowProvider = null;

		// Guards lazy creation of systemWindowProvider so concurrent first-show calls
		// cannot create two providers.
		private static readonly object systemWindowProviderLock = new object();

		/// <summary>
		/// Resets the static systemWindowProvider to allow fresh initialization for tests
		/// </summary>
		public static void ResetSystemWindowProvider()
		{
			DebugLogger.EnableFilter("SystemWindow");
			DebugLogger.LogMessage("SystemWindow", $"ResetSystemWindowProvider called - Current provider: {systemWindowProvider?.GetType().Name ?? "null"}");

			lock (systemWindowProviderLock)
			{
				systemWindowProvider = null;
			}

			_openWindows.Clear();
		}

		/// <summary>
		/// The provider type name to build the platform window from: normally
		/// <see cref="AggContext.Config.ProviderTypes.SystemWindowProvider"/>, but overridden by the
		/// <c>AGG_WINDOW_PROVIDER</c> environment variable when it is set.
		/// <para>
		/// The override deliberately beats code that assigned the config value (several demos hard-code
		/// theirs), because its whole purpose is running an unmodified demo on a chosen host. It
		/// understands the short names <c>webgpu</c> (the WinForms host) and <c>mac</c> (the AppKit host),
		/// and passes anything else through as a fully qualified type name so an out-of-tree provider can be
		/// named too. Neither short name is normally needed - the per-OS default in
		/// <c>AggContext.Config.ProviderTypes</c> already resolves to the right one.
		/// </para>
		/// <para>
		/// <c>bitmap</c> and <c>d3d11</c> were the other two short names. Both backends are deleted -
		/// WebGPU is the only render path to screen - so they are still recognised here for exactly one
		/// reason: to fail with a message that says what happened, rather than with "not a 'Type, Assembly'
		/// name", which reads like a typo to whoever has the variable left over in a shell.
		/// </para>
		/// </summary>
		/// <exception cref="InvalidOperationException">The variable names a short form that does not exist.</exception>
		private static string ResolveSystemWindowProviderTypeName()
		{
			string requested = Environment.GetEnvironmentVariable("AGG_WINDOW_PROVIDER");
			if (string.IsNullOrWhiteSpace(requested))
			{
				return AggContext.Config.ProviderTypes.SystemWindowProvider;
			}

			switch (requested.Trim().ToLowerInvariant())
			{
				case "webgpu":
					return "MatterHackers.Agg.UI.WebGpuWinformsWindowProvider, agg_platform_win32";

				case "mac":
					return "MatterHackers.Agg.UI.WebGpuMacWindowProvider, agg_platform_mac";

				case "bitmap":
				case "d3d11":
					throw new InvalidOperationException(
						$"AGG_WINDOW_PROVIDER='{requested}' names a render backend that no longer exists."
						+ " WebGPU is the only window backend; use 'webgpu' or unset the variable.");

				default:
					// A comma means the caller wrote "Type, Assembly" themselves; anything else is a
					// misspelled short name, which is worth failing loudly rather than silently ignoring.
					if (requested.Contains(","))
					{
						return requested;
					}

					throw new InvalidOperationException(
						$"AGG_WINDOW_PROVIDER='{requested}' is not 'webgpu' or 'mac' and is not a 'Type, Assembly' name.");
			}
		}

		public void ShowAsSystemWindow()
		{
			DebugLogger.EnableFilter("SystemWindow");
			DebugLogger.LogMessage("SystemWindow", $"ShowAsSystemWindow called - Title: '{Title}', Width: {Width}, Height: {Height}");
			DebugLogger.LogMessage("SystemWindow", $"SystemWindow state - HasBeenClosed: {HasBeenClosed}, Visible: {Visible}");
			DebugLogger.LogMessage("SystemWindow", $"PlatformWindow is null: {PlatformWindow == null}");
			DebugLogger.LogMessage("SystemWindow", $"_openWindows count before add: {_openWindows.Count}");
			
			lock (systemWindowProviderLock)
			{
				// Lazy creation is done under the lock so concurrent first-show calls resolve
				// to a single provider. A provider pre-set by the platform layer is preserved.
				if (systemWindowProvider == null)
				{
					var providerTypeName = ResolveSystemWindowProviderTypeName();
					DebugLogger.LogMessage("SystemWindow", $"systemWindowProvider is null, creating from '{providerTypeName}'");
					systemWindowProvider = AggContext.CreateInstanceFrom<ISystemWindowProvider>(providerTypeName);

					if (systemWindowProvider == null)
					{
						throw new InvalidOperationException(
							$"Failed to create ISystemWindowProvider from type '{providerTypeName}'. " +
							"Ensure AggContext.Config.ProviderTypes.SystemWindowProvider is set to a valid type name.");
					}

					DebugLogger.LogMessage("SystemWindow", $"Created systemWindowProvider type: {systemWindowProvider.GetType().Name}");
				}
			}

			// The window is fully constructed and about to be shown - activate tooltip
			// tracking now (idempotent if already activated by earlier mouse input).
			ToolTipManager.Initialize();

			_openWindows.Add(this);

			// Create the backing IPlatformWindow object and set its AggSystemWindow property to this new SystemWindow
            systemWindowProvider.ShowSystemWindow(this);
			DebugLogger.LogMessage("SystemWindow", $"systemWindowProvider.ShowSystemWindow completed - PlatformWindow null: {PlatformWindow == null}");
		}

		public virtual bool Maximized { get; set; } = false;

		public Point2D InitialDesktopPosition { get; set; } = new Point2D(-1, -1);

		public Point2D DesktopPosition
		{
			get => PlatformWindow.DesktopPosition;
			set
			{
				Point2D position = value;

				if (PlatformWindow != null)
				{
					// Make sure the window is on screen (this logic should improve over time)
					position.x = Math.Max(0, position.x);
					position.y = Math.Max(0, position.y);

					// If it's mac make sure we are not completely under the menu bar.
					if (AggContext.OperatingSystem == OSType.Mac)
					{
						position.y = Math.Max(5, position.y);
					}

					PlatformWindow.DesktopPosition = position;
				}
				else
				{
					InitialDesktopPosition = position;
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether only one os window will be created and all system windows will share it.
		/// Make sure this is set prior to creating any SystemWindows (don't change at runtime).
		/// </summary>
		public static bool ShareSingleOsWindow { get; set; }

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		protected override void SetCursor(Cursors cursorToSet)
		{
			PlatformWindow?.SetCursor(cursorToSet);
		}

		public void SetHoveredWidget(GuiWidget widgetToShowToolTipFor)
		{
			ToolTipManager.SetHoveredWidget(widgetToShowToolTipFor);
		}

		public override void Invalidate(RectangleDouble rectToInvalidate)
		{
			PlatformWindow?.Invalidate(LocalBounds);
		}

		// TODO: This should become private... Callers should interact with SystemWindow proxies
		public IPlatformWindow PlatformWindow { get; set; }

		public override Keys ModifierKeys => PlatformWindow.ModifierKeys;

		/// <summary>
		/// Captures a screenshot of this window and saves it to the given file path.
		/// Delegates to the platform window implementation.
		/// </summary>
		public void CaptureScreenshot(string path)
		{
			PlatformWindow?.CaptureScreenshot(path);
		}
	}
}
