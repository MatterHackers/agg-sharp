/*
Copyright (c) 2025, Lars Brubaker
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
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class MouseInteractionTests
	{
		[Test]
		public async Task DoClickButtonInWindow()
		{
			int leftClickCount = 0;
			int rightClickCount = 0;

					AutomationTest testToRun = async (testRunner) =>
		{
			// Now do the actions specific to this test. (replace this for new tests)
			testRunner.ClickByName("left");
			testRunner.Delay(.5);

			await Assert.That(leftClickCount == 1).IsTrue();

			testRunner.ClickByName("right");
			testRunner.Delay(.5);

			await Assert.That(rightClickCount == 1).IsTrue();

			testRunner.DragDropByName("left", "right", offsetDrag: new Point2D(1, 0));
			testRunner.Delay(.5);

			await Assert.That(leftClickCount == 1).IsTrue();
		};

			var buttonContainer = new SystemWindow(300, 200);

			var leftButton = new Button("left", 10, 40)
			{
				Name = "left"
			};
			leftButton.Click += (sender, e) => { leftClickCount++; };
			buttonContainer.AddChild(leftButton);
			var rightButton = new Button("right", 110, 40);
			rightButton.Click += async (sender, e) => { rightClickCount++; };
			rightButton.Name = "right";
			buttonContainer.AddChild(rightButton);

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, testToRun, secondsToTestFailure: 30);
		}

		[Test]
		public async Task RadioButtonSiblingsAreChildren()
		{
			AutomationRunner.TimeToMoveMouse = .1;

			var buttonCount = 5;
					AutomationTest testToRun = async (testRunner) =>
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
			radioButton.Click += async (sender, e) =>
			{
				var buttons = buttonContainer.Children.ToArray();
				for (int j = 0; j < buttonCount; j++)
				{
					if (j == index)
					{
						await Assert.That(((IRadioButton)buttons[j]).Checked).IsTrue();
					}
					else
					{
						await Assert.That(((IRadioButton)buttons[j]).Checked).IsFalse();
					}
				}
			};
				buttonContainer.AddChild(radioButton);
			}

			await AutomationRunner.ShowWindowAndExecuteTests(buttonWindow, testToRun, secondsToTestFailure: 30);
		}

		[Test]
		public async Task ExtensionMethodsTests()
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
			await Assert.That(child == allWidgets[1]).IsTrue();
		}

		foreach (var child in level1.Children<GuiWidget>())
		{
			await Assert.That(child == allWidgets[2]).IsTrue();
		}

		foreach (var child in level2.Children<GuiWidget>())
		{
			await Assert.That(child == allWidgets[3]).IsTrue();
		}

		foreach (var child in level3.Children<GuiWidget>())
		{
			await Assert.That(false).IsTrue(); // there are no children we should not get here
		}

		int index = allWidgets.Count - 1;
		int parentCount = 0;
		foreach (var parent in level3.Parents<GuiWidget>())
		{
			parentCount++;
			await Assert.That(parent == allWidgets[--index]).IsTrue();
		}

		await Assert.That(parentCount == 3).IsTrue();
		}

		[Test]
		public async Task ValidateSimpleLeftClick()
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
			button.Click += async (sender, e) => { gotClick = true; };
			container.AddChild(button);

			await Assert.That(gotClick == false).IsTrue();
			await Assert.That(button.Focused == false).IsTrue();

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(gotClick == false).IsTrue();
			await Assert.That(button.Focused == false).IsTrue();

			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(gotClick == false).IsTrue();
			await Assert.That(button.Focused == true).IsTrue();

			await Assert.That(gotClick == false).IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			await Assert.That(gotClick == true).IsTrue();
			await Assert.That(button.Focused == true).IsTrue();

			gotClick = false;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(gotClick == false).IsTrue();
			await Assert.That(button.Focused == false).IsTrue();
		}

		[Test]
		public async Task ValidateOnlyTopWidgetGetsLeftClick()
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
			await Assert.That(gotClick == false).IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			await Assert.That(container.MouseCaptured == false).IsTrue();
			await Assert.That(blockingWidegt.MouseCaptured == false).IsTrue();
			await Assert.That(container.ChildHasMouseCaptured == true).IsTrue();
			await Assert.That(blockingWidegt.ChildHasMouseCaptured == false).IsTrue();
			await Assert.That(button.MouseCaptured == true).IsTrue();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			await Assert.That(container.MouseCaptured == false).IsTrue();
			await Assert.That(blockingWidegt.MouseCaptured == false).IsTrue();
			await Assert.That(button.MouseCaptured == false).IsTrue();
			await Assert.That(gotClick == true).IsTrue();

			gotClick = false;

			// the widget is in the way
			await Assert.That(gotClick == false).IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			await Assert.That(container.MouseCaptured == false).IsTrue();
			await Assert.That(blockingWidegt.MouseCaptured == true).IsTrue();
			await Assert.That(button.MouseCaptured == false).IsTrue();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			await Assert.That(container.MouseCaptured == false).IsTrue();
			await Assert.That(blockingWidegt.MouseCaptured == false).IsTrue();
			await Assert.That(button.MouseCaptured == false).IsTrue();
			await Assert.That(gotClick == false).IsTrue();
		}

		[Test]
		public async Task ValidateSimpleMouseUpDown()
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
			topWidget.MouseDown += async (sender, e) => { topWidgetGotMouseDownInBounds++; };
			container.AddChild(topWidget);

			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 0).IsTrue();
			// down outside everything
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, -10, -10, 0));
			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 0).IsTrue();
			await Assert.That(containerGotMouseDown == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 0).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 0).IsTrue();
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			// up outside everything
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, -10, -10, 0));
			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 0).IsTrue();
			await Assert.That(containerGotMouseDown == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 0).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 0).IsTrue();
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			// down on container
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 0).IsTrue();
			await Assert.That(containerGotMouseDown == 1).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 0).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 1).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 0).IsTrue();
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(containerGotMouseUp == 1).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 0).IsTrue();
			await Assert.That(containerGotMouseDown == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 0).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 0).IsTrue();
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 0).IsTrue();
			await Assert.That(containerGotMouseDown == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 1).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 1).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 1).IsTrue();
			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 1).IsTrue();
			await Assert.That(containerGotMouseDown == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 0).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 0).IsTrue();

			topWidgetGotMouseUp = topWidgetGotMouseDown = topWidgetGotMouseDownInBounds = 0;
			containerGotMouseDown = containerGotMouseUp = containerGotMouseDownInBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			await Assert.That(containerGotMouseUp == 0).IsTrue();
			await Assert.That(topWidgetGotMouseUp == 1).IsTrue();
			await Assert.That(containerGotMouseDown == 0).IsTrue();
			await Assert.That(topWidgetGotMouseDown == 1).IsTrue();
			await Assert.That(containerGotMouseDownInBounds == 1).IsTrue();
			await Assert.That(topWidgetGotMouseDownInBounds == 1).IsTrue();
		}

		[Test]
		public async Task ValidateOnlyTopWidgetGetsMouseUp()
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
			blockingWidegt.MouseUpCaptured += async (sender, e) => { blockingGotMouseUp = true; };
			blockingWidegt.LocalBounds = new RectangleDouble(105, 105, 125, 125);
			container.AddChild(blockingWidegt);

			// the widget is not in the way
			await Assert.That(topGotMouseUp == false).IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 101, 101, 0));
			await Assert.That(blockingGotMouseUp == false).IsTrue();
			await Assert.That(topGotMouseUp == true).IsTrue();

			topGotMouseUp = false;

			// the widget is in the way
			await Assert.That(topGotMouseUp == false).IsTrue();
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 110, 110, 0));
			await Assert.That(blockingGotMouseUp == true).IsTrue();
			await Assert.That(topGotMouseUp == false).IsTrue();
		}

		[Test]
		public async Task ValidateEnterAndLeaveEvents()
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

			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(mouseLeave == 0).IsTrue();
			await Assert.That(mouseEnter == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 0).IsTrue();
			await Assert.That(mouseEnterBounds == 0).IsTrue();

			// put the mouse into the widget but outside regionA
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(regionA.UnderMouseState == UnderMouseState.NotUnderMouse).IsTrue();
			await Assert.That(mouseLeave == 0).IsTrue();
			await Assert.That(mouseEnter == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 0).IsTrue();
			await Assert.That(mouseEnterBounds == 0).IsTrue();

			// move it into regionA
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse).IsTrue();
			await Assert.That(mouseLeave == 0).IsTrue();
			await Assert.That(mouseEnter == 1).IsTrue();
			await Assert.That(mouseLeaveBounds == 0).IsTrue();
			await Assert.That(mouseEnterBounds == 1).IsTrue();

			// now move it inside regionA and make sure it does not re-trigger either event
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(regionA.UnderMouseState == UnderMouseState.FirstUnderMouse).IsTrue();
			await Assert.That(mouseLeave == 0).IsTrue();
			await Assert.That(mouseEnter == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 0).IsTrue();
			await Assert.That(mouseEnterBounds == 0).IsTrue();

			// now leave and make sure we see the leave
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, -5, -5, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(mouseLeave == 1).IsTrue();
			await Assert.That(mouseEnter == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 1).IsTrue();
			await Assert.That(mouseEnterBounds == 0).IsTrue();

			// move back on
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			// now leave only the inside widget and make sure we see the leave
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(mouseEnter == 1).IsTrue();
			await Assert.That(mouseLeave == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 0).IsTrue();
			await Assert.That(mouseEnterBounds == 1).IsTrue();

			// move off
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(mouseLeave == 1).IsTrue();
			await Assert.That(mouseEnter == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 1).IsTrue();
			await Assert.That(mouseEnterBounds == 0).IsTrue();

			// click back on
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 16, 15, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 1).IsTrue();
			await Assert.That(mouseUp == 1).IsTrue();
			await Assert.That(mouseEnter == 1).IsTrue();
			await Assert.That(mouseLeave == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 0).IsTrue();
			await Assert.That(mouseEnterBounds == 1).IsTrue();

			// click off
			mouseDown = mouseUp = mouseLeave = mouseEnter = mouseLeaveBounds = mouseEnterBounds = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			UiThread.InvokePendingActions();
			await Assert.That(mouseDown == 0).IsTrue();
			await Assert.That(mouseUp == 0).IsTrue();
			await Assert.That(mouseLeave == 1).IsTrue();
			await Assert.That(mouseEnter == 0).IsTrue();
			await Assert.That(mouseLeaveBounds == 1).IsTrue();
			await Assert.That(mouseEnterBounds == 0).IsTrue();
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

		[Test]
		public async Task ValidateEnterAndLeaveEventsWhenNested()
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
			regionA.MouseEnter += async (sender, e) =>
			{
				await Assert.That(regionA.UnderMouseState).IsEqualTo(UnderMouseState.FirstUnderMouse);//, "It must be the first under the mouse.");
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				await Assert.That(container.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				gotEnterA++;
			};
			regionA.MouseLeave += async (sender, e) =>
			{
				await Assert.That(regionA.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				gotLeaveA++;
			};
			int gotEnterBoundsA = 0;
			int gotLeaveBoundsA = 0;
			regionA.MouseEnterBounds += async (sender, e) =>
			{
				await Assert.That(regionA.UnderMouseState).IsEqualTo(UnderMouseState.FirstUnderMouse);//, "It must be the first under the mouse.");
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				await Assert.That(container.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				gotEnterBoundsA++;
			};
			regionA.MouseLeaveBounds += async (sender, e) =>
			{
				await Assert.That(regionA.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				gotLeaveBoundsA++;
			};

			regionB.Name = "regionB";
			regionB.AddChild(regionA);
			regionB.SetBoundsToEncloseChildren();
			container.AddChild(regionB);
			int gotEnterB = 0;
			int gotLeaveB = 0;
			regionB.MouseEnter += async (sender, e) =>
			{
				await Assert.That(regionA.UnderMouseState).IsEqualTo(UnderMouseState.FirstUnderMouse);//, "It must be the first under the mouse.");
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				await Assert.That(container.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				gotEnterB++;
			};
			regionB.MouseLeave += async (sender, e) =>
			{
				await Assert.That(regionA.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				await Assert.That(container.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				gotLeaveB++;
			};
			int gotEnterBoundsB = 0;
			int gotLeaveBoundsB = 0;
			regionB.MouseEnterBounds += async (sender, e) =>
			{
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				await Assert.That(container.UnderMouseState).IsEqualTo(UnderMouseState.UnderMouseNotFirst);//, "It must be under the mouse not first.");
				gotEnterBoundsB++;
			};
			regionB.MouseLeaveBounds += async (sender, e) =>
			{
				await Assert.That(regionB.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);//, "It must be not under the mouse.");
				gotLeaveBoundsB++;
			};

			await Assert.That(gotLeaveA == 0).IsTrue();
			await Assert.That(gotEnterA == 0).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();

			// put the mouse into the widget but outside regionA and region B
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			await Assert.That(gotLeaveA == 0).IsTrue();
			await Assert.That(gotEnterA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 0).IsTrue();
			await Assert.That(gotEnterBoundsA == 0).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 0).IsTrue();
			await Assert.That(gotEnterBoundsB == 0).IsTrue();

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
			await Assert.That(gotLeaveA == 0).IsTrue();
			await Assert.That(gotEnterA == 1).IsTrue();
			await Assert.That(gotLeaveBoundsA == 0).IsTrue();
			await Assert.That(gotEnterBoundsA == 1).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 0).IsTrue();
			await Assert.That(gotEnterBoundsB == 1).IsTrue();

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
			await Assert.That(gotLeaveA == 0).IsTrue();
			await Assert.That(gotEnterA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 0).IsTrue();
			await Assert.That(gotEnterBoundsA == 0).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 0).IsTrue();
			await Assert.That(gotEnterBoundsB == 0).IsTrue();

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
			await Assert.That(gotLeaveA == 1).IsTrue();
			await Assert.That(gotEnterA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 1).IsTrue();
			await Assert.That(gotEnterBoundsA == 0).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 1).IsTrue();
			await Assert.That(gotEnterBoundsB == 0).IsTrue();

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
			await Assert.That(gotEnterA == 1).IsTrue();
			await Assert.That(gotLeaveA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 0).IsTrue();
			await Assert.That(gotEnterBoundsA == 1).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 0).IsTrue();
			await Assert.That(gotEnterBoundsB == 1).IsTrue();

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
			await Assert.That(gotLeaveA == 1).IsTrue();
			await Assert.That(gotEnterA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 1).IsTrue();
			await Assert.That(gotEnterBoundsA == 0).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 1).IsTrue();
			await Assert.That(gotEnterBoundsB == 0).IsTrue();

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
			await Assert.That(gotEnterA == 1).IsTrue();
			await Assert.That(gotLeaveA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 0).IsTrue();
			await Assert.That(gotEnterBoundsA == 1).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 0).IsTrue();
			await Assert.That(gotEnterBoundsB == 1).IsTrue();

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
			await Assert.That(gotLeaveA == 1).IsTrue();
			await Assert.That(gotEnterA == 0).IsTrue();
			await Assert.That(gotLeaveBoundsA == 1).IsTrue();
			await Assert.That(gotEnterBoundsA == 0).IsTrue();
			await Assert.That(gotLeaveB == 0).IsTrue();
			await Assert.That(gotEnterB == 0).IsTrue();
			await Assert.That(gotLeaveBoundsB == 1).IsTrue();
			await Assert.That(gotEnterBoundsB == 0).IsTrue();
		}

		[Test]
		public async Task ValidateEnterAndLeaveEventsWhenCoverd()
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

			await Assert.That(gotLeaveCover == 0).IsTrue();
			await Assert.That(gotEnterCover == 0).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();

			// put the mouse into the widget but outside the children
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			await Assert.That(gotLeaveCover == 0).IsTrue();
			await Assert.That(gotEnterCover == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCover == 0).IsTrue();
			await Assert.That(gotEnterBoundsCover == 0).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCovered == 0).IsTrue();
			await Assert.That(gotEnterBoundsCovered == 0).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterBoundsCoveredChild == 0).IsTrue();

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
			await Assert.That(gotLeaveCover == 0).IsTrue();
			await Assert.That(gotEnterCover == 1).IsTrue();
			await Assert.That(gotLeaveBoundsCover == 0).IsTrue();
			await Assert.That(gotEnterBoundsCover == 1).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCovered == 0).IsTrue();
			await Assert.That(gotEnterBoundsCovered == 0).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterBoundsCoveredChild == 0).IsTrue();

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
			await Assert.That(gotLeaveCover == 0).IsTrue();
			await Assert.That(gotEnterCover == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCover == 0).IsTrue();
			await Assert.That(gotEnterBoundsCover == 0).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCovered == 0).IsTrue();
			await Assert.That(gotEnterBoundsCovered == 0).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterBoundsCoveredChild == 0).IsTrue();

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
			await Assert.That(gotLeaveCover == 1).IsTrue();
			await Assert.That(gotEnterCover == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCover == 1).IsTrue();
			await Assert.That(gotEnterBoundsCover == 0).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCovered == 0).IsTrue();
			await Assert.That(gotEnterBoundsCovered == 0).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterBoundsCoveredChild == 0).IsTrue();

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
			await Assert.That(gotEnterCover == 1).IsTrue();
			await Assert.That(gotLeaveCover == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCover == 0).IsTrue();
			await Assert.That(gotEnterBoundsCover == 1).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCovered == 0).IsTrue();
			await Assert.That(gotEnterBoundsCovered == 1).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterBoundsCoveredChild == 1).IsTrue();

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
			await Assert.That(gotLeaveCover == 1).IsTrue();
			await Assert.That(gotEnterCover == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCover == 1).IsTrue();
			await Assert.That(gotEnterBoundsCover == 0).IsTrue();
			await Assert.That(gotLeaveCovered == 0).IsTrue();
			await Assert.That(gotEnterCovered == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCovered == 1).IsTrue();
			await Assert.That(gotEnterBoundsCovered == 0).IsTrue();
			await Assert.That(gotLeaveCoveredChild == 0).IsTrue();
			await Assert.That(gotEnterCoveredChild == 0).IsTrue();
			await Assert.That(gotLeaveBoundsCoveredChild == 1).IsTrue();
			await Assert.That(gotEnterBoundsCoveredChild == 0).IsTrue();
		}

		[Test]
		public async Task ValidateEnterAndLeaveInOverlapArea()
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

			await Assert.That(topGotEnter == 0).IsTrue();
			await Assert.That(topGotLeave == 0).IsTrue();
			await Assert.That(topGotEnterBounds == 0).IsTrue();
			await Assert.That(topGotLeaveBounds == 0).IsTrue();

			// move into the bottom widget only
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 15, 0));
			await Assert.That(bottomGotLeave == 0).IsTrue();
			await Assert.That(bottomGotEnter == 0).IsTrue();
			await Assert.That(bottomGotLeaveBounds == 0).IsTrue();
			await Assert.That(bottomGotEnterBounds == 0).IsTrue();
			await Assert.That(topGotLeave == 0).IsTrue();
			await Assert.That(topGotEnter == 0).IsTrue();
			await Assert.That(topGotLeaveBounds == 0).IsTrue();
			await Assert.That(topGotEnterBounds == 0).IsTrue();

			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(bottomGotLeave == 0).IsTrue();
			await Assert.That(bottomGotEnter == 1).IsTrue();
			await Assert.That(bottomGotLeaveBounds == 0).IsTrue();
			await Assert.That(bottomGotEnterBounds == 1).IsTrue();
			await Assert.That(topGotLeave == 0).IsTrue();
			await Assert.That(topGotEnter == 0).IsTrue();
			await Assert.That(topGotLeaveBounds == 0).IsTrue();
			await Assert.That(topGotEnterBounds == 0).IsTrue();

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move out of the bottom widget only
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 15, 0));
			await Assert.That(bottomGotLeave == 1).IsTrue();
			await Assert.That(bottomGotEnter == 0).IsTrue();
			await Assert.That(bottomGotLeaveBounds == 1).IsTrue();
			await Assert.That(bottomGotEnterBounds == 0).IsTrue();
			await Assert.That(topGotLeave == 0).IsTrue();
			await Assert.That(topGotEnter == 0).IsTrue();
			await Assert.That(topGotLeaveBounds == 0).IsTrue();
			await Assert.That(topGotEnterBounds == 0).IsTrue();

			// move to just outside both widgets
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 25, 0));
			await Assert.That(bottomWidget.TransformToScreenSpace(bottomWidget.LocalBounds).Contains(1, 25)).IsFalse();
			await Assert.That(topWidget.TransformToScreenSpace(topWidget.LocalBounds).Contains(1, 25)).IsFalse();
			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move over the top widget when it is over the bottom widget (only the top should see this)
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 25, 0));
			await Assert.That(bottomGotEnter == 0).IsTrue();
			await Assert.That(bottomGotLeave == 0).IsTrue();
			await Assert.That(bottomGotEnterBounds == 1).IsTrue();
			await Assert.That(bottomGotLeaveBounds == 0).IsTrue();
			await Assert.That(topGotEnter == 1).IsTrue();
			await Assert.That(topGotLeave == 0).IsTrue();
			await Assert.That(topGotEnterBounds == 1).IsTrue();
			await Assert.That(topGotLeaveBounds == 0).IsTrue();

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move out of the top widget into the bottom
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(bottomGotEnter == 1).IsTrue();
			await Assert.That(bottomGotLeave == 0).IsTrue();
			await Assert.That(bottomGotEnterBounds == 0).IsTrue();
			await Assert.That(bottomGotLeaveBounds == 0).IsTrue();
			await Assert.That(topGotEnter == 0).IsTrue();
			await Assert.That(topGotLeave == 1).IsTrue();
			await Assert.That(topGotEnterBounds == 0).IsTrue();
			await Assert.That(topGotLeaveBounds == 1).IsTrue();

			// clear our states
			bottomGotEnter = bottomGotLeave = bottomGotEnterBounds = bottomGotLeaveBounds = 0;
			topGotEnter = topGotLeave = topGotEnterBounds = topGotLeaveBounds = 0;
			// move back up into the top and make sure we see the leave in the bottom
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 25, 0));
			await Assert.That(bottomGotEnter == 0).IsTrue();
			await Assert.That(bottomGotLeave == 1).IsTrue();
			await Assert.That(bottomGotEnterBounds == 0).IsTrue();
			await Assert.That(bottomGotLeaveBounds == 0).IsTrue();
			await Assert.That(topGotEnter == 1).IsTrue();
			await Assert.That(topGotLeave == 0).IsTrue();
			await Assert.That(topGotEnterBounds == 1).IsTrue();
			await Assert.That(topGotLeaveBounds == 0).IsTrue();
		}

		[Test]
		public async Task MouseCapturedSpressesLeaveEvents()
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
			regionA.MouseUpCaptured += async (sender, e) => { aGotUp++; };

			// make sure we know we are entered and captured on a down event
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(regionA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 1).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 1).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 0).IsTrue();

			// make sure we stay on top when internal moves occur
			aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 16, 16, 0));
			await Assert.That(regionA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 0).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 0).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 1).IsTrue();

			// make sure we see leave events when captured
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			await Assert.That(container.FirstWidgetUnderMouse == false).IsTrue();
			await Assert.That(regionA.FirstWidgetUnderMouse == false).IsTrue();
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 0).IsTrue();
			await Assert.That(aGotLeave == 1).IsTrue();
			await Assert.That(aGotEnterBounds == 0).IsTrue();
			await Assert.That(aGotLeaveBounds == 1).IsTrue();
			await Assert.That(aGotMove == 1).IsTrue();

			// make sure we see enter events when captured
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(regionA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 1).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 1).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 1).IsTrue();

			// and we are not captured after mouseup above region
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(regionA.MouseCaptured == false).IsTrue();
			await Assert.That(aGotEnter == 0).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 0).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 0).IsTrue();
			await Assert.That(aGotUp == 1).IsTrue();

			// make sure we are not captured after mouseup above off region
			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 0).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 0).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 0).IsTrue();
			container.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			await Assert.That(regionA.MouseCaptured == false).IsTrue();
			await Assert.That(aGotEnter == 0).IsTrue();
			await Assert.That(aGotLeave == 1).IsTrue();
			await Assert.That(aGotEnterBounds == 0).IsTrue();
			await Assert.That(aGotLeaveBounds == 1).IsTrue();
			await Assert.That(aGotMove == 0).IsTrue();
			await Assert.That(aGotUp == 1).IsTrue();

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
			regionB.MouseMove += async (sender, e) => { bGotMove++; };

			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			// when captured regionA make sure regionB can not see move events
			container.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 1).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 1).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 0).IsTrue();
			await Assert.That(regionB.MouseCaptured == false).IsTrue();
			await Assert.That(bGotEnter == 0).IsTrue();
			await Assert.That(bGotLeave == 0).IsTrue();
			await Assert.That(bGotEnterBounds == 0).IsTrue();
			await Assert.That(bGotLeaveBounds == 0).IsTrue();
			await Assert.That(bGotMove == 0).IsTrue();

			aGotUp = aGotEnter = aGotLeave = aGotEnterBounds = aGotLeaveBounds = aGotMove = 0;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 25, 25, 0));
			await Assert.That(regionA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == 0).IsTrue();
			await Assert.That(aGotLeave == 0).IsTrue();
			await Assert.That(aGotEnterBounds == 0).IsTrue();
			await Assert.That(aGotLeaveBounds == 0).IsTrue();
			await Assert.That(aGotMove == 1).IsTrue();
			await Assert.That(regionB.MouseCaptured == false).IsTrue();
			await Assert.That(bGotEnter == 0).IsTrue();
			await Assert.That(bGotLeave == 0).IsTrue();
			await Assert.That(bGotEnterBounds == 0).IsTrue();
			await Assert.That(bGotLeaveBounds == 0).IsTrue();
			await Assert.That(bGotMove == 0).IsTrue();
		}

		[Test]
		public async Task MouseCapturedSpressesLeaveEventsInButtonsSameAsRectangles()
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
			await Assert.That(buttonA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(buttonA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == true).IsTrue();
			await Assert.That(aGotLeave == false).IsTrue();
			await Assert.That(aGotMove == false).IsTrue();

			// make sure we stay on top when internal moves occur
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 16, 16, 0));
			await Assert.That(buttonA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(buttonA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == false).IsTrue();
			await Assert.That(aGotLeave == false).IsTrue();
			await Assert.That(aGotMove == true).IsTrue();
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 20, 20, 0));
			// lets prove that the move has been transformed into the correct coordinate system
			await Assert.That(aMoveX == 10 && aMoveY == 10).IsTrue();
			await Assert.That(buttonA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(buttonA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == false).IsTrue();
			await Assert.That(aGotLeave == false).IsTrue();
			await Assert.That(aGotMove == true).IsTrue();

			// make sure we see leave events when captured
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			await Assert.That(container.FirstWidgetUnderMouse == false).IsTrue();
			await Assert.That(buttonA.FirstWidgetUnderMouse == false).IsTrue();
			await Assert.That(buttonA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == false).IsTrue();
			await Assert.That(aGotLeave == true).IsTrue();
			await Assert.That(aGotMove == true).IsTrue();

			// make sure we see enter events when captured
			aGotEnter = aGotLeave = aGotMove = false;
			container.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
			await Assert.That(buttonA.FirstWidgetUnderMouse == true).IsTrue();
			await Assert.That(buttonA.MouseCaptured == true).IsTrue();
			await Assert.That(aGotEnter == true).IsTrue();
			await Assert.That(aGotLeave == false).IsTrue();
			await Assert.That(aGotMove == true).IsTrue();
		}
	}
}
