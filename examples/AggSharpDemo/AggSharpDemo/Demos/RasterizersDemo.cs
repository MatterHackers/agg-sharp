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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's rasterizers.cpp: the same triangle drawn aliased (left) and anti-aliased (right). The gamma
	/// slider is the aliased triangle's coverage threshold and, doubled, the anti-aliased one's power gamma.
	/// Drag a corner of either triangle, or inside one to move both.
	/// </summary>
	public class RasterizersDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly double[] vertexX = { 100 + 120, 369 + 120, 143 + 120 };

		private readonly double[] vertexY = { 60, 170, 310 };

		private double dragDx;

		private double dragDy;

		// 0-2 drags that corner, 3 drags the whole triangle, -1 is no drag.
		private int dragIndex = -1;

		public RasterizersDemo()
		{
			// rasterizers.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.GammaSlider = new SliderCtrl(130 + 10.0, 10.0 + 4.0, 130 + 150.0, 10.0 + 8.0 + 4.0, false) { Label = "Gamma={0:F2}" };
			this.GammaSlider.SetRange(0.0, 1.0);
			this.GammaSlider.Value = 0.5;

			this.AlphaSlider = new SliderCtrl(130 + 150.0 + 10.0, 10.0 + 4.0, 500 - 10.0, 10.0 + 8.0 + 4.0, false) { Label = "Alpha={0:F2}" };
			this.AlphaSlider.SetRange(0.0, 1.0);
			this.AlphaSlider.Value = 1.0;

			this.TestCbox = new CboxCtrl(130 + 10.0, 10.0 + 4.0 + 16.0, "Test Performance");

			this.ctrls.Add(this.GammaSlider);
			this.ctrls.Add(this.AlphaSlider);
			this.ctrls.Add(this.TestCbox);
			this.ctrls.Changed += (s, e) =>
			{
				// C++ on_ctrl_change times 1000 draws of each triangle into a message box and clears the box
				// again. The timings are not part of the image, so the port only clears the box.
				this.TestCbox.Checked = false;
				this.Invalidate();
			};
		}

		/// <summary>C++ <c>m_gamma</c>: the aliased coverage threshold; twice it is the anti-aliased power gamma.</summary>
		public SliderCtrl GammaSlider { get; }

		/// <summary>C++ <c>m_alpha</c>: the opacity of both triangles.</summary>
		public SliderCtrl AlphaSlider { get; }

		/// <summary>C++ <c>m_test</c>: "Test Performance", which the port unchecks at once.</summary>
		public CboxCtrl TestCbox { get; }

		public override string Name => "rasterizers";

		public override string Category => "Vector Graphics";

		public override string Description => "One triangle aliased and anti-aliased side by side. Change the gamma and alpha, or drag a triangle.";

		public override int Width => 500;

		public override int Height => 330;

		/// <summary>Moves corner <paramref name="index"/> (0 to 2) of the anti-aliased triangle; the aliased one follows 200 to its left.</summary>
		public void SetVertex(int index, double x, double y)
		{
			this.vertexX[index] = x;
			this.vertexY[index] = y;
			this.Invalidate();
		}

		/// <summary>Corner <paramref name="index"/> (0 to 2) of the anti-aliased triangle.</summary>
		public Vector2 GetVertex(int index) => new Vector2(this.vertexX[index], this.vertexY[index]);

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			double alpha = this.AlphaSlider.Value;
			double gamma = this.GammaSlider.Value;

			// C++ draw_anti_aliased, then draw_aliased, each through its own gamma (GammaFill: the software
			// rasterizer's, or the GPU's coverage gamma).
			this.DrawTriangle(graphics, 0, Rgba8.FromRgba(0.7, 0.5, 0.1, alpha), new gamma_power(gamma * 2.0));

			// C++ renders this one through scanline_bin and renderer_scanline_bin_solid. A threshold gamma
			// leaves every cell's coverage 0 or 255, so the anti-aliased scanline fills exactly the same pixels
			// at full cover - the same bytes.
			this.DrawTriangle(graphics, -200, Rgba8.FromRgba(0.1, 0.5, 0.7, alpha), new gamma_threshold(gamma));

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
				if (Math.Sqrt((dx * dx) + (dy * dy)) < 20.0 || Math.Sqrt(((dx + 200) * (dx + 200)) + (dy * dy)) < 20)
				{
					this.dragDx = dx;
					this.dragDy = dy;
					this.dragIndex = i;
					return;
				}
			}

			if (agg_math.point_in_triangle(this.vertexX[0], this.vertexY[0], this.vertexX[1], this.vertexY[1], this.vertexX[2], this.vertexY[2], x, y)
				|| agg_math.point_in_triangle(this.vertexX[0] - 200, this.vertexY[0], this.vertexX[1] - 200, this.vertexY[1], this.vertexX[2] - 200, this.vertexY[2], x, y))
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
				// Grabbed on the aliased copy, the offset is 200 more, and the corner still lands under the pointer.
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
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			// C++ on_key nudges the first two corners by a tenth of a pixel (the third stays put).
			double dx = key == Keys.Left ? -0.1 : key == Keys.Right ? 0.1 : 0;
			double dy = key == Keys.Up ? 0.1 : key == Keys.Down ? -0.1 : 0;
			for (int i = 0; i < 2; i++)
			{
				this.vertexX[i] += dx;
				this.vertexY[i] += dy;
			}

			this.Invalidate();
		}

		// Fills the triangle offset by offsetX with its coverage through gamma; the fills after it (the ctrls) are unaffected.
		private void DrawTriangle(Graphics2D graphics, double offsetX, Color color, IGammaFunction gamma)
		{
			var path = new VertexStorage();
			path.MoveTo(this.vertexX[0] + offsetX, this.vertexY[0]);
			path.LineTo(this.vertexX[1] + offsetX, this.vertexY[1]);
			path.LineTo(this.vertexX[2] + offsetX, this.vertexY[2]);
			path.ClosePolygon();

			GammaFill.Render(graphics, path, color, gamma);
		}
	}
}
