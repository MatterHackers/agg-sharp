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
	public class RichTableCodeOperationsTests
	{
		private const string Code = "Intro\n\n```cs\nab\ncd\n```\n";

		private const string Table = "| h1 | h2 |\n| --- | :-: |\n| a | b |\n";

		private static string Cell(RichBlock table, int row, int column)
		{
			return string.Concat(table.TableRows[row][column].Inlines.Select(i => i is RichRun run ? run.Text : "@"));
		}

		// The table as text, rows joined by '/', cells by ',', so a shape reads at a glance.
		private static string Grid(RichBlock table)
		{
			return string.Join("/", table.TableRows.Select((row, r) => string.Join(",", row.Select((_, c) => Cell(table, r, c)))));
		}

		private static DocPosition T(int row, int column, int offset) => new DocPosition(0, offset, row, column);

		/// <summary>
		/// A caret the editor can show: its block exists, a table's row and column exist (0 elsewhere), and the
		/// offset is within that block or cell.
		/// </summary>
		private static bool IsValid(RichDocument document, DocPosition position)
		{
			if (position.BlockIndex < 0 || position.BlockIndex >= document.Blocks.Count)
			{
				return false;
			}

			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.Table)
			{
				if (position.Row < 0 || position.Row >= block.TableRows.Count
					|| position.Column < 0 || position.Column >= block.TableRows[position.Row].Count)
				{
					return false;
				}
			}
			else if (position.Row != 0 || position.Column != 0)
			{
				return false;
			}

			return position.Offset >= 0 && position.Offset <= document.TextLength(position);
		}

		[Test]
		public async Task TypingInCodeKeepsNewlinesAndEmojiDeleteWhole()
		{
			var document = RichMarkdownParser.Parse(Code);
			var code = document.Blocks[1];
			await Assert.That(code.Dirty).IsFalse();

			var selection = RichEditOperations.InsertText(document, new DocPosition(1, 2), "\n😀x");
			await Assert.That(code.CodeText).IsEqualTo("ab\n😀x\ncd");
			await Assert.That(code.Dirty).IsTrue();
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(1, 6));
			await Assert.That(document.Blocks.Count).IsEqualTo(2);

			// Backspace takes x, then the whole emoji (two UTF-16 units), then the newline.
			selection = RichEditOperations.Backspace(document, selection.Caret);
			selection = RichEditOperations.Backspace(document, selection.Caret);
			await Assert.That(code.CodeText).IsEqualTo("ab\n\ncd");
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(1, 3));
			selection = RichEditOperations.Backspace(document, selection.Caret);
			await Assert.That(code.CodeText).IsEqualTo("ab\ncd");

			// Forward Delete takes the newline after the caret; at the end nothing happens.
			selection = RichEditOperations.Delete(document, selection.Caret);
			await Assert.That(code.CodeText).IsEqualTo("abcd");
			selection = RichEditOperations.Delete(document, new DocPosition(1, 4));
			await Assert.That(code.CodeText).IsEqualTo("abcd");
			await Assert.That(IsValid(document, selection.Caret)).IsTrue();

			// Backspace at a non-empty code block's start does nothing; the block never merges.
			RichEditOperations.Backspace(document, new DocPosition(1, 0));
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
			await Assert.That(code.Kind).IsEqualTo(RichBlockKind.CodeBlock);
		}

		[Test]
		public async Task EnterInCodeAddsALineAndTwiceAtTheEndLeavesTheBlock()
		{
			var document = RichMarkdownParser.Parse(Code);
			var code = document.Blocks[1];

			// Enter mid-text is a newline, never a split.
			var selection = RichEditOperations.SplitBlock(document, new DocPosition(1, 1));
			await Assert.That(code.CodeText).IsEqualTo("a\nb\ncd");
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(1, 2));

			selection = RichEditOperations.SplitBlock(document, new DocPosition(1, code.CodeText.Length));
			await Assert.That(code.CodeText).IsEqualTo("a\nb\ncd\n");
			await Assert.That(document.Blocks.Count).IsEqualTo(2);

			selection = RichEditOperations.SplitBlock(document, selection.Caret);
			await Assert.That(code.CodeText).IsEqualTo("a\nb\ncd");
			await Assert.That(document.Blocks.Count).IsEqualTo(3);
			await Assert.That(document.Blocks[2].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(document.Blocks[2].Dirty).IsTrue();
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(2, 0));
		}

		[Test]
		public async Task CodeRangeDeleteAndWholeBlockSelection()
		{
			var document = RichMarkdownParser.Parse(Code);
			var selection = RichEditOperations.DeleteRange(document, new DocPosition(1, 4), new DocPosition(1, 1));
			await Assert.That(document.Blocks[1].CodeText).IsEqualTo("ad");
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(1, 1));

			// Selecting all of the code's text and deleting leaves an empty code block; pasting then replaces it.
			selection = RichEditOperations.DeleteSelection(document, new RichSelection(new DocPosition(1, 0), new DocPosition(1, 2)));
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.CodeBlock);
			await Assert.That(document.Blocks[1].CodeText).IsEqualTo("");
			RichEditOperations.InsertText(document, selection.Caret, "new\ncode");
			await Assert.That(document.Blocks[1].CodeText).IsEqualTo("new\ncode");
			await Assert.That(document.Blocks.Count).IsEqualTo(2);

			// The block selected whole, as Backspace from the paragraph after it selects it, goes.
			document.Blocks.Add(new RichBlock { Kind = RichBlockKind.Paragraph });
			var whole = RichEditOperations.Backspace(document, new DocPosition(2, 0));
			await Assert.That(whole.WholeBlock).IsTrue();
			RichEditOperations.DeleteSelection(document, whole);
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
			await Assert.That(document.Blocks.All(b => b.Kind == RichBlockKind.Paragraph)).IsTrue();
		}

		[Test]
		public async Task BackspaceInAnEmptyCodeBlockMakesItAParagraph()
		{
			var document = RichMarkdownParser.Parse("```\nx\n```\n");
			var selection = RichEditOperations.Backspace(document, new DocPosition(0, 1));
			selection = RichEditOperations.Backspace(document, selection.Caret);
			await Assert.That(document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(document.Blocks[0].Dirty).IsTrue();
			await Assert.That(IsValid(document, selection.Caret)).IsTrue();

			// Nothing of the code block is left behind for the writer or layout to trip over.
			var block = document.Blocks[0];
			await Assert.That(block.CodeText + block.CodeFence + block.CodeInfo + block.CodeLanguage).IsEqualTo("");
		}

		[Test]
		public async Task SelectingAOneCellTablesTextDeletesTheTextNotTheTable()
		{
			var document = RichMarkdownParser.Parse("| h |\n| --- |\n");
			var selection = RichEditOperations.DeleteSelection(document, new RichSelection(T(0, 0, 0), T(0, 0, 1)));
			await Assert.That(document.Blocks.Single().Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(Grid(document.Blocks[0])).IsEqualTo("");
			await Assert.That(selection.Caret).IsEqualTo(T(0, 0, 0));
		}

		[Test]
		public async Task PastingLinesIntoTheLastRowLeavesTheBlockBelowIntact()
		{
			var document = RichMarkdownParser.Parse(Table + "\nHello\n");
			var selection = RichEditOperations.InsertText(document, T(1, 0, 1), "a\nb\nc");
			await Assert.That(Grid(document.Blocks[0])).IsEqualTo("h1,h2/aa,b");
			await Assert.That(document.Blocks.Count).IsEqualTo(4);

			// The rest of the paste went on as ordinary lines in new paragraphs between the table and Hello.
			await Assert.That(string.Concat(document.Blocks[1].Inlines.Select(i => ((RichRun)i).Text))).IsEqualTo("b");
			await Assert.That(string.Concat(document.Blocks[2].Inlines.Select(i => ((RichRun)i).Text))).IsEqualTo("c");
			await Assert.That(document.Blocks[3].Dirty).IsFalse();
			await Assert.That(string.Concat(document.Blocks[3].Inlines.Select(i => ((RichRun)i).Text))).IsEqualTo("Hello");
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(2, 1));
		}

		[Test]
		public async Task TypingInACellStaysInThatCellAndTakesItsStyle()
		{
			var document = RichMarkdownParser.Parse("| h |\n| --- |\n| **b** |\n");
			var table = document.Blocks[0];
			var selection = RichEditOperations.InsertText(document, T(1, 0, 1), "😀");
			await Assert.That(Cell(table, 1, 0)).IsEqualTo("b😀");
			await Assert.That(table.TableRows[1][0].Inlines.Single() is RichRun { Bold: true }).IsTrue();
			await Assert.That(table.Dirty).IsTrue();
			await Assert.That(selection.Caret).IsEqualTo(T(1, 0, 3));

			// Backspace removes the emoji whole and stops at the cell's start.
			selection = RichEditOperations.Backspace(document, selection.Caret);
			selection = RichEditOperations.Backspace(document, selection.Caret);
			await Assert.That(Cell(table, 1, 0)).IsEqualTo("");
			selection = RichEditOperations.Backspace(document, selection.Caret);
			await Assert.That(Grid(table)).IsEqualTo("h/");
			await Assert.That(selection.Caret).IsEqualTo(T(1, 0, 0));

			// Delete at a cell's end never reaches the next cell.
			RichEditOperations.Delete(document, T(0, 0, 1));
			await Assert.That(Grid(table)).IsEqualTo("h/");
			RichEditOperations.Delete(document, T(0, 0, 0));
			await Assert.That(Grid(table)).IsEqualTo("/");
		}

		[Test]
		public async Task LineBreaksInACellMoveDownTheColumnAndOutOfTheTable()
		{
			var document = RichMarkdownParser.Parse(Table);
			var table = document.Blocks[0];
			var selection = RichEditOperations.InsertText(document, T(0, 1, 2), "x\ny\nz");
			await Assert.That(Grid(table)).IsEqualTo("h1,h2x/a,by");

			// The last line left the table into a new paragraph below it.
			await Assert.That(document.Blocks.Count).IsEqualTo(2);
			await Assert.That(document.Blocks[1].Kind).IsEqualTo(RichBlockKind.Paragraph);
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(1, 1));
			await Assert.That(document.Blocks[1].Inlines.Single() is RichRun { Text: "z" }).IsTrue();
		}

		[Test]
		public async Task RangeDeleteAcrossCellsClearsTextAndKeepsTheGrid()
		{
			var document = RichMarkdownParser.Parse(Table);
			var table = document.Blocks[0];
			var selection = RichEditOperations.DeleteRange(document, T(1, 0, 1), T(0, 1, 1));
			await Assert.That(Grid(table)).IsEqualTo("h1,h/,b");
			await Assert.That(selection.Caret).IsEqualTo(T(0, 1, 1));

			// Covering the whole table removes it; an empty paragraph is left to type in.
			RichEditOperations.DeleteSelection(document, RichEditOperations.WholeBlock(document, 0));
			await Assert.That(document.Blocks.Single().Kind).IsEqualTo(RichBlockKind.Paragraph);
		}

		[Test]
		public async Task TabAndShiftTabWalkTheCellsAndTabInTheLastCellAddsARow()
		{
			var document = RichMarkdownParser.Parse(Table);
			var table = document.Blocks[0];
			var caret = RichTableCodeOperations.NextCell(document, T(0, 0, 0)).Caret;
			await Assert.That(caret).IsEqualTo(T(0, 1, 2));
			caret = RichTableCodeOperations.NextCell(document, caret).Caret;
			await Assert.That(caret).IsEqualTo(T(1, 0, 1));
			caret = RichTableCodeOperations.PreviousCell(document, caret).Caret;
			await Assert.That(caret).IsEqualTo(T(0, 1, 2));
			await Assert.That(RichTableCodeOperations.PreviousCell(document, T(0, 0, 0)).Caret).IsEqualTo(T(0, 0, 2));
			await Assert.That(table.Dirty).IsFalse();

			caret = RichTableCodeOperations.NextCell(document, T(1, 1, 0)).Caret;
			await Assert.That(caret).IsEqualTo(T(2, 0, 0));
			await Assert.That(Grid(table)).IsEqualTo("h1,h2/a,b/,");
			await Assert.That(table.Dirty).IsTrue();
			await Assert.That(IsValid(document, caret)).IsTrue();
		}

		[Test]
		public async Task RowsInsertAndDeleteButTheHeaderStays()
		{
			var document = RichMarkdownParser.Parse(Table);
			var table = document.Blocks[0];
			var caret = RichTableCodeOperations.InsertRow(document, T(1, 1, 0), below: true).Caret;
			await Assert.That(Grid(table)).IsEqualTo("h1,h2/a,b/,");
			await Assert.That(caret).IsEqualTo(T(2, 1, 0));

			// Above the header means just below it: the header is always the first row.
			caret = RichTableCodeOperations.InsertRow(document, T(0, 0, 0), below: false).Caret;
			await Assert.That(Grid(table)).IsEqualTo("h1,h2/,/a,b/,");
			await Assert.That(caret).IsEqualTo(T(1, 0, 0));

			var refused = RichTableCodeOperations.DeleteRow(document, T(0, 0, 1));
			await Assert.That(Grid(table)).IsEqualTo("h1,h2/,/a,b/,");
			await Assert.That(refused.Caret).IsEqualTo(T(0, 0, 1));

			caret = RichTableCodeOperations.DeleteRow(document, T(3, 1, 0)).Caret;
			await Assert.That(caret).IsEqualTo(T(2, 1, 1));
			caret = RichTableCodeOperations.DeleteRow(document, T(1, 0, 0)).Caret;
			caret = RichTableCodeOperations.DeleteRow(document, caret).Caret;
			await Assert.That(Grid(table)).IsEqualTo("h1,h2");
			await Assert.That(caret).IsEqualTo(T(0, 0, 2));
			await Assert.That(IsValid(document, caret)).IsTrue();
		}

		[Test]
		public async Task ColumnsInsertAndDeleteWithTheirAlignments()
		{
			var document = RichMarkdownParser.Parse(Table);
			var table = document.Blocks[0];
			var caret = RichTableCodeOperations.InsertColumn(document, T(1, 1, 0), right: false).Caret;
			await Assert.That(Grid(table)).IsEqualTo("h1,,h2/a,,b");
			await Assert.That(table.ColumnAlignments).IsEquivalentTo(new RichAlignment?[] { null, null, RichAlignment.Center }, CollectionOrdering.Matching);
			await Assert.That(caret).IsEqualTo(T(1, 1, 0));
			await Assert.That(table.Dirty).IsTrue();

			caret = RichTableCodeOperations.InsertColumn(document, T(0, 2, 0), right: true).Caret;
			await Assert.That(Grid(table)).IsEqualTo("h1,,h2,/a,,b,");
			await Assert.That(table.ColumnAlignments).IsEquivalentTo(new RichAlignment?[] { null, null, RichAlignment.Center, null }, CollectionOrdering.Matching);
			await Assert.That(caret).IsEqualTo(T(0, 3, 0));

			caret = RichTableCodeOperations.DeleteColumn(document, T(1, 0, 0)).Caret;
			await Assert.That(Grid(table)).IsEqualTo(",h2,/,b,");
			await Assert.That(table.ColumnAlignments).IsEquivalentTo(new RichAlignment?[] { null, RichAlignment.Center, null }, CollectionOrdering.Matching);
			await Assert.That(caret).IsEqualTo(T(1, 0, 0));

			caret = RichTableCodeOperations.DeleteColumn(document, T(1, 2, 0)).Caret;
			await Assert.That(caret).IsEqualTo(T(1, 1, 1));
			RichTableCodeOperations.DeleteColumn(document, T(0, 0, 0));
			await Assert.That(Grid(table)).IsEqualTo("h2/b");
			await Assert.That(table.ColumnAlignments).IsEquivalentTo(new RichAlignment?[] { RichAlignment.Center }, CollectionOrdering.Matching);

			// The only column is refused.
			var refused = RichTableCodeOperations.DeleteColumn(document, T(1, 0, 1));
			await Assert.That(Grid(table)).IsEqualTo("h2/b");
			await Assert.That(refused.Caret).IsEqualTo(T(1, 0, 1));
		}

		[Test]
		public async Task InsertTableAddsAnEmptyGridAfterTheBlockOrItsList()
		{
			var document = RichMarkdownParser.Parse("Intro\n\n- one\n- two\n\nEnd\n");
			var selection = RichTableCodeOperations.InsertTable(document, new DocPosition(0, 2), bodyRows: 2, columns: 3);
			var table = document.Blocks[1];
			await Assert.That(table.Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(Grid(table)).IsEqualTo(",,/,,/,,");
			await Assert.That(table.ColumnAlignments).IsEquivalentTo(new RichAlignment?[] { null, null, null }, CollectionOrdering.Matching);
			await Assert.That(table.Dirty).IsTrue();
			await Assert.That(selection.Caret).IsEqualTo(new DocPosition(1, 0, 0, 0));
			await Assert.That(IsValid(document, selection.Caret)).IsTrue();

			// From the first list item the table goes after the whole list, not between its items.
			selection = RichTableCodeOperations.InsertTable(document, new DocPosition(2, 0), bodyRows: 0, columns: 1);
			await Assert.That(document.Blocks[4].Kind).IsEqualTo(RichBlockKind.Table);
			await Assert.That(Grid(document.Blocks[4])).IsEqualTo("");
			await Assert.That(document.Blocks[4].TableRows.Count).IsEqualTo(1);
			await Assert.That(selection.Caret.BlockIndex).IsEqualTo(4);
			await Assert.That(document.Blocks[5].Kind).IsEqualTo(RichBlockKind.Paragraph);
		}
	}
}
