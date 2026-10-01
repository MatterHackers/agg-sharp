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
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Reads code blocks and pipe tables into their <see cref="RichBlock"/> models. A block this cannot model
	/// is left untouched, so it stays the Raw block the parser made with its exact source.
	/// </summary>
	internal static class RichCodeAndTables
	{
		// Exact types: extension blocks deriving from CodeBlock (math, YAML) are not code the rich view edits.
		public static bool IsCode(Block block) => block.GetType() == typeof(FencedCodeBlock) || block.GetType() == typeof(CodeBlock);

		/// <summary>
		/// Where a code block's source ends. An indented block's span runs on over the blank lines after it,
		/// which belong in the separator; an unclosed fence's span stops at its opening line though it owns every
		/// line after it. Both end at their last content line instead.
		/// </summary>
		public static int CodeEnd(CodeBlock code, int spanEnd)
		{
			bool closedFence = code is FencedCodeBlock fenced && fenced.ClosingFencedCharCount > 0;
			if (closedFence || code.Lines.Count == 0)
			{
				return spanEnd;
			}

			// Slices index the whole source and stop before the line ending (and its '\r').
			return code.Lines.Lines[code.Lines.Count - 1].Slice.End + 1;
		}

		/// <summary>
		/// Where a fenced block's source starts: the start of its line, so the fence's indentation is part of
		/// the block. Markdig strips up to that much indentation from every content line, so a rewrite that
		/// indented only the opening fence (leaving the indent in the separator) would change the code.
		/// </summary>
		public static int CodeStart(CodeBlock code, string body, int start, int cursor)
		{
			if (code is not FencedCodeBlock)
			{
				return start;
			}

			int lineStart = start;
			while (lineStart > cursor && (body[lineStart - 1] == ' ' || body[lineStart - 1] == '\t'))
			{
				lineStart--;
			}

			bool atLineStart = lineStart == 0 || body[lineStart - 1] == '\n';
			return atLineStart ? lineStart : start;
		}

		public static void ReadCode(CodeBlock code, string body, RichBlock result)
		{
			string info = "";
			string fence = "";
			if (code is FencedCodeBlock fenced)
			{
				fence = new string(fenced.FencedChar, fenced.OpeningFencedCharCount);

				// The info string as written, not Markdig's decoded Info: entities and escapes stay as the author
				// typed them, and words after the language ("```cs title=x") are kept for a rewrite.
				int fenceStart = body.IndexOf(fence, code.Span.Start, StringComparison.Ordinal);
				int infoStart = fenceStart + fence.Length;
				int lineEnd = body.IndexOf('\n', infoStart);
				info = body.Substring(infoStart, (lineEnd < 0 ? body.Length : lineEnd) - infoStart).Trim();
			}

			// Splitting on whitespace: Markdig takes the language as the info string's first word too.
			string language = info.Split(new[] { ' ', '\t' }, 2)[0];

			// Markdig has already removed the fence's (or the indented block's) indentation from each line.
			var lines = new List<string>(code.Lines.Count);
			for (int i = 0; i < code.Lines.Count; i++)
			{
				lines.Add(code.Lines.Lines[i].Slice.ToString());
			}

			result.Kind = RichBlockKind.CodeBlock;
			result.CodeText = string.Join("\n", lines);
			result.CodeLanguage = language;
			result.CodeInfo = info;
			result.CodeFence = fence;
		}

		/// <summary>
		/// Fills a pipe table's rows and column alignments, or leaves it Raw: grid tables, rows wider than the
		/// header, and cells holding anything but one paragraph are not tables the rich view edits.
		/// </summary>
		/// <param name="inlinesOf">The paragraph inline builder, so cells read exactly as paragraphs do.</param>
		public static void ReadTable(Table table, RichBlock result, Func<LeafBlock, List<RichInline>> inlinesOf)
		{
			// Pipe tables are built by an inline parser and carry no block parser; grid tables carry theirs.
			if (table.Parser != null || table.Count == 0)
			{
				return;
			}

			var rows = new List<List<RichTableCell>>();
			int columns = 0;
			foreach (var child in table)
			{
				if (child is not TableRow row || row.IsHeader != (rows.Count == 0))
				{
					return;
				}

				var cells = new List<RichTableCell>();
				foreach (var cellBlock in row)
				{
					var cell = ReadCell(cellBlock, inlinesOf);
					if (cell == null)
					{
						return;
					}

					cells.Add(cell);
				}

				// Markdig pads every row to the widest one with cells holding no paragraph (a cell written empty
				// still holds an empty one). Padding in the header means some row is wider than it - the extra
				// cells have no header or alignment to write - so the table stays Raw; padding in a body row is a
				// row written short.
				int written = cells.Count;
				while (written > 0 && ((ContainerBlock)row[written - 1]).Count == 0)
				{
					written--;
				}

				if (rows.Count == 0)
				{
					columns = written;
					if (columns == 0 || written != cells.Count || columns > table.ColumnDefinitions.Count)
					{
						return;
					}
				}
				else if (written > columns)
				{
					return;
				}

				cells.RemoveRange(written, cells.Count - written);
				while (cells.Count < columns)
				{
					cells.Add(new RichTableCell());
				}

				rows.Add(cells);
			}

			var alignments = new List<RichAlignment?>(columns);
			for (int i = 0; i < columns; i++)
			{
				alignments.Add(table.ColumnDefinitions[i].Alignment switch
				{
					TableColumnAlign.Left => RichAlignment.Left,
					TableColumnAlign.Center => RichAlignment.Center,
					TableColumnAlign.Right => RichAlignment.Right,
					_ => null,
				});
			}

			result.Kind = RichBlockKind.Table;
			result.TableRows = rows;
			result.ColumnAlignments = alignments;
		}

		/// <summary>
		/// A cell's inlines, an empty cell for padding or a cell written empty, or null for anything the table
		/// model cannot hold.
		/// </summary>
		private static RichTableCell ReadCell(Block block, Func<LeafBlock, List<RichInline>> inlinesOf)
		{
			if (block is not TableCell cell || cell.ColumnSpan != 1 || cell.RowSpan != 1 || cell.Count > 1)
			{
				return null;
			}

			if (cell.Count == 0)
			{
				return new RichTableCell();
			}

			if (cell[0].GetType() != typeof(ParagraphBlock))
			{
				return null;
			}

			var paragraph = (ParagraphBlock)cell[0];

			// A pipe the table parser left unconsumed would read as cell text but split the cell when written.
			if (paragraph.Inline != null)
			{
				foreach (var inline in paragraph.Inline.Descendants<Inline>())
				{
					if (inline is PipeTableDelimiterInline)
					{
						return null;
					}
				}
			}

			return new RichTableCell { Inlines = inlinesOf(paragraph) };
		}
	}
}
