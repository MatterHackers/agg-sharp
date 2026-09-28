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
	/// C++ AGG's trans_curve2.cpp: text warped into the band between two B-splines with trans_double_path.
	/// The text is Liberation Serif Italic through agg-sharp's TrueType engine.
	/// </summary>
	public class TransCurve2Demo : AggDemo
	{
		private static readonly double[] InitialPoints = { 50, 50, 150 + 20, 150 - 20, 250 - 20, 250 + 20, 350 + 20, 350 - 20, 450 - 20, 450 + 20, 550, 550 };

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly InteractivePolygon polygon1 = new InteractivePolygon(6, 5.0) { Close = false };

		private readonly InteractivePolygon polygon2 = new InteractivePolygon(6, 5.0) { Close = false };

		private readonly double[] speedX1 = new double[6];

		private readonly double[] speedY1 = new double[6];

		private readonly double[] speedX2 = new double[6];

		private readonly double[] speedY2 = new double[6];

		private readonly Random random = new Random();

		private bool previousAnimate;

		public TransCurve2Demo()
		{
			// trans_curve2.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.NumPointsSlider = new SliderCtrl(5.0, 5.0, 340.0, 12.0, false)
			{
				Label = "Number of intermediate Points = {0:F3}",
			};
			this.NumPointsSlider.SetRange(10.0, 400.0);
			this.NumPointsSlider.Value = 200.0;
			this.FixedLengthBox = new CboxCtrl(350, 5.0, "Fixed Length", false) { Checked = true };
			this.PreserveXScaleBox = new CboxCtrl(465, 5.0, "Preserve X scale", false) { Checked = true };
			this.AnimateBox = new CboxCtrl(350, 25.0, "Animate", false);

			this.ctrls.Add(this.FixedLengthBox);
			this.ctrls.Add(this.PreserveXScaleBox);
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

		public CboxCtrl FixedLengthBox { get; }

		public CboxCtrl PreserveXScaleBox { get; }

		public CboxCtrl AnimateBox { get; }

		public override string Name => "trans_curve2";

		/// <summary>
		/// The glyphs the text is drawn with: Liberation Serif at C++'s 40px (italic, as trans_curve2.cpp, curves flattened at
		/// 5.0 as C++ does) unless a test swaps in <see cref="GsvCurveTextFont"/> to match the C++ goldens.
		/// </summary>
		public CurveTextFont TextFont { get; set; } = new TrueTypeCurveTextFont(italic: true, curveApproximationScale: 5.0);

		public override string Category => "Curves";

		public override string Description => "Text stretched between two curves. Drag their points, lines or whole curves.";

		public override int Width => 600;

		public override int Height => 600;

		/// <summary>Moves point <paramref name="index"/> (0 to 5) of curve 1 (the text's baseline) or curve 2 (its top).</summary>
		public void SetPoint(int curve, int index, double x, double y)
		{
			(curve == 1 ? this.polygon1 : this.polygon2).SetPoint(index, x, y);
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			double step = 1.0 / this.NumPointsSlider.Value;
			var bspline1 = new BSplinePath(this.polygon1.ToPath(false)) { InterpolationStep = step };
			var bspline2 = new BSplinePath(this.polygon2.ToPath(false)) { InterpolationStep = step };

			var curve = new TransDoublePath
			{
				PreserveXScale = this.PreserveXScaleBox.Checked,
				BaseHeight = 30.0,
			};
			if (this.FixedLengthBox.Checked)
			{
				curve.BaseLength = 1140.0;
			}

			curve.AddPaths(bspline1, bspline2);

			TextAlongPath.Draw(graphics, curve, curve.TotalLength1, this.TextFont);

			Color curveColor = SrgbLut.FromSrgba8(170, 50, 20, 100);
			graphics.Render(new Stroke(bspline1, 2.0), curveColor);
			graphics.Render(new Stroke(bspline2, 2.0), curveColor);

			Color handleColor = Rgba8.FromRgba(0, 0.3, 0.5, 0.2);
			graphics.Render(this.polygon1, handleColor);
			graphics.Render(this.polygon2, handleColor);

			this.ctrls.Render(graphics);
		}

		public override void OnIdle()
		{
			for (int i = 0; i < 6; i++)
			{
				double x1 = this.polygon1.GetPoint(i).X;
				double y1 = this.polygon1.GetPoint(i).Y;
				double x2 = this.polygon2.GetPoint(i).X;
				double y2 = this.polygon2.GetPoint(i).Y;
				TransCurve1Demo.MovePoint(ref x1, ref y1, ref this.speedX1[i], ref this.speedY1[i], this.Width, this.Height);
				TransCurve1Demo.MovePoint(ref x2, ref y2, ref this.speedX2[i], ref this.speedY2[i], this.Width, this.Height);

				// C++ normalize_point: keep the curves' matching points within 28.28 (20 * sqrt(2)) of each other.
				double d = agg_math.CalcDistance(x1, y1, x2, y2);
				if (d > 28.28)
				{
					x2 = x1 + ((x2 - x1) * 28.28 / d);
					y2 = y1 + ((y2 - y1) * 28.28 / d);
				}

				this.polygon1.SetPoint(i, x1, y1);
				this.polygon2.SetPoint(i, x2, y2);
			}

			this.Invalidate();
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			// As in C++ both curves are offered the press, so where they overlap both move.
			bool hit1 = this.polygon1.OnMouseButtonDown(x, y);
			bool hit2 = this.polygon2.OnMouseButtonDown(x, y);
			if (hit1 || hit2)
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
				this.polygon1.OnMouseButtonUp(x, y);
				this.polygon2.OnMouseButtonUp(x, y);
				return;
			}

			bool moved1 = this.polygon1.OnMouseMove(x, y);
			bool moved2 = this.polygon2.OnMouseMove(x, y);
			if (moved1 || moved2)
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			bool released1 = this.polygon1.OnMouseButtonUp(x, y);
			bool released2 = this.polygon2.OnMouseButtonUp(x, y);
			if (released1 || released2)
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

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
					this.speedX1[i] = TransCurve1Demo.RandomSpeed(this.random);
					this.speedY1[i] = TransCurve1Demo.RandomSpeed(this.random);
					this.speedX2[i] = TransCurve1Demo.RandomSpeed(this.random);
					this.speedY2[i] = TransCurve1Demo.RandomSpeed(this.random);
				}
			}

			this.WaitMode = !this.AnimateBox.Checked;
			this.previousAnimate = this.AnimateBox.Checked;
		}

		// C++ on_init: the two curves sit 10 units either side of trans_curve1's.
		private void ResetPoints()
		{
			for (int i = 0; i < 6; i++)
			{
				this.polygon1.SetPoint(i, 10 + InitialPoints[i * 2], -10 + InitialPoints[(i * 2) + 1]);
				this.polygon2.SetPoint(i, -10 + InitialPoints[i * 2], 10 + InitialPoints[(i * 2) + 1]);
			}
		}
	}
}
