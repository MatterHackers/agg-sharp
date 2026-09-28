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
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// vcgen_markers_term: the terminal (arrowhead/arrowtail) marker locator. Handed to a converter such as
	/// <see cref="Dash"/> as its <see cref="IMarkers"/>, it records the first two and last two points of every
	/// sub-path the converter reads. Read back as a vertex source, path 0 is each sub-path's tail (a MoveTo
	/// at the first point, a LineTo at the second) and path 1 its head (MoveTo at the last point, LineTo at
	/// the one before), which is the (position, direction) pair <see cref="MarkerPlacer"/> expects.
	/// </summary>
	/// <remarks>
	/// It only holds points once its converter has been read, so read the converter (for example rasterize
	/// its stroke) before reading this.
	/// </remarks>
	public class TerminalMarkers : IMarkers, IVertexSource
	{
		// Four points per sub-path: first, second, last, one-before-last.
		private readonly List<Vector2> markers = new List<Vector2>();
		private int currentId;
		private int currentIndex;

		public void Clear()
		{
			markers.Clear();
		}

		public void add_vertex(double x, double y, FlagsAndCommand cmd)
		{
			if (ShapePath.IsMoveTo(cmd))
			{
				if ((markers.Count & 1) != 0)
				{
					// Only the first point of a sub-path is in; a repeated MoveTo replaces it.
					markers[markers.Count - 1] = new Vector2(x, y);
				}
				else
				{
					markers.Add(new Vector2(x, y));
				}
			}
			else if (ShapePath.IsVertex(cmd))
			{
				if ((markers.Count & 1) != 0)
				{
					// The second point: store 0,1,1,0 so a two-point path already has a head.
					markers.Add(new Vector2(x, y));
					markers.Add(markers[markers.Count - 1]);
					markers.Add(markers[markers.Count - 3]);
				}
				else if (markers.Count > 0)
				{
					// Every later point shifts the head along: 0,1,2,1.
					markers[markers.Count - 1] = markers[markers.Count - 2];
					markers[markers.Count - 2] = new Vector2(x, y);
				}
			}
		}

		/// <summary>
		/// Selects the tails (<paramref name="pathId"/> 0) or the heads (1); any other id yields nothing.
		/// </summary>
		public void Rewind(int pathId = 0)
		{
			currentId = pathId * 2;
			currentIndex = currentId;
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (currentId > 2 || currentIndex >= markers.Count)
			{
				return FlagsAndCommand.Stop;
			}

			Vector2 point = markers[currentIndex];
			x = point.X;
			y = point.Y;
			if ((currentIndex & 1) != 0)
			{
				currentIndex += 3;
				return FlagsAndCommand.LineTo;
			}

			++currentIndex;
			return FlagsAndCommand.MoveTo;
		}

		/// <summary>
		/// The tails then the heads, ending in one Stop after both.
		/// </summary>
		public IEnumerable<VertexData> Vertices()
		{
			for (int pathId = 0; pathId < 2; pathId++)
			{
				Rewind(pathId);
				FlagsAndCommand command;
				while (!ShapePath.IsStop(command = Vertex(out double x, out double y)))
				{
					yield return new VertexData(command, new Vector2(x, y));
				}
			}

			yield return new VertexData(FlagsAndCommand.Stop, Vector2.Zero);
		}

		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			foreach (var vertex in this.Vertices())
			{
				hash = vertex.GetLongHashCode(hash);
			}

			return hash;
		}
	}
}
