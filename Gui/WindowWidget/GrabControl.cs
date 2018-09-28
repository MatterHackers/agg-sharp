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
	public class GrabControl : GuiWidget
	{
		public Vector2 downPosition;
		internal Action<GrabControl, MouseEventArgs> AdjustParent;
		private Cursors cursor;
		private bool mouseIsDown = false;
		private GuiWidget perviousParent;

		public GrabControl(Cursors cursor)
		{
			this.cursor = cursor;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseIsDown = true;
			downPosition = mouseEvent.Position;

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseIsDown)
			{
				if (Parent?.Resizable == true)
				{
					AdjustParent?.Invoke(this, mouseEvent);
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
			Parent.ResizeableChanged += PerviousParent_ResizeableChanged;
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