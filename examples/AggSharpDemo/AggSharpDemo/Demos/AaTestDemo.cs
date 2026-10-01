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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's aa_test.cpp: an anti-aliasing test sheet - radial lines (dashed on one side), dots of whole and
	/// fractional sizes and positions, gradient-coloured lines of whole and fractional widths, lengths and
	/// positions, and a stack of thin gradient-coloured triangles.
	/// </summary>
	/// <remarks>
	/// aa_test.cpp runs with flip_y = false (y down, row 0 at the top), so the port <see cref="DrawsYDown"/>.
	/// It has no ctrls. C++ runs a timed "stress test" of random dots, lines and triangles on a mouse press; that
	/// is a benchmark, not part of the demo, and is left out. The C++ window is resizable, and the radial lines
	/// and the triangles follow its size, so the port takes its size in the constructor.
	/// </remarks>
	public class AaTestDemo : AggDemo
	{
		private readonly int width;

		private readonly int height;

		public AaTestDemo(int width = 480, int height = 350)
		{
			this.width = width;
			this.height = height;
		}

		public override string Name => "aa_test";

		public override string Category => "Rendering";

		public override string Description => "An anti-aliasing test sheet: radial lines, dots and gradient-coloured lines at whole and fractional sizes, widths and positions, and thin gradient triangles.";

		public override int Width => this.width;

		public override int Height => this.height;

		public override bool DrawsYDown => true;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(new Color(0, 0, 0));

			// Radial lines: solid on one half, dashed (dash and gap i) on the other, white at alpha 0.2.
			double cx = this.Width / 2.0;
			double cy = this.Height / 2.0;
			double radius = Math.Min(cx, cy);
			Color faintWhite = Rgba8.FromRgba(1.0, 1.0, 1.0, 0.2);
			for (int i = 180; i > 0; i--)
			{
				double n = 2.0 * Math.PI * i / 180.0;
				graphics.Render(DashedLine(cx + (radius * Math.Sin(n)), cy + (radius * Math.Cos(n)), cx, cy, 1.0, i < 90 ? i : 0.0), faintWhite);
			}

			var white = new Color(255, 255, 255);
			for (int i = 1; i <= 20; i++)
			{
				// Integral point sizes 1..20.
				graphics.Render(new Ellipse(20 + (i * (i + 1)) + 0.5, 20.5, i / 2.0, i / 2.0, 8 + i), white);

				// Fractional point sizes 0..2.
				graphics.Render(new Ellipse(18 + (i * 4) + 0.5, 33 + 0.5, i / 20.0, i / 20.0, 8), white);

				// Fractional point positioning.
				graphics.Render(new Ellipse(18 + (i * 4) + ((i - 1) / 10.0) + 0.5, 27 + ((i - 1) / 10.0) + 0.5, 0.5, 0.5, 8), white);

				// Integral line widths 1..20.
				Color[] colors = ColorArray(1, 1, 1, i % 2, (i % 3) * 0.5, (i % 5) * 0.25);
				double x1 = 20 + (i * (i + 1));
				double y1 = 40.5;
				double x2 = 20 + (i * (i + 1)) + ((i - 1) * 4);
				double y2 = 100.5;
				DrawGradient(graphics, DashedLine(x1, y1, x2, y2, i, 0), colors, x1, y1, x2, y2);

				// Fractional line lengths, horizontal and vertical (red to blue).
				colors = ColorArray(1, 0, 0, 0, 0, 1);
				x1 = 17.5 + (i * 4);
				y1 = 107;
				x2 = 17.5 + (i * 4) + (i / 6.66666667);
				y2 = 107;
				DrawGradient(graphics, DashedLine(x1, y1, x2, y2, 1.0, 0), colors, x1, y1, x2, y2);

				x1 = 18 + (i * 4);
				y1 = 112.5;
				x2 = 18 + (i * 4);
				y2 = 112.5 + (i / 6.66666667);
				DrawGradient(graphics, DashedLine(x1, y1, x2, y2, 1.0, 0), colors, x1, y1, x2, y2);

				// Fractional line positioning (red).
				colors = ColorArray(1, 0, 0, 1, 1, 1);
				x1 = 21.5;
				y1 = 120 + ((i - 1) * 3.1);
				x2 = 52.5;
				y2 = 120 + ((i - 1) * 3.1);
				DrawGradient(graphics, DashedLine(x1, y1, x2, y2, 1.0, 0), colors, x1, y1, x2, y2);

				// Fractional line width 2..0 (green).
				colors = ColorArray(0, 1, 0, 1, 1, 1);
				x1 = 52.5;
				y1 = 118 + (i * 3);
				x2 = 83.5;
				y2 = 118 + (i * 3);
				DrawGradient(graphics, DashedLine(x1, y1, x2, y2, 2.0 - ((i - 1) / 10.0), 0), colors, x1, y1, x2, y2);

				// Stippled fractional width 2..0 (blue).
				colors = ColorArray(0, 0, 1, 1, 1, 1);
				x1 = 83.5;
				y1 = 119 + (i * 3);
				x2 = 114.5;
				y2 = 119 + (i * 3);
				DrawGradient(graphics, DashedLine(x1, y1, x2, y2, 2.0 - ((i - 1) / 10.0), 3.0), colors, x1, y1, x2, y2);

				if (i <= 10)
				{
					// Integral line width, horizontally aligned (mipmap test).
					graphics.Render(DashedLine(125.5, 119.5 + ((i + 2) * (i / 2.0)), 135.5, 119.5 + ((i + 2) * (i / 2.0)), i, 0.0), white);
				}

				// Fractional line width 0..2, 1 px long.
				graphics.Render(DashedLine(17.5 + (i * 4), 192, 18.5 + (i * 4), 192, i / 10.0, 0), white);

				// Fractional line positioning, 1 px long.
				graphics.Render(DashedLine(17.5 + (i * 4) + ((i - 1) / 10.0), 186, 18.5 + (i * 4) + ((i - 1) / 10.0), 186, 1.0, 0), white);
			}

			// Triangles, each shaded along its upper edge.
			for (int i = 1; i <= 13; i++)
			{
				Color[] colors = ColorArray(1, 1, 1, i % 2, (i % 3) * 0.5, (i % 5) * 0.25);
				double x1 = this.Width - 150;
				double y1 = this.Height - 20 - (i * (i + 1.5));
				double x2 = this.Width - 20;
				double y2 = this.Height - 20 - (i * (i + 1));
				var triangle = new VertexStorage();
				triangle.MoveTo(x1, y1);
				triangle.LineTo(x2, y2);
				triangle.LineTo(this.Width - 20, this.Height - 20 - (i * (i + 2)));
				DrawGradient(graphics, triangle, colors, x1, y1, x2, y2);
			}
		}

		/// <summary>
		/// C++ <c>dashed_line::draw</c>'s path: the segment moved by half a pixel, dashed (dash and gap
		/// <paramref name="dashLength"/>) when that is positive, stroked <paramref name="lineWidth"/> wide with
		/// round caps.
		/// </summary>
		private static IVertexSource DashedLine(double x1, double y1, double x2, double y2, double lineWidth, double dashLength)
		{
			var segment = new VertexStorage();
			segment.MoveTo(x1 + 0.5, y1 + 0.5);
			segment.LineTo(x2 + 0.5, y2 + 0.5);

			IVertexSource source = segment;
			if (dashLength > 0.0)
			{
				var dash = new Dash(segment);
				dash.AddDash(dashLength, dashLength);
				source = dash;
			}

			return new Stroke(source, lineWidth) { LineCap = LineCap.Round };
		}

		/// <summary>
		/// C++ <c>fill_color_array</c> into its <c>srgba8</c> array, as the linear rgba8 span_gradient reads it
		/// back: the two ends go to sRGB through the float table, the 256 steps are interpolated in sRGB, and
		/// each step comes back to linear through the 8-bit table.
		/// </summary>
		private static Color[] ColorArray(double r1, double g1, double b1, double r2, double g2, double b2)
		{
			Color begin = SrgbLut.Srgba8FromRgba(r1, g1, b1);
			Color end = SrgbLut.Srgba8FromRgba(r2, g2, b2);
			var colors = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				Color srgb = Rgba8.Gradient(begin, end, i / 255.0);
				colors[i] = SrgbLut.FromSrgba8(srgb.red, srgb.green, srgb.blue, srgb.alpha);
			}

			return colors;
		}

		/// <summary>
		/// C++ <c>calc_linear_gradient_transform</c> before its inversion: gradient space (x 0 to 100 along the
		/// line) to demo space, with the line's start moved by half a pixel as <see cref="DashedLine"/> moves it.
		/// </summary>
		private static Affine LinearGradientMatrix(double x1, double y1, double x2, double y2)
		{
			double dx = x2 - x1;
			double dy = y2 - y1;
			return Affine.NewIdentity()
				* Affine.NewScaling(Math.Sqrt((dx * dx) + (dy * dy)) / 100.0)
				* Affine.NewRotation(Math.Atan2(dy, dx))
				* Affine.NewTranslation(x1 + 0.5, y1 + 0.5);
		}

		/// <summary>
		/// <paramref name="shape"/> filled by C++'s <c>ren_gradient</c>: a linear gradient_x span_gradient (d 0
		/// to 100) through <paramref name="colors"/>, running from (<paramref name="x1"/>, <paramref name="y1"/>)
		/// to (<paramref name="x2"/>, <paramref name="y2"/>).
		/// </summary>
		private static void DrawGradient(Graphics2D graphics, IVertexSource shape, Color[] colors, double x1, double y1, double x2, double y2)
		{
			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null)
			{
				if (graphics.DestImage is IImageByte destination)
				{
					// The software reference: the spans come straight from span_gradient, as C++'s do.
					Affine gradientMatrix = LinearGradientMatrix(x1, y1, x2, y2) * transform;
					gradientMatrix.invert();
					var spanGenerator = new span_gradient(new span_interpolator_linear(gradientMatrix), new gradient_x(), new ColorArrayFunction(colors), 0, 100);

					rasterizer.reset();
					rasterizer.add_path(new VertexSourceApplyTransform(shape, transform));
					new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
					destination.MarkImageChanged();
				}
			}
			else if (graphics is IGradientFillGraphics gradientGraphics)
			{
				// A GPU surface: the same span_gradient evaluated per pixel under the shape.
				Affine screenToGradient = LinearGradientMatrix(x1, y1, x2, y2) * transform;
				screenToGradient.invert();
				var fill = new GradientFill
				{
					Shape = GradientShape.X,
					D1 = 0,
					D2 = 100,
					ScreenToGradient = screenToGradient,
					Colors = new ColorArrayFunction(colors),
				};
				gradientGraphics.FillPathWithGradient(shape, fill);
			}
		}

		/// <summary>C++ <c>pod_auto_array&lt;srgba8, 256&gt;</c>, already converted to linear.</summary>
		private sealed class ColorArrayFunction : IColorFunction
		{
			private readonly Color[] colors;

			public ColorArrayFunction(Color[] colors)
			{
				this.colors = colors;
			}

			public Color this[int v] => this.colors[v];

			public int size() => 256;
		}
	}
}
