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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// Floats one <see cref="WindowWidget"/> per <see cref="DemoSpec"/> over the GUI demo's canvas, as agg-gui's
	/// app_builder.rs does: windows are tiled by specs.rs's tile_rect, the open-by-default ones open at start,
	/// and the title bar's close button closes a window.
	/// </summary>
	/// <remarks>
	/// A window is only built the first time it is opened. Closing takes it out of the canvas but keeps it, so
	/// reopening brings it back where the user left it (agg-gui keeps its windows and toggles visibility).
	/// The canvas's child order is the z-order: the last child draws on top and is hit first. Raising is a
	/// <see cref="GuiWidget.BringToFront"/> reorder, which WindowWidget also does itself when it takes focus,
	/// so <see cref="ZOrder"/> is read from the canvas rather than kept in a second list that could disagree.
	/// </remarks>
	public class DemoWindowHost
	{
		// The sizes and rectangles below, and each DemoSpec's default size, are design units (agg-gui's logical
		// pixels); they are multiplied by GuiWidget.DeviceScale where a window is placed, so at 2x the windows
		// open twice as many device pixels big, the size their doubled text needs.

		/// <summary>app_builder.rs tiles for this canvas height until the canvas has been laid out.</summary>
		public const double DefaultCanvasHeight = 720;

		/// <summary>The width tiled for until the canvas has been laid out: wide enough for specs.rs's four columns.</summary>
		public const double DefaultCanvasWidth = TileOrigin + TileColumns * (GuiDemoSpecs.DefaultWindowWidth + TileGap);

		// specs.rs's tiling constants (WIN_COLS etc.); WIN_ORIGIN_Y is measured down from the canvas top.
		private const int TileColumns = 4;

		private const double TileGap = 20;

		private const double TileOrigin = 20;

		/// <summary>WindowWidget's resize grab border (its private grabWidth), in design units. The visible
		/// window is inset by this much, so the widget is grown by it to make the visible card the spec's size.</summary>
		internal const double GrabBorder = 5;

		/// <summary>app_builder.rs's default About rectangle (x, y up from the bottom, width, height).</summary>
		private static readonly RectangleDouble AboutRect = new RectangleDouble(80, 80, 80 + 360, 80 + 420);

		/// <summary>app_builder.rs's INSPECTOR_DEFAULT_BOUNDS, kept on a narrower canvas.</summary>
		private static readonly RectangleDouble InspectorRect = new RectangleDouble(960, 60, 960 + 320, 60 + 520);

		private readonly GuiWidget canvas;

		private readonly DemoTheme demoTheme;

		private readonly Dictionary<DemoSpec, WindowWidget> windows = new Dictionary<DemoSpec, WindowWidget>();

		/// <summary>Windows placed before the canvas had a height; they are tiled again once it has one.</summary>
		private readonly HashSet<DemoSpec> placedBeforeLayout = new HashSet<DemoSpec>();

		/// <summary>Windows still where the host put them (their tile or restored rectangle); they are placed
		/// again whenever the canvas changes size. A user's drag takes a window out.</summary>
		private readonly HashSet<DemoSpec> followsCanvas = new HashSet<DemoSpec>();

		/// <summary>Rectangles restored from a previous run; they replace the tile until Organize.</summary>
		private readonly Dictionary<DemoSpec, RectangleDouble> restoredRects = new Dictionary<DemoSpec, RectangleDouble>();

		/// <summary>What each content-sized window has that its client area does not (title bar, the line under
		/// it, the grab border). Measured once, while the window is settled: mid-layout the client area can
		/// still have its old size, and a fit read from it lands wrong.</summary>
		private readonly Dictionary<DemoSpec, Vector2> windowChrome = new Dictionary<DemoSpec, Vector2>();

		/// <param name="canvas">The widget the windows float over. Closing it closes every window built.</param>
		/// <param name="demoTheme">The theme the windows are drawn with and follow; null for agg-gui's default
		/// (System, which resolves to dark).</param>
		public DemoWindowHost(GuiWidget canvas, DemoTheme demoTheme = null)
		{
			this.canvas = canvas;
			this.demoTheme = demoTheme ?? new DemoTheme();

			// before any window, so its guide overlay is the canvas's first child and the windows keep the rest
			this.Snap = new SnapCoordinator(canvas);

			foreach (DemoSpec spec in GuiDemoSpecs.All.Where(s => s.OpenByDefault))
			{
				this.SetOpen(spec, true);
			}

			this.canvas.BoundsChanged += this.Canvas_BoundsChanged;
			this.demoTheme.ThemeChanged += this.DemoTheme_ThemeChanged;
			this.canvas.Closed += this.Canvas_Closed;
		}

		/// <summary>Snaps the windows to each other as they are dragged or resized (View > Window Snapping).</summary>
		public SnapCoordinator Snap { get; }

		/// <summary>A window was opened or closed; <see cref="IsOpen"/> says which.</summary>
		public event EventHandler<DemoSpec> OpenChanged;

		/// <summary>A window was opened, closed, raised, moved or resized: anything saved state records.</summary>
		public event EventHandler LayoutChanged;

		/// <summary>The open windows' specs from the back to the front.</summary>
		public IReadOnlyList<DemoSpec> ZOrder => this.canvas.Children
			.Select(child => this.windows.FirstOrDefault(pair => pair.Value == child).Key)
			.Where(spec => spec != null)
			.ToList();

		public bool IsOpen(DemoSpec spec)
		{
			return this.windows.TryGetValue(spec, out WindowWidget window) && window.Parent == this.canvas;
		}

		/// <summary>Opens (on top of the others) or closes <paramref name="spec"/>'s window, building it the
		/// first time it opens. Fires <see cref="OpenChanged"/> only when the state actually changes.</summary>
		public void SetOpen(DemoSpec spec, bool open)
		{
			if (open == this.IsOpen(spec))
			{
				return;
			}

			if (open)
			{
				if (!this.windows.TryGetValue(spec, out WindowWidget window))
				{
					window = this.CreateWindow(spec);
					this.windows.Add(spec, window);
				}

				// A window that was closed is re-added on purpose, to keep where the user left it.
				window.ClearRemovedFlag();
				window.DoubleBuffer = true;
				this.canvas.AddChild(window);
			}
			else
			{
				WindowWidget window = this.windows[spec];
				this.canvas.RemoveChild(window);

				// A removed widget keeps its GPU texture (only Close releases it) and a closed demo window is
				// kept to be reopened, so its cache is dropped here; reopening repaints it once.
				window.DoubleBuffer = false;
			}

			this.OpenChanged?.Invoke(this, spec);
			this.LayoutChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Brings <paramref name="spec"/>'s window in front of the others; does nothing if it is closed.</summary>
		public void Raise(DemoSpec spec)
		{
			if (this.IsOpen(spec))
			{
				this.windows[spec].BringToFront();
				this.LayoutChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// The visible rectangle of <paramref name="spec"/>'s window in the canvas (y up), or null when it has
		/// never been built and has no restored rectangle.
		/// </summary>
		public RectangleDouble? GetVisibleRect(DemoSpec spec)
		{
			bool hasRestored = this.restoredRects.TryGetValue(spec, out RectangleDouble restored);
			if (!this.windows.TryGetValue(spec, out WindowWidget window))
			{
				return hasRestored ? restored : null;
			}

			if (hasRestored && this.placedBeforeLayout.Contains(spec))
			{
				// Not placed for real until the canvas has a size: the restored rectangle is what the user will see.
				return restored;
			}

			// A maximized window saves the rectangle it restores to, as agg-gui's pre_maximize_bounds.
			double grab = GrabBorder * GuiWidget.DeviceScale;
			RectangleDouble bounds = window.RestoreBounds;
			return new RectangleDouble(bounds.Left + grab, bounds.Bottom + grab, bounds.Right - grab, bounds.Top - grab);
		}

		/// <summary>Whether <paramref name="spec"/>'s window is built and maximized to fill the canvas.</summary>
		public bool IsMaximized(DemoSpec spec) => this.GetWindow(spec)?.Maximized == true;

		/// <summary>Maximizes or restores <paramref name="spec"/>'s window; does nothing if it has never been built.</summary>
		public void SetMaximized(DemoSpec spec, bool maximized)
		{
			if (this.GetWindow(spec) is WindowWidget window)
			{
				window.Maximized = maximized;
			}
		}

		/// <summary>
		/// Puts <paramref name="spec"/>'s window at <paramref name="visible"/> (a rectangle saved by a previous run)
		/// instead of its tile, now if it is built and otherwise when it is. The rectangle is clamped into the
		/// canvas once the canvas has a size, so a window saved on a bigger screen cannot be lost off the edge.
		/// </summary>
		public void RestoreRect(DemoSpec spec, RectangleDouble visible)
		{
			this.restoredRects[spec] = visible;
			if (this.windows.TryGetValue(spec, out WindowWidget window))
			{
				this.Place(spec, window);
			}
		}

		/// <summary>
		/// <paramref name="rect"/> moved (and if need be shrunk) to lie inside a canvas
		/// <paramref name="canvasWidth"/> x <paramref name="canvasHeight"/>.
		/// </summary>
		public static RectangleDouble ClampToCanvas(RectangleDouble rect, double canvasWidth, double canvasHeight)
		{
			double width = Math.Min(rect.Width, canvasWidth);
			double height = Math.Min(rect.Height, canvasHeight);
			double x = Math.Max(0, Math.Min(rect.Left, canvasWidth - width));
			double y = Math.Max(0, Math.Min(rect.Bottom, canvasHeight - height));
			return new RectangleDouble(x, y, x + width, y + height);
		}

		/// <summary>The window built for <paramref name="spec"/>, or null if it has never been opened.</summary>
		public WindowWidget GetWindow(DemoSpec spec)
		{
			return this.windows.TryGetValue(spec, out WindowWidget window) ? window : null;
		}

		/// <summary>
		/// The sidebar's "Organize windows": puts every window back on its specs.rs tile, as app_builder.rs's
		/// on_organize resets each window's rectangle to tile_rect. Windows never opened are already tiled when
		/// they are built; About keeps its place, as it does in agg-gui.
		/// </summary>
		public void Organize()
		{
			this.restoredRects.Clear();
			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				if (this.windows.TryGetValue(spec, out WindowWidget window))
				{
					this.Place(spec, window);
				}
			}

			this.canvas.Invalidate();
		}

		/// <summary>
		/// specs.rs's tile_rect: the visible window's rectangle (y up) for the spec at <paramref name="index"/>
		/// in a canvas <paramref name="canvasWidth"/> x <paramref name="canvasHeight"/>. Four columns of WIN_W x
		/// WIN_H cells from the top left; a row that would run off the bottom is re-anchored near the top with a
		/// small stagger.
		/// </summary>
		/// <remarks>
		/// Unlike specs.rs, the columns wrap to the canvas width (a 1200-wide browser page leaves the canvas about
		/// 780 wide, room for two), a window wider than its cell is pulled left to fit, and one bigger than the
		/// canvas (SVG Test is 960 wide) is shrunk to it, as <see cref="ClampToCanvas"/> does: a window that
		/// opens off-screen looks broken to someone who does not know it is there. On a canvas wide enough for
		/// four columns this is specs.rs's tiling unchanged.
		/// </remarks>
		public static RectangleDouble TileRect(int index, double canvasWidth, double canvasHeight, double width, double height)
		{
			// The arguments are device pixels; specs.rs's cell, gap, origin and stagger are design units.
			double scale = GuiWidget.DeviceScale;
			double gap = TileGap * scale;
			double origin = TileOrigin * scale;
			double edge = 4 * scale;
			width = Math.Min(width, canvasWidth);
			height = Math.Min(height, canvasHeight);
			double pitch = GuiDemoSpecs.DefaultWindowWidth * scale + gap;
			int columns = Math.Clamp((int)((canvasWidth - origin + gap) / pitch), 1, TileColumns);
			int column = index % columns;
			int row = index / columns;
			double x = Math.Max(0, Math.Min(origin + column * pitch, canvasWidth - width));
			double yDown = origin + row * (GuiDemoSpecs.DefaultWindowHeight * scale + gap);
			double y = canvasHeight - yDown - height;
			if (y < edge)
			{
				double topY = Math.Max(canvasHeight - height - origin, edge);
				double stagger = index * 24.0 % 200.0 * scale;
				y = Math.Max(topY - stagger, edge);
			}

			return new RectangleDouble(x, y, x + width, y + height);
		}

		private WindowWidget CreateWindow(DemoSpec spec)
		{
			var window = new WindowWidget(this.demoTheme.Theme, new RectangleDouble(0, 0, spec.DefaultWidth * GuiWidget.DeviceScale, spec.DefaultHeight * GuiWidget.DeviceScale))
			{
				Name = spec.Title + " Window",
				CornerRadius = 8,

				// agg-gui's windows maximize from a title bar button or a double-click on the bar.
				Maximizable = true,

				// agg-gui's shadow (blur 14, offset 2, 6) is wider than WindowWidget's grab border, which is all
				// the room its shadow has; this keeps the same direction within that border.
				ShadowBlur = 3.5,
				ShadowOffset = new Vector2(0.5, -1.5),

				// Each window caches its pixels (a GPU texture on a GPU surface), so an unchanged window costs a
				// blit rather than a repaint while others animate. SetOpen turns it off while the window is closed.
				DoubleBuffer = true,
			};
			window.AddTitleBar(spec.Title, () => this.SetOpen(spec, false));
			AddTitleIcon(spec, window);
			this.AddContent(spec, window);
			this.ApplyTheme(window);

			// Pressing anywhere on a window raises it, not only on a control that takes focus (the title bar
			// does not). MouseDown fires even when a child handles the press; BringToFront keeps the capture.
			window.MouseDown += (s, e) => this.Raise(spec);
			this.Snap.Attach(window);
			window.TitleBar.DragMoved += (s, e) => this.StopFollowingCanvas(spec);
			foreach (GrabControl grab in window.Children.OfType<GrabControl>())
			{
				grab.DragMoved += (s, e) => this.StopFollowingCanvas(spec);
			}

			window.PositionChanged += (s, e) => this.LayoutChanged?.Invoke(this, EventArgs.Empty);
			window.SizeChanged += (s, e) => this.LayoutChanged?.Invoke(this, EventArgs.Empty);
			window.MaximizedChanged += (s, e) => this.LayoutChanged?.Invoke(this, EventArgs.Empty);

			this.Place(spec, window);
			return window;
		}

		private void Place(DemoSpec spec, WindowWidget window)
		{
			bool laidOut = this.canvas.Height > 0;
			if (!laidOut)
			{
				this.placedBeforeLayout.Add(spec);
			}

			this.followsCanvas.Add(spec);

			double scale = GuiWidget.DeviceScale;
			RectangleDouble visible;
			if (this.restoredRects.TryGetValue(spec, out RectangleDouble restored))
			{
				visible = laidOut ? ClampToCanvas(restored, this.canvas.Width, this.canvas.Height) : restored;
			}
			else
			{
				visible = spec == GuiDemoSpecs.About
					? Scaled(AboutRect, scale)
					: spec == GuiDemoSpecs.Inspector
					? (laidOut ? ClampToCanvas(Scaled(InspectorRect, scale), this.canvas.Width, this.canvas.Height) : Scaled(InspectorRect, scale))
					: TileRect(
						IndexOf(spec),
						laidOut ? this.canvas.Width : DefaultCanvasWidth * scale,
						laidOut ? this.canvas.Height : DefaultCanvasHeight * scale,
						spec.DefaultWidth * scale,
						spec.DefaultHeight * scale);
			}

			double grab = GrabBorder * scale;
			window.Position = new Vector2(visible.Left - grab, visible.Bottom - grab);
			window.Size = new Vector2(visible.Width + grab * 2, visible.Height + grab * 2);
			if (spec.AutoSize || spec.FitHeightToContent)
			{
				this.FitToContent(spec, window);
			}
		}

		/// <summary>
		/// Builds <paramref name="spec"/>'s content into the window's client area, applying the spec's window
		/// behaviour: a scroll area around it, and for a window sized by its content, anchors that let the
		/// content find its natural size and a handler that keeps the window fitted to it.
		/// </summary>
		private void AddContent(DemoSpec spec, WindowWidget window)
		{
			GuiWidget content = GuiDemoSpecs.CreateContent(spec, this.demoTheme);
			if (spec.VerticalScroll)
			{
				var scroll = new ScrollableWidget(autoScroll: true)
				{
					Name = spec.Title + " Scroll",
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
				};
				scroll.ScrollArea.HAnchor = HAnchor.Stretch;
				content.HAnchor = HAnchor.Stretch;
				content.VAnchor = VAnchor.Fit | VAnchor.Top;
				scroll.AddChild(content);
				window.ClientArea.AddChild(scroll);
				(content as IHostAwareDemoContent)?.AttachHost(window);
				return;
			}

			this.windowChrome[spec] = window.Size - window.ClientArea.Size;
			window.ClientArea.AddChild(content);
			(content as IHostAwareDemoContent)?.AttachHost(window);

			// agg-gui's auto-sized window has no resize handles: its size is its content's.
			window.Resizable = spec.Resizable && !spec.AutoSize;
			if (!spec.AutoSize
				&& !spec.FitHeightToContent)
			{
				return;
			}

			// Content with its own scroll area stays stretched to the window; what sizes itself is inside it.
			if (!(content is IScrollFittedDemoContent))
			{
				content.HAnchor = spec.AutoSize ? HAnchor.Fit | HAnchor.Left : HAnchor.Stretch;
				content.VAnchor = VAnchor.Fit | VAnchor.Top;
			}

			GuiWidget measured = FittedContent(window);

			// Re-entrant when the fit itself re-lays the content out; the inner call would only repeat the outer.
			bool fitting = false;
			measured.SizeChanged += (s, e) =>
			{
				if (!fitting)
				{
					fitting = true;
					try
					{
						this.FitToContent(spec, window);
					}
					finally
					{
						fitting = false;
					}
				}
			};
		}

		/// <summary>
		/// Sizes a content-sized window to its content, keeping its top edge where it is: both axes for
		/// <see cref="DemoSpec.AutoSize"/>, and for <see cref="DemoSpec.FitHeightToContent"/> the height, which
		/// also becomes the resize floor and ceiling so the user's drags only change the width.
		/// </summary>
		private void FitToContent(DemoSpec spec, WindowWidget window)
		{
			GuiWidget content = FittedContent(window);
			if (content == null)
			{
				return;
			}

			Vector2 chrome = this.windowChrome[spec];
			double height = content.Height + content.Margin.Height + chrome.Y;
			double top = window.Position.Y + window.Height;
			if (spec.AutoSize)
			{
				window.MinimumSize = Vector2.Zero;
				window.Size = new Vector2(content.Width + content.Margin.Width + chrome.X, height);
			}
			else
			{
				window.MinimumSize = new Vector2(window.MinimumSize.X, height);
				window.MaximumSize = new Vector2(window.MaximumSize.X, height);
				window.Height = height;
			}

			window.Position = new Vector2(window.Position.X, top - window.Height);

			// Content wider than the tile it was placed on (Frame) grows right; pull it back onto the canvas, as
			// TileRect does for a spec wider than its cell.
			if (spec.AutoSize
				&& this.canvas.Width > 0)
			{
				double grab = GrabBorder * GuiWidget.DeviceScale;
				double visibleWidth = window.Width - grab * 2;
				double left = Math.Max(0, Math.Min(window.Position.X + grab, this.canvas.Width - visibleWidth));
				window.Position = new Vector2(left - grab, window.Position.Y);
			}
		}

		/// <summary>The widget a content-sized window is fitted to: its content, or for content with its own
		/// scroll area (<see cref="IScrollFittedDemoContent"/>) the self-sizing widget inside that.</summary>
		private static GuiWidget FittedContent(WindowWidget window)
		{
			GuiWidget content = window.ClientArea.Children.FirstOrDefault();
			return (content as IScrollFittedDemoContent)?.FittedContent ?? content;
		}

		/// <summary><paramref name="rect"/> (design units) in device pixels.</summary>
		private static RectangleDouble Scaled(RectangleDouble rect, double scale)
			=> new RectangleDouble(rect.Left * scale, rect.Bottom * scale, rect.Right * scale, rect.Top * scale);

		private static int IndexOf(DemoSpec spec)
		{
			for (int i = 0; i < GuiDemoSpecs.All.Count; i++)
			{
				if (GuiDemoSpecs.All[i] == spec)
				{
					return i;
				}
			}

			throw new ArgumentException($"'{spec.Title}' is not one of the GUI demo's specs.", nameof(spec));
		}

		/// <summary>agg-gui titles each window with its icon glyph before the text; the glyph goes in front of
		/// the title text, in the same row. ApplyTheme colours it with the text.</summary>
		private static void AddTitleIcon(DemoSpec spec, WindowWidget window)
		{
			if (string.IsNullOrEmpty(spec.Icon))
			{
				return;
			}

			TextWidget titleText = window.TitleBar.Descendants<TextWidget>().First(text => text.Text == spec.Title);
			GuiWidget row = titleText.Parent;
			row.AddChild(
				new IconGlyphWidget(spec.Icon, titleText.TextColor)
				{
					Name = spec.Title + " Title Icon",
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(0, 0, 4, 0),
				},
				row.Children.IndexOf(titleText));
		}

		/// <summary>The window chrome copies its colours when it is built, so each window is recoloured from
		/// the palette here, including the title text. The close button's glyph is left as built.</summary>
		private void ApplyTheme(WindowWidget window)
		{
			DemoPalette palette = this.demoTheme.Palette;
			window.BackgroundColor = palette.WindowFill;
			window.TitleBarColor = palette.WindowTitleFill;
			window.ShadowColor = palette.WindowShadow;
			window.WindowBorderColor = palette.WindowStroke;
			foreach (TextWidget title in window.TitleBar.Descendants<TextWidget>())
			{
				title.TextColor = palette.TextColor;
			}

			foreach (IconGlyphWidget icon in window.TitleBar.Descendants<IconGlyphWidget>())
			{
				icon.Color = palette.TextColor;
			}

			window.Invalidate();
		}

		private void DemoTheme_ThemeChanged(object sender, EventArgs e)
		{
			foreach (WindowWidget window in this.windows.Values)
			{
				this.ApplyTheme(window);
			}
		}

		/// <summary>The page is gone: stop following the theme (it outlives the page), and close the windows
		/// that are not on the canvas - the canvas closed the ones that were - so their content lets go of the
		/// theme and their GPU memory is freed.</summary>
		private void Canvas_Closed(object sender, EventArgs e)
		{
			this.demoTheme.ThemeChanged -= this.DemoTheme_ThemeChanged;
			this.canvas.BoundsChanged -= this.Canvas_BoundsChanged;
			foreach (WindowWidget window in this.windows.Values.Where(w => !w.HasBeenClosed))
			{
				window.Close();
			}
		}

		/// <summary>
		/// The canvas changed size: every window the user has not moved or resized is placed again for the new
		/// size, so it hangs from the top of the canvas as agg-gui's do.
		/// </summary>
		/// <remarks>
		/// A head's first layout is not necessarily at its final size: the browser builds the app in a
		/// SystemWindow of its constructor size and only then learns the canvas's backing size (at
		/// devicePixelRatio 2 the page is first laid out at 1200 x 800 device pixels, then about 2800 x 1800).
		/// Placing only on the first layout left the windows tiled, and shrunk, for the small canvas, and agg
		/// being y-up they then sat at the bottom of the big one.
		/// </remarks>
		private void Canvas_BoundsChanged(object sender, EventArgs e)
		{
			if (this.canvas.Height <= 0)
			{
				return;
			}

			this.placedBeforeLayout.Clear();
			foreach (DemoSpec spec in this.followsCanvas.ToList())
			{
				WindowWidget window = this.windows[spec];
				if (!window.Maximized)
				{
					this.Place(spec, window);
				}
			}
		}

		/// <summary>The user dragged <paramref name="spec"/>'s window (moved or resized it): it stays where they
		/// put it from now on, whatever the canvas does.</summary>
		private void StopFollowingCanvas(DemoSpec spec) => this.followsCanvas.Remove(spec);
	}
}
