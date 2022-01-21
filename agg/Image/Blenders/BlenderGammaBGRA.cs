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

		public void CopyPixels(byte[] buffer, int bufferOffset, Color sourceColor, int count)
		{
			do
			{
				buffer[bufferOffset + ImageBuffer.OrderR] = m_gamma.inv(sourceColor.red);
				buffer[bufferOffset + ImageBuffer.OrderG] = m_gamma.inv(sourceColor.green);
				buffer[bufferOffset + ImageBuffer.OrderB] = m_gamma.inv(sourceColor.blue);
				buffer[bufferOffset + ImageBuffer.OrderA] = m_gamma.inv(sourceColor.alpha);
				bufferOffset += 4;
			}
			while (--count != 0);
		}

		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			unchecked
			{
				int r = buffer[bufferOffset + ImageBuffer.OrderR];
				int g = buffer[bufferOffset + ImageBuffer.OrderG];
				int b = buffer[bufferOffset + ImageBuffer.OrderB];
				int a = buffer[bufferOffset + ImageBuffer.OrderA];
				buffer[bufferOffset + ImageBuffer.OrderR] = m_gamma.inv((byte)(((sourceColor.red - r) * sourceColor.alpha + (r << (int)Color.base_shift)) >> (int)Color.base_shift));
				buffer[bufferOffset + ImageBuffer.OrderG] = m_gamma.inv((byte)(((sourceColor.green - g) * sourceColor.alpha + (g << (int)Color.base_shift)) >> (int)Color.base_shift));
				buffer[bufferOffset + ImageBuffer.OrderB] = m_gamma.inv((byte)(((sourceColor.blue - b) * sourceColor.alpha + (b << (int)Color.base_shift)) >> (int)Color.base_shift));
				buffer[ImageBuffer.OrderA] = (byte)((sourceColor.alpha + a) - ((sourceColor.alpha * a + base_mask) >> (int)Color.base_shift));
			}
		}

		public void BlendPixels(byte[] buffer, int bufferOffset,
			Color[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			throw new NotImplementedException();
		}
	}
}
