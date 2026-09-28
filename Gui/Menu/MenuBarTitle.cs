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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// One <see cref="MenuBarWidget"/> title: the menu's text, led by its <see cref="MenuItemModel.Icon"/> or
	/// <see cref="MenuItemModel.IconGlyph"/> when it has one - agg-gui's "\u{F009} Demos". Without an icon it is a
	/// plain <see cref="ThemedTextButton"/>.
	/// </summary>
	/// <remarks>
	/// The icon is painted in the left padding rather than added as a child: a Left anchored child would sit after
	/// the padding, and the bar's hit testing wants one widget per title.
	/// </remarks>
	internal class MenuBarTitle : ThemedTextButton
	{
		/// <summary>The icon's square, in design units - the size the popup rows draw theirs.</summary>
		public const double IconSize = 16;

		/// <summary>The gap between the icon and the text, in design units.</summary>
		public const double IconGap = 4;

		private readonly MenuItemModel menu;
		private ImageBuffer glyphImage;
		private Color glyphColor;

		public MenuBarTitle(MenuItemModel menu, ThemeConfig theme)
			: base(menu.Text, theme)
		{
			this.menu = menu;
			if (HasIcon)
			{
				Padding = new BorderDouble(Padding.Left + IconSize + IconGap, Padding.Bottom, Padding.Right, Padding.Top);
			}
		}

		private bool HasIcon => menu.Icon != null || !string.IsNullOrEmpty(menu.IconGlyph);

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (!HasIcon)
			{
				return;
			}

			ImageBuffer icon = menu.Icon;
			if (icon == null)
			{
				// Re-rasterized only when the lettering colour changes (hover, theme), so the glyph matches the text.
				if (glyphImage == null || glyphColor != TextColor)
				{
					glyphColor = TextColor;
					glyphImage = GlyphIcon.Render(menu.IconGlyph, menu.IconTypeFace, glyphColor, (int)Math.Round(IconSize * DeviceScale));
				}

				icon = glyphImage;
			}

			if (icon != null)
			{
				double left = LocalBounds.Left + DevicePadding.Left - (IconSize + IconGap) * DeviceScale;
				double bottom = Math.Round(LocalBounds.Center.Y - icon.Height / 2.0);
				graphics2D.Render(icon, Math.Round(left), bottom);
			}
		}
	}
}
