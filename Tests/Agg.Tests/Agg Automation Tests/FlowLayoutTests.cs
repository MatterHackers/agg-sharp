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

using System.Threading.Tasks;
using Agg.Tests.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI.Tests
{
    [MhTestFixture("Opens Winforms Window")]
    public class FlowLayoutTests
	{
		public static bool saveImagesForDebug = false;


		static bool enforceIntegerBounds;
        [MhSetupAttribute]
        public static void Setup()
		{
			enforceIntegerBounds = GuiWidget.DefaultEnforceIntegerBounds;
            GuiWidget.DefaultEnforceIntegerBounds = false;
        }

		[MhTearDownAttribute]
		public static void Restore()
        {
            GuiWidget.DefaultEnforceIntegerBounds = enforceIntegerBounds;
        }

        private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

		private void OutputImage(GuiWidget widgetToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				OutputImage(widgetToOutput.BackBuffer, fileName);
			}
		}

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			OutputImage(control, "image-control.tga");
			OutputImage(test, "image-test.tga");
		}

        [MhTest]
        public void TopToBottomContainerAppliesExpectedMargin()
		{
			int marginSize = 40;
			int dimensions = 300;

			var outerContainer = new GuiWidget(dimensions, dimensions);

			var topToBottomContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			outerContainer.AddChild(topToBottomContainer);

			var childWidget = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(marginSize),
				BackgroundColor = Color.Red,
			};

			topToBottomContainer.AddChild(childWidget);
			topToBottomContainer.AnchorAll();
			topToBottomContainer.PerformLayout();

			outerContainer.DoubleBuffer = true;
			outerContainer.BackBuffer.NewGraphics2D().Clear(Color.White);
			outerContainer.OnDraw(outerContainer.NewGraphics2D());

			// For troubleshooting or visual validation
			// saveImagesForDebug = true;
			// OutputImages(outerContainer, outerContainer);

			var bounds = childWidget.BoundsRelativeToParent;
			MhAssert.True(bounds.Left == marginSize, "Left margin is incorrect");
			MhAssert.True(bounds.Right == dimensions - marginSize, "Right margin is incorrect");
			MhAssert.True(bounds.Top == dimensions - marginSize, "Top margin is incorrect");
			MhAssert.True(bounds.Bottom == marginSize, "Bottom margin is incorrect");
		}

        [MhTest]
        public async Task SpacingClearedAfterLoadPositionsCorrectly()
		{
			var systemWindow = new SystemWindow(700, 200)
			{
				BackgroundColor = Color.LightGray,
			};

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			systemWindow.AddChild(column);

			FlowLayoutWidget row;

			int initialSpacing = 8;

			// Header
			row = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Margin = initialSpacing,
				Padding = initialSpacing,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				DebugShowBounds = true
			};
			column.AddChild(row);

			row.AddChild(new GuiWidget(45, 45) { BackgroundColor = Color.Gray });

			// Body
			row = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = initialSpacing,
				Padding = initialSpacing,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				DebugShowBounds = true
			};
			column.AddChild(row);

			row.AddChild(new GuiWidget(45, 45) { BackgroundColor = Color.Gray });

			// Footer
			var footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Margin = initialSpacing,
				Padding = initialSpacing,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				DebugShowBounds = true
			};
			column.AddChild(footerRow);

			footerRow.AddChild(new GuiWidget(45, 45) { BackgroundColor = Color.Gray });

			systemWindow.Load += (s, e) =>
			{
				// Remove padding and margin at runtime
				foreach (var child in column.Children)
				{
					child.Invalidate();
					child.Margin = 0;
					child.Padding = 0;
				}
			};

			await AutomationRunner.ShowWindowAndExecuteTests(
			systemWindow,
			(testRunner) =>
			{
				// Enable to observe
				// testRunner.Delay(30);

				MhAssert.Equal(0, footerRow.Position.Y);//, "Footer should be positioned at Y0 when it is the first item in a TopToBottom FlowlayoutWidget");

                return Task.CompletedTask;
			});
		}

        [MhTest]
        public void NestedLayoutTopToBottomTests()
		{
			NestedLayoutTopToBottomTest(default(BorderDouble), default(BorderDouble));
			NestedLayoutTopToBottomTest(default(BorderDouble), new BorderDouble(3));
			NestedLayoutTopToBottomTest(new BorderDouble(2), new BorderDouble(0));
			NestedLayoutTopToBottomTest(new BorderDouble(2), new BorderDouble(3));
			NestedLayoutTopToBottomTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

        [MhTest]
        public void ChangingChildVisiblityUpdatesFlow()
		{
			// ___________________________________________________
			//  |       containerControl 300                      |
			//  | _______________________________________________ |
			//  | |     Flow1 ParentWidth  300                  | |
			//  | | __________________________________________  | |
			//  | | |   Flow2 FitToChildren 250              |  | |
			//  | | | ____________________________ _________ |  | |
			//  | | | | Size1 200                | |Size2  | |  | |
			//  | | | |                          | |50     | |  | |
			//  | | | |__________________________| |_______| |  | |
			//  | | |________________________________________|  | |
			//  | |_____________________________________________| |
			//  |_________________________________________________|

			var containerControl = new GuiWidget(300, 200)
			{
				Name = "containerControl"
			};

			var flow1 = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Name = "flow1",
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(3, 3)
			};
			containerControl.AddChild(flow1);

			var flow2 = new FlowLayoutWidget
			{
				Name = "flow2"
			};
			flow1.AddChild(flow2);

			var size1 = new GuiWidget(200, 20)
			{
				Name = "size2"
			};
			flow2.AddChild(size1);

			var size2 = new GuiWidget(50, 20)
			{
				Name = "size1"
			};
			flow2.AddChild(size2);

			MhAssert.True(flow1.Width == containerControl.Width);
			MhAssert.True(flow2.Width == size2.Width + size1.Width);

			size1.Visible = false;
			// ___________________________________________________
			//  |       containerControl 300                      |
			//  | _______________________________________________ |
			//  | |   Flow1 ParentWidth  300                    | |
			//  | | _____________                               | |
			//  | | | Flow2 50  |                               | |
			//  | | | _________ |                               | |
			//  | | | |Size2  | |                               | |
			//  | | | |50     | |                               | |
			//  | | | |       | |                               | |
			//  | | | |_______| |                               | |
			//  | | |___________|                               | |
			//  | |_____________________________________________| |
			//  |_________________________________________________|

			MhAssert.True(flow1.Width == containerControl.Width);
			MhAssert.True(flow2.Width == size2.Width);
		}

        [MhTest]
        public void ChangingChildFlowWidgetVisiblityUpdatesParentFlow()
		{
			// ___________________________________________________
			//  |       containerControl 300                      |
			//  | _______________________________________________ |
			//  | |     Flow1 ParentWidth  300                  | |
			//  | | __________________________________________  | |
			//  | | |   Flow2 FitToChildren 250              |  | |
			//  | | | ____________________________ _________ |  | |
			//  | | | | Flow 3 FitToChildren 200 | |Size2  | |  | |
			//  | | | | ________________________ | |50     | |  | |
			//  | | | | | Size1 200            | | |       | |  | |
			//  | | | | |______________________| | |       | |  | |
			//  | | | |__________________________| |_______| |  | |
			//  | | |________________________________________|  | |
			//  | |_____________________________________________| |
			//  |_________________________________________________|

			var containerControl = new GuiWidget(300, 200)
			{
				Name = "containerControl"
			};

			var flow1 = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Name = "flow1",
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(3, 3)
			};
			containerControl.AddChild(flow1);

			var flow2 = new FlowLayoutWidget
			{
				Name = "flow2"
			};
			flow1.AddChild(flow2);

			GuiWidget flow3 = new FlowLayoutWidget
			{
				Name = "flow3"
			};
			flow2.AddChild(flow3);

			var size1 = new GuiWidget(200, 20)
			{
				Name = "size2"
			};
			flow3.AddChild(size1);

			var size2 = new GuiWidget(50, 20)
			{
				Name = "size1"
			};
			flow2.AddChild(size2);


			MhAssert.True(flow1.Width == containerControl.Width);
			MhAssert.True(flow3.Width == size1.Width);
			MhAssert.True(flow2.Width == size2.Width + flow3.Width);

			size1.Visible = false;
			// ___________________________________________________
			//  |       containerControl 300                      |
			//  | _______________________________________________ |
			//  | |   Flow1 ParentWidth  300                    | |
			//  | | _____________                               | |
			//  | | | Flow2 50  |                               | |
			//  | | | _________ |                               | |
			//  | | | |Size2  | |                               | |
			//  | | | |50     | |                               | |
			//  | | | |       | |                               | |
			//  | | | |_______| |                               | |
			//  | | |___________|                               | |
			//  | |_____________________________________________| |
			//  |_________________________________________________|
			//

			MhAssert.True(flow1.Width == containerControl.Width);
			MhAssert.True(flow2.Width == size2.Width);
		}

		public void NestedLayoutTopToBottomTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(300, 200)
			{
				DoubleBuffer = true
			};
			containerControl.BackBuffer.NewGraphics2D().Clear(Color.White);
			{
				var topButtonC = new Button("top button");
				var bottomButtonC = new Button("bottom wide button");
				topButtonC.LocalBounds = new RectangleDouble(0, 0, bottomButtonC.LocalBounds.Width, 40);
				topButtonC.OriginRelativeParent = new Vector2(bottomButtonC.OriginRelativeParent.X + buttonMargin.Left, containerControl.Height - controlPadding.Top - topButtonC.Height - buttonMargin.Top);
				containerControl.AddChild(topButtonC);
				bottomButtonC.OriginRelativeParent = new Vector2(bottomButtonC.OriginRelativeParent.X + buttonMargin.Left, topButtonC.OriginRelativeParent.Y - buttonMargin.Height - bottomButtonC.Height);
				containerControl.AddChild(bottomButtonC);
			}

			containerControl.OnDraw(containerControl.NewGraphics2D());

			var containerTest = new GuiWidget(300, 200)
			{
				DoubleBuffer = true
			};
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);

			var allButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			allButtons.AnchorAll();
			Button topButtonT;
			Button bottomButtonT;
			FlowLayoutWidget topButtonBar;
			FlowLayoutWidget bottomButtonBar;
			allButtons.Padding = controlPadding;
			{
				bottomButtonT = new Button("bottom wide button");

				topButtonBar = new FlowLayoutWidget();
				{
					topButtonT = new Button("top button")
					{
						LocalBounds = new RectangleDouble(0, 0, bottomButtonT.LocalBounds.Width, 40),
						Margin = buttonMargin
					};
					topButtonBar.AddChild(topButtonT);
				}

				allButtons.AddChild(topButtonBar);

				bottomButtonBar = new FlowLayoutWidget();
				{
					bottomButtonT.Margin = buttonMargin;
					bottomButtonBar.AddChild(bottomButtonT);
				}

				allButtons.AddChild(bottomButtonBar);
			}

			containerTest.AddChild(allButtons);
			containerTest.OnDraw(containerTest.NewGraphics2D());

			OutputImages(containerControl, containerTest);

			MhAssert.True(containerTest.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerTest.BackBuffer.Equals(containerControl.BackBuffer, 1), "The test should contain the same image as the control.");
		}

        [MhTest]
        public void NestedLayoutTopToBottomWithResizeTests()
		{
			NestedLayoutTopToBottomWithResizeTest(default(BorderDouble), default(BorderDouble));
			NestedLayoutTopToBottomWithResizeTest(default(BorderDouble), new BorderDouble(3));
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(2), new BorderDouble(0));
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(2), new BorderDouble(3));
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedLayoutTopToBottomWithResizeTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerTest = new GuiWidget(300, 200)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);

			var allButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				var topButtonBar = new FlowLayoutWidget();
				{
					var button1 = new Button("button1")
					{
						Margin = buttonMargin
					};
					topButtonBar.AddChild(button1);
				}

				allButtons.AddChild(topButtonBar);

				var bottomButtonBar = new FlowLayoutWidget();
				{
					var button2 = new Button("wide button2")
					{
						Margin = buttonMargin
					};
					bottomButtonBar.AddChild(button2);
				}

				allButtons.AddChild(bottomButtonBar);
			}

			containerTest.AddChild(allButtons);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			var controlImage = new ImageBuffer(containerTest.BackBuffer, new BlenderBGRA());

			OutputImage(controlImage, "image-control.tga");

			RectangleDouble oldBounds = containerTest.LocalBounds;
			RectangleDouble newBounds = oldBounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImage(containerTest, "image-test.tga");

			containerTest.LocalBounds = oldBounds;
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImage(containerTest, "image-test.tga");

			MhAssert.True(containerTest.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerTest.BackBuffer == controlImage, "The control should contain the same image after being scaled away and back to the same size.");
		}

        [MhTest]
        public void LeftToRightTests()
		{
			LeftToRightTest(default(BorderDouble), default(BorderDouble));
			LeftToRightTest(default(BorderDouble), new BorderDouble(3));
			LeftToRightTest(new BorderDouble(2), new BorderDouble(0));
			LeftToRightTest(new BorderDouble(2), new BorderDouble(3));
			LeftToRightTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private void LeftToRightTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(300, 200)
			{
				DoubleBuffer = true
			};
			var controlButton1 = new Button("buttonLeft");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left, controlButton1.OriginRelativeParent.Y);
			containerControl.AddChild(controlButton1);
			var controlButton2 = new Button("buttonRight");
			controlButton2.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Width + buttonMargin.Left + controlButton1.Width, controlButton2.OriginRelativeParent.Y);
			containerControl.AddChild(controlButton2);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			var containerTest = new GuiWidget(300, 200);
			var flowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			flowLayout.AnchorAll();
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			var testButton1 = new Button("buttonLeft")
			{
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButton1);

			var testButton2 = new Button("buttonRight")
			{
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButton2);

			containerTest.AddChild(flowLayout);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			MhAssert.True(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			MhAssert.True(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void RightToLeftTests()
		{
			RightToLeftTest(default(BorderDouble), default(BorderDouble));
			RightToLeftTest(default(BorderDouble), new BorderDouble(3));
			RightToLeftTest(new BorderDouble(2), new BorderDouble(0));
			RightToLeftTest(new BorderDouble(2), new BorderDouble(3));
			RightToLeftTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private void RightToLeftTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(300, 200)
			{
				DoubleBuffer = true
			};
			var controlButtonRight = new Button("buttonRight");
			controlButtonRight.OriginRelativeParent = new VectorMath.Vector2(containerControl.Width - controlPadding.Right - buttonMargin.Right - controlButtonRight.Width, controlButtonRight.OriginRelativeParent.Y);
			containerControl.AddChild(controlButtonRight);
			var controlButtonLeft = new Button("buttonLeft");
			controlButtonLeft.OriginRelativeParent = new VectorMath.Vector2(controlButtonRight.BoundsRelativeToParent.Left - buttonMargin.Width - controlButtonLeft.Width, controlButtonLeft.OriginRelativeParent.Y);
			containerControl.AddChild(controlButtonLeft);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			var containerTest = new GuiWidget(300, 200);
			var flowLayout = new FlowLayoutWidget(FlowDirection.RightToLeft);
			flowLayout.AnchorAll();
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			var testButton2 = new Button("buttonRight")
			{
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButton2);

			var testButton1 = new Button("buttonLeft")
			{
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButton1);

			containerTest.AddChild(flowLayout);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			MhAssert.True(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			MhAssert.True(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void NestedMaxFitOrStretchToChildrenParentWidth()
		{
			// child of flow layout is Stretch
			{
				// _________________________________________
				//  |  containerControl 300x300              |
				//  | _____________________________________  |
				//  | |    MaxFitOrStretch                 | |
				//  | | ________________________ ________  | |
				//  | | |                      | |       | | |
				//  | | |    Stretch           | | 10x10 | | |
				//  | | |______________________| |_______| | |
				//  | |____________________________________| |
				//  |________________________________________|
				//

				var containerControl = new GuiWidget(300, 200)
				{
					DoubleBuffer = true
				};
				var maxFitOrStretch = new FlowLayoutWidget()
				{
					Name = "MaxFitOrStretch",
					HAnchor = HAnchor.MaxFitOrStretch,
				};
				containerControl.AddChild(maxFitOrStretch);
				var stretch = new GuiWidget(20, 20)
				{
					Name = "stretch",
					HAnchor = HAnchor.Stretch,
				};
				maxFitOrStretch.AddChild(stretch);
				var fixed10x10 = new GuiWidget(10, 10);
				maxFitOrStretch.AddChild(fixed10x10);
				containerControl.OnDraw(containerControl.NewGraphics2D());

				MhAssert.True(maxFitOrStretch.Width == containerControl.Width);
				MhAssert.True(stretch.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 350;
				MhAssert.True(maxFitOrStretch.Width == containerControl.Width);
				MhAssert.True(stretch.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 310;
				MhAssert.True(maxFitOrStretch.Width == containerControl.Width);
				MhAssert.True(stretch.Width + fixed10x10.Width == containerControl.Width);
			}

			// child of flow layout is MaxFitOrStretch
			{
				// ___________________________________________________
				//  |            containerControl                      |
				//  | _______________________________________________  |
				//  | |    MaxFitOrStretch                           | |
				//  | | _________________________________   _______  | |
				//  | | |                                | |       | | |
				//  | | | MaxFitOrStretch                | | 10x10 | | |
				//  | | |________________________________| |_______| | |
				//  | |______________________________________________| |
				//  |__________________________________________________|
				//

				var containerControl = new GuiWidget(300, 200)
				{
					DoubleBuffer = true
				};
				var flowWidget = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.MaxFitOrStretch,
				};
				containerControl.AddChild(flowWidget);
				var fitToChildrenOrParent = new GuiWidget(20, 20)
				{
					Name = "fitToChildrenOrParent",
					HAnchor = HAnchor.MaxFitOrStretch,
				};
				flowWidget.AddChild(fitToChildrenOrParent);
				var fixed10x10 = new GuiWidget(10, 10);
				flowWidget.AddChild(fixed10x10);
				containerControl.OnDraw(containerControl.NewGraphics2D());

				MhAssert.True(flowWidget.Width == containerControl.Width);
				MhAssert.True(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 350;
				MhAssert.True(flowWidget.Width == containerControl.Width);
				MhAssert.True(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 310;
				MhAssert.True(flowWidget.Width == containerControl.Width);
				MhAssert.True(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);
			}
		}

        [MhTest]
        public void NestedMinFitOrStretchToChildrenParentWidth()
		{
			// child of flow layout is Stretch
			{
				// _________________________________________
				//  |     containerControl  300x200          |
				//  | _____________________________________  |
				//  | |    MinFitOrStretch                 | |
				//  | |  ______________________            | |
				//  | | |                     |            | |
				//  | | |              150x10 |            | |
				//  | | |_____________________|            | |
				//  | |____________________________________| |
				//  |________________________________________|

				var containerControl = new GuiWidget(300, 200)
				{
					Name = "containerControl"
				};
				containerControl.MinimumSize = Vector2.Zero;
				containerControl.DoubleBuffer = true;
				var minFitOrStretch = new FlowLayoutWidget()
				{
					Name = "minFitOrStretch",
					HAnchor = HAnchor.MinFitOrStretch,
				};
				containerControl.AddChild(minFitOrStretch);
				MhAssert.Equal(0, minFitOrStretch.Width);

				var fixed150x10 = new GuiWidget(150, 10)
				{
					MinimumSize = Vector2.Zero
				};
				minFitOrStretch.AddChild(fixed150x10);
				MhAssert.Equal(150, minFitOrStretch.Width);

				containerControl.Width = 100;
				MhAssert.Equal(100, minFitOrStretch.Width);

				containerControl.Width = 200;
				MhAssert.Equal(150, minFitOrStretch.Width);

				fixed150x10.Width = 21;
				MhAssert.Equal(21, minFitOrStretch.Width);
			}

			// in this test the main container starts out as the constraint
			{
				// _________________________________________
				//  |     containerControl  123x200          |
				//  | _____________________________________  |
				//  | |    MinFitOrStretch                 | |
				//  | |  ______________________            | |
				//  | | |                     |            | |
				//  | | |              150x10 |            | |
				//  | | |_____________________|            | |
				//  | |____________________________________| |
				//  |________________________________________|

				var containerControl = new GuiWidget(123, 200)
				{
					Name = "containerControl"
				};
				containerControl.MinimumSize = Vector2.Zero;
				containerControl.DoubleBuffer = true;
				var minFitOrStretch = new FlowLayoutWidget()
				{
					Name = "minFitOrStretch",
					HAnchor = HAnchor.MinFitOrStretch,
				};
				containerControl.AddChild(minFitOrStretch);
				MhAssert.Equal(0, minFitOrStretch.Width);

				var fixed150x10 = new GuiWidget(150, 10)
				{
					MinimumSize = Vector2.Zero
				};
				minFitOrStretch.AddChild(fixed150x10);
				MhAssert.Equal(123, minFitOrStretch.Width);

				containerControl.Width = 100;
				MhAssert.Equal(100, minFitOrStretch.Width);

				containerControl.Width = 200;
				MhAssert.Equal(150, minFitOrStretch.Width);

				fixed150x10.Width = 21;
				MhAssert.Equal(21, fixed150x10.Width);
				MhAssert.Equal(21, minFitOrStretch.Width);
			}

			// child of flow layout is Stretch
			{
				// _________________________________________
				//  |     containerControl  300x200          |
				//  | _____________________________________  |
				//  | |    MinFitOrStretch                 | |
				//  | | ________________________ ________  | |
				//  | | |                      | |       | | |
				//  | | |    Stretch           | | 10x10 | | |
				//  | | |______________________| |_______| | |
				//  | |____________________________________| |
				//  |________________________________________|

				var containerControl = new GuiWidget(300, 200)
				{
					DoubleBuffer = true
				};
				var minFitOrStretch = new FlowLayoutWidget()
				{
					Name = "minFitOrStretch",
					HAnchor = HAnchor.MinFitOrStretch,
				};
				containerControl.AddChild(minFitOrStretch);
				var stretch = new GuiWidget(20, 20)
				{
					Name = "stretch",
					HAnchor = HAnchor.Stretch,
				};
				minFitOrStretch.AddChild(stretch);
				var fixed10x10 = new GuiWidget(10, 10);
				minFitOrStretch.AddChild(fixed10x10);
				containerControl.OnDraw(containerControl.NewGraphics2D());

				MhAssert.Equal(30, minFitOrStretch.Width);
				MhAssert.Equal(20, stretch.Width);

				containerControl.Width = 350;
				MhAssert.Equal(30, minFitOrStretch.Width);
				MhAssert.Equal(20, stretch.Width);

				containerControl.Width = 310;
				MhAssert.Equal(30, minFitOrStretch.Width);
				MhAssert.Equal(20, stretch.Width);

				fixed10x10.Width = 21;
				MhAssert.Equal(41, minFitOrStretch.Width);
				MhAssert.Equal(20, stretch.Width);
			}

			return; // this last test does not work yet
			// child of flow layout is MaxFitOrStretch
			{
				// ___________________________________________________
				//  |            containerControl 300x200             |
				//  | ______________________________________________  |
				//  | |    MinFitOrStretchOuter                     | |
				//  | | ________________________________________    | |
				//  | | |                      _________        |   | |
				//  | | |                      |       |        |   | |
				//  | | | MinFitOrStretchInner | 10x10 |        |   | |
				//  | | |                      |_______|        |   | |
				//  | | |_______________________________________|   | |
				//  | |_____________________________________________| |
				//  |_________________________________________________|

				var containerControl = new GuiWidget(300, 200)
				{
					DoubleBuffer = true
				};
				var minFitOrStretchOuter = new FlowLayoutWidget()
				{
					Name = "minFitOrStretchOuter",
					HAnchor = HAnchor.MinFitOrStretch,
					MinimumSize = Vector2.Zero,
				};
				containerControl.AddChild(minFitOrStretchOuter);
				var minFitOrStretchInner = new FlowLayoutWidget()
				{
					Name = "minFitOrStretchInner",
					HAnchor = HAnchor.MinFitOrStretch,
					MinimumSize = Vector2.Zero,
				};
				minFitOrStretchOuter.AddChild(minFitOrStretchInner);

				var fixed10x10 = new GuiWidget(10, 10)
				{
					Name = "fixed10x10"
				};

				minFitOrStretchInner.AddChild(fixed10x10);

				MhAssert.Equal(10, minFitOrStretchInner.Width);
				MhAssert.Equal(10, minFitOrStretchOuter.Width);

				containerControl.Width = 350;
				MhAssert.Equal(10, minFitOrStretchInner.Width);
				MhAssert.Equal(10, minFitOrStretchOuter.Width);

				containerControl.Width = 300;
				MhAssert.Equal(10, minFitOrStretchInner.Width);
				MhAssert.Equal(10, minFitOrStretchOuter.Width);
			}
		}

        [MhTest]
        public void LeftToRightAnchorLeftBottomTests()
		{
			LeftToRightAnchorLeftBottomTest(default(BorderDouble), default(BorderDouble));
			LeftToRightAnchorLeftBottomTest(default(BorderDouble), new BorderDouble(3));
			LeftToRightAnchorLeftBottomTest(new BorderDouble(2), new BorderDouble(0));
			LeftToRightAnchorLeftBottomTest(new BorderDouble(2), new BorderDouble(3));
			LeftToRightAnchorLeftBottomTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private void LeftToRightAnchorLeftBottomTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(300, 200)
			{
				DoubleBuffer = true
			};
			var controlButton1 = new Button("buttonLeft");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left, controlButton1.OriginRelativeParent.Y + controlPadding.Bottom + buttonMargin.Bottom);
			containerControl.AddChild(controlButton1);
			var controlButton2 = new Button("buttonRight");
			controlButton2.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Width + buttonMargin.Left + controlButton1.Width, controlButton2.OriginRelativeParent.Y + controlPadding.Bottom + buttonMargin.Bottom);
			containerControl.AddChild(controlButton2);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			var containerTest = new GuiWidget(300, 200);
			var flowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			flowLayout.AnchorAll();
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			var testButton1 = new Button("buttonLeft")
			{
				VAnchor = VAnchor.Bottom,
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButton1);

			var testButton2 = new Button("buttonRight")
			{
				VAnchor = VAnchor.Bottom,
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButton2);

			containerTest.AddChild(flowLayout);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			MhAssert.True(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			MhAssert.True(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void AnchorLeftRightTests()
		{
			FlowTopBottomAnchorChildrenLeftRightTest(default(BorderDouble), default(BorderDouble));
			FlowTopBottomAnchorChildrenLeftRightTest(default(BorderDouble), new BorderDouble(3));
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(2), new BorderDouble(0));
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(2), new BorderDouble(3));
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void FlowTopBottomAnchorChildrenLeftRightTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(300, 500)
			{
				DoubleBuffer = true
			};
			var controlButtonWide = new Button("Button Wide Text");
			containerControl.AddChild(controlButtonWide);
			var controlButton1 = new Button("button1");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left + controlButton1.OriginRelativeParent.X, controlPadding.Bottom + buttonMargin.Bottom);
			controlButtonWide.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left + controlButtonWide.OriginRelativeParent.X, controlButton1.BoundsRelativeToParent.Top + buttonMargin.Height);
			controlButton1.LocalBounds = controlButtonWide.LocalBounds;
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			var containerTest = new GuiWidget(300, 500);
			var flowLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = controlPadding
			};
			containerTest.DoubleBuffer = true;

			var testButtonWide = new Button("Button Wide Text")
			{
				HAnchor = HAnchor.Left,
				Margin = buttonMargin
			};
			flowLayout.AddChild(testButtonWide);

			double correctHeightOfFlowLayout = testButtonWide.Height + flowLayout.Padding.Height + testButtonWide.Margin.Height;
			MhAssert.Equal(flowLayout.Height, correctHeightOfFlowLayout, .001);

			var testButton1 = new Button("button1")
			{
				Margin = buttonMargin,
				HAnchor = HAnchor.Left | HAnchor.Right
			};
			flowLayout.AddChild(testButton1);

			correctHeightOfFlowLayout += testButton1.Height + testButton1.Margin.Height;
			MhAssert.Equal(flowLayout.Height, correctHeightOfFlowLayout, .001);

			flowLayout.HAnchor = HAnchor.Left;
			flowLayout.VAnchor = VAnchor.Bottom;
			containerTest.AddChild(flowLayout);

			Vector2 controlButton1Pos = controlButton1.OriginRelativeParent;
			Vector2 testButton1Pos = testButton1.TransformToScreenSpace(Vector2.Zero);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			MhAssert.True(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			MhAssert.True(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void NestedFlowWidgetsTopToBottomTests()
		{
			NestedFlowWidgetsTopToBottomTest(default(BorderDouble), default(BorderDouble));
			NestedFlowWidgetsTopToBottomTest(default(BorderDouble), new BorderDouble(3));
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(2), new BorderDouble(0));
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(2), new BorderDouble(3));
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedFlowWidgetsTopToBottomTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(300, 500)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};
			{
				var buttonTop = new Button("buttonTop");
				var buttonBottom = new Button("buttonBottom");
				buttonTop.OriginRelativeParent = new VectorMath.Vector2(buttonTop.OriginRelativeParent.X, containerControl.LocalBounds.Top - buttonMargin.Top - controlPadding.Top - buttonTop.Height);
				buttonBottom.OriginRelativeParent = new VectorMath.Vector2(buttonBottom.OriginRelativeParent.X, buttonTop.BoundsRelativeToParent.Bottom - buttonBottom.Height - buttonMargin.Height);
				containerControl.AddChild(buttonTop);
				containerControl.AddChild(buttonBottom);
				containerControl.OnDraw(containerControl.NewGraphics2D());
			}

			var containerTest = new GuiWidget(300, 500)
			{
				DoubleBuffer = true
			};
			{
				var topToBottomFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
				topToBottomFlowLayoutAll.AnchorAll();
				topToBottomFlowLayoutAll.Padding = controlPadding;
				{
					var topToBottomFlowLayoutTop = new FlowLayoutWidget(FlowDirection.TopToBottom);
					var buttonTop = new Button("buttonTop")
					{
						Margin = buttonMargin
					};
					topToBottomFlowLayoutTop.AddChild(buttonTop);
					topToBottomFlowLayoutTop.SetBoundsToEncloseChildren();
					topToBottomFlowLayoutAll.AddChild(topToBottomFlowLayoutTop);
				}

				{
					var topToBottomFlowLayoutBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
					var buttonBottom = new Button("buttonBottom")
					{
						Margin = buttonMargin
					};
					topToBottomFlowLayoutBottom.AddChild(buttonBottom);
					topToBottomFlowLayoutBottom.SetBoundsToEncloseChildren();
					topToBottomFlowLayoutAll.AddChild(topToBottomFlowLayoutBottom);
				}

				containerTest.AddChild(topToBottomFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void NestedFlowWidgetsRightToLeftTests()
		{
			NestedFlowWidgetsRightToLeftTest(default(BorderDouble), default(BorderDouble));
			NestedFlowWidgetsRightToLeftTest(default(BorderDouble), new BorderDouble(3));
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(2), new BorderDouble(0));
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(2), new BorderDouble(3));
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedFlowWidgetsRightToLeftTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(500, 300)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};
			{
				var buttonRight = new Button("buttonRight");
				var buttonLeft = new Button("buttonLeft");
				buttonRight.OriginRelativeParent = new VectorMath.Vector2(containerControl.LocalBounds.Right - controlPadding.Right - buttonMargin.Right - buttonRight.Width, buttonRight.OriginRelativeParent.Y + controlPadding.Bottom + buttonMargin.Bottom);
				buttonLeft.OriginRelativeParent = new VectorMath.Vector2(buttonRight.BoundsRelativeToParent.Left - buttonMargin.Width - buttonLeft.Width, buttonLeft.OriginRelativeParent.Y + controlPadding.Bottom + buttonMargin.Bottom);
				containerControl.AddChild(buttonRight);
				containerControl.AddChild(buttonLeft);
				containerControl.OnDraw(containerControl.NewGraphics2D());
			}

			var containerTest = new GuiWidget(500, 300)
			{
				DoubleBuffer = true
			};
			{
				var rightToLeftFlowLayoutAll = new FlowLayoutWidget(FlowDirection.RightToLeft);
				rightToLeftFlowLayoutAll.AnchorAll();
				rightToLeftFlowLayoutAll.Padding = controlPadding;
				{
					var rightToLeftFlowLayoutRight = new FlowLayoutWidget(FlowDirection.RightToLeft);
					var buttonRight = new Button("buttonRight")
					{
						Margin = buttonMargin
					};
					rightToLeftFlowLayoutRight.AddChild(buttonRight);
					rightToLeftFlowLayoutRight.SetBoundsToEncloseChildren();
					rightToLeftFlowLayoutRight.VAnchor = VAnchor.Bottom;
					rightToLeftFlowLayoutAll.AddChild(rightToLeftFlowLayoutRight);
				}

				{
					var rightToLeftFlowLayoutLeft = new FlowLayoutWidget(FlowDirection.RightToLeft);
					var buttonLeft = new Button("buttonLeft")
					{
						Margin = buttonMargin
					};
					rightToLeftFlowLayoutLeft.AddChild(buttonLeft);
					rightToLeftFlowLayoutLeft.SetBoundsToEncloseChildren();
					rightToLeftFlowLayoutLeft.VAnchor = VAnchor.Bottom;
					rightToLeftFlowLayoutAll.AddChild(rightToLeftFlowLayoutLeft);
				}

				containerTest.AddChild(rightToLeftFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			MhAssert.True(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 50), "The test and control need to match.");
			MhAssert.True(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void NestedFlowWidgetsLeftToRightTests()
		{
			NestedFlowWidgetsLeftToRightTest(default(BorderDouble), default(BorderDouble));
			NestedFlowWidgetsLeftToRightTest(default(BorderDouble), new BorderDouble(3));
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(2), new BorderDouble(0));
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(2), new BorderDouble(3));
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedFlowWidgetsLeftToRightTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			var containerControl = new GuiWidget(500, 300)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};
			{
				var buttonRight = new Button("buttonRight");
				var buttonLeft = new Button("buttonLeft");
				buttonLeft.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left, buttonLeft.OriginRelativeParent.Y);
				buttonRight.OriginRelativeParent = new VectorMath.Vector2(buttonLeft.BoundsRelativeToParent.Right + buttonMargin.Width, buttonRight.OriginRelativeParent.Y);
				containerControl.AddChild(buttonRight);
				containerControl.AddChild(buttonLeft);
				containerControl.OnDraw(containerControl.NewGraphics2D());
			}

			var containerTest = new GuiWidget(500, 300)
			{
				DoubleBuffer = true
			};
			{
				var leftToRightFlowLayoutAll = new FlowLayoutWidget(FlowDirection.LeftToRight);
				leftToRightFlowLayoutAll.AnchorAll();
				leftToRightFlowLayoutAll.Padding = controlPadding;
				{
					var leftToRightFlowLayoutLeft = new FlowLayoutWidget(FlowDirection.LeftToRight);
					var buttonTop = new Button("buttonLeft")
					{
						Margin = buttonMargin
					};
					leftToRightFlowLayoutLeft.AddChild(buttonTop);
					leftToRightFlowLayoutLeft.SetBoundsToEncloseChildren();
					leftToRightFlowLayoutAll.AddChild(leftToRightFlowLayoutLeft);
				}

				{
					var leftToRightFlowLayoutRight = new FlowLayoutWidget(FlowDirection.LeftToRight);
					var buttonBottom = new Button("buttonRight")
					{
						Margin = buttonMargin
					};
					leftToRightFlowLayoutRight.AddChild(buttonBottom);
					leftToRightFlowLayoutRight.SetBoundsToEncloseChildren();
					leftToRightFlowLayoutAll.AddChild(leftToRightFlowLayoutRight);
				}

				containerTest.AddChild(leftToRightFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			MhAssert.True(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

        [MhTest]
        public void FlowWithMaxSizeChildAllocatesToOthers()
		{
			var container = new GuiWidget(200, 300);

			var leftToRightFlowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			leftToRightFlowLayout.AnchorAll();
			container.AddChild(leftToRightFlowLayout);

			var leftItem = new GuiWidget()
			{
				Name = "leftItem",
				HAnchor = HAnchor.Stretch
			};
			leftToRightFlowLayout.AddChild(leftItem);

			var rightItem = new GuiWidget()
			{
				Name = "rightItem",
				HAnchor = HAnchor.Stretch
			};
			leftToRightFlowLayout.AddChild(rightItem);

			MhAssert.Equal(100, leftItem.Width);
			MhAssert.Equal(100, rightItem.Width);

			leftItem.MaximumSize = new Vector2(50, 500);

			MhAssert.Equal(50, leftItem.Width);
			MhAssert.Equal(150, rightItem.Width);
		}

        [MhTest]
        public void LeftRightWithAnchorLeftRightChildTests()
		{
			LeftRightWithAnchorLeftRightChildTest(default(BorderDouble), default(BorderDouble));
			LeftRightWithAnchorLeftRightChildTest(default(BorderDouble), new BorderDouble(3));
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(0));
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(3));
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void LeftRightWithAnchorLeftRightChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			var containerControl = new GuiWidget(buttonSize * 6, buttonSize * 3)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};

			var eightControlRectangles = new RectangleDouble[6];
			var testColors = new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.YellowGreen, Color.Green, Color.Blue };
			{
				double currentleft = controlPadding.Left + buttonMargin.Left;
				double buttonHeightWithMargin = buttonSize + buttonMargin.Height;
				double scaledWidth = (containerControl.Width - controlPadding.Width - buttonMargin.Width * 6 - buttonSize * 2) / 4;
				// the left unsized rect
				eightControlRectangles[0] = new RectangleDouble(
						currentleft,
						0,
						currentleft + buttonSize,
						buttonSize);

				// a bottom anchor
				currentleft += buttonSize + buttonMargin.Width;
				double bottomAnchorY = controlPadding.Bottom + buttonMargin.Bottom;
				eightControlRectangles[1] = new RectangleDouble(currentleft, bottomAnchorY, currentleft + scaledWidth, bottomAnchorY + buttonSize);

				// center anchor
				double centerYOfContainer = controlPadding.Bottom + (containerControl.Height - controlPadding.Height) / 2;
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[2] = new RectangleDouble(currentleft, centerYOfContainer - buttonHeightWithMargin / 2 + buttonMargin.Bottom, currentleft + scaledWidth, centerYOfContainer + buttonHeightWithMargin / 2 - buttonMargin.Top);

				// top anchor
				double topAnchorY = containerControl.Height - controlPadding.Top - buttonMargin.Top;
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[3] = new RectangleDouble(currentleft, topAnchorY - buttonSize, currentleft + scaledWidth, topAnchorY);

				// bottom top anchor
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[4] = new RectangleDouble(currentleft, bottomAnchorY, currentleft + scaledWidth, topAnchorY);

				// right anchor
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[5] = new RectangleDouble(currentleft, 0, currentleft + buttonSize, buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 6; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], testColors[i]);
				}
			}

			var containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			var leftToRightFlowLayoutAll = new FlowLayoutWidget(FlowDirection.LeftToRight);
			containerTest.DoubleBuffer = true;
			{
				leftToRightFlowLayoutAll.AnchorAll();
				leftToRightFlowLayoutAll.Padding = controlPadding;
				{
					var left = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = testColors[0],
						Margin = buttonMargin
					};
					leftToRightFlowLayoutAll.AddChild(left);

					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Bottom, testColors[1]));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Center, testColors[2]));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Top, testColors[3]));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Stretch, testColors[4]));

					var right = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = testColors[5],
						Margin = buttonMargin
					};
					leftToRightFlowLayoutAll.AddChild(right);
				}

				containerTest.AddChild(leftToRightFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			int index = 0;
			foreach (var child in leftToRightFlowLayoutAll.Children)
			{
				MhAssert.True(eightControlRectangles[index++] == child.BoundsRelativeToParent);
			}

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			MhAssert.True(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		private GuiWidget CreateLeftToRightMiddleWidget(BorderDouble buttonMargin, double buttonSize, VAnchor vAnchor, Color color)
		{
			var middle = new GuiWidget(buttonSize / 2, buttonSize)
			{
				Margin = buttonMargin,
				HAnchor = HAnchor.Stretch,
				VAnchor = vAnchor,
				BackgroundColor = color
			};
			return middle;
		}

        [MhTest]
        public void RightLeftWithAnchorLeftRightChildTests()
		{
			RightLeftWithAnchorLeftRightChildTest(default(BorderDouble), default(BorderDouble));
			RightLeftWithAnchorLeftRightChildTest(default(BorderDouble), new BorderDouble(3));
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(0));
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(3));
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void RightLeftWithAnchorLeftRightChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			var containerControl = new GuiWidget(buttonSize * 6, buttonSize * 3)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};

			var eightControlRectangles = new RectangleDouble[6];
			var testColors = new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.YellowGreen, Color.Green, Color.Blue };
			{
				double currentLeft = containerControl.Width - controlPadding.Right - buttonMargin.Right - buttonSize;
				double buttonHeightWithMargin = buttonSize + buttonMargin.Height;
				double scaledWidth = (containerControl.Width - controlPadding.Width - buttonMargin.Width * 6 - buttonSize * 2) / 4;
				// the left unsized rect
				eightControlRectangles[0] = new RectangleDouble(
						currentLeft,
						0,
						currentLeft + buttonSize,
						buttonSize);

				// a bottom anchor
				currentLeft -= scaledWidth + buttonMargin.Width;
				double bottomAnchorY = controlPadding.Bottom + buttonMargin.Bottom;
				eightControlRectangles[1] = new RectangleDouble(currentLeft, bottomAnchorY, currentLeft + scaledWidth, bottomAnchorY + buttonSize);

				// center anchor
				double centerYOfContainer = controlPadding.Bottom + (containerControl.Height - controlPadding.Height) / 2;
				currentLeft -= scaledWidth + buttonMargin.Width;
				eightControlRectangles[2] = new RectangleDouble(currentLeft, centerYOfContainer - buttonHeightWithMargin / 2 + buttonMargin.Bottom, currentLeft + scaledWidth, centerYOfContainer + buttonHeightWithMargin / 2 - buttonMargin.Top);

				// top anchor
				double topAnchorY = containerControl.Height - controlPadding.Top - buttonMargin.Top;
				currentLeft -= scaledWidth + buttonMargin.Width;
				eightControlRectangles[3] = new RectangleDouble(currentLeft, topAnchorY - buttonSize, currentLeft + scaledWidth, topAnchorY);

				// bottom top anchor
				currentLeft -= scaledWidth + buttonMargin.Width;
				eightControlRectangles[4] = new RectangleDouble(currentLeft, bottomAnchorY, currentLeft + scaledWidth, topAnchorY);

				// right anchor
				currentLeft -= buttonSize + buttonMargin.Width;
				eightControlRectangles[5] = new RectangleDouble(currentLeft, 0, currentLeft + buttonSize, buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 6; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], testColors[i]);
				}
			}

			var containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			var rightToLeftFlowLayoutAll = new FlowLayoutWidget(FlowDirection.RightToLeft);
			containerTest.DoubleBuffer = true;
			{
				rightToLeftFlowLayoutAll.AnchorAll();
				rightToLeftFlowLayoutAll.Padding = controlPadding;
				{
					var left = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = testColors[0],
						Margin = buttonMargin
					};
					rightToLeftFlowLayoutAll.AddChild(left);

					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Bottom, testColors[1]));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Center, testColors[2]));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Top, testColors[3]));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.Stretch, testColors[4]));

					var right = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = testColors[5],
						Margin = buttonMargin
					};
					rightToLeftFlowLayoutAll.AddChild(right);
				}

				containerTest.AddChild(rightToLeftFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			int index = 0;
			foreach (var child in rightToLeftFlowLayoutAll.Children)
			{
				MhAssert.True(eightControlRectangles[index++].Equals(child.BoundsRelativeToParent, .001));
			}

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			MhAssert.True(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

        [MhTest]
        public void BottomTopWithAnchorBottomTopChildTests()
		{
			BottomTopWithAnchorBottomTopChildTest(default(BorderDouble), default(BorderDouble));
			BottomTopWithAnchorBottomTopChildTest(default(BorderDouble), new BorderDouble(3));
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(0));
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(3));
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void BottomTopWithAnchorBottomTopChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			var containerControl = new GuiWidget(buttonSize * 3, buttonSize * 6)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};

			var eightControlRectangles = new RectangleDouble[6];
			var sixColors = new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.YellowGreen, Color.Green, Color.Blue };
			{
				double currentBottom = controlPadding.Bottom + buttonMargin.Bottom;
				double buttonWidthWithMargin = buttonSize + buttonMargin.Width;
				double scaledHeight = (containerControl.Height - controlPadding.Height - buttonMargin.Height * 6 - buttonSize * 2) / 4;
				// the bottom unsized rect
				eightControlRectangles[0] = new RectangleDouble(
						0,
						currentBottom,
						buttonSize,
						currentBottom + buttonSize);

				// left anchor
				currentBottom += buttonSize + buttonMargin.Height;
				double leftAnchorX = controlPadding.Left + buttonMargin.Left;
				eightControlRectangles[1] = new RectangleDouble(leftAnchorX, currentBottom, leftAnchorX + buttonSize, currentBottom + scaledHeight);

				// center anchor
				double centerXOfContainer = controlPadding.Left + (containerControl.Width - controlPadding.Width) / 2;
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[2] = new RectangleDouble(centerXOfContainer - buttonWidthWithMargin / 2 + buttonMargin.Left, currentBottom, centerXOfContainer + buttonWidthWithMargin / 2 - buttonMargin.Right, currentBottom + scaledHeight);

				// right anchor
				double rightAnchorX = containerControl.Width - controlPadding.Right - buttonMargin.Right;
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[3] = new RectangleDouble(rightAnchorX - buttonSize, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// left right anchor
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[4] = new RectangleDouble(leftAnchorX, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// top anchor
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[5] = new RectangleDouble(0, currentBottom, buttonSize, currentBottom + buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 6; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], sixColors[i]);
				}
			}

			var containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			var bottomToTopFlowLayoutAll = new FlowLayoutWidget(FlowDirection.BottomToTop);
			containerTest.DoubleBuffer = true;
			{
				bottomToTopFlowLayoutAll.AnchorAll();
				bottomToTopFlowLayoutAll.Padding = controlPadding;
				{
					var bottom = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = sixColors[0],
						Margin = buttonMargin
					};
					bottomToTopFlowLayoutAll.AddChild(bottom);

					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Left, sixColors[1]));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Center, sixColors[2]));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Right, sixColors[3]));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Stretch, sixColors[4]));

					var top = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = sixColors[5],
						Margin = buttonMargin
					};
					bottomToTopFlowLayoutAll.AddChild(top);
				}

				containerTest.AddChild(bottomToTopFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			int index = 0;
			foreach (var child in bottomToTopFlowLayoutAll.Children)
			{
				MhAssert.True(eightControlRectangles[index++] == child.BoundsRelativeToParent);
			}

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			MhAssert.True(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		private GuiWidget CreateBottomToTopMiddleWidget(BorderDouble buttonMargin, double buttonSize, HAnchor hAnchor, Color color)
		{
			var middle = new GuiWidget(buttonSize, buttonSize / 2)
			{
				Margin = buttonMargin,
				VAnchor = VAnchor.Stretch,
				HAnchor = hAnchor,
				BackgroundColor = color
			};
			return middle;
		}

        [MhTest]
        public void TopBottomWithAnchorBottomTopChildTests()
		{
			TopBottomWithAnchorBottomTopChildTest(default(BorderDouble), default(BorderDouble));
			TopBottomWithAnchorBottomTopChildTest(default(BorderDouble), new BorderDouble(3));
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(0));
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(3));
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void TopBottomWithAnchorBottomTopChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			var containerControl = new GuiWidget(buttonSize * 3, buttonSize * 6)
			{
				Padding = controlPadding,
				DoubleBuffer = true
			};

			var eightControlRectangles = new RectangleDouble[6];
			var testColors = new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.YellowGreen, Color.Green, Color.Blue };
			{
				double currentBottom = containerControl.Height - controlPadding.Top - buttonMargin.Top - buttonSize;
				double buttonWidthWithMargin = buttonSize + buttonMargin.Width;
				double scaledHeight = (containerControl.Height - controlPadding.Height - buttonMargin.Height * 6 - buttonSize * 2) / 4;
				// the bottom unsized rect
				eightControlRectangles[0] = new RectangleDouble(
						0,
						currentBottom,
						buttonSize,
						currentBottom + buttonSize);

				// left anchor
				currentBottom -= scaledHeight + buttonMargin.Height;
				double leftAnchorX = controlPadding.Left + buttonMargin.Left;
				eightControlRectangles[1] = new RectangleDouble(leftAnchorX, currentBottom, leftAnchorX + buttonSize, currentBottom + scaledHeight);

				// center anchor
				double centerXOfContainer = controlPadding.Left + (containerControl.Width - controlPadding.Width) / 2;
				currentBottom -= scaledHeight + buttonMargin.Height;
				eightControlRectangles[2] = new RectangleDouble(centerXOfContainer - buttonWidthWithMargin / 2 + buttonMargin.Left, currentBottom, centerXOfContainer + buttonWidthWithMargin / 2 - buttonMargin.Right, currentBottom + scaledHeight);

				// right anchor
				double rightAnchorX = containerControl.Width - controlPadding.Right - buttonMargin.Right;
				currentBottom -= scaledHeight + buttonMargin.Height;
				eightControlRectangles[3] = new RectangleDouble(rightAnchorX - buttonSize, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// left right anchor
				currentBottom -= scaledHeight + buttonMargin.Height;
				eightControlRectangles[4] = new RectangleDouble(leftAnchorX, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// top anchor
				currentBottom -= buttonSize + buttonMargin.Height;
				eightControlRectangles[5] = new RectangleDouble(0, currentBottom, buttonSize, currentBottom + buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 6; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], testColors[i]);
				}
			}

			var containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			var bottomToTopFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
			containerTest.DoubleBuffer = true;
			{
				bottomToTopFlowLayoutAll.AnchorAll();
				bottomToTopFlowLayoutAll.Padding = controlPadding;
				{
					var top = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = testColors[0],
						Margin = buttonMargin
					};
					bottomToTopFlowLayoutAll.AddChild(top);

					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Left, testColors[1]));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Center, testColors[2]));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Right, testColors[3]));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.Stretch, testColors[4]));

					var bottom = new GuiWidget(buttonSize, buttonSize)
					{
						BackgroundColor = testColors[5],
						Margin = buttonMargin
					};
					bottomToTopFlowLayoutAll.AddChild(bottom);
				}

				containerTest.AddChild(bottomToTopFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			int index = 0;
			foreach (var child in bottomToTopFlowLayoutAll.Children)
			{
				MhAssert.True(eightControlRectangles[index++].Equals(child.BoundsRelativeToParent, .001));
			}

			MhAssert.True(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			MhAssert.True(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		public void EnsureFlowLayoutMinSizeFitsChildrenMinSize()
		{
			// This test is to prove that a flow layout widget always has it's min size set
			// to the enclosing bounds size of all it's childrens min size.
			// The code to be tested will expand the flow layouts min size as it's children's min size change.
			var containerTest = new GuiWidget(640, 480);
			var topToBottomFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
			containerTest.AddChild(topToBottomFlowLayoutAll);
			containerTest.DoubleBuffer = true;

			var topLeftToRight = new FlowLayoutWidget(FlowDirection.LeftToRight);
			topToBottomFlowLayoutAll.AddChild(topLeftToRight);
			GuiWidget bottomLeftToRight = new FlowLayoutWidget(FlowDirection.LeftToRight);
			topToBottomFlowLayoutAll.AddChild(bottomLeftToRight);

			topLeftToRight.AddChild(new Button("top button"));

			var bottomContentTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			bottomLeftToRight.AddChild(bottomContentTopToBottom);

			var button1 = new Button("button1");
			MhAssert.True(button1.MinimumSize.X > 0, "Buttons should set their min size on construction.");
			bottomContentTopToBottom.AddChild(button1);
			// Assert.True(bottomContentTopToBottom.MinimumSize.x >= button1.MinimumSize.x, "There should be space for the button.");
			bottomContentTopToBottom.AddChild(new Button("button2"));
			var wideButton = new Button("button3 Wide");
			bottomContentTopToBottom.AddChild(wideButton);
			// Assert.True(bottomContentTopToBottom.MinimumSize.x >= wideButton.MinimumSize.x, "These should be space for the button.");

			containerTest.BackgroundColor = Color.White;
			containerTest.OnDrawBackground(containerTest.NewGraphics2D());
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImage(containerTest.BackBuffer, "zFlowLaoutsGetMinSize.tga");

			MhAssert.True(bottomLeftToRight.Width > 0, "This needs to have been expanded when the bottomContentTopToBottom grew.");
			MhAssert.True(bottomLeftToRight.MinimumSize.X >= bottomContentTopToBottom.MinimumSize.X, "These should be space for the next flowLayout.");
			MhAssert.True(containerTest.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
		}

        [MhTest]
        public void ChildVisibilityChangeCauseResize()
		{
			// Test whether toggling the visibility of children changes the flow layout
			var containerTest = new GuiWidget(640, 480);
			var topToBottomFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
			containerTest.AddChild(topToBottomFlowLayoutAll);

			var item1 = new GuiWidget(1, 20);
			var item2 = new GuiWidget(1, 30);
			var item3 = new GuiWidget(1, 40);

			topToBottomFlowLayoutAll.AddChild(item1);
			MhAssert.True(topToBottomFlowLayoutAll.Height == 20);
			topToBottomFlowLayoutAll.AddChild(item2);
			MhAssert.True(topToBottomFlowLayoutAll.Height == 50);
			topToBottomFlowLayoutAll.AddChild(item3);
			MhAssert.True(topToBottomFlowLayoutAll.Height == 90);

			item2.Visible = false;

			MhAssert.True(topToBottomFlowLayoutAll.Height == 60);
		}

		internal void EnsureCorrectMinimumSize()
		{
			{
				var containerTest = new GuiWidget(640, 480)
				{
					DoubleBuffer = true
				};
				var leftToRightLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
				containerTest.AddChild(leftToRightLayout);

				var item1 = new GuiWidget(10, 11);
				var item2 = new GuiWidget(20, 22);
				var item3 = new GuiWidget(30, 33)
				{
					HAnchor = HAnchor.Stretch
				};

				leftToRightLayout.AddChild(item1);
				leftToRightLayout.AddChild(item2);
				leftToRightLayout.AddChild(item3);

				leftToRightLayout.AnchorAll();
				containerTest.OnDraw(containerTest.NewGraphics2D());
				MhAssert.True(leftToRightLayout.Width == 640);
				// Assert.True(leftToRightLayout.MinimumSize.x == 60);
				MhAssert.True(leftToRightLayout.Height == 480);
				// Assert.True(leftToRightLayout.MinimumSize.y == 33);
				MhAssert.True(item3.Width == 610);

				containerTest.OnDraw(containerTest.NewGraphics2D());
				MhAssert.True(leftToRightLayout.Width == 640);
				// Assert.True(leftToRightLayout.MinimumSize.x == 60);
				MhAssert.True(leftToRightLayout.Height == 480);
				// Assert.True(leftToRightLayout.MinimumSize.y == 33);
				MhAssert.True(item3.Width == 610);

				containerTest.Width = 650;
				containerTest.OnDraw(containerTest.NewGraphics2D());
				MhAssert.True(leftToRightLayout.Width == 650);
				// Assert.True(leftToRightLayout.MinimumSize.x == 60);
				MhAssert.True(leftToRightLayout.Height == 480);
				// Assert.True(leftToRightLayout.MinimumSize.y == 33);
				MhAssert.True(item3.Width == 620);

				containerTest.Width = 640;
				containerTest.OnDraw(containerTest.NewGraphics2D());
				MhAssert.True(leftToRightLayout.Width == 640);
				// Assert.True(leftToRightLayout.MinimumSize.x == 60);
				MhAssert.True(leftToRightLayout.Height == 480);
				// Assert.True(leftToRightLayout.MinimumSize.y == 33);
				MhAssert.True(item3.Width == 610);
			}

			{
				var containerTest = new GuiWidget(640, 480)
				{
					DoubleBuffer = true
				};
				var leftToRightLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
				containerTest.AddChild(leftToRightLayout);

				var item1 = new GuiWidget(10, 11);
				var item2 = new GuiWidget(20, 22);
				var item3 = new GuiWidget(30, 33)
				{
					VAnchor = VAnchor.Stretch
				};

				leftToRightLayout.AddChild(item1);
				leftToRightLayout.AddChild(item2);
				leftToRightLayout.AddChild(item3);

				leftToRightLayout.AnchorAll();
				containerTest.OnDraw(containerTest.NewGraphics2D());
				MhAssert.True(leftToRightLayout.Width == 640);
				// Assert.True(leftToRightLayout.MinimumSize.x == 30);

				MhAssert.True(leftToRightLayout.Height == 480);
				// Assert.True(leftToRightLayout.MinimumSize.y == 66);
			}
		}

		internal void EnsureNestedAreMinimumSize()
		{
			var containerTest = new GuiWidget(640, 480)
			{
				DoubleBuffer = true
			};
			var leftToRightLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			containerTest.AddChild(leftToRightLayout);

			var item1 = new GuiWidget(10, 11);
			var item2 = new GuiWidget(20, 22);
			var item3 = new GuiWidget(30, 33)
			{
				HAnchor = HAnchor.Stretch
			};

			leftToRightLayout.AddChild(item1);
			leftToRightLayout.AddChild(item2);
			leftToRightLayout.AddChild(item3);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			MhAssert.True(leftToRightLayout.Width == 60);
			MhAssert.True(leftToRightLayout.MinimumSize.X == 0);
			MhAssert.True(leftToRightLayout.Height == 33);
			MhAssert.True(leftToRightLayout.MinimumSize.Y == 0);
			MhAssert.True(item3.Width == 30);

			containerTest.Width = 650;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			MhAssert.True(leftToRightLayout.Width == 60);
			MhAssert.True(leftToRightLayout.MinimumSize.X == 0);
			MhAssert.True(leftToRightLayout.Height == 33);
			MhAssert.True(leftToRightLayout.MinimumSize.Y == 0);
			MhAssert.True(item3.Width == 30);
		}

        [MhTest]
        public void EnsureCorrectSizeOnChildrenVisibleChange()
		{
			// just one column changes correctly
			{
				var testColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Name = "testColumn"
				};

				var item1 = new GuiWidget(10, 10)
				{
					Name = "item1"
				};
				testColumn.AddChild(item1);

				MhAssert.True(testColumn.Height == 10);

				var item2 = new GuiWidget(11, 11)
				{
					Name = "item2"
				};
				testColumn.AddChild(item2);

				MhAssert.True(testColumn.Height == 21);

				var item3 = new GuiWidget(12, 12)
				{
					Name = "item3"
				};
				testColumn.AddChild(item3);

				MhAssert.True(testColumn.Height == 33);

				item2.Visible = false;

				MhAssert.True(testColumn.Height == 22);

				item2.Visible = true;

				MhAssert.True(testColumn.Height == 33);
			}

			// nested columns change correctly
			{
				GuiWidget.DefaultEnforceIntegerBounds = true;
				CheckBox hideCheckBox;
				FlowLayoutWidget leftColumn;
				FlowLayoutWidget topLeftStuff;
				var everything = new GuiWidget(500, 500);
				GuiWidget firstItem;
				GuiWidget thingToHide;
				{
					var twoColumns = new FlowLayoutWidget
					{
						Name = "twoColumns",
						VAnchor = VAnchor.Top
					};
					{
						leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
						{
							Name = "leftColumn"
						};
						{
							topLeftStuff = new FlowLayoutWidget(FlowDirection.TopToBottom)
							{
								Name = "topLeftStuff"
							};
							firstItem = new TextWidget("Top of Top Stuff");
							topLeftStuff.AddChild(firstItem);
							thingToHide = new Button("thing to hide");
							topLeftStuff.AddChild(thingToHide);
							topLeftStuff.AddChild(new TextWidget("Bottom of Top Stuff"));

							leftColumn.AddChild(topLeftStuff);
							// leftColumn.DebugShowBounds = true;
						}

						twoColumns.AddChild(leftColumn);
					}

					{
						var rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
						{
							Name = "rightColumn"
						};
						hideCheckBox = new CheckBox("Hide Stuff");
						rightColumn.AddChild(hideCheckBox);
						hideCheckBox.CheckedStateChanged += (sender, e) =>
						{
							if (hideCheckBox.Checked)
							{
								thingToHide.Visible = false;
							}
							else
							{
								thingToHide.Visible = true;
							}
						};

						twoColumns.AddChild(rightColumn);
					}

					everything.AddChild(twoColumns);

					MhAssert.True(firstItem.OriginRelativeParent.Y == 54);
					// Assert.True(firstItem.OriginRelativeParent.y - topLeftStuff.LocalBounds.Bottom == 54);
					MhAssert.True(twoColumns.BoundsRelativeToParent.Top == 500);
					MhAssert.True(leftColumn.BoundsRelativeToParent.Top == 67);
					MhAssert.True(leftColumn.BoundsRelativeToParent.Bottom == 0);
					MhAssert.True(leftColumn.OriginRelativeParent.Y == 0);
					MhAssert.True(topLeftStuff.BoundsRelativeToParent.Top == 67);
					MhAssert.True(topLeftStuff.Height == 67);
					MhAssert.True(leftColumn.Height == 67);

					hideCheckBox.Checked = true;

					MhAssert.True(firstItem.OriginRelativeParent.Y == 21);
					MhAssert.True(leftColumn.OriginRelativeParent.Y == 0);
					MhAssert.True(leftColumn.BoundsRelativeToParent.Bottom == 0);
					MhAssert.True(topLeftStuff.Height == 34);
					MhAssert.True(leftColumn.Height == 34);
				}

				GuiWidget.DefaultEnforceIntegerBounds = false;
			}
		}

        [MhTest]
        public void ChildHAnchorPriority()
		{
			// make sure a middle spacer grows and shrinks correctly
			{
				var leftRightFlowLayout = new FlowLayoutWidget()
				{
					Name = "leftRightFlowLayout"
				};
				MhAssert.True(leftRightFlowLayout.HAnchor == HAnchor.Fit); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.Stretch; // add to the existing flags Stretch (starts with FitToChildren)
				// [no content] // attempting to make a visual description of what is happening
				MhAssert.True(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't
				var leftWidget = new GuiWidget(10, 10)
				{
					Name = "leftWidget"
				}; // we call it left widget as it will be the first one in the left to right flow layout
				leftRightFlowLayout.AddChild(leftWidget); // add in a child with a width of 10
				// [<->(10)<->] // the flow layout should now be forced to be 10 wide
				MhAssert.True(leftRightFlowLayout.Width == 10);

				var middleSpacer = new GuiWidget(0, 10)
				{
					Name = "middleSpacer"
				}; // this widget will hold the space
				middleSpacer.HAnchor = HAnchor.Stretch; // by resizing to whatever width it can be
				leftRightFlowLayout.AddChild(middleSpacer);
				// [<->(10)(<->)<->]
				MhAssert.True(leftRightFlowLayout.Width == 10);
				MhAssert.True(middleSpacer.Width == 0);

				var rightItem = new GuiWidget(10, 10);
				leftRightFlowLayout.AddChild(rightItem);
				// [<->(10)(<->)(10)<->]
				MhAssert.True(leftRightFlowLayout.Width == 20);

				var container = new GuiWidget(40, 20);
				container.AddChild(leftRightFlowLayout);
				// (40[<->(10)(<->)(10)<->]) // the extra 20 must be put into the expandable (<->)
				MhAssert.True(container.Width == 40);
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 20);

				container.Width = 50;
				// (50[<->(10)(<->)(10)<->]) // the extra 30 must be put into the expandable (<->)
				MhAssert.True(container.Width == 50);
				MhAssert.True(leftRightFlowLayout.Width == 50);
				MhAssert.True(middleSpacer.Width == 30);

				container.Width = 40;
				// (40[<->(10)(<->)(10)<->]) // the extra 20 must be put into the expandable (<->) by shrinking it
				MhAssert.True(container.Width == 40);
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 20);

				MhAssert.True(container.MinimumSize.X == 40); // minimum size is set to the construction size for normal GuiWidgets
				container.MinimumSize = new Vector2(0, 0); // make sure we can make this smaller
				container.Width = 10;
				// (10[<->(10)(<->)(10)<->]) // the extra 20 must be put into the expandable (<->) by shrinking it
				MhAssert.True(container.Width == 10); // nothing should be keeping this big
				MhAssert.True(leftRightFlowLayout.Width == 20); // it can't get smaller than its contents
				MhAssert.True(middleSpacer.Width == 0);
			}

			// make sure the middle spacer works the same when in a flow layout
			{
				var leftRightFlowLayout = new FlowLayoutWidget
				{
					Name = "leftRightFlowLayout"
				};
				MhAssert.True(leftRightFlowLayout.HAnchor == HAnchor.Fit); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.Stretch; // add to the existing flags Stretch (starts with FitToChildren)
				// [<-><->] // attempting to make a visual description of what is happening
				MhAssert.True(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't
				var leftWidget = new GuiWidget(10, 10)
				{
					Name = "leftWidget"
				}; // we call it left widget as it will be the first one in the left to right flow layout
				leftRightFlowLayout.AddChild(leftWidget); // add in a child with a width of 10
				// [<->(10)<->] // the flow layout should now be forced to be 10 wide
				MhAssert.True(leftRightFlowLayout.Width == 10);

				var middleFlowLayoutWrapper = new FlowLayoutWidget
				{
					Name = "middleFlowLayoutWrapper"
				}; // we are going to wrap the implicitly middle items to test nested resizing
				middleFlowLayoutWrapper.HAnchor |= HAnchor.Stretch;
				var middleSpacer = new GuiWidget(0, 10)
				{
					Name = "middleSpacer",
					HAnchor = HAnchor.Stretch // by resizing to whatever width it can be
				}; // this widget will hold the space
				middleFlowLayoutWrapper.AddChild(middleSpacer);
				// {<->(<->)<->}
				leftRightFlowLayout.AddChild(middleFlowLayoutWrapper);
				// [<->(10){<->(<->)<->}<->]
				MhAssert.True(leftRightFlowLayout.Width == 10);
				MhAssert.True(middleFlowLayoutWrapper.Width == 0);
				MhAssert.True(middleSpacer.Width == 0);

				var rightWidget = new GuiWidget(10, 10)
				{
					Name = "rightWidget"
				};
				leftRightFlowLayout.AddChild(rightWidget);
				// [<->(10){<->(<->)<->}(10)<->]
				MhAssert.True(leftRightFlowLayout.Width == 20);

				var container = new GuiWidget(40, 20)
				{
					Name = "container"
				};
				container.AddChild(leftRightFlowLayout);
				// (40[<->(10){<->(<->)<->}(10)<->]) // the extra 20 must be put into the expandable (<->)
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleFlowLayoutWrapper.Width == 20);
				MhAssert.True(middleSpacer.Width == 20);

				container.Width = 50;
				// (50[<->(10){<->(<->)<->}(10)<->]) // the extra 30 must be put into the expandable (<->)
				MhAssert.True(leftRightFlowLayout.Width == 50);
				MhAssert.True(middleSpacer.Width == 30);

				container.Width = 40;
				// (50[<->(10){<->(<->)<->}(10)<->]) // the extra 20 must be put into the expandable (<->) by shrinking it
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 20);
			}

			// make sure a middle spacer grows and shrinks correctly when in another guiwidget (not a flow widget) that is LeftRight
			{
				var leftRightFlowLayout = new FlowLayoutWidget
				{
					Name = "leftRightFlowLayout"
				};
				MhAssert.True(leftRightFlowLayout.HAnchor == HAnchor.Fit); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.Stretch; // add to the existing flags Stretch (starts with FitToChildren)
				// [<-><->] // attempting to make a visual description of what is happening
				MhAssert.True(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't

				var middleSpacer = new GuiWidget(0, 10)
				{
					Name = "middleSpacer",
					HAnchor = HAnchor.Stretch // by resizing to whatever width it can be
				}; // this widget will hold the space
				leftRightFlowLayout.AddChild(middleSpacer);
				// [<->(<->)<->]
				MhAssert.True(leftRightFlowLayout.Width == 0);
				MhAssert.True(middleSpacer.Width == 0);

				MhAssert.True(leftRightFlowLayout.Width == 0);

				var containerOuter = new GuiWidget(40, 20)
				{
					Name = "containerOuter"
				};
				var containerInner = new GuiWidget(0, 20)
				{
					HAnchor = HAnchor.Stretch | HAnchor.Fit,
					Name = "containerInner"
				};
				containerOuter.AddChild(containerInner);
				MhAssert.True(containerInner.Width == 40);
				containerInner.AddChild(leftRightFlowLayout);
				// (40(<-[<->(<->)<->]->)) // the extra 20 must be put into the expandable (<->)
				MhAssert.True(containerInner.Width == 40);
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 40);

				containerOuter.Width = 50;
				// (50(<-[<->(<->)<->]->) // the extra 30 must be put into the expandable (<->)
				MhAssert.True(containerInner.Width == 50);
				MhAssert.True(leftRightFlowLayout.Width == 50);
				MhAssert.True(middleSpacer.Width == 50);

				containerOuter.Width = 40;
				// (40(<-[<->(<->)<->]->) // the extra 20 must be put into the expandable (<->) by shrinking it
				MhAssert.True(containerInner.Width == 40);
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 40);
			}

			// make sure a middle spacer grows and shrinks correctly when in another guiwidget (not a flow widget) that is LeftRight
			{
				var leftRightFlowLayout = new FlowLayoutWidget();
				MhAssert.True(leftRightFlowLayout.HAnchor == HAnchor.Fit); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.Stretch; // add to the existing flags Stretch (starts with FitToChildren)
				// [<-><->] // attempting to make a visual description of what is happening
				MhAssert.True(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't
				var leftWidget = new GuiWidget(10, 10); // we call it left widget as it will be the first one in the left to right flow layout
				leftRightFlowLayout.AddChild(leftWidget); // add in a child with a width of 10
				// [<->(10)<->] // the flow layout should now be forced to be 10 wide
				MhAssert.True(leftRightFlowLayout.Width == 10);

				var middleSpacer = new GuiWidget(0, 10)
				{
					HAnchor = HAnchor.Stretch // by resizing to whatever width it can be
				}; // this widget will hold the space
				leftRightFlowLayout.AddChild(middleSpacer);
				// [<->(10)(<->)<->]
				MhAssert.True(leftRightFlowLayout.Width == 10);
				MhAssert.True(middleSpacer.Width == 0);

				var rightItem = new GuiWidget(10, 10);
				leftRightFlowLayout.AddChild(rightItem);
				// [<->(10)(<->)(10)<->]
				MhAssert.True(leftRightFlowLayout.Width == 20);

				var containerOuter = new GuiWidget(40, 20)
				{
					Name = "containerOuter"
				};
				var containerInner = new GuiWidget(0, 20)
				{
					HAnchor = HAnchor.Stretch | HAnchor.Fit,
					Name = "containerInner"
				};
				containerOuter.AddChild(containerInner);
				MhAssert.True(containerInner.Width == 40);
				containerInner.AddChild(leftRightFlowLayout);
				// (40(<-[<->(10)(<->)(10)<->]->)) // the extra 20 must be put into the expandable (<->)
				MhAssert.True(containerInner.Width == 40);
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 20);

				containerOuter.Width = 50;
				// (50(<-[<->(10)(<->)(10)<->]->) // the extra 30 must be put into the expandable (<->)
				MhAssert.True(containerInner.Width == 50);
				MhAssert.True(leftRightFlowLayout.Width == 50);
				MhAssert.True(middleSpacer.Width == 30);

				containerOuter.Width = 40;
				// (40(<-[<->(10)(<->)(10)<->]->) // the extra 20 must be put into the expandable (<->) by shrinking it
				MhAssert.True(containerInner.Width == 40);
				MhAssert.True(leftRightFlowLayout.Width == 40);
				MhAssert.True(middleSpacer.Width == 20);
			}
		}

        [MhTest]
        public void TestVAnchorCenter()
		{
			var searchPanel = new FlowLayoutWidget
			{
				BackgroundColor = new Color(180, 180, 180),
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(3, 3)
			};
			{
				var searchInput = new TextEditWidget("Test")
				{
					Margin = new BorderDouble(6, 0),
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Center
				};

				var searchButton = new Button("Search")
				{
					Margin = new BorderDouble(right: 9)
				};

				searchPanel.AddChild(searchInput);
				MhAssert.True(searchInput.BoundsRelativeToParent.Bottom - searchPanel.BoundsRelativeToParent.Bottom == searchPanel.BoundsRelativeToParent.Top - searchInput.BoundsRelativeToParent.Top);
				searchPanel.AddChild(searchButton);
				MhAssert.True(searchInput.BoundsRelativeToParent.Bottom - searchPanel.BoundsRelativeToParent.Bottom == searchPanel.BoundsRelativeToParent.Top - searchInput.BoundsRelativeToParent.Top);
			}

			searchPanel.Close();
		}
	}
}
