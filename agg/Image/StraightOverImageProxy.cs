//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026, Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// A 32 bit <see cref="ImageBuffer"/> whose solid blends use <see cref="BlenderStraightOverBGRA"/> whatever the
	/// buffer's own blender is - without touching the buffer, so nothing else drawing into it is affected. Everything
	/// else (copies, colour spans, reads) goes to the buffer as usual.
	/// </summary>
	/// <remarks>
	/// ImageGraphics2D puts it under a clipping proxy for a solid fill onto a buffer labelled premultiplied that
	/// holds straight colour (see ImageGraphics2D.StraightOverDestination). Offsets match ImageBuffer's own solid
	/// blends: raw buffer pixels, with no <see cref="ImageBuffer.OriginOffset"/> applied.
	/// </remarks>
	internal sealed class StraightOverImageProxy : ImageProxy
	{
		private const int BytesPerPixel = 4;

		private static readonly BlenderStraightOverBGRA Blender = BlenderStraightOverBGRA.Instance;

		private readonly ImageBuffer buffer;

		public StraightOverImageProxy(ImageBuffer buffer)
			: base(buffer)
		{
			this.buffer = buffer;
		}

		public override void BlendPixel(int x, int y, Color sourceColor, byte cover)
		{
			Blend(buffer.GetBufferOffsetXY(x, y), sourceColor, cover);
		}

		public override void blend_hline(int x1, int y, int x2, Color sourceColor, byte cover)
		{
			int offset = buffer.GetBufferOffsetXY(x1, y);

			// An opaque run at full cover is one copy, as on ImageBuffer.
			if (x2 >= x1 && sourceColor.alpha == 255 && cover == 255)
			{
				Blender.CopyPixels(buffer.GetBuffer(), offset, sourceColor, x2 - x1 + 1);
				return;
			}

			for (int x = x1; x <= x2; x++)
			{
				Blend(offset, sourceColor, cover);
				offset += BytesPerPixel;
			}
		}

		public override void blend_vline(int x, int y1, int y2, Color sourceColor, byte cover)
		{
			int offset = buffer.GetBufferOffsetXY(x, y1);
			for (int y = y1; y <= y2; y++)
			{
				Blend(offset, sourceColor, cover);
				offset += buffer.StrideInBytes();
			}
		}

		public override void blend_solid_hspan(int x, int y, int len, Color c, byte[] covers, int coversIndex)
		{
			int offset = buffer.GetBufferOffsetXY(x, y);
			for (int i = 0; i < len; i++)
			{
				Blend(offset, c, covers[coversIndex++]);
				offset += BytesPerPixel;
			}
		}

		public override void blend_solid_vspan(int x, int y, int len, Color c, byte[] covers, int coversIndex)
		{
			int offset = buffer.GetBufferOffsetXY(x, y);
			for (int i = 0; i < len; i++)
			{
				Blend(offset, c, covers[coversIndex++]);
				offset += buffer.StrideInBytes();
			}
		}

		/// <summary>ImageBuffer.BlendSolid with the straight-over blender: copy opaque at full cover, skip transparent.</summary>
		private void Blend(int offset, Color c, int cover)
		{
			if (c.alpha == 0 || cover == 0)
			{
				return;
			}

			if (c.alpha == 255 && cover == 255)
			{
				Blender.CopyPixels(buffer.GetBuffer(), offset, c, 1);
				return;
			}

			BlenderStraightOverBGRA.Blend(buffer.GetBuffer(), offset, c, Rgba8Math.Multiply(c.alpha, cover));
		}
	}
}
