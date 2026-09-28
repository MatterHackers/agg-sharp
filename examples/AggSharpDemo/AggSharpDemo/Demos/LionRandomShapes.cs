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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// The scene C++ AGG's alpha_mask2.cpp and multi_clip.cpp share: the lion and random lines, markers and
	/// gradient discs, drawn through whichever clipping renderer the example sets up (an alpha mask, or many
	/// clip boxes).
	/// </summary>
	internal static class LionRandomShapes
	{
		/// <summary>
		/// The examples' on_draw after the clear and the mask or clip setup, drawn into <paramref name="target"/>:
		/// the lion, then 50 random Bresenham lines and markers, 50 round-capped anti-aliased lines and 50
		/// radial-gradient discs, all random from <paramref name="random"/>.
		/// </summary>
		public static void DrawThroughRenderer(ImageClippingProxy target, LionShape lion, MsvcRand random, Affine transform, int width, int height)
		{

			// C++ fills the lion through rasterizer_scanline_aa (no clip box) and scanline_u8.
			var lionGraphics = new ImageGraphics2D(target, new ScanlineRasterizer(), new scanline_unpacked_8());
			lionGraphics.Rasterizer.reset_clipping();
			lion.Render(lionGraphics, transform, 255);

			DrawLinesAndMarkers(new RendererMarkers(target), random, width, height);

			var outlineRenderer = new OutlineRenderer(target, new LineProfileAnitAlias(5.0, new gamma_none()));
			var outlineRasterizer = new rasterizer_outline_aa(outlineRenderer);
			outlineRasterizer.round_cap(true);
			for (int i = 0; i < 50; i++)
			{
				outlineRenderer.color(RandomColor(random));
				outlineRasterizer.move_to_d(random.Next() % width, random.Next() % height);
				outlineRasterizer.line_to_d(random.Next() % width, random.Next() % height);
				outlineRasterizer.render(false);
			}

			var rasterizer = new ScanlineRasterizer();
			var scanlineRenderer = new ScanlineRenderer();
			var gradient = new gradient_circle();
			for (int i = 0; i < 50; i++)
			{
				int x = random.Next() % width;
				int y = random.Next() % height;
				double r = (random.Next() % 10) + 5;
				Affine gradientMatrix = Affine.NewScaling(r / 10.0) * Affine.NewTranslation(x, y);
				gradientMatrix.invert();
				int red = random.Next() & 0x7F;
				int green = random.Next() & 0x7F;
				int blue = random.Next() & 0x7F;
				var colors = new LinearColors(new Color(255, 255, 255, 0), SrgbLut.FromSrgba8(red, green, blue));
				var spanGenerator = new span_gradient(new span_interpolator_linear(gradientMatrix), gradient, colors, 0, 10);

				rasterizer.add_path(new Ellipse(x, y, r, r, 32));
				scanlineRenderer.GenerateAndRender(rasterizer, new scanline_unpacked_8(), target, new span_allocator(), spanGenerator);
			}
		}

		/// <summary>
		/// The GPU stand-in for <see cref="DrawThroughRenderer"/>, taking the same random values: the lion, the
		/// Bresenham lines and markers as renderer_markers' own pixels (each span a rectangle, through a
		/// <see cref="Graphics2DSpanImage"/>), the anti-aliased lines as round-capped strokes and the gradient
		/// discs through <see cref="IGradientFillGraphics"/> when the surface has it.
		/// </summary>
		public static void DrawOnGraphics(Graphics2D graphics, LionShape lion, MsvcRand random, Affine transform, int width, int height)
		{

			lion.Render(graphics, transform, 255);

			DrawLinesAndMarkers(new RendererMarkers(new ImageClippingProxy(new Graphics2DSpanImage(graphics, width, height))), random, width, height);

			for (int i = 0; i < 50; i++)
			{
				Color color = RandomColor(random);
				var line = new VertexStorage();
				line.MoveTo(random.Next() % width, random.Next() % height);
				line.LineTo(random.Next() % width, random.Next() % height);
				graphics.Render(new Stroke(line, 5) { LineCap = LineCap.Round }, color);
			}

			if (!(graphics is IGradientFillGraphics gradientGraphics))
			{
				return;
			}

			// The discs' gradient runs in screen pixels, so its matrix takes the demo's transform too.
			Affine demoToScreen = graphics.GetTransform();
			for (int i = 0; i < 50; i++)
			{
				int x = random.Next() % width;
				int y = random.Next() % height;
				double r = (random.Next() % 10) + 5;
				Affine screenToGradient = Affine.NewScaling(r / 10.0) * Affine.NewTranslation(x, y) * demoToScreen;
				screenToGradient.invert();
				int red = random.Next() & 0x7F;
				int green = random.Next() & 0x7F;
				int blue = random.Next() & 0x7F;
				var fill = new GradientFill
				{
					Shape = GradientShape.Radial,
					D1 = 0,
					D2 = 10,
					ScreenToGradient = screenToGradient,
					Colors = new LinearColors(new Color(255, 255, 255, 0), SrgbLut.FromSrgba8(red, green, blue)),
				};
				gradientGraphics.FillPathWithGradient(new Ellipse(x, y, r, r, 32), fill);
			}
		}

		/// <summary>The 50 random Bresenham lines, each followed by a random marker, in the examples' order.</summary>
		private static void DrawLinesAndMarkers(RendererMarkers markers, MsvcRand random, int width, int height)
		{
			for (int i = 0; i < 50; i++)
			{
				markers.LineColor = RandomColor(random);
				markers.FillColor = RandomColor(random);

				int x1 = RendererMarkers.Coord(random.Next() % width);
				int y1 = RendererMarkers.Coord(random.Next() % height);
				int x2 = RendererMarkers.Coord(random.Next() % width);
				int y2 = RendererMarkers.Coord(random.Next() % height);
				markers.Line(x1, y1, x2, y2);

				int x = random.Next() % width;
				int y = random.Next() % height;
				int r = (random.Next() % 10) + 5;
				markers.Marker(x, y, r, (MarkerType)(random.Next() % (int)MarkerType.EndOfMarkers));
			}
		}

		/// <summary>
		/// The example's <c>srgba8(rand() &amp; 0x7F, rand() &amp; 0x7F, rand() &amp; 0x7F, (rand() &amp; 0x7F) + 0x7F)</c>,
		/// as the linear rgba8 its renderers convert it to.
		/// </summary>
		private static Color RandomColor(MsvcRand random)
		{
			int r = random.Next() & 0x7F;
			int g = random.Next() & 0x7F;
			int b = random.Next() & 0x7F;
			return SrgbLut.FromSrgba8(r, g, b, (random.Next() & 0x7F) + 0x7F);
		}

		/// <summary>
		/// C++ gradient_linear_color&lt;rgba8&gt;: 256 steps from c1 to c2, each c1.gradient(c2, v / 255.0) - the
		/// step rounded to 0..255, then rgba8::lerp per channel.
		/// </summary>
		/// <remarks>
		/// The examples specialize gradient_linear_color for srgba8 and sgray8 with a truncating formula,
		/// but their color_type is the pixel format's rgba8, so the generic template is the one it runs.
		/// </remarks>
		private class LinearColors : IColorFunction
		{
			private readonly Color c1;

			private readonly Color c2;

			public LinearColors(Color c1, Color c2)
			{
				this.c1 = c1;
				this.c2 = c2;
			}

			public Color this[int v]
			{
				get
				{
					// C++ multiplies by a precomputed 1 / (size - 1) (m_mult), then uround(k * 255).
					int k = (int)((v * (1.0 / 255.0) * 255) + 0.5);
					return new Color(
						Rgba8Math.Lerp(this.c1.red, this.c2.red, k),
						Rgba8Math.Lerp(this.c1.green, this.c2.green, k),
						Rgba8Math.Lerp(this.c1.blue, this.c2.blue, k),
						Rgba8Math.Lerp(this.c1.alpha, this.c2.alpha, k));
				}
			}

			public int size() => 256;
		}
	}
}
