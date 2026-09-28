//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026
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
// conv_smooth_poly1, conv_smooth_poly1_curve
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// conv_smooth_poly1: rounds every sub-path of the source into Bezier curves through its vertices. The
	/// output is Curve3/Curve4 commands with their control points, so drawn directly it shows the control
	/// polygon; use <see cref="SmoothPolygonCurve"/> (or wrap this in <see cref="FlattenCurves"/>) to draw
	/// the curves themselves.
	/// </summary>
	public sealed class SmoothPolygon : VertexSourceAdapter
	{
		public SmoothPolygon(IVertexSource vertexSource)
			: base(vertexSource, new SmoothPolygonGenerator())
		{
		}

		private SmoothPolygonGenerator SmoothPolygonGenerator => (SmoothPolygonGenerator)this.Generator;

		/// <summary>
		/// How far the curves bulge away from the straight edges: 0 keeps the edges straight, 1 (the default)
		/// is C++ AGG's natural smoothing, larger values overshoot. A <see cref="FlattenCurves"/> you wrap
		/// around this yourself caches its vertex enumerator and does not see a change made after it was
		/// read without being rewound: Rewind it, or set its VertexSource again to invalidate it
		/// (<see cref="SmoothPolygonCurve.SmoothValue"/> does that for you).
		/// </summary>
		public double SmoothValue
		{
			get => SmoothPolygonGenerator.SmoothValue;
			set => SmoothPolygonGenerator.SmoothValue = value;
		}
	}

	/// <summary>
	/// conv_smooth_poly1_curve: <see cref="SmoothPolygon"/> flattened to line segments, ready to fill or stroke.
	/// </summary>
	public sealed class SmoothPolygonCurve : FlattenCurves
	{
		private readonly SmoothPolygon smooth;

		public SmoothPolygonCurve(IVertexSource vertexSource)
			: this(new SmoothPolygon(vertexSource))
		{
		}

		private SmoothPolygonCurve(SmoothPolygon smooth)
			: base(smooth)
		{
			this.smooth = smooth;
		}

		/// <inheritdoc cref="SmoothPolygon.SmoothValue"/>
		public double SmoothValue
		{
			get => smooth.SmoothValue;
			set
			{
				smooth.SmoothValue = value;
				InvalidateVertices();
			}
		}
	}
}
