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
		/// request the WARP software rasterizer when hosted in a D3D11SystemWindow.
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
					var providerTypeName = AggContext.Config.ProviderTypes.SystemWindowProvider;
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
		/// Delegates to the platform window implementation (e.g. D3D11, OpenGL).
		/// </summary>
		public void CaptureScreenshot(string path)
		{
			PlatformWindow?.CaptureScreenshot(path);
		}
	}
}
