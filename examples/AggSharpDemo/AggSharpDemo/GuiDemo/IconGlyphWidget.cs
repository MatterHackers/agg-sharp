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
	/// One <see cref="IconFont"/> glyph in a square, 16px by default (the size menu glyph icons get), drawn in <see cref="Color"/>.
	/// agg-gui prefixes its titles with the codepoint and lets the text run render it; our text runs have no
	/// Font Awesome fallback, so the glyph sits beside the text as its own widget.
	/// </summary>
	public class IconGlyphWidget : GuiWidget
	{
		/// <summary>The design-unit side of the square.</summary>
		public const double IconSize = 16;

		private readonly double size;

		private Color color;

		private ImageBuffer image;

		/// <param name="size">The square's side in design units; smaller than <see cref="IconSize"/> where the glyph
		/// stands in a smaller run of text (the sidebar's 12px search placeholder).</param>
		public IconGlyphWidget(string glyph, Color color, double size = IconSize)
		{
			this.Glyph = glyph;
			this.color = color;
			this.size = size;
			this.Width = size * DeviceScale;
			this.Height = size * DeviceScale;
			this.Selectable = false;
		}

		public string Glyph { get; }

		/// <summary>The glyph's colour; setting it re-renders the glyph on the next draw.</summary>
		public Color Color
		{
			get => this.color;
			set
			{
				if (value != this.color)
				{
					this.color = value;
					this.image = null;
					this.Invalidate();
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// Rendered once and kept until the colour changes, so a redraw is a blit
			this.image ??= GlyphIcon.Render(this.Glyph, IconFont.TypeFace, this.color, (int)Math.Round(this.size * DeviceScale));
			if (this.image != null)
			{
				graphics2D.Render(this.image, 0, 0);
			}

			base.OnDraw(graphics2D);
		}
	}
}
