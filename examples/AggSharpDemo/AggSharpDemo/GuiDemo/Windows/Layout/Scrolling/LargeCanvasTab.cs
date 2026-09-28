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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling
{
	/// <summary>
	/// The Scrolling window's "Scroll a large canvas" tab, agg-gui's large_canvas.rs: 10,000 rows, each indented by its
	/// index mod 100, on a canvas wider than the view, drawn only where the view shows them.
	/// </summary>
	public class LargeCanvasTab : GuiWidget
	{
		private readonly GuiWidget rule;
		private readonly DemoTheme demoTheme;

		internal LargeCanvasTab(DemoTheme demoTheme, MiscDemoKit kit)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(10),
			};
			this.AddChild(column);

			WrappedTextWidget intro = kit.Wrapped(
				"10 000 rows, indented by their index mod 100, painted only where the viewport intersects them.  "
				+ "Both axes scroll — horizontal via shift+wheel or the bottom scrollbar.",
				11);
			intro.HAnchor = HAnchor.Stretch;
			column.AddChild(intro);

			this.rule = new GuiWidget(1, DeviceScale) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Absolute, Margin = new BorderDouble(0, 3) };
			column.AddChild(this.rule);

			this.Canvas = new LargeCanvas(demoTheme);
			this.Scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Scrolling Large Canvas",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				HorizontalScroll = true,
			};
			this.Scroll.AddChild(this.Canvas);
			column.AddChild(this.Scroll);

			this.Recolor();
		}

		public ScrollableWidget Scroll { get; }

		public LargeCanvas Canvas { get; }

		public void Recolor()
		{
			this.rule.BackgroundColor = this.demoTheme.Palette.Separator;
		}

		/// <summary>The 700 x 200,000 design-pixel canvas, drawing just the rows its clip reaches.</summary>
		public class LargeCanvas : GuiWidget
		{
			public const int RowCount = 10_000;
			public const double RowHeight = 20;
			public const double ContentWidth = 700;

			private readonly DemoTheme demoTheme;

			public LargeCanvas(DemoTheme demoTheme)
				: base(ContentWidth * DeviceScale, RowCount * RowHeight * DeviceScale)
			{
				this.demoTheme = demoTheme;
			}

			/// <summary>The rows the last draw painted, [First, End).</summary>
			public (int First, int End) LastDrawnRows { get; private set; }

			public static string RowText(int row) => $"This is row {row + 1}/{RowCount}, indented by {row % 100} pixels";

			public override void OnDraw(Graphics2D graphics2D)
			{
				double s = DeviceScale;
				RectangleDouble bounds = this.LocalBounds;
				RectangleDouble clip = ScrollOffsets.LocalClippingRect(graphics2D);
				this.LastDrawnRows = ScrollOffsets.VisibleRows(bounds.Top, Math.Max(clip.Bottom, bounds.Bottom), Math.Min(clip.Top, bounds.Top), RowHeight * s, RowCount);
				Color text = this.demoTheme.Palette.TextColor;
				double pointSize = this.demoTheme.Theme.DefaultFontSize * s;
				for (int row = this.LastDrawnRows.First; row < this.LastDrawnRows.End; row++)
				{
					double middle = bounds.Top - (row + .5) * RowHeight * s;
					graphics2D.DrawString(RowText(row), bounds.Left + row % 100 * s, middle, pointSize, baseline: Agg.Font.Baseline.BoundsCenter, color: text);
				}

				base.OnDraw(graphics2D);
			}
		}
	}
}
