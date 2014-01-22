/*
Copyright (c) 2013, Lars Brubaker
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

using NUnit.Framework;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI.Tests
{
    public static class UnitTests
    {
        static bool ranTests = false;

        public static bool RanTests { get { return ranTests; } }
        public static void Run()
        {
            if (!ranTests)
            {
                // we depend on the graphics functions so make sure their tests succeed
                MatterHackers.Agg.Image.UnitTests.Run();

                MouseInteractionTests mouseInteractionTests = new MouseInteractionTests();
                mouseInteractionTests.ValidateSimpleLeftClick();
                mouseInteractionTests.ValidateOnlyTopWidgetGetsLeftClick();
                mouseInteractionTests.ValidateSimpleMouseUpDown();
                mouseInteractionTests.ValidateOnlyTopWidgetGetsMouseUp();
                mouseInteractionTests.ValidateEnterAndLeaveEvents();
                mouseInteractionTests.ValidateEnterAndLeaveEventsWhenNested();
                mouseInteractionTests.ValidateEnterAndLeaveEventsWhenCoverd();
                mouseInteractionTests.ValidateEnterAndLeaveInOverlapArea();
                mouseInteractionTests.MouseCapturedSpressesLeaveEvents();
                mouseInteractionTests.MouseCapturedSpressesLeaveEventsInButtonsSameAsRectangles();

                BackBufferTests backBufferTests = new BackBufferTests();
                //backBufferTests.saveImagesForDebug = true;
                backBufferTests.DoubleBufferTests();
                backBufferTests.BackBuffersAreScreenAligned();

                TextAndTextWidgetTests textTests = new TextAndTextWidgetTests();
                //textTests.saveImagesForDebug = true;
                textTests.TextWidgetVisibleTest();

                TextEditTests textEditTests = new TextEditTests();
                //TextEditTests.saveImagesForDebug = true;
                textEditTests.TextEditGetsFocusTests();
                textEditTests.TextEditTextSelectionTests();
                textEditTests.TextChangedEventsTests();
                textEditTests.NumEditHandlesNonNumberChars();
                textEditTests.TextEditingSpecialKeysWork();
                textEditTests.AddThenDeleteCausesNoVisualChange();
                textEditTests.MiltiLineTests();
                textEditTests.ScrollingToEndShowsEnd();

                AnchorTests anchorTests = new AnchorTests();
                //AnchorTests.saveImagesForDebug = true;
                anchorTests.SimpleFitToChildren();
                anchorTests.BottomAndTopSetAnchorBeforAddChild();
                anchorTests.BottomAndTop();
                anchorTests.CenterBothTests();
                anchorTests.CenterBothOffsetBoundsTests();
                anchorTests.AnchorLeftBottomTests();
                anchorTests.AnchorRightBottomTests();
                anchorTests.AnchorRightTopTests();
                anchorTests.AnchorAllTests();
                anchorTests.HCenterHRightAndVCenterVTopTests();
                anchorTests.GroupBoxResizeThenLayoutBeforeMatchChildren();

                FlowLayoutTests flowLayoutTests = new FlowLayoutTests();
                //FlowLayoutTests.saveImagesForDebug = true;
                flowLayoutTests.LeftToRightTests();
                flowLayoutTests.RightToLeftTests();
                flowLayoutTests.LeftToRightAnchorLeftBottomTests();
                flowLayoutTests.NestedLayoutTopToBottomTests();
                flowLayoutTests.NestedLayoutTopToBottomWithResizeTests();
                flowLayoutTests.AnchorLeftRightTests();
                flowLayoutTests.NestedFlowWidgetsTopToBottomTests();
                flowLayoutTests.NestedFlowWidgetsRightToLeftTests();
                flowLayoutTests.NestedFlowWidgetsLeftToRightTests();
                flowLayoutTests.LeftRightWithAnchorLeftRightChildTests();
                flowLayoutTests.RightLeftWithAnchorLeftRightChildTests();
                flowLayoutTests.BottomTopWithAnchorBottomTopChildTests();
                flowLayoutTests.TopBottomWithAnchorBottomTopChildTests();
                flowLayoutTests.EnsureFlowLayoutMinSizeFitsChildrenMinSize();
                flowLayoutTests.ChildVisibilityChangeCauseResize();
                flowLayoutTests.EnsureCorrectMinimumSize();
                flowLayoutTests.EnsureNestedAreMinimumSize();
                flowLayoutTests.EnsureCorrectSizeOnChildrenVisibleChange();
                flowLayoutTests.ChildHAnchorPriority();
                flowLayoutTests.TestVAnchorCenter();

                ScrollableWidgetTests scrollableWidgetTests = new ScrollableWidgetTests();
                //ScrollableWidgetTests.saveImagesForDebug = true;
                scrollableWidgetTests.LimitScrolToContetsTests();

                ListBoxTests listBoxTests = new ListBoxTests();
                //ListBoxTests.saveImagesForDebug = true;
                listBoxTests.SingleItemVisibleTest();
                listBoxTests.ScrollPositionStartsCorrect();

                MenuTests menuTests = new MenuTests();
                //MenuTests.saveImagesForDebug = true;
                menuTests.ListMenuTests();
                menuTests.DropDownListTests();

                ranTests = true;
            }
        }
    }
}
