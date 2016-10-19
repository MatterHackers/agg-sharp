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

using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class FlowLayoutTests
	{
		public static bool saveImagesForDebug = false;

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

		[Test]
		public void TopToBottomContainerAppliesExpectedMargin()
		{
			int marginSize = 40;
			int dimensions = 300;

			GuiWidget outerContainer = new GuiWidget(dimensions, dimensions);

			FlowLayoutWidget topToBottomContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = UI.VAnchor.ParentBottomTop,
			};
			outerContainer.AddChild(topToBottomContainer);

			GuiWidget childWidget = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
				Margin = new BorderDouble(marginSize),
				BackgroundColor = RGBA_Bytes.Red,
			};

			topToBottomContainer.AddChild(childWidget);
			topToBottomContainer.AnchorAll();
			topToBottomContainer.PerformLayout();

			outerContainer.DoubleBuffer = true;
			outerContainer.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			outerContainer.OnDraw(outerContainer.NewGraphics2D());

			// For troubleshooting or visual validation
			//saveImagesForDebug = true;
			//OutputImages(outerContainer, outerContainer);

			var bounds = childWidget.BoundsRelativeToParent;
			Assert.IsTrue(bounds.Left == marginSize, "Left margin is incorrect");
			Assert.IsTrue(bounds.Right == dimensions - marginSize, "Right margin is incorrect");
			Assert.IsTrue(bounds.Top == dimensions - marginSize, "Top margin is incorrect");
			Assert.IsTrue(bounds.Bottom == marginSize, "Bottom margin is incorrect");
		}

		[Test]
		public void NestedLayoutTopToBottomTests()
		{
			NestedLayoutTopToBottomTest(new BorderDouble(), new BorderDouble());
			NestedLayoutTopToBottomTest(new BorderDouble(), new BorderDouble(3));
			NestedLayoutTopToBottomTest(new BorderDouble(2), new BorderDouble(0));
			NestedLayoutTopToBottomTest(new BorderDouble(2), new BorderDouble(3));
			NestedLayoutTopToBottomTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		[Test]
		public void ChangingChildVisiblityUpdatesFlow()
		{
			//  ___________________________________________________
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
			//

			GuiWidget containerControl = new GuiWidget(300, 200);
			containerControl.Name = "containerControl";
	
			FlowLayoutWidget flow1 = new FlowLayoutWidget(FlowDirection.LeftToRight);
			flow1.Name = "flow1";
			flow1.HAnchor = HAnchor.ParentLeftRight;
			flow1.Padding = new BorderDouble(3, 3);
			containerControl.AddChild(flow1);

			FlowLayoutWidget flow2 = new FlowLayoutWidget();
			flow2.Name = "flow2";
			flow1.AddChild(flow2);

			GuiWidget size1 = new GuiWidget(200, 20);
			size1.Name = "size2";
			flow2.AddChild(size1);

			GuiWidget size2 = new GuiWidget(50, 20);
			size2.Name = "size1";
			flow2.AddChild(size2);

			Assert.IsTrue(flow1.Width == containerControl.Width);
			Assert.IsTrue(flow2.Width == size2.Width + size1.Width);

			size1.Visible = false;
			//  ___________________________________________________
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

			Assert.IsTrue(flow1.Width == containerControl.Width);
			Assert.IsTrue(flow2.Width == size2.Width);
		}

		[Test]
		public void ChangingChildFlowWidgetVisiblityUpdatesParentFlow()
		{
			//  ___________________________________________________
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
			//

			GuiWidget containerControl = new GuiWidget(300, 200);
			containerControl.Name = "containerControl";

			FlowLayoutWidget flow1 = new FlowLayoutWidget(FlowDirection.LeftToRight);
			flow1.Name = "flow1";
			flow1.HAnchor = HAnchor.ParentLeftRight;
			flow1.Padding = new BorderDouble(3, 3);
			containerControl.AddChild(flow1);

			FlowLayoutWidget flow2 = new FlowLayoutWidget();
			flow2.Name = "flow2";
			flow1.AddChild(flow2);

			GuiWidget flow3 = new FlowLayoutWidget();
			flow3.Name = "flow3";
			flow2.AddChild(flow3);

			GuiWidget size1 = new GuiWidget(200, 20);
			size1.Name = "size2";
			flow3.AddChild(size1);

			GuiWidget size2 = new GuiWidget(50, 20);
			size2.Name = "size1";
			flow2.AddChild(size2);


			Assert.IsTrue(flow1.Width == containerControl.Width);
			Assert.IsTrue(flow3.Width == size1.Width);
			Assert.IsTrue(flow2.Width == size2.Width + flow3.Width);

			size1.Visible = false;
			//  ___________________________________________________
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

			Assert.IsTrue(flow1.Width == containerControl.Width);
			Assert.IsTrue(flow2.Width == size2.Width);
		}

		public void NestedLayoutTopToBottomTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(300, 200);
			containerControl.DoubleBuffer = true;
			containerControl.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			{
				Button topButtonC = new Button("top button");
				Button bottomButtonC = new Button("bottom wide button");
				topButtonC.LocalBounds = new RectangleDouble(0, 0, bottomButtonC.LocalBounds.Width, 40);
				topButtonC.OriginRelativeParent = new Vector2(bottomButtonC.OriginRelativeParent.x + buttonMargin.Left, containerControl.Height - controlPadding.Top - topButtonC.Height - buttonMargin.Top);
				containerControl.AddChild(topButtonC);
				bottomButtonC.OriginRelativeParent = new Vector2(bottomButtonC.OriginRelativeParent.x + buttonMargin.Left, topButtonC.OriginRelativeParent.y - buttonMargin.Height - bottomButtonC.Height);
				containerControl.AddChild(bottomButtonC);
			}
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(300, 200);
			containerTest.DoubleBuffer = true;
			containerTest.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);

			FlowLayoutWidget allButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
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
					topButtonT = new Button("top button");
					topButtonT.LocalBounds = new RectangleDouble(0, 0, bottomButtonT.LocalBounds.Width, 40);
					topButtonT.Margin = buttonMargin;
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

			Assert.IsTrue(containerTest.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerTest.BackBuffer.Equals(containerControl.BackBuffer, 1), "The test should contain the same image as the control.");
		}

		[Test]
		public void NestedLayoutTopToBottomWithResizeTests()
		{
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(), new BorderDouble());
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(), new BorderDouble(3));
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(2), new BorderDouble(0));
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(2), new BorderDouble(3));
			NestedLayoutTopToBottomWithResizeTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedLayoutTopToBottomWithResizeTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerTest = new GuiWidget(300, 200);
			containerTest.Padding = controlPadding;
			containerTest.DoubleBuffer = true;
			containerTest.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);

			FlowLayoutWidget allButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				FlowLayoutWidget topButtonBar = new FlowLayoutWidget();
				{
					Button button1 = new Button("button1");
					button1.Margin = buttonMargin;
					topButtonBar.AddChild(button1);
				}
				allButtons.AddChild(topButtonBar);

				FlowLayoutWidget bottomButtonBar = new FlowLayoutWidget();
				{
					Button button2 = new Button("wide button2");
					button2.Margin = buttonMargin;
					bottomButtonBar.AddChild(button2);
				}
				allButtons.AddChild(bottomButtonBar);
			}
			containerTest.AddChild(allButtons);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			ImageBuffer controlImage = new ImageBuffer(containerTest.BackBuffer, new BlenderBGRA());

			OutputImage(controlImage, "image-control.tga");

			RectangleDouble oldBounds = containerTest.LocalBounds;
			RectangleDouble newBounds = oldBounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			containerTest.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImage(containerTest, "image-test.tga");

			containerTest.LocalBounds = oldBounds;
			containerTest.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImage(containerTest, "image-test.tga");

			Assert.IsTrue(containerTest.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerTest.BackBuffer == controlImage, "The control should contain the same image after being scaled away and back to the same size.");
		}

		[Test]
		public void LeftToRightTests()
		{
			LeftToRightTest(new BorderDouble(), new BorderDouble());
			LeftToRightTest(new BorderDouble(), new BorderDouble(3));
			LeftToRightTest(new BorderDouble(2), new BorderDouble(0));
			LeftToRightTest(new BorderDouble(2), new BorderDouble(3));
			LeftToRightTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private void LeftToRightTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(300, 200);
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("buttonLeft");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left, controlButton1.OriginRelativeParent.y);
			containerControl.AddChild(controlButton1);
			Button controlButton2 = new Button("buttonRight");
			controlButton2.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Width + buttonMargin.Left + controlButton1.Width, controlButton2.OriginRelativeParent.y);
			containerControl.AddChild(controlButton2);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(300, 200);
			FlowLayoutWidget flowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			flowLayout.AnchorAll();
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			Button testButton1 = new Button("buttonLeft");
			testButton1.Margin = buttonMargin;
			flowLayout.AddChild(testButton1);

			Button testButton2 = new Button("buttonRight");
			testButton2.Margin = buttonMargin;
			flowLayout.AddChild(testButton2);

			containerTest.AddChild(flowLayout);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			Assert.IsTrue(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void RightToLeftTests()
		{
			RightToLeftTest(new BorderDouble(), new BorderDouble());
			RightToLeftTest(new BorderDouble(), new BorderDouble(3));
			RightToLeftTest(new BorderDouble(2), new BorderDouble(0));
			RightToLeftTest(new BorderDouble(2), new BorderDouble(3));
			RightToLeftTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private void RightToLeftTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(300, 200);
			containerControl.DoubleBuffer = true;
			Button controlButtonRight = new Button("buttonRight");
			controlButtonRight.OriginRelativeParent = new VectorMath.Vector2(containerControl.Width - controlPadding.Right - buttonMargin.Right - controlButtonRight.Width, controlButtonRight.OriginRelativeParent.y);
			containerControl.AddChild(controlButtonRight);
			Button controlButtonLeft = new Button("buttonLeft");
			controlButtonLeft.OriginRelativeParent = new VectorMath.Vector2(controlButtonRight.BoundsRelativeToParent.Left - buttonMargin.Width - controlButtonLeft.Width, controlButtonLeft.OriginRelativeParent.y);
			containerControl.AddChild(controlButtonLeft);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(300, 200);
			FlowLayoutWidget flowLayout = new FlowLayoutWidget(FlowDirection.RightToLeft);
			flowLayout.AnchorAll();
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			Button testButton2 = new Button("buttonRight");
			testButton2.Margin = buttonMargin;
			flowLayout.AddChild(testButton2);

			Button testButton1 = new Button("buttonLeft");
			testButton1.Margin = buttonMargin;
			flowLayout.AddChild(testButton1);

			containerTest.AddChild(flowLayout);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			Assert.IsTrue(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			Assert.IsTrue(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void NestedFitToChildrenParentWidth()
		{
			// child of flow layout is ParentLeftRight
			{
				//  _________________________________________
				//  |            containerControl            |
				//  | _____________________________________  |
				//  | |    Max_FitToChildren_ParentWidth   | |
				//  | | ________________________ ________  | |
				//  | | |                      | |       | | |
				//  | | |    ParentLeftRight   | | 10x10 | | |
				//  | | |______________________| |_______| | |
				//  | |____________________________________| |
				//  |________________________________________|
				//

				GuiWidget containerControl = new GuiWidget(300, 200); // containerControl = 0, 0, 300, 200
				containerControl.DoubleBuffer = true;
				FlowLayoutWidget flowWidget = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Max_FitToChildren_ParentWidth,
				};
				containerControl.AddChild(flowWidget); // flowWidget = 0, 0, 300, 0
				GuiWidget fitToChildrenOrParent = new GuiWidget(20, 20)
				{
					HAnchor = HAnchor.ParentLeftRight,
				};
				flowWidget.AddChild(fitToChildrenOrParent); // flowWidget = 0, 0, 300, 20  fitToChildrenOrParent = 0, 0, 300, 20
				GuiWidget fixed10x10 = new GuiWidget(10, 10);
				flowWidget.AddChild(fixed10x10); // flowWidget = 0, 0, 300, 20  fitToChildrenOrParent = 0, 0, 290, 20
				containerControl.OnDraw(containerControl.NewGraphics2D());

				//OutputImage(containerControl, "countainer");

				Assert.IsTrue(flowWidget.Width == containerControl.Width);
				Assert.IsTrue(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 350;
				Assert.IsTrue(flowWidget.Width == containerControl.Width);
				Assert.IsTrue(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 310;
				Assert.IsTrue(flowWidget.Width == containerControl.Width);
				Assert.IsTrue(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);
			}

			// child of flow layout is Max_FitToChildren_ParentWidth
			{
				//  ___________________________________________________
				//  |            containerControl                      |
				//  | _______________________________________________  |
				//  | |    Max_FitToChildren_ParentWidth             | |
				//  | | _________________________________   _______  | |
				//  | | |                                | |       | | |
				//  | | | Max_FitToChildren_ParentWidth  | | 10x10 | | |
				//  | | |________________________________| |_______| | |
				//  | |______________________________________________| |
				//  |__________________________________________________|
				//

				GuiWidget containerControl = new GuiWidget(300, 200); // containerControl = 0, 0, 300, 200
				containerControl.DoubleBuffer = true;
				FlowLayoutWidget flowWidget = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Max_FitToChildren_ParentWidth,
				};
				containerControl.AddChild(flowWidget);
				GuiWidget fitToChildrenOrParent = new GuiWidget(20, 20)
				{
					Name = "fitToChildrenOrParent",
					HAnchor = HAnchor.Max_FitToChildren_ParentWidth,
				};
				flowWidget.AddChild(fitToChildrenOrParent); // flowWidget = 0, 0, 300, 20  fitToChildrenOrParent = 0, 0, 300, 20
				GuiWidget fixed10x10 = new GuiWidget(10, 10);
				flowWidget.AddChild(fixed10x10); // flowWidget = 0, 0, 300, 20  fitToChildrenOrParent = 0, 0, 290, 20
				containerControl.OnDraw(containerControl.NewGraphics2D());

				//OutputImage(containerControl, "countainer");

				Assert.IsTrue(flowWidget.Width == containerControl.Width);
				Assert.IsTrue(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 350;
				Assert.IsTrue(flowWidget.Width == containerControl.Width);
				Assert.IsTrue(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);

				containerControl.Width = 310;
				Assert.IsTrue(flowWidget.Width == containerControl.Width);
				Assert.IsTrue(fitToChildrenOrParent.Width + fixed10x10.Width == containerControl.Width);
			}
		}

		[Test]
		public void LeftToRightAnchorLeftBottomTests()
		{
			LeftToRightAnchorLeftBottomTest(new BorderDouble(), new BorderDouble());
			LeftToRightAnchorLeftBottomTest(new BorderDouble(), new BorderDouble(3));
			LeftToRightAnchorLeftBottomTest(new BorderDouble(2), new BorderDouble(0));
			LeftToRightAnchorLeftBottomTest(new BorderDouble(2), new BorderDouble(3));
			LeftToRightAnchorLeftBottomTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private void LeftToRightAnchorLeftBottomTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(300, 200);
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("buttonLeft");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left, controlButton1.OriginRelativeParent.y + controlPadding.Bottom + buttonMargin.Bottom);
			containerControl.AddChild(controlButton1);
			Button controlButton2 = new Button("buttonRight");
			controlButton2.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Width + buttonMargin.Left + controlButton1.Width, controlButton2.OriginRelativeParent.y + controlPadding.Bottom + buttonMargin.Bottom);
			containerControl.AddChild(controlButton2);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(300, 200);
			FlowLayoutWidget flowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			flowLayout.AnchorAll();
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			Button testButton1 = new Button("buttonLeft");
			testButton1.VAnchor = VAnchor.ParentBottom;
			testButton1.Margin = buttonMargin;
			flowLayout.AddChild(testButton1);

			Button testButton2 = new Button("buttonRight");
			testButton2.VAnchor = VAnchor.ParentBottom;
			testButton2.Margin = buttonMargin;
			flowLayout.AddChild(testButton2);

			containerTest.AddChild(flowLayout);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			Assert.IsTrue(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void AnchorLeftRightTests()
		{
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(), new BorderDouble());
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(), new BorderDouble(3));
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(2), new BorderDouble(0));
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(2), new BorderDouble(3));
			FlowTopBottomAnchorChildrenLeftRightTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void FlowTopBottomAnchorChildrenLeftRightTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(300, 500);
			containerControl.DoubleBuffer = true;
			Button controlButtonWide = new Button("Button Wide Text");
			containerControl.AddChild(controlButtonWide);
			Button controlButton1 = new Button("button1");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left + controlButton1.OriginRelativeParent.x, controlPadding.Bottom + buttonMargin.Bottom);
			controlButtonWide.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left + controlButtonWide.OriginRelativeParent.x, controlButton1.BoundsRelativeToParent.Top + buttonMargin.Height);
			controlButton1.LocalBounds = controlButtonWide.LocalBounds;
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(300, 500);
			FlowLayoutWidget flowLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
			flowLayout.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			Button testButtonWide = new Button("Button Wide Text");
			testButtonWide.HAnchor = HAnchor.ParentLeft;
			testButtonWide.Margin = buttonMargin;
			flowLayout.AddChild(testButtonWide);

			double correctHeightOfFlowLayout = testButtonWide.Height + flowLayout.Padding.Height + testButtonWide.Margin.Height;
			Assert.AreEqual(flowLayout.Height, correctHeightOfFlowLayout, .001);

			Button testButton1 = new Button("button1");
			testButton1.Margin = buttonMargin;
			testButton1.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
			flowLayout.AddChild(testButton1);

			correctHeightOfFlowLayout += testButton1.Height + testButton1.Margin.Height;
			Assert.AreEqual(flowLayout.Height, correctHeightOfFlowLayout, .001);

			flowLayout.HAnchor = HAnchor.ParentLeft;
			flowLayout.VAnchor = VAnchor.ParentBottom;
			containerTest.AddChild(flowLayout);

			Vector2 controlButton1Pos = controlButton1.OriginRelativeParent;
			Vector2 testButton1Pos = testButton1.TransformToScreenSpace(Vector2.Zero);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");

			// make sure it can resize without breaking
			RectangleDouble bounds = containerTest.LocalBounds;
			RectangleDouble newBounds = bounds;
			newBounds.Right += 10;
			containerTest.LocalBounds = newBounds;
			Assert.IsTrue(containerControl.BackBuffer != containerTest.BackBuffer, "The Anchored widget should not be the same size.");
			containerTest.LocalBounds = bounds;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);
			Assert.IsTrue(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void NestedFlowWidgetsTopToBottomTests()
		{
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(), new BorderDouble());
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(), new BorderDouble(3));
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(2), new BorderDouble(0));
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(2), new BorderDouble(3));
			NestedFlowWidgetsTopToBottomTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedFlowWidgetsTopToBottomTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(300, 500);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			{
				Button buttonTop = new Button("buttonTop");
				Button buttonBottom = new Button("buttonBottom");
				buttonTop.OriginRelativeParent = new VectorMath.Vector2(buttonTop.OriginRelativeParent.x, containerControl.LocalBounds.Top - buttonMargin.Top - controlPadding.Top - buttonTop.Height);
				buttonBottom.OriginRelativeParent = new VectorMath.Vector2(buttonBottom.OriginRelativeParent.x, buttonTop.BoundsRelativeToParent.Bottom - buttonBottom.Height - buttonMargin.Height);
				containerControl.AddChild(buttonTop);
				containerControl.AddChild(buttonBottom);
				containerControl.OnDraw(containerControl.NewGraphics2D());
			}

			GuiWidget containerTest = new GuiWidget(300, 500);
			containerTest.DoubleBuffer = true;
			{
				FlowLayoutWidget topToBottomFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
				topToBottomFlowLayoutAll.AnchorAll();
				topToBottomFlowLayoutAll.Padding = controlPadding;
				{
					FlowLayoutWidget topToBottomFlowLayoutTop = new FlowLayoutWidget(FlowDirection.TopToBottom);
					Button buttonTop = new Button("buttonTop");
					buttonTop.Margin = buttonMargin;
					topToBottomFlowLayoutTop.AddChild(buttonTop);
					topToBottomFlowLayoutTop.SetBoundsToEncloseChildren();
					topToBottomFlowLayoutAll.AddChild(topToBottomFlowLayoutTop);
				}

				{
					FlowLayoutWidget topToBottomFlowLayoutBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
					Button buttonBottom = new Button("buttonBottom");
					buttonBottom.Margin = buttonMargin;
					topToBottomFlowLayoutBottom.AddChild(buttonBottom);
					topToBottomFlowLayoutBottom.SetBoundsToEncloseChildren();
					topToBottomFlowLayoutAll.AddChild(topToBottomFlowLayoutBottom);
				}

				containerTest.AddChild(topToBottomFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void NestedFlowWidgetsRightToLeftTests()
		{
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(), new BorderDouble());
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(), new BorderDouble(3));
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(2), new BorderDouble(0));
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(2), new BorderDouble(3));
			NestedFlowWidgetsRightToLeftTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedFlowWidgetsRightToLeftTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(500, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			{
				Button buttonRight = new Button("buttonRight");
				Button buttonLeft = new Button("buttonLeft");
				buttonRight.OriginRelativeParent = new VectorMath.Vector2(containerControl.LocalBounds.Right - controlPadding.Right - buttonMargin.Right - buttonRight.Width, buttonRight.OriginRelativeParent.y + controlPadding.Bottom + buttonMargin.Bottom);
				buttonLeft.OriginRelativeParent = new VectorMath.Vector2(buttonRight.BoundsRelativeToParent.Left - buttonMargin.Width - buttonLeft.Width, buttonLeft.OriginRelativeParent.y + controlPadding.Bottom + buttonMargin.Bottom);
				containerControl.AddChild(buttonRight);
				containerControl.AddChild(buttonLeft);
				containerControl.OnDraw(containerControl.NewGraphics2D());
			}

			GuiWidget containerTest = new GuiWidget(500, 300);
			containerTest.DoubleBuffer = true;
			{
				FlowLayoutWidget rightToLeftFlowLayoutAll = new FlowLayoutWidget(FlowDirection.RightToLeft);
				rightToLeftFlowLayoutAll.AnchorAll();
				rightToLeftFlowLayoutAll.Padding = controlPadding;
				{
					FlowLayoutWidget rightToLeftFlowLayoutRight = new FlowLayoutWidget(FlowDirection.RightToLeft);
					Button buttonRight = new Button("buttonRight");
					buttonRight.Margin = buttonMargin;
					rightToLeftFlowLayoutRight.AddChild(buttonRight);
					rightToLeftFlowLayoutRight.SetBoundsToEncloseChildren();
					rightToLeftFlowLayoutRight.VAnchor = VAnchor.ParentBottom;
					rightToLeftFlowLayoutAll.AddChild(rightToLeftFlowLayoutRight);
				}

				{
					FlowLayoutWidget rightToLeftFlowLayoutLeft = new FlowLayoutWidget(FlowDirection.RightToLeft);
					Button buttonLeft = new Button("buttonLeft");
					buttonLeft.Margin = buttonMargin;
					rightToLeftFlowLayoutLeft.AddChild(buttonLeft);
					rightToLeftFlowLayoutLeft.SetBoundsToEncloseChildren();
					rightToLeftFlowLayoutLeft.VAnchor = VAnchor.ParentBottom;
					rightToLeftFlowLayoutAll.AddChild(rightToLeftFlowLayoutLeft);
				}

				containerTest.AddChild(rightToLeftFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			Assert.IsTrue(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 50), "The test and control need to match.");
			Assert.IsTrue(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void NestedFlowWidgetsLeftToRightTests()
		{
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(), new BorderDouble());
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(), new BorderDouble(3));
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(2), new BorderDouble(0));
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(2), new BorderDouble(3));
			NestedFlowWidgetsLeftToRightTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void NestedFlowWidgetsLeftToRightTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(500, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			{
				Button buttonRight = new Button("buttonRight");
				Button buttonLeft = new Button("buttonLeft");
				buttonLeft.OriginRelativeParent = new VectorMath.Vector2(controlPadding.Left + buttonMargin.Left, buttonLeft.OriginRelativeParent.y);
				buttonRight.OriginRelativeParent = new VectorMath.Vector2(buttonLeft.BoundsRelativeToParent.Right + buttonMargin.Width, buttonRight.OriginRelativeParent.y);
				containerControl.AddChild(buttonRight);
				containerControl.AddChild(buttonLeft);
				containerControl.OnDraw(containerControl.NewGraphics2D());
			}

			GuiWidget containerTest = new GuiWidget(500, 300);
			containerTest.DoubleBuffer = true;
			{
				FlowLayoutWidget leftToRightFlowLayoutAll = new FlowLayoutWidget(FlowDirection.LeftToRight);
				leftToRightFlowLayoutAll.AnchorAll();
				leftToRightFlowLayoutAll.Padding = controlPadding;
				{
					FlowLayoutWidget leftToRightFlowLayoutLeft = new FlowLayoutWidget(FlowDirection.LeftToRight);
					Button buttonTop = new Button("buttonLeft");
					buttonTop.Margin = buttonMargin;
					leftToRightFlowLayoutLeft.AddChild(buttonTop);
					leftToRightFlowLayoutLeft.SetBoundsToEncloseChildren();
					leftToRightFlowLayoutAll.AddChild(leftToRightFlowLayoutLeft);
				}

				{
					FlowLayoutWidget leftToRightFlowLayoutRight = new FlowLayoutWidget(FlowDirection.LeftToRight);
					Button buttonBottom = new Button("buttonRight");
					buttonBottom.Margin = buttonMargin;
					leftToRightFlowLayoutRight.AddChild(buttonBottom);
					leftToRightFlowLayoutRight.SetBoundsToEncloseChildren();
					leftToRightFlowLayoutAll.AddChild(leftToRightFlowLayoutRight);
				}

				containerTest.AddChild(leftToRightFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void LeftRightWithAnchorLeftRightChildTests()
		{
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(), new BorderDouble());
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(), new BorderDouble(3));
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(0));
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(3));
			LeftRightWithAnchorLeftRightChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void LeftRightWithAnchorLeftRightChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			GuiWidget containerControl = new GuiWidget(buttonSize * 8, buttonSize * 3);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			RectangleDouble[] eightControlRectangles = new RectangleDouble[8];
			RGBA_Bytes[] eightColors = new RGBA_Bytes[] { RGBA_Bytes.Red, RGBA_Bytes.Orange, RGBA_Bytes.Yellow, RGBA_Bytes.YellowGreen, RGBA_Bytes.Green, RGBA_Bytes.Blue, RGBA_Bytes.Indigo, RGBA_Bytes.Violet };
			{
				double currentleft = controlPadding.Left + buttonMargin.Left;
				double buttonHeightWithMargin = buttonSize + buttonMargin.Height;
				double scaledWidth = (containerControl.Width - controlPadding.Width - buttonMargin.Width * 8 - buttonSize * 2) / 6;
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

				// bottom center anchor
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[4] = new RectangleDouble(currentleft, bottomAnchorY, currentleft + scaledWidth, centerYOfContainer - buttonMargin.Top);

				// center top anchor
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[5] = new RectangleDouble(currentleft, centerYOfContainer + buttonMargin.Bottom, currentleft + scaledWidth, topAnchorY);

				// bottom top anchor
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[6] = new RectangleDouble(currentleft, bottomAnchorY, currentleft + scaledWidth, topAnchorY);

				// right anchor
				currentleft += scaledWidth + buttonMargin.Width;
				eightControlRectangles[7] = new RectangleDouble(currentleft, 0, currentleft + buttonSize, buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 8; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], eightColors[i]);
				}
			}

			GuiWidget containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			FlowLayoutWidget leftToRightFlowLayoutAll = new FlowLayoutWidget(FlowDirection.LeftToRight);
			containerTest.DoubleBuffer = true;
			{
				leftToRightFlowLayoutAll.AnchorAll();
				leftToRightFlowLayoutAll.Padding = controlPadding;
				{
					GuiWidget left = new GuiWidget(buttonSize, buttonSize);
					left.BackgroundColor = RGBA_Bytes.Red;
					left.Margin = buttonMargin;
					leftToRightFlowLayoutAll.AddChild(left);

					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentBottom, RGBA_Bytes.Orange));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentCenter, RGBA_Bytes.Yellow));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentTop, RGBA_Bytes.YellowGreen));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentBottomCenter, RGBA_Bytes.Green));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentCenterTop, RGBA_Bytes.Blue));
					leftToRightFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentBottomTop, RGBA_Bytes.Indigo));

					GuiWidget right = new GuiWidget(buttonSize, buttonSize);
					right.BackgroundColor = RGBA_Bytes.Violet;
					right.Margin = buttonMargin;
					leftToRightFlowLayoutAll.AddChild(right);
				}

				containerTest.AddChild(leftToRightFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			for (int i = 0; i < 8; i++)
			{
				Assert.IsTrue(eightControlRectangles[i] == leftToRightFlowLayoutAll.Children[i].BoundsRelativeToParent);
			}

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			Assert.IsTrue(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		private GuiWidget CreateLeftToRightMiddleWidget(BorderDouble buttonMargin, double buttonSize, VAnchor vAnchor, RGBA_Bytes color)
		{
			GuiWidget middle = new GuiWidget(buttonSize / 2, buttonSize);
			middle.Margin = buttonMargin;
			middle.HAnchor = HAnchor.ParentLeftRight;
			middle.VAnchor = vAnchor;
			middle.BackgroundColor = color;
			return middle;
		}

		[Test]
		public void RightLeftWithAnchorLeftRightChildTests()
		{
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(), new BorderDouble());
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(), new BorderDouble(3));
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(0));
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(2), new BorderDouble(3));
			RightLeftWithAnchorLeftRightChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void RightLeftWithAnchorLeftRightChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			GuiWidget containerControl = new GuiWidget(buttonSize * 8, buttonSize * 3);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			RectangleDouble[] eightControlRectangles = new RectangleDouble[8];
			RGBA_Bytes[] eightColors = new RGBA_Bytes[] { RGBA_Bytes.Red, RGBA_Bytes.Orange, RGBA_Bytes.Yellow, RGBA_Bytes.YellowGreen, RGBA_Bytes.Green, RGBA_Bytes.Blue, RGBA_Bytes.Indigo, RGBA_Bytes.Violet };
			{
				double currentLeft = containerControl.Width - controlPadding.Right - buttonMargin.Right - buttonSize;
				double buttonHeightWithMargin = buttonSize + buttonMargin.Height;
				double scaledWidth = (containerControl.Width - controlPadding.Width - buttonMargin.Width * 8 - buttonSize * 2) / 6;
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

				// bottom center anchor
				currentLeft -= scaledWidth + buttonMargin.Width;
				eightControlRectangles[4] = new RectangleDouble(currentLeft, bottomAnchorY, currentLeft + scaledWidth, centerYOfContainer - buttonMargin.Top);

				// center top anchor
				currentLeft -= scaledWidth + buttonMargin.Width;
				eightControlRectangles[5] = new RectangleDouble(currentLeft, centerYOfContainer + buttonMargin.Bottom, currentLeft + scaledWidth, topAnchorY);

				// bottom top anchor
				currentLeft -= scaledWidth + buttonMargin.Width;
				eightControlRectangles[6] = new RectangleDouble(currentLeft, bottomAnchorY, currentLeft + scaledWidth, topAnchorY);

				// right anchor
				currentLeft -= buttonSize + buttonMargin.Width;
				eightControlRectangles[7] = new RectangleDouble(currentLeft, 0, currentLeft + buttonSize, buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 8; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], eightColors[i]);
				}
			}

			GuiWidget containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			FlowLayoutWidget rightToLeftFlowLayoutAll = new FlowLayoutWidget(FlowDirection.RightToLeft);
			containerTest.DoubleBuffer = true;
			{
				rightToLeftFlowLayoutAll.AnchorAll();
				rightToLeftFlowLayoutAll.Padding = controlPadding;
				{
					GuiWidget left = new GuiWidget(buttonSize, buttonSize);
					left.BackgroundColor = RGBA_Bytes.Red;
					left.Margin = buttonMargin;
					rightToLeftFlowLayoutAll.AddChild(left);

					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentBottom, RGBA_Bytes.Orange));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentCenter, RGBA_Bytes.Yellow));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentTop, RGBA_Bytes.YellowGreen));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentBottomCenter, RGBA_Bytes.Green));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentCenterTop, RGBA_Bytes.Blue));
					rightToLeftFlowLayoutAll.AddChild(CreateLeftToRightMiddleWidget(buttonMargin, buttonSize, VAnchor.ParentBottomTop, RGBA_Bytes.Indigo));

					GuiWidget right = new GuiWidget(buttonSize, buttonSize);
					right.BackgroundColor = RGBA_Bytes.Violet;
					right.Margin = buttonMargin;
					rightToLeftFlowLayoutAll.AddChild(right);
				}

				containerTest.AddChild(rightToLeftFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			for (int i = 0; i < 8; i++)
			{
				Assert.IsTrue(eightControlRectangles[i].Equals(rightToLeftFlowLayoutAll.Children[i].BoundsRelativeToParent, .001));
			}

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			Assert.IsTrue(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		[Test]
		public void BottomTopWithAnchorBottomTopChildTests()
		{
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(), new BorderDouble());
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(), new BorderDouble(3));
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(0));
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(3));
			BottomTopWithAnchorBottomTopChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void BottomTopWithAnchorBottomTopChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			GuiWidget containerControl = new GuiWidget(buttonSize * 3, buttonSize * 8);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			RectangleDouble[] eightControlRectangles = new RectangleDouble[8];
			RGBA_Bytes[] eightColors = new RGBA_Bytes[] { RGBA_Bytes.Red, RGBA_Bytes.Orange, RGBA_Bytes.Yellow, RGBA_Bytes.YellowGreen, RGBA_Bytes.Green, RGBA_Bytes.Blue, RGBA_Bytes.Indigo, RGBA_Bytes.Violet };
			{
				double currentBottom = controlPadding.Bottom + buttonMargin.Bottom;
				double buttonWidthWithMargin = buttonSize + buttonMargin.Width;
				double scaledHeight = (containerControl.Height - controlPadding.Height - buttonMargin.Height * 8 - buttonSize * 2) / 6;
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

				// left center anchor
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[4] = new RectangleDouble(leftAnchorX, currentBottom, centerXOfContainer - buttonMargin.Right, currentBottom + scaledHeight);

				// center right anchor
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[5] = new RectangleDouble(centerXOfContainer + buttonMargin.Left, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// left right anchor
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[6] = new RectangleDouble(leftAnchorX, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// top anchor
				currentBottom += scaledHeight + buttonMargin.Height;
				eightControlRectangles[7] = new RectangleDouble(0, currentBottom, buttonSize, currentBottom + buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 8; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], eightColors[i]);
				}
			}

			GuiWidget containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			FlowLayoutWidget bottomToTopFlowLayoutAll = new FlowLayoutWidget(FlowDirection.BottomToTop);
			containerTest.DoubleBuffer = true;
			{
				bottomToTopFlowLayoutAll.AnchorAll();
				bottomToTopFlowLayoutAll.Padding = controlPadding;
				{
					GuiWidget bottom = new GuiWidget(buttonSize, buttonSize);
					bottom.BackgroundColor = RGBA_Bytes.Red;
					bottom.Margin = buttonMargin;
					bottomToTopFlowLayoutAll.AddChild(bottom);

					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentLeft, RGBA_Bytes.Orange));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentCenter, RGBA_Bytes.Yellow));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentRight, RGBA_Bytes.YellowGreen));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentLeftCenter, RGBA_Bytes.Green));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentCenterRight, RGBA_Bytes.Blue));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentLeftRight, RGBA_Bytes.Indigo));

					GuiWidget top = new GuiWidget(buttonSize, buttonSize);
					top.BackgroundColor = RGBA_Bytes.Violet;
					top.Margin = buttonMargin;
					bottomToTopFlowLayoutAll.AddChild(top);
				}

				containerTest.AddChild(bottomToTopFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			for (int i = 0; i < 8; i++)
			{
				Assert.IsTrue(eightControlRectangles[i] == bottomToTopFlowLayoutAll.Children[i].BoundsRelativeToParent);
			}

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			Assert.IsTrue(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		private GuiWidget CreateBottomToTopMiddleWidget(BorderDouble buttonMargin, double buttonSize, HAnchor hAnchor, RGBA_Bytes color)
		{
			GuiWidget middle = new GuiWidget(buttonSize, buttonSize / 2);
			middle.Margin = buttonMargin;
			middle.VAnchor = VAnchor.ParentBottomTop;
			middle.HAnchor = hAnchor;
			middle.BackgroundColor = color;
			return middle;
		}

		[Test]
		public void TopBottomWithAnchorBottomTopChildTests()
		{
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(), new BorderDouble());
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(), new BorderDouble(3));
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(0));
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(2), new BorderDouble(3));
			TopBottomWithAnchorBottomTopChildTest(new BorderDouble(2, 4, 6, 8), new BorderDouble(1, 3, 5, 7));
		}

		public void TopBottomWithAnchorBottomTopChildTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			double buttonSize = 40;
			GuiWidget containerControl = new GuiWidget(buttonSize * 3, buttonSize * 8);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;

			RectangleDouble[] eightControlRectangles = new RectangleDouble[8];
			RGBA_Bytes[] eightColors = new RGBA_Bytes[] { RGBA_Bytes.Red, RGBA_Bytes.Orange, RGBA_Bytes.Yellow, RGBA_Bytes.YellowGreen, RGBA_Bytes.Green, RGBA_Bytes.Blue, RGBA_Bytes.Indigo, RGBA_Bytes.Violet };
			{
				double currentBottom = containerControl.Height - controlPadding.Top - buttonMargin.Top - buttonSize;
				double buttonWidthWithMargin = buttonSize + buttonMargin.Width;
				double scaledHeight = (containerControl.Height - controlPadding.Height - buttonMargin.Height * 8 - buttonSize * 2) / 6;
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

				// left center anchor
				currentBottom -= scaledHeight + buttonMargin.Height;
				eightControlRectangles[4] = new RectangleDouble(leftAnchorX, currentBottom, centerXOfContainer - buttonMargin.Right, currentBottom + scaledHeight);

				// center right anchor
				currentBottom -= scaledHeight + buttonMargin.Height;
				eightControlRectangles[5] = new RectangleDouble(centerXOfContainer + buttonMargin.Left, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// left right anchor
				currentBottom -= scaledHeight + buttonMargin.Height;
				eightControlRectangles[6] = new RectangleDouble(leftAnchorX, currentBottom, rightAnchorX, currentBottom + scaledHeight);

				// top anchor
				currentBottom -= buttonSize + buttonMargin.Height;
				eightControlRectangles[7] = new RectangleDouble(0, currentBottom, buttonSize, currentBottom + buttonSize);

				Graphics2D graphics = containerControl.NewGraphics2D();
				for (int i = 0; i < 8; i++)
				{
					graphics.FillRectangle(eightControlRectangles[i], eightColors[i]);
				}
			}

			GuiWidget containerTest = new GuiWidget(containerControl.Width, containerControl.Height);
			FlowLayoutWidget bottomToTopFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
			containerTest.DoubleBuffer = true;
			{
				bottomToTopFlowLayoutAll.AnchorAll();
				bottomToTopFlowLayoutAll.Padding = controlPadding;
				{
					GuiWidget top = new GuiWidget(buttonSize, buttonSize);
					top.BackgroundColor = RGBA_Bytes.Red;
					top.Margin = buttonMargin;
					bottomToTopFlowLayoutAll.AddChild(top);

					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentLeft, RGBA_Bytes.Orange));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentCenter, RGBA_Bytes.Yellow));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentRight, RGBA_Bytes.YellowGreen));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentLeftCenter, RGBA_Bytes.Green));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentCenterRight, RGBA_Bytes.Blue));
					bottomToTopFlowLayoutAll.AddChild(CreateBottomToTopMiddleWidget(buttonMargin, buttonSize, HAnchor.ParentLeftRight, RGBA_Bytes.Indigo));

					GuiWidget bottom = new GuiWidget(buttonSize, buttonSize);
					bottom.BackgroundColor = RGBA_Bytes.Violet;
					bottom.Margin = buttonMargin;
					bottomToTopFlowLayoutAll.AddChild(bottom);
				}

				containerTest.AddChild(bottomToTopFlowLayoutAll);
			}

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			for (int i = 0; i < 8; i++)
			{
				Assert.IsTrue(eightControlRectangles[i].Equals(bottomToTopFlowLayoutAll.Children[i].BoundsRelativeToParent, .001));
			}

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");

			// we use a least squares match because the erase background that is setting the widgets is integer pixel based and the fill rectangle is not.
			Assert.IsTrue(containerControl.BackBuffer.FindLeastSquaresMatch(containerTest.BackBuffer, 0), "The test and control need to match.");
		}

		public void EnsureFlowLayoutMinSizeFitsChildrenMinSize()
		{
			// This test is to prove that a flow layout widget always has it's min size set
			// to the enclosing bounds size of all it's childrens min size.
			// The code to be tested will expand the flow layouts min size as it's children's min size change.
			GuiWidget containerTest = new GuiWidget(640, 480);
			FlowLayoutWidget topToBottomFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
			containerTest.AddChild(topToBottomFlowLayoutAll);
			containerTest.DoubleBuffer = true;

			FlowLayoutWidget topLeftToRight = new FlowLayoutWidget(FlowDirection.LeftToRight);
			topToBottomFlowLayoutAll.AddChild(topLeftToRight);
			GuiWidget bottomLeftToRight = new FlowLayoutWidget(FlowDirection.LeftToRight);
			topToBottomFlowLayoutAll.AddChild(bottomLeftToRight);

			topLeftToRight.AddChild(new Button("top button"));

			FlowLayoutWidget bottomContentTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			bottomLeftToRight.AddChild(bottomContentTopToBottom);

			Button button1 = new Button("button1");
			Assert.IsTrue(button1.MinimumSize.x > 0, "Buttons should set their min size on construction.");
			bottomContentTopToBottom.AddChild(button1);
			//Assert.IsTrue(bottomContentTopToBottom.MinimumSize.x >= button1.MinimumSize.x, "There should be space for the button.");
			bottomContentTopToBottom.AddChild(new Button("button2"));
			Button wideButton = new Button("button3 Wide");
			bottomContentTopToBottom.AddChild(wideButton);
			//Assert.IsTrue(bottomContentTopToBottom.MinimumSize.x >= wideButton.MinimumSize.x, "These should be space for the button.");

			containerTest.BackgroundColor = RGBA_Bytes.White;
			containerTest.OnDrawBackground(containerTest.NewGraphics2D());
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImage(containerTest.BackBuffer, "zFlowLaoutsGetMinSize.tga");

			Assert.IsTrue(bottomLeftToRight.Width > 0, "This needs to have been expanded when the bottomContentTopToBottom grew.");
			Assert.IsTrue(bottomLeftToRight.MinimumSize.x >= bottomContentTopToBottom.MinimumSize.x, "These should be space for the next flowLayout.");
			Assert.IsTrue(containerTest.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
		}

		[Test]
		public void ChildVisibilityChangeCauseResize()
		{
			//Test whether toggling the visibility of children changes the flow layout
			GuiWidget containerTest = new GuiWidget(640, 480);
			FlowLayoutWidget topToBottomFlowLayoutAll = new FlowLayoutWidget(FlowDirection.TopToBottom);
			containerTest.AddChild(topToBottomFlowLayoutAll);

			GuiWidget item1 = new GuiWidget(1, 20);
			GuiWidget item2 = new GuiWidget(1, 30);
			GuiWidget item3 = new GuiWidget(1, 40);

			topToBottomFlowLayoutAll.AddChild(item1);
			Assert.IsTrue(topToBottomFlowLayoutAll.Height == 20);
			topToBottomFlowLayoutAll.AddChild(item2);
			Assert.IsTrue(topToBottomFlowLayoutAll.Height == 50);
			topToBottomFlowLayoutAll.AddChild(item3);
			Assert.IsTrue(topToBottomFlowLayoutAll.Height == 90);

			item2.Visible = false;

			Assert.IsTrue(topToBottomFlowLayoutAll.Height == 60);
		}

		internal void EnsureCorrectMinimumSize()
		{
			{
				GuiWidget containerTest = new GuiWidget(640, 480);
				containerTest.DoubleBuffer = true;
				FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
				containerTest.AddChild(leftToRightLayout);

				GuiWidget item1 = new GuiWidget(10, 11);
				GuiWidget item2 = new GuiWidget(20, 22);
				GuiWidget item3 = new GuiWidget(30, 33);
				item3.HAnchor = HAnchor.ParentLeftRight;

				leftToRightLayout.AddChild(item1);
				leftToRightLayout.AddChild(item2);
				leftToRightLayout.AddChild(item3);

				leftToRightLayout.AnchorAll();
				containerTest.OnDraw(containerTest.NewGraphics2D());
				Assert.IsTrue(leftToRightLayout.Width == 640);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.x == 60);
				Assert.IsTrue(leftToRightLayout.Height == 480);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.y == 33);
				Assert.IsTrue(item3.Width == 610);

				containerTest.OnDraw(containerTest.NewGraphics2D());
				Assert.IsTrue(leftToRightLayout.Width == 640);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.x == 60);
				Assert.IsTrue(leftToRightLayout.Height == 480);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.y == 33);
				Assert.IsTrue(item3.Width == 610);

				containerTest.Width = 650;
				containerTest.OnDraw(containerTest.NewGraphics2D());
				Assert.IsTrue(leftToRightLayout.Width == 650);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.x == 60);
				Assert.IsTrue(leftToRightLayout.Height == 480);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.y == 33);
				Assert.IsTrue(item3.Width == 620);

				containerTest.Width = 640;
				containerTest.OnDraw(containerTest.NewGraphics2D());
				Assert.IsTrue(leftToRightLayout.Width == 640);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.x == 60);
				Assert.IsTrue(leftToRightLayout.Height == 480);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.y == 33);
				Assert.IsTrue(item3.Width == 610);
			}

			{
				GuiWidget containerTest = new GuiWidget(640, 480);
				containerTest.DoubleBuffer = true;
				FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
				containerTest.AddChild(leftToRightLayout);

				GuiWidget item1 = new GuiWidget(10, 11);
				GuiWidget item2 = new GuiWidget(20, 22);
				GuiWidget item3 = new GuiWidget(30, 33);
				item3.VAnchor = VAnchor.ParentBottomTop;

				leftToRightLayout.AddChild(item1);
				leftToRightLayout.AddChild(item2);
				leftToRightLayout.AddChild(item3);

				leftToRightLayout.AnchorAll();
				containerTest.OnDraw(containerTest.NewGraphics2D());
				Assert.IsTrue(leftToRightLayout.Width == 640);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.x == 30);

				Assert.IsTrue(leftToRightLayout.Height == 480);
				//Assert.IsTrue(leftToRightLayout.MinimumSize.y == 66);
			}
		}

		internal void EnsureNestedAreMinimumSize()
		{
			GuiWidget containerTest = new GuiWidget(640, 480);
			containerTest.DoubleBuffer = true;
			FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			containerTest.AddChild(leftToRightLayout);

			GuiWidget item1 = new GuiWidget(10, 11);
			GuiWidget item2 = new GuiWidget(20, 22);
			GuiWidget item3 = new GuiWidget(30, 33);
			item3.HAnchor = HAnchor.ParentLeftRight;

			leftToRightLayout.AddChild(item1);
			leftToRightLayout.AddChild(item2);
			leftToRightLayout.AddChild(item3);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			Assert.IsTrue(leftToRightLayout.Width == 60);
			Assert.IsTrue(leftToRightLayout.MinimumSize.x == 0);
			Assert.IsTrue(leftToRightLayout.Height == 33);
			Assert.IsTrue(leftToRightLayout.MinimumSize.y == 0);
			Assert.IsTrue(item3.Width == 30);

			containerTest.Width = 650;
			containerTest.OnDraw(containerTest.NewGraphics2D());
			Assert.IsTrue(leftToRightLayout.Width == 60);
			Assert.IsTrue(leftToRightLayout.MinimumSize.x == 0);
			Assert.IsTrue(leftToRightLayout.Height == 33);
			Assert.IsTrue(leftToRightLayout.MinimumSize.y == 0);
			Assert.IsTrue(item3.Width == 30);
		}

		[Test]
		public void EnsureCorrectSizeOnChildrenVisibleChange()
		{
			// just one column changes correctly
			{
				FlowLayoutWidget testColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
				testColumn.Name = "testColumn";

				GuiWidget item1 = new GuiWidget(10, 10);
				item1.Name = "item1";
				testColumn.AddChild(item1);

				Assert.IsTrue(testColumn.Height == 10);

				GuiWidget item2 = new GuiWidget(11, 11);
				item2.Name = "item2";
				testColumn.AddChild(item2);

				Assert.IsTrue(testColumn.Height == 21);

				GuiWidget item3 = new GuiWidget(12, 12);
				item3.Name = "item3";
				testColumn.AddChild(item3);

				Assert.IsTrue(testColumn.Height == 33);

				item2.Visible = false;

				Assert.IsTrue(testColumn.Height == 22);

				item2.Visible = true;

				Assert.IsTrue(testColumn.Height == 33);
			}

			// nested columns change correctly
			{
				GuiWidget.DefaultEnforceIntegerBounds = true;
				CheckBox hideCheckBox;
				FlowLayoutWidget leftColumn;
				FlowLayoutWidget topLeftStuff;
				GuiWidget everything = new GuiWidget(500, 500);
				GuiWidget firstItem;
				GuiWidget thingToHide;
				{
					FlowLayoutWidget twoColumns = new FlowLayoutWidget();
					twoColumns.Name = "twoColumns";
					twoColumns.VAnchor = UI.VAnchor.ParentTop;

					{
						leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
						leftColumn.Name = "leftColumn";
						{
							topLeftStuff = new FlowLayoutWidget(FlowDirection.TopToBottom);
							topLeftStuff.Name = "topLeftStuff";
							firstItem = new TextWidget("Top of Top Stuff");
							topLeftStuff.AddChild(firstItem);
							thingToHide = new Button("thing to hide");
							topLeftStuff.AddChild(thingToHide);
							topLeftStuff.AddChild(new TextWidget("Bottom of Top Stuff"));

							leftColumn.AddChild(topLeftStuff);
							//leftColumn.DebugShowBounds = true;
						}

						twoColumns.AddChild(leftColumn);
					}

					{
						FlowLayoutWidget rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
						rightColumn.Name = "rightColumn";
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

					Assert.IsTrue(firstItem.OriginRelativeParent.y == 54);
					//Assert.IsTrue(firstItem.OriginRelativeParent.y - topLeftStuff.LocalBounds.Bottom == 54);
					Assert.IsTrue(twoColumns.BoundsRelativeToParent.Top == 500);
					Assert.IsTrue(leftColumn.BoundsRelativeToParent.Top == 67);
					Assert.IsTrue(leftColumn.BoundsRelativeToParent.Bottom == 0);
					Assert.IsTrue(leftColumn.OriginRelativeParent.y == 0);
					Assert.IsTrue(topLeftStuff.BoundsRelativeToParent.Top == 67);
					Assert.IsTrue(topLeftStuff.Height == 67);
					Assert.IsTrue(leftColumn.Height == 67);

					hideCheckBox.Checked = true;

					Assert.IsTrue(firstItem.OriginRelativeParent.y == 21);
					Assert.IsTrue(leftColumn.OriginRelativeParent.y == 0);
					Assert.IsTrue(leftColumn.BoundsRelativeToParent.Bottom == 0);
					Assert.IsTrue(topLeftStuff.Height == 34);
					Assert.IsTrue(leftColumn.Height == 34);
				}
				GuiWidget.DefaultEnforceIntegerBounds = false;
			}
		}

		[Test]
		public void ChildHAnchorPriority()
		{
			// make sure a middle spacer grows and shrinks correctly
			{
				FlowLayoutWidget leftRightFlowLayout = new FlowLayoutWidget();
				Assert.IsTrue(leftRightFlowLayout.HAnchor == HAnchor.FitToChildren); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.ParentLeftRight; // add to the existing flags ParentLeftRight (starts with FitToChildren)
				// [<-><->] // attempting to make a visual descrition of what is happening
				Assert.IsTrue(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't
				GuiWidget leftWidget = new GuiWidget(10, 10); // we call it left widget as it will be the first one in the left to right flow layout
				leftRightFlowLayout.AddChild(leftWidget); // add in a child with a width of 10
				// [<->(10)<->] // the flow layout should now be forced to be 10 wide
				Assert.IsTrue(leftRightFlowLayout.Width == 10);

				GuiWidget middleSpacer = new GuiWidget(0, 10); // this widget will hold the space
				middleSpacer.HAnchor = HAnchor.ParentLeftRight; // by resizing to whatever width it can be
				leftRightFlowLayout.AddChild(middleSpacer);
				// [<->(10)(<->)<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 10);
				Assert.IsTrue(middleSpacer.Width == 0);

				GuiWidget rightItem = new GuiWidget(10, 10);
				leftRightFlowLayout.AddChild(rightItem);
				// [<->(10)(<->)(10)<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 20);

				GuiWidget container = new GuiWidget(40, 20);
				container.AddChild(leftRightFlowLayout);
				// (40[<->(10)(<->)(10)<->]) // the extra 20 must be put into the expandable (<->)
				Assert.IsTrue(container.Width == 40);
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 20);

				container.Width = 50;
				// (50[<->(10)(<->)(10)<->]) // the extra 30 must be put into the expandable (<->)
				Assert.IsTrue(container.Width == 50);
				Assert.IsTrue(leftRightFlowLayout.Width == 50);
				Assert.IsTrue(middleSpacer.Width == 30);

				container.Width = 40;
				// (40[<->(10)(<->)(10)<->]) // the extra 20 must be put into the expandable (<->) by shrinking it
				Assert.IsTrue(container.Width == 40);
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 20);

				Assert.IsTrue(container.MinimumSize.x == 40); // minimum size is set to the construction size for normal GuiWidgets
				container.MinimumSize = new Vector2(0, 0); // make sure we can make this smaller
				container.Width = 10;
				// (10[<->(10)(<->)(10)<->]) // the extra 20 must be put into the expandable (<->) by shrinking it
				Assert.IsTrue(container.Width == 10); // nothing should be keeping this big
				Assert.IsTrue(leftRightFlowLayout.Width == 20); // it can't get smaller than its contents
				Assert.IsTrue(middleSpacer.Width == 0);
			}

			// make sure the middle spacer works the same when in a flow layout
			{
				FlowLayoutWidget leftRightFlowLayout = new FlowLayoutWidget();
				leftRightFlowLayout.Name = "leftRightFlowLayout";
				Assert.IsTrue(leftRightFlowLayout.HAnchor == HAnchor.FitToChildren); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.ParentLeftRight; // add to the existing flags ParentLeftRight (starts with FitToChildren)
				// [<-><->] // attempting to make a visual descrition of what is happening
				Assert.IsTrue(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't
				GuiWidget leftWidget = new GuiWidget(10, 10); // we call it left widget as it will be the first one in the left to right flow layout
				leftWidget.Name = "leftWidget";
				leftRightFlowLayout.AddChild(leftWidget); // add in a child with a width of 10
				// [<->(10)<->] // the flow layout should now be forced to be 10 wide
				Assert.IsTrue(leftRightFlowLayout.Width == 10);

				FlowLayoutWidget middleFlowLayoutWrapper = new FlowLayoutWidget(); // we are going to wrap the implicitly middle items to test nested resizing
				middleFlowLayoutWrapper.Name = "middleFlowLayoutWrapper";
				middleFlowLayoutWrapper.HAnchor |= HAnchor.ParentLeftRight;
				GuiWidget middleSpacer = new GuiWidget(0, 10); // this widget will hold the space
				middleSpacer.Name = "middleSpacer";
				middleSpacer.HAnchor = HAnchor.ParentLeftRight; // by resizing to whatever width it can be
				middleFlowLayoutWrapper.AddChild(middleSpacer);
				// {<->(<->)<->}
				leftRightFlowLayout.AddChild(middleFlowLayoutWrapper);
				// [<->(10){<->(<->)<->}<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 10);
				Assert.IsTrue(middleFlowLayoutWrapper.Width == 0);
				Assert.IsTrue(middleSpacer.Width == 0);

				GuiWidget rightWidget = new GuiWidget(10, 10);
				rightWidget.Name = "rightWidget";
				leftRightFlowLayout.AddChild(rightWidget);
				// [<->(10){<->(<->)<->}(10)<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 20);

				GuiWidget container = new GuiWidget(40, 20);
				container.Name = "container";
				container.AddChild(leftRightFlowLayout);
				// (40[<->(10){<->(<->)<->}(10)<->]) // the extra 20 must be put into the expandable (<->)
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleFlowLayoutWrapper.Width == 20);
				Assert.IsTrue(middleSpacer.Width == 20);

				container.Width = 50;
				// (50[<->(10){<->(<->)<->}(10)<->]) // the extra 30 must be put into the expandable (<->)
				Assert.IsTrue(leftRightFlowLayout.Width == 50);
				Assert.IsTrue(middleSpacer.Width == 30);

				container.Width = 40;
				// (50[<->(10){<->(<->)<->}(10)<->]) // the extra 20 must be put into the expandable (<->) by shrinking it
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 20);
			}

			// make sure a middle spacer grows and shrinks correctly when in another guiwidget (not a flow widget) that is LeftRight
			{
				FlowLayoutWidget leftRightFlowLayout = new FlowLayoutWidget();
				leftRightFlowLayout.Name = "leftRightFlowLayout";
				Assert.IsTrue(leftRightFlowLayout.HAnchor == HAnchor.FitToChildren); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.ParentLeftRight; // add to the existing flags ParentLeftRight (starts with FitToChildren)
				// [<-><->] // attempting to make a visual descrition of what is happening
				Assert.IsTrue(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't

				GuiWidget middleSpacer = new GuiWidget(0, 10); // this widget will hold the space
				middleSpacer.Name = "middleSpacer";
				middleSpacer.HAnchor = HAnchor.ParentLeftRight; // by resizing to whatever width it can be
				leftRightFlowLayout.AddChild(middleSpacer);
				// [<->(<->)<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 0);
				Assert.IsTrue(middleSpacer.Width == 0);

				Assert.IsTrue(leftRightFlowLayout.Width == 0);

				GuiWidget containerOuter = new GuiWidget(40, 20);
				containerOuter.Name = "containerOuter";
				GuiWidget containerInner = new GuiWidget(0, 20);
				containerInner.HAnchor = HAnchor.ParentLeftRight | HAnchor.FitToChildren;
				containerInner.Name = "containerInner";
				containerOuter.AddChild(containerInner);
				Assert.IsTrue(containerInner.Width == 40);
				containerInner.AddChild(leftRightFlowLayout);
				// (40(<-[<->(<->)<->]->)) // the extra 20 must be put into the expandable (<->)
				Assert.IsTrue(containerInner.Width == 40);
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 40);

				containerOuter.Width = 50;
				// (50(<-[<->(<->)<->]->) // the extra 30 must be put into the expandable (<->)
				Assert.IsTrue(containerInner.Width == 50);
				Assert.IsTrue(leftRightFlowLayout.Width == 50);
				Assert.IsTrue(middleSpacer.Width == 50);

				containerOuter.Width = 40;
				// (40(<-[<->(<->)<->]->) // the extra 20 must be put into the expandable (<->) by shrinking it
				Assert.IsTrue(containerInner.Width == 40);
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 40);
			}

			// make sure a middle spacer grows and shrinks correctly when in another guiwidget (not a flow widget) that is LeftRight
			{
				FlowLayoutWidget leftRightFlowLayout = new FlowLayoutWidget();
				Assert.IsTrue(leftRightFlowLayout.HAnchor == HAnchor.FitToChildren); // flow layout starts with FitToChildren
				leftRightFlowLayout.HAnchor |= HAnchor.ParentLeftRight; // add to the existing flags ParentLeftRight (starts with FitToChildren)
				// [<-><->] // attempting to make a visual descrition of what is happening
				Assert.IsTrue(leftRightFlowLayout.Width == 0); // nothing is forcing it to have a width so it doesn't
				GuiWidget leftWidget = new GuiWidget(10, 10); // we call it left widget as it will be the first one in the left to right flow layout
				leftRightFlowLayout.AddChild(leftWidget); // add in a child with a width of 10
				// [<->(10)<->] // the flow layout should now be forced to be 10 wide
				Assert.IsTrue(leftRightFlowLayout.Width == 10);

				GuiWidget middleSpacer = new GuiWidget(0, 10); // this widget will hold the space
				middleSpacer.HAnchor = HAnchor.ParentLeftRight; // by resizing to whatever width it can be
				leftRightFlowLayout.AddChild(middleSpacer);
				// [<->(10)(<->)<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 10);
				Assert.IsTrue(middleSpacer.Width == 0);

				GuiWidget rightItem = new GuiWidget(10, 10);
				leftRightFlowLayout.AddChild(rightItem);
				// [<->(10)(<->)(10)<->]
				Assert.IsTrue(leftRightFlowLayout.Width == 20);

				GuiWidget containerOuter = new GuiWidget(40, 20);
				containerOuter.Name = "containerOuter";
				GuiWidget containerInner = new GuiWidget(0, 20);
				containerInner.HAnchor = HAnchor.ParentLeftRight | HAnchor.FitToChildren;
				containerInner.Name = "containerInner";
				containerOuter.AddChild(containerInner);
				Assert.IsTrue(containerInner.Width == 40);
				containerInner.AddChild(leftRightFlowLayout);
				// (40(<-[<->(10)(<->)(10)<->]->)) // the extra 20 must be put into the expandable (<->)
				Assert.IsTrue(containerInner.Width == 40);
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 20);

				containerOuter.Width = 50;
				// (50(<-[<->(10)(<->)(10)<->]->) // the extra 30 must be put into the expandable (<->)
				Assert.IsTrue(containerInner.Width == 50);
				Assert.IsTrue(leftRightFlowLayout.Width == 50);
				Assert.IsTrue(middleSpacer.Width == 30);

				containerOuter.Width = 40;
				// (40(<-[<->(10)(<->)(10)<->]->) // the extra 20 must be put into the expandable (<->) by shrinking it
				Assert.IsTrue(containerInner.Width == 40);
				Assert.IsTrue(leftRightFlowLayout.Width == 40);
				Assert.IsTrue(middleSpacer.Width == 20);
			}
		}

		[Test]
		public void TestVAnchorCenter()
		{
			FlowLayoutWidget searchPanel = new FlowLayoutWidget();
			searchPanel.BackgroundColor = new RGBA_Bytes(180, 180, 180);
			searchPanel.HAnchor = HAnchor.ParentLeftRight;
			searchPanel.Padding = new BorderDouble(3, 3);
			{
				TextEditWidget searchInput = new TextEditWidget("Test");
				searchInput.Margin = new BorderDouble(6, 0);
				searchInput.HAnchor = HAnchor.ParentLeftRight;
				searchInput.VAnchor = VAnchor.ParentCenter;

				Button searchButton = new Button("Search");
				searchButton.Margin = new BorderDouble(right: 9);

				searchPanel.AddChild(searchInput);
				Assert.IsTrue(searchInput.BoundsRelativeToParent.Bottom - searchPanel.BoundsRelativeToParent.Bottom == searchPanel.BoundsRelativeToParent.Top - searchInput.BoundsRelativeToParent.Top);
				searchPanel.AddChild(searchButton);
				Assert.IsTrue(searchInput.BoundsRelativeToParent.Bottom - searchPanel.BoundsRelativeToParent.Bottom == searchPanel.BoundsRelativeToParent.Top - searchInput.BoundsRelativeToParent.Top);
			}

			searchPanel.Close();
		}
	}
}