/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// A software graphics whose rasterizer has no vector clip box - as C++ AGG's rasterizer_scanline_aa starts,
	/// and as the AGG demo reference frame draws - still clips to its canvas: the clip rect reads as the canvas,
	/// not as an empty box, and a clear covers every pixel.
	/// </summary>
	public class UnclippedGraphicsTests
	{
		[Test]
		public async Task ImageGraphicsWithoutAClipBoxReportsTheCanvas()
		{
			var image = new ImageBuffer(30, 20);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.Rasterizer.reset_clipping();

			await Assert.That(graphics.Rasterizer.HasVectorClipBox).IsFalse();
			await Assert.That(graphics.GetClippingRect()).IsEqualTo(new RectangleDouble(0, 0, 30, 20));

			graphics.Clear(Color.Red);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(Color.Red);
			await Assert.That(image.GetPixel(29, 19)).IsEqualTo(Color.Red);
		}

		[Test]
		public async Task ImageGraphicsFillsPastTheCanvasWithoutAClipBox()
		{
			var image = new ImageBuffer(30, 20);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.Rasterizer.reset_clipping();

			graphics.FillRectangle(-10, -10, 40, 30, Color.Blue);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(Color.Blue);
			await Assert.That(image.GetPixel(29, 19)).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task LcdBufferGraphicsWithoutAClipBoxReportsTheCanvas()
		{
			var graphics = new LcdBufferGraphics2D(new LcdBuffer(30, 20));
			graphics.Rasterizer.reset_clipping();

			await Assert.That(graphics.GetClippingRect()).IsEqualTo(new RectangleDouble(0, 0, 30, 20));
		}
	}
}
