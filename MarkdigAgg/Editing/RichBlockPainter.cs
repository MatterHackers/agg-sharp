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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The colours the rich editor paints with besides the text, link and table colours its
	/// <see cref="RichLayoutStyle"/> holds.
	/// </summary>
	public sealed class RichEditColors
	{
		public Color CodeBackground { get; set; }

		public Color QuoteBar { get; set; }

		public Color Selection { get; set; }

		public Color Caret { get; set; }

		public Color Hint { get; set; }
	}

	/// <summary>
	/// Paints one laid-out block (<see cref="RichBlockLayout"/> or <see cref="RichTableLayout"/>): backgrounds,
	/// quote bar, list marker, table grid, text, images, selection and caret. Every coordinate in a layout is
	/// relative to its block's bottom, so each call takes the y that bottom sits at in the widget.
	/// </summary>
	internal static class RichBlockPainter
	{
		public static void DrawBlock(Graphics2D graphics2D, IRichBlockLayout layout, double originY, RichLayoutStyle style, RichEditColors colors, Func<InlineAtom, ImageBuffer> loadedImage)
		{
			if (layout is RichTableLayout table)
			{
				DrawTable(graphics2D, table, originY, style, colors, loadedImage);
				return;
			}

			var text = (RichBlockLayout)layout;
			if (text.CodeBackground is RectangleDouble code)
			{
				graphics2D.FillRectangle(Offset(code, originY), colors.CodeBackground);
			}

			if (text.QuoteBar is RectangleDouble bar)
			{
				graphics2D.FillRectangle(Offset(bar, originY), colors.QuoteBar);
			}

			if (text.MarkerIsBullet)
			{
				DrawBullet(graphics2D, text, originY, style);
			}
			else if (text.Marker != null)
			{
				DrawText(graphics2D, text.Marker, text.MarkerFace, text.MarkerX, originY + text.MarkerBaseline, style.TextColor);
			}

			DrawLines(graphics2D, text.Lines, text.Block.Inlines, originY, style, colors, loadedImage);
		}

		// A bullet is drawn, not typed: a filled dot, a hollow dot, then a small square by depth
		// (RichLayoutStyle.BulletMarker), centred on the x-height in the box BulletBoxWidth gave it.
		private static void DrawBullet(Graphics2D graphics2D, RichBlockLayout text, double originY, RichLayoutStyle style)
		{
			double box = RichLayoutStyle.BulletBoxWidth(text.MarkerFace);
			double centerX = text.MarkerX + box / 2;
			double centerY = originY + text.MarkerBaseline + text.MarkerFace.XHeightInPixels / 2;
			double radius = box * 0.32;
			switch (text.Marker)
			{
				case "•":
					graphics2D.Circle(centerX, centerY, radius, style.TextColor);
					break;
				case "◦":
					var ring = new Stroke(new Ellipse(centerX, centerY, radius, radius), Math.Max(1, style.Scale));
					graphics2D.Render(ring, style.TextColor);
					break;
				default:
					graphics2D.FillRectangle(centerX - radius, centerY - radius, centerX + radius, centerY + radius, style.TextColor);
					break;
			}
		}

		private static void DrawTable(Graphics2D graphics2D, RichTableLayout table, double originY, RichLayoutStyle style, RichEditColors colors, Func<InlineAtom, ImageBuffer> loadedImage)
		{
			// Painted like the viewer's AggTable: every line on whole device pixels, stripes between the lines and
			// verticals between the rules, so no pixel of the translucent grid colour is painted twice. The header is
			// marked only by its bold face, which the layout already chose.
			if (table.SnapGrid(graphics2D, originY))
			{
				var lefts = table.SnappedLefts;
				var bottoms = table.SnappedBottoms;
				int line = table.SnappedThickness;
				foreach (int r in table.StripedRowIndices)
				{
					for (int c = 0; c + 1 < lefts.Count; c++)
					{
						graphics2D.FillRectangle(lefts[c] + line, bottoms[r + 1] + line, lefts[c + 1], bottoms[r], style.TableStripeColor);
					}
				}

				double gridLeft = lefts[0];
				double gridRight = lefts[lefts.Count - 1] + line;
				foreach (double bottom in bottoms)
				{
					graphics2D.FillRectangle(gridLeft, bottom, gridRight, bottom + line, style.TableGridColor);
				}

				foreach (double left in lefts)
				{
					for (int r = 0; r + 1 < bottoms.Count; r++)
					{
						graphics2D.FillRectangle(left, bottoms[r + 1] + line, left + line, bottoms[r], style.TableGridColor);
					}
				}
			}

			foreach (var row in table.Rows)
			{
				foreach (var cell in row)
				{
					DrawLines(graphics2D, cell.Lines, table.Block.InlinesAt(cell.Row, cell.Column), originY, style, colors, loadedImage);
				}
			}
		}

		private static void DrawLines(Graphics2D graphics2D, List<RichLayoutLine> lines, List<RichInline> inlines, double originY, RichLayoutStyle style, RichEditColors colors, Func<InlineAtom, ImageBuffer> loadedImage)
		{
			foreach (var line in lines)
			{
				foreach (var fragment in line.Fragments)
				{
					RichInline inline = fragment.Atom;
					if (inline == null && fragment.InlineIndex >= 0 && fragment.InlineIndex < inlines.Count)
					{
						inline = inlines[fragment.InlineIndex];
					}

					double baseline = originY + fragment.Baseline;
					if (fragment.Face == null)
					{
						// A box: an image draws itself here; a Raw block's box is covered by its hosted child widget.
						if (fragment.Atom?.Kind == InlineAtomKind.Image)
						{
							var imageBox = new RectangleDouble(fragment.X, baseline, fragment.X + fragment.Width, baseline + fragment.Ascent);
							DrawImage(graphics2D, loadedImage(fragment.Atom), imageBox, colors);
						}

						continue;
					}

					if (inline is RichRun { Code: true })
					{
						graphics2D.FillRectangle(fragment.X, baseline - fragment.Descent, fragment.X + fragment.Width, baseline + fragment.Ascent, colors.CodeBackground);
					}

					// A tab is laid out as an advance to the next stop; drawn as a glyph it would show a missing-glyph box.
					if (fragment.Text.Length == 0 || fragment.Text == "\t")
					{
						continue;
					}

					bool link = inline?.LinkUrl != null || fragment.Atom?.Kind == InlineAtomKind.Autolink;
					var color = link ? style.LinkColor : style.TextColor;
					DrawText(graphics2D, fragment.Text, fragment.Face, fragment.X, baseline, color);
					if (inline?.Strike == true)
					{
						// The fonts have no strikethrough of their own, so draw it through the middle of the lower case.
						double strikeY = baseline + fragment.Ascent * .3;
						graphics2D.FillRectangle(fragment.X, strikeY, fragment.X + fragment.Width, strikeY + Math.Max(1, style.Scale), color);
					}
				}
			}
		}

		/// <summary>
		/// Highlights the part of <paramref name="selection"/> inside block <paramref name="blockIndex"/>: the whole
		/// block for a <see cref="RichSelection.WholeBlock"/> selection, otherwise its text line by line. In a table
		/// the range runs through the cells in reading order, as the table edit operations walk them.
		/// </summary>
		public static void DrawSelection(Graphics2D graphics2D, IRichBlockLayout layout, double originY, RichSelection selection, int blockIndex, double width, Color color)
		{
			var start = selection.Start;
			var end = selection.End;
			if (blockIndex < start.BlockIndex || blockIndex > end.BlockIndex)
			{
				return;
			}

			if (selection.WholeBlock)
			{
				if (layout is RichTableLayout wholeTable && wholeTable.SnapGrid(graphics2D, originY))
				{
					FillAroundGrid(graphics2D, wholeTable, originY, width, color);
				}
				else
				{
					graphics2D.FillRectangle(0, originY, width, originY + layout.Height, color);
				}

				return;
			}

			if (layout is RichBlockLayout text)
			{
				int from = blockIndex == start.BlockIndex ? start.Offset : 0;
				int to = blockIndex == end.BlockIndex ? end.Offset : int.MaxValue;
				DrawLineSelection(graphics2D, text.Lines, originY, from, to, color);
				return;
			}

			var table = (RichTableLayout)layout;
			if (table.Rows.Count == 0)
			{
				return;
			}

			int columns = table.Rows[0].Count;
			int firstCell = blockIndex == start.BlockIndex ? start.Row * columns + start.Column : 0;
			int lastCell = blockIndex == end.BlockIndex ? end.Row * columns + end.Column : table.Rows.Count * columns - 1;
			for (int index = firstCell; index <= lastCell && index / columns < table.Rows.Count; index++)
			{
				var cell = table.Rows[index / columns][index % columns];
				int from = blockIndex == start.BlockIndex && index == firstCell ? start.Offset : 0;
				int to = blockIndex == end.BlockIndex && index == lastCell ? end.Offset : int.MaxValue;
				DrawLineSelection(graphics2D, cell.Lines, originY, from, to, color);
			}
		}

		// A selected table is shaded everywhere in its block except on the grid lines (just snapped by the caller),
		// so the translucent selection never darkens a line and the grid keeps one tone, as when unselected.
		private static void FillAroundGrid(Graphics2D graphics2D, RichTableLayout table, double originY, double width, Color color)
		{
			var lefts = table.SnappedLefts;
			var bottoms = table.SnappedBottoms;
			int line = table.SnappedThickness;
			double gridLeft = lefts[0];
			double gridRight = lefts[lefts.Count - 1] + line;
			double gridTop = bottoms[0] + line;
			double gridBottom = bottoms[bottoms.Count - 1];

			// A table wider than the block runs past width, so a band can be empty; skip it rather than fill it inverted.
			void Fill(double left, double bottom, double right, double top)
			{
				if (right > left && top > bottom)
				{
					graphics2D.FillRectangle(left, bottom, right, top, color);
				}
			}

			Fill(0, gridTop, width, originY + table.Height);
			Fill(0, originY, width, gridBottom);
			Fill(0, gridBottom, gridLeft, gridTop);
			Fill(gridRight, gridBottom, width, gridTop);
			for (int r = 0; r + 1 < bottoms.Count; r++)
			{
				for (int c = 0; c + 1 < lefts.Count; c++)
				{
					Fill(lefts[c] + line, bottoms[r + 1] + line, Math.Min(lefts[c + 1], width), bottoms[r]);
				}
			}
		}

		private static void DrawLineSelection(Graphics2D graphics2D, List<RichLayoutLine> lines, double originY, int start, int end, Color color)
		{
			if (start >= end)
			{
				return;
			}

			foreach (var line in lines)
			{
				// The offset a line wraps at is the next line's start, so a range ending there stops on this line.
				int lineEnd = line.IsLast ? int.MaxValue : line.End;
				if (end <= line.Start || start > lineEnd)
				{
					continue;
				}

				double left = start <= line.Start ? line.Left : StopX(line, start, preferLineEnd: false) ?? line.Left;
				double right = end >= lineEnd && !line.IsLast
					? Math.Max(line.Left + line.Width, StopX(line, line.End, preferLineEnd: true) ?? 0)
					: StopX(line, end, preferLineEnd: true) ?? line.Left + line.Width;
				if (right > left)
				{
					graphics2D.FillRectangle(left, originY + line.Bottom, right, originY + line.Top, color);
				}
			}
		}

		/// <summary>
		/// Draws the caret. A stale caret (the document changed under it) is clamped into the block, or the table,
		/// so it still draws somewhere sensible rather than throwing.
		/// </summary>
		public static void DrawCaret(Graphics2D graphics2D, IRichBlockLayout layout, double originY, RichCaret caret, RichLayoutStyle style, Color color)
		{
			if (layout is RichTableLayout table)
			{
				if (table.Rows.Count == 0)
				{
					return;
				}

				int row = Math.Clamp(caret.Row, 0, table.Rows.Count - 1);
				int column = Math.Clamp(caret.Column, 0, table.Rows[row].Count - 1);
				caret = caret with { Row = row, Column = column, Offset = Math.Clamp(caret.Offset, 0, table.Cell(row, column).Length) };
			}
			else
			{
				caret = caret with { Offset = Math.Clamp(caret.Offset, 0, ((RichBlockLayout)layout).Block.TextLength()) };
			}

			graphics2D.FillRectangle(Offset(layout.CaretRect(caret, style.CaretWidth), originY), color);
		}

		private static double? StopX(RichLayoutLine line, int offset, bool preferLineEnd)
		{
			double? plain = null;
			foreach (var stop in line.CaretStops)
			{
				if (stop.Caret.Offset == offset)
				{
					if (stop.Caret.AtLineEnd == preferLineEnd)
					{
						return stop.X;
					}

					plain ??= stop.X;
				}
			}

			return plain;
		}

		private static void DrawImage(Graphics2D graphics2D, ImageBuffer image, RectangleDouble box, RichEditColors colors)
		{
			if (image != null && image.Width > 0 && image.Height > 0)
			{
				graphics2D.Render(image, box.Left, box.Bottom, box.Width, box.Height);
			}
			else
			{
				// Not loaded (yet): a shaded box keeps the space visible so a caret beside it makes sense.
				graphics2D.FillRectangle(box, colors.CodeBackground);
			}
		}

		private static void DrawText(Graphics2D graphics2D, string text, StyledTypeFace face, double x, double baseline, Color color)
		{
			new TypeFacePrinter(text, face, new Vector2(x, baseline)).Render(graphics2D, color);
		}

		private static RectangleDouble Offset(RectangleDouble rect, double originY)
		{
			return new RectangleDouble(rect.Left, rect.Bottom + originY, rect.Right, rect.Top + originY);
		}
	}
}
