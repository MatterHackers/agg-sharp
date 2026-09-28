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
	/// C++ AGG's distortions.cpp: the spheres image and a circular gradient, each seen through a disc, warped by a
	/// wave, a swirl, or both. The warp is a span_interpolator_adaptor: every source coordinate the span
	/// generators ask for is moved by the distortion before it is looked up. The phase animates; click or drag
	/// to move the distortion's center.
	/// </summary>
	public class DistortionsDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// The example's image is BGR24, and the span generator is the rgb one, which reads 3-byte pixels.
		private readonly ImageBuffer sourceImage24;

		private readonly Color[] gradientColors = DistortionsGradient.Colors();

		private readonly int imageWidth;

		private readonly int imageHeight;

		private double centerX;

		private double centerY;

		public DistortionsDemo()
		{
			ImageBuffer sourceImage = SpheresImage.Load();
			this.imageWidth = sourceImage.Width;
			this.imageHeight = sourceImage.Height;
			this.sourceImage24 = new ImageBuffer(this.imageWidth, this.imageHeight, 24, new BlenderBGR());
			for (int y = 0; y < this.imageHeight; y++)
			{
				for (int x = 0; x < this.imageWidth; x++)
				{
					this.sourceImage24.SetPixel(x, y, sourceImage.GetPixel(x, y));
				}
			}

			// distortions.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.AngleSlider = new SliderCtrl(5, 5, 150, 12, false) { Label = "Angle={0:F2}" };
			this.ScaleSlider = new SliderCtrl(5, 5 + 15, 150, 12 + 15, false) { Label = "Scale={0:F2}" };
			this.PeriodSlider = new SliderCtrl(5 + 170, 5, 150 + 170, 12, false) { Label = "Period={0:F2}" };
			this.AmplitudeSlider = new SliderCtrl(5 + 170, 5 + 15, 150 + 170, 12 + 15, false) { Label = "Amplitude={0:F2}" };
			this.DistortionRbox = new RboxCtrl(480, 5, 600, 90, false);

			this.AngleSlider.SetRange(-180.0, 180.0);
			this.AngleSlider.Value = 20.0;
			this.ScaleSlider.SetRange(0.1, 5.0);
			this.ScaleSlider.Value = 1.0;
			this.AmplitudeSlider.SetRange(0.1, 40.0);
			this.PeriodSlider.SetRange(0.1, 2.0);
			this.AmplitudeSlider.Value = 10.0;
			this.PeriodSlider.Value = 1.0;
			this.DistortionRbox.AddItem("Wave");
			this.DistortionRbox.AddItem("Swirl");
			this.DistortionRbox.AddItem("Wave-Swirl");
			this.DistortionRbox.AddItem("Swirl-Wave");
			this.DistortionRbox.CurrentItem = 0;

			// C++ add_ctrl order, which is the order presses are offered in.
			this.ctrls.Add(this.AngleSlider);
			this.ctrls.Add(this.ScaleSlider);
			this.ctrls.Add(this.AmplitudeSlider);
			this.ctrls.Add(this.PeriodSlider);
			this.ctrls.Add(this.DistortionRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// C++ on_init.
			this.centerX = (this.imageWidth / 2.0) + 10;
			this.centerY = (this.imageHeight / 2.0) + 10 + 40;

			// agg_main runs with wait_mode(false): the phase animates.
			this.WaitMode = false;
		}

		/// <summary>C++ <c>m_angle</c>, in degrees: turns the image, the gradient and the discs.</summary>
		public SliderCtrl AngleSlider { get; }

		/// <summary>C++ <c>m_scale</c>: scales the image and the gradient inside their discs.</summary>
		public SliderCtrl ScaleSlider { get; }

		/// <summary>C++ <c>m_period</c>: the wave's wavelength.</summary>
		public SliderCtrl PeriodSlider { get; }

		/// <summary>C++ <c>m_amplitude</c>: how strong the wave and the swirl are.</summary>
		public SliderCtrl AmplitudeSlider { get; }

		/// <summary>C++ <c>m_distortion</c>: Wave, Swirl, Wave-Swirl or Swirl-Wave.</summary>
		public RboxCtrl DistortionRbox { get; }

		/// <summary>C++ <c>m_phase</c>, in radians: advanced 15 degrees each frame.</summary>
		public double Phase { get; set; }

		public override string Name => "distortions";

		public override string Category => "Images";

		public override string Description => "An image and a gradient warped by an animated wave or swirl. Click or drag to move the distortion's center; the sliders turn, scale and tune it.";

		// agg_main opens the window at the image's size plus 300 by 60.
		public override int Width => this.imageWidth + 300;

		public override int Height => this.imageHeight + 40 + 20;

		/// <summary>C++ <c>m_center_x</c>, <c>m_center_y</c>: the distortion's center, in demo coordinates over the image.</summary>
		public void SetCenter(double x, double y)
		{
			this.centerX = x;
			this.centerY = y;
			this.Invalidate();
		}

		public override void OnIdle()
		{
			// C++ on_idle.
			this.Phase += 15.0 * Math.PI / 180.0;
			if (this.Phase > Math.PI * 200.0)
			{
				this.Phase -= Math.PI * 200.0;
			}

			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			double imgWidth = this.imageWidth;
			double imgHeight = this.imageHeight;
			double angle = this.AngleSlider.Value * Math.PI / 180.0;
			double scale = this.ScaleSlider.Value;

			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			Affine srcMatrix = Affine.NewTranslation(-imgWidth / 2, -imgHeight / 2);
			srcMatrix *= Affine.NewRotation(angle);
			srcMatrix *= Affine.NewTranslation((imgWidth / 2) + 10, (imgHeight / 2) + 10 + 40);

			Affine demoToImage = Affine.NewTranslation(-imgWidth / 2, -imgHeight / 2);
			demoToImage *= Affine.NewRotation(angle);
			demoToImage *= Affine.NewScaling(scale);
			demoToImage *= Affine.NewTranslation((imgWidth / 2) + 10, (imgHeight / 2) + 10 + 40);
			demoToImage.invert();

			double cx = this.centerX;
			double cy = this.centerY;
			demoToImage.Transform(ref cx, ref cy);
			Distortion imageDistortion = this.NewDistortion(cx, cy);

			Affine gradientShape = Affine.NewTranslation(-imgWidth / 2, -imgHeight / 2);
			gradientShape *= Affine.NewScaling(0.8);
			gradientShape *= Affine.NewRotation(angle);
			gradientShape *= Affine.NewTranslation(imgWidth - (imgWidth / 10) + (imgWidth / 2) + 10, (imgHeight / 2) + 10 + 40);

			Affine demoToGradient = Affine.NewRotation(angle);
			demoToGradient *= Affine.NewScaling(scale);
			demoToGradient *= Affine.NewTranslation(imgWidth - (imgWidth / 10) + (imgWidth / 2) + 10 + 50, (imgHeight / 2) + 10 + 40 + 50);
			demoToGradient.invert();

			cx = this.centerX + imgWidth - (imgWidth / 10);
			cy = this.centerY;
			demoToGradient.Transform(ref cx, ref cy);
			Distortion gradientDistortion = this.NewDistortion(cx, cy);

			double r = Math.Min(imgWidth, imgHeight);
			double discRadius = (r / 2.0) - 20.0;
			var ellipse = new Ellipse(imgWidth / 2.0, imgHeight / 2.0, discRadius, discRadius, 200);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			IImageByte destination = rasterizer != null ? graphics.DestImage : null;
			IImageByte spans = null;
			if (destination != null)
			{
				// The software reference: each disc's spans generated as C++ does. A span generator has no
				// Graphics2D call, so its interpolator maps a frame pixel back through the graphics transform itself.
				var imageInterpolator = new span_interpolator_linear(FrameTo(transform, demoToImage));
				RenderSpans(rasterizer, destination, new VertexSourceApplyTransform(new VertexSourceApplyTransform(ellipse, srcMatrix), transform), this.NewImageSpans(imageInterpolator, imageDistortion));
			}
			else
			{
				// A GPU surface has no span generator, so the same span generators run on the CPU and their exact
				// pixels go through a Graphics2DSpanImage, which the graphics transform places.
				spans = new ImageClippingProxy(new Graphics2DSpanImage(graphics, this.Width, this.Height));
				var imageInterpolator = new span_interpolator_linear(demoToImage);
				RenderSpans(new ScanlineRasterizer(), spans, new VertexSourceApplyTransform(ellipse, srcMatrix), this.NewImageSpans(imageInterpolator, imageDistortion));
			}

			// The black disc the gradient sits in, the image's disc moved right.
			Affine blackDisc = srcMatrix * Affine.NewTranslation(imgWidth - (imgWidth / 10), 0.0);
			graphics.Render(new VertexSourceApplyTransform(ellipse, blackDisc), Color.Black);

			if (destination != null)
			{
				var gradientInterpolator = new span_interpolator_linear(FrameTo(transform, demoToGradient));
				RenderSpans(rasterizer, destination, new VertexSourceApplyTransform(new VertexSourceApplyTransform(ellipse, gradientShape), transform), this.NewGradientSpans(gradientInterpolator, gradientDistortion));
				destination.MarkImageChanged();
			}
			else
			{
				var gradientInterpolator = new span_interpolator_linear(demoToGradient);
				RenderSpans(new ScanlineRasterizer(), spans, new VertexSourceApplyTransform(ellipse, gradientShape), this.NewGradientSpans(gradientInterpolator, gradientDistortion));
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			// C++ moves the center for any button a control does not take.
			if (!this.ctrls.OnMouseDown(x, y, button))
			{
				this.SetCenter(x, y);
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (!this.ctrls.OnMouseMove(x, y, flags) && flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.SetCenter(x, y);
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		// Frame pixel -> demo space (the graphics transform undone) -> the span generator's space.
		private static Affine FrameTo(Affine demoToFrame, Affine demoToSpace)
		{
			if (demoToFrame.is_identity())
			{
				return demoToSpace;
			}

			var frameTo = new Affine(demoToFrame);
			frameTo.invert();
			frameTo *= demoToSpace;
			return frameTo;
		}

		private static void RenderSpans(ScanlineRasterizer rasterizer, IImageByte destination, IVertexSource shape, ISpanGenerator spanGenerator)
		{
			rasterizer.reset();
			rasterizer.add_path(shape);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
		}

		private Distortion NewDistortion(double cx, double cy)
		{
			return new Distortion(this.DistortionRbox.CurrentItem)
			{
				CenterX = cx,
				CenterY = cy,
				Period = this.PeriodSlider.Value,
				Amplitude = 1.0 / this.AmplitudeSlider.Value,
				Phase = this.Phase,
			};
		}

		// C++ span_image_filter_rgb_bilinear_clip over the 24-bit image, white outside it.
		private span_image_filter NewImageSpans(span_interpolator_linear interpolator, Distortion distortion)
		{
			var accessor = new ImageBufferAccessorClip(this.sourceImage24, Color.White);
			return new span_image_filter_rgb_bilinear_clip(accessor, Color.White, new DistortedInterpolator(interpolator, distortion));
		}

		private ISpanGenerator NewGradientSpans(span_interpolator_linear interpolator, Distortion distortion)
		{
			return new span_gradient(new DistortedInterpolator(interpolator, distortion), new gradient_circle(), new ColorArray(this.gradientColors), 0, 180);
		}

		/// <summary>
		/// distortions.cpp's periodic_distortion and its four subclasses: moves a source coordinate, in
		/// image_subpixel units, by a wave (rings out from the center) and/or a swirl (a turn that fades with
		/// distance), in the order the Distortion radio box names them.
		/// </summary>
		private sealed class Distortion
		{
			private readonly int kind;

			public Distortion(int kind)
			{
				this.kind = kind;
			}

			public double CenterX { get; set; }

			public double CenterY { get; set; }

			public double Period { get; set; } = 0.5;

			/// <summary>C++ <c>m_amplitude</c>: already the reciprocal of the slider's value, as C++ <c>amplitude(v)</c> stores it.</summary>
			public double Amplitude { get; set; } = 0.5;

			public double Phase { get; set; }

			public void Calculate(ref int x, ref int y)
			{
				switch (this.kind)
				{
					case 0:
						this.Wave(ref x, ref y);
						break;

					case 1:
						this.Swirl(ref x, ref y);
						break;

					case 2:
						this.Wave(ref x, ref y);
						this.Swirl(ref x, ref y);
						break;

					default:
						this.Swirl(ref x, ref y);
						this.Wave(ref x, ref y);
						break;
				}
			}

			private void Wave(ref int x, ref int y)
			{
				const double subpixelScale = (int)ImageFilterLookUpTable.image_subpixel_scale_e.image_subpixel_scale;
				double xd = (x / subpixelScale) - this.CenterX;
				double yd = (y / subpixelScale) - this.CenterY;
				double d = Math.Sqrt((xd * xd) + (yd * yd));
				if (d > 1)
				{
					double a = (Math.Cos((d / (16.0 * this.Period)) - this.Phase) * (1.0 / (this.Amplitude * d))) + 1.0;
					x = (int)(((xd * a) + this.CenterX) * subpixelScale);
					y = (int)(((yd * a) + this.CenterY) * subpixelScale);
				}
			}

			private void Swirl(ref int x, ref int y)
			{
				const double subpixelScale = (int)ImageFilterLookUpTable.image_subpixel_scale_e.image_subpixel_scale;
				double xd = (x / subpixelScale) - this.CenterX;
				double yd = (y / subpixelScale) - this.CenterY;
				double a = (100.0 - Math.Sqrt((xd * xd) + (yd * yd))) / 100.0 * (0.1 / -this.Amplitude);
				double sa = Math.Sin(a - (this.Phase / 25.0));
				double ca = Math.Cos(a - (this.Phase / 25.0));
				x = (int)(((xd * ca) - (yd * sa) + this.CenterX) * subpixelScale);
				y = (int)(((xd * sa) + (yd * ca) + this.CenterY) * subpixelScale);
			}
		}

		/// <summary>
		/// C++ span_interpolator_adaptor&lt;span_interpolator_linear, periodic_distortion&gt;: the linear
		/// interpolator's coordinates, then moved by the distortion.
		/// </summary>
		private sealed class DistortedInterpolator : ISpanInterpolator
		{
			private readonly span_interpolator_linear linear;

			private readonly Distortion distortion;

			public DistortedInterpolator(span_interpolator_linear linear, Distortion distortion)
			{
				this.linear = linear;
				this.distortion = distortion;
			}

			public void begin(double x, double y, int len) => this.linear.begin(x, y, len);

			public void coordinates(out int x, out int y)
			{
				this.linear.coordinates(out x, out y);
				this.distortion.Calculate(ref x, ref y);
			}

			public void Next() => this.linear.Next();

			public ITransform transformer() => this.linear.transformer();

			public void transformer(ITransform trans) => this.linear.transformer(trans);

			public void resynchronize(double xe, double ye, int len) => this.linear.resynchronize(xe, ye, len);

			public void local_scale(out int x, out int y) => this.linear.local_scale(out x, out y);
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
