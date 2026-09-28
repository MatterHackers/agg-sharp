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
    public sealed class BlenderPolyColorPreMultBGRA : BlenderBase8888, IRecieveBlenderByte
	{
		private Color polyColor;

		public BlenderPolyColorPreMultBGRA(Color polyColor)
		{
			this.polyColor = polyColor;
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
		/// Scales the premultiplied color by the poly color's alpha, then blends it source-over by C++ rgba8's
		/// prelerp (p + q - multiply(p, a)), as BlenderPreMultBGRA does. Destination alpha is left as it was.
		/// The port used to add the color to a truncated (p * (255 - a) + 255) &gt;&gt; 8, which could land a level high.
		/// </summary>
		public void BlendPixel(byte[] pDestBuffer, int bufferOffset, Color sourceColor)
		{
			int polyAlpha = polyColor.Alpha0To255;
			int sourceA = Rgba8Math.Multiply(polyAlpha, sourceColor.alpha);
			pDestBuffer[bufferOffset + ImageBuffer.OrderR] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderR], Rgba8Math.Multiply(polyAlpha, sourceColor.red), sourceA);
			pDestBuffer[bufferOffset + ImageBuffer.OrderG] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderG], Rgba8Math.Multiply(polyAlpha, sourceColor.green), sourceA);
			pDestBuffer[bufferOffset + ImageBuffer.OrderB] = Prelerp(pDestBuffer[bufferOffset + ImageBuffer.OrderB], Rgba8Math.Multiply(polyAlpha, sourceColor.blue), sourceA);
		}

		// A straight-alpha color (a channel above its alpha) can sum past 255; saturate rather than wrap to a dark speck.
		private static byte Prelerp(int p, int q, int a)
		{
			return (byte)Math.Min(Rgba8Math.Prelerp(p, q, a), 255);
		}

		/// <summary>
		/// Blends each premultiplied color through <see cref="BlendPixel"/>; a partial cover scales all four
		/// channels, as a cover does to a premultiplied color. (This used to throw for any cover below 255.)
		/// </summary>
		public void BlendPixels(byte[] pDestBuffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			do
			{
				Color color = sourceColors[sourceColorsOffset++];
				int cover = firstCoverForAll ? sourceCovers[sourceCoversOffset] : sourceCovers[sourceCoversOffset++];
				if (cover != 255)
				{
					color = new Color(
						Rgba8Math.Multiply(color.red, cover),
						Rgba8Math.Multiply(color.green, cover),
						Rgba8Math.Multiply(color.blue, cover),
						Rgba8Math.Multiply(color.alpha, cover));
				}

				BlendPixel(pDestBuffer, bufferOffset, color);
				bufferOffset += 4;
			}
			while (--count != 0);
		}
	}
}
