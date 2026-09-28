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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// agg-gui's "Text Layout" window (text_demos/text_layout.rs, egui's LayoutJob playground): rows of controls for
	/// the row limit, line breaking, overflow marker, letter spacing, line height, alignment, justification and
	/// text, over a <see cref="TextLayoutPreview"/> that lays the text out live. Controls are named
	/// "Text Layout &lt;setting&gt;"; a choice's buttons add " &lt;index&gt;".
	/// </summary>
	public class TextLayoutWindow : GuiWidget
	{
		/// <summary>agg-gui's fixed label column.</summary>
		private const double LabelWidthUnits = 130;

		private readonly DemoTheme demoTheme;
		private readonly MiscDemoKit kit;
		private readonly List<GuiWidget> separators = new List<GuiWidget>();
		private readonly List<ThemedRadioTextButton> choiceButtons = new List<ThemedRadioTextButton>();
		private readonly TextWidget title;

		public TextLayoutWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			this.ScrollArea = new ScrollableWidget(autoScroll: true)
			{
				Name = "Text Layout Scroll",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.ScrollArea.ScrollArea.HAnchor = HAnchor.Stretch;
			this.AddChild(this.ScrollArea);

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = new BorderDouble(14),
			};
			this.ScrollArea.AddChild(column);
			void Add(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(widget.Margin.Left, 8, widget.Margin.Right, 0);
				column.AddChild(widget);
			}

			// agg-gui's heading is a fixed blue, not the accent.
			this.title = this.kit.Label("Text layout", 18, DemoPalette.Rgb(.22, .45, .88));
			Add(this.title);
			Add(this.kit.Label("A live rich text layout playground.", 12));
			Add(this.Separator());

			TextLayoutSettings settings = this.Settings;
			Add(this.Row("Max rows:", this.Drag("Text Layout Max Rows", settings.MaxRows, 0, 1000, 0, 1, v => settings.MaxRows = (int)Math.Round(Math.Max(0, v)))));
			Add(this.Row("Line-break:", this.Choice("Text Layout Line Break", new[] { "word boundaries", "anywhere" }, 0, i => settings.BreakAnywhere = i == 1)));
			Add(this.Row("Overflow character:", this.Choice("Text Layout Overflow", new[] { "None", "…", "—", "  -  " }, settings.Overflow, i => settings.Overflow = i)));
			Add(this.Row("Extra letter spacing:", this.Drag("Text Layout Letter Spacing", 0, -5, 20, 1, .1, v => settings.ExtraLetterSpacing = v)));

			FlowLayoutWidget lineHeight = this.kit.Row(0);
			lineHeight.HAnchor = HAnchor.Stretch;
			lineHeight.Margin = new BorderDouble(0);
			CheckBox custom = this.kit.CheckBox("Text Layout Custom Line Height", "Custom", false, 12);
			custom.Margin = new BorderDouble(right: 10);
			custom.CheckedStateChanged += (s, e) => this.Change(() => settings.CustomLineHeight = custom.Checked);
			lineHeight.AddChild(custom);
			GuiWidget lineHeightDrag = this.Drag("Text Layout Line Height", settings.LineHeight, 8, 64, 0, 1, v => settings.LineHeight = Math.Max(8, Math.Round(v)));
			lineHeightDrag.HAnchor = HAnchor.Stretch;
			lineHeight.AddChild(lineHeightDrag);
			Add(this.Row("Line height:", lineHeight));

			Add(this.Row("Horizontal align:", this.Choice("Text Layout Align", new[] { "Left", "Center", "Right" }, 0, i => settings.Align = i == 1 ? Justification.Center : i == 2 ? Justification.Right : Justification.Left)));
			CheckBox justify = this.kit.CheckBox("Text Layout Justify", "Fill row width", false, 12);
			justify.CheckedStateChanged += (s, e) => this.Change(() => settings.Justify = justify.Checked);
			Add(this.Row("Justify:", justify));
			Add(this.Row("Text:", this.Choice("Text Layout Text", new[] { "Lorem Ipsum", "La Pasionaria" }, 0, i => settings.PasionariaText = i == 1)));

			Add(this.Separator());
			this.Preview = new TextLayoutPreview(settings, demoTheme) { Name = "Text Layout Preview" };
			Add(this.Preview);

			this.Recolor();
			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public TextLayoutSettings Settings { get; } = new TextLayoutSettings();

		public ScrollableWidget ScrollArea { get; }

		public TextLayoutPreview Preview { get; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		/// <summary>Applies a setting change and lays the preview out again.</summary>
		private void Change(Action apply)
		{
			apply();
			this.Preview?.Relayout();
		}

		/// <summary>agg-gui's text_layout_control_row: a label in a fixed column and the control filling the rest.</summary>
		private GuiWidget Row(string label, GuiWidget control)
		{
			FlowLayoutWidget row = this.kit.Row(0);
			row.HAnchor = HAnchor.Stretch;
			row.Margin = new BorderDouble(0);
			TextWidget text = this.kit.Label(label, 12);
			text.AutoExpandBoundsToText = false;
			text.Width = LabelWidthUnits * DeviceScale;
			text.Margin = new BorderDouble(right: 10);
			row.AddChild(text);
			control.HAnchor = HAnchor.Stretch;
			row.AddChild(control);
			return row;
		}

		private GuiWidget Drag(string name, double value, double minimum, double maximum, int decimals, double speed, Action<double> set)
		{
			var drag = new DragValue(value, minimum, maximum, this.kit.Theme) { Name = name, Decimals = decimals, Speed = speed };
			drag.ValueChanged += (s, e) => this.Change(() => set(drag.Value));
			return drag;
		}

		/// <summary>agg-gui's SelectionButtons: equal-width toggles with a gap, the chosen one on the accent.</summary>
		private GuiWidget Choice(string name, string[] labels, int selected, Action<int> set)
		{
			var buttons = new List<ThemedRadioTextButton>();
			for (int i = 0; i < labels.Length; i++)
			{
				int index = i;
				var button = new ThemedRadioTextButton(labels[i], this.kit.Theme, this.kit.FontSize(12))
				{
					Name = $"{name} {i}",
					HAnchor = HAnchor.Absolute,
					DrawUnderline = false,
				};
				button.CheckedStateChanged += (s, e) =>
				{
					// The chosen button's text flips to contrast with the accent.
					this.RecolorChoice(button);
					if (button.Checked)
					{
						this.Change(() => set(index));
					}
				};
				buttons.Add(button);
				this.choiceButtons.Add(button);
			}

			var row = new EqualWidthRow(buttons) { Name = name };
			buttons[selected].Checked = true;
			return row;
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
			this.kit.Recolor();
			this.BackgroundColor = this.demoTheme.Palette.PanelFill;
			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = this.demoTheme.Palette.Separator;
			}

			foreach (ThemedRadioTextButton button in this.choiceButtons)
			{
				this.RecolorChoice(button);
			}
		}

		/// <summary>with_subtle: idle choices are flat, the chosen one sits on the accent.</summary>
		private void RecolorChoice(ThemedRadioTextButton button)
		{
			ThemeConfig theme = this.kit.Theme;
			button.SelectedBackgroundColor = theme.PrimaryAccentColor;
			button.UnselectedBackgroundColor = Color.Transparent;
			button.HoverColor = theme.SlightShade;
			button.MouseDownColor = theme.MinimalShade;
			button.BackgroundColor = button.Checked ? button.SelectedBackgroundColor : button.UnselectedBackgroundColor;
			button.TextColor = button.Checked ? theme.TextColor.WithContrast(theme.PrimaryAccentColor, 3).ToColor() : theme.TextColor;
		}

		/// <summary>Lays its buttons out side by side at equal widths with agg-gui's row gap between them.</summary>
		private sealed class EqualWidthRow : GuiWidget
		{
			private const double GapUnits = 6;

			private readonly List<ThemedRadioTextButton> buttons;

			public EqualWidthRow(List<ThemedRadioTextButton> buttons)
			{
				this.buttons = buttons;
				this.VAnchor = VAnchor.Absolute;
				double height = 0;
				foreach (ThemedRadioTextButton button in buttons)
				{
					button.SiblingRadioButtonList = new List<GuiWidget>(buttons);
					height = Math.Max(height, button.Height);
					this.AddChild(button);
				}

				this.Height = height;
				this.MinimumSize = new Vector2(0, height);
			}

			public override void OnBoundsChanged(EventArgs e)
			{
				base.OnBoundsChanged(e);
				double gap = GapUnits * DeviceScale;
				double cell = Math.Max(0, (this.Width - gap * (this.buttons.Count - 1)) / this.buttons.Count);
				for (int i = 0; i < this.buttons.Count; i++)
				{
					this.buttons[i].Position = new Vector2(i * (cell + gap), 0);
					this.buttons[i].Width = cell;
				}
			}
		}
	}
}
