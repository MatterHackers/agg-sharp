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
		public WindowWidget(RectangleDouble InBounds)
		{
			var grabWidth = 5;
			Border = new BorderDouble(1);
			BorderColor = Color.Cyan;

			BackgroundColor = Color.White;

			Position = new Vector2(InBounds.Left, InBounds.Bottom);
			Size = new Vector2(InBounds.Width, InBounds.Height);

			DragBarColor = Color.LightGray;
			TitleBar = new TitleBarWidget()
			{
				Size = new Vector2(0, 29 - grabWidth),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top,
				Margin = new BorderDouble(grabWidth, 0, grabWidth, grabWidth),
				BackgroundColor = Color.LightGray,
				Border = new BorderDouble(0, 1, 0, 0),
				BorderColor = Color.Black,
			};
			base.AddChild(TitleBar);

			// left grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = Color.Red,
				Margin = new BorderDouble(0, grabWidth),
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Stretch,
				Cursor = Cursors.SizeWE,
				Size = new Vector2(grabWidth, 0),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					delta.Y = 0;
					Position = Position + delta;
					Size = new Vector2(Size.X - delta.X, Size.Y);
				}
			});

			// bottom grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = Color.Red,
				Margin = new BorderDouble(grabWidth, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom,
				Cursor = Cursors.SizeNS,
				Size = new Vector2(0, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					delta.X = 0;
					Position = Position + delta;
					Size = new Vector2(Size.X, Size.Y - delta.Y);
				}
			});

			// left bottom grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = Color.Blue,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Bottom,
				Cursor = Cursors.SizeNESW,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Position = Position + delta;
					Size -= delta;
				}
			});

			// left top grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = Color.Blue,
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Top,
				Cursor = Cursors.SizeNWSE,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Position += new Vector2(delta.X, 0);
					Size = new Vector2(Size.X - delta.X, Size.Y + delta.Y);
				}
			});

			// right grab control
			base.AddChild(new GrabControl()
			{
				BackgroundColor = Color.Red,
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
				BackgroundColor = Color.Blue,
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
				BackgroundColor = Color.Red,
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
				BackgroundColor = Color.Blue,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				Cursor = Cursors.SizeNWSE,
				Size = new Vector2(grabWidth, grabWidth),
				MoveParent = (s, e) =>
				{
					var delta = e.Position - s.downPosition;
					Position = new Vector2(Position.X, Position.Y + delta.Y);
					Size = new Vector2(Size.X + delta.X, Size.Y - delta.Y);
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

		public GuiWidget ClientArea { get; }

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