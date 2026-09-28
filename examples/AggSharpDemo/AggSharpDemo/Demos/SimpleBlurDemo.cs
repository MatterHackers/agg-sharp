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
	/// C++ AGG's simple_blur.cpp: the filled lion and its outline side by side, and a circle that blurs
	/// whatever lies under it with a 3x3 box filter - a span generator reading a copy of the finished frame.
	/// Left-drag moves the circle.
	/// </summary>
	/// <remarks>
	/// The blur reads back pixels already drawn; on the GPU that is <see cref="IBlurGraphics.BlurBox"/> with the same
	/// 3x3 box. The outline lion there is conv_stroke rather than rasterizer_outline_aa, as in lion_outline.
	/// </remarks>
	public class SimpleBlurDemo : AggDemo
	{
		private readonly LionShape lion = new LionShape();

		private double centerX = 100;

		private double centerY = 102;

		public override string Name => "simple_blur";

		public override string Category => "Images";

		public override string Description => "A 3x3 box blur applied through a span generator inside a circle, over the filled and outlined lion. Left-drag to move the circle.";

		public override int Width => 512;

		public override int Height => 400;

		/// <summary>
		/// Whether the circle blurs what lies under it, as C++ always does. Off, the frame is the one the blur
		/// starts from, which is what a test blurs itself to check the GPU's blur.
		/// </summary>
		public bool BlurCircle { get; set; } = true;

		/// <summary>Moves the blur circle, as a left press or drag does.</summary>
		public void SetCircleCenter(double x, double y)
		{
			this.centerX = x;
			this.centerY = y;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			// simple_blur.cpp's lion: no scale or skew, centered on a quarter of the width (integer divisions,
			// as C++'s unsigned initial_width() / 4).
			Affine transform = Affine.NewIdentity();
			transform *= Affine.NewTranslation(-this.lion.Center.X, -this.lion.Center.Y);
			transform *= Affine.NewRotation(Math.PI);
			transform *= Affine.NewTranslation(this.Width / 4, this.Height / 2);

			this.lion.Render(graphics, transform, 255);

			// The outlined copy, half the width to the right.
			Affine outlineTransform = transform * Affine.NewTranslation(this.Width / 2, 0);
			bool software = graphics is ImageGraphics2D && graphics.Rasterizer != null;
			if (software)
			{
				// The outline rasterizer writes pixels itself, so it gets the graphics transform folded in.
				outlineTransform *= graphics.GetTransform();
				var rasterizer = new rasterizer_outline_aa(new OutlineRenderer(new ImageClippingProxy(graphics.DestImage), new LineProfileAnitAlias(1.0, new gamma_none())));
				rasterizer.round_cap(true);
				foreach (var shape in this.lion.Shapes)
				{
					rasterizer.RenderAllPaths(new VertexSourceApplyTransform(shape.VertexStorage, outlineTransform), new[] { shape.Color }, new[] { 0 }, 1);
				}
			}
			else
			{
				foreach (var shape in this.lion.Shapes)
				{
					graphics.Render(new VertexSourceApplyTransform(new Stroke(shape.VertexStorage, 1.0), outlineTransform), shape.Color);
				}
			}

			// The circle's rim: a 6 wide stroke, itself stroked 2 wide, in rgba(0, 0.2, 0).
			var circle = new Ellipse(this.centerX, this.centerY, 100.0, 100.0, 100);
			graphics.Render(new Stroke(new Stroke(circle, 6.0), 2.0), new Color(0, 51, 0));

			if (this.BlurCircle && software && graphics.DestImage is IImageByte destination)
			{
				// copy_window_to_img, then the circle filled with the blur of that copy.
				var frameCopy = new ImageBuffer(destination, destination.GetRecieveBlender());
				var blurRasterizer = new ScanlineRasterizer();
				blurRasterizer.add_path(new VertexSourceApplyTransform(circle, graphics.GetTransform()));
				new ScanlineRenderer().GenerateAndRender(blurRasterizer, new scanline_unpacked_8(), destination, new span_allocator(), new SimpleBlurSpanGenerator(frameCopy));
			}
			else if (this.BlurCircle && !software && graphics is IBlurGraphics blurGraphics)
			{
				blurGraphics.BlurBox(circle, 0, BlurKind.Box3x3);
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (button.HasFlag(AggInputFlags.MouseLeft))
			{
				this.SetCircleCenter(x, y);
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.SetCircleCenter(x, y);
			}
		}

		/// <summary>
		/// simple_blur.cpp's span_simple_blur_rgb24: each pixel becomes the truncated mean of the 3x3 block
		/// around it in the source image.
		/// </summary>
		/// <remarks>
		/// Where the block would leave the image, C++ gives no_color for the top and bottom rows but opaque black
		/// for the left and right columns (its sums stay 0 and it still builds an opaque colour). That black
		/// seam is a bug in the example; here, and in the reference renderer's copy (demo_simple_blur.cpp), the
		/// side columns get no_color too, so they are left as they were, like the top and bottom rows.
		/// </remarks>
		public class SimpleBlurSpanGenerator : ISpanGenerator
		{
			private readonly ImageBuffer source;

			public SimpleBlurSpanGenerator(ImageBuffer source)
			{
				this.source = source;
			}

			public void prepare()
			{
			}

			public void generate(Color[] span, int spanIndex, int x, int y, int len)
			{
				byte[] buffer = this.source.GetBuffer();
				int pixelStep = this.source.GetBytesBetweenPixelsInclusive();
				for (int i = 0; i < len; i++, x++)
				{
					if (y < 1 || y >= this.source.Height - 1 || x < 1 || x >= this.source.Width - 1)
					{
						span[spanIndex + i] = new Color(0, 0, 0, 0);
						continue;
					}

					int r = 0;
					int g = 0;
					int b = 0;
					for (int row = y - 1; row <= y + 1; row++)
					{
						int offset = this.source.GetBufferOffsetXY(x - 1, row);
						for (int column = 0; column < 3; column++, offset += pixelStep)
						{
							r += buffer[offset + ImageBuffer.OrderR];
							g += buffer[offset + ImageBuffer.OrderG];
							b += buffer[offset + ImageBuffer.OrderB];
						}
					}

					span[spanIndex + i] = new Color(r / 9, g / 9, b / 9);
				}
			}
		}
	}
}
