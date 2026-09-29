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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg, stage 3: the <image> element. Renders are 100x100 of a 100x100 viewBox, so one user unit is one
	// pixel, and At takes SVG's y-down pixel coordinates. The raster is a 2x2 image - red, blue on its top row,
	// green, white below - handed out by a decoder the test supplies, as an application supplies its codecs.
	public class SvgImageTests
	{
		private const string AnyRaster = "data:image/png;base64,AAAA";

		private static ImageBuffer Quad()
		{
			var image = new ImageBuffer(2, 2);
			image.SetPixel(0, 1, Color.Red);
			image.SetPixel(1, 1, Color.Blue);
			image.SetPixel(0, 0, new Color(0, 255, 0));
			image.SetPixel(1, 0, Color.White);
			return image;
		}

		private static ImageBuffer Render(string body, Func<byte[], ImageBuffer> decoder = null, Func<string, byte[]> resolver = null)
		{
			SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">{body}</svg>");
			document.ImageDecoder = decoder ?? (_ => Quad());
			document.ResourceResolver = resolver;
			return SvgRenderer.RenderToImage(document, 100, 100);
		}

		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		private static bool Is(Color c, int red, int green, int blue) => c.red == red && c.green == green && c.blue == blue && c.alpha == 255;

		[Test]
		public async Task RasterIsCentredAndScaledToMeetItsViewport()
		{
			// 2x2 met into 100x50 is 50x50, centred at x 25..75.
			ImageBuffer image = Render($"<image x=\"0\" y=\"0\" width=\"100\" height=\"50\" xlink:href=\"{AnyRaster}\"/>");
			await Assert.That(Is(At(image, 30, 5), 255, 0, 0)).IsTrue();
			await Assert.That(Is(At(image, 70, 5), 0, 0, 255)).IsTrue();
			await Assert.That(Is(At(image, 30, 45), 0, 255, 0)).IsTrue();
			await Assert.That(At(image, 10, 25).alpha).IsEqualTo((byte)0);
			await Assert.That(At(image, 50, 60).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task PreserveAspectRatioNoneStretchesAndSliceCovers()
		{
			ImageBuffer none = Render($"<image width=\"100\" height=\"50\" preserveAspectRatio=\"none\" xlink:href=\"{AnyRaster}\"/>");
			await Assert.That(Is(At(none, 2, 2), 255, 0, 0)).IsTrue();
			await Assert.That(Is(At(none, 97, 47), 255, 255, 255)).IsTrue();

			// Sliced, 2x2 covers 100x50 at 100x100, its middle showing: the top row's lower half and the bottom's upper
			// (their centres fall on the viewport's edges, so smoothing tints the pixels beside them a little).
			ImageBuffer slice = Render($"<image width=\"100\" height=\"50\" preserveAspectRatio=\"xMidYMid slice\" xlink:href=\"{AnyRaster}\"/>");
			await Assert.That(At(slice, 2, 2).red).IsGreaterThan((byte)240);
			await Assert.That(At(slice, 2, 47).green).IsGreaterThan((byte)240);
			await Assert.That(At(slice, 2, 47).alpha).IsEqualTo((byte)255);
			await Assert.That(At(slice, 50, 60).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task MissingSizeIsTheImagesOwn()
		{
			ImageBuffer image = Render($"<image x=\"10\" y=\"10\" xlink:href=\"{AnyRaster}\"/>");
			await Assert.That(Is(At(image, 10, 10), 255, 0, 0)).IsTrue();
			await Assert.That(Is(At(image, 11, 11), 255, 255, 255)).IsTrue();
			await Assert.That(At(image, 12, 12).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task PixelatedImageRenderingIsNotSmoothed()
		{
			// 2x2 over 100x100: x 45 is inside the red texel but near enough blue that smoothing blends it.
			ImageBuffer smooth = Render($"<image width=\"100\" height=\"100\" xlink:href=\"{AnyRaster}\"/>");
			ImageBuffer pixelated = Render($"<g image-rendering=\"pixelated\"><image width=\"100\" height=\"100\" xlink:href=\"{AnyRaster}\"/></g>");
			await Assert.That(Is(At(pixelated, 45, 10), 255, 0, 0)).IsTrue();
			await Assert.That(At(smooth, 45, 10).blue).IsGreaterThan((byte)0);
		}

		[Test]
		public async Task WithoutADecoderOrResolverNothingIsDrawn()
		{
			ImageBuffer image = Render($"<image width=\"100\" height=\"100\" xlink:href=\"{AnyRaster}\"/><image width=\"100\" height=\"100\" xlink:href=\"pic.png\"/>", decoder: _ => null);
			await Assert.That(At(image, 50, 50).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ExternalHrefIsReadThroughTheResolverAsWritten()
		{
			var asked = new List<string>();
			ImageBuffer image = Render("<image width=\"100\" height=\"100\" xlink:href=\"../images/pic.png\"/>", resolver: href =>
			{
				asked.Add(href);
				return new byte[] { 1 };
			});
			await Assert.That(asked).IsEquivalentTo(new[] { "../images/pic.png" }, CollectionOrdering.Matching);
			await Assert.That(Is(At(image, 5, 5), 255, 0, 0)).IsTrue();
		}

		[Test]
		public async Task SvgImageIsDrawnAsAVectorDocument()
		{
			// A percent-encoded (not base64) SVG with a 10x10 viewBox and a red left half, met into 50x50 at (50, 50).
			string nested = Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><rect width='5' height='10' fill='red'/></svg>");
			ImageBuffer image = Render($"<image x=\"50\" y=\"50\" width=\"50\" height=\"50\" xlink:href=\"data:image/svg+xml,{nested}\"/>", decoder: _ => throw new InvalidDataException("SVG is not a raster"));
			await Assert.That(Is(At(image, 60, 75), 255, 0, 0)).IsTrue();
			await Assert.That(At(image, 90, 75).alpha).IsEqualTo((byte)0);
			await Assert.That(At(image, 20, 75).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task SvgImageIsClippedToItsViewport()
		{
			// A red square three times the nested viewBox, centred on it: only the image's own 50x50 at (50, 50) shows.
			string nested = Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><rect x='-10' y='-10' width='30' height='30' fill='red'/></svg>");
			ImageBuffer image = Render($"<image x=\"50\" y=\"50\" width=\"50\" height=\"50\" xlink:href=\"data:image/svg+xml,{nested}\"/>");
			await Assert.That(Is(At(image, 50, 50), 255, 0, 0)).IsTrue();
			await Assert.That(Is(At(image, 99, 99), 255, 0, 0)).IsTrue();
			await Assert.That(At(image, 49, 75).alpha).IsEqualTo((byte)0);
			await Assert.That(At(image, 75, 49).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task AnSvgImageDrawsNoImagesOfItsOwn()
		{
			// As in usvg, an SVG image's own <image> elements are not drawn (resvg's recursive-2: a document including
			// itself shows once, with an empty frame inside). The nested document's red rect shows; its raster does not.
			string nested = Uri.EscapeDataString($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><rect width='5' height='10' fill='red'/><image x='5' width='5' height='10' href='{AnyRaster}'/></svg>");
			ImageBuffer image = Render($"<image width=\"100\" height=\"100\" xlink:href=\"data:image/svg+xml,{nested}\"/>");
			await Assert.That(Is(At(image, 25, 50), 255, 0, 0)).IsTrue();
			await Assert.That(At(image, 75, 50).alpha).IsEqualTo((byte)0);
		}
	}
}
