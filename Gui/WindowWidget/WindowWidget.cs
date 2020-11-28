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
		private int grabWidth2 = 5;

		private double deviceGrabWidth => grabWidth2 * DeviceScale;

		private readonly GuiWidget windowBackground;

		public WindowWidget(RectangleDouble inBounds)
			: this(new GuiWidget(inBounds.Width, inBounds.Height)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Position = new Vector2(inBounds.Left, inBounds.Bottom),
				Size = new Vector2(inBounds.Width, inBounds.Height)
			})
		{
		}

		public WindowWidget(GuiWidget clientArea)
		{
			windowBackground = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(grabWidth2),
			};

			AddChild(windowBackground);

			TitleBar = new TitleBarWidget(this)
			{
				Size = new Vector2(0, 30 * GuiWidget.DeviceScale),
				HAnchor = HAnchor.Stretch,
			};
			windowBackground.AddChild(TitleBar);

			MinimumSize = new Vector2(deviceGrabWidth * 8, deviceGrabWidth * 4 + TitleBar.Height * 2);
			WindowBorder = new BorderDouble(1);
			WindowBorderColor = Color.Cyan;

			Position = clientArea.Position - new Vector2(deviceGrabWidth, deviceGrabWidth);
			Size = clientArea.Size + new Vector2(deviceGrabWidth * 2, deviceGrabWidth * 2 + TitleBar.Height);

			AddGrabControls();

			ClientArea = clientArea;

			windowBackground.AddChild(ClientArea);
		}

		public BorderDouble WindowBorder { get => windowBackground.Border; set => windowBackground.Border = value; }

		public Color WindowBorderColor { get => windowBackground.BorderColor; set => windowBackground.BorderColor = value; }

		public GuiWidget ClientArea { get; }

		public TitleBarWidget TitleBar { get; private set; }

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
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