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
			this.Generator.line_join(lj);
		}

		public void inner_join(InnerJoin ij)
		{
			this.Generator.inner_join(ij);
		}

		public void width(double w)
		{
			this.Generator.width(w);
		}

		public void miter_limit(double ml)
		{
			this.Generator.miter_limit(ml);
		}

		public void miter_limit_theta(double t)
		{
			this.Generator.miter_limit_theta(t);
		}

		public void inner_miter_limit(double ml)
		{
			this.Generator.inner_miter_limit(ml);
		}

		public void approximation_scale(double approxScale)
		{
			this.Generator.approximation_scale(approxScale);
		}

		public void auto_detect_orientation(bool v)
		{
			this.Generator.auto_detect_orientation(v);
		}

		public LineJoin line_join()
		{
			return this.Generator.line_join();
		}

		public InnerJoin inner_join()
		{
			return this.Generator.inner_join();
		}

		public double width()
		{
			return this.Generator.width();
		}

		public double miter_limit()
		{
			return this.Generator.miter_limit();
		}

		public double inner_miter_limit()
		{
			return this.Generator.inner_miter_limit();
		}

		public double approximation_scale()
		{
			return this.Generator.approximation_scale();
		}

		public bool auto_detect_orientation()
		{
			return this.Generator.auto_detect_orientation();
		}
	}
}