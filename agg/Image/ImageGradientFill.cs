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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// <see cref="ImageGraphics2D"/>'s <see cref="IGradientFillGraphics"/>: the path is rasterized as any fill is and
	/// its spans come from span_gradient itself - with span_gradient_alpha over them when an alpha gradient is given -
	/// so the software surface is the reference the GPU one is compared with.
	/// </summary>
	internal static class ImageGradientFill
	{
		public static void Fill(ImageGraphics2D graphics, IVertexSource path, GradientFill gradient, GradientFill alphaGradient)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (gradient?.Colors == null || (alphaGradient != null && alphaGradient.Colors == null))
			{
				throw new ArgumentException("A gradient fill needs its colour function.", nameof(gradient));
			}

			ISpanGenerator spanGenerator = new span_gradient(new span_interpolator_linear(gradient.ScreenToGradient), GradientFunction(gradient), gradient.Colors, gradient.D1, gradient.D2);
			if (alphaGradient != null)
			{
				// span_gradient_alpha takes a byte table; GradientFill's colour function supplies it through its alphas.
				var alphas = new byte[alphaGradient.Colors.size()];
				for (int i = 0; i < alphas.Length; i++)
				{
					alphas[i] = alphaGradient.Colors[i].alpha;
				}

				var alphaSpans = new span_gradient_alpha(new span_interpolator_linear(alphaGradient.ScreenToGradient), GradientFunction(alphaGradient), alphas, alphaGradient.D1, alphaGradient.D2);
				spanGenerator = new span_converter(spanGenerator, alphaSpans);
			}

			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(path, graphics.GetTransform()));

			IImageByte destination = graphics.DestImage;
			// Straight span colours blend straight-over into a backbuffer; see ImageGraphics2D.StraightOverDestination.
			IImageByte target = graphics.StraightOverDestination() ?? new ImageClippingProxy(destination);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), target, new span_allocator(), spanGenerator);
			destination.MarkImageChanged();
		}

		/// <summary>The C++ gradient function <paramref name="gradient"/> names, wrapped in its spread's adaptor.</summary>
		private static IGradient GradientFunction(GradientFill gradient)
		{
			IGradient function = gradient.Shape switch
			{
				GradientShape.X => new gradient_x(),
				GradientShape.Y => new gradient_y(),
				GradientShape.Radial => new gradient_radial(),
				GradientShape.Diamond => new gradient_diamond(),
				GradientShape.XY => new gradient_xy(),
				GradientShape.SqrtXY => new gradient_sqrt_xy(),
				GradientShape.Conic => new gradient_conic(),
				GradientShape.RadialFocus => new gradient_radial_focus(gradient.FocusRadius, gradient.FocusX, gradient.FocusY),
				_ => throw new ArgumentOutOfRangeException(nameof(gradient), gradient.Shape, "Unknown gradient shape."),
			};

			return gradient.Spread switch
			{
				GradientSpread.Repeat => new gradient_repeat_adaptor(function),
				GradientSpread.Reflect => new gradient_reflect_adaptor(function),
				_ => function,
			};
		}
	}
}
