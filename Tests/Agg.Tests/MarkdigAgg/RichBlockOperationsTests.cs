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

using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichBlockOperationsTests
	{
		private static string Text(RichBlock block) => string.Concat(block.Inlines.Select(i => i is RichRun run ? run.Text : "@"));

		private static DocPosition P(int block, int offset) => new DocPosition(block, offset);

		// A selection from the start of block a to the end of block b.
		private static RichSelection Blocks(RichDocument document, int a, int b) => new RichSelection(P(a, 0), P(b, document.Blocks[b].TextLength()));

		private static RichSelection Caret(int block) => RichSelection.At(P(block, 0));

		// One letter per block: p paragraph, hN heading, b/n bullet/numbered item with its depth, q quote, r raw.
		private static string Shape(RichDocument document) => string.Join(",", document.Blocks.Select(b => b.Kind switch
		{
			RichBlockKind.Paragraph => "p",
			RichBlockKind.Heading => "h" + b.HeadingLevel,
			RichBlockKind.ListItem => (b.List.Ordered ? "n" : "b") + b.List.Depth,
			RichBlockKind.Quote => "q",
			_ => "r",
		}));

		// Each block's list info and the index of its list (by first appearance), to compare two models exactly.
		private static string ListModel(RichDocument document)
		{
			var groups = document.Blocks.Select(b => b.ListGroup).Where(g => g != null).Distinct().ToList();
			return string.Join(",", document.Blocks.Select(b => b.List == null
				? "-"
				: $"{groups.IndexOf(b.ListGroup)}:{b.List.Ordered}{b.List.Marker}{b.List.StartNumber}d{b.List.Depth}"));
		}

		/// <summary>
		/// Every group the blocks reference is one unbroken run, each list starts at Depth 0 with no skipped level
		/// and has one top-level kind, and members agree with their kind (only list items in lists, quotes in quotes).
		/// </summary>
		private static bool GroupsAreWellFormed(RichDocument document)
		{
			var blocks = document.Blocks;
			foreach (var group in blocks.SelectMany(b => new RichBlockGroup[] { b.AlignGroup, b.QuoteGroup, b.ListGroup }).Where(g => g != null).Distinct())
			{
				var indices = blocks.Select((b, i) => (b, i))
					.Where(x => x.b.AlignGroup == group || x.b.QuoteGroup == group || x.b.ListGroup == group)
					.Select(x => x.i).ToList();
				if (indices.Last() - indices.First() + 1 != indices.Count)
				{
					return false;
				}
			}

			RichListGroup list = null;
			int previousDepth = -1;
			bool topOrdered = false;
			foreach (var block in blocks)
			{
				if ((block.Kind == RichBlockKind.ListItem) != (block.ListGroup != null)
					|| (block.Kind == RichBlockKind.Quote) != (block.QuoteGroup != null)
					|| (block.AlignGroup != null && block.Kind != RichBlockKind.Paragraph && block.Kind != RichBlockKind.Heading))
				{
					return false;
				}

				if (block.ListGroup == null)
				{
					list = null;
					continue;
				}

				bool sameList = block.ListGroup == list;
				int allowed = sameList ? previousDepth + 1 : 0;
				if (block.List.Depth < 0 || block.List.Depth > allowed)
				{
					return false;
				}

				if (!sameList)
				{
					topOrdered = block.List.Ordered;
				}
				else if (block.List.Depth == 0 && block.List.Ordered != topOrdered)
				{
					return false;
				}

				list = block.ListGroup;
				previousDepth = block.List.Depth;
			}

			return true;
		}

		[Test]
		public async Task SetBlockKindMakesHeadingsAndNormal()
		{
			var document = RichMarkdownParser.Parse("one\n\ntwo\n\nthree\n");
			RichBlockOperations.SetBlockKind(document, Caret(1), 2);
			await Assert.That(Shape(document)).IsEqualTo("p,h2,p");
			await Assert.That(document.Blocks.Select(b => b.Dirty)).IsEquivalentTo(new[] { false, true, false }, CollectionOrdering.Matching);

			RichBlockOperations.SetBlockKind(document, Blocks(document, 0, 2), 1);
			await Assert.That(Shape(document)).IsEqualTo("h1,h1,h1");

			RichBlockOperations.SetBlockKind(document, Blocks(document, 0, 1), 0);
			await Assert.That(Shape(document)).IsEqualTo("p,p,h1");
		}

		[Test]
		public async Task SetBlockKindTakesListItemsAndQuotesOutOfTheirGroups()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n- c\n\n> q\n> \n> r\n");
			await Assert.That(Shape(document)).IsEqualTo("b0,b1,b0,q,q");
			RichBlockOperations.SetBlockKind(document, Caret(0), 3);

			// The rest of the list, starting with the heading's former child, is lifted to start at the top level.
			await Assert.That(Shape(document)).IsEqualTo("h3,b0,b0,q,q");
			await Assert.That(document.Blocks[0].ListGroup).IsNull();
			await Assert.That(document.Blocks[0].List).IsNull();

			RichBlockOperations.SetBlockKind(document, Caret(3), 0);
			await Assert.That(Shape(document)).IsEqualTo("h3,b0,b0,p,q");
			await Assert.That(document.Blocks[3].QuoteGroup).IsNull();
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task SelectionEndingAtABlockStartDoesNotTouchIt()
		{
			var document = RichMarkdownParser.Parse("one\n\ntwo\n");
			RichBlockOperations.SetBlockKind(document, new RichSelection(P(0, 1), P(1, 0)), 1);
			await Assert.That(Shape(document)).IsEqualTo("h1,p");
		}

		[Test]
		public async Task ToggleBulletOnAndOff()
		{
			var document = RichMarkdownParser.Parse("# one\n\ntwo\n\nthree\n");
			RichBlockOperations.ToggleList(document, Blocks(document, 0, 1), false);

			// A heading becomes a plain item: a list item holds one paragraph.
			await Assert.That(Shape(document)).IsEqualTo("b0,b0,p");
			await Assert.That(document.Blocks[0].ListGroup).IsSameReferenceAs(document.Blocks[1].ListGroup);
			await Assert.That(document.Blocks[0].ListGroup.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(document.Blocks[0].Dirty && document.Blocks[1].Dirty && !document.Blocks[2].Dirty).IsTrue();

			RichBlockOperations.ToggleList(document, Blocks(document, 0, 1), false);
			await Assert.That(Shape(document)).IsEqualTo("p,p,p");
			await Assert.That(document.Blocks.All(b => b.ListGroup == null && b.List == null)).IsTrue();
		}

		[Test]
		public async Task BulletAndNumberedConvertInPlace()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n- c\n");
			var group = document.Blocks[0].ListGroup;
			RichBlockOperations.ToggleList(document, Blocks(document, 0, 2), true);
			await Assert.That(Shape(document)).IsEqualTo("n0,n1,n0");
			await Assert.That(document.Blocks.All(b => b.ListGroup == document.Blocks[0].ListGroup)).IsTrue();
			await Assert.That(document.Blocks[0].List.Marker).IsEqualTo('.');

			// Numbered again everywhere: toggling off.
			RichBlockOperations.ToggleList(document, Blocks(document, 0, 2), true);
			await Assert.That(Shape(document)).IsEqualTo("p,p,p");
		}

		[Test]
		public async Task ConvertingTheMiddleOfAListSplitsItAndChildrenFollowTheirParent()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n  - c\n- d\n");
			RichBlockOperations.ToggleList(document, Caret(1), true);

			await Assert.That(Shape(document)).IsEqualTo("b0,n0,b1,b0");
			var blocks = document.Blocks;
			await Assert.That(blocks[1].ListGroup).IsSameReferenceAs(blocks[2].ListGroup);
			await Assert.That(blocks[0].ListGroup).IsNotSameReferenceAs(blocks[1].ListGroup);
			await Assert.That(blocks[3].ListGroup).IsNotSameReferenceAs(blocks[0].ListGroup);
			await Assert.That(blocks.All(b => b.ListGroup.OriginalMemberCount == 0)).IsTrue();
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();

			// Back to bullet: the three lists join into one again.
			RichBlockOperations.ToggleList(document, Caret(1), false);
			await Assert.That(Shape(document)).IsEqualTo("b0,b0,b1,b0");
			await Assert.That(blocks.All(b => b.ListGroup == blocks[0].ListGroup)).IsTrue();
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task ConvertingANestedItemKeepsItInItsParentsList()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n  - c\n- d\n");
			RichBlockOperations.ToggleList(document, Caret(1), true);

			await Assert.That(Shape(document)).IsEqualTo("b0,n1,b1,b0");
			await Assert.That(document.Blocks.All(b => b.ListGroup == document.Blocks[0].ListGroup)).IsTrue();
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task ANestedItemStaysInItsListWhenAParagraphBelowBecomesAnItem()
		{
			var document = RichMarkdownParser.Parse("- a\n  1. b\n\npara\n");
			await Assert.That(Shape(document)).IsEqualTo("b0,n1,p");
			RichBlockOperations.ToggleList(document, Blocks(document, 1, 2), true);

			await Assert.That(Shape(document)).IsEqualTo("b0,n1,n0");
			await Assert.That(document.Blocks[1].ListGroup).IsSameReferenceAs(document.Blocks[0].ListGroup);
			await Assert.That(document.Blocks[2].ListGroup).IsNotSameReferenceAs(document.Blocks[0].ListGroup);
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task OutdentTakesTheStyleOfItsNewLevel()
		{
			var document = RichMarkdownParser.Parse("- a\n  1. b\n");
			RichBlockOperations.Outdent(document, Caret(1));

			// b joins a's level as a bullet rather than splitting the list in two.
			await Assert.That(Shape(document)).IsEqualTo("b0,b0");
			await Assert.That(document.Blocks[1].ListGroup).IsSameReferenceAs(document.Blocks[0].ListGroup);
			await Assert.That(document.Blocks[1].List.Marker).IsEqualTo('-');
		}

		[Test]
		public async Task IndentTakesTheStyleOfItsNewLevel()
		{
			// The first item of a new sublist numbers from 1, not from where the outer list was.
			var numbered = RichMarkdownParser.Parse("5. a\n6. b\n");
			RichBlockOperations.Indent(numbered, Caret(1));
			await Assert.That(Shape(numbered)).IsEqualTo("n0,n1");
			await Assert.That(numbered.Blocks[1].List.StartNumber).IsEqualTo(1);
			await Assert.That(numbered.Blocks[0].List.StartNumber).IsEqualTo(5);

			// Under an existing sublist the item takes that sublist's kind and numbering.
			// (A sublist not starting at 1 cannot interrupt a's text, so it follows a blank line.)
			var mixed = RichMarkdownParser.Parse("- a\n\n  3) x\n\n- b\n");
			await Assert.That(Shape(mixed)).IsEqualTo("b0,n1,b0");
			RichBlockOperations.Indent(mixed, Caret(2));
			await Assert.That(Shape(mixed)).IsEqualTo("b0,n1,n1");
			await Assert.That(mixed.Blocks[2].List.Marker).IsEqualTo(')');
			await Assert.That(mixed.Blocks[2].List.StartNumber).IsEqualTo(3);
		}

		[Test]
		public async Task ATopLevelItemLeavingKeepsTheSubtreesAfterIt()
		{
			// Only a's own children are lifted; c keeps its child d.
			var outdented = RichMarkdownParser.Parse("- a\n  - b\n- c\n  - d\n");
			RichBlockOperations.Outdent(outdented, Caret(0));
			await Assert.That(Shape(outdented)).IsEqualTo("p,b0,b0,b1");
			await Assert.That(GroupsAreWellFormed(outdented)).IsTrue();

			var backspaced = RichMarkdownParser.Parse("- a\n  - b\n- c\n  - d\n");
			RichEditOperations.Backspace(backspaced, P(0, 0));
			await Assert.That(Shape(backspaced)).IsEqualTo("p,b0,b0,b1");

			var toggled = RichMarkdownParser.Parse("- p\n  - c\n    - g\n- q\n  - q1\n");
			RichBlockOperations.ToggleList(toggled, Caret(1), false);
			await Assert.That(Shape(toggled)).IsEqualTo("b0,p,b0,b0,b1");
			await Assert.That(GroupsAreWellFormed(toggled)).IsTrue();
		}

		[Test]
		public async Task BackspaceAtANestedItemsStartOutdentsLikeShiftTab()
		{
			var shiftTab = RichMarkdownParser.Parse("- a\n  1. b\n- c\n");
			RichBlockOperations.Outdent(shiftTab, Caret(1));
			var backspace = RichMarkdownParser.Parse("- a\n  1. b\n- c\n");
			RichEditOperations.Backspace(backspace, P(1, 0));

			await Assert.That(Shape(shiftTab)).IsEqualTo("b0,b0,b0");
			await Assert.That(Shape(backspace)).IsEqualTo(Shape(shiftTab));
			await Assert.That(backspace.Blocks.All(b => b.ListGroup == backspace.Blocks[0].ListGroup)).IsTrue();
			await Assert.That(backspace.Blocks[1].List.Marker).IsEqualTo(shiftTab.Blocks[1].List.Marker);
			await Assert.That(ListModel(backspace)).IsEqualTo(ListModel(shiftTab));
		}

		[Test]
		public async Task AnEmptyDocumentIsANoOp()
		{
			var document = RichMarkdownParser.Parse("");
			await Assert.That(document.Blocks.Count).IsEqualTo(0);
			var caret = Caret(0);

			await Assert.That(RichBlockOperations.BlockStateAt(document, caret)).IsEqualTo(new RichBlockState(0, RichListKind.None, false, null, false));
			RichBlockOperations.SetBlockKind(document, caret, 1);
			RichBlockOperations.ToggleList(document, caret, true);
			RichBlockOperations.ToggleQuote(document, caret);
			RichBlockOperations.SetAlignment(document, caret, RichAlignment.Center);
			await Assert.That(RichBlockOperations.Indent(document, caret)).IsFalse();
			await Assert.That(RichBlockOperations.Outdent(document, caret)).IsFalse();
			await Assert.That(RichBlockOperations.TryApplyLineStartShortcut(document, P(0, 2), out _)).IsFalse();
			await Assert.That(document.Blocks.Count).IsEqualTo(0);
		}

		[Test]
		public async Task NewItemsJoinATouchingListOfTheSameKind()
		{
			var document = RichMarkdownParser.Parse("1. a\n2. b\n\nc\n\n3. d\n");
			await Assert.That(document.Blocks[0].ListGroup).IsNotSameReferenceAs(document.Blocks[3].ListGroup);
			RichBlockOperations.ToggleList(document, Caret(2), true);

			await Assert.That(Shape(document)).IsEqualTo("n0,n0,n0,n0");
			await Assert.That(document.Blocks.All(b => b.ListGroup == document.Blocks[0].ListGroup)).IsTrue();

			// The joined items number on from the list's first item rather than restarting.
			await Assert.That(document.Blocks.All(b => b.List.StartNumber == 1 && b.List.Marker == '.')).IsTrue();
			await Assert.That(document.Blocks[3].Dirty).IsTrue();
		}

		[Test]
		public async Task ANewListBesideADifferentKindStaysSeparateAndRegenerates()
		{
			var document = RichMarkdownParser.Parse("- a\n\nb\n");
			var bullets = document.Blocks[0].ListGroup;
			RichBlockOperations.ToggleList(document, Caret(1), true);

			await Assert.That(Shape(document)).IsEqualTo("b0,n0");
			await Assert.That(document.Blocks[1].ListGroup).IsNotSameReferenceAs(bullets);
			await Assert.That(bullets.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(document.Blocks[0].Dirty).IsFalse();
		}

		[Test]
		public async Task ToggleQuoteOnJoinsAndOffSplits()
		{
			var document = RichMarkdownParser.Parse("> a\n\n## b\n\n- c\n\n> d\n");
			RichBlockOperations.ToggleQuote(document, Blocks(document, 1, 2));
			await Assert.That(Shape(document)).IsEqualTo("q,q,q,q");
			await Assert.That(document.Blocks.All(b => b.QuoteGroup == document.Blocks[0].QuoteGroup)).IsTrue();
			await Assert.That(document.Blocks[1].HeadingLevel).IsEqualTo(0);
			await Assert.That(document.Blocks[2].List).IsNull();

			RichBlockOperations.ToggleQuote(document, Caret(1));
			await Assert.That(Shape(document)).IsEqualTo("q,p,q,q");
			await Assert.That(document.Blocks[2].QuoteGroup).IsNotSameReferenceAs(document.Blocks[0].QuoteGroup);
			await Assert.That(document.Blocks[2].QuoteGroup.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();

			// A mixed selection turns the quote on for all of it rather than off.
			RichBlockOperations.ToggleQuote(document, Blocks(document, 0, 1));
			await Assert.That(Shape(document)).IsEqualTo("q,q,q,q");
		}

		[Test]
		public async Task AlignmentAppliesToParagraphsAndHeadingsOnly()
		{
			var document = RichMarkdownParser.Parse("# h\n\np\n\n- item\n\n> quote\n");
			RichBlockOperations.SetAlignment(document, Blocks(document, 0, 3), RichAlignment.Center);

			await Assert.That(document.Blocks.Select(b => b.Alignment)).IsEquivalentTo(
				new[] { RichAlignment.Center, RichAlignment.Center, RichAlignment.Left, RichAlignment.Left }, CollectionOrdering.Matching);
			await Assert.That(document.Blocks[2].Dirty || document.Blocks[3].Dirty).IsFalse();

			var state = RichBlockOperations.BlockStateAt(document, Caret(2));
			await Assert.That(state.CanAlign).IsFalse();
		}

		[Test]
		public async Task RealigningLeavesTheWrapperAndSplitsIt()
		{
			var document = RichMarkdownParser.Parse("<div align=\"center\">\n\na\n\nb\n\nc\n\n</div>\n");
			await Assert.That(Shape(document)).IsEqualTo("p,p,p");
			var wrapper = document.Blocks[0].AlignGroup;
			await Assert.That(wrapper).IsNotNull();

			RichBlockOperations.SetAlignment(document, Caret(1), RichAlignment.Right);
			await Assert.That(document.Blocks[1].AlignGroup).IsNull();
			await Assert.That(document.Blocks[1].Alignment).IsEqualTo(RichAlignment.Right);
			await Assert.That(document.Blocks[0].AlignGroup).IsSameReferenceAs(wrapper);
			await Assert.That(document.Blocks[2].AlignGroup).IsNotSameReferenceAs(wrapper);
			await Assert.That(document.Blocks[2].AlignGroup.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(document.Blocks[2].Alignment).IsEqualTo(RichAlignment.Center);
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task IndentCarriesTheSubtreeAndNeverSkipsALevel()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n  - c\n- d\n");

			// The first item has nothing to nest under.
			await Assert.That(RichBlockOperations.Indent(document, Caret(0))).IsTrue();
			await Assert.That(Shape(document)).IsEqualTo("b0,b0,b1,b0");

			RichBlockOperations.Indent(document, Caret(1));
			await Assert.That(Shape(document)).IsEqualTo("b0,b1,b2,b0");
			await Assert.That(document.Blocks[2].Dirty).IsTrue();

			// c is already one level below b; another Tab would skip a level.
			RichBlockOperations.Indent(document, Caret(2));
			await Assert.That(Shape(document)).IsEqualTo("b0,b1,b2,b0");

			// A selection of siblings indents together; c moves once, with b.
			// Over b..d only d can go deeper: b and c are each already one level below the item above.
			RichBlockOperations.Indent(document, Blocks(document, 1, 3));
			await Assert.That(Shape(document)).IsEqualTo("b0,b1,b2,b1");
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task IndentOfSiblingsKeepsListsWellFormed()
		{
			var document = RichMarkdownParser.Parse("- a\n- b\n- c\n\npara\n");
			await Assert.That(RichBlockOperations.Indent(document, Blocks(document, 1, 2))).IsTrue();
			await Assert.That(Shape(document)).IsEqualTo("b0,b1,b1,p");
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();

			// Tab outside a list is not a list command.
			await Assert.That(RichBlockOperations.Indent(document, Caret(3))).IsFalse();
		}

		[Test]
		public async Task OutdentCarriesTheSubtreeAndLeavesTheListFromTheTop()
		{
			var document = RichMarkdownParser.Parse("- a\n  - b\n    - c\n  - d\n- e\n");
			RichBlockOperations.Outdent(document, Caret(1));
			await Assert.That(Shape(document)).IsEqualTo("b0,b0,b1,b1,b0");
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();

			// Outdenting a top-level item leaves the list; its children start a list of their own.
			RichBlockOperations.Outdent(document, Caret(1));
			await Assert.That(Shape(document)).IsEqualTo("b0,p,b0,b0,b0");
			var blocks = document.Blocks;
			await Assert.That(blocks[2].ListGroup).IsNotSameReferenceAs(blocks[0].ListGroup);
			await Assert.That(blocks[2].ListGroup.OriginalMemberCount).IsEqualTo(0);
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}

		[Test]
		public async Task ShortcutsConvertAParagraph()
		{
			foreach (var (typed, shape) in new[] { ("# ", "h1"), ("## ", "h2"), ("### ", "h3"), ("- ", "b0"), ("* ", "b0"), ("1. ", "n0"), ("> ", "q") })
			{
				var document = RichMarkdownParser.Parse("text\n");
				RichEditOperations.InsertText(document, P(0, 0), typed);
				bool applied = RichBlockOperations.TryApplyLineStartShortcut(document, P(0, typed.Length), out var caret);

				await Assert.That(applied).IsTrue();
				await Assert.That(Shape(document)).IsEqualTo(shape);
				await Assert.That(Text(document.Blocks[0])).IsEqualTo("text");
				await Assert.That(caret).IsEqualTo(P(0, 0));
				await Assert.That(document.Blocks[0].Dirty).IsTrue();
				await Assert.That(GroupsAreWellFormed(document)).IsTrue();
			}
		}

		[Test]
		public async Task NumberedShortcutKeepsItsStartAndStarKeepsItsMarker()
		{
			var numbered = RichMarkdownParser.Parse("x\n");
			numbered.Blocks[0].Inlines = new() { new RichRun("7. ") };
			await Assert.That(RichBlockOperations.TryApplyLineStartShortcut(numbered, P(0, 3), out _)).IsTrue();
			await Assert.That(numbered.Blocks[0].List.StartNumber).IsEqualTo(7);

			var star = RichMarkdownParser.Parse("x\n");
			star.Blocks[0].Inlines = new() { new RichRun("* ") };
			RichBlockOperations.TryApplyLineStartShortcut(star, P(0, 2), out _);
			await Assert.That(star.Blocks[0].List.Marker).IsEqualTo('*');
		}

		[Test]
		public async Task ShortcutJoinsAListAbove()
		{
			var document = RichMarkdownParser.Parse("- a\n\nb\n");
			document.Blocks[1].Inlines = new() { new RichRun("- ") };
			RichBlockOperations.TryApplyLineStartShortcut(document, P(1, 2), out _);
			await Assert.That(Shape(document)).IsEqualTo("b0,b0");
			await Assert.That(document.Blocks[1].ListGroup).IsSameReferenceAs(document.Blocks[0].ListGroup);
		}

		[Test]
		public async Task NonMatchingTextIsNotAShortcut()
		{
			foreach (var (text, caret) in new[] { ("#x ", 3), ("abc # ", 6), ("#### ", 5), ("1) ", 3), ("a. ", 3), ("# ", 1), ("-", 1), ("1234567890. ", 12) })
			{
				var document = RichMarkdownParser.Parse("x\n");
				document.Blocks[0].Inlines = new() { new RichRun(text) };
				await Assert.That(RichBlockOperations.TryApplyLineStartShortcut(document, P(0, caret), out var after)).IsFalse();
				await Assert.That(Shape(document)).IsEqualTo("p");
				await Assert.That(Text(document.Blocks[0])).IsEqualTo(text);
				await Assert.That(after).IsEqualTo(P(0, caret));
			}

			// Only plain paragraphs: in a heading or list item "- " is text.
			var heading = RichMarkdownParser.Parse("# x\n");
			heading.Blocks[0].Inlines = new() { new RichRun("- ") };
			await Assert.That(RichBlockOperations.TryApplyLineStartShortcut(heading, P(0, 2), out _)).IsFalse();

			// A marker in inline code is content.
			var code = RichMarkdownParser.Parse("x\n");
			code.Blocks[0].Inlines = new() { new RichRun("# ") { Code = true } };
			await Assert.That(RichBlockOperations.TryApplyLineStartShortcut(code, P(0, 2), out _)).IsFalse();
		}

		[Test]
		public async Task BlockStateReportsSharedAndMixedState()
		{
			var document = RichMarkdownParser.Parse("## a\n\n## b\n\n- c\n\n1. d\n\n> e\n");
			var headings = RichBlockOperations.BlockStateAt(document, Blocks(document, 0, 1));
			await Assert.That(headings).IsEqualTo(new RichBlockState(2, RichListKind.None, false, RichAlignment.Left, true));

			var mixed = RichBlockOperations.BlockStateAt(document, Blocks(document, 1, 3));
			await Assert.That(mixed.HeadingLevel).IsNull();
			await Assert.That(mixed.List).IsEqualTo(RichListKind.Mixed);

			var bullet = RichBlockOperations.BlockStateAt(document, Caret(2));
			await Assert.That(bullet).IsEqualTo(new RichBlockState(0, RichListKind.Bullet, false, null, false));

			var quote = RichBlockOperations.BlockStateAt(document, Caret(4));
			await Assert.That(quote.Quote).IsTrue();

			RichBlockOperations.SetAlignment(document, Caret(0), RichAlignment.Right);
			await Assert.That(RichBlockOperations.BlockStateAt(document, Blocks(document, 0, 1)).Alignment).IsNull();
		}

		[Test]
		public async Task RawBlocksAreLeftAlone()
		{
			var document = RichMarkdownParser.Parse("a\n\n---\n\nb\n");
			RichBlockOperations.ToggleList(document, Blocks(document, 0, 2), false);

			// The thematic break stays, between two new lists.
			await Assert.That(Shape(document)).IsEqualTo("b0,r,b0");
			await Assert.That(document.Blocks[1].Dirty).IsFalse();
			await Assert.That(document.Blocks[0].ListGroup).IsNotSameReferenceAs(document.Blocks[2].ListGroup);
			await Assert.That(GroupsAreWellFormed(document)).IsTrue();
		}
	}
}
