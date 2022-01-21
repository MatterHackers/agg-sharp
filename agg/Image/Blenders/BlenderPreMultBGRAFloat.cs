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
using System;

namespace MatterHackers.Agg.Image
{
    public sealed class BlenderPreMultBGRAFloat : BlenderBaseBGRAFloat, IRecieveBlenderFloat
	{
		public ColorF PixelToColorRGBA_Floats(float[] buffer, int bufferOffset)
		{
			throw new NotImplementedException();
			//return new RGBA_(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
		}

		public void SetPixels(float[] buffer, int bufferOffset, ColorF sourceColor, int count)
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

		public void CopyPixels(float[] buffer, int bufferOffset, ColorF[] sourceColors, int sourceColorsOffset, int count)
		{
			throw new NotImplementedException();
		}

		public void CopyPixels(float[] buffer, int bufferOffset, ColorF sourceColor, int count)
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

		public void BlendPixel(float[] buffer, int bufferOffset, ColorF sourceColor)
		{
			if (sourceColor.alpha == 1)
			{
				buffer[bufferOffset + ImageBuffer.OrderR] = (byte)(sourceColor.red);
				buffer[bufferOffset + ImageBuffer.OrderG] = (byte)(sourceColor.green);
				buffer[bufferOffset + ImageBuffer.OrderB] = (byte)(sourceColor.blue);
				buffer[bufferOffset + ImageBuffer.OrderA] = (byte)(sourceColor.alpha);
			}
			else
			{
				float r = buffer[bufferOffset + ImageBuffer.OrderR];
				float g = buffer[bufferOffset + ImageBuffer.OrderG];
				float b = buffer[bufferOffset + ImageBuffer.OrderB];
				float a = buffer[bufferOffset + ImageBuffer.OrderA];
				buffer[bufferOffset + ImageBuffer.OrderR] = (sourceColor.red - r) * sourceColor.alpha + r;
				buffer[bufferOffset + ImageBuffer.OrderG] = (sourceColor.green - g) * sourceColor.alpha + g;
				buffer[bufferOffset + ImageBuffer.OrderB] = (sourceColor.blue - b) * sourceColor.alpha + b;
				buffer[bufferOffset + ImageBuffer.OrderA] = (sourceColor.alpha + a) - sourceColor.alpha * a;
			}
		}

		public void BlendPixels(float[] pDestBuffer, int bufferOffset,
			ColorF[] sourceColors, int sourceColorsOffset, int count)
		{
		}

		public void BlendPixels(float[] pDestBuffer, int bufferOffset,
			ColorF[] sourceColors, int sourceColorsOffset,
			byte sourceCovers, int count)
		{
		}

		public void BlendPixels(float[] pDestBuffer, int bufferOffset,
			ColorF[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, int count)
		{
		}

		public void BlendPixels(float[] pDestBuffer, int bufferOffset,
			ColorF[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			if (firstCoverForAll)
			{
				//unsafe
				{
					if (sourceCovers[sourceCoversOffset] == 255)
					{
						for (int i = 0; i < count; i++)
						{
							BlendPixel(pDestBuffer, bufferOffset, sourceColors[sourceColorsOffset]);
							sourceColorsOffset++;
							bufferOffset += 4;
						}
					}
					else
					{
						for (int i = 0; i < count; i++)
						{
							ColorF sourceColor = sourceColors[sourceColorsOffset];
							float alpha = (sourceColor.alpha * sourceCovers[sourceCoversOffset] + 255) / 256;
							if (alpha == 0)
							{
								continue;
							}
							else if (alpha == 255)
							{
								pDestBuffer[bufferOffset + ImageBuffer.OrderR] = (byte)sourceColor.red;
								pDestBuffer[bufferOffset + ImageBuffer.OrderG] = (byte)sourceColor.green;
								pDestBuffer[bufferOffset + ImageBuffer.OrderB] = (byte)sourceColor.blue;
								pDestBuffer[bufferOffset + ImageBuffer.OrderA] = (byte)alpha;
							}
							else
							{
								float OneOverAlpha = base_mask - alpha;
								unchecked
								{
									float r = pDestBuffer[bufferOffset + ImageBuffer.OrderR] * OneOverAlpha + sourceColor.red;
									float g = pDestBuffer[bufferOffset + ImageBuffer.OrderG] * OneOverAlpha + sourceColor.green;
									float b = pDestBuffer[bufferOffset + ImageBuffer.OrderB] * OneOverAlpha + sourceColor.blue;
									float a = pDestBuffer[bufferOffset + ImageBuffer.OrderA];
									pDestBuffer[bufferOffset + ImageBuffer.OrderR] = r;
									pDestBuffer[bufferOffset + ImageBuffer.OrderG] = g;
									pDestBuffer[bufferOffset + ImageBuffer.OrderB] = b;
									pDestBuffer[bufferOffset + ImageBuffer.OrderA] = (1.0f - ((OneOverAlpha * (1.0f - a))));
								}
							}
							sourceColorsOffset++;
							bufferOffset += 4;
						}
					}
				}
			}
			else
			{
				for (int i = 0; i < count; i++)
				{
					ColorF sourceColor = sourceColors[sourceColorsOffset];
					if (sourceColor.alpha == 1 && sourceCovers[sourceCoversOffset] == 255)
					{
						pDestBuffer[bufferOffset + ImageBuffer.OrderR] = sourceColor.red;
						pDestBuffer[bufferOffset + ImageBuffer.OrderG] = sourceColor.green;
						pDestBuffer[bufferOffset + ImageBuffer.OrderB] = sourceColor.blue;
						pDestBuffer[bufferOffset + ImageBuffer.OrderA] = 1;
					}
					else
					{
						// the cover is known to be less than opaque
						float coverFloat = (sourceCovers[sourceCoversOffset] * (1.0f / 255.0f));
						float alpha = sourceColor.alpha * coverFloat;
						if (coverFloat > 0 && alpha > 0)
						{
							float OneOverAlpha = 1.0f - alpha;
							unchecked
							{
								// the color is already pre multiplied by the alpha but not by the cover value so we only need to multiply the color by the cover
								float r = (pDestBuffer[bufferOffset + ImageBuffer.OrderR] * OneOverAlpha) + sourceColor.red * coverFloat;
								float g = (pDestBuffer[bufferOffset + ImageBuffer.OrderG] * OneOverAlpha) + sourceColor.green * coverFloat;
								float b = (pDestBuffer[bufferOffset + ImageBuffer.OrderB] * OneOverAlpha) + sourceColor.blue * coverFloat;

								float destAlpha = pDestBuffer[bufferOffset + ImageBuffer.OrderA];
								float a = (destAlpha + (1.0f - destAlpha) * sourceColor.alpha * coverFloat);
								pDestBuffer[bufferOffset + ImageBuffer.OrderR] = r;
								pDestBuffer[bufferOffset + ImageBuffer.OrderG] = g;
								pDestBuffer[bufferOffset + ImageBuffer.OrderB] = b;
								pDestBuffer[bufferOffset + ImageBuffer.OrderA] = a;
							}
						}
					}
					sourceColorsOffset++;
					sourceCoversOffset++;
					bufferOffset += 4;
				}
			}
		}
	}
}
