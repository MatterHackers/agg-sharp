/*
Copyright (c) 2025, Lars Brubaker, John Lewin
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

        public static void SetFixedWidthChildrenToLargestMatching(GuiWidget widgetToFindChildrenIn)
        {
            SetFixedWidthChildrenToLargestMatching(widgetToFindChildrenIn.Children);
        }
        
        public static void SetFixedWidthChildrenToLargestMatching(IEnumerable<GuiWidget> widgetsToSetFixedWidth)
        {
            bool IsSizeableChild(GuiWidget guiWidget)
            {
                return guiWidget.HAnchor != HAnchor.Fit
                    && guiWidget.HAnchor != HAnchor.Stretch
                    && guiWidget.HAnchor != HAnchor.MaxFitOrStretch;
            }

            // find the largest child count of all widgetWithFlowLayouts children
            int maxChildCount = 0;
            foreach (var child in widgetsToSetFixedWidth)
            {
                if (child is FlowLayoutWidget flowLayoutWidget)
                {
                    maxChildCount = Math.Max(maxChildCount, flowLayoutWidget.Children.Count);
                }
            }

            // keep track of the largest width of each child
            var largestWidths = new double[maxChildCount];

            // for every child of widgetWithFlowLayouts that is a FlowLayoutWidget
            foreach (var child in widgetsToSetFixedWidth)
            {
                if (child is FlowLayoutWidget flowLayoutWidget)
                {
                    var index = 0;
                    // for every child of the flowLayoutWidget that is a GuiWidget
                    foreach (var flowChild in flowLayoutWidget.Children)
                    {
                        // if the child has a fixed width
                        if (IsSizeableChild(flowChild))
                        {
                            // if the width is larger than the largest width for this child
                            if (flowChild.Width > largestWidths[index])
                            {
                                // set the largest width for this child
                                largestWidths[flowLayoutWidget.Children.IndexOf(flowChild)] = flowChild.Width;
                            }
                        }
                        index++;
                    }
                }
            }

            // for every child of widgetWithFlowLayouts that is a FlowLayoutWidget
            foreach (var child in widgetsToSetFixedWidth)
            {
                if (child is FlowLayoutWidget flowLayoutWidget)
                {
                    // for every child of the flowLayoutWidget that is a GuiWidget
                    foreach (var flowChild in flowLayoutWidget.Children)
                    {
                        if (IsSizeableChild(flowChild))
                        {
                            // set the width to the largest width for this child
                            flowChild.Width = largestWidths[flowLayoutWidget.Children.IndexOf(flowChild)];
                        }
                    }
                }
            }
        }
    }
}