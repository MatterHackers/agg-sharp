/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.UI
{
    public class ScrollingArea : GuiWidget
    {
        ScrollableWidget parentScrollableWidget;

        public ScrollingArea(ScrollableWidget parentScrollableWidget)
        {
            this.parentScrollableWidget = parentScrollableWidget;
        }

        void CalculateChildrenBounds()
        {
            if (Children.Count > 0)
            {
                RectangleDouble boundsOfChildren = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
                foreach (GuiWidget widget in Children)
                {
                    boundsOfChildren.ExpandToInclude(widget.BoundsRelativeToParent);
                    boundsOfChildren.Left = Math.Min(boundsOfChildren.Left, widget.BoundsRelativeToParent.Left);
                    boundsOfChildren.Bottom = Math.Min(boundsOfChildren.Bottom, widget.BoundsRelativeToParent.Bottom);
                    boundsOfChildren.Right = Math.Max(boundsOfChildren.Right, widget.BoundsRelativeToParent.Right);
                    boundsOfChildren.Top = Math.Max(boundsOfChildren.Top, widget.BoundsRelativeToParent.Top);
                }

                LocalBounds = boundsOfChildren;
            }
        }

        public override void OnMarginChanged()
        {
            base.OnMarginChanged();
            ValidateScrollPosition();
        }

        void RecalculateChildrenBounds(Object sender, EventArgs e)
        {
            Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;
            CalculateChildrenBounds();
            parentScrollableWidget.TopLeftOffset = topLeftOffset;
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            child.BoundsChanged += RecalculateChildrenBounds;
            child.PositionChanged += RecalculateChildrenBounds;

            // remember the offset
            Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;

            base.AddChild(child, indexInChildrenList);
            CalculateChildrenBounds();

            // and restore it
            parentScrollableWidget.TopLeftOffset = topLeftOffset;
        }

        int debugRucursionCount = 0;
        internal void ValidateScrollPosition()
        {
            Vector2 newOrigin = OriginRelativeParent;

            Vector2 topLeftOffset = parentScrollableWidget.TopLeftOffset;

            RectangleDouble boundsWithMargin = LocalBounds;
            boundsWithMargin.Inflate(Margin);
            if (boundsWithMargin.Height < parentScrollableWidget.LocalBounds.Height)
            {
                debugRucursionCount++;
                if (debugRucursionCount < 20)
                {
                    parentScrollableWidget.TopLeftOffset = new Vector2(parentScrollableWidget.TopLeftOffset.x, 0);
                }
                debugRucursionCount--;
                newOrigin.y = OriginRelativeParent.y;
            }
            else
            {
                if (newOrigin.y + Margin.Top + Padding.Top + LocalBounds.Top < Parent.LocalBounds.Top)
                {
                    newOrigin.y = Parent.LocalBounds.Top - Margin.Top - Padding.Top - LocalBounds.Top;
                }
                else if (LocalBounds.Height + Margin.Height >= Parent.LocalBounds.Height)
                {
                    if (BoundsRelativeToParent.Bottom - Margin.Bottom > Parent.LocalBounds.Bottom)
                    {
                        newOrigin.y = Parent.LocalBounds.Bottom - LocalBounds.Bottom + Margin.Bottom;
                    }
                }
            }

            if (BoundsRelativeToParent.Left - Margin.Left > Parent.LocalBounds.Left)
            {
                newOrigin.x = Parent.LocalBounds.Left - LocalBounds.Left + Margin.Left;
            }
            else if (LocalBounds.Width + Margin.Width > Parent.LocalBounds.Width)
            {
                if (BoundsRelativeToParent.Right + Margin.Right < Parent.LocalBounds.Right)
                {
                    newOrigin.x = Parent.LocalBounds.Right - LocalBounds.Right - Margin.Right;
                }
            }

            if (newOrigin != OriginRelativeParent)
            {
                OriginRelativeParent = newOrigin;
            }
        }
    }
}
