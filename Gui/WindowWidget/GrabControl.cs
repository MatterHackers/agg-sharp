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
	/// <summary>
	/// One edge or corner handle of a <see cref="WindowWidget"/>. It does no resizing itself - it tracks the
	/// drag and hands <see cref="AdjustParent"/> everything needed to place the window absolutely.
	/// </summary>
	public class GrabControl : GuiWidget
	{
		/// <summary>
		/// Called on every move of a drag. It takes only the handle, because everything a resize needs -
		/// how far the mouse has moved and where the window started - is read from it.
		/// </summary>
		internal Action<GrabControl> AdjustParent;
		private Cursors cursor;
		private bool mouseIsDown = false;
		private MouseButtons dragButton = MouseButtons.None;
		private Vector2 downScreenPosition;
		private GuiWidget perviousParent;

		public GrabControl(Cursors cursor)
		{
			this.cursor = cursor;
		}

		/// <summary>
		/// How far the mouse has travelled since the press that started the drag, in screen space.
		/// </summary>
		/// <remarks>
		/// Screen space, and measured from the press rather than from the previous move, because the handle is
		/// edge anchored: it slides out from under the mouse as the window resizes, so its local coordinates
		/// are a reference frame that moves with what is being measured.
		/// </remarks>
		public Vector2 DragDelta { get; private set; }

		/// <summary>
		/// The size the parent had when the drag started. Handlers size the window from this rather than from
		/// its current size, so a move that arrives out of order, twice, or after a skipped one still lands the
		/// window exactly where the mouse is.
		/// </summary>
		public Vector2 ParentSizeAtMouseDown { get; private set; }

		/// <summary>
		/// The position the parent had when the drag started - the other half of what an absolute placement
		/// needs, for the handles that move the window's left or bottom edge.
		/// </summary>
		public Vector2 ParentPositionAtMouseDown { get; private set; }

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Which button started the drag is remembered so OnMouseMove can tell a real drag from a move with
			// nothing held down, and a press with no button at all starts no drag.
			dragButton = mouseEvent.Button;
			mouseIsDown = dragButton != MouseButtons.None;

			if (mouseIsDown)
			{
				downScreenPosition = this.TransformToScreenSpace(mouseEvent.Position);
				DragDelta = Vector2.Zero;
				ParentSizeAtMouseDown = Parent == null ? Vector2.Zero : Parent.Size;
				ParentPositionAtMouseDown = Parent == null ? Vector2.Zero : Parent.Position;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseIsDown)
			{
				if (mouseEvent.Button != dragButton)
				{
					// The drag is over even though no mouse up reached us. Both platform sinks report the pointer
					// leaving the window as a buttonless move to (-10, -10), and a mouse up that lands outside the
					// window can be dropped before it ever gets here - taking either for a drag snapped the window
					// to its minimum size and then had it chase the pointer around with no button held.
					mouseIsDown = false;
				}
				else if (Parent?.Resizable == true)
				{
					DragDelta = this.TransformToScreenSpace(mouseEvent.Position) - downScreenPosition;
					AdjustParent?.Invoke(this);
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouseIsDown = false;

			base.OnMouseUp(mouseEvent);
		}

		public override void OnParentChanged(EventArgs e)
		{
			if (perviousParent != null)
			{
				perviousParent.ResizeableChanged -= PerviousParent_ResizeableChanged;
			}

			perviousParent = Parent;
			if (Parent != null)
			{
				Parent.ResizeableChanged += PerviousParent_ResizeableChanged;
			}

			base.OnParentChanged(e);
			PerviousParent_ResizeableChanged(null, null);
		}

		private void PerviousParent_ResizeableChanged(object sender, EventArgs e)
		{
			if (Parent?.Resizable == true)
			{
				this.Cursor = cursor;
			}
			else
			{
				this.Cursor = Cursors.Arrow;
			}
		}
	}
}
