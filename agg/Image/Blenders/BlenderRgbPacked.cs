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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ pixfmt_alpha_blend_rgb_packed: one 16-bit word per pixel, stored little-endian as C++'s int16u is on
	/// every host agg-sharp runs on. A 16-bit <see cref="ImageBuffer"/> takes a subclass as its blender.
	/// </summary>
	/// <remarks>
	/// The packed formats keep C++'s own conventions: a channel is quantised by truncation (make_pix masks off
	/// the low bits, blend_pix truncates the blended value) and read back without bit replication (make_color
	/// shifts the bits up), so white reads back as 248, and a pixel with any coverage of a darker color drops at
	/// least one level. A pixel has no alpha; it reads back opaque.
	/// </remarks>
	public abstract class BlenderRgbPacked : IRecieveBlenderByte
	{
		public int NumPixelBits => 16;

		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			return this.MakeColor(buffer[bufferOffset] | (buffer[bufferOffset + 1] << 8));
		}

		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			int pixel = this.MakePix(sourceColor.red, sourceColor.green, sourceColor.blue);
			do
			{
				Store(buffer, bufferOffset, pixel);
				bufferOffset += 2;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			this.CopyOrBlendPix(buffer, bufferOffset, sourceColor, 255);
		}

		public void BlendPixels(byte[] buffer, int bufferOffset, Color[] sourceColors, int sourceColorsOffset, byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			do
			{
				this.CopyOrBlendPix(buffer, bufferOffset, sourceColors[sourceColorsOffset++], sourceCovers[sourceCoversOffset]);
				if (!firstCoverForAll)
				{
					sourceCoversOffset++;
				}

				bufferOffset += 2;
			}
			while (--count != 0);
		}

		/// <summary>C++ blender make_pix: the color's channels packed into one word.</summary>
		protected abstract int MakePix(int r, int g, int b);

		/// <summary>C++ blender make_color: the word's channels shifted back up to 8 bits, opaque.</summary>
		protected abstract Color MakeColor(int pixel);

		/// <summary>C++ blender blend_pix: the word moved toward (r, g, b) by <paramref name="alpha"/> of 255.</summary>
		protected abstract int BlendPix(int pixel, int r, int g, int b, int alpha);

		private static void Store(byte[] buffer, int bufferOffset, int pixel)
		{
			buffer[bufferOffset] = (byte)pixel;
			buffer[bufferOffset + 1] = (byte)(pixel >> 8);
		}

		// C++ copy_or_blend_pix. The packed formats scale alpha by the cover as (a * (cover + 1)) >> 8, not the
		// rounded multiply the 8-bit formats use.
		private void CopyOrBlendPix(byte[] buffer, int bufferOffset, Color c, int cover)
		{
			if (c.alpha == 0)
			{
				return;
			}

			int alpha = (c.alpha * (cover + 1)) >> 8;
			if (alpha == 255)
			{
				Store(buffer, bufferOffset, this.MakePix(c.red, c.green, c.blue));
			}
			else
			{
				int pixel = buffer[bufferOffset] | (buffer[bufferOffset + 1] << 8);
				Store(buffer, bufferOffset, this.BlendPix(pixel, c.red, c.green, c.blue, alpha));
			}
		}
	}

	/// <summary>C++ pixfmt_rgb555 (blender_rgb555): 5 bits each of red, green and blue, and the top bit always set.</summary>
	public sealed class BlenderRgb555 : BlenderRgbPacked
	{
		protected override int MakePix(int r, int g, int b)
		{
			return ((r & 0xF8) << 7) | ((g & 0xF8) << 2) | (b >> 3) | 0x8000;
		}

		protected override Color MakeColor(int pixel)
		{
			return new Color((pixel >> 7) & 0xF8, (pixel >> 2) & 0xF8, (pixel << 3) & 0xF8, 255);
		}

		protected override int BlendPix(int pixel, int cr, int cg, int cb, int alpha)
		{
			int r = (pixel >> 7) & 0xF8;
			int g = (pixel >> 2) & 0xF8;
			int b = (pixel << 3) & 0xF8;

			// Each blended value is the channel in 8.8 fixed point and never negative, so int holds what C++'s
			// unsigned arithmetic wraps back to.
			return ((((cr - r) * alpha + (r << 8)) >> 1) & 0x7C00)
				| ((((cg - g) * alpha + (g << 8)) >> 6) & 0x03E0)
				| (((cb - b) * alpha + (b << 8)) >> 11)
				| 0x8000;
		}
	}

	/// <summary>C++ pixfmt_rgb565 (blender_rgb565): 5 bits of red, 6 of green, 5 of blue.</summary>
	public sealed class BlenderRgb565 : BlenderRgbPacked
	{
		protected override int MakePix(int r, int g, int b)
		{
			return ((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3);
		}

		protected override Color MakeColor(int pixel)
		{
			return new Color((pixel >> 8) & 0xF8, (pixel >> 3) & 0xFC, (pixel << 3) & 0xF8, 255);
		}

		protected override int BlendPix(int pixel, int cr, int cg, int cb, int alpha)
		{
			int r = (pixel >> 8) & 0xF8;
			int g = (pixel >> 3) & 0xFC;
			int b = (pixel << 3) & 0xF8;
			return (((cr - r) * alpha + (r << 8)) & 0xF800)
				| ((((cg - g) * alpha + (g << 8)) >> 5) & 0x07E0)
				| (((cb - b) * alpha + (b << 8)) >> 11);
		}
	}
}
