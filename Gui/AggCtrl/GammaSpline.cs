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

using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// C++ AGG's <c>gamma_spline</c>: a gamma curve through (0, 0), two control points and (1, 1), set by four
	/// values in [0.001, 1.999] (1.0 is a quarter of the box), and the 256-entry gamma array it produces.
	/// It is also an <see cref="IGammaFunction"/>, so a rasterizer can take it as C++ <c>ras.gamma(ctrl)</c> does.
	/// </summary>
	public class GammaSpline : IGammaFunction
	{
		private readonly byte[] gammaArray = new byte[256];
		private readonly double[] xs = new double[4];
		private readonly double[] ys = new double[4];
		private readonly bspline spline = new bspline();
		private double boxX1;
		private double boxY1;
		private double boxX2 = 10;
		private double boxY2 = 10;

		public GammaSpline()
		{
			this.SetValues(1.0, 1.0, 1.0, 1.0);
		}

		/// <summary>C++ <c>gamma()</c>: entry i is the curve at i / 255, times 255, truncated.</summary>
		public byte[] Gamma => this.gammaArray;

		/// <summary>C++ <c>values(kx1, ky1, kx2, ky2)</c>; each is clamped to [0.001, 1.999].</summary>
		public void SetValues(double kx1, double ky1, double kx2, double ky2)
		{
			if (kx1 < 0.001) kx1 = 0.001;
			if (kx1 > 1.999) kx1 = 1.999;
			if (ky1 < 0.001) ky1 = 0.001;
			if (ky1 > 1.999) ky1 = 1.999;
			if (kx2 < 0.001) kx2 = 0.001;
			if (kx2 > 1.999) kx2 = 1.999;
			if (ky2 < 0.001) ky2 = 0.001;
			if (ky2 > 1.999) ky2 = 1.999;

			this.xs[0] = 0.0;
			this.ys[0] = 0.0;
			this.xs[1] = kx1 * 0.25;
			this.ys[1] = ky1 * 0.25;
			this.xs[2] = 1.0 - (kx2 * 0.25);
			this.ys[2] = 1.0 - (ky2 * 0.25);
			this.xs[3] = 1.0;
			this.ys[3] = 1.0;

			this.spline.init(4, this.xs, this.ys);

			for (int i = 0; i < 256; i++)
			{
				this.gammaArray[i] = (byte)(this.Y(i / 255.0) * 255.0);
			}
		}

		/// <summary>C++ <c>values(&amp;kx1, ...)</c>: the four values as stored, after clamping.</summary>
		public void GetValues(out double kx1, out double ky1, out double kx2, out double ky2)
		{
			kx1 = this.xs[1] * 4.0;
			ky1 = this.ys[1] * 4.0;
			kx2 = (1.0 - this.xs[2]) * 4.0;
			ky2 = (1.0 - this.ys[2]) * 4.0;
		}

		/// <summary>C++ <c>y(x)</c>: the curve at <paramref name="x"/>, both clamped to [0, 1].</summary>
		public double Y(double x)
		{
			if (x < 0.0) x = 0.0;
			if (x > 1.0) x = 1.0;
			double val = this.spline.get(x);
			if (val < 0.0) val = 0.0;
			if (val > 1.0) val = 1.0;
			return val;
		}

		/// <summary>C++ <c>operator()</c>, what <c>rasterizer.gamma(spline)</c> samples.</summary>
		public double GetGamma(double x) => this.Y(x);

		/// <summary>C++ <c>box</c>: where <see cref="CurvePath"/> draws the curve.</summary>
		public void SetBox(double x1, double y1, double x2, double y2)
		{
			this.boxX1 = x1;
			this.boxY1 = y1;
			this.boxX2 = x2;
			this.boxY2 = y2;
		}

		/// <summary>C++ <c>rewind</c> + <c>vertex</c>: the curve as a polyline across the box, one vertex per x unit.</summary>
		public VertexStorage CurvePath()
		{
			var path = new VertexStorage();
			path.MoveTo(this.boxX1, this.boxY1);
			if (!(this.boxX2 > this.boxX1))
			{
				// An empty or reversed box (a control narrower than its borders) has no curve: C++ would step
				// x away from 1 forever here.
				return path;
			}

			double step = 1.0 / (this.boxX2 - this.boxX1);

			// x is accumulated step by step as C++ does, so the samples carry the same rounding.
			for (double curX = step; curX <= 1.0; curX += step)
			{
				path.LineTo(this.boxX1 + (curX * (this.boxX2 - this.boxX1)), this.boxY1 + (this.Y(curX) * (this.boxY2 - this.boxY1)));
			}

			return path;
		}
	}
}
