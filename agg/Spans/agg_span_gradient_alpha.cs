//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
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

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's <c>span_gradient_alpha</c>: like <see cref="span_gradient"/>, but it only replaces the alpha
	/// of spans another generator already filled, from a table of alphas. Used as the converter of a
	/// <see cref="span_converter"/>.
	/// </summary>
	public class span_gradient_alpha : ISpanGenerator
	{
		private readonly ISpanInterpolator interpolator;
		private readonly IGradient gradientFunction;
		private readonly byte[] alphaFunction;
		private readonly int d1;
		private readonly int d2;

		/// <param name="alphaFunction">The alphas, indexed like a color function's colors (C++ <c>AlphaF</c>).</param>
		public span_gradient_alpha(ISpanInterpolator interpolator, IGradient gradientFunction, byte[] alphaFunction, double d1, double d2)
		{
			this.interpolator = interpolator;
			this.gradientFunction = gradientFunction;
			this.alphaFunction = alphaFunction;
			this.d1 = Util.iround(d1 * span_gradient.gradient_subpixel_scale);
			this.d2 = Util.iround(d2 * span_gradient.gradient_subpixel_scale);
		}

		public void prepare()
		{
		}

		public void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			int dd = this.d2 - this.d1;
			if (dd < 1)
			{
				dd = 1;
			}

			int size = this.alphaFunction.Length;
			this.interpolator.begin(x + 0.5, y + 0.5, len);
			do
			{
				this.interpolator.coordinates(out x, out y);
				int d = this.gradientFunction.calculate(x >> span_gradient.downscale_shift, y >> span_gradient.downscale_shift, this.d2);
				d = ((d - this.d1) * size) / dd;
				if (d < 0)
				{
					d = 0;
				}

				if (d >= size)
				{
					d = size - 1;
				}

				span[spanIndex++].alpha = this.alphaFunction[d];
				this.interpolator.Next();
			}
			while (--len != 0);
		}
	}

	/// <summary>
	/// C++ AGG's <c>span_converter</c>: a span generator followed by a converter that rewrites what it made
	/// (such as <see cref="span_gradient_alpha"/>), both over the same span.
	/// </summary>
	public class span_converter : ISpanGenerator
	{
		private readonly ISpanGenerator spanGenerator;
		private readonly ISpanGenerator spanConverter;

		public span_converter(ISpanGenerator spanGenerator, ISpanGenerator spanConverter)
		{
			this.spanGenerator = spanGenerator;
			this.spanConverter = spanConverter;
		}

		public void prepare()
		{
			this.spanGenerator.prepare();
			this.spanConverter.prepare();
		}

		public void generate(Color[] span, int spanIndex, int x, int y, int len)
		{
			this.spanGenerator.generate(span, spanIndex, x, y, len);
			this.spanConverter.generate(span, spanIndex, x, y, len);
		}
	}
}
