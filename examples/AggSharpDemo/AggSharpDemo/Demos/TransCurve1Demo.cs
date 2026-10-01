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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's trans_curve1.cpp: text bent along a B-spline through six draggable points with
	/// trans_single_path. The text is Liberation Serif through agg-sharp's TrueType engine.
	/// </summary>
	public class TransCurve1Demo : AggDemo
	{
		private static readonly double[] InitialPoints = { 50, 50, 150 + 20, 150 - 20, 250 - 20, 250 + 20, 350 + 20, 350 - 20, 450 - 20, 450 + 20, 550, 550 };

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly InteractivePolygon polygon = new InteractivePolygon(6, 5.0);

		private readonly double[] speedX = new double[6];

		private readonly double[] speedY = new double[6];

		private readonly Random random = new Random();

		private bool previousAnimate;

		public TransCurve1Demo()
		{
			// trans_curve1.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.NumPointsSlider = new SliderCtrl(5.0, 5.0, 340.0, 12.0, false)
			{
				Label = "Number of intermediate Points = {0:F3}",
			};
			this.NumPointsSlider.SetRange(10.0, 400.0);
			this.NumPointsSlider.Value = 200.0;
			this.CloseBox = new CboxCtrl(350, 5.0, "Close", false);
			this.PreserveXScaleBox = new CboxCtrl(460, 5.0, "Preserve X scale", false) { Checked = true };
			this.FixedLengthBox = new CboxCtrl(350, 25.0, "Fixed Length", false) { Checked = true };
			this.AnimateBox = new CboxCtrl(460, 25.0, "Animate", false);

			// C++ render order; the slider is added last there too.
			this.ctrls.Add(this.CloseBox);
			this.ctrls.Add(this.PreserveXScaleBox);
			this.ctrls.Add(this.FixedLengthBox);
			this.ctrls.Add(this.AnimateBox);
			this.ctrls.Add(this.NumPointsSlider);
			this.ctrls.Changed += (s, e) =>
			{
				this.OnCtrlChange();
				this.Invalidate();
			};

			this.ResetPoints();
		}

		public SliderCtrl NumPointsSlider { get; }

		public CboxCtrl CloseBox { get; }

		public CboxCtrl PreserveXScaleBox { get; }

		public CboxCtrl FixedLengthBox { get; }

		public CboxCtrl AnimateBox { get; }

		public override string Name => "trans_curve1";

		/// <summary>
		/// The glyphs the text is drawn with: Liberation Serif at C++'s 40px (upright, curves flattened at
		/// 2.0 as C++ does) unless a test swaps in <see cref="GsvCurveTextFont"/> to match the C++ goldens.
		/// </summary>
		public CurveTextFont TextFont { get; set; } = new TrueTypeCurveTextFont(italic: false, curveApproximationScale: 2.0);

		public override string Category => "Transforms";

		public override string Description => "Text follows a curve through six points. Drag a point, a line or the whole curve.";

		public override int Width => 600;

		public override int Height => 600;

		/// <summary>Moves control point <paramref name="index"/> (0 to 5).</summary>
		public void SetPoint(int index, double x, double y)
		{
			this.polygon.SetPoint(index, x, y);
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			this.polygon.Close = this.CloseBox.Checked;
			var bspline = new BSplinePath(this.polygon.ToPath(this.polygon.Close)) { InterpolationStep = 1.0 / this.NumPointsSlider.Value };

			var curve = new TransSinglePath();
			curve.AddPath(bspline);
			curve.PreserveXScale = this.PreserveXScaleBox.Checked;
			if (this.FixedLengthBox.Checked)
			{
				curve.BaseLength = 1120;
			}

			TextAlongPath.Draw(graphics, curve, curve.TotalLength, this.TextFont);

			graphics.Render(new Stroke(bspline, 2.0), SrgbLut.FromSrgba8(170, 50, 20, 100));

			graphics.Render(this.polygon, Rgba8.FromRgba(0, 0.3, 0.5, 0.3));

			this.ctrls.Render(graphics);
		}

		public override void OnIdle()
		{
			for (int i = 0; i < 6; i++)
			{
				double x = this.polygon.GetPoint(i).X;
				double y = this.polygon.GetPoint(i).Y;
				MovePoint(ref x, ref y, ref this.speedX[i], ref this.speedY[i], this.Width, this.Height);
				this.polygon.SetPoint(i, x, y);
			}

			this.Invalidate();
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.polygon.OnMouseButtonDown(x, y))
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

			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.polygon.OnMouseButtonUp(x, y);
				return;
			}

			if (this.polygon.OnMouseMove(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.polygon.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>C++ move_point: one animation step, bouncing off the window edges.</summary>
		internal static void MovePoint(ref double x, ref double y, ref double dx, ref double dy, double width, double height)
		{
			if (x < 0.0) { x = 0.0; dx = -dx; }
			if (x > width) { x = width; dx = -dx; }
			if (y < 0.0) { y = 0.0; dy = -dy; }
			if (y > height) { y = height; dy = -dy; }
			x += dx;
			y += dy;
		}

		/// <summary>C++ ((rand() % 1000) - 500) * 0.01: a speed of up to 5 pixels a frame either way.</summary>
		internal static double RandomSpeed(Random random) => (random.Next(1000) - 500) * 0.01;

		// C++ on_ctrl_change: turning Animate on restarts from the initial points with random speeds.
		private void OnCtrlChange()
		{
			if (this.AnimateBox.Checked == this.previousAnimate)
			{
				return;
			}

			if (this.AnimateBox.Checked)
			{
				this.ResetPoints();
				for (int i = 0; i < 6; i++)
				{
					this.speedX[i] = RandomSpeed(this.random);
					this.speedY[i] = RandomSpeed(this.random);
				}
			}

			this.WaitMode = !this.AnimateBox.Checked;
			this.previousAnimate = this.AnimateBox.Checked;
		}

		// C++ on_init
		private void ResetPoints()
		{
			for (int i = 0; i < 6; i++)
			{
				this.polygon.SetPoint(i, InitialPoints[i * 2], InitialPoints[(i * 2) + 1]);
			}
		}
	}
}
