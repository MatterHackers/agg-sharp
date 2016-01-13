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
	public sealed class Stroke : VertexSourceAdapter
	{
		public Stroke(IVertexSource vertexSource, double inWidth = 1)
			: base(vertexSource, new StrokeGenerator())
		{
			width(inWidth);
		}

		public void line_cap(LineCap lc)
		{
			base.GetGenerator().line_cap(lc);
		}

		public void line_join(LineJoin lj)
		{
			base.GetGenerator().line_join(lj);
		}

		public void inner_join(InnerJoin ij)
		{
			base.GetGenerator().inner_join(ij);
		}

		public LineCap line_cap()
		{
			return base.GetGenerator().line_cap();
		}

		public LineJoin line_join()
		{
			return base.GetGenerator().line_join();
		}

		public InnerJoin inner_join()
		{
			return base.GetGenerator().inner_join();
		}

		public double Width { get { return width(); } set { width(value); } }

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

		public void shorten(double s)
		{
			base.GetGenerator().shorten(s);
		}

		public double shorten()
		{
			return base.GetGenerator().shorten();
		}
	}
}