using MatterHackers.Agg.Platform;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2007 Lars Brubaker
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

        public override void OnContainsFocusChanged(FocusChangedArgs e)
        {
            base.OnContainsFocusChanged(e);

			UiThread.RunOnIdle(() =>
			{
				var parent = Parent;
				// move this window to the first position in the children list
				if (ContainsFocus
					&& parent != null)
				{
                    parent.RemoveChild(this);
					this.ClearRemovedFlag();
                    parent.AddChild(this);
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
			GuiWidget closeButton = null;
			if (closeAction != null)
			{
				closeButton = theme.CreateSmallResetButton();

				closeButton.HAnchor = HAnchor.Right;
				closeButton.ToolTipText = "Close".Localize();
				closeButton.Click += (s, e) =>
				{
					closeAction?.Invoke();
				};
			}

            var titleBarRow = new Toolbar(theme.TabbarPadding, closeButton)
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
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					delta.Y = 0;
					var startSize = Size;
					Size = new Vector2(Size.X - delta.X, Size.Y);
					Position += startSize - Size;
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
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					delta.X = 0;
					var startSize = Size;
					Size = new Vector2(Size.X, Size.Y - delta.Y);
					Position = Position + startSize - Size;
				}
			});

			// left bottom grab control
			this.AddChild(new GrabControl(Cursors.SizeNESW)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size -= delta;
					Position = Position + startSize - Size;
				}
			});

			// left top grab control
			this.AddChild(new GrabControl(Cursors.SizeNWSE)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Top,
				Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size = new Vector2(Size.X - delta.X, Size.Y + delta.Y);
                    Position += new Vector2(startSize.X - Size.X, 0);
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
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X + delta.X, Size.Y);
				}
			});

            // right top grab control
            this.AddChild(new GrabControl(Cursors.SizeNESW)
            {
                BackgroundColor = grabCornnerColor,
                HAnchor = HAnchor.Right,
                VAnchor = VAnchor.Top,
                Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
                AdjustParent = (s, e) =>
                {
                    var delta = e.Position - s.downPosition;
                    Size = new Vector2(Size.X + delta.X, Size.Y + delta.Y);
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
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X, Size.Y + delta.Y);
				}
			});

			// right bottom
			this.AddChild(new GrabControl(Cursors.SizeNWSE)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(deviceGrabWidth, deviceGrabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size = new Vector2(Size.X + delta.X, Size.Y - delta.Y);
					Position = new Vector2(Position.X, Position.Y + (startSize.Y - Size.Y));
				}
			});
		}
	}
}