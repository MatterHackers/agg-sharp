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

namespace MatterHackers.Agg
{
	/// <summary>A C++ AGG color function over rgba32 (float) colors, as <see cref="SpanGradientFloat"/> reads it.</summary>
	public interface IColorFunctionFloat
	{
		int size();

		ColorF this[int v] { get; }
	}

	/// <summary>
	/// C++ <c>span_gradient&lt;rgba32, ...&gt;</c>: the same integer gradient walk as <see cref="span_gradient"/>, with
	/// float colors.
	/// </summary>
	public class SpanGradientFloat : ISpanGeneratorFloat
	{
		private const int DownscaleShift = span_gradient.downscale_shift;

		private readonly ISpanInterpolator interpolator;
		private readonly IGradient gradientFunction;
		private readonly IColorFunctionFloat colorFunction;
		private readonly int d1;
		private readonly int d2;

		public SpanGradientFloat(ISpanInterpolator interpolator, IGradient gradientFunction, IColorFunctionFloat colorFunction, double d1, double d2)
		{
			this.interpolator = interpolator;
			this.gradientFunction = gradientFunction;
			this.colorFunction = colorFunction;
			this.d1 = Util.iround(d1 * span_gradient.gradient_subpixel_scale);
			this.d2 = Util.iround(d2 * span_gradient.gradient_subpixel_scale);
		}

		public void prepare()
		{
		}

		public void generate(ColorF[] span, int spanIndex, int x, int y, int len)
		{
			int dd = d2 - d1;
			if (dd < 1)
			{
				dd = 1;
			}

			int size = colorFunction.size();
			interpolator.begin(x + 0.5, y + 0.5, len);
			do
			{
				interpolator.coordinates(out x, out y);
				int d = gradientFunction.calculate(x >> DownscaleShift, y >> DownscaleShift, d2);
				d = ((d - d1) * size) / dd;
				if (d < 0)
				{
					d = 0;
				}

				if (d >= size)
				{
					d = size - 1;
				}

				span[spanIndex++] = colorFunction[d];
				interpolator.Next();
			}
			while (--len != 0);
		}
	}

	/// <summary>
	/// C++ <c>gradient_linear_color&lt;rgba32&gt;</c>: <c>size</c> steps from c1 to c2 by rgba32::gradient, each channel
	/// <c>float(c1 + float(c2 - c1) * k)</c> with k = v * (1 / (size - 1)) in double, as C++ computes it.
	/// </summary>
	public class GradientLinearColorFloat : IColorFunctionFloat
	{
		private readonly ColorF c1;
		private readonly ColorF c2;
		private readonly int count;
		private readonly double mult;

		public GradientLinearColorFloat(ColorF c1, ColorF c2, int size = 256)
		{
			this.c1 = c1;
			this.c2 = c2;
			count = size;
			mult = 1 / ((double)size - 1);
		}

		public int size() => count;

		public ColorF this[int v]
		{
			get
			{
				double k = v * mult;
				return new ColorF(Lerp(c1.red, c2.red, k), Lerp(c1.green, c2.green, k), Lerp(c1.blue, c2.blue, k), Lerp(c1.alpha, c2.alpha, k));
			}
		}

		// The difference is float arithmetic, the scale and sum double, then narrowed - C++'s value_type(r + (c.r - r) * k).
		private static float Lerp(float a, float b, double k) => (float)(a + ((double)(b - a) * k));
	}
}
