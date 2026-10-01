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
using System.Linq;
using System.Text;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// A caret the keys move to: a position and which side of a wrap it shows on (see
	/// <see cref="RichMarkdownEditWidget.CaretAtLineEnd"/>).
	/// </summary>
	internal readonly record struct RichCaretTarget(DocPosition Position, bool AtLineEnd = false);

	/// <summary>
	/// Where the caret keys take the caret in a laid-out <see cref="RichMarkdownEditWidget"/>. Character steps use
	/// the layout's caret stops, so an emoji or combining sequence is one step exactly as it draws; up/down go
	/// through the lines as drawn (wrapped lines, then the next table row or block) at a remembered x. A
	/// "container" below is what a position's offset counts through: a block, or one table cell. Every method
	/// returns null when there is nowhere to go (Left at the document's start).
	/// </summary>
	internal static class RichCaretNavigator
	{
		/// <summary>
		/// Right: the next stop in the container, else the start of the next cell or block.
		/// </summary>
		public static RichCaretTarget? Next(RichMarkdownEditWidget editor, DocPosition position)
		{
			foreach (int offset in StopOffsets(editor, position))
			{
				if (offset > position.Offset)
				{
					return new RichCaretTarget(position with { Offset = offset });
				}
			}

			return NextContainer(editor.Document, position);
		}

		/// <summary>
		/// Left: the stop before, else the end of the previous cell or block.
		/// </summary>
		public static RichCaretTarget? Previous(RichMarkdownEditWidget editor, DocPosition position)
		{
			var stops = StopOffsets(editor, position);
			for (int i = stops.Count - 1; i >= 0; i--)
			{
				if (stops[i] < position.Offset)
				{
					return new RichCaretTarget(position with { Offset = stops[i] });
				}
			}

			return PreviousContainer(editor.Document, position);
		}

		/// <summary>
		/// A word step right (Alt on mac, Ctrl elsewhere) with the plain text field's word rule; at the
		/// container's end it steps into the next one, as Right does.
		/// </summary>
		public static RichCaretTarget? NextWord(RichMarkdownEditWidget editor, DocPosition position)
		{
			string text = TextOf(editor.Document, position);
			if (position.Offset >= text.Length)
			{
				return NextContainer(editor.Document, position);
			}

			return new RichCaretTarget(position with { Offset = InternalTextEditWidget.IndexOfNextToken(text, position.Offset) });
		}

		public static RichCaretTarget? PreviousWord(RichMarkdownEditWidget editor, DocPosition position)
		{
			if (position.Offset == 0)
			{
				return PreviousContainer(editor.Document, position);
			}

			string text = TextOf(editor.Document, position);
			return new RichCaretTarget(position with { Offset = InternalTextEditWidget.IndexOfPreviousToken(text, Math.Min(position.Offset, text.Length)) });
		}

		/// <summary>
		/// Home or End: the first or last stop of the line the caret is drawn on. End of a line that wrapped with no
		/// space lands on its end-of-line stop (AtLineEnd), so the caret stays on that line.
		/// </summary>
		public static RichCaretTarget LineEdge(RichMarkdownEditWidget editor, DocPosition position, bool atLineEnd, bool end)
		{
			var lines = LinesOf(editor, position);
			if (lines == null)
			{
				return new RichCaretTarget(position with { Offset = end ? editor.Document.TextLength(position) : 0 });
			}

			var stops = RichBlockLayout.FindStop(lines, editor.Document.TextLength(position), position.Offset, atLineEnd).Line.CaretStops;
			if (stops.Count == 0)
			{
				return new RichCaretTarget(position, atLineEnd);
			}

			var ordered = stops.OrderBy(s => s.X).ThenBy(s => s.Caret.Offset);
			var edge = end ? ordered.Last() : ordered.First();
			return new RichCaretTarget(position with { Offset = edge.Caret.Offset }, edge.Caret.AtLineEnd);
		}

		/// <summary>
		/// One drawn line up (<paramref name="direction"/> -1) or down (+1), nearest <paramref name="x"/>: the next
		/// line of the same block or cell, else the same column of the next table row, else the nearest line of the
		/// next block. Past the first or last line it goes to the document's start or end, as text editors do.
		/// </summary>
		public static RichCaretTarget? Vertical(RichMarkdownEditWidget editor, DocPosition position, bool atLineEnd, double x, int direction)
		{
			var document = editor.Document;
			var lines = LinesOf(editor, position);
			if (lines == null)
			{
				return null;
			}

			var line = RichBlockLayout.FindStop(lines, document.TextLength(position), position.Offset, atLineEnd).Line;
			for (int i = lines.IndexOf(line) + direction; i >= 0 && i < lines.Count; i += direction)
			{
				// A line holding only the middle of a split atom has no stop; step over it.
				if (lines[i].CaretStops.Count > 0)
				{
					var hit = RichBlockLayout.HitTestLines(lines, new Vector2(x, (lines[i].Top + lines[i].Bottom) / 2));
					return new RichCaretTarget(position with { Offset = hit.Offset }, hit.AtLineEnd);
				}
			}

			// A point far above or below a layout hits its first or last line (or table row).
			double edgeY = direction < 0 ? -1e9 : 1e9;
			var block = document.Blocks[position.BlockIndex];
			int row = position.Row + direction;
			if (block.Kind == RichBlockKind.Table && row >= 0 && row < block.TableRows.Count
				&& editor.BlockLayout(position.BlockIndex) is RichTableLayout table)
			{
				int column = Math.Min(position.Column, block.TableRows[row].Count - 1);
				var hit = table.Cell(row, column).HitTest(new Vector2(x, edgeY));
				return new RichCaretTarget(new DocPosition(position.BlockIndex, hit.Offset, row, column), hit.AtLineEnd);
			}

			int next = position.BlockIndex + direction;
			if (next < 0)
			{
				return new RichCaretTarget(new DocPosition(0, 0));
			}

			if (next >= document.Blocks.Count)
			{
				return new RichCaretTarget(RichEditOperations.EndOf(document, document.Blocks.Count - 1));
			}

			// Going up lands on the next block's last line, going down on its first.
			var nextHit = editor.BlockLayout(next).HitTest(new Vector2(x, edgeY));
			return new RichCaretTarget(new DocPosition(next, nextHit.Offset, nextHit.Row, nextHit.Column), nextHit.AtLineEnd);
		}

		/// <summary>
		/// The caret's box in <see cref="RichMarkdownEditWidget.DocumentView"/> coordinates, or null before layout.
		/// </summary>
		public static RectangleDouble? CaretBounds(RichMarkdownEditWidget editor, DocPosition position, bool atLineEnd)
		{
			if (!HasLayout(editor, position.BlockIndex))
			{
				return null;
			}

			var rect = editor.BlockLayout(position.BlockIndex).CaretRect(new RichCaret(position.Offset, atLineEnd, position.Row, position.Column));
			rect.Offset(0, editor.BlockOrigin(position.BlockIndex));
			return rect;
		}

		/// <summary>
		/// The start of the next table cell in reading order, else of the next block.
		/// </summary>
		private static RichCaretTarget? NextContainer(RichDocument document, DocPosition position)
		{
			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.Table && block.TableRows.Count > 0)
			{
				int row = position.Row;
				int column = position.Column + 1;
				if (column >= block.TableRows[row].Count)
				{
					row++;
					column = 0;
				}

				if (row < block.TableRows.Count)
				{
					return new RichCaretTarget(new DocPosition(position.BlockIndex, 0, row, column));
				}
			}

			return position.BlockIndex + 1 < document.Blocks.Count
				? new RichCaretTarget(new DocPosition(position.BlockIndex + 1, 0))
				: null;
		}

		/// <summary>
		/// The end of the previous table cell in reading order, else of the previous block.
		/// </summary>
		private static RichCaretTarget? PreviousContainer(RichDocument document, DocPosition position)
		{
			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.Table && block.TableRows.Count > 0 && (position.Row > 0 || position.Column > 0))
			{
				int row = position.Column > 0 ? position.Row : position.Row - 1;
				int column = position.Column > 0 ? position.Column - 1 : block.TableRows[row].Count - 1;
				return new RichCaretTarget(new DocPosition(position.BlockIndex, block.TextLength(row, column), row, column));
			}

			return position.BlockIndex > 0
				? new RichCaretTarget(RichEditOperations.EndOf(document, position.BlockIndex - 1))
				: null;
		}

		/// <summary>
		/// The container's caret offsets in order, from its layout; without a layout (no width yet) every offset.
		/// </summary>
		private static List<int> StopOffsets(RichMarkdownEditWidget editor, DocPosition position)
		{
			var lines = LinesOf(editor, position);
			if (lines == null)
			{
				return Enumerable.Range(0, editor.Document.TextLength(position) + 1).ToList();
			}

			return lines.SelectMany(l => l.CaretStops).Select(s => s.Caret.Offset).Distinct().OrderBy(o => o).ToList();
		}

		private static List<RichLayoutLine> LinesOf(RichMarkdownEditWidget editor, DocPosition position)
		{
			if (!HasLayout(editor, position.BlockIndex))
			{
				return null;
			}

			return editor.BlockLayout(position.BlockIndex) switch
			{
				RichBlockLayout block => block.Lines.Count > 0 ? block.Lines : null,
				RichTableLayout table => table.Cell(position.Row, position.Column).Lines,
				_ => null,
			};
		}

		// Before the widget has a width nothing is laid out, and a block an edit just added has no layout until
		// the relayout that follows it.
		private static bool HasLayout(RichMarkdownEditWidget editor, int blockIndex)
		{
			return editor.Style != null && editor.LaidOutBlockCount == editor.Document.Blocks.Count
				&& blockIndex < editor.Document.Blocks.Count;
		}

		/// <summary>
		/// The container's caret text for word stepping: one placeholder character per atom, as offsets count.
		/// </summary>
		private static string TextOf(RichDocument document, DocPosition position)
		{
			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				return block.CodeText;
			}

			if (block.Kind == RichBlockKind.Raw)
			{
				return "￼";
			}

			var text = new StringBuilder();
			foreach (var inline in block.InlinesAt(position.Row, position.Column))
			{
				text.Append(inline is RichRun run ? run.Text : "￼");
			}

			return text.ToString();
		}
	}
}
