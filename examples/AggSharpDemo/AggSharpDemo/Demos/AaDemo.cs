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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's aa_demo.cpp: a triangle rasterized at 1/pixel-size scale and shown enlarged, one square per
	/// covered pixel with that pixel's coverage as its alpha, under the real triangle's outline. Drag a
	/// vertex, or inside the triangle to move it all.
	/// </summary>
	public class AaDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly double[] vertexX = { 57, 369, 143 };

		private readonly double[] vertexY = { 100, 170, 310 };

		private double dragDx;

		private double dragDy;

		// 0-2 drags that vertex, 3 drags the whole triangle, -1 is no drag.
		private int dragIndex = -1;

		public AaDemo()
		{
			// aa_demo.cpp runs with flip_y = true and gives its slider !flip_y.
			this.PixelSizeSlider = new SliderCtrl(80, 10, 600 - 10, 19, false)
			{
				Label = "Pixel size={0:F0}",
				NumSteps = 23,
			};
			this.PixelSizeSlider.SetRange(8.0, 100.0);
			this.PixelSizeSlider.Value = 32.0;

			this.ctrls.Add(this.PixelSizeSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_slider1</c>: how many screen pixels wide each enlarged pixel is.</summary>
		public SliderCtrl PixelSizeSlider { get; }

		public override string Name => "aa_demo";

		public override string Category => "Vector Graphics";

		public override string Description => "Each square is one pixel of the small triangle. Drag a corner or the whole triangle.";

		public override int Width => 600;

		public override int Height => 400;

		/// <summary>Moves triangle vertex <paramref name="index"/> (0 to 2) to (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public void SetVertex(int index, double x, double y)
		{
			this.vertexX[index] = x;
			this.vertexY[index] = y;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			int sizeMul = (int)this.PixelSizeSlider.Value;

			var smallTriangle = new VertexStorage();
			smallTriangle.MoveTo(this.vertexX[0] / sizeMul, this.vertexY[0] / sizeMul);
			smallTriangle.LineTo(this.vertexX[1] / sizeMul, this.vertexY[1] / sizeMul);
			smallTriangle.LineTo(this.vertexX[2] / sizeMul, this.vertexY[2] / sizeMul);

			RenderEnlarged(graphics, smallTriangle, sizeMul);

			// C++ then renders the same rasterizer again straight to the window: the triangle at its natural size.
			graphics.Render(smallTriangle, Color.Black);

			Color outlineColor = SrgbLut.FromSrgba8(0, 150, 160, 200);
			for (int i = 0; i < 3; i++)
			{
				int j = (i + 1) % 3;
				var edge = new VertexStorage();
				edge.MoveTo(this.vertexX[i], this.vertexY[i]);
				edge.LineTo(this.vertexX[j], this.vertexY[j]);
				graphics.Render(new Stroke(edge, 2.0), outlineColor);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			for (int i = 0; i < 3; i++)
			{
				double dx = x - this.vertexX[i];
				double dy = y - this.vertexY[i];
				if (Math.Sqrt((dx * dx) + (dy * dy)) < 10.0)
				{
					this.dragDx = dx;
					this.dragDy = dy;
					this.dragIndex = i;
					return;
				}
			}

			if (agg_math.point_in_triangle(this.vertexX[0], this.vertexY[0], this.vertexX[1], this.vertexY[1], this.vertexX[2], this.vertexY[2], x, y))
			{
				this.dragDx = x - this.vertexX[0];
				this.dragDy = y - this.vertexY[0];
				this.dragIndex = 3;
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.dragIndex = -1;
				return;
			}

			if (this.dragIndex == 3)
			{
				double newX = x - this.dragDx;
				double newY = y - this.dragDy;
				for (int i = 1; i < 3; i++)
				{
					this.vertexX[i] -= this.vertexX[0] - newX;
					this.vertexY[i] -= this.vertexY[0] - newY;
				}

				this.SetVertex(0, newX, newY);
			}
			else if (this.dragIndex >= 0)
			{
				this.SetVertex(this.dragIndex, x - this.dragDx, y - this.dragDy);
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.dragIndex = -1;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>
		/// C++ <c>renderer_enlarged</c>: sweeps <paramref name="shape"/>'s scanlines and, for every covered cell,
		/// fills a <paramref name="size"/>-pixel square in black at the cell's coverage.
		/// </summary>
		/// <remarks>
		/// Only the coverage sweep is software; each square goes through <paramref name="graphics"/> so the GPU
		/// path draws the same picture the software reference render does.
		/// </remarks>
		private static void RenderEnlarged(Graphics2D graphics, IVertexSource shape, double size)
		{
			var rasterizer = new ScanlineRasterizer();
			rasterizer.add_path(shape);
			if (!rasterizer.rewind_scanlines())
			{
				return;
			}

			var scanline = new scanline_unpacked_8();
			scanline.reset(rasterizer.min_x(), rasterizer.max_x());
			while (rasterizer.sweep_scanline(scanline))
			{
				int y = scanline.y();
				byte[] covers = scanline.GetCovers();
				int numSpans = scanline.num_spans();
				ScanlineSpan span = scanline.begin();
				for (int s = 0; s < numSpans; s++)
				{
					if (s > 0)
					{
						span = scanline.GetNextScanlineSpan();
					}

					for (int i = 0; i < span.len; i++)
					{
						// C++: a = (cover * m_color.a) >> 8, with m_color black at full alpha.
						int alpha = (covers[span.cover_index + i] * 255) >> 8;
						double x = span.x + i;
						var square = new VertexStorage();
						square.MoveTo(x * size, y * size);
						square.LineTo((x * size) + size, y * size);
						square.LineTo((x * size) + size, (y * size) + size);
						square.LineTo(x * size, (y * size) + size);
						graphics.Render(square, new Color(0, 0, 0, alpha));
					}
				}
			}
		}
	}
}
