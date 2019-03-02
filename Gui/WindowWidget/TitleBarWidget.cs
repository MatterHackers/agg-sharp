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

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class TitleBarWidget : GuiWidget
	{
		private Vector2 DownPosition;
		private bool mouseDownOnBar = false;
		GuiWidget windowToDrag;

		public TitleBarWidget(GuiWidget windowToDrag)
		{
			this.windowToDrag = windowToDrag;
		}

		protected bool MouseDownOnBar
		{
			get { return mouseDownOnBar; }
			set { mouseDownOnBar = value; }
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				MouseDownOnBar = true;
				Vector2 mouseRelClient = new Vector2(mouseEvent.X, mouseEvent.Y);
				DownPosition = mouseRelClient;
			}
			else
			{
				MouseDownOnBar = false;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseDownOnBar)
			{
				Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);

				Vector2 dragPosition = windowToDrag.Position;
				dragPosition.X += mousePosition.X - DownPosition.X;
				dragPosition.Y += mousePosition.Y - DownPosition.Y;
				if (dragPosition.Y + windowToDrag.Height - (Height - DownPosition.Y) > windowToDrag.Parent.Height)
				{
					dragPosition.Y = windowToDrag.Parent.Height - windowToDrag.Height + (Height - DownPosition.Y);
				}

				var windowToDragParent = windowToDrag.Parent;
				if (windowToDragParent != null)
				{
					dragPosition.X = agg_basics.Clamp(dragPosition.X, -windowToDrag.Width + 10, windowToDragParent.Width - 10);
					dragPosition.Y = agg_basics.Clamp(dragPosition.Y, -windowToDrag.Height + 10, windowToDragParent.Height - windowToDrag.Height);
				}

				windowToDrag.Position = dragPosition;
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			MouseDownOnBar = false;
			base.OnMouseUp(mouseEvent);
		}
	}
}