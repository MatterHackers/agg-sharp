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
	/// The Scrolling window's "Scroll to" tab, agg-gui's scroll_to.rs: a 500-item list scrolled from code - to a
	/// tracked item with an alignment, to a pixel offset, to either end, or by a step. The tracked item is
	/// highlighted, and moving the slider or changing the alignment scrolls again. Offsets shown and typed are in
	/// design pixels, as agg-gui's are. Controls are named "Scrolling &lt;what&gt;".
	/// </summary>
	public class ScrollToTab : GuiWidget
	{
		public const int ItemCount = 500;
		public const double RowHeight = 18;

		public static readonly string[] AlignLabels = { "Top", "Center", "Bottom", "None (Bring into view)" };

		private readonly DemoTheme demoTheme;
		private readonly TextWidget readout;
		private readonly GuiWidget rule;
		private double lastMaxScroll = double.NaN;

		internal ScrollToTab(DemoTheme demoTheme, MiscDemoKit kit)
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
				"Scroll to a specific item index or pixel offset.  The tracked item is highlighted; moving the slider or "
				+ "changing the alignment re-scrolls the list so the item lands in the chosen position.",
				11);
			intro.HAnchor = HAnchor.Stretch;
			column.AddChild(intro);

			this.TrackItem = kit.Slider("Scrolling Track Item", 25, 1, ItemCount, 1);
			TextWidget trackValue = kit.Label("25");
			this.TrackItem.ValueChanged += (s, e) =>
			{
				trackValue.Text = ((int)Math.Round(this.TrackItem.Value)).ToString();
				this.ScrollToTrackedItem();
			};
			column.AddChild(Row(kit, "Track item", this.TrackItem, trackValue));

			this.Align = new SegmentedControl(AlignLabels, kit.Theme, (int)ScrollAlignment.Center) { Name = "Scrolling Align" };
			this.Align.SelectedIndexChanged += (s, e) => this.ScrollToTrackedItem();
			column.AddChild(Row(kit, "Align", this.Align));

			this.Offset = new DragValue(0, 0, 1e9, kit.Theme) { Name = "Scrolling Offset", Decimals = 0, Speed = 2 };
			this.Offset.Width = Math.Max(this.Offset.Width, 60 * DeviceScale);
			this.Offset.ValueChanged += (s, e) => this.Scroll.SetScrollOffsetFromTop(this.Offset.Value * DeviceScale);
			column.AddChild(Row(kit, "Scroll offset (px)", this.Offset));

			ThemedTextButton top = kit.Button("Scrolling To Top", "Scroll to top");
			top.Click += (s, e) => this.Scroll.SetScrollOffsetFromTop(0);
			ThemedTextButton bottom = kit.Button("Scrolling To Bottom", "Scroll to bottom");
			bottom.Click += (s, e) => this.Scroll.SetScrollOffsetFromTop(this.Scroll.MaxScrollFromTop());
			column.AddChild(Row(kit, null, top, bottom));

			this.StepAmount = new DragValue(64, 1, 1000, kit.Theme) { Name = "Scrolling By Amount", Decimals = 0 };
			this.StepAmount.Width = Math.Max(this.StepAmount.Width, 60 * DeviceScale);
			ThemedIconButton down = kit.GlyphButton("Scrolling By Down", IconFont.ArrowDown);
			down.Click += (s, e) => this.ScrollBy(this.StepAmount.Value);
			ThemedIconButton up = kit.GlyphButton("Scrolling By Up", IconFont.ArrowUp);
			up.Click += (s, e) => this.ScrollBy(-this.StepAmount.Value);
			column.AddChild(Row(kit, "Scroll by", this.StepAmount, down, up));

			this.rule = new GuiWidget(1, DeviceScale) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Absolute, Margin = new BorderDouble(0, 3) };
			column.AddChild(this.rule);

			this.readout = new TextWidget("", pointSize: kit.FontSize(11)) { AutoExpandBoundsToText = true, Margin = new BorderDouble(2, 2) };
			column.AddChild(this.readout);

			this.List = new ScrollingRowList(demoTheme, ItemCount, RowHeight, i => $"This is item {i + 1}") { Highlight = 24 };
			var scroll = new SettledScrollableWidget
			{
				Name = "Scrolling Scroll To List",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 4),
			};
			this.Scroll = scroll;
			this.Scroll.ScrollArea.HAnchor = HAnchor.Stretch;
			this.Scroll.AddChild(this.List);
			this.Scroll.ScrollPositionChanged += (s, e) => this.UpdateReadout();

			// agg-gui's MaxScrollWatcher: the first layout (and any resize) is when the travel is known, so the
			// tracked item is aligned again then - the window opens with item 25 centred.
			scroll.BoundsSettled += (s, e) =>
			{
				double max = this.Scroll.MaxScrollFromTop();
				if (double.IsNaN(this.lastMaxScroll) || Math.Abs(max - this.lastMaxScroll) > .5)
				{
					this.lastMaxScroll = max;
					this.ScrollToTrackedItem();
				}

				this.UpdateReadout();
			};
			column.AddChild(this.Scroll);

			this.Recolor();
		}

		public Slider TrackItem { get; }

		public SegmentedControl Align { get; }

		public DragValue Offset { get; }

		public DragValue StepAmount { get; }

		public ScrollableWidget Scroll { get; }

		public ScrollingRowList List { get; }

		/// <summary>"Scroll offset: 120 / 8460 px", in design pixels.</summary>
		public string ReadoutText => this.readout.Text;

		/// <summary>The list's scroll in design pixels down from its top.</summary>
		public double ScrollOffset => this.Scroll.ScrollOffsetFromTop() / DeviceScale;

		public void Recolor()
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.readout.TextColor = palette.TextDim;
			this.rule.BackgroundColor = palette.Separator;
		}

		/// <summary>A row: an optional label, then <paramref name="widgets"/>.</summary>
		internal static GuiWidget Row(MiscDemoKit kit, string label, params GuiWidget[] widgets)
		{
			FlowLayoutWidget row = kit.Row(8);
			if (label != null)
			{
				TextWidget text = kit.Label(label);
				text.Margin = new BorderDouble(0, 0, 8, 0);
				row.AddChild(text);
			}

			foreach (GuiWidget widget in widgets)
			{
				widget.Margin = widget.Margin.Clone(right: 8);
				row.AddChild(widget);
			}

			return row;
		}

		/// <summary>Highlights the tracked item and scrolls it to where Align says.</summary>
		private void ScrollToTrackedItem()
		{
			int item = (int)Math.Round(this.TrackItem.Value) - 1;
			this.List.Highlight = item;
			double target = ScrollOffsets.TargetForSpan(
				this.List.RowTop(item),
				RowHeight * DeviceScale,
				this.Scroll.LocalBounds.Height,
				this.Scroll.ScrollOffsetFromTop(),
				this.Scroll.MaxScrollFromTop(),
				(ScrollAlignment)this.Align.SelectedIndex);
			this.Scroll.SetScrollOffsetFromTop(target);
			this.UpdateReadout();
		}

		private void ScrollBy(double designPixels)
		{
			this.Scroll.SetScrollOffsetFromTop(this.Scroll.ScrollOffsetFromTop() + designPixels * DeviceScale);
		}

		/// <summary>
		/// A scrollable that says when a resize is finished. ScrollableWidget's LocalBounds setter puts the old scroll
		/// back after BoundsChanged has run, so a scroll set from BoundsChanged is undone; BoundsSettled comes after.
		/// </summary>
		private sealed class SettledScrollableWidget : ScrollableWidget
		{
			public SettledScrollableWidget()
				: base(autoScroll: true)
			{
			}

			public event EventHandler BoundsSettled;

			public override RectangleDouble LocalBounds
			{
				get => base.LocalBounds;
				set
				{
					base.LocalBounds = value;
					this.BoundsSettled?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		private void UpdateReadout()
		{
			double s = DeviceScale;
			this.readout.Text = $"Scroll offset: {this.Scroll.ScrollOffsetFromTop() / s:0} / {this.Scroll.MaxScrollFromTop() / s:0} px";
		}
	}
}
