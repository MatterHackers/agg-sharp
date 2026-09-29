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
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg <text> metrics as usvg reads them from the font's OS/2 and post tables, in Noto Sans (the resvg
	// suite's face): ySubscriptYOffset 75, ySuperscriptYOffset 350, yStrikeoutPosition 322, post underline -100 / 50.
	// Bounds are in SVG's y-down user space, so a run's Top is its lowest point.
	public class SvgTextMetricsTests
	{
		private static List<(VertexStorage Path, SvgStyle Style)> Layout(string text)
		{
			SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\" font-family=\"Noto Sans\">{text}</svg>");
			SvgElement element = document.Root.Children[0];
			SvgStyle style = SvgStyle.Compute(element, SvgStyle.Compute(document.Root, null, 200), 200);
			return SvgText.Layout(element, style, 200, 200, 200, SvgTestSample.ResolveFont);
		}

		private static TypeFace Noto => SvgTestSample.ResolveFont("Noto Sans", false);

		[Test]
		public async Task TypeFaceReadsTheOs2AndPostMetrics()
		{
			await Assert.That(Noto.SubscriptYOffset).IsEqualTo(75);
			await Assert.That(Noto.SuperscriptYOffset).IsEqualTo(350);
			await Assert.That(Noto.StrikeoutPosition).IsEqualTo(322);
			await Assert.That(Noto.PostUnderlineThickness).IsEqualTo(50);
			await Assert.That(Noto.OS2XHeight).IsEqualTo(536);
		}

		[Test]
		public async Task SubAndSuperMoveByTheFontsScriptOffsetsInTheInnermostSpansFont()
		{
			double Top(string tspan) => Layout($"<text x=\"20\" y=\"100\" font-size=\"40\">{tspan}</text>")[0].Path.GetBounds().Top;
			double plain = Top("<tspan>x</tspan>");
			await Assert.That(plain - Top("<tspan baseline-shift=\"super\">x</tspan>")).IsEqualTo(350 * 40 / 1000.0).Within(1e-6);
			await Assert.That(Top("<tspan baseline-shift=\"sub\">x</tspan>") - plain).IsEqualTo(75 * 40 / 1000.0).Within(1e-6);

			// Two supers, the inner at 20: both measured at the inner span's 20, as usvg does.
			double inner = Layout("<text x=\"20\" y=\"100\" font-size=\"20\">x</text>")[0].Path.GetBounds().Top;
			double nested = Top("<tspan baseline-shift=\"super\"><tspan font-size=\"20\" baseline-shift=\"super\">x</tspan></tspan>");
			await Assert.That(inner - nested).IsEqualTo(2 * 350 * 20 / 1000.0).Within(1e-6);
		}

		[Test]
		public async Task KerningZeroTurnsThePairKerningOff()
		{
			double Width(string attributes) => Layout($"<text x=\"20\" y=\"100\" font-size=\"48\" {attributes}>AVA</text>")[0].Path.GetBounds().Width;
			double kerningOff = 2 * Noto.GetKerningForCodePoints('A', 'V') * 48.0 / Noto.UnitsPerEm;
			await Assert.That(kerningOff).IsLessThan(-1);
			await Assert.That(Width("kerning=\"0\"") - Width("")).IsEqualTo(-kerningOff).Within(1e-6);
			await Assert.That(Width("font-kerning=\"none\"")).IsEqualTo(Width("kerning=\"0\"")).Within(1e-6);
		}

		[Test]
		public async Task LetterSpacingSkipsTheChunksLastGlyphAndItsPercentIsOfTheDiagonal()
		{
			// The underline spans the advances: two H's with one 10-unit gap between, none after the last.
			double H = Noto.GetAdvanceForCodePoint('H') * 40.0 / Noto.UnitsPerEm;
			var runs = Layout("<text x=\"20\" y=\"100\" font-size=\"40\" letter-spacing=\"10\" text-decoration=\"underline\">HH</text>");
			await Assert.That(runs[0].Path.GetBounds().Width).IsEqualTo(2 * H + 10).Within(1e-6);

			// 5% of a 200 x 200 viewport's diagonal is 10.
			runs = Layout("<text x=\"20\" y=\"100\" font-size=\"40\" letter-spacing=\"5%\" text-decoration=\"underline\">HH</text>");
			await Assert.That(runs[0].Path.GetBounds().Width).IsEqualTo(2 * H + 10).Within(1e-6);
		}

		[Test]
		public async Task DecorationsUseThePostAndOs2MetricsAndDrawInUsvgsOrder()
		{
			var runs = Layout("<text x=\"20\" y=\"100\" font-size=\"100\" text-decoration=\"line-through overline underline\">H</text>");
			await Assert.That(runs.Count).IsEqualTo(4);

			// Overline and underline before the glyphs, line-through after, each post's 50 units (5) thick.
			RectangleDouble overline = runs[0].Path.GetBounds(), underline = runs[1].Path.GetBounds(), lineThrough = runs[3].Path.GetBounds();
			await Assert.That(overline.YCenter).IsEqualTo(100 - Noto.Ascent / 10.0).Within(1e-6);
			await Assert.That(underline.YCenter).IsEqualTo(110).Within(1e-6);
			await Assert.That(lineThrough.YCenter).IsEqualTo(100 - 32.2).Within(1e-6);
			await Assert.That(lineThrough.Height).IsEqualTo(5).Within(1e-6);
		}

		[Test]
		public async Task ADecorationDrawsInTheStyleOfTheElementThatDeclaresIt()
		{
			// resvg's style-resolving-1: the <text> declares it, so its yellow fill wins over the tspan's blue.
			var runs = Layout("<text x=\"20\" y=\"100\" font-size=\"40\" fill=\"yellow\" text-decoration=\"line-through\"><tspan fill=\"blue\">Text</tspan></text>");
			await Assert.That(runs[0].Style.Fill.Color).IsEqualTo(Color.Blue);
			await Assert.That(runs[1].Style.Fill.Color).IsEqualTo(new Color(255, 255, 0));

			// Declared again on the tspan, the tspan's own fill draws it.
			runs = Layout("<text x=\"20\" y=\"100\" font-size=\"40\" fill=\"yellow\" text-decoration=\"line-through\"><tspan fill=\"blue\" text-decoration=\"line-through\">Text</tspan></text>");
			await Assert.That(runs[1].Style.Fill.Color).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task FillRuleDoesNotApplyToGlyphs()
		{
			var runs = Layout("<text x=\"20\" y=\"100\" font-size=\"40\" fill-rule=\"evenodd\">O</text>");
			await Assert.That(runs[0].Style.FillEvenOdd).IsFalse();
		}

		[Test]
		public async Task TextLengthZeroSpreadsTheGlyphsBackOverEachOther()
		{
			// usvg's spacing: each advance becomes its width plus (0 - total width) / 3, the last included.
			// Each step is H - 4H / 3 = -H / 3, so the fourth H lands a whole advance left of the first.
			double H = Noto.GetAdvanceForCodePoint('H') * 40.0 / Noto.UnitsPerEm;
			double spread = Layout("<text x=\"20\" y=\"100\" font-size=\"40\" textLength=\"0\">HHHH</text>")[0].Path.GetBounds().Left;
			double fourth = Layout(FormattableString.Invariant($"<text x=\"{20 - H}\" y=\"100\" font-size=\"40\">H</text>"))[0].Path.GetBounds().Left;
			await Assert.That(spread).IsEqualTo(fourth).Within(1e-6);
		}
	}
}
