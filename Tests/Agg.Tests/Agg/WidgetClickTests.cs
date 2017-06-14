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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.Agg.Tests
{
	[TestFixture, Category("Agg.UI"), RunInApplicationDomain]
	public class WidgetClickTests
	{
		GuiWidget lastClicked = null;

		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickFiresOnCorrectWidgets()
		{
			int blueClickCount = 0;
			int orangeClickCount = 0;
			int purpleClickCount = 0;

			lastClicked = null;

			double waitTime = .5;

			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.Delay(2);
				testRunner.ClickByName("rootClickable");
				testRunner.Delay(waitTime);

				Assert.AreEqual(blueClickCount, 1, "Expected 1 click on blue widget");
				Assert.AreEqual(orangeClickCount, 0, "Expected 0 clicks on orange widget");
				Assert.AreEqual(purpleClickCount, 0, "Expected 1 click on purple widget");

				testRunner.ClickByName("orangeClickable");
				testRunner.Delay(waitTime);
				Assert.AreEqual(blueClickCount, 1, "Expected 1 click on blue widget");
				Assert.AreEqual(orangeClickCount, 1, "Expected 1 clicks on orange widget");
				Assert.AreEqual(purpleClickCount, 0, "Expected 0 click on purple widget");

				testRunner.ClickByName("rootClickable");
				testRunner.Delay(waitTime);
				Assert.AreEqual(blueClickCount, 2, "Expected 1 click on blue widget");
				Assert.AreEqual(orangeClickCount, 1, "Expected 0 clicks on orange widget");
				Assert.AreEqual(purpleClickCount, 0, "Expected 1 click on purple widget");

				testRunner.ClickByName("orangeClickable");
				testRunner.Delay(waitTime);
				Assert.AreEqual(blueClickCount, 2, "Expected 1 click on root widget");
				Assert.AreEqual(orangeClickCount, 2, "Expected 2 clicks on orange widget");
				Assert.AreEqual(purpleClickCount, 0, "Expected 0 click on purple widget");

				testRunner.ClickByName("purpleClickable");
				testRunner.Delay(waitTime);
				Assert.AreEqual(blueClickCount, 2, "Expected 1 click on blue widget");
				Assert.AreEqual(orangeClickCount, 2, "Expected 2 clicks on orange widget");
				Assert.AreEqual(purpleClickCount, 1, "Expected 1 click on purple widget");

				return Task.CompletedTask;
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
			rootClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;
				blueClickCount += 1;
				var color = rootClickable.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = rootClickable;
			};
			rootClickable.AfterDraw += widget_DrawSelection;

			var orangeClickable = new GuiWidget()
			{
				Width = 35,
				Height = 25,
				OriginRelativeParent = new VectorMath.Vector2(10, 10),
				Name = "orangeClickable",
				Margin = new BorderDouble(10),
				BackgroundColor = RGBA_Bytes.Orange
			};
			orangeClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;
				orangeClickCount += 1;

				var color = widget.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = widget;
			};
			orangeClickable.AfterDraw += widget_DrawSelection;
			rootClickable.AddChild(orangeClickable);

			var purpleClickable = new GuiWidget()
			{
				Width = 35,
				Height = 25,
				OriginRelativeParent = new VectorMath.Vector2(0, 10),
				HAnchor = HAnchor.ParentRight,
				Name = "purpleClickable",
				Margin = new BorderDouble(10),
				BackgroundColor = new RGBA_Bytes(141, 0, 206)
			};
			purpleClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;

				purpleClickCount += 1;

				var color = widget.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = widget;
			};
			purpleClickable.AfterDraw += widget_DrawSelection;
			rootClickable.AddChild(purpleClickable);

			systemWindow.AddChild(rootClickable);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, testToRun, 25);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ClickSuppressedOnExternalMouseUp()
		{
			int rootClickCount = 0;
			int childClickCount = 0;

			lastClicked = null;

			var systemWindow = new TestHostWindow(300, 200)
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
			rootClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;

				rootClickCount += 1;
				var color = widget.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = widget;

			};
			rootClickable.AfterDraw += widget_DrawSelection;

			var childClickable = new GuiWidget()
			{
				Width = 35,
				Height = 25,
				OriginRelativeParent = new VectorMath.Vector2(20, 15),
				Name = "childClickable",
				Margin = new BorderDouble(10),
				BackgroundColor = RGBA_Bytes.Orange
			};
			childClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;
				childClickCount += 1;

				var color = widget.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = widget;
			};
			childClickable.AfterDraw += widget_DrawSelection;

			rootClickable.AddChild(childClickable);
			systemWindow.AddChild(rootClickable);

			var bounds = rootClickable.BoundsRelativeToParent;
			double x = bounds.Left + 25;
			double y = bounds.Bottom + 8;

			UiThread.RunOnIdle((Action)(async () =>
			{
				try
				{
					MouseEventArgs mouseEvent;
					AutomationRunner testRunner = new AutomationRunner();

					// Click should occur on mouse[down/up] within the controls bounds
					{
						// Move to a position within rootClickable for mousedown
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseDown(mouseEvent);
						await Task.Delay(1000);

						// Move to a position within rootClickable for mouseup
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, x + 119, y + 40, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseUp(mouseEvent);
						await Task.Delay(1000);

						Assert.IsTrue(rootClickCount == 1, "Expected 1 click on root widget");
					}

					lastClicked = null;
					systemWindow.BackgroundColor = RGBA_Bytes.Gray;
					await Task.Delay(1000);

					// Click should not occur when mouse up is outside of the control bounds
					{
						// Move to a position within rootClickable for mousedown
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseDown(mouseEvent);
						await Task.Delay(1000);

						// Move to a position **outside** of rootClickable for mouseup
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, 50, 50, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseUp(mouseEvent);
						await Task.Delay(1000);

						// There should be no increment in the click count
						Assert.IsTrue(rootClickCount == 1, "Expected 1 click on root widget");
					}
				}
				catch (Exception ex)
				{
					systemWindow.ErrorMessage = ex.Message;
					systemWindow.TestsPassed = false;
				}

				UiThread.RunOnIdle(systemWindow.Close, 1);

			}), 1);
			systemWindow.ShowAsSystemWindow();

			Assert.IsTrue(systemWindow.TestsPassed, systemWindow.ErrorMessage);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ClickSuppressedOnMouseUpWithinChild()
		{
			// Agg currently fires mouse up events in child controls when the parent has the mouse captured
			// and is performing drag like operations. If the mouse goes down in the parent and comes up on the child
			// neither control should get a click event

			int rootClickCount = 0;
			int childClickCount = 0;

			lastClicked = null;

			var systemWindow = new TestHostWindow(300, 200)
			{
				Padding = new BorderDouble(20),
				BackgroundColor = RGBA_Bytes.Gray,
				Name = "System Window",
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
			rootClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;

				rootClickCount += 1;
				var color = widget.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = widget;

			};
			rootClickable.AfterDraw += widget_DrawSelection;

			var childClickable = new GuiWidget()
			{
				Width = 35,
				Height = 25,
				OriginRelativeParent = new VectorMath.Vector2(20, 15),
				Name = "childClickable",
				Margin = new BorderDouble(10),
				BackgroundColor = RGBA_Bytes.Orange
			};
			childClickable.Click += (sender, e) =>
			{
				var widget = sender as GuiWidget;
				childClickCount += 1;

				var color = widget.BackgroundColor.AdjustSaturation(0.4);
				systemWindow.BackgroundColor = color.GetAsRGBA_Bytes();
				lastClicked = widget;
			};
			childClickable.AfterDraw += widget_DrawSelection;

			rootClickable.AddChild(childClickable);
			systemWindow.AddChild(rootClickable);

			var bounds = rootClickable.BoundsRelativeToParent;
			double x = bounds.Left + 25;
			double y = bounds.Bottom + 8;

			var childBounds = childClickable.BoundsRelativeToParent;
			double childX = bounds.Left + childBounds.Left + 16;
			double childY = bounds.Bottom + childBounds.Bottom + 10;

			UiThread.RunOnIdle((Action)(async () =>
			{
				try
				{
					MouseEventArgs mouseEvent;
					AutomationRunner testRunner = new AutomationRunner();

					// Click should occur on mouse[down/up] within the controls bounds
					{
						// Move to a position within rootClickable for mousedown
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseDown(mouseEvent);
						await Task.Delay(1000);

						// Move to a position within rootClickable for mouseup
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, x + 119, y + 40, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseUp(mouseEvent);
						await Task.Delay(1000);

						Assert.IsTrue(rootClickCount == 1, "Expected 1 click on root widget");
					}

					lastClicked = null;
					systemWindow.BackgroundColor = RGBA_Bytes.Gray;
					await Task.Delay(1000);

					// Click should not occur when mouse up occurs on child controls
					{
						// Move to a position within rootClickable for mousedown
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseDown(mouseEvent);
						await Task.Delay(1000);

						// Move to a position with the childClickable for mouseup
						mouseEvent = new MouseEventArgs(MouseButtons.Left, 1, childX, childY, 0);
						testRunner.SetMouseCursorPosition(systemWindow, (int)mouseEvent.X, (int)mouseEvent.Y);
						systemWindow.OnMouseUp(mouseEvent);
						await Task.Delay(1000);

						// There should be no increment in the click count
						Assert.IsTrue(rootClickCount == 1, "Expected click count to not increment on mouse up within child control");
					}
				}
				catch(Exception ex)
				{
					systemWindow.ErrorMessage = ex.Message;
					systemWindow.TestsPassed = false;
				}

				UiThread.RunOnIdle(systemWindow.Close, 1);

			}), 1);
			systemWindow.ShowAsSystemWindow();

			Assert.IsTrue(systemWindow.TestsPassed, systemWindow.ErrorMessage);

		}

		private class TestHostWindow : SystemWindow
		{
			public TestHostWindow(double width, double height) : base (width, height)
			{
			}

			public bool TestsPassed { get; set; } = true;
			public string ErrorMessage { get; set; }
		}

		private void widget_DrawSelection(Object drawingWidget, DrawEventArgs e)
		{
			if (lastClicked == drawingWidget)
			{
				e.graphics2D.Rectangle(((GuiWidget)drawingWidget).LocalBounds, RGBA_Bytes.White);
			}
		}
	}
}