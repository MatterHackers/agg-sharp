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
    public sealed class BlenderPolyColorPreMultBGRA : BlenderBase8888, IRecieveBlenderByte
	{
		private static int[] m_Saturate9BitToByte = new int[1 << 9];
		private Color polyColor;

		public BlenderPolyColorPreMultBGRA(Color polyColor)
		{
			this.polyColor = polyColor;

			if (m_Saturate9BitToByte[2] == 0)
			{
				for (int i = 0; i < m_Saturate9BitToByte.Length; i++)
				{
					m_Saturate9BitToByte[i] = Math.Min(i, 255);
				}
			}
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

		public void BlendPixel(byte[] pDestBuffer, int bufferOffset, Color sourceColor)
		{
			//unsafe
			{
				int sourceA = (byte)(m_Saturate9BitToByte[(polyColor.Alpha0To255 * sourceColor.alpha + 255) >> 8]);
				int oneOverAlpha = base_mask - sourceA;
				unchecked
				{
					int sourceR = (byte)(m_Saturate9BitToByte[(polyColor.Alpha0To255 * sourceColor.red + 255) >> 8]);
					int sourceG = (byte)(m_Saturate9BitToByte[(polyColor.Alpha0To255 * sourceColor.green + 255) >> 8]);
					int sourceB = (byte)(m_Saturate9BitToByte[(polyColor.Alpha0To255 * sourceColor.blue + 255) >> 8]);

					int destR = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderR] * oneOverAlpha + 255) >> 8) + sourceR];
					int destG = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderG] * oneOverAlpha + 255) >> 8) + sourceG];
					int destB = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderB] * oneOverAlpha + 255) >> 8) + sourceB];
					// TODO: calculated the correct dest alpha
					//int destA = pDestBuffer[bufferOffset + ImageBuffer.OrderA];

					pDestBuffer[bufferOffset + ImageBuffer.OrderR] = (byte)destR;
					pDestBuffer[bufferOffset + ImageBuffer.OrderG] = (byte)destG;
					pDestBuffer[bufferOffset + ImageBuffer.OrderB] = (byte)destB;
					//pDestBuffer[bufferOffset + ImageBuffer.OrderA] = (byte)(base_mask - m_Saturate9BitToByte[(oneOverAlpha * (base_mask - a) + 255) >> 8]);
				}
			}
		}

		public void BlendPixels(byte[] pDestBuffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
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
						throw new NotImplementedException("need to consider the polyColor");
#if false
                        for (int i = 0; i < count; i++)
                        {
                            RGBA_Bytes sourceColor = sourceColors[sourceColorsOffset];
                            int alpha = (sourceColor.alpha * sourceCovers[sourceCoversOffset] + 255) / 256;
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
                                int OneOverAlpha = base_mask - alpha;
                                unchecked
                                {
                                    int r = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderR] * OneOverAlpha + 255) >> 8) + sourceColor.red];
                                    int g = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderG] * OneOverAlpha + 255) >> 8) + sourceColor.green];
                                    int b = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderB] * OneOverAlpha + 255) >> 8) + sourceColor.blue];
                                    int a = pDestBuffer[bufferOffset + ImageBuffer.OrderA];
                                    pDestBuffer[bufferOffset + ImageBuffer.OrderR] = (byte)r;
                                    pDestBuffer[bufferOffset + ImageBuffer.OrderG] = (byte)g;
                                    pDestBuffer[bufferOffset + ImageBuffer.OrderB] = (byte)b;
                                    pDestBuffer[bufferOffset + ImageBuffer.OrderA] = (byte)(base_mask - m_Saturate9BitToByte[(OneOverAlpha * (base_mask - a) + 255) >> 8]);
                                }
                            }
                            sourceColorsOffset++;
                            bufferOffset += 4;
                        }
#endif
					}
				}
			}
			else
			{
				throw new NotImplementedException("need to consider the polyColor");
#if false
                for (int i = 0; i < count; i++)
                {
                    RGBA_Bytes sourceColor = sourceColors[sourceColorsOffset];
                    int alpha = (sourceColor.alpha * sourceCovers[sourceCoversOffset] + 255) / 256;
                    if (alpha == 255)
                    {
                        pDestBuffer[bufferOffset + ImageBuffer.OrderR] = (byte)sourceColor.red;
                        pDestBuffer[bufferOffset + ImageBuffer.OrderG] = (byte)sourceColor.green;
                        pDestBuffer[bufferOffset + ImageBuffer.OrderB] = (byte)sourceColor.blue;
                        pDestBuffer[bufferOffset + ImageBuffer.OrderA] = (byte)alpha;
                    }
                    else if (alpha > 0)
                    {
                        int OneOverAlpha = base_mask - alpha;
                        unchecked
                        {
                            int r = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderR] * OneOverAlpha + 255) >> 8) + sourceColor.red];
                            int g = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderG] * OneOverAlpha + 255) >> 8) + sourceColor.green];
                            int b = m_Saturate9BitToByte[((pDestBuffer[bufferOffset + ImageBuffer.OrderB] * OneOverAlpha + 255) >> 8) + sourceColor.blue];
                            int a = pDestBuffer[bufferOffset + ImageBuffer.OrderA];
                            pDestBuffer[bufferOffset + ImageBuffer.OrderR] = (byte)r;
                            pDestBuffer[bufferOffset + ImageBuffer.OrderG] = (byte)g;
                            pDestBuffer[bufferOffset + ImageBuffer.OrderB] = (byte)b;
                            pDestBuffer[bufferOffset + ImageBuffer.OrderA] = (byte)(base_mask - m_Saturate9BitToByte[(OneOverAlpha * (base_mask - a) + 255) >> 8]);
                        }
                    }
                    sourceColorsOffset++;
                    sourceCoversOffset++;
                    bufferOffset += 4;
                }
#endif
			}
		}
	}
}
