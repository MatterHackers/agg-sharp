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
// trans_double_path
//
//----------------------------------------------------------------------------
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Transform
{
	/// <summary>
	/// C++ AGG's trans_double_path: warps the band 0 &lt;= y &lt;= <see cref="BaseHeight"/> between two paths.
	/// x runs along both paths (path 2 scaled to path 1's length), y = 0 lands on path 1 and
	/// y = <see cref="BaseHeight"/> on path 2. Densify what you transform first (<see cref="Segmentator"/>).
	/// </summary>
	public class TransDoublePath : ITransform
	{
		private readonly VertexSequence sourceVertices1 = new VertexSequence();

		private readonly VertexSequence sourceVertices2 = new VertexSequence();

		private double kindex1;

		private double kindex2;

		private Status status1 = Status.Initial;

		private Status status2 = Status.Initial;

		private enum Status
		{
			Initial,
			MakingPath,
			Ready,
		}

		/// <summary>When above zero, x is scaled so that this length spans path 1 (C++ base_length).</summary>
		public double BaseLength { get; set; }

		/// <summary>The y that maps onto path 2 (C++ base_height, default 1).</summary>
		public double BaseHeight { get; set; } = 1.0;

		/// <summary>See <see cref="TransSinglePath.PreserveXScale"/>.</summary>
		public bool PreserveXScale { get; set; } = true;

		/// <summary>Length x is mapped over along path 1: <see cref="BaseLength"/> if set, else path 1's arc length.</summary>
		public double TotalLength1 => TotalLength(sourceVertices1, status1);

		/// <summary>Length x is mapped over along path 2: <see cref="BaseLength"/> if set, else path 2's arc length.</summary>
		public double TotalLength2 => TotalLength(sourceVertices2, status2);

		public void Reset()
		{
			sourceVertices1.Clear();
			sourceVertices2.Clear();
			kindex1 = 0.0;
			kindex2 = 0.0;
			status1 = Status.Initial;
			status2 = Status.Initial;
		}

		public void MoveTo1(double x, double y) => MoveTo(sourceVertices1, ref status1, x, y);

		public void LineTo1(double x, double y) => LineTo(sourceVertices1, status1, x, y);

		public void MoveTo2(double x, double y) => MoveTo(sourceVertices2, ref status2, x, y);

		public void LineTo2(double x, double y) => LineTo(sourceVertices2, status2, x, y);

		/// <summary>Measures both paths; nothing is transformed until each has at least two distinct points.</summary>
		public void FinalizePaths()
		{
			if (status1 == Status.MakingPath && sourceVertices1.Count > 1
				&& status2 == Status.MakingPath && sourceVertices2.Count > 1)
			{
				// Both are finalized even if the first collapses, as C++ does; neither is used unless both keep a segment.
				bool path1Usable = PathArcLength.Finalize(sourceVertices1, out kindex1);
				bool path2Usable = PathArcLength.Finalize(sourceVertices2, out kindex2);
				if (path1Usable && path2Usable)
				{
					status1 = Status.Ready;
					status2 = Status.Ready;
				}
			}
		}

		/// <summary>Reads both vertex sources into the two paths and finalizes them (C++ add_paths).</summary>
		public void AddPaths(IVertexSource vertexSource1, IVertexSource vertexSource2, int path1Id = 0, int path2Id = 0)
		{
			AddVertices(vertexSource1, path1Id, MoveTo1, LineTo1);
			AddVertices(vertexSource2, path2Id, MoveTo2, LineTo2);
			FinalizePaths();
		}

		public void Transform(ref double x, ref double y)
		{
			if (status1 != Status.Ready || status2 != Status.Ready)
			{
				return;
			}

			double length1 = PathArcLength.TotalLength(sourceVertices1);
			if (BaseLength > 1e-10)
			{
				x *= length1 / BaseLength;
			}

			double x1 = x;
			double y1 = y;
			double x2 = x;
			double y2 = y;
			double dd = PathArcLength.TotalLength(sourceVertices2) / length1;
			TransformOnPath(sourceVertices1, kindex1, 1.0, ref x1, ref y1);
			TransformOnPath(sourceVertices2, kindex2, dd, ref x2, ref y2);
			x = x1 + y * (x2 - x1) / BaseHeight;
			y = y1 + y * (y2 - y1) / BaseHeight;
		}

		private static void AddVertices(IVertexSource vertexSource, int pathId, System.Action<double, double> moveTo, System.Action<double, double> lineTo)
		{
			vertexSource.Rewind(pathId);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = vertexSource.Vertex(out double x, out double y)))
			{
				if (ShapePath.IsMoveTo(command))
				{
					moveTo(x, y);
				}
				else if (ShapePath.IsVertex(command))
				{
					lineTo(x, y);
				}
			}
		}

		private static void MoveTo(VertexSequence vertices, ref Status status, double x, double y)
		{
			if (status == Status.Initial)
			{
				vertices.modify_last(new VertexDistance(x, y));
				status = Status.MakingPath;
			}
			else
			{
				LineTo(vertices, status, x, y);
			}
		}

		private static void LineTo(VertexSequence vertices, Status status, double x, double y)
		{
			if (status == Status.MakingPath)
			{
				vertices.Add(new VertexDistance(x, y));
			}
		}

		private double TotalLength(VertexSequence vertices, Status status)
		{
			if (BaseLength >= 1e-10)
			{
				return BaseLength;
			}

			return status == Status.Ready ? PathArcLength.TotalLength(vertices) : 0.0;
		}

		/// <summary>C++ transform1: the point at arc length x * <paramref name="kx"/> on one path, y ignored.</summary>
		private void TransformOnPath(VertexSequence vertices, double kindex, double kx, ref double x, ref double y)
		{
			x *= kx;
			PathArcLength.Locate(vertices, kindex, PreserveXScale, x,
				out double x1, out double y1, out double dx, out double dy, out double d, out double dd);
			x = x1 + dx * d / dd;
			y = y1 + dy * d / dd;
		}
	}
}
