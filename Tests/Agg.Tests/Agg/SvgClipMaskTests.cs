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
	// agg/Svg, stage 5: clip-path and mask. Renders are 100x100 of a 100x100 viewBox, one user unit per pixel.
	public class SvgClipMaskTests
	{
		private const string Green = "<rect width=\"100\" height=\"100\" fill=\"#00ff00\" {0}/>";

		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">{body}</svg>", 100, 100);
		}

		/// <summary>The alpha at SVG coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static int AlphaAt(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y).alpha;

		private static string Filled(string attributes) => string.Format(Green, attributes);

		[Test]
		public async Task AClipPathKeepsOnlyWhatItsShapesCover()
		{
			ImageBuffer image = Render("<clipPath id=\"c\"><rect x=\"20\" y=\"20\" width=\"30\" height=\"30\"/><circle cx=\"80\" cy=\"80\" r=\"10\"/></clipPath>"
				+ Filled("clip-path=\"url(#c)\""));
			await Assert.That(AlphaAt(image, 35, 35)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 80, 80)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 10, 10)).IsEqualTo(0);
			await Assert.That(AlphaAt(image, 60, 35)).IsEqualTo(0);
		}

		[Test]
		public async Task ClipRuleTransformsAndBoundingBoxUnitsShapeTheClip()
		{
			// evenodd leaves the inner square a hole; the clip's transform moves it right by 50.
			ImageBuffer evenOdd = Render("<clipPath id=\"c\" transform=\"translate(50 0)\"><path clip-rule=\"evenodd\" d=\"M0 0H40V40H0Z M10 10H30V30H10Z\"/></clipPath>"
				+ Filled("clip-path=\"url(#c)\""));
			await Assert.That(AlphaAt(evenOdd, 55, 5)).IsEqualTo(255);
			await Assert.That(AlphaAt(evenOdd, 70, 20)).IsEqualTo(0);
			await Assert.That(AlphaAt(evenOdd, 5, 5)).IsEqualTo(0);

			// objectBoundingBox: the clip's unit square is the shape's box (here x 50..100, y 0..50).
			ImageBuffer box = Render("<clipPath id=\"c\" clipPathUnits=\"objectBoundingBox\"><rect width=\".5\" height=\".5\"/></clipPath>"
				+ "<rect x=\"50\" width=\"50\" height=\"50\" fill=\"#00ff00\" clip-path=\"url(#c)\"/>");
			await Assert.That(AlphaAt(box, 60, 10)).IsEqualTo(255);
			await Assert.That(AlphaAt(box, 90, 10)).IsEqualTo(0);
			await Assert.That(AlphaAt(box, 60, 40)).IsEqualTo(0);
		}

		[Test]
		public async Task NestedClipPathsIntersect()
		{
			ImageBuffer image = Render("<clipPath id=\"outer\"><rect width=\"50\" height=\"100\"/></clipPath>"
				+ "<clipPath id=\"inner\" clip-path=\"url(#outer)\"><rect width=\"100\" height=\"50\"/></clipPath>"
				+ Filled("clip-path=\"url(#inner)\""));
			await Assert.That(AlphaAt(image, 25, 25)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 75, 25)).IsEqualTo(0);
			await Assert.That(AlphaAt(image, 25, 75)).IsEqualTo(0);
		}

		[Test]
		public async Task AMissingClipIsIgnoredButOneThatClipsItselfDrawsNothing()
		{
			await Assert.That(AlphaAt(Render(Filled("clip-path=\"url(#missing)\"")), 50, 50)).IsEqualTo(255);
			ImageBuffer self = Render("<clipPath id=\"c\" clip-path=\"url(#c)\"><rect width=\"100\" height=\"100\"/></clipPath>" + Filled("clip-path=\"url(#c)\""));
			await Assert.That(AlphaAt(self, 50, 50)).IsEqualTo(0);
		}

		[Test]
		public async Task AMaskScalesByItsLuminanceWithinItsRegion()
		{
			// White keeps everything, black nothing; the default region is the box grown by 10% each side.
			ImageBuffer image = Render("<mask id=\"m\"><rect width=\"50\" height=\"100\" fill=\"white\"/><rect x=\"50\" width=\"50\" height=\"100\" fill=\"black\"/></mask>"
				+ "<rect x=\"10\" y=\"10\" width=\"80\" height=\"80\" fill=\"#00ff00\" mask=\"url(#m)\"/>");
			await Assert.That(AlphaAt(image, 30, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 70, 50)).IsEqualTo(0);

			// A grey of luminance 50% halves the alpha; mask-type alpha uses the mask's alpha instead.
			ImageBuffer grey = Render("<mask id=\"m\"><rect width=\"100\" height=\"100\" fill=\"rgb(128,128,128)\"/></mask>" + Filled("mask=\"url(#m)\""));
			await Assert.That(AlphaAt(grey, 50, 50)).IsEqualTo(128);
			ImageBuffer alpha = Render("<mask id=\"m\" mask-type=\"alpha\"><rect width=\"100\" height=\"100\" fill=\"black\" fill-opacity=\".5\"/></mask>" + Filled("mask=\"url(#m)\""));
			await Assert.That(AlphaAt(alpha, 50, 50)).IsEqualTo(128);
		}

		[Test]
		public async Task MaskUnitsAndContentUnitsPlaceTheMask()
		{
			// userSpaceOnUse region x 0..50: the white content only counts there.
			ImageBuffer region = Render("<mask id=\"m\" maskUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"50\" height=\"100\"><rect width=\"100\" height=\"100\" fill=\"white\"/></mask>"
				+ Filled("mask=\"url(#m)\""));
			await Assert.That(AlphaAt(region, 25, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(region, 75, 50)).IsEqualTo(0);

			// objectBoundingBox content: a unit-square half is half the shape's box.
			ImageBuffer content = Render("<mask id=\"m\" maskContentUnits=\"objectBoundingBox\"><rect width=\".5\" height=\"1\" fill=\"white\"/></mask>"
				+ "<rect x=\"20\" y=\"20\" width=\"60\" height=\"60\" fill=\"#00ff00\" mask=\"url(#m)\"/>");
			await Assert.That(AlphaAt(content, 30, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(content, 70, 50)).IsEqualTo(0);
		}

		[Test]
		public async Task ClipPathsApplyToGroups()
		{
			ImageBuffer image = Render("<clipPath id=\"c\"><rect width=\"50\" height=\"50\"/></clipPath>"
				+ "<g clip-path=\"url(#c)\" transform=\"translate(10 10)\"><rect width=\"100\" height=\"100\" fill=\"#00ff00\"/></g>");
			await Assert.That(AlphaAt(image, 30, 30)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 70, 30)).IsEqualTo(0);
			await Assert.That(AlphaAt(image, 5, 5)).IsEqualTo(0);
		}
	}
}
