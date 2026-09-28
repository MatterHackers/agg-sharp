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
// trans_single_path
//
//----------------------------------------------------------------------------
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Transform
{
	/// <summary>
	/// C++ AGG's trans_single_path: bends the plane along a path. x becomes the distance along the path and y
	/// the distance to its left, so text laid out on a straight baseline follows the curve. Densify what you
	/// transform first (<see cref="Segmentator"/>): only vertices move, straight segments stay straight.
	/// </summary>
	public class TransSinglePath : ITransform
	{
		private readonly VertexSequence sourceVertices = new VertexSequence();

		private double kindex;

		private Status status = Status.Initial;

		private enum Status
		{
			Initial,
			MakingPath,
			Ready,
		}

		/// <summary>
		/// When above zero, x is scaled so that this length spans the whole path (C++ base_length);
		/// zero maps x one to one onto arc length.
		/// </summary>
		public double BaseLength { get; set; }

		/// <summary>
		/// True (the default) maps x onto true arc length. False assumes the path's vertices are evenly spaced
		/// and looks segments up by index, which is faster but stretches x where the vertices bunch up.
		/// </summary>
		public bool PreserveXScale { get; set; } = true;

		/// <summary>Length x is mapped over: <see cref="BaseLength"/> if set, else the path's arc length (0 before it is finalized).</summary>
		public double TotalLength
		{
			get
			{
				if (BaseLength >= 1e-10)
				{
					return BaseLength;
				}

				return status == Status.Ready ? PathArcLength.TotalLength(sourceVertices) : 0.0;
			}
		}

		public void Reset()
		{
			sourceVertices.Clear();
			kindex = 0.0;
			status = Status.Initial;
		}

		/// <summary>Starts the path. Only the first move-to starts it; later ones continue it as line-tos, as in C++.</summary>
		public void MoveTo(double x, double y)
		{
			if (status == Status.Initial)
			{
				sourceVertices.modify_last(new VertexDistance(x, y));
				status = Status.MakingPath;
			}
			else
			{
				LineTo(x, y);
			}
		}

		public void LineTo(double x, double y)
		{
			if (status == Status.MakingPath)
			{
				sourceVertices.Add(new VertexDistance(x, y));
			}
		}

		/// <summary>Measures the path; <see cref="Transform"/> does nothing until this has run on a path with two distinct points.</summary>
		public void FinalizePath()
		{
			if (status == Status.MakingPath && sourceVertices.Count > 1
				&& PathArcLength.Finalize(sourceVertices, out kindex))
			{
				status = Status.Ready;
			}
		}

		/// <summary>Reads every vertex of <paramref name="vertexSource"/> into the path and finalizes it (C++ add_path).</summary>
		public void AddPath(IVertexSource vertexSource, int pathId = 0)
		{
			vertexSource.Rewind(pathId);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = vertexSource.Vertex(out double x, out double y)))
			{
				if (ShapePath.IsMoveTo(command))
				{
					MoveTo(x, y);
				}
				else if (ShapePath.IsVertex(command))
				{
					LineTo(x, y);
				}
			}

			FinalizePath();
		}

		public void Transform(ref double x, ref double y)
		{
			if (status != Status.Ready)
			{
				return;
			}

			if (BaseLength > 1e-10)
			{
				x *= PathArcLength.TotalLength(sourceVertices) / BaseLength;
			}

			PathArcLength.Locate(sourceVertices, kindex, PreserveXScale, x,
				out double x1, out double y1, out double dx, out double dy, out double d, out double dd);

			double x2 = x1 + dx * d / dd;
			double y2 = y1 + dy * d / dd;
			x = x2 - y * dy / dd;
			y = y2 + y * dx / dd;
		}
	}
}
