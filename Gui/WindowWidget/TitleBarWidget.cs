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
using System;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class TitleBarWidget : GuiWidget
    {
        bool mouseDownOnBar = false;
        Vector2 DownPosition;

        public TitleBarWidget()
        {
        }

        public TitleBarWidget(RectangleDouble InBounds)
        {
            OriginRelativeParent = new Vector2(InBounds.Left, InBounds.Bottom);
            LocalBounds = new RectangleDouble(0, 0, InBounds.Width, InBounds.Height);
        }

        protected bool MouseDownOnBar
        {
            get { return mouseDownOnBar; }
            set { mouseDownOnBar = value; }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RoundedRect roundRect = new RoundedRect(BoundsRelativeToParent, 0);
            graphics2D.Render(roundRect, new RGBA_Bytes(0, 0, 0, 30));
            base.OnDraw(graphics2D);
        }

        override public void OnMouseDown(MouseEventArgs mouseEvent)
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

        override public void OnMouseUp(MouseEventArgs mouseEvent)
        {
            MouseDownOnBar = false;
            base.OnMouseUp(mouseEvent);
        }

        override public void OnMouseMove(MouseEventArgs mouseEvent)
        {
            if (MouseDownOnBar)
            {
                Vector2 mousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);

                Vector2 parentOriginRelativeToItsParent = Parent.OriginRelativeParent;
                parentOriginRelativeToItsParent.x += mousePosition.x - DownPosition.x;
                parentOriginRelativeToItsParent.y += mousePosition.y - DownPosition.y;
                if (parentOriginRelativeToItsParent.y + Parent.Height - (Height - DownPosition.y) > Parent.Parent.Height)
                {
                    parentOriginRelativeToItsParent.y = Parent.Parent.Height - Parent.Height + (Height - DownPosition.y);
                }
                Parent.Invalidate();
                Parent.OriginRelativeParent = parentOriginRelativeToItsParent;
                Parent.Invalidate();
            }
            base.OnMouseMove(mouseEvent);
        }
    }
}
