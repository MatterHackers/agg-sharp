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
    public interface IRecieveBlenderFloat
	{
		int NumPixelBits { get; }

		ColorF PixelToColorRGBA_Floats(float[] buffer, int bufferOffset);

		void CopyPixels(float[] buffer, int bufferOffset, ColorF sourceColor, int count);

		void BlendPixel(float[] buffer, int bufferOffset, ColorF sourceColor);

		void BlendPixels(float[] buffer, int bufferOffset,
			ColorF[] sourceColors, int sourceColorsOffset,
			byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count);
	}
}
