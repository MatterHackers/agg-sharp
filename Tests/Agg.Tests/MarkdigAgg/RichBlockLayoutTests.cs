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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichBlockLayoutTests
	{
		private const string Sentence = "The quick brown fox jumps over the lazy dog and keeps on running far away";

		// A fixed scale so the numbers do not depend on the machine's display.
		private static RichLayoutStyle Style() => new RichLayoutStyle(deviceScale: 1);

		private static RichBlock Paragraph(params RichInline[] inlines)
		{
			return new RichBlock { Kind = RichBlockKind.Paragraph, Inlines = new List<RichInline>(inlines) };
		}

		[Test]
		public async Task TextWrapsIntoMoreLinesAsTheWidthShrinks()
		{
			var block = Paragraph(new RichRun(Sentence));
			var style = Style();

			int wide = RichBlockLayout.Layout(block, 5000, style).Lines.Count;
			int medium = RichBlockLayout.Layout(block, 200, style).Lines.Count;
			int narrow = RichBlockLayout.Layout(block, 80, style).Lines.Count;

			await Assert.That(wide).IsEqualTo(1);
			await Assert.That(medium).IsGreaterThan(wide);
			await Assert.That(narrow).IsGreaterThan(medium);

			// Every wrapped line but the last fits; wraps happen at spaces, so no line starts with one.
			var layout = RichBlockLayout.Layout(block, 200, style);
			foreach (var line in layout.Lines)
			{
				await Assert.That(line.Width).IsLessThanOrEqualTo(200);
				await Assert.That(Sentence[line.Start]).IsNotEqualTo(' ');
			}
		}

		[Test]
		public async Task AWordWiderThanTheLineBreaksBetweenCharacters()
		{
			var layout = RichBlockLayout.Layout(Paragraph(new RichRun(new string('W', 60))), 100, Style());

			await Assert.That(layout.Lines.Count).IsGreaterThan(1);
			foreach (var line in layout.Lines)
			{
				await Assert.That(line.Width).IsLessThanOrEqualTo(100);
			}
		}

		[Test]
		public async Task CenteredAndRightAlignedLinesAreOffset()
		{
			const double width = 400;
			var block = Paragraph(new RichRun("Short line"));
			block.Alignment = RichAlignment.Center;
			var centered = RichBlockLayout.Layout(block, width, Style()).Lines[0];
			block.Alignment = RichAlignment.Right;
			var right = RichBlockLayout.Layout(block, width, Style()).Lines[0];

			await Assert.That(centered.Width).IsGreaterThan(0);
			await Assert.That(centered.Left).IsEqualTo((width - centered.Width) / 2).Within(0.001);
			await Assert.That(right.Left).IsEqualTo(width - right.Width).Within(0.001);
			await Assert.That(centered.Fragments[0].X).IsEqualTo(centered.Left).Within(0.001);
		}

		[Test]
		public async Task HeadingIsTallerThanParagraph()
		{
			var paragraph = RichBlockLayout.Layout(Paragraph(new RichRun("Title")), 400, Style()).Lines[0];
			var headingBlock = Paragraph(new RichRun("Title"));
			headingBlock.Kind = RichBlockKind.Heading;
			headingBlock.HeadingLevel = 1;
			var heading = RichBlockLayout.Layout(headingBlock, 400, Style()).Lines[0];

			await Assert.That(heading.Top - heading.Bottom).IsGreaterThan(paragraph.Top - paragraph.Bottom);
			await Assert.That(heading.Width).IsGreaterThan(paragraph.Width);
		}

		[Test]
		public async Task BoldIsWiderThanRegular()
		{
			var regular = RichBlockLayout.Layout(Paragraph(new RichRun("Some words")), 400, Style()).Lines[0];
			var bold = RichBlockLayout.Layout(Paragraph(new RichRun("Some words") { Bold = true }), 400, Style()).Lines[0];

			await Assert.That(bold.Width).IsGreaterThan(regular.Width);
		}

		[Test]
		public async Task ListDepthShiftsTextRightAndShowsTheMarker()
		{
			var style = Style();
			RichBlockLayout ListItem(int depth, bool ordered)
			{
				var block = Paragraph(new RichRun("Item"));
				block.Kind = RichBlockKind.ListItem;
				block.List = new RichListInfo { Depth = depth, Ordered = ordered, Marker = ordered ? '.' : '-' };
				return RichBlockLayout.Layout(block, 400, style, listNumber: 3);
			}

			var paragraph = RichBlockLayout.Layout(Paragraph(new RichRun("Item")), 400, style);
			var top = ListItem(0, false);
			var nested = ListItem(1, false);
			var numbered = ListItem(0, true);

			await Assert.That(top.Lines[0].Left).IsGreaterThan(paragraph.Lines[0].Left);
			await Assert.That(nested.Lines[0].Left - top.Lines[0].Left).IsEqualTo(style.ListIndent).Within(0.001);
			await Assert.That(top.Marker).IsEqualTo("-");
			await Assert.That(numbered.Marker).IsEqualTo("3.");
			await Assert.That(numbered.MarkerX).IsLessThan(numbered.TextLeft);
			await Assert.That(numbered.MarkerX).IsGreaterThanOrEqualTo(0);
		}

		// The shipped UI font has no emoji, so a bare emoji is zero width and its carets coincide with its
		// neighbours'. Tests about emoji give the default face Noto Emoji as a fallback, the way an app does; they
		// run alone because the default face is process-wide.
		[Test]
		[NotInParallel]
		public async Task HitTestOfEveryCaretCenterReturnsItsOffset()
		{
			using var emoji = new EmojiFallback();
			var block = Paragraph(
				new RichRun("Plain words then "),
				new RichRun("bold text") { Bold = true },
				new RichRun(" and "),
				new RichRun("slanted words") { Italic = true },
				new RichRun(" smile \U0001F600\U0001F600 now "),
				new InlineAtom(InlineAtomKind.Image, "![pic](a.png)"),
				new RichRun(" a "),
				new RichRun("link") { LinkUrl = "https://example.com" },
				new InlineAtom(InlineAtomKind.HardBreak, "\\\n"),
				new RichRun("code()") { Code = true },
				new InlineAtom(InlineAtomKind.Html, "<b>"),
				new RichRun(" end of the\tblock"));
			var layout = RichBlockLayout.Layout(block, 120, Style());

			// The emoji are surrogate pairs: no caret between their halves.
			int first = "Plain words then bold text and slanted words smile ".Length;
			await Assert.That(layout.Lines.Count).IsGreaterThan(3);
			await AssertCaretRoundTrips(layout, block.TextLength(), first + 1, first + 3);
		}

		[Test]
		public async Task CodeBlockKeepsSpacesAndBreaksAtNewlines()
		{
			var block = new RichBlock
			{
				Kind = RichBlockKind.CodeBlock,
				CodeText = "int  x = 1;\n\nvar aVeryLongIdentifierThatCannotFitOnOneLineAtAll = 2;\n",
			};
			var layout = RichBlockLayout.Layout(block, 150, Style());

			// Line 1, the empty line, the long line char-wrapped onto at least two, and the empty line after the
			// trailing newline.
			await Assert.That(layout.Lines.Count).IsGreaterThanOrEqualTo(5);
			await Assert.That(layout.Lines[0].Fragments[0].Text).IsEqualTo("int  x = 1;");
			await Assert.That(layout.Lines[1].Start).IsEqualTo(layout.Lines[1].End - 1);
			await Assert.That(layout.CodeBackground).IsNotNull();
			await AssertCaretRoundTrips(layout, block.TextLength());
		}

		[Test]
		public async Task EmptyBlockHasOneLineWithACaret()
		{
			var layout = RichBlockLayout.Layout(Paragraph(), 400, Style());
			var caret = layout.CaretRect(0);

			await Assert.That(layout.Lines.Count).IsEqualTo(1);
			await Assert.That(caret.Height).IsGreaterThan(5);
			await Assert.That(layout.Height).IsGreaterThan(caret.Height);
			await Assert.That(layout.HitTest(new Vector2(200, 0))).IsEqualTo(new RichCaret(0));
			await Assert.That(layout.HitTest(caret.Center)).IsEqualTo(new RichCaret(0));
		}

		[Test]
		public async Task TableBlocksAreLeftToTheTableLayout()
		{
			var block = new RichBlock { Kind = RichBlockKind.Table };

			await Assert.That(() => RichBlockLayout.Layout(block, 400, Style())).Throws<NotSupportedException>();
		}

		[Test]
		[NotInParallel]
		public async Task ClickingJustAfterAnEmojiPutsTheCaretAfterIt()
		{
			using var emoji = new EmojiFallback();
			var layout = RichBlockLayout.Layout(Paragraph(new RichRun("a\U0001F600b")), 400, Style());
			var afterEmoji = layout.CaretRect(3);

			await Assert.That(afterEmoji.Left).IsGreaterThan(layout.CaretRect(1).Left);
			await Assert.That(layout.HitTest(new Vector2(afterEmoji.XCenter + 0.5, afterEmoji.YCenter))).IsEqualTo(new RichCaret(3));
			await Assert.That(layout.HitTest(new Vector2(afterEmoji.XCenter - 0.5, afterEmoji.YCenter))).IsEqualTo(new RichCaret(3));
		}

		[Test]
		public async Task AWordBrokenByCharacterHasACaretAtTheLineEnd()
		{
			var layout = RichBlockLayout.Layout(Paragraph(new RichRun(new string('W', 60))), 100, Style());
			var first = layout.Lines[0];
			var second = layout.Lines[1];

			var clickRight = layout.HitTest(new Vector2(first.Left + first.Width + 30, first.Baseline));
			await Assert.That(clickRight).IsEqualTo(new RichCaret(second.Start, AtLineEnd: true));
			await Assert.That(layout.CaretRect(clickRight).XCenter).IsEqualTo(first.Left + first.Width).Within(0.001);
			await Assert.That(layout.LineOf(second.Start, atLineEnd: true)).IsSameReferenceAs(first);
			await Assert.That(layout.LineOf(second.Start)).IsSameReferenceAs(second);
			await AssertCaretRoundTrips(layout, 60);
		}

		[Test]
		public async Task ABoxPushedDownWithNoSpaceLeavesACaretAtTheLineEnd()
		{
			var style = Style();
			style.ImageSize = _ => new Vector2(80, 10);
			var layout = RichBlockLayout.Layout(Paragraph(new RichRun("word"), new InlineAtom(InlineAtomKind.Image, "![i](i.png)")), 100, style);
			var first = layout.Lines[0];

			await Assert.That(layout.Lines.Count).IsEqualTo(2);
			await Assert.That(layout.HitTest(new Vector2(first.Left + first.Width + 30, first.Baseline))).IsEqualTo(new RichCaret(4, AtLineEnd: true));
			await AssertCaretRoundTrips(layout, 5);
		}

		[Test]
		public async Task TextAtomsWrapLikeWords()
		{
			var style = Style();
			var block = Paragraph(new RichRun("see "), new InlineAtom(InlineAtomKind.Autolink, "<https://x.com>"), new RichRun("."));

			// Narrower than the link and its "." the pair breaks by character like a long word, which may put the
			// "." first; at every width it fits, it stays on the link's line.
			double linkAndDot = 0;
			foreach (var fragment in RichBlockLayout.Layout(block, 1000, style).Lines[0].Fragments)
			{
				linkAndDot += fragment.InlineIndex > 0 ? fragment.Width : 0;
			}

			for (double width = 200; width >= linkAndDot; width -= 1)
			{
				var layout = RichBlockLayout.Layout(block, width, style);
				foreach (var line in layout.Lines)
				{
					// The "." after the autolink (offset 5) never starts a line on its own.
					await Assert.That(line.Start).IsNotEqualTo(5);
				}

				await AssertCaretRoundTrips(layout, 6);
			}

			var longLink = Paragraph(new RichRun("a "), new InlineAtom(InlineAtomKind.Autolink, "<https://example.com/a/very/long/path/that/goes/on>"));
			var longLayout = RichBlockLayout.Layout(longLink, 80, style);
			await Assert.That(longLayout.Lines.Count).IsGreaterThan(2);
			foreach (var line in longLayout.Lines)
			{
				await Assert.That(line.Width).IsLessThanOrEqualTo(80);
			}

			await AssertCaretRoundTrips(longLayout, 3);

			// Every piece of the split link stands for the whole atom at offset 2.
			var linkLength = ((InlineAtom)longLink.Inlines[1]).RawMarkdown.Length;
			var pieces = new List<(RichLayoutLine Line, RichLayoutFragment Piece)>();
			foreach (var line in longLayout.Lines)
			{
				foreach (var fragment in line.Fragments)
				{
					if (fragment.Atom != null)
					{
						pieces.Add((line, fragment));
						await Assert.That(fragment.Start).IsEqualTo(2);
						await Assert.That(fragment.Length).IsEqualTo(1);
					}
				}
			}

			// A click on any piece goes before or after the whole link by which half of it was clicked, on lines
			// with no caret stop of their own too.
			await Assert.That(pieces.Count).IsGreaterThan(2);
			await Assert.That(longLayout.Lines.Exists(line => line.CaretStops.Count == 0)).IsTrue();
			foreach (var (line, piece) in pieces)
			{
				foreach (double x in new[] { piece.X + 0.01, piece.X + piece.Width - 0.01 })
				{
					double fraction = (piece.StartInInline + (x - piece.X) / piece.Width * piece.Text.Length) / linkLength;
					var expected = new RichCaret(fraction < 0.5 ? 2 : 3);
					await Assert.That(longLayout.HitTest(new Vector2(x, line.Baseline))).IsEqualTo(expected);
				}
			}

			var firstPiece = pieces[0];
			var lastPiece = pieces[^1];
			await Assert.That(longLayout.HitTest(new Vector2(firstPiece.Piece.X + 0.01, firstPiece.Line.Baseline))).IsEqualTo(new RichCaret(2));
			await Assert.That(longLayout.HitTest(new Vector2(lastPiece.Piece.X + lastPiece.Piece.Width - 0.01, lastPiece.Line.Baseline))).IsEqualTo(new RichCaret(3));
		}

		[Test]
		public async Task AMarkAfterAWrapSpaceStaysWithIt()
		{
			// "\u0301" is a combining acute accent on the space before it.
			var block = Paragraph(new RichRun("word \u0301word word"));
			for (double width = 200; width >= 10; width -= 1)
			{
				var layout = RichBlockLayout.Layout(block, width, Style());
				foreach (var line in layout.Lines)
				{
					await Assert.That(line.Start).IsNotEqualTo(5);
				}
			}
		}

		[Test]
		public async Task CaretStopsFallOnlyBetweenWholeCharacters()
		{
			// A ZWJ family emoji with a variation selector, two flags, Devanagari with a spacing mark, e + combining acute.
			string text = "a\U0001F468\u200D\U0001F469\u200D\U0001F467\uFE0Fb\U0001F1FA\U0001F1F8\U0001F1EB\U0001F1F7c\u0915\u093Fde\u0301f";
			var layout = RichBlockLayout.Layout(Paragraph(new RichRun(text)), 1000, Style());
			var stops = new HashSet<int>();
			foreach (var line in layout.Lines)
			{
				foreach (var stop in line.CaretStops)
				{
					stops.Add(stop.Caret.Offset);
				}
			}

			// .NET's grapheme segmentation is the independent judge of where characters start.
			var expected = new HashSet<int>(StringInfo.ParseCombiningCharacters(text)) { text.Length };
			await Assert.That(stops.SetEquals(expected)).IsTrue();

			// Narrow enough to break by character, no line starts inside one.
			var narrow = RichBlockLayout.Layout(Paragraph(new RichRun(text)), 1, Style());
			foreach (var line in narrow.Lines)
			{
				await Assert.That(expected.Contains(line.Start)).IsTrue();
			}
		}

		[Test]
		public async Task TabsAdvanceToTheNextFourSpaceStop()
		{
			var style = Style();
			var paragraph = RichBlockLayout.Layout(Paragraph(new RichRun("a\tb\t\tc")), 400, style);
			var code = RichBlockLayout.Layout(new RichBlock { Kind = RichBlockKind.CodeBlock, CodeText = "\tx\ty" }, 400, style);
			foreach (var layout in new[] { paragraph, code })
			{
				var line = layout.Lines[0];
				double tabStop = 4 * line.Fragments[0].Face.GetAdvanceForCharacter(' ');
				for (int i = 1; i < line.CaretStops.Count; i++)
				{
					await Assert.That(line.CaretStops[i].X).IsGreaterThan(line.CaretStops[i - 1].X);
				}

				foreach (var fragment in line.Fragments)
				{
					if (fragment.Text == "\t")
					{
						await Assert.That(fragment.Width).IsGreaterThan(0);
						double end = fragment.X + fragment.Width - line.Left;
						await Assert.That(Math.Abs(end / tabStop - Math.Round(end / tabStop))).IsLessThan(0.0001);
					}
				}
			}
		}

		[Test]
		public async Task TrailingSpaceCaretsStayInsideTheColumn()
		{
			// The clamped stops share one x, so a click reaches only the first of them; that is deliberate (the
			// spaces past the edge are invisible) and why this test checks positions, not the click round trip.
			var block = Paragraph(new RichRun("word" + new string(' ', 40)));
			block.Alignment = RichAlignment.Right;
			var layout = RichBlockLayout.Layout(block, 200, Style());

			for (int offset = 0; offset <= block.TextLength(); offset++)
			{
				await Assert.That(layout.CaretRect(offset).XCenter).IsLessThanOrEqualTo(200);
			}
		}

		[Test]
		public async Task AWideListNumberGrowsTheGutter()
		{
			var block = Paragraph(new RichRun("Item"));
			block.Kind = RichBlockKind.ListItem;
			block.List = new RichListInfo { Ordered = true, Marker = '.' };
			var style = Style();
			var small = RichBlockLayout.Layout(block, 400, style, listNumber: 1);
			var wide = RichBlockLayout.Layout(block, 400, style, listNumber: 1000000);

			await Assert.That(wide.MarkerX).IsGreaterThanOrEqualTo(0);
			await Assert.That(wide.TextLeft).IsGreaterThan(small.TextLeft);
			await Assert.That(() => wide.LineOf(-1)).Throws<ArgumentOutOfRangeException>();
		}

		private sealed class EmojiFallback : IDisposable
		{
			private readonly TypeFace previous = AggContext.DefaultFont.Fallback;

			public EmojiFallback()
			{
				AggContext.DefaultFont.Fallback = EmojiFont.TypeFace;
			}

			public void Dispose()
			{
				AggContext.DefaultFont.Fallback = previous;
			}
		}

		// Every offset but the listed ones (inside a character) has a caret; clicking any caret's centre gives back
		// that caret, line-end carets included.
		private static async Task AssertCaretRoundTrips(RichBlockLayout layout, int length, params int[] offsetsWithoutStops)
		{
			var stops = new List<RichCaretStop>();
			foreach (var line in layout.Lines)
			{
				stops.AddRange(line.CaretStops);
			}

			for (int offset = 0; offset <= length; offset++)
			{
				bool hasStop = stops.Exists(stop => stop.Caret.Offset == offset);
				await Assert.That(hasStop).IsEqualTo(Array.IndexOf(offsetsWithoutStops, offset) < 0);
			}

			foreach (var stop in stops)
			{
				var caret = layout.CaretRect(stop.Caret);
				await Assert.That(layout.HitTest(caret.Center)).IsEqualTo(stop.Caret);
			}

			// Lines run top to bottom and stay inside the block.
			for (int i = 1; i < layout.Lines.Count; i++)
			{
				await Assert.That(layout.Lines[i].Top).IsLessThanOrEqualTo(layout.Lines[i - 1].Bottom + 0.001);
			}

			await Assert.That(layout.Lines[^1].Bottom).IsGreaterThanOrEqualTo(0);
			await Assert.That(layout.Lines[0].Top).IsLessThanOrEqualTo(layout.Height);
		}
	}
}
