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
// conv_dash
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// conv_dash: turns every sub-path of the source into dashes following the pattern built with
	/// <see cref="AddDash"/>. Stroke the result to see it. Pass a <see cref="TerminalMarkers"/> to also
	/// collect where each source sub-path starts and ends, for arrowheads drawn with <see cref="MarkerPlacer"/>.
	/// </summary>
	public sealed class Dash : VertexSourceAdapter
	{
		public Dash(IVertexSource vertexSource)
			: base(vertexSource, new DashGenerator())
		{
		}

		public Dash(IVertexSource vertexSource, IMarkers markers)
			: base(vertexSource, new DashGenerator(), markers)
		{
		}

		private DashGenerator DashGenerator => (DashGenerator)this.Generator;

		/// <summary>
		/// Length cut off the end of each source sub-path before it is dashed (vcgen_dash::shorten).
		/// </summary>
		public double Shorten
		{
			get => DashGenerator.Shorten;
			set => DashGenerator.Shorten = value;
		}

		/// <summary>
		/// Appends a dash of <paramref name="dashLength"/> followed by a gap of <paramref name="gapLength"/>.
		/// The pattern repeats; like C++ AGG it holds at most 16 dash/gap pairs and ignores any more. A pattern
		/// whose lengths add up to zero draws nothing (C++ AGG would never finish).
		/// </summary>
		public void AddDash(double dashLength, double gapLength)
		{
			DashGenerator.AddDash(dashLength, gapLength);
		}

		/// <summary>
		/// Offsets where the pattern starts along each sub-path. A negative value offsets by its magnitude
		/// only once, at this call: after that each sub-path (and each re-read of the whole path) continues
		/// the pattern where the previous one ended, as in C++ AGG.
		/// </summary>
		public void DashStart(double distance)
		{
			DashGenerator.DashStart(distance);
		}

		public void RemoveAllDashes()
		{
			DashGenerator.RemoveAllDashes();
		}
	}
}
