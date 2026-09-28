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
namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// The contract of a C++ AGG vpgen (vertex processor), driven by <see cref="VertexProcessorAdapter"/>:
	/// unlike an <see cref="IGenerator"/>, which collects a whole sub-path before emitting anything, a vpgen
	/// is fed one segment at a time and emits that segment's output before the next is read.
	/// </summary>
	public interface IVertexProcessor
	{
		/// <summary>True if the adapter should close every polygon (C++ auto_close).</summary>
		bool AutoClose { get; }

		/// <summary>True if the adapter should drop end-poly commands (C++ auto_unclose).</summary>
		bool AutoUnclose { get; }

		void Reset();

		void MoveTo(double x, double y);

		void LineTo(double x, double y);

		/// <summary>Next output vertex of the segments fed so far; Stop when it needs more input.</summary>
		FlagsAndCommand Vertex(out double x, out double y);
	}
}
