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
    public class WindowWidget : GuiWidget
    {
        TitleBarWidget dragBar;
        GuiWidget clientArea = new GuiWidget();

        RGBA_Bytes DragBarColor
        {
            get;
            set;
        }

        RGBA_Bytes BackGroundColor
        {
            get;
            set;
        }

        public WindowWidget(RectangleDouble InBounds)
        {
            int sizeOfDragBar = 20;

            BackGroundColor = RGBA_Bytes.White;

            OriginRelativeParent = new Vector2(InBounds.Left, InBounds.Bottom);
            LocalBounds = new RectangleDouble(0, 0, InBounds.Width, InBounds.Height);

            DragBarColor = RGBA_Bytes.LightGray;
            dragBar = new TitleBarWidget(new RectangleDouble(0, InBounds.Height - sizeOfDragBar, InBounds.Width, InBounds.Height));
            //dragBar.DebugShowBounds = true;
            base.AddChild(dragBar);

            //clientArea.DebugShowBounds = true;
            base.AddChild(clientArea);
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            clientArea.AddChild(child, indexInChildrenList);
        }

        public override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);

            clientArea.Margin = new BorderDouble(0, 0, 0, dragBar.Height);
            clientArea.AnchorAll();
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            if (dragBar != null)
            {
                dragBar.BoundsRelativeToParent = new RectangleDouble(0, Height - dragBar.Height, Width, Height);
                clientArea.BoundsRelativeToParent = new RectangleDouble(0, 0, Width, Height - dragBar.Height);
            }
            base.OnBoundsChanged(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RoundedRect roundRect = new RoundedRect(LocalBounds, 0);
            graphics2D.Render(roundRect, BackGroundColor);

            roundRect = new RoundedRect(dragBar.BoundsRelativeToParent, 0);
            graphics2D.Render(roundRect, DragBarColor);

            base.OnDraw(graphics2D);
        }
    }
}
