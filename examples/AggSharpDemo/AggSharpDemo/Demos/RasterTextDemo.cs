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
using MatterHackers.Agg.Transform;
using MatterHackers.RenderGl;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's raster_text.cpp: a line in each of AGG's 34 embedded bitmap fonts, black, from the bottom up, then a
	/// line whose colors come from a span generator - a radial gradient repeated through a sine, red to dark green.
	/// </summary>
	/// <remarks>
	/// C++ is flip_y = true, so render_text gets flip = false and y is the glyphs' base line, counted up.
	/// The window resizes in C++ and nothing is laid out to it, so the size is a constructor argument (tests render
	/// a smaller window, where the top lines clip). The GPU draws the same glyphs from per-font atlas textures
	/// (<see cref="RasterFontAtlas"/>); see there for how it differs.
	/// </remarks>
	public class RasterTextDemo : AggDemo
	{
		private const string GradientText = "RADIAL REPEATING GRADIENT: A quick brown fox jumps over the lazy dog";

		// raster_text.cpp's font list. Its order is not the fonts' source order (gse6x9 comes before gse6x12 here).
		private static readonly string[] FontNames =
		{
			"gse4x6", "gse4x8", "gse5x7", "gse5x9", "gse6x9", "gse6x12", "gse7x11", "gse7x11_bold", "gse7x15", "gse7x15_bold",
			"gse8x16", "gse8x16_bold", "mcs11_prop", "mcs11_prop_condensed", "mcs12_prop", "mcs13_prop", "mcs5x10_mono",
			"mcs5x11_mono", "mcs6x10_mono", "mcs6x11_mono", "mcs7x12_mono_high", "mcs7x12_mono_low", "verdana12",
			"verdana12_bold", "verdana13", "verdana13_bold", "verdana14", "verdana14_bold", "verdana16", "verdana16_bold",
			"verdana17", "verdana17_bold", "verdana18", "verdana18_bold",
		};

		private readonly int width;
		private readonly int height;

		// The gradient line for the GPU, rendered by the software path once (it depends on nothing), then a texture.
		private ImageBuffer gradientLine;
		private int gradientLineLeft;
		private int gradientLineBottom;

		public RasterTextDemo(int width = 640, int height = 480)
		{
			this.width = width;
			this.height = height;
		}

		public override string Name => "raster_text";

		public override string Category => "Text";

		public override string Description => "AGG's 34 embedded bitmap fonts, drawn a glyph row at a time, and a line colored by a repeating radial gradient.";

		public override int Width => this.width;

		public override int Height => this.height;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				DrawFonts(destination);
				destination.MarkImageChanged();
			}
			else if (graphics is Graphics2DGpu gpu)
			{
				this.DrawFontsGpu(gpu);
			}
		}

		/// <summary>"A quick brown fox ...: name", the line C++ draws in each font.</summary>
		private static string SampleText(string fontName)
		{
			return "A quick brown fox jumps over the lazy dog 0123456789: " + fontName;
		}

		// The software reference, as C++ on_draw.
		private static void DrawFonts(IImageByte destination)
		{
			var glyph = new glyph_raster_bin(null);
			var text = new renderer_raster_htext_solid(destination, glyph) { color = Color.Black };
			double y = 5;
			foreach (string name in FontNames)
			{
				glyph.font(EmbeddedRasterFonts.Get(name));
				text.render_text(5, y, SampleText(name));
				y += glyph.height() + 1;
			}

			// C++ leaves the last font (verdana18_bold) in place for the gradient line.
			var spanGenerator = new span_gradient(
				new span_interpolator_linear(Affine.NewIdentity()),
				new gradient_sine_repeat_adaptor(new gradient_circle(), 5),
				new gradient_linear_color(Rgba8.FromRgba(1, 0, 0), Rgba8.FromRgba(0, 0.5, 0)),
				0,
				150);
			new renderer_raster_htext(destination, new span_allocator(), spanGenerator, glyph).render_text(5, 465, GradientText);
		}

		// Each solid line is a row of textured quads from its font's atlas. The gradient line's colors vary within a
		// glyph, which a tinted atlas cannot do, so it is the software path's pixels, rendered into a strip once.
		private void DrawFontsGpu(Graphics2DGpu gpu)
		{
			double y = 5;
			foreach (string name in FontNames)
			{
				RasterFontAtlas atlas = RasterFontAtlas.Get(name);
				atlas.Draw(gpu, 5, y, SampleText(name), Color.Black);
				y += atlas.Height + 1;
			}

			if (this.gradientLine == null)
			{
				RasterFontAtlas last = RasterFontAtlas.Get(FontNames[FontNames.Length - 1]);
				var glyph = new glyph_raster_bin(EmbeddedRasterFonts.Get(last.Name));
				glyph.prepare(out glyph_raster_bin.glyph_rect r, 5, 465, 'R', false);
				this.gradientLineLeft = r.x1;
				this.gradientLineBottom = r.y1;
				this.gradientLine = new ImageBuffer((int)glyph.width(GradientText), last.Height);

				// The strip's pixel (0, 0) is the demo's (left, bottom): the interpolator moves it back, so the gradient
				// is the one about the demo's origin.
				var spanGenerator = new span_gradient(
					new span_interpolator_linear(Affine.NewTranslation(this.gradientLineLeft, this.gradientLineBottom)),
					new gradient_sine_repeat_adaptor(new gradient_circle(), 5),
					new gradient_linear_color(Rgba8.FromRgba(1, 0, 0), Rgba8.FromRgba(0, 0.5, 0)),
					0,
					150);
				new renderer_raster_htext(this.gradientLine, new span_allocator(), spanGenerator, glyph)
					.render_text(5 - this.gradientLineLeft, 465 - this.gradientLineBottom, GradientText);
			}

			gpu.Render(this.gradientLine, this.gradientLineLeft, this.gradientLineBottom);
		}

		/// <summary>raster_text.cpp's gradient_sine_repeat_adaptor: the gradient run through a sine, so it repeats
		/// <c>periods</c> times over the span generator's d2 and rises and falls smoothly instead of jumping back.</summary>
		private sealed class gradient_sine_repeat_adaptor : IGradient
		{
			private readonly IGradient gradient;
			private readonly double periods;

			public gradient_sine_repeat_adaptor(IGradient gradient, double periods)
			{
				this.gradient = gradient;
				this.periods = periods * Math.PI * 2.0;
			}

			public int calculate(int x, int y, int d)
			{
				return (int)((1.0 + Math.Sin(this.gradient.calculate(x, y, d) * this.periods / d)) * d / 2);
			}
		}
	}
}
