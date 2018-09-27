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
		int grabWidth => (int)Math.Round(5 * GuiWidget.DeviceScale);
		int titleBarHeight => (int)Math.Round(30 * GuiWidget.DeviceScale);

		public WindowWidget(RectangleDouble InBounds)
		{
			MinimumSize = new Vector2(grabWidth * 8, grabWidth * 4 + titleBarHeight * 2);
			Border = new BorderDouble(1);
			BorderColor = Color.Cyan;

			BackgroundColor = Color.White;

			Position = new Vector2(InBounds.Left, InBounds.Bottom);
			Size = new Vector2(InBounds.Width, InBounds.Height);

			var grabCornnerColor = Color.Transparent;// Color.Blue;
			var grabEdgeColor = Color.Transparent;//Color.Red;

			DragBarColor = Color.LightGray;
			TitleBar = new TitleBarWidget()
			{
				Size = new Vector2(0, titleBarHeight - grabWidth),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top,
				Margin = new BorderDouble(grabWidth, 0, grabWidth, grabWidth),
			};
			base.AddChild(TitleBar);

			// left grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(0, grabWidth),
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Stretch,
				Cursor = Cursors.SizeWE,
				Size = new Vector2(grabWidth, 0),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					delta.Y = 0;
					var startSize = Size;
					Size = new Vector2(Size.X - delta.X, Size.Y);
					Position += startSize - Size;
				}
			});

			// bottom grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(grabWidth, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom,
				Cursor = Cursors.SizeNS,
				Size = new Vector2(0, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					delta.X = 0;
					var startSize = Size;
					Size = new Vector2(Size.X, Size.Y - delta.Y);
					Position = Position + startSize - Size;
				}
			});

			// left bottom grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Bottom,
				Cursor = Cursors.SizeNESW,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size -= delta;
					Position = Position + startSize - Size;
				}
			});

			// left top grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Top,
				Cursor = Cursors.SizeNWSE,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size = new Vector2(Size.X - delta.X, Size.Y + delta.Y);
					Position += new Vector2(startSize.X - Size.X, 0);
				}
			});

			// right grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(0, grabWidth),
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right,
				Cursor = Cursors.SizeWE,
				Size = new Vector2(grabWidth, 0),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X + delta.X, Size.Y);
				}
			});

			// right top grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Top,
				Cursor = Cursors.SizeNESW,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X + delta.X, Size.Y + delta.Y);
				}
			});

			// top grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabEdgeColor,
				Margin = new BorderDouble(grabWidth, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top,
				Cursor = Cursors.SizeNS,
				Size = new Vector2(0, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Size = new Vector2(Size.X, Size.Y + delta.Y);
				}
			});

			// right bottom
			base.AddChild(new GrabControl()
			{
				BackgroundColor = grabCornnerColor,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				Cursor = Cursors.SizeNWSE,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					var startSize = Size;
					Size = new Vector2(Size.X + delta.X, Size.Y - delta.Y);
					Position = new Vector2(Position.X, Position.Y + (startSize.Y - Size.Y));
				}
			});

			ClientArea = new GuiWidget()
			{
				//DebugShowBounds = true,
				Margin = new BorderDouble(grabWidth, grabWidth, grabWidth, TitleBar.Height + grabWidth),
			};
			ClientArea.AnchorAll();

			base.AddChild(ClientArea);
		}

		public WindowWidget(int x, int y, int width, int height)
			: this(new RectangleDouble(x, y, x + width, y + height))
		{
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			base.OnDrawBackground(graphics2D);

			// draw on top of the backgroud color
			var totalHeight = titleBarHeight + grabWidth;
			graphics2D.FillRectangle(0, Height, Width, Height - totalHeight, TitleBarBackgroundColor);

			var lineWidth = Math.Round(1 * GuiWidget.DeviceScale);
			graphics2D.FillRectangle(0, Height - totalHeight, Width, Height - totalHeight + lineWidth, Color.Black);
		}

		public GuiWidget ClientArea { get; }

		public Color TitleBarBackgroundColor { get; set; } = Color.LightGray;
		public TitleBarWidget TitleBar { get; private set; }

		private Color DragBarColor
		{
			get;
			set;
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			ClientArea.AddChild(child, indexInChildrenList);
		}

		private class GrabControl : GuiWidget
		{
			public Vector2 downPosition;
			internal Action<GrabControl, MouseEventArgs> MoveParent;
			private bool downOnTop = false;

			internal GrabControl()
			{
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				downOnTop = true;
				downPosition = mouseEvent.Position;

				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (downOnTop)
				{
					MoveParent?.Invoke(this, mouseEvent);
				}

				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				downOnTop = false;

				base.OnMouseUp(mouseEvent);
			}
		}

		//public override void OnMouseMove(MouseEventArgs mouseEvent)
		//{
		//	var delta = mouseEvent.Position - downPosition;
		//	switch (downEdge)
		//	{
		//		case WindowEdges.none:
		//			hoverEdge = GetEdge(mouseEvent.Position);
		//			//SetCursor(hoverEdge);
		//			break;

		//		case WindowEdges.left:
		//			Position = new Vector2(Position.X + delta.X, Position.Y);
		//			Size = new Vector2(Size.X - delta.X, Size.Y);
		//			break;

		//		case WindowEdges.leftBottom:
		//			Position = Position + delta;
		//			Size = new Vector2(Size.X - delta.X, Size.Y - delta.Y);
		//			break;

		//		case WindowEdges.leftTop:
		//			break;

		//		case WindowEdges.right:
		//			Size = new Vector2(Size.X + delta.X, Size.Y);
		//			downPosition = mouseEvent.Position;
		//			break;

		//		case WindowEdges.rightBottom:
		//			Position = new Vector2(Position.X, Position.Y + delta.Y);
		//			Size = new Vector2(Size.X + delta.X, Size.Y - delta.Y);
		//			downPosition.X = mouseEvent.Position.X;
		//			break;

		//		case WindowEdges.rightTop:
		//			break;

		//		case WindowEdges.top:
		//			Size = new Vector2(Size.X, Size.Y + delta.Y);
		//			downPosition = mouseEvent.Position;
		//			break;

		//		case WindowEdges.bottom:
		//			Position = new Vector2(Position.X, Position.Y + delta.Y);
		//			Size = new Vector2(Size.X, Size.Y - delta.Y);
		//			break;
		//	}

		//	base.OnMouseMove(mouseEvent);
		//}
	}
}