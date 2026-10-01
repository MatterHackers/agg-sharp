/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichPasteOperationsTests
	{
		private static (string Markdown, RichSelection Caret) Paste(string markdown, DocPosition at, string fragmentMarkdown)
		{
			var document = RichMarkdownParser.Parse(markdown);
			var fragment = RichMarkdownParser.Parse(fragmentMarkdown);
			var caret = RichPasteOperations.InsertDocument(document, at, fragment);
			return (RichMarkdownWriter.Write(document), caret);
		}

		[Test]
		public async Task ParagraphPastesMidWordKeepingItsStyle()
		{
			var (markdown, caret) = Paste("Hello world\n", new DocPosition(0, 3), "**big**");
			await Assert.That(markdown).IsEqualTo("Hel**big**lo world\n");
			await Assert.That(caret.Caret).IsEqualTo(new DocPosition(0, 6));
		}

		[Test]
		public async Task ParagraphsSplitTheCaretBlock()
		{
			var (markdown, caret) = Paste("Hello world\n", new DocPosition(0, 5), "one\n\ntwo\n\nthree");
			await Assert.That(markdown).IsEqualTo("Helloone\n\ntwo\n\nthree world\n");
			await Assert.That(caret.Caret).IsEqualTo(new DocPosition(2, 5));
		}

		[Test]
		public async Task ListComesInAsBlocksBetweenTheHalves()
		{
			var (markdown, _) = Paste("Before after\n", new DocPosition(0, 7), "- a\n- b");
			await Assert.That(markdown).IsEqualTo("Before \n\n- a\n- b\n\nafter\n");
		}

		[Test]
		public async Task HeadingOnAnEmptyLineLeavesNoBlankParagraph()
		{
			// The empty line a user pressed Enter for.
			var document = RichMarkdownParser.Parse("First");
			document.Blocks.Add(new RichBlock { Kind = RichBlockKind.Paragraph, SeparatorBefore = "\n\n", Dirty = true });
			var caret = RichPasteOperations.InsertDocument(document, new DocPosition(1, 0), RichMarkdownParser.Parse("# Title"));
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo("First\n\n# Title");
			await Assert.That(caret.Caret).IsEqualTo(new DocPosition(1, 5));
		}

		[Test]
		public async Task TablePastesAsATable()
		{
			var (markdown, _) = Paste("End\n", new DocPosition(0, 3), "| a | b |\n|---|---|\n| 1 | 2 |");
			await Assert.That(markdown).StartsWith("End\n\n| a | b |");
			var document = RichMarkdownParser.Parse(markdown);
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Table);
		}

		[Test]
		public async Task CodeBlockTakesPlainTextOnly()
		{
			var (markdown, caret) = Paste("```\nxy\n```\n", new DocPosition(0, 1), "**b**\n\n- c");
			await Assert.That(markdown).IsEqualTo("```\nxb\ncy\n```\n");
			await Assert.That(caret.Caret).IsEqualTo(new DocPosition(0, 4));
		}

		[Test]
		public async Task SliceThenPasteRoundTripsAcrossBlocks()
		{
			var source = RichMarkdownParser.Parse("# Head line\n\nSome **bold** text\n");
			var fragment = RichPasteOperations.Slice(source, new RichSelection(new DocPosition(0, 5), new DocPosition(1, 9)));
			await Assert.That(RichPasteOperations.VisibleText(fragment)).IsEqualTo("line\nSome bold");
			await Assert.That(RichMarkdownWriter.Write(fragment)).IsEqualTo("# line\n\nSome **bold**");

			// The source is untouched and the fragment pastes more than once.
			await Assert.That(RichMarkdownWriter.Write(source)).IsEqualTo("# Head line\n\nSome **bold** text\n");
			var target = RichMarkdownParser.Parse("x\n");
			RichPasteOperations.InsertDocument(target, new DocPosition(0, 1), fragment);
			RichPasteOperations.InsertDocument(target, new DocPosition(0, 0), fragment);
			await Assert.That(target.Blocks.Count).IsEqualTo(4);
		}

		private static string PasteText(string markdown, DocPosition at, string clipboardText)
		{
			var document = RichMarkdownParser.Parse(markdown);
			RichPasteOperations.InsertDocument(document, at, RichPasteOperations.ParseClipboardText(clipboardText));
			return RichMarkdownWriter.Write(document);
		}

		[Test]
		public async Task ClipboardTextKeepsItsLines()
		{
			var parsed = RichPasteOperations.ParseClipboardText("123 Main St\nSpringfield\n\nNext");
			await Assert.That(parsed.Blocks.Count).IsEqualTo(2);
			await Assert.That(RichPasteOperations.VisibleText(parsed)).IsEqualTo("123 Main St\nSpringfield\nNext");
		}

		[Test]
		public async Task ClipboardTextWithManyLinesBecomesAParagraphPerLine()
		{
			var lines = new string[RichPasteOperations.MaxPastedLineBreaks + 2];
			for (int i = 0; i < lines.Length; i++)
			{
				lines[i] = "line " + i;
			}

			var parsed = RichPasteOperations.ParseClipboardText(string.Join("\n", lines));
			await Assert.That(parsed.Blocks.Count).IsEqualTo(lines.Length);
			await Assert.That(RichInlines.Length(parsed.Blocks[3].Inlines)).IsEqualTo("line 3".Length);
			await Assert.That(parsed.Blocks[3].Inlines.Exists(inline => inline is InlineAtom)).IsFalse();
		}

		[Test]
		public async Task ListPastedIntoAListStaysOneNumberedList()
		{
			string markdown = PasteText("1. a\n2. b\n", new DocPosition(0, 1), "1. x\n2. y");
			await Assert.That(markdown).IsEqualTo("1. a\n2. x\n3. y\n4. b\n");
		}

		[Test]
		public async Task ParagraphsPastedIntoAQuoteStayInIt()
		{
			string markdown = PasteText("> A B\n", new DocPosition(0, 1), "x\n\ny\n\nz");
			var document = RichMarkdownParser.Parse(markdown);
			await Assert.That(document.Blocks.Count).IsEqualTo(3);
			await Assert.That(document.Blocks.TrueForAll(block => block.Kind == RichBlockKind.Quote && block.QuoteGroup == document.Blocks[0].QuoteGroup)).IsTrue();
		}

		[Test]
		public async Task ParagraphsPastedIntoACenteredSectionStayCentered()
		{
			string markdown = PasteText("<div align=\"center\">\n\nA B\n\n</div>\n", new DocPosition(0, 1), "x\n\ny\n\nz");
			var document = RichMarkdownParser.Parse(markdown);
			await Assert.That(document.Blocks.Count).IsEqualTo(3);
			await Assert.That(document.Blocks.TrueForAll(block => block.Alignment == RichAlignment.Center)).IsTrue();
		}

		[Test]
		public async Task MultiCellTableSliceHoldsOnlyTheCoveredText()
		{
			var source = RichMarkdownParser.Parse("| a | b |\n|---|---|\n| cc | d |\n");
			var selection = new RichSelection(new DocPosition(0, 0, 0, 1), new DocPosition(0, 1, 1, 0));
			var fragment = RichPasteOperations.Slice(source, selection);
			await Assert.That(RichPasteOperations.VisibleText(fragment)).IsEqualTo("\tb\nc\t");

			// What a cut removes is exactly what was copied.
			RichEditOperations.DeleteSelection(source, selection);
			await Assert.That(RichPasteOperations.VisibleText(source)).IsEqualTo("a\t\nc\td");
		}

		[Test]
		public async Task PasteIntoCrlfDocumentWritesOnlyCrlf()
		{
			string markdown = PasteText("a b\r\n\r\nend\r\n", new DocPosition(0, 1), "x\ny\n\n<div>\nhi\n</div>\n\nz");
			await Assert.That(markdown).Contains("<div>");
			await Assert.That(markdown.Replace("\r\n", "")).DoesNotContain("\n");
		}

		[Test]
		public async Task TableCellTakesAParagraphFormattedAndBlocksAsOneLine()
		{
			const string table = "| a | b |\n|---|---|\n| c | d |\n";
			var cell = new DocPosition(0, 1, 1, 0);
			await Assert.That(PasteText(table, cell, "**x** y")).Contains("| c**x** y |");
			await Assert.That(PasteText(table, cell, "# h\n\n- i")).Contains("| ch i |");
		}

		[Test]
		public async Task SliceSkipsBlocksTheRangeOnlyTouches()
		{
			var source = RichMarkdownParser.Parse("- a\n- b\n- c\n");
			var fragment = RichPasteOperations.Slice(source, new RichSelection(new DocPosition(0, 1), new DocPosition(2, 0)));
			await Assert.That(fragment.Blocks.Count).IsEqualTo(1);
			await Assert.That(RichPasteOperations.VisibleText(fragment)).IsEqualTo("b");
		}
	}
}
