/*
Copyright (c) 2017, John Lewin, Lars Brubaker
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

using System.Threading.Tasks;
using Agg.Tests.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;

namespace Matter_CAD_Lib.Tests.AutomationTests
{
    [MhTestFixture("Opens Winforms Window")]
    public class WidgetClickTests
	{
        [MhTest]
        public async Task ClickFiresOnCorrectWidgets()
		{
			var testWindow = new ClickTestsWindow(300, 200);

			await AutomationRunner.ShowWindowAndExecuteTests(
				testWindow,
				(testRunner) =>
				{
					testRunner.ClickByName("blueWidget");
					testRunner.Delay(.1);
					MhAssert.Equal(1, testWindow.BlueWidget.ClickCount);//, "Unexpected click count on blue widget");
                    MhAssert.Equal(0, testWindow.OrangeWidget.ClickCount);//, "Unexpected click count on orange widget");
                    MhAssert.Equal(0, testWindow.PurpleWidget.ClickCount);//, "Unexpected click count on purple widget");

                    testRunner.ClickByName("orangeWidget");
					testRunner.Delay(.1);
					MhAssert.Equal(1, testWindow.BlueWidget.ClickCount);//, "Unexpected click count on blue widget");
                    MhAssert.Equal(1, testWindow.OrangeWidget.ClickCount);//, "Unexpected click count on orange widget");
                    MhAssert.Equal(0, testWindow.PurpleWidget.ClickCount);//, "Unexpected click count on purple widget");

                    testRunner.ClickByName("blueWidget");
					testRunner.Delay(.1);
					MhAssert.Equal(2, testWindow.BlueWidget.ClickCount);//, "Unexpected click count on blue widget");
                    MhAssert.Equal(1, testWindow.OrangeWidget.ClickCount);//, "Unexpected click count on orange widget");
                    MhAssert.Equal(0, testWindow.PurpleWidget.ClickCount);//, "Unexpected click count on purple widget");

                    testRunner.ClickByName("orangeWidget");
					testRunner.Delay(.1);
					MhAssert.Equal(2, testWindow.BlueWidget.ClickCount);//, "Unexpected click count on root widget");
                    MhAssert.Equal(2, testWindow.OrangeWidget.ClickCount);//, "Unexpected click count on orange widget");
                    MhAssert.Equal(0, testWindow.PurpleWidget.ClickCount);//, "Unexpected click count on purple widget");

                    testRunner.ClickByName("purpleWidget");
					testRunner.Delay(.1);
					MhAssert.Equal(2, testWindow.BlueWidget.ClickCount);//, "Unexpected click count on blue widget");
                    MhAssert.Equal(2, testWindow.OrangeWidget.ClickCount);//, "Unexpected click count on orange widget");
                    MhAssert.Equal(1, testWindow.PurpleWidget.ClickCount);//, "Unexpected click count on purple widget");

                    return Task.CompletedTask;
				});
		}

        [MhTest]
        public async Task ClickSuppressedOnExternalMouseUp()
		{
			var testWindow = new ClickTestsWindow(300, 200);
			var bounds = testWindow.BlueWidget.BoundsRelativeToParent;
			var mouseDownPosition = new Vector2(bounds.Left + 25, bounds.Bottom + 4);

			await AutomationRunner.ShowWindowAndExecuteTests(
				testWindow,
				(testRunner) =>
				{
					MouseEventArgs mouseEvent;

					// ** Click should occur on mouse[down/up] within the controls bounds **
					//
					// Move to a position within the blueWidget for mousedown
					mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, mouseDownPosition.X, mouseDownPosition.Y, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseDown(mouseEvent);

					// Move to a position within blueWidget for mouseup
					mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, bounds.Center.X, bounds.Center.Y, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseUp(mouseEvent);

					MhAssert.Equal(1, testWindow.BlueWidget.ClickCount);//, "Expected 1 click on root widget");

                    // ** Click should not occur when mouse up is outside of the control bounds **
                    //
                    // Move to a position within BlueWidget for mousedown
                    mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, mouseDownPosition.X, mouseDownPosition.Y, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseDown(mouseEvent);

					// Move to a position **outside** of BlueWidget for mouseup
					mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, 50, 50, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseUp(mouseEvent);

					// There should be no increment in the click count
					MhAssert.Equal(1, testWindow.BlueWidget.ClickCount);//, "Expected 1 click on root widget");

                    return Task.CompletedTask;
				});
		}

        [MhTest]
        public async Task ClickSuppressedOnMouseUpWithinChild2()
		{
			// Agg currently fires mouse up events in child controls when the parent has the mouse captured
			// and is performing drag like operations. If the mouse goes down in the parent and comes up on the child
			// neither control should get a click event

			var testWindow = new ClickTestsWindow(300, 200);
			var bounds = testWindow.BlueWidget.BoundsRelativeToParent;
			var mouseDownPosition = new Vector2(bounds.Left + 25, bounds.Bottom + 4);

			var childBounds = testWindow.OrangeWidget.BoundsRelativeToParent;
			double childX = bounds.Left + childBounds.Center.X;
			double childY = bounds.Bottom + childBounds.Center.Y;

			await AutomationRunner.ShowWindowAndExecuteTests(
				testWindow,
				(testRunner) =>
				{
					MouseEventArgs mouseEvent;

					// ** Click should occur on mouse[down/up] within the controls bounds **
					//
					// Move to a position within BlueWidget for mousedown
					mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, mouseDownPosition.X, mouseDownPosition.Y, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseDown(mouseEvent);

					// Move to a position within BlueWidget for mouseup
					mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, bounds.Center.X, bounds.Center.Y, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseUp(mouseEvent);

					MhAssert.Equal(1, testWindow.BlueWidget.ClickCount);//, "Expected 1 click on root widget");

                    // ** Click should not occur when mouse up occurs on child controls **
                    //
                    // Move to a position within BlueWidget for mousedown
                    mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, mouseDownPosition.X, mouseDownPosition.Y, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseDown(mouseEvent);

					// Move to a position with the OrangeWidget for mouseup
					mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, childX, childY, 0);
					testRunner.SetMouseCursorPosition(testWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
					testWindow.OnMouseUp(mouseEvent);

					// There should be no increment in the click count
					MhAssert.Equal(1, testWindow.BlueWidget.ClickCount);//, "Expected click count to not increment on mouse up within child control");

                    return Task.CompletedTask;
				});
		}

		// Test SystemWindow with three clickable controls
		private class ClickTestsWindow : SystemWindow
		{
			private GuiWidget lastClicked = null;

			public ClickableWidget BlueWidget { get; }
			public ClickableWidget OrangeWidget { get; }
			public ClickableWidget PurpleWidget { get; }

			public ClickTestsWindow(double width, double height) : base(width, height)
			{
				this.Padding = 50;

				// A clickable widget containing child controls
				this.BlueWidget = new ClickableWidget()
				{
					Name = "blueWidget",
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit | VAnchor.Center,
					BackgroundColor = Color.Blue,
					Padding = 8
				};
				this.BlueWidget.Click += Widget_Click;
				this.AddChild(BlueWidget);

				// A clickable child control
				this.OrangeWidget = new ClickableWidget()
				{
					Name = "orangeWidget",
					HAnchor = HAnchor.Left,
					BackgroundColor = Color.Orange
				};
				this.OrangeWidget.Click += Widget_Click;
				this.BlueWidget.AddChild(OrangeWidget);

				// A clickable child control
				this.PurpleWidget = new ClickableWidget()
				{
					Name = "purpleWidget",
					HAnchor = HAnchor.Right,
					BackgroundColor = new Color(141, 0, 206)
				};
				this.PurpleWidget.Click += Widget_Click;
				this.BlueWidget.AddChild(PurpleWidget);
			}

			// Default click event listener which updates lastClicked and increments widget.ClickCount
			private void Widget_Click(object sender, MouseEventArgs e)
			{
				if (sender is ClickableWidget clickWidget)
				{
					clickWidget.ClickCount += 1;
					lastClicked = clickWidget;
				}
			}

			// Test class with a default size and a field to track click counts
			public class ClickableWidget : GuiWidget
			{
				public int ClickCount { get; set; } = 0;

				public ClickableWidget()
				{
					this.Width = 35;
					this.Height = 25;
				}
			}
		}
	}
}
