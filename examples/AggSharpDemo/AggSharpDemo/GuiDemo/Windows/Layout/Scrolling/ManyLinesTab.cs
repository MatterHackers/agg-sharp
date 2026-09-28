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
	/// The Scrolling window's "Scroll a lot of lines" tab, agg-gui's many_lines.rs: a row-count slider (10 to 100,000)
	/// over a <see cref="ScrollingRowList"/> that draws only the rows on screen.
	/// </summary>
	public class ManyLinesTab : GuiWidget
	{
		public const int StartRowCount = 10_000;

		private readonly GuiWidget rule;
		private readonly DemoTheme demoTheme;

		internal ManyLinesTab(DemoTheme demoTheme, MiscDemoKit kit)
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
				"A lot of rows, but only the visible ones are painted — the row list reads the ScrollView's viewport rect "
				+ "each frame and skips everything outside it.  Even at 10 000 rows the per-frame text cost is constant.",
				11);
			intro.HAnchor = HAnchor.Stretch;
			column.AddChild(intro);

			this.List = new ScrollingRowList(demoTheme, StartRowCount, 18, i => $"This is row {i + 1}/{this.List.RowCount}");

			this.RowCount = kit.Slider("Scrolling Row Count", StartRowCount, 10, 100_000, 100);
			TextWidget countValue = kit.Label(StartRowCount.ToString());
			this.RowCount.ValueChanged += (s, e) =>
			{
				int count = (int)Math.Round(this.RowCount.Value);
				countValue.Text = count.ToString();
				this.List.RowCount = count;
			};
			column.AddChild(ScrollToTab.Row(kit, "Row count", this.RowCount, countValue));

			this.rule = new GuiWidget(1, DeviceScale) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Absolute, Margin = new BorderDouble(0, 3) };
			column.AddChild(this.rule);

			this.Scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Scrolling Many Lines List",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.Scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			this.Scroll.AddChild(this.List);
			column.AddChild(this.Scroll);

			this.Recolor();
		}

		public Slider RowCount { get; }

		public ScrollableWidget Scroll { get; }

		public ScrollingRowList List { get; }

		public void Recolor()
		{
			this.rule.BackgroundColor = this.demoTheme.Palette.Separator;
		}
	}
}
