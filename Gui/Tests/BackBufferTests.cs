/*
Copyright (c) 2013, Lars Brubaker
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI.Tests
{
    public class BackBufferTests
    {
        public bool saveImagesForDebug;

        void SaveImage(ImageBuffer image, string dest)
        {
            if (saveImagesForDebug)
            {
                ImageTgaIO.Save(image, dest);
            }
        }

        [Test]
        public void DoubleBufferTests()
        {
            {
                bool textWidgetDoubleBufferDefault = TextWidget.DoubleBufferDefault;

                // the text widget is double buffered 
                TextWidget.DoubleBufferDefault = true;
                ImageBuffer doubleBufferImage = new ImageBuffer(65, 50, 24, new BlenderBGR());
                Button doubleBufferButton = new Button("testing", 0, 0);
                doubleBufferButton.OnDraw(doubleBufferImage.NewGraphics2D());
                SaveImage(doubleBufferImage, "z control.tga");

                // make sure the frame comparison function works.
                {
                    ImageBuffer doubleBufferImageCopy = new ImageBuffer(doubleBufferImage, new BlenderBGR());
                    Assert.IsTrue(doubleBufferImage == doubleBufferImageCopy);
                }

                // the text widget is not double buffered 
                TextWidget.DoubleBufferDefault = false;
                ImageBuffer notDoubleBufferImage = new ImageBuffer(65, 50, 24, new BlenderBGR());
                Button notDoubleBufferButton = new Button("testing", 0, 0);
                notDoubleBufferButton.OnDraw(notDoubleBufferImage.NewGraphics2D());
                SaveImage(notDoubleBufferImage, "z test.tga");

                Assert.IsTrue(doubleBufferImage == notDoubleBufferImage);

                TextWidget.DoubleBufferDefault = textWidgetDoubleBufferDefault;
            }
        }

        public void BackBuffersAreScreenAligned()
        {
            // make sure draw string and a text widget produce the same result when drawn to the same spot
            {
                ImageBuffer drawStringImage = new ImageBuffer(100, 20, 24, new BlenderBGR());
                {
                    Graphics2D drawStringGraphics = drawStringImage.NewGraphics2D();
                    drawStringGraphics.Clear(RGBA_Bytes.White);
                    drawStringGraphics.DrawString("test", 0, 0);
                    SaveImage(drawStringImage, "z draw string.tga");
                }

                ImageBuffer textWidgetImage = new ImageBuffer(100, 20, 24, new BlenderBGR());
                {
                    TextWidget textWidget = new TextWidget("test");
                    Graphics2D textWidgetGraphics = textWidgetImage.NewGraphics2D();
                    textWidgetGraphics.Clear(RGBA_Bytes.White);
                    textWidget.OnDraw(textWidgetGraphics);
                }

                Assert.IsTrue(drawStringImage == textWidgetImage);
            }

            // make sure that a back buffer is always trying to draw 1:1 pixels to the buffer above
            {
                ImageBuffer drawStringOffsetImage = new ImageBuffer(100, 20, 32, new BlenderBGRA());
                {
                    Graphics2D drawStringGraphics = drawStringOffsetImage.NewGraphics2D();
                    drawStringGraphics.Clear(RGBA_Bytes.White);
                    drawStringGraphics.DrawString("test", 23.3, 0);
                    SaveImage(drawStringOffsetImage, "z draw offset string.tga");
                }

                GuiWidget container = new GuiWidget(100, 20);
                container.DoubleBuffer = true;
                {
                    TextWidget textWidget = new TextWidget("test", 23.3);
                    container.AddChild(textWidget);
                    container.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
                    container.OnDraw(container.BackBuffer.NewGraphics2D());
                    SaveImage(container.BackBuffer, "z offset text widget.tga");
                }

                Vector2 bestPosition;
                double bestLeastSquares;
                double maxError = 10;
                container.BackBuffer.FindLeastSquaresMatch(drawStringOffsetImage, out bestPosition, out bestLeastSquares, maxError);
                Assert.IsTrue(bestLeastSquares < maxError);
            }

            {
                ImageBuffer drawStringOffsetImage = new ImageBuffer(100, 20, 32, new BlenderBGRA());
                {
                    Graphics2D drawStringGraphics = drawStringOffsetImage.NewGraphics2D();
                    drawStringGraphics.Clear(RGBA_Bytes.White);
                    drawStringGraphics.DrawString("test", 23.8, 0);
                    SaveImage(drawStringOffsetImage, "z draw offset string.tga");
                }

                GuiWidget container1 = new GuiWidget(100, 20);
                container1.DoubleBuffer = true;
                GuiWidget container2 = new GuiWidget(90, 20);
                container2.OriginRelativeParent = new Vector2(.5, 0);
                container1.AddChild(container2);
                {
                    TextWidget textWidget = new TextWidget("test", 23.3);
                    container2.AddChild(textWidget);
                    container1.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
                    container1.OnDraw(container1.BackBuffer.NewGraphics2D());
                    SaveImage(container1.BackBuffer, "z offset text widget.tga");
                }

                Assert.IsTrue(container1.BackBuffer.FindLeastSquaresMatch(drawStringOffsetImage, 5));
            }
        }
    }
}
