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
	public class RichEditOperationsTests
	{
		// Plain text of a block with each atom shown as '@', so asserts read like what the user sees.
		private static string Text(RichBlock block) => string.Concat(block.Inlines.Select(i => i is RichRun run ? run.Text : "@"));

		private static string Texts(RichDocument document) => string.Join("|", document.Blocks.Select(Text));

		private static DocPosition P(int block, int offset) => new DocPosition(block, offset);

		/// <summary>
		/// True when the blocks belonging to the group form one unbroken run, the shape the writer rule needs.
		/// </summary>
		private static bool IsContiguous(RichDocument document, RichBlockGroup group)
		{
			var indices = document.Blocks
				.Select((block, index) => (block, index))
				.Where(x => x.block.AlignGroup == group || x.block.QuoteGroup == group || x.block.ListGroup == group)
				.Select(x => x.index)
				.ToList();
			return indices.Count == 0 || indices.Last() - indices.First() + 1 == indices.Count;
		}

		/// <summary>
		/// The list shape markdown can express: each list group starts at Depth 0 and an item is at most one level
		/// deeper than the item before it.
		/// </summary>
		private static bool ListsAreWellFormed(RichDocument document)
		{
			RichListGroup group = null;
			int previousDepth = -1;
			foreach (var block in document.Blocks)
			{
				if (block.ListGroup == null)
				{
					group = null;
					continue;
				}

				int allowed = block.ListGroup == group ? previousDepth + 1 : 0;
				if (block.List.Depth < 0 || block.List.Depth > allowed)
				{
					return false;
				}

				group = block.ListGroup;
				previousDepth = block.List.Depth;
			}

			return true;
		}

		private static string Depths(RichDocument document) => string.Join(",", document.Blocks.Select(b => b.List?.Depth.ToString() ?? "p"));

		[Test]
		public async Task TypingAfterBoldContinuesBold()
		{
			var document = RichMarkdownParser.Parse("a **bold** c\n");
			var selection = RichEditOperations.InsertText(document, P(0, 6), "er");

			var inlines = document.Blocks[0].Inlines;
			await Assert.That(Text(document.Blocks[0])).IsEqualTo("a bolder c");
			await Assert.That(((RichRun)inlines[1]).Text).IsEqualTo("bolder");
			await Assert.That(((RichRun)inlines[1]).Bold).IsTrue();
			await Assert.That(inlines.Count).IsEqualTo(3);
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(0, 8)));
			await Assert.That(document.Blocks[0].Dirty).IsTrue();
		}

		[Test]
		public async Task TypingAfterAnImageTakesThePrecedingRunsStyle()
		{
			var document = RichMarkdownParser.Parse("**ab**![x](x.png)\n");
			RichEditOperations.InsertText(document, P(0, 3), "z");

			var inlines = document.Blocks[0].Inlines;
			await Assert.That(Text(document.Blocks[0])).IsEqualTo("ab@z");
			await Assert.That(inlines[2] is RichRun { Bold: true, Text: "z" }).IsTrue();

			// An explicit pending style wins over the run before the caret.
			RichEditOperations.InsertText(document, P(0, 1), "p", new RichRun { Italic = true });
			await Assert.That(inlines[1] is RichRun { Italic: true, Bold: false, Text: "p" }).IsTrue();
		}

		[Test]
		public async Task TypingAtBlockStartTakesTheFirstRunsStyle()
		{
			var document = RichMarkdownParser.Parse("**ab** c\n");
			RichEditOperations.InsertText(document, P(0, 0), "x");
			await Assert.That(document.Blocks[0].Inlines[0] is RichRun { Bold: true, Text: "xab" }).IsTrue();
		}

		[Test]
		public async Task InsertedLinesSplitIntoBlocks()
		{
			var document = RichMarkdownParser.Parse("- hello\n");
			var selection = RichEditOperations.InsertText(document, P(0, 5), "a\r\nb");

			await Assert.That(Texts(document)).IsEqualTo("helloa|b");
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.ListItem);
			await Assert.That(document.Blocks[1].ListGroup).IsSameReferenceAs(document.Blocks[0].ListGroup);
			await Assert.That(selection.Caret).IsEqualTo(P(1, 1));
			await Assert.That(ListsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task DeleteAcrossThreeBlocksMergesTheEndIntoTheStart()
		{
			var document = RichMarkdownParser.Parse("# One\n\ntwo\n\n- three\n- four\n");
			var selection = RichEditOperations.DeleteRange(document, P(2, 2), P(0, 1));

			await Assert.That(Texts(document)).IsEqualTo("Oree|four");
			var survivor = document.Blocks[0];
			await Assert.That(survivor.Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(survivor.HeadingLevel).IsEqualTo(1);
			await Assert.That(survivor.ListGroup).IsNull();
			await Assert.That(survivor.Dirty).IsTrue();
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(0, 1)));

			// The list lost a member, which the writer reads from its member count.
			var list = document.Blocks[1].ListGroup;
			await Assert.That(list.OriginalMemberCount).IsEqualTo(2);
			await Assert.That(document.Blocks.Count(b => b.ListGroup == list)).IsEqualTo(1);
			await Assert.That(ListsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task RawBlocksInARangeAreRemovedWhole()
		{
			var document = RichMarkdownParser.Parse("a\n\n---\n\nbc\n");
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Raw);

			var merged = document.Clone();
			RichEditOperations.DeleteRange(merged, P(0, 1), P(2, 1));
			await Assert.That(Texts(merged)).IsEqualTo("ac");

			// Starting on the Raw block selects part of it: it goes whole and nothing merges across it.
			var partial = document.Clone();
			var selection = RichEditOperations.DeleteRange(partial, P(1, 0), P(2, 1));
			await Assert.That(Texts(partial)).IsEqualTo("a|c");
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(1, 0)));

			// Ending at the Raw block's start touches it without selecting it.
			var touching = document.Clone();
			RichEditOperations.DeleteRange(touching, P(0, 0), P(1, 0));
			await Assert.That(touching.Blocks.Count).IsEqualTo(3);
			await Assert.That(touching.Blocks[1].Kind).IsEqualTo(RichBlockKind.Raw);
		}

		[Test]
		public async Task DeleteAcrossAnAlignGroupEdgeLeavesBothGroupsConsistent()
		{
			var document = RichMarkdownParser.Parse(
				"<div align=\"center\">\n\none\n\ntwo\n\n</div>\n\n<div align=\"right\">\n\nthree\n\nfour\n\n</div>\n");
			var center = document.Blocks[0].AlignGroup;
			var right = document.Blocks[2].AlignGroup;
			await Assert.That(center).IsNotNull();
			await Assert.That(right).IsNotNull();

			RichEditOperations.DeleteRange(document, P(1, 1), P(2, 2));

			await Assert.That(Texts(document)).IsEqualTo("one|tree|four");
			await Assert.That(document.Blocks[1].AlignGroup).IsSameReferenceAs(center);
			await Assert.That(document.Blocks[1].Alignment).IsEqualTo(RichAlignment.Center);
			await Assert.That(document.Blocks[2].AlignGroup).IsSameReferenceAs(right);
			await Assert.That(IsContiguous(document, center)).IsTrue();
			await Assert.That(IsContiguous(document, right)).IsTrue();
			await Assert.That(document.Blocks.Count(b => b.AlignGroup == right)).IsNotEqualTo(right.OriginalMemberCount);
		}

		[Test]
		public async Task EnterInTheMiddleOfABoldRunSplitsTheRun()
		{
			var document = RichMarkdownParser.Parse("a **bold** c\n");
			var selection = RichEditOperations.SplitBlock(document, P(0, 4));

			await Assert.That(Texts(document)).IsEqualTo("a bo|ld c");
			await Assert.That(document.Blocks[0].Inlines.Last() is RichRun { Bold: true, Text: "bo" }).IsTrue();
			await Assert.That(document.Blocks[1].Inlines[0] is RichRun { Bold: true, Text: "ld" }).IsTrue();
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(document.Blocks.All(b => b.Dirty)).IsTrue();
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(1, 0)));
		}

		[Test]
		public async Task EnterAroundAHeading()
		{
			var start = RichMarkdownParser.Parse("## Title\n");
			var selection = RichEditOperations.SplitBlock(start, P(0, 0));
			await Assert.That(Texts(start)).IsEqualTo("|Title");
			await Assert.That(start.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(start.Blocks[1].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(selection.Caret).IsEqualTo(P(1, 0));

			var end = RichMarkdownParser.Parse("## Title\n");
			RichEditOperations.SplitBlock(end, P(0, 5));
			await Assert.That(Texts(end)).IsEqualTo("Title|");
			await Assert.That(end.Blocks[1].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(end.Blocks[1].HeadingLevel).IsEqualTo(0);

			// Splitting the middle keeps the heading on both halves.
			var middle = RichMarkdownParser.Parse("## Title\n");
			RichEditOperations.SplitBlock(middle, P(0, 2));
			await Assert.That(middle.Blocks[1].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(middle.Blocks[1].HeadingLevel).IsEqualTo(2);
		}

		[Test]
		public async Task EnterOnAnEmptyTopLevelListItemLeavesTheList()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n- c\n");
			var list = document.Blocks[0].ListGroup;
			document.Blocks[1].Inlines.Clear();

			var selection = RichEditOperations.SplitBlock(document, P(1, 0));

			var left = document.Blocks[1];
			await Assert.That(document.Blocks.Count).IsEqualTo(3);
			await Assert.That(left.Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(left.List).IsNull();
			await Assert.That(left.ListGroup).IsNull();
			await Assert.That(left.Dirty).IsTrue();
			await Assert.That(selection.Caret).IsEqualTo(P(1, 0));

			// The items after it become their own list, so neither group spans the paragraph.
			await Assert.That(document.Blocks[0].ListGroup).IsSameReferenceAs(list);
			var after = document.Blocks[2].ListGroup;
			await Assert.That(after).IsNotNull();
			await Assert.That(after).IsNotSameReferenceAs(list);
			await Assert.That(after.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(ListsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task EnterOnAnEmptyNestedListItemOutdents()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n");
			await Assert.That(document.Blocks[1].List.Depth).IsEqualTo(1);
			document.Blocks[1].Inlines.Clear();

			RichEditOperations.SplitBlock(document, P(1, 0));

			var item = document.Blocks[1];
			await Assert.That(item.Kind).IsEqualTo(RichBlockKind.ListItem);
			await Assert.That(item.List.Depth).IsEqualTo(0);
			await Assert.That(item.ListGroup).IsSameReferenceAs(document.Blocks[0].ListGroup);
			await Assert.That(item.Dirty).IsTrue();
			await Assert.That(ListsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task EnterOnAnEmptyQuoteParagraphLeavesTheQuote()
		{
			var document = RichMarkdownParser.Parse("> a\n>\n> b\n");
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Quote);
			document.Blocks[1].Inlines.Clear();

			RichEditOperations.SplitBlock(document, P(1, 0));

			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(document.Blocks[1].QuoteGroup).IsNull();
			await Assert.That(document.Blocks[0].QuoteGroup).IsNotNull();
		}

		[Test]
		public async Task BackspaceAtBlockStart()
		{
			var list = RichMarkdownParser.Parse("- a\n  - b\n- c\n");
			RichEditOperations.Backspace(list, P(1, 0));
			await Assert.That(list.Blocks[1].List.Depth).IsEqualTo(0);
			RichEditOperations.Backspace(list, P(2, 0));
			await Assert.That(list.Blocks[2].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(list.Blocks[2].ListGroup).IsNull();
			await Assert.That(Texts(list)).IsEqualTo("a|b|c");
			await Assert.That(ListsAreWellFormed(list)).IsTrue();

			var quote = RichMarkdownParser.Parse("> q\n");
			RichEditOperations.Backspace(quote, P(0, 0));
			await Assert.That(quote.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(quote.Blocks[0].QuoteGroup).IsNull();
			await Assert.That(quote.Blocks[0].Dirty).IsTrue();

			var heading = RichMarkdownParser.Parse("# h\n");
			RichEditOperations.Backspace(heading, P(0, 0));
			await Assert.That(heading.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(heading.Blocks[0].HeadingLevel).IsEqualTo(0);

			var paragraphs = RichMarkdownParser.Parse("- item\n\n**p**\n");
			var selection = RichEditOperations.Backspace(paragraphs, P(1, 0));
			await Assert.That(Texts(paragraphs)).IsEqualTo("itemp");
			await Assert.That(paragraphs.Blocks[0].Kind).IsEqualTo(RichBlockKind.ListItem);
			await Assert.That(paragraphs.Blocks[0].Inlines[1] is RichRun { Bold: true }).IsTrue();
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(0, 4)));
		}

		[Test]
		public async Task BackspaceAfterARawBlockSelectsItWhole()
		{
			var document = RichMarkdownParser.Parse("a\n\n---\n\nb\n");
			var selection = RichEditOperations.Backspace(document, P(2, 0));

			await Assert.That(document.Blocks.Count).IsEqualTo(3);
			await Assert.That(selection).IsEqualTo(new RichSelection(P(1, 0), P(1, 1), WholeBlock: true));
			await Assert.That(document.Blocks.Any(b => b.Dirty)).IsFalse();
		}

		[Test]
		public async Task BackspaceAndDeleteRemoveWholeCharacters()
		{
			var document = RichMarkdownParser.Parse("a\U0001F600b\n");
			var selection = RichEditOperations.Backspace(document, P(0, 3));
			await Assert.That(Text(document.Blocks[0])).IsEqualTo("ab");
			await Assert.That(selection.Caret).IsEqualTo(P(0, 1));

			var forward = RichMarkdownParser.Parse("a\U0001F600b\n");
			RichEditOperations.Delete(forward, P(0, 1));
			await Assert.That(Text(forward.Blocks[0])).IsEqualTo("ab");
		}

		[Test]
		public async Task DeleteAtBlockEndMergesTheNextTextBlock()
		{
			var document = RichMarkdownParser.Parse("# One\n\ntwo\n\n---\n");
			var selection = RichEditOperations.Delete(document, P(0, 3));
			await Assert.That(Texts(document)).IsEqualTo("Onetwo|");
			await Assert.That(document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Heading);
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(0, 3)));

			// The next block is Raw: it is selected, not merged.
			selection = RichEditOperations.Delete(document, P(0, 6));
			await Assert.That(selection).IsEqualTo(new RichSelection(P(1, 0), P(1, 1), WholeBlock: true));
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
		}

		[Test]
		public async Task RemovingABlockBetweenTwoListsForcesBothToRegenerate()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n\n---\n\n- c\n");
			var first = document.Blocks[0].ListGroup;
			var second = document.Blocks[3].ListGroup;
			await Assert.That(first).IsNotSameReferenceAs(second);

			RichEditOperations.DeleteRange(document, P(2, 0), P(2, 1));

			// Both lists are clean, but writing their bytes back to back would read as one loose list.
			await Assert.That(document.Blocks.Count).IsEqualTo(3);
			await Assert.That(first.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(second.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(ListsAreWellFormed(document)).IsTrue();

			var quotes = RichMarkdownParser.Parse("> a\n\nb\n\n> c\n");
			RichEditOperations.Backspace(quotes, P(1, 0));
			await Assert.That(Texts(quotes)).IsEqualTo("ab|c");
			await Assert.That(quotes.Blocks[0].QuoteGroup.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(quotes.Blocks[1].QuoteGroup.OriginalMemberCount).IsEqualTo(0);
		}

		[Test]
		public async Task LeavingAListFromTheMiddleLiftsTheNestedTail()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n  - c\n    - d\n  - e\n");
			RichEditOperations.Backspace(document, P(1, 0));

			await Assert.That(Depths(document)).IsEqualTo("0,p,0,1,0");
			await Assert.That(document.Blocks[2].Dirty).IsTrue();
			await Assert.That(ListsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task OutdentingAnItemTakesItsChildrenAlong()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n    - c\n  - d\n");
			RichEditOperations.Backspace(document, P(1, 0));

			await Assert.That(Depths(document)).IsEqualTo("0,0,1,1");
			await Assert.That(ListsAreWellFormed(document)).IsTrue();

			// Deleting a parent's text merges its line away; deeper items left behind are lifted into shape.
			var merged = RichMarkdownParser.Parse("- a\n  - b\n    - c\n");
			RichEditOperations.DeleteRange(merged, P(0, 1), P(1, 1));
			await Assert.That(Texts(merged)).IsEqualTo("a|c");
			await Assert.That(ListsAreWellFormed(merged)).IsTrue();
		}

		[Test]
		public async Task NewBlocksUseTheDocumentsLineEnding()
		{
			var document = RichMarkdownParser.Parse("hello\r\n");
			RichEditOperations.SplitBlock(document, P(0, 5));
			await Assert.That(document.Blocks[1].SeparatorBefore).IsEqualTo("\r\n\r\n");
		}

		[Test]
		public async Task RemovingTheFirstBlockKeepsTheLeadingGap()
		{
			var document = RichMarkdownParser.Parse("---\ntitle: x\n---\n\n<hr>\n\nb\n");
			var lead = document.Blocks[0].SeparatorBefore;
			await Assert.That(document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Raw);

			RichEditOperations.DeleteRange(document, P(0, 0), P(0, 1));
			await Assert.That(Texts(document)).IsEqualTo("b");
			await Assert.That(document.Blocks[0].SeparatorBefore).IsEqualTo(lead);
		}

		[Test]
		public async Task BackspaceAtARawBlocksStartMovesToThePreviousTextBlock()
		{
			var document = RichMarkdownParser.Parse("ab\n\n---\n");
			var selection = RichEditOperations.Backspace(document, P(1, 0));
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(0, 2)));
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
		}

		[Test]
		public async Task TypingAtTheEndOfALinkOrCodeDoesNotExtendIt()
		{
			var document = RichMarkdownParser.Parse("**[go](u)** `x`\n");
			RichEditOperations.InsertText(document, P(0, 2), "!");
			var typed = (RichRun)document.Blocks[0].Inlines[1];
			await Assert.That(typed.Text).IsEqualTo("!");
			await Assert.That(typed.LinkUrl).IsNull();
			await Assert.That(typed.Bold).IsTrue();

			int end = document.Blocks[0].TextLength();
			RichEditOperations.InsertText(document, P(0, end), "y");
			await Assert.That(document.Blocks[0].Inlines.Last() is RichRun { Code: false, Text: "y" }).IsTrue();

			// Inside or at the start of a link, typing still extends it.
			RichEditOperations.InsertText(document, P(0, 1), "o");
			await Assert.That(document.Blocks[0].Inlines[0] is RichRun { LinkUrl: "u", Text: "goo" }).IsTrue();
			RichEditOperations.InsertText(document, P(0, 0), "a");
			await Assert.That(document.Blocks[0].Inlines[0] is RichRun { LinkUrl: "u", Text: "agoo" }).IsTrue();
		}

		[Test]
		public async Task CombiningMarkAcrossARunBoundaryIsOneCharacter()
		{
			var document = RichMarkdownParser.Parse("**e**́x\n");
			await Assert.That(document.Blocks[0].Inlines.Count).IsEqualTo(2);
			var selection = RichEditOperations.Backspace(document, P(0, 2));
			await Assert.That(Text(document.Blocks[0])).IsEqualTo("x");
			await Assert.That(selection.Caret).IsEqualTo(P(0, 0));

			var forward = RichMarkdownParser.Parse("**e**́x\n");
			RichEditOperations.Delete(forward, P(0, 0));
			await Assert.That(Text(forward.Blocks[0])).IsEqualTo("x");
		}

		[Test]
		public async Task InsertingNothingChangesNothing()
		{
			var document = RichMarkdownParser.Parse("a\n\n---\n");
			var selection = RichEditOperations.InsertText(document, P(1, 1), "");
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
			await Assert.That(document.Blocks.Any(b => b.Dirty)).IsFalse();
			await Assert.That(selection).IsEqualTo(RichSelection.At(P(1, 1)));
		}

		[Test]
		public async Task AnEmptyDocumentTakesTypingAndIgnoresDeletes()
		{
			var document = RichMarkdownParser.Parse("");
			await Assert.That(RichEditOperations.Backspace(document, P(0, 0))).IsEqualTo(RichSelection.At(P(0, 0)));
			await Assert.That(RichEditOperations.Delete(document, P(0, 0))).IsEqualTo(RichSelection.At(P(0, 0)));
			await Assert.That(RichEditOperations.DeleteRange(document, P(0, 0), P(0, 1))).IsEqualTo(RichSelection.At(P(0, 0)));
			await Assert.That(RichEditOperations.DeleteSelection(document, RichEditOperations.WholeBlock(document, 0))).IsEqualTo(RichSelection.At(P(0, 0)));
			await Assert.That(document.Blocks.Count).IsEqualTo(0);

			// Typing into nothing starts the paragraph a first-time user expects to be there.
			var typed = RichEditOperations.InsertText(document, P(0, 0), "hi");
			await Assert.That(Texts(document)).IsEqualTo("hi");
			await Assert.That(document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(typed).IsEqualTo(RichSelection.At(P(0, 2)));

			var entered = RichMarkdownParser.Parse("");
			await Assert.That(RichEditOperations.SplitBlock(entered, P(0, 0))).IsEqualTo(RichSelection.At(P(1, 0)));
			await Assert.That(entered.Blocks.Count).IsEqualTo(2);
		}
	}
}
