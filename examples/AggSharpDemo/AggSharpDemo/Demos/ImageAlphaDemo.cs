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
	/// C++ AGG's image_alpha.cpp: the spheres image, turned 10 degrees in an ellipse over 50 random ellipses, with
	/// each pixel's alpha looked up from its brightness through the spline you edit (0 dark to 1 bright).
	/// </summary>
	/// <remarks>
	/// The ellipses come from <see cref="MsvcRand"/> seeded 1, as C's unseeded rand() is and as in the C++
	/// reference render (demo_image_alpha.cpp), each value taken in argument order.
	/// <para>
	/// The example's brightness index is one past the table's end for a white pixel; it is clamped to the last
	/// entry here and in the reference render.
	/// </para>
	/// </remarks>
	public class ImageAlphaDemo : AggDemo
	{
		// span_conv_brightness_alpha::array_size: one entry per value of r + g + b.
		private const int BrightnessLevels = 256 * 3;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// The example's image is BGR24, and the span generator is the rgb one, which reads 3-byte pixels.
		private readonly ImageBuffer sourceImage24;

		private readonly double[] ellipseX = new double[50];

		private readonly double[] ellipseY = new double[50];

		private readonly double[] ellipseRx = new double[50];

		private readonly double[] ellipseRy = new double[50];

		private readonly Color[] ellipseColors = new Color[50];

		// The spheres at 32 bit, for the GPU's filtered image fill.
		private readonly ImageBuffer sourceImage32;

		public ImageAlphaDemo()
		{
			ImageBuffer spheres = SpheresImage.Load();
			this.sourceImage32 = spheres;
			this.sourceImage24 = new ImageBuffer(spheres.Width, spheres.Height, 24, new BlenderBGR());
			for (int y = 0; y < spheres.Height; y++)
			{
				for (int x = 0; x < spheres.Width; x++)
				{
					this.sourceImage24.SetPixel(x, y, spheres.GetPixel(x, y));
				}
			}

			// image_alpha.cpp runs with flip_y = true and gives its ctrl !flip_y.
			this.AlphaSpline = new SplineCtrl(2, 2, 200, 30, 6, false);
			this.AlphaSpline.SetValue(0, 1.0);
			this.AlphaSpline.SetValue(1, 1.0);
			this.AlphaSpline.SetValue(2, 1.0);
			this.AlphaSpline.SetValue(3, 0.5);
			this.AlphaSpline.SetValue(4, 0.5);
			this.AlphaSpline.SetValue(5, 1.0);
			this.AlphaSpline.UpdateSpline();
			this.ctrls.Add(this.AlphaSpline);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// on_init, at the initial window size.
			var random = new MsvcRand(1);
			for (int i = 0; i < 50; i++)
			{
				this.ellipseX[i] = random.Next() % this.Width;
				this.ellipseY[i] = random.Next() % this.Height;
				this.ellipseRx[i] = (random.Next() % 60) + 10;
				this.ellipseRy[i] = (random.Next() % 60) + 10;
				int r = random.Next() & 0xFF;
				int g = random.Next() & 0xFF;
				int b = random.Next() & 0xFF;
				int a = random.Next() & 0xFF;
				this.ellipseColors[i] = SrgbLut.FromSrgba8(r, g, b, a);
			}
		}

		/// <summary>C++ <c>m_alpha</c>: alpha (0 to 1) as a function of brightness (0 to 1).</summary>
		public SplineCtrl AlphaSpline { get; }

		public override string Name => "image_alpha";

		public override string Category => "Images";

		public override string Description => "An image whose pixels' alpha comes from their brightness through a curve you edit, over random ellipses.";

		// agg_main opens the window at the image's size.
		public override int Width => 320;

		public override int Height => 300;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			for (int i = 0; i < 50; i++)
			{
				graphics.Render(new Ellipse(this.ellipseX[i], this.ellipseY[i], this.ellipseRx[i], this.ellipseRy[i], 50), this.ellipseColors[i]);
			}

			Affine imageToDemo = Affine.NewTranslation(-this.Width / 2.0, -this.Height / 2.0);
			imageToDemo *= Affine.NewRotation(10.0 * Math.PI / 180.0);
			imageToDemo *= Affine.NewTranslation(this.Width / 2.0, this.Height / 2.0);

			byte[] brightnessToAlpha = this.BrightnessToAlpha();

			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.RenderImageSpans(rasterizer, destination, transform, imageToDemo, brightnessToAlpha);
			}
			else
			{
				// The GPU runs the same bilinear filter through the transparent clip per pixel, and the same table.
				var ellipse = new VertexSourceApplyTransform(this.ImageEllipse(), imageToDemo);
				((IImageFilterGraphics)graphics).FillPathWithFilteredImage(ellipse, this.sourceImage32, new ImageFilterFill(imageToDemo * transform)
				{
					Kind = ImageFilterKind.Bilinear,
					Edge = ImageFilterEdge.Clip,
					BrightnessToAlpha = brightnessToAlpha,
				});
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		// C++ looks the brightness up as x * array_size / (3 * 255), which is one past the end for white.
		private static int BrightnessIndex(int r, int g, int b)
		{
			return Math.Min((r + g + b) * BrightnessLevels / (3 * 255), BrightnessLevels - 1);
		}

		// brightness_alpha_array: the spline sampled at each brightness level, truncated to a byte.
		private byte[] BrightnessToAlpha()
		{
			var table = new byte[BrightnessLevels];
			for (int i = 0; i < BrightnessLevels; i++)
			{
				table[i] = (byte)(this.AlphaSpline.Value(i / (double)BrightnessLevels) * 255.0);
			}

			return table;
		}

		// The ellipse the image shows in, in image space: the example turns it with the image.
		private Ellipse ImageEllipse()
		{
			return new Ellipse(this.Width / 2.0, this.Height / 2.0, this.Width / 1.9, this.Height / 1.9, 200);
		}

		// The software reference: the ellipse's spans from the bilinear filter (through a clip accessor with a
		// transparent black background), their alpha replaced by span_conv_brightness_alpha, blended straight as
		// C++'s rb does. A span generator has no Graphics2D call, so the interpolator maps a frame pixel back
		// through the graphics transform into demo space itself.
		private void RenderImageSpans(ScanlineRasterizer rasterizer, IImageByte destination, Affine transform, Affine imageToDemo, byte[] brightnessToAlpha)
		{
			var demoToImage = new Affine(imageToDemo);
			demoToImage.invert();
			Affine frameToImage = demoToImage;
			if (!transform.is_identity())
			{
				frameToImage = new Affine(transform);
				frameToImage.invert();
				frameToImage *= demoToImage;
			}

			var interpolator = new span_interpolator_linear(frameToImage);
			var accessor = new ImageBufferAccessorClip(this.sourceImage24, new Color(0, 0, 0, 0));
			var spanGenerator = new span_converter(new span_image_filter_rgb_bilinear(accessor, interpolator), new BrightnessToAlphaConverter(brightnessToAlpha));

			var ellipse = new VertexSourceApplyTransform(this.ImageEllipse(), imageToDemo);
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(ellipse, transform));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
			destination.MarkImageChanged();
		}

		// The example's span_conv_brightness_alpha: each pixel's alpha from its brightness. C++ treats the table's
		// byte as a cover, and mult_cover(255, cover) is the cover itself.
		private sealed class BrightnessToAlphaConverter : ISpanGenerator
		{
			private readonly byte[] brightnessToAlpha;

			public BrightnessToAlphaConverter(byte[] brightnessToAlpha)
			{
				this.brightnessToAlpha = brightnessToAlpha;
			}

			public void prepare()
			{
			}

			public void generate(Color[] span, int spanIndex, int x, int y, int len)
			{
				for (int i = spanIndex; i < spanIndex + len; i++)
				{
					span[i].alpha = (byte)Rgba8Math.Multiply(255, this.brightnessToAlpha[BrightnessIndex(span[i].red, span[i].green, span[i].blue)]);
				}
			}
		}
	}
}
