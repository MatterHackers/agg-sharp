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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// The "Frame" window, a port of agg-gui's demo-ui/src/windows/frame_demo.rs (egui's FrameDemo): on the
	/// left, rows for Inner margin, Outer margin and Corner radius (each a "same" check box that expands to four
	/// values), Shadow (x, y, blur, spread and a colour), Fill, Stroke (width and colour), Reset and the source
	/// link; on the right, a live <see cref="FramePreview"/>. Every control is named "Frame &lt;row&gt; ...".
	/// </summary>
	public class FrameWindow : GuiWidget, IScrollFittedDemoContent
	{
		public const string SourceUrl = "https://github.com/larsbrubaker/agg-gui/blob/main/demo-ui/src/windows/frame_demo.rs";

		private const double ControlsWidth = 360;
		private const double LabelWidth = 104;
		private const double PrefixWidth = 40;

		private static readonly string[] MarginLabels = { "Left", "Right", "Top", "Bottom" };
		private static readonly string[] CornerLabels = { "NW", "NE", "SW", "SE" };

		private readonly DemoTheme demoTheme;
		private readonly FlowLayoutWidget row;
		private readonly GuiWidget separator;
		private MiscDemoKit kit;

		public FrameWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			// The window auto-sizes to the row, as agg-gui's does, so the scroll area never scrolls in the demo;
			// it is kept so the content still works in a window that is not fitted to it.
			this.ScrollArea = new ScrollableWidget(autoScroll: true)
			{
				Name = "Frame Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(this.ScrollArea);

			this.row = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Fit | HAnchor.Left,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = new BorderDouble(8),
			};
			this.ScrollArea.AddChild(this.row);

			this.Controls = this.BuildControls();
			this.row.AddChild(this.Controls);

			this.separator = new GuiWidget(1 * DeviceScale, 1)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(8, 0),
			};
			this.row.AddChild(this.separator);

			this.Preview = new FramePreview(this.State, demoTheme, this.kit.FontSize(13))
			{
				Name = "Frame Preview",
				VAnchor = VAnchor.Top | VAnchor.Absolute,
			};
			this.row.AddChild(this.Preview);

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public FrameState State { get; } = new FrameState();

		public ScrollableWidget ScrollArea { get; }

		/// <summary>The row of controls and preview inside the scroll area, which sizes itself; the window is fitted to it.</summary>
		public GuiWidget FittedContent => this.row;

		public FramePreview Preview { get; }

		/// <summary>The left column. Rebuilt by Reset so every control shows the restored values.</summary>
		public GuiWidget Controls { get; private set; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>Restores egui's defaults and rebuilds the controls to show them.</summary>
		public void Reset()
		{
			this.State.Reset();
			GuiWidget old = this.Controls;
			this.Controls = this.BuildControls();
			this.row.AddChild(this.Controls, 0);
			old.Close();
			this.Recolor();
		}

		private GuiWidget BuildControls()
		{
			this.kit = new MiscDemoKit(this.demoTheme);
			double scale = DeviceScale;
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "Frame Controls",
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Width = ControlsWidth * scale,
			};

			column.AddChild(this.LabeledRow("Inner margin", new FourValueField("Frame Inner margin", this.State.InnerMargin, MarginLabels, this.kit)));
			column.AddChild(this.LabeledRow("Outer margin", new FourValueField("Frame Outer margin", this.State.OuterMargin, MarginLabels, this.kit)));
			column.AddChild(this.LabeledRow("Corner radius", new FourValueField("Frame Corner radius", this.State.CornerRadius, CornerLabels, this.kit)));
			column.AddChild(this.LabeledRow("Shadow", this.ShadowEditor()));

			var fill = new ColorPicker(this.State.Fill, this.kit.Theme) { Name = "Frame Fill Color" };
			fill.ColorChanged += (s, e) => this.State.Fill = fill.Color;
			column.AddChild(this.LabeledRow("Fill", fill));
			column.AddChild(this.LabeledRow("Stroke", this.StrokeEditor()));

			ThemedTextButton reset = this.kit.Button("Frame Reset", "Reset");
			reset.HAnchor = HAnchor.Left;
			reset.Margin = new BorderDouble(0, 4);

			// Reset closes this button; let its click finish first.
			reset.Click += (s, e) => UiThread.RunOnIdle(this.Reset);
			column.AddChild(reset);

			column.AddChild(new Hyperlink("(source code)", this.kit.Theme, SourceUrl)
			{
				Name = "Frame Source Link",
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 4),
			});
			return column;
		}

		/// <summary>agg-gui's labeled_row: a fixed-width label, then the field.</summary>
		private GuiWidget LabeledRow(string label, GuiWidget field)
		{
			var line = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(0, 4),
			};
			TextWidget text = this.kit.Label(label, 13);
			text.AutoExpandBoundsToText = false;
			text.Width = LabelWidth * DeviceScale;
			text.VAnchor = VAnchor.Top;
			text.Margin = new BorderDouble(0, 0, 8, 0);
			line.AddChild(text);
			field.VAnchor = VAnchor.Top | (field.VAnchor & VAnchor.Fit);
			line.AddChild(field);
			return line;
		}

		/// <summary>x and y on one row, blur and spread on the next, then the shadow colour.</summary>
		private GuiWidget ShadowEditor()
		{
			FlowLayoutWidget column = this.kit.Column();
			FlowLayoutWidget offsets = this.kit.Row(4);
			this.LabeledDrag(offsets, "x:", "Frame Shadow X", this.State.ShadowX, -100, v => this.State.ShadowX = v);
			this.LabeledDrag(offsets, "y:", "Frame Shadow Y", this.State.ShadowY, -100, v => this.State.ShadowY = v);
			column.AddChild(offsets);
			FlowLayoutWidget size = this.kit.Row(4);
			this.LabeledDrag(size, "blur:", "Frame Shadow Blur", this.State.ShadowBlur, 0, v => this.State.ShadowBlur = v);
			this.LabeledDrag(size, "spread:", "Frame Shadow Spread", this.State.ShadowSpread, 0, v => this.State.ShadowSpread = v);
			column.AddChild(size);

			var color = new ColorPicker(this.State.ShadowColor, this.kit.Theme) { Name = "Frame Shadow Color" };
			color.ColorChanged += (s, e) => this.State.ShadowColor = color.Color;
			column.AddChild(color);
			return column;
		}

		/// <summary>
		/// agg-gui's labeled_drag, added to <paramref name="row"/>: a prefix label in a fixed column, so the x / blur
		/// and y / spread drags line up, and a whole-number drag value up to 100.
		/// </summary>
		private void LabeledDrag(GuiWidget row, string prefix, string name, double value, double minimum, Action<double> set)
		{
			TextWidget label = this.kit.Label(prefix, 12);
			label.AutoExpandBoundsToText = false;

			// agg-gui's 40 units, widened if the theme's text is too big for "spread:" so it never clips.
			double widest = new TextWidget("spread:", pointSize: this.kit.FontSize(12)) { AutoExpandBoundsToText = true }.Width;
			label.Width = Math.Max(PrefixWidth * DeviceScale, Math.Ceiling(widest));
			label.Margin = new BorderDouble(0, 0, 4, 0);
			row.AddChild(label);
			var drag = new DragValue(value, minimum, 100, this.kit.Theme) { Name = name, Decimals = 0 };
			drag.Width = Math.Max(drag.Width, 60 * DeviceScale);
			drag.Margin = new BorderDouble(0, 0, 6, 0);
			drag.ValueChanged += (s, e) => set(drag.Value);
			row.AddChild(drag);
		}

		/// <summary>The stroke width (0 to 20, one decimal) and its colour.</summary>
		private GuiWidget StrokeEditor()
		{
			FlowLayoutWidget column = this.kit.Column();
			var width = new DragValue(this.State.StrokeWidth, 0, 20, this.kit.Theme)
			{
				Name = "Frame Stroke Width",
				Decimals = 1,
				Speed = .1,
				Margin = new BorderDouble(0, 2),
			};
			width.Width = Math.Max(width.Width, 60 * DeviceScale);
			width.HAnchor = HAnchor.Left | HAnchor.Absolute;
			width.ValueChanged += (s, e) => this.State.StrokeWidth = width.Value;
			column.AddChild(width);

			// agg-gui puts the picker beside the width; its open panel is too wide for that here, so it goes below.
			var color = new ColorPicker(this.State.StrokeColor, this.kit.Theme) { Name = "Frame Stroke Color" };
			color.ColorChanged += (s, e) => this.State.StrokeColor = color.Color;
			column.AddChild(color);
			return column;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.kit.Recolor();
			this.BackgroundColor = this.demoTheme.Palette.PanelFill;
			this.separator.BackgroundColor = this.demoTheme.Palette.Separator;
		}
	}
}
