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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// <textPath>: glyphs placed by arc length along a path, turned to its direction.
	public class SvgTextPathTests
	{
		private static List<(VertexStorage Path, SvgStyle Style)> Layout(string defs, string text)
		{
			SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 200 200\">{defs}{text}</svg>");
			SvgElement element = document.Root.Children.Last();
			SvgStyle style = SvgStyle.Compute(element, SvgStyle.Compute(document.Root, null, 200), 200);
			return SvgText.Layout(element, style, 200, 200, 200, document: document);
		}

		[Test]
		public async Task GlyphsFollowAVerticalPathTurnedToItsDirection()
		{
			// Down a vertical line the text reads top to bottom: taller than wide, centred on x = 100.
			RectangleDouble bounds = Layout("<path id=\"p\" d=\"M 100 20 L 100 180\"/>", "<text font-size=\"20\"><textPath xlink:href=\"#p\">Text</textPath></text>")[0].Path.GetBounds();
			await Assert.That(bounds.Height).IsGreaterThan(bounds.Width * 1.5);
			await Assert.That(bounds.Bottom).IsBetween(19, 22);
			await Assert.That((bounds.Left + bounds.Right) / 2).IsBetween(102, 112);
		}

		[Test]
		public async Task StartOffsetMovesAlongThePathAndGlyphsPastItsEndAreHidden()
		{
			string path = "<path id=\"p\" d=\"M 20 100 L 120 100\"/>";
			RectangleDouble Bounds(string offset) => Layout(path, $"<text font-size=\"20\"><textPath xlink:href=\"#p\" startOffset=\"{offset}\">Text</textPath></text>")[0].Path.GetBounds();
			await Assert.That(Bounds("0").Left).IsBetween(20, 22);
			await Assert.That(Bounds("50%").Left).IsBetween(70, 72);

			// At 90 only the T's midpoint is still on the 100-long path.
			RectangleDouble end = Bounds("90");
			await Assert.That(end.Left).IsBetween(110, 112);
			await Assert.That(end.Right).IsLessThan(125);
		}

		[Test]
		public async Task PlacesGlyphsWhereKurbosArcLengthPutsThem()
		{
			// usvg measures each segment as a kurbo cubic (a line gets controls at t = 0.33 and 0.66, so it is not
			// traversed at a uniform speed) and inverts kurbo's arc length only to within 0.5 units. The expected
			// values are kurbo 0.11.3's own, run on these curves.
			SvgTextPath Path(string d)
			{
				SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\"><text><textPath path=\"{d}\">T</textPath></text></svg>");
				return SvgTextPath.Resolve(document.Root.Children.Last().Children.Last(), document, 200, 200, 16);
			}

			await Assert.That(Path("M 0 0 L 100 0").TryPlace(50, out double x, out double y, out double angle)).IsTrue();
			await Assert.That(x).IsEqualTo(49.841138758).Within(1e-6);

			SvgTextPath curve = Path("M 20 100 C 35 135 85 135 100 100");
			await Assert.That(curve.Length).IsEqualTo(101.482351952).Within(1e-6);
			await Assert.That(curve.TryPlace(40, out x, out y, out angle)).IsTrue();
			await Assert.That(x).IsEqualTo(49.571314133).Within(1e-6);
			await Assert.That(y).IsEqualTo(125.028411864).Within(1e-6);
			await Assert.That(angle).IsEqualTo(0.233926413).Within(1e-6);
		}

		[Test]
		public async Task AnInlinePathWinsOverTheHrefAndABrokenOneFallsBackToIt()
		{
			string defs = "<path id=\"p\" d=\"M 20 50 L 180 50\"/>";
			double Baseline(string attributes) => Layout(defs, $"<text font-size=\"20\"><textPath {attributes}>Text</textPath></text>")[0].Path.GetBounds().Top;
			await Assert.That(Baseline("path=\"M 20 150 L 180 150\" xlink:href=\"#p\"")).IsBetween(149, 151);
			await Assert.That(Baseline("path=\"q\" xlink:href=\"#p\"")).IsBetween(49, 51);
		}
	}
}
