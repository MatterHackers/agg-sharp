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
	/// C++ AGG's compositing2.cpp: a large radial gradient drawn with difference, then four smaller ones drawn with
	/// the chosen SVG compositing operator (<see cref="BlenderCompOpBGRA"/>). The sliders set each group's alpha.
	/// </summary>
	/// <remarks>
	/// On the GPU each circle is one fill with an image of its gradient (<see cref="FillGradientCircle"/>), drawn
	/// through its operator (<see cref="ICompOpGraphics"/>) one circle per call: a call composites its draws as one layer, which is what one C++ span pass is, and the
	/// circles overlap, so each has to meet what the ones before it left. Where the GPU cannot read the destination
	/// (a swapchain without copies) they are drawn source-over.
	/// </remarks>
	public class Compositing2Demo : AggDemo
	{
		private static readonly string[] OperatorNames =
		{
			"clear", "src", "dst", "src-over", "dst-over", "src-in", "dst-in", "src-out", "dst-out", "src-atop",
			"dst-atop", "xor", "plus", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge",
			"color-burn", "hard-light", "soft-light", "difference", "exclusion",
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		/// <summary>The GPU path's gradient images, with the slider alpha each was made at, kept across frames.</summary>
		private (double Alpha, ImageBuffer Image) largeGradient;
		private (double Alpha, ImageBuffer Image) smallGradient;

		public Compositing2Demo()
		{
			// compositing2.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.DstAlphaSlider = new SliderCtrl(5, 5, 400, 11, false) { Label = "Dst Alpha={0:F2}" };
			this.DstAlphaSlider.Value = 1.0;

			this.SrcAlphaSlider = new SliderCtrl(5, 5 + 15, 400, 11 + 15, false) { Label = "Src Alpha={0:F2}" };
			this.SrcAlphaSlider.Value = 1.0;

			this.OperatorRbox = new RboxCtrl(420, 5.0, 420 + 170.0, 340.0, false);
			this.OperatorRbox.SetTextSize(6.8);
			foreach (string name in OperatorNames)
			{
				this.OperatorRbox.AddItem(name);
			}

			this.OperatorRbox.CurrentItem = (int)CompOp.SrcOver;

			this.ctrls.Add(this.DstAlphaSlider);
			this.ctrls.Add(this.SrcAlphaSlider);
			this.ctrls.Add(this.OperatorRbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_alpha_dst</c>: the alpha of the large circle's ramp.</summary>
		public SliderCtrl DstAlphaSlider { get; }

		/// <summary>C++ <c>m_alpha_src</c>: the alpha of the four small circles' ramp.</summary>
		public SliderCtrl SrcAlphaSlider { get; }

		/// <summary>C++ <c>m_comp_op</c>: the operator the small circles are drawn with, in <see cref="CompOp"/> order.</summary>
		public RboxCtrl OperatorRbox { get; }

		public override string Name => "compositing2";

		public override string Category => "Compositing";

		public override string Description => "Radial gradients combined with the SVG compositing operators. Pick an operator; the sliders set the source and destination alpha.";

		public override int Width => 600;

		public override int Height => 400;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			Color[] ramp1 = ColorRamp(this.DstAlphaSlider.Value);
			Color[] ramp2 = ColorRamp(this.SrcAlphaSlider.Value);
			const double cx = 50;
			const double cy = 50;
			var circles = new (double X1, double Y1, double X2, double Y2)[]
			{
				(cx + 120 - 70, cy + 120 - 70, cx + 120 + 70, cy + 120 + 70),
				(cx + 200 - 70, cy + 120 - 70, cx + 200 + 70, cy + 120 + 70),
				(cx + 120 - 70, cy + 200 - 70, cx + 120 + 70, cy + 200 + 70),
				(cx + 200 - 70, cy + 200 - 70, cx + 200 + 70, cy + 200 + 70),
			};

			if (graphics.Rasterizer != null && graphics.DestImage is ImageClippingProxy frame && frame.BitDepth == 32)
			{
				// C++ draws the circles through pixfmt_custom_blend_rgba<comp_op_adaptor_rgba>: the frame's blender is
				// swapped for the compositing one for the length of the circles.
				Affine transform = graphics.GetTransform();
				IRecieveBlenderByte previousBlender = frame.GetRecieveBlender();
				var blender = new BlenderCompOpBGRA(CompOp.Difference);
				frame.SetRecieveBlender(blender);
				try
				{
					RadialShape(frame, transform, ramp1, 50, 50, 50 + 320, 50 + 320);
					blender.Operator = (CompOp)this.OperatorRbox.CurrentItem;
					foreach (var c in circles)
					{
						RadialShape(frame, transform, ramp2, c.X1, c.Y1, c.X2, c.Y2);
					}
				}
				finally
				{
					frame.SetRecieveBlender(previousBlender);
				}

				frame.MarkImageChanged();
			}
			else
			{
				var large = this.GpuGradient(ref this.largeGradient, this.DstAlphaSlider.Value, ramp1, 160);
				var small = this.GpuGradient(ref this.smallGradient, this.SrcAlphaSlider.Value, ramp2, 70);
				DrawThrough(graphics, CompOp.Difference, () => FillGradientCircle(graphics, large, 50, 50, 50 + 320, 50 + 320));
				var op = (CompOp)this.OperatorRbox.CurrentItem;
				foreach (var c in circles)
				{
					DrawThrough(graphics, op, () => FillGradientCircle(graphics, small, c.X1, c.Y1, c.X2, c.Y2));
				}
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

		/// <summary>
		/// C++ generate_color_ramp: black, blue, green (all at <paramref name="alpha"/>), then transparent red, in
		/// three runs of 85. The stops are C++ <c>rgba</c>, so each entry is the double-precision rgba::gradient
		/// converted to rgba8 - not rgba8::gradient, whose rounded integer alpha differs below full alpha.
		/// </summary>
		private static Color[] ColorRamp(double alpha)
		{
			double[][] stops =
			{
				new[] { 0.0, 0.0, 0.0, alpha },
				new[] { 0.0, 0.0, 1.0, alpha },
				new[] { 0.0, 1.0, 0.0, alpha },
				new[] { 1.0, 0.0, 0.0, 0.0 },
			};

			var ramp = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				int run = i < 85 ? 0 : (i < 170 ? 1 : 2);
				double k = (i - (run * 85)) / 85.0;
				double[] from = stops[run];
				double[] to = stops[run + 1];
				ramp[i] = Rgba8.FromRgba(
					from[0] + ((to[0] - from[0]) * k),
					from[1] + ((to[1] - from[1]) * k),
					from[2] + ((to[2] - from[2]) * k),
					from[3] + ((to[3] - from[3]) * k));
			}

			return ramp;
		}

		/// <summary>
		/// C++ radial_shape: a circle in the box x1,y1 - x2,y2 filled with <paramref name="ramp"/> as a radial gradient
		/// (0 at the center, 100 at the rim), rendered straight into <paramref name="frame"/> through its blender.
		/// </summary>
		private static void RadialShape(IImageByte frame, Affine transform, Color[] ramp, double x1, double y1, double x2, double y2)
		{
			double cx = (x1 + x2) / 2.0;
			double cy = (y1 + y2) / 2.0;
			double r = 0.5 * ((x2 - x1) < (y2 - y1) ? (x2 - x1) : (y2 - y1));

			Affine gradientMatrix = Affine.NewIdentity();
			gradientMatrix *= Affine.NewScaling(r / 100.0);
			gradientMatrix *= Affine.NewTranslation(cx, cy);
			gradientMatrix *= transform;
			gradientMatrix.invert();

			// C++ gradient_radial is int(fast_sqrt(x*x + y*y)), as agg-sharp's gradient_radial and gradient_circle are.
			var spanGradient = new span_gradient(new span_interpolator_linear(gradientMatrix), new gradient_radial(), new RampColors(ramp), 0, 100);

			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(new VertexSourceApplyTransform(new Ellipse(cx, cy, r, r, 100), transform));
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), frame, new span_allocator(), spanGradient);
		}

		private ImageBuffer GpuGradient(ref (double Alpha, ImageBuffer Image) cached, double alpha, Color[] ramp, double r)
		{
			if (cached.Image == null || cached.Alpha != alpha)
			{
				cached = (alpha, GradientImage(ramp, r));
			}

			return cached.Image;
		}

		private static void DrawThrough(Graphics2D graphics, CompOp op, Action draw)
		{
			if (graphics is ICompOpGraphics compositing && compositing.SupportsCompOp(op))
			{
				compositing.DrawWithCompOp(op, draw);
			}
			else
			{
				draw();
			}
		}

		/// <summary>
		/// The GPU stand-in for <see cref="RadialShape"/>: the circle filled, as one shape, with
		/// <paramref name="gradient"/> (<see cref="GradientImage"/>) laid over its bounding square. One fill, so a
		/// compositing operator sees exact coverage everywhere inside, with no seams between pieces.
		/// </summary>
		public static void FillGradientCircle(Graphics2D graphics, ImageBuffer gradient, double x1, double y1, double x2, double y2)
		{
			double cx = (x1 + x2) / 2.0;
			double cy = (y1 + y2) / 2.0;
			double r = 0.5 * ((x2 - x1) < (y2 - y1) ? (x2 - x1) : (y2 - y1));
			var circle = new Ellipse(cx, cy, r, r, 100);
			if (graphics is IPatternFillGraphics patterns)
			{
				patterns.FillPathWithImage(circle, gradient, Affine.NewTranslation(cx - r, cy - r) * graphics.GetTransform(), ImageWrapMode.Repeat, ImageWrapMode.Repeat);
			}
			else
			{
				graphics.Render(circle, gradient.GetPixel(gradient.Width / 2, gradient.Height / 4));
			}
		}

		/// <summary>
		/// The radial gradient <see cref="RadialShape"/> spans, as a 2r square of straight-alpha pixels (row 0 at the
		/// bottom): each pixel the ramp color at its centre's distance from the middle, 0 to 255 over 0 to r.
		/// </summary>
		public static ImageBuffer GradientImage(Color[] ramp, double r)
		{
			int size = Math.Max(1, (int)Math.Ceiling(2 * r));
			var image = new ImageBuffer(size, size);
			byte[] buffer = image.GetBuffer();
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					double dx = x + 0.5 - r;
					double dy = y + 0.5 - r;
					Color color = ramp[Math.Clamp((int)(Math.Sqrt((dx * dx) + (dy * dy)) * 256 / r), 0, ramp.Length - 1)];
					int offset = image.GetBufferOffsetXY(x, y);
					buffer[offset + ImageBuffer.OrderR] = color.red;
					buffer[offset + ImageBuffer.OrderG] = color.green;
					buffer[offset + ImageBuffer.OrderB] = color.blue;
					buffer[offset + ImageBuffer.OrderA] = color.alpha;
				}
			}

			image.MarkImageChanged();
			return image;
		}

		private class RampColors : IColorFunction
		{
			private readonly Color[] colors;

			public RampColors(Color[] colors)
			{
				this.colors = colors;
			}

			public Color this[int v] => this.colors[v];

			public int size() => this.colors.Length;
		}
	}
}
