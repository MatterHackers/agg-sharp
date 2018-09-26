using MatterHackers.VectorMath;

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
		private WindowEdges downEdge = WindowEdges.none;
		private Vector2 downPosition;
		private WindowEdges hoverEdge = WindowEdges.none;

		public WindowWidget(RectangleDouble InBounds)
		{
			Border = new BorderDouble(1);
			BorderColor = Color.Cyan;

			BackgroundColor = Color.White;

			Position = new Vector2(InBounds.Left, InBounds.Bottom);
			Size = new Vector2(InBounds.Width, InBounds.Height);

			DragBarColor = Color.LightGray;
			TitleBar = new TitleBarWidget(29);
			TitleBar.BackgroundColor = Color.LightGray;
			TitleBar.Border = new BorderDouble(0, 1, 0, 0);
			TitleBar.BorderColor = Color.Black;
			//dragBar.DebugShowBounds = true;
			base.AddChild(TitleBar);

			//clientArea.DebugShowBounds = true;
			ClientArea = new GuiWidget();
			ClientArea.Margin = new BorderDouble(0, 0, 0, TitleBar.Height);
			ClientArea.AnchorAll();

			base.AddChild(ClientArea);
		}

		private enum WindowEdges { none, left, leftBottom, leftTop, right, rightBottom, rightTop, top, bottom }

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

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			downPosition = mouseEvent.Position;
			downEdge = GetEdge(downPosition);

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			var delta = mouseEvent.Position - downPosition;
			switch (downEdge)
			{
				case WindowEdges.none:
					hoverEdge = GetEdge(mouseEvent.Position);
					SetCursor(hoverEdge);
					break;

				case WindowEdges.left:
					Position = new Vector2(Position.X + delta.X, Position.Y);
					Size = new Vector2(Size.X - delta.X, Size.Y);
					break;

				case WindowEdges.leftBottom:
					Position = Position + delta;
					Size = new Vector2(Size.X - delta.X, Size.Y - delta.Y);
					break;

				case WindowEdges.leftTop:
					break;

				case WindowEdges.right:
					Size = new Vector2(Size.X + delta.X, Size.Y);
					downPosition = mouseEvent.Position;
					break;

				case WindowEdges.rightBottom:
					Position = new Vector2(Position.X, Position.Y + delta.Y);
					Size = new Vector2(Size.X + delta.X, Size.Y - delta.Y);
					downPosition.X = mouseEvent.Position.X;
					break;

				case WindowEdges.rightTop:
					break;

				case WindowEdges.top:
					Size = new Vector2(Size.X, Size.Y + delta.Y);
					downPosition = mouseEvent.Position;
					break;

				case WindowEdges.bottom:
					Position = new Vector2(Position.X, Position.Y + delta.Y);
					Size = new Vector2(Size.X, Size.Y - delta.Y);
					break;
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downEdge = WindowEdges.none;

			base.OnMouseUp(mouseEvent);
		}

		private WindowEdges GetEdge(Vector2 position)
		{
			if (!Resizable)
			{
				return WindowEdges.none;
			}

			WindowEdges edge = WindowEdges.none;
			if (this.ContainsFirstUnderMouseRecursive())
			{
				if (position.X < 5)
				{
					if (position.Y < 5)
					{
						edge = WindowEdges.leftBottom;
					}
					else if (position.Y > Height - 5)
					{
						//edge = WindowEdges.leftTop;
					}
					else
					{
						edge = WindowEdges.left;
					}
				}
				else if (position.X > Width - 5)
				{
					if (position.Y < 5)
					{
						edge = WindowEdges.rightBottom;
					}
					else if (position.Y > Height - 5)
					{
						//edge = WindowEdges.rightTop;
					}
					else
					{
						edge = WindowEdges.right;
					}
				}
				else if (position.Y < 5)
				{
					edge = WindowEdges.bottom;
				}
				else if (position.Y > Height - 5)
				{
					//edge = WindowEdges.top;
				}
			}

			return edge;
		}

		private void SetCursor(WindowEdges target)
		{
			switch (target)
			{
				case WindowEdges.none:
					Cursor = Cursors.Default;
					break;

				case WindowEdges.left:
					Cursor = Cursors.SizeWE;
					break;

				case WindowEdges.leftBottom:
					Cursor = Cursors.SizeNESW;
					break;

				case WindowEdges.leftTop:
					Cursor = Cursors.SizeNWSE;
					break;

				case WindowEdges.right:
					Cursor = Cursors.SizeWE;
					break;

				case WindowEdges.rightBottom:
					Cursor = Cursors.SizeNWSE;
					break;

				case WindowEdges.rightTop:
					Cursor = Cursors.SizeNESW;
					break;

				case WindowEdges.top:
					Cursor = Cursors.SizeNS;
					break;

				case WindowEdges.bottom:
					Cursor = Cursors.SizeNS;
					break;
			}

			SetCursor(Cursor);
		}
	}
}