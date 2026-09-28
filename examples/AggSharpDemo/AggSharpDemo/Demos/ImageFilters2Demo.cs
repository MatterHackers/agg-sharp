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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's image_filters2.cpp: a 4x4 image blown up to 300 pixels square through one of 17 reconstruction
	/// filters, so each filter's blur, ringing or blockiness is plain to see, with the filter's lookup table graphed
	/// beside it.
	/// </summary>
	public class ImageFilters2Demo : AggDemo
	{
		private static readonly string[] FilterNames =
		{
			"simple (NN)", "bilinear", "bicubic", "spline16", "spline36", "hanning", "hamming", "hermite", "kaiser",
			"quadric", "catrom", "gaussian", "bessel", "mitchell", "sinc", "lanczos", "blackman",
		};

		// C++ g_image, row 0 first.
		private static readonly Color[] ImagePixels =
		{
			Rgba8.FromRgba(0, 1, 0), Rgba8.FromRgba(1, 0, 0), Rgba8.FromRgba(1, 1, 1), Rgba8.FromRgba(0, 0, 1),
			Rgba8.FromRgba(0, 0, 1), Rgba8.FromRgba(0, 0, 0), Rgba8.FromRgba(1, 1, 1), Rgba8.FromRgba(1, 1, 1),
			Rgba8.FromRgba(1, 1, 1), Rgba8.FromRgba(1, 1, 1), Rgba8.FromRgba(1, 0, 0), Rgba8.FromRgba(0, 0, 1),
			Rgba8.FromRgba(1, 0, 0), Rgba8.FromRgba(1, 1, 1), Rgba8.FromRgba(0, 0, 0), Rgba8.FromRgba(0, 1, 0),
		};

		// C++ para: where the image's corners land, (0, 0), (4, 0), (4, 4) and (0, 4).
		private static readonly double[] Parallelogram = { 200, 40, 200 + 300, 40, 200 + 300, 40 + 300, 200, 40 + 300 };

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly ImageBuffer image = NewImage();

		public ImageFilters2Demo()
		{
			// image_filters2.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.RadiusSlider = new SliderCtrl(115, 5, 500 - 5, 11, false) { Label = "Filter Radius={0:F3}" };
			this.FiltersRbox = new RboxCtrl(0.0, 0.0, 110.0, 210.0, false) { TextThickness = 0.85, BackgroundColor = Rgba8.FromRgba(0.0, 0.0, 0.0, 0.1) };
			this.NormalizeCbox = new CboxCtrl(8.0, 215.0, "Normalize Filter", false);
			this.NormalizeCbox.SetTextSize(7.5);
			this.NormalizeCbox.Checked = true;

			this.RadiusSlider.SetRange(2.0, 8.0);
			this.RadiusSlider.Value = 4.0;

			foreach (string name in FilterNames)
			{
				this.FiltersRbox.AddItem(name);
			}

			this.FiltersRbox.CurrentItem = 1;
			this.FiltersRbox.SetBorderWidth(0, 0);
			this.FiltersRbox.SetTextSize(6.0);

			// add_ctrl order. The radius slider takes clicks even while it is hidden, as in C++.
			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Add(this.FiltersRbox);
			this.ctrls.Add(this.NormalizeCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_radius</c>: the radius of sinc, lanczos and blackman, shown only for them.</summary>
		public SliderCtrl RadiusSlider { get; }

		/// <summary>C++ <c>m_filters</c>: which filter draws the image.</summary>
		public RboxCtrl FiltersRbox { get; }

		/// <summary>C++ <c>m_normalize</c>: normalize the filter lookup table's weights.</summary>
		public CboxCtrl NormalizeCbox { get; }

		public override string Name => "image_filters2";

		public override string Category => "Images";

		public override string Description => "A 4x4 image blown up through one of 17 image filters, with the filter's shape graphed beside it.";

		// agg_main: init(500, 340, 0).
		public override int Width => 500;

		public override int Height => 340;

		public override void Draw(Graphics2D graphics)
		{
			// The example also copies rbuf_img(0) to (110, 35), but it never loads an image there, so that draws nothing.
			graphics.Clear(Color.White);

			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.RenderImageSpans(rasterizer, destination, graphics.GetTransform());
			}
			else if (graphics is IImageFilterGraphics filtered)
			{
				// The GPU runs the same filter per pixel. Its mapping is in the target's pixels, so the graphics
				// transform follows the parallelogram's.
				Affine imageToFrame = ParallelogramToImage();
				imageToFrame.invert();
				imageToFrame *= graphics.GetTransform();
				filtered.FillPathWithFilteredImage(Outline(), this.image, new ImageFilterFill(imageToFrame)
				{
					Kind = this.FiltersRbox.CurrentItem == 0 ? ImageFilterKind.Nearest : ImageFilterKind.Filter,
					Filter = this.FiltersRbox.CurrentItem == 0 ? null : this.NewFilterLookUpTable(),
					Edge = ImageFilterEdge.Clamp,
				});
			}

			if (this.FiltersRbox.CurrentItem != 0)
			{
				DrawLookUpTable(graphics, this.NewFilterLookUpTable());
			}

			if (this.FiltersRbox.CurrentItem >= 14)
			{
				this.RadiusSlider.Render(graphics);
			}

			this.FiltersRbox.Render(graphics);
			this.NormalizeCbox.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags) => this.ctrls.OnMouseDown(x, y, button);

		public override void OnMouseMove(int x, int y, AggInputFlags flags) => this.ctrls.OnMouseMove(x, y, flags);

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags) => this.ctrls.OnMouseUp(x, y, button);

		public override void OnKeyDown(Keys key, AggInputFlags flags) => this.ctrls.OnKeyDown(key);

		private static ImageBuffer NewImage()
		{
			var image = new ImageBuffer(4, 4);
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					image.SetPixel(x, y, ImagePixels[(4 * y) + x]);
				}
			}

			return image;
		}

		// C++ trans_affine(para, 0, 0, 4, 4), parl_to_rect: the parallelogram's first three corners to the image's
		// (0, 0), (4, 0) and (4, 4), built as C++ parl_to_parl builds it so it rounds the same.
		private static Affine ParallelogramToImage()
		{
			double[] p = Parallelogram;
			var matrix = new Affine(p[2] - p[0], p[3] - p[1], p[4] - p[0], p[5] - p[1], p[0], p[1]);
			matrix.invert();
			matrix *= new Affine(4, 0, 4, 4, 0, 0);
			return matrix;
		}

		// The filter's lookup table graphed under the rbox, over a 16-column grid, as image_fltr_graph draws it.
		private static void DrawLookUpTable(Graphics2D graphics, ImageFilterLookUpTable filter)
		{
			double xStart = 5.0;
			double xEnd = 195.0;
			double yStart = 235.0;
			double yEnd = 340 - 5.0;

			// C++ srgba8(0, 0, 0, a) drawn into the rgba8 window: black is black in sRGB too, and alpha is not converted.
			for (int i = 0; i <= 16; i++)
			{
				double x = xStart + ((xEnd - xStart) * i / 16.0);
				var gridLine = new VertexStorage();
				gridLine.MoveTo(x + 0.5, yStart);
				gridLine.LineTo(x + 0.5, yEnd);
				graphics.Render(new Stroke(gridLine, 0.8), new Color(0, 0, 0, i == 8 ? 255 : 100));
			}

			double ys = yStart + ((yEnd - yStart) / 6.0);
			var baseLine = new VertexStorage();
			baseLine.MoveTo(xStart, ys);
			baseLine.LineTo(xEnd, ys);
			graphics.Render(new Stroke(baseLine, 0.8), new Color(0, 0, 0));

			double radius = filter.radius();
			int n = (int)(radius * 256 * 2);
			double dx = (xEnd - xStart) * radius / 8.0;
			double dy = yEnd - ys;

			int[] weights = filter.weight_array();
			double filterScale = (int)ImageFilterLookUpTable.image_filter_scale_e.image_filter_scale;
			double xs = ((xEnd + xStart) / 2.0) - (filter.diameter() * (xEnd - xStart) / 32.0);
			int nn = filter.diameter() * 256;
			var tableCurve = new VertexStorage();
			tableCurve.MoveTo(xs + 0.5, ys + (dy * weights[0] / filterScale));
			for (int i = 1; i < nn; i++)
			{
				tableCurve.LineTo(xs + (dx * i / n) + 0.5, ys + (dy * weights[i] / filterScale));
			}

			graphics.Render(new Stroke(tableCurve, 0.8), SrgbLut.FromSrgba8(100, 0, 0));
		}

		// The image through the chosen filter onto the parallelogram. image_accessor_clone repeats the edge pixels
		// outside the image, and the window is the plain (not premultiplied) bgra32 the example is built with.
		private void RenderImageSpans(ScanlineRasterizer rasterizer, IImageByte destination, Affine transform)
		{
			Affine frameToImage = ParallelogramToImage();
			if (!transform.is_identity())
			{
				frameToImage = new Affine(transform);
				frameToImage.invert();
				frameToImage *= ParallelogramToImage();
			}

			var interpolator = new span_interpolator_linear(frameToImage);
			var source = new ImageBufferAccessorClamp(this.image);
			span_image_filter spanGenerator = this.FiltersRbox.CurrentItem == 0
				? new span_image_filter_rgba_nn(source, interpolator)
				: new span_image_filter_rgba(source, interpolator, this.NewFilterLookUpTable());

			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(Outline(), transform));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
			destination.MarkImageChanged();
		}

		private static VertexStorage Outline()
		{
			var outline = new VertexStorage();
			outline.MoveTo(Parallelogram[0], Parallelogram[1]);
			outline.LineTo(Parallelogram[2], Parallelogram[3]);
			outline.LineTo(Parallelogram[4], Parallelogram[5]);
			outline.LineTo(Parallelogram[6], Parallelogram[7]);
			return outline;
		}

		private ImageFilterLookUpTable NewFilterLookUpTable()
		{
			double radius = this.RadiusSlider.Value;
			IImageFilterFunction function = this.FiltersRbox.CurrentItem switch
			{
				1 => new image_filter_bilinear(),
				2 => new image_filter_bicubic(),
				3 => new image_filter_spline16(),
				4 => new image_filter_spline36(),
				5 => new image_filter_hanning(),
				6 => new image_filter_hamming(),
				7 => new image_filter_hermite(),
				8 => new image_filter_kaiser(),
				9 => new image_filter_quadric(),
				10 => new image_filter_catrom(),
				11 => new image_filter_gaussian(),
				12 => new image_filter_bessel(),
				13 => new image_filter_mitchell(),
				14 => new image_filter_sinc(radius),
				15 => new image_filter_lanczos(radius),
				_ => new image_filter_blackman(radius),
			};

			return new ImageFilterLookUpTable(function, this.NormalizeCbox.Checked);
		}
	}
}
