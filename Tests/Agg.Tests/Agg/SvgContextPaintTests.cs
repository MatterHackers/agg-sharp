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
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// context-fill and context-stroke, as usvg resolves them: in a use's content, the use's own fill/stroke; in a
	// marker's content, the fill/stroke of the path it marks. Renders are 100x100 of a 100x100 viewBox.
	public class SvgContextPaintTests
	{
		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">{body}</svg>", 100, 100);
		}

		/// <summary>The pixel at SVG coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		[Test]
		public async Task AUsesFillAndStrokeAreItsContentsContextPaint()
		{
			ImageBuffer used = Render("<defs><rect id=\"r\" width=\"50\" height=\"50\" fill=\"context-stroke\" stroke=\"context-fill\" stroke-width=\"10\"/></defs>"
				+ "<use xlink:href=\"#r\" x=\"20\" y=\"20\" fill=\"#0000ff\" stroke=\"#008000\"/>");
			await Assert.That(At(used, 45, 45)).IsEqualTo(new Color(0, 128, 0, 255));
			await Assert.That(At(used, 17, 45)).IsEqualTo(new Color(0, 0, 255, 255));

			// A use whose own fill is context-fill passes on the context of the use around it.
			ImageBuffer nested = Render("<defs><rect id=\"r\" width=\"50\" height=\"50\" fill=\"context-fill\"/></defs>"
				+ "<use id=\"u\" xlink:href=\"#r\" fill=\"context-fill\"/><use xlink:href=\"#u\" x=\"20\" fill=\"#0000ff\"/>");
			await Assert.That(At(nested, 45, 25)).IsEqualTo(new Color(0, 0, 255, 255));
		}

		[Test]
		public async Task ContextPaintWithoutAContextElementIsNone()
		{
			ImageBuffer image = Render("<rect width=\"50\" height=\"50\" fill=\"context-fill\" stroke=\"context-stroke\"/>");
			await Assert.That(At(image, 25, 25).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task AMarkersContextPaintIsThePathsFillAndStroke()
		{
			ImageBuffer image = Render("<marker id=\"m\" refX=\"5\" refY=\"5\" markerWidth=\"10\" markerHeight=\"10\" markerUnits=\"userSpaceOnUse\">"
				+ "<rect width=\"10\" height=\"10\" fill=\"context-stroke\"/></marker>"
				+ "<path d=\"M 20 50 L 80 50\" fill=\"none\" stroke=\"#008000\" marker-start=\"url(#m)\"/>");
			await Assert.That(At(image, 18, 46)).IsEqualTo(new Color(0, 128, 0, 255));
		}

		[Test]
		public async Task AContextGradientSpansTheContextElementsBox()
		{
			// The gradient runs across the use's box (0..100), not each rect's own half.
			ImageBuffer image = Render("<linearGradient id=\"lg\"><stop offset=\"0\" stop-color=\"#ff0000\"/><stop offset=\"1\" stop-color=\"#0000ff\"/></linearGradient>"
				+ "<defs><g id=\"g\"><rect width=\"50\" height=\"100\" fill=\"context-fill\"/><rect x=\"50\" width=\"50\" height=\"100\" fill=\"context-fill\"/></g></defs>"
				+ "<use xlink:href=\"#g\" fill=\"url(#lg)\"/>");
			Color nearMiddle = At(image, 45, 50);
			await Assert.That((int)nearMiddle.red).IsGreaterThan(nearMiddle.blue);
		}
	}
}
