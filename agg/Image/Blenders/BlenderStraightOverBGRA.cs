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
	/// C++ AGG's <c>blender_rgba_plain</c>: true straight-alpha source-over, which takes the destination's alpha into
	/// account. Over a fully transparent pixel it stores the source as (c, a); over an opaque one it is the ordinary
	/// straight lerp. <see cref="BlenderBGRA"/> (<c>blender_rgba</c>) ignores the destination alpha and stores
	/// (c * a, a) over transparent, which a straight reader then darkens.
	/// </summary>
	/// <remarks>
	/// Only <see cref="ImageGraphics2D"/> uses it, for a solid fill onto a buffer labelled
	/// <see cref="BlenderPreMultBGRA"/> that holds straight colour (see ImageGraphics2D.StraightOverDestination, through StraightOverImageProxy).
	/// </remarks>
	internal sealed class BlenderStraightOverBGRA : BlenderBase8888, IRecieveBlenderByte
	{
		public static readonly BlenderStraightOverBGRA Instance = new BlenderStraightOverBGRA();

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

		public void BlendPixel(byte[] buffer, int bufferOffset, Color sourceColor)
		{
			Blend(buffer, bufferOffset, sourceColor, sourceColor.alpha);
		}

		public void BlendPixels(byte[] buffer, int bufferOffset, Color[] sourceColors, int sourceColorsOffset, byte[] sourceCovers, int sourceCoversOffset, bool firstCoverForAll, int count)
		{
			for (int i = 0; i < count; i++)
			{
				Color color = sourceColors[sourceColorsOffset++];
				int cover = firstCoverForAll ? sourceCovers[sourceCoversOffset] : sourceCovers[sourceCoversOffset++];
				Blend(buffer, bufferOffset, color, Rgba8Math.Multiply(color.alpha, cover));
				bufferOffset += 4;
			}
		}

		/// <summary>blender_rgba_plain::blend_pix with <paramref name="alpha"/> already scaled by the cover.</summary>
		internal static void Blend(byte[] p, int offset, Color c, int alpha)
		{
			if (alpha == 0)
			{
				return;
			}

			int a = p[offset + ImageBuffer.OrderA];
			int r = p[offset + ImageBuffer.OrderR] * a;
			int g = p[offset + ImageBuffer.OrderG] * a;
			int b = p[offset + ImageBuffer.OrderB] * a;
			a = ((alpha + a) << 8) - alpha * a;
			p[offset + ImageBuffer.OrderA] = (byte)(a >> 8);
			p[offset + ImageBuffer.OrderR] = (byte)((((c.red << 8) - r) * alpha + (r << 8)) / a);
			p[offset + ImageBuffer.OrderG] = (byte)((((c.green << 8) - g) * alpha + (g << 8)) / a);
			p[offset + ImageBuffer.OrderB] = (byte)((((c.blue << 8) - b) * alpha + (b << 8)) / a);
		}
	}
}
