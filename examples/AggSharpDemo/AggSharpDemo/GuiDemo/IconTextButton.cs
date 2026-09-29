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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// A text button whose label starts with an <see cref="IconFont"/> glyph, as agg-gui's labels start with a
	/// Font Awesome codepoint and a space. The glyph is painted at the start of the left padding in the text's
	/// colour, and the padding grows by its width so the text moves over.
	/// </summary>
	/// <remarks>
	/// Painted rather than a child widget: a Stretch button's LocalBounds need not start at zero, and a Left
	/// anchored child would sit after the padding rather than in it.
	/// </remarks>
	public class IconTextButton : ThemedTextButton
	{
		/// <summary>The gap between the glyph and the text, in design units.</summary>
		private const double IconGap = 4;

		private ImageBuffer icon;

		private Color iconColor;

		/// <param name="iconGlyph">Null or empty for a plain text button.</param>
		public IconTextButton(string text, string iconGlyph, ThemeConfig theme, double pointSize = -1)
			: base(text, theme, pointSize)
		{
			this.IconGlyph = string.IsNullOrEmpty(iconGlyph) ? null : iconGlyph;
		}

		/// <summary>The glyph in front of the text, or null for none.</summary>
		public string IconGlyph { get; }

		/// <summary>Sets the padding as for a plain button; with a glyph, its width and gap are added on the left.</summary>
		/// <remarks>The glyph's room is in design units, as Padding is: GuiWidget scales Padding by DeviceScale
		/// itself (DevicePadding), so room added here already times DeviceScale doubled the gap at 2x and pushed
		/// the label about 20 device pixels right of the glyph.</remarks>
		public BorderDouble TextPadding
		{
			set => this.Padding = this.IconGlyph == null
				? value
				: new BorderDouble(value.Left + IconGlyphWidget.IconSize + IconGap, value.Bottom, value.Right, value.Top);
		}

		/// <summary>Where the glyph is drawn, in the button's own coordinates: at the start of the left padding,
		/// centred vertically. Device pixels, so it is measured from DevicePadding, not the design-unit Padding.</summary>
		public RectangleDouble IconBounds
		{
			get
			{
				double size = Math.Round(IconGlyphWidget.IconSize * DeviceScale);
				double left = this.LocalBounds.Left + this.DevicePadding.Left - (IconGlyphWidget.IconSize + IconGap) * DeviceScale;
				double bottom = Math.Round(this.LocalBounds.Center.Y - size / 2);
				return new RectangleDouble(left, bottom, left + size, bottom + size);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (this.IconGlyph == null)
			{
				return;
			}

			// Re-rendered only when the lettering colour changes (lit, unlit, theme)
			if (this.icon == null || this.iconColor != this.TextColor)
			{
				this.iconColor = this.TextColor;
				this.icon = GlyphIcon.Render(this.IconGlyph, IconFont.TypeFace, this.iconColor, (int)Math.Round(IconGlyphWidget.IconSize * DeviceScale));
			}

			if (this.icon != null)
			{
				RectangleDouble bounds = this.IconBounds;
				graphics2D.Render(this.icon, bounds.Left, bounds.Bottom);
			}
		}
	}
}
