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
			this.Width = inWidth;
		}

		public double ApproximationScale
		{
			get => this.Generator.ApproximationScale;
			set => this.Generator.ApproximationScale = value;
		}

		public InnerJoin InnerJoin
		{
			get => this.Generator.InnerJoin;
			set => this.Generator.InnerJoin = value;
		}

		public double InnerMiterLimit
		{
			get => this.Generator.InnerMiterLimit;
			set => this.Generator.InnerMiterLimit = value;
		}

		public LineCap LineCap
		{
			get => this.Generator.LineCap;
			set => this.Generator.LineCap = value;
		}

		public LineJoin LineJoin
		{
			get => this.Generator.LineJoin;
			set => this.Generator.LineJoin = value;
		}

		public double MiterLimit
		{
			get => this.Generator.MiterLimit;
			set => this.Generator.MiterLimit = value;
		}

		public double Shorten
		{
			get => this.Generator.Shorten;
			set => this.Generator.Shorten = value;
		}

		public double Width
		{
			get => this.Generator.Width;
			set => this.Generator.Width = value;
		}

		public void MiterLimitTheta(double t)
		{
			this.Generator.MiterLimitTheta(t);
		}
	}
}