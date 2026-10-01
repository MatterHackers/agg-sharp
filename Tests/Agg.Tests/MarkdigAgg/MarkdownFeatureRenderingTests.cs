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

using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg;
using Markdig.Renderers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class MarkdownFeatureRenderingTests
	{
		[Test]
		public async Task HeadingLevelsUseDistinctSizes()
		{
			var root = RenderMarkdown(
				"""
				# Heading 1
				## Heading 2
				### Heading 3
				""");

			var headings = root.Children.OfType<HeadingRowX>().ToList();
			await Assert.That(headings.Count).IsEqualTo(3);

			var sizes = headings
				.Select(heading => heading.Descendants<TextWidget>().First().PointSize)
				.ToList();

			await Assert.That(sizes[0]).IsGreaterThan(sizes[1]);
			await Assert.That(sizes[1]).IsGreaterThan(sizes[2]);
		}

		[Test]
		public async Task OrderedListsRenderNumberMarkers()
		{
			var root = RenderMarkdown(
				"""
				1. First step
				2. Second step
				3. Third step
				""");

			var textWidgets = root.Descendants<TextWidget>().ToList();
			await Assert.That(textWidgets.Any(text => text.Text == "1.")).IsTrue();
			await Assert.That(textWidgets.Any(text => text.Text == "2.")).IsTrue();
			await Assert.That(textWidgets.Any(text => text.Text == "3.")).IsTrue();
		}

		[Test]
		public async Task NestedListsIndentNestedItems()
		{
			var root = RenderMarkdown(
				"""
				- First bullet
				- Second bullet
				  - Nested bullet
				""");

			var lists = root.Descendants<ListX>().ToList();
			await Assert.That(lists.Count).IsGreaterThan(1);
			await Assert.That(lists.Skip(1).First().Margin.Left).IsGreaterThan(0);
		}

		[Test]
		public async Task BlockQuotesRenderContainedText()
		{
			var root = RenderMarkdown(
				"""
				> Keep help articles short, practical, and easy to scan.
				""");

			var quote = root.Descendants<QuoteBlockX>().FirstOrDefault();
			await Assert.That(quote).IsNotNull();
			await Assert.That(quote.Descendants<TextWidget>().Any(text => text.Text == "Keep")).IsTrue();
		}

		[Test]
		public async Task FencedCodeBlocksRenderCodeBlockWidget()
		{
			var root = RenderMarkdown(
				"""
				```cs
				var settings = LoadSettings();
				settings.Save();
				```
				""");

			var codeBlock = root.Descendants<CodeBlockX>().FirstOrDefault();
			await Assert.That(codeBlock).IsNotNull();
			await Assert.That(codeBlock.Descendants<TextWidget>().Any(text => text.Text == "settings.Save();")).IsTrue();
		}

		[Test]
		public async Task CodeBlocksPreserveLeadingSpacesAndUseMonospaceFont()
		{
			var root = RenderMarkdown(
				"""
				```md
				  - Nested bullet
				```
				""");

			var codeText = root.Descendants<TextWidget>().FirstOrDefault(text => text.Text.Contains("Nested bullet"));
			await Assert.That(codeText).IsNotNull();
			await Assert.That(codeText.Text).IsEqualTo("  - Nested bullet");
			await Assert.That(codeText.Printer.TypeFaceStyle.TypeFace).IsNotEqualTo(AggContext.DefaultFont);
			await Assert.That(codeText.Height).IsGreaterThan(codeText.Printer.LocalBounds.Height);
		}

		[Test]
		public async Task HorizontalRulesRenderVisibleLine()
		{
			var root = RenderMarkdown(
				"""
				Top

				---

				Bottom
				""");

			await Assert.That(root.Descendants<HorizontalLine>().Any()).IsTrue();
		}

		[Test]
		public async Task EmphasisRendersItalicBoldAndBoth()
		{
			var words = RenderWords("plain *slanted* **heavy** ***both*** _under_ __double__ ~~gone~~");

			async Task AssertStyle(string word, bool italic, bool bold)
			{
				await Assert.That(words[word].Italic).IsEqualTo(italic);
				await Assert.That(words[word].Bold).IsEqualTo(bold);
				// The flag must reach the face that draws, whichever order the nested spans styled the word in.
				await Assert.That(words[word].Printer.TypeFaceStyle.FauxItalic).IsEqualTo(italic);
			}

			await AssertStyle("plain", italic: false, bold: false);
			await AssertStyle("slanted", italic: true, bold: false);
			await AssertStyle("heavy", italic: false, bold: true);
			await AssertStyle("both", italic: true, bold: true);
			await AssertStyle("under", italic: true, bold: false);
			await AssertStyle("double", italic: false, bold: true);
			await AssertStyle("gone", italic: false, bold: false);
			await Assert.That(words["gone"].StrikeThrough).IsTrue();
		}

		[Test]
		public async Task PairedHtmlStyleTagsStyleTheWordsBetween()
		{
			// The rich editor writes these where markdown delimiters cannot flank, so the viewer must show them.
			var words = RenderWords("a <strong>heavy</strong> <b>bee</b> <em>slanted</em> <i>eye</i> <del>gone</del> <s>struck</s> "
				+ "<strike>old</strike> <b><i>both</i></b> <B>upper</b> b");

			async Task AssertStyle(string word, bool italic = false, bool bold = false, bool strike = false)
			{
				await Assert.That(words[word].Italic).IsEqualTo(italic);
				await Assert.That(words[word].Bold).IsEqualTo(bold);
				await Assert.That(words[word].StrikeThrough).IsEqualTo(strike);
				await Assert.That(words[word].Printer.TypeFaceStyle.FauxItalic).IsEqualTo(italic);
			}

			await AssertStyle("a");
			await AssertStyle("heavy", bold: true);
			await AssertStyle("bee", bold: true);
			await AssertStyle("slanted", italic: true);
			await AssertStyle("eye", italic: true);
			await AssertStyle("gone", strike: true);
			await AssertStyle("struck", strike: true);
			await AssertStyle("old", strike: true);
			await AssertStyle("both", italic: true, bold: true);
			await AssertStyle("upper", bold: true);
			await AssertStyle("b");
			await Assert.That(words.Keys.Any(word => word.Contains('<'))).IsFalse();
		}

		[Test]
		public async Task UnpairedAttributedOrCrossedHtmlStyleTagsAreDropped()
		{
			foreach (var markdown in new[] { "<b>open tail", "<b class=y>classy</b> tail", "<b>crossed <i>over</b> end</i> tail" })
			{
				var words = RenderWords(markdown);
				foreach (var word in words.Values)
				{
					await Assert.That(word.Bold).IsFalse();
					await Assert.That(word.Italic).IsFalse();
				}

				await Assert.That(words.ContainsKey("tail")).IsTrue();
				await Assert.That(words.Keys.Any(word => word.Contains('<'))).IsFalse();
			}
		}

		[Test]
		public async Task ItalicWordsDrawSlantedGlyphs()
		{
			var words = RenderWords("plain *slanted*");

			// An 'l' is a vertical stem, so a sheared one reaches further right at the same advance.
			var uprightBounds = words["plain"].Printer.TypeFaceStyle.GetGlyphForCharacter('l').GetBounds();
			var slantedBounds = words["slanted"].Printer.TypeFaceStyle.GetGlyphForCharacter('l').GetBounds();

			await Assert.That(slantedBounds.Right).IsGreaterThan(uprightBounds.Right + 1);
			await Assert.That(words["slanted"].Printer.TypeFaceStyle.GetAdvanceForCharacter('l'))
				.IsEqualTo(words["plain"].Printer.TypeFaceStyle.GetAdvanceForCharacter('l'));
		}

		// Each word is its own text widget, between space widgets that would collide as keys.
		private static System.Collections.Generic.Dictionary<string, TextWidget> RenderWords(string markdown)
		{
			return RenderMarkdown(markdown)
				.Descendants<TextWidget>()
				.Where(text => !string.IsNullOrWhiteSpace(text.Text))
				.ToDictionary(text => text.Text);
		}

		private static GuiWidget RenderMarkdown(string markdown)
		{
			var root = new GuiWidget();
			var document = new AggMarkdownDocument
			{
				Markdown = markdown
			};

			document.Parse(new ThemeConfig(), root);
			return root;
		}
	}
}
