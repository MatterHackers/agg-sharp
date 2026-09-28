/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

namespace MatterHackers.Agg.UI
{
	/// <summary>One visual row of a <see cref="CodeLayout"/>: a source line, or a wrapped piece of one. Indices are
	/// into the whole text.</summary>
	public readonly struct CodeRow
	{
		public CodeRow(int line, int start, int end, int nextStart, int caretEnd, bool firstInLine, bool lastInLine)
		{
			Line = line;
			Start = start;
			End = end;
			NextStart = nextStart;
			CaretEnd = caretEnd;
			FirstInLine = firstInLine;
			LastInLine = lastInLine;
		}

		/// <summary>The source line the row is part of.</summary>
		public int Line { get; }

		/// <summary>The row's first character.</summary>
		public int Start { get; }

		/// <summary>The end of the row's drawn text: before the whitespace a soft wrap broke at, or the line's end.</summary>
		public int End { get; }

		/// <summary>Where the next row starts, past the whitespace a soft wrap broke at; the line's end on its last
		/// row. Carets in [<see cref="Start"/>, NextStart) sit on this row.</summary>
		public int NextStart { get; }

		/// <summary>The furthest the caret goes on this row (End and clicks past the text): <see cref="End"/>,
		/// except on a row that broke an overlong word, where End is the next row's start and the caret stops one
		/// character short of it.</summary>
		public int CaretEnd { get; }

		/// <summary>Whether this is the source line's first row - the one the gutter numbers.</summary>
		public bool FirstInLine { get; }

		/// <summary>Whether this is the source line's last row, which runs to the line's end.</summary>
		public bool LastInLine { get; }
	}

	/// <summary>
	/// A <see cref="CodeDocument"/> laid out into visual rows - agg-gui's TextArea wrap (wrap_text_indexed). Without
	/// <see cref="Wrap"/> every line is one row. With it a line breaks after the last word that fits the width, the
	/// whitespace it broke at staying at the end of that row, and a word wider than a whole row breaks wherever it
	/// overflows. Rows are counted for every line, so the scroll range is exact, but kept only for the lines asked
	/// about - the ones in view - and everything is dropped by <see cref="Invalidate"/> on an edit.
	/// </summary>
	public class CodeLayout
	{
		// Lines whose rows are kept before the cache starts over; a view's worth of lines fits many times over.
		private const int MaxCachedLines = 512;

		private readonly CodeDocument document;
		private readonly Func<string, int, int, double> measure;
		private readonly Dictionary<int, List<CodeRow>> rowsOfLine = new Dictionary<int, List<CodeRow>>();

		// firstRows[line] is the index of the line's first row and firstRows[LineCount] the row count; null until
		// next needed.
		private int[] firstRows;
		private bool wrap;
		private double width;

		// The x the last up/down aimed for and the caret it left, so a run of moves through short rows keeps it.
		private double verticalX = -1;
		private int verticalCaret = -1;

		/// <param name="measure">The width of text[start..end) in pixels, as the editor draws it.</param>
		public CodeLayout(CodeDocument document, Func<string, int, int, double> measure)
		{
			this.document = document;
			this.measure = measure;
		}

		/// <summary>Whether lines wrap to <see cref="Width"/>.</summary>
		public bool Wrap => wrap;

		/// <summary>The width rows wrap to, in pixels.</summary>
		public double Width => width;

		public int RowCount => wrap ? FirstRows[document.LineCount] : document.LineCount;

		private int[] FirstRows
		{
			get
			{
				if (firstRows == null)
				{
					firstRows = new int[document.LineCount + 1];
					for (int line = 0; line < document.LineCount; line++)
					{
						firstRows[line + 1] = firstRows[line] + RowsOf(line).Count;
					}
				}

				return firstRows;
			}
		}

		/// <summary>Sets whether, and to what width, lines wrap - laying out afresh when either changed.</summary>
		public void Configure(bool wrap, double width)
		{
			if (wrap != this.wrap || (wrap && Math.Abs(width - this.width) >= .5))
			{
				this.wrap = wrap;
				this.width = width;
				Invalidate();
			}
		}

		/// <summary>Drops every row, to be laid out again when next asked for: the text changed.</summary>
		public void Invalidate()
		{
			firstRows = null;
			rowsOfLine.Clear();
			verticalX = -1;
		}

		public int FirstRowOfLine(int line) => wrap ? FirstRows[line] : line;

		public CodeRow Row(int row)
		{
			if (!wrap)
			{
				int end = document.LineEnd(row);
				return new CodeRow(row, document.LineStart(row), end, end, end, true, true);
			}

			// Every line has at least one row, so a row that starts a line is found exactly.
			int line = Array.BinarySearch(FirstRows, 0, document.LineCount, row);
			line = line >= 0 ? line : ~line - 1;
			return RowsOf(line)[row - firstRows[line]];
		}

		/// <summary>The row the caret at <paramref name="index"/> sits on.</summary>
		public int RowOfIndex(int index)
		{
			int line = document.LineOf(Math.Clamp(index, 0, document.Text.Length));
			if (!wrap)
			{
				return line;
			}

			List<CodeRow> rows = RowsOf(line);
			int k = rows.Count - 1;
			while (k > 0 && rows[k].Start > index)
			{
				k--;
			}

			return FirstRows[line] + k;
		}

		/// <summary>How far the caret at <paramref name="index"/> sits from its row's left edge.</summary>
		public double XOfIndex(int index)
		{
			CodeRow row = Row(RowOfIndex(index));
			int lineStart = document.LineStart(row.Line);
			return measure(document.LineText(row.Line), row.Start - lineStart, index - lineStart);
		}

		/// <summary>The caret position on <paramref name="row"/> nearest <paramref name="x"/> pixels from its left
		/// edge, no further than the row's <see cref="CodeRow.CaretEnd"/>.</summary>
		public int IndexAtX(int row, double x)
		{
			CodeRow codeRow = Row(row);
			int lineStart = document.LineStart(codeRow.Line);
			string text = document.LineText(codeRow.Line);
			int column = codeRow.Start - lineStart;
			int last = codeRow.CaretEnd - lineStart;
			double at = 0;
			while (column < last)
			{
				int next = CodeDocument.NextCharIndex(text, column);
				double advance = measure(text, column, next);
				if (at + advance / 2 > x)
				{
					break;
				}

				at += advance;
				column = next;
			}

			return lineStart + column;
		}

		/// <summary>
		/// Up/down arrow (-1/+1) and page up/down over wrapped rows: <paramref name="rows"/> rows on from the
		/// caret's at the same x - agg-gui's move_lines, which clamps to the first and last row and stays put when
		/// already there. A run of moves keeps the x the first started from, so a short row on the way loses nothing.
		/// <paramref name="rowShift"/>, when given, is how far right each row is drawn from the text's left edge
		/// (centred or right-aligned rows), so the caret keeps its on-screen x. agg-gui keeps the x from each row's
		/// own start instead, which on aligned rows of different widths jumps sideways; this follows egui.
		/// </summary>
		public int MoveVertically(int caret, int rows, Func<int, double> rowShift = null)
		{
			int row = RowOfIndex(caret);
			int target = Math.Clamp(row + rows, 0, RowCount - 1);
			double x = caret == verticalCaret && verticalX >= 0 ? verticalX : XOfIndex(caret) + (rowShift?.Invoke(row) ?? 0);
			int index = target == row ? caret : IndexAtX(target, Math.Max(0, x - (rowShift?.Invoke(target) ?? 0)));
			verticalX = x;
			verticalCaret = index;
			return index;
		}

		/// <summary>The rows <paramref name="line"/> wraps into (one when not wrapping), laid out on first use.</summary>
		public List<CodeRow> RowsOf(int line)
		{
			if (!wrap)
			{
				return new List<CodeRow> { Row(line) };
			}

			if (!rowsOfLine.TryGetValue(line, out List<CodeRow> rows))
			{
				if (rowsOfLine.Count >= MaxCachedLines)
				{
					rowsOfLine.Clear();
				}

				rows = WrapLine(line);
				rowsOfLine[line] = rows;
			}

			return rows;
		}

		private List<CodeRow> WrapLine(int line)
		{
			string text = document.LineText(line);
			int lineStart = document.LineStart(line);
			int lineEnd = lineStart + text.Length;
			var rows = new List<CodeRow>();
			int start = 0;
			while (true)
			{
				// The longest run from start that fits (at least one character), and the last word end in it: a
				// whitespace character just after a non-whitespace one, so an indent is never a break.
				int fitEnd = start;
				int wordEnd = -1;
				double at = 0;
				while (fitEnd < text.Length)
				{
					int next = CodeDocument.NextCharIndex(text, fitEnd);
					double advance = measure(text, fitEnd, next);
					if (at + advance > width && fitEnd > start)
					{
						break;
					}

					if (fitEnd > start && char.IsWhiteSpace(text[fitEnd]) && !char.IsWhiteSpace(text[fitEnd - 1]))
					{
						wordEnd = fitEnd;
					}

					at += advance;
					fitEnd = next;
				}

				// Break after the last word that fits or, in a word wider than the row, where it overflows. The
				// whitespace run the break falls in stays on this row.
				int end = wordEnd > start ? wordEnd : fitEnd;
				int nextStart = end;
				while (nextStart < text.Length && char.IsWhiteSpace(text[nextStart]))
				{
					nextStart++;
				}

				if (fitEnd >= text.Length || nextStart >= text.Length)
				{
					// The rest fits, or only whitespace is left to trail it.
					rows.Add(new CodeRow(line, lineStart + start, lineEnd, lineEnd, lineEnd, start == 0, true));
					return rows;
				}

				int caretEnd = end < nextStart ? end : CodeDocument.PreviousCharIndex(text, nextStart);
				rows.Add(new CodeRow(line, lineStart + start, lineStart + end, lineStart + nextStart, lineStart + caretEnd, start == 0, false));
				start = nextStart;
			}
		}
	}
}
