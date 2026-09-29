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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling
{
	/// <summary>
	/// The Scrolling window's "Bidirectional" tab, agg-gui's bidirectional.rs: 100 lorem-ipsum paragraphs as single
	/// unwrapped lines in a view that scrolls both ways - a horizontal bar and shift+wheel
	/// (<see cref="ScrollableWidget.HorizontalScroll"/>) as well as the vertical ones.
	/// </summary>
	public class BidirectionalTab : GuiWidget
	{
		private readonly GuiWidget rule;
		private readonly DemoTheme demoTheme;

		internal BidirectionalTab(DemoTheme demoTheme, MiscDemoKit kit)
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
				"100 lorem-ipsum paragraphs, rendered as single non-wrapped lines.  Use the scrollbars or shift+wheel for horizontal scroll.",
				11);
			intro.HAnchor = HAnchor.Stretch;
			column.AddChild(intro);

			this.rule = new GuiWidget(1, DeviceScale) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Absolute, Margin = new BorderDouble(0, 3) };
			column.AddChild(this.rule);

			this.Canvas = new LoremCanvas(demoTheme);
			this.Scroll = new ScrollableWidget(autoScroll: true)
			{
				Name = "Scrolling Bidirectional",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				HorizontalScroll = true,
			};
			this.Scroll.AddChild(this.Canvas);
			column.AddChild(this.Scroll);

			this.Recolor();
		}

		public ScrollableWidget Scroll { get; }

		public LoremCanvas Canvas { get; }

		public void Recolor()
		{
			this.rule.BackgroundColor = this.demoTheme.Palette.Separator;
		}

		/// <summary>The paragraphs, as wide as one line of text plus its padding and as tall as all of them.</summary>
		public class LoremCanvas : GuiWidget
		{
			public const int LineCount = 100;
			public const double LineHeight = 20;
			public const double PaddingX = 8;

			private readonly DemoTheme demoTheme;

			public LoremCanvas(DemoTheme demoTheme)
			{
				this.demoTheme = demoTheme;
				double s = DeviceScale;
				double textWidth = new TypeFacePrinter(PanelsWindow.LoremIpsum, this.PointSize).LocalBounds.Width;
				this.LocalBounds = new RectangleDouble(0, 0, textWidth + PaddingX * 2 * s, LineCount * LineHeight * s);
			}

			/// <summary>The text size in device points, at the theme's font size.</summary>
			private double PointSize => DemoText.Points(12) * DeviceScale;

			public override void OnDraw(Graphics2D graphics2D)
			{
				double s = DeviceScale;
				RectangleDouble bounds = this.LocalBounds;
				RectangleDouble clip = ScrollOffsets.LocalClippingRect(graphics2D);
				(int first, int end) = ScrollOffsets.VisibleRows(bounds.Top, Math.Max(clip.Bottom, bounds.Bottom), Math.Min(clip.Top, bounds.Top), LineHeight * s, LineCount);
				Color text = this.demoTheme.Palette.TextColor;
				for (int line = first; line < end; line++)
				{
					double middle = bounds.Top - (line + .5) * LineHeight * s;
					graphics2D.DrawString(PanelsWindow.LoremIpsum, bounds.Left + PaddingX * s, middle, this.PointSize, baseline: Baseline.BoundsCenter, color: text);
				}

				base.OnDraw(graphics2D);
			}
		}
	}
}
