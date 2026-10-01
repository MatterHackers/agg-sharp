/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// Changed lists, quotes and alignment wrappers, built through the edit ops: the markdown written, and that
	/// reading it back gives the same block kinds, depths, groups and alignment.
	/// </summary>
	public class RichMarkdownWriterGroupTests
	{
		[Test]
		public async Task BulletsOnTwoParagraphsWriteOneTightList()
		{
			var document = RichMarkdownParser.Parse("A\n\nB\n");
			RichBlockOperations.ToggleList(document, Range(0, 1), ordered: false);
			await AssertWrites(document, "- A\n- B\n");
		}

		[Test]
		public async Task NumberedListKeepsItsStartAndNestsABullet()
		{
			var document = RichMarkdownParser.Parse("3. a\n4. b\n5. c\n");
			RichBlockOperations.Indent(document, Range(1, 1));
			RichBlockOperations.ToggleList(document, Range(1, 1), ordered: false);
			await AssertWrites(document, "3. a\n   - b\n4. c\n");
		}

		[Test]
		public async Task IndentThenOutdentRestoresTheList()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n- c\n");
			RichBlockOperations.Indent(document, Range(1, 1));
			await AssertWrites(document, "- a\n  - b\n- c\n");
			RichBlockOperations.Outdent(document, Range(1, 1));
			await AssertWrites(document, "- a\n- b\n- c\n");
		}

		[Test]
		public async Task LooseListStaysLooseWhenRegenerated()
		{
			var document = RichMarkdownParser.Parse("- a\n\n- bc\n");
			RichEditOperations.SplitBlock(document, new DocPosition(1, 1));
			await AssertWrites(document, "- a\n\n- b\n\n- c\n");
		}

		[Test]
		public async Task AdjacentListsStaySeparate()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n\nBetween\n\n- c\n");
			RichEditOperations.DeleteSelection(document, RichEditOperations.WholeBlock(document, 2));
			var lists = document.Blocks.Where(block => block.Kind == RichBlockKind.ListItem).Select(block => block.ListGroup).Distinct().Count();
			await Assert.That(lists).IsEqualTo(2);
			await AssertWrites(document, null);
		}

		[Test]
		public async Task QuoteWithTwoParagraphs()
		{
			var document = RichMarkdownParser.Parse("A\n\nB\n");
			RichBlockOperations.ToggleQuote(document, Range(0, 1));
			await AssertWrites(document, "> A\n>\n> B\n");
		}

		[Test]
		public async Task CenteredHeadingAndParagraphShareOneWrapper()
		{
			var document = RichMarkdownParser.Parse("# T\n\nP\n");
			RichBlockOperations.SetAlignment(document, Range(0, 1), RichAlignment.Center);
			await AssertWrites(document, "<div align=\"center\">\n\n# T\n\nP\n\n</div>\n");
		}

		[Test]
		public async Task CenterThenRightWriteTwoWrappersApart()
		{
			var document = RichMarkdownParser.Parse("A\n\nB\n");
			RichBlockOperations.SetAlignment(document, Range(0, 0), RichAlignment.Center);
			RichBlockOperations.SetAlignment(document, Range(1, 1), RichAlignment.Right);
			await AssertWrites(document, "<div align=\"center\">\n\nA\n\n</div>\n\n<div align=\"right\">\n\nB\n\n</div>\n");
		}

		[Test]
		public async Task DeletingTheFirstCenteredBlockLeavesNoOrphanTag()
		{
			var document = RichMarkdownParser.Parse("<div align=\"center\">\n\nA\n\nB\n\n</div>\n\nC\n");
			RichEditOperations.DeleteSelection(document, RichEditOperations.WholeBlock(document, 0));
			await AssertWrites(document, "<div align=\"center\">\n\nB\n\n</div>\n\nC\n");
		}

		[Test]
		public async Task CrlfDocumentKeepsItsLineEnding()
		{
			var document = RichMarkdownParser.Parse("A\r\n\r\nB\r\n");
			RichBlockOperations.ToggleQuote(document, Range(0, 1));
			await AssertWrites(document, "> A\r\n>\r\n> B\r\n");
		}

		/// <summary>
		/// The usual way to build a list - type an item, press Enter - leaves an empty item, which must write as
		/// an item and read back as an empty, editable item wherever it sits.
		/// </summary>
		[Test]
		[Arguments("- a\n- b\n", 1, 1, false, "- a\n- b\n-\n")]
		[Arguments("- a\n- b\n", 0, 1, false, "- a\n-\n- b\n")]
		[Arguments("1. a\n2. b\n", 1, 1, false, "1. a\n2. b\n3.\n")]
		[Arguments("1. a\n2. b\n", 0, 1, false, "1. a\n2.\n3. b\n")]
		[Arguments("- a\n  - b\n", 1, 1, false, "- a\n  - b\n  -\n")]
		[Arguments("- a\n- b\n", 0, 1, true, null)]
		[Arguments("1. a\n2. b\n", 0, 1, true, null)]
		[Arguments("P\n\n- a\n", 1, 0, false, null)]
		[Arguments("P\n\n1. a\n", 1, 0, false, null)]
		public async Task EmptyListItemSurvivesWriteAndRead(string markdown, int block, int offset, bool indentNewItem, string expected)
		{
			var document = RichMarkdownParser.Parse(markdown);
			RichEditOperations.SplitBlock(document, new DocPosition(block, offset));
			if (indentNewItem)
			{
				RichBlockOperations.Indent(document, Range(block + 1, block + 1));
			}

			await Assert.That(document.Blocks.Count(b => b.Kind == RichBlockKind.ListItem && b.Inlines.Count == 0)).IsEqualTo(1);
			await AssertWrites(document, expected);
		}

		[Test]
		public async Task EnterOnAnEmptyMiddleItemLeavesTwoLists()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n- c\n");
			RichEditOperations.SplitBlock(document, new DocPosition(1, 1));
			RichEditOperations.SplitBlock(document, new DocPosition(2, 0));
			await Assert.That(document.Blocks[2].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await AssertWrites(document, null);
		}

		/// <summary>
		/// A clean block indented as deep as a list item's content, once it sits right after that list, would be
		/// read into the last item.
		/// </summary>
		[Test]
		[Arguments("- a\n- b\n\nP\n\n    code\n")]
		[Arguments("- a\n- b\n\nP\n\n  ~~~\n  code\n  ~~~\n")]
		[Arguments("- a\n- b\n\nP\n\n   Q\n")]
		public async Task IndentedBlockAfterARewrittenListStaysOut(string markdown)
		{
			var document = RichMarkdownParser.Parse(markdown);
			RichEditOperations.DeleteSelection(document, RichEditOperations.WholeBlock(document, 2));
			RichEditOperations.InsertText(document, new DocPosition(1, 1), "x");
			await AssertWrites(document, null);
		}

		[Test]
		public async Task IndentedBlockAfterAnUntouchedListStaysOut()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n\nP\n\n    code\n");
			RichEditOperations.DeleteSelection(document, RichEditOperations.WholeBlock(document, 2));
			await AssertWrites(document, null);
		}

		[Test]
		public async Task RewrittenCodeKeepsTheSpaceBeforeItsInfoString()
		{
			var document = RichMarkdownParser.Parse("~~~ c#\nx\n~~~\n");
			document.Blocks[0].Dirty = true;
			await Assert.That(RichMarkdownWriter.Write(document)).IsEqualTo("~~~ c#\nx\n~~~\n");
		}

		private static RichSelection Range(int first, int last)
		{
			return new RichSelection(new DocPosition(first, 0), new DocPosition(last, 1));
		}

		/// <summary>
		/// Writes the document, checks the markdown when <paramref name="expected"/> is given, and checks it reads
		/// back to the same structure.
		/// </summary>
		private static async Task AssertWrites(RichDocument document, string expected)
		{
			string markdown = RichMarkdownWriter.Write(document);
			if (expected != null)
			{
				await Assert.That(markdown).IsEqualTo(expected);
			}

			await Assert.That(Shape(RichMarkdownParser.Parse(markdown))).IsEqualTo(Shape(document));
		}

		/// <summary>
		/// Each block's kind, depth, list kind, alignment, text and which list/quote it shares with the block
		/// before. An empty paragraph writes nothing, so it is left out.
		/// </summary>
		private static string Shape(RichDocument document)
		{
			var lines = new List<string>();
			RichBlock previous = null;
			foreach (var block in document.Blocks)
			{
				if (block.Kind == RichBlockKind.Paragraph && block.Inlines.Count == 0)
				{
					continue;
				}

				bool sameList = previous != null && block.ListGroup != null && block.ListGroup == previous.ListGroup;
				bool sameQuote = previous != null && block.QuoteGroup != null && block.QuoteGroup == previous.QuoteGroup;
				lines.Add($"{block.Kind} depth={block.List?.Depth} ordered={block.List?.Ordered} {block.Alignment} text={string.Concat(block.Inlines.OfType<RichRun>().Select(run => run.Text))} sameList={sameList} sameQuote={sameQuote}");
				previous = block;
			}

			return string.Join("\n", lines);
		}
	}
}
