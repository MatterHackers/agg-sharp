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
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's pattern_resample.cpp: pattern_perspective's mirrored "agg" picture on a draggable quad, here also
	/// through the resampling filters, which widen the hanning filter to cover every source pixel a screen pixel
	/// spans when the pattern is shrunk (with an extra blur), and with the picture filtered in linear light: taken
	/// through a gamma table's direct curve before and the window through its inverse after.
	/// </summary>
	/// <remarks>C++ also prints how long the pattern took ("%3.2f ms"); it differs every frame, so the port leaves it out.</remarks>
	public class PatternResampleDemo : AggDemo
	{
		// C++ g_x1 .. g_y2: the pattern's rectangle that maps onto the quad.
		private const double PatternX1 = -150;
		private const double PatternY1 = -150;
		private const double PatternX2 = 150;
		private const double PatternY2 = 150;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly GammaLookUpTable gammaLut = new GammaLookUpTable(2.0);

		private readonly ImageBuffer picture = PatternPerspectiveDemo.LoadAggPicture();

		private ImageBuffer linearPicture;

		private double linearPictureGamma = double.NaN;

		// The linear picture as 32 bit, for the GPU's filtered image fill.
		private ImageBuffer linearPicture32;

		// The linear picture linearPicture32 was copied from, so a gamma change remakes it.
		private ImageBuffer linearPicture32Source;

		public PatternResampleDemo()
		{
			// pattern_resample.cpp runs with flip_y = true and gives its controls !flip_y.
			this.TransTypeRbox = new RboxCtrl(400, 5.0, 430 + 170.0, 100.0, false);
			this.TransTypeRbox.SetTextSize(7);
			this.TransTypeRbox.AddItem("Affine No Resample");
			this.TransTypeRbox.AddItem("Affine Resample");
			this.TransTypeRbox.AddItem("Perspective No Resample LERP");
			this.TransTypeRbox.AddItem("Perspective No Resample Exact");
			this.TransTypeRbox.AddItem("Perspective Resample LERP");
			this.TransTypeRbox.AddItem("Perspective Resample Exact");
			this.TransTypeRbox.CurrentItem = 4;
			this.ctrls.Add(this.TransTypeRbox);

			this.GammaSlider = new SliderCtrl(5.0, 5.0 + (15 * 0), 400 - 5, 10.0 + (15 * 0), false) { Label = "Gamma={0:F3}" };
			this.GammaSlider.SetRange(0.5, 3.0);
			this.GammaSlider.Value = 2.0;
			this.ctrls.Add(this.GammaSlider);

			this.BlurSlider = new SliderCtrl(5.0, 5.0 + (15 * 1), 400 - 5, 10.0 + (15 * 1), false) { Label = "Blur={0:F3}" };
			this.BlurSlider.SetRange(0.5, 2.0);
			this.BlurSlider.Value = 1.0;
			this.ctrls.Add(this.BlurSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// on_init: a 400 pixel square centered in the window.
			double dx = this.Width / 2.0;
			double dy = this.Height / 2.0;
			this.Quad.SetPoint(0, Math.Floor(-200 + dx), Math.Floor(-200 + dy));
			this.Quad.SetPoint(1, Math.Floor(200 + dx), Math.Floor(-200 + dy));
			this.Quad.SetPoint(2, Math.Floor(200 + dx), Math.Floor(200 + dy));
			this.Quad.SetPoint(3, Math.Floor(-200 + dx), Math.Floor(200 + dy));
		}

		/// <summary>C++ <c>m_trans_type</c>: which transform and which span generator draw the pattern.</summary>
		public RboxCtrl TransTypeRbox { get; }

		/// <summary>C++ <c>m_gamma</c>: the gamma the picture is filtered under.</summary>
		public SliderCtrl GammaSlider { get; }

		/// <summary>C++ <c>m_blur</c>: the resampling filters' footprint, times the scale.</summary>
		public SliderCtrl BlurSlider { get; }

		/// <summary>C++ <c>m_quad</c>: the four corners the pattern's rectangle maps onto.</summary>
		public InteractivePolygon Quad { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "pattern_resample";

		public override string Category => "Images";

		public override string Description => "A small picture mirrored into an endless pattern and mapped onto a quadrilateral, resampled so it stays smooth when shrunk. Drag a corner, an edge or the whole shape; press the space bar to turn it; choose a transform and filter; the sliders set the gamma and blur.";

		public override int Width => 600;

		public override int Height => 600;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			if (this.TransTypeRbox.CurrentItem < 2)
			{
				// For the affine parallelogram the 4th corner is implicit.
				Vector2 p0 = this.Quad.GetPoint(0);
				Vector2 p1 = this.Quad.GetPoint(1);
				Vector2 p2 = this.Quad.GetPoint(2);
				this.Quad.SetPoint(3, p0.X + (p2.X - p1.X), p0.Y + (p2.Y - p1.Y));
			}

			// C++ draws the quad tool first; the pattern covers the quad's inside.
			graphics.Render(this.Quad, Rgba8.FromRgba(0, 0.3, 0.5, 0.1));

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-window CPU layer.
			if (graphics.Rasterizer != null)
			{
				// An image's Graphics2D draws through a clipping proxy; the gamma pass needs the image under it.
				ImageBuffer destination = graphics.DestImage as ImageBuffer ?? (graphics.DestImage as ImageProxy)?.LinkedImage as ImageBuffer;
				if (destination != null)
				{
					this.RenderPatternSpans(graphics, destination);

					// C++ takes the whole window back out of linear light before drawing the controls.
					destination.ApplyGammaInv(this.gammaLut);
				}
			}
			else if (graphics is IImageFilterGraphics filtered)
			{
				this.FillPattern(graphics, filtered);

				// As the software path: the whole frame back out of linear light before the controls.
				GammaFill.ApplyGammaInv(graphics, this.gammaLut, this.Width, this.Height);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.Quad.OnMouseButtonDown(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				if (this.Quad.OnMouseMove(x, y))
				{
					this.Invalidate();
				}
			}
			else if (this.Quad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.Quad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			if (key == Keys.Space)
			{
				// C++ on_key: the space bar turns the quad a quarter turn about its center.
				double cx = 0;
				double cy = 0;
				for (int i = 0; i < 4; i++)
				{
					cx += this.Quad.GetPoint(i).X;
					cy += this.Quad.GetPoint(i).Y;
				}

				cx /= 4;
				cy /= 4;
				Affine turn = Affine.NewTranslation(-cx, -cy);
				turn *= Affine.NewRotation(Math.PI / 2.0);
				turn *= Affine.NewTranslation(cx, cy);
				for (int i = 0; i < 4; i++)
				{
					double x = this.Quad.GetPoint(i).X;
					double y = this.Quad.GetPoint(i).Y;
					turn.Transform(ref x, ref y);
					this.Quad.SetPoint(i, x, y);
				}

				this.Invalidate();
			}
		}

		// The quad's corners in frame pixels: the graphics transform is affine, so the pattern's affine and
		// perspective mappings built from these carry frame pixels straight to the pattern, as C++'s do window
		// pixels.
		private double[] FrameQuad(Affine demoToFrame)
		{
			var quad = new double[8];
			for (int i = 0; i < 4; i++)
			{
				double x = this.Quad.GetPoint(i).X;
				double y = this.Quad.GetPoint(i).Y;
				demoToFrame.Transform(ref x, ref y);
				quad[i * 2] = x;
				quad[(i * 2) + 1] = y;
			}

			return quad;
		}

		// C++ load_img then apply_gamma_dir, redone only when the gamma changes: the picture in linear light.
		private ImageBuffer LinearPicture()
		{
			if (this.linearPictureGamma != this.GammaSlider.Value)
			{
				this.gammaLut.SetGamma(this.GammaSlider.Value);
				this.linearPicture = new ImageBuffer(this.picture);
				ImageGammaInverse.ApplyDir(this.linearPicture, this.gammaLut);
				this.linearPictureGamma = this.GammaSlider.Value;
			}

			return this.linearPicture;
		}

		// The software reference: the quad's spans generated straight from the pattern, as C++ does, through the
		// premultiplied blender C++'s rb_pre uses.
		private void RenderPatternSpans(Graphics2D graphics, ImageBuffer destination)
		{
			ImageBuffer linear = this.LinearPicture();
			var accessor = new ImageBufferAccessorWrap(linear, new WrapModeReflectAutoPow2(linear.Width), new WrapModeReflectAutoPow2(linear.Height));

			// C++ image_filter_lut(image_filter_hanning, true): a normalized lookup table.
			var filter = new ImageFilterLookUpTable(new image_filter_hanning(), true);

			double[] quad = this.FrameQuad(graphics.GetTransform());
			double blur = this.BlurSlider.Value;
			span_image_filter spanGenerator = null;
			switch (this.TransTypeRbox.CurrentItem)
			{
				case 0:
					spanGenerator = new span_image_filter_rgb_2x2(accessor, new span_interpolator_linear(ImagePerspectiveDemo.ParallelogramToRectangle(quad, PatternX1, PatternY1, PatternX2, PatternY2)), filter);
					break;

				case 1:
					var affineResample = new span_image_resample_rgb_affine(accessor, new span_interpolator_linear(ImagePerspectiveDemo.ParallelogramToRectangle(quad, PatternX1, PatternY1, PatternX2, PatternY2)), filter);
					affineResample.blur(blur);
					spanGenerator = affineResample;
					break;

				case 2:
				case 3:
					var perspective = new Perspective(quad, PatternX1, PatternY1, PatternX2, PatternY2);
					if (perspective.is_valid())
					{
						ISpanInterpolator interpolator = this.TransTypeRbox.CurrentItem == 2 ? new span_interpolator_linear_subdiv(perspective) : new span_interpolator_trans(perspective);
						spanGenerator = new span_image_filter_rgb_2x2(accessor, interpolator, filter);
					}

					break;

				case 4:
					var lerp = new span_interpolator_persp_lerp(quad, PatternX1, PatternY1, PatternX2, PatternY2);
					if (lerp.is_valid())
					{
						var resample = new span_image_resample_rgb(accessor, new span_subdiv_adaptor(lerp), filter);
						resample.blur(blur);
						spanGenerator = resample;
					}

					break;

				default:
					var exact = new span_interpolator_persp_exact(quad, PatternX1, PatternY1, PatternX2, PatternY2);
					if (exact.is_valid())
					{
						var resample = new span_image_resample_rgb(accessor, new span_subdiv_adaptor(exact), filter);
						resample.blur(blur);
						spanGenerator = resample;
					}

					break;
			}

			// C++ draws nothing but the quad while the corners make the mapping singular.
			if (spanGenerator == null)
			{
				return;
			}

			var outline = new VertexStorage();
			outline.MoveTo(quad[0], quad[1]);
			outline.LineTo(quad[2], quad[3]);
			outline.LineTo(quad[4], quad[5]);
			outline.LineTo(quad[6], quad[7]);

			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			rasterizer.reset();
			rasterizer.add_path(outline);

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

		// The GPU's draw: the reference's generator, mapping, blur and reflect wrap through IImageFilterGraphics, from
		// the picture in linear light; Draw takes the frame back out through the inverse gamma
		// (IGammaGraphics.MapChannels) as the reference does. The perspective lerp and exact resamples both run the
		// exact mapping's local scale per pixel.
		private void FillPattern(Graphics2D graphics, IImageFilterGraphics filtered)
		{
			double[] frameQuad = this.FrameQuad(graphics.GetTransform());
			int mode = this.TransTypeRbox.CurrentItem;
			ImageFilterFill fill = mode < 2
				? QuadImageFill.Parallelogram(frameQuad, PatternX1, PatternY1, PatternX2, PatternY2)
				: QuadImageFill.Perspective(frameQuad, PatternX1, PatternY1, PatternX2, PatternY2);

			// C++ draws nothing but the quad while the corners make the mapping singular.
			if (fill == null)
			{
				return;
			}

			fill.Kind = mode == 1 || mode >= 4 ? ImageFilterKind.Resample : ImageFilterKind.Filter;
			fill.Filter = new ImageFilterLookUpTable(new image_filter_hanning(), true);
			fill.Blur = this.BlurSlider.Value;
			fill.Edge = ImageFilterEdge.Reflect;

			ImageBuffer linear = this.LinearPicture();
			if (this.linearPicture32Source != linear)
			{
				this.linearPicture32 = QuadImageFill.To32Bit(linear);
				this.linearPicture32Source = linear;
			}

			filtered.FillPathWithFilteredImage(QuadImageFill.Outline(this.FrameQuad(Affine.NewIdentity())), this.linearPicture32, fill);
		}
	}
}
