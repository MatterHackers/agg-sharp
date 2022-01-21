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
    public sealed class BlenderBGRAFloat : BlenderBaseBGRAFloat, IRecieveBlenderFloat
	{
		public ColorF PixelToColorRGBA_Floats(float[] buffer, int bufferOffset)
		{
			return new ColorF(buffer[bufferOffset + ImageBuffer.OrderR], buffer[bufferOffset + ImageBuffer.OrderG], buffer[bufferOffset + ImageBuffer.OrderB], buffer[bufferOffset + ImageBuffer.OrderA]);
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

		public void BlendPixels(float[] destBuffer, int bufferOffset,
			ColorF[] sourceColors, int sourceColorsOffset,
			byte[] covers, int coversIndex, bool firstCoverForAll, int count)
		{
			throw new NotImplementedException();
		}
	}
}
