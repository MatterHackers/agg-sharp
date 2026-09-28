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
		private Vector2 downPosition;
		private GuiWidget perviousParent;

		public GrabControl(Cursors cursor)
		{
			this.cursor = cursor;
		}

		/// <summary>
		/// How far the mouse has travelled since the press that started the drag, in the coordinates of the
		/// window's parent - the units the window's Size and Position are in.
		/// </summary>
		/// <remarks>
		/// Measured from the press rather than from the previous move, because the handle is edge anchored: it
		/// slides out from under the mouse as the window resizes, so its local coordinates are a reference
		/// frame that moves with what is being measured. The window's parent does not move during the drag.
		/// Not screen space: a window inside a zoomed container (the geometry-node editor's graph) would then
		/// grow by the mouse travel times the zoom and its edge would run away from the mouse.
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

		/// <summary>
		/// Raised on every move of a drag, after <see cref="AdjustParent"/> has resized the window. A handler may
		/// place the window again - <see cref="SnapCoordinator"/> snaps it here - because every move is placed
		/// from the press (see <see cref="DragDelta"/>), not from where the previous one left the window.
		/// </summary>
		public event EventHandler DragMoved;

		/// <summary>
		/// Raised once when a drag is over: the button came up, or a move arrived without it.
		/// </summary>
		public event EventHandler DragEnded;

		/// <summary>
		/// The edge or corner of the window this handle drags, read from how it is anchored: a handle anchored
		/// Left and stretched vertically is the west edge, one anchored Right and Top the north-east corner.
		/// </summary>
		public ResizeEdge Edge
		{
			get
			{
				bool left = HAnchor.HasFlag(HAnchor.Left) && !HAnchor.HasFlag(HAnchor.Right);
				bool right = HAnchor.HasFlag(HAnchor.Right) && !HAnchor.HasFlag(HAnchor.Left);
				bool bottom = VAnchor.HasFlag(VAnchor.Bottom) && !VAnchor.HasFlag(VAnchor.Top);
				bool top = VAnchor.HasFlag(VAnchor.Top) && !VAnchor.HasFlag(VAnchor.Bottom);

				if (top)
				{
					return left ? ResizeEdge.NorthWest : right ? ResizeEdge.NorthEast : ResizeEdge.North;
				}

				if (bottom)
				{
					return left ? ResizeEdge.SouthWest : right ? ResizeEdge.SouthEast : ResizeEdge.South;
				}

				return left ? ResizeEdge.West : ResizeEdge.East;
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Which button started the drag is remembered so OnMouseMove can tell a real drag from a move with
			// nothing held down, and a press with no button at all starts no drag.
			dragButton = mouseEvent.Button;
			mouseIsDown = dragButton != MouseButtons.None;

			if (mouseIsDown)
			{
				downPosition = MouseInWindowParent(mouseEvent);
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
					DragEnded?.Invoke(this, EventArgs.Empty);
				}
				else if (Parent?.Resizable == true)
				{
					DragDelta = MouseInWindowParent(mouseEvent) - downPosition;
					AdjustParent?.Invoke(this);
					DragMoved?.Invoke(this, EventArgs.Empty);
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		/// <summary>
		/// The mouse in the coordinates the window is placed in, by way of the screen so every transform
		/// between here and there - a container's zoom included - is undone. Screen space when the window has
		/// no parent.
		/// </summary>
		private Vector2 MouseInWindowParent(MouseEventArgs mouseEvent)
		{
			var screen = this.TransformToScreenSpace(mouseEvent.Position);
			var windowParent = Parent?.Parent;
			return windowParent == null ? screen : windowParent.TransformFromScreenSpace(screen);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (mouseIsDown)
			{
				mouseIsDown = false;
				DragEnded?.Invoke(this, EventArgs.Empty);
			}

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
