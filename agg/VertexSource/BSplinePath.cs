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
// conv_bspline
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// conv_bspline: replaces every sub-path of the source that has three or more vertices with a smooth
	/// cubic spline through them, already flattened to line segments. A closed sub-path gives a closed loop.
	/// </summary>
	public sealed class BSplinePath : VertexSourceAdapter
	{
		public BSplinePath(IVertexSource vertexSource)
			: base(vertexSource, new BSplineGenerator())
		{
		}

		private BSplineGenerator BSplineGenerator => (BSplineGenerator)this.Generator;

		/// <summary>
		/// The spacing of the samples, as a fraction of the way from one source vertex to the next: 1/50 (the
		/// default) cuts each span into 50 segments. A <see cref="FlattenCurves"/> (or other caching source)
		/// wrapped around this caches its vertex enumerator and does not see a change made after it was read
		/// without being rewound: Rewind it, or set its VertexSource again to invalidate it.
		/// </summary>
		public double InterpolationStep
		{
			get => BSplineGenerator.InterpolationStep;
			set => BSplineGenerator.InterpolationStep = value;
		}
	}
}
