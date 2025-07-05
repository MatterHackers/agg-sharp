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

using Agg.Tests.Agg;
using TUnit.Assertions;
using TUnit.Core;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System.IO;

namespace MatterHackers.Agg.UI.Tests
{
    
    public class TextAndTextWidgetTests
	{
		public bool saveImagesForDebug;

        [Test]
        public async Task TextWidgetAutoSizeTest()
		{
			// resize works on text widgets
			{
				TextWidget textItem = new TextWidget("test Item", 10, 10);
				textItem.AutoExpandBoundsToText = true;

				double origWidth = textItem.Width;
				textItem.Text = "test Items";
				double newlineWidth = textItem.Width;
				await Assert.That(newlineWidth > origWidth).IsTrue();

				textItem.Text = "test Item";
				double backToOrignWidth = textItem.Width;
				await Assert.That(backToOrignWidth == origWidth).IsTrue();


				double origHeight = textItem.Height;
				textItem.Text = "test\nItem";
				double newlineHeight = textItem.Height;
				textItem.Text = "test Item";
				double backToOrignHeight = textItem.Height;

				await Assert.That(backToOrignHeight == origHeight).IsTrue();
			}

			// make sure text widget gets smaller vertically when it needs to
			{
				GuiWidget containerControl = new GuiWidget(640, 480);
				containerControl.DoubleBuffer = true;

				GuiWidget holder = new GuiWidget(500, 10)
				{
					VAnchor = VAnchor.Fit,
					MinimumSize = Vector2.Zero
				};
				containerControl.AddChild(holder);

				var textItem = new WrappedTextWidget("some very long text that can wrap");
				holder.AddChild(textItem);

				var origSize = textItem.Size;
				await Assert.That(origSize.X > 10).IsTrue();
				holder.Width = 100;
				var bigSize = textItem.Size;

				await Assert.That(bigSize.X < origSize.X).IsTrue();
				await Assert.That(bigSize.Y > origSize.Y).IsTrue();

				holder.Width = 500;
				var backToOrignSize = textItem.Size;
				await Assert.That(backToOrignSize.X == origSize.X).IsTrue();
				await Assert.That(backToOrignSize.Y == origSize.Y).IsTrue();

				double origHeight = textItem.Height;
				textItem.Text = "test\nItem";
				double newlineHeight = textItem.Height;
				textItem.Text = "test Item";
				double backToOrignHeight = textItem.Height;

				await Assert.That(backToOrignHeight == origHeight).IsTrue();
			}
		}

        [Test]
        public async Task TextWidgetVisibleTest()
		{
			{
				GuiWidget rectangleWidget = new GuiWidget(100, 50);
				TextWidget itemToAdd = new TextWidget("test Item", 10, 10);
				rectangleWidget.AddChild(itemToAdd);
				rectangleWidget.DoubleBuffer = true;
				rectangleWidget.BackBuffer.NewGraphics2D().Clear(Color.White);
				rectangleWidget.OnDraw(rectangleWidget.BackBuffer.NewGraphics2D());

				ImageBuffer textOnly = new ImageBuffer(75, 20);
				textOnly.NewGraphics2D().Clear(Color.White);

				textOnly.NewGraphics2D().DrawString("test Item", 1, 1);

				if (saveImagesForDebug)
				{
					ImageTgaIO.Save(rectangleWidget.BackBuffer, "-rectangleWidget.tga");
					//ImageTgaIO.Save(itemToAdd.Children[0].BackBuffer, "-internalTextWidget.tga");
					ImageTgaIO.Save(textOnly, "-textOnly.tga");
				}

				await Assert.That(rectangleWidget.BackBuffer.FindLeastSquaresMatch(textOnly, 1)).IsTrue();
				rectangleWidget.Close();
			}

			{
				var oldEnforceIntegerBounds = GuiWidget.DefaultEnforceIntegerBounds;
				GuiWidget.DefaultEnforceIntegerBounds = false;
				GuiWidget rectangleWidget = new GuiWidget(100, 50);
				TextEditWidget itemToAdd = new TextEditWidget("test Item", 10, 10);
				rectangleWidget.AddChild(itemToAdd);
				rectangleWidget.DoubleBuffer = true;
				rectangleWidget.BackBuffer.NewGraphics2D().Clear(Color.White);
				rectangleWidget.OnDraw(rectangleWidget.BackBuffer.NewGraphics2D());

				ImageBuffer textOnly = new ImageBuffer(75, 20);
				textOnly.NewGraphics2D().Clear(Color.White);

				TypeFacePrinter stringPrinter = new TypeFacePrinter("test Item", 12);
				IVertexSource offsetText = new VertexSourceApplyTransform(stringPrinter, Affine.NewTranslation(1, -stringPrinter.LocalBounds.Bottom));
				textOnly.NewGraphics2D().Render(offsetText, Color.Black);

				if (saveImagesForDebug)
				{
					var basePath = Path.Combine("C:", "Temp", "Debug");
					Directory.CreateDirectory(basePath);

                    ImageTgaIO.Save(rectangleWidget.BackBuffer, Path.Combine(basePath, "-rectangleWidget.tga"));
                    //ImageTgaIO.Save(itemToAdd.Children[0].BackBuffer, Path.Combine(basePath, "-internalTextWidget.tga"));
                    ImageTgaIO.Save(textOnly, Path.Combine(basePath, "-textOnly.tga"));
				}

				await Assert.That(rectangleWidget.BackBuffer.FindLeastSquaresMatch(textOnly, 1)).IsTrue();
				rectangleWidget.Close();
				GuiWidget.DefaultEnforceIntegerBounds = oldEnforceIntegerBounds;
            }
		}
	}
}
