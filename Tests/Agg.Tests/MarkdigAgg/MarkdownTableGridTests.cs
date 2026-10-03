/*
Copyright (c) 2026, Lars Brubaker
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
using System.Linq;
using System.Threading.Tasks;
using Markdig.Renderers.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	// Keyless: the test changes GuiWidget.DeviceScale, which nearly every UI test reads (and it builds a MarkdownWidget).
	[NotInParallel]
	public class MarkdownTableGridTests
	{
		/// <summary>
		/// A rendered table must show one connected grid: every horizontal rule runs exactly from the left outer
		/// border to the right one, and every vertical line runs unbroken from the top rule to the bottom rule.
		/// Read from the drawn pixels, so it checks what the Help viewer actually paints.
		/// </summary>
		[Test]
		[Arguments(1.0)]
		[Arguments(1.25)]
		[Arguments(1.5)]
		[Arguments(2.0)]
		public async Task TableGridLinesMeetAtEveryJunction(double deviceScale)
		{
			double savedScale = GuiWidget.DeviceScale;
			GuiWidget.DeviceScale = deviceScale;
			try
			{
				var markdownWidget = new MarkdownWidget(new ThemeConfig(), scrollContent: false)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					Markdown =
					"""
					| Feature | Status | Notes |
					| :--- | :---: | ---: |
					| Headings | Ready | 6 levels |
					| Tables | Ready | Pipe and grid |
					"""
				};
				var container = new GuiWidget(700 * deviceScale, 260 * deviceScale)
				{
					DoubleBuffer = true,
					BackgroundColor = Color.White
				};
				container.AddChild(markdownWidget);
				container.PerformLayout();
				var graphics = container.BackBuffer.NewGraphics2D();
				graphics.Clear(Color.White);
				container.OnDraw(graphics);

				var image = container.BackBuffer;
				var table = markdownWidget.Descendants<AggTable>().Single();
				var tableBounds = table.TransformToParentSpace(container, table.LocalBounds);
				int minX = Math.Max(0, (int)Math.Floor(tableBounds.Left));
				int maxX = Math.Min(image.Width - 1, (int)Math.Ceiling(tableBounds.Right));
				int minY = Math.Max(0, (int)Math.Floor(tableBounds.Bottom));
				int maxY = Math.Min(image.Height - 1, (int)Math.Ceiling(tableBounds.Top));

				// Grid lines are the theme text color at alpha 150 (about 135 gray on white); stripes stay near 236.
				bool Ink(int x, int y) => image.GetPixel(x, y).red < 180;

				// A line is a run of pixel rows (columns) inked across most of the table; text never covers that much.
				// The table stretches to the page but its grid is only as wide as the columns, so the verticals are
				// found first and the rules are measured across the span they bound.
				var vertical = Groups(Enumerable.Range(minX, maxX - minX + 1)
					.Where(x => Enumerable.Range(minY, maxY - minY + 1).Count(y => Ink(x, y)) > (maxY - minY) * 0.6));
				await Assert.That(vertical.Count).IsGreaterThanOrEqualTo(2);
				int spanLeft = vertical.First().Min();
				int spanRight = vertical.Last().Max();
				var horizontal = Groups(Enumerable.Range(minY, maxY - minY + 1)
					.Where(y => Enumerable.Range(spanLeft, spanRight - spanLeft + 1).Count(x => Ink(x, y)) > (spanRight - spanLeft) * 0.6));

				// 3 rows -> 4 horizontal rules; 3 columns -> 4 vertical lines.
				await Assert.That(horizontal.Count).IsEqualTo(4);
				await Assert.That(vertical.Count).IsEqualTo(4);

				int gridLeft = vertical.First().Min();
				int gridRight = vertical.Last().Max();
				int gridBottom = horizontal.First().Min();
				int gridTop = horizontal.Last().Max();

				foreach (int y in horizontal.SelectMany(group => group))
				{
					// The rule reaches both outer borders and stops there.
					int gaps = Enumerable.Range(gridLeft, gridRight - gridLeft + 1).Count(x => !Ink(x, y));
					await Assert.That(gaps).IsEqualTo(0);
					await Assert.That(Ink(gridLeft - 1, y)).IsFalse();
					await Assert.That(Ink(gridRight + 1, y)).IsFalse();
				}

				foreach (int x in vertical.SelectMany(group => group))
				{
					// The line is unbroken from the bottom rule to the top rule, and stops at them.
					int gaps = Enumerable.Range(gridBottom, gridTop - gridBottom + 1).Count(y => !Ink(x, y));
					await Assert.That(gaps).IsEqualTo(0);
					await Assert.That(Ink(x, gridBottom - 1)).IsFalse();
					await Assert.That(Ink(x, gridTop + 1)).IsFalse();
				}

				// Every line is the same whole number of device pixels thick.
				int thickness = Math.Max(1, (int)Math.Round(deviceScale));
				await Assert.That(horizontal.All(group => group.Count == thickness)).IsTrue();
				await Assert.That(vertical.All(group => group.Count == thickness)).IsTrue();

				// Each column shows the width layout gave its cells: no line ate into a cell, and consistent rounding
				// kept every column the same (at 1.25 the cell edges fall on half pixels).
				var headerCells = table.Rows[0].Cells;
				for (int c = 0; c < headerCells.Count; c++)
				{
					int interior = vertical[c + 1].Min() - vertical[c].Max() - 1;
					await Assert.That(Math.Abs(interior - headerCells[c].Width)).IsLessThanOrEqualTo(0.5);
				}
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
			}
		}

		private static List<List<int>> Groups(IEnumerable<int> sortedValues)
		{
			var groups = new List<List<int>>();
			foreach (int value in sortedValues)
			{
				if (groups.Count == 0 || groups[^1][^1] != value - 1)
				{
					groups.Add(new List<int>());
				}

				groups[^1].Add(value);
			}

			return groups;
		}
	}
}
