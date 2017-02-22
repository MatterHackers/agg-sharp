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
	public class ListBoxTests
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
		public void SingleItemVisibleTest()
		{
			{
				ListBox containerListBox = new ListBox(new RectangleDouble(0, 0, 100, 100));
				ListBoxTextItem itemToAddToList = new ListBoxTextItem("test Item", "test data for item");
				itemToAddToList.Name = "list item";
				containerListBox.AddChild(itemToAddToList);
				containerListBox.DoubleBuffer = true;
				containerListBox.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
				containerListBox.OnDraw(containerListBox.BackBuffer.NewGraphics2D());

				ImageBuffer textImage = new ImageBuffer(80, 16);
				textImage.NewGraphics2D().Clear(RGBA_Bytes.White);
				textImage.NewGraphics2D().DrawString("test Item", 1, 1);

				OutputImage(containerListBox.BackBuffer, "test.tga");
				OutputImage(textImage, "control.tga");

				double maxError = 20000000;
				Vector2 bestPosition;
				double leastSquares;
				containerListBox.BackBuffer.FindLeastSquaresMatch(textImage, out bestPosition, out leastSquares, maxError);

				Assert.IsTrue(leastSquares < maxError, "The list box need to be showing the item we added to it.");
			}

			{
				GuiWidget container = new GuiWidget(202, 302);
				container.DoubleBuffer = true;
				container.NewGraphics2D().Clear(RGBA_Bytes.White);
				FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget();
				leftToRightLayout.AnchorAll();
				{
					{
						ListBox listBox = new ListBox(new RectangleDouble(0, 0, 200, 300));
						//listBox.BackgroundColor = RGBA_Bytes.Red;
						listBox.Name = "listBox";
						listBox.VAnchor = UI.VAnchor.ParentTop;
						listBox.ScrollArea.Margin = new BorderDouble(15);
						leftToRightLayout.AddChild(listBox);

						for (int i = 0; i < 1; i++)
						{
							ListBoxTextItem newItem = new ListBoxTextItem("hand" + i.ToString() + ".stl", "c:\\development\\hand" + i.ToString() + ".stl");
							newItem.Name = "ListBoxItem" + i.ToString();
							listBox.AddChild(newItem);
						}
					}
				}

				container.AddChild(leftToRightLayout);
				container.OnDraw(container.NewGraphics2D());

				ImageBuffer textImage = new ImageBuffer(80, 16);
				textImage.NewGraphics2D().Clear(RGBA_Bytes.White);
				textImage.NewGraphics2D().DrawString("hand0.stl", 1, 1);

				OutputImage(container.BackBuffer, "control.tga");
				OutputImage(textImage, "test.tga");

				double maxError = 1000000;
				Vector2 bestPosition;
				double leastSquares;
				container.BackBuffer.FindLeastSquaresMatch(textImage, out bestPosition, out leastSquares, maxError);

				Assert.IsTrue(leastSquares < maxError, "The list box need to be showing the item we added to it.");
			}
		}

		[Test]
		public void ScrollPositionStartsCorrect()
		{
			GuiWidget contents = new GuiWidget(300, 300);
			contents.DoubleBuffer = true;
			ListBox container = new ListBox(new RectangleDouble(0, 0, 200, 300));
			//container.BackgroundColor = RGBA_Bytes.Red;
			container.Name = "containerListBox";
			container.VAnchor = UI.VAnchor.ParentTop;
			container.Margin = new BorderDouble(15);

			contents.AddChild(container);

			container.AddChild(new ListBoxTextItem("hand.stl", "c:\\development\\hand.stl"));

			contents.OnDraw(contents.NewGraphics2D());

			Assert.IsTrue(container.TopLeftOffset.y == 0);
		}

		private static void AddContents(GuiWidget widgetToAddItemsTo)
		{
			string[] listItems = new string[] { "Item1", "Item2", "Item3", "Item4" };

			widgetToAddItemsTo.Padding = new BorderDouble(5);
			widgetToAddItemsTo.BackgroundColor = new RGBA_Bytes(68, 68, 68);

			//Get a list of printer records and add them to radio button list
			foreach (string listItem in listItems)
			{
				TextWidget textItem = new TextWidget(listItem);
				textItem.BackgroundColor = RGBA_Bytes.Blue;
				textItem.Margin = new BorderDouble(2);
				widgetToAddItemsTo.AddChild(textItem);
			}
		}
	}
}