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
	/// C++ AGG's image1.cpp: the spheres image turned and scaled through the 24-bit bilinear image filter
	/// (span_image_filter_rgb_bilinear_clip), seen through an ellipse that turns and scales with it. Where the
	/// ellipse reaches past the image it shows the filter's half-transparent green background.
	/// </summary>
	public class Image1Demo : AggDemo
	{
		// C++ rgba_pre(0, 0.4, 0, 0.5) as the filter's rgba8 background: premultiplied, so green is 0.2 of 255.
		private static readonly Color Background = new Color(0, 51, 0, 128);

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// The example's image and window are BGR24, and the span generator is the rgb one, which reads 3-byte
		// pixels; the GPU draws the 32-bit picture.
		private readonly ImageBuffer sourceImage = SpheresImage.Load();

		private readonly ImageBuffer sourceImage24;

		public Image1Demo()
		{
			this.sourceImage24 = new ImageBuffer(this.sourceImage.Width, this.sourceImage.Height, 24, new BlenderBGR());
			for (int y = 0; y < this.sourceImage.Height; y++)
			{
				for (int x = 0; x < this.sourceImage.Width; x++)
				{
					this.sourceImage24.SetPixel(x, y, this.sourceImage.GetPixel(x, y));
				}
			}

			// image1.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.AngleSlider = new SliderCtrl(5, 5, 300, 12, false) { Label = "Angle={0:F2}" };
			this.AngleSlider.SetRange(-180.0, 180.0);
			this.AngleSlider.Value = 0.0;
			this.ScaleSlider = new SliderCtrl(5, 5 + 15, 300, 12 + 15, false) { Label = "Scale={0:F2}" };
			this.ScaleSlider.SetRange(0.1, 5.0);
			this.ScaleSlider.Value = 1.0;

			this.ctrls.Add(this.AngleSlider);
			this.ctrls.Add(this.ScaleSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_angle</c>, in degrees.</summary>
		public SliderCtrl AngleSlider { get; }

		/// <summary>C++ <c>m_scale</c>.</summary>
		public SliderCtrl ScaleSlider { get; }

		public override string Name => "image1";

		public override string Category => "Images";

		public override string Description => "An image turned and scaled through a bilinear filter, seen through an ellipse that turns with it.";

		// agg_main opens the window at the image's size plus 20 by 60.
		public override int Width => this.sourceImage.Width + 20;

		public override int Height => this.sourceImage.Height + 40 + 20;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			double angle = this.AngleSlider.Value * Math.PI / 180.0;
			double scale = this.ScaleSlider.Value;

			// C++ spells the two matrices with different offsets, but both turn and scale about, and land on,
			// (Width / 2, Height / 2 + 20): the ellipse's center and the image's middle.
			Affine ellipseMatrix = Affine.NewTranslation(-this.Width / 2.0 - 10, -this.Height / 2.0 - 20 - 10);
			ellipseMatrix *= Affine.NewRotation(angle);
			ellipseMatrix *= Affine.NewScaling(scale);
			ellipseMatrix *= Affine.NewTranslation(this.Width / 2.0, this.Height / 2.0 + 20);

			Affine imageToDemo = Affine.NewTranslation(-this.Width / 2.0 + 10, -this.Height / 2.0 + 20 + 10);
			imageToDemo *= Affine.NewRotation(angle);
			imageToDemo *= Affine.NewScaling(scale);
			imageToDemo *= Affine.NewTranslation(this.Width / 2.0, this.Height / 2.0 + 20);

			double r = Math.Min(this.Width, this.Height - 60);
			var ellipse = new VertexSourceApplyTransform(
				new Ellipse(this.Width / 2.0 + 10, this.Height / 2.0 + 20 + 10, r / 2.0 + 16.0, r / 2.0 + 16.0, 200),
				ellipseMatrix);

			Affine transform = graphics.GetTransform();
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.RenderImageSpans(rasterizer, destination, transform, ellipse, imageToDemo);
				this.ctrls.Render(graphics);
				rasterizer.reset_clipping();
				return;
			}

			// The GPU runs the same bilinear filter through the clip accessor per pixel, over the 32-bit picture. It
			// composites straight alpha where C++ blends premultiplied (rb_pre), so its background is the straight
			// form of C++'s premultiplied rgba_pre(0, 0.4, 0, 0.5): green 102 at alpha 128 lands as 51 does there.
			if (graphics is IImageFilterGraphics filtered)
			{
				filtered.FillPathWithFilteredImage(ellipse, this.sourceImage, new ImageFilterFill(imageToDemo * transform)
				{
					Kind = ImageFilterKind.Bilinear,
					Edge = ImageFilterEdge.Clip,
					Background = new Color(0, 102, 0, 128),
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

		// The software reference: the ellipse's spans generated straight from the 24-bit image and blended
		// premultiplied (C++'s rb_pre), with the rasterizer clipped to the window as C++ sets it. A span generator
		// has no Graphics2D call, so the interpolator maps a frame pixel back through the graphics transform
		// into demo space itself.
		private void RenderImageSpans(ScanlineRasterizer rasterizer, IImageByte destination, Affine transform, IVertexSource ellipse, Affine imageToDemo)
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
			var accessor = new ImageBufferAccessorClip(this.sourceImage24, Background);
			var spanGenerator = new span_image_filter_rgb_bilinear_clip(accessor, Background, interpolator);

			rasterizer.SetVectorClipBox(0, 0, destination.Width, destination.Height);
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(ellipse, transform));

			IRecieveBlenderByte blender = destination.GetRecieveBlender();
			destination.SetRecieveBlender(new BlenderPreMultBGRA());
			try
			{
				new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
			}
			finally
			{
				destination.SetRecieveBlender(blender);
			}

			destination.MarkImageChanged();
		}
	}
}
