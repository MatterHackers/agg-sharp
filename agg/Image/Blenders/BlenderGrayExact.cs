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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// 8 bit-per-pixel gray blender that interpolates exactly the way AGG proper does, rather than the
	/// way <see cref="blender_gray"/> does.
	/// </summary>
	/// <remarks>
	/// <see cref="blender_gray"/> interpolates with a plain <c>&gt;&gt; 8</c>
	/// (<c>((src - dst) * alpha + (dst &lt;&lt; 8)) &gt;&gt; 8</c>), which truncates: blending white over
	/// black at alpha 128 lands on 127, and in general every partially covered pixel comes out one low.
	/// AGG's <c>gray8::lerp</c> - and the agg-rust reference port
	/// (<c>agg-rust/src/color.rs</c> <c>Gray8::lerp</c>, reached through
	/// <c>pixfmt_gray.rs</c> <c>PixfmtGray8::blend_pix</c>) - adds the rounding term
	/// <c>((t &gt;&gt; 8) + t) &gt;&gt; 8</c> and gets 128.
	/// <para>
	/// That one-count difference exists across all of agg-sharp and is invisible in ordinary painting, so
	/// <see cref="blender_gray"/> is left alone. This blender exists for
	/// <see cref="MatterHackers.Agg.LcdCoverage.LcdMaskBuilder"/>, whose gray bytes <b>are</b> the LCD
	/// coverage values and are required to be byte-identical to the Rust reference's; a systematic -1 on
	/// every anti-aliased subpixel would make fixture comparison against that reference impossible.
	/// </para>
	/// <para>
	/// Only the interpolation differs. The RGB to gray weights are deliberately left identical to
	/// <see cref="blender_gray"/>'s (77/151/28, i.e. BT.601-ish) even though the reference uses BT.709
	/// (55/184/18): the LCD pipeline only ever renders opaque white, where both land on exactly 255, and
	/// matching the sibling blender keeps this class a drop-in replacement. Any future caller that feeds
	/// non-white colors through it does <i>not</i> get reference-exact luminance.
	/// </para>
	/// </remarks>
	public class BlenderGrayExact : IRecieveBlenderByte
	{
		/// <summary>AGG's <c>base_shift</c> for 8 bit channels.</summary>
		private const int BaseShift = 8;

		/// <summary>AGG's <c>base_msb</c>: <c>1 &lt;&lt; (base_shift - 1)</c>, the rounding bias.</summary>
		private const int BaseMsb = 1 << (BaseShift - 1);

		private readonly int bytesBetweenPixelsInclusive;

		public BlenderGrayExact(int bytesBetweenPixelsInclusive)
		{
			this.bytesBetweenPixelsInclusive = bytesBetweenPixelsInclusive;
		}

		public int NumPixelBits => 8;

		/// <summary>
		/// AGG's <c>gray8::multiply</c>: <c>a * b / 255</c> with correct rounding, so that
		/// <c>Multiply(255, x) == x</c> for every x (the plain <c>(a * b) &gt;&gt; 8</c> is one low).
		/// </summary>
		public static byte Multiply(int a, int b)
		{
			int t = (a * b) + BaseMsb;
			return (byte)(((t >> BaseShift) + t) >> BaseShift);
		}

		/// <summary>
		/// AGG's <c>gray8::lerp</c>: interpolate <paramref name="p"/> towards <paramref name="q"/> by
		/// <paramref name="alpha"/>, rounding so both endpoints are reached exactly.
		/// </summary>
		/// <remarks>
		/// <c>t</c> goes negative whenever <c>q &lt; p</c>, and the shifts must stay arithmetic for the
		/// result to be right in that direction - C#'s <c>&gt;&gt;</c> on <see cref="int"/> is, matching
		/// Rust's <c>i32</c> shift and C++'s <c>int</c> shift. The <c>-1</c> when <c>p &gt; q</c> is AGG's
		/// bias correction for the downward case; without it lerp(255, 0, 255) misses 0.
		/// </remarks>
		public static byte Lerp(int p, int q, int alpha)
		{
			int t = ((q - p) * alpha) + BaseMsb - (p > q ? 1 : 0);
			return (byte)(p + (((t >> BaseShift) + t) >> BaseShift));
		}

		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			int value = buffer[bufferOffset];
			return new Color(value, value, value, 255);
		}

		public void CopyPixels(byte[] destBuffer, int bufferOffset, Color sourceColor, int count)
		{
			byte gray = ToGray(sourceColor);
			do
			{
				destBuffer[bufferOffset] = gray;
				bufferOffset += this.bytesBetweenPixelsInclusive;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] destBuffer, int bufferOffset, Color sourceColor)
		{
			destBuffer[bufferOffset] = Lerp(destBuffer[bufferOffset], ToGray(sourceColor), sourceColor.alpha);
		}

		public void BlendPixels(
			byte[] destBuffer,
			int bufferOffset,
			Color[] sourceColors,
			int sourceColorsOffset,
			byte[] covers,
			int coversIndex,
			bool firstCoverForAll,
			int count)
		{
			do
			{
				int cover = covers[firstCoverForAll ? coversIndex : coversIndex++];
				Color sourceColor = sourceColors[sourceColorsOffset++];
				int alpha = cover == 255 ? sourceColor.alpha : Multiply(sourceColor.alpha, cover);
				destBuffer[bufferOffset] = Lerp(destBuffer[bufferOffset], ToGray(sourceColor), alpha);
				bufferOffset += this.bytesBetweenPixelsInclusive;
			}
			while (--count != 0);
		}

		/// <summary>
		/// The same luminance weights <see cref="blender_gray"/> uses; white maps to exactly 255 because
		/// 77 + 151 + 28 == 256.
		/// </summary>
		private static byte ToGray(Color sourceColor)
		{
			int y = (sourceColor.red * 77) + (sourceColor.green * 151) + (sourceColor.blue * 28);
			return (byte)(y >> BaseShift);
		}
	}
}
