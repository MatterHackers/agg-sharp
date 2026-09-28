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
using System.Collections.Generic;
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's bezier_div.cpp: a cubic Bezier flattened by <c>curve4</c>, incrementally or by subdivision, stroked
	/// wide with the chosen joins and cap, its vertices dotted and the stroke's own outline drawn. The text reports
	/// how far the flattening strays from the true curve, in distance and in angle, at five approximation scales.
	/// Drag the curve's points; the case rbox loads awkward curves (cusps, loops, a jaw).
	/// </summary>
	/// <remarks>
	/// C++ also prints "Time=%.2fmks", the flattening timed with the wall clock. That is left out so the frame is
	/// repeatable; the reference (demo_bezier_div.cpp) leaves it out too.
	/// </remarks>
	public class BezierDivDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly Random random = new Random();

		// C++ m_cur_case_type: the case last loaded, so only a change of case moves the curve.
		private int currentCaseType = -1;

		public BezierDivDemo()
		{
			// C++ m_ctrl_color: an srgba8 of rgba(0, 0.3, 0.5, 0.8), handed to a ctrl that draws rgba8.
			this.CurveCtrl = new BezierCtrl { LineColor = SrgbLut.FromRgbaThroughSrgba8(0, 0.3, 0.5, 0.8) };
			this.CurveCtrl.SetCurve(170, 424, 13, 87, 488, 423, 26, 333);

			// bezier_div.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.AngleToleranceSlider = new SliderCtrl(5.0, 5.0, 240.0, 12.0, false) { Label = "Angle Tolerance={0:F0} deg" };
			this.AngleToleranceSlider.SetRange(0, 90);
			this.AngleToleranceSlider.Value = 15;

			this.ApproximationScaleSlider = new SliderCtrl(5.0, 17 + 5.0, 240.0, 17 + 12.0, false) { Label = "Approximation Scale={0:F3}" };
			this.ApproximationScaleSlider.SetRange(0.1, 5);
			this.ApproximationScaleSlider.Value = 1.0;

			this.CuspLimitSlider = new SliderCtrl(5.0, 17 + 17 + 5.0, 240.0, 17 + 17 + 12.0, false) { Label = "Cusp Limit={0:F0} deg" };
			this.CuspLimitSlider.SetRange(0, 90);
			this.CuspLimitSlider.Value = 0;

			this.WidthSlider = new SliderCtrl(245.0, 5.0, 495.0, 12.0, false) { Label = "Width={0:F2}" };
			this.WidthSlider.SetRange(-50, 100);
			this.WidthSlider.Value = 50.0;

			this.ShowPointsBox = new CboxCtrl(250.0, 15 + 5, "Show Points", false) { Checked = true };
			this.ShowOutlineBox = new CboxCtrl(250.0, 30 + 5, "Show Stroke Outline", false) { Checked = true };

			this.CurveTypeRbox = new RboxCtrl(535.0, 5.0, 535.0 + 115.0, 55.0, false);
			this.CurveTypeRbox.AddItem("Incremental");
			this.CurveTypeRbox.AddItem("Subdiv");
			this.CurveTypeRbox.CurrentItem = 1;

			this.CaseTypeRbox = new RboxCtrl(535.0, 60.0, 535.0 + 115.0, 195.0, false) { TextThickness = 1.0 };
			this.CaseTypeRbox.SetTextSize(7);
			foreach (string item in new[] { "Random", "13---24", "Smooth Cusp 1", "Smooth Cusp 2", "Real Cusp 1", "Real Cusp 2", "Fancy Stroke", "Jaw", "Ugly Jaw" })
			{
				this.CaseTypeRbox.AddItem(item);
			}

			this.InnerJoinRbox = new RboxCtrl(535.0, 200.0, 535.0 + 115.0, 290.0, false);
			this.InnerJoinRbox.SetTextSize(8);
			foreach (string item in new[] { "Inner Bevel", "Inner Miter", "Inner Jag", "Inner Round" })
			{
				this.InnerJoinRbox.AddItem(item);
			}

			this.InnerJoinRbox.CurrentItem = 3;

			this.LineJoinRbox = new RboxCtrl(535.0, 295.0, 535.0 + 115.0, 385.0, false);
			this.LineJoinRbox.SetTextSize(8);
			foreach (string item in new[] { "Miter Join", "Miter Revert", "Round Join", "Bevel Join", "Miter Round" })
			{
				this.LineJoinRbox.AddItem(item);
			}

			this.LineJoinRbox.CurrentItem = 1;

			this.LineCapRbox = new RboxCtrl(535.0, 395.0, 535.0 + 115.0, 455.0, false);
			this.LineCapRbox.SetTextSize(8);
			foreach (string item in new[] { "Butt Cap", "Square Cap", "Round Cap" })
			{
				this.LineCapRbox.AddItem(item);
			}

			this.LineCapRbox.CurrentItem = 0;

			this.ctrls.Add(this.CurveCtrl);
			this.ctrls.Add(this.AngleToleranceSlider);
			this.ctrls.Add(this.ApproximationScaleSlider);
			this.ctrls.Add(this.CuspLimitSlider);
			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.ShowPointsBox);
			this.ctrls.Add(this.ShowOutlineBox);
			this.ctrls.Add(this.CurveTypeRbox);
			this.ctrls.Add(this.CaseTypeRbox);
			this.ctrls.Add(this.InnerJoinRbox);
			this.ctrls.Add(this.LineJoinRbox);
			this.ctrls.Add(this.LineCapRbox);
			this.ctrls.Changed += (s, e) => this.OnCtrlChange();
		}

		/// <summary>C++ <c>m_curve1</c>: the curve, edited by dragging its four points.</summary>
		public BezierCtrl CurveCtrl { get; }

		/// <summary>C++ <c>m_angle_tolerance</c>, in degrees: how sharply subdivision lets the flattened curve turn.</summary>
		public SliderCtrl AngleToleranceSlider { get; }

		/// <summary>C++ <c>m_approximation_scale</c>: how finely the curve is flattened.</summary>
		public SliderCtrl ApproximationScaleSlider { get; }

		/// <summary>C++ <c>m_cusp_limit</c>, in degrees: subdivision's cusp limit, 0 for none.</summary>
		public SliderCtrl CuspLimitSlider { get; }

		/// <summary>C++ <c>m_width</c>: the stroke width; negative widths stroke the other way round.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_show_points</c>: dot every vertex of the flattened curve.</summary>
		public CboxCtrl ShowPointsBox { get; }

		/// <summary>C++ <c>m_show_outline</c>: stroke the stroke, to show how it is built.</summary>
		public CboxCtrl ShowOutlineBox { get; }

		/// <summary>C++ <c>m_curve_type</c>: incremental or subdivision flattening.</summary>
		public RboxCtrl CurveTypeRbox { get; }

		/// <summary>C++ <c>m_case_type</c>: choosing an item loads that test curve (Random picks one).</summary>
		public RboxCtrl CaseTypeRbox { get; }

		/// <summary>C++ <c>m_inner_join</c>.</summary>
		public RboxCtrl InnerJoinRbox { get; }

		/// <summary>C++ <c>m_line_join</c>.</summary>
		public RboxCtrl LineJoinRbox { get; }

		/// <summary>C++ <c>m_line_cap</c>.</summary>
		public RboxCtrl LineCapRbox { get; }

		public override string Name => "bezier_div";

		public override string Category => "Vector Graphics";

		public override string Description => "Bezier flattening by subdivision or increments, with its error measured. Drag the curve's points or pick a test case.";

		public override int Width => 655;

		public override int Height => 520;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Rgba8.FromRgba(1.0, 1.0, 0.95));

			var curve = new Curve4();
			curve.approximation_method((Curves.CurveApproximationMethod)this.CurveTypeRbox.CurrentItem);
			curve.approximation_scale(this.ApproximationScaleSlider.Value);
			curve.angle_tolerance(Util.deg2rad(this.AngleToleranceSlider.Value));
			curve.cusp_limit(Util.deg2rad(this.CuspLimitSlider.Value));

			var scales = new[] { 0.01, 0.1, 1, 10, 100 };
			var maxErrors = new double[scales.Length];
			var maxAngleErrors = new double[scales.Length];
			for (int i = 0; i < scales.Length; i++)
			{
				maxErrors[i] = this.CalcMaxError(curve, scales[i], out maxAngleErrors[i]);
			}

			curve.approximation_scale(this.ApproximationScaleSlider.Value);
			this.InitCurve(curve);

			// C++ path.concat_path(curve): every command the curve gives, the leading move_to included.
			var path = new VertexStorage();
			curve.Rewind(0);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = curve.Vertex(out double x, out double y)))
			{
				path.Add(x, y, command);
			}

			var stroke = new Stroke(path, this.WidthSlider.Value)
			{
				LineJoin = (LineJoin)this.LineJoinRbox.CurrentItem,
				LineCap = (LineCap)this.LineCapRbox.CurrentItem,
				InnerJoin = (InnerJoin)this.InnerJoinRbox.CurrentItem,
				InnerMiterLimit = 1.01,
			};
			graphics.Render(stroke, Rgba8.FromRgba(0, 0.5, 0, 0.5));

			int numPoints = 0;
			foreach (VertexData vertex in path.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				if (this.ShowPointsBox.Checked)
				{
					graphics.Render(new Ellipse(vertex.Position.X, vertex.Position.Y, 1.5, 1.5, 8), Rgba8.FromRgba(0, 0, 0, 0.5));
				}

				numPoints++;
			}

			if (this.ShowOutlineBox.Checked)
			{
				graphics.Render(new Stroke(stroke), Rgba8.FromRgba(0, 0, 0, 0.5));
			}

			string report = string.Format(
				CultureInfo.InvariantCulture,
				"Num Points={0}\n\n Dist Error: x0.01={1:F5} x0.1={2:F5} x1={3:F5} x10={4:F5} x100={5:F5}\n\n"
					+ "Angle Error: x0.01={6:F1} x0.1={7:F1} x1={8:F1} x10={9:F1} x100={10:F1}",
				numPoints,
				maxErrors[0],
				maxErrors[1],
				maxErrors[2],
				maxErrors[3],
				maxErrors[4],
				maxAngleErrors[0],
				maxAngleErrors[1],
				maxAngleErrors[2],
				maxAngleErrors[3],
				maxAngleErrors[4]);

			// C++ strokes the gsv_text 1.5 wide with round caps and joins.
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; bezier_div.cpp draws its report with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(8.0, 0.0);
			text.start_point(10.0, 85.0);
			text.text(report);
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			graphics.Render(new Stroke(outline, 1.5) { LineJoin = LineJoin.Round, LineCap = LineCap.Round }, Rgba8.FromRgba(0, 0, 0));

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

		/// <summary>C++ <c>bezier4_point</c>: the point at <paramref name="mu"/> (0 to 1) along the exact Bezier.</summary>
		private static Vector2 Bezier4Point(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, double mu)
		{
			double mum1 = 1 - mu;
			double mum13 = mum1 * mum1 * mum1;
			double mu3 = mu * mu * mu;
			return new Vector2(
				(mum13 * p1.X) + (3 * mu * mum1 * mum1 * p2.X) + (3 * mu * mu * mum1 * p3.X) + (mu3 * p4.X),
				(mum13 * p1.Y) + (3 * mu * mum1 * mum1 * p2.Y) + (3 * mu * mu * mum1 * p3.Y) + (mu3 * p4.Y));
		}

		/// <summary>Cumulative length along <paramref name="points"/> at each point, as C++ fills <c>dist</c>.</summary>
		private static double[] Distances(List<Vector2> points)
		{
			var distances = new double[points.Count];
			double distance = 0;
			for (int i = 1; i < points.Count; i++)
			{
				distances[i - 1] = distance;
				distance += agg_math.CalcDistance(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y);
			}

			distances[points.Count - 1] = distance;
			return distances;
		}

		private void InitCurve(Curve4 curve)
		{
			Vector2 p1 = this.CurveCtrl.GetPoint(0);
			Vector2 p2 = this.CurveCtrl.GetPoint(1);
			Vector2 p3 = this.CurveCtrl.GetPoint(2);
			Vector2 p4 = this.CurveCtrl.GetPoint(3);
			curve.init(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y);
		}

		/// <summary>
		/// C++ <c>calc_max_error</c>: flattens the curve at <paramref name="scale"/> times the approximation scale and
		/// measures it against 4096 exact points matched by arc length - the largest distance of one from the
		/// flattened segment it falls on (times <paramref name="scale"/>), and the sharpest turn between segments.
		/// </summary>
		private double CalcMaxError(Curve4 curve, double scale, out double maxAngleError)
		{
			curve.approximation_scale(this.ApproximationScaleSlider.Value * scale);
			this.InitCurve(curve);

			var curvePoints = new List<Vector2>();
			curve.Rewind(0);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = curve.Vertex(out double x, out double y)))
			{
				if (ShapePath.IsVertex(command))
				{
					curvePoints.Add(new Vector2(x, y));
				}
			}

			double[] curveDistances = Distances(curvePoints);

			var referencePoints = new List<Vector2>(4096);
			for (int i = 0; i < 4096; i++)
			{
				referencePoints.Add(Bezier4Point(this.CurveCtrl.GetPoint(0), this.CurveCtrl.GetPoint(1), this.CurveCtrl.GetPoint(2), this.CurveCtrl.GetPoint(3), i / 4095.0));
			}

			double[] referenceDistances = Distances(referencePoints);

			double maxError = 0;
			for (int i = 0; i < referencePoints.Count; i++)
			{
				// C++ find_point: a binary search for the flattened segment this arc length falls on.
				int idx1 = 0;
				int idx2 = curvePoints.Count - 1;
				while (idx2 - idx1 > 1)
				{
					int k = (idx1 + idx2) >> 1;
					if (referenceDistances[i] < curveDistances[k])
					{
						idx2 = k;
					}
					else
					{
						idx1 = k;
					}
				}

				double error = Math.Abs(agg_math.calc_line_point_distance(
					curvePoints[idx1].X,
					curvePoints[idx1].Y,
					curvePoints[idx2].X,
					curvePoints[idx2].Y,
					referencePoints[i].X,
					referencePoints[i].Y));
				maxError = Math.Max(maxError, error);
			}

			double angleError = 0;
			for (int i = 2; i < curvePoints.Count; i++)
			{
				double a1 = Math.Atan2(curvePoints[i - 1].Y - curvePoints[i - 2].Y, curvePoints[i - 1].X - curvePoints[i - 2].X);
				double a2 = Math.Atan2(curvePoints[i].Y - curvePoints[i - 1].Y, curvePoints[i].X - curvePoints[i - 1].X);
				double da = Math.Abs(a1 - a2);
				if (da >= Math.PI)
				{
					da = (2 * Math.PI) - da;
				}

				angleError = Math.Max(angleError, da);
			}

			maxAngleError = angleError * 180.0 / Math.PI;
			return maxError * scale;
		}

		/// <summary>C++ <c>on_ctrl_change</c>: picking a new case loads its curve (Fancy Stroke also widens the stroke).</summary>
		private void OnCtrlChange()
		{
			int caseType = this.CaseTypeRbox.CurrentItem;
			if (caseType != this.currentCaseType)
			{
				switch (caseType)
				{
					case 0:
						int w = this.Width - 120;
						int h = this.Height - 80;
						this.CurveCtrl.SetCurve(
							this.random.Next(w),
							this.random.Next(h) + 80,
							this.random.Next(w),
							this.random.Next(h) + 80,
							this.random.Next(w),
							this.random.Next(h) + 80,
							this.random.Next(w),
							this.random.Next(h) + 80);
						break;

					case 1: this.CurveCtrl.SetCurve(150, 150, 350, 150, 150, 150, 350, 150); break;
					case 2: this.CurveCtrl.SetCurve(50, 142, 483, 251, 496, 62, 26, 333); break;
					case 3: this.CurveCtrl.SetCurve(50, 142, 484, 251, 496, 62, 26, 333); break;
					case 4: this.CurveCtrl.SetCurve(100, 100, 300, 200, 200, 200, 200, 100); break;
					case 5: this.CurveCtrl.SetCurve(475, 157, 200, 100, 453, 100, 222, 157); break;
					case 6:
						this.CurveCtrl.SetCurve(129, 233, 32, 283, 258, 285, 159, 232);
						this.WidthSlider.Value = 100;
						break;

					case 7: this.CurveCtrl.SetCurve(100, 100, 300, 200, 264, 286, 264, 284); break;
					case 8: this.CurveCtrl.SetCurve(100, 100, 413, 304, 264, 286, 264, 284); break;
				}

				this.currentCaseType = caseType;
			}

			this.Invalidate();
		}
	}
}
