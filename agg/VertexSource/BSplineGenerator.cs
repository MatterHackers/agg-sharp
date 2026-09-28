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
	/// vcgen_bspline: replaces one accumulated polyline (or closed polygon) with a cubic spline through its
	/// vertices, sampled as line segments. x and y are each splined against the vertex index, stepping the
	/// index by <see cref="InterpolationStep"/>. Driven by <see cref="BSplinePath"/> through
	/// <see cref="VertexSourceAdapter"/>.
	/// </summary>
	internal class BSplineGenerator : IGenerator
	{
		// C++ keeps these in a pod_bvector: unlike the other generators' vertex_sequence it drops neither
		// coincident vertices nor a closing vertex that repeats the first.
		private readonly List<Vector2> sourceVertices = new List<Vector2>();
		private readonly bspline splineX = new bspline();
		private readonly bspline splineY = new bspline();
		private double interpolationStep = 1.0 / 50.0;
		private int closed;
		private Status status = Status.Initial;
		private int sourceVertex;
		private double currentAbscissa;
		private double maxAbscissa;

		private enum Status
		{
			Initial,
			Ready,
			Polygon,
			EndPoly,
			Stop
		}

		public double InterpolationStep
		{
			get => interpolationStep;
			set => interpolationStep = value;
		}

		public void RemoveAll()
		{
			sourceVertices.Clear();
			closed = 0;
			status = Status.Initial;
			sourceVertex = 0;
		}

		public void AddVertex(double x, double y, FlagsAndCommand cmd)
		{
			status = Status.Initial;
			if (ShapePath.IsMoveTo(cmd))
			{
				// pod_bvector::modify_last: replace the last vertex, or add the first.
				if (sourceVertices.Count > 0)
				{
					sourceVertices.RemoveAt(sourceVertices.Count - 1);
				}

				sourceVertices.Add(new Vector2(x, y));
			}
			else if (ShapePath.IsVertex(cmd))
			{
				sourceVertices.Add(new Vector2(x, y));
			}
			else
			{
				closed = (int)ShapePath.get_close_flag(cmd);
			}
		}

		private Vector2 Previous(int index) => sourceVertices[(index + sourceVertices.Count - 1) % sourceVertices.Count];

		private Vector2 Next(int index) => sourceVertices[(index + 1) % sourceVertices.Count];

		private void AddSplinePoint(double abscissa, Vector2 point)
		{
			splineX.add_point(abscissa, point.X);
			splineY.add_point(abscissa, point.Y);
		}

		public void Rewind(int pathId)
		{
			currentAbscissa = 0.0;
			maxAbscissa = 0.0;
			sourceVertex = 0;
			int count = sourceVertices.Count;
			if (status == Status.Initial && count > 2)
			{
				// A closed polygon is splined over the vertices with the last three (plus the one before them)
				// ahead of it and the first three (plus the one after them) behind it, so the curve closes
				// smoothly; only the middle stretch, abscissa 4 to count + 4, is drawn.
				if (closed != 0)
				{
					splineX.init(count + 8);
					splineY.init(count + 8);
					AddSplinePoint(0.0, Previous(count - 3));
					AddSplinePoint(1.0, sourceVertices[count - 3]);
					AddSplinePoint(2.0, sourceVertices[count - 2]);
					AddSplinePoint(3.0, sourceVertices[count - 1]);
				}
				else
				{
					splineX.init(count);
					splineY.init(count);
				}

				for (int i = 0; i < count; i++)
				{
					AddSplinePoint(closed != 0 ? i + 4 : i, sourceVertices[i]);
				}

				currentAbscissa = 0.0;
				maxAbscissa = count - 1;
				if (closed != 0)
				{
					currentAbscissa = 4.0;
					maxAbscissa += 5.0;
					AddSplinePoint(count + 4, sourceVertices[0]);
					AddSplinePoint(count + 5, sourceVertices[1]);
					AddSplinePoint(count + 6, sourceVertices[2]);
					AddSplinePoint(count + 7, Next(2));
				}

				splineX.prepare();
				splineY.prepare();
			}

			status = Status.Ready;
		}

		public FlagsAndCommand Vertex(ref double x, ref double y)
		{
			FlagsAndCommand cmd = FlagsAndCommand.LineTo;
			while (!ShapePath.IsStop(cmd))
			{
				switch (status)
				{
					case Status.Initial:
						Rewind(0);
						goto case Status.Ready;

					case Status.Ready:
						if (sourceVertices.Count < 2)
						{
							cmd = FlagsAndCommand.Stop;
							break;
						}

						if (sourceVertices.Count == 2)
						{
							// A straight line, with no end_poly after it (as in C++). C++ reads a third
							// vertex past the end before it stops; that read is skipped here.
							if (sourceVertex < 2)
							{
								x = sourceVertices[sourceVertex].X;
								y = sourceVertices[sourceVertex].Y;
								sourceVertex++;
								return sourceVertex == 1 ? FlagsAndCommand.MoveTo : FlagsAndCommand.LineTo;
							}

							cmd = FlagsAndCommand.Stop;
							break;
						}

						cmd = FlagsAndCommand.MoveTo;
						status = Status.Polygon;
						sourceVertex = 0;
						goto case Status.Polygon;

					case Status.Polygon:
						if (currentAbscissa >= maxAbscissa)
						{
							status = Status.EndPoly;
							if (closed != 0)
							{
								goto case Status.EndPoly;
							}

							// An open path ends exactly on its last vertex, which the sampling steps may miss.
							x = sourceVertices[sourceVertices.Count - 1].X;
							y = sourceVertices[sourceVertices.Count - 1].Y;
							return FlagsAndCommand.LineTo;
						}

						x = splineX.get_stateful(currentAbscissa);
						y = splineY.get_stateful(currentAbscissa);
						sourceVertex++;
						currentAbscissa += interpolationStep;
						return sourceVertex == 1 ? FlagsAndCommand.MoveTo : FlagsAndCommand.LineTo;

					case Status.EndPoly:
						status = Status.Stop;
						return FlagsAndCommand.EndPoly | (FlagsAndCommand)closed;

					case Status.Stop:
						return FlagsAndCommand.Stop;
				}
			}

			return cmd;
		}

		// IGenerator is shaped for the stroke and contour generators; vcgen_bspline has no width, joins or
		// caps, so these are inert.
		double IGenerator.ApproximationScale { get; set; } = 1;

		bool IGenerator.AutoDetectOrientation { get; set; }

		InnerJoin IGenerator.InnerJoin { get; set; }

		double IGenerator.InnerMiterLimit { get; set; }

		LineCap IGenerator.LineCap { get; set; }

		LineJoin IGenerator.LineJoin { get; set; }

		double IGenerator.MiterLimit { get; set; }

		double IGenerator.Shorten { get; set; }

		double IGenerator.Width { get; set; }

		void IGenerator.MiterLimitTheta(double t)
		{
		}
	}
}
