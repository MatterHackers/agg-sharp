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
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using System.Threading.Tasks;

namespace MatterHackers.Agg.UI.Tests
{

	public class BackBufferTests
	{
		public bool saveImagesForDebug;

		private void SaveImage(ImageBuffer image, string dest)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(image, dest);
			}
		}

		[Test]
		public async Task DoubleBufferTests()
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
				await Assert.That(doubleBufferImage == doubleBufferImageCopy).IsTrue();
			}

			// the text widget is not double buffered
			TextWidget.DoubleBufferDefault = false;
			ImageBuffer notDoubleBufferImage = new ImageBuffer(65, 50, 24, new BlenderBGR());
			Button notDoubleBufferButton = new Button("testing", 0, 0);
			notDoubleBufferButton.OnDraw(notDoubleBufferImage.NewGraphics2D());
			SaveImage(notDoubleBufferImage, "z test.tga");

			await Assert.That(doubleBufferImage == notDoubleBufferImage).IsTrue();

			TextWidget.DoubleBufferDefault = textWidgetDoubleBufferDefault;
		}

		[Test]
		public async Task BackBuffersAreScreenAligned()
		{
			// make sure draw string and a text widget produce the same result when drawn to the same spot
			{
				ImageBuffer drawStringImage = new ImageBuffer(100, 20, 24, new BlenderBGR());
				{
					Graphics2D drawStringGraphics = drawStringImage.NewGraphics2D();
					drawStringGraphics.Clear(Color.White);
					drawStringGraphics.DrawString("test", 0, 0);
					SaveImage(drawStringImage, "z draw string.tga");
				}

				ImageBuffer textWidgetImage = new ImageBuffer(100, 20, 24, new BlenderBGR());
				{
					TextWidget textWidget = new TextWidget("test");
					Graphics2D textWidgetGraphics = textWidgetImage.NewGraphics2D();
					textWidgetGraphics.Clear(Color.White);
					textWidget.OnDraw(textWidgetGraphics);
				}

				await Assert.That(drawStringImage == textWidgetImage).IsTrue();
			}

			// make sure that a back buffer is always trying to draw 1:1 pixels to the buffer above
			{
				ImageBuffer drawStringOffsetImage = new ImageBuffer(100, 20);
				{
					Graphics2D drawStringGraphics = drawStringOffsetImage.NewGraphics2D();
					drawStringGraphics.Clear(Color.White);
					drawStringGraphics.DrawString("test", 23.3, 0);
					SaveImage(drawStringOffsetImage, "z draw offset string.tga");
				}

				GuiWidget container = new GuiWidget(100, 20);
				container.DoubleBuffer = true;
				{
					TextWidget textWidget = new TextWidget("test", 23.3);
					container.AddChild(textWidget);
					container.BackBuffer.NewGraphics2D().Clear(Color.White);
					container.OnDraw(container.BackBuffer.NewGraphics2D());
					SaveImage(container.BackBuffer, "z offset text widget.tga");
				}

				Vector2 bestPosition;
				double bestLeastSquares;
				double maxError = 10;
				container.BackBuffer.FindLeastSquaresMatch(drawStringOffsetImage, out bestPosition, out bestLeastSquares, maxError);
				await Assert.That(bestLeastSquares < maxError).IsTrue();
			}

			{
				ImageBuffer drawStringOffsetImage = new ImageBuffer(100, 20);
				{
					Graphics2D drawStringGraphics = drawStringOffsetImage.NewGraphics2D();
					drawStringGraphics.Clear(Color.White);
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
					container1.BackBuffer.NewGraphics2D().Clear(Color.White);
					container1.OnDraw(container1.BackBuffer.NewGraphics2D());
					SaveImage(container1.BackBuffer, "z offset text widget.tga");
				}

				Vector2 bestPos3;
				double bestLS3;
				container1.BackBuffer.FindLeastSquaresMatch(drawStringOffsetImage, out bestPos3, out bestLS3, 1000);
				System.Console.WriteLine($"DIAG: bestPos={bestPos3}, bestLeastSquares={bestLS3}, backBuffer={container1.BackBuffer.Width}x{container1.BackBuffer.Height}x{container1.BackBuffer.BitDepth}, ref={drawStringOffsetImage.Width}x{drawStringOffsetImage.Height}x{drawStringOffsetImage.BitDepth}");
				// Also dump first non-white pixel differences
				var bbBuf = container1.BackBuffer.GetBuffer();
				var refBuf = drawStringOffsetImage.GetBuffer();
				int diffCount = 0;
				for (int i = 0; i < System.Math.Min(bbBuf.Length, refBuf.Length) && diffCount < 20; i++)
				{
					if (bbBuf[i] != refBuf[i])
					{
						int pixel = i / (container1.BackBuffer.BitDepth / 8);
						int channel = i % (container1.BackBuffer.BitDepth / 8);
						int px = pixel % container1.BackBuffer.Width;
						int py = pixel / container1.BackBuffer.Width;
						System.Console.WriteLine($"DIAG: diff at pixel ({px},{py}) ch{channel}: bb={bbBuf[i]} ref={refBuf[i]} delta={bbBuf[i]-refBuf[i]}");
						diffCount++;
					}
				}
				await Assert.That(container1.BackBuffer.FindLeastSquaresMatch(drawStringOffsetImage, 5)).IsTrue();
			}
		}
	}
}
