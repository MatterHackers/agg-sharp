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
	/// C++ AGG's conv_stroke.cpp: a triangle and the triangle of its midpoints stroked wide with the chosen
	/// join, cap and miter limit, outlined thin, the wide stroke's outline dashed and stroked again, and the
	/// path filled faintly. Drag a corner, or inside the triangle to move it all.
	/// </summary>
	public class ConvStrokeDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly double[] vertexX = { 57 + 100, 369 + 100, 143 + 100 };

		private readonly double[] vertexY = { 60, 170, 310 };

		private double dragDx;

		private double dragDy;

		// 0-2 drags that corner, 3 drags the whole triangle, -1 is no drag.
		private int dragIndex = -1;

		public ConvStrokeDemo()
		{
			// conv_stroke.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.JoinRbox = new RboxCtrl(10.0, 10.0, 133.0, 80.0, false) { TextThickness = 1.0 };
			this.JoinRbox.SetTextSize(7.5);
			this.JoinRbox.AddItem("Miter Join");
			this.JoinRbox.AddItem("Miter Join Revert");
			this.JoinRbox.AddItem("Round Join");
			this.JoinRbox.AddItem("Bevel Join");
			this.JoinRbox.CurrentItem = 2;

			this.CapRbox = new RboxCtrl(10.0, 80.0 + 10.0, 133.0, 80.0 + 80.0, false);
			this.CapRbox.AddItem("Butt Cap");
			this.CapRbox.AddItem("Square Cap");
			this.CapRbox.AddItem("Round Cap");
			this.CapRbox.CurrentItem = 2;

			this.WidthSlider = new SliderCtrl(130 + 10.0, 10.0 + 4.0, 500.0 - 10.0, 10.0 + 8.0 + 4.0, false) { Label = "Width={0:F2}" };
			this.WidthSlider.SetRange(3.0, 40.0);
			this.WidthSlider.Value = 20.0;

			this.MiterLimitSlider = new SliderCtrl(130 + 10.0, 20.0 + 10.0 + 4.0, 500.0 - 10.0, 20.0 + 10.0 + 8.0 + 4.0, false) { Label = "Miter Limit={0:F2}" };
			this.MiterLimitSlider.SetRange(1.0, 10.0);
			this.MiterLimitSlider.Value = 4.0;

			this.ctrls.Add(this.JoinRbox);
			this.ctrls.Add(this.CapRbox);
			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.MiterLimitSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_join</c>: miter, miter revert, round or bevel joins.</summary>
		public RboxCtrl JoinRbox { get; }

		/// <summary>C++ <c>m_cap</c>: butt, square or round caps.</summary>
		public RboxCtrl CapRbox { get; }

		/// <summary>C++ <c>m_width</c>: the wide stroke's width; a fifth of it is the dashed stroke's.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_miter_limit</c>: the wide stroke's miter limit.</summary>
		public SliderCtrl MiterLimitSlider { get; }

		public override string Name => "conv_stroke";

		public override string Category => "Vector Graphics";

		public override string Description => "Line joins, caps and miter limits on a wide stroke. Drag a corner or the whole triangle.";

		public override int Width => 500;

		public override int Height => 330;

		/// <summary>Moves triangle corner <paramref name="index"/> (0 to 2) to (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public void SetVertex(int index, double x, double y)
		{
			this.vertexX[index] = x;
			this.vertexY[index] = y;
			this.Invalidate();
		}

		/// <summary>Triangle corner <paramref name="index"/> (0 to 2).</summary>
		public Vector2 GetVertex(int index) => new Vector2(this.vertexX[index], this.vertexY[index]);

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			double[] x = this.vertexX;
			double[] y = this.vertexY;
			var path = new VertexStorage();
			path.MoveTo(x[0], y[0]);

			// C++ adds this midpoint and the repeated last corner only to check the stroker's numerical stability.
			path.LineTo((x[0] + x[1]) / 2, (y[0] + y[1]) / 2);
			path.LineTo(x[1], y[1]);
			path.LineTo(x[2], y[2]);
			path.LineTo(x[2], y[2]);

			path.MoveTo((x[0] + x[1]) / 2, (y[0] + y[1]) / 2);
			path.LineTo((x[1] + x[2]) / 2, (y[1] + y[2]) / 2);
			path.LineTo((x[2] + x[0]) / 2, (y[2] + y[0]) / 2);
			path.ClosePolygon();

			LineCap cap = this.CapRbox.CurrentItem == 1 ? LineCap.Square : this.CapRbox.CurrentItem == 2 ? LineCap.Round : LineCap.Butt;
			LineJoin join = this.JoinRbox.CurrentItem == 1 ? LineJoin.MiterRevert
				: this.JoinRbox.CurrentItem == 2 ? LineJoin.Round
				: this.JoinRbox.CurrentItem == 3 ? LineJoin.Bevel
				: LineJoin.Miter;
			double width = this.WidthSlider.Value;

			var stroke = new Stroke(path, width)
			{
				LineJoin = join,
				LineCap = cap,
				MiterLimit = this.MiterLimitSlider.Value,
			};
			graphics.Render(stroke, Rgba8.FromRgba(0.8, 0.7, 0.6));

			graphics.Render(new Stroke(path, 1.5), Rgba8.FromRgba(0, 0, 0));

			var dash = new Dash(stroke);
			dash.AddDash(20.0, width / 2.5);
			var dashStroke = new Stroke(dash, width / 5.0)
			{
				MiterLimit = 4.0,
				LineCap = cap,
				LineJoin = join,
			};
			graphics.Render(dashStroke, Rgba8.FromRgba(0, 0, 0.3));

			graphics.Render(path, Rgba8.FromRgba(0.0, 0.0, 0.0, 0.2));

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

		/// <summary>Arrow keys the current ctrl does not take nudge the first two corners 0.1 pixel, as C++'s on_key does.</summary>
		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			double dx = key == Keys.Left ? -0.1 : key == Keys.Right ? 0.1 : 0;
			double dy = key == Keys.Up ? 0.1 : key == Keys.Down ? -0.1 : 0;
			for (int i = 0; i < 2; i++)
			{
				this.vertexX[i] += dx;
				this.vertexY[i] += dy;
			}

			this.Invalidate();
		}
	}
}
