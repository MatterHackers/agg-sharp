//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007, 2026
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

		/// <summary>
		/// conv_stroke with a markers type: <paramref name="markers"/> (a <see cref="TerminalMarkers"/>) collects
		/// where each source sub-path starts and ends as the stroke is read, for arrowheads drawn with
		/// <see cref="MarkerPlacer"/>.
		/// </summary>
		public Stroke(IVertexSource vertexSource, IMarkers markers, double inWidth = 1)
			: base(vertexSource, new StrokeGenerator(), markers)
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

		/// <summary>
		/// Hashes the source and the stroke's parameters rather than generating the outline: the outline is a
		/// function of exactly those, and generating it allocated on every GPU cache lookup. Markers only
		/// record positions, so they do not change the outline.
		/// </summary>
		public override ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			// Tags the parameter hash so it cannot line up with a vertex walk of the same numbers.
			hash = 0x5374726f6b653031UL.GetLongHashCode(hash);
			hash = this.VertexSource.GetLongHashCode(hash);
			hash = this.Width.GetLongHashCode(hash);
			hash = ((int)this.LineCap).GetLongHashCode(hash);
			hash = ((int)this.LineJoin).GetLongHashCode(hash);
			hash = ((int)this.InnerJoin).GetLongHashCode(hash);
			hash = this.MiterLimit.GetLongHashCode(hash);
			hash = this.InnerMiterLimit.GetLongHashCode(hash);
			hash = this.ApproximationScale.GetLongHashCode(hash);
			hash = this.Shorten.GetLongHashCode(hash);
			return (((StrokeGenerator)this.Generator).AutoDetectOrientation ? 1 : 0).GetLongHashCode(hash);
		}

		public void MiterLimitTheta(double t)
		{
			this.Generator.MiterLimitTheta(t);
		}
	}
}