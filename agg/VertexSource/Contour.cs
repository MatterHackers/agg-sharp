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
// conv_stroke
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.VertexSource
{
	public sealed class Contour : VertexSourceAdapter
	{
		public Contour(IVertexSource vertexSource) :
			base(vertexSource, new ContourGenerator())
		{
		}

		public void line_join(LineJoin lj)
		{
			base.GetGenerator().line_join(lj);
		}

		public void inner_join(InnerJoin ij)
		{
			base.GetGenerator().inner_join(ij);
		}

		public void width(double w)
		{
			base.GetGenerator().width(w);
		}

		public void miter_limit(double ml)
		{
			base.GetGenerator().miter_limit(ml);
		}

		public void miter_limit_theta(double t)
		{
			base.GetGenerator().miter_limit_theta(t);
		}

		public void inner_miter_limit(double ml)
		{
			base.GetGenerator().inner_miter_limit(ml);
		}

		public void approximation_scale(double approxScale)
		{
			base.GetGenerator().approximation_scale(approxScale);
		}

		public void auto_detect_orientation(bool v)
		{
			base.GetGenerator().auto_detect_orientation(v);
		}

		public LineJoin line_join()
		{
			return base.GetGenerator().line_join();
		}

		public InnerJoin inner_join()
		{
			return base.GetGenerator().inner_join();
		}

		public double width()
		{
			return base.GetGenerator().width();
		}

		public double miter_limit()
		{
			return base.GetGenerator().miter_limit();
		}

		public double inner_miter_limit()
		{
			return base.GetGenerator().inner_miter_limit();
		}

		public double approximation_scale()
		{
			return base.GetGenerator().approximation_scale();
		}

		public bool auto_detect_orientation()
		{
			return base.GetGenerator().auto_detect_orientation();
		}
	}
}