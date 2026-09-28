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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's bspline.cpp: a B-spline (conv_bspline) through six draggable points. Drag a point, an edge or
	/// the inside of the polygon; space flips the polygon upside down.
	/// </summary>
	public class BSplineDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// C++ m_flip, toggled by the space key.
		private bool flip;

		public BSplineDemo()
		{
			// bspline.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.NumPointsSlider = new SliderCtrl(5.0, 5.0, 340.0, 12.0, false)
			{
				Label = "Number of intermediate Points = {0:F3}",
			};
			this.NumPointsSlider.SetRange(1.0, 40.0);
			this.NumPointsSlider.Value = 20.0;

			this.CloseCbox = new CboxCtrl(350, 5.0, "Close");

			// C++ add_ctrl order, which is also the order they are drawn in.
			this.ctrls.Add(this.CloseCbox);
			this.ctrls.Add(this.NumPointsSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			this.ResetPolygon();
		}

		/// <summary>C++ <c>m_num_points</c>: spline samples per polygon span.</summary>
		public SliderCtrl NumPointsSlider { get; }

		/// <summary>C++ <c>m_close</c>: closes the spline (the polygon outline is always closed).</summary>
		public CboxCtrl CloseCbox { get; }

		/// <summary>C++ <c>m_poly</c>: the six control points, drawn with their outline and handles.</summary>
		public InteractivePolygon Polygon { get; } = new InteractivePolygon(6, 5.0);

		public override string Name => "bspline";

		public override string Category => "Vector Graphics";

		public override string Description => "A B-spline through six points. Drag a point, an edge or the whole shape; space flips it.";

		public override int Width => 600;

		public override int Height => 600;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			var bspline = new BSplinePath(this.Polygon.ToPath(this.CloseCbox.Checked))
			{
				InterpolationStep = 1.0 / this.NumPointsSlider.Value,
			};
			graphics.Render(new Stroke(bspline, 2.0), Rgba8.FromRgba(0, 0, 0));

			graphics.Render(this.Polygon, Rgba8.FromRgba(0, 0.3, 0.5, 0.6));

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.Polygon.OnMouseButtonDown(x, y))
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
				if (this.Polygon.OnMouseMove(x, y))
				{
					this.Invalidate();
				}
			}
			else if (this.Polygon.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.Polygon.OnMouseButtonUp(x, y))
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
				this.flip = !this.flip;
				this.ResetPolygon();
				this.Invalidate();
			}
		}

		// C++ on_init: a square 100 in from the edges (its first side along the bottom, or along the top when
		// flipped) and two points down the middle.
		private void ResetPolygon()
		{
			double width = this.Width;
			double height = this.Height;
			double bottom = this.flip ? height - 100 : 100;
			double top = this.flip ? 100 : height - 100;
			this.Polygon.SetPoint(0, 100, bottom);
			this.Polygon.SetPoint(1, width - 100, bottom);
			this.Polygon.SetPoint(2, width - 100, top);
			this.Polygon.SetPoint(3, 100, top);
			this.Polygon.SetPoint(4, width / 2, height / 2);
			this.Polygon.SetPoint(5, width / 2, height / 3);
		}
	}
}
