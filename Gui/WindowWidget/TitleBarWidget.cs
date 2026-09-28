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
using System;

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

		/// <summary>
		/// Raised on every move of a drag, after the window has been placed (and clamped). A handler may place
		/// the window again - <see cref="SnapCoordinator"/> snaps it here - because the next move is measured
		/// from the press, not from where this one left the window.
		/// </summary>
		public event EventHandler DragMoved;

		/// <summary>
		/// Raised once when a drag that started on the bar is over: the button came up, or a move arrived
		/// without it (the pointer left the platform window, or the release was lost).
		/// </summary>
		public event EventHandler DragEnded;

        protected bool MouseDownOnBar
		{
			get { return mouseDownOnBar; }
			set { mouseDownOnBar = value; }
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// a press with no button at all starts no drag - the same rule GrabControl follows
			dragButton = mouseEvent.Button;

			// A maximized window stays put, and a double-click that maximizes or restores it starts no drag:
			// the window has jumped from under the press the drag would be measured from.
			var window = windowToDrag as WindowWidget;
			if (window != null
				&& PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y)
				&& window.TitleBarDoubleClicked(this, mouseEvent))
			{
				MouseDownOnBar = false;
			}
			else if (window?.Maximized != true
				&& dragButton != MouseButtons.None
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
				DragEnded?.Invoke(this, EventArgs.Empty);
			}

			if (MouseDownOnBar)
			{
				Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);

				Vector2 dragPosition = windowToDrag.Position;
				dragPosition.X += mousePosition.X - DownPosition.X;
				dragPosition.Y += mousePosition.Y - DownPosition.Y;

				windowToDrag.Position = dragPosition;
				DragMoved?.Invoke(this, EventArgs.Empty);

				// Clamped after the DragMoved handlers, as agg-gui snaps and then clamps to the canvas
				// (widgets/window/events.rs): a snap to a window that pokes out of the parent can never
				// take this one out after it.
				if (ClampToParent)
				{
					windowToDrag.Position = ClampedToParent(windowToDrag.Position);
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		/// <summary>
		/// Keeps the window's top inside its parent (and the grabbed point of the bar with it), and at least 10
		/// pixels of it inside the parent's sides and bottom, so the bar can always be grabbed again.
		/// </summary>
		private Vector2 ClampedToParent(Vector2 dragPosition)
		{
			var windowToDragParent = windowToDrag.Parent;
			if (windowToDragParent == null)
			{
				return dragPosition;
			}

			if (dragPosition.Y + windowToDrag.Height - (Height - DownPosition.Y) > windowToDragParent.Height)
			{
				dragPosition.Y = windowToDragParent.Height - windowToDrag.Height + (Height - DownPosition.Y);
			}

			dragPosition.X = Util.Clamp(dragPosition.X, -windowToDrag.Width + 10, windowToDragParent.Width - 10);
			dragPosition.Y = Util.Clamp(dragPosition.Y, -windowToDrag.Height + 10, windowToDragParent.Height - windowToDrag.Height);
			return dragPosition;
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool wasDragging = MouseDownOnBar;
			MouseDownOnBar = false;
			dragButton = MouseButtons.None;
			if (wasDragging)
			{
				DragEnded?.Invoke(this, EventArgs.Empty);
			}

			base.OnMouseUp(mouseEvent);
		}
	}
}