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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// agg-gui's (and so egui's) checkbox, radio and toggle switch look, drawn from a <see cref="ThemeConfig"/>.
	/// ThemeConfig has no egui Visuals, so the few neutrals agg-gui takes from them are derived here:
	/// widget_bg is ButtonBackgroundColor, window_fill is BackgroundColor, and widget_stroke is agg-gui's own
	/// value for the theme's light or dark side. Every length is in device pixels - callers pass geometry
	/// already scaled by DeviceScale.
	/// </summary>
	internal static class SelectionControlStyle
	{
		/// <summary>agg-gui draws disabled controls at 40% opacity (GalleryScope's non-interactive dim).</summary>
		private const double DisabledOpacity = .4;

		public static Color Accent(ThemeConfig theme) => theme.PrimaryAccentColor;

		public static Color AccentHovered(ThemeConfig theme) => theme.PrimaryAccentColor.Blend(Color.White, .12);

		public static Color AccentPressed(ThemeConfig theme) => theme.PrimaryAccentColor.Blend(Color.Black, .18);

		public static Color WidgetBackground(ThemeConfig theme) => theme.ButtonBackgroundColor;

		/// <summary>agg-gui's widget_bg_hovered: widget_bg nudged toward the text colour.</summary>
		public static Color WidgetBackgroundHovered(ThemeConfig theme) => WithOpaqueAlpha(theme.ButtonBackgroundColor.Blend(theme.TextColor, .08));

		/// <summary>agg-gui's widget_stroke for Visuals::dark() and Visuals::light().</summary>
		public static Color WidgetStroke(ThemeConfig theme) => theme.IsDarkTheme
			? new ColorF(.60, .60, .65, .60).ToColor()
			: new ColorF(.75, .76, .78).ToColor();

		/// <summary>agg-gui's track_bg (a slider's unfilled rail) for Visuals::dark() and Visuals::light().</summary>
		public static Color TrackBackground(ThemeConfig theme) => theme.IsDarkTheme
			? new ColorF(.25, .25, .28).ToColor()
			: new ColorF(.85, .86, .88).ToColor();

		/// <summary>
		/// agg-gui's checkbox: a rounded box, accent filled with a white tick when checked, window or widget
		/// fill with a neutral border when not. Indeterminate draws the checked fill with a white dash instead.
		/// </summary>
		public static void DrawCheckBox(Graphics2D graphics2D, RectangleDouble box, bool isChecked, bool hovered, bool pressed, bool enabled, ThemeConfig theme, bool indeterminate = false)
		{
			bool filled = isChecked || indeterminate;
			// agg-gui's box is 16 with a 3 radius, a 1.5 border and a 2 wide tick; scale them to this box.
			double unit = box.Width / 16;
			double radius = 3 * unit;
			double borderWidth = Math.Max(1, 1.5 * unit);

			Color fill;
			Color border;
			if (filled)
			{
				fill = pressed ? AccentPressed(theme) : hovered ? AccentHovered(theme) : Accent(theme);
				border = AccentPressed(theme);
			}
			else
			{
				bool active = hovered || pressed;
				if (theme.IsDarkTheme)
				{
					fill = active ? WidgetBackground(theme) : theme.BackgroundColor;
					border = new ColorF(1, 1, 1, .34).ToColor();
				}
				else
				{
					fill = active ? WidgetBackgroundHovered(theme) : WidgetBackground(theme);
					border = WidgetStroke(theme);
				}
			}

			graphics2D.Render(new RoundedRect(box, radius), Dim(fill, enabled));

			var borderRect = box;
			borderRect.Inflate(-borderWidth / 2);
			graphics2D.Render(new Stroke(new RoundedRect(borderRect, radius - borderWidth / 2), borderWidth), Dim(border, enabled));

			if (indeterminate)
			{
				double middle = box.Bottom + box.Height * .5;
				var dash = new VertexStorage();
				dash.MoveTo(box.Left + 3 * unit, middle);
				dash.LineTo(box.Right - 3 * unit, middle);
				graphics2D.Render(new Stroke(dash, Math.Max(1, 2 * unit)), Dim(Color.White, enabled));
			}
			else if (isChecked)
			{
				var tick = new VertexStorage();
				tick.MoveTo(box.Left + 3 * unit, box.Bottom + box.Height * .55);
				tick.LineTo(box.Left + box.Width * .42, box.Bottom + box.Height * .28);
				tick.LineTo(box.Right - 3 * unit, box.Bottom + box.Height * .75);
				graphics2D.Render(new Stroke(tick, Math.Max(1, 2 * unit)), Dim(Color.White, enabled));
			}
		}

		/// <summary>agg-gui's radio: a circle, accent with an inner widget_bg dot when selected.</summary>
		public static void DrawRadio(Graphics2D graphics2D, Vector2 center, double radius, bool isChecked, bool hovered, bool pressed, bool enabled, ThemeConfig theme)
		{
			double borderWidth = Math.Max(1, 1.5 * radius / 8);

			Color fill = isChecked ? (pressed ? AccentPressed(theme) : Accent(theme)) : WidgetBackground(theme);
			Color border = isChecked ? fill : (hovered || pressed) ? AccentHovered(theme) : WidgetStroke(theme);

			graphics2D.Render(new Ellipse(center, radius), Dim(fill, enabled));
			graphics2D.Render(new Stroke(new Ellipse(center, radius - borderWidth / 2), borderWidth), Dim(border, enabled));

			if (isChecked)
			{
				graphics2D.Render(new Ellipse(center, radius * .45), Dim(WidgetBackground(theme), enabled));
			}
		}

		/// <summary>
		/// agg-gui's toggle switch: a full pill track, the on colour when on and widget_stroke when off, with a
		/// round knob inset 2.5 (of an 18 high pill) at the matching end. agg-gui slides the knob over 0.14s;
		/// this snaps, as the switch has no frame timer to drive a tween.
		/// </summary>
		public static void DrawSwitch(Graphics2D graphics2D, RectangleDouble track, bool isOn, bool hovered, bool enabled, Color onColor, Color knobColor, ThemeConfig theme)
		{
			double radius = track.Height / 2;
			Color trackColor = isOn
				? (hovered ? onColor.Blend(Color.White, .12) : onColor)
				: (hovered ? WidgetBackgroundHovered(theme) : WidgetStroke(theme));
			graphics2D.Render(new RoundedRect(track, radius), Dim(trackColor, enabled));

			double knobRadius = radius - 2.5 * track.Height / 18;
			double knobX = isOn ? track.Right - radius : track.Left + radius;
			graphics2D.Render(new Ellipse(new Vector2(knobX, track.Center.Y), knobRadius), Dim(knobColor, enabled));
		}

		private static Color Dim(Color color, bool enabled) => enabled ? color : color.WithAlpha(color.Alpha0To1 * DisabledOpacity);

		private static Color WithOpaqueAlpha(Color color) => new Color(color, 255);
	}
}
