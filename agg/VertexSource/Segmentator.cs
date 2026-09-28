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
// conv_segmentator
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// C++ AGG's conv_segmentator: splits every line segment of the source into pieces about
	/// 1 / <see cref="ApproximationScale"/> long. Put it in front of a non-linear transform such as
	/// <see cref="Transform.TransSinglePath"/>, which only moves vertices, so long straight edges bend too.
	/// Curves must be flattened first.
	/// </summary>
	public sealed class Segmentator : VertexProcessorAdapter
	{
		public Segmentator(IVertexSource vertexSource)
			: base(vertexSource, new SegmentatorProcessor())
		{
		}

		/// <summary>Pieces per unit of length (default 1).</summary>
		public double ApproximationScale
		{
			get => ((SegmentatorProcessor)Processor).ApproximationScale;
			set => ((SegmentatorProcessor)Processor).ApproximationScale = value;
		}
	}
}
