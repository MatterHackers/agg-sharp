/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>interactive_polygon</c> (examples/interactive_polygon.cpp), the editable polygon several
	/// AGG demos share. As a vertex source it is the polygon's closed 1-pixel outline followed by a 32-step
	/// circle on every point, the one being dragged 1.2 times larger. Its mouse handlers drag a point (a press
	/// within <see cref="PointRadius"/> of it), an edge (a press within that distance of the edge, moving both
	/// its points) or, for a press inside the polygon, the whole polygon.
	/// </summary>
	/// <remarks>
	/// Not an <see cref="AggCtrl"/>: C++ never adds it to the ctrl list. The demo renders it in a color of its
	/// choosing and forwards the left mouse button to it after the ctrls have had their turn.
	/// </remarks>
	public class InteractivePolygon : VertexSourceLegacySupport
	{
		private readonly double[] polygon;

		private double dragX;

		private double dragY;

		private double lineWidth = 1.0;

		private double pointRadius;

		// C++ m_edge: the edge being dragged, named by its later point (the other is the point before it).
		private int edge = -1;

		/// <summary>A polygon of <paramref name="numPoints"/> points, all at the origin until placed with <see cref="SetPoint"/>.</summary>
		/// <param name="pointRadius">The grab distance and drawn radius of the points (C++ <c>point_radius</c>).</param>
		public InteractivePolygon(int numPoints, double pointRadius)
		{
			this.polygon = new double[numPoints * 2];
			this.PointRadius = pointRadius;
		}

		/// <summary>How many points the polygon has; fixed at construction.</summary>
		public int NumPoints => this.polygon.Length / 2;

		/// <summary>
		/// How near a press must be to grab a point or an edge, and the radius the points are drawn at (C++
		/// <c>point_radius</c>). line_patterns_clip divides it by its zoom so the drawn, transformed points stay 5 wide.
		/// </summary>
		public double PointRadius
		{
			get => this.pointRadius;
			set
			{
				this.pointRadius = value;
				this.InvalidateVertices();
			}
		}

		/// <summary>The width the outline is stroked at, before any transform (C++ <c>polygon_ctrl::line_width</c>, default 1).</summary>
		public double LineWidth
		{
			get => this.lineWidth;
			set
			{
				this.lineWidth = value;
				this.InvalidateVertices();
			}
		}

		/// <summary>
		/// C++ <c>m_node</c>: the point being dragged, <see cref="NumPoints"/> while the whole polygon is, or -1.
		/// </summary>
		public int Node { get; private set; } = -1;

		/// <summary>
		/// Whether the drawn outline is closed (C++ <c>interactive_polygon::close</c>, default true). trans_curve1
		/// and trans_curve2 open it to match their open curves.
		/// </summary>
		public bool Close { get; set; } = true;

		/// <summary>
		/// Whether a press inside the polygon grabs the whole polygon (C++ <c>polygon_ctrl::in_polygon_check</c>,
		/// default true). bezier_ctrl turns it off: its four points are a curve's, not a shape's.
		/// </summary>
		public bool InPolygonCheck { get; set; } = true;

		/// <summary>Point <paramref name="index"/> (C++ <c>xn</c>/<c>yn</c>), wherever it was set or dragged to.</summary>
		public Vector2 GetPoint(int index) => new Vector2(this.polygon[index * 2], this.polygon[(index * 2) + 1]);

		/// <summary>Moves point <paramref name="index"/> to (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public void SetPoint(int index, double x, double y)
		{
			this.polygon[index * 2] = x;
			this.polygon[(index * 2) + 1] = y;
			this.InvalidateVertices();
		}

		/// <summary>The points as an open or closed polyline, as C++ <c>simple_polygon_vertex_source</c> gives them.</summary>
		public VertexStorage ToPath(bool close)
		{
			var path = new VertexStorage();
			for (int i = 0; i < this.NumPoints; i++)
			{
				if (i == 0)
				{
					path.MoveTo(this.polygon[0], this.polygon[1]);
				}
				else
				{
					path.LineTo(this.polygon[i * 2], this.polygon[(i * 2) + 1]);
				}
			}

			path.EndPoly(close ? FlagsAndCommand.FlagClose : FlagsAndCommand.FlagNone);
			return path;
		}

		public override IEnumerable<VertexData> Vertices()
		{
			// C++ strokes the points at LineWidth, closed unless the demo opened the outline with Close.
			foreach (VertexData vertex in new Stroke(this.ToPath(this.Close), this.LineWidth).Vertices())
			{
				if (!vertex.IsStop)
				{
					yield return vertex;
				}
			}

			for (int i = 0; i < this.NumPoints; i++)
			{
				double radius = this.Node == i ? this.PointRadius * 1.2 : this.PointRadius;
				foreach (VertexData vertex in new Ellipse(this.polygon[i * 2], this.polygon[(i * 2) + 1], radius, radius, 32).Vertices())
				{
					if (!vertex.IsStop)
					{
						yield return vertex;
					}
				}
			}

			yield return new VertexData(FlagsAndCommand.Stop, default(Vector2));
		}

		/// <summary>Grabs a point, else an edge, else the whole polygon.</summary>
		/// <returns>True when the press grabbed something and the polygon must be redrawn.</returns>
		public bool OnMouseButtonDown(double x, double y)
		{
			this.Node = -1;
			this.edge = -1;
			this.InvalidateVertices();
			for (int i = 0; i < this.NumPoints; i++)
			{
				Vector2 point = this.GetPoint(i);
				if (Math.Sqrt(((x - point.X) * (x - point.X)) + ((y - point.Y) * (y - point.Y))) < this.PointRadius)
				{
					this.dragX = x - point.X;
					this.dragY = y - point.Y;
					this.Node = i;
					return true;
				}
			}

			for (int i = 0; i < this.NumPoints; i++)
			{
				if (this.CheckEdge(i, x, y))
				{
					this.dragX = x;
					this.dragY = y;
					this.edge = i;
					return true;
				}
			}

			if (this.InPolygonCheck && this.PointInPolygon(x, y))
			{
				this.dragX = x;
				this.dragY = y;
				this.Node = this.NumPoints;
				return true;
			}

			return false;
		}

		/// <summary>Moves whatever <see cref="OnMouseButtonDown"/> grabbed.</summary>
		/// <returns>True when something moved.</returns>
		public bool OnMouseMove(double x, double y)
		{
			if (this.Node == this.NumPoints)
			{
				double dx = x - this.dragX;
				double dy = y - this.dragY;
				for (int i = 0; i < this.NumPoints; i++)
				{
					this.polygon[i * 2] += dx;
					this.polygon[(i * 2) + 1] += dy;
				}

				this.dragX = x;
				this.dragY = y;
				this.InvalidateVertices();
				return true;
			}

			if (this.edge >= 0)
			{
				int n1 = this.edge;
				int n2 = (n1 + this.NumPoints - 1) % this.NumPoints;
				double dx = x - this.dragX;
				double dy = y - this.dragY;
				this.polygon[n1 * 2] += dx;
				this.polygon[(n1 * 2) + 1] += dy;
				this.polygon[n2 * 2] += dx;
				this.polygon[(n2 * 2) + 1] += dy;
				this.dragX = x;
				this.dragY = y;
				this.InvalidateVertices();
				return true;
			}

			if (this.Node >= 0)
			{
				this.SetPoint(this.Node, x - this.dragX, y - this.dragY);
				return true;
			}

			return false;
		}

		/// <summary>Lets go of whatever was grabbed.</summary>
		/// <returns>True when something had been grabbed (its point shrinks back, so redraw).</returns>
		public bool OnMouseButtonUp(double x, double y)
		{
			bool grabbed = this.Node >= 0 || this.edge >= 0;
			this.Node = -1;
			this.edge = -1;
			this.InvalidateVertices();
			return grabbed;
		}

		// C++ check_edge: whether (x, y) is within PointRadius of edge i, measured along the perpendicular and
		// only strictly between the edge's ends.
		private bool CheckEdge(int i, double x, double y)
		{
			int n1 = i;
			int n2 = (i + this.NumPoints - 1) % this.NumPoints;
			double x1 = this.polygon[n1 * 2];
			double y1 = this.polygon[(n1 * 2) + 1];
			double x2 = this.polygon[n2 * 2];
			double y2 = this.polygon[(n2 * 2) + 1];

			double dx = x2 - x1;
			double dy = y2 - y1;
			if (Math.Sqrt((dx * dx) + (dy * dy)) <= 0.0000001)
			{
				return false;
			}

			double x3 = x;
			double y3 = y;
			double x4 = x3 - dy;
			double y4 = y3 + dx;

			double den = ((y4 - y3) * (x2 - x1)) - ((x4 - x3) * (y2 - y1));
			double u1 = (((x4 - x3) * (y1 - y3)) - ((y4 - y3) * (x1 - x3))) / den;

			double xi = x1 + (u1 * (x2 - x1));
			double yi = y1 + (u1 * (y2 - y1));

			dx = xi - x;
			dy = yi - y;
			return u1 > 0.0 && u1 < 1.0 && Math.Sqrt((dx * dx) + (dy * dy)) <= this.PointRadius;
		}

		// C++ point_in_polygon: Eric Haines' crossings-multiply test, a +X ray from (tx, ty) counted against
		// every edge.
		private bool PointInPolygon(double tx, double ty)
		{
			int numPoints = this.NumPoints;
			if (numPoints < 3)
			{
				return false;
			}

			double vtx0 = this.polygon[(numPoints - 1) * 2];
			double vty0 = this.polygon[((numPoints - 1) * 2) + 1];
			bool yflag0 = vty0 >= ty;

			double vtx1 = this.polygon[0];
			double vty1 = this.polygon[1];

			bool inside = false;
			for (int j = 1; j <= numPoints; j++)
			{
				bool yflag1 = vty1 >= ty;
				if (yflag0 != yflag1
					&& (((vty1 - ty) * (vtx0 - vtx1)) >= ((vtx1 - tx) * (vty0 - vty1))) == yflag1)
				{
					inside = !inside;
				}

				yflag0 = yflag1;
				vtx0 = vtx1;
				vty0 = vty1;

				int k = j >= numPoints ? j - numPoints : j;
				vtx1 = this.polygon[k * 2];
				vty1 = this.polygon[(k * 2) + 1];
			}

			return inside;
		}
	}
}
