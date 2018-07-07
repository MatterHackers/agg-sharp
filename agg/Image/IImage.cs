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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Image
{
	public interface IImage
	{
		Vector2 OriginOffset
		{
			get;
			set;
		}

		int BitDepth { get; }

		int Width { get; }

		int Height { get; }

		RectangleInt GetBounds();

		int GetBufferOffsetY(int y);

		int GetBufferOffsetXY(int x, int y);

		Graphics2D NewGraphics2D();

		void MarkImageChanged();
	}

	public interface IImageByte : IImage
	{
		int StrideInBytes();

		int StrideInBytesAbs();

		IRecieveBlenderByte GetRecieveBlender();

		void SetRecieveBlender(IRecieveBlenderByte value);

		int GetBytesBetweenPixelsInclusive();

		byte[] GetBuffer();

		Color GetPixel(int x, int y);

		void copy_pixel(int x, int y, byte[] c, int ByteOffset);

		void CopyFrom(IImageByte sourceImage);

		void CopyFrom(IImageByte sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset);

		void SetPixel(int x, int y, Color color);

		void BlendPixel(int x, int y, Color sourceColor, byte cover);

		// line stuff
		void copy_hline(int x, int y, int len, Color sourceColor);

		void copy_vline(int x, int y, int len, Color sourceColor);

		void blend_hline(int x, int y, int x2, Color sourceColor, byte cover);

		void blend_vline(int x, int y1, int y2, Color sourceColor, byte cover);

		// color stuff
		void copy_color_hspan(int x, int y, int len, Color[] colors, int colorIndex);

		void copy_color_vspan(int x, int y, int len, Color[] colors, int colorIndex);

		void blend_solid_hspan(int x, int y, int len, Color sourceColor, byte[] covers, int coversIndex);

		void blend_solid_vspan(int x, int y, int len, Color sourceColor, byte[] covers, int coversIndex);

		void blend_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll);

		void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll);
	}

	public interface IImageFloat : IImage
	{
		int StrideInFloats();

		int StrideInFloatsAbs();

		IRecieveBlenderFloat GetBlender();

		void SetRecieveBlender(IRecieveBlenderFloat value);

		int GetFloatsBetweenPixelsInclusive();

		float[] GetBuffer();

		ColorF GetPixel(int x, int y);

		void copy_pixel(int x, int y, float[] c, int floatOffset);

		void CopyFrom(IImageFloat sourceImage);

		void CopyFrom(IImageFloat sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset);

		void SetPixel(int x, int y, ColorF color);

		void BlendPixel(int x, int y, ColorF sourceColor, byte cover);

		// line stuff
		void copy_hline(int x, int y, int len, ColorF sourceColor);

		void copy_vline(int x, int y, int len, ColorF sourceColor);

		void blend_hline(int x, int y, int x2, ColorF sourceColor, byte cover);

		void blend_vline(int x, int y1, int y2, ColorF sourceColor, byte cover);

		// color stuff
		void copy_color_hspan(int x, int y, int len, ColorF[] colors, int colorIndex);

		void copy_color_vspan(int x, int y, int len, ColorF[] colors, int colorIndex);

		void blend_solid_hspan(int x, int y, int len, ColorF sourceColor, byte[] covers, int coversIndex);

		void blend_solid_vspan(int x, int y, int len, ColorF sourceColor, byte[] covers, int coversIndex);

		void blend_color_hspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll);

		void blend_color_vspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll);
	}
}