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

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The bar across the top of a <see cref="WindowWidget"/>. Dragging it moves the window.
	/// </summary>
	public class TitleBarWidget : GuiWidget
	{
		private Vector2 DownPosition;
		private bool mouseDownOnBar = false;

		// which button started the drag, so a move arriving without it can be told from one that is part of it
		private MouseButtons dragButton = MouseButtons.None;

		GuiWidget windowToDrag;

		public TitleBarWidget(GuiWidget windowToDrag)
		{
			this.windowToDrag = windowToDrag;
		}

		public bool ClampToParent { get; set; } = true;

        protected bool MouseDownOnBar
		{
			get { return mouseDownOnBar; }
			set { mouseDownOnBar = value; }
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// a press with no button at all starts no drag - the same rule GrabControl follows
			dragButton = mouseEvent.Button;

			if (dragButton != MouseButtons.None
				&& PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
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
			if (MouseDownOnBar
				&& mouseEvent.Button != dragButton)
			{
				// The drag is over even though no mouse up reached us. Both platform sinks report the pointer
				// leaving the window as a buttonless move to (-10, -10), and a mouse up that lands outside the
				// window can be dropped before it ever gets here - taking either for a drag threw the window at
				// the corner of the screen, or had it follow the pointer around with nothing held down.
				MouseDownOnBar = false;
			}

			if (MouseDownOnBar)
			{
				Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);

				Vector2 dragPosition = windowToDrag.Position;
				dragPosition.X += mousePosition.X - DownPosition.X;
				dragPosition.Y += mousePosition.Y - DownPosition.Y;

				if (ClampToParent)
				{
                    if (dragPosition.Y + windowToDrag.Height - (Height - DownPosition.Y) > windowToDrag.Parent.Height)
                    {
                        dragPosition.Y = windowToDrag.Parent.Height - windowToDrag.Height + (Height - DownPosition.Y);
                    }
                    
					var windowToDragParent = windowToDrag.Parent;
					if (windowToDragParent != null)
					{
						dragPosition.X = Util.Clamp(dragPosition.X, -windowToDrag.Width + 10, windowToDragParent.Width - 10);
						dragPosition.Y = Util.Clamp(dragPosition.Y, -windowToDrag.Height + 10, windowToDragParent.Height - windowToDrag.Height);
					}
				}

				windowToDrag.Position = dragPosition;
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			MouseDownOnBar = false;
			dragButton = MouseButtons.None;
			base.OnMouseUp(mouseEvent);
		}
	}
}