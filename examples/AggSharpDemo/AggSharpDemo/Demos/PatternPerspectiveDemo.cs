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
using System.IO;
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
	/// C++ AGG's pattern_perspective.cpp: the small "agg" picture, mirrored at every edge to tile the plane
	/// (image_accessor_wrap with wrap_mode_reflect_auto_pow2), mapped onto a quadrilateral whose corners you drag, by
	/// an affine, a bilinear or a perspective transform, through the 2x2 hanning filter.
	/// </summary>
	public class PatternPerspectiveDemo : AggDemo
	{
		// C++ g_x1 .. g_y2: the pattern's rectangle that maps onto the quad.
		private const double PatternX1 = -150;
		private const double PatternY1 = -150;
		private const double PatternX2 = 150;
		private const double PatternY2 = 150;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly ImageBuffer pattern = LoadAggPicture();

		// The picture as 32 bit, for the GPU's filtered image fill.
		private ImageBuffer pattern32;

		public PatternPerspectiveDemo()
		{
			// pattern_perspective.cpp runs with flip_y = true and gives its rbox !flip_y.
			this.TransTypeRbox = new RboxCtrl(460, 5.0, 420 + 170.0, 60.0, false) { TextThickness = 1 };
			this.TransTypeRbox.SetTextSize(8);
			this.TransTypeRbox.AddItem("Affine");
			this.TransTypeRbox.AddItem("Bilinear");
			this.TransTypeRbox.AddItem("Perspective");
			this.TransTypeRbox.CurrentItem = 2;
			this.ctrls.Add(this.TransTypeRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			// on_init: a 400 pixel square centered in the window.
			double dx = this.Width / 2.0;
			double dy = this.Height / 2.0;
			this.Quad.SetPoint(0, Math.Floor(-200 + dx), Math.Floor(-200 + dy));
			this.Quad.SetPoint(1, Math.Floor(200 + dx), Math.Floor(-200 + dy));
			this.Quad.SetPoint(2, Math.Floor(200 + dx), Math.Floor(200 + dy));
			this.Quad.SetPoint(3, Math.Floor(-200 + dx), Math.Floor(200 + dy));
		}

		/// <summary>C++ <c>m_trans_type</c>: 0 affine, 1 bilinear, 2 perspective.</summary>
		public RboxCtrl TransTypeRbox { get; }

		/// <summary>C++ <c>m_quad</c>: the four corners the pattern's rectangle maps onto.</summary>
		public InteractivePolygon Quad { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "pattern_perspective";

		public override string Category => "Images";

		public override string Description => "A small picture mirrored into an endless pattern and mapped onto a quadrilateral. Drag a corner, an edge or the whole shape; choose an affine, bilinear or perspective mapping.";

		public override int Width => 600;

		public override int Height => 600;

		/// <summary>
		/// C++ <c>load_img(0, "agg")</c> in a flip_y = true example: art/agg.ppm as a 24-bit image, row 0 the
		/// picture's bottom so it shows upright in a y-up demo.
		/// </summary>
		public static ImageBuffer LoadAggPicture()
		{
			const string resourceName = "MatterHackers.AggSharpDemo.Images.agg.ppm";
			using var stream = typeof(PatternPerspectiveDemo).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The image resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from Images/agg.ppm.");
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			byte[] ppm = memory.ToArray();

			// Reads only what agg.ppm is: a binary (P6) PPM, 8 bits a channel, a comment allowed in the header.
			int position = 0;
			if (NextToken(ppm, ref position) != "P6")
			{
				throw new InvalidDataException("agg.ppm is expected to be a binary (P6) PPM.");
			}

			int width = int.Parse(NextToken(ppm, ref position));
			int height = int.Parse(NextToken(ppm, ref position));
			NextToken(ppm, ref position);
			position++; // the one whitespace byte before the pixels

			var image = new ImageBuffer(width, height, 24, new BlenderBGR());
			byte[] buffer = image.GetBuffer();
			for (int y = 0; y < height; y++)
			{
				int destination = image.GetBufferOffsetY(height - 1 - y);
				for (int x = 0; x < width; x++)
				{
					int i = position + (((y * width) + x) * 3);
					buffer[destination + (x * 3) + ImageBuffer.OrderR] = ppm[i];
					buffer[destination + (x * 3) + ImageBuffer.OrderG] = ppm[i + 1];
					buffer[destination + (x * 3) + ImageBuffer.OrderB] = ppm[i + 2];
				}
			}

			return image;
		}

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

			// C++ draws the quad tool and the rbox first; the pattern covers the quad's inside.
			graphics.Render(this.Quad, Rgba8.FromRgba(0, 0.3, 0.5, 0.6));
			this.ctrls.Render(graphics);

			double[] quad = this.QuadPolygon();
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.RenderPatternSpans(graphics, destination, quad);
			}
			else if (graphics is IImageFilterGraphics filtered)
			{
				this.FillPattern(graphics, filtered, quad);
			}
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

		private static string NextToken(byte[] data, ref int position)
		{
			while (true)
			{
				if (data[position] == '#')
				{
					while (data[position] != '\n')
					{
						position++;
					}
				}
				else if (char.IsWhiteSpace((char)data[position]))
				{
					position++;
				}
				else
				{
					break;
				}
			}

			int start = position;
			while (!char.IsWhiteSpace((char)data[position]))
			{
				position++;
			}

			return System.Text.Encoding.ASCII.GetString(data, start, position - start);
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

		// The software reference: the quad's spans generated straight from the pattern, as C++ does, through the
		// premultiplied blender C++'s rb_pre uses.
		private void RenderPatternSpans(Graphics2D graphics, IImageByte destination, double[] quad)
		{
			var accessor = new ImageBufferAccessorWrap(this.pattern, new WrapModeReflectAutoPow2(this.pattern.Width), new WrapModeReflectAutoPow2(this.pattern.Height));

			// C++ image_filter<image_filter_hanning>: a normalized lookup table.
			var filter = new ImageFilterLookUpTable(new image_filter_hanning(), true);

			// A span generator has no Graphics2D call, so its interpolator maps a frame pixel back through the
			// graphics transform into demo space itself.
			Affine transform = graphics.GetTransform();
			ITransform ToPattern(ITransform demoToPattern) => transform.is_identity() ? demoToPattern : new FrameToPattern(transform, demoToPattern);

			span_image_filter spanGenerator = null;
			switch (this.TransTypeRbox.CurrentItem)
			{
				case 0:
					spanGenerator = new span_image_filter_rgb_2x2(accessor, new span_interpolator_linear(ToPattern(ImagePerspectiveDemo.ParallelogramToRectangle(quad, PatternX1, PatternY1, PatternX2, PatternY2))), filter);
					break;

				case 1:
					var bilinear = new Bilinear(quad, PatternX1, PatternY1, PatternX2, PatternY2);
					if (bilinear.is_valid())
					{
						spanGenerator = new span_image_filter_rgb_2x2(accessor, new span_interpolator_linear(ToPattern(bilinear)), filter);
					}

					break;

				default:
					var perspective = new Perspective(quad, PatternX1, PatternY1, PatternX2, PatternY2);
					if (perspective.is_valid())
					{
						spanGenerator = new span_image_filter_rgb_2x2(accessor, new span_interpolator_linear_subdiv(ToPattern(perspective)), filter);
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

		// The GPU's draw: the reference's 2x2 hanning, mapping and reflect wrap through IImageFilterGraphics, over a
		// 32 bit copy of the picture.
		private void FillPattern(Graphics2D graphics, IImageFilterGraphics filtered, double[] quad)
		{
			double[] frameQuad = QuadImageFill.ToFrame(quad, graphics.GetTransform());
			ImageFilterFill fill = this.TransTypeRbox.CurrentItem switch
			{
				0 => QuadImageFill.Parallelogram(frameQuad, PatternX1, PatternY1, PatternX2, PatternY2),
				1 => QuadImageFill.Bilinear(frameQuad, PatternX1, PatternY1, PatternX2, PatternY2),
				_ => QuadImageFill.Perspective(frameQuad, PatternX1, PatternY1, PatternX2, PatternY2),
			};

			// C++ draws nothing but the quad and the rbox while the corners make the mapping singular.
			if (fill == null)
			{
				return;
			}

			fill.Kind = ImageFilterKind.Filter;
			fill.Filter = new ImageFilterLookUpTable(new image_filter_hanning(), true);
			fill.Edge = ImageFilterEdge.Reflect;
			this.pattern32 ??= QuadImageFill.To32Bit(this.pattern);
			filtered.FillPathWithFilteredImage(QuadImageFill.Outline(quad), this.pattern32, fill);
		}

		// Frame pixel -> demo space (the graphics transform undone) -> pattern space.
		private sealed class FrameToPattern : ITransform
		{
			private readonly Affine frameToDemo;

			private readonly ITransform demoToPattern;

			public FrameToPattern(Affine demoToFrame, ITransform demoToPattern)
			{
				this.frameToDemo = new Affine(demoToFrame);
				this.frameToDemo.invert();
				this.demoToPattern = demoToPattern;
			}

			public void Transform(ref double x, ref double y)
			{
				this.frameToDemo.Transform(ref x, ref y);
				this.demoToPattern.Transform(ref x, ref y);
			}
		}
	}
}
