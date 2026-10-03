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
using Markdig.Renderers.Agg;
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// One laid-out table cell: its box inside the grid lines and its text lines, laid out exactly as a paragraph's
	/// at the column's text width. Coordinates are the owning <see cref="RichTableLayout"/>'s (y up from the bottom
	/// of the table block). Every caret stop on the lines carries this cell's <see cref="RichCaret.Row"/> and
	/// <see cref="RichCaret.Column"/>.
	/// </summary>
	public sealed class RichTableCellLayout
	{
		internal RichTableCellLayout(int row, int column, int length)
		{
			Row = row;
			Column = column;
			Length = length;
		}

		public int Row { get; }

		public int Column { get; }

		/// <summary>
		/// The cell's box between its grid lines, padding included; the row's height, not just the text's.
		/// </summary>
		public RectangleDouble Rect { get; internal set; }

		public List<RichLayoutLine> Lines { get; } = new List<RichLayoutLine>();

		/// <summary>
		/// The cell's largest caret offset.
		/// </summary>
		public int Length { get; }

		/// <summary>
		/// The caret in this cell nearest <paramref name="point"/>, wherever the point is: a click in the padding
		/// above or below the text goes to the first or last line, one left or right of it to that line's ends.
		/// </summary>
		public RichCaret HitTest(Vector2 point)
		{
			return RichBlockLayout.HitTestLines(Lines, point) with { Row = Row, Column = Column };
		}

		/// <summary>
		/// The caret box for <paramref name="offset"/> in this cell (see <see cref="RichBlockLayout.CaretRect(int, bool, double)"/>).
		/// </summary>
		public RectangleDouble CaretRect(int offset, bool atLineEnd = false, double caretWidth = 1)
		{
			var (line, stop) = RichBlockLayout.FindStop(Lines, Length, offset, atLineEnd);
			return RichBlockLayout.CaretBox(line, stop, caretWidth);
		}
	}

	/// <summary>
	/// A Table block laid out to a width, drawn like the markdown viewer's AggTable: a full grid of thin lines, cells
	/// padded inside it, the header row bold and every other body row striped. Each column is as wide as its widest
	/// unwrapped cell; when the table is wider than the block, columns shrink in proportion to how much they can
	/// give (none below <see cref="RichLayoutStyle.TableMinColumnTextWidth"/>) and their cells wrap. A row is as tall
	/// as its tallest cell. Coordinates follow <see cref="RichBlockLayout"/>: y up, origin at the bottom-left of the
	/// block's box with the block spacing included, so the widget stacks a table by its <see cref="Height"/> like any
	/// block.
	/// </summary>
	public sealed class RichTableLayout : IRichBlockLayout
	{
		private readonly List<double> gridLineTops = new List<double>();
		private readonly List<double> columnLefts = new List<double>();
		private double gridLineWidth;

		private RichTableLayout(RichBlock block)
		{
			Block = block;
		}

		public RichBlock Block { get; }

		/// <summary>
		/// The block's whole height, including the style's spacing above and below it.
		/// </summary>
		public double Height { get; private set; }

		/// <summary>
		/// The grid's width, outer grid lines included; at most the width it was laid out to unless every column is
		/// already at its minimum.
		/// </summary>
		public double Width { get; private set; }

		/// <summary>
		/// Each column's width between its grid lines, cell padding included.
		/// </summary>
		public List<double> ColumnWidths { get; } = new List<double>();

		/// <summary>
		/// The cells by row, header row first; every row has one cell per column.
		/// </summary>
		public List<List<RichTableCellLayout>> Rows { get; } = new List<List<RichTableCellLayout>>();

		/// <summary>
		/// The grid as laid out: a thin rectangle above every row, one below the last, and one left of every column
		/// and right of the last. The widget does not fill these directly: it paints the grid from
		/// <see cref="SnapGrid"/>, which puts each line on whole device pixels and keeps crossings from being painted
		/// twice.
		/// </summary>
		public List<RectangleDouble> GridLines { get; } = new List<RectangleDouble>();

		// The left edge of every vertical grid line, left to right, and the bottom edge of every rule, top to bottom,
		// in block coordinates.
		private readonly List<double> lineLefts = new List<double>();
		private readonly List<double> lineBottoms = new List<double>();

		// The rows the viewer stripes, by index (the rows StripedRows boxes).
		internal List<int> StripedRowIndices { get; } = new List<int>();

		/// <summary>
		/// The grid as <see cref="SnapGrid"/> last snapped it, in drawing coordinates: each vertical line's left edge,
		/// each rule's bottom edge (top rule first) and the whole-pixel line thickness. The lists are refilled in
		/// place rather than reallocated each frame.
		/// </summary>
		internal List<double> SnappedLefts { get; } = new List<double>();

		internal List<double> SnappedBottoms { get; } = new List<double>();

		internal int SnappedThickness { get; private set; }

		/// <summary>
		/// Snaps the grid, drawn with the block's bottom at <paramref name="originY"/>, to whole device pixels under
		/// <paramref name="graphics2D"/>'s transform, as the viewer's AggTable does (see <see cref="SnappedLefts"/>).
		/// Returns false for a table with no rows, which has no grid.
		/// </summary>
		internal bool SnapGrid(Graphics2D graphics2D, double originY)
		{
			SnappedLefts.Clear();
			SnappedBottoms.Clear();
			if (Rows.Count == 0)
			{
				return false;
			}

			var transform = graphics2D.GetTransform();
			SnappedThickness = Math.Max(1, (int)Math.Round(gridLineWidth));
			foreach (double left in lineLefts)
			{
				SnappedLefts.Add(GridPixels.Snap(left, transform.tx));
			}

			foreach (double bottom in lineBottoms)
			{
				SnappedBottoms.Add(GridPixels.Snap(originY + bottom, transform.ty));
			}

			return true;
		}

		/// <summary>
		/// The header row's box inside the outer grid lines, for a widget that shades it; null for a table with no
		/// rows. The viewer leaves it unshaded and marks the header only by its bold text.
		/// </summary>
		public RectangleDouble? HeaderBackground { get; private set; }

		/// <summary>
		/// The body rows the viewer stripes (in <see cref="RichLayoutStyle.TableStripeColor"/>): the second, fourth, ...
		/// body rows.
		/// </summary>
		public List<RectangleDouble> StripedRows { get; } = new List<RectangleDouble>();

		public RichTableCellLayout Cell(int row, int column) => Rows[row][column];

		/// <summary>
		/// Lays a Table block out to <paramref name="width"/>.
		/// </summary>
		public static RichTableLayout Layout(RichBlock block, double width, RichLayoutStyle style)
		{
			if (block.Kind != RichBlockKind.Table)
			{
				throw new ArgumentException("Only Table blocks have a table layout.", nameof(block));
			}

			var layout = new RichTableLayout(block);
			var (before, after) = style.BlockSpacing(block);
			double line = style.TableGridLineWidth;
			layout.gridLineWidth = line;
			double padX = style.TableCellPaddingX;
			double padY = style.TableCellPaddingY;
			int columnCount = 0;
			foreach (var row in block.TableRows)
			{
				columnCount = Math.Max(columnCount, row.Count);
			}

			// Each cell's items are built once: they give the column's natural width and are then broken into lines.
			var items = new List<LayoutItem[]>[block.TableRows.Count];
			var natural = new double[columnCount];
			for (int r = 0; r < block.TableRows.Count; r++)
			{
				bool header = r == 0;
				var face = style.TableCellFace(header);
				items[r] = new List<LayoutItem[]>();
				for (int c = 0; c < block.TableRows[r].Count; c++)
				{
					var cellItems = RichLayoutItems.InlineItems(block.TableRows[r][c].Inlines, run => style.TableRunFace(run, header), style, face);
					items[r].Add(cellItems);
					natural[c] = Math.Max(natural[c], NaturalWidth(cellItems));
				}
			}

			double availableText = width - (columnCount + 1) * line - columnCount * 2 * padX;
			var textWidths = ColumnTextWidths(natural, availableText, style.TableMinColumnTextWidth);

			double x = line;
			foreach (double textWidth in textWidths)
			{
				layout.columnLefts.Add(x);
				layout.ColumnWidths.Add(textWidth + 2 * padX);
				x += textWidth + 2 * padX + line;
			}

			layout.Width = columnCount == 0 ? 0 : x;

			// Rows are stacked top-down first (y down from the block's top), then flipped once the height is known.
			var rowTops = new List<double>();
			var rowBottoms = new List<double>();
			double y = before;
			for (int r = 0; r < block.TableRows.Count; r++)
			{
				layout.gridLineTops.Add(y);
				double rowTop = y + line;
				double rowBottom = rowTop;
				var rowLayouts = new List<RichTableCellLayout>();
				var face = style.TableCellFace(r == 0);
				for (int c = 0; c < block.TableRows[r].Count; c++)
				{
					var cell = new RichTableCellLayout(r, c, block.TextLength(r, c));
					var alignment = c < block.ColumnAlignments.Count ? block.ColumnAlignments[c] ?? RichAlignment.Left : RichAlignment.Left;
					var source = new RichBlockLayout.LineSource(block.TableRows[r][c].Inlines, null, alignment, face);
					double textBottom = RichBlockLayout.StackLines(cell.Lines, items[r][c], source, rowTop + padY, layout.columnLefts[c] + padX, Math.Max(textWidths[c], 1), cell.Length, wrapAtSpaces: true, style.LineGap);
					rowBottom = Math.Max(rowBottom, textBottom + padY);
					rowLayouts.Add(cell);
				}

				layout.Rows.Add(rowLayouts);
				rowTops.Add(rowTop);
				rowBottoms.Add(rowBottom);
				y = rowBottom;
			}

			double gridTop = before;
			double gridBottom = block.TableRows.Count == 0 ? before : y + line;
			if (block.TableRows.Count > 0)
			{
				layout.gridLineTops.Add(y);
			}

			layout.Height = gridBottom + after;
			double Flip(double down) => layout.Height - down;

			for (int r = 0; r < layout.Rows.Count; r++)
			{
				foreach (var cell in layout.Rows[r])
				{
					RichBlockLayout.FlipLines(cell.Lines, layout.Height);
					MarkCell(cell);
					double left = layout.columnLefts[cell.Column];
					cell.Rect = new RectangleDouble(left, Flip(rowBottoms[r]), left + layout.ColumnWidths[cell.Column], Flip(rowTops[r]));
				}

				var rowBox = new RectangleDouble(line, Flip(rowBottoms[r]), layout.Width - line, Flip(rowTops[r]));
				if (r == 0)
				{
					layout.HeaderBackground = rowBox;
				}
				else if (r % 2 == 0)
				{
					// The viewer counts its header as row 0 and stripes the even rows after it.
					layout.StripedRows.Add(rowBox);
					layout.StripedRowIndices.Add(r);
				}
			}

			foreach (double top in layout.gridLineTops)
			{
				layout.GridLines.Add(new RectangleDouble(0, Flip(top + line), layout.Width, Flip(top)));
				layout.lineBottoms.Add(Flip(top + line));
			}

			if (layout.Rows.Count > 0)
			{
				foreach (double left in layout.columnLefts)
				{
					layout.GridLines.Add(new RectangleDouble(left - line, Flip(gridBottom), left, Flip(gridTop)));
					layout.lineLefts.Add(left - line);
				}

				layout.GridLines.Add(new RectangleDouble(layout.Width - line, Flip(gridBottom), layout.Width, Flip(gridTop)));
				layout.lineLefts.Add(layout.Width - line);
			}

			return layout;
		}

		/// <summary>
		/// The caret nearest <paramref name="point"/> (block coordinates), in the cell whose box holds it. A point on
		/// a grid line goes to the nearer of the two cells it divides (the boundary is the line's middle), and a
		/// point outside the grid to the nearest edge cell; within the cell, padding clicks go to the nearest caret.
		/// </summary>
		public RichCaret HitTest(Vector2 point)
		{
			if (Rows.Count == 0 || columnLefts.Count == 0)
			{
				return new RichCaret(0);
			}

			double down = Height - point.Y;
			int row = 0;
			for (int r = 1; r < Rows.Count; r++)
			{
				if (down >= gridLineTops[r] + gridLineWidth / 2)
				{
					row = r;
				}
			}

			int column = 0;
			for (int c = 1; c < columnLefts.Count; c++)
			{
				if (point.X >= columnLefts[c] - gridLineWidth / 2)
				{
					column = c;
				}
			}

			return Rows[row][Math.Min(column, Rows[row].Count - 1)].HitTest(point);
		}

		/// <summary>
		/// The caret box for <paramref name="offset"/> in the cell at <paramref name="row"/>, <paramref name="column"/>
		/// (see <see cref="RichBlockLayout.CaretRect(int, bool, double)"/>).
		/// </summary>
		public RectangleDouble CaretRect(int row, int column, int offset, bool atLineEnd = false, double caretWidth = 1)
		{
			return Cell(row, column).CaretRect(offset, atLineEnd, caretWidth);
		}

		public RectangleDouble CaretRect(RichCaret caret, double caretWidth = 1) => CaretRect(caret.Row, caret.Column, caret.Offset, caret.AtLineEnd, caretWidth);

		/// <summary>
		/// Each column's text width. Columns get their natural width when the table fits. Otherwise each column
		/// keeps a minimum (its natural width when that is smaller) and the room left over is shared in proportion
		/// to how much wider than its minimum each column would like to be, so a long column gives up the most and a
		/// short one keeps its text on one line. With no room past the minimums every column sits at its minimum
		/// and the table runs past the width rather than squeezing text to nothing.
		/// </summary>
		private static double[] ColumnTextWidths(double[] natural, double available, double minimumWidth)
		{
			double naturalSum = 0;
			double minimumSum = 0;
			var minimums = new double[natural.Length];
			for (int c = 0; c < natural.Length; c++)
			{
				minimums[c] = Math.Min(natural[c], minimumWidth);
				naturalSum += natural[c];
				minimumSum += minimums[c];
			}

			if (naturalSum <= available)
			{
				return (double[])natural.Clone();
			}

			var widths = new double[natural.Length];
			double share = available > minimumSum ? (available - minimumSum) / (naturalSum - minimumSum) : 0;
			for (int c = 0; c < natural.Length; c++)
			{
				widths[c] = minimums[c] + (natural[c] - minimums[c]) * share;
			}

			return widths;
		}

		// The width of the cell's widest hard-broken line laid out unwrapped, trailing spaces not counted, summed
		// the same way BreakLines sums so a column at this width does not wrap.
		private static double NaturalWidth(LayoutItem[] items)
		{
			double widest = 0;
			double x = 0;
			foreach (var item in items)
			{
				if (item.Kind == LayoutItemKind.Break)
				{
					x = 0;
					continue;
				}

				x += item.AdvanceAt(x);
				if (item.Kind == LayoutItemKind.Char || item.Kind == LayoutItemKind.Box)
				{
					widest = Math.Max(widest, x);
				}
			}

			return widest;
		}

		// Stamps the cell's row and column onto every caret stop, so a stop the widget reads (for up/down movement)
		// already names its cell.
		private static void MarkCell(RichTableCellLayout cell)
		{
			foreach (var line in cell.Lines)
			{
				for (int i = 0; i < line.CaretStops.Count; i++)
				{
					var stop = line.CaretStops[i];
					line.CaretStops[i] = stop with { Caret = stop.Caret with { Row = cell.Row, Column = cell.Column } };
				}
			}
		}
	}
}
