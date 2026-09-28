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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Sliders" window: one demo slider reconfigured through every Slider option, a port of agg-gui's
	/// demo-ui/src/windows/sliders_demo.rs (itself egui's sliders demo). The demo slider and the two range
	/// sliders are rebuilt when an option changes, as agg-gui's Rebuilders do; the value, min and max live here
	/// so they survive the rebuild.
	/// </summary>
	public class SlidersWindow : ScrollableWidget
	{
		/// <summary>agg-gui's slider track, value strip and vertical track length.</summary>
		private const double TrackLength = 220;

		private const double ValueWidth = 60;

		private const double VerticalLength = 140;

		private static readonly string[] ClampingNames = { "Never", "Edits", "Always" };

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;

		// Widgets that copy a colour when built, recoloured on ThemeChanged (the rebuilt ones pick it up anew).
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<WrappedTextWidget> wrappedTexts = new List<WrappedTextWidget>();
		private readonly List<CheckBox> checkBoxes = new List<CheckBox>();
		private readonly List<RadioButton> radios = new List<RadioButton>();
		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		private readonly GuiWidget demoHost;
		private readonly GuiWidget rangeHost;
		private TextWidget demoValueText;

		private bool syncing;

		public SlidersWindow(DemoTheme demoTheme)
			: base(autoScroll: true)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(16),
			};
			this.AddChild(column);

			// agg-gui's wording: its value strip is not editable, so the keyboard is the arrows.
			column.AddChild(this.Wrapped("Focus a slider and use the arrow keys to nudge its value."));

			this.demoHost = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
			column.AddChild(this.demoHost);
			column.AddChild(this.Separator());

			this.rangeHost = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
			column.AddChild(this.rangeHost);
			column.AddChild(this.Separator());

			this.TrailingFillCheckBox = this.Check("Toggle trailing color", "Sliders Trailing Fill", column, v => this.TrailingFill = v);
			column.AddChild(this.Wrapped("When enabled, trailing color will be painted up until the handle."));
			column.AddChild(this.Separator());

			column.AddChild(this.Label("Handle shape:"));
			this.HandleShapeRadios = this.Radios(column, "Sliders Handle", new[] { "Circle", "Rectangle" }, i => this.HandleShape = (SliderHandleShape)i);
			column.AddChild(this.Separator());

			this.UseStepsCheckBox = this.Check("Use steps", "Sliders Use Steps", column, v =>
			{
				this.UseSteps = v;
				this.StepValue.Visible = v;
			});
			column.AddChild(this.Wrapped("When enabled, the minimal value change would be restricted to a given step."));
			this.StepValue = new DragValue(this.Step, 0, 1000000, this.theme)
			{
				Name = "Sliders Step",
				Speed = 1,
				Decimals = 2,
				HAnchor = HAnchor.Left,
				Visible = false,
			};
			this.StepValue.ValueChanged += (s, e) =>
			{
				if (this.syncing)
				{
					return;
				}

				this.Step = this.StepValue.Value;
				this.Rebuild();
			};
			column.AddChild(this.StepValue);
			column.AddChild(this.Separator());

			// egui's order: i32 first.
			column.AddChild(this.Label("Slider type:"));
			this.TypeRadios = this.Radios(column, "Sliders Type", new[] { "i32", "f64" }, i => this.Integer = i == 0);
			column.AddChild(this.Label("Slider orientation:"));
			this.OrientationRadios = this.Radios(column, "Sliders Orientation", new[] { "Horizontal", "Vertical" }, i => this.Vertical = i == 1);
			column.AddChild(new GuiWidget { Height = 8 * DeviceScale });

			this.LogarithmicCheckBox = this.Check("Logarithmic", "Sliders Logarithmic", column, v => this.Logarithmic = v);
			column.AddChild(this.Wrapped("Logarithmic sliders are great for when you want to span a huge range, i.e. from zero to a million."));
			column.AddChild(this.Wrapped("Logarithmic sliders can include infinity and zero."));
			column.AddChild(new GuiWidget { Height = 8 * DeviceScale });

			column.AddChild(this.Label("Clamping:"));
			this.ClampingRadios = this.Radios(column, "Sliders Clamping", ClampingNames, i => this.Clamping = (SliderClamping)i);
			column.AddChild(this.Wrapped("If true, the slider will clamp incoming and outgoing values to the given range."));
			column.AddChild(this.Wrapped("If false, the slider can show values outside its range, and you cannot enter new values outside the range."));
			column.AddChild(new GuiWidget { Height = 8 * DeviceScale });

			this.SmartAimCheckBox = this.Check("Smart Aim", "Sliders Smart Aim", column, v => this.SmartAim = v);
			column.AddChild(this.Wrapped("Smart Aim will guide you towards round values when you drag the slider so you you are more likely to hit 250 than 247.23"));
			column.AddChild(new GuiWidget { Height = 8 * DeviceScale });

			var reset = new ThemedTextButton("Reset", this.theme)
			{
				Name = "Sliders Reset",
				HAnchor = HAnchor.Left,
				Margin = 0,
			};
			reset.Click += (s, e) => this.Reset();
			column.AddChild(reset);

			this.Reset();
			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		// sliders_demo.rs's SliderConfig, with egui's defaults (set by Reset).
		public double Value { get; private set; }

		public double Minimum { get; private set; }

		public double Maximum { get; private set; }

		public bool Logarithmic { get; private set; }

		public SliderClamping Clamping { get; private set; }

		public bool SmartAim { get; private set; }

		public double Step { get; private set; }

		public bool UseSteps { get; private set; }

		public bool Integer { get; private set; }

		public bool Vertical { get; private set; }

		public bool TrailingFill { get; private set; }

		public SliderHandleShape HandleShape { get; private set; }

		/// <summary>The live demo slider. Replaced whenever an option changes.</summary>
		public Slider DemoSlider { get; private set; }

		public Slider MinimumSlider { get; private set; }

		public Slider MaximumSlider { get; private set; }

		public CheckBox TrailingFillCheckBox { get; }

		public CheckBox UseStepsCheckBox { get; }

		public CheckBox LogarithmicCheckBox { get; }

		public CheckBox SmartAimCheckBox { get; }

		public DragValue StepValue { get; }

		public IReadOnlyList<RadioButton> HandleShapeRadios { get; }

		public IReadOnlyList<RadioButton> TypeRadios { get; }

		public IReadOnlyList<RadioButton> OrientationRadios { get; }

		public IReadOnlyList<RadioButton> ClampingRadios { get; }

		/// <summary>The demo slider's value as its strip shows it.</summary>
		public string DemoValueText => this.demoValueText.Text;

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>Assign PI's button: the value becomes pi (through the slider's rules).</summary>
		public void AssignPi() => this.DemoSlider.Value = Math.PI;

		/// <summary>Back to egui's defaults: 10 in a logarithmic 0..10000, always clamped, smart aim on.</summary>
		public void Reset()
		{
			this.Value = 10;
			this.Minimum = 0;
			this.Maximum = 10000;
			this.Logarithmic = true;
			this.Clamping = SliderClamping.Always;
			this.SmartAim = true;
			this.Step = 10;
			this.UseSteps = false;
			this.Integer = false;
			this.Vertical = false;
			this.TrailingFill = false;
			this.HandleShape = SliderHandleShape.Circle;

			// Show the defaults in the controls without their change events rebuilding once per control.
			this.syncing = true;
			try
			{
				this.TrailingFillCheckBox.Checked = this.TrailingFill;
				this.UseStepsCheckBox.Checked = this.UseSteps;
				this.StepValue.Visible = this.UseSteps;
				this.StepValue.Value = this.Step;
				this.LogarithmicCheckBox.Checked = this.Logarithmic;
				this.SmartAimCheckBox.Checked = this.SmartAim;
				this.HandleShapeRadios[(int)this.HandleShape].Checked = true;
				this.TypeRadios[this.Integer ? 0 : 1].Checked = true;
				this.OrientationRadios[this.Vertical ? 1 : 0].Checked = true;
				this.ClampingRadios[(int)this.Clamping].Checked = true;
			}
			finally
			{
				this.syncing = false;
			}

			this.Rebuild();
		}

		/// <summary>The value type's extremes, as egui's demo picks them.</summary>
		private (double, double) TypeRange()
		{
			if (this.Integer)
			{
				return (int.MinValue, int.MaxValue);
			}

			// Linear sliders make little sense with huge numbers.
			return this.Logarithmic ? (double.NegativeInfinity, double.PositiveInfinity) : (-1e5, 1e5);
		}

		/// <summary>Rebuilds the demo slider and the range sliders from the current options.</summary>
		private void Rebuild()
		{
			(double typeMin, double typeMax) = this.TypeRange();
			this.Minimum = SliderMath.ClampToRange(this.Minimum, typeMin, typeMax);
			this.Maximum = SliderMath.ClampToRange(this.Maximum, typeMin, typeMax);

			this.rangeHost.CloseChildren();
			this.rangeHost.AddChild(this.Label("Slider range:"));
			this.MinimumSlider = this.RangeSlider("Sliders Range Min", this.Minimum, v => this.Minimum = v);
			this.MaximumSlider = this.RangeSlider("Sliders Range Max", this.Maximum, v => this.Maximum = v);

			this.RebuildDemoSlider();
		}

		/// <summary>The demo slider alone - a range edit rebuilds this but not the range slider being dragged.</summary>
		private void RebuildDemoSlider()
		{
			this.demoHost.CloseChildren();
			this.demoHost.AddChild(this.Label(this.Integer ? "i32 demo slider" : "f64 demo slider"));

			this.DemoSlider = this.CreateSlider(this.Vertical, this.Minimum, this.Maximum);
			this.DemoSlider.Name = "Sliders Demo Slider";
			this.DemoSlider.Logarithmic = this.Logarithmic;
			this.DemoSlider.Clamping = this.Clamping;
			this.DemoSlider.SmartAim = this.SmartAim;
			this.DemoSlider.Step = this.UseSteps ? this.Step : 0;
			this.DemoSlider.Integer = this.Integer;
			this.DemoSlider.Value = this.Value;

			// Always clamps the kept value too, as egui's slider writes the clamped value back.
			this.Value = this.DemoSlider.Value;

			this.demoValueText = this.Label("");
			this.demoValueText.VAnchor = VAnchor.Center;
			this.DemoSlider.ValueChanged += (s, e) =>
			{
				this.Value = this.DemoSlider.Value;
				this.ShowValue(this.DemoSlider, this.demoValueText);
			};
			this.ShowValue(this.DemoSlider, this.demoValueText);
			this.demoHost.AddChild(this.SliderRow(this.DemoSlider, this.demoValueText));

			if (!this.Integer)
			{
				this.demoHost.AddChild(this.Wrapped("Sliders will intelligently pick how many decimals to show."));
				var assignPi = new ThemedTextButton("Assign PI", this.theme)
				{
					Name = "Sliders Assign PI",
					HAnchor = HAnchor.Left,
					Margin = new BorderDouble(0, 4),
				};
				assignPi.Click += (s, e) => this.AssignPi();
				this.demoHost.AddChild(assignPi);
			}
		}

		/// <summary>One of egui's pair of logarithmic range sliders over the value type's whole range.</summary>
		private Slider RangeSlider(string name, double value, Action<double> setBound)
		{
			(double typeMin, double typeMax) = this.TypeRange();
			Slider slider = this.CreateSlider(false, typeMin, typeMax);
			slider.Name = name;
			slider.Logarithmic = true;
			slider.SmartAim = this.SmartAim;
			slider.Integer = this.Integer;
			slider.Value = value;

			TextWidget valueText = this.Label("");
			valueText.VAnchor = VAnchor.Center;
			this.ShowValue(slider, valueText);
			slider.ValueChanged += (s, e) =>
			{
				setBound(slider.Value);
				this.ShowValue(slider, valueText);
				this.RebuildDemoSlider();
			};
			this.rangeHost.AddChild(this.SliderRow(slider, valueText));
			return slider;
		}

		/// <summary>A slider styled from the window's options (drawn with ThemeConfig.Current), arrows on.</summary>
		private Slider CreateSlider(bool vertical, double min, double max)
		{
			var slider = new Slider(Vector2.Zero, (vertical ? VerticalLength : TrackLength) * DeviceScale, min, max, vertical ? Orientation.Vertical : Orientation.Horizontal)
			{
				KeyboardStepping = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(0, 4),
			};
			slider.View.TrailingFill = this.TrailingFill;
			slider.View.HandleShape = this.HandleShape;
			return slider;
		}

		/// <summary>agg-gui's slider draws its value in a strip right of the track; agg-sharp's is a label beside it.</summary>
		private GuiWidget SliderRow(Slider slider, TextWidget valueText)
		{
			var row = new FlowLayoutWidget { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit };
			row.AddChild(slider);
			valueText.HAnchor = HAnchor.Absolute;
			valueText.Margin = new BorderDouble(left: 6);
			valueText.MinimumSize = new Vector2(ValueWidth * DeviceScale, 0);
			row.AddChild(valueText);
			return row;
		}

		private void ShowValue(Slider slider, TextWidget valueText)
		{
			double value = slider.Value;
			valueText.Text = double.IsInfinity(value)
				? (value > 0 ? "∞" : "-∞")
				: value.ToString("F" + SliderMath.AutoDecimals(value, slider.Step, slider.Integer));
		}

		private CheckBox Check(string text, string name, GuiWidget parent, Action<bool> set)
		{
			var checkBox = new CheckBox(text, this.theme.TextColor, this.theme.DefaultFontSize)
			{
				Name = name,
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 2),
			};
			checkBox.CheckedStateChanged += (s, e) =>
			{
				if (!this.syncing)
				{
					set(checkBox.Checked);
					this.Rebuild();
				}
			};
			this.checkBoxes.Add(checkBox);
			parent.AddChild(checkBox);
			return checkBox;
		}

		/// <summary>agg-gui's RadioGroup: the options stacked, one chosen.</summary>
		private IReadOnlyList<RadioButton> Radios(GuiWidget parent, string namePrefix, string[] options, Action<int> set)
		{
			var group = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Left | HAnchor.Fit, VAnchor = VAnchor.Fit };
			var buttons = new List<RadioButton>();
			for (int i = 0; i < options.Length; i++)
			{
				int index = i;
				var radio = new RadioButton(options[i], this.theme.TextColor, this.theme.DefaultFontSize)
				{
					Name = namePrefix + " " + options[i],
					HAnchor = HAnchor.Left,
				};
				radio.CheckedStateChanged += (s, e) =>
				{
					if (radio.Checked && !this.syncing)
					{
						set(index);
						this.Rebuild();
					}
				};
				group.AddChild(radio);
				buttons.Add(radio);
				this.radios.Add(radio);
			}

			parent.AddChild(group);
			return buttons;
		}

		private TextWidget Label(string text)
		{
			var widget = new TextWidget(text, pointSize: this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 2),
				AutoExpandBoundsToText = true,
			};
			this.texts.Add(widget);
			return widget;
		}

		private WrappedTextWidget Wrapped(string text)
		{
			var widget = new WrappedTextWidget(text, this.theme.DefaultFontSize, textColor: this.theme.TextColor)
			{
				Margin = new BorderDouble(0, 2),
			};
			this.wrappedTexts.Add(widget);
			return widget;
		}

		private GuiWidget Separator()
		{
			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(0, 6),
			};
			this.separators.Add(separator);
			return separator;
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			this.Recolor();
			this.Invalidate();
		}

		/// <summary>Pushes the current theme into the widgets that copied their colours when they were built.</summary>
		private void Recolor()
		{
			this.texts.RemoveAll(t => t.Parent == null);
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = this.theme.TextColor;
			}

			this.wrappedTexts.RemoveAll(t => t.Parent == null);
			foreach (WrappedTextWidget text in this.wrappedTexts)
			{
				text.TextColor = this.theme.TextColor;
			}

			foreach (CheckBox checkBox in this.checkBoxes)
			{
				checkBox.TextColor = this.theme.TextColor;
			}

			foreach (RadioButton radio in this.radios)
			{
				radio.TextColor = this.theme.TextColor;
			}

			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = this.demoTheme.Palette.Separator;
			}
		}
	}
}
