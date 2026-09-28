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
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools
{
	/// <summary>
	/// The System window's "Font" tab, a port of agg-gui's build_font_tab (demo-ui/src/windows/system.rs): the
	/// process-wide text settings, each control writing straight through to the agg-sharp static the render path
	/// reads - <see cref="AggContext.DefaultFont"/>, <see cref="LcdRenderSettings"/>,
	/// <see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/> and <see cref="TextStyleSettings"/>. Controls open on the
	/// current values, so building the tab never changes a setting. Nothing here is persisted.
	/// </summary>
	public class SystemFontTab : ScrollableWidget
	{
		/// <summary>The faces agg-sharp embeds; agg-gui's catalogue of shipped fonts is not bundled.</summary>
		public static readonly IReadOnlyList<(string Name, Func<TypeFace> Face)> FontOptions = new (string, Func<TypeFace>)[]
		{
			("Liberation Sans", () => LiberationSansFont.Instance),
			("Liberation Sans Bold", () => LiberationSansBoldFont.Instance),
		};

		/// <summary>agg-gui's BASE_POINT_SIZE: the body-text size the point size field shows at a size scale of 1.</summary>
		public const double BasePointSize = 14;

		private readonly MiscDemoKit kit;
		private readonly List<GuiWidget> separators = new List<GuiWidget>();

		/// <summary>The accent the toggle switches were built in; a switch copies its colours, so a new accent rebuilds them.</summary>
		private Color toggleAccent;

		private readonly Dictionary<CheckBox, Action<bool>> toggleWrites = new Dictionary<CheckBox, Action<bool>>();

		internal SystemFontTab(MiscDemoKit kit)
			: base(autoScroll: true)
		{
			this.kit = kit;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			FlowLayoutWidget column = kit.Column();
			column.Margin = new BorderDouble(14);
			this.AddChild(column);

			column.AddChild(kit.Wrapped("Process-wide text rendering settings.  Changes apply on the next frame.", 13));
			column.AddChild(this.Separator());

			column.AddChild(kit.Label("Font", 16));
			column.AddChild(kit.Wrapped("Sets the system font for every widget built after the change.", 13));
			this.Font = new DropDownList("Other", kit.Theme.TextColor, pointSize: kit.FontSize(14))
			{
				Name = "System Font",
				HAnchor = HAnchor.Left | HAnchor.Fit,
				Margin = new BorderDouble(0, 4),
			};
			foreach ((string name, _) in FontOptions)
			{
				this.Font.AddItem(name);
			}

			this.Font.SelectedIndex = CurrentFontIndex();
			this.Font.SelectionChanged += (s, e) =>
			{
				if (this.Font.SelectedIndex >= 0)
				{
					this.Apply(() => AggContext.DefaultFont = FontOptions[this.Font.SelectedIndex].Face());
				}
			};
			column.AddChild(this.Font);
			column.AddChild(this.Separator());

			// Displayed as the body-text point size, stored as the size scale every text widget's own size is
			// multiplied by, as agg-gui does. Applied when the edit completes (Enter or leaving the field).
			column.AddChild(kit.Label("Point size", 16));
			column.AddChild(kit.Wrapped("Body-text size in points.  Scales every label proportionally.  Range 7–42 pt.", 13));
			this.PointSize = new ThemedNumberEdit(
				TextStyleSettings.SizeScale * BasePointSize,
				kit.Theme,
				pixelWidth: 60 * GuiWidget.DeviceScale,
				allowDecimals: true,
				minValue: 7,
				maxValue: 42,
				increment: .5)
			{
				Name = "System Point Size",
				Margin = new BorderDouble(0, 4),
			};
			this.PointSize.ActuallNumberEdit.EditComplete += (s, e) =>
			{
				this.Apply(() => TextStyleSettings.SizeScale = this.PointSize.Value / BasePointSize);
			};
			column.AddChild(this.PointSize);
			column.AddChild(this.Separator());

			column.AddChild(kit.Label("LCD subpixel text", 16));
			column.AddChild(kit.Wrapped("Renders text using per-channel R/G/B coverage for sharper edges on LCD displays.", 13));
			this.Lcd = this.ToggleRow(column, "System LCD", "Enable LCD subpixel rendering", LcdRenderSettings.Enabled, on => LcdRenderSettings.Enabled = on);
			column.AddChild(this.Separator());

			column.AddChild(kit.Label("Hinting", 16));
			column.AddChild(kit.Wrapped(
				"Snaps glyph baselines to whole pixels for crisper text at small sizes.  Required if you want LCD and grayscale "
				+ "renderers to land on the same vertical position.", 13));
			this.Hinting = this.ToggleRow(
				column, "System Hinting", "Snap baselines to whole pixels", TypeFacePrinter.SnapBaselinesToWholePixels, on => TypeFacePrinter.SnapBaselinesToWholePixels = on);
			column.AddChild(this.Separator());

			// Gamma and Primary Weight shape the LCD filter, so they change nothing while LCD text is off.
			column.AddChild(kit.Label("Typography style", 16));
			column.AddChild(kit.Wrapped("Process-wide style overrides applied to every glyph at paint time.  Defaults are pass-through.", 13));
			this.Gamma = this.StyleRow(column, "Gamma", "System Gamma", LcdRenderSettings.Gamma, .5, 2.5, .01, v => LcdRenderSettings.Gamma = v);
			this.GlyphWidth = this.StyleRow(column, "Width", "System Width", TextStyleSettings.Width, .75, 1.25, .01, v => TextStyleSettings.Width = v);
			this.Interval = this.StyleRow(column, "Interval", "System Interval", TextStyleSettings.Interval, -.2, .2, .001, v => TextStyleSettings.Interval = v);
			this.FauxWeight = this.StyleRow(
				column, "Faux Weight", "System Faux Weight", TextStyleSettings.FauxWeight, -1, 1, .01, v => TextStyleSettings.FauxWeight = v);
			this.FauxItalic = this.StyleRow(
				column, "Faux Italic", "System Faux Italic", TextStyleSettings.FauxItalic, -1, 1, .01, v => TextStyleSettings.FauxItalic = v);
			this.PrimaryWeight = this.StyleRow(
				column, "Primary Weight", "System Primary Weight", LcdRenderSettings.PrimaryWeight, 0, 1, .01, v => LcdRenderSettings.PrimaryWeight = v);

			this.Recolor();
		}

		/// <summary>Raised after a control changed a process-wide setting, so the window can repaint its preview.</summary>
		public event EventHandler SettingChanged;

		public DropDownList Font { get; }

		/// <summary>The body-text point size; see <see cref="BasePointSize"/>.</summary>
		public ThemedNumberEdit PointSize { get; }

		/// <summary>A toggle switch; replaced by a new one when the accent colour changes.</summary>
		public CheckBox Lcd { get; private set; }

		/// <summary>A toggle switch; replaced by a new one when the accent colour changes.</summary>
		public CheckBox Hinting { get; private set; }

		public Slider Gamma { get; }

		/// <summary>agg-gui's "Width" slider - named for the glyphs so it does not hide <see cref="GuiWidget.Width"/>.</summary>
		public Slider GlyphWidth { get; }

		public Slider Interval { get; }

		public Slider FauxWeight { get; }

		public Slider FauxItalic { get; }

		public Slider PrimaryWeight { get; }

		/// <summary>The <see cref="FontOptions"/> entry that is the current default font, or -1 if another face was set.</summary>
		public static int CurrentFontIndex()
		{
			for (int i = 0; i < FontOptions.Count; i++)
			{
				if (FontOptions[i].Face() == AggContext.DefaultFont)
				{
					return i;
				}
			}

			return -1;
		}

		/// <summary>Pushes the current theme into the widgets the kit does not recolour.</summary>
		public void Recolor()
		{
			DemoPalette palette = this.kit.DemoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.Font.TextColor = palette.TextColor;
			foreach (GuiWidget separator in this.separators)
			{
				separator.BackgroundColor = palette.Separator;
			}

			// The field reads its fill and border from the theme as it draws, but copies its text colour, and only
			// refreshes that on a focus change.
			InternalTextEditWidget pointSizeText = this.PointSize.ActuallNumberEdit.InternalTextEditWidget;
			ThemeConfig.ThreeStateColor fieldColors = this.kit.Theme.EditFieldColors;
			pointSizeText.TextColor = pointSizeText.Focused ? fieldColors.Focused.TextColor : fieldColors.Inactive.TextColor;

			if (this.toggleAccent != this.kit.Theme.PrimaryAccentColor)
			{
				this.Lcd = this.RebuildToggle(this.Lcd);
				this.Hinting = this.RebuildToggle(this.Hinting);
			}
		}

		private void Apply(Action write)
		{
			write();
			this.SettingChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>agg-gui's toggle-switch row: the switch, then its label.</summary>
		private CheckBox ToggleRow(GuiWidget column, string name, string label, bool isChecked, Action<bool> write)
		{
			FlowLayoutWidget row = this.kit.Row(12);
			CheckBox toggle = this.Toggle(name, isChecked, write);
			row.AddChild(toggle);
			row.AddChild(this.kit.Label(label, 13));
			column.AddChild(row);
			return toggle;
		}

		private CheckBox Toggle(string name, bool isChecked, Action<bool> write)
		{
			DemoPalette palette = this.kit.DemoTheme.Palette;
			this.toggleAccent = this.kit.Theme.PrimaryAccentColor;
			var toggle = new CheckBox(new ToggleSwitchView(
				"",
				"",
				40 * GuiWidget.DeviceScale,
				20 * GuiWidget.DeviceScale,
				palette.WidgetBackground,
				this.toggleAccent,
				Color.White,
				this.kit.Theme.TextColor,
				palette.Separator))
			{
				Name = name,
				Checked = isChecked,
				Margin = new BorderDouble(right: 8),
			};
			toggle.CheckedStateChanged += (s, e) => this.Apply(() => write(toggle.Checked));
			this.toggleWrites[toggle] = write;
			return toggle;
		}

		private CheckBox RebuildToggle(CheckBox old)
		{
			CheckBox replacement = this.Toggle(old.Name, old.Checked, this.toggleWrites[old]);
			this.toggleWrites.Remove(old);
			old.Parent.ReplaceChild(old, replacement);
			return replacement;
		}

		private Slider StyleRow(GuiWidget column, string label, string name, double value, double minimum, double maximum, double step, Action<double> write)
		{
			FlowLayoutWidget row = this.kit.Row(10);
			TextWidget caption = this.kit.Label(label, 13);
			caption.AutoExpandBoundsToText = false;
			caption.Width = 140 * GuiWidget.DeviceScale;
			row.AddChild(caption);
			Slider slider = this.kit.Slider(name, value, minimum, maximum, step);
			TextWidget readout = this.kit.Label(value.ToString("0.00"), 13);
			slider.ValueChanged += (s, e) =>
			{
				readout.Text = slider.Value.ToString("0.00");
				this.Apply(() => write(slider.Value));
			};
			row.AddChild(slider);
			row.AddChild(readout);
			column.AddChild(row);
			return slider;
		}

		private GuiWidget Separator()
		{
			var line = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = GuiWidget.DeviceScale,
				Margin = new BorderDouble(0, 6),
			};
			this.separators.Add(line);
			return line;
		}
	}
}
