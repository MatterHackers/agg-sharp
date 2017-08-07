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
using NUnit.Framework;
using System.IO;

namespace MatterHackers.Agg.UI.Tests
{
    [TestFixture, Category("Agg.UI")]
	public class AnchorTests
	{
		public static bool saveImagesForDebug = true;

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			if (saveImagesForDebug)
			{
				string outputPath = TestContext.CurrentContext.WorkDirectory;

				ImageTgaIO.Save(control.BackBuffer, Path.Combine(outputPath, "image-control.tga"));
				ImageTgaIO.Save(test.BackBuffer, Path.Combine(outputPath, "image-test.tga"));
			}
		}

		[Test]
		public void BottomAndTop()
		{
			BottomAndTopTextControl(0, 0);
			BottomAndTopTextControl(0, 3);
			BottomAndTopTextControl(2, 0);
			BottomAndTopTextControl(2.2, 3.3);
			BottomAndTopTextControl(0, 3.3);
			BottomAndTopTextControl(2.2, 0);
			BottomAndTopTextControl(2.2, 3.3);

			BottomAndTopButton(0, 0);
			BottomAndTopButton(0, 3);
			BottomAndTopButton(2, 0);
			BottomAndTopButton(2.2, 3.3);
			BottomAndTopButton(0, 3.3);
			BottomAndTopButton(2.2, 0);
			BottomAndTopButton(2.2, 3.3);
		}

		public void BottomAndTopTextControl(double controlPadding, double buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.DoubleBuffer = true;
			containerControl.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			TextWidget controlButton1 = new TextWidget("text1");
			controlButton1.Margin = new BorderDouble(buttonMargin);
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(-controlButton1.LocalBounds.Left, -controlButton1.LocalBounds.Bottom + controlPadding + buttonMargin);
			controlButton1.LocalBounds = new RectangleDouble(controlButton1.LocalBounds.Left, controlButton1.LocalBounds.Bottom, controlButton1.LocalBounds.Right, controlButton1.LocalBounds.Bottom + containerControl.Height - (controlPadding + buttonMargin) * 2);
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 200);
			containerTest.Padding = new BorderDouble(controlPadding);
			containerTest.DoubleBuffer = true;
			containerTest.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);

			TextWidget testButton1 = new TextWidget("text1");
			testButton1.Margin = new BorderDouble(buttonMargin);
			testButton1.VAnchor = VAnchor.Bottom | VAnchor.Top;
			containerTest.AddChild(testButton1);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			// now change it's size
			containerTest.LocalBounds = containerControl.LocalBounds;
			containerTest.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		public void BottomAndTopButton(double controlPadding, double buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = new BorderDouble(controlPadding);
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.Margin = new BorderDouble(buttonMargin);
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(0, controlPadding + buttonMargin);
			controlButton1.LocalBounds = new RectangleDouble(0, 0, controlButton1.LocalBounds.Width, containerControl.LocalBounds.Height - (controlPadding + buttonMargin) * 2);
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 200);
			containerTest.Padding = new BorderDouble(controlPadding);
			containerTest.DoubleBuffer = true;

			Button testButton1 = new Button("button1");
			testButton1.Margin = new BorderDouble(buttonMargin);
			testButton1.VAnchor = VAnchor.Bottom | VAnchor.Top;
			containerTest.AddChild(testButton1);

			containerTest.LocalBounds = containerControl.LocalBounds;

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void BottomAndTopSetAnchorBeforAddChild()
		{
			CenterBothTest(new BorderDouble(), new BorderDouble());
			CenterBothTest(new BorderDouble(), new BorderDouble(3));
			CenterBothTest(new BorderDouble(2), new BorderDouble(0));
			CenterBothTest(new BorderDouble(2), new BorderDouble(3));
			CenterBothTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		[Test]
		public void ParentStretchAndFitToChildren()
		{
			// Make sure normal nested layouts works as expected. First inner added then outer
			{
				GuiWidget parent = new GuiWidget(100, 200);

				GuiWidget childOuter = new GuiWidget(31, 32);
				childOuter.VAnchor = VAnchor.Fit | VAnchor.Stretch;
				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 32));

				GuiWidget childInner = new GuiWidget(41, 42);
				childOuter.AddChild(childInner);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 42));

				parent.AddChild(childOuter);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 200));
			}

			// Make sure vertical flow layout nested works with both top bottom and children
			{
				GuiWidget parent = new GuiWidget(100, 200);
				parent.Name = "Parent";

				FlowLayoutWidget childOuter = new FlowLayoutWidget(FlowDirection.TopToBottom);
				childOuter.Name = "childOuter";
				childOuter.VAnchor = VAnchor.Fit | VAnchor.Stretch;
				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 0, 0));

				GuiWidget childInner = new GuiWidget(41, 42);
				childInner.Name = "childInner";
				childOuter.AddChild(childInner);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 41, 42));

				parent.AddChild(childOuter);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 41, 200));
			}

			// Make sure horizontal flow layout nested works with both top bottom and children
			{
				GuiWidget parent = new GuiWidget(100, 200);
				parent.Name = "Parent";

				FlowLayoutWidget childOuter = new FlowLayoutWidget(FlowDirection.TopToBottom);
				childOuter.Name = "childOuter";
				childOuter.HAnchor = HAnchor.Fit | HAnchor.Stretch;
				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 0, 0));

				GuiWidget childInner = new GuiWidget(41, 42);
				childInner.Name = "childInner";
				childOuter.AddChild(childInner);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 41, 42));

				parent.AddChild(childOuter);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 100, 42));
			}

			// Make sure normal nested layouts works as expected. First outer than inner added
			{
				GuiWidget parent = new GuiWidget(100, 200);

				GuiWidget childOuter = new GuiWidget(31, 32);
				childOuter.VAnchor = VAnchor.Fit | VAnchor.Stretch;
				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 32));

				parent.AddChild(childOuter);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 200));

				GuiWidget childInner = new GuiWidget(41, 42);
				childOuter.AddChild(childInner);

				Assert.IsTrue(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 200));
			}
		}

		[Test]
		public void SimpleFitToChildren()
		{
			// this is what will happen when the default of minimum size gets set on guiwidget construction
			{
				GuiWidget parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				GuiWidget child = new GuiWidget(30, 30);
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10));
				parent.AddChild(child);
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10));
				child.LocalBounds = new RectangleDouble(-10, -11, 10, 11);
				Assert.IsTrue(child.LocalBounds == new RectangleDouble(-10, -11, 20, 19));
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(-10, 0, 20, 10));
				parent.VAnchor = VAnchor.Fit;
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(-10, -11, 20, 19));
				child.Width = 50; // we set the max so this won't work
				Assert.IsTrue(child.LocalBounds == new RectangleDouble(-10, -11, 40, 19));
			}

			// this is how it should be resized when we set it change to get smaller than the initial size
			{
				GuiWidget parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				GuiWidget child = new GuiWidget(30, 30, SizeLimitsToSet.None);
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10));
				parent.AddChild(child);
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10));
				child.LocalBounds = new RectangleDouble(-10, -11, 10, 11);
				Assert.IsTrue(child.LocalBounds == new RectangleDouble(-10, -11, 10, 11));
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(-10, 0, 10, 10));
				parent.VAnchor = VAnchor.Fit;
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(-10, -11, 10, 11));
				child.Width = 50; // we set the max so this won't work
				Assert.IsTrue(child.LocalBounds == new RectangleDouble(-10, -11, 40, 11));
			}

			// if we set min an max size it should no change size at all
			{
				GuiWidget parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				GuiWidget child = new GuiWidget(30, 30, SizeLimitsToSet.Minimum | SizeLimitsToSet.Maximum);
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10));
				parent.AddChild(child);
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10));
				child.LocalBounds = new RectangleDouble(-10, -11, 10, 11);
				Assert.IsTrue(child.LocalBounds == new RectangleDouble(-10, -11, 20, 19));
				child.Width = 50; // we set the max so this won't work
				Assert.IsTrue(child.LocalBounds == new RectangleDouble(-10, -11, 20, 19));
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(-10, 0, 20, 10));
				parent.VAnchor = VAnchor.Fit;
				Assert.IsTrue(parent.LocalBounds == new RectangleDouble(-10, -11, 20, 19));
			}
		}

		public void BottomAndTopSetAnchorBeforAddChild(double controlPadding, double buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = new BorderDouble(controlPadding);
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.Margin = new BorderDouble(buttonMargin);
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(0, controlPadding + buttonMargin);
			controlButton1.LocalBounds = new RectangleDouble(0, 0, controlButton1.LocalBounds.Width, containerControl.LocalBounds.Height - (controlPadding + buttonMargin) * 2);
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 300);
			containerTest.Padding = new BorderDouble(controlPadding);
			containerTest.DoubleBuffer = true;

			Button testButton1 = new Button("button1");
			testButton1.Margin = new BorderDouble(buttonMargin);
			testButton1.VAnchor = VAnchor.Bottom | VAnchor.Top;
			containerTest.AddChild(testButton1);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void AnchorLeftBottomTests()
		{
			// bottom left. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				anchoredButton.Margin = new BorderDouble(); // make sure we have no margin
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.HAnchor = HAnchor.Left;
				anchoredButton.VAnchor = VAnchor.Bottom;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());

				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}

			// bottom left with some crazy localBounds.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				RectangleDouble positionedButtonBounds = positionedButton.LocalBounds;
				positionedButtonBounds.Offset(-10, -10);
				positionedButton.LocalBounds = positionedButtonBounds;
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(10, 10);
				containerNoAnchor.AddChild(positionedButton);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				RectangleDouble anchoredButtonBounds = anchoredButton.LocalBounds;
				anchoredButtonBounds.Offset(-10, -10);
				anchoredButton.LocalBounds = anchoredButtonBounds;
				anchoredButton.Margin = new BorderDouble(); // make sure we have no margin
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.HAnchor = HAnchor.Left;
				anchoredButton.VAnchor = VAnchor.Bottom;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());

				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}

			// bottom left, respect margin. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(5, 5);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.Margin = new BorderDouble(5);
				anchoredButton.HAnchor = HAnchor.Left;
				anchoredButton.VAnchor = VAnchor.Bottom;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}

			// bottom left, respect margin and padding. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(8, 8);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.Padding = new BorderDouble(3);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.Margin = new BorderDouble(5);
				anchoredButton.HAnchor = HAnchor.Left;
				anchoredButton.VAnchor = VAnchor.Bottom;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}

			// bottom left, respect margin. This time we set the Margin after the AnchorFlags.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(5, 5);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.HAnchor = HAnchor.Left;
				anchoredButton.VAnchor = VAnchor.Bottom;
				anchoredButton.Margin = new BorderDouble(5);
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}
		}

		[Test]
		public void AnchorRightBottomTests()
		{
			// bottom right.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(containerNoAnchor.Width - positionedButton.Width, 0);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				anchoredButton.Margin = new BorderDouble(); // make sure we have no margin
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.HAnchor = HAnchor.Right;
				anchoredButton.VAnchor = VAnchor.Bottom;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}

			// bottom right, respect margin. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(containerNoAnchor.Width - positionedButton.Width - 5, 5);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.Margin = new BorderDouble(5);
				anchoredButton.HAnchor = HAnchor.Right;
				anchoredButton.VAnchor = VAnchor.Bottom;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}

			// bottom right, respect margin. This time we set the Margin after the AnchorFlags.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(containerNoAnchor.Width - positionedButton.Width - 5, 5);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.HAnchor = HAnchor.Right;
				anchoredButton.VAnchor = VAnchor.Bottom;
				anchoredButton.Margin = new BorderDouble(5);
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}
		}

		[Test]
		public void AnchorRightTopTests()
		{
			// bottom Top.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new VectorMath.Vector2(containerNoAnchor.Width - positionedButton.Width, containerNoAnchor.Height - positionedButton.Height);
				containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

				GuiWidget containerAnchor = new GuiWidget(300, 200);
				containerAnchor.DoubleBuffer = true;
				Button anchoredButton = new Button("button");
				anchoredButton.Margin = new BorderDouble(); // make sure we have no margin
				containerAnchor.AddChild(anchoredButton);
				anchoredButton.HAnchor = HAnchor.Right;
				anchoredButton.VAnchor = VAnchor.Top;
				containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
				OutputImages(containerNoAnchor, containerAnchor);

				Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
				Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
			}
		}

		internal void AnchorAllTests()
		{
			GuiWidget containerNoAnchor = new GuiWidget(300, 200);
			containerNoAnchor.DoubleBuffer = true;
			Button positionedButton = new Button("button");
			positionedButton.LocalBounds = containerNoAnchor.LocalBounds;
			containerNoAnchor.AddChild(positionedButton);
			containerNoAnchor.OnDraw(containerNoAnchor.NewGraphics2D());

			GuiWidget containerAnchor = new GuiWidget(300, 200);
			containerAnchor.DoubleBuffer = true;
			Button anchoredButton = new Button("button");
			anchoredButton.Margin = new BorderDouble(); // make sure we have no margin
			containerAnchor.AddChild(anchoredButton);
			anchoredButton.HAnchor = HAnchor.Left | HAnchor.Right;
			anchoredButton.VAnchor = VAnchor.Bottom | VAnchor.Top;
			containerAnchor.OnDraw(containerAnchor.NewGraphics2D());
			OutputImages(containerNoAnchor, containerAnchor);

			Assert.IsTrue(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void CenterBothTests()
		{
			CenterBothTest(new BorderDouble(), new BorderDouble());
			CenterBothTest(new BorderDouble(), new BorderDouble(3));
			CenterBothTest(new BorderDouble(2), new BorderDouble(0));
			CenterBothTest(new BorderDouble(2), new BorderDouble(3));
			CenterBothTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void CenterBothTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.Margin = buttonMargin;
			double controlCenterX = controlPadding.Left + (containerControl.Width - controlPadding.Left - controlPadding.Right) / 2;
			double buttonX = controlCenterX - (controlButton1.Width + controlButton1.Margin.Left + controlButton1.Margin.Right) / 2 + controlButton1.Margin.Left;
			double controlCenterY = controlPadding.Bottom + (containerControl.Height - controlPadding.Bottom - controlPadding.Top) / 2 + controlButton1.Margin.Bottom;
			double buttonY = controlCenterY - (controlButton1.Height + controlButton1.Margin.Bottom + controlButton1.Margin.Top) / 2;
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(buttonX, buttonY);
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 300);
			containerTest.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			Button testButton1 = new Button("button1");
			testButton1.Margin = buttonMargin;
			testButton1.VAnchor = VAnchor.Center;
			testButton1.HAnchor = HAnchor.Center;
			containerTest.AddChild(testButton1);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			OutputImages(containerControl, containerTest);
			Assert.IsTrue(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void CenterBothOffsetBoundsTests()
		{
			CenterBothOffsetBoundsTest(new BorderDouble(), new BorderDouble());
			CenterBothOffsetBoundsTest(new BorderDouble(), new BorderDouble(3));
			CenterBothOffsetBoundsTest(new BorderDouble(2), new BorderDouble(0));
			CenterBothOffsetBoundsTest(new BorderDouble(2), new BorderDouble(3));
			CenterBothOffsetBoundsTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void CenterBothOffsetBoundsTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;
			GuiWidget controlRectangle = new GuiWidget(100, 100);
			controlRectangle.BackgroundColor = RGBA_Bytes.Red;
			controlRectangle.Margin = buttonMargin;
			double controlCenterX = controlPadding.Left + (containerControl.Width - controlPadding.Left - controlPadding.Right) / 2;
			double buttonX = controlCenterX - (controlRectangle.Width + controlRectangle.Margin.Left + controlRectangle.Margin.Right) / 2 + controlRectangle.Margin.Left;
			double controlCenterY = controlPadding.Bottom + (containerControl.Height - controlPadding.Bottom - controlPadding.Top) / 2 + controlRectangle.Margin.Bottom;
			double buttonY = controlCenterY - (controlRectangle.Height + controlRectangle.Margin.Bottom + controlRectangle.Margin.Top) / 2;
			controlRectangle.OriginRelativeParent = new VectorMath.Vector2(buttonX, buttonY);
			containerControl.AddChild(controlRectangle);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 300);
			containerTest.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			GuiWidget testRectangle = new GuiWidget(100, 100);
			RectangleDouble offsetBounds = testRectangle.LocalBounds;
			offsetBounds.Offset(-10, -10);
			testRectangle.LocalBounds = offsetBounds;
			testRectangle.BackgroundColor = RGBA_Bytes.Red;
			testRectangle.Margin = buttonMargin;
			testRectangle.VAnchor = VAnchor.Center;
			testRectangle.HAnchor = HAnchor.Center;
			containerTest.AddChild(testRectangle);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void HCenterHRightAndVCenterVTopTests()
		{
			HCenterHRightAndVCenterVTopTest(new BorderDouble(), new BorderDouble());
			HCenterHRightAndVCenterVTopTest(new BorderDouble(), new BorderDouble(3));
			HCenterHRightAndVCenterVTopTest(new BorderDouble(2), new BorderDouble(0));
			HCenterHRightAndVCenterVTopTest(new BorderDouble(2), new BorderDouble(3));
			HCenterHRightAndVCenterVTopTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		public void HCenterHRightAndVCenterVTopTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.OriginRelativeParent = new VectorMath.Vector2(
				controlPadding.Left + buttonMargin.Left + (containerControl.Width - (controlPadding.Left + controlPadding.Right)) / 2,
				controlPadding.Bottom + buttonMargin.Bottom + (containerControl.Height - (controlPadding.Bottom + controlPadding.Top)) / 2);
			controlButton1.LocalBounds = new RectangleDouble(
				controlButton1.LocalBounds.Left,
				controlButton1.LocalBounds.Bottom,
				controlButton1.LocalBounds.Left + containerControl.Width / 2 - (controlPadding.Left + controlPadding.Right) / 2 - (buttonMargin.Left + buttonMargin.Right),
				controlButton1.LocalBounds.Bottom + containerControl.Height / 2 - (controlPadding.Bottom + controlPadding.Top) / 2 - (buttonMargin.Bottom + buttonMargin.Top));
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 300);
			containerTest.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			Button testButton1 = new Button("button1");
			testButton1.Margin = buttonMargin;
			testButton1.VAnchor = VAnchor.Center | VAnchor.Top;
			testButton1.HAnchor = HAnchor.Center | HAnchor.Right;
			containerTest.AddChild(testButton1);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}

		[Test]
		public void GroupBoxResizeThenLayoutBeforeMatchChildren()
		{
			bool integerBounds = GuiWidget.DefaultEnforceIntegerBounds;
			GuiWidget.DefaultEnforceIntegerBounds = true;
			GroupBox groupBox = new GroupBox("group box");
			groupBox.Name = "groupBox";
			GuiWidget contents = new GuiWidget(30, 20);
			contents.Name = "contents";
			contents.MinimumSize = new VectorMath.Vector2(0, 0);

			// make sure the client area will get smaller when the contents get smaller
			groupBox.ClientArea.VAnchor = Agg.UI.VAnchor.Fit;
			groupBox.ClientArea.Name = "groupBox.ClientArea";

			groupBox.AddChild(contents);

			Assert.IsTrue(contents.Height == 20);
			Assert.IsTrue(groupBox.ClientArea.Height == 20);
			Assert.IsTrue(groupBox.Height == 50);
			TextWidget groupBoxLabel = groupBox.Children[0] as TextWidget;
			groupBoxLabel.Name = "groupBoxLabel";
			Assert.IsTrue(groupBoxLabel.BoundsRelativeToParent.Top == groupBox.LocalBounds.Top);
			contents.Height = 10;
			Assert.IsTrue(groupBoxLabel.BoundsRelativeToParent.Top == groupBox.LocalBounds.Top);
			Assert.IsTrue(contents.Height == 10);
			Assert.IsTrue(groupBox.ClientArea.Height == 10);
			Assert.IsTrue(groupBox.Height == 40);

			GuiWidget.DefaultEnforceIntegerBounds = integerBounds;
		}
	}
}