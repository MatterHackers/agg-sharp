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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using Xunit;

namespace MatterHackers.Agg.UI.Tests
{
	//[TestFixture, Category("Agg.UI"), Parallelizable(ParallelScope.All)]
	public class MouseInteractionTests
	{
		[Fact]
		public async Task DoClickButtonInWindow()
		{
			int leftClickCount = 0;
			int rightClickCount = 0;

			AutomationTest testToRun = (testRunner) =>
			{
				// Now do the actions specific to this test. (replace this for new tests)
				testRunner.ClickByName("left");
				testRunner.Delay(.5);

				Assert.True(leftClickCount == 1, "Got left button click");

				testRunner.ClickByName("right");
				testRunner.Delay(.5);

				Assert.True(rightClickCount == 1, "Got right button click");

				testRunner.DragDropByName("left", "right", offsetDrag: new Point2D(1, 0));
				testRunner.Delay(.5);

				Assert.True(leftClickCount == 1, "Mouse down not a click");

				return Task.CompletedTask;
			};

			var buttonContainer = new SystemWindow(300, 200);

			var leftButton = new Button("left", 10, 40)
			{
				Name = "left"
			};
			leftButton.Click += (sender, e) => { leftClickCount++; };
			buttonContainer.AddChild(leftButton);
			var rightButton = new Button("right", 110, 40);
			rightButton.Click += (sender, e) => { rightClickCount++; };
			rightButton.Name = "right";
			buttonContainer.AddChild(rightButton);

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, testToRun);
		}

		[Fact]
		public async Task RadioButtonSiblingsAreChildren()
		{
			AutomationRunner.TimeToMoveMouse = .1;

			var buttonCount = 5;
			AutomationTest testToRun = (testRunner) =>
			{
				for (int i = 0; i < buttonCount; i++)
				{
					testRunner.ClickByName($"button {i}");
					testRunner.Delay(.5);
				}

				for (int i = buttonCount - 1; i >= 0; i--)
				{
					testRunner.ClickByName($"button {i}");
					testRunner.Delay(.5);
				}

				return Task.CompletedTask;
			};

			var buttonWindow = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.White
			};

			var buttonContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Left,
				VAnchor = VAnchor.Fit | VAnchor.Top,
			};

			buttonWindow.AddChild(buttonContainer);

			for (int i = 0; i < buttonCount; i++)
			{
				var radioButton = new RadioButton($"Button {i}")
				{
					Name = $"button {i}"
				};
				var index = i;
				radioButton.Click += (sender, e) =>
				{
					var buttons = buttonContainer.Children.ToArray();
					for (int j = 0; j < buttonCount; j++)
					{
						if (j == index)
						{
							Assert.True(((IRadioButton)buttons[j]).Checked);
						}
						else
						{
							Assert.False(((IRadioButton)buttons[j]).Checked);
						}
					}
				};
				buttonContainer.AddChild(radioButton);
			}

			await AutomationRunner.ShowWindowAndExecuteTests(buttonWindow, testToRun);
		}

		[Fact]
		public void ExtensionMethodsTests()
		{
			var level0 = new GuiWidget() { Name = "level0" };
			var level1 = new GuiWidget() { Name = "level1" };
			level0.AddChild(level1);
			var level2 = new GuiWidget() { Name = "level2" };
			level1.AddChild(level2);
			var level3 = new GuiWidget() { Name = "level3" };
			level2.AddChild(level3);
			var allWidgets = new List<GuiWidget>() { level0, level1, level2, level3 };

			foreach (var child in level0.Children<GuiWidget>())
			{
				Assert.True(child == allWidgets[1]);
			}

			foreach (var child in level1.Children<GuiWidget>())
			{
				Assert.True(child == allWidgets[2]);
			}

			foreach (var child in level2.Children<GuiWidget>())
			{
				Assert.True(child == allWidgets[3]);
			}

			foreach (var child in level3.Children<GuiWidget>())
			{
				Assert.True(false); // there are no children we should not get here
			}

			int index = allWidgets.Count - 1;
			int parentCount = 0;
			foreach (var parent in level3.Parents<GuiWidget>())
			{
				parentCount++;
				Assert.True(parent == allWidgets[--index]);
			}

			Assert.True(parentCount == 3);
		}

		[Fact]
		public void ValidateSimpleLeftClick()
		{
			var container = new GuiWidget
			{
				Name = "Container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var button = new Button("Test", 100, 100)
			{
				Name = "button"
			};
			bool gotClick = false;
			button.Click += (sender, e) => { gotClick = true; };
			container.AddChild(button);

			Assert.True(gotClick == false);
			Assert.True(button.Focused == false);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.True(gotClick == false);
			Assert.True(button.Focused == false);

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.True(gotClick == false);
			Assert.True(button.Focused == true, "Down click triggers focused.");

			Assert.True(gotClick == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.True(gotClick == true);
			Assert.True(button.Focused == true);

			gotClick = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.True(gotClick == false);
			Assert.True(button.Focused == false);
		}

		[Fact]
		public void ValidateOnlyTopWidgetGetsLeftClick()
		{
			bool gotClick = false;
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var button = new Button("Test", 100, 100)
			{
				Name = "button"
			};
			button.Click += (sender, e) => { gotClick = true; };
			container.AddChild(button);

			var blockingWidegt = new GuiWidget
			{
				Name = "blockingWidegt",
				LocalBounds = new RectangleDouble(105, 105, 125, 125)
			};
			container.AddChild(blockingWidegt);

			// the widget is not in the way
			Assert.True(gotClick == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			Assert.True(container.MouseCaptured == false);
			Assert.True(blockingWidegt.MouseCaptured == false);
			Assert.True(container.ChildHasMouseCaptured == true);
			Assert.True(blockingWidegt.ChildHasMouseCaptured == false);
			Assert.True(button.MouseCaptured == true);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			Assert.True(container.MouseCaptured == false);
			Assert.True(blockingWidegt.MouseCaptured == false);
			Assert.True(button.MouseCaptured == false);
			Assert.True(gotClick == true);

			gotClick = false;

			// the widget is in the way
			Assert.True(gotClick == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.True(container.MouseCaptured == false);
			Assert.True(blockingWidegt.MouseCaptured == true);
			Assert.True(button.MouseCaptured == false);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.True(container.MouseCaptured == false);
			Assert.True(blockingWidegt.MouseCaptured == false);
			Assert.True(button.MouseCaptured == false);
			Assert.True(gotClick == false);
		}

		[Fact]
		public void ValidateSimpleMouseUpDown()
		{
			var container = new GuiWidget
			{
				Name = "container"
			};
			int containerGotMouseUp = 0;
			int containerGotMouseDown = 0;
			int containerGotMouseDownInBounds = 0;
			container.MouseUpCaptured += (sender, e) => { containerGotMouseUp++; };
			container.MouseDownCaptured += (sender, e) => { containerGotMouseDown++; };
			container.MouseDown += (sender, e) => { containerGotMouseDownInBounds++; };
			container.LocalBounds = new RectangleDouble(0, 0, 200, 200);
			var topWidget = new GuiWidget
			{
				Name = "topWidget",
				LocalBounds = new RectangleDouble(100, 100, 150, 150)
			};
			int topWidgetGotMouseUp = 0;
			int topWidgetGotMouseDown = 0;
			int topWidgetGotMouseDownInBounds = 0;
			topWidget.MouseUpCaptured += (sender, e) => { topWidgetGotMouseUp++; };
			topWidget.MouseDownCaptured += (sender, e) => { topWidgetGotMouseDown++; };
			topWidget.MouseDown += (sender, e) => { topWidgetGotMouseDownInBounds++; };
			container.AddChild(topWidget);

			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 0);
			// down outside everything
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, -10, -10, 0));
			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 0);
			Assert.True(containerGotMouseDown == 0);
			Assert.True(topWidgetGotMouseDown == 0);
			Assert.True(containerGotMouseDownInBounds == 0);
			Assert.True(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			// up outside everything
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, -10, -10, 0));
			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 0);
			Assert.True(containerGotMouseDown == 0);
			Assert.True(topWidgetGotMouseDown == 0);
			Assert.True(containerGotMouseDownInBounds == 0);
			Assert.True(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			// down on container
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 0);
			Assert.True(containerGotMouseDown == 1);
			Assert.True(topWidgetGotMouseDown == 0);
			Assert.True(containerGotMouseDownInBounds == 1);
			Assert.True(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.True(containerGotMouseUp == 1);
			Assert.True(topWidgetGotMouseUp == 0);
			Assert.True(containerGotMouseDown == 0);
			Assert.True(topWidgetGotMouseDown == 0);
			Assert.True(containerGotMouseDownInBounds == 0);
			Assert.True(topWidgetGotMouseDownInBounds == 0);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 0);
			Assert.True(containerGotMouseDown == 0);
			Assert.True(topWidgetGotMouseDown == 1);
			Assert.True(containerGotMouseDownInBounds == 1);
			Assert.True(topWidgetGotMouseDownInBounds == 1);
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 1);
			Assert.True(containerGotMouseDown == 0);
			Assert.True(topWidgetGotMouseDown == 0);
			Assert.True(containerGotMouseDownInBounds == 0);
			Assert.True(topWidgetGotMouseDownInBounds == 0);

			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.True(containerGotMouseUp == 0);
			Assert.True(topWidgetGotMouseUp == 1);
			Assert.True(containerGotMouseDown == 0);
			Assert.True(topWidgetGotMouseDown == 1);
			Assert.True(containerGotMouseDownInBounds == 1);
			Assert.True(topWidgetGotMouseDownInBounds == 1);
		}

		[Fact]
		public void ValidateOnlyTopWidgetGetsMouseUp()
		{
			bool topGotMouseUp = false;
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};
			var topWidget = new GuiWidget
			{
				Name = "topWidget",
				LocalBounds = new RectangleDouble(100, 100, 150, 150)
			};
			topWidget.MouseUpCaptured += (sender, e) => { topGotMouseUp = true; };

			container.AddChild(topWidget);

			bool blockingGotMouseUp = false;
			var blockingWidegt = new GuiWidget
			{
				Name = "blockingWidegt"
			};
			blockingWidegt.MouseUpCaptured += (sender, e) => { blockingGotMouseUp = true; };
			blockingWidegt.LocalBounds = new RectangleDouble(105, 105, 125, 125);
			container.AddChild(blockingWidegt);

			// the widget is not in the way
			Assert.True(topGotMouseUp == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			Assert.True(blockingGotMouseUp == false);
			Assert.True(topGotMouseUp == true);

			topGotMouseUp = false;

			// the widget is in the way
			Assert.True(topGotMouseUp == false);
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			Assert.True(blockingGotMouseUp == true);
			Assert.True(topGotMouseUp == false);
		}

		[Fact]
		public void ValidateEnterAndLeaveEvents()
		{
			int mouseEnter = 0;
			int mouseLeave = 0;
			int mouseEnterBounds = 0;
			int mouseLeaveBounds = 0;
			int mouseDown = 0;
			int mouseUp = 0;

			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};

			var regionA = new GuiWidget
			{
				Name = "regionA",
				BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190)
			};
			regionA.MouseDownCaptured += (sender, e) =>
			{
				mouseDown++;
			};
			regionA.MouseUpCaptured += (sender, e) =>
			{
				mouseUp++;
			};
			regionA.MouseEnter += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }
				mouseEnter++;
			};
			regionA.MouseLeave += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }
				mouseLeave++;
			};
			regionA.MouseEnterBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }
				mouseEnterBounds++;
			};
			regionA.MouseLeaveBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }
				mouseLeaveBounds++;
			};
			container.AddChild(regionA);

			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(mouseLeave == 0);
			Assert.True(mouseEnter == 0);
			Assert.True(mouseLeaveBounds == 0);
			Assert.True(mouseEnterBounds == 0);

			// put the mouse into the widget but outside regionA
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(regionA.UnderMouseState == UnderMouseState.NotUnderMouse);
			Assert.True(mouseLeave == 0);
			Assert.True(mouseEnter == 0);
			Assert.True(mouseLeaveBounds == 0);
			Assert.True(mouseEnterBounds == 0);

			// move it into regionA
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.True(mouseLeave == 0);
			Assert.True(mouseEnter == 1);
			Assert.True(mouseLeaveBounds == 0);
			Assert.True(mouseEnterBounds == 1);

			// now move it inside regionA and make sure it does not re-trigger either event
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.True(mouseLeave == 0);
			Assert.True(mouseEnter == 0);
			Assert.True(mouseLeaveBounds == 0);
			Assert.True(mouseEnterBounds == 0);

			// now leave and make sure we see the leave
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, -5, -5, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(mouseLeave == 1);
			Assert.True(mouseEnter == 0);
			Assert.True(mouseLeaveBounds == 1);
			Assert.True(mouseEnterBounds == 0);

			// move back on
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			// now leave only the inside widget and make sure we see the leave
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(mouseEnter == 1);
			Assert.True(mouseLeave == 0);
			Assert.True(mouseLeaveBounds == 0);
			Assert.True(mouseEnterBounds == 1);

			// move off
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(mouseLeave == 1);
			Assert.True(mouseEnter == 0);
			Assert.True(mouseLeaveBounds == 1);
			Assert.True(mouseEnterBounds == 0);

			// click back on
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 1);
			Assert.True(mouseUp == 1);
			Assert.True(mouseEnter == 1);
			Assert.True(mouseLeave == 0);
			Assert.True(mouseLeaveBounds == 0);
			Assert.True(mouseEnterBounds == 1);

			// click off
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			Assert.True(mouseDown == 0);
			Assert.True(mouseUp == 0);
			Assert.True(mouseLeave == 1);
			Assert.True(mouseEnter == 0);
			Assert.True(mouseLeaveBounds == 1);
			Assert.True(mouseEnterBounds == 0);
		}

		[Fact]//, Ignore("WorkInProgress - Functionality needs to be implemented")]
		public void ValidateEnterLeaveOnWidgetMoves()
		{
			GuiWidget container = new SystemWindow(200, 200)
			{
				Name = "container"
			};

			var regionA = new GuiWidget
			{
				Name = "regionA",
				BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180),
				OriginRelativeParent = new Vector2(10, 10)
			};
			int gotEnter = 0;
			int gotLeave = 0;
			regionA.MouseEnter += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse)
				{
					throw new Exception("It must be under the mouse.");
				}

				gotEnter++;
			};
			regionA.MouseLeave += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse)
				{
					throw new Exception("It must not be under the mouse.");
				}

				gotLeave++;
			};
			int gotEnterBounds = 0;
			int gotLeaveBounds = 0;
			regionA.MouseEnterBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }
				gotEnterBounds++;
			};
			regionA.MouseLeaveBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }
				gotLeaveBounds++;
			};
			container.AddChild(regionA);

			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 0);

			// put the mouse into the widget but outside regionA
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(regionA.UnderMouseState == UnderMouseState.NotUnderMouse);
			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 0);
			// move regionA under mouse
			regionA.OriginRelativeParent = new Vector2(0, 0);
			Assert.True(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 1);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 1);
			// now regionA and make sure it does not re-trigger either event
			gotEnter = 0;
			gotEnterBounds = 0;
			regionA.OriginRelativeParent = new Vector2(1, 1);
			Assert.True(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 0);

			// now move out from under mouse and make sure we see the leave
			regionA.OriginRelativeParent = new Vector2(10, 10);
			Assert.True(gotLeave == 1);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 1);
			Assert.True(gotEnterBounds == 0);

			// move back under
			gotLeave = gotEnter = gotLeaveBounds = gotEnterBounds = 0;
			regionA.OriginRelativeParent = new Vector2(0, 0);
			Assert.True(gotEnter == 1);
			Assert.True(gotLeave == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 1);
		}

		[Fact]//, Ignore("WorkInProgress - Functionality needs to be implemented")]
		public void ValidateEnterLeaveOnWidgetBoundsChange()
		{
			GuiWidget container = new SystemWindow(200, 200)
			{
				Name = "container"
			};

			var regionA = new GuiWidget
			{
				Name = "regionA",
				BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190)
			};
			int gotEnter = 0;
			int gotLeave = 0;
			regionA.MouseEnter += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnter++;
			};
			regionA.MouseLeave += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeave++;
			};
			int gotEnterBounds = 0;
			int gotLeaveBounds = 0;
			regionA.MouseEnterBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterBounds++;
			};
			regionA.MouseLeaveBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveBounds++;
			};
			container.AddChild(regionA);

			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 0);

			// put the mouse into the widget but outside regionA
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(regionA.UnderMouseState == UnderMouseState.NotUnderMouse);
			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 0);
			// move regionA under mouse
			regionA.BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180);
			Assert.True(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 1);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 1);
			// now regionA and make sure it does not re-trigger either event
			gotEnter = 0;
			gotEnterBounds = 0;
			regionA.BoundsRelativeToParent = new RectangleDouble(1, 1, 181, 181);
			Assert.True(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse);
			Assert.True(gotLeave == 0);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 0);

			// now move out from under mouse and make sure we see the leave
			regionA.BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190);
			Assert.True(gotLeave == 1);
			Assert.True(gotEnter == 0);
			Assert.True(gotLeaveBounds == 1);
			Assert.True(gotEnterBounds == 0);

			// move back under
			gotLeave = gotEnter = gotLeaveBounds = gotEnterBounds = 0;
			regionA.BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180);
			Assert.True(gotEnter == 1);
			Assert.True(gotLeave == 0);
			Assert.True(gotLeaveBounds == 0);
			Assert.True(gotEnterBounds == 1);
		}

		public RadioButton GenerateRadioButton(string label)
		{
			var nomalState = new TextWidget(label);
			var hoverState = new TextWidget(label);
			var checkingState = new TextWidget(label);
			var checkedState = new TextWidget(label);
			var disabledState = new TextWidget(label);
			var checkBoxButtonViewWidget = new RadioButtonViewStates(nomalState, hoverState, checkingState, checkedState, disabledState);
			var radioButton = new RadioButton(checkBoxButtonViewWidget)
			{
				Margin = default(BorderDouble)
			};
			return radioButton;
		}

		[Fact]
		public void ValidateEnterAndLeaveEventsWhenNested()
		{
			// ___container__(200, 200)_______________________________________
			// |                                                             |
			// |    __regionB__(match A)________________________________     |
			// |   |                                                    |    |
			// |   |    __regionA__(10, 10)_________________________    |    |
			// |   |    |                                           |   |    |
			// |   |    |                                           |   |    |
			// |   |    |______________________________(190, 190)___|   |    |
			// |   |______________________________________(match A)_|    |
			// |                                                             |
			// |___________________________________________________(200,200)_|
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};

			var regionB = new GuiWidget();

			var regionA = new GuiWidget
			{
				Name = "regionA",
				BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190)
			};
			int gotEnterA = 0;
			int gotLeaveA = 0;
			regionA.MouseEnter += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.FirstUnderMouse, regionA.UnderMouseState);//, "It must be the first under the mouse.");
				Assert.Equal(UnderMouseState.UnderMouseNotFirst, regionB.UnderMouseState);//, "It must be under the mouse not first.");
                Assert.Equal(UnderMouseState.UnderMouseNotFirst, container.UnderMouseState);//, "It must be under the mouse not first.");
                gotEnterA++;
			};
			regionA.MouseLeave += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.NotUnderMouse, regionA.UnderMouseState);//, "It must be not under the mouse.");
                Assert.Equal(UnderMouseState.NotUnderMouse, regionB.UnderMouseState);//, "It must be not under the mouse.");
                gotLeaveA++;
			};
			int gotEnterBoundsA = 0;
			int gotLeaveBoundsA = 0;
			regionA.MouseEnterBounds += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.FirstUnderMouse, regionA.UnderMouseState);//, "It must be the first under the mouse.");
                Assert.Equal(UnderMouseState.UnderMouseNotFirst, regionB.UnderMouseState);//, "It must be under the mouse not first.");
                Assert.Equal(UnderMouseState.UnderMouseNotFirst, container.UnderMouseState);//, "It must be under the mouse not first.");
                gotEnterBoundsA++;
			};
			regionA.MouseLeaveBounds += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.NotUnderMouse, regionA.UnderMouseState);//, "It must be not under the mouse.");
                Assert.Equal(UnderMouseState.NotUnderMouse, regionB.UnderMouseState);//, "It must be not under the mouse.");
                gotLeaveBoundsA++;
			};

			regionB.Name = "regionB";
			regionB.AddChild(regionA);
			regionB.SetBoundsToEncloseChildren();
			container.AddChild(regionB);
			int gotEnterB = 0;
			int gotLeaveB = 0;
			regionB.MouseEnter += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.FirstUnderMouse, regionA.UnderMouseState);//, "It must be the first under the mouse.");
                Assert.Equal(UnderMouseState.UnderMouseNotFirst, regionB.UnderMouseState);//, "It must be under the mouse not first.");
                Assert.Equal(UnderMouseState.UnderMouseNotFirst, container.UnderMouseState);//, "It must be under the mouse not first.");
                gotEnterB++;
			};
			regionB.MouseLeave += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.NotUnderMouse, regionA.UnderMouseState);//, "It must be not under the mouse.");
                Assert.Equal(UnderMouseState.NotUnderMouse, regionB.UnderMouseState);//, "It must be not under the mouse.");
                Assert.Equal(UnderMouseState.NotUnderMouse, container.UnderMouseState);//, "It must be not under the mouse.");
                gotLeaveB++;
			};
			int gotEnterBoundsB = 0;
			int gotLeaveBoundsB = 0;
			regionB.MouseEnterBounds += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.UnderMouseNotFirst, regionB.UnderMouseState);//, "It must be under the mouse not first.");
                Assert.Equal(UnderMouseState.UnderMouseNotFirst, container.UnderMouseState);//, "It must be under the mouse not first.");
                gotEnterBoundsB++;
			};
			regionB.MouseLeaveBounds += (sender, e) =>
			{
				Assert.Equal(UnderMouseState.NotUnderMouse, regionB.UnderMouseState);//, "It must be not under the mouse.");
                gotLeaveBoundsB++;
			};

			Assert.True(gotLeaveA == 0);
			Assert.True(gotEnterA == 0);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);

			// put the mouse into the widget but outside regionA and region B
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(gotLeaveA == 0);
			Assert.True(gotEnterA == 0);
			Assert.True(gotLeaveBoundsA == 0);
			Assert.True(gotEnterBoundsA == 0);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 0);
			Assert.True(gotEnterBoundsB == 0);

			// move it into regionA
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(gotLeaveA == 0);
			Assert.True(gotEnterA == 1);
			Assert.True(gotLeaveBoundsA == 0);
			Assert.True(gotEnterBoundsA == 1);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 0);
			Assert.True(gotEnterBoundsB == 1);

			// now move it inside regionA and make sure it does not re-trigger either event
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			Assert.True(gotLeaveA == 0);
			Assert.True(gotEnterA == 0);
			Assert.True(gotLeaveBoundsA == 0);
			Assert.True(gotEnterBoundsA == 0);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 0);
			Assert.True(gotEnterBoundsB == 0);

			// now leave and make sure we see the leave
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, -5, -5, 0));
			Assert.True(gotLeaveA == 1);
			Assert.True(gotEnterA == 0);
			Assert.True(gotLeaveBoundsA == 1);
			Assert.True(gotEnterBoundsA == 0);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 1);
			Assert.True(gotEnterBoundsB == 0);

			// move back on
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			// now leave only the inside widget and make sure we see the leave
			Assert.True(gotEnterA == 1);
			Assert.True(gotLeaveA == 0);
			Assert.True(gotLeaveBoundsA == 0);
			Assert.True(gotEnterBoundsA == 1);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 0);
			Assert.True(gotEnterBoundsB == 1);

			// and a final leave
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(gotLeaveA == 1);
			Assert.True(gotEnterA == 0);
			Assert.True(gotLeaveBoundsA == 1);
			Assert.True(gotEnterBoundsA == 0);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 1);
			Assert.True(gotEnterBoundsB == 0);

			// click back on
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			Assert.True(gotEnterA == 1);
			Assert.True(gotLeaveA == 0);
			Assert.True(gotLeaveBoundsA == 0);
			Assert.True(gotEnterBoundsA == 1);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 0);
			Assert.True(gotEnterBoundsB == 1);

			// click off
			gotEnterA = 0;
			gotEnterBoundsA = 0;
			gotLeaveA = 0;
			gotLeaveBoundsA = 0;
			gotEnterB = 0;
			gotEnterBoundsB = 0;
			gotLeaveB = 0;
			gotLeaveBoundsB = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			Assert.True(gotLeaveA == 1);
			Assert.True(gotEnterA == 0);
			Assert.True(gotLeaveBoundsA == 1);
			Assert.True(gotEnterBoundsA == 0);
			Assert.True(gotLeaveB == 0);
			Assert.True(gotEnterB == 0);
			Assert.True(gotLeaveBoundsB == 1);
			Assert.True(gotEnterBoundsB == 0);
		}

		[Fact]
		public void ValidateEnterAndLeaveEventsWhenCoverd()
		{
			// A widget contains two children the second completely covering the first.
			// When the mouse moves into the first it should not receive an enter event only a bounds enter event.
			// When the mouse move out of the first it should receive only a bounds exit, not an exit.
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};

			var coveredWidget = new GuiWidget
			{
				Name = "coveredWidget",
				BoundsRelativeToParent = new RectangleDouble(20, 20, 180, 180)
			};
			int gotEnterCovered = 0;
			int gotLeaveCovered = 0;
			coveredWidget.MouseEnter += (sender, e) =>
			{
				if (coveredWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterCovered++;
			};
			coveredWidget.MouseLeave += (sender, e) =>
			{
				if (coveredWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveCovered++;
			};
			int gotEnterBoundsCovered = 0;
			int gotLeaveBoundsCovered = 0;
			coveredWidget.MouseEnterBounds += (sender, e) =>
			{
				if (coveredWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterBoundsCovered++;
			};
			coveredWidget.MouseLeaveBounds += (sender, e) =>
			{
				if (coveredWidget.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveBoundsCovered++;
			};
			container.AddChild(coveredWidget);

			var coveredChildWidget = new GuiWidget
			{
				Name = "coveredChildWidget"
			};
			int gotEnterCoveredChild = 0;
			int gotLeaveCoveredChild = 0;
			coveredChildWidget.MouseEnter += (sender, e) =>
			{
				if (coveredChildWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterCoveredChild++;
			};
			coveredChildWidget.MouseLeave += (sender, e) =>
			{
				if (coveredChildWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveCoveredChild++;
			};
			int gotEnterBoundsCoveredChild = 0;
			int gotLeaveBoundsCoveredChild = 0;
			coveredChildWidget.MouseEnterBounds += (sender, e) =>
			{
				if (coveredChildWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterBoundsCoveredChild++;
			};
			coveredChildWidget.MouseLeaveBounds += (sender, e) =>
			{
				if (coveredChildWidget.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveBoundsCoveredChild++;
			};
			coveredWidget.AddChild(coveredChildWidget);
			coveredChildWidget.BoundsRelativeToParent = coveredWidget.LocalBounds;

			var coverWidget = new GuiWidget
			{
				Name = "coverWidget",
				BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190)
			};
			int gotEnterCover = 0;
			int gotLeaveCover = 0;
			coverWidget.MouseEnter += (sender, e) =>
			{
				if (coverWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterCover++;
			};
			coverWidget.MouseLeave += (sender, e) =>
			{
				if (coverWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveCover++;
			};
			int gotEnterBoundsCover = 0;
			int gotLeaveBoundsCover = 0;
			coverWidget.MouseEnterBounds += (sender, e) =>
			{
				if (coverWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				gotEnterBoundsCover++;
			};
			coverWidget.MouseLeaveBounds += (sender, e) =>
			{
				if (coverWidget.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				gotLeaveBoundsCover++;
			};
			container.AddChild(coverWidget);

			Assert.True(gotLeaveCover == 0);
			Assert.True(gotEnterCover == 0);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);

			// put the mouse into the widget but outside the children
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(gotLeaveCover == 0);
			Assert.True(gotEnterCover == 0);
			Assert.True(gotLeaveBoundsCover == 0);
			Assert.True(gotEnterBoundsCover == 0);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveBoundsCovered == 0);
			Assert.True(gotEnterBoundsCovered == 0);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);
			Assert.True(gotLeaveBoundsCoveredChild == 0);
			Assert.True(gotEnterBoundsCoveredChild == 0);

			// move it into the cover
			gotEnterCover = 0;
			gotEnterBoundsCover = 0;
			gotLeaveCover = 0;
			gotLeaveBoundsCover = 0;
			gotEnterCovered = 0;
			gotEnterBoundsCovered = 0;
			gotLeaveCovered = 0;
			gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0;
			gotEnterBoundsCoveredChild = 0;
			gotLeaveCoveredChild = 0;
			gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(gotLeaveCover == 0);
			Assert.True(gotEnterCover == 1);
			Assert.True(gotLeaveBoundsCover == 0);
			Assert.True(gotEnterBoundsCover == 1);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveBoundsCovered == 0);
			Assert.True(gotEnterBoundsCovered == 0);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);
			Assert.True(gotLeaveBoundsCoveredChild == 0);
			Assert.True(gotEnterBoundsCoveredChild == 0);

			// now move it inside cover and make sure it does not re-trigger either event
			gotEnterCover = 0;
			gotEnterBoundsCover = 0;
			gotLeaveCover = 0;
			gotLeaveBoundsCover = 0;
			gotEnterCovered = 0;
			gotEnterBoundsCovered = 0;
			gotLeaveCovered = 0;
			gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0;
			gotEnterBoundsCoveredChild = 0;
			gotLeaveCoveredChild = 0;
			gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			Assert.True(gotLeaveCover == 0);
			Assert.True(gotEnterCover == 0);
			Assert.True(gotLeaveBoundsCover == 0);
			Assert.True(gotEnterBoundsCover == 0);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveBoundsCovered == 0);
			Assert.True(gotEnterBoundsCovered == 0);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);
			Assert.True(gotLeaveBoundsCoveredChild == 0);
			Assert.True(gotEnterBoundsCoveredChild == 0);

			// now leave and make sure we see the leave
			gotEnterCover = 0;
			gotEnterBoundsCover = 0;
			gotLeaveCover = 0;
			gotLeaveBoundsCover = 0;
			gotEnterCovered = 0;
			gotEnterBoundsCovered = 0;
			gotLeaveCovered = 0;
			gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0;
			gotEnterBoundsCoveredChild = 0;
			gotLeaveCoveredChild = 0;
			gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(gotLeaveCover == 1);
			Assert.True(gotEnterCover == 0);
			Assert.True(gotLeaveBoundsCover == 1);
			Assert.True(gotEnterBoundsCover == 0);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveBoundsCovered == 0);
			Assert.True(gotEnterBoundsCovered == 0);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);
			Assert.True(gotLeaveBoundsCoveredChild == 0);
			Assert.True(gotEnterBoundsCoveredChild == 0);

			// now enter the covered and make sure we only see bounds enter
			gotEnterCover = 0;
			gotEnterBoundsCover = 0;
			gotLeaveCover = 0;
			gotLeaveBoundsCover = 0;
			gotEnterCovered = 0;
			gotEnterBoundsCovered = 0;
			gotLeaveCovered = 0;
			gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0;
			gotEnterBoundsCoveredChild = 0;
			gotLeaveCoveredChild = 0;
			gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 25, 25, 0));
			// now leave only the inside widget and make sure we see the leave
			Assert.True(gotEnterCover == 1);
			Assert.True(gotLeaveCover == 0);
			Assert.True(gotLeaveBoundsCover == 0);
			Assert.True(gotEnterBoundsCover == 1);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveBoundsCovered == 0);
			Assert.True(gotEnterBoundsCovered == 1);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);
			Assert.True(gotLeaveBoundsCoveredChild == 0);
			Assert.True(gotEnterBoundsCoveredChild == 1);

			// and a final leave and make sure we only see bounds leave
			gotEnterCover = 0;
			gotEnterBoundsCover = 0;
			gotLeaveCover = 0;
			gotLeaveBoundsCover = 0;
			gotEnterCovered = 0;
			gotEnterBoundsCovered = 0;
			gotLeaveCovered = 0;
			gotLeaveBoundsCovered = 0;
			gotEnterCoveredChild = 0;
			gotEnterBoundsCoveredChild = 0;
			gotLeaveCoveredChild = 0;
			gotLeaveBoundsCoveredChild = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			Assert.True(gotLeaveCover == 1);
			Assert.True(gotEnterCover == 0);
			Assert.True(gotLeaveBoundsCover == 1);
			Assert.True(gotEnterBoundsCover == 0);
			Assert.True(gotLeaveCovered == 0);
			Assert.True(gotEnterCovered == 0);
			Assert.True(gotLeaveBoundsCovered == 1);
			Assert.True(gotEnterBoundsCovered == 0);
			Assert.True(gotLeaveCoveredChild == 0);
			Assert.True(gotEnterCoveredChild == 0);
			Assert.True(gotLeaveBoundsCoveredChild == 1);
			Assert.True(gotEnterBoundsCoveredChild == 0);
		}

		[Fact]
		public void ValidateEnterAndLeaveInOverlapArea()
		{
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};

			var bottomWidget = new GuiWidget
			{
				Name = "bottom",
				BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190)
			};

			int bottomGotEnter = 0;
			int bottomGotLeave = 0;
			bottomWidget.MouseEnter += (sender, e) =>
			{
				if (bottomWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				bottomGotEnter++;
			};
			bottomWidget.MouseLeave += (sender, e) =>
			{
				if (bottomWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be the first under the mouse."); }

				bottomGotLeave++;
			};
			int bottomGotEnterBounds = 0;
			int bottomGotLeaveBounds = 0;
			bottomWidget.MouseEnterBounds += (sender, e) =>
			{
				if (bottomWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				bottomGotEnterBounds++;
			};
			bottomWidget.MouseLeaveBounds += (sender, e) =>
			{
				if (bottomWidget.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				bottomGotLeaveBounds++;
			};
			container.AddChild(bottomWidget);

			var topWidget = new GuiWidget
			{
				Name = "top",
				BoundsRelativeToParent = new RectangleDouble(5, 20, 190, 190)
			};
			int topGotEnter = 0;
			int topGotLeave = 0;
			topWidget.MouseEnter += (sender, e) =>
			{
				if (topWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				topGotEnter++;
			};
			topWidget.MouseLeave += (sender, e) =>
			{
				if (topWidget.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be the first under the mouse."); }

				topGotLeave++;
			};
			int topGotEnterBounds = 0;
			int topGotLeaveBounds = 0;
			topWidget.MouseEnterBounds += (sender, e) =>
			{
				if (topWidget.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				topGotEnterBounds++;
			};
			topWidget.MouseLeaveBounds += (sender, e) =>
			{
				if (topWidget.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				topGotLeaveBounds++;
			};
			container.AddChild(topWidget);

			Assert.True(topGotEnter == 0);
			Assert.True(topGotLeave == 0);
			Assert.True(topGotEnterBounds == 0);
			Assert.True(topGotLeaveBounds == 0);

			// move into the bottom widget only
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 15, 0));
			Assert.True(bottomGotLeave == 0);
			Assert.True(bottomGotEnter == 0);
			Assert.True(bottomGotLeaveBounds == 0);
			Assert.True(bottomGotEnterBounds == 0);
			Assert.True(topGotLeave == 0);
			Assert.True(topGotEnter == 0);
			Assert.True(topGotLeaveBounds == 0);
			Assert.True(topGotEnterBounds == 0);

			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(bottomGotLeave == 0);
			Assert.True(bottomGotEnter == 1);
			Assert.True(bottomGotLeaveBounds == 0);
			Assert.True(bottomGotEnterBounds == 1);
			Assert.True(topGotLeave == 0);
			Assert.True(topGotEnter == 0);
			Assert.True(topGotLeaveBounds == 0);
			Assert.True(topGotEnterBounds == 0);

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move out of the bottom widget only
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 15, 0));
			Assert.True(bottomGotLeave == 1);
			Assert.True(bottomGotEnter == 0);
			Assert.True(bottomGotLeaveBounds == 1);
			Assert.True(bottomGotEnterBounds == 0);
			Assert.True(topGotLeave == 0);
			Assert.True(topGotEnter == 0);
			Assert.True(topGotLeaveBounds == 0);
			Assert.True(topGotEnterBounds == 0);

			// move to just outside both widgets
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 25, 0));
			Assert.True(bottomWidget.TransformToScreenSpace(bottomWidget.LocalBounds).Contains(1, 25) == false);
			Assert.True(topWidget.TransformToScreenSpace(topWidget.LocalBounds).Contains(1, 25) == false);
			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move over the top widget when it is over the bottom widget (only the top should see this)
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 25, 0));
			Assert.True(bottomGotEnter == 0);
			Assert.True(bottomGotLeave == 0);
			Assert.True(bottomGotEnterBounds == 1);
			Assert.True(bottomGotLeaveBounds == 0);
			Assert.True(topGotEnter == 1);
			Assert.True(topGotLeave == 0);
			Assert.True(topGotEnterBounds == 1);
			Assert.True(topGotLeaveBounds == 0);

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move out of the top widget into the bottom
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(bottomGotEnter == 1);
			Assert.True(bottomGotLeave == 0);
			Assert.True(bottomGotEnterBounds == 0);
			Assert.True(bottomGotLeaveBounds == 0);
			Assert.True(topGotEnter == 0);
			Assert.True(topGotLeave == 1);
			Assert.True(topGotEnterBounds == 0);
			Assert.True(topGotLeaveBounds == 1);

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move back up into the top and make sure we see the leave in the bottom
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 25, 0));
			Assert.True(bottomGotEnter == 0);
			Assert.True(bottomGotLeave == 1);
			Assert.True(bottomGotEnterBounds == 0);
			Assert.True(bottomGotLeaveBounds == 0);
			Assert.True(topGotEnter == 1);
			Assert.True(topGotLeave == 0);
			Assert.True(topGotEnterBounds == 1);
			Assert.True(topGotLeaveBounds == 0);
		}

		[Fact]
		public void MouseCapturedSpressesLeaveEvents()
		{
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};

			var regionA = new GuiWidget
			{
				Name = "regionA",
				BoundsRelativeToParent = new RectangleDouble(10, 10, 190, 190)
			};
			container.AddChild(regionA);
			int aGotEnter = 0;
			int aGotLeave = 0;
			int aGotEnterBounds = 0;
			int aGotLeaveBounds = 0;
			int aGotMove = 0;
			int aGotUp = 0;
			regionA.MouseEnter += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				aGotEnter++;
			};
			regionA.MouseLeave += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }

				aGotLeave++;
			};
			regionA.MouseEnterBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				aGotEnterBounds++;
			};
			regionA.MouseLeaveBounds += (sender, e) =>
			{
				if (regionA.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				aGotLeaveBounds++;
			};
			regionA.MouseMove += (sender, e) => { aGotMove++; };
			regionA.MouseUpCaptured += (sender, e) => { aGotUp++; };

			// make sure we know we are entered and captured on a down event
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(regionA.FirstWidgetUnderMouse == true);
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 1);
			Assert.True(aGotLeave == 0);
			Assert.True(aGotEnterBounds == 1);
			Assert.True(aGotLeaveBounds == 0);
			Assert.True(aGotMove == 0);

			// make sure we stay on top when internal moves occur
			aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 16, 0));
			Assert.True(regionA.FirstWidgetUnderMouse == true);
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 0);
			Assert.True(aGotLeave == 0);
			Assert.True(aGotEnterBounds == 0);
			Assert.True(aGotLeaveBounds == 0);
			Assert.True(aGotMove == 1);

			// make sure we see leave events when captured
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.True(container.FirstWidgetUnderMouse == false);
			Assert.True(regionA.FirstWidgetUnderMouse == false);
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 0);
			Assert.True(aGotLeave == 1);
			Assert.True(aGotEnterBounds == 0);
			Assert.True(aGotLeaveBounds == 1);
			Assert.True(aGotMove == 1);

			// make sure we see enter events when captured
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(regionA.FirstWidgetUnderMouse == true);
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 1);
			Assert.True(aGotLeave == 0);
			Assert.True(aGotEnterBounds == 1);
			Assert.True(aGotLeaveBounds == 0);
			Assert.True(aGotMove == 1);

			// and we are not captured after mouseup above region
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(regionA.MouseCaptured == false);
			Assert.True(aGotEnter == 0);
			Assert.True(aGotLeave == 0);
			Assert.True(aGotEnterBounds == 0);
			Assert.True(aGotLeaveBounds == 0);
			Assert.True(aGotMove == 0);
			Assert.True(aGotUp == 1, "When we are captured we need to see mouse up messages.");

			// make sure we are not captured after mouseup above off region
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 0, "we are already in the button from the last move");
			Assert.True(aGotLeave == 0);
			Assert.True(aGotEnterBounds == 0, "we are already in the button from the last move");
			Assert.True(aGotLeaveBounds == 0);
			Assert.True(aGotMove == 0);
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.True(regionA.MouseCaptured == false);
			Assert.True(aGotEnter == 0);
			Assert.True(aGotLeave == 1, "During the mouse up we also happen to be off the widget.  Need to get a mouse leave event.");
			Assert.True(aGotEnterBounds == 0);
			Assert.True(aGotLeaveBounds == 1, "During the mouse up we also happen to be off the widget.  Need to get a mouse leave event.");
			Assert.True(aGotMove == 0);
			Assert.True(aGotUp == 1, "When we are captured we need to see mouse up messages.");

			// when captured make sure we see move events even when they are not above us.
			var regionB = new GuiWidget
			{
				Name = "regionB",
				BoundsRelativeToParent = new RectangleDouble(20, 20, 180, 180)
			};
			container.AddChild(regionB);
			int bGotEnter = 0;
			int bGotLeave = 0;
			int bGotEnterBounds = 0;
			int bGotLeaveBounds = 0;
			int bGotMove = 0;
			regionB.MouseEnter += (sender, e) =>
			{
				if (regionB.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				bGotEnter++;
			};
			regionB.MouseLeave += (sender, e) =>
			{
				if (regionB.UnderMouseState == UnderMouseState.FirstUnderMouse) { throw new Exception("It must not be under the mouse."); }

				bGotLeave++;
			};
			regionB.MouseEnterBounds += (sender, e) =>
			{
				if (regionB.UnderMouseState == UnderMouseState.NotUnderMouse) { throw new Exception("It must be under the mouse."); }

				bGotEnterBounds++;
			};
			regionB.MouseLeaveBounds += (sender, e) =>
			{
				if (regionB.UnderMouseState != UnderMouseState.NotUnderMouse) { throw new Exception("It must not be under the mouse."); }

				bGotLeaveBounds++;
			};
			regionB.MouseMove += (sender, e) => { bGotMove++; };

			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			// when captured regionA make sure regionB can not see move events
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 1);
			Assert.True(aGotLeave == 0);
			Assert.True(aGotEnterBounds == 1);
			Assert.True(aGotLeaveBounds == 0);
			Assert.True(aGotMove == 0);
			Assert.True(regionB.MouseCaptured == false);
			Assert.True(bGotEnter == 0);
			Assert.True(bGotLeave == 0);
			Assert.True(bGotEnterBounds == 0);
			Assert.True(bGotLeaveBounds == 0);
			Assert.True(bGotMove == 0);

			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 25, 25, 0));
			Assert.True(regionA.MouseCaptured == true);
			Assert.True(aGotEnter == 0);
			Assert.True(aGotLeave == 0, "We exited a into b but we don't check children on capture.");
			Assert.True(aGotEnterBounds == 0);
			Assert.True(aGotLeaveBounds == 0, "We exited a into b but we don't check children on capture.");
			Assert.True(aGotMove == 1);
			Assert.True(regionB.MouseCaptured == false);
			Assert.True(bGotEnter == 0);
			Assert.True(bGotLeave == 0);
			Assert.True(bGotEnterBounds == 0);
			Assert.True(bGotLeaveBounds == 0);
			Assert.True(bGotMove == 0);
		}

		[Fact]
		public void MouseCapturedSpressesLeaveEventsInButtonsSameAsRectangles()
		{
			var container = new GuiWidget
			{
				Name = "container",
				LocalBounds = new RectangleDouble(0, 0, 200, 200)
			};

			var buttonA = new Button
			{
				Name = "buttonA",
				BoundsRelativeToParent = new RectangleDouble(0, 0, 180, 180),
				OriginRelativeParent = new Vector2(10, 10)
			};
			container.AddChild(buttonA);
			bool aGotEnter = false;
			bool aGotLeave = false;
			bool aGotMove = false;
			double aMoveX = 0;
			double aMoveY = 0;
			buttonA.MouseEnter += (sender, e) => { aGotEnter = true; };
			buttonA.MouseLeave += (sender, e) => { aGotLeave = true; };
			buttonA.MouseMove += (sender, mouseEvent) =>
			{
				aGotMove = true;
				aMoveX = mouseEvent.X;
				aMoveY = mouseEvent.Y;
			};

			// make sure we know we are entered and captured on a down event
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(buttonA.FirstWidgetUnderMouse == true);
			Assert.True(buttonA.MouseCaptured == true);
			Assert.True(aGotEnter == true);
			Assert.True(aGotLeave == false);
			Assert.True(aGotMove == false);

			// make sure we stay on top when internal moves occur
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 16, 16, 0));
			Assert.True(buttonA.FirstWidgetUnderMouse == true);
			Assert.True(buttonA.MouseCaptured == true);
			Assert.True(aGotEnter == false);
			Assert.True(aGotLeave == false);
			Assert.True(aGotMove == true);
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 20, 20, 0));
			// lets prove that the move has been transformed into the correct coordinate system
			Assert.True(aMoveX == 10 && aMoveY == 10);
			Assert.True(buttonA.FirstWidgetUnderMouse == true);
			Assert.True(buttonA.MouseCaptured == true);
			Assert.True(aGotEnter == false);
			Assert.True(aGotLeave == false);
			Assert.True(aGotMove == true);

			// make sure we see leave events when captured
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			Assert.True(container.FirstWidgetUnderMouse == false);
			Assert.True(buttonA.FirstWidgetUnderMouse == false);
			Assert.True(buttonA.MouseCaptured == true);
			Assert.True(aGotEnter == false);
			Assert.True(aGotLeave == true);
			Assert.True(aGotMove == true);

			// make sure we see enter events when captured
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			Assert.True(buttonA.FirstWidgetUnderMouse == true);
			Assert.True(buttonA.MouseCaptured == true);
			Assert.True(aGotEnter == true);
			Assert.True(aGotLeave == false);
			Assert.True(aGotMove == true);
		}
	}
}