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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Stage 3 of the LCD pipeline: composites an <see cref="LcdMask"/> onto a 32 bit-per-pixel
	/// destination, applying the draw color per channel. Each subpixel mixes the source color into
	/// whatever is already in the destination by <b>its own</b> coverage, which is why a mask can be
	/// cached and reused over any background - the mask carries no destination knowledge.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference (<c>lcd_coverage\mask.rs</c> <c>composite_lcd_mask</c>,
	/// with the framebuffer twin at <c>gfx_ctx\draw_impl.rs</c> <c>draw_lcd_mask</c>). The arithmetic is
	/// deliberately <see cref="float"/> to match the reference's <c>f32</c> bit for bit.
	/// <para>
	/// <b>Destination requirements.</b> 32 bits per pixel, in agg-sharp's B, G, R, A byte order (see
	/// <see cref="ImageBuffer.OrderR"/> and friends - the Rust reference's buffer is R, G, B, A, so the
	/// channel indices are remapped here rather than copied). Color values are treated as
	/// <b>straight (non-premultiplied)</b>, exactly as the reference documents for its "straight RGBA
	/// dst" path, and destination alpha is left untouched.
	/// </para>
	/// <para>
	/// That makes a premultiplied destination - including MatterCAD's widget backbuffers, which use
	/// <see cref="BlenderPreMultBGRA"/> - correct only where its alpha is already 255, because straight
	/// and premultiplied coincide at full opacity. That is not a real restriction today: LCD subpixel
	/// geometry is only valid against an opaque destination in the first place (a transparent
	/// compositing layer takes <see cref="LcdMaskBuilder.FinalizeGray"/> instead), and the genuinely
	/// premultiplied, per-channel-alpha target is the two-plane <c>LcdBuffer</c> that follows this step.
	/// </para>
	/// <para>
	/// Like the reference, this writes destination bytes directly and does not go through
	/// <see cref="IRecieveBlenderByte"/>: per-channel coverage is not expressible there
	/// (<c>ImageBuffer.blend_solid_hspan</c> collapses covers to a scalar alpha before the blender sees
	/// them), which is the whole reason the LCD path is a mask pipeline rather than a blender.
	/// </para>
	/// </remarks>
	public static class LcdComposite
	{
		/// <summary>
		/// Composites <paramref name="mask"/> onto <paramref name="destination"/> in
		/// <paramref name="source"/>, placing the mask's bottom-left pixel at
		/// (<paramref name="destX"/>, <paramref name="destY"/>).
		/// </summary>
		/// <param name="destination">32 bit-per-pixel destination; see the class remarks for the alpha and
		/// byte-order convention. Its alpha channel is not modified.</param>
		/// <param name="mask">Per-channel coverage. Empty masks are a no-op.</param>
		/// <param name="source">The draw color. Its alpha scales every channel's coverage, so a
		/// half-opacity color paints a half-faded blit rather than a solid one.</param>
		/// <param name="destX">Destination x of the mask's left column.</param>
		/// <param name="destY">Destination y of the mask's <b>bottom</b> row - both the mask and
		/// <see cref="ImageBuffer"/> are Y-up, so mask row <c>my</c> lands on destination row
		/// <c>destY + my</c> with no flip.</param>
		/// <remarks>
		/// <b>The origin is integer on purpose.</b> Sub-pixel placement would smear each channel's phase
		/// across neighboring pixels and destroy the subpixel geometry, so a caller working in continuous
		/// coordinates must round (<c>sx.round()</c>, <c>sy.round()</c> in the reference's
		/// <c>draw_lcd_mask</c>) before calling this - unconditionally, independent of any
		/// baseline-snapping setting.
		/// <para>
		/// A mask hanging off the destination edge composites only the overlapping part; that is clipping,
		/// not an error.
		/// </para>
		/// </remarks>
		public static void Composite(ImageBuffer destination, LcdMask mask, Color source, int destX, int destY)
		{
			if (destination == null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			if (mask == null)
			{
				throw new ArgumentNullException(nameof(mask));
			}

			if (destination.BitDepth != 32)
			{
				throw new ArgumentException($"LcdComposite requires a 32 bit-per-pixel destination, was {destination.BitDepth}.", nameof(destination));
			}

			if (mask.Width == 0 || mask.Height == 0 || destination.Width == 0 || destination.Height == 0)
			{
				return;
			}

			// Color is byte-backed, so these are already inside 0..1 and the reference's clamp on the
			// source components cannot bite.
			float sourceAlpha = source.alpha / 255.0f;
			float sourceRed = source.red / 255.0f;
			float sourceGreen = source.green / 255.0f;
			float sourceBlue = source.blue / 255.0f;

			byte[] buffer = destination.GetBuffer();
			int bytesPerPixel = destination.GetBytesBetweenPixelsInclusive();
			int destWidth = destination.Width;
			int destHeight = destination.Height;

			for (int maskY = 0; maskY < mask.Height; maskY++)
			{
				int destinationY = destY + maskY;
				if (destinationY < 0 || destinationY >= destHeight)
				{
					continue;
				}

				// Hoisted per row: the x lookup table is linear, so this plus x * bytesPerPixel is exactly
				// GetBufferOffsetXY(x, destinationY), and it keeps a flipped or offset (sub-image) buffer
				// working.
				int destRowOffset = destination.GetBufferOffsetXY(0, destinationY);

				for (int maskX = 0; maskX < mask.Width; maskX++)
				{
					int destinationX = destX + maskX;
					if (destinationX < 0 || destinationX >= destWidth)
					{
						continue;
					}

					int maskOffset = mask.PixelOffset(maskX, maskY);
					float coverRed = (mask.Data[maskOffset] / 255.0f) * sourceAlpha;
					float coverGreen = (mask.Data[maskOffset + 1] / 255.0f) * sourceAlpha;
					float coverBlue = (mask.Data[maskOffset + 2] / 255.0f) * sourceAlpha;
					if (coverRed == 0.0f && coverGreen == 0.0f && coverBlue == 0.0f)
					{
						continue;
					}

					int destOffset = destRowOffset + (destinationX * bytesPerPixel);
					float destRed = buffer[destOffset + ImageBuffer.OrderR] / 255.0f;
					float destGreen = buffer[destOffset + ImageBuffer.OrderG] / 255.0f;
					float destBlue = buffer[destOffset + ImageBuffer.OrderB] / 255.0f;

					// Per-channel source-over, straight in sRGB. Linearizing first is the correct next
					// step (see the design doc); sRGB-direct matches the reference and FreeType's
					// non-linear mode.
					float blendedRed = (sourceRed * coverRed) + (destRed * (1.0f - coverRed));
					float blendedGreen = (sourceGreen * coverGreen) + (destGreen * (1.0f - coverGreen));
					float blendedBlue = (sourceBlue * coverBlue) + (destBlue * (1.0f - coverBlue));

					buffer[destOffset + ImageBuffer.OrderR] = ToByte(blendedRed);
					buffer[destOffset + ImageBuffer.OrderG] = ToByte(blendedGreen);
					buffer[destOffset + ImageBuffer.OrderB] = ToByte(blendedBlue);

					// Destination alpha untouched: a mask paints onto an existing surface without
					// introducing transparency of its own.
				}
			}
		}

		/// <summary>
		/// Quantizes a 0..1 channel to a byte, rounding half up then clamping - the reference's
		/// <c>(value * 255.0 + 0.5).clamp(0.0, 255.0) as u8</c>, in the same order (the truncating cast
		/// after the clamp is what makes 255.0 land on 255 rather than wrapping).
		/// </summary>
		private static byte ToByte(float value)
		{
			return (byte)Math.Clamp((value * 255.0f) + 0.5f, 0.0f, 255.0f);
		}
	}
}
