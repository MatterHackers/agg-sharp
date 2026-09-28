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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ <c>pixfmt_alpha_blend_rgba&lt;Blender, rendering_buffer&gt;</c> for rgba32 (AGG_BGRA128), together with the
	/// few <c>renderer_base</c> calls that clip (<see cref="Clear"/>, <see cref="CopyBar"/>, <see cref="BlendFrom(PixelFormatBGRAFloat, int, int, int)"/>),
	/// over an <see cref="ImageBufferFloat"/>'s premultiplied float pixels. It exists beside ImageBufferFloat's own
	/// span methods because those fold the cover into alpha up front, which C++ does not: this keeps C++'s order
	/// of float operations - its opaque-copy shortcuts, and its separate full-cover path - so results are
	/// bit-identical. The span methods do not clip, as in C++.
	/// </summary>
	public class PixelFormatBGRAFloat : IPixelFormatFloat
	{
		private const int CoverFull = 255;

		private readonly ImageBufferFloat image;

		private readonly IBlenderRgbaFloat blender;

		public PixelFormatBGRAFloat(ImageBufferFloat image, IBlenderRgbaFloat blender)
		{
			this.image = image;
			this.blender = blender;
		}

		public ImageBufferFloat Image => image;

		public int Width => image.Width;

		public int Height => image.Height;

		/// <summary>C++ <c>pixel(x, y)</c>: the stored (premultiplied) channels.</summary>
		public ColorF Pixel(int x, int y)
		{
			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			return new ColorF(p[o + ImageBuffer.OrderR], p[o + ImageBuffer.OrderG], p[o + ImageBuffer.OrderB], p[o + ImageBuffer.OrderA]);
		}

		/// <summary>C++ <c>copy_hline</c>: stores the color as is, no blending.</summary>
		public void CopyHline(int x, int y, int len, ColorF c)
		{
			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				Set(p, o, c);
				o += 4;
			}
			while (--len != 0);
		}

		/// <summary>C++ <c>blend_hline</c>.</summary>
		public void BlendHline(int x, int y, int len, ColorF c, int cover)
		{
			if (IsTransparent(c))
			{
				return;
			}

			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				if (IsOpaque(c) && cover == CoverFull)
				{
					Set(p, o, c);
				}
				else if (cover == CoverFull)
				{
					blender.BlendPix(p, o, c);
				}
				else
				{
					blender.BlendPix(p, o, c, cover);
				}

				o += 4;
			}
			while (--len != 0);
		}

		/// <summary>C++ <c>blend_solid_hspan</c>. Unlike blend_hline, a full cover still goes through the covered blend.</summary>
		public void BlendSolidHspan(int x, int y, int len, ColorF c, byte[] covers, int coversIndex)
		{
			if (IsTransparent(c))
			{
				return;
			}

			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				int cover = covers[coversIndex++];
				if (IsOpaque(c) && cover == CoverFull)
				{
					Set(p, o, c);
				}
				else
				{
					blender.BlendPix(p, o, c, cover);
				}

				o += 4;
			}
			while (--len != 0);
		}

		/// <summary>C++ <c>blend_color_hspan</c>: per-pixel covers when <paramref name="covers"/> is given, else one <paramref name="cover"/> for all.</summary>
		public void BlendColorHspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, int cover)
		{
			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				if (covers != null)
				{
					CopyOrBlendPix(p, o, colors[colorsIndex++], covers[coversIndex++]);
				}
				else if (cover == CoverFull)
				{
					CopyOrBlendPix(p, o, colors[colorsIndex++]);
				}
				else
				{
					CopyOrBlendPix(p, o, colors[colorsIndex++], cover);
				}

				o += 4;
			}
			while (--len != 0);
		}

		/// <summary>C++ <c>blend_from(from, xdst, ydst, xsrc, ysrc, len, cover)</c>: one row of source pixels, blended as colors.</summary>
		public void BlendFrom(PixelFormatBGRAFloat from, int xdst, int ydst, int xsrc, int ysrc, int len, int cover)
		{
			float[] src = from.image.GetBuffer();
			int so = from.image.GetBufferOffsetXY(xsrc, ysrc);
			float[] dst = image.GetBuffer();
			int d = image.GetBufferOffsetXY(xdst, ydst);
			int inc = 4;

			// Walk backwards when shifting right, so a copy within one buffer does not read what it just wrote.
			if (xdst > xsrc)
			{
				so += (len - 1) * 4;
				d += (len - 1) * 4;
				inc = -4;
			}

			do
			{
				var c = new ColorF(src[so + ImageBuffer.OrderR], src[so + ImageBuffer.OrderG], src[so + ImageBuffer.OrderB], src[so + ImageBuffer.OrderA]);
				if (cover == CoverFull)
				{
					CopyOrBlendPix(dst, d, c);
				}
				else
				{
					CopyOrBlendPix(dst, d, c, cover);
				}

				so += inc;
				d += inc;
			}
			while (--len != 0);
		}

		/// <summary>C++ <c>renderer_base::clear</c>: every pixel set to the color.</summary>
		public void Clear(ColorF c)
		{
			for (int y = 0; y < Height; y++)
			{
				CopyHline(0, y, Width, c);
			}
		}

		/// <summary>C++ <c>renderer_base::copy_bar</c>: the inclusive rectangle, in either corner order, clipped to the image.</summary>
		public void CopyBar(int x1, int y1, int x2, int y2, ColorF c)
		{
			int left = Math.Max(Math.Min(x1, x2), 0);
			int right = Math.Min(Math.Max(x1, x2), Width - 1);
			int bottom = Math.Max(Math.Min(y1, y2), 0);
			int top = Math.Min(Math.Max(y1, y2), Height - 1);
			if (left > right || bottom > top)
			{
				return;
			}

			for (int y = bottom; y <= top; y++)
			{
				CopyHline(left, y, right - left + 1, c);
			}
		}

		/// <summary>
		/// C++ <c>renderer_base::blend_from(src, 0, dx, dy, cover)</c>: all of <paramref name="from"/>, offset by
		/// (<paramref name="dx"/>, <paramref name="dy"/>) and clipped to both images, blended row by row.
		/// </summary>
		public void BlendFrom(PixelFormatBGRAFloat from, int dx, int dy, int cover = CoverFull)
		{
			// C++ clip_rect_area with the whole source as the source rectangle.
			int srcX1 = 0, srcY1 = 0;
			int dstX1 = dx, dstY1 = dy;
			int dstX2 = from.Width + dx, dstY2 = from.Height + dy;
			if (dstX1 < 0)
			{
				srcX1 -= dstX1;
				dstX1 = 0;
			}

			if (dstY1 < 0)
			{
				srcY1 -= dstY1;
				dstY1 = 0;
			}

			dstX2 = Math.Min(dstX2, Width);
			dstY2 = Math.Min(dstY2, Height);
			int width = Math.Min(dstX2 - dstX1, from.Width - srcX1);
			int height = Math.Min(dstY2 - dstY1, from.Height - srcY1);
			if (width <= 0)
			{
				return;
			}

			// Rows go top-down when shifting up, for the same reason BlendFrom walks a row backwards.
			int incY = 1;
			if (dstY1 > srcY1)
			{
				srcY1 += height - 1;
				dstY1 += height - 1;
				incY = -1;
			}

			for (; height > 0; height--)
			{
				BlendFrom(from, dstX1, dstY1, srcX1, srcY1, width, cover);
				dstY1 += incY;
				srcY1 += incY;
			}
		}

		// C++ rgba32::is_transparent and is_opaque.
		private static bool IsTransparent(ColorF c) => c.alpha <= 0;

		private static bool IsOpaque(ColorF c) => c.alpha >= 1;

		private static void Set(float[] p, int o, ColorF c)
		{
			p[o + ImageBuffer.OrderR] = c.red;
			p[o + ImageBuffer.OrderG] = c.green;
			p[o + ImageBuffer.OrderB] = c.blue;
			p[o + ImageBuffer.OrderA] = c.alpha;
		}

		// C++ copy_or_blend_pix with a cover: nothing for a transparent color, a copy when opaque at full cover.
		private void CopyOrBlendPix(float[] p, int o, ColorF c, int cover)
		{
			if (!IsTransparent(c))
			{
				if (IsOpaque(c) && cover == CoverFull)
				{
					Set(p, o, c);
				}
				else
				{
					blender.BlendPix(p, o, c, cover);
				}
			}
		}

		// C++ copy_or_blend_pix without a cover.
		private void CopyOrBlendPix(float[] p, int o, ColorF c)
		{
			if (!IsTransparent(c))
			{
				if (IsOpaque(c))
				{
					Set(p, o, c);
				}
				else
				{
					blender.BlendPix(p, o, c);
				}
			}
		}
	}
}
