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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>VirtualTable's column sharing, row geometry, virtualised drawing, scroll-to-row, resize and clicks.</summary>
	public class VirtualTableTests
	{
		private static TableColumn[] DemoColumns() => new[]
		{
			TableColumn.Auto(56, resizable: true),
			TableColumn.Remainder(atLeast: 40, clip: true, resizable: true),
			TableColumn.Auto(72, resizable: true),
		};

		[Test]
		public async Task RemainderColumnsShareWhatFixedColumnsLeave()
		{
			double[] widths = TableColumn.DistributeWidths(DemoColumns(), 300, null);
			await Assert.That(widths).IsEquivalentTo(new double[] { 56, 172, 72 });

			// Too narrow: the remainder column keeps its at-least and the table scrolls.
			widths = TableColumn.DistributeWidths(DemoColumns(), 100, null);
			await Assert.That(widths[1]).IsEqualTo(40);

			// A dragged width is pinned and taken out of the share; nothing goes under the minimum.
			widths = TableColumn.DistributeWidths(DemoColumns(), 300, new double?[] { 100, null, 2 });
			await Assert.That(widths).IsEquivalentTo(new double[] { 100, 184, TableColumn.MinimumWidth });
		}

		[Test]
		public async Task HeterogeneousRowsFindTheirRowsByPosition()
		{
			TableRows rows = TableRows.Heterogeneous(new double[] { 30, 18, 18, 18, 18, 18, 30 });
			await Assert.That(rows.TotalHeight).IsEqualTo(150);
			await Assert.That(rows.TopOf(2)).IsEqualTo(48);
			await Assert.That(rows.RowAt(0)).IsEqualTo(0);
			await Assert.That(rows.RowAt(29.9)).IsEqualTo(0);
			await Assert.That(rows.RowAt(30)).IsEqualTo(1);
			await Assert.That(rows.RowAt(149)).IsEqualTo(6);
			await Assert.That(rows.RowAt(150)).IsEqualTo(-1);
			await Assert.That(rows.RowAt(-1)).IsEqualTo(-1);

			TableRows same = TableRows.Homogeneous(100_000, 18);
			await Assert.That(same.RowAt(18 * 5000 + 1)).IsEqualTo(5000);
			await Assert.That(same.TopOf(200_000)).IsEqualTo(18.0 * 100_000);
		}

		[Test]
		public async Task OnlyTheRowsInViewArePainted()
		{
			double s = GuiWidget.DeviceScale;
			var table = new VirtualTable(DemoColumns())
			{
				Width = 300 * s,
				Height = (22 + 180) * s,
				Rows = TableRows.Homogeneous(100_000, 18),
			};
			var painted = new HashSet<int>();
			table.CellPainter = (g, cell) => painted.Add(cell.Row);

			table.OnDraw(new ImageBuffer((int)table.Width, (int)table.Height).NewGraphics2D());
			await Assert.That(painted.OrderBy(r => r)).IsEquivalentTo(Enumerable.Range(0, 10));

			painted.Clear();
			table.ScrollToRow(5000);
			await Assert.That(table.ScrollOffset).IsEqualTo(18.0 * 5000);
			table.OnDraw(new ImageBuffer((int)table.Width, (int)table.Height).NewGraphics2D());
			await Assert.That(painted.Min()).IsEqualTo(5000);
			await Assert.That(painted.Count).IsLessThanOrEqualTo(11);

			// Past the end it stops where the last row sits at the bottom.
			table.ScrollToRow(1_000_000);
			await Assert.That(table.ScrollOffset).IsEqualTo(table.MaxScrollOffset);
			await Assert.That(table.MaxScrollOffset).IsEqualTo(18.0 * 100_000 - 180);
		}

		[Test]
		public async Task DraggingAHeaderEdgeResizesItsColumnAndARowClickReportsTheCell()
		{
			double s = GuiWidget.DeviceScale;
			var table = new VirtualTable(DemoColumns())
			{
				Width = 308 * s,
				Height = 222 * s,
				Rows = TableRows.Homogeneous(50, 20),
				ClickableRows = true,
			};
			(int Row, int Column) clicked = (-1, -1);
			table.RowClicked += (row, column) => clicked = (row, column);

			// The first column's edge sits at 56; drag it to 90.
			double headerY = 211 * s;
			table.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 57 * s, headerY, 0));
			table.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 91 * s, headerY, 0));
			table.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 91 * s, headerY, 0));
			await Assert.That(table.ColumnWidths[0]).IsEqualTo(90).Within(1e-9);
			await Assert.That(table.ColumnWidths[1]).IsEqualTo(300 - 90 - 72).Within(1e-9);
			await Assert.That(clicked.Row).IsEqualTo(-1);

			// Row 2 is 40..60 below the header; x 100 is in the second column.
			table.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 100 * s, 150 * s, 0));
			table.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 100 * s, 150 * s, 0));
			await Assert.That(clicked).IsEqualTo((2, 1));

			table.ResetColumnWidths();
			await Assert.That(table.ColumnWidths[0]).IsEqualTo(56);
		}
	}
}
