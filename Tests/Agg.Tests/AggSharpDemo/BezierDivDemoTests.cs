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

using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// bezier_div.cpp's port against C++ AGG (demo_bezier_div.cpp): curve4 flattened by subdivision or increments,
	// a wide stroke of it and that stroke's outline, the error report in gsv_text, and the bezier ctrl, sliders,
	// cboxes and rboxes.
	public class BezierDivDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new BezierDivDemo()), "bezier_div_655x520");
		}

		/// <summary>
		/// The Real Cusp 2 curve flattened incrementally at scale 2.5 (angle 30, cusp 10), stroked -20 wide with
		/// inner miter, miter-round joins and square caps, points hidden.
		/// </summary>
		[Test]
		public async Task RealCusp2IncrementalMiterRoundFrameMatchesCppAgg()
		{
			var demo = new BezierDivDemo();
			demo.CurveCtrl.SetCurve(475, 157, 200, 100, 453, 100, 222, 157);
			demo.WidthSlider.Value = -20;
			demo.ShowPointsBox.Checked = false;
			demo.AngleToleranceSlider.Value = 30;
			demo.ApproximationScaleSlider.Value = 2.5;
			demo.CuspLimitSlider.Value = 10;
			demo.CurveTypeRbox.CurrentItem = 0;
			demo.CaseTypeRbox.CurrentItem = 5;
			demo.InnerJoinRbox.CurrentItem = 1;
			demo.LineJoinRbox.CurrentItem = 4;
			demo.LineCapRbox.CurrentItem = 1;

			await AggReference.Check(Render(demo), "bezier_div_655x520_real_cusp2_inc_miter_round");
		}

		/// <summary>Picking Fancy Stroke in the case rbox loads its curve and widens the stroke to 100, as C++'s on_ctrl_change does.</summary>
		[Test]
		public async Task PickingFancyStrokeLoadsItsCurveAndWidth()
		{
			var demo = new BezierDivDemo();

			// The case rbox's items are 14 apart from y 61; Fancy Stroke is the seventh, its circle at (546.8, 155.8).
			demo.OnMouseDown(547, 156, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(547, 156, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.CaseTypeRbox.CurrentItem).IsEqualTo(6);
			await Assert.That(demo.CurveCtrl.GetPoint(0)).IsEqualTo(new Vector2(129, 233));
			await Assert.That(demo.CurveCtrl.GetPoint(3)).IsEqualTo(new Vector2(159, 232));
			await Assert.That(demo.WidthSlider.Value).IsEqualTo(100.0);
		}

		private static ImageBuffer Render(BezierDivDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
