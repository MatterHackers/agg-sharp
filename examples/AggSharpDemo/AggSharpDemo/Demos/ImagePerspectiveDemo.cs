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
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's image_perspective.cpp: the spheres image mapped onto a quadrilateral whose corners you drag,
	/// by an affine parallelogram (nearest neighbour), a bilinear or a perspective transform (both of those
	/// through the 2x2 bilinear image filter).
	/// </summary>
	public class ImagePerspectiveDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly ImageBuffer sourceImage = SpheresImage.Load();

		public ImagePerspectiveDemo()
		{
			// image_perspective.cpp runs with flip_y = true and gives its rbox !flip_y.
			this.TransTypeRbox = new RboxCtrl(420, 5.0, 420 + 170.0, 70.0, false);
			this.TransTypeRbox.AddItem("Affine Parallelogram");
			this.TransTypeRbox.AddItem("Bilinear");
			this.TransTypeRbox.AddItem("Perspective");
			this.TransTypeRbox.CurrentItem = 2;
			this.ctrls.Add(this.TransTypeRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// on_init: the quad inset 100 from the window's edges.
			this.Quad.SetPoint(0, 100, 100);
			this.Quad.SetPoint(1, this.Width - 100, 100);
			this.Quad.SetPoint(2, this.Width - 100, this.Height - 100);
			this.Quad.SetPoint(3, 100, this.Height - 100);
		}

		/// <summary>C++ <c>m_trans_type</c>: 0 affine parallelogram, 1 bilinear, 2 perspective.</summary>
		public RboxCtrl TransTypeRbox { get; }

		/// <summary>C++ <c>m_quad</c>: the four corners the image maps onto.</summary>
		public InteractivePolygon Quad { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "image_perspective";

		public override string Category => "Images";

		public override string Description => "An image mapped onto a quadrilateral. Drag a corner, an edge or the whole shape; choose an affine, bilinear or perspective mapping.";

		public override int Width => 600;

		public override int Height => 600;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			if (this.TransTypeRbox.CurrentItem == 0)
			{
				// For the affine parallelogram the 4th corner is implicit.
				Vector2 p0 = this.Quad.GetPoint(0);
				Vector2 p1 = this.Quad.GetPoint(1);
				Vector2 p2 = this.Quad.GetPoint(2);
				this.Quad.SetPoint(3, p0.X + (p2.X - p1.X), p0.Y + (p2.Y - p1.Y));
			}

			// The quad tool is drawn first; the image covers its inside.
			graphics.Render(this.Quad, Rgba8.FromRgba(0, 0.3, 0.5, 0.6));

			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.RenderImageSpans(graphics, destination);
			}
			else if (graphics is IImageFilterGraphics filtered)
			{
				// The GPU runs the same generators per pixel through the same mappings.
				this.FillImage(graphics, filtered);
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
			this.ctrls.OnKeyDown(key);
		}

		// C++ trans_affine(parl, x1, y1, x2, y2), parl_to_rect: the parallelogram's first three corners to the
		// rectangle's (x1, y1), (x2, y1), (x2, y2).
		internal static Affine ParallelogramToRectangle(double[] parl, double x1, double y1, double x2, double y2)
		{
			var m = new Affine(parl[2] - parl[0], parl[3] - parl[1], parl[4] - parl[0], parl[5] - parl[1], parl[0], parl[1]);
			m.invert();
			m *= new Affine(x2 - x1, 0, x2 - x1, y2 - y1, x1, y1);
			return m;
		}

		// The GPU's draw: the reference's generator, mapping and edge through IImageFilterGraphics.
		private void FillImage(Graphics2D graphics, IImageFilterGraphics filtered)
		{
			double[] quad = this.QuadPolygon();
			double[] frameQuad = QuadImageFill.ToFrame(quad, graphics.GetTransform());
			double x2 = this.sourceImage.Width;
			double y2 = this.sourceImage.Height;
			ImageFilterFill fill = this.TransTypeRbox.CurrentItem switch
			{
				0 => QuadImageFill.Parallelogram(frameQuad, 0, 0, x2, y2),
				1 => QuadImageFill.Bilinear(frameQuad, 0, 0, x2, y2),
				_ => QuadImageFill.Perspective(frameQuad, 0, 0, x2, y2),
			};

			// C++ draws nothing but the quad and the rbox while the corners make the mapping singular.
			if (fill == null)
			{
				return;
			}

			fill.Kind = this.TransTypeRbox.CurrentItem == 0 ? ImageFilterKind.Nearest : ImageFilterKind.Filter;
			fill.Filter = new ImageFilterLookUpTable(new image_filter_bilinear(), false);
			filtered.FillPathWithFilteredImage(QuadImageFill.Outline(quad), this.sourceImage, fill);
		}

		private double[] QuadPolygon()
		{
			var quad = new double[8];
			for (int i = 0; i < 4; i++)
			{
				quad[i * 2] = this.Quad.GetPoint(i).X;
				quad[(i * 2) + 1] = this.Quad.GetPoint(i).Y;
			}

			return quad;
		}

		// The software reference: the quad's spans generated straight from the image, as C++ does, through the
		// premultiplied blender C++'s rb_pre uses.
		private void RenderImageSpans(Graphics2D graphics, IImageByte destination)
		{
			double[] quad = this.QuadPolygon();
			double x2 = this.sourceImage.Width;
			double y2 = this.sourceImage.Height;
			var accessor = new ImageBufferAccessorClamp(this.sourceImage);
			var filter = new ImageFilterLookUpTable(new image_filter_bilinear(), false);

			// A span generator has no Graphics2D call, so its interpolator maps a frame pixel back through the
			// graphics transform into demo space itself.
			Affine transform = graphics.GetTransform();
			ITransform ToImage(ITransform demoToImage) => transform.is_identity() ? demoToImage : new FrameToImage(transform, demoToImage);

			span_image_filter spanGenerator = null;
			switch (this.TransTypeRbox.CurrentItem)
			{
				case 0:
					spanGenerator = new span_image_filter_rgba_nn(accessor, new span_interpolator_linear(ToImage(ParallelogramToRectangle(quad, 0, 0, x2, y2))));
					break;

				case 1:
					var bilinear = new Bilinear(quad, 0, 0, x2, y2);
					if (bilinear.is_valid())
					{
						spanGenerator = new span_image_filter_rgba_2x2(accessor, new span_interpolator_linear(ToImage(bilinear)), filter);
					}

					break;

				default:
					var perspective = new Perspective(quad, 0, 0, x2, y2);
					if (perspective.is_valid())
					{
						spanGenerator = new span_image_filter_rgba_2x2(accessor, new span_interpolator_trans(ToImage(perspective)), filter);
					}

					break;
			}

			// C++ draws nothing but the quad and the rbox while the corners make the mapping singular.
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
			rasterizer.add_path(new VertexSourceApplyTransform(outline, transform));

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

		// Frame pixel -> demo space (the graphics transform undone) -> image space.
		private sealed class FrameToImage : ITransform
		{
			private readonly Affine frameToDemo;

			private readonly ITransform demoToImage;

			public FrameToImage(Affine demoToFrame, ITransform demoToImage)
			{
				this.frameToDemo = new Affine(demoToFrame);
				this.frameToDemo.invert();
				this.demoToImage = demoToImage;
			}

			public void Transform(ref double x, ref double y)
			{
				this.frameToDemo.Transform(ref x, ref y);
				this.demoToImage.Transform(ref x, ref y);
			}
		}
	}
}
