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

		public void LineJoin(LineJoin lj)
		{
			this.Generator.LineJoin(lj);
		}

		public void InnerJoin(InnerJoin ij)
		{
			this.Generator.InnerJoin(ij);
		}

		public void Width(double w)
		{
			this.Generator.Width(w);
		}

		public void MiterLimit(double ml)
		{
			this.Generator.MiterLimit(ml);
		}

		public void MiterLimitTheta(double t)
		{
			this.Generator.MiterLimitTheta(t);
		}

		public void InnerMiterLimit(double ml)
		{
			this.Generator.InnerMiterLimit(ml);
		}

		public void ApproximationScale(double approxScale)
		{
			this.Generator.ApproximationScale(approxScale);
		}

		public void AutoDetectOrientation(bool v)
		{
			this.Generator.AutoDetectOrientation(v);
		}

		public LineJoin LineJoin()
		{
			return this.Generator.LineJoin();
		}

		public InnerJoin InnerJoin()
		{
			return this.Generator.InnerJoin();
		}

		public double Width()
		{
			return this.Generator.width();
		}

		public double MiterLimit()
		{
			return this.Generator.MiterLimit();
		}

		public double InnerMiterLimit()
		{
			return this.Generator.InnerMiterLimit();
		}

		public double ApproximationScale()
		{
			return this.Generator.ApproximationScale();
		}

		public bool AutoDetectOrientation()
		{
			return this.Generator.AutoDetectOrientation();
		}
	}
}