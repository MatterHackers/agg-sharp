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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's alpha_gradient.cpp: a circular color gradient whose alpha comes from a second, XY gradient laid
	/// on a draggable parallelogram, over 100 random translucent ellipses. The spline control shapes the alpha.
	/// Drag a corner or the whole parallelogram; the arrow keys nudge it.
	/// </summary>
	/// <remarks>
	/// The ellipses come from <see cref="MsvcRand"/> seeded 1234 (the example's <c>srand(1234)</c>), as they do
	/// in the C++ reference renderer, so the frame is the same on every platform.
	/// </remarks>
	public class AlphaGradientDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();
		private int dragIndex = -1;
		private double dragDx;
		private double dragDy;

		public AlphaGradientDemo()
		{
			// alpha_gradient.cpp runs with flip_y = true and gives its ctrl !flip_y.
			this.AlphaSpline = new SplineCtrl(2, 2, 200, 30, 6, false);
			this.AlphaSpline.SetPoint(0, 0.0, 0.0);
			this.AlphaSpline.SetPoint(1, 1.0 / 5.0, 1.0 - (4.0 / 5.0));
			this.AlphaSpline.SetPoint(2, 2.0 / 5.0, 1.0 - (3.0 / 5.0));
			this.AlphaSpline.SetPoint(3, 3.0 / 5.0, 1.0 - (2.0 / 5.0));
			this.AlphaSpline.SetPoint(4, 4.0 / 5.0, 1.0 - (1.0 / 5.0));
			this.AlphaSpline.SetPoint(5, 1.0, 1.0);
			this.AlphaSpline.UpdateSpline();
			this.ctrls.Add(this.AlphaSpline);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_alpha</c>: the alpha across the parallelogram's XY gradient.</summary>
		public SplineCtrl AlphaSpline { get; }

		/// <summary>C++ <c>m_x</c>: three corners of the parallelogram the alpha gradient is laid on.</summary>
		public double[] CornerX { get; } = { 257, 369, 143 };

		/// <summary>C++ <c>m_y</c>.</summary>
		public double[] CornerY { get; } = { 60, 170, 310 };

		public override string Name => "alpha_gradient";

		public override string Category => "Gradients";

		public override string Description => "A color gradient whose transparency comes from a second gradient on a parallelogram. Drag a corner or the whole parallelogram, and shape the transparency with the spline.";

		public override int Width => 400;

		public override int Height => 320;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Rgba8.FromRgba(1, 1, 1));

			// srand(1234), then each value in argument order (see the class remarks).
			var random = new MsvcRand(1234);
			for (int i = 0; i < 100; i++)
			{
				double x = random.Next() % this.Width;
				double y = random.Next() % this.Height;
				double rx = (random.Next() % 60) + 5;
				double ry = (random.Next() % 60) + 5;
				var ellipse = new Ellipse(x, y, rx, ry, 50);
				double r = random.Next() / 32767.0;
				double g = random.Next() / 32767.0;
				double b = random.Next() / 32767.0;
				double a = random.Next() / 32767.0 / 2.0;
				graphics.Render(ellipse, Rgba8.FromRgba(r, g, b, a));
			}

			double cx = this.Width / 2;
			double cy = this.Height / 2;
			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null)
			{
				if (graphics.DestImage is IImageByte destination)
				{
					// The software reference: the circle's spans from span_gradient, their alpha replaced by
					// span_gradient_alpha, as C++ does.
					this.RenderGradientCircle(destination, rasterizer, transform, cx, cy);
					destination.MarkImageChanged();
				}
			}
			else if (graphics is IGradientFillGraphics gradientGraphics)
			{
				// A GPU surface: the same span_gradient and span_gradient_alpha evaluated per pixel.
				Affine screenToGradient = GradientMatrix(cx, cy) * transform;
				screenToGradient.invert();
				var screenToDemo = new Affine(transform);
				screenToDemo.invert();
				var fill = new GradientFill
				{
					Shape = GradientShape.Radial,
					D1 = 0,
					D2 = 150,
					ScreenToGradient = screenToGradient,
					Colors = new ColorArray(NewColors()),
				};
				var alphaFill = new GradientFill
				{
					Shape = GradientShape.XY,
					D1 = 0,
					D2 = 100,
					ScreenToGradient = screenToDemo * this.AlphaMatrix(),
					Colors = new AlphaArray(this.NewAlphas()),
				};
				gradientGraphics.FillPathWithGradient(new Ellipse(cx, cy, 150, 150, 100), fill, alphaFill);
			}

			Color pointColor = Rgba8.FromRgba(0, 0.4, 0.4, 0.31);
			for (int i = 0; i < 3; i++)
			{
				graphics.Render(new Ellipse(this.CornerX[i], this.CornerY[i], 5, 5, 20), pointColor);
			}

			// C++ strokes the closed parallelogram with a bare vcgen_stroke: width 1, miter joins.
			var outline = new VertexStorage();
			outline.MoveTo(this.CornerX[0], this.CornerY[0]);
			outline.LineTo(this.CornerX[1], this.CornerY[1]);
			outline.LineTo(this.CornerX[2], this.CornerY[2]);
			outline.LineTo(this.CornerX[0] + this.CornerX[2] - this.CornerX[1], this.CornerY[0] + this.CornerY[2] - this.CornerY[1]);
			outline.ClosePolygon();
			graphics.Render(new Stroke(outline, 1.0), Rgba8.FromRgba(0, 0, 0));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || button != AggInputFlags.MouseLeft)
			{
				return;
			}

			int i;
			for (i = 0; i < 3; i++)
			{
				if (Math.Sqrt(((x - this.CornerX[i]) * (x - this.CornerX[i])) + ((y - this.CornerY[i]) * (y - this.CornerY[i]))) < 10.0)
				{
					this.dragDx = x - this.CornerX[i];
					this.dragDy = y - this.CornerY[i];
					this.dragIndex = i;
					break;
				}
			}

			if (i == 3 && agg_math.point_in_triangle(this.CornerX[0], this.CornerY[0], this.CornerX[1], this.CornerY[1], this.CornerX[2], this.CornerY[2], x, y))
			{
				this.dragDx = x - this.CornerX[0];
				this.dragDy = y - this.CornerY[0];
				this.dragIndex = 3;
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.dragIndex = -1;
				return;
			}

			if (this.dragIndex == 3)
			{
				// The whole parallelogram follows corner 0.
				double dx = x - this.dragDx;
				double dy = y - this.dragDy;
				this.CornerX[1] -= this.CornerX[0] - dx;
				this.CornerY[1] -= this.CornerY[0] - dy;
				this.CornerX[2] -= this.CornerX[0] - dx;
				this.CornerY[2] -= this.CornerY[0] - dy;
				this.CornerX[0] = dx;
				this.CornerY[0] = dy;
				this.Invalidate();
			}
			else if (this.dragIndex >= 0)
			{
				this.CornerX[this.dragIndex] = x - this.dragDx;
				this.CornerY[this.dragIndex] = y - this.dragDy;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.dragIndex = -1;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			// As C++, the arrow keys nudge corners 0 and 1 only (y up).
			double dx = key == Keys.Left ? -0.1 : key == Keys.Right ? 0.1 : 0;
			double dy = key == Keys.Up ? 0.1 : key == Keys.Down ? -0.1 : 0;
			this.CornerX[0] += dx;
			this.CornerY[0] += dy;
			this.CornerX[1] += dx;
			this.CornerY[1] += dy;
			this.Invalidate();
		}

		/// <summary>
		/// C++ on_draw's gradient pass: the 150-radius circle at (<paramref name="cx"/>, <paramref name="cy"/>),
		/// colored by a circular gradient (d 0 to 150) scaled 0.75 x 1.2 and turned -60 degrees, its alpha from
		/// gradient_xy (d 0 to 100) over the parallelogram mapped to (-100, -100)-(100, 100).
		/// <paramref name="frameTransform"/> takes demo coordinates to <paramref name="destination"/>'s pixels.
		/// </summary>
		private void RenderGradientCircle(IImageByte destination, ScanlineRasterizer rasterizer, Affine frameTransform, double cx, double cy)
		{
			Affine gradientMatrix = GradientMatrix(cx, cy) * frameTransform;
			gradientMatrix.invert();

			Affine alphaMatrix = this.AlphaMatrix();
			if (!frameTransform.is_identity())
			{
				var frameToDemo = new Affine(frameTransform);
				frameToDemo.invert();
				alphaMatrix = frameToDemo * alphaMatrix;
			}

			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(new Ellipse(cx, cy, 150, 150, 100), frameTransform));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), this.NewSpanGenerator(new span_interpolator_linear(gradientMatrix), new span_interpolator_linear(alphaMatrix)));
		}

		/// <summary>C++ <c>gradient_mtx</c> before its inversion: gradient space to demo space.</summary>
		private static Affine GradientMatrix(double cx, double cy)
		{
			return Affine.NewScaling(0.75, 1.2) * Affine.NewRotation(-Math.PI / 3.0) * Affine.NewTranslation(cx, cy);
		}

		/// <summary>C++ <c>alpha_mtx.parl_to_rect</c>: demo space to the alpha gradient's (-100, -100)-(100, 100).</summary>
		private Affine AlphaMatrix()
		{
			double[] parallelogram = { this.CornerX[0], this.CornerY[0], this.CornerX[1], this.CornerY[1], this.CornerX[2], this.CornerY[2] };
			return ImagePerspectiveDemo.ParallelogramToRectangle(parallelogram, -100, -100, 100, 100);
		}

		/// <summary>C++ <c>span_conv</c>: span_gradient's colors, then span_gradient_alpha's alpha over them.</summary>
		private ISpanGenerator NewSpanGenerator(span_interpolator_linear gradientInterpolator, span_interpolator_linear alphaInterpolator)
		{
			var spanGradient = new span_gradient(gradientInterpolator, new gradient_circle(), new ColorArray(NewColors()), 0, 150);
			var spanAlpha = new span_gradient_alpha(alphaInterpolator, new gradient_xy(), this.NewAlphas(), 0, 100);
			return new span_converter(spanGradient, spanAlpha);
		}

		/// <summary>C++ fill_color_array: begin to middle over the first 128 entries, middle to end over the rest.</summary>
		private static Color[] NewColors()
		{
			Color begin = Rgba8.FromRgba(0, 0.19, 0.19);
			Color middle = Rgba8.FromRgba(0.7, 0.7, 0.19);
			Color end = Rgba8.FromRgba(0.31, 0, 0);
			var colors = new Color[256];
			for (int i = 0; i < 128; i++)
			{
				colors[i] = Rgba8.Gradient(begin, middle, i / 128.0);
			}

			for (int i = 128; i < 256; i++)
			{
				colors[i] = Rgba8.Gradient(middle, end, (i - 128) / 128.0);
			}

			return colors;
		}

		/// <summary>C++ <c>rgba8::from_double(m_alpha.value(i / 255.0))</c> for each of the 256 alpha steps.</summary>
		private byte[] NewAlphas()
		{
			var alphas = new byte[256];
			for (int i = 0; i < 256; i++)
			{
				alphas[i] = (byte)Util.uround(this.AlphaSpline.Value(i / 255.0) * 255.0);
			}

			return alphas;
		}

		/// <summary>The alpha bytes as a colour function, for <see cref="GradientFill"/>, which reads only their alpha.</summary>
		private sealed class AlphaArray : IColorFunction
		{
			private readonly byte[] alphas;

			public AlphaArray(byte[] alphas)
			{
				this.alphas = alphas;
			}

			public Color this[int v] => new Color(0, 0, 0, this.alphas[v]);

			public int size() => this.alphas.Length;
		}

		/// <summary>C++ <c>pod_auto_array&lt;color_type, 256&gt;</c> as a color function.</summary>
		private sealed class ColorArray : IColorFunction
		{
			private readonly Color[] colors;

			public ColorArray(Color[] colors)
			{
				this.colors = colors;
			}

			public Color this[int v] => this.colors[v];

			public int size() => this.colors.Length;
		}
	}
}
