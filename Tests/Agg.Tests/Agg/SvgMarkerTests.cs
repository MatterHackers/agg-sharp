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
	// agg/Svg: markers, placed and oriented as resvg places them. Renders are 100x100 of a 100x100 viewBox.
	public class SvgMarkerTests
	{
		/// <summary>A 4 by 4 square marker centred on its vertex, one user unit per unit, unclipped.</summary>
		private const string Square = "<marker id=\"m\" markerUnits=\"userSpaceOnUse\" markerWidth=\"4\" markerHeight=\"4\" refX=\"2\" refY=\"2\">"
			+ "<rect width=\"4\" height=\"4\" fill=\"#00ff00\"/></marker>";

		/// <summary>A 10-long, 2-wide bar from its vertex along the marker's +x: shows which way a marker faces.</summary>
		private static string Bar(string orient) => $"<marker id=\"b\" markerUnits=\"userSpaceOnUse\" markerWidth=\"10\" markerHeight=\"2\" refY=\"1\" orient=\"{orient}\">"
			+ "<rect width=\"10\" height=\"2\" fill=\"#00ff00\"/></marker>";

		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{body}</svg>", 100, 100);
		}

		/// <summary>The alpha at SVG coordinates (x, y), y down from the top as SVG counts it.</summary>
		private static int AlphaAt(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y).alpha;

		[Test]
		public async Task StartMidAndEndMarkersDrawAtTheirVertices()
		{
			ImageBuffer all = Render(Square + "<path d=\"M10 50 L50 50 L90 50\" fill=\"none\" marker-start=\"url(#m)\" marker-mid=\"url(#m)\" marker-end=\"url(#m)\"/>");
			await Assert.That(AlphaAt(all, 10, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(all, 50, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(all, 90, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(all, 30, 50)).IsEqualTo(0);

			ImageBuffer endOnly = Render(Square + "<path d=\"M10 50 L50 50 L90 50\" fill=\"none\" marker-end=\"url(#m)\"/>");
			await Assert.That(AlphaAt(endOnly, 10, 50)).IsEqualTo(0);
			await Assert.That(AlphaAt(endOnly, 50, 50)).IsEqualTo(0);
			await Assert.That(AlphaAt(endOnly, 90, 50)).IsEqualTo(255);
		}

		[Test]
		public async Task TheMarkerShorthandInCssSetsAllThreeAndIsInherited()
		{
			ImageBuffer image = Render("<style>g { marker: url(#m) }</style>" + Square + "<g><polyline points=\"10 50 50 50 90 50\" fill=\"none\"/></g>");
			await Assert.That(AlphaAt(image, 10, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 50, 50)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 90, 50)).IsEqualTo(255);
		}

		[Test]
		public async Task OrientAutoFollowsThePathAndAutoStartReverseFlipsTheStart()
		{
			// The path runs down: auto turns the bar down, auto-start-reverse turns the start's up.
			ImageBuffer auto = Render(Bar("auto") + "<path d=\"M50 30 L50 70\" fill=\"none\" marker-start=\"url(#b)\"/>");
			await Assert.That(AlphaAt(auto, 50, 36)).IsEqualTo(255);
			await Assert.That(AlphaAt(auto, 50, 24)).IsEqualTo(0);
			await Assert.That(AlphaAt(auto, 56, 30)).IsEqualTo(0);

			ImageBuffer reversed = Render(Bar("auto-start-reverse") + "<path d=\"M50 30 L50 70\" fill=\"none\" marker-start=\"url(#b)\"/>");
			await Assert.That(AlphaAt(reversed, 50, 24)).IsEqualTo(255);
			await Assert.That(AlphaAt(reversed, 50, 36)).IsEqualTo(0);

			// A fixed angle ignores the path: a quarter turn points down whichever way the path goes.
			ImageBuffer angle = Render(Bar("0.25turn") + "<path d=\"M50 30 L90 30\" fill=\"none\" marker-start=\"url(#b)\"/>");
			await Assert.That(AlphaAt(angle, 50, 36)).IsEqualTo(255);
			await Assert.That(AlphaAt(angle, 56, 30)).IsEqualTo(0);
		}

		[Test]
		public async Task AMidMarkerBisectsTheCornerItSitsOn()
		{
			// In along +x, out along +y: the bar points down-right, at 45 degrees.
			ImageBuffer image = Render(Bar("auto") + "<path d=\"M10 20 L50 20 L50 60\" fill=\"none\" marker-mid=\"url(#b)\"/>");
			await Assert.That(AlphaAt(image, 55, 25)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 57, 20)).IsEqualTo(0);
			await Assert.That(AlphaAt(image, 50, 27)).IsEqualTo(0);
		}

		[Test]
		public async Task StrokeWidthUnitsScaleTheMarkerAndOverflowIsClippedToItsSize()
		{
			// Default markerUnits (strokeWidth) at stroke-width 4 make the default 3x3 marker 12x12; the content
			// runs 30 wide but is clipped to the marker's width.
			ImageBuffer image = Render("<marker id=\"s\"><rect width=\"30\" height=\"3\" fill=\"#00ff00\"/></marker>"
				+ "<path d=\"M20 20 L20 90\" stroke-width=\"4\" fill=\"none\" marker-start=\"url(#s)\"/>");
			await Assert.That(AlphaAt(image, 30, 30)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 36, 26)).IsEqualTo(0);
			await Assert.That(AlphaAt(image, 30, 34)).IsEqualTo(0);

			ImageBuffer visible = Render("<marker id=\"s\" overflow=\"visible\"><rect width=\"30\" height=\"3\" fill=\"#00ff00\"/></marker>"
				+ "<path d=\"M20 20 L20 90\" stroke-width=\"4\" fill=\"none\" marker-start=\"url(#s)\"/>");
			await Assert.That(AlphaAt(visible, 60, 26)).IsEqualTo(255);
		}

		[Test]
		public async Task AViewBoxFitsTheContentToTheMarkerSize()
		{
			ImageBuffer image = Render("<marker id=\"v\" markerUnits=\"userSpaceOnUse\" markerWidth=\"20\" markerHeight=\"20\" viewBox=\"0 0 1 1\">"
				+ "<rect width=\"1\" height=\"1\" fill=\"#00ff00\"/></marker>"
				+ "<path d=\"M40 40 L90 90\" fill=\"none\" marker-start=\"url(#v)\"/>");
			await Assert.That(AlphaAt(image, 58, 58)).IsEqualTo(255);
			await Assert.That(AlphaAt(image, 62, 50)).IsEqualTo(0);
		}

		[Test]
		public async Task AMarkerWithoutSizeOrThatUsesItselfDoesNotHang()
		{
			ImageBuffer zero = Render("<marker id=\"z\" markerWidth=\"0\"><rect width=\"3\" height=\"3\"/></marker>"
				+ "<path d=\"M40 40 L90 90\" fill=\"none\" marker-start=\"url(#z)\"/>");
			await Assert.That(AlphaAt(zero, 41, 41)).IsEqualTo(0);

			ImageBuffer recursive = Render("<marker id=\"r\" markerUnits=\"userSpaceOnUse\" markerWidth=\"10\" markerHeight=\"10\" overflow=\"visible\">"
				+ "<path d=\"M0 0 L5 5\" stroke=\"#00ff00\" marker-start=\"url(#r)\"/><rect width=\"4\" height=\"4\" fill=\"#00ff00\"/></marker>"
				+ "<path d=\"M40 40 L90 90\" fill=\"none\" marker-start=\"url(#r)\"/>");
			await Assert.That(AlphaAt(recursive, 42, 42)).IsEqualTo(255);
		}
	}
}
