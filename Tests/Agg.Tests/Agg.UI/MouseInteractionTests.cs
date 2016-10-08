﻿/*
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

#if !__ANDROID__

using MatterHackers.GuiAutomation;

#endif

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class MouseInteractionTests
	{
#if !__ANDROID__
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void DoClickButtonInWindow()
		{
			int leftClickCount = 0;
			int rightClickCount = 0;

			Action<AutomationRunner> testToRun = (testRunner) =>
			{
				// Now do the actions specific to this test. (replace this for new tests)
				testRunner.ClickByName("left", 1);
				testRunner.Wait(.5);

				testRunner.AddTestResult(leftClickCount == 1, "Got left button click");

				testRunner.ClickByName("right", 1);
				testRunner.Wait(.5);

				testRunner.AddTestResult(rightClickCount == 1, "Got right button click");

				testRunner.DragDropByName("left", "right", offsetDrag: new Point2D(1, 0));
				testRunner.Wait(.5);

				testRunner.AddTestResult(leftClickCount == 1, "Mouse down not a click");
			};

			SystemWindow buttonContainer = new SystemWindow(300, 200);

			Button leftButton = new Button("left", 10, 40);
			leftButton.Name = "left";
			leftButton.Click += (sender, e) => { leftClickCount++; };
			buttonContainer.AddChild(leftButton);
			Button rightButton = new Button("right", 110, 40);
			rightButton.Click += (sender, e) => { rightClickCount++; };
			rightButton.Name = "right";
			buttonContainer.AddChild(rightButton);

			var testHarness = AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, testToRun, 10);

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}

#endif

		[Test]
		public void ExtensionMethodsTests()
		{
			GuiWidget level0 = new GuiWidget() { Name = "level0" };
			GuiWidget level1 = new GuiWidget() { Name = "level1" };
			level0.AddChild(level1);
			GuiWidget level2 = new GuiWidget() { Name = "level2" };
			level1.AddChild(level2);
			GuiWidget level3 = new GuiWidget() { Name = "level3" };
			level2.AddChild(level3);
			List<GuiWidget> allWidgets = new List<GuiWidget>() { level0, level1, level2, level3 };

			foreach (var child in level0.Children<GuiWidget>())
			{
				Assert.IsTrue(child == allWidgets[1]);
			}
			foreach (var child in level1.Children<GuiWidget>())
			{
				Assert.IsTrue(child == allWidgets[2]);
			}
			foreach (var child in level2.Children<GuiWidget>())
			{
				Assert.IsTrue(child == allWidgets[3]);
			}
			foreach (var child in level3.Children<GuiWidget>())
			{
				Assert.IsTrue(false); // there are no children we should not get here
			}

			int index = allWidgets.Count-1;
			int parentCount = 0;
			foreach (var parent in level3.Parents<GuiWidget>())
			{
				parentCount++;
                Assert.IsTrue(parent == allWidgets[--index]);
			}
			Assert.IsTrue(parentCount == 3);
		}

		[Test]
		public void ValidateSimpleLeftClick()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "Container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			Button button = new Button("Test", 100, 100);
			button.Name = "button";
			bool gotClick = false;
			button.Click += (sender, e) => { gotClick = true; };
			container.AddChild(button);

			Assert.IsTrue(gotClick == false);
			Assert.IsTrue(button.Focused == false);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(gotClick == false);
			Assert.IsTrue(button.Focused == false);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(gotClick == false);
			Assert.IsTrue(button.Focused == true, "Down click triggers focused.");

			Assert.IsTrue(gotClick == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.IsTrue(gotClick == true);
			Assert.IsTrue(button.Focused == true);

			gotClick = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(gotClick == false);
			Assert.IsTrue(button.Focused == false);
		}

		[Test]
		public void ValidateOnlyTopWidgetGetsLeftClick()
		{
			bool gotClick = false;
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			Button button = new Button("Test", 100, 100);
			button.Name = "button";
			button.Click += (sender, e) => { gotClick = true; };
			container.AddChild(button);

			GuiWidget blockingWidegt = new GuiWidget();
			blockingWidegt.Name = "blockingWidegt";
			blockingWidegt.LocalBounds = new RectangleDouble(105, 105, 125, 125);
			container.AddChild(blockingWidegt);

			// the widget is not in the way
			Assert.IsTrue(gotClick == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			Assert.IsTrue(container.MouseCaptured == false);
			Assert.IsTrue(blockingWidegt.MouseCaptured == false);
			Assert.IsTrue(container.ChildHasMouseCaptured == true);
			Assert.IsTrue(blockingWidegt.ChildHasMouseCaptured == false);
			Assert.IsTrue(button.MouseCaptured == true);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			Assert.IsTrue(container.MouseCaptured == false);
			Assert.IsTrue(blockingWidegt.MouseCaptured == false);
			Assert.IsTrue(button.MouseCaptured == false);
			Assert.IsTrue(gotClick == true);

			gotClick = false;

			// the widget is in the way
			Assert.IsTrue(gotClick == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.IsTrue(container.MouseCaptured == false);
			Assert.IsTrue(blockingWidegt.MouseCaptured == true);
			Assert.IsTrue(button.MouseCaptured == false);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.IsTrue(container.MouseCaptured == false);
			Assert.IsTrue(blockingWidegt.MouseCaptured == false);
			Assert.IsTrue(button.MouseCaptured == false);
			Assert.IsTrue(gotClick == false);
		}

		[Test]
		public void ValidateSimpleMouseUpDown()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			int containerGotMouseUp = 0;
			int containerGotMouseDown = 0;
			int containerGotMouseDownInBounds = 0;
			container.MouseUp += (sender, e) => { containerGotMouseUp++; };
			container.MouseDown += (sender, e) => { containerGotMouseDown++; };
			container.MouseDownInBounds += (sender, e) => { containerGotMouseDownInBounds++; };
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			GuiWidget topWidget = new GuiWidget();
			topWidget.Name = "topWidget";
			topWidget.LocalBounds = new RectangleDouble(100, 100, 150, 150);
			int topWidgetGotMouseUp = 0;
			int topWidgetGotMouseDown = 0;
			int topWidgetGotMouseDownInBounds = 0;
			topWidget.MouseUp += (sender, e) => { topWidgetGotMouseUp++; };
			topWidget.MouseDown += (sender, e) => { topWidgetGotMouseDown++; };
			topWidget.MouseDownInBounds += (sender, e) => { topWidgetGotMouseDownInBounds++; };
			container.AddChild(topWidget);

			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 0);
			// down outside everything
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, -10, -10, 0));
			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 0);
			Assert.IsTrue(containerGotMouseDown == 0);
			Assert.IsTrue(topWidgetGotMouseDown == 0);
			Assert.IsTrue(containerGotMouseDownInBounds == 0);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			// up outside everything
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, -10, -10, 0));
			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 0);
			Assert.IsTrue(containerGotMouseDown == 0);
			Assert.IsTrue(topWidgetGotMouseDown == 0);
			Assert.IsTrue(containerGotMouseDownInBounds == 0);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			// down on container
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 0);
			Assert.IsTrue(containerGotMouseDown == 1);
			Assert.IsTrue(topWidgetGotMouseDown == 0);
			Assert.IsTrue(containerGotMouseDownInBounds == 1);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(containerGotMouseUp == 1);
			Assert.IsTrue(topWidgetGotMouseUp == 0);
			Assert.IsTrue(containerGotMouseDown == 0);
			Assert.IsTrue(topWidgetGotMouseDown == 0);
			Assert.IsTrue(containerGotMouseDownInBounds == 0);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 0);
			Assert.IsTrue(containerGotMouseDown == 0);
			Assert.IsTrue(topWidgetGotMouseDown == 1);
			Assert.IsTrue(containerGotMouseDownInBounds == 1);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 1);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 1);
			Assert.IsTrue(containerGotMouseDown == 0);
			Assert.IsTrue(topWidgetGotMouseDown == 0);
			Assert.IsTrue(containerGotMouseDownInBounds == 0);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 0);

			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.IsTrue(containerGotMouseUp == 0);
			Assert.IsTrue(topWidgetGotMouseUp == 1);
			Assert.IsTrue(containerGotMouseDown == 0);
			Assert.IsTrue(topWidgetGotMouseDown == 1);
			Assert.IsTrue(containerGotMouseDownInBounds == 1);
			Assert.IsTrue(topWidgetGotMouseDownInBounds == 1);
		}

		[Test]
		public void ValidateOnlyTopWidgetGetsMouseUp()
		{
			bool topGotMouseUp = false;
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			GuiWidget topWidget = new GuiWidget();
			topWidget.Name = "topWidget";
			topWidget.LocalBounds = new RectangleDouble(100, 100, 150, 150);
			topWidget.MouseUp += (sender, e) => { topGotMouseUp = true; };

			container.AddChild(topWidget);

			bool blockingGotMouseUp = false;
			GuiWidget blockingWidegt = new GuiWidget();
			blockingWidegt.Name = "blockingWidegt";
			blockingWidegt.MouseUp += (sender, e) => { blockingGotMouseUp = true; };
			blockingWidegt.LocalBounds = new RectangleDouble(105, 105, 125, 125);
			container.AddChild(blockingWidegt);

			// the widget is not in the way
			Assert.IsTrue(topGotMouseUp == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			Assert.IsTrue(blockingGotMouseUp == false);
			Assert.IsTrue(topGotMouseUp == true);

			topGotMouseUp = false;

			// the widget is in the way
			Assert.IsTrue(topGotMouseUp == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.IsTrue(blockingGotMouseUp == true);
			Assert.IsTrue(topGotMouseUp == false);
		}

		[Test]
		public void ValidateEnterAndLeaveEvents()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);

			GuiWidget regionA = new GuiWidget();
			regionA.Name = "regionA";
			regionA.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			int gotEnter = 0;
			int gotLeave = 0;
			regionA.MouseEnter += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnter++; };
			regionA.MouseLeave += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeave++; };
			int gotEnterBounds = 0;
			int gotLeaveBounds = 0;
			regionA.MouseEnterBounds += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBounds++; };
			regionA.MouseLeaveBounds += (sender, e) => { if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBounds++; };
			container.AddChild(regionA);

			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);

			// put the mouse into the widget but outside regionA
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.NotUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);
			// move it into regionA
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 1);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 1);
			// now move it inside regionA and make sure it does not re-trigger either event
			gotEnter = 0;
			gotEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);

			// now leave and make sure we see the leave
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, -5, -5, 0));
			Assert.IsTrue(gotLeave == 1);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 1);
			Assert.IsTrue(gotEnterBounds == 0);

			// move back on
			gotLeave = gotEnter = gotLeaveBounds = gotEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			// now leave only the inside widget and make sure we see the leave
			Assert.IsTrue(gotEnter == 1);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 1);
			gotLeave = gotEnter = gotLeaveBounds = gotEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(gotLeave == 1);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 1);
			Assert.IsTrue(gotEnterBounds == 0);
		}

		[Test, Category("FixNeeded" /* Functionality needs to be implemented */)]
		public void ValidateEnterLeaveOnWidgetMoves()
		{
			GuiWidget container = new SystemWindow(200, 200);
			container.Name = "container";

			GuiWidget regionA = new GuiWidget();
			regionA.Name = "regionA";
			regionA.BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180);
			regionA.OriginRelativeParent = new VectorMath.Vector2(10, 10);
			int gotEnter = 0;
			int gotLeave = 0;
			regionA.MouseEnter += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnter++; };
			regionA.MouseLeave += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeave++; };
			int gotEnterBounds = 0;
			int gotLeaveBounds = 0;
			regionA.MouseEnterBounds += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBounds++; };
			regionA.MouseLeaveBounds += (sender, e) => { if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBounds++; };
			container.AddChild(regionA);

			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);

			// put the mouse into the widget but outside regionA
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.NotUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);
			// move regionA under mouse
			regionA.OriginRelativeParent = new VectorMath.Vector2(0, 0);
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 1);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 1);
			// now regionA and make sure it does not re-trigger either event
			gotEnter = 0;
			gotEnterBounds = 0;
			regionA.OriginRelativeParent = new VectorMath.Vector2(1, 1);
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);

			// now move out from under mouse and make sure we see the leave
			regionA.OriginRelativeParent = new VectorMath.Vector2(10, 10);
			Assert.IsTrue(gotLeave == 1);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 1);
			Assert.IsTrue(gotEnterBounds == 0);

			// move back under
			gotLeave = gotEnter = gotLeaveBounds = gotEnterBounds = 0;
			regionA.OriginRelativeParent = new VectorMath.Vector2(0, 0);
			Assert.IsTrue(gotEnter == 1);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 1);
		}

		[Test, Category("FixNeeded" /* Functionality needs to be implemented */)]
		public void ValidateEnterLeaveOnWidgetBoundsChange()
		{
			GuiWidget container = new SystemWindow(200, 200);
			container.Name = "container";

			GuiWidget regionA = new GuiWidget();
			regionA.Name = "regionA";
			regionA.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			int gotEnter = 0;
			int gotLeave = 0;
			regionA.MouseEnter += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnter++; };
			regionA.MouseLeave += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeave++; };
			int gotEnterBounds = 0;
			int gotLeaveBounds = 0;
			regionA.MouseEnterBounds += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBounds++; };
			regionA.MouseLeaveBounds += (sender, e) => { if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBounds++; };
			container.AddChild(regionA);

			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);

			// put the mouse into the widget but outside regionA
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.NotUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);
			// move regionA under mouse
			regionA.BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180);
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 1);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 1);
			// now regionA and make sure it does not re-trigger either event
			gotEnter = 0;
			gotEnterBounds = 0;
			regionA.BoundsRelativeToParent = new RectangleDouble(1, 1, 181, 181);
			Assert.IsTrue(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 0);

			// now move out from under mouse and make sure we see the leave
			regionA.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			Assert.IsTrue(gotLeave == 1);
			Assert.IsTrue(gotEnter == 0);
			Assert.IsTrue(gotLeaveBounds == 1);
			Assert.IsTrue(gotEnterBounds == 0);

			// move back under
			gotLeave = gotEnter = gotLeaveBounds = gotEnterBounds = 0;
			regionA.BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180);
			Assert.IsTrue(gotEnter == 1);
			Assert.IsTrue(gotLeave == 0);
			Assert.IsTrue(gotLeaveBounds == 0);
			Assert.IsTrue(gotEnterBounds == 1);
		}

		public RadioButton GenerateRadioButton(string label)
		{
			BorderDouble internalMargin = new BorderDouble(0);
			TextWidget nomalState = new TextWidget(label);
			TextWidget hoverState = new TextWidget(label);
			TextWidget checkingState = new TextWidget(label);
			TextWidget checkedState = new TextWidget(label);
			TextWidget disabledState = new TextWidget(label);
			RadioButtonViewStates checkBoxButtonViewWidget = new RadioButtonViewStates(nomalState, hoverState, checkingState, checkedState, disabledState);
			RadioButton radioButton = new RadioButton(checkBoxButtonViewWidget);
			radioButton.Margin = new BorderDouble();
			return radioButton;
		}

		[Test]
		public void ValidateEnterAndLeaveEventsWhenNested()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "continer";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);

			GuiWidget regionA = new GuiWidget();
			regionA.Name = "regionA";
			regionA.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			int gotEnterA = 0;
			int gotLeaveA = 0;
			regionA.MouseEnter += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterA++; };
			regionA.MouseLeave += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveA++; };
			int gotEnterBoundsA = 0;
			int gotLeaveBoundsA = 0;
			regionA.MouseEnterBounds += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBoundsA++; };
			regionA.MouseLeaveBounds += (sender, e) => { if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBoundsA++; };

			GuiWidget regionB = new GuiWidget();
			regionB.Name = "regionB";
			regionB.AddChild(regionA);
			regionB.SetBoundsToEncloseChildren();
			container.AddChild(regionB);
			int gotEnterB = 0;
			int gotLeaveB = 0;
			regionB.MouseEnter += (sender, e) => { if (regionB.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterB++; };
			regionB.MouseLeave += (sender, e) => { if (regionB.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveB++; };
			int gotEnterBoundsB = 0;
			int gotLeaveBoundsB = 0;
			regionB.MouseEnterBounds += (sender, e) => { if (regionB.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBoundsB++; };
			regionB.MouseLeaveBounds += (sender, e) => { if (regionB.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBoundsB++; };

			Assert.IsTrue(gotLeaveA == 0);
			Assert.IsTrue(gotEnterA == 0);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);

			// put the mouse into the widget but outside regionA and region B
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(gotLeaveA == 0);
			Assert.IsTrue(gotEnterA == 0);
			Assert.IsTrue(gotLeaveBoundsA == 0);
			Assert.IsTrue(gotEnterBoundsA == 0);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);
			Assert.IsTrue(gotLeaveBoundsB == 0);
			Assert.IsTrue(gotEnterBoundsB == 0);

			// move it into regionA
			gotEnterA = 0; gotEnterBoundsA = 0; gotLeaveA = 0; gotLeaveBoundsA = 0;
			gotEnterB = 0; gotEnterBoundsB = 0; gotLeaveB = 0; gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(gotLeaveA == 0);
			Assert.IsTrue(gotEnterA == 1);
			Assert.IsTrue(gotLeaveBoundsA == 0);
			Assert.IsTrue(gotEnterBoundsA == 1);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);
			Assert.IsTrue(gotLeaveBoundsB == 0);
			Assert.IsTrue(gotEnterBoundsB == 1);

			// now move it inside regionA and make sure it does not re-trigger either event
			gotEnterA = 0; gotEnterBoundsA = 0; gotLeaveA = 0; gotLeaveBoundsA = 0;
			gotEnterB = 0; gotEnterBoundsB = 0; gotLeaveB = 0; gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			Assert.IsTrue(gotLeaveA == 0);
			Assert.IsTrue(gotEnterA == 0);
			Assert.IsTrue(gotLeaveBoundsA == 0);
			Assert.IsTrue(gotEnterBoundsA == 0);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);
			Assert.IsTrue(gotLeaveBoundsB == 0);
			Assert.IsTrue(gotEnterBoundsB == 0);

			// now leave and make sure we see the leave
			gotEnterA = 0; gotEnterBoundsA = 0; gotLeaveA = 0; gotLeaveBoundsA = 0;
			gotEnterB = 0; gotEnterBoundsB = 0; gotLeaveB = 0; gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, -5, -5, 0));
			Assert.IsTrue(gotLeaveA == 1);
			Assert.IsTrue(gotEnterA == 0);
			Assert.IsTrue(gotLeaveBoundsA == 1);
			Assert.IsTrue(gotEnterBoundsA == 0);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);
			Assert.IsTrue(gotLeaveBoundsB == 1);
			Assert.IsTrue(gotEnterBoundsB == 0);

			// move back on
			gotEnterA = 0; gotEnterBoundsA = 0; gotLeaveA = 0; gotLeaveBoundsA = 0;
			gotEnterB = 0; gotEnterBoundsB = 0; gotLeaveB = 0; gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			// now leave only the inside widget and make sure we see the leave
			Assert.IsTrue(gotEnterA == 1);
			Assert.IsTrue(gotLeaveA == 0);
			Assert.IsTrue(gotLeaveBoundsA == 0);
			Assert.IsTrue(gotEnterBoundsA == 1);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);
			Assert.IsTrue(gotLeaveBoundsB == 0);
			Assert.IsTrue(gotEnterBoundsB == 1);

			// and a final leave
			gotEnterA = 0; gotEnterBoundsA = 0; gotLeaveA = 0; gotLeaveBoundsA = 0;
			gotEnterB = 0; gotEnterBoundsB = 0; gotLeaveB = 0; gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(gotLeaveA == 1);
			Assert.IsTrue(gotEnterA == 0);
			Assert.IsTrue(gotLeaveBoundsA == 1);
			Assert.IsTrue(gotEnterBoundsA == 0);
			Assert.IsTrue(gotLeaveB == 0);
			Assert.IsTrue(gotEnterB == 0);
			Assert.IsTrue(gotLeaveBoundsB == 1);
			Assert.IsTrue(gotEnterBoundsB == 0);
		}

		[Test]
		public void ValidateEnterAndLeaveEventsWhenCoverd()
		{
			// A widget contains two children the second completely covering the first.
			// When the mouse moves into the first it should not receive an enter event only a bounds enter event.
			// When the mouse move out of the first it should receive only a bounds exit, not an exit.
			GuiWidget container = new GuiWidget();
			container.Name = "continer";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);

			GuiWidget coveredWidget = new GuiWidget();
			coveredWidget.Name = "coveredWidget";
			coveredWidget.BoundsRelativeToParent = new RectangleDouble(20, 20, 180, 180);
			int gotEnterCovered = 0;
			int gotLeaveCovered = 0;
			coveredWidget.MouseEnter += (sender, e) => { if (coveredWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterCovered++; };
			coveredWidget.MouseLeave += (sender, e) => { if (coveredWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveCovered++; };
			int gotEnterBoundsCovered = 0;
			int gotLeaveBoundsCovered = 0;
			coveredWidget.MouseEnterBounds += (sender, e) => { if (coveredWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBoundsCovered++; };
			coveredWidget.MouseLeaveBounds += (sender, e) => { if (coveredWidget.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBoundsCovered++; };
			container.AddChild(coveredWidget);

			GuiWidget coveredChildWidget = new GuiWidget();
			coveredChildWidget.Name = "coveredChildWidget";
			int gotEnterCoveredChild = 0;
			int gotLeaveCoveredChild = 0;
			coveredChildWidget.MouseEnter += (sender, e) => { if (coveredChildWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterCoveredChild++; };
			coveredChildWidget.MouseLeave += (sender, e) => { if (coveredChildWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveCoveredChild++; };
			int gotEnterBoundsCoveredChild = 0;
			int gotLeaveBoundsCoveredChild = 0;
			coveredChildWidget.MouseEnterBounds += (sender, e) => { if (coveredChildWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBoundsCoveredChild++; };
			coveredChildWidget.MouseLeaveBounds += (sender, e) => { if (coveredChildWidget.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBoundsCoveredChild++; };
			coveredWidget.AddChild(coveredChildWidget);
			coveredChildWidget.BoundsRelativeToParent = coveredWidget.LocalBounds;

			GuiWidget coverWidget = new GuiWidget();
			coverWidget.Name = "coverWidget";
			coverWidget.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			int gotEnterCover = 0;
			int gotLeaveCover = 0;
			coverWidget.MouseEnter += (sender, e) => { if (coverWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterCover++; };
			coverWidget.MouseLeave += (sender, e) => { if (coverWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveCover++; };
			int gotEnterBoundsCover = 0;
			int gotLeaveBoundsCover = 0;
			coverWidget.MouseEnterBounds += (sender, e) => { if (coverWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); gotEnterBoundsCover++; };
			coverWidget.MouseLeaveBounds += (sender, e) => { if (coverWidget.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); gotLeaveBoundsCover++; };
			container.AddChild(coverWidget);

			Assert.IsTrue(gotLeaveCover == 0);
			Assert.IsTrue(gotEnterCover == 0);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);

			// put the mouse into the widget but outside the children
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(gotLeaveCover == 0);
			Assert.IsTrue(gotEnterCover == 0);
			Assert.IsTrue(gotLeaveBoundsCover == 0);
			Assert.IsTrue(gotEnterBoundsCover == 0);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveBoundsCovered == 0);
			Assert.IsTrue(gotEnterBoundsCovered == 0);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);
			Assert.IsTrue(gotLeaveBoundsCoveredChild == 0);
			Assert.IsTrue(gotEnterBoundsCoveredChild == 0);

			// move it into the cover
			gotEnterCover = 0; gotEnterBoundsCover = 0; gotLeaveCover = 0; gotLeaveBoundsCover = 0;
			gotEnterCovered = 0; gotEnterBoundsCovered = 0; gotLeaveCovered = 0; gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0; gotEnterBoundsCoveredChild = 0; gotLeaveCoveredChild = 0; gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(gotLeaveCover == 0);
			Assert.IsTrue(gotEnterCover == 1);
			Assert.IsTrue(gotLeaveBoundsCover == 0);
			Assert.IsTrue(gotEnterBoundsCover == 1);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveBoundsCovered == 0);
			Assert.IsTrue(gotEnterBoundsCovered == 0);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);
			Assert.IsTrue(gotLeaveBoundsCoveredChild == 0);
			Assert.IsTrue(gotEnterBoundsCoveredChild == 0);

			// now move it inside cover and make sure it does not re-trigger either event
			gotEnterCover = 0; gotEnterBoundsCover = 0; gotLeaveCover = 0; gotLeaveBoundsCover = 0;
			gotEnterCovered = 0; gotEnterBoundsCovered = 0; gotLeaveCovered = 0; gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0; gotEnterBoundsCoveredChild = 0; gotLeaveCoveredChild = 0; gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			Assert.IsTrue(gotLeaveCover == 0);
			Assert.IsTrue(gotEnterCover == 0);
			Assert.IsTrue(gotLeaveBoundsCover == 0);
			Assert.IsTrue(gotEnterBoundsCover == 0);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveBoundsCovered == 0);
			Assert.IsTrue(gotEnterBoundsCovered == 0);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);
			Assert.IsTrue(gotLeaveBoundsCoveredChild == 0);
			Assert.IsTrue(gotEnterBoundsCoveredChild == 0);

			// now leave and make sure we see the leave
			gotEnterCover = 0; gotEnterBoundsCover = 0; gotLeaveCover = 0; gotLeaveBoundsCover = 0;
			gotEnterCovered = 0; gotEnterBoundsCovered = 0; gotLeaveCovered = 0; gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0; gotEnterBoundsCoveredChild = 0; gotLeaveCoveredChild = 0; gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(gotLeaveCover == 1);
			Assert.IsTrue(gotEnterCover == 0);
			Assert.IsTrue(gotLeaveBoundsCover == 1);
			Assert.IsTrue(gotEnterBoundsCover == 0);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveBoundsCovered == 0);
			Assert.IsTrue(gotEnterBoundsCovered == 0);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);
			Assert.IsTrue(gotLeaveBoundsCoveredChild == 0);
			Assert.IsTrue(gotEnterBoundsCoveredChild == 0);

			// now enter the covered and make sure we only see bounds enter
			gotEnterCover = 0; gotEnterBoundsCover = 0; gotLeaveCover = 0; gotLeaveBoundsCover = 0;
			gotEnterCovered = 0; gotEnterBoundsCovered = 0; gotLeaveCovered = 0; gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0; gotEnterBoundsCoveredChild = 0; gotLeaveCoveredChild = 0; gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 25, 25, 0));
			// now leave only the inside widget and make sure we see the leave
			Assert.IsTrue(gotEnterCover == 1);
			Assert.IsTrue(gotLeaveCover == 0);
			Assert.IsTrue(gotLeaveBoundsCover == 0);
			Assert.IsTrue(gotEnterBoundsCover == 1);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveBoundsCovered == 0);
			Assert.IsTrue(gotEnterBoundsCovered == 1);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);
			Assert.IsTrue(gotLeaveBoundsCoveredChild == 0);
			Assert.IsTrue(gotEnterBoundsCoveredChild == 1);

			// and a final leave and make sure we only see bounds leave
			gotEnterCover = 0; gotEnterBoundsCover = 0; gotLeaveCover = 0; gotLeaveBoundsCover = 0;
			gotEnterCovered = 0; gotEnterBoundsCovered = 0; gotLeaveCovered = 0; gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0; gotEnterBoundsCoveredChild = 0; gotLeaveCoveredChild = 0; gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.IsTrue(gotLeaveCover == 1);
			Assert.IsTrue(gotEnterCover == 0);
			Assert.IsTrue(gotLeaveBoundsCover == 1);
			Assert.IsTrue(gotEnterBoundsCover == 0);
			Assert.IsTrue(gotLeaveCovered == 0);
			Assert.IsTrue(gotEnterCovered == 0);
			Assert.IsTrue(gotLeaveBoundsCovered == 1);
			Assert.IsTrue(gotEnterBoundsCovered == 0);
			Assert.IsTrue(gotLeaveCoveredChild == 0);
			Assert.IsTrue(gotEnterCoveredChild == 0);
			Assert.IsTrue(gotLeaveBoundsCoveredChild == 1);
			Assert.IsTrue(gotEnterBoundsCoveredChild == 0);
		}

		[Test]
		public void ValidateEnterAndLeaveInOverlapArea()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);

			GuiWidget bottomWidget = new GuiWidget();
			bottomWidget.Name = "bottom";
			bottomWidget.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);

			int bottomGotEnter = 0;
			int bottomGotLeave = 0;
			bottomWidget.MouseEnter += (sender, e) => { if (bottomWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); bottomGotEnter++; };
			bottomWidget.MouseLeave += (sender, e) => { if (bottomWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be the first under the mouse."); bottomGotLeave++; };
			int bottomGotEnterBounds = 0;
			int bottomGotLeaveBounds = 0;
			bottomWidget.MouseEnterBounds += (sender, e) => { if (bottomWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); bottomGotEnterBounds++; };
			bottomWidget.MouseLeaveBounds += (sender, e) => { if (bottomWidget.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); bottomGotLeaveBounds++; };
			container.AddChild(bottomWidget);

			GuiWidget topWidget = new GuiWidget();
			topWidget.Name = "top";
			topWidget.BoundsRelativeToParent = new RectangleDouble(5, 20, 190, 190);
			int topGotEnter = 0;
			int topGotLeave = 0;
			topWidget.MouseEnter += (sender, e) => { if (topWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); topGotEnter++; };
			topWidget.MouseLeave += (sender, e) => { if (topWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be the first under the mouse."); topGotLeave++; };
			int topGotEnterBounds = 0;
			int topGotLeaveBounds = 0;
			topWidget.MouseEnterBounds += (sender, e) => { if (topWidget.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); topGotEnterBounds++; };
			topWidget.MouseLeaveBounds += (sender, e) => { if (topWidget.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); topGotLeaveBounds++; };
			container.AddChild(topWidget);

			Assert.IsTrue(topGotEnter == 0);
			Assert.IsTrue(topGotLeave == 0);
			Assert.IsTrue(topGotEnterBounds == 0);
			Assert.IsTrue(topGotLeaveBounds == 0);

			// move into the bottom widget only
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 15, 0));
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(bottomGotLeave == 0);
			Assert.IsTrue(bottomGotEnter == 1);
			Assert.IsTrue(bottomGotLeaveBounds == 0);
			Assert.IsTrue(bottomGotEnterBounds == 1);
			Assert.IsTrue(topGotLeave == 0);
			Assert.IsTrue(topGotEnter == 0);
			Assert.IsTrue(topGotLeaveBounds == 0);
			Assert.IsTrue(topGotEnterBounds == 0);

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move out of the bottom widget only
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 15, 0));
			Assert.IsTrue(bottomGotLeave == 1);
			Assert.IsTrue(bottomGotEnter == 0);
			Assert.IsTrue(bottomGotLeaveBounds == 1);
			Assert.IsTrue(bottomGotEnterBounds == 0);
			Assert.IsTrue(topGotLeave == 0);
			Assert.IsTrue(topGotEnter == 0);
			Assert.IsTrue(topGotLeaveBounds == 0);
			Assert.IsTrue(topGotEnterBounds == 0);

			// move to just outside both widgets
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 25, 0));
			Assert.IsTrue(bottomWidget.TransformToScreenSpace(bottomWidget.LocalBounds).Contains(1, 25) == false);
			Assert.IsTrue(topWidget.TransformToScreenSpace(topWidget.LocalBounds).Contains(1, 25) == false);
			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move over the top widget when it is over the bottom widget (only the top should see this)
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 25, 0));
			Assert.IsTrue(bottomGotEnter == 0);
			Assert.IsTrue(bottomGotLeave == 0);
			Assert.IsTrue(bottomGotEnterBounds == 1);
			Assert.IsTrue(bottomGotLeaveBounds == 0);
			Assert.IsTrue(topGotEnter == 1);
			Assert.IsTrue(topGotLeave == 0);
			Assert.IsTrue(topGotEnterBounds == 1);
			Assert.IsTrue(topGotLeaveBounds == 0);

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move out of the top widget into the bottom
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(bottomGotEnter == 1);
			Assert.IsTrue(bottomGotLeave == 0);
			Assert.IsTrue(bottomGotEnterBounds == 0);
			Assert.IsTrue(bottomGotLeaveBounds == 0);
			Assert.IsTrue(topGotEnter == 0);
			Assert.IsTrue(topGotLeave == 1);
			Assert.IsTrue(topGotEnterBounds == 0);
			Assert.IsTrue(topGotLeaveBounds == 1);

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move back up into the top and make sure we see the leave in the bottom
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 25, 0));
			Assert.IsTrue(bottomGotEnter == 0);
			Assert.IsTrue(bottomGotLeave == 1);
			Assert.IsTrue(bottomGotEnterBounds == 0);
			Assert.IsTrue(bottomGotLeaveBounds == 0);
			Assert.IsTrue(topGotEnter == 1);
			Assert.IsTrue(topGotLeave == 0);
			Assert.IsTrue(topGotEnterBounds == 1);
			Assert.IsTrue(topGotLeaveBounds == 0);
		}

		[Test]
		public void MouseCapturedSpressesLeaveEvents()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "continer";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);

			GuiWidget regionA = new GuiWidget();
			regionA.Name = "regionA";
			regionA.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			container.AddChild(regionA);
			int aGotEnter = 0;
			int aGotLeave = 0;
			int aGotEnterBounds = 0;
			int aGotLeaveBounds = 0;
			int aGotMove = 0;
			int aGotUp = 0;
			regionA.MouseEnter += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); aGotEnter++; };
			regionA.MouseLeave += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); aGotLeave++; };
			regionA.MouseEnterBounds += (sender, e) => { if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); aGotEnterBounds++; };
			regionA.MouseLeaveBounds += (sender, e) => { if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); aGotLeaveBounds++; };
			regionA.MouseMove += (sender, e) => { aGotMove++; };
			regionA.MouseUp += (sender, e) => { aGotUp++; };

			// make sure we know we are entered and captued on a down event
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(regionA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 1);
			Assert.IsTrue(aGotLeave == 0);
			Assert.IsTrue(aGotEnterBounds == 1);
			Assert.IsTrue(aGotLeaveBounds == 0);
			Assert.IsTrue(aGotMove == 0);

			// make sure we stay on top when internal moves occure
			aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 16, 0));
			Assert.IsTrue(regionA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 0);
			Assert.IsTrue(aGotLeave == 0);
			Assert.IsTrue(aGotEnterBounds == 0);
			Assert.IsTrue(aGotLeaveBounds == 0);
			Assert.IsTrue(aGotMove == 1);

			// make sure we see leave events when captured
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.IsTrue(container.FirstWidgetUnderMouse == false);
			Assert.IsTrue(regionA.FirstWidgetUnderMouse == false);
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 0);
			Assert.IsTrue(aGotLeave == 1);
			Assert.IsTrue(aGotEnterBounds == 0);
			Assert.IsTrue(aGotLeaveBounds == 1);
			Assert.IsTrue(aGotMove == 1);

			// make sure we see enter events when captured
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(regionA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 1);
			Assert.IsTrue(aGotLeave == 0);
			Assert.IsTrue(aGotEnterBounds == 1);
			Assert.IsTrue(aGotLeaveBounds == 0);
			Assert.IsTrue(aGotMove == 1);

			// and we are not captured after mouseup above region
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(regionA.MouseCaptured == false);
			Assert.IsTrue(aGotEnter == 0);
			Assert.IsTrue(aGotLeave == 0);
			Assert.IsTrue(aGotEnterBounds == 0);
			Assert.IsTrue(aGotLeaveBounds == 0);
			Assert.IsTrue(aGotMove == 0);
			Assert.IsTrue(aGotUp == 1, "When we are captured we need to see mouse up messages.");

			// make sure we are not captured after mouseup above off region
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 0, "we are already in the button from the last move");
			Assert.IsTrue(aGotLeave == 0);
			Assert.IsTrue(aGotEnterBounds == 0, "we are already in the button from the last move");
			Assert.IsTrue(aGotLeaveBounds == 0);
			Assert.IsTrue(aGotMove == 0);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.IsTrue(regionA.MouseCaptured == false);
			Assert.IsTrue(aGotEnter == 0);
			Assert.IsTrue(aGotLeave == 1, "During the mouse up we also happen to be off the widget.  Need to get a mouse leave event.");
			Assert.IsTrue(aGotEnterBounds == 0);
			Assert.IsTrue(aGotLeaveBounds == 1, "During the mouse up we also happen to be off the widget.  Need to get a mouse leave event.");
			Assert.IsTrue(aGotMove == 0);
			Assert.IsTrue(aGotUp == 1, "When we are captured we need to see mouse up messages.");

			// when captured make sure we see move events even when they are not above us.
			GuiWidget regionB = new GuiWidget();
			regionB.Name = "regionB";
			regionB.BoundsRelativeToParent = new RectangleDouble(20, 20, 180, 180);
			container.AddChild(regionB);
			int bGotEnter = 0;
			int bGotLeave = 0;
			int bGotEnterBounds = 0;
			int bGotLeaveBounds = 0;
			int bGotMove = 0;
			regionB.MouseEnter += (sender, e) => { if (regionB.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); bGotEnter++; };
			regionB.MouseLeave += (sender, e) => { if (regionB.UnderMouseState == UnderMouseState.FirstUnderMouse) throw new Exception("It must not be under the mouse."); bGotLeave++; };
			regionB.MouseEnterBounds += (sender, e) => { if (regionB.UnderMouseState == UnderMouseState.NotUnderMouse) throw new Exception("It must be under the mouse."); bGotEnterBounds++; };
			regionB.MouseLeaveBounds += (sender, e) => { if (regionB.UnderMouseState != UnderMouseState.NotUnderMouse) throw new Exception("It must not be under the mouse."); bGotLeaveBounds++; };
			regionB.MouseMove += (sender, e) => { bGotMove++; };

			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			// when captured regionA make sure regionB can not see move events
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 1);
			Assert.IsTrue(aGotLeave == 0);
			Assert.IsTrue(aGotEnterBounds == 1);
			Assert.IsTrue(aGotLeaveBounds == 0);
			Assert.IsTrue(aGotMove == 0);
			Assert.IsTrue(regionB.MouseCaptured == false);
			Assert.IsTrue(bGotEnter == 0);
			Assert.IsTrue(bGotLeave == 0);
			Assert.IsTrue(bGotEnterBounds == 0);
			Assert.IsTrue(bGotLeaveBounds == 0);
			Assert.IsTrue(bGotMove == 0);

			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 25, 25, 0));
			Assert.IsTrue(regionA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == 0);
			Assert.IsTrue(aGotLeave == 0, "We exited a into b but we don't check children on capture.");
			Assert.IsTrue(aGotEnterBounds == 0);
			Assert.IsTrue(aGotLeaveBounds == 0, "We exited a into b but we don't check children on capture.");
			Assert.IsTrue(aGotMove == 1);
			Assert.IsTrue(regionB.MouseCaptured == false);
			Assert.IsTrue(bGotEnter == 0);
			Assert.IsTrue(bGotLeave == 0);
			Assert.IsTrue(bGotEnterBounds == 0);
			Assert.IsTrue(bGotLeaveBounds == 0);
			Assert.IsTrue(bGotMove == 0);
		}

		[Test]
		public void MouseCapturedSpressesLeaveEventsInButtonsSameAsRectangles()
		{
			GuiWidget container = new GuiWidget();
			container.Name = "container";
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);

			Button buttonA = new Button();
			buttonA.Name = "buttonA";
			buttonA.BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180);
			buttonA.OriginRelativeParent = new VectorMath.Vector2(10, 10);
			container.AddChild(buttonA);
			bool aGotEnter = false;
			bool aGotLeave = false;
			bool aGotMove = false;
			double aMoveX = 0;
			double aMoveY = 0;
			buttonA.MouseEnter += (sender, e) => { aGotEnter = true; };
			buttonA.MouseLeave += (sender, e) => { aGotLeave = true; };
			buttonA.MouseMove += (sender, mouseEvent) => { aGotMove = true; aMoveX = mouseEvent.X; aMoveY = mouseEvent.Y; };

			// make sure we know we are entered and captued on a down event
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(buttonA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(buttonA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == true);
			Assert.IsTrue(aGotLeave == false);
			Assert.IsTrue(aGotMove == false);

			// make sure we stay on top when internal moves occur
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 16, 16, 0));
			Assert.IsTrue(buttonA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(buttonA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == false);
			Assert.IsTrue(aGotLeave == false);
			Assert.IsTrue(aGotMove == true);
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 20, 20, 0));
			// lets prove that the move has been transformed into the correct coordinate system
			Assert.IsTrue(aMoveX == 10 && aMoveY == 10);
			Assert.IsTrue(buttonA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(buttonA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == false);
			Assert.IsTrue(aGotLeave == false);
			Assert.IsTrue(aGotMove == true);

			// make sure we see leave events when captured
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.IsTrue(container.FirstWidgetUnderMouse == false);
			Assert.IsTrue(buttonA.FirstWidgetUnderMouse == false);
			Assert.IsTrue(buttonA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == false);
			Assert.IsTrue(aGotLeave == true);
			Assert.IsTrue(aGotMove == true);

			// make sure we see enter events when captured
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.IsTrue(buttonA.FirstWidgetUnderMouse == true);
			Assert.IsTrue(buttonA.MouseCaptured == true);
			Assert.IsTrue(aGotEnter == true);
			Assert.IsTrue(aGotLeave == false);
			Assert.IsTrue(aGotMove == true);
		}
	}
}