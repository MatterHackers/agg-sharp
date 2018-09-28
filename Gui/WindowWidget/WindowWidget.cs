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
		private int grabWidth = (int)Math.Round(5 * GuiWidget.DeviceScale);
		private GuiWidget windowBackground;

		public WindowWidget(RectangleDouble InBounds)
		{
			windowBackground = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(grabWidth),
			};

			base.AddChild(windowBackground);

			TitleBar = new TitleBarWidget(this)
			{
				Size = new Vector2(0, 30 * GuiWidget.DeviceScale),
				HAnchor = HAnchor.Stretch,
			};
			windowBackground.AddChild(TitleBar);

			MinimumSize = new Vector2(grabWidth * 8, grabWidth * 4 + TitleBar.Height * 2);
			WindowBorder = new BorderDouble(1);
			WindowBorderColor = Color.Cyan;

			Position = new Vector2(InBounds.Left, InBounds.Bottom);
			Size = new Vector2(InBounds.Width, InBounds.Height);

			AddGrabControls();

			ClientArea = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			windowBackground.AddChild(ClientArea);
		}

		public WindowWidget(int x, int y, int width, int height)
			: this(new RectangleDouble(x, y, x + width, y + height))
		{
		}

		public Color WindowBackgroundColor
		{
			get => windowBackground.BackgroundColor;
			set => windowBackground.BackgroundColor = value;
		}

		public BorderDouble WindowBorder { get => windowBackground.Border; set => windowBackground.Border = value; }
		public Color WindowBorderColor { get => windowBackground.BorderColor; set => windowBackground.BorderColor = value; }
		public GuiWidget ClientArea { get; }

		public TitleBarWidget TitleBar { get; private set; }

		public Color TitleBarBackgroundColor { get; set; } = Color.LightGray;

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			// draw on top of the backgroud color
			graphics2D.FillRectangle(grabWidth, Height - grabWidth, Width - grabWidth, Height - TitleBar.Height - grabWidth, TitleBarBackgroundColor);

			// draw the shadow
			for (int i = 0; i < grabWidth; i++)
			{
				var color = new Color(Color.Black, 100 * i / grabWidth);
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
			// this is for debuging
			var grabCornnerColor = Color.Transparent;// Color.Blue;
			var grabEdgeColor = Color.Transparent;//Color.Red;

			// left grab control
			base.AddChild(new GrabControl(Cursors.SizeWE)
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(0, grabWidth),
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Stretch,
				Size = new Vector2(grabWidth, 0),
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
			base.AddChild(new GrabControl(Cursors.SizeNS)
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(grabWidth, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(0, grabWidth),
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
			base.AddChild(new GrabControl(Cursors.SizeNESW)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(grabWidth, grabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size -= delta;
					Position = Position + startSize - Size;
				}
			});

			// left top grab control
			base.AddChild(new GrabControl(Cursors.SizeNWSE)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Top,
				Size = new Vector2(grabWidth, grabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size = new Vector2(Size.X - delta.X, Size.Y + delta.Y);
					Position += new Vector2(startSize.X - Size.X, 0);
				}
			});

			// right grab control
			base.AddChild(new GrabControl(Cursors.SizeWE)
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(0, grabWidth),
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right,
				Size = new Vector2(grabWidth, 0),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X + delta.X, Size.Y);
				}
			});

			// right top grab control
			base.AddChild(new GrabControl(Cursors.SizeNESW)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Top,
				Size = new Vector2(grabWidth, grabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X + delta.X, Size.Y + delta.Y);
				}
			});

			// top grab control
			base.AddChild(new GrabControl(Cursors.SizeNS)
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(grabWidth, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top,
				Size = new Vector2(0, grabWidth),
				AdjustParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X, Size.Y + delta.Y);
				}
			});

			// right bottom
			base.AddChild(new GrabControl(Cursors.SizeNWSE)
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				Size = new Vector2(grabWidth, grabWidth),
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