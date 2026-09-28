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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	/// <summary>
	/// <see cref="Graphics2DSpanImage"/> merges a row's neighboring pixels into one rectangle, and issues what it
	/// holds before any other draw on its graphics, so the result is the same pixels in the same order.
	/// </summary>
	public class Graphics2DSpanImageTests
	{
		private const int Size = 20;

		[Test]
		public async Task NeighboringPixelsOfOneColorAndCoverBecomeOneRectanglePerRow()
		{
			var graphics = new CountingGraphics(new ImageBuffer(Size, Size));
			var spans = new Graphics2DSpanImage(graphics, Size, Size);

			for (int x = 2; x < 12; x++)
			{
				spans.BlendPixel(x, 5, Color.Red, 128);
				spans.BlendPixel(x, 6, Color.Red, 128);
			}

			// A span of the same cover carries its row's run on.
			spans.blend_solid_hspan(12, 5, 3, Color.Red, new byte[] { 128, 128, 128 }, 0);
			spans.Flush();

			await Assert.That(graphics.Rectangles).IsEqualTo(2);
		}

		[Test]
		public async Task PendingRunsAreDrawnBeforeTheNextOtherDraw()
		{
			var software = new ImageBuffer(Size, Size);
			var softwareTarget = new ImageClippingProxy(software);
			var merged = new ImageBuffer(Size, Size);
			var graphics = merged.NewGraphics2D();
			var spans = new ImageClippingProxy(new Graphics2DSpanImage(graphics, Size, Size));

			// A translucent run, a vector fill over half of it, then more run over the fill: order decides the pixels.
			foreach (var (target, draw) in new (IImageByte, Graphics2D)[] { (softwareTarget, software.NewGraphics2D()), (spans, graphics) })
			{
				for (int x = 0; x < 10; x++)
				{
					target.BlendPixel(x, 3, new Color(255, 0, 0, 160), 255);
				}

				draw.Render(new RoundedRect(5, 0, 15, 10, 0), new Color(0, 0, 255, 160));
				target.blend_hline(8, 3, 16, new Color(0, 255, 0, 160), 200);
				draw.FillRectangle(0, 0, 1, 1, Color.Black);
			}

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					await Assert.That(merged.GetPixel(x, y)).IsEqualTo(software.GetPixel(x, y));
				}
			}
		}

		/// <summary>Counts the rectangles the span image issues.</summary>
		private class CountingGraphics : ImageGraphics2D
		{
			public CountingGraphics(ImageBuffer image)
				: base(image, new ScanlineRasterizer(), new ScanlineCachePacked8())
			{
			}

			public int Rectangles { get; private set; }

			public override void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor)
			{
				this.Rectangles++;
				base.FillRectangle(left, bottom, right, top, fillColor);
			}
		}
	}
}
