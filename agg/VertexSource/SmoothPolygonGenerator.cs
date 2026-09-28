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
	/// vcgen_smooth_poly1: turns one accumulated polyline (or closed polygon) into Bezier curve commands that
	/// pass through its vertices. A closed polygon becomes Curve4 segments; an open polyline gets a Curve3 at
	/// each end and Curve4 segments between (a two-vertex path stays a line). The output still carries its
	/// control points as vertices, so flatten it with <see cref="FlattenCurves"/> to draw the curve.
	/// Driven by <see cref="SmoothPolygon"/> through <see cref="VertexSourceAdapter"/>.
	/// </summary>
	internal class SmoothPolygonGenerator : IGenerator
	{
		private readonly VertexSequence sourceVertices = new VertexSequence();
		private double smoothValue = 0.5;
		private int closed;
		private Status status = Status.Initial;
		private int sourceVertex;
		private double ctrl1X;
		private double ctrl1Y;
		private double ctrl2X;
		private double ctrl2Y;

		private enum Status
		{
			Initial,
			Ready,
			Polygon,
			CtrlB,
			CtrlE,
			Ctrl1,
			Ctrl2,
			EndPoly,
			Stop
		}

		// C++ keeps half the value it is given.
		public double SmoothValue
		{
			get => smoothValue * 2.0;
			set => smoothValue = value * 0.5;
		}

		public void RemoveAll()
		{
			sourceVertices.Clear();
			closed = 0;
			status = Status.Initial;
		}

		public void AddVertex(double x, double y, FlagsAndCommand cmd)
		{
			status = Status.Initial;
			if (ShapePath.IsMoveTo(cmd))
			{
				sourceVertices.modify_last(new VertexDistance(x, y));
			}
			else if (ShapePath.IsVertex(cmd))
			{
				sourceVertices.Add(new VertexDistance(x, y));
			}
			else
			{
				closed = (int)ShapePath.get_close_flag(cmd);
			}
		}

		public void Rewind(int pathId)
		{
			if (status == Status.Initial)
			{
				sourceVertices.close(closed != 0);
			}

			status = Status.Ready;
			sourceVertex = 0;
		}

		// The control points of the curve from v1 to v2, with v0 before it and v3 after. Each vertex's dist is
		// the length of the edge leaving it (0 for an open path's last vertex).
		private void Calculate(VertexDistance v0, VertexDistance v1, VertexDistance v2, VertexDistance v3)
		{
			double k1 = v0.dist / (v0.dist + v1.dist);
			double k2 = v1.dist / (v1.dist + v2.dist);

			double xm1 = v0.x + (v2.x - v0.x) * k1;
			double ym1 = v0.y + (v2.y - v0.y) * k1;
			double xm2 = v1.x + (v3.x - v1.x) * k2;
			double ym2 = v1.y + (v3.y - v1.y) * k2;

			ctrl1X = v1.x + smoothValue * (v2.x - xm1);
			ctrl1Y = v1.y + smoothValue * (v2.y - ym1);
			ctrl2X = v2.x + smoothValue * (v1.x - xm2);
			ctrl2Y = v2.y + smoothValue * (v1.y - ym2);
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
							// Only a line to draw, and no end_poly after it (as in C++), so a closed
							// two-vertex path comes out open. C++ reads a third
							// vertex past the end before it stops; that read is skipped here.
							if (sourceVertex < 2)
							{
								x = sourceVertices[sourceVertex].x;
								y = sourceVertices[sourceVertex].y;
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
						if (closed != 0)
						{
							if (sourceVertex >= sourceVertices.Count)
							{
								x = sourceVertices[0].x;
								y = sourceVertices[0].y;
								status = Status.EndPoly;
								return FlagsAndCommand.Curve4;
							}
						}
						else if (sourceVertex >= sourceVertices.Count - 1)
						{
							x = sourceVertices[sourceVertices.Count - 1].x;
							y = sourceVertices[sourceVertices.Count - 1].y;
							status = Status.EndPoly;
							return FlagsAndCommand.Curve3;
						}

						Calculate(
							sourceVertices.prev(sourceVertex),
							sourceVertices.curr(sourceVertex),
							sourceVertices.next(sourceVertex),
							sourceVertices.next(sourceVertex + 1));

						x = sourceVertices[sourceVertex].x;
						y = sourceVertices[sourceVertex].y;
						sourceVertex++;

						if (closed != 0)
						{
							status = Status.Ctrl1;
							return sourceVertex == 1 ? FlagsAndCommand.MoveTo : FlagsAndCommand.Curve4;
						}

						if (sourceVertex == 1)
						{
							status = Status.CtrlB;
							return FlagsAndCommand.MoveTo;
						}

						if (sourceVertex >= sourceVertices.Count - 1)
						{
							status = Status.CtrlE;
							return FlagsAndCommand.Curve3;
						}

						status = Status.Ctrl1;
						return FlagsAndCommand.Curve4;

					case Status.CtrlB:
						x = ctrl2X;
						y = ctrl2Y;
						status = Status.Polygon;
						return FlagsAndCommand.Curve3;

					case Status.CtrlE:
						x = ctrl1X;
						y = ctrl1Y;
						status = Status.Polygon;
						return FlagsAndCommand.Curve3;

					case Status.Ctrl1:
						x = ctrl1X;
						y = ctrl1Y;
						status = Status.Ctrl2;
						return FlagsAndCommand.Curve4;

					case Status.Ctrl2:
						x = ctrl2X;
						y = ctrl2Y;
						status = Status.Polygon;
						return FlagsAndCommand.Curve4;

					case Status.EndPoly:
						status = Status.Stop;
						return FlagsAndCommand.EndPoly | (FlagsAndCommand)closed;

					case Status.Stop:
						return FlagsAndCommand.Stop;
				}
			}

			return cmd;
		}

		// IGenerator is shaped for the stroke and contour generators; vcgen_smooth_poly1 has no width, joins
		// or caps, so these are inert.
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
