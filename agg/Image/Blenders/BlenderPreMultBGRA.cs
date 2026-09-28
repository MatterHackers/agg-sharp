//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007, 2026 Lars Brubaker
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
using System;

namespace MatterHackers.Agg.Image
{
    public sealed class BlenderPreMultBGRA : BlenderBase8888, IRecieveBlenderByte
	{
		public BlenderPreMultBGRA()
		{
		}

		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			return new Color(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
		}

		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			for (int i = 0; i < count; i++)
			{
				buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
				buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
				buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
				buffer[bufferOffset + ImageBuffer.OrderA] = sourceColor.alpha;
				bufferOffset += 4;
			}
		}

		/// <summary>
		/// Full-cover source-over of a premultiplied color: the same prelerp as a partial cover. The port used to add
		/// the color to a truncated (p * (255 - a) + 255) &gt;&gt; 8, which could land a level high (gray 9 under
		/// half-transparent black gave 5, not 4).
		/// </summary>
		public void BlendPixel(byte[] pDestBuffer, int bufferOffset, Color sourceColor)
		{
			BlendPixelWithCover(pDestBuffer, bufferOffset, sourceColor, 255);
		}

		public void BlendPixels(byte[] pDestBuffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			if (firstCoverForAll)
			{
				if (sourceCovers[sourceCoversOffset] == 255)
				{
					for (int i = 0; i < count; i++)
					{
						Color sourceColor = sourceColors[sourceColorsOffset];
						if (sourceColor.alpha == 255)
						{
							CopyOpaquePixel(pDestBuffer, bufferOffset, sourceColor);
						}
						else
						{
							BlendPixel(pDestBuffer, bufferOffset, sourceColor);
						}

						sourceColorsOffset++;
						bufferOffset += 4;
					}
				}
				else
				{
					for (int i = 0; i < count; i++)
					{
						// A fully transparent pixel writes nothing, but the offsets must still
						// advance or the loop re-reads the same source pixel and the rest of
						// the span is never drawn.
						BlendPixelWithCover(pDestBuffer, bufferOffset, sourceColors[sourceColorsOffset], sourceCovers[sourceCoversOffset]);
						sourceColorsOffset++;
						bufferOffset += 4;
					}
				}
			}
			else
			{
				for (int i = 0; i < count; i++)
				{
					int cover = sourceCovers[sourceCoversOffset];
					Color sourceColor = sourceColors[sourceColorsOffset];
					if (cover == 255)
					{
						if (sourceColor.alpha == 255)
						{
							CopyOpaquePixel(pDestBuffer, bufferOffset, sourceColor);
						}
						else if (sourceColor.alpha != 0)
						{
							BlendPixel(pDestBuffer, bufferOffset, sourceColor);
						}
					}
					else
					{
						BlendPixelWithCover(pDestBuffer, bufferOffset, sourceColor, cover);
					}

					sourceColorsOffset++;
					sourceCoversOffset++;
					bufferOffset += 4;
				}
			}
		}

		/// <summary>
		/// C++ blender_rgba_pre::blend_pix with a cover below full: color and alpha are scaled by the cover
		/// (mult_cover), then each channel is prelerp(p, q, a) = p + q - multiply(p, a). The port used to test the
		/// covered alpha and then blend (or copy) the uncovered color, so a partial cover of an opaque color
		/// drew it at full strength and anti-aliased image edges came out hard.
		/// </summary>
		private static void BlendPixelWithCover(byte[] pDestBuffer, int bufferOffset, Color sourceColor, int cover)
		{
			if (sourceColor.alpha == 0)
			{
				return;
			}

			int alpha = Rgba8Math.Multiply(sourceColor.alpha, cover);
			pDestBuffer[bufferOffset + ImageBuffer.OrderR] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderR], Rgba8Math.Multiply(sourceColor.red, cover), alpha);
			pDestBuffer[bufferOffset + ImageBuffer.OrderG] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderG], Rgba8Math.Multiply(sourceColor.green, cover), alpha);
			pDestBuffer[bufferOffset + ImageBuffer.OrderB] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderB], Rgba8Math.Multiply(sourceColor.blue, cover), alpha);
			pDestBuffer[bufferOffset + ImageBuffer.OrderA] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderA], alpha, alpha);
		}

		// C++ rgba8::prelerp. Valid premultiplied input (q <= a) never exceeds 255, but a straight-alpha color can;
		// C++'s value_type cast wraps it to a dark speck, so saturate instead.
		private static byte Prelerp(int p, int q, int a)
		{
			return (byte)Math.Min(Rgba8Math.Prelerp(p, q, a), 255);
		}

		private static void CopyOpaquePixel(byte[] pDestBuffer, int bufferOffset, Color sourceColor)
		{
			pDestBuffer[bufferOffset + ImageBuffer.OrderR] = (byte)sourceColor.red;
			pDestBuffer[bufferOffset + ImageBuffer.OrderG] = (byte)sourceColor.green;
			pDestBuffer[bufferOffset + ImageBuffer.OrderB] = (byte)sourceColor.blue;
			pDestBuffer[bufferOffset + ImageBuffer.OrderA] = 255;
		}
	}
}
