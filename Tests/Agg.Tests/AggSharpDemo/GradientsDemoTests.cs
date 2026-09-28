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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// gradients.cpp's port against C++ AGG (demo_gradients.cpp): the gamma profile, the four spline_ctrls, the
	// rbox, and the circle filled by span_gradient through the profile.
	public class GradientsDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GradientsDemo()), "gradients_512x400");
		}

		/// <summary>
		/// The conic gradient moved, scaled and turned, under a bent profile, with the alpha spline's inner
		/// points at 0.5 so the gradient blends over the black background.
		/// </summary>
		[Test]
		public async Task ConicBentHalfAlphaFrameMatchesCppAgg()
		{
			var demo = new GradientsDemo();
			demo.GradientRbox.CurrentItem = 5;
			demo.GradientCenterX = 300;
			demo.GradientCenterY = 250;
			demo.GradientScale = 1.4;
			demo.GradientAngle = 0.6;
			demo.Profile.SetValues(1.3, 0.7, 0.8, 1.2);
			for (int i = 1; i < 5; i++)
			{
				demo.SplineA.SetPoint(i, i / 5.0, 0.5);
			}

			demo.SplineA.UpdateSpline();

			await AggReference.Check(Render(demo), "gradients_512x400_conic_bent_halfalpha");
		}

		/// <summary>
		/// Dragging the red spline's point 1 (at 1/5, 4/5 of its box) up and right: it becomes the active point,
		/// moves by the drag in box units, and the curve is resampled. Then an arrow key nudges it by 0.001.
		/// </summary>
		[Test]
		public async Task DraggingASplinePointMovesItAndArrowKeysNudgeIt()
		{
			var demo = new GradientsDemo();
			SplineCtrl spline = demo.SplineR;

			// Box (210, 10)-(460, 45), border 1: the spline box is (211, 11)-(459, 44), 248 x 33.
			double x = 211 + (248 * 0.2);
			double y = 11 + (33 * 0.8);
			demo.OnMouseDown((int)x, (int)y, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove((int)x + 31, (int)y - 11, AggInputFlags.MouseLeft);
			demo.OnMouseUp((int)x + 31, (int)y - 11, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(spline.ActivePoint).IsEqualTo(1);
			await Assert.That(spline.GetX(1)).IsEqualTo(0.2 + (31 / 248.0)).Within(1e-9);
			await Assert.That(spline.GetY(1)).IsEqualTo(0.8 - (11 / 33.0)).Within(1e-9);
			await Assert.That(spline.Value(spline.GetX(1))).IsEqualTo(spline.GetY(1)).Within(1e-9);
			await Assert.That(demo.GradientCenterX).IsEqualTo(350.0);

			demo.OnKeyDown(Keys.Up, AggInputFlags.None);
			await Assert.That(spline.GetY(1)).IsEqualTo(0.8 - (11 / 33.0) + 0.001).Within(1e-9);
		}

		/// <summary>Inner points keep 0.001 clear of their neighbours and the end points stay pinned at x 0 and 1.</summary>
		[Test]
		public async Task SplinePointsKeepTheirOrder()
		{
			var spline = new SplineCtrl(0, 0, 100, 100, 4);
			spline.SetPoint(1, 0.9, 2.0);
			spline.SetPoint(0, 0.5, -1.0);

			await Assert.That(spline.GetX(1)).IsEqualTo((2.0 / 3.0) - 0.001).Within(1e-12);
			await Assert.That(spline.GetY(1)).IsEqualTo(1.0);
			await Assert.That(spline.GetX(0)).IsEqualTo(0.0);
			await Assert.That(spline.GetY(0)).IsEqualTo(0.0);
		}

		/// <summary>A drag outside every ctrl moves the gradient's center with the mouse.</summary>
		[Test]
		public async Task DraggingOutsideTheCtrlsMovesTheGradient()
		{
			var demo = new GradientsDemo();
			demo.OnMouseDown(340, 270, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(360, 300, AggInputFlags.MouseLeft);
			demo.OnMouseUp(360, 300, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.GradientCenterX).IsEqualTo(370.0);
			await Assert.That(demo.GradientCenterY).IsEqualTo(310.0);
		}

		private static ImageBuffer Render(GradientsDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
