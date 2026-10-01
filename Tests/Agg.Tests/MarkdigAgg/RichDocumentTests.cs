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
	public class RichDocumentTests
	{
		// "ab" + image atom + "cd" (bold): plain text "ab?cd", length 5.
		private static List<RichInline> MixedInlines() => new List<RichInline>
		{
			new RichRun("ab"),
			new InlineAtom(InlineAtomKind.Image, "![x](x.png)"),
			new RichRun("cd") { Bold = true },
		};

		[Test]
		public async Task LocateResolvesOffsetsAcrossRunsAndAtoms()
		{
			var inlines = MixedInlines();
			await Assert.That(RichInlines.Length(inlines)).IsEqualTo(5);

			await Assert.That(RichInlines.Locate(inlines, 0)).IsEqualTo((0, 0));
			await Assert.That(RichInlines.Locate(inlines, 1)).IsEqualTo((0, 1));

			// Boundaries resolve to the end of the earlier inline unless the caller prefers the next one.
			await Assert.That(RichInlines.Locate(inlines, 2)).IsEqualTo((0, 2));
			await Assert.That(RichInlines.Locate(inlines, 2, preferNext: true)).IsEqualTo((1, 0));
			await Assert.That(RichInlines.Locate(inlines, 3)).IsEqualTo((1, 1));
			await Assert.That(RichInlines.Locate(inlines, 3, preferNext: true)).IsEqualTo((2, 0));
			await Assert.That(RichInlines.Locate(inlines, 4)).IsEqualTo((2, 1));
			await Assert.That(RichInlines.Locate(inlines, 5)).IsEqualTo((2, 2));
			await Assert.That(RichInlines.Locate(inlines, 5, preferNext: true)).IsEqualTo((2, 2));

			for (int offset = 0; offset <= 5; offset++)
			{
				foreach (bool preferNext in new[] { false, true })
				{
					var (index, inInline) = RichInlines.Locate(inlines, offset, preferNext);
					await Assert.That(RichInlines.OffsetOf(inlines, index, inInline)).IsEqualTo(offset);
				}
			}

			await Assert.That(RichInlines.Locate(new List<RichInline>(), 0)).IsEqualTo((0, 0));
		}

		[Test]
		public async Task SplitAtMakesABoundaryAndMergeAdjacentUndoesIt()
		{
			var inlines = MixedInlines();

			int index = RichInlines.SplitAt(inlines, 4);
			await Assert.That(index).IsEqualTo(3);
			await Assert.That(inlines.Count).IsEqualTo(4);
			var head = (RichRun)inlines[2];
			var tail = (RichRun)inlines[3];
			await Assert.That(head.Text).IsEqualTo("c");
			await Assert.That(tail.Text).IsEqualTo("d");
			await Assert.That(tail.Bold).IsTrue();

			// Offsets already on a boundary do not split.
			await Assert.That(RichInlines.SplitAt(inlines, 2)).IsEqualTo(1);
			await Assert.That(RichInlines.SplitAt(inlines, 0)).IsEqualTo(0);
			await Assert.That(RichInlines.SplitAt(inlines, 5)).IsEqualTo(4);
			await Assert.That(inlines.Count).IsEqualTo(4);

			RichInlines.MergeAdjacent(inlines);
			await Assert.That(inlines.Count).IsEqualTo(3);
			await Assert.That(((RichRun)inlines[2]).Text).IsEqualTo("cd");
		}

		[Test]
		public async Task MergeAdjacentKeepsDifferentStylesAndAtomsApartAndDropsEmptyRuns()
		{
			var inlines = new List<RichInline>
			{
				new RichRun("a"),
				new RichRun(""),
				new RichRun("b"),
				new RichRun("c") { Italic = true },
				new RichRun("d") { Italic = true, LinkUrl = "https://x" },
				new InlineAtom(InlineAtomKind.HardBreak, "  \n"),
				new RichRun("e"),
			};

			RichInlines.MergeAdjacent(inlines);

			// Joined with a separator so the check covers order as well as membership.
			var texts = string.Join("|", inlines.Select(inline => inline.ToString()));
			await Assert.That(texts).IsEqualTo("ab|c|d|  \n|e");
		}

		[Test]
		public async Task MergeAdjacentKeepsRunsWithDifferentLinkTitlesApart()
		{
			var inlines = new List<RichInline>
			{
				new RichRun("a") { LinkUrl = "u", LinkTitle = "one" },
				new RichRun("b") { LinkUrl = "u", LinkTitle = "two" },
			};

			RichInlines.MergeAdjacent(inlines);

			await Assert.That(inlines.Count).IsEqualTo(2);
			await Assert.That(((RichRun)inlines[1].Clone()).LinkTitle).IsEqualTo("two");
		}

		[Test]
		public async Task RunsWithDifferentLinkLabelsAreDifferentStyles()
		{
			var inline = new RichRun("a") { LinkUrl = "u" };
			var reference = new RichRun("b") { LinkUrl = "u", LinkLabel = "ref" };
			var otherReference = new RichRun("c") { LinkUrl = "u", LinkLabel = "other" };

			await Assert.That(inline.HasSameStyle(reference)).IsFalse();
			await Assert.That(reference.HasSameStyle(otherReference)).IsFalse();
			await Assert.That(reference.HasSameStyle(reference.WithText("d"))).IsTrue();
			await Assert.That(((RichRun)reference.Clone()).LinkLabel).IsEqualTo("ref");
		}

		[Test]
		public async Task LocateAndOffsetOfThrowOutOfRange()
		{
			var inlines = MixedInlines();

			await Assert.That(() => RichInlines.Locate(inlines, -1)).Throws<System.ArgumentOutOfRangeException>();
			await Assert.That(() => RichInlines.Locate(inlines, 6)).Throws<System.ArgumentOutOfRangeException>();
			await Assert.That(() => RichInlines.OffsetOf(inlines, 4, 0)).Throws<System.ArgumentOutOfRangeException>();
			await Assert.That(() => RichInlines.OffsetOf(inlines, 0, 3)).Throws<System.ArgumentOutOfRangeException>();
			await Assert.That(() => RichInlines.OffsetOf(inlines, 1, 2)).Throws<System.ArgumentOutOfRangeException>();
			await Assert.That(() => RichInlines.OffsetOf(inlines, 3, 1)).Throws<System.ArgumentOutOfRangeException>();
			await Assert.That(RichInlines.OffsetOf(inlines, 3, 0)).IsEqualTo(5);
		}

		[Test]
		public async Task LocateSkipsLeadingAndTrailingEmptyRuns()
		{
			var inlines = new List<RichInline> { new RichRun(""), new RichRun("ab"), new RichRun("") };

			// Offset 0 lands in the first non-empty run, not the empty run at index 0.
			await Assert.That(RichInlines.Locate(inlines, 0)).IsEqualTo((1, 0));
			await Assert.That(RichInlines.Locate(inlines, 2)).IsEqualTo((1, 2));
			await Assert.That(RichInlines.Locate(inlines, 2, preferNext: true)).IsEqualTo((2, 0));
		}

		[Test]
		public async Task SplitAtTheEndOrOnAnEmptyListDoesNotSplit()
		{
			var trailingEmpty = new List<RichInline> { new RichRun("ab"), new RichRun("") };
			int index = RichInlines.SplitAt(trailingEmpty, 2);
			await Assert.That(index).IsEqualTo(1);
			await Assert.That(trailingEmpty.Count).IsEqualTo(2);
			await Assert.That(((RichRun)trailingEmpty[0]).Text).IsEqualTo("ab");

			var empty = new List<RichInline>();
			await Assert.That(RichInlines.SplitAt(empty, 0)).IsEqualTo(0);
			await Assert.That(empty.Count).IsEqualTo(0);
		}

		[Test]
		public async Task PositionsOrderByBlockThenCellThenOffset()
		{
			var a = new DocPosition(1, 5);
			var b = new DocPosition(2, 0);
			await Assert.That(a < b).IsTrue();
			await Assert.That(b > a).IsTrue();
			await Assert.That(new DocPosition(1, 5) == a).IsTrue();
			await Assert.That(DocPosition.Min(b, a)).IsEqualTo(a);
			await Assert.That(DocPosition.Max(a, b)).IsEqualTo(b);

			// Within a table: rows first, then columns, then the offset in the cell.
			var lateInFirstRow = new DocPosition(3, 9, Row: 0, Column: 1);
			var startOfSecondRow = new DocPosition(3, 0, Row: 1, Column: 0);
			await Assert.That(lateInFirstRow < startOfSecondRow).IsTrue();
			await Assert.That(new DocPosition(3, 9, 0, 0) < lateInFirstRow).IsTrue();
		}

		[Test]
		public async Task TableCellsAreAddressedByRowAndColumn()
		{
			var table = new RichBlock { Kind = RichBlockKind.Table };
			table.ColumnAlignments.AddRange(new RichAlignment?[] { null, RichAlignment.Right });
			table.TableRows.Add(new List<RichTableCell> { Cell("Name"), Cell("Qty") });
			table.TableRows.Add(new List<RichTableCell> { Cell("Bolt"), Cell("12") });

			var document = new RichDocument();
			document.Blocks.Add(new RichBlock { Kind = RichBlockKind.Paragraph, Inlines = { new RichRun("intro") } });
			document.Blocks.Add(table);

			await Assert.That(document.TextLength(new DocPosition(0, 0))).IsEqualTo(5);
			await Assert.That(document.TextLength(new DocPosition(1, 0, Row: 1, Column: 1))).IsEqualTo(2);
			await Assert.That(document.TextLength(new DocPosition(1, 0, Row: 0, Column: 0))).IsEqualTo(4);
			await Assert.That(table.InlinesAt(1, 0)[0].ToString()).IsEqualTo("Bolt");
		}

		[Test]
		public async Task CodeAndRawBlocksHaveTheirOwnLengths()
		{
			var code = new RichBlock { Kind = RichBlockKind.CodeBlock, CodeText = "a\nbc", CodeLanguage = "cs" };
			var raw = new RichBlock { Kind = RichBlockKind.Raw, OriginalSource = "<hr/>" };

			await Assert.That(code.TextLength()).IsEqualTo(4);
			await Assert.That(raw.TextLength()).IsEqualTo(1);
			await Assert.That(code.IsTextBlock).IsFalse();
		}

		[Test]
		public async Task CloneSharesNoMutableState()
		{
			var table = new RichBlock { Kind = RichBlockKind.Table };
			table.ColumnAlignments.Add(RichAlignment.Center);
			table.TableRows.Add(new List<RichTableCell> { Cell("cell") });
			var item = new RichBlock
			{
				Kind = RichBlockKind.ListItem,
				List = new RichListInfo { Ordered = true, Depth = 1, Marker = '.', StartNumber = 3 },
				Inlines = { new RichRun("item") { Bold = true } },
				OriginalSource = "3. **item**",
				SeparatorBefore = "\n\n",
			};
			var original = new RichDocument { Frontmatter = "---\na: 1\n---\n", TrailingText = "\n" };
			original.Blocks.Add(item);
			original.Blocks.Add(table);

			var copy = original.Clone();
			var copyItem = copy.Blocks[0];
			((RichRun)copyItem.Inlines[0]).Text = "changed";
			copyItem.Inlines.Add(new RichRun("more"));
			copyItem.List.Depth = 0;
			copyItem.Dirty = true;
			copy.Blocks[1].TableRows[0][0].Inlines.Clear();
			copy.Blocks[1].ColumnAlignments[0] = RichAlignment.Left;
			copy.Blocks.RemoveAt(1);

			await Assert.That(original.Blocks.Count).IsEqualTo(2);
			await Assert.That(item.Inlines.Count).IsEqualTo(1);
			await Assert.That(((RichRun)item.Inlines[0]).Text).IsEqualTo("item");
			await Assert.That(item.List.Depth).IsEqualTo(1);
			await Assert.That(item.Dirty).IsFalse();
			await Assert.That(table.TableRows[0][0].Inlines.Count).IsEqualTo(1);
			await Assert.That(table.ColumnAlignments[0]).IsEqualTo(RichAlignment.Center);

			// Unchanged values carried over.
			await Assert.That(copy.Frontmatter).IsEqualTo(original.Frontmatter);
			await Assert.That(copyItem.List.StartNumber).IsEqualTo(3);
			await Assert.That(copyItem.SeparatorBefore).IsEqualTo("\n\n");
		}

		private static RichTableCell Cell(string text) => new RichTableCell { Inlines = { new RichRun(text) } };
	}
}
