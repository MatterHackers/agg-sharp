using MatterHackers.Agg.Platform;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;

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
        private readonly GuiWidget windowBackground;

        /// <summary>
        /// The right hand end of the title bar, holding the close button and anything
        /// <see cref="AddTitleBarButton"/> has put beside it. Null until <see cref="AddTitleBar"/> runs.
        /// </summary>
        private FlowLayoutWidget titleBarButtons;

        private GuiWidget closeButton;

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
            
			windowBackground = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(grabWidth),
				BackgroundRadius = 3,
			};

			AddChild(windowBackground);

			TitleBar = new TitleBarWidget(this)
			{
				Size = new Vector2(0, 30 * GuiWidget.DeviceScale),
				HAnchor = HAnchor.Stretch,
			};
			windowBackground.AddChild(TitleBar);

            windowBackground.AddChild(new HorizontalLine(theme.PrimaryAccentColor));

            MinimumSize = new Vector2(deviceGrabWidth * 8, deviceGrabWidth * 4 + TitleBar.Height * 2);
			WindowBorder = 1;
			WindowBorderColor = theme.PrimaryAccentColor;

			Position = clientArea.Position - new Vector2(deviceGrabWidth, deviceGrabWidth);
			Size = clientArea.Size + new Vector2(deviceGrabWidth * 2, deviceGrabWidth * 2 + TitleBar.Height);

			AddGrabControls();

			ClientArea = clientArea;

			windowBackground.AddChild(ClientArea);
		}

		public double WindowBorder { get => windowBackground.BackgroundOutlineWidth; set => windowBackground.BackgroundOutlineWidth = value; }

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

            var titleBarRow = new Toolbar(theme.TabbarPadding, titleBarButtons)
            {
                HAnchor = HAnchor.Stretch,
                VAnchor = VAnchor.Fit | VAnchor.Center,
            };

            titleBarRow.AddChild(new ImageWidget(StaticData.Instance.LoadIcon("mh.png", 16, 16).GrayToColor(theme.TextColor))
            {
                Margin = new BorderDouble(4, 0, 6, 0),
                VAnchor = VAnchor.Center
            });

            titleBarRow.ActionArea.AddChild(new TextWidget(title ?? "", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
            {
                VAnchor = VAnchor.Center,
            });

            TitleBar.AddChild(titleBarRow);
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
		/// Adds the eight edge and corner handles that resize the window.
		/// </summary>
		/// <remarks>
		/// Every handler places the window absolutely - the size and position the window had when the drag
		/// started, plus how far the mouse has moved since, in screen space. Nothing is accumulated from the
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
			AddChild(new GrabControl(Cursors.SizeWE)
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
			this.AddChild(new GrabControl(Cursors.SizeNS)
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
			this.AddChild(new GrabControl(Cursors.SizeNESW)
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
			this.AddChild(new GrabControl(Cursors.SizeNWSE)
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
			this.AddChild(new GrabControl(Cursors.SizeWE)
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
            this.AddChild(new GrabControl(Cursors.SizeNESW)
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
            this.AddChild(new GrabControl(Cursors.SizeNS)
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
			this.AddChild(new GrabControl(Cursors.SizeNWSE)
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