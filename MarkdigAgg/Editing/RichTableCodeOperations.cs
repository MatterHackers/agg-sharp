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

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Edits inside a code block or a table: typing, Backspace/Delete, Enter and range deletes that stay within
	/// one block (<see cref="RichEditOperations"/> routes those here), plus the table structure ops behind
	/// Tab/Shift-Tab and the toolbar's row, column and insert-table commands. Every op mutates the document in
	/// place, marks the block it changes Dirty and returns the selection to show next. A table caret is
	/// <see cref="DocPosition"/> Row/Column/Offset, the offset counting through that cell's inlines.
	/// <para>
	/// Code blocks hold plain text: Enter inserts a newline (the block never splits), and Enter on the empty
	/// last line leaves the block, the way a first-time user gets out of it without knowing about fences.
	/// Table cells never take a line break (markdown has none inside a cell): Enter moves down a row instead,
	/// and Backspace/Delete never cross a cell's edge, so the grid's shape changes only through the row and
	/// column ops.
	/// </para>
	/// </summary>
	public static class RichTableCodeOperations
	{
		/// <summary>
		/// What Tab types in a code block: four spaces rather than a tab character, the indentation markdown
		/// itself counts in and most published code uses, so the code reads the same in every viewer.
		/// </summary>
		public const string CodeTab = "    ";

		/// <summary>
		/// Types <paramref name="text"/> at <paramref name="position"/> in a code block or table cell. In code
		/// every line break is kept as '\n'. In a cell the text takes <paramref name="style"/>, or the same style
		/// rule as text blocks (the run before the caret, a link or inline code stopping at its end); each line
		/// break acts as Enter (<see cref="SplitBlock"/>), so pasted lines fill down the column.
		/// </summary>
		public static RichSelection InsertText(RichDocument document, DocPosition position, string text, RichRun style = null)
		{
			var block = document.Blocks[position.BlockIndex];
			if (string.IsNullOrEmpty(text))
			{
				return RichSelection.At(position);
			}

			text = text.Replace("\r\n", "\n").Replace('\r', '\n');
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				block.CodeText = block.CodeText.Insert(position.Offset, text);
				block.Dirty = true;
				return RichSelection.At(position with { Offset = position.Offset + text.Length });
			}

			RequireTable(block);
			style ??= RichEditOperations.StyleAt(block.InlinesAt(position.Row, position.Column), position.Offset);
			var lines = text.Split('\n');
			var caret = position;
			for (int i = 0; i < lines.Length; i++)
			{
				if (i > 0)
				{
					caret = SplitBlock(document, caret).Caret;
					if (caret.BlockIndex != position.BlockIndex)
					{
						// Enter on the last row left the table; the rest is ordinary text below it.
						string rest = string.Join("\n", lines, i, lines.Length - i);
						return RichEditOperations.InsertText(document, caret, rest, style);
					}
				}

				if (lines[i].Length > 0)
				{
					var inlines = block.InlinesAt(caret.Row, caret.Column);
					int index = RichInlines.SplitAt(inlines, caret.Offset);
					inlines.Insert(index, style.WithText(lines[i]));
					RichInlines.MergeAdjacent(inlines);
					block.Dirty = true;
					caret = caret with { Offset = caret.Offset + lines[i].Length };
				}
			}

			return RichSelection.At(caret);
		}

		/// <summary>
		/// Removes the text between two positions of one code block or table, never the block itself, even when the
		/// range is all of its text (<see cref="RichEditOperations.DeleteSelection"/> removes a block selected
		/// whole). Across table cells it clears the selected text of each cell in reading order and leaves the grid
		/// intact; removing rows or columns is what <see cref="DeleteRow"/> and <see cref="DeleteColumn"/> are for.
		/// </summary>
		public static RichSelection DeleteRange(RichDocument document, DocPosition a, DocPosition b)
		{
			var start = DocPosition.Min(a, b);
			var end = DocPosition.Max(a, b);
			if (start.BlockIndex != end.BlockIndex)
			{
				throw new ArgumentException("Both positions must be in one block; RichEditOperations.DeleteRange handles ranges across blocks.");
			}

			var block = document.Blocks[start.BlockIndex];
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				RemoveCode(block, start.Offset, end.Offset);
				return RichSelection.At(start);
			}

			RequireTable(block);
			int columns = block.TableRows[0].Count;
			for (int cell = start.Row * columns + start.Column; cell <= end.Row * columns + end.Column; cell++)
			{
				int row = cell / columns;
				int column = cell % columns;
				int from = row == start.Row && column == start.Column ? start.Offset : 0;
				int to = row == end.Row && column == end.Column ? end.Offset : block.TextLength(row, column);
				RemoveInlines(block, block.InlinesAt(row, column), from, to);
			}

			return RichSelection.At(start);
		}

		/// <summary>
		/// Enter. In code it inserts a newline, except on an empty last line (the text ends in a newline and the
		/// caret is at its end): that is the second Enter in a row, so the block drops that empty line and an
		/// empty paragraph after it takes the caret. In a table the caret moves to the end of the same column one
		/// row down; from the last row it leaves the table into a new empty paragraph after it, as code does, so a
		/// table at the end of a document has a way out below it and a paste never runs into the block after.
		/// </summary>
		public static RichSelection SplitBlock(RichDocument document, DocPosition position)
		{
			int index = position.BlockIndex;
			var block = document.Blocks[index];
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				var code = block.CodeText;
				if (position.Offset == code.Length && code.EndsWith("\n", StringComparison.Ordinal))
				{
					block.CodeText = code.Substring(0, code.Length - 1);
					block.Dirty = true;
					return RichSelection.At(InsertParagraphAfter(document, index));
				}

				return InsertText(document, position, "\n");
			}

			RequireTable(block);
			if (position.Row + 1 < block.TableRows.Count)
			{
				return RichSelection.At(CellEnd(block, index, position.Row + 1, position.Column));
			}

			return RichSelection.At(InsertParagraphAfter(document, index));
		}

		/// <summary>
		/// Backspace with a collapsed caret: removes one character (a whole emoji or combining sequence) or atom.
		/// At a cell's start nothing happens, so a cell never merges into its neighbour. At the start of a code
		/// block nothing happens either, unless the block is empty: then it becomes an empty paragraph, the way a
		/// novice expects to take back a code block they just made.
		/// </summary>
		public static RichSelection Backspace(RichDocument document, DocPosition position) => Backspace(document, position, out _);

		/// <summary>
		/// <see cref="Backspace(RichDocument, DocPosition)"/>, saying whether it changed the document (false at a
		/// cell's start), so the editor leaves no empty undo step.
		/// </summary>
		public static RichSelection Backspace(RichDocument document, DocPosition position, out bool changed)
		{
			changed = true;
			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				if (position.Offset > 0)
				{
					var starts = StringInfo.ParseCombiningCharacters(block.CodeText.Substring(0, position.Offset));
					int previous = starts[starts.Length - 1];
					RemoveCode(block, previous, position.Offset);
					return RichSelection.At(position with { Offset = previous });
				}

				if (block.CodeText.Length == 0)
				{
					// Cleared so the writer and layout see a plain paragraph, with no fence or language left over.
					block.Kind = RichBlockKind.Paragraph;
					block.Inlines.Clear();
					block.CodeText = "";
					block.CodeFence = "";
					block.CodeInfo = "";
					block.CodeLanguage = "";
					block.Dirty = true;
					return RichSelection.At(position);
				}

				changed = false;
				return RichSelection.At(position);
			}

			RequireTable(block);
			if (position.Offset == 0)
			{
				changed = false;
				return RichSelection.At(position);
			}

			var inlines = block.InlinesAt(position.Row, position.Column);
			int stop = RichEditOperations.PreviousStop(inlines, position.Offset);
			RemoveInlines(block, inlines, stop, position.Offset);
			return RichSelection.At(position with { Offset = stop });
		}

		/// <summary>
		/// Forward Delete with a collapsed caret: removes the next character or atom; at the end of a cell or a
		/// code block nothing happens.
		/// </summary>
		public static RichSelection Delete(RichDocument document, DocPosition position) => Delete(document, position, out _);

		/// <summary>
		/// <see cref="Delete(RichDocument, DocPosition)"/>, saying whether it changed the document.
		/// </summary>
		public static RichSelection Delete(RichDocument document, DocPosition position, out bool changed)
		{
			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				changed = position.Offset < block.CodeText.Length;
				if (changed)
				{
					int next = position.Offset + StringInfo.GetNextTextElementLength(block.CodeText, position.Offset);
					RemoveCode(block, position.Offset, next);
				}

				return RichSelection.At(position);
			}

			RequireTable(block);
			var inlines = block.InlinesAt(position.Row, position.Column);
			changed = position.Offset < RichInlines.Length(inlines);
			if (changed)
			{
				RemoveInlines(block, inlines, position.Offset, RichEditOperations.NextStop(inlines, position.Offset));
			}

			return RichSelection.At(position);
		}

		/// <summary>
		/// Tab in a table: the caret goes to the end of the next cell in reading order. Tab in the last cell adds
		/// a body row and goes to its first cell, as in a word processor, so a table grows by typing.
		/// </summary>
		public static RichSelection NextCell(RichDocument document, DocPosition position) => NextCell(document, position, out _);

		/// <summary>
		/// <see cref="NextCell(RichDocument, DocPosition)"/>, saying whether it added a row (otherwise it only moved
		/// the caret).
		/// </summary>
		public static RichSelection NextCell(RichDocument document, DocPosition position, out bool addedRow)
		{
			addedRow = false;
			var block = RequireTable(document.Blocks[position.BlockIndex]);
			int columns = block.TableRows[0].Count;
			int row = position.Row;
			int column = position.Column + 1;
			if (column == columns)
			{
				column = 0;
				row++;
				if (row == block.TableRows.Count)
				{
					block.TableRows.Add(NewRow(columns));
					block.Dirty = true;
					addedRow = true;
				}
			}

			return RichSelection.At(CellEnd(block, position.BlockIndex, row, column));
		}

		/// <summary>
		/// Shift-Tab in a table: the caret goes to the end of the previous cell; in the first cell, to that cell's
		/// end.
		/// </summary>
		public static RichSelection PreviousCell(RichDocument document, DocPosition position)
		{
			var block = RequireTable(document.Blocks[position.BlockIndex]);
			int columns = block.TableRows[0].Count;
			int cell = Math.Max(0, position.Row * columns + position.Column - 1);
			return RichSelection.At(CellEnd(block, position.BlockIndex, cell / columns, cell % columns));
		}

		/// <summary>
		/// Adds an empty body row above or below the caret's row; the caret goes to the new row's cell in the same
		/// column. Markdown's header is always the first row, so a row asked for above the header goes just below
		/// it instead.
		/// </summary>
		public static RichSelection InsertRow(RichDocument document, DocPosition position, bool below)
		{
			var block = RequireTable(document.Blocks[position.BlockIndex]);
			int row = Math.Max(1, below ? position.Row + 1 : position.Row);
			block.TableRows.Insert(row, NewRow(block.TableRows[0].Count));
			block.Dirty = true;
			return RichSelection.At(new DocPosition(position.BlockIndex, 0, row, position.Column));
		}

		/// <summary>
		/// Removes the caret's row; the caret goes to the end of the same column in the row now there, or the row
		/// above when it was the last. The header row is refused (nothing changes): a markdown table cannot exist
		/// without one, while a table with only a header is valid, so the last body row may go.
		/// </summary>
		public static RichSelection DeleteRow(RichDocument document, DocPosition position)
		{
			var block = RequireTable(document.Blocks[position.BlockIndex]);
			if (position.Row == 0)
			{
				return RichSelection.At(position);
			}

			block.TableRows.RemoveAt(position.Row);
			block.Dirty = true;
			int row = Math.Min(position.Row, block.TableRows.Count - 1);
			return RichSelection.At(CellEnd(block, position.BlockIndex, row, position.Column));
		}

		/// <summary>
		/// Adds an empty column left or right of the caret's column, with no alignment (null, written "---");
		/// the caret goes to the new column's cell in the same row.
		/// </summary>
		public static RichSelection InsertColumn(RichDocument document, DocPosition position, bool right)
		{
			var block = RequireTable(document.Blocks[position.BlockIndex]);
			int column = right ? position.Column + 1 : position.Column;
			foreach (var row in block.TableRows)
			{
				row.Insert(column, new RichTableCell());
			}

			block.ColumnAlignments.Insert(Math.Min(column, block.ColumnAlignments.Count), null);
			block.Dirty = true;
			return RichSelection.At(new DocPosition(position.BlockIndex, 0, position.Row, column));
		}

		/// <summary>
		/// Removes the caret's column and its alignment; the caret goes to the end of the cell now in that column
		/// (or the one left of it). The only column is refused (nothing changes): removing a whole table is a
		/// selection delete, not a column op.
		/// </summary>
		public static RichSelection DeleteColumn(RichDocument document, DocPosition position)
		{
			var block = RequireTable(document.Blocks[position.BlockIndex]);
			int columns = block.TableRows[0].Count;
			if (columns <= 1)
			{
				return RichSelection.At(position);
			}

			foreach (var row in block.TableRows)
			{
				row.RemoveAt(position.Column);
			}

			if (position.Column < block.ColumnAlignments.Count)
			{
				block.ColumnAlignments.RemoveAt(position.Column);
			}

			block.Dirty = true;
			int column = Math.Min(position.Column, columns - 2);
			return RichSelection.At(CellEnd(block, position.BlockIndex, position.Row, column));
		}

		/// <summary>
		/// Adds a new table of empty cells - a header row plus <paramref name="bodyRows"/> body rows, each
		/// <paramref name="columns"/> wide (at least one), no column alignments - after the caret's block, and
		/// puts the caret in the first header cell. When that block is in a list, quote or alignment wrapper the
		/// table goes after the whole wrapper: the model holds no table inside one, and a stranger in the middle
		/// would split the group.
		/// </summary>
		public static RichSelection InsertTable(RichDocument document, DocPosition position, int bodyRows, int columns)
		{
			columns = Math.Max(1, columns);
			var blocks = document.Blocks;
			var current = blocks[position.BlockIndex];
			int after = position.BlockIndex;
			while (after + 1 < blocks.Count && SharesGroup(blocks[after + 1], current))
			{
				after++;
			}

			var table = new RichBlock
			{
				Kind = RichBlockKind.Table,
				SeparatorBefore = RichEditOperations.BlankLine(document),
				Dirty = true,
			};

			for (int i = 0; i <= Math.Max(0, bodyRows); i++)
			{
				table.TableRows.Add(NewRow(columns));
			}

			for (int i = 0; i < columns; i++)
			{
				table.ColumnAlignments.Add(null);
			}

			blocks.Insert(after + 1, table);
			return RichSelection.At(new DocPosition(after + 1, 0, 0, 0));
		}

		private static bool SharesGroup(RichBlock block, RichBlock other)
		{
			return (other.ListGroup != null && block.ListGroup == other.ListGroup)
				|| (other.QuoteGroup != null && block.QuoteGroup == other.QuoteGroup)
				|| (other.AlignGroup != null && block.AlignGroup == other.AlignGroup);
		}

		private static RichBlock RequireTable(RichBlock block)
		{
			if (block.Kind != RichBlockKind.Table)
			{
				throw new InvalidOperationException($"A {block.Kind} block is not a table.");
			}

			return block;
		}

		private static List<RichTableCell> NewRow(int columns)
		{
			var row = new List<RichTableCell>(columns);
			for (int i = 0; i < columns; i++)
			{
				row.Add(new RichTableCell());
			}

			return row;
		}

		private static DocPosition CellEnd(RichBlock table, int index, int row, int column)
		{
			return new DocPosition(index, table.TextLength(row, column), row, column);
		}

		private static DocPosition InsertParagraphAfter(RichDocument document, int index)
		{
			document.Blocks.Insert(index + 1, new RichBlock
			{
				Kind = RichBlockKind.Paragraph,
				SeparatorBefore = RichEditOperations.BlankLine(document),
				Dirty = true,
			});
			return new DocPosition(index + 1, 0);
		}

		private static void RemoveCode(RichBlock block, int from, int to)
		{
			if (from < to)
			{
				block.CodeText = block.CodeText.Remove(from, to - from);
				block.Dirty = true;
			}
		}

		private static void RemoveInlines(RichBlock block, List<RichInline> inlines, int from, int to)
		{
			if (from >= to)
			{
				return;
			}

			// SplitAt(to) runs second, so its split lands after the first one and leaves 'start' valid.
			int start = RichInlines.SplitAt(inlines, from);
			int end = RichInlines.SplitAt(inlines, to);
			inlines.RemoveRange(start, end - start);
			RichInlines.MergeAdjacent(inlines);
			block.Dirty = true;
		}
	}
}
