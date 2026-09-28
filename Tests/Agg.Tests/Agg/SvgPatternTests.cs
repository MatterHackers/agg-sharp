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
	// agg/Svg, stage 3: the <pattern> paint server. Renders are 100x100 of a 100x100 viewBox, so one user unit is
	// one pixel, and At takes SVG's y-down pixel coordinates.
	public class SvgPatternTests
	{
		// A 20x20 tile: red top-left quarter, blue bottom-right quarter, the rest transparent.
		private const string Checker = "<rect width=\"10\" height=\"10\" fill=\"red\"/><rect x=\"10\" y=\"10\" width=\"10\" height=\"10\" fill=\"blue\"/>";

		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 100 100\">{body}</svg>", 100, 100);
		}

		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		private static bool IsRed(Color c) => c.red == 255 && c.green == 0 && c.blue == 0 && c.alpha == 255;

		private static bool IsBlue(Color c) => c.red == 0 && c.green == 0 && c.blue == 255 && c.alpha == 255;

		[Test]
		public async Task UserSpaceTileRepeatsAcrossTheShape()
		{
			ImageBuffer image = Render($"<pattern id=\"p\" patternUnits=\"userSpaceOnUse\" width=\"20\" height=\"20\">{Checker}</pattern>"
				+ "<rect width=\"100\" height=\"100\" fill=\"url(#p)\"/>");
			foreach ((int x, int y) in new[] { (5, 5), (25, 5), (85, 65), (45, 85) })
			{
				await Assert.That(IsRed(At(image, x, y))).IsTrue().Because($"({x}, {y}) is in a tile's top-left quarter");
				await Assert.That(IsBlue(At(image, x + 10, y + 10))).IsTrue().Because($"({x + 10}, {y + 10}) is in a tile's bottom-right quarter");
				await Assert.That(At(image, x + 10, y).alpha).IsEqualTo((byte)0);
			}
		}

		[Test]
		public async Task TileStartsAtXAndYAndOnlyInsideTheShape()
		{
			ImageBuffer image = Render($"<pattern id=\"p\" patternUnits=\"userSpaceOnUse\" x=\"5\" y=\"5\" width=\"20\" height=\"20\">{Checker}</pattern>"
				+ "<rect x=\"0\" y=\"0\" width=\"50\" height=\"100\" fill=\"url(#p)\"/>");
			await Assert.That(IsRed(At(image, 7, 7))).IsTrue();
			await Assert.That(IsBlue(At(image, 2, 2))).IsTrue().Because("the tile before (5, 5) wraps round to its blue quarter");
			await Assert.That(At(image, 60, 7).alpha).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ObjectBoundingBoxUnitsScaleTheTileToTheShape()
		{
			// The default patternUnits: a quarter of the 40x40 box, with content in user space.
			ImageBuffer image = Render($"<pattern id=\"p\" width=\".5\" height=\".5\">{Checker}</pattern>"
				+ "<rect x=\"20\" y=\"20\" width=\"40\" height=\"40\" fill=\"url(#p)\"/>");
			await Assert.That(IsRed(At(image, 25, 25))).IsTrue();
			await Assert.That(IsBlue(At(image, 35, 35))).IsTrue();
			await Assert.That(IsRed(At(image, 45, 45))).IsTrue();
		}

		[Test]
		public async Task ViewBoxFitsTheContentIntoTheTile()
		{
			// A 40x40 viewBox squeezed into a 20x20 tile halves the checker.
			ImageBuffer image = Render($"<pattern id=\"p\" patternUnits=\"userSpaceOnUse\" width=\"20\" height=\"20\" viewBox=\"0 0 20 20\" patternContentUnits=\"objectBoundingBox\">{Checker}</pattern>"
				+ $"<pattern id=\"q\" patternUnits=\"userSpaceOnUse\" width=\"10\" height=\"10\" viewBox=\"0 0 20 20\">{Checker}</pattern>"
				+ "<rect width=\"100\" height=\"50\" fill=\"url(#p)\"/><rect y=\"50\" width=\"100\" height=\"50\" fill=\"url(#q)\"/>");
			await Assert.That(IsRed(At(image, 5, 5))).IsTrue().Because("with a viewBox patternContentUnits is ignored");
			await Assert.That(IsRed(At(image, 2, 52))).IsTrue();
			await Assert.That(IsBlue(At(image, 7, 57))).IsTrue();
			await Assert.That(IsRed(At(image, 12, 52))).IsTrue();
		}

		[Test]
		public async Task PatternTransformTurnsTheTiles()
		{
			// Turned a quarter about the origin, the tile's red quarter (x 0..10, y 0..10) lands on x -10..0: it
			// shows at x 10..20 of each period, where blue's quarter sits untransformed.
			ImageBuffer image = Render($"<pattern id=\"p\" patternUnits=\"userSpaceOnUse\" width=\"20\" height=\"20\" patternTransform=\"rotate(90)\">{Checker}</pattern>"
				+ "<rect width=\"100\" height=\"100\" fill=\"url(#p)\"/>");
			await Assert.That(IsRed(At(image, 15, 5))).IsTrue();
			await Assert.That(IsBlue(At(image, 5, 15))).IsTrue();
		}

		[Test]
		public async Task HrefInheritsAttributesAndContent()
		{
			ImageBuffer image = Render($"<pattern id=\"base\" patternUnits=\"userSpaceOnUse\" width=\"20\" height=\"20\">{Checker}</pattern>"
				+ "<pattern id=\"p\" xlink:href=\"#base\" x=\"10\"/><rect width=\"100\" height=\"100\" fill=\"url(#p)\"/>");
			await Assert.That(IsRed(At(image, 15, 5))).IsTrue().Because("x=10 moves the inherited tile right");
			await Assert.That(IsBlue(At(image, 5, 15))).IsTrue();
		}

		[Test]
		public async Task FillOpacityScalesThePatternAndStrokesTakeItToo()
		{
			ImageBuffer image = Render($"<pattern id=\"p\" patternUnits=\"userSpaceOnUse\" width=\"20\" height=\"20\">{Checker}</pattern>"
				+ "<rect width=\"50\" height=\"50\" fill=\"url(#p)\" fill-opacity=\".5\"/>"
				+ "<line x1=\"60\" y1=\"5\" x2=\"100\" y2=\"5\" stroke=\"url(#p)\" stroke-width=\"6\"/>");
			Color half = At(image, 5, 5);
			await Assert.That((int)half.alpha).IsEqualTo(128).Within(1);
			await Assert.That((int)half.red).IsGreaterThan(250);
			await Assert.That(IsRed(At(image, 65, 5))).IsTrue();
		}

		[Test]
		public async Task SelfReferencingPatternContentStops()
		{
			ImageBuffer image = Render("<pattern id=\"p\" patternUnits=\"userSpaceOnUse\" width=\"20\" height=\"20\"><rect width=\"10\" height=\"10\" fill=\"url(#p) red\"/></pattern>"
				+ "<rect width=\"100\" height=\"100\" fill=\"url(#p)\"/>");
			await Assert.That(IsRed(At(image, 5, 5))).IsTrue().Because("the innermost use falls back to its paint's colour");
		}
	}
}
