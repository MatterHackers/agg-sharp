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

namespace MatterHackers.Agg.Image
{
	//=======================================================pixfmt_transposer
	public sealed class FormatTransposer : ImageProxy
	{
		public FormatTransposer(IImageByte pixelFormat)
			: base(pixelFormat)
		{
		}

		public override int Width { get { return linkedImage.Height; } }

		public override int Height { get { return linkedImage.Width; } }

		public override RGBA_Bytes GetPixel(int x, int y)
		{
			return linkedImage.GetPixel(y, x);
		}

		public override void copy_pixel(int x, int y, byte[] c, int ByteOffset)
		{
			linkedImage.copy_pixel(y, x, c, ByteOffset);
		}

		public override void copy_hline(int x, int y, int len, RGBA_Bytes c)
		{
			linkedImage.copy_vline(y, x, len, c);
		}

		public override void copy_vline(int x, int y,
								   int len,
								   RGBA_Bytes c)
		{
			linkedImage.copy_hline(y, x, len, c);
		}

		public override void blend_hline(int x1, int y, int x2, RGBA_Bytes c, byte cover)
		{
			linkedImage.blend_vline(y, x1, x2, c, cover);
		}

		public override void blend_vline(int x, int y1, int y2, RGBA_Bytes c, byte cover)
		{
			linkedImage.blend_hline(y1, x, y2, c, cover);
		}

		public override void blend_solid_hspan(int x, int y, int len, RGBA_Bytes c, byte[] covers, int coversIndex)
		{
			linkedImage.blend_solid_vspan(y, x, len, c, covers, coversIndex);
		}

		public override void blend_solid_vspan(int x, int y, int len, RGBA_Bytes c, byte[] covers, int coversIndex)
		{
			linkedImage.blend_solid_hspan(y, x, len, c, covers, coversIndex);
		}

		public override void copy_color_hspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex)
		{
			linkedImage.copy_color_vspan(y, x, len, colors, colorsIndex);
		}

		public override void copy_color_vspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex)
		{
			linkedImage.copy_color_hspan(y, x, len, colors, colorsIndex);
		}

		public override void blend_color_hspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			linkedImage.blend_color_vspan(y, x, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public override void blend_color_vspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			linkedImage.blend_color_hspan(y, x, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}
	};
}