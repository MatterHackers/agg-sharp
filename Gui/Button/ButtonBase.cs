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
// classes ButtonWidget
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public class ButtonBase : GuiWidget
	{
		public bool MouseDownOnButton { get; private set; } = false;

		public ButtonBase()
		{
		}

		public ButtonBase(double x, double y)
		{
			OriginRelativeParent = new Vector2(x, y);
		}

		public void ClickButton(MouseEventArgs mouseEvent)
		{
			this.OnClick(mouseEvent);
		}

		protected void FixBoundsAndChildrenPositions()
		{
			SetBoundsToEncloseChildren();

			if (LocalBounds.Left != 0 || LocalBounds.Bottom != 0)
			{
				SuspendLayout();
				// let's make sure that a button has 0, 0 at the lower left
				// move the children so they will fit with 0, 0 at the lower left
				foreach (GuiWidget child in Children)
				{
					child.OriginRelativeParent = child.OriginRelativeParent + new Vector2(-LocalBounds.Left, -LocalBounds.Bottom);
				}
				ResumeLayout();

				SetBoundsToEncloseChildren();
			}
		}

		override public void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				MouseDownOnButton = true;
			}
			else
			{
				MouseDownOnButton = false;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnEnabledChanged(EventArgs e)
		{
			if (Enabled == false)
			{
				MouseDownOnButton = false;
			}
			base.OnEnabledChanged(e);
		}

		override public void OnMouseUp(MouseEventArgs mouseEvent)
		{
			MouseDownOnButton = false;
			base.OnMouseUp(mouseEvent);
		}
	}
}