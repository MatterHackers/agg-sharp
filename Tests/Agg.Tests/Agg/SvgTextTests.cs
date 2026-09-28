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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg, stage 4: <text> laid out by SvgText in a 200x200 viewBox. Bounds are in SVG's y-down user space,
	// so a run's Top is its lowest point.
	public class SvgTextTests
	{
		private static List<(VertexStorage Path, SvgStyle Style)> Layout(string text)
		{
			SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\">{text}</svg>");
			SvgElement element = document.Root.Children[0];
			SvgStyle style = SvgStyle.Compute(element, SvgStyle.Compute(document.Root, null, 200), 200);
			return SvgText.Layout(element, style, 200, 200, 200);
		}

		[Test]
		public async Task TspansCollapseWhitespaceAcrossTheirBoundaries()
		{
			// resvg's text/tspan/sequential: the runs are "Text " and "Text", one space between, none around.
			var runs = Layout("<text x=\"33\" y=\"100\" font-size=\"32\" fill=\"red\">\n  <tspan fill=\"green\">\n    Text\n  </tspan>\n  <tspan fill=\"blue\">\n    Text\n  </tspan>\n</text>");
			await Assert.That(runs.Count).IsEqualTo(2);
			await Assert.That(runs[0].Style.Fill.Color).IsEqualTo(new Color(0, 128, 0));
			await Assert.That(runs[1].Style.Fill.Color).IsEqualTo(Color.Blue);

			// "T" starts within a unit of the pen, and the runs' "Text"s are the same outline a whole word apart.
			RectangleDouble first = runs[0].Path.GetBounds();
			RectangleDouble second = runs[1].Path.GetBounds();
			await Assert.That(first.Left).IsBetween(33, 34);
			await Assert.That(second.Width).IsEqualTo(first.Width).Within(1e-6);
			await Assert.That(second.Left - first.Right).IsBetween(8, 14);
			await Assert.That(first.Top).IsEqualTo(100).Within(.5);
		}

		[Test]
		public async Task TextAnchorShiftsTheChunkByHalfOrAllOfItsAdvance()
		{
			double Left(string anchor) => Layout($"<text x=\"100\" y=\"100\" font-size=\"48\" text-anchor=\"{anchor}\">Text</text>")[0].Path.GetBounds().Left;
			double start = Left("start"), middle = Left("middle"), end = Left("end");
			await Assert.That(start - end).IsGreaterThan(80);
			await Assert.That(start - middle).IsEqualTo((start - end) / 2).Within(1e-6);
		}

		[Test]
		public async Task PositionListsPlaceEachCharacter()
		{
			// Absolute x/y per character start new chunks; dx moves the second on from its x.
			var runs = Layout("<text x=\"10 50\" y=\"20 60\" dx=\"0 5\" font-size=\"20\">HH</text>");
			await Assert.That(runs.Count).IsEqualTo(2);
			RectangleDouble second = runs[1].Path.GetBounds();
			await Assert.That(second.Left).IsBetween(55, 57);
			await Assert.That(second.Top).IsEqualTo(60).Within(.01);
			await Assert.That(runs[0].Path.GetBounds().Top).IsEqualTo(20).Within(.01);
		}

		[Test]
		public async Task UnderlineIsDrawnFirstAcrossTheRunBelowTheBaseline()
		{
			var runs = Layout("<text x=\"50\" y=\"100\" font-size=\"48\" text-decoration=\"underline\">Text</text>");
			await Assert.That(runs.Count).IsEqualTo(2);
			RectangleDouble underline = runs[0].Path.GetBounds();
			RectangleDouble glyphs = runs[1].Path.GetBounds();
			await Assert.That(underline.Bottom).IsGreaterThan(100);
			await Assert.That(underline.Left).IsEqualTo(50);
			await Assert.That(underline.Right).IsGreaterThan(glyphs.Right - 1);
		}

		[Test]
		public async Task BoldWeightDrawsTheWiderBoldFace()
		{
			double Width(string weight) => Layout($"<text x=\"10\" y=\"100\" font-size=\"48\" font-weight=\"{weight}\">Text</text>")[0].Path.GetBounds().Width;
			await Assert.That(Width("bold")).IsGreaterThan(Width("normal") + 2);
			await Assert.That(Width("700")).IsEqualTo(Width("bold"));
		}

		[Test]
		public async Task TheFontResolverIsOfferedEachFamilyInTurnAndItsFaceIsDrawn()
		{
			const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><text x=\"5\" y=\"60\" font-size=\"40\" font-family=\"'Missing', Custom, sans-serif\">Te</text></svg>";
			var offered = new List<(string Family, bool Bold)>();
			SvgDocument document = SvgDocument.Parse(Svg);
			document.FontResolver = (family, bold) =>
			{
				offered.Add((family, bold));
				return family == "Custom" ? global::MatterHackers.Agg.Font.LiberationSansBoldFont.Instance : null;
			};

			ImageBuffer resolved = SvgRenderer.RenderToImage(document, 100, 100);
			await Assert.That(offered).IsEquivalentTo(new[] { ("Missing", false), ("Custom", false) }, CollectionOrdering.Matching);

			// The resolved (bold) face covers more than the default regular one.
			int Covered(ImageBuffer image) => Enumerable.Range(0, 100).SelectMany(x => Enumerable.Range(0, 100).Select(y => image.GetPixel(x, y).alpha)).Count(a => a > 128);
			await Assert.That(Covered(resolved)).IsGreaterThan(Covered(SvgDocument.RenderToImage(Svg, 100, 100)) + 20);
		}

		[Test]
		public async Task KernedPairsAreDrawnCloserAsTheFontKernsThem()
		{
			// Noto Sans (the SVG Test samples' face) kerns "Te" through GPOS, as resvg's shaper applies it.
			TypeFace noto = SvgTestSample.ResolveFont("Noto Sans", false);
			int kerning = noto.GetKerningForCodePoints('T', 'e');
			await Assert.That(kerning).IsEqualTo(-70);
			await Assert.That(noto.GetKerningForCodePoints('H', 'H')).IsEqualTo(0);

			// "e" is in no ClassDef1 range, so it is class 0 - whose row kerns "ex" by -20 (fontTools reads the same).
			await Assert.That(noto.GetKerningForCodePoints('e', 'x')).IsEqualTo(-20);

			// "Te" in one chunk against the same letters in two chunks, "e" placed at T's unkerned advance.
			double Right(string content)
			{
				SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\"><text y=\"100\" font-size=\"100\" font-family=\"Noto Sans\">{content}</text></svg>");
				SvgElement element = document.Root.Children[0];
				SvgStyle style = SvgStyle.Compute(element, SvgStyle.Compute(document.Root, null, 200), 200);
				return SvgText.Layout(element, style, 200, 200, 200, SvgTestSample.ResolveFont).Last().Path.GetBounds().Right;
			}

			double scale = 100.0 / noto.UnitsPerEm;
			double unkerned = Right($"<tspan x=\"0\">T</tspan><tspan x=\"{noto.GetAdvanceForCodePoint('T') * scale}\">e</tspan>");
			await Assert.That(Right("<tspan x=\"0\">Te</tspan>")).IsEqualTo(unkerned + kerning * scale).Within(1e-6);
		}

		[Test]
		public async Task TextIsFilledByTheRenderer()
		{
			ImageBuffer image = SvgDocument.RenderToImage("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><text x=\"10\" y=\"80\" font-size=\"80\" fill=\"red\">I</text></svg>", 100, 100);

			// The "I" stem's middle, a few units right of the pen, halfway up the cap height.
			int stemX = (int)Layout("<text x=\"10\" y=\"80\" font-size=\"80\">I</text>")[0].Path.GetBounds().XCenter;
			Color stem = image.GetPixel(stemX, image.Height - 1 - 50);
			await Assert.That(stem).IsEqualTo(Color.Red);
		}
	}
}
