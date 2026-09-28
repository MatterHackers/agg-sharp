//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ AGG's <c>blender_rgb_gamma</c> (the blender of <c>pixfmt_bgr24_gamma</c>) on a 32-bit BGRA buffer:
	/// a partly covered or translucent color is blended in the LUT's linear space,
	/// <c>inv(downscale((dir(c) - dir(p)) * alpha) + dir(p))</c>, an opaque color at full cover is copied as
	/// is, and a color whose alpha (after its cover) is 0 leaves the pixel alone. Like C++'s 24-bit pixel
	/// format it never touches the destination alpha on a blend.
	/// </summary>
	public sealed class BlenderGammaBGRA : BlenderBase8888, IRecieveBlenderByte
	{
		private GammaLookUpTable m_gamma;

		public BlenderGammaBGRA()
		{
			m_gamma = new GammaLookUpTable();
		}

		public BlenderGammaBGRA(GammaLookUpTable g)
		{
			m_gamma = g;
		}

		public void gamma(GammaLookUpTable g)
		{
			m_gamma = g;
		}

		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			return new Color(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
		}

		/// <summary>C++ <c>copy_hline</c>: the color is written as is - the gamma only applies to blending.</summary>
		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			do
			{
				buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
				buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
				buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
				buffer[bufferOffset + ImageBuffer.OrderA] = sourceColor.alpha;
				bufferOffset += 4;
			}
			while (--count != 0);
		}

		/// <summary>
		/// C++ <c>blend_pix</c> with the cover already multiplied into the alpha; an alpha of 255 is copied and an
		/// alpha of 0 leaves the pixel alone.
		/// </summary>
		/// <remarks>
		/// C++ bug fixed: C++ skips only a color transparent before its cover (<c>is_transparent</c>), so a cover that
		/// brings the alpha to 0 still blends and writes inv(dir(p)) - a lossy LUT round trip that darkens dark
		/// pixels (gamma 2 turns 10 into 0). A zero blend is a no-op here, and the C++ reference renderer is patched
		/// the same way (tools/cpp-renderer/patches/agg_pixfmt_rgb.h).
		/// </remarks>
		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			int alpha = sourceColor.alpha;
			if (alpha == base_mask)
			{
				CopyPixels(buffer, bufferOffset, sourceColor, 1);
				return;
			}

			if (alpha == 0)
			{
				return;
			}

			buffer[bufferOffset + ImageBuffer.OrderR] = BlendChannel(buffer[bufferOffset + ImageBuffer.OrderR], sourceColor.red, alpha);
			buffer[bufferOffset + ImageBuffer.OrderG] = BlendChannel(buffer[bufferOffset + ImageBuffer.OrderG], sourceColor.green, alpha);
			buffer[bufferOffset + ImageBuffer.OrderB] = BlendChannel(buffer[bufferOffset + ImageBuffer.OrderB], sourceColor.blue, alpha);
		}

		public void BlendPixels(byte[] buffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			do
			{
				Color color = sourceColors[sourceColorsOffset++];
				int cover = firstCoverForAll ? sourceCovers[sourceCoversOffset] : sourceCovers[sourceCoversOffset++];

				// C++ copy_or_blend_pix skips a transparent color; BlendPixel skips one its cover made transparent.
				if (color.alpha != 0)
				{
					if (cover != 255)
					{
						color.alpha = (byte)Rgba8Math.Multiply(color.alpha, cover);
					}

					BlendPixel(buffer, bufferOffset, color);
				}
				bufferOffset += 4;
			}
			while (--count != 0);
		}

		// C++ does this in unsigned calc_type: a negative difference wraps, and after ">> 8", "+ p" and the
		// int8u argument of inv() it comes out as the arithmetic floor would. The uint arithmetic is kept so
		// the port reads as the C++ does.
		private byte BlendChannel(byte destination, byte source, int alpha)
		{
			unchecked
			{
				uint p = m_gamma.dir(destination);
				uint blended = (((m_gamma.dir(source) - p) * (uint)alpha) >> 8) + p;
				return m_gamma.inv((byte)blended);
			}
		}
	}
}
