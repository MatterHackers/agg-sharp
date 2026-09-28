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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Non-BMP code points (surrogate pairs) and the <see cref="TypeFace.Fallback"/> chain. The emoji face is the
	/// GUI demo's embedded Noto Emoji; the primary is a one-glyph SVG face built here, so no test mutates the
	/// process-wide default fonts.
	/// </summary>
	public class TypeFaceFallbackTests
	{
		// U+1F493 BEATING HEART, a surrogate pair in UTF-16
		private const string Heart = "\uD83D\uDC93";
		private const int HeartCodePoint = 0x1F493;

		private const double PointSize = 12;

		private static TypeFace OnlyAFace()
		{
			return TypeFace.LoadFrom(@"<svg><defs>
<font id=""OnlyA"" horiz-adv-x=""1000"" >
<font-face font-family=""OnlyA"" font-weight=""400"" units-per-em=""2048"" panose-1=""2 11 6 4 2 2 2 2 2 4"" ascent=""1854"" descent=""-434"" x-height=""1082"" cap-height=""1409"" bbox=""0 -434 1366 1854"" underline-thickness=""150"" underline-position=""-217"" unicode-range=""U+0041-U+0041"" />
<missing-glyph horiz-adv-x=""1000"" d=""M0 0z"" />
<glyph glyph-name=""A"" unicode=""A"" horiz-adv-x=""1366"" d=""M0 0L1366 0L683 1409z"" />
</font></defs></svg>");
		}

		[Test]
		public async Task NotoEmojiHasTheMiscLabelsEmojis()
		{
			await Assert.That(EmojiFont.TypeFace.HasGlyph(HeartCodePoint)).IsTrue();
			await Assert.That(EmojiFont.TypeFace.HasGlyph(0x1F31F)).IsTrue(); // glowing star
			await Assert.That(EmojiFont.TypeFace.HasGlyph('A')).IsFalse();
		}

		[Test]
		public async Task CodePointsAreEveryGlyphTheFaceItselfHasInOrder()
		{
			// A TrueType face lists its cmap, surrogate-pair code points included; an SVG face its glyph table.
			var emoji = EmojiFont.TypeFace.CodePoints();
			await Assert.That(emoji).Contains(HeartCodePoint);
			await Assert.That(emoji).DoesNotContain((int)'A');
			await Assert.That(emoji.All(EmojiFont.TypeFace.HasGlyph)).IsTrue();
			await Assert.That(emoji.SequenceEqual(emoji.Distinct().OrderBy(c => c))).IsTrue();

			var onlyA = OnlyAFace();
			onlyA.Fallback = EmojiFont.TypeFace;
			await Assert.That(onlyA.CodePoints().SequenceEqual(new[] { (int)'A' })).IsTrue();
		}

		[Test]
		public async Task SurrogatePairResolvesToTheFallbacksGlyph()
		{
			var primary = OnlyAFace();
			primary.Fallback = EmojiFont.TypeFace;
			var styled = new StyledTypeFace(primary, PointSize);
			var emojiStyled = new StyledTypeFace(EmojiFont.TypeFace, PointSize);

			var viaFallback = styled.GetGlyphForCodePoint(HeartCodePoint).Vertices().ToList();
			var direct = emojiStyled.GetGlyphForCodePoint(HeartCodePoint).Vertices().ToList();

			await Assert.That(viaFallback.Count).IsGreaterThan(2);
			await Assert.That(viaFallback.SequenceEqual(direct)).IsTrue();

			// The printer draws the pair as that one glyph
			var printed = new TypeFacePrinter(Heart, styled).Vertices().Where(v => v.Command != FlagsAndCommand.Stop).ToList();
			await Assert.That(printed.Count).IsEqualTo(direct.Count(v => v.Command != FlagsAndCommand.Stop));
		}

		[Test]
		public async Task SurrogatePairMeasuresOneAdvance()
		{
			var primary = OnlyAFace();
			primary.Fallback = EmojiFont.TypeFace;
			var styled = new StyledTypeFace(primary, PointSize);

			double heartAdvance = styled.GetAdvanceForCodePoint(HeartCodePoint);
			double aAdvance = styled.GetAdvanceForCharacter('A');
			double expectedHeart = EmojiFont.TypeFace.GetAdvanceForCodePoint(HeartCodePoint) * styled.EmSizeInPixels / EmojiFont.TypeFace.UnitsPerEm;
			await Assert.That(heartAdvance).IsEqualTo(expectedHeart);
			await Assert.That(heartAdvance).IsGreaterThan(0);

			var printer = new TypeFacePrinter("A" + Heart + "A", styled);
			await Assert.That(printer.GetSize().X).IsEqualTo(aAdvance + heartAdvance + aAdvance).Within(1e-9);

			// The caret offset after the pair is one advance on, and the trailing half adds nothing
			await Assert.That(printer.GetOffsetLeftOfCharacterIndex(1).X).IsEqualTo(aAdvance).Within(1e-9);
			await Assert.That(printer.GetOffsetLeftOfCharacterIndex(3).X).IsEqualTo(aAdvance + heartAdvance).Within(1e-9);
			await Assert.That(printer.GetOffsetLeftOfCharacterIndex(4).X).IsEqualTo(aAdvance + heartAdvance + aAdvance).Within(1e-9);
		}

		[Test]
		public async Task TwoEmojisSharingAHighSurrogateMeasureTheirOwnAdvances()
		{
			// GetOffset memoises advances per char; a high surrogate alone does not name the glyph
			var primary = OnlyAFace();
			primary.Fallback = EmojiFont.TypeFace;
			var styled = new StyledTypeFace(primary, PointSize);
			const string Star = "\uD83C\uDF1F"; // U+1F31F
			const string Grin = "\uD83D\uDE00"; // U+1F600, same high surrogate as the heart

			var printer = new TypeFacePrinter(Heart + Grin + Star, styled);
			double expected = styled.GetAdvanceForCodePoint(HeartCodePoint) + styled.GetAdvanceForCodePoint(0x1F600) + styled.GetAdvanceForCodePoint(0x1F31F);

			await Assert.That(printer.GetOffsetLeftOfCharacterIndex(6).X).IsEqualTo(expected).Within(1e-9);
			await Assert.That(printer.GetSize().X).IsEqualTo(expected).Within(1e-9);
		}

		[Test]
		public async Task WrappingNeverSplitsASurrogatePair()
		{
			var primary = OnlyAFace();
			primary.Fallback = EmojiFont.TypeFace;
			var wrapping = new EnglishTextWrapping(new StyledTypeFace(primary, PointSize));

			// Narrower than one emoji, so every glyph goes on its own line
			var lines = wrapping.WrapSingleLineOnWidth(Heart + Heart, 1);

			await Assert.That(string.Join("|", lines)).IsEqualTo(Heart + "|" + Heart);
		}

		[Test]
		public async Task WithoutAFallbackAMissingPairStillDrawsNothing()
		{
			var styled = new StyledTypeFace(OnlyAFace(), PointSize);

			var printer = new TypeFacePrinter(Heart, styled);

			await Assert.That(printer.GetSize().X).IsEqualTo(0);
			await Assert.That(printer.Vertices().Count(v => v.Command != FlagsAndCommand.Stop)).IsEqualTo(0);
		}

		[Test]
		public async Task AFallbackDoesNotChangeGlyphsThePrimaryHas()
		{
			var plain = new StyledTypeFace(OnlyAFace(), PointSize);
			var chained = OnlyAFace();
			chained.Fallback = EmojiFont.TypeFace;
			var withFallback = new StyledTypeFace(chained, PointSize);

			var plainVertices = new TypeFacePrinter("AAA", plain).Vertices().ToList();
			var chainedVertices = new TypeFacePrinter("AAA", withFallback).Vertices().ToList();

			await Assert.That(chainedVertices.SequenceEqual(plainVertices)).IsTrue();
		}

		[Test]
		public async Task AFallbackCycleEndsTheSearch()
		{
			var first = OnlyAFace();
			var second = OnlyAFace();
			first.Fallback = second;
			second.Fallback = first;

			var styled = new StyledTypeFace(first, PointSize);

			await Assert.That(styled.GetAdvanceForCodePoint(HeartCodePoint)).IsEqualTo(0);
		}
	}
}
