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
// bounding_rect function template
//
//----------------------------------------------------------------------------
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	static public class bounding_rect
	{
		public static bool get_bounding_rect(PathStorage vs, int[] gi,
						   int start, int num,
						   out double x1, out double y1, out double x2, out double y2)
		{
			int i;
			double x = 0;
			double y = 0;
			bool first = true;

			x1 = 1;
			y1 = 1;
			x2 = 0;
			y2 = 0;

			for (i = 0; i < num; i++)
			{
				vs.rewind(gi[start + i]);
				ShapePath.FlagsAndCommand PathAndFlags;
				while (!ShapePath.is_stop(PathAndFlags = vs.vertex(out x, out y)))
				{
					if (ShapePath.is_vertex(PathAndFlags))
					{
						if (first)
						{
							x1 = x;
							y1 = y;
							x2 = x;
							y2 = y;
							first = false;
						}
						else
						{
							if (x < x1) x1 = x;
							if (y < y1) y1 = y;
							if (x > x2) x2 = x;
							if (y > y2) y2 = y;
						}
					}
				}
			}
			return x1 <= x2 && y1 <= y2;
		}

		public static bool get_bounding_rect(PathStorage vs, int[] gi,
						   int start, int num,
						   out RectangleDouble boundingRect)
		{
			return get_bounding_rect(vs, gi, start, num, out boundingRect.Left, out boundingRect.Bottom, out boundingRect.Right, out boundingRect.Top);
		}

		public static bool bounding_rect_single(IVertexSource vs, int path_id, ref RectangleDouble rect)
		{
			double x1, y1, x2, y2;
			bool rValue = bounding_rect_single(vs, path_id, out x1, out y1, out x2, out y2);
			rect.Left = x1;
			rect.Bottom = y1;
			rect.Right = x2;
			rect.Top = y2;
			return rValue;
		}

		//-----------------------------------------------------bounding_rect_single
		//template<class VertexSource, class CoordT>
		public static bool bounding_rect_single(IVertexSource vs, int path_id,
								  out double x1, out double y1, out double x2, out double y2)
		{
			double x = 0;
			double y = 0;
			bool first = true;

			x1 = 1;
			y1 = 1;
			x2 = 0;
			y2 = 0;

			vs.rewind(path_id);
			ShapePath.FlagsAndCommand PathAndFlags;
			while (!ShapePath.is_stop(PathAndFlags = vs.vertex(out x, out y)))
			{
				if (ShapePath.is_vertex(PathAndFlags))
				{
					if (first)
					{
						x1 = x;
						y1 = y;
						x2 = x;
						y2 = y;
						first = false;
					}
					else
					{
						if (x < x1) x1 = x;
						if (y < y1) y1 = y;
						if (x > x2) x2 = x;
						if (y > y2) y2 = y;
					}
				}
			}
			return x1 <= x2 && y1 <= y2;
		}
	}
}

//#endif