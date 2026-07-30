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

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// 3-byte-per-pixel LCD coverage mask. The distinction from a normal RGB image is crucial: the
	/// three bytes are <b>independent coverage values</b>, not a color - they drive a per-channel
	/// blend where each subpixel mixes the source color with the destination color by its own amount.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference (<c>lcd_coverage\mask.rs</c> <c>LcdMask</c>).
	/// <para>
	/// Row order is Y-up: row 0 is the bottom row, matching both the Rust masks and agg-sharp's
	/// <see cref="MatterHackers.Agg.Image.ImageBuffer"/> convention. (Cached backbuffer planes in the
	/// Rust reference are top-row-first; that flip lives at the texture boundary, not here.)
	/// </para>
	/// </remarks>
	public class LcdMask
	{
		/// <summary>
		/// Packed coverage, length <c>Width * Height * 3</c>, stride <c>Width * 3</c>, channel order
		/// R, G, B within each pixel.
		/// </summary>
		public byte[] Data { get; }

		public int Width { get; }

		public int Height { get; }

		/// <summary>
		/// Allocates a zero-filled mask of <paramref name="width"/> x <paramref name="height"/> pixels.
		/// </summary>
		public LcdMask(int width, int height)
		{
			if (width < 0 || height < 0)
			{
				throw new ArgumentException("LcdMask dimensions must not be negative.");
			}

			this.Width = width;
			this.Height = height;
			this.Data = new byte[width * height * 3];
		}

		/// <summary>
		/// Wraps an existing packed coverage buffer (no copy). <paramref name="data"/> must be exactly
		/// <c>width * height * 3</c> bytes.
		/// </summary>
		public LcdMask(byte[] data, int width, int height)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			if (width < 0 || height < 0)
			{
				throw new ArgumentException("LcdMask dimensions must not be negative.");
			}

			if (data.Length != width * height * 3)
			{
				throw new ArgumentException($"LcdMask data must be width * height * 3 bytes ({width * height * 3}), was {data.Length}.");
			}

			this.Data = data;
			this.Width = width;
			this.Height = height;
		}

		/// <summary>
		/// Index into <see cref="Data"/> of the red byte of pixel (<paramref name="x"/>,
		/// <paramref name="y"/>); green and blue follow at +1 and +2. Y is measured from the bottom.
		/// </summary>
		public int PixelOffset(int x, int y)
		{
			return ((y * this.Width) + x) * 3;
		}

		/// <summary>
		/// Reads the three independent coverage values of pixel (<paramref name="x"/>,
		/// <paramref name="y"/>). Y is measured from the bottom.
		/// </summary>
		public void GetPixel(int x, int y, out byte red, out byte green, out byte blue)
		{
			int offset = this.PixelOffset(x, y);
			red = this.Data[offset];
			green = this.Data[offset + 1];
			blue = this.Data[offset + 2];
		}
	}
}
