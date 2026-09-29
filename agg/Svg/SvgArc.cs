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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// Elliptical arcs - of circles, ellipses, rounded rect corners and path A commands - as usvg builds them:
	/// kurbo's Arc::append_iter at usvg's tolerance of 0.1 user units, equal cubic Béziers of at most 90 degrees,
	/// more of them for a radius over about 367 units.
	/// </summary>
	/// <remarks>
	/// The resvg suite's references are not all from one resvg: its older cases (circle/simple-case among them)
	/// were drawn when usvg made arcs of 45 degree quadratics, which bulge 0.3% of the radius outward between
	/// 0, 45 and 90 degrees. Drawing quadratics passes those and fails more of the newer ones, so arcs stay
	/// usvg 0.45's cubics.
	/// </remarks>
	public static class SvgArc
	{
		/// <summary>usvg's arc tolerance (usvg/src/parser/shapes.rs, arc_to: to_cubic_beziers(0.1, ...)).</summary>
		private const double Tolerance = 0.1;

		/// <summary>
		/// Appends the arc of the ellipse centred on (<paramref name="cx"/>, <paramref name="cy"/>) with radii
		/// <paramref name="rx"/>, <paramref name="ry"/> turned by <paramref name="xRotation"/> (radians), from
		/// parameter angle <paramref name="startAngle"/> through <paramref name="sweepAngle"/>. The current point
		/// must already be at the arc's start.
		/// </summary>
		public static void AppendCubics(VertexStorage path, double cx, double cy, double rx, double ry, double xRotation, double startAngle, double sweepAngle)
		{
			// kurbo's count: enough subdivisions per whole ellipse for the tolerance, never fewer than four.
			double perEllipse = Math.Max(Math.Pow(1.1163 * Math.Max(rx, ry) / Tolerance, 1.0 / 6), 3.999999);
			int steps = Math.Max(1, (int)Math.Ceiling(perEllipse * Math.Abs(sweepAngle) / (2 * Math.PI)));
			double step = sweepAngle / steps;
			double arm = 4.0 / 3 * Math.Tan(Math.Abs(step) / 4) * Math.Sign(sweepAngle);
			double cos = Math.Cos(xRotation), sin = Math.Sin(xRotation);
			Vector2 Sample(double angle)
			{
				double x = rx * Math.Cos(angle), y = ry * Math.Sin(angle);
				return new Vector2(x * cos - y * sin, y * cos + x * sin);
			}

			var center = new Vector2(cx, cy);
			double angle0 = startAngle;
			Vector2 p0 = Sample(angle0);
			for (int i = 0; i < steps; i++)
			{
				double angle1 = angle0 + step;
				Vector2 p3 = Sample(angle1);
				Vector2 p1 = p0 + arm * Sample(angle0 + Math.PI / 2);
				Vector2 p2 = p3 - arm * Sample(angle1 + Math.PI / 2);
				path.Curve4(center.X + p1.X, center.Y + p1.Y, center.X + p2.X, center.Y + p2.Y, center.X + p3.X, center.Y + p3.Y);
				angle0 = angle1;
				p0 = p3;
			}
		}

		/// <summary>
		/// Appends an SVG path arc (the A command) from <paramref name="from"/> to <paramref name="to"/> as kurbo's
		/// SvgArc reads it (SVG 1.1 appendix F.6): radii too small to reach are scaled up, and an arc with a
		/// radius under 1e-5 or to its own start is a straight line - still a segment, which a marker or a cap sees.
		/// </summary>
		public static void AppendSvgArc(VertexStorage path, Vector2 from, Vector2 radii, double xRotationDegrees, bool largeArc, bool sweep, Vector2 to)
		{
			double rx = Math.Abs(radii.X), ry = Math.Abs(radii.Y);
			if (rx <= 1e-5 || ry <= 1e-5 || from == to)
			{
				path.LineTo(to.X, to.Y);
				return;
			}

			double phi = xRotationDegrees * Math.PI / 180;
			double cos = Math.Cos(phi), sin = Math.Sin(phi);
			double hx = (from.X - to.X) / 2, hy = (from.Y - to.Y) / 2;
			double x1 = cos * hx + sin * hy, y1 = -sin * hx + cos * hy;
			double lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
			if (lambda > 1)
			{
				double grow = Math.Sqrt(lambda);
				rx *= grow;
				ry *= grow;
			}

			double numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
			double denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
			double root = numerator <= 0 || denominator == 0 ? 0 : Math.Sqrt(numerator / denominator);
			if (largeArc == sweep)
			{
				root = -root;
			}

			double cxPrime = root * rx * y1 / ry, cyPrime = -root * ry * x1 / rx;
			double cx = cos * cxPrime - sin * cyPrime + (from.X + to.X) / 2;
			double cy = sin * cxPrime + cos * cyPrime + (from.Y + to.Y) / 2;
			double startAngle = Math.Atan2((y1 - cyPrime) / ry, (x1 - cxPrime) / rx);
			double endAngle = Math.Atan2((-y1 - cyPrime) / ry, (-x1 - cxPrime) / rx);
			double sweepAngle = endAngle - startAngle;
			if (sweep && sweepAngle < 0)
			{
				sweepAngle += 2 * Math.PI;
			}
			else if (!sweep && sweepAngle > 0)
			{
				sweepAngle -= 2 * Math.PI;
			}

			AppendCubics(path, cx, cy, rx, ry, phi, startAngle, sweepAngle);
		}
	}
}
