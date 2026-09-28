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
using System.Collections.Generic;
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's image_filters.cpp: the spheres image turned about its center, clipped to a circle, over and over
	/// through one of 17 reconstruction filters, so each filter's blur or ringing accumulates step by step.
	/// Single Step turns it once; RUN Test! turns it a full circle on idle; Refresh (or a new filter) starts over.
	/// </summary>
	public class ImageFiltersDemo : AggDemo
	{
		private static readonly string[] FilterNames =
		{
			"simple (NN)", "bilinear", "bicubic", "spline16", "spline36", "hanning", "hamming", "hermite", "kaiser",
			"quadric", "catrom", "gaussian", "bessel", "mitchell", "sinc", "lanczos", "blackman",
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// The original picture, as rbuf_img(2) keeps it.
		private readonly ImageBuffer originalImage = SpheresImage.Load();

		// Every transform_image since the last restart, the restart's own (no turn) first. Each view filters the ones
		// it has not yet drawn when it next draws, so only the view on screen does the work.
		private readonly List<FilterStep> steps = new List<FilterStep>();

		// How many of the steps shownImage has been through.
		private int softwareStepsDone;

		// The GPU view's img0 and img1: each step filters the previous result into the other layer on the device.
		private IRetainedLayer gpuShown;

		private IRetainedLayer gpuPrevious;

		// How many of the steps gpuShown has been through.
		private int gpuStepsDone;

		// rbuf_img(0), what on_draw shows, and rbuf_img(1), the previous result transform_image reads. The example
		// is pixfmt_bgr24, and its span filters are the rgb ones, so both are 24-bit; img0 blends through the
		// premultiplied blender, as transform_image's rb_pre does.
		private readonly ImageBuffer shownImage;

		private readonly ImageBuffer previousImage;

		// C++ m_cur_angle: how far the shown image has turned, in degrees.
		private double currentAngle;

		// C++ m_cur_filter: the filter the shown image was made with.
		private int currentFilter = 1;

		public ImageFiltersDemo()
		{
			int width = this.originalImage.Width;
			int height = this.originalImage.Height;
			this.shownImage = new ImageBuffer(width, height, 24, new BlenderPreMultBGR());
			this.previousImage = new ImageBuffer(width, height, 24, new BlenderPreMultBGR());

			// image_filters.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.StepSlider = new SliderCtrl(115, 5, 400, 11, false) { Label = "Step={0:F2}" };
			this.RadiusSlider = new SliderCtrl(115, 5 + 15, 400, 11 + 15, false) { Label = "Filter Radius={0:F3}" };
			this.FiltersRbox = new RboxCtrl(0.0, 0.0, 110.0, 210.0, false) { TextThickness = 0.85, BackgroundColor = Rgba8.FromRgba(0.0, 0.0, 0.0, 0.1) };
			this.NormalizeCbox = new CboxCtrl(8.0, 215.0, "Normalize Filter", false);
			this.RunCbox = new CboxCtrl(8.0, 245.0, "RUN Test!", false);
			this.SingleStepCbox = new CboxCtrl(8.0, 230.0, "Single Step", false);
			this.RefreshCbox = new CboxCtrl(8.0, 265.0, "Refresh", false);
			this.RunCbox.SetTextSize(7.5);
			this.SingleStepCbox.SetTextSize(7.5);
			this.NormalizeCbox.SetTextSize(7.5);
			this.RefreshCbox.SetTextSize(7.5);
			this.NormalizeCbox.Checked = true;

			this.RadiusSlider.SetRange(2.0, 8.0);
			this.RadiusSlider.Value = 4.0;
			this.StepSlider.SetRange(1.0, 10.0);
			this.StepSlider.Value = 5.0;

			foreach (string name in FilterNames)
			{
				this.FiltersRbox.AddItem(name);
			}

			this.FiltersRbox.CurrentItem = 1;
			this.FiltersRbox.SetBorderWidth(0, 0);
			this.FiltersRbox.SetTextSize(6.0);

			// add_ctrl order, which is also the order clicks are offered in. The radius slider takes clicks even
			// while it is hidden, as in C++.
			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Add(this.StepSlider);
			this.ctrls.Add(this.FiltersRbox);
			this.ctrls.Add(this.RunCbox);
			this.ctrls.Add(this.SingleStepCbox);
			this.ctrls.Add(this.NormalizeCbox);
			this.ctrls.Add(this.RefreshCbox);
			this.ctrls.Changed += (s, e) =>
			{
				this.OnCtrlChange();
				this.Invalidate();
			};

			// agg_main: copy_img_to_img(1, 0), copy_img_to_img(2, 0), transform_image(0.0).
			this.RestartFromOriginal();
		}

		/// <summary>C++ <c>m_step</c>: degrees turned per step.</summary>
		public SliderCtrl StepSlider { get; }

		/// <summary>C++ <c>m_radius</c>: the radius of sinc, lanczos and blackman, shown only for them.</summary>
		public SliderCtrl RadiusSlider { get; }

		/// <summary>C++ <c>m_filters</c>: which filter transform_image uses.</summary>
		public RboxCtrl FiltersRbox { get; }

		/// <summary>C++ <c>m_normalize</c>: normalize the filter lookup table's weights.</summary>
		public CboxCtrl NormalizeCbox { get; }

		/// <summary>C++ <c>m_run</c>: turn the image a full circle, one step per idle tick.</summary>
		public CboxCtrl RunCbox { get; }

		/// <summary>C++ <c>m_single_step</c>: turn the image one step, then uncheck.</summary>
		public CboxCtrl SingleStepCbox { get; }

		/// <summary>C++ <c>m_refresh</c>: start over from the original image, then uncheck.</summary>
		public CboxCtrl RefreshCbox { get; }

		/// <summary>C++ <c>m_num_steps</c>: how many steps the shown image has been turned.</summary>
		public int NumSteps { get; private set; }

		public override string Name => "image_filters";

		public override string Category => "Images";

		public override string Description => "An image turned again and again through one of 17 image filters, so each filter's blur or ringing builds up.";

		// agg_main opens the window at the image's size plus 110 by 40, at least 305 by 325.
		public override int Width => Math.Max(this.originalImage.Width + 110, 305);

		public override int Height => Math.Max(this.originalImage.Height + 40, 325);

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			// A GPU Graphics2D has a DestImage too, but no rasterizer.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte)
			{
				// The software reference: copy_from of the filtered image. An untransformed opaque image at whole
				// pixels is a straight copy.
				this.CatchUpSoftware();
				graphics.Render(this.shownImage, 110, 35);
			}
			else if (graphics.SupportsRetainedLayers && graphics is IImageFilterGraphics)
			{
				this.CatchUpGpu(graphics);
				graphics.RenderRetainedLayer(this.gpuShown, 110, 35);
			}
			else
			{
				// A surface that can neither run the span generators nor keep a filtered result draws the original
				// image turned by the same angle in one resampling, without the circle clip or the accumulated
				// filter artifacts that are the point of the demo.
				double angle = this.currentAngle * Math.PI / 180.0;
				double centerX = this.originalImage.Width / 2.0;
				double centerY = this.originalImage.Height / 2.0;
				double cos = Math.Cos(angle);
				double sin = Math.Sin(angle);
				double originX = 110 + centerX - (centerX * cos) + (centerY * sin);
				double originY = 35 + centerY - (centerX * sin) - (centerY * cos);
				graphics.Render(this.originalImage, originX, originY, angle, 1, 1);
			}

			// The "NSteps=%d" text: gsv_text 10 high, stroked 1.5 wide.
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; image_filters.cpp draws its step count with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.start_point(10.0, 295.0);
			text.size(10.0, 0.0);
			text.text("NSteps=" + this.NumSteps.ToString(CultureInfo.InvariantCulture));
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			graphics.Render(new Stroke(outline, 1.5), Color.Black);

			// on_draw's render order, not add_ctrl's.
			if (this.FiltersRbox.CurrentItem >= 14)
			{
				this.RadiusSlider.Render(graphics);
			}

			this.StepSlider.Render(graphics);
			this.FiltersRbox.Render(graphics);
			this.RunCbox.Render(graphics);
			this.NormalizeCbox.Render(graphics);
			this.SingleStepCbox.Render(graphics);
			this.RefreshCbox.Render(graphics);
		}

		/// <summary>
		/// C++ <c>on_ctrl_change</c>: Single Step turns the image once; RUN Test! starts the idle ticks; Refresh or a
		/// new filter starts over from the original image. The ctrls call it on any change.
		/// </summary>
		public void OnCtrlChange()
		{
			if (this.SingleStepCbox.Checked)
			{
				this.Step();
				this.SingleStepCbox.Checked = false;
			}

			if (this.RunCbox.Checked)
			{
				this.WaitMode = false;
			}

			if (this.RefreshCbox.Checked || this.FiltersRbox.CurrentItem != this.currentFilter)
			{
				this.RestartFromOriginal();
				this.RefreshCbox.Checked = false;
			}
		}

		/// <summary>C++ <c>on_idle</c>: while RUN Test! is checked, one step per tick until a full circle.</summary>
		public override void OnIdle()
		{
			if (this.RunCbox.Checked)
			{
				if (this.currentAngle < 360.0)
				{
					this.Step();
				}
				else
				{
					this.currentAngle = 0.0;
					this.WaitMode = true;
					this.RunCbox.Checked = false;
				}

				this.Invalidate();
			}
			else
			{
				this.WaitMode = true;
			}
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

		// One more step: copy_img_to_img(1, 0), then transform_image(step) turns the last result again.
		private void Step()
		{
			this.currentAngle += this.StepSlider.Value;
			this.steps.Add(this.NewStep(this.StepSlider.Value));
			this.NumSteps++;
		}

		// copy_img_to_img(1, 2), transform_image(0.0): the chosen filter applied to the untouched image.
		private void RestartFromOriginal()
		{
			this.currentAngle = 0.0;
			this.steps.Clear();
			this.steps.Add(this.NewStep(0.0));
			this.softwareStepsDone = 0;
			this.gpuStepsDone = 0;
			this.currentFilter = this.FiltersRbox.CurrentItem;
			this.NumSteps = 0;
		}

		// transform_image reads the ctrls when it runs, so a step keeps what they said then.
		private FilterStep NewStep(double angle)
		{
			int filter = this.FiltersRbox.CurrentItem;
			return new FilterStep(angle, filter, filter >= 2 ? this.NewFilterLookUpTable(this.NormalizeCbox.Checked) : null);
		}

		// The steps shownImage has not been through, as C++ ran them when they were taken.
		private void CatchUpSoftware()
		{
			for (; this.softwareStepsDone < this.steps.Count; this.softwareStepsDone++)
			{
				if (this.softwareStepsDone == 0)
				{
					this.CopyOriginalToPrevious();
				}
				else
				{
					CopyImage(this.shownImage, this.previousImage);
				}

				this.TransformImage(this.steps[this.softwareStepsDone]);
			}
		}

		/// <summary>
		/// transform_image on the device: each step not yet drawn filters the previous result (the original image for
		/// the first) into the other layer, so the filter's artifacts build up as they do in software without the
		/// picture leaving the GPU.
		/// </summary>
		/// <remarks>
		/// Not byte-exact against software: the shader steps span_interpolator_linear's subpixels as software does,
		/// but its circle's anti-aliasing is the GPU's, and it runs the rgba generators, made opaque as C++'s rgb ones
		/// are, over premultiplied colour (kaiser, 2 wide, gets the 2x2 generator). Each step filters the edge's
		/// differences a filter's reach further in.
		/// </remarks>
		private void CatchUpGpu(Graphics2D graphics)
		{
			if (this.gpuShown == null || !this.gpuShown.BelongsTo(graphics))
			{
				this.gpuShown?.Dispose();
				this.gpuPrevious?.Dispose();
				this.gpuShown = graphics.CreateRetainedLayer();
				this.gpuPrevious = graphics.CreateRetainedLayer();
				this.gpuStepsDone = 0;
			}
			else if (this.gpuShown.NeedsRepaintFor(graphics))
			{
				// Painted at another resolution (a supersampled capture): the whole run is filtered again.
				this.gpuStepsDone = 0;
			}

			int width = this.originalImage.Width;
			int height = this.originalImage.Height;
			for (; this.gpuStepsDone < this.steps.Count; this.gpuStepsDone++)
			{
				(this.gpuShown, this.gpuPrevious) = (this.gpuPrevious, this.gpuShown);
				FilterStep step = this.steps[this.gpuStepsDone];
				Affine sourceMatrix = SourceMatrix(width, height, step.Angle);
				var fill = new ImageFilterFill(sourceMatrix)
				{
					Kind = step.Filter == 0 ? ImageFilterKind.Nearest : step.Filter == 1 ? ImageFilterKind.Bilinear : ImageFilterKind.Filter,
					Filter = step.LookUpTable,
					Edge = ImageFilterEdge.Clip,

					// The rgb generators read image_accessor_clip's (0, 0, 0, 0) as black and give every pixel full
					// alpha; opaque black is what the rgba ones need to do the same.
					Background = Color.Black,

					// C++ runs the rgb generators: with Normalize Filter off the weights don't sum to 1, and that
					// shifts the colour's level while every pixel stays opaque.
					Opaque = true,
				};

				using (var paint = this.gpuShown.Begin(width, height))
				{
					paint.Graphics.Clear(Color.White);
					var filtered = (IImageFilterGraphics)paint.Graphics;
					IVertexSource circle = Circle(width, height, sourceMatrix);
					if (this.gpuStepsDone == 0)
					{
						filtered.FillPathWithFilteredImage(circle, this.originalImage, fill);
					}
					else
					{
						filtered.FillPathWithFilteredImage(circle, this.gpuPrevious, fill);
					}
				}
			}
		}

		// copy_img_to_img(1, 2): the original's colour into the 24-bit previous image.
		private void CopyOriginalToPrevious()
		{
			byte[] rgb = this.previousImage.GetBuffer();
			for (int y = 0; y < this.originalImage.Height; y++)
			{
				for (int x = 0; x < this.originalImage.Width; x++)
				{
					Color color = this.originalImage.GetPixel(x, y);
					int offset = this.previousImage.GetBufferOffsetXY(x, y);
					rgb[offset + ImageBuffer.OrderR] = color.red;
					rgb[offset + ImageBuffer.OrderG] = color.green;
					rgb[offset + ImageBuffer.OrderB] = color.blue;
				}
			}

			this.previousImage.MarkImageChanged();
		}

		private static void CopyImage(ImageBuffer source, ImageBuffer destination)
		{
			Buffer.BlockCopy(source.GetBuffer(), 0, destination.GetBuffer(), 0, source.GetBuffer().Length);
			destination.MarkImageChanged();
		}

		// The image turned by angle degrees about its center.
		private static Affine SourceMatrix(double width, double height, double angle)
		{
			Affine sourceMatrix = Affine.NewTranslation(-width / 2.0, -height / 2.0);
			sourceMatrix *= Affine.NewRotation(angle * Math.PI / 180.0);
			sourceMatrix *= Affine.NewTranslation(width / 2.0, height / 2.0);
			return sourceMatrix;
		}

		// The circle 4 in from the image's nearer edges, turned with it.
		private static IVertexSource Circle(double width, double height, Affine sourceMatrix)
		{
			double r = Math.Min(width, height);
			r *= 0.5;
			r -= 4.0;
			return new VertexSourceApplyTransform(new Ellipse(width / 2.0, height / 2.0, r, r, 200), sourceMatrix);
		}

		// transform_image(angle): the previous image turned by angle about its center, clipped to a circle 4 in
		// from its nearer edges, through the chosen filter onto white.
		private void TransformImage(FilterStep step)
		{
			double width = this.shownImage.Width;
			double height = this.shownImage.Height;

			// rb.clear(white): every byte of a bgr24 image.
			Array.Fill(this.shownImage.GetBuffer(), (byte)255);

			Affine sourceMatrix = SourceMatrix(width, height, step.Angle);
			var imageMatrix = new Affine(sourceMatrix);
			imageMatrix.invert();

			IVertexSource circle = Circle(width, height, sourceMatrix);

			var interpolator = new span_interpolator_linear(imageMatrix);
			var source = new ImageBufferAccessorClip(this.previousImage, new Color(0, 0, 0, 0));

			span_image_filter spanGenerator;
			switch (step.Filter)
			{
				case 0:
					spanGenerator = new span_image_filter_rgb_nn(source, interpolator);
					break;

				case 1:
					spanGenerator = new span_image_filter_rgb_bilinear_clip(source, new Color(0, 0, 0, 0), interpolator);
					break;

				case 5:
				case 6:
				case 7:
					spanGenerator = new span_image_filter_rgb_2x2(source, interpolator, step.LookUpTable);
					break;

				default:
					spanGenerator = new span_image_filter_rgb(source, interpolator, step.LookUpTable);
					break;
			}

			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(circle);
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), this.shownImage, new span_allocator(), spanGenerator);
			this.shownImage.MarkImageChanged();
		}

		private ImageFilterLookUpTable NewFilterLookUpTable(bool normalize)
		{
			double radius = this.RadiusSlider.Value;
			IImageFilterFunction function = this.FiltersRbox.CurrentItem switch
			{
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

			var lookUpTable = new ImageFilterLookUpTable();
			lookUpTable.calculate(function, normalize);
			return lookUpTable;
		}

		/// <summary>One transform_image: the turn in degrees, the filter's index and its weights (null for 0 and 1).</summary>
		private sealed record FilterStep(double Angle, int Filter, ImageFilterLookUpTable LookUpTable);
	}
}
