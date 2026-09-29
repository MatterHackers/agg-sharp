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
*/

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling
{
	/// <summary>
	/// agg-gui's RowList (demo-ui/src/windows/scrolling/helpers.rs): <see cref="RowCount"/> striped text rows, one of
	/// them optionally highlighted in the accent colour, as tall as all its rows but drawing only the ones inside the
	/// draw's clipping rect - so 100,000 rows cost what the dozen on screen cost. Goes in a
	/// <see cref="ScrollableWidget"/> whose scroll area stretches.
	/// </summary>
	public class ScrollingRowList : GuiWidget
	{
		private readonly DemoTheme demoTheme;
		private readonly Func<int, string> formatter;
		private int rowCount;
		private int? highlight;

		/// <param name="rowHeight">In design units.</param>
		public ScrollingRowList(DemoTheme demoTheme, int rowCount, double rowHeight, Func<int, string> formatter)
		{
			this.demoTheme = demoTheme;
			this.formatter = formatter;
			this.RowHeight = rowHeight;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.RowCount = rowCount;
		}

		/// <summary>A row's height in design units.</summary>
		public double RowHeight { get; }

		/// <summary>The design point size of the row text.</summary>
		public double PointSize { get; set; } = 12;

		public int RowCount
		{
			get => this.rowCount;
			set
			{
				this.rowCount = Math.Max(0, value);
				this.Height = this.rowCount * this.RowHeight * DeviceScale;
				this.Invalidate();
			}
		}

		/// <summary>The row painted in the accent colour, or null.</summary>
		public int? Highlight
		{
			get => this.highlight;
			set
			{
				this.highlight = value;
				this.Invalidate();
			}
		}

		/// <summary>The rows the last draw painted, [First, End) - what virtualisation is measured by.</summary>
		public (int First, int End) LastDrawnRows { get; private set; }

		/// <summary>Where row <paramref name="row"/>'s top is, in device pixels down from the list's top.</summary>
		public double RowTop(int row) => row * this.RowHeight * DeviceScale;

		public override void OnDraw(Graphics2D graphics2D)
		{
			double s = DeviceScale;
			double rowHeight = this.RowHeight * s;
			RectangleDouble bounds = this.LocalBounds;
			RectangleDouble clip = ScrollOffsets.LocalClippingRect(graphics2D);
			this.LastDrawnRows = ScrollOffsets.VisibleRows(bounds.Top, Math.Max(clip.Bottom, bounds.Bottom), Math.Min(clip.Top, bounds.Top), rowHeight, this.rowCount);

			DemoPalette palette = this.demoTheme.Palette;
			Color accent = this.demoTheme.Theme.PrimaryAccentColor;
			Color stripe = palette.TextColor.WithAlpha(13);
			Color highlightFill = accent.WithAlpha(64);
			double pointSize = DemoText.Points(this.PointSize) * s;
			for (int row = this.LastDrawnRows.First; row < this.LastDrawnRows.End; row++)
			{
				double top = bounds.Top - row * rowHeight;
				double bottom = top - rowHeight;
				if (row % 2 == 0)
				{
					graphics2D.FillRectangle(bounds.Left, bottom, bounds.Right, top, stripe);
				}

				bool highlighted = this.highlight == row;
				if (highlighted)
				{
					graphics2D.FillRectangle(bounds.Left, bottom, bounds.Right, top, highlightFill);
				}

				graphics2D.DrawString(this.formatter(row), bounds.Left + 8 * s, (top + bottom) / 2, pointSize, baseline: Agg.Font.Baseline.BoundsCenter, color: highlighted ? accent : palette.TextColor);
			}

			base.OnDraw(graphics2D);
		}
	}
}
