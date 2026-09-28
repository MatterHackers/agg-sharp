//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026 Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ span_pattern_rgba: copies the source's pixels one for one, unfiltered and untransformed, with the span's
	/// (x, y) shifted by the offsets - a pattern fill, tiled when the accessor wraps
	/// (<see cref="ImageBufferAccessorWrap"/>). The pixels are taken as they are stored, so a premultiplied
	/// pattern wants a premultiplied blender.
	/// </summary>
	public class span_pattern_rgba : ISpanGenerator
	{
		private readonly IImageBufferAccessor source;

		public span_pattern_rgba(IImageBufferAccessor source, int offsetX, int offsetY)
		{
			this.source = source;
			this.OffsetX = offsetX;
			this.OffsetY = offsetY;
		}

		public int OffsetX { get; set; }

		public int OffsetY { get; set; }

		/// <summary>
		/// C++'s alpha(): kept so the rgba, rgb and gray pattern generators share an interface, but an rgba pattern
		/// has its own alpha, so this one ignores it (as C++ does).
		/// </summary>
		public byte Alpha { get; set; }

		public void prepare()
		{
		}

		public void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			byte[] buffer = this.source.span(x + this.OffsetX, y + this.OffsetY, len, out int offset);
			do
			{
				span[spanIndex].red = buffer[offset + ImageBuffer.OrderR];
				span[spanIndex].green = buffer[offset + ImageBuffer.OrderG];
				span[spanIndex].blue = buffer[offset + ImageBuffer.OrderB];
				span[spanIndex].alpha = buffer[offset + ImageBuffer.OrderA];
				buffer = this.source.next_x(out offset);
				spanIndex++;
			}
			while (--len != 0);
		}
	}
}
