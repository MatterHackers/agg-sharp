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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Code Example" window, a port of agg-gui's demo-ui/src/windows/code_example.rs (itself egui's code
	/// example): each row shows a syntax-coloured snippet beside the live widget it builds. The name and age are
	/// shared by the text field, the drag value, the Increment button and both read-outs, so all stay in sync.
	/// </summary>
	/// <remarks>
	/// The snippets keep agg-gui's fixed dark code palette in both themes; everything around them follows the theme.
	/// </remarks>
	public class CodeExampleWindow : ScrollableWidget
	{
		// agg-gui's snippet palette.
		private static readonly Color Keyword = DemoPalette.Rgb(0.56, 0.74, 0.95);
		private static readonly Color FunctionName = DemoPalette.Rgb(0.86, 0.78, 0.55);
		private static readonly Color StringLiteral = DemoPalette.Rgb(0.82, 0.60, 0.45);
		private static readonly Color Dim = DemoPalette.Rgba(1.0, 1.0, 1.0, 0.38);
		private static readonly Color Foreground = DemoPalette.Rgba(0.88, 0.90, 0.93, 1.0);
		private static readonly Color CodeBackground = DemoPalette.Rgb(0.12, 0.13, 0.15);

		private readonly DemoTheme demoTheme;
		private readonly ThemeConfig theme;
		private readonly List<TextWidget> texts = new List<TextWidget>();
		private readonly List<GuiWidget> separators = new List<GuiWidget>();
		private readonly FlowLayoutWidget column;
		private int age = 42;

		public CodeExampleWindow(DemoTheme demoTheme)
			: base(autoScroll: true)
		{
			this.demoTheme = demoTheme;
			this.theme = demoTheme.Theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			this.column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(12),
			};
			this.AddChild(this.column);

			// egui frames the grid with the surrounding struct / impl source.
			this.Add(CodeBox(
				("pub struct CodeExample {", Keyword),
				("    name: String,", Foreground),
				("    age: u32,", Foreground),
				("}", Foreground),
				("", Foreground),
				("impl CodeExample {", Keyword),
				("    fn ui(&mut self, font: &Arc<Font>) -> Box<dyn Widget> {", FunctionName),
				("        // Saves us from writing `&mut self.name` etc", Dim),
				("        let Self { name, age } = self;", Foreground)));

			this.Add(Row(CodeBox(("Label::new(\"Example\", font)", Foreground)), this.Text("Example", 18)));
			this.Add(this.Separator());

			var nameRow = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
			TextWidget nameLabel = this.Text("Name", 13);
			nameLabel.HAnchor = HAnchor.Absolute;
			nameLabel.VAnchor = VAnchor.Center;
			nameRow.AddChild(nameLabel);
			this.NameField = new ThemedTextEditWidget("Arthur", this.theme, pixelWidth: 80 * GuiWidget.DeviceScale)
			{
				Name = "Code Example Name",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 8),
			};
			this.NameField.ActualTextEditWidget.TextChanged += (s, e) => this.RefreshReadouts();
			nameRow.AddChild(this.NameField);
			this.Add(Row(
				CodeBox(
					("FlexRow::new()", Foreground),
					("  .add(Label::new(", Foreground),
					("    \"Name\", font))", Dim),
					("  .add(TextField::new(font))", FunctionName)),
				nameRow));
			this.Add(this.Separator());

			this.AgeValue = new DragValue(this.age, 0, 120, this.theme)
			{
				Name = "Code Example Age",
				Decimals = 0,
				Suffix = " years",
				VAnchor = VAnchor.Center,
			};
			this.AgeValue.ValueChanged += (s, e) => this.SetAge((int)this.AgeValue.Value);
			this.Add(Row(
				CodeBox(
					("DragValue::new(", FunctionName),
					("    age, 0.0, 120.0, font)", Foreground),
					("    .with_suffix(\" years\")", FunctionName)),
				this.AgeValue));
			this.Add(this.Separator());

			this.IncrementButton = new ThemedTextButton("Increment", this.theme)
			{
				Name = "Code Example Increment",
				VAnchor = VAnchor.Center,
			};
			this.IncrementButton.Click += (s, e) => this.SetAge(this.age + 1);
			this.Add(Row(
				CodeBox(
					("if Button::new(", FunctionName),
					("    \"Increment\", font)", StringLiteral),
					("    .on_click(|| *age += 1)", Dim)),
				this.IncrementButton));
			this.Add(this.Separator());

			this.AgeReadout = this.Text("", 13);
			this.AgeReadout.Name = "Code Example Age Readout";
			this.Add(Row(CodeBox(("ui.label(format!(\"{name} is {age}\"));", Foreground)), this.AgeReadout));

			// The closing braces of fn ui and impl, after the grid.
			this.Add(CodeBox(("    }", Foreground), ("}", Foreground)));

			this.Add(new GuiWidget { HAnchor = HAnchor.Stretch, Height = 8 * DeviceScale });
			this.Add(this.Separator());
			this.ThemeHeader = new CollapsingHeader("Theme", this.theme, expanded: false) { Name = "Code Example Theme" };
			var themeNote = new WrappedTextWidget("Syntax highlighting is represented by the fixed dark code palette in this agg-gui demo.", pointSize: 12)
			{
				HAnchor = HAnchor.Stretch,
			};
			this.ThemeHeader.Body.AddChild(themeNote);
			this.Add(this.ThemeHeader);

			this.DebugReadout = this.Text("", 11);
			this.DebugReadout.Name = "Code Example Debug Readout";
			this.Add(this.DebugReadout);

			this.RefreshReadouts();
			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public ThemedTextEditWidget NameField { get; }

		public DragValue AgeValue { get; }

		public ThemedTextButton IncrementButton { get; }

		/// <summary>agg-gui's AgeDisplay: "{name} is {age}".</summary>
		public TextWidget AgeReadout { get; }

		/// <summary>agg-gui's debug AgeDisplay: the struct as Rust's Debug prints it.</summary>
		public TextWidget DebugReadout { get; }

		public CollapsingHeader ThemeHeader { get; }

		public int Age => this.age;

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>One syntax-coloured line of a snippet, in agg-gui's 11.5 point code size.</summary>
		private static TextWidget CodeLine(string text, Color color)
		{
			return new TextWidget(text, pointSize: 11.5, textColor: color)
			{
				HAnchor = HAnchor.Left,
				AutoExpandBoundsToText = true,
			};
		}

		/// <summary>Snippet lines on the dark code background, 4 units in from their neighbours.</summary>
		private static GuiWidget CodeBox(params (string Text, Color Color)[] lines)
		{
			var box = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				BackgroundColor = CodeBackground,
				Margin = new BorderDouble(4),
			};
			foreach ((string text, Color color) in lines)
			{
				box.AddChild(CodeLine(text, color));
			}

			return box;
		}

		/// <summary>One row: the snippet in a fixed 210 unit column, a 12 unit gap, then the live widget.</summary>
		private static GuiWidget Row(GuiWidget code, GuiWidget output)
		{
			var row = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
			var codeColumn = new GuiWidget { Width = 210 * DeviceScale, HAnchor = HAnchor.Absolute, VAnchor = VAnchor.Fit };
			codeColumn.AddChild(code);
			row.AddChild(codeColumn);
			output.Margin = new BorderDouble(left: 12);
			output.VAnchor = VAnchor.Center;

			// A left-to-right flow takes only fixed or stretching children.
			if (output.HAnchor != HAnchor.Stretch)
			{
				output.HAnchor = HAnchor.Absolute;
			}

			row.AddChild(output);
			return row;
		}

		private void SetAge(int value)
		{
			this.age = Math.Clamp(value, 0, 120);
			if ((int)this.AgeValue.Value != this.age)
			{
				this.AgeValue.Value = this.age;
			}

			this.RefreshReadouts();
		}

		private void RefreshReadouts()
		{
			if (this.AgeReadout == null || this.DebugReadout == null)
			{
				return;
			}

			string name = this.NameField.Text;
			this.AgeReadout.Text = $"{name} is {this.age}";
			this.DebugReadout.Text = $"CodeExample {{ name: \"{name}\", age: {this.age} }}";
		}

		/// <summary>The column's children are 10 units apart, agg-gui's gap.</summary>
		private void Add(GuiWidget child)
		{
			if (this.column.Children.Count > 0)
			{
				child.Margin = child.Margin + new BorderDouble(top: 10);
			}

			this.column.AddChild(child);
		}

		private TextWidget Text(string text, double pointSize)
		{
			var widget = new TextWidget(text, pointSize: pointSize, textColor: this.theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				AutoExpandBoundsToText = true,
			};
			this.texts.Add(widget);
			return widget;
		}

		private GuiWidget Separator()
		{
			var separator = new GuiWidget { HAnchor = HAnchor.Stretch, Height = Math.Max(1, Math.Round(DeviceScale)) };
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
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			foreach (TextWidget text in this.texts)
			{
				text.TextColor = palette.TextColor;
			}

			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = palette.Separator;
			}

			if (this.ThemeHeader.Body.Children[0] is WrappedTextWidget note)
			{
				note.TextColor = palette.TextColor;
			}

			this.NameField.ActualTextEditWidget.TextColor = palette.TextColor;
			this.NameField.BackgroundColor = palette.WidgetBackground;
			this.IncrementButton.BackgroundColor = this.theme.ButtonBackgroundColor;
			this.IncrementButton.TextColor = palette.TextColor;
			this.IncrementButton.HoverColor = this.theme.SlightShade;
			this.IncrementButton.MouseDownColor = this.theme.MinimalShade;
		}
	}
}
