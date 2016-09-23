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

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using NUnit.Framework;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class TextAndTextWidgetTests
	{
		public bool saveImagesForDebug;

		[Test]
		public void TextWidgetAutoSizeTest()
		{
			// resize works on text widgets
			{
				TextWidget textItem = new TextWidget("test Item", 10, 10);
				textItem.AutoExpandBoundsToText = true;

				double origWidth = textItem.Width;
				textItem.Text = "test Items";
				double newlineWidth = textItem.Width;
				Assert.IsTrue(newlineWidth > origWidth);

				textItem.Text = "test Item";
				double backToOrignWidth = textItem.Width;
				Assert.IsTrue(backToOrignWidth == origWidth);


				double origHeight = textItem.Height;
				textItem.Text = "test\nItem";
				double newlineHeight = textItem.Height;
				textItem.Text = "test Item";
				double backToOrignHeight = textItem.Height;

				Assert.IsTrue(backToOrignHeight == origHeight);
			}
		}

		[Test]
		public void TextWidgetVisibleTest()
		{
			{
				GuiWidget rectangleWidget = new GuiWidget(100, 50);
				TextWidget itemToAdd = new TextWidget("test Item", 10, 10);
				rectangleWidget.AddChild(itemToAdd);
				rectangleWidget.DoubleBuffer = true;
				rectangleWidget.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
				rectangleWidget.OnDraw(rectangleWidget.BackBuffer.NewGraphics2D());

				ImageBuffer textOnly = new ImageBuffer(75, 20);
				textOnly.NewGraphics2D().Clear(RGBA_Bytes.White);

				textOnly.NewGraphics2D().DrawString("test Item", 1, 1);

				if (saveImagesForDebug)
				{
					ImageTgaIO.Save(rectangleWidget.BackBuffer, "-rectangleWidget.tga");
					//ImageTgaIO.Save(itemToAdd.Children[0].BackBuffer, "-internalTextWidget.tga");
					ImageTgaIO.Save(textOnly, "-textOnly.tga");
				}

				Assert.IsTrue(rectangleWidget.BackBuffer.FindLeastSquaresMatch(textOnly, 1), "TextWidgets need to be drawing.");
				rectangleWidget.Close();
			}

			{
				GuiWidget rectangleWidget = new GuiWidget(100, 50);
				TextEditWidget itemToAdd = new TextEditWidget("test Item", 10, 10);
				rectangleWidget.AddChild(itemToAdd);
				rectangleWidget.DoubleBuffer = true;
				rectangleWidget.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
				rectangleWidget.OnDraw(rectangleWidget.BackBuffer.NewGraphics2D());

				ImageBuffer textOnly = new ImageBuffer(75, 20);
				textOnly.NewGraphics2D().Clear(RGBA_Bytes.White);

				TypeFacePrinter stringPrinter = new TypeFacePrinter("test Item", 12);
				IVertexSource offsetText = new VertexSourceApplyTransform(stringPrinter, Affine.NewTranslation(1, -stringPrinter.LocalBounds.Bottom));
				textOnly.NewGraphics2D().Render(offsetText, RGBA_Bytes.Black);

				if (saveImagesForDebug)
				{
					ImageTgaIO.Save(rectangleWidget.BackBuffer, "-rectangleWidget.tga");
					//ImageTgaIO.Save(itemToAdd.Children[0].BackBuffer, "-internalTextWidget.tga");
					ImageTgaIO.Save(textOnly, "-textOnly.tga");
				}

				Assert.IsTrue(rectangleWidget.BackBuffer.FindLeastSquaresMatch(textOnly, 1), "TextWidgets need to be drawing.");
				rectangleWidget.Close();
			}
		}
	}
}