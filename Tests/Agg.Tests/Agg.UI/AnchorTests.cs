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

using MatterHackers.VectorMath;
using System.Linq;
using System.Threading.Tasks;
using Agg.Tests.Agg;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{

	public class AnchorTests
	{
		public static bool saveImagesForDebug = true;

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			if (saveImagesForDebug)
			{
				//string outputPath = TestContext.CurrentContext.WorkDirectory;

				//ImageTgaIO.Save(control.BackBuffer, Path.Combine(outputPath, "image-control.tga"));
				//ImageTgaIO.Save(test.BackBuffer, Path.Combine(outputPath, "image-test.tga"));
			}
		}

		[Test]
		public async Task BottomAndTop()
		{
			await BottomAndTopTextControl(0, 0);
			await BottomAndTopTextControl(0, 3);
			await BottomAndTopTextControl(2, 0);
			await BottomAndTopTextControl(2.2, 3.3);
			await BottomAndTopTextControl(0, 3.3);
			await BottomAndTopTextControl(2.2, 0);
			await BottomAndTopTextControl(2.2, 3.3);

			await BottomAndTopButton(0, 0);
			await BottomAndTopButton(0, 3);
			await BottomAndTopButton(2, 0);
			await BottomAndTopButton(2.2, 3.3);
			await BottomAndTopButton(0, 3.3);
			await BottomAndTopButton(2.2, 0);
			await BottomAndTopButton(2.2, 3.3);
		}

		private async Task BottomAndTopTextControl(double controlPadding, double buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.DoubleBuffer = true;
			containerControl.BackBuffer.NewGraphics2D().Clear(Color.White);
			TextWidget controlButton1 = new TextWidget("text1");
			controlButton1.Margin = new BorderDouble(buttonMargin);
			controlButton1.OriginRelativeParent = new Vector2(-controlButton1.LocalBounds.Left, -controlButton1.LocalBounds.Bottom + controlPadding + buttonMargin);
			controlButton1.LocalBounds = new RectangleDouble(controlButton1.LocalBounds.Left, controlButton1.LocalBounds.Bottom, controlButton1.LocalBounds.Right, controlButton1.LocalBounds.Bottom + containerControl.Height - (controlPadding + buttonMargin) * 2);
			containerControl.AddChild(controlButton1);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 200);
			containerTest.Padding = new BorderDouble(controlPadding);
			containerTest.DoubleBuffer = true;
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);

			TextWidget testButton1 = new TextWidget("text1");
			testButton1.Margin = new BorderDouble(buttonMargin);
			testButton1.VAnchor = VAnchor.Bottom | VAnchor.Top;
			containerTest.AddChild(testButton1);
			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			// now change it's size
			containerTest.LocalBounds = containerControl.LocalBounds;
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			await Assert.That(containerControl.BackBuffer != null).IsTrue();
			await Assert.That(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1000)).IsTrue();
		}

		private async Task BottomAndTopButton(double controlPadding, double buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = new BorderDouble(controlPadding);
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.Margin = new BorderDouble(buttonMargin);
			controlButton1.OriginRelativeParent = new Vector2(0, controlPadding + buttonMargin);
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

			await Assert.That(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
			await Assert.That(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1000), "The Anchored widget should be in the correct place.").IsTrue();
		}

		[Test]
		public async Task BottomAndTopSetAnchorBeforAddChildTest()
		{
			await CenterBothTest(new BorderDouble(), new BorderDouble());
			await CenterBothTest(new BorderDouble(), new BorderDouble(3));
			await CenterBothTest(new BorderDouble(2), new BorderDouble(0));
			await CenterBothTest(new BorderDouble(2), new BorderDouble(3));
			await CenterBothTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		[Test]
		public async Task ParentStretchAndFitToChildren()
		{
			// Make sure normal nested layouts works as expected. First inner added then outer
			{
				GuiWidget parent = new GuiWidget(100, 200);

				GuiWidget childOuter = new GuiWidget(31, 32);
				childOuter.VAnchor = VAnchor.Fit | VAnchor.Stretch;
				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 32)).IsTrue();

				GuiWidget childInner = new GuiWidget(41, 42);
				childOuter.AddChild(childInner);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 42)).IsTrue();

				parent.AddChild(childOuter);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 200)).IsTrue();
			}

			// Make sure vertical flow layout nested works with both top bottom and children
			{
				GuiWidget parent = new GuiWidget(100, 200);
				parent.Name = "Parent";

				FlowLayoutWidget childOuter = new FlowLayoutWidget(FlowDirection.TopToBottom);
				childOuter.Name = "childOuter";
				childOuter.VAnchor = VAnchor.Fit | VAnchor.Stretch;
				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 0, 0)).IsTrue();

				GuiWidget childInner = new GuiWidget(41, 42);
				childInner.Name = "childInner";
				childOuter.AddChild(childInner);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 41, 42)).IsTrue();

				parent.AddChild(childOuter);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 41, 200)).IsTrue();
			}

			// Make sure horizontal flow layout nested works with both top bottom and children
			{
				GuiWidget parent = new GuiWidget(100, 200);
				parent.Name = "Parent";

				FlowLayoutWidget childOuter = new FlowLayoutWidget(FlowDirection.TopToBottom);
				childOuter.Name = "childOuter";
				childOuter.HAnchor = HAnchor.Fit | HAnchor.Stretch;
				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 0, 0)).IsTrue();

				GuiWidget childInner = new GuiWidget(41, 42);
				childInner.Name = "childInner";
				childOuter.AddChild(childInner);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 41, 42)).IsTrue();

				parent.AddChild(childOuter);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 100, 42)).IsTrue();
			}

			// Make sure normal nested layouts works as expected. First outer than inner added
			{
				GuiWidget parent = new GuiWidget(100, 200);

				GuiWidget childOuter = new GuiWidget(31, 32);
				childOuter.VAnchor = VAnchor.Fit | VAnchor.Stretch;
				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 32)).IsTrue();

				parent.AddChild(childOuter);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 200)).IsTrue();

				GuiWidget childInner = new GuiWidget(41, 42);
				childOuter.AddChild(childInner);

				await Assert.That(childOuter.LocalBounds == new RectangleDouble(0, 0, 31, 200)).IsTrue();
			}
		}

		[Test]
		public async Task SimpleFitToChildren()
		{
			// this is what will happen when the default of minimum size gets set on guiwidget construction
			{
				GuiWidget parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				GuiWidget child = new GuiWidget(30, 30);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10)).IsTrue();
				parent.AddChild(child);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10)).IsTrue();
				child.LocalBounds = new RectangleDouble(-10, -11, 10, 11);
				await Assert.That(child.LocalBounds == new RectangleDouble(-10, -11, 20, 19)).IsTrue();
				await Assert.That(parent.LocalBounds == new RectangleDouble(-10, 0, 20, 10)).IsTrue();
				parent.VAnchor = VAnchor.Fit;
				await Assert.That(parent.LocalBounds == new RectangleDouble(-10, -11, 20, 19)).IsTrue();
				child.Width = 50; // we set the max so this won't work
				await Assert.That(child.LocalBounds == new RectangleDouble(-10, -11, 40, 19)).IsTrue();
			}

			// this is how it should be resized when we set it change to get smaller than the initial size
			{
				GuiWidget parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				GuiWidget child = new GuiWidget(30, 30, SizeLimitsToSet.None);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10)).IsTrue();
				parent.AddChild(child);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10)).IsTrue();
				child.LocalBounds = new RectangleDouble(-10, -11, 10, 11);
				await Assert.That(child.LocalBounds == new RectangleDouble(-10, -11, 10, 11)).IsTrue();
				await Assert.That(parent.LocalBounds == new RectangleDouble(-10, 0, 10, 10)).IsTrue();
				parent.VAnchor = VAnchor.Fit;
				await Assert.That(parent.LocalBounds == new RectangleDouble(-10, -11, 10, 11)).IsTrue();
				child.Width = 50; // we set the max so this won't work
				await Assert.That(child.LocalBounds == new RectangleDouble(-10, -11, 40, 11)).IsTrue();
			}

			// if we set min an max size it should no change size at all
			{
				GuiWidget parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				GuiWidget child = new GuiWidget(30, 30, SizeLimitsToSet.Minimum | SizeLimitsToSet.Maximum);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10)).IsTrue();
				parent.AddChild(child);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10)).IsTrue();
				child.LocalBounds = new RectangleDouble(-10, -11, 10, 11);
				await Assert.That(child.LocalBounds == new RectangleDouble(-10, -11, 20, 19)).IsTrue();
				child.Width = 50; // we set the max so this won't work
				await Assert.That(child.LocalBounds == new RectangleDouble(-10, -11, 20, 19)).IsTrue();
				await Assert.That(parent.LocalBounds == new RectangleDouble(-10, 0, 20, 10)).IsTrue();
				parent.VAnchor = VAnchor.Fit;
				await Assert.That(parent.LocalBounds == new RectangleDouble(-10, -11, 20, 19)).IsTrue();
			}
		}

		private async Task BottomAndTopSetAnchorBeforAddChild(double controlPadding, double buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = new BorderDouble(controlPadding);
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.Margin = new BorderDouble(buttonMargin);
			controlButton1.OriginRelativeParent = new Vector2(0, controlPadding + buttonMargin);
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

			await Assert.That(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
			await Assert.That(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
		}

		[Test]
		public async Task AnchorLeftBottomTests()
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}

			// bottom left with some crazy localBounds.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				RectangleDouble positionedButtonBounds = positionedButton.LocalBounds;
				positionedButtonBounds.Offset(-10, -10);
				positionedButton.LocalBounds = positionedButtonBounds;
				positionedButton.OriginRelativeParent = new Vector2(10, 10);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}

			// bottom left, respect margin. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(5, 5);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}

			// bottom left, respect margin and padding. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(8, 8);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}

			// bottom left, respect margin. This time we set the Margin after the AnchorFlags.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(5, 5);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}
		}

		[Test]
		public async Task AnchorRightBottomTests()
		{
			// bottom right.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(containerNoAnchor.Width - positionedButton.Width, 0);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}

			// bottom right, respect margin. this is the easiest as there should be nothing to it.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(containerNoAnchor.Width - positionedButton.Width - 5, 5);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}

			// bottom right, respect margin. This time we set the Margin after the AnchorFlags.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(containerNoAnchor.Width - positionedButton.Width - 5, 5);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}
		}

		[Test]
		public async Task AnchorRightTopTests()
		{
			// bottom Top.
			{
				GuiWidget containerNoAnchor = new GuiWidget(300, 200);
				containerNoAnchor.DoubleBuffer = true;
				Button positionedButton = new Button("button");
				containerNoAnchor.AddChild(positionedButton);
				positionedButton.OriginRelativeParent = new Vector2(containerNoAnchor.Width - positionedButton.Width, containerNoAnchor.Height - positionedButton.Height);
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

				await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
				await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
			}
		}

		internal async Task AnchorAllTests()
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

			await Assert.That(containerNoAnchor.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
			await Assert.That(containerNoAnchor.BackBuffer == containerAnchor.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
		}

		[Test]
		public async Task CenterBothTests()
		{
			await CenterBothTest(new BorderDouble(), new BorderDouble());
			await CenterBothTest(new BorderDouble(), new BorderDouble(3));
			await CenterBothTest(new BorderDouble(2), new BorderDouble(0));
			await CenterBothTest(new BorderDouble(2), new BorderDouble(3));
			await CenterBothTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private async Task CenterBothTest(BorderDouble controlPadding, BorderDouble buttonMargin)
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
			controlButton1.OriginRelativeParent = new Vector2(buttonX, buttonY);
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

			await Assert.That(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
			OutputImages(containerControl, containerTest);
			await Assert.That(containerControl.BackBuffer.Equals(containerTest.BackBuffer, 1), "The Anchored widget should be in the correct place.").IsTrue();
		}

		[Test]
		public async Task CenterBothOffsetBoundsTests()
		{
			await CenterBothOffsetBoundsTest(new BorderDouble(), new BorderDouble());
			await CenterBothOffsetBoundsTest(new BorderDouble(), new BorderDouble(3));
			await CenterBothOffsetBoundsTest(new BorderDouble(2), new BorderDouble(0));
			await CenterBothOffsetBoundsTest(new BorderDouble(2), new BorderDouble(3));
			await CenterBothOffsetBoundsTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private async Task CenterBothOffsetBoundsTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;
			GuiWidget controlRectangle = new GuiWidget(100, 100);
			controlRectangle.BackgroundColor = Color.Red;
			controlRectangle.Margin = buttonMargin;
			double controlCenterX = controlPadding.Left + (containerControl.Width - controlPadding.Left - controlPadding.Right) / 2;
			double buttonX = controlCenterX - (controlRectangle.Width + controlRectangle.Margin.Left + controlRectangle.Margin.Right) / 2 + controlRectangle.Margin.Left;
			double controlCenterY = controlPadding.Bottom + (containerControl.Height - controlPadding.Bottom - controlPadding.Top) / 2 + controlRectangle.Margin.Bottom;
			double buttonY = controlCenterY - (controlRectangle.Height + controlRectangle.Margin.Bottom + controlRectangle.Margin.Top) / 2;
			controlRectangle.OriginRelativeParent = new Vector2(buttonX, buttonY);
			containerControl.AddChild(controlRectangle);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			GuiWidget containerTest = new GuiWidget(200, 300);
			containerTest.Padding = controlPadding;
			containerTest.DoubleBuffer = true;

			GuiWidget testRectangle = new GuiWidget(100, 100);
			RectangleDouble offsetBounds = testRectangle.LocalBounds;
			offsetBounds.Offset(-10, -10);
			testRectangle.LocalBounds = offsetBounds;
			testRectangle.BackgroundColor = Color.Red;
			testRectangle.Margin = buttonMargin;
			testRectangle.VAnchor = VAnchor.Center;
			testRectangle.HAnchor = HAnchor.Center;
			containerTest.AddChild(testRectangle);

			containerTest.OnDraw(containerTest.NewGraphics2D());
			OutputImages(containerControl, containerTest);

			await Assert.That(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
			await Assert.That(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
		}

		[Test]
		public async Task VAnchorFitIgnoresChildrenWithVAnchorStretch()
		{
			//  ______________________________________________________________
			//  |       containerControl 300                                  |
			//  | __________________________________________________________  |
			//  | |      Child A VAnchor.Fit                                | |
			//  | |   ________________________  __________________________  | |
			//  | |   | Child B Absolute Size | | Child C VAnchor.Stretch | | |
			//  | |   |_______________________| |_________________________| | |
			//  | |_________________________________________________________| |
			//  |_____________________________________________________________|
			//

			// create controls
			var containerControl = new GuiWidget(200, 300)
			{
				Name = "containerControl",
				DoubleBuffer = true
			};
			containerControl.DoubleBuffer = true;
			var childA = new GuiWidget(100, 10)
			{
				VAnchor = VAnchor.Fit,
				Name = "childA",
				MinimumSize = Vector2.Zero
			};
			await Assert.That(childA.Height).IsEqualTo(10);
			containerControl.AddChild(childA);
			var childB = new GuiWidget(100, 100)
			{
				Name = "childB",
				MinimumSize = Vector2.Zero
			};
			childA.AddChild(childB);
			await Assert.That(childA.Height).IsEqualTo(100);
			var childC = new GuiWidget(100, 100)
			{
				VAnchor = VAnchor.Stretch,
				Name = "childA",
				MinimumSize = Vector2.Zero
			};
			childA.AddChild(childC);

			// assert sizes
			await Assert.That(childA.Height).IsEqualTo(100);

			// expand B
			childB.Height = 120;

			// assert sizes
			await Assert.That(childA.Height).IsEqualTo(120);

			// compact B
			childB.Height = 80;

			// assert sizes
			await Assert.That(childA.Height).IsEqualTo(80);
		}

		[Test]
		public async Task HAnchorFitIgnoresChildrenWithHAnchorStretch()
		{
			//  ______________________________________________________________
			//  |       containerControl 300                                  |
			//  | __________________________________________________________  |
			//  | |      Child A HAnchor.Fit                                | |
			//  | |   ________________________  __________________________  | |
			//  | |   | Child B Absolute Size | | Child C HAnchor.Stretch | | |
			//  | |   |_______________________| |_________________________| | |
			//  | |_________________________________________________________| |
			//  |_____________________________________________________________|
			//

			// create controls
			var containerControl = new GuiWidget(300, 200)
			{
				Name = "containerControl",
				DoubleBuffer = true
			};
			containerControl.DoubleBuffer = true;
			var childA = new GuiWidget(10, 100)
			{
				HAnchor = HAnchor.Fit,
				Name = "childA",
				MinimumSize = Vector2.Zero
			};
			await Assert.That(childA.Width).IsEqualTo(10);
			containerControl.AddChild(childA);
			var childB = new GuiWidget(100, 100)
			{
				Name = "childB",
				MinimumSize = Vector2.Zero
			};
			childA.AddChild(childB);
			await Assert.That(childA.Width).IsEqualTo(100);
			var childC = new GuiWidget(100, 100)
			{
				HAnchor = HAnchor.Stretch,
				Name = "childA",
				MinimumSize = Vector2.Zero
			};
			childA.AddChild(childC);

			// assert sizes
			await Assert.That(childA.Width).IsEqualTo(100);

			// expand B
			childB.Width = 120;

			// assert sizes
			await Assert.That(childA.Width).IsEqualTo(120);

			// compact B
			childB.Width = 80;

			// assert sizes
			await Assert.That(childA.Width).IsEqualTo(80);
		}

		[Test]
		public async Task VAnchorCenterAndVAnchorFitWorkCorrectlyTogetherTest()
		{
			VAnchorCenterAndVAnchorFitWorkCorrectlyTogether(new BorderDouble(), new BorderDouble());
			VAnchorCenterAndVAnchorFitWorkCorrectlyTogether(new BorderDouble(), new BorderDouble(3));
			VAnchorCenterAndVAnchorFitWorkCorrectlyTogether(new BorderDouble(2), new BorderDouble(0));
			VAnchorCenterAndVAnchorFitWorkCorrectlyTogether(new BorderDouble(2), new BorderDouble(3));
			//VAnchorCenterAndVAnchorFitWorkCorrectlyTogether(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private async Task VAnchorCenterAndVAnchorFitWorkCorrectlyTogether(BorderDouble padding, BorderDouble childMargin)
		{
			//  ______________________________________________________________
			//  |       containerControl 200, 300                             |
			//  | __________________________________________________________  |
			//  | |      Child A VAnchor.Center | Fit                       | |
			//  | |   ________________________                              | |
			//  | |   | Child B Absolute Size |                             | |
			//  | |   |_______________________|                             | |
			//  | |_________________________________________________________| |
			//  |_____________________________________________________________|
			//

			// create controls
			GuiWidget containerControl = new GuiWidget(200, 300)
			{
				Name = "containerControl",
				Padding = padding,
			};
			containerControl.Padding = padding;
			var childA = new GuiWidget()
			{
				Name = "childA",
				VAnchor = VAnchor.Center | VAnchor.Fit,
				Padding = padding,
				Margin = childMargin,
			};
			containerControl.AddChild(childA);
			var childB = new GuiWidget(50, 50, SizeLimitsToSet.None)
			{
				Name = "childB",
				Margin = childMargin,
			};
			childA.AddChild(childB);

			// assert sizes and positions
			await Assert.That(childB.Height).IsEqualTo(50);
			await Assert.That(childA.Height).IsEqualTo(50 + childMargin.Height + padding.Height);
			await Assert.That(childA.Position.Y).IsEqualTo((containerControl.Height - childA.Height) / 2);
			await Assert.That(childB.Position.Y).IsEqualTo(0);
			// expand B
			childB.Height = 60;
			// assert sizes and positions
			await Assert.That(childB.Height).IsEqualTo(60);
			await Assert.That(childA.Height).IsEqualTo(60 + childMargin.Height + padding.Height);
			await Assert.That(childA.Position.Y).IsEqualTo((containerControl.Height - childA.Height) / 2);
			// compact B
			childB.Height = 40;
			// assert sizes and positions
			await Assert.That(childB.Height).IsEqualTo(40);
			await Assert.That(childA.Height).IsEqualTo(40 + childMargin.Height + padding.Height);
			await Assert.That(childA.Position.Y).IsEqualTo((containerControl.Height - childA.Height) / 2);
		}

		[Test]
		public async Task HAnchorCenterAndHAnchorFitWorkCorrectlyTogetherTest()
		{
			await HAnchorCenterAndHAnchorFitWorkCorrectlyTogether(new BorderDouble(), new BorderDouble());
			await HAnchorCenterAndHAnchorFitWorkCorrectlyTogether(new BorderDouble(), new BorderDouble(3));
			await HAnchorCenterAndHAnchorFitWorkCorrectlyTogether(new BorderDouble(2), new BorderDouble(0));
			await HAnchorCenterAndHAnchorFitWorkCorrectlyTogether(new BorderDouble(2), new BorderDouble(3));
			//HAnchorCenterAndHAnchorFitWorkCorrectlyTogether(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private async Task HAnchorCenterAndHAnchorFitWorkCorrectlyTogether(BorderDouble padding, BorderDouble childMargin)
		{
			//  ______________________________________________________________
			//  |       containerControl 200, 300                             |
			//  | __________________________________________________________  |
			//  | |      Child A HAnchor.Center | Fit                       | |
			//  | |   ________________________                              | |
			//  | |   | Child B Absolute Size |                             | |
			//  | |   |_______________________|                             | |
			//  | |_________________________________________________________| |
			//  |_____________________________________________________________|
			//

			// create controls
			GuiWidget containerControl = new GuiWidget(200, 300)
			{
				Name = "containerControl",
				Padding = padding,
			};
			containerControl.Padding = padding;
			var childA = new GuiWidget()
			{
				Name = "childA",
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Padding = padding,
				Margin = childMargin,
			};
			containerControl.AddChild(childA);
			var childB = new GuiWidget(50, 50, SizeLimitsToSet.None)
			{
				Name = "childB",
				Margin = childMargin,
			};
			childA.AddChild(childB);

			// assert sizes and positions
			await Assert.That(childB.Width).IsEqualTo(50);
			await Assert.That(childA.Width).IsEqualTo(50 + childMargin.Width + padding.Width);
			await Assert.That(childA.Position.X).IsEqualTo((containerControl.Width - childA.Width) / 2);
			await Assert.That(childB.Position.X).IsEqualTo(0);
			// expand B
			childB.Width = 60;
			// assert sizes and positions
			await Assert.That(childB.Width).IsEqualTo(60);
			await Assert.That(childA.Width).IsEqualTo(60 + childMargin.Width + padding.Width);
			await Assert.That(childA.Position.X).IsEqualTo((containerControl.Width - childA.Width) / 2);
			// compact B
			childB.Width = 40;
			// assert sizes and positions
			await Assert.That(childB.Width).IsEqualTo(40);
			await Assert.That(childA.Width).IsEqualTo(40 + childMargin.Width + padding.Width);
			await Assert.That(childA.Position.X).IsEqualTo((containerControl.Width - childA.Width) / 2);
		}

		[Test]
		public async Task HCenterHRightAndVCenterVTopTests()
		{
			await HCenterHRightAndVCenterVTopTest(new BorderDouble(), new BorderDouble());
			await HCenterHRightAndVCenterVTopTest(new BorderDouble(), new BorderDouble(3));
			await HCenterHRightAndVCenterVTopTest(new BorderDouble(2), new BorderDouble(0));
			await HCenterHRightAndVCenterVTopTest(new BorderDouble(2), new BorderDouble(3));
			await HCenterHRightAndVCenterVTopTest(new BorderDouble(1.1, 1.2, 1.3, 1.4), new BorderDouble(2.1, 2.2, 2.3, 2.4));
		}

		private async Task HCenterHRightAndVCenterVTopTest(BorderDouble controlPadding, BorderDouble buttonMargin)
		{
			GuiWidget containerControl = new GuiWidget(200, 300);
			containerControl.Padding = controlPadding;
			containerControl.DoubleBuffer = true;
			Button controlButton1 = new Button("button1");
			controlButton1.OriginRelativeParent = new Vector2(
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

			await Assert.That(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.").IsTrue();
			await Assert.That(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.").IsTrue();
		}

		[Test]
		public async Task GroupBoxResizeThenLayoutBeforeMatchChildren()
		{
			bool integerBounds = GuiWidget.DefaultEnforceIntegerBounds;
			GuiWidget.DefaultEnforceIntegerBounds = true;
			GroupBox groupBox = new GroupBox("group box");
			groupBox.Name = "groupBox";
			GuiWidget contents = new GuiWidget(30, 20);
			contents.Name = "contents";
			contents.MinimumSize = new Vector2(0, 0);

			// make sure the client area will get smaller when the contents get smaller
			groupBox.ClientArea.VAnchor = Agg.UI.VAnchor.Fit;
			groupBox.ClientArea.Name = "groupBox.ClientArea";

			groupBox.AddChild(contents);

			await Assert.That(contents.Height == 20).IsTrue();
			await Assert.That(groupBox.ClientArea.Height == 20).IsTrue();
			await Assert.That(groupBox.Height == 50).IsTrue();
			TextWidget groupBoxLabel = groupBox.Children.FirstOrDefault() as TextWidget;
			groupBoxLabel.Name = "groupBoxLabel";
			await Assert.That(groupBoxLabel.BoundsRelativeToParent.Top == groupBox.LocalBounds.Top).IsTrue();
			contents.Height = 10;
			await Assert.That(groupBoxLabel.BoundsRelativeToParent.Top == groupBox.LocalBounds.Top).IsTrue();
			await Assert.That(contents.Height == 10).IsTrue();
			await Assert.That(groupBox.ClientArea.Height == 10).IsTrue();
			await Assert.That(groupBox.Height == 40).IsTrue();

			GuiWidget.DefaultEnforceIntegerBounds = integerBounds;
		}
	}
}