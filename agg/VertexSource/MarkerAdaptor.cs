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
	/// conv_marker_adaptor: passes a path through unchanged but for <see cref="Shorten"/>, while its
	/// <see cref="IMarkers"/> (a <see cref="TerminalMarkers"/>) collects where each sub-path starts and ends -
	/// for arrowheads (<see cref="MarkerPlacer"/>) on a path that is not stroked, such as one drawn as aliased
	/// lines.
	/// </summary>
	public sealed class MarkerAdaptor : VertexSourceAdapter
	{
		public MarkerAdaptor(IVertexSource vertexSource, IMarkers markers)
			: base(vertexSource, new VertexSequenceGenerator(), markers)
		{
		}

		/// <summary>Length cut off the end of each sub-path; the markers still see the whole path.</summary>
		public double Shorten
		{
			get => ((VertexSequenceGenerator)this.Generator).Shorten;
			set => ((VertexSequenceGenerator)this.Generator).Shorten = value;
		}
	}
}
