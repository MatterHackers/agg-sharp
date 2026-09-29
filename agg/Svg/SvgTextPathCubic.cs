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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// One segment of a textPath as usvg measures it: a cubic Bezier with kurbo 0.11.3's arc length (adaptive
	/// Gauss-Legendre quadrature) and its inverse (an ITP solve). Neither is exact - both work to a requested accuracy -
	/// and usvg asks for only half a unit, so glyphs land where kurbo's approximation puts them, a fraction of a unit
	/// from the true arc length. Ported as is so they land in the same place here.
	/// </summary>
	internal readonly struct SvgTextPathCubic
	{
		// Weight and abscissa pairs, the positive half of each symmetric Gauss-Legendre rule (kurbo's common.rs).
		private static readonly (double W, double X)[] Gauss8 =
		{
			(0.3626837833783620, -0.1834346424956498), (0.3626837833783620, 0.1834346424956498),
			(0.3137066458778873, -0.5255324099163290), (0.3137066458778873, 0.5255324099163290),
			(0.2223810344533745, -0.7966664774136267), (0.2223810344533745, 0.7966664774136267),
			(0.1012285362903763, -0.9602898564975363), (0.1012285362903763, 0.9602898564975363),
		};

		private static readonly (double W, double X)[] Gauss8Half =
		{
			(0.3626837833783620, 0.1834346424956498), (0.3137066458778873, 0.5255324099163290),
			(0.2223810344533745, 0.7966664774136267), (0.1012285362903763, 0.9602898564975363),
		};

		private static readonly (double W, double X)[] Gauss16Half =
		{
			(0.1894506104550685, 0.0950125098376374), (0.1826034150449236, 0.2816035507792589),
			(0.1691565193950025, 0.4580167776572274), (0.1495959888165767, 0.6178762444026438),
			(0.1246289712555339, 0.7554044083550030), (0.0951585116824928, 0.8656312023878318),
			(0.0622535239386479, 0.9445750230732326), (0.0271524594117541, 0.9894009349916499),
		};

		private static readonly (double W, double X)[] Gauss24Half =
		{
			(0.1279381953467522, 0.0640568928626056), (0.1258374563468283, 0.1911188674736163),
			(0.1216704729278034, 0.3150426796961634), (0.1155056680537256, 0.4337935076260451),
			(0.1074442701159656, 0.5454214713888396), (0.0976186521041139, 0.6480936519369755),
			(0.0861901615319533, 0.7401241915785544), (0.0733464814110803, 0.8200019859739029),
			(0.0592985849154368, 0.8864155270044011), (0.0442774388174198, 0.9382745520027328),
			(0.0285313886289337, 0.9747285559713095), (0.0123412297999872, 0.9951872199970213),
		};

		public readonly Vector2 P0, P1, P2, P3;

		public SvgTextPathCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
		{
			P0 = p0;
			P1 = p1;
			P2 = p2;
			P3 = p3;
		}

		/// <summary>
		/// A line as usvg's collect_normals makes it a cubic: controls at t = 0.33 and 0.66, not thirds, so the
		/// parameter does not move at a uniform speed along it.
		/// </summary>
		public static SvgTextPathCubic FromLine(Vector2 p0, Vector2 p1) => new(p0, p0 + (p1 - p0) * 0.33, p0 + (p1 - p0) * 0.66, p1);

		/// <summary>A quadratic raised to the cubic that traces it (kurbo's QuadBez::raise).</summary>
		public static SvgTextPathCubic FromQuadratic(Vector2 p0, Vector2 control, Vector2 p2)
			=> new(p0, p0 + (2.0 / 3.0) * (control - p0), p2 + (2.0 / 3.0) * (control - p2), p2);

		public Vector2 Evaluate(double t)
		{
			double mt = 1 - t;
			return P0 * (mt * mt * mt) + (P1 * (mt * mt * 3) + (P2 * (mt * 3) + P3 * t) * t) * t;
		}

		/// <summary>The first derivative at <paramref name="t"/>.</summary>
		public Vector2 Derivative(double t)
		{
			// kurbo's deriv(): the quadratic on 3 (P1 - P0), 3 (P2 - P1), 3 (P3 - P2), evaluated as a QuadBez.
			Vector2 d0 = 3 * (P1 - P0), d1 = 3 * (P2 - P1), d2 = 3 * (P3 - P2);
			double mt = 1 - t;
			return d0 * (mt * mt) + (d1 * (mt * 2) + d2 * t) * t;
		}

		/// <summary>The part of this cubic between parameters <paramref name="t0"/> and <paramref name="t1"/>.</summary>
		public SvgTextPathCubic Subsegment(double t0, double t1)
		{
			Vector2 p0 = Evaluate(t0), p3 = Evaluate(t1);
			double scale = (t1 - t0) * (1.0 / 3.0);
			return new SvgTextPathCubic(p0, p0 + scale * Derivative(t0), p3 - scale * Derivative(t1), p3);
		}

		/// <summary>kurbo's arclen: the length to within <paramref name="accuracy"/>.</summary>
		public double ArcLength(double accuracy) => ArcLength(this, accuracy, 0);

		/// <summary>
		/// kurbo's inv_arclen: the parameter <paramref name="arcLength"/> along, solved to within
		/// <paramref name="accuracy"/> of arc length.
		/// </summary>
		public double InverseArcLength(double arcLength, double accuracy)
		{
			if (arcLength <= 0)
			{
				return 0;
			}

			double total = ArcLength(accuracy);
			if (arcLength >= total)
			{
				return 1;
			}

			double tLast = 0, arcLast = 0;
			double epsilon = accuracy / total;
			double n = 1 - Math.Min(Math.Ceiling(Math.Log2(epsilon)), 0);
			double innerAccuracy = accuracy / n;
			SvgTextPathCubic self = this;

			// Measures only the piece since the last guess, as kurbo does; its rounding is part of the answer.
			double F(double t)
			{
				double arc = t > tLast ? self.Subsegment(tLast, t).ArcLength(innerAccuracy) : -self.Subsegment(t, tLast).ArcLength(innerAccuracy);
				arcLast += arc;
				tLast = t;
				return arcLast - arcLength;
			}

			return SolveItp(F, 0, 1, epsilon, 1, 0.2, -arcLength, total - arcLength);
		}

		private static double ArcLength(SvgTextPathCubic c, double accuracy, int depth)
		{
			Vector2 d03 = c.P3 - c.P0, d01 = c.P1 - c.P0, d12 = c.P2 - c.P1, d23 = c.P3 - c.P2;
			double lpLc = d01.Length + d12.Length + d23.Length - d03.Length;
			Vector2 dd1 = d12 - d01, dd2 = d23 - d12;
			Vector2 dm = 0.25 * (d01 + d23) + 0.5 * d12;
			Vector2 dm1 = 0.5 * (dd2 + dd1);
			Vector2 dm2 = 0.25 * (dd2 - dd1);

			double est = 0;
			foreach (var (w, x) in Gauss8)
			{
				double dNorm2 = (dm + dm1 * x + dm2 * (x * x)).LengthSquared;
				double ddNorm2 = (dm1 + dm2 * (2 * x)).LengthSquared;
				est += w * (ddNorm2 / dNorm2);
			}

			if (Math.Min(est * est * est * 2.5e-6, 3e-2) * lpLc < accuracy)
			{
				return Quadrature(Gauss8Half, dm, dm1, dm2);
			}

			if (Math.Min(Math.Pow(est, 6) * 1.5e-11, 9e-3) * lpLc < accuracy)
			{
				return Quadrature(Gauss16Half, dm, dm1, dm2);
			}

			if (Math.Min(Math.Pow(est, 9) * 3.5e-16, 3.5e-3) * lpLc < accuracy || depth >= 20)
			{
				return Quadrature(Gauss24Half, dm, dm1, dm2);
			}

			// de Casteljau halves.
			Vector2 pm = c.Evaluate(0.5);
			var first = new SvgTextPathCubic(c.P0, (c.P0 + c.P1) * 0.5, (c.P0 + c.P1 * 2 + c.P2) * 0.25, pm);
			var second = new SvgTextPathCubic(pm, (c.P1 + c.P2 * 2 + c.P3) * 0.25, (c.P2 + c.P3) * 0.5, c.P3);
			return ArcLength(first, accuracy * 0.5, depth + 1) + ArcLength(second, accuracy * 0.5, depth + 1);
		}

		private static double Quadrature((double W, double X)[] coefficients, Vector2 dm, Vector2 dm1, Vector2 dm2)
		{
			double sum = 0;
			foreach (var (w, x) in coefficients)
			{
				Vector2 d = dm + dm2 * (x * x);
				sum += 1.5 * w * ((d + dm1 * x).Length + (d - dm1 * x).Length);
			}

			return sum;
		}

		/// <summary>kurbo's solve_itp: a root of the increasing <paramref name="f"/> on [a, b] to within epsilon.</summary>
		private static double SolveItp(Func<double, double> f, double a, double b, double epsilon, int n0, double k1, double ya, double yb)
		{
			int n1_2 = (int)Math.Max(Math.Ceiling(Math.Log2((b - a) / epsilon)) - 1, 0);
			int nmax = n0 + n1_2;
			double scaledEpsilon = epsilon * (1UL << nmax);
			while (b - a > 2 * epsilon)
			{
				double x1_2 = 0.5 * (a + b);
				double r = scaledEpsilon - 0.5 * (b - a);
				double xf = (yb * a - ya * b) / (yb - ya);
				double sigma = x1_2 - xf;
				double delta = k1 * (b - a) * (b - a);
				double xt = delta <= Math.Abs(x1_2 - xf) ? xf + Math.CopySign(delta, sigma) : x1_2;
				double xitp = Math.Abs(xt - x1_2) <= r ? xt : x1_2 - Math.CopySign(r, sigma);
				double yitp = f(xitp);
				if (yitp > 0)
				{
					b = xitp;
					yb = yitp;
				}
				else if (yitp < 0)
				{
					a = xitp;
					ya = yitp;
				}
				else
				{
					return xitp;
				}

				scaledEpsilon *= 0.5;
			}

			return 0.5 * (a + b);
		}
	}
}
