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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="IGradientFillGraphics"/> on <c>Graphics2DGpu</c> against a real device, compared with software
	/// span_gradient filling the same rectangle with the same matrix.
	/// </summary>
	/// <remarks>
	/// Only pixels at least <see cref="Margin"/> inside the rectangle are compared, so the GPU's anti-aliasing (not
	/// AGG's) stays out of it. The shader transforms each pixel centre in floats where span_interpolator_linear
	/// steps integer subpixels, so a pixel on a table-entry boundary may take its neighbour; with a smooth
	/// 256-entry ramp that is at most two units per channel, or, where the gradient is steep, a neighbouring
	/// software pixel's colour.
	/// </remarks>
	[NotInParallel]
	public class GpuGradientFillTests
	{
		private const int Width = 120;
		private const int Height = 90;
		private const int Margin = 3;
		private const int Tolerance = 2;

		/// <summary>
		/// SqrtXY's tolerance, per channel. The test ramp moves about one unit per colour-table entry, so this is
		/// fast_sqrt's error in entries: the worst pixel measured is 9 (four pixels over <see cref="Tolerance"/>).
		/// </summary>
		private const int FastSqrtTolerance = 12;

		private static readonly IVertexSource Shape = new RoundedRect(0, 0, Width, Height, 0);

		[Test]
		[Arguments(GradientShape.X, GradientSpread.Pad)]
		[Arguments(GradientShape.Radial, GradientSpread.Reflect)]
		[Arguments(GradientShape.Diamond, GradientSpread.Repeat)]
		[Arguments(GradientShape.XY, GradientSpread.Reflect)]
		[Arguments(GradientShape.Conic, GradientSpread.Pad)]
		[Arguments(GradientShape.RadialFocus, GradientSpread.Reflect)]
		public async Task GradientMatchesSpanGradient(GradientShape shape, GradientSpread spread)
		{
			await CompareWithSpanGradient(shape, spread, Tolerance);
		}

		/// <summary>
		/// SqrtXY at a wider tolerance. Software gradient_sqrt_xy runs through agg_math.fast_sqrt, C++ AGG's
		/// table-driven approximate square root, kept C++-exact on purpose; the shader takes the exact sqrt. The
		/// approximation's error is a few colour-table entries near the gradient's centre, so this compares at
		/// <see cref="FastSqrtTolerance"/> instead of leaving the shape untested.
		/// </summary>
		[Test]
		public async Task SqrtXYMatchesSpanGradientWithinFastSqrtError()
		{
			await CompareWithSpanGradient(GradientShape.SqrtXY, GradientSpread.Reflect, FastSqrtTolerance);
		}

		private static async Task CompareWithSpanGradient(GradientShape shape, GradientSpread spread, int tolerance)
		{
			var colors = new gradient_linear_color(new Color(255, 20, 0, 255), new Color(0, 90, 255, 200), 256);

			// Gradient space centred on the image, rotated and squashed, as the demos' matrices are.
			Affine gradientToScreen = Affine.NewScaling(1.1, 0.7) * Affine.NewRotation(0.4) * Affine.NewTranslation(57.3, 41.8);
			Affine screenToGradient = gradientToScreen;
			screenToGradient.invert();

			var fill = new GradientFill
			{
				Shape = shape,
				Spread = spread,
				D1 = 0,
				D2 = 40,
				ScreenToGradient = screenToGradient,
				Colors = colors,
				FocusRadius = 40,
				FocusX = 12,
				FocusY = -7,
			};

			ImageBuffer gpu = await Capture(graphics =>
			{
				graphics.FillRectangle(0, 0, Width, Height, Color.White);
				((IGradientFillGraphics)graphics).FillPathWithGradient(Shape, fill);
			});

			var software = new ImageBuffer(Width, Height);
			software.NewGraphics2D().FillRectangle(0, 0, Width, Height, Color.White);
			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(Shape);
			var spans = new span_gradient(new span_interpolator_linear(screenToGradient), SoftwareGradient(fill), colors, fill.D1, fill.D2);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), software, new span_allocator(), spans);

			await AssertInteriorNear(gpu, software, tolerance);
		}

		/// <summary>
		/// ImageGraphics2D's FillPathWithGradient is span_gradient itself (and span_gradient_alpha over it), so a
		/// caller need not branch on the surface: byte for byte what the span generators give.
		/// </summary>
		[Test]
		public async Task SoftwareGradientFillIsSpanGradient()
		{
			var colors = new gradient_linear_color(new Color(255, 20, 0, 255), new Color(0, 90, 255, 200), 256);
			Affine screenToGradient = Affine.NewTranslation(-57.3, -41.8);
			var fill = new GradientFill { Shape = GradientShape.Radial, Spread = GradientSpread.Reflect, D2 = 40, ScreenToGradient = screenToGradient, Colors = colors };
			var alphaColors = new gradient_linear_color(new Color(0, 0, 0, 255), new Color(0, 0, 0, 30), 256);
			var alphaFill = new GradientFill { Shape = GradientShape.X, D2 = 100, ScreenToGradient = Affine.NewIdentity(), Colors = alphaColors };

			var image = new ImageBuffer(Width, Height);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IGradientFillGraphics)graphics).FillPathWithGradient(Shape, fill, alphaFill);

			var expected = new ImageBuffer(Width, Height);
			expected.NewGraphics2D().FillRectangle(0, 0, Width, Height, Color.White);
			var alphas = new byte[256];
			for (int i = 0; i < alphas.Length; i++)
			{
				alphas[i] = alphaColors[i].alpha;
			}

			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(Shape);
			var spans = new span_converter(
				new span_gradient(new span_interpolator_linear(screenToGradient), SoftwareGradient(fill), colors, 0, 40),
				new span_gradient_alpha(new span_interpolator_linear(Affine.NewIdentity()), new gradient_x(), alphas, 0, 100));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), expected, new span_allocator(), spans);

			await Assert.That(image.GetBuffer().AsSpan().SequenceEqual(expected.GetBuffer())).IsTrue();
		}

		private static IGradient SoftwareGradient(GradientFill fill)
		{
			IGradient function = fill.Shape switch
			{
				GradientShape.X => new gradient_x(),
				GradientShape.Radial => new gradient_radial(),
				GradientShape.Diamond => new gradient_diamond(),
				GradientShape.XY => new gradient_xy(),
				GradientShape.SqrtXY => new gradient_sqrt_xy(),
				GradientShape.Conic => new gradient_conic(),
				_ => new gradient_radial_focus(fill.FocusRadius, fill.FocusX, fill.FocusY),
			};

			return fill.Spread switch
			{
				GradientSpread.Repeat => new gradient_repeat_adaptor(function),
				GradientSpread.Reflect => new gradient_reflect_adaptor(function),
				_ => function,
			};
		}

		private static async Task<ImageBuffer> Capture(Action<Graphics2D> draw)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			draw(capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0)));
			return await capture.CaptureAsync();
		}

		private static int Difference(Color a, Color b)
			=> Math.Max(Math.Max(Math.Abs(a.red - b.red), Math.Abs(a.green - b.green)), Math.Max(Math.Abs(a.blue - b.blue), Math.Abs(a.alpha - b.alpha)));

		private static async Task AssertInteriorNear(ImageBuffer gpu, ImageBuffer software, int tolerance)
		{
			for (int y = Margin; y < Height - Margin; y++)
			{
				for (int x = Margin; x < Width - Margin; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = software.GetPixel(x, y);
					int worst = Difference(g, s);

					// Where the gradient is steep (sqrt_xy near its axes) a sub-pixel difference in the transformed
					// coordinate moves the index several entries; the GPU then matches a neighbouring software pixel.
					for (int i = 0; i < 4 && worst > tolerance; i++)
					{
						worst = Math.Min(worst, Difference(g, software.GetPixel(x + (i == 0 ? 1 : i == 1 ? -1 : 0), y + (i == 2 ? 1 : i == 3 ? -1 : 0))));
					}

					if (worst > tolerance)
					{
						await Assert.That(worst).IsLessThanOrEqualTo(tolerance)
							.Because($"({x}, {y}): GPU {g} vs software {s}");
					}
				}
			}
		}
	}
}
