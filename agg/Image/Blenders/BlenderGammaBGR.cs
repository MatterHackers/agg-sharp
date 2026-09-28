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
	/// C++ AGG's <c>blender_rgb_gamma</c> (the blender of <c>pixfmt_bgr24_gamma</c>) on a 24-bit BGR buffer, the
	/// same blend as <see cref="BlenderGammaBGRA"/>: a partly covered or translucent color is blended in the
	/// LUT's linear space, <c>inv(downscale((dir(c) - dir(p)) * alpha) + dir(p))</c>, an opaque color at full
	/// cover is copied as is, and a color whose alpha (after its cover) is 0 leaves the pixel alone.
	/// </summary>
	public sealed class BlenderGammaBGR : BlenderBaseBGR, IRecieveBlenderByte
	{
		private GammaLookUpTable m_gamma;

		public BlenderGammaBGR()
		{
			m_gamma = new GammaLookUpTable();
		}

		public BlenderGammaBGR(GammaLookUpTable g)
		{
			m_gamma = g;
		}

		public void gamma(GammaLookUpTable g)
		{
			m_gamma = g;
		}

		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			return new Color(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], 255);
		}

		/// <summary>C++ <c>copy_hline</c>: the color is written as is - the gamma only applies to blending.</summary>
		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			do
			{
				buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
				buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
				buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
				bufferOffset += 3;
			}
			while (--count != 0);
		}

		/// <summary>
		/// C++ <c>blend_pix</c> with the cover already multiplied into the alpha; an alpha of 255 is copied and an
		/// alpha of 0 leaves the pixel alone.
		/// </summary>
		/// <remarks>
		/// C++ bug fixed, as in <see cref="BlenderGammaBGRA"/>: C++ still blends an alpha its cover brought to 0,
		/// writing inv(dir(p)), a lossy LUT round trip that darkens dark pixels.
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

		/// <summary>C++ <c>copy_or_blend_pix</c> per color: a transparent color is skipped, the rest go to BlendPixel.</summary>
		public void BlendPixels(byte[] buffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			do
			{
				Color color = sourceColors[sourceColorsOffset++];
				int cover = firstCoverForAll ? sourceCovers[sourceCoversOffset] : sourceCovers[sourceCoversOffset++];
				if (color.alpha != 0)
				{
					if (cover != 255)
					{
						color.alpha = (byte)Rgba8Math.Multiply(color.alpha, cover);
					}

					BlendPixel(buffer, bufferOffset, color);
				}

				bufferOffset += 3;
			}
			while (--count != 0);
		}

		// C++ does this in unsigned calc_type; see BlenderGammaBGRA.BlendChannel.
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
