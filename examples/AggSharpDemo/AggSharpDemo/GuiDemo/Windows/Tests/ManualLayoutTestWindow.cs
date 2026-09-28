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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// agg-gui's "Manual Layout Test" window (tests/basic/layout.rs manual_layout_test, after egui's
	/// manual_layout_test.rs): pick a widget type, then place a real widget of that type with the position and size
	/// sliders inside a bounded canvas. Deviation: agg-gui's slider draws its value in a strip of its own; here
	/// the value is a label beside the track.
	/// </summary>
	public class ManualLayoutTestWindow : FlowLayoutWidget
	{
		/// <summary>The widget types, in radio order.</summary>
		public static readonly string[] WidgetTypes = { "Button", "Label", "TextEdit" };

		public const string DefaultText = "Editable text — this widget is placed manually.";

		// egui's defaults: offset (150, 150), size (200, 100).
		private const double DefaultX = 150;
		private const double DefaultY = 150;
		private const double DefaultWidth = 200;
		private const double DefaultHeight = 100;

		private const double Gap = 8;

		private readonly MiscDemoKit kit;
		private readonly List<RadioButton> typeRadios = new List<RadioButton>();

		public ManualLayoutTestWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(10);
			this.kit = new MiscDemoKit(demoTheme);
			double s = DeviceScale;

			ThemedTextButton reset = this.kit.Button("Manual Layout Reset", "Reset");
			reset.HAnchor = HAnchor.Left | HAnchor.Absolute;
			reset.VAnchor = VAnchor.Absolute;
			reset.Size = new Vector2(80 * s, 28 * s);
			reset.Margin = new BorderDouble(bottom: Gap);
			reset.Click += (sender, e) => this.Reset();
			this.AddChild(reset);

			// "Test widget:" beside agg-gui's vertical radio group.
			var typeRow = new FlowLayoutWidget { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit, Margin = new BorderDouble(bottom: Gap) };
			TextWidget typeLabel = this.kit.Label("Test widget:");
			typeLabel.VAnchor = VAnchor.Top;
			typeLabel.Margin = new BorderDouble(right: Gap);
			typeRow.AddChild(typeLabel);
			var radioColumn = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Fit, VAnchor = VAnchor.Fit };
			for (int i = 0; i < WidgetTypes.Length; i++)
			{
				int index = i;
				RadioButton radio = this.kit.Radio("Manual Layout " + WidgetTypes[i], WidgetTypes[i], 12);
				radio.HAnchor = HAnchor.Left;
				radio.CheckedStateChanged += (sender, e) =>
				{
					if (radio.Checked)
					{
						this.SetWidgetType(index);
					}
				};
				radioColumn.AddChild(radio);
				this.typeRadios.Add(radio);
			}

			typeRow.AddChild(radioColumn);
			this.AddChild(typeRow);

			this.XSlider = this.AddSliderRow("Widget position:", "Manual Layout X", "Manual Layout Y", out ValueSlider y);
			this.YSlider = y;
			this.WidthSlider = this.AddSliderRow("Widget size:", "Manual Layout Width", "Manual Layout Height", out ValueSlider height);
			this.HeightSlider = height;

			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(s)),
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(separator);

			this.Canvas = new ManualLayoutCanvas(demoTheme) { Name = "Manual Layout Canvas" };
			this.AddChild(this.Canvas);

			foreach (ValueSlider slider in new[] { this.XSlider, this.YSlider, this.WidthSlider, this.HeightSlider })
			{
				slider.Slider.ValueChanged += (sender, e) => this.PlaceFromSliders();
			}

			void Recolor(object sender, EventArgs e)
			{
				this.BackgroundColor = demoTheme.Palette.PanelFill;
				separator.BackgroundColor = demoTheme.Palette.Separator;
				this.kit.Recolor();
				this.RecolorPlaced();
				this.Canvas.Invalidate();
			}

			this.Reset();
			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (sender, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The chosen widget type: an index into <see cref="WidgetTypes"/>.</summary>
		public int WidgetType { get; private set; } = -1;

		/// <summary>The edit field's text, kept across type switches as agg-gui's shared text cell.</summary>
		public string Text { get; private set; } = DefaultText;

		public IReadOnlyList<RadioButton> TypeRadios => this.typeRadios;

		public ValueSlider XSlider { get; }

		public ValueSlider YSlider { get; }

		public ValueSlider WidthSlider { get; }

		public ValueSlider HeightSlider { get; }

		public ManualLayoutCanvas Canvas { get; }

		/// <summary>Back to egui's defaults: a Button at (150, 150), 200 by 100, and the edit text as it started.</summary>
		public void Reset()
		{
			this.Text = DefaultText;
			this.XSlider.Slider.Value = DefaultX;
			this.YSlider.Slider.Value = DefaultY;
			this.WidthSlider.Slider.Value = DefaultWidth;
			this.HeightSlider.Slider.Value = DefaultHeight;

			// Rebuild even when Button is already chosen, so an edit field would pick up the reset text.
			this.WidgetType = -1;
			this.typeRadios[0].Checked = true;
			this.SetWidgetType(0);
		}

		private void SetWidgetType(int index)
		{
			if (index == this.WidgetType)
			{
				return;
			}

			this.WidgetType = index;
			GuiWidget placed;
			switch (index)
			{
				case 1:
					placed = new TextWidget("Example label", pointSize: this.kit.FontSize(13), textColor: this.kit.Theme.TextColor);
					break;

				case 2:
					// agg-gui's TextArea: the plain-text CodeEditor, wrapping to the size the sliders give it.
					var editor = new CodeEditor(this.Text, pointSize: this.kit.FontSize(12.5))
					{
						Border = 1,
						ShowLineNumbers = false,
						WordWrap = true,
						AggGuiEditing = true,
					};
					editor.Document.TextChanged += (sender, e) => this.Text = editor.Text;
					placed = editor;
					break;

				default:
					var button = new ThemedTextButton("Example button", this.kit.Theme, this.kit.FontSize(13));
					placed = button;
					break;
			}

			placed.Name = "Manual Layout Placed " + WidgetTypes[index];
			this.Canvas.SetPlaced(placed);
			this.PlaceFromSliders();
			this.RecolorPlaced();
		}

		private void PlaceFromSliders()
		{
			this.Canvas.SetRect(this.XSlider.Slider.Value, this.YSlider.Slider.Value, this.WidthSlider.Slider.Value, this.HeightSlider.Slider.Value);
		}

		private void RecolorPlaced()
		{
			DemoPalette palette = this.kit.DemoTheme.Palette;
			switch (this.Canvas.Placed)
			{
				case CodeEditor editor:
					editor.BackgroundColor = palette.WidgetBackground;
					editor.BorderColor = palette.Separator;
					editor.TextColor = palette.TextColor;
					editor.CaretColor = palette.TextColor;
					editor.SelectionColor = this.kit.Theme.PrimaryAccentColor.WithAlpha(90);
					editor.ScrollbarColor = palette.TextColor.WithAlpha(70);
					editor.ScrollbarDragColor = palette.TextColor.WithAlpha(130);
					break;

				case TextWidget label:
					label.TextColor = palette.TextColor;
					break;
			}
		}

		/// <summary>One labelled row of two 0..400 sliders sharing its width, agg-gui's slider_pair_row.</summary>
		private ValueSlider AddSliderRow(string label, string firstName, string secondName, out ValueSlider second)
		{
			double s = DeviceScale;
			var row = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit, Margin = new BorderDouble(bottom: Gap) };
			TextWidget caption = this.kit.Label(label);
			caption.AutoExpandBoundsToText = false;
			caption.HAnchor = HAnchor.Absolute;
			caption.VAnchor = VAnchor.Center;
			caption.Width = 110 * s;
			row.AddChild(caption);

			var first = new ValueSlider(this.kit, firstName) { Margin = new BorderDouble(left: Gap) };
			second = new ValueSlider(this.kit, secondName) { Margin = new BorderDouble(left: Gap) };
			row.AddChild(first);
			row.AddChild(second);
			this.AddChild(row);
			return first;
		}

		/// <summary>
		/// A 0..400 whole-number slider that fills the width it is given, its value in a label after the track.
		/// A Slider's bounds derive from its track length, so this sizes the track from its own width.
		/// </summary>
		public sealed class ValueSlider : FlowLayoutWidget
		{
			private const double ValueGap = 4;

			private readonly TextWidget valueText;

			internal ValueSlider(MiscDemoKit kit, string name)
			{
				double s = DeviceScale;
				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Absolute;
				this.Height = 28 * s;
				this.Slider = new Slider(Vector2.Zero, 100 * s, 0, 400)
				{
					Name = name,
					Step = 1,
					Integer = true,
					KeyboardStepping = true,
					VAnchor = VAnchor.Center,
				};
				this.AddChild(this.Slider);

				this.valueText = kit.Label("0");
				this.valueText.AutoExpandBoundsToText = false;
				this.valueText.HAnchor = HAnchor.Absolute;
				this.valueText.VAnchor = VAnchor.Center;
				this.valueText.Width = 28 * s;
				this.valueText.Margin = new BorderDouble(left: ValueGap);
				this.AddChild(this.valueText);

				this.Slider.ValueChanged += (sender, e) => this.valueText.Text = this.Slider.Value.ToString("0");
			}

			public Slider Slider { get; }

			public string ValueText => this.valueText.Text;

			public override void OnBoundsChanged(EventArgs e)
			{
				// Setting the anchors in the constructor raises this before the value label exists.
				if (this.valueText != null)
				{
					double track = this.Width - this.valueText.Width - (ValueGap * DeviceScale);
					if (track > 20 && Math.Abs(this.Slider.TotalWidthInPixels - track) > 0.5)
					{
						this.Slider.TotalWidthInPixels = track;

						// The track length does not dirty this row's layout, so the label would stay where the old
						// track ended.
						this.PerformLayout();
					}
				}

				base.OnBoundsChanged(e);
			}
		}
	}
}
