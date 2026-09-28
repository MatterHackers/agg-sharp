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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Misc Demos" window, a port of agg-gui's demo-ui/src/windows/misc/misc_demos.rs (itself egui's
	/// MiscDemoWindow): a scrolling column of collapsing sections - Label (open), Misc widgets, Colors, Tree,
	/// Checkboxes, Columns, Test box rendering, Custom Collapsing Header, Misc, Resize and Many circles.
	/// Each header row is named "&lt;title&gt; Header" by CollapsingHeader.
	/// </summary>
	public class MiscDemosWindow : GuiWidget
	{
		public static readonly string[] SectionTitles =
		{
			"Label", "Misc widgets", "Colors", "Tree", "Checkboxes", "Columns", "Test box rendering",
			"Custom Collapsing Header", "Misc", "Resize", "Many circles of different sizes",
		};

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly FlowLayoutWidget content;
		private FlowLayoutWidget columnsHost;

		public MiscDemosWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.ScrollArea = new ScrollableWidget(autoScroll: true)
			{
				Name = "Misc Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.ScrollArea.ScrollArea.HAnchor = HAnchor.Stretch;
			this.AddChild(this.ScrollArea);

			this.content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(6, 14, 6, 6),
			};
			this.ScrollArea.AddChild(this.content);

			this.Section("Label", true, this.LabelSection());
			this.Section("Misc widgets", false, MiscDemoSections.MiscWidgets(this.kit));
			this.Section("Colors", false, MiscDemoSections.Colors(this.kit));
			this.Section("Tree", false, new MiscTreeSection(this.kit));
			this.Section("Checkboxes", false, MiscDemoSections.Checkboxes(this.kit));
			this.Section("Columns", false, this.ColumnsSection());
			this.Section("Test box rendering", false, this.BoxRenderingSection());
			this.Section("Custom Collapsing Header", false, MiscDemoSections.CustomCollapsing(this.kit));
			this.Section("Misc", false, MiscDemoSections.PaintIcon(this.kit));
			this.Section("Resize", false, this.ResizeSection());
			this.Section("Many circles of different sizes", false, new MiscManyCircles(this.kit.Theme) { Name = "Misc Many Circles" });

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public ScrollableWidget ScrollArea { get; }

		/// <summary>The Columns section's slider: how many columns the row below it shows.</summary>
		public Slider ColumnCountSlider { get; private set; }

		public MiscBoxPainter BoxPainter { get; private set; }

		public ResizeArea ResizeArea { get; private set; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void Section(string title, bool expanded, GuiWidget body)
		{
			var section = new CollapsingHeader(title, this.kit.Theme, expanded)
			{
				Name = $"Misc Section {title}",
				Margin = new BorderDouble(0, 1),
			};
			body.Margin = body.Margin + new BorderDouble(4, 6, 0, 4);
			section.Body.AddChild(body);
			this.content.AddChild(section);
		}

		/// <summary>Coloured text on one row, then the default font's script coverage.</summary>
		private GuiWidget LabelSection()
		{
			FlowLayoutWidget column = this.kit.Column();
			FlowLayoutWidget colors = this.kit.Row();
			colors.Name = "Misc Label Colors";
			colors.AddChild(this.kit.Label("Text can have"));
			colors.AddChild(this.kit.Label("color,", color: new ColorF(0.43, 1.0, 0.43).ToColor()));
			colors.AddChild(this.kit.Label("size,", color: new ColorF(0.50, 0.55, 1.0).ToColor()));
			colors.AddChild(this.kit.Label("and style.", color: new ColorF(1.0, 0.75, 0.40).ToColor()));
			foreach (GuiWidget child in colors.Children)
			{
				child.Margin = new BorderDouble(0, 0, 6, 0);
			}

			column.AddChild(colors);
			column.AddChild(this.kit.Wrapped("The default font supports latin, cyrillic (ИÅđ…), math (∫√∞²⅓…), and emojis (💓🌟🖩…)."));
			return column;
		}

		/// <summary>A slider (1 to 10) driving a row of that many equal columns; the last can delete itself.</summary>
		private GuiWidget ColumnsSection()
		{
			FlowLayoutWidget column = this.kit.Column();
			FlowLayoutWidget sliderRow = this.kit.Row(8);
			TextWidget label = this.kit.Label("Columns");
			label.Margin = new BorderDouble(0, 0, 8, 0);
			sliderRow.AddChild(label);
			this.ColumnCountSlider = this.kit.Slider("Misc Columns Slider", 2, 1, 10, 1);
			sliderRow.AddChild(this.ColumnCountSlider);
			TextWidget value = this.kit.Label("2");
			sliderRow.AddChild(value);
			column.AddChild(sliderRow);

			this.columnsHost = new FlowLayoutWidget
			{
				Name = "Misc Columns",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(0, 4),
			};
			column.AddChild(this.columnsHost);

			this.ColumnCountSlider.ValueChanged += (s, e) =>
			{
				value.Text = this.ColumnCount.ToString();
				this.RebuildColumns();
			};
			this.RebuildColumns();
			return column;
		}

		private int ColumnCount => Math.Clamp((int)Math.Round(this.ColumnCountSlider.Value), 1, 10);

		private void RebuildColumns()
		{
			this.columnsHost.CloseChildren();
			int count = this.ColumnCount;
			for (int i = 0; i < count; i++)
			{
				var cell = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Name = $"Misc Column {i}",
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit | VAnchor.Top,
					Margin = new BorderDouble(0, 0, 8, 0),
				};

				// Not kept by the kit - a theme change rebuilds the columns instead.
				cell.AddChild(new WrappedTextWidget($"Column {i + 1} out of {count}", this.kit.FontSize(11.5), textColor: this.kit.Theme.TextColor));
				if (i == count - 1)
				{
					var delete = new ThemedTextButton("Delete this", this.kit.Theme)
					{
						Name = "Misc Columns Delete",
						HAnchor = HAnchor.Left,
						Margin = new BorderDouble(0, 2),
					};

					// The slider's change rebuilds the row, closing this button; let its click finish first.
					delete.Click += (s, e) => UiThread.RunOnIdle(() => this.ColumnCountSlider.Value = Math.Max(1, this.ColumnCount - 1));
					cell.AddChild(delete);
				}

				this.columnsHost.AddChild(cell);
			}
		}

		/// <summary>Five sliders - width, height, corner radius, stroke width, count - over the painted boxes.</summary>
		private GuiWidget BoxRenderingSection()
		{
			FlowLayoutWidget column = this.kit.Column();
			this.BoxPainter = new MiscBoxPainter(this.kit.Theme) { Name = "Misc Box Painter" };
			MiscBoxPainter painter = this.BoxPainter;
			column.AddChild(this.BoxSlider("width", painter.BoxWidth, 0, 500, 1, v => painter.BoxWidth = v));
			column.AddChild(this.BoxSlider("height", painter.BoxHeight, 0, 500, 1, v => painter.BoxHeight = v));
			column.AddChild(this.BoxSlider("corner radius", painter.CornerRadius, 0, 50, .5, v => painter.CornerRadius = v));
			column.AddChild(this.BoxSlider("stroke width", painter.StrokeWidth, 0, 10, .5, v => painter.StrokeWidth = v));
			column.AddChild(this.BoxSlider("number of boxes", painter.BoxCount, 0, 8, 1, v => painter.BoxCount = (int)Math.Round(v)));
			column.AddChild(painter);
			return column;
		}

		/// <summary>egui's slider row: the slider, its value, then what it sets.</summary>
		private GuiWidget BoxSlider(string label, double value, double minimum, double maximum, double step, Action<double> set)
		{
			FlowLayoutWidget row = this.kit.Row(2);
			Slider slider = this.kit.Slider($"Misc Box {label}", value, minimum, maximum, step);
			row.AddChild(slider);
			TextWidget valueText = this.kit.Label(FormatValue(value, step));
			valueText.MinimumSize = new Vector2(36 * DeviceScale, 0);
			valueText.Margin = new BorderDouble(0, 0, 6, 0);
			row.AddChild(valueText);
			row.AddChild(this.kit.Label(label));
			slider.ValueChanged += (s, e) =>
			{
				set(slider.Value);
				valueText.Text = FormatValue(slider.Value, step);
			};
			return row;
		}

		private static string FormatValue(double value, double step) => step < 1 ? value.ToString("0.0") : value.ToString("0");

		/// <summary>A 250 by 100 region with a bottom-right grip, its text pinned to the top.</summary>
		private GuiWidget ResizeSection()
		{
			double scale = DeviceScale;
			this.ResizeArea = new ResizeArea(250 * scale, 100 * scale, this.kit.Theme)
			{
				Name = "Misc Resize",
				MinimumSize = new Vector2(80 * scale, 40 * scale),
				MaximumSize = new Vector2(4000 * scale, 3000 * scale),
			};

			var text = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Left | HAnchor.Fit,
				VAnchor = VAnchor.Top | VAnchor.Fit,
				Padding = new BorderDouble(8),
			};
			text.AddChild(this.kit.Label("This ui can be resized!"));
			text.AddChild(this.kit.Label("Just pull the handle on the bottom right"));
			this.ResizeArea.AddChild(text);
			return this.ResizeArea;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.RebuildColumns();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.kit.Recolor();
			this.BackgroundColor = this.demoTheme.Palette.PanelFill;
		}
	}
}
