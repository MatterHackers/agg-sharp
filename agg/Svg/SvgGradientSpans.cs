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

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// An SVG gradient's pixels: agg's gradient function (with its spread adaptor) gives each pixel's place along
	/// the gradient, and the stops are interpolated at exactly that place, premultiplied, and rounded once.
	/// span_gradient instead indexes a colour table with a floored (d * size) / length, which left about one pixel in
	/// eight a level low - an error lighting filters' surfaceScale magnifies into visible steps - and its d * size
	/// overflows int a few gradient lengths out once the table is made fine enough to fix that.
	/// </summary>
	internal sealed class SvgGradientSpans : ISpanGenerator
	{
		private readonly ISpanInterpolator interpolator;
		private readonly IGradient function;
		private readonly List<(double Offset, Color Color)> stops;
		private readonly int length;

		/// <param name="length">The gradient vector's (or radius's) length in gradient units; the function gets it in 1/16ths.</param>
		public SvgGradientSpans(ISpanInterpolator interpolator, IGradient function, List<(double Offset, Color Color)> stops, double length)
		{
			this.interpolator = interpolator;
			this.function = function;
			this.stops = stops;
			this.length = (int)Math.Round(length * span_gradient.gradient_subpixel_scale);
		}

		/// <summary>
		/// An interpolator coordinate (1/256 units) in the gradient functions' 1/16 units, rounded to nearest:
		/// span_gradient's shift floors, putting every pixel up to 1/16 unit short of its centre.
		/// </summary>
		public static int ToGradient(int coordinate) => (coordinate + (1 << (span_gradient.downscale_shift - 1))) >> span_gradient.downscale_shift;

		public void prepare()
		{
		}

		public void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			this.interpolator.begin(x + 0.5, y + 0.5, len);
			for (int i = 0; i < len; i++)
			{
				this.interpolator.coordinates(out int ix, out int iy);
				int d = this.function.calculate(ToGradient(ix), ToGradient(iy), this.length);
				span[spanIndex + i] = this.At(Math.Min(1, Math.Max(0, d / (double)this.length)));
				this.interpolator.Next();
			}
		}

		/// <summary>
		/// The premultiplied colour <paramref name="t"/> of the way along the stops: interpolated straight, as SVG
		/// specifies, then premultiplied, rounding once.
		/// </summary>
		private Color At(double t)
		{
			int next = this.stops.FindIndex(s => s.Offset > t);
			if (next == -1)
			{
				return SvgGradient.Premultiply(this.stops[this.stops.Count - 1].Color);
			}

			if (next == 0)
			{
				return SvgGradient.Premultiply(this.stops[0].Color);
			}

			(double Offset, Color Color) a = this.stops[next - 1], b = this.stops[next];
			double f = (t - a.Offset) / (b.Offset - a.Offset);
			double Lerp(int from, int to) => from + (to - from) * f;
			double alpha = Lerp(a.Color.alpha, b.Color.alpha);
			int Channel(int from, int to) => (int)Math.Round(Lerp(from, to) * alpha / 255);
			return new Color(Channel(a.Color.red, b.Color.red), Channel(a.Color.green, b.Color.green), Channel(a.Color.blue, b.Color.blue), (int)Math.Round(alpha));
		}
	}
}
