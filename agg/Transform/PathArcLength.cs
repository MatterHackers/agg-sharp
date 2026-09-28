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
// The path bookkeeping trans_single_path and trans_double_path share
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.Transform
{
	/// <summary>
	/// The part of C++ AGG's trans_single_path and trans_double_path that is the same in both: a polyline
	/// held as a <see cref="VertexSequence"/> whose dist fields become the arc length at each vertex, and
	/// the lookup of the segment a given arc length falls on.
	/// </summary>
	internal static class PathArcLength
	{
		/// <summary>
		/// trans_double_path::finalize_path (trans_single_path inlines the same code): closes the sequence as a
		/// polyline, folds a very short last segment into the one before it, turns each vertex's dist into the
		/// arc length up to that vertex and gives (vertex count - 1) / total length as <paramref name="kindex"/>.
		/// </summary>
		/// <returns>
		/// False when closing left fewer than two vertices (every point coincided). C++ goes on to call such a
		/// path ready and then reads past its one vertex; callers here leave it not-ready instead.
		/// </returns>
		public static bool Finalize(VertexSequence vertices, out double kindex)
		{
			kindex = 0.0;
			vertices.close(false);
			if (vertices.Count < 2)
			{
				return false;
			}

			VertexDistance[] v = vertices.Array;
			if (vertices.Count > 2)
			{
				int n = vertices.Count;
				if (v[n - 2].dist * 10.0 < v[n - 3].dist)
				{
					double d = v[n - 3].dist + v[n - 2].dist;
					v[n - 2] = v[n - 1];
					vertices.RemoveLast();
					v[vertices.Count - 2].dist = d;
				}
			}

			double dist = 0.0;
			for (int i = 0; i < vertices.Count; i++)
			{
				double d = v[i].dist;
				v[i].dist = dist;
				dist += d;
			}

			kindex = (vertices.Count - 1) / dist;
			return true;
		}

		/// <summary>Arc length of the whole finalized path.</summary>
		public static double TotalLength(VertexSequence vertices) => vertices.Array[vertices.Count - 1].dist;

		/// <summary>
		/// Finds where arc length <paramref name="x"/> falls on the finalized path: the segment start
		/// (<paramref name="x1"/>, <paramref name="y1"/>), its direction (<paramref name="dx"/>,
		/// <paramref name="dy"/>) and length <paramref name="dd"/>, and how far along it <paramref name="d"/>.
		/// Before the start and past the end the first or last segment is extended.
		/// </summary>
		public static void Locate(VertexSequence vertices, double kindex, bool preserveXScale, double x,
			out double x1, out double y1, out double dx, out double dy, out double d, out double dd)
		{
			VertexDistance[] v = vertices.Array;
			int count = vertices.Count;
			if (x < 0.0)
			{
				// Extrapolation on the left
				x1 = v[0].x;
				y1 = v[0].y;
				dx = v[1].x - x1;
				dy = v[1].y - y1;
				dd = v[1].dist - v[0].dist;
				d = x;
			}
			else if (x > v[count - 1].dist)
			{
				// Extrapolation on the right
				int i = count - 2;
				int j = count - 1;
				x1 = v[j].x;
				y1 = v[j].y;
				dx = x1 - v[i].x;
				dy = y1 - v[i].y;
				dd = v[j].dist - v[i].dist;
				d = x - v[j].dist;
			}
			else
			{
				// Interpolation
				int i = 0;
				int j = count - 1;
				if (preserveXScale)
				{
					while ((j - i) > 1)
					{
						int k = (i + j) >> 1;
						if (x < v[k].dist)
						{
							j = k;
						}
						else
						{
							i = k;
						}
					}

					d = v[i].dist;
					dd = v[j].dist - d;
					d = x - d;
				}
				else
				{
					i = (int)(x * kindex);

					// At x == total length C++ reads the element one past the last vertex; stepping back one
					// segment lands on the same end point without leaving the array.
					if (i > count - 2)
					{
						i = count - 2;
					}

					j = i + 1;
					dd = v[j].dist - v[i].dist;
					d = ((x * kindex) - i) * dd;
				}

				x1 = v[i].x;
				y1 = v[i].y;
				dx = v[j].x - x1;
				dy = v[j].y - y1;
			}
		}
	}
}
