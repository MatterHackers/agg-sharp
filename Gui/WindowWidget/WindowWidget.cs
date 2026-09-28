using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2026 Lars Brubaker
//                  larsbrubaker@gmail.com
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.UI
{
    public class WindowWidget : GuiWidget
	{
		private int grabWidth = 5;

		private double deviceGrabWidth => grabWidth * DeviceScale;

        private readonly ThemeConfig theme;
        private readonly RoundedPanel windowBackground;

        /// <summary>
        /// The right hand end of the title bar, holding the close button and anything
        /// <see cref="AddTitleBarButton"/> has put beside it. Null until <see cref="AddTitleBar"/> runs.
        /// </summary>
        private FlowLayoutWidget titleBarButtons;

        private GuiWidget closeButton;

		private readonly List<GrabControl> grabControls = new List<GrabControl>();

		private GuiWidget titleLine;

		private TextWidget titleText;

		private CollapseChevron collapseButton;

		private string title = "";

		private bool resizable = true;

		private bool collapsible;

		private bool collapsed;

		private bool autoSize;

		private bool applyingAutoSize;

		private double expandedHeight;

		private Vector2 expandedMinimumSize;

		private bool maximizable;

		private readonly WindowMaximizer maximizer;

		private MaximizeButton maximizeButton;

		public WindowWidget(ThemeConfig theme, RectangleDouble inBounds)
			: this(theme, new GuiWidget(inBounds.Width, inBounds.Height, SizeLimitsToSet.None)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Position = new Vector2(inBounds.Left, inBounds.Bottom),
				Size = new Vector2(inBounds.Width, inBounds.Height)
			})
		{
		}

        /// <summary>
        /// Raises the window as soon as anything inside it takes focus, so clicking a control on a window that
        /// is behind another one brings it forward.
        /// </summary>
        /// <remarks>
        /// Deferred to the next idle because focus changes arrive part way through the parent's own walk of its
        /// children, and this reorders that list. That idle lands between the press that gave the window focus
        /// and the release that completes the click, which is why the raise has to be a
        /// <see cref="GuiWidget.BringToFront"/> reorder rather than a remove and re-add - a remove clears the
        /// mouse capture the press just set, and the click would be swallowed. See BringToFront's remarks.
        /// </remarks>
        public override void OnContainsFocusChanged(FocusChangedArgs e)
        {
            base.OnContainsFocusChanged(e);

			UiThread.RunOnIdle(() =>
			{
				if (ContainsFocus)
				{
					this.BringToFront();
				}
			});
        }

        public WindowWidget(ThemeConfig theme, GuiWidget clientArea)
		{
			this.theme = theme;
			maximizer = new WindowMaximizer(this, () => deviceGrabWidth);
            
			windowBackground = new RoundedPanel()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(grabWidth),
				BackgroundRadius = 3 * GuiWidget.DeviceScale,
			};

			AddChild(windowBackground);

			TitleBar = new TitleBarWidget(this)
			{
				Size = new Vector2(0, 30 * GuiWidget.DeviceScale),
				HAnchor = HAnchor.Stretch,
			};
			windowBackground.AddChild(TitleBar);

            titleLine = new HorizontalLine(theme.PrimaryAccentColor);
            windowBackground.AddChild(titleLine);

            MinimumSize = new Vector2(deviceGrabWidth * 8, deviceGrabWidth * 4 + TitleBar.Height * 2);
			WindowBorder = 1;
			WindowBorderColor = theme.PrimaryAccentColor;

			Position = clientArea.Position - new Vector2(deviceGrabWidth, deviceGrabWidth);
			Size = clientArea.Size + new Vector2(deviceGrabWidth * 2, deviceGrabWidth * 2 + TitleBar.Height);

			AddGrabControls();

			ClientArea = clientArea;

			windowBackground.AddChild(ClientArea);
			ClientArea.Layout += (s, e) => ApplyAutoSize();
		}

		public double WindowBorder { get => windowBackground.BackgroundOutlineWidth; set => windowBackground.BackgroundOutlineWidth = value; }

		private double cornerRadius;

		/// <summary>
		/// Rounds the corners of the visible window (background, border and title bar colour) by this many design
		/// units. 0, the default, keeps the classic square window.
		/// </summary>
		/// <remarks>
		/// Like agg-gui's rounded layer clip, whatever the title bar and client area paint is cut to the rounded
		/// corners: a rounded window double-buffers its panel (the part inside the grab border) and composites
		/// that buffer through a rounded, anti-aliased mask (<see cref="RoundedClipCoverage"/>). The shadow is
		/// drawn by the window itself, outside the panel's buffer, so the mask never cuts it.
		/// </remarks>
		public double CornerRadius
		{
			get => cornerRadius;
			set
			{
				cornerRadius = value;
				windowBackground.BackgroundRadius = value > 0 ? value * DeviceScale : 3 * DeviceScale;
				windowBackground.BackbufferCornerRadius = Math.Max(0, value) * DeviceScale;

				// The clip only applies when the panel is drawn through its buffer, and a panel under a zoom
				// outside 0.95..1.05 (GuiWidget.DrawChild) is drawn straight onto its parent instead. So the
				// sides and bottom stay padded by radius * (1 - 1/sqrt 2), which puts the client area's square
				// corners on the arc either way. Always padded, not only when unbuffered, so zooming never
				// changes the layout. The title bar is not padded; the clip rounds whatever it paints.
				var inset = Math.Max(0, value) * (1 - 1 / Math.Sqrt(2));
				windowBackground.Padding = new BorderDouble(inset, inset, inset, 0);

				// A square window keeps drawing straight onto its parent, exactly as it always has.
				windowBackground.DoubleBuffer = value > 0;
				windowBackground.Invalidate();
			}
		}

		/// <summary>
		/// The visible window inside the grab border, which carries the rounded clip its backbuffer is
		/// composited through (see <see cref="CornerRadius"/>).
		/// </summary>
		private class RoundedPanel : FlowLayoutWidget, IRoundedBackbuffer
		{
			public RoundedPanel()
				: base(FlowDirection.TopToBottom)
			{
			}

			public double BackbufferCornerRadius { get; set; }
		}

		/// <summary>
		/// The colour of the soft drop shadow drawn around the window; its alpha is how dark the shadow is at its
		/// darkest. Transparent, the default, draws the classic thin edge shade instead.
		/// </summary>
		/// <remarks>
		/// The shadow is drawn in the resize grab border that surrounds the visible window, so it never changes
		/// where the window can be grabbed. That border is only a few design units wide, so
		/// <see cref="ShadowBlur"/> plus the length of <see cref="ShadowOffset"/> should stay within it; anything
		/// past it is clipped at the window's bounds.
		/// </remarks>
		public Color ShadowColor { get; set; } = Color.Transparent;

		/// <summary>How far the shadow is shifted from the window, in design units (negative Y is down).</summary>
		public Vector2 ShadowOffset { get; set; }

		/// <summary>How far the shadow fades out past the window's edge, in design units.</summary>
		public double ShadowBlur { get; set; }

		/// <summary>
		/// A colour painted behind the title bar, following the window's rounded top corners. Transparent, the
		/// default, lets the window's background show through as before.
		/// </summary>
		public Color TitleBarColor { get; set; } = Color.Transparent;

		private bool IsStyled => cornerRadius > 0 || ShadowColor.Alpha0To255 > 0 || TitleBarColor.Alpha0To255 > 0;

		public Color WindowBorderColor { get => windowBackground.BorderColor; set => windowBackground.BorderColor = value; }

		public GuiWidget ClientArea { get; }

		public TitleBarWidget TitleBar { get; private set; }

        public void AddTitleBar(string title, Action closeAction)
		{
			// The buttons live in their own flow rather than the close button being the toolbar's right anchor
			// item directly: Toolbar.AddChild redirects into its stretched ActionArea, so with the button
			// anchored on its own there is no way to get anything to sit beside it. See AddTitleBarButton.
			titleBarButtons = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				// Fit as well as Right: without it the flow keeps a width of zero and its buttons lay themselves
				// out to the right of that empty rectangle - past the end of the title bar, where the drawing
				// clip cuts them off entirely and nothing can be clicked
				HAnchor = HAnchor.Right | HAnchor.Fit,
				VAnchor = VAnchor.Fit | VAnchor.Center,
			};

			if (closeAction != null)
			{
				closeButton = theme.CreateSmallResetButton();

				// No HAnchor.Right on the button itself any more - the flow it now sits in carries that, and a
				// left to right flow rejects a Right anchored child outright (LayoutEngineFlow).
				closeButton.ToolTipText = "Close".Localize();
				closeButton.Click += (s, e) =>
				{
					closeAction?.Invoke();
				};

				titleBarButtons.AddChild(closeButton);
			}

			// Left of the close button, as agg-gui draws it; built always so turning Maximizable on later shows it.
			maximizeButton = new MaximizeButton(this, () => titleText.TextColor)
			{
				Name = "Window Maximize Button",
				VAnchor = VAnchor.Center,
				Visible = maximizable,
			};
			titleBarButtons.AddChild(maximizeButton, 0);

            var titleBarRow = new Toolbar(theme.TabbarPadding, titleBarButtons)
            {
                HAnchor = HAnchor.Stretch,
                VAnchor = VAnchor.Fit | VAnchor.Center,
            };

            // mh.png is MatterCAD's icon and ships in its StaticData, not agg-sharp's; any other app has none, and
            // loading a missing icon throws in a debug build. Such an app gets a title bar without the icon.
            if (StaticData.Instance.FileExists(System.IO.Path.Combine("Icons", "mh.png")))
            {
                titleBarRow.AddChild(new ImageWidget(StaticData.Instance.LoadIcon("mh.png", 16, 16).GrayToColor(theme.TextColor))
                {
                    Margin = new BorderDouble(4, 0, 6, 0),
                    VAnchor = VAnchor.Center
                });
            }
            else
            {
                titleBarRow.AddChild(new GuiWidget(8, 1));
            }

            // Drawn in the title's colour, so a host that recolours the title recolours the chevron with it.
            collapseButton = new CollapseChevron(this, () => titleText.TextColor)
            {
                Name = "Window Collapse Button",
                VAnchor = VAnchor.Center,
                Visible = collapsible,
            };
            titleBarRow.ActionArea.AddChild(collapseButton);

            this.title = title ?? "";
            titleText = new TextWidget(this.title, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
            {
                VAnchor = VAnchor.Center,
            };
            titleBarRow.ActionArea.AddChild(titleText);

            TitleBar.AddChild(titleBarRow);
        }

		/// <summary>
		/// The text shown in the title bar. Setting it before <see cref="AddTitleBar"/> is overwritten by the
		/// title given there.
		/// </summary>
		public string Title
		{
			get => title;
			set
			{
				title = value ?? "";
				if (titleText != null)
				{
					titleText.Text = title;
				}
			}
		}

		/// <summary>
		/// Whether the edges and corners can be dragged to resize the window. True, the default. The handles
		/// are also off while the window is <see cref="Collapsed"/> or <see cref="AutoSize"/>d, as in egui.
		/// </summary>
		public bool Resizable
		{
			get => resizable;
			set
			{
				resizable = value;
				UpdateGrabControls();
			}
		}

		/// <summary>
		/// Whether the title bar offers a chevron that folds the window up to its title bar. False, the default.
		/// Turning it off unfolds a folded window, since nothing would be left to unfold it with.
		/// </summary>
		public bool Collapsible
		{
			get => collapsible;
			set
			{
				collapsible = value;
				if (collapseButton != null)
				{
					collapseButton.Visible = value;
				}

				if (!value)
				{
					Collapsed = false;
				}
			}
		}

		/// <summary>
		/// Folds the window up to its title bar (true) or back out to the height it had (false). The top edge
		/// stays where it is either way, so the title bar does not jump.
		/// </summary>
		public bool Collapsed
		{
			get => collapsed;
			set
			{
				if (value == collapsed)
				{
					return;
				}

				collapsed = value;
				double top = Position.Y + Height;
				ClientArea.Visible = !collapsed;
				titleLine.Visible = !collapsed;
				if (collapsed)
				{
					expandedHeight = Height;
					expandedMinimumSize = MinimumSize;

					// MinimumSize is first: it would hold the window at two title bars' height.
					double folded = deviceGrabWidth * 2 + TitleBar.Height + windowBackground.Padding.Height;
					MinimumSize = new Vector2(MinimumSize.X, folded);
					Size = new Vector2(Width, folded);
				}
				else
				{
					MinimumSize = expandedMinimumSize;
					Size = new Vector2(Width, expandedHeight);
				}

				Position = new Vector2(Position.X, top - Height);
				UpdateGrabControls();
				ApplyAutoSize();
				collapseButton?.Invalidate();
			}
		}

		/// <summary>
		/// Keeps the window's height fitted to its content, the top edge staying put, while the width stays as
		/// set; resizing is off meanwhile. False, the default. agg-gui's auto_size, which also pins the width.
		/// </summary>
		/// <remarks>
		/// The content is <see cref="ClientArea"/>'s children, so it has to size itself (VAnchor Fit): a child
		/// that stretches to the client area has no height of its own and leaves the window as it is.
		/// </remarks>
		public bool AutoSize
		{
			get => autoSize;
			set
			{
				autoSize = value;
				UpdateGrabControls();
				ApplyAutoSize();
			}
		}

		/// <summary>
		/// Whether the window can be maximized to fill its parent - a maximize button beside the close button and
		/// a double-click on the title bar, which both restore it again, as agg-gui's Window does. False, the
		/// default. Turning it off restores a maximized window.
		/// </summary>
		public bool Maximizable
		{
			get => maximizable;
			set
			{
				maximizable = value;
				if (maximizeButton != null)
				{
					maximizeButton.Visible = value;
				}

				if (!value)
				{
					Maximized = false;
				}
			}
		}

		/// <summary>Raised when <see cref="Maximized"/> changes.</summary>
		public event EventHandler MaximizedChanged;

		/// <summary>
		/// Fills the parent with the visible window (true) and keeps it filled as the parent resizes, or puts the
		/// window back where and how big it was (false). Settable whether or not <see cref="Maximizable"/> is on,
		/// so a host can restore a saved state. A maximized window cannot be dragged or resized.
		/// </summary>
		public bool Maximized
		{
			get => maximizer.IsMaximized;
			set
			{
				if (value)
				{
					Collapsed = false;
				}

				if (maximizer.Set(value))
				{
					UpdateGrabControls();
					maximizeButton?.Invalidate();
					MaximizedChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// The window's own position and size - what it goes back to when restored - whether or not it is
		/// maximized. The rectangle is the whole widget, grab border included, in the parent's coordinates.
		/// </summary>
		public RectangleDouble RestoreBounds => maximizer.RestoreBounds;

		/// <summary>
		/// A double-click on the title bar of a <see cref="Maximizable"/> window, anywhere but its buttons,
		/// toggles <see cref="Maximized"/>. Returns true when it did, so the bar does not start a drag.
		/// </summary>
		internal bool TitleBarDoubleClicked(TitleBarWidget bar, MouseEventArgs mouseEvent)
		{
			if (!maximizable
				|| mouseEvent.Clicks != 2
				|| mouseEvent.Button != MouseButtons.Left)
			{
				return false;
			}

			// The buttons take their own clicks: a double-click on the maximize button is two toggles, as in agg-gui.
			Vector2 screen = bar.TransformToScreenSpace(new Vector2(mouseEvent.X, mouseEvent.Y));
			foreach (GuiWidget control in new GuiWidget[] { titleBarButtons, collapseButton })
			{
				if (control != null
					&& control.Visible
					&& control.PositionWithinLocalBounds(control.TransformFromScreenSpace(screen)))
				{
					return false;
				}
			}

			Maximized = !Maximized;
			return true;
		}

		public override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			maximizer.FollowParent();
		}

		public override void OnClosed(EventArgs e)
		{
			maximizer.Release();
			base.OnClosed(e);
		}

		private void AddGrabControl(GrabControl grabControl)
		{
			grabControls.Add(grabControl);
			AddChild(grabControl);
		}

		private void UpdateGrabControls()
		{
			bool on = resizable && !autoSize && !collapsed && !Maximized;
			foreach (var grabControl in grabControls)
			{
				grabControl.Visible = on;
			}
		}

		/// <summary>Sizes the window to its content's height when <see cref="AutoSize"/> is on.</summary>
		private void ApplyAutoSize()
		{
			if (!autoSize
				|| collapsed
				|| applyingAutoSize)
			{
				return;
			}

			double content = 0;
			foreach (var child in ClientArea.Children)
			{
				if (child.Visible)
				{
					content = Math.Max(content, child.Height + child.Margin.Height);
				}
			}

			double wanted = Height - ClientArea.Height + content + ClientArea.Padding.Height;
			if (Math.Abs(wanted - Height) < .5)
			{
				return;
			}

			// Resizing lays the client area out again, which lands back here before the window has its new size.
			applyingAutoSize = true;
			try
			{
				double top = Position.Y + Height;
				Size = new Vector2(Width, wanted);
				Position = new Vector2(Position.X, top - Height);
			}
			finally
			{
				applyingAutoSize = false;
			}
		}

		/// <summary>
		/// Puts a widget in the title bar immediately to the left of the close button, or at the right hand end
		/// of the bar when the window has no close button.
		/// </summary>
		/// <remarks>
		/// Callable at any point after <see cref="AddTitleBar"/>, so a window can gain a button as its content
		/// decides it needs one. Calling it before there is a title bar does nothing, because there is nowhere
		/// for the button to go.
		/// </remarks>
		public void AddTitleBarButton(GuiWidget button)
		{
			if (titleBarButtons == null
				|| button == null)
			{
				return;
			}

			// Index rather than append: the close button is the last thing in the row and has to stay there -
			// a close button that moves when a window adds a feature is a misclick waiting to happen.
			int insertIndex = closeButton == null ? -1 : titleBarButtons.Children.IndexOf(closeButton);
			titleBarButtons.AddChild(button, insertIndex);
		}

        public override void OnDrawBackground(Graphics2D graphics2D)
		{
			if (IsStyled)
			{
				DrawStyledBackground(graphics2D);
				return;
			}

            var bounds = this.LocalBounds;
			bounds.Deflate(new BorderDouble(deviceGrabWidth));
            graphics2D.FillRectangle(bounds, BackgroundColor);

            // draw the shadow
            for (int i = 0; i < deviceGrabWidth; i++)
			{
				var color = new Color(Color.Black, (int)(50 * i / deviceGrabWidth));
				// left line
				graphics2D.Line(i + .5,
					i + .5,
					i + .5,
					Height - i - .5,
					color);

				// right line
				graphics2D.Line(Width - i - .5,
					i + .5,
					Width - i - .5,
					Height - i - .5,
					color);

				// bottom line
				graphics2D.Line(i + .5,
					i + .5,
					Width - i - .5,
					i + .5,
					color);

				// top line
				graphics2D.Line(i + .5,
					Height - i - .5,
					Width - i - .5,
					Height - i - .5,
					color);
			}
		}

		/// <summary>
		/// Draws the soft shadow, the rounded background and the title bar colour of a window with any of the
		/// style options set. The border is the panel's own (<see cref="WindowBorder"/>), which follows the same
		/// radius.
		/// </summary>
		private void DrawStyledBackground(Graphics2D graphics2D)
		{
			// the visible window is the panel inside the grab border, wherever layout has put it
			var panel = windowBackground.BoundsRelativeToParent;
			var radius = cornerRadius * DeviceScale;

			if (ShadowColor.Alpha0To255 > 0)
			{
				// A blur is stacked translucent rounded rectangles, widest first: each ring past the window's edge
				// is covered by fewer layers, so the shadow fades out over ShadowBlur. One layer per device pixel
				// of blur (capped) keeps the steps under a pixel apart at any display scale.
				var shadow = panel;
				shadow.Offset(ShadowOffset * DeviceScale);
				var blur = Math.Max(0, ShadowBlur * DeviceScale);
				int layers = Math.Max(1, Math.Min(12, (int)Math.Ceiling(blur)));
				// per layer alpha such that all of them together reach the requested alpha
				var layerAlpha = 1 - Math.Pow(1 - ShadowColor.Alpha0To1, 1.0 / layers);
				var layerColor = new Color(ShadowColor, (int)Math.Round(layerAlpha * 255));
				for (int i = layers; i >= 1; i--)
				{
					var grow = blur * i / layers;
					var layer = shadow;
					layer.Inflate(grow);
					var rect = new RoundedRect(layer, radius + grow);
					graphics2D.Render(rect, layerColor);
				}
			}

			if (BackgroundColor.Alpha0To255 > 0)
			{
				graphics2D.Render(new RoundedRect(panel, radius), BackgroundColor);
			}

			if (TitleBarColor.Alpha0To255 > 0
				&& TitleBar != null)
			{
				var titleBarBounds = new RectangleDouble(panel.Left, panel.Top - TitleBar.Height, panel.Right, panel.Top);
				var titleBarShape = new RoundedRect(titleBarBounds, 0);
				titleBarShape.radius(0, 0, radius, radius);
				graphics2D.Render(titleBarShape, TitleBarColor);
			}
		}

		/// <summary>
		/// Adds the eight edge and corner handles that resize the window.
		/// </summary>
		/// <remarks>
		/// Every handler places the window absolutely - the size and position the window had when the drag
		/// started, plus how far the mouse has moved since, in the window's parent's units (see GrabControl.DragDelta). Nothing is accumulated from the
		/// previous move, because the handle is anchored to the edge it drags: it slides out from under the
		/// mouse on every resize, so a delta measured in its own coordinates is measured against a moving
		/// reference frame. Position is always derived from the size the window actually took, so the minimum
		/// size clamp stops the moving edge instead of sliding the whole window.
		/// </remarks>
		private void AddGrabControls()
		{
			// this is for debugging
			var grabCornnerColor = Color.Transparent;
			var grabEdgeColor = Color.Transparent;

			// left grab control
			AddGrabControl(new GrabControl(Cursors.SizeWE)
			{
				BackgroundColor = grabEdgeColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Stretch,
                Margin = new BorderDouble(0, deviceGrabWidth, 0, deviceGrabWidth),
                Size = new Vector2(deviceGrabWidth, 0),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = new Vector2(startSize.X - s.DragDelta.X, startSize.Y);
					// from the size that was actually taken, not the one asked for, so a drag past the minimum
					// width stops the left edge rather than walking the whole window across the screen
					Position = new Vector2(s.ParentPositionAtMouseDown.X + (startSize.X - Size.X), s.ParentPositionAtMouseDown.Y);
				}
			});

			// bottom grab control
			AddGrabControl(new GrabControl(Cursors.SizeNS)
			{
				BackgroundColor = grabEdgeColor,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom,
                Margin = new BorderDouble(deviceGrabWidth, 0, deviceGrabWidth, 0),
                Size = new Vector2(0, deviceGrabWidth),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = new Vector2(startSize.X, startSize.Y - s.DragDelta.Y);
					Position = new Vector2(s.ParentPositionAtMouseDown.X, s.ParentPositionAtMouseDown.Y + (startSize.Y - Size.Y));
				}
			});

			// left bottom grab control
			AddGrabControl(new GrabControl(Cursors.SizeNESW)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = startSize - s.DragDelta;
					Position = s.ParentPositionAtMouseDown + startSize - Size;
				}
			});

			// left top grab control
			AddGrabControl(new GrabControl(Cursors.SizeNWSE)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Top,
				Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = new Vector2(startSize.X - s.DragDelta.X, startSize.Y + s.DragDelta.Y);
					Position = new Vector2(s.ParentPositionAtMouseDown.X + (startSize.X - Size.X), s.ParentPositionAtMouseDown.Y);
                }
			});

			// right grab control
			AddGrabControl(new GrabControl(Cursors.SizeWE)
			{
				BackgroundColor = grabEdgeColor,
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right,
                Margin = new BorderDouble(0, deviceGrabWidth, 0, deviceGrabWidth),
                Size = new Vector2(deviceGrabWidth, 0),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = new Vector2(startSize.X + s.DragDelta.X, startSize.Y);
				}
			});

            // right top grab control
            AddGrabControl(new GrabControl(Cursors.SizeNESW)
            {
                BackgroundColor = grabCornnerColor,
                HAnchor = HAnchor.Right,
                VAnchor = VAnchor.Top,
                Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
                AdjustParent = (s) =>
                {
                    Size = s.ParentSizeAtMouseDown + s.DragDelta;
                }
            });
            
            // top grab control
            AddGrabControl(new GrabControl(Cursors.SizeNS)
			{
				BackgroundColor = grabEdgeColor,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top,
                Margin = new BorderDouble(deviceGrabWidth, 0, deviceGrabWidth, 0),
                Size = new Vector2(0, deviceGrabWidth),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = new Vector2(startSize.X, startSize.Y + s.DragDelta.Y);
				}
			});

			// right bottom
			AddGrabControl(new GrabControl(Cursors.SizeNWSE)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
				AdjustParent = (s) =>
				{
					var startSize = s.ParentSizeAtMouseDown;
					Size = new Vector2(startSize.X + s.DragDelta.X, startSize.Y - s.DragDelta.Y);
					Position = new Vector2(s.ParentPositionAtMouseDown.X, s.ParentPositionAtMouseDown.Y + (startSize.Y - Size.Y));
				}
			});
		}
	}
}