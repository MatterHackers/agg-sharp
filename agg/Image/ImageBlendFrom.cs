//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
// C# port Copyright (c) 2026, Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ AGG's <c>renderer_base::blend_from_color</c> and <c>blend_from_lut</c>: a gray image (one byte per pixel)
	/// drawn onto a colour image at an offset, each gray byte either the cover of one colour or the index of a colour
	/// in a 256-entry table. How blend_color turns a blurred gray shadow into a coloured one.
	/// </summary>
	public static class ImageBlendFrom
	{
		/// <summary>
		/// Blends <paramref name="color"/> into <paramref name="dest"/> through <paramref name="gray"/>: gray pixel
		/// (x, y) is the cover of dest pixel (x + <paramref name="dx"/>, y + <paramref name="dy"/>). Clipped to dest.
		/// </summary>
		public static void BlendFromColor(this IImageByte dest, IImageByte gray, Color color, int dx, int dy)
		{
			if (color.alpha == 0)
			{
				return;
			}

			BlendFrom(dest, gray, dx, dy, value =>
			{
				// C++ copy_or_blend_pix(p, color, scale_cover(cover_full, v)): the colour's alpha times the gray.
				Color c = color;
				c.alpha = (byte)Rgba8Math.Multiply(color.alpha, value);
				return c;
			});
		}

		/// <summary>
		/// Blends <paramref name="colorLut"/>[gray] into <paramref name="dest"/>: gray pixel (x, y) picks the colour
		/// blended at dest pixel (x + <paramref name="dx"/>, y + <paramref name="dy"/>). Clipped to dest.
		/// </summary>
		public static void BlendFromLut(this IImageByte dest, IImageByte gray, Color[] colorLut, int dx, int dy)
		{
			if (colorLut.Length < 256)
			{
				throw new ArgumentException("A colour table needs an entry for every gray value, 256.", nameof(colorLut));
			}

			BlendFrom(dest, gray, dx, dy, value => colorLut[value]);
		}

		// Every gray pixel that lands inside dest, blended through dest's own blender. Its BlendPixel copies an opaque
		// colour and leaves the pixel alone at alpha 0, which is C++'s copy_or_blend_pix and blend_pix for rgba8 alike.
		private static void BlendFrom(IImageByte dest, IImageByte gray, int dx, int dy, Func<byte, Color> colorFor)
		{
			int x1 = Math.Max(0, -dx);
			int y1 = Math.Max(0, -dy);
			int x2 = Math.Min(gray.Width, dest.Width - dx);
			int y2 = Math.Min(gray.Height, dest.Height - dy);

			byte[] grayBuffer = gray.GetBuffer();
			byte[] destBuffer = dest.GetBuffer();
			IRecieveBlenderByte blender = dest.GetRecieveBlender();
			for (int y = y1; y < y2; y++)
			{
				for (int x = x1; x < x2; x++)
				{
					Color c = colorFor(grayBuffer[gray.GetBufferOffsetXY(x, y)]);
					if (c.alpha != 0)
					{
						blender.BlendPixel(destBuffer, dest.GetBufferOffsetXY(x + dx, y + dy), c);
					}
				}
			}

			dest.MarkImageChanged();
		}
	}
}
