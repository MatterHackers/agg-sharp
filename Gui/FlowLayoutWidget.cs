/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class FlowLayoutWidget : GuiWidget
    {
        private LayoutEngineFlow layoutEngine;

        public FlowLayoutWidget(FlowDirection direction = FlowDirection.LeftToRight)
        {
            this.HAnchor = HAnchor.Fit;
            this.VAnchor = VAnchor.Fit;
            this.LayoutEngine = layoutEngine = new LayoutEngineFlow(direction);
        }

        public FlowDirection FlowDirection
        {
            get => layoutEngine.FlowDirection;
            set => layoutEngine.FlowDirection = value;
        }

        /// <summary>
        /// Gets or sets the padding. A vertical flow does not place children without a horizontal alignment
        /// (Absolute or Fit) across its width; they keep x = 0, so they sit one padding in only while the flow's
        /// left edge is one padding left of its origin. A flow that fits its width gets that edge from the fit when
        /// padding is set; a stretched or fixed-width one would keep its old edge and draw those children against
        /// it. So a change of left padding on a vertical flow that does not fit its width moves the left edge by
        /// the change and the origin the other way: the flow stays where it is, and its unaligned children sit one
        /// padding in whichever of Padding and HAnchor was set first.
        /// </summary>
        public override BorderDouble Padding
        {
            get => base.Padding;
            set
            {
                double oldLeft = DevicePadding.Left;
                base.Padding = value;
                double shift = DevicePadding.Left - oldLeft;
                if (shift != 0
                    && (FlowDirection == FlowDirection.TopToBottom || FlowDirection == FlowDirection.BottomToTop)
                    && !HAnchorIsSet(HAnchor.Fit))
                {
                    RectangleDouble oldBounds = LocalBounds;
                    var bounds = new RectangleDouble(oldBounds.Left - shift, oldBounds.Bottom, oldBounds.Right - shift, oldBounds.Top);
                    using (LayoutLock())
                    {
                        OriginRelativeParent = new Vector2(OriginRelativeParent.X + shift, OriginRelativeParent.Y);
                        LocalBounds = bounds;
                    }

                    PerformLayout();
                }
            }
        }
    }
}