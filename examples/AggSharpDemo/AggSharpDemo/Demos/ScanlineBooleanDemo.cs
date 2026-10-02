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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's scanline_boolean.cpp: two rings of circles along two draggable quads, combined scanline by
	/// scanline (<see cref="ScanlineBooleanAlgebra"/>) by union, intersection, three kinds of xor or a
	/// difference, the result in black over the two shapes. The sliders scale each shape's coverage.
	/// </summary>
	/// <remarks>
	/// On the GPU the same rasterizers and the same boolean run on the CPU, and their scanlines' pixels are
	/// drawn through a <see cref="Graphics2DSpanImage"/>, so both frames hold the same pixels.
	/// </remarks>
	public class ScanlineBooleanDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public ScanlineBooleanDemo()
		{
			// scanline_boolean.cpp runs with flip_y = true and gives its controls !flip_y.
			this.OperationRbox = new RboxCtrl(420, 5.0, 420 + 130.0, 145.0, false);
			foreach (string item in new[] { "Union", "Intersection", "Linear XOR", "Saddle XOR", "Abs Diff XOR", "A-B", "B-A" })
			{
				this.OperationRbox.AddItem(item);
			}

			this.OperationRbox.CurrentItem = 0;
			this.ResetCbox = new CboxCtrl(350, 5.0, "Reset", false);
			this.Opacity1Slider = new SliderCtrl(5.0, 5.0, 340.0, 12.0, false) { Label = "Opacity1={0:F3}", Value = 1.0 };
			this.Opacity2Slider = new SliderCtrl(5.0, 20.0, 340.0, 27.0, false) { Label = "Opacity2={0:F3}", Value = 1.0 };

			this.ctrls.Add(this.OperationRbox);
			this.ctrls.Add(this.ResetCbox);
			this.ctrls.Add(this.Opacity1Slider);
			this.ctrls.Add(this.Opacity2Slider);
			this.ctrls.Changed += (s, e) =>
			{
				if (this.ResetCbox.Checked)
				{
					this.PlaceQuads();
					this.ResetCbox.Checked = false;
				}

				this.Invalidate();
			};

			this.PlaceQuads();
		}

		/// <summary>C++ <c>m_trans_type</c>: its item is the <see cref="SboolOp"/>.</summary>
		public RboxCtrl OperationRbox { get; }

		/// <summary>C++ <c>m_reset</c>: checking it puts the quads back and unchecks it.</summary>
		public CboxCtrl ResetCbox { get; }

		/// <summary>C++ <c>m_mul1</c>: gamma_multiply on the first shape's coverage.</summary>
		public SliderCtrl Opacity1Slider { get; }

		/// <summary>C++ <c>m_mul2</c>: gamma_multiply on the second shape's coverage.</summary>
		public SliderCtrl Opacity2Slider { get; }

		public InteractivePolygon Quad1 { get; } = new InteractivePolygon(4, 5.0);

		public InteractivePolygon Quad2 { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "scanline_boolean";

		public override string Category => "Masks & Clipping";

		public override string Description => "Boolean operations on two rings of circles, done on anti-aliased scanlines. Pick an operation, drag the quads' corners and edges, and set each shape's opacity.";

		public override int Width => 800;

		public override int Height => 600;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			// No clip box: as in C++, ras1 and ras2 are the shapes whole.
			var ras1 = new ScanlineRasterizer();
			var ras2 = new ScanlineRasterizer();
			ras1.gamma(new gamma_multiply(this.Opacity1Slider.Value));
			ras2.gamma(new gamma_multiply(this.Opacity2Slider.Value));
			ras1.filling_rule(Util.filling_rule_e.fill_even_odd);
			AddCircles(ras1, this.Quad1);
			AddCircles(ras2, this.Quad2);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			// Any other surface takes the same scanlines' exact pixels through a Graphics2DSpanImage.
			IImageByte destination = graphics.Rasterizer != null ? graphics.DestImage as IImageByte : null;
			Graphics2DSpanImage spans = destination == null ? new Graphics2DSpanImage(graphics, this.Width, this.Height) : null;
			IImageByte target = destination ?? new ImageClippingProxy(spans);
			IScanlineSink shape1 = new SolidScanlineSink(target, SrgbLut.FromSrgba8(240, 255, 200, 100));
			IScanlineSink shape2 = new SolidScanlineSink(target, SrgbLut.FromSrgba8(255, 240, 240, 100));
			IScanlineSink result = new SolidScanlineSink(target, SrgbLut.FromSrgba8(0, 0, 0));

			Sweep(ras1, shape1);
			Sweep(ras2, shape2);
			ScanlineBooleanAlgebra.CombineShapesAa(
				(SboolOp)this.OperationRbox.CurrentItem,
				ras1,
				ras2,
				new ScanlineCachePacked8(),
				new ScanlineCachePacked8(),
				new ScanlineCachePacked8(),
				result);
			destination?.MarkImageChanged();
			spans?.Flush();

			graphics.Render(this.Quad1, Rgba8.FromRgba(0, 0.3, 0.5, 0.6));
			graphics.Render(this.Quad2, Rgba8.FromRgba(0, 0.3, 0.5, 0.6));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.Quad1.OnMouseButtonDown(x, y) || this.Quad2.OnMouseButtonDown(x, y))
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
				if (this.Quad1.OnMouseMove(x, y) || this.Quad2.OnMouseMove(x, y))
				{
					this.Invalidate();
				}
			}
			else if (this.Quad1.OnMouseButtonUp(x, y) || this.Quad2.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.Quad1.OnMouseButtonUp(x, y) || this.Quad2.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>C++ generate_circles: five circles of radius 20 along each side of the quad, from its corner.</summary>
		private static void AddCircles(ScanlineRasterizer rasterizer, InteractivePolygon quad)
		{
			const int NumCircles = 5;
			for (int i = 0; i < 4; i++)
			{
				var from = quad.GetPoint(i);
				var to = quad.GetPoint((i + 1) % 4);
				for (int j = 0; j < NumCircles; j++)
				{
					rasterizer.add_path(new Ellipse(
						from.X + ((to.X - from.X) * j / NumCircles),
						from.Y + ((to.Y - from.Y) * j / NumCircles),
						20,
						20,
						100));
				}
			}
		}

		/// <summary>C++ render_scanlines: every scanline the rasterizer makes, into the sink.</summary>
		private static void Sweep(ScanlineRasterizer rasterizer, IScanlineSink sink)
		{
			var scanline = new ScanlineCachePacked8();
			if (rasterizer.rewind_scanlines())
			{
				scanline.reset(rasterizer.min_x(), rasterizer.max_x());
				sink.prepare();
				while (rasterizer.sweep_scanline(scanline))
				{
					sink.render(scanline);
				}
			}
		}

		/// <summary>scanline_boolean.cpp's on_init: the two quads side by side, each a little skewed.</summary>
		private void PlaceQuads()
		{
			double w = this.Width;
			double h = this.Height;
			this.Quad1.SetPoint(0, 50, 200 - 20);
			this.Quad1.SetPoint(1, (w / 2) - 25, 200);
			this.Quad1.SetPoint(2, (w / 2) - 25, h - 50 - 20);
			this.Quad1.SetPoint(3, 50, h - 50);

			this.Quad2.SetPoint(0, (w / 2) + 25, 200 - 20);
			this.Quad2.SetPoint(1, w - 50, 200);
			this.Quad2.SetPoint(2, w - 50, h - 50 - 20);
			this.Quad2.SetPoint(3, (w / 2) + 25, h - 50);
		}
	}
}
