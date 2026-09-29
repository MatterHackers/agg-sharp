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

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// A radial gradient drawn as the two-point cone resvg (and canvas) draw, used when agg's gradient_radial_focus
	/// cannot: when the focus is on or outside the circle, or SVG 2's fr gives the focus a circle of its own. The
	/// circles grow from the focal circle (radius fr at t = 0) to the gradient circle (t = 1), and a point takes the
	/// largest t whose circle, with a radius not below 0, passes through it. Pulling the focus inside would change the
	/// picture; here the area no circle reaches stays unpainted (<see cref="Reaches"/>).
	/// Works in span_gradient's subpixel units, with the circle at the origin and radius d.
	/// </summary>
	internal sealed class SvgFocalCone : IGradient
	{
		private readonly double focusX;
		private readonly double focusY;
		private readonly double focalRadius;
		private readonly double radius;

		public SvgFocalCone(double radius, double focusX, double focusY, double focalRadius = 0)
		{
			this.radius = radius * span_gradient.gradient_subpixel_scale;
			this.focusX = focusX * span_gradient.gradient_subpixel_scale;
			this.focusY = focusY * span_gradient.gradient_subpixel_scale;
			this.focalRadius = focalRadius * span_gradient.gradient_subpixel_scale;
		}

		public int calculate(int x, int y, int d)
		{
			return Solve(x, y, out double t) ? (int)Math.Round(t * d) : 0;
		}

		/// <summary>Whether any circle of the cone (radius >= 0) passes through (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public bool Reaches(int x, int y) => Solve(x, y, out _);

		/// <summary>
		/// Solves |q - t e| = fr + t dr for the largest t whose radius fr + t dr is not negative, with q = p - f,
		/// e = c - f (c the origin) and dr = r - fr: a t^2 - 2 b t + k = 0 where a = e.e - dr^2, b = q.e + fr dr and
		/// k = q.q - fr^2.
		/// </summary>
		private bool Solve(int x, int y, out double t)
		{
			double qx = x - this.focusX, qy = y - this.focusY;
			double ex = -this.focusX, ey = -this.focusY;
			double dr = this.radius - this.focalRadius;
			double a = ex * ex + ey * ey - dr * dr;
			double b = qx * ex + qy * ey + this.focalRadius * dr;
			double k = qx * qx + qy * qy - this.focalRadius * this.focalRadius;
			t = 0;
			if (Math.Abs(a) <= 1e-9 * this.radius * this.radius)
			{
				// The focal circle touches the gradient circle from inside: one root.
				if (k == 0)
				{
					return true;
				}

				if (b == 0)
				{
					return false;
				}

				t = k / (2 * b);
				return this.focalRadius + t * dr >= 0;
			}

			double discriminant = b * b - a * k;
			if (discriminant < 0)
			{
				return false;
			}

			double root = Math.Sqrt(discriminant);
			double t1 = (b + root) / a, t2 = (b - root) / a;
			t = Math.Max(t1, t2);
			if (this.focalRadius + t * dr >= 0)
			{
				return true;
			}

			t = Math.Min(t1, t2);
			return this.focalRadius + t * dr >= 0;
		}

		/// <summary>span_gradient's colours, with the pixels no circle of the cone reaches left transparent.</summary>
		internal sealed class Spans : ISpanGenerator
		{
			private readonly span_gradient colors;
			private readonly SvgFocalCone cone;
			private readonly ISpanInterpolator interpolator;

			public Spans(span_gradient colors, SvgFocalCone cone, ISpanInterpolator interpolator)
			{
				this.colors = colors;
				this.cone = cone;
				this.interpolator = interpolator;
			}

			public void prepare() => this.colors.prepare();

			public void generate(Color[] span, int spanIndex, int x, int y, int len)
			{
				this.colors.generate(span, spanIndex, x, y, len);
				this.interpolator.begin(x + 0.5, y + 0.5, len);
				for (int i = 0; i < len; i++)
				{
					this.interpolator.coordinates(out int ix, out int iy);
					if (!this.cone.Reaches(ix >> span_gradient.downscale_shift, iy >> span_gradient.downscale_shift))
					{
						span[spanIndex + i] = new Color(0, 0, 0, 0);
					}

					this.interpolator.Next();
				}
			}
		}
	}
}
