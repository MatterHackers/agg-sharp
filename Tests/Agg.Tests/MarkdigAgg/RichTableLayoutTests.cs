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
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichTableLayoutTests
	{
		private const string Sentence = "The quick brown fox jumps over the lazy dog and keeps on running far away";

		// A fixed scale so the numbers do not depend on the machine's display.
		private static RichLayoutStyle Style() => new RichLayoutStyle(deviceScale: 1);

		// rows[0] is the header; each string is one cell's plain text.
		private static RichBlock Table(params string[][] rows)
		{
			var block = new RichBlock { Kind = RichBlockKind.Table };
			foreach (var row in rows)
			{
				var cells = new List<RichTableCell>();
				foreach (var text in row)
				{
					var cell = new RichTableCell();
					if (text.Length > 0)
					{
						cell.Inlines.Add(new RichRun(text));
					}

					cells.Add(cell);
				}

				block.TableRows.Add(cells);
				block.ColumnAlignments = new List<RichAlignment?>(new RichAlignment?[row.Length]);
			}

			return block;
		}

		[Test]
		public async Task ATableThatFitsKeepsItsNaturalWidthAndAWideOneShrinksToTheBlock()
		{
			var block = Table(new[] { "Name", "Notes" }, new[] { "Fox", Sentence + " " + Sentence });
			var style = Style();

			var wide = RichTableLayout.Layout(block, 5000, style);
			await Assert.That(wide.Width).IsLessThan(5000);
			await Assert.That(wide.Cell(1, 1).Lines.Count).IsEqualTo(1);

			var narrow = RichTableLayout.Layout(block, 300, style);
			double sum = 0;
			foreach (double column in narrow.ColumnWidths)
			{
				sum += column;
			}

			await Assert.That(sum).IsLessThanOrEqualTo(300);
			await Assert.That(narrow.Width).IsLessThanOrEqualTo(300);

			// The long column gives up the room; the short one keeps its text on one line.
			await Assert.That(narrow.ColumnWidths[0]).IsEqualTo(wide.ColumnWidths[0]).Within(0.001);
			await Assert.That(narrow.Cell(1, 0).Lines.Count).IsEqualTo(1);
			await Assert.That(narrow.Cell(1, 1).Lines.Count).IsGreaterThan(1);
			foreach (var line in narrow.Cell(1, 1).Lines)
			{
				await Assert.That(line.Left + line.Width).IsLessThanOrEqualTo(narrow.Cell(1, 1).Rect.Right);
			}
		}

		[Test]
		public async Task ARowIsAsTallAsItsTallestCell()
		{
			var layout = RichTableLayout.Layout(Table(new[] { "A", "B" }, new[] { "short", Sentence }), 200, Style());
			var shortCell = layout.Cell(1, 0);
			var tallCell = layout.Cell(1, 1);

			await Assert.That(tallCell.Lines.Count).IsGreaterThan(1);
			await Assert.That(shortCell.Rect.Height).IsEqualTo(tallCell.Rect.Height).Within(0.001);
			await Assert.That(layout.Cell(1, 0).Rect.Height).IsGreaterThan(layout.Cell(0, 0).Rect.Height);
		}

		[Test]
		public async Task HeaderCellsAreBoldAndBodyCellsAreNot()
		{
			var layout = RichTableLayout.Layout(Table(new[] { "Head" }, new[] { "Body" }), 400, Style());

			await Assert.That(layout.Cell(0, 0).Lines[0].Fragments[0].Face.TypeFace).IsEqualTo(AggContext.DefaultFontBold);
			await Assert.That(layout.Cell(1, 0).Lines[0].Fragments[0].Face.TypeFace).IsEqualTo(AggContext.DefaultFont);
			await Assert.That(layout.HeaderBackground).IsNotNull();
			await Assert.That(layout.HeaderBackground.Value.Top).IsEqualTo(layout.Cell(0, 0).Rect.Top).Within(0.001);
		}

		[Test]
		public async Task ColumnAlignmentOffsetsEachCellsText()
		{
			var block = Table(new[] { "Left column header", "Center column header", "Right column header" }, new[] { "a", "b", "c" });
			block.ColumnAlignments = new List<RichAlignment?> { null, RichAlignment.Center, RichAlignment.Right };
			var style = Style();
			var layout = RichTableLayout.Layout(block, 1000, style);

			var left = layout.Cell(1, 0);
			var center = layout.Cell(1, 1);
			var right = layout.Cell(1, 2);
			double padX = style.TableCellPaddingX;

			await Assert.That(left.Lines[0].Left).IsEqualTo(left.Rect.Left + padX).Within(0.001);
			double centerText = center.Rect.Width - 2 * padX;
			await Assert.That(center.Lines[0].Left).IsEqualTo(center.Rect.Left + padX + (centerText - center.Lines[0].Width) / 2).Within(0.001);
			await Assert.That(right.Lines[0].Left + right.Lines[0].Width).IsEqualTo(right.Rect.Right - padX).Within(0.001);
		}

		[Test]
		public async Task EveryCaretInEveryCellHitTestsBackToItself()
		{
			var block = Table(new[] { "Name", "", "Notes" }, new[] { "Fox", "", Sentence }, new[] { "", "x", "Dog" });
			block.ColumnAlignments = new List<RichAlignment?> { null, RichAlignment.Center, RichAlignment.Right };
			var layout = RichTableLayout.Layout(block, 260, Style());

			for (int row = 0; row < block.TableRows.Count; row++)
			{
				for (int column = 0; column < block.TableRows[row].Count; column++)
				{
					foreach (var line in layout.Cell(row, column).Lines)
					{
						double? previousX = null;
						foreach (var stop in line.CaretStops)
						{
							var caret = stop.Caret;
							await Assert.That(caret.Row).IsEqualTo(row);
							await Assert.That(caret.Column).IsEqualTo(column);

							// Trailing spaces past a wrapped line's edge share an x; only the first is clickable.
							bool sharesX = previousX == stop.X;
							previousX = stop.X;
							if (!sharesX)
							{
								var rect = layout.CaretRect(caret);
								await Assert.That(layout.HitTest(rect.Center)).IsEqualTo(caret);
							}
						}
					}
				}
			}
		}

		[Test]
		public async Task AnEmptyCellHoldsACaret()
		{
			var layout = RichTableLayout.Layout(Table(new[] { "A", "" }, new[] { "", "" }), 400, Style());
			var cell = layout.Cell(1, 1);
			var rect = layout.CaretRect(1, 1, 0);

			await Assert.That(rect.Height).IsGreaterThan(0);
			await Assert.That(cell.Rect.Contains(rect.Center)).IsTrue();
			await Assert.That(layout.HitTest(rect.Center)).IsEqualTo(new RichCaret(0, Row: 1, Column: 1));
		}

		[Test]
		public async Task PaddingAndGridLineClicksGoToTheNearestCell()
		{
			var layout = RichTableLayout.Layout(Table(new[] { "Alpha", "Beta" }, new[] { "one", "two" }), 400, Style());
			var topLeft = layout.Cell(0, 0);
			var bottomRight = layout.Cell(1, 1);

			// The cell's top-left padding corner goes to its first caret.
			await Assert.That(layout.HitTest(new Vector2(topLeft.Rect.Left + 1, topLeft.Rect.Top - 1))).IsEqualTo(new RichCaret(0));

			// Just right of and just left of the middle of the vertical line between the columns.
			double divider = (topLeft.Rect.Right + layout.Cell(0, 1).Rect.Left) / 2;
			double y = topLeft.Rect.YCenter;
			await Assert.That(layout.HitTest(new Vector2(divider - 0.1, y)).Column).IsEqualTo(0);
			await Assert.That(layout.HitTest(new Vector2(divider + 0.1, y)).Column).IsEqualTo(1);

			// Above and below the middle of the line between the rows.
			double rule = (topLeft.Rect.Bottom + layout.Cell(1, 0).Rect.Top) / 2;
			await Assert.That(layout.HitTest(new Vector2(topLeft.Rect.XCenter, rule + 0.1)).Row).IsEqualTo(0);
			await Assert.That(layout.HitTest(new Vector2(topLeft.Rect.XCenter, rule - 0.1)).Row).IsEqualTo(1);

			// Past the grid's bottom-right corner is the last cell's end.
			await Assert.That(layout.HitTest(new Vector2(layout.Width + 50, -50))).IsEqualTo(new RichCaret(3, Row: 1, Column: 1));
			await Assert.That(bottomRight.Rect.Right).IsLessThan(layout.Width);
		}

		[Test]
		public async Task AHeaderOnlyTableLaysOutWithAFullGrid()
		{
			var style = Style();
			var layout = RichTableLayout.Layout(Table(new[] { "Only", "Header" }), 400, style);
			var (before, after) = style.BlockSpacing(layout.Block);

			await Assert.That(layout.Rows.Count).IsEqualTo(1);
			await Assert.That(layout.StripedRows.Count).IsEqualTo(0);

			// Two horizontal rules (above and below the row) and three vertical ones.
			await Assert.That(layout.GridLines.Count).IsEqualTo(5);
			double line = style.TableGridLineWidth;
			await Assert.That(layout.Height).IsEqualTo(before + line + layout.Cell(0, 0).Rect.Height + line + after).Within(0.001);
			var header = layout.Cell(0, 1).Rect;
			await Assert.That(layout.HitTest(new Vector2(header.Right - 1, header.YCenter))).IsEqualTo(new RichCaret(6, Row: 0, Column: 1));
		}

		[Test]
		public async Task EveryOtherBodyRowIsStripedLikeTheViewer()
		{
			var layout = RichTableLayout.Layout(Table(new[] { "H" }, new[] { "1" }, new[] { "2" }, new[] { "3" }, new[] { "4" }), 400, Style());

			await Assert.That(layout.StripedRows.Count).IsEqualTo(2);
			await Assert.That(layout.StripedRows[0].Top).IsEqualTo(layout.Cell(2, 0).Rect.Top).Within(0.001);
			await Assert.That(layout.StripedRows[1].Top).IsEqualTo(layout.Cell(4, 0).Rect.Top).Within(0.001);
		}
	}
}
