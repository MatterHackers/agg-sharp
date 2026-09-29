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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>Which palette the demo shows: agg-gui's ThemePreference.</summary>
	public enum ThemePreference
	{
		Light,
		Dark,

		/// <summary>Follow the operating system (see the systemPrefersDark argument of <see cref="DemoTheme"/>).</summary>
		System,
	}

	/// <summary>agg-gui's accent swatches (theme.rs AccentColor), in its menu order.</summary>
	public enum AccentColor
	{
		Blue,
		Purple,
		Pink,
		Red,
		Orange,
		Yellow,
		Green,
		Teal,
	}

	/// <summary>
	/// The colours of one agg-gui palette (theme.rs Visuals::light / Visuals::dark) that the demo uses.
	/// </summary>
	public sealed class DemoPalette
	{
		/// <summary>agg-gui's Visuals::dark().</summary>
		public static DemoPalette Dark { get; } = new DemoPalette
		{
			IsDark = true,
			BackgroundColor = Rgb(0.10, 0.10, 0.12),
			PanelFill = Rgb(0.13, 0.13, 0.15),
			TopBarBackground = Rgb(0.15, 0.15, 0.17),
			WindowFill = Rgb(0.15, 0.15, 0.18),
			WindowTitleFill = Rgb(0.20, 0.20, 0.24),
			WindowShadow = Rgba(0.0, 0.0, 0.0, 0.35),
			WindowStroke = Rgba(1.0, 1.0, 1.0, 0.08),
			TextColor = Rgb(0.90, 0.90, 0.92),
			TextDim = Rgba(0.90, 0.90, 0.92, 0.50),
			WidgetBackground = Rgb(0.22, 0.22, 0.26),
			WidgetStroke = Rgba(0.60, 0.60, 0.65, 0.60),
			Separator = Rgba(1.0, 1.0, 1.0, 0.10),
		};

		/// <summary>agg-gui's Visuals::light().</summary>
		public static DemoPalette Light { get; } = new DemoPalette
		{
			IsDark = false,
			BackgroundColor = Rgb(0.90, 0.90, 0.92),
			PanelFill = Rgb(0.92, 0.92, 0.95),
			TopBarBackground = Rgb(0.88, 0.88, 0.91),
			WindowFill = Rgb(0.97, 0.97, 0.98),
			WindowTitleFill = Rgb(0.87, 0.87, 0.91),
			WindowShadow = Rgba(0.0, 0.0, 0.0, 0.18),
			WindowStroke = Rgba(0.0, 0.0, 0.0, 0.15),
			TextColor = Rgb(0.08, 0.08, 0.10),
			TextDim = Rgba(0.08, 0.08, 0.10, 0.50),
			WidgetBackground = Rgb(1.00, 1.00, 1.00),
			WidgetStroke = Rgb(0.75, 0.76, 0.78),
			Separator = Rgba(0.0, 0.0, 0.0, 0.12),
		};

		public bool IsDark { get; private set; }

		/// <summary>bg_color: behind everything - the canvas the demo windows float over.</summary>
		public Color BackgroundColor { get; private set; }

		/// <summary>panel_fill: the sidebar.</summary>
		public Color PanelFill { get; private set; }

		/// <summary>top_bar_bg: the menu bar.</summary>
		public Color TopBarBackground { get; private set; }

		/// <summary>window_fill: window bodies, and here the menu popups.</summary>
		public Color WindowFill { get; private set; }

		/// <summary>window_title_fill: a demo window's title bar.</summary>
		public Color WindowTitleFill { get; private set; }

		/// <summary>window_shadow: the shadow under a demo window.</summary>
		public Color WindowShadow { get; private set; }

		public Color WindowStroke { get; private set; }

		public Color TextColor { get; private set; }

		public Color TextDim { get; private set; }

		public Color WidgetBackground { get; private set; }

		/// <summary>widget_stroke: the outline of an idle text field.</summary>
		public Color WidgetStroke { get; private set; }

		public Color Separator { get; private set; }

		/// <summary>agg-gui's colours are 0..1 floats; ColorF rounds them to bytes.</summary>
		public static Color Rgb(double r, double g, double b) => new ColorF(r, g, b).ToColor();

		public static Color Rgba(double r, double g, double b, double a) => new ColorF(r, g, b, a).ToColor();
	}

	/// <summary>
	/// The GUI demo's theme service: a light/dark/system preference and an accent swatch, applied to one shared
	/// <see cref="ThemeConfig"/> the way agg-gui's apply_theme_visuals installs a Visuals.
	/// </summary>
	/// <remarks>
	/// The ThemeConfig is changed in place rather than replaced, so widgets that read it when they draw or open
	/// (a menu builds its popup from it on every open) pick the change up for free; widgets that copied a
	/// colour at construction are recoloured by whoever listens to <see cref="ThemeChanged"/>.
	/// </remarks>
	public class DemoTheme
	{
		private readonly Func<bool> systemPrefersDark;

		/// <param name="preference">agg-gui's demo starts on System.</param>
		/// <param name="accent">agg-gui's default swatch is Blue.</param>
		/// <param name="systemPrefersDark">Whether the operating system is in dark mode, asked each time the
		/// System preference is applied. Null means dark - agg-gui's fallback when it cannot tell.</param>
		public DemoTheme(ThemePreference preference = ThemePreference.System, AccentColor accent = AccentColor.Blue, Func<bool> systemPrefersDark = null)
		{
			this.systemPrefersDark = systemPrefersDark ?? (() => true);
			this.Preference = preference;
			this.Accent = accent;
			this.Apply();

			// Library widgets built without a theme (CheckBox, RadioButton, ...) draw from ThemeConfig.Current;
			// Apply mutates this same object, so they follow every preference and accent change.
			ThemeConfig.Current = this.Theme;
		}

		/// <summary>Raised after the preference or accent changed and <see cref="Theme"/> has been updated.</summary>
		public event EventHandler ThemeChanged;

		/// <summary>The theme config the demo's widgets are built with; updated in place on every change.</summary>
		public ThemeConfig Theme { get; } = ThemeConfig.DefaultTheme();

		public ThemePreference Preference { get; private set; }

		public AccentColor Accent { get; private set; }

		/// <summary>The palette the current preference resolves to.</summary>
		public DemoPalette Palette { get; private set; }

		public bool IsDark => this.Palette.IsDark;

		/// <summary>The swatch colour agg-gui uses for <paramref name="accent"/> (theme.rs AccentColor::color).</summary>
		public static Color ColorOf(AccentColor accent)
		{
			switch (accent)
			{
				case AccentColor.Purple: return DemoPalette.Rgb(0.48, 0.36, 0.86);
				case AccentColor.Pink: return DemoPalette.Rgb(0.78, 0.28, 0.58);
				case AccentColor.Red: return DemoPalette.Rgb(0.82, 0.24, 0.24);
				case AccentColor.Orange: return DemoPalette.Rgb(0.90, 0.46, 0.18);
				case AccentColor.Yellow: return DemoPalette.Rgb(0.82, 0.62, 0.16);
				case AccentColor.Green: return DemoPalette.Rgb(0.20, 0.62, 0.34);
				case AccentColor.Teal: return DemoPalette.Rgb(0.14, 0.62, 0.66);
				default: return DemoPalette.Rgb(0.22, 0.45, 0.88);
			}
		}

		/// <summary>
		/// Colours <paramref name="button"/> as agg-gui's Button: an accent fill with white text, a lighter accent
		/// under the pointer and accent_pressed while held. Windows that recolour their buttons on
		/// <see cref="ThemeChanged"/> call this there; <see cref="AccentButton"/> keeps a button styled by itself.
		/// </summary>
		/// <remarks>
		/// Only buttons are styled, not ThemeConfig.ButtonBackgroundColor: that colour is also every check box's
		/// fill (SelectionControlStyle.WidgetBackground) and the menu bar's, which stay widget_bg in agg-gui.
		/// </remarks>
		public void StyleButton(ThemedTextButton button)
		{
			Color accent = ColorOf(this.Accent);
			button.BackgroundColor = accent;
			button.TextColor = Color.White;
			button.HoverColor = accent.Blend(Color.White, 0.12);
			button.MouseDownColor = AccentPressed(this.Accent);
			button.Invalidate();
		}

		/// <summary>
		/// Gives <paramref name="scroll"/> agg-gui's default scroll bar: floating over the content, hidden until
		/// the pointer is over it or the view scrolls, and a fade at each edge with more content past it. The
		/// values are agg-gui's ScrollBarStyle::floating, as the Scrolling window's Floating preset shows them.
		/// </summary>
		public void StyleScroll(ScrollableWidget scroll)
		{
			double scale = GuiWidget.DeviceScale;
			ScrollBar bar = scroll.VerticalScrollBar;
			bar.Floating = true;
			bar.Show = ScrollBar.ShowState.WhenRequired;
			bar.BarWidth = 10 * scale;
			bar.FloatingWidth = 2 * scale;
			bar.HandleMinLength = 12 * scale;
			bar.InnerMargin = 0;
			this.ColorScroll(scroll);

			ScrollEdgeFade fade = scroll.EdgeFade;
			fade.Strength = 0.5;
			fade.Size = 20 * scale;

			EventHandler recolor = (s, e) => this.ColorScroll(scroll);
			this.ThemeChanged += recolor;
			scroll.Closed += (s, e) => this.ThemeChanged -= recolor;
		}

		private void ColorScroll(ScrollableWidget scroll)
		{
			ScrollBar bar = scroll.VerticalScrollBar;
			bar.TrackColor = Color.Transparent;
			bar.ThumbColor = this.Palette.TextDim.WithAlpha(140);
			bar.ThumbHoverColor = this.Palette.TextDim;
		}

		/// <summary>Styles <paramref name="button"/> with <see cref="StyleButton"/> now and on every theme change
		/// until it is closed, for buttons whose window does not recolour them itself.</summary>
		public ThemedTextButton AccentButton(ThemedTextButton button)
		{
			this.StyleButton(button);
			EventHandler restyle = (s, e) => this.StyleButton(button);
			this.ThemeChanged += restyle;
			button.Closed += (s, e) => this.ThemeChanged -= restyle;
			return button;
		}

		/// <summary>agg-gui's AccentColor::key: the lower case id used in menu ids (and later saved state).</summary>
		public static string KeyOf(AccentColor accent) => accent.ToString().ToLowerInvariant();

		/// <summary>agg-gui's ThemePreference::key.</summary>
		public static string KeyOf(ThemePreference preference) => preference.ToString().ToLowerInvariant();

		public void SetPreference(ThemePreference preference)
		{
			if (preference != this.Preference)
			{
				this.Preference = preference;
				this.ApplyAndNotify();
			}
		}

		public void SetAccent(AccentColor accent)
		{
			if (accent != this.Accent)
			{
				this.Accent = accent;
				this.ApplyAndNotify();
			}
		}

		/// <summary>
		/// Asks the operating system again when the preference is System, and recolours (raising
		/// <see cref="ThemeChanged"/>) only if its answer changed. The OS answer can differ from the one the
		/// constructor got: the platform installs its <see cref="SystemAppearance"/> provider when the first
		/// window is shown, and the user can flip the OS setting at any time.
		/// </summary>
		public void RefreshSystemPreference()
		{
			if (this.Preference == ThemePreference.System
				&& this.systemPrefersDark() != this.IsDark)
			{
				this.ApplyAndNotify();
			}
		}

		private void ApplyAndNotify()
		{
			this.Apply();
			this.ThemeChanged?.Invoke(this, EventArgs.Empty);
		}

		private void Apply()
		{
			bool dark = this.Preference == ThemePreference.Dark
				|| (this.Preference == ThemePreference.System && this.systemPrefersDark());
			this.Palette = dark ? DemoPalette.Dark : DemoPalette.Light;

			DemoPalette palette = this.Palette;
			Color accent = ColorOf(this.Accent);
			ThemeConfig theme = this.Theme;

			theme.IsDarkTheme = dark;
			theme.BackgroundColor = palette.WindowFill;
			theme.TextColor = palette.TextColor;
			theme.LightTextColor = palette.TextDim;
			theme.PrimaryAccentColor = accent;
			theme.ButtonBackgroundColor = palette.WidgetBackground;
			theme.BorderColor20 = palette.Separator;
			theme.PopupBorderColor = palette.WindowStroke;

			// The accent-derived shades ThemeConfig.DefaultTheme derives, so hover and open highlights follow
			// the swatch.
			theme.AccentMimimalOverlay = accent.WithAlpha(128);
			theme.SlightShade = accent.WithAlpha(80);
			theme.MinimalShade = accent.WithAlpha(60);
			theme.RowBorder = palette.TextColor;

			// agg-gui's TextField: widget_bg fill; widget_stroke border idle, accent_pressed hovered, accent
			// focused. ThemedTextEditWidget reads these whenever it draws or changes focus, so replacing them
			// here recolours every field built with this theme.
			theme.EditFieldColors = new ThemeConfig.ThreeStateColor()
			{
				Focused = EditFieldState(palette, accent),
				Hovered = EditFieldState(palette, AccentPressed(this.Accent)),
				Inactive = EditFieldState(palette, palette.WidgetStroke),
			};
		}

		private static ThemeConfig.StateColor EditFieldState(DemoPalette palette, Color border)
		{
			return new ThemeConfig.StateColor()
			{
				BackgroundColor = palette.WidgetBackground,
				ForegroundColor = Color.Transparent,
				BorderColor = border,
				TextColor = palette.TextColor,
				LightTextColor = palette.TextDim,
			};
		}

		/// <summary>agg-gui's Visuals::accent_pressed: Blue has a tuned shade, the rest are 18% towards black.</summary>
		private static Color AccentPressed(AccentColor accent)
		{
			if (accent == AccentColor.Blue)
			{
				return DemoPalette.Rgb(0.16, 0.36, 0.72);
			}

			ColorF color = ColorOf(accent).ToColorF();
			return new ColorF(color.red * 0.82, color.green * 0.82, color.blue * 0.82).ToColor();
		}
	}
}
