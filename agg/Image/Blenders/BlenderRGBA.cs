//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
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
    public sealed class BlenderRGBA : BlenderBase8888, IRecieveBlenderByte
	{
		public Color PixelToColor(byte[] buffer, int bufferOffset)
		{
			return new Color(buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderA]);
		}

		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			do
			{
				buffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.red;
				buffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
				buffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.blue;
				buffer[bufferOffset + ImageBuffer.OrderA] = sourceColor.alpha;
				bufferOffset += 4;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			//unsafe
			{
				unchecked
				{
					if (sourceColor.alpha == 255)
					{
						buffer[bufferOffset + ImageBuffer.OrderB] = (byte)(sourceColor.red);
						buffer[bufferOffset + ImageBuffer.OrderG] = (byte)(sourceColor.green);
						buffer[bufferOffset + ImageBuffer.OrderR] = (byte)(sourceColor.blue);
						buffer[bufferOffset + ImageBuffer.OrderA] = (byte)(sourceColor.alpha);
					}
					else
					{
						int r = buffer[bufferOffset + ImageBuffer.OrderB];
						int g = buffer[bufferOffset + ImageBuffer.OrderG];
						int b = buffer[bufferOffset + ImageBuffer.OrderR];
						int a = buffer[bufferOffset + ImageBuffer.OrderA];
						buffer[bufferOffset + ImageBuffer.OrderB] = (byte)(((sourceColor.red - r) * sourceColor.alpha + (r << (int)Color.base_shift)) >> (int)Color.base_shift);
						buffer[bufferOffset + ImageBuffer.OrderG] = (byte)(((sourceColor.green - g) * sourceColor.alpha + (g << (int)Color.base_shift)) >> (int)Color.base_shift);
						buffer[bufferOffset + ImageBuffer.OrderR] = (byte)(((sourceColor.blue - b) * sourceColor.alpha + (b << (int)Color.base_shift)) >> (int)Color.base_shift);
						buffer[bufferOffset + ImageBuffer.OrderA] = (byte)((sourceColor.alpha + a) - ((sourceColor.alpha * a + base_mask) >> (int)Color.base_shift));
					}
				}
			}
		}

		public void BlendPixels(byte[] destBuffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] covers, int coversIndex, bool firstCoverForAll, int count)
		{
			if (firstCoverForAll)
			{
				int cover = covers[coversIndex];
				if (cover == 255)
				{
					do
					{
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset++]);
						bufferOffset += 4;
					}
					while (--count != 0);
				}
				else
				{
					do
					{
						sourceColors[sourceColorsOffset].alpha = (byte)((sourceColors[sourceColorsOffset].alpha * cover + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
						bufferOffset += 4;
						++sourceColorsOffset;
					}
					while (--count != 0);
				}
			}
			else
			{
				do
				{
					int cover = covers[coversIndex++];
					if (cover == 255)
					{
						BlendPixel(destBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
					}
					else
					{
						Color color = sourceColors[sourceColorsOffset];
						color.alpha = (byte)((color.alpha * (cover) + 255) >> 8);
						BlendPixel(destBuffer, bufferOffset, color);
					}
					bufferOffset += 4;
					++sourceColorsOffset;
				}
				while (--count != 0);
			}
		}
	}
}
