/*
Copyright (c) 2016, John Lewin
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

using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.Agg.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class WidgetClickTests
	{
		GuiWidget lastClicked = null;

		[Test, RequiresSTA, RunInApplicationDomain]
		public void ClickFiresOnCorrectWidgets()
		{
			int rootClickCount = 0;
			int childClickCount = 0;

			lastClicked = null;

			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner();
				testRunner.ClickByName("rootClickable");
				testRunner.Wait(1.5);
				resultsHarness.AddTestResult(rootClickCount == 1, "Expected 1 click on root widget");
				resultsHarness.AddTestResult(childClickCount == 0, "Expected 0 clicks on child widget");

				testRunner.ClickByName("childClickable");
				testRunner.Wait(1.5);
				resultsHarness.AddTestResult(childClickCount == 1, "Expected 1 click on child widget");

				testRunner.ClickByName("childClickable");
				testRunner.Wait(1.5);
				resultsHarness.AddTestResult(childClickCount == 2, "Expected 2 clicks on child widget");
				resultsHarness.AddTestResult(rootClickCount == 1, "Expected 1 click on root widget");
			};

			SystemWindow systemWindow = new SystemWindow(300, 200)
			{
				Padding = new BorderDouble(20)
			};

			var rootClickable = new GuiWidget()
			{
				Width = 50,
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
				Margin = new BorderDouble(50),
				Name = "rootClickable",
				BackgroundColor = RGBA_Bytes.Blue
			};
			rootClickable.Click += (s, e) =>
			{
				rootClickCount += 1;
				var color = rootClickable.BackgroundColor.AdjustSaturation(0.5);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = rootClickable;
			};
			rootClickable.DrawAfter += widget_DrawSelection;

			var childClickable = new GuiWidget()
			{
				Width = 35,
				Height = 25,
				OriginRelativeParent = new VectorMath.Vector2(10, 10),
				Name = "childClickable",
				Margin = new BorderDouble(10),
				BackgroundColor = RGBA_Bytes.Green
			};
			childClickable.Click += (s, e) =>
			{
				childClickCount += 1;

				var color = childClickable.BackgroundColor.AdjustSaturation(0.5);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = childClickable;
			};
			childClickable.DrawAfter += widget_DrawSelection;

			rootClickable.AddChild(childClickable);
			systemWindow.AddChild(rootClickable);

			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(systemWindow, testToRun, 10);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 5);
		}

		[Test, RequiresSTA]
		public void ClickSuppressedOnExternalMouseUp()
		{
			int rootClickCount = 0;
			int childClickCount = 0;

			lastClicked = null;

			SystemWindow systemWindow = new SystemWindow(300, 200)
			{
				Padding = new BorderDouble(20),
				BackgroundColor = RGBA_Bytes.Gray
			};

			var rootClickable = new GuiWidget()
			{
				Width = 50,
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
				Margin = new BorderDouble(50),
				Name = "rootClickable",
				BackgroundColor = RGBA_Bytes.Blue
			};
			rootClickable.Click += (s, e) =>
			{
				rootClickCount += 1;
				var color = rootClickable.BackgroundColor.AdjustSaturation(0.5);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = rootClickable;

			};
			rootClickable.DrawAfter += widget_DrawSelection;

			var childClickable = new GuiWidget()
			{
				Width = 35,
				Height = 25,
				OriginRelativeParent = new VectorMath.Vector2(20, 15),
				Name = "childClickable",
				Margin = new BorderDouble(10),
				BackgroundColor = RGBA_Bytes.Green
			};
			childClickable.Click += (s, e) =>
			{
				childClickCount += 1;

				var color = childClickable.BackgroundColor.AdjustSaturation(0.5);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = childClickable;
			};
			childClickable.DrawAfter += widget_DrawSelection;

			rootClickable.AddChild(childClickable);
			systemWindow.AddChild(rootClickable);

			var bounds = rootClickable.BoundsRelativeToParent;
			double x = bounds.Left + 2;
			double y = bounds.Bottom + 2;

			UiThread.RunOnIdle(() =>
			{
				systemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
				systemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x + 5, y + 5, 0));

				// Click should occur on mouse[down/up] within the controls bounds
				Assert.IsTrue(rootClickCount == 1, "Expected 1 click on root widget");
				systemWindow.Invalidate();

				UiThread.RunOnIdle(() =>
				{
					System.Threading.Thread.Sleep(1200);

					lastClicked = null;
					systemWindow.BackgroundColor = RGBA_Bytes.Gray;

					systemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
					systemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));

					// Click should not occur when mouse up is outside of the control bounds
					Assert.IsTrue(rootClickCount == 1, "Expected 1 click on root widget");

					UiThread.RunOnIdle(systemWindow.Close, 1);
				}, 1);

			}, 1);
			systemWindow.ShowAsSystemWindow();
		}

		private void widget_DrawSelection(GuiWidget drawingWidget, DrawEventArgs e)
		{
			if (lastClicked == drawingWidget)
			{
				e.graphics2D.Rectangle(drawingWidget.LocalBounds, RGBA_Bytes.White);
			}
		}
	}
}