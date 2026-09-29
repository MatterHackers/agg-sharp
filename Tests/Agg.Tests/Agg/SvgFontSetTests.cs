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

using System.IO;
using System.Linq;
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
	// SvgFontSet's CSS font matching, over the Liberation Serif, Cascadia Code and Noto Sans faces the AggSharpDemo
	// assembly embeds (its monospace face is Cascadia Code, agg-gui's code font; it no longer embeds Liberation Mono).
	public class SvgFontSetTests
	{
		private static byte[] Font(string file)
		{
			using Stream stream = typeof(SvgTestSample).Assembly.GetManifestResourceStream("MatterHackers.AggSharpDemo.Fonts." + file)
				?? throw new FileNotFoundException($"AggSharpDemo.csproj does not embed the font '{file}'.");
			var memory = new MemoryStream();
			stream.CopyTo(memory);
			return memory.ToArray();
		}

		private static SvgFontSet Fonts()
		{
			var fonts = new SvgFontSet { Serif = "Liberation Serif", SansSerif = "Noto Sans", Monospace = "Cascadia Code" };
			foreach (string file in new[] { "LiberationSerif-Regular.ttf", "LiberationSerif-Italic.ttf", "CascadiaCode.ttf", "NotoSans-Regular.ttf" })
			{
				fonts.Add(Font(file));
			}

			return fonts;
		}

		[Test]
		public async Task StyleAndGenericFamiliesPickTheMatchingFace()
		{
			SvgFontSet fonts = Fonts();
			TypeFace regular = fonts.Match(new[] { "Liberation Serif" }, 400, "normal", 5);
			TypeFace italic = fonts.Match(new[] { "Liberation Serif" }, 400, "italic", 5);
			await Assert.That(italic).IsNotSameReferenceAs(regular);

			// Oblique takes the italic face when there is no oblique one; bold takes the nearest weight there is.
			await Assert.That(fonts.Match(new[] { "serif" }, 700, "oblique", 5)).IsSameReferenceAs(italic);
			await Assert.That(fonts.Match(new[] { "monospace" }, 400, "normal", 5)).IsNotSameReferenceAs(regular);

			// The first family the set has wins; a list with none of them falls back to serif, as usvg's does.
			await Assert.That(fonts.Match(new[] { "Missing", "sans-serif", "serif" }, 400, "normal", 5)).IsSameReferenceAs(fonts.Match(new[] { "Noto Sans" }, 400, "normal", 5));
			await Assert.That(fonts.Match(new[] { "Missing" }, 400, "normal", 5)).IsSameReferenceAs(regular);
		}

		[Test]
		public async Task BolderAndLighterStepAsUsvgDoes()
		{
			int Weight(string outer, string inner)
			{
				SvgDocument document = SvgDocument.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\"><g font-weight=\"{outer}\"><text font-weight=\"{inner}\">a</text></g></svg>");
				SvgElement group = document.Root.Children[0];
				SvgStyle root = SvgStyle.Compute(document.Root, null, 100);
				return SvgStyle.Compute(group.Children[0], SvgStyle.Compute(group, root, 100), 100).FontWeight;
			}

			await Assert.That(Weight("normal", "bolder")).IsEqualTo(700);
			await Assert.That(Weight("normal", "lighter")).IsEqualTo(200);
			await Assert.That(Weight("800", "bolder")).IsEqualTo(900);
			await Assert.That(Weight("900", "bolder")).IsEqualTo(900);
			await Assert.That(Weight("300", "650")).IsEqualTo(300);
		}

		[Test]
		public async Task TextIsDrawnInTheDocumentsMatchedFace()
		{
			// Monospace draws each of "iii" at the mono face's advance, much wider than a proportional face's "i".
			SvgFontSet fonts = Fonts();
			TypeFace mono = fonts.Match(new[] { "monospace" }, 400, "normal", 5);
			SvgDocument document = SvgDocument.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 200\"><text x=\"0\" y=\"100\" font-size=\"50\" font-family=\"monospace\">iii</text></svg>");
			SvgElement element = document.Root.Children[0];
			SvgStyle style = SvgStyle.Compute(element, SvgStyle.Compute(document.Root, null, 200), 200);
			double right = SvgText.Layout(element, style, 200, 200, 200, null, fonts).Last().Path.GetBounds().Right;
			double advance = mono.GetAdvanceForCodePoint('i') * 50.0 / mono.UnitsPerEm;
			await Assert.That(right).IsGreaterThan(2 * advance);
		}

		[Test]
		public async Task AGlyphWithNoOutlineReadsAsAnEmptyPath()
		{
			// A colour-bitmap glyph (Noto Color Emoji's CBDT) or a CFF glyph has no TrueType points or contours;
			// TypeFace.GetGlyph reads every glyph through here, so text in such a font used to throw.
			var storage = new VertexStorage();
			Typography.OpenFont.IGlyphReaderExtensions.Read(new VertexSourceGlyphTranslator(storage), (Typography.OpenFont.GlyphPointF[])null, null);
			await Assert.That(storage.Count).IsEqualTo(0);
		}
	}
}
