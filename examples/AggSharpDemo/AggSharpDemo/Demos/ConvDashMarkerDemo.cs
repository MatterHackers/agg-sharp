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

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's conv_dash_marker.cpp: a triangle-and-fan path filled, smoothed (conv_smooth_poly1), then
	/// dashed and stroked with arrowheads at its ends. Drag a vertex, or inside the triangle to move it all.
	/// </summary>
	public class ConvDashMarkerDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly double[] vertexX = { 57 + 100, 369 + 100, 143 + 100 };

		private readonly double[] vertexY = { 60, 170, 310 };

		private double dragDx;

		private double dragDy;

		// 0-2 drags that vertex, 3 drags the whole triangle, -1 is no drag.
		private int dragIndex = -1;

		public ConvDashMarkerDemo()
		{
			// conv_dash_marker.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.CapRbox = new RboxCtrl(10.0, 10.0, 130.0, 80.0, false);
			this.CapRbox.AddItem("Butt Cap");
			this.CapRbox.AddItem("Square Cap");
			this.CapRbox.AddItem("Round Cap");
			this.CapRbox.CurrentItem = 0;

			this.WidthSlider = new SliderCtrl(130 + 10.0, 10.0 + 4.0, 130 + 150.0, 10.0 + 8.0 + 4.0, false)
			{
				Label = "Width={0:F2}",
			};
			this.WidthSlider.SetRange(0.0, 10.0);
			this.WidthSlider.Value = 3.0;

			this.SmoothSlider = new SliderCtrl(130 + 150.0 + 10.0, 10.0 + 4.0, 500 - 10.0, 10.0 + 8.0 + 4.0, false)
			{
				Label = "Smooth={0:F2}",
			};
			this.SmoothSlider.SetRange(0.0, 2.0);
			this.SmoothSlider.Value = 1.0;

			this.CloseCbox = new CboxCtrl(130 + 10.0, 10.0 + 4.0 + 16.0, "Close Polygons");
			this.EvenOddCbox = new CboxCtrl(130 + 150.0 + 10.0, 10.0 + 4.0 + 16.0, "Even-Odd Fill");

			this.ctrls.Add(this.CapRbox);
			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.SmoothSlider);
			this.ctrls.Add(this.CloseCbox);
			this.ctrls.Add(this.EvenOddCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_cap</c>: butt, square or round caps on the dashes.</summary>
		public RboxCtrl CapRbox { get; }

		/// <summary>C++ <c>m_width</c>: the dash stroke width, which also sizes the arrowheads.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_smooth</c>: conv_smooth_poly1's smooth value.</summary>
		public SliderCtrl SmoothSlider { get; }

		/// <summary>C++ <c>m_close</c>: closes both polygons (and drops the tail arrowhead).</summary>
		public CboxCtrl CloseCbox { get; }

		/// <summary>C++ <c>m_even_odd</c>: fills with the even-odd rule instead of non-zero.</summary>
		public CboxCtrl EvenOddCbox { get; }

		public override string Name => "conv_dash_marker";

		public override string Category => "Paths & Strokes";

		public override string Description => "Dashes and arrowheads along a smoothed path. Drag a corner or the whole triangle.";

		public override int Width => 500;

		public override int Height => 330;

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

			bool close = this.CloseCbox.Checked;
			double[] x = this.vertexX;
			double[] y = this.vertexY;
			var path = new VertexStorage();
			path.MoveTo(x[0], y[0]);
			path.LineTo(x[1], y[1]);
			path.LineTo((x[0] + x[1] + x[2]) / 3.0, (y[0] + y[1] + y[2]) / 3.0);
			path.LineTo(x[2], y[2]);
			if (close)
			{
				path.ClosePolygon();
			}

			path.MoveTo((x[0] + x[1]) / 2, (y[0] + y[1]) / 2);
			path.LineTo((x[1] + x[2]) / 2, (y[1] + y[2]) / 2);
			path.LineTo((x[2] + x[0]) / 2, (y[2] + y[0]) / 2);
			if (close)
			{
				path.ClosePolygon();
			}

			// The software surface takes its fill rule on its rasterizer, the GPU on the graphics itself.
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			var gpuFillRule = graphics as IFillRuleGraphics;
			if (this.EvenOddCbox.Checked)
			{
				rasterizer?.filling_rule(Util.filling_rule_e.fill_even_odd);
				if (gpuFillRule != null)
				{
					gpuFillRule.FillingRule = Util.filling_rule_e.fill_even_odd;
				}
			}

			graphics.Render(path, Rgba8.FromRgba(0.7, 0.5, 0.1, 0.5));

			// The smoothed path goes to the rasterizer as it comes: its curve control points are plain vertices.
			var smooth = new SmoothPolygon(path) { SmoothValue = this.SmoothSlider.Value };
			graphics.Render(smooth, Rgba8.FromRgba(0.1, 0.5, 0.7, 0.1));
			graphics.Render(new Stroke(smooth), Rgba8.FromRgba(0.0, 0.6, 0.0, 0.8));

			// The dash follows conv_curve<conv_smooth_poly1>: the same smoothing, flattened.
			var markers = new TerminalMarkers();
			var dash = new Dash(new SmoothPolygonCurve(path) { SmoothValue = this.SmoothSlider.Value }, markers);
			var stroke = new Stroke(dash, this.WidthSlider.Value)
			{
				LineCap = this.CapRbox.CurrentItem == 1 ? LineCap.Square : this.CapRbox.CurrentItem == 2 ? LineCap.Round : LineCap.Butt,
			};

			double k = Math.Pow(this.WidthSlider.Value, 0.7);
			var arrowhead = new Arrowhead();
			arrowhead.Head(4 * k, 4 * k, 3 * k, 2 * k);
			if (!close)
			{
				arrowhead.Tail(1 * k, 1.5 * k, 3 * k, 5 * k);
			}

			dash.AddDash(20.0, 5.0);
			dash.AddDash(5.0, 5.0);
			dash.AddDash(5.0, 5.0);
			dash.DashStart(10);

			// C++ adds the stroke and the arrows to one rasterizer pass, so where they overlap the black is not
			// blended twice. The arrows read the markers the stroke's pass over the dash collected, so the
			// stroke must be read first.
			var strokeAndArrows = new VertexStorage();
			strokeAndArrows.AddRangeDroppingTrailingStops(stroke.Vertices());
			strokeAndArrows.AddRangeDroppingTrailingStops(new MarkerPlacer(markers, arrowhead).Vertices());
			graphics.Render(strokeAndArrows, Rgba8.FromRgba(0.0, 0.0, 0.0));

			rasterizer?.filling_rule(Util.filling_rule_e.fill_non_zero);
			if (gpuFillRule != null)
			{
				gpuFillRule.FillingRule = Util.filling_rule_e.fill_non_zero;
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
				if (Math.Sqrt((dx * dx) + (dy * dy)) < 20.0)
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

		/// <summary>
		/// Arrow keys the current ctrl does not take nudge the first two vertices 0.1 pixel, as C++'s on_key does
		/// (it leaves the third where it is).
		/// </summary>
		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			double dx = key == Keys.Left ? -0.1 : key == Keys.Right ? 0.1 : 0;
			double dy = key == Keys.Up ? 0.1 : key == Keys.Down ? -0.1 : 0;
			if (dx != 0 || dy != 0)
			{
				for (int i = 0; i < 2; i++)
				{
					this.vertexX[i] += dx;
					this.vertexY[i] += dy;
				}

				this.Invalidate();
			}
		}
	}
}
