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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Rasterizes one character of a font - typically an icon font's private use glyph, like Font Awesome's
	/// U+F0C5 copy - into a square image, so anywhere that takes an <see cref="ImageBuffer"/> icon can take a
	/// glyph instead. The outline goes through agg-sharp's own TrueType engine.
	/// </summary>
	public static class GlyphIcon
	{
		/// <summary>
		/// Draws <paramref name="glyph"/> centred in a <paramref name="size"/> pixel square.
		/// </summary>
		/// <param name="glyph">The text to draw; only its first character is used. Null or empty returns null.</param>
		/// <param name="typeFace">The font carrying the glyph. Null takes <see cref="LiberationSansFont.Instance"/>.</param>
		/// <param name="color">The glyph's colour.</param>
		/// <param name="size">The image's width and height in device pixels.</param>
		/// <returns>The image, or null when there is no glyph or the font has no outline for it.</returns>
		public static ImageBuffer Render(string glyph, TypeFace typeFace, Color color, int size)
		{
			if (string.IsNullOrEmpty(glyph)
				|| size <= 0)
			{
				return null;
			}

			typeFace ??= LiberationSansFont.Instance;

			// One em across the square: Font Awesome 4 draws on a 14 unit grid inside a 16 unit em, so its
			// usual icon lands a pixel or so inside the square, the size agg-gui's rows show them.
			var styled = new StyledTypeFace(typeFace, size * StyledTypeFace.PointsPerInch / StyledTypeFace.PixelsPerInch);
			IVertexSource outline = styled.GetGlyphForCharacter(glyph[0]);
			if (outline == null)
			{
				return null;
			}

			RectangleDouble bounds = outline.GetBounds();
			if (bounds.Width <= 0
				|| bounds.Height <= 0)
			{
				return null;
			}

			// Icon fonts do not share one advance or baseline, so centre on the outline itself rather than the
			// pen position, and shrink anything wider or taller than the square (a few icons are).
			double scale = Math.Min(1, size / Math.Max(bounds.Width, bounds.Height));
			Affine transform = Affine.NewTranslation(-bounds.Center.X, -bounds.Center.Y)
				* Affine.NewScaling(scale)
				* Affine.NewTranslation(size / 2.0, size / 2.0);

			// Premultiplied, like the menu's own radio icons, which the row draws the same way
			var image = new ImageBuffer(size, size);
			image.SetRecieveBlender(new BlenderPreMultBGRA());
			image.NewGraphics2D().Render(new VertexSourceApplyTransform(outline, transform), color);
			return image;
		}
	}
}
