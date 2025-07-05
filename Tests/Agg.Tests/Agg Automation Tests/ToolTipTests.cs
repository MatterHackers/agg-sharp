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

using MatterHackers.GuiAutomation;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
    internal class TempData
	{
		internal string lastShownText;
		internal int showCount;
		internal int popCount;
	}

    
	public class ToolTipTests
	{
		static readonly string toolTip1Text = "toolTip1";
		static readonly string toolTip2Text = "toolTip2";

		static readonly int minMsTimeToRespond = 60;
		static readonly int minMsToBias = 80;

        [Test]
        public async Task ToolTipInitialOpenTests()
		{
			TempData tempData = new TempData();
			// test simple open then wait for pop
			SystemWindow systemWindow = CreateTwoChildWindow(tempData);

			// move into the first widget 
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 11, 11, 0));
			UiThread.InvokePendingActions();

			// show that initially we don't have a tooltip
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();

			// sleep 1/2 long enough to show the tool tip
			Thread.Sleep((int)(ToolTipManager.InitialDelay / 2 * 1000 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure it is still not up
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.showCount == 0).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();

			// sleep 1/2 long enough to show the tool tip
			Thread.Sleep((int)(ToolTipManager.InitialDelay / 2 * 1000 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip came up
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.showCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip1Text).IsTrue();

			// wait 1/2 long enough for the tool tip to go away
			Thread.Sleep((int)(ToolTipManager.AutoPopDelay * 1000 / 2 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool did not go away
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.popCount == 0).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip1Text).IsTrue();

			// wait 1/2 long enough for the tool tip to go away
			Thread.Sleep((int)(ToolTipManager.AutoPopDelay * 1000 / 2 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip went away
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();
		}

        [Test]
        public async Task ToolTipsShow()
		{
			SystemWindow buttonContainer = new SystemWindow(300, 200)
			{
				BackgroundColor = Color.White,
			};

			AutomationTest testToRun = async (testRunner) =>
			{
				testRunner.Delay(1);

				testRunner.MoveToByName("ButtonWithToolTip");
				testRunner.Delay(1.5);
				GuiWidget toolTipWidget = buttonContainer.FindDescendant("ToolTipWidget");
				await Assert.That(toolTipWidget != null).IsTrue();
				testRunner.MoveToByName("right");
				toolTipWidget = buttonContainer.FindDescendant("ToolTipWidget");
				await Assert.That(toolTipWidget == null).IsTrue();

				testRunner.Delay(1);
				buttonContainer.CloseOnIdle();
			};

			Button leftButton = new Button("left", 10, 40);
			leftButton.Name = "ButtonWithToolTip";
			leftButton.ToolTipText = "Left Tool Tip";
			buttonContainer.AddChild(leftButton);
			Button rightButton = new Button("right", 110, 40);
			rightButton.Name = "right";
			buttonContainer.AddChild(rightButton);

			await AutomationRunner.ShowWindowAndExecuteTests(buttonContainer, testToRun);
		}

        [Test]
        public async Task ToolTipCloseOnLeave()
		{
			TempData tempData = new TempData();
			SystemWindow systemWindow = CreateTwoChildWindow(tempData);

			// move into the first widget 
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 11, 11, 0));
			UiThread.InvokePendingActions();

			// sleep long enough to show the tool tip
			Thread.Sleep((int)(ToolTipManager.InitialDelay * 1000 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip came up
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.showCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip1Text).IsTrue();

			// move off the first widget 
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 9, 9, 0));
			Thread.Sleep(minMsTimeToRespond); // sleep enough for the tool tip to want to respond
			UiThread.InvokePendingActions();

			// make sure the tool tip went away
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();
		}

        [Test]
        public async Task MoveFromToolTipToToolTip()
		{
			TempData tempData = new TempData();
			SystemWindow systemWindow = CreateTwoChildWindow(tempData);

			// move into the first widget 
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 11, 11, 0));
			UiThread.InvokePendingActions();

			// sleep long enough to show the tool tip
			Thread.Sleep((int)(ToolTipManager.InitialDelay * 1000 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip came up
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.showCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip1Text).IsTrue();

			// move off the first widget
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 29, 29, 0));
			Thread.Sleep(minMsTimeToRespond); // sleep enough for the tool tip to want to respond
			UiThread.InvokePendingActions();

			// make sure the first tool tip went away 
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();

			// sleep long enough to clear the fast move time
			Thread.Sleep((int)(ToolTipManager.ReshowDelay * 1000 * 2));
			UiThread.InvokePendingActions();

			// make sure the first tool still gone
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();

			// move onto the other widget
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 31, 31, 0));
			Thread.Sleep(minMsTimeToRespond); // sleep enough for the tool tip to want to respond
			UiThread.InvokePendingActions();

			// make sure the first tool tip still gone
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();

			// wait 1/2 long enough for the second tool tip to come up
			Thread.Sleep((int)(ToolTipManager.InitialDelay * 1000 / 2 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the second tool tip not showing
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();

			// wait 1/2 long enough for the second tool tip to come up
			Thread.Sleep((int)(ToolTipManager.AutoPopDelay * 1000 / 2 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip 2 came up
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.showCount == 2).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip2Text).IsTrue();
		}

        [Test]
        public async Task MoveFastFromToolTipToToolTip()
		{
			TempData tempData = new TempData();
			SystemWindow systemWindow = CreateTwoChildWindow(tempData);

			// move into the first widget 
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 11, 11, 0));
			UiThread.InvokePendingActions();

			// sleep long enough to show the tool tip
			Thread.Sleep((int)(ToolTipManager.InitialDelay * 1000 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip came up
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.showCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip1Text).IsTrue();

			// wait 1/2 long enough for the tool tip to go away
			Thread.Sleep((int)(ToolTipManager.AutoPopDelay * 1000 / 2 + minMsToBias));
			UiThread.InvokePendingActions();

			// move onto the other widget
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 31, 31, 0));
			Thread.Sleep(minMsTimeToRespond); // sleep enough for the tool tip to want to respond
			UiThread.InvokePendingActions();

			// make sure the first tool tip went away 
			await Assert.That(systemWindow.Children.Count == 2).IsTrue();
			await Assert.That(tempData.popCount == 1).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == "").IsTrue();

			// wait long enough for the second tool tip to come up
			Thread.Sleep((int)(ToolTipManager.ReshowDelay * 1000 + minMsToBias));
			UiThread.InvokePendingActions();

			// make sure the tool tip 2 came up
			await Assert.That(systemWindow.Children.Count == 3).IsTrue();
			await Assert.That(tempData.showCount == 2).IsTrue();
			await Assert.That(systemWindow.ToolTipManager.CurrentText == toolTip2Text).IsTrue();
		}

		private static SystemWindow CreateTwoChildWindow(TempData tempData)
		{
			SystemWindow systemWindow = new SystemWindow(200, 200);
			GuiWidget toolTip1 = new GuiWidget()
			{
				LocalBounds = new RectangleDouble(10, 10, 20, 20),
				ToolTipText = toolTip1Text,
			};
			GuiWidget toolTip2 = new GuiWidget()
			{
				LocalBounds = new RectangleDouble(30, 30, 40, 40),
				ToolTipText = toolTip2Text,
			};

			systemWindow.ToolTipManager.ToolTipShown += (sender, stringEvent) =>
			{
				tempData.showCount++;
				tempData.lastShownText = stringEvent.Data;
			};

			systemWindow.ToolTipManager.ToolTipPop += (sender, e) =>
			{
				tempData.popCount++;
			};

			systemWindow.AddChild(toolTip1);
			systemWindow.AddChild(toolTip2);

			// This needs to be a synchronous assert since it's not in a test method
			if (systemWindow.Children.Count != 2)
				throw new System.Exception("Expected 2 children in system window");

			// make sure we start out with only the widgets (no tool tip)
			systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0));
			UiThread.InvokePendingActions();
			
			if (systemWindow.Children.Count != 2)
				throw new System.Exception("Expected 2 children in system window after mouse move");

			tempData.lastShownText = "";
			tempData.showCount = 0;
			tempData.popCount = 0;

			return systemWindow;
		}
	}
}
