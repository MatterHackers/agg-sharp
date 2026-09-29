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

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg on input that used to crash or hang it: each case is the smallest form of a resvg-test-suite case
	// that threw or timed out. Renders are 100x100 of a 100x100 viewBox, so one user unit is one pixel; At takes
	// SVG's y-down pixel coordinates.
	public class SvgRobustnessTests
	{
		private const string Head = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">";

		private static ImageBuffer Render(string svg, Func<string, byte[]> resolver = null)
		{
			SvgDocument document = SvgDocument.Parse(svg);
			document.ResourceResolver = resolver;
			return SvgRenderer.RenderToImage(document, 100, 100);
		}

		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		[Test]
		public async Task AGradientFillReachingOffTheCanvasDraws()
		{
			// painting/context/with-gradient-in-use: a paint-server fill whose shape runs past the canvas edges
			// indexed the target outside its rows.
			ImageBuffer image = Render(Head + "<linearGradient id=\"lg\"><stop offset=\"0\" stop-color=\"blue\"/><stop offset=\"1\" stop-color=\"blue\"/></linearGradient>"
				+ "<rect x=\"-50\" y=\"-50\" width=\"200\" height=\"200\" fill=\"url(#lg)\"/></svg>");
			await Assert.That(At(image, 50, 50)).IsEqualTo(Color.Blue);
			await Assert.That(At(image, 0, 99)).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task AnImageReachingOffTheCanvasDraws()
		{
			// painting/marker/with-an-image-child and structure/image/no-width-and-height-on-svg: an <image> whose
			// viewport runs past the canvas edges.
			var document = SvgDocument.Parse(Head + "<image x=\"-50\" y=\"-50\" width=\"200\" height=\"200\" xlink:href=\"data:image/png;base64,AAAA\"/></svg>");
			var raster = new ImageBuffer(2, 2);
			raster.NewGraphics2D().Clear(Color.Blue);
			document.ImageDecoder = _ => raster;
			ImageBuffer image = SvgRenderer.RenderToImage(document, 100, 100);
			await Assert.That(At(image, 50, 50)).IsEqualTo(Color.Blue);
			await Assert.That(At(image, 0, 99)).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task MorphologyWithAHugeRadiusIsQuick()
		{
			// filters/feMorphology/huge-radius timed out: each pixel walked all 2r + 1 pixels of its window.
			const string Filter = "<filter id=\"f\" filterUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"100\" height=\"100\"><feMorphology operator=\"{0}\" radius=\"99999\"/></filter>"
				+ "<rect x=\"40\" y=\"40\" width=\"20\" height=\"20\" fill=\"blue\" filter=\"url(#f)\"/></svg>";
			var watch = System.Diagnostics.Stopwatch.StartNew();
			ImageBuffer dilated = Render(Head + string.Format(Filter, "dilate"));
			ImageBuffer eroded = Render(Head + string.Format(Filter, "erode"));
			watch.Stop();

			// A window wider than the region takes in the whole region, and the transparency around it.
			await Assert.That(At(dilated, 5, 95)).IsEqualTo(Color.Blue);
			await Assert.That(At(eroded, 50, 50).alpha).IsEqualTo((byte)0);
			await Assert.That(watch.Elapsed.TotalSeconds).IsLessThan(2);
		}
	}
}
