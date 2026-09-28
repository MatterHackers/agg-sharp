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
	// alpha_gradient.cpp's port against C++ AGG (demo_alpha_gradient.cpp): the random ellipses, span_gradient
	// through span_converter + span_gradient_alpha, the parallelogram and the spline_ctrl.
	public class AlphaGradientDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new AlphaGradientDemo()), "alpha_gradient_400x320");
		}

		/// <summary>The parallelogram moved and skewed, the alpha spline's point 2 raised and active (drawn red).</summary>
		[Test]
		public async Task MovedBentPoint2ActiveFrameMatchesCppAgg()
		{
			var demo = new AlphaGradientDemo();
			double[] corners = { 230, 40, 380, 150, 120, 290 };
			for (int i = 0; i < 3; i++)
			{
				demo.CornerX[i] = corners[i * 2];
				demo.CornerY[i] = corners[(i * 2) + 1];
			}

			demo.AlphaSpline.SetPoint(2, 2.0 / 5.0, 0.9);
			demo.AlphaSpline.ActivePoint = 2;
			demo.AlphaSpline.UpdateSpline();

			await AggReference.Check(Render(demo), "alpha_gradient_400x320_moved_bent_p2active");
		}

		/// <summary>Dragging inside the triangle moves the whole parallelogram; dragging a corner moves only it.</summary>
		[Test]
		public async Task DraggingMovesTheParallelogram()
		{
			var demo = new AlphaGradientDemo();
			demo.OnMouseDown(260, 180, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(270, 175, AggInputFlags.MouseLeft);
			demo.OnMouseUp(270, 175, AggInputFlags.MouseLeft, AggInputFlags.None);
			await Assert.That(demo.CornerX[0]).IsEqualTo(267.0);
			await Assert.That(demo.CornerY[0]).IsEqualTo(55.0);
			await Assert.That(demo.CornerX[2]).IsEqualTo(153.0);
			await Assert.That(demo.CornerY[2]).IsEqualTo(305.0);

			demo.OnMouseDown(380, 167, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(390, 187, AggInputFlags.MouseLeft);
			demo.OnMouseUp(390, 187, AggInputFlags.MouseLeft, AggInputFlags.None);
			await Assert.That(demo.CornerX[1]).IsEqualTo(389.0);
			await Assert.That(demo.CornerY[1]).IsEqualTo(185.0);
			await Assert.That(demo.CornerX[0]).IsEqualTo(267.0);
		}

		/// <summary>A drag on the alpha spline's point moves the point, not the parallelogram.</summary>
		[Test]
		public async Task DraggingTheSplineMovesItsPoint()
		{
			var demo = new AlphaGradientDemo();

			// Box (2, 2)-(200, 30), border 1: the spline box is (3, 3)-(199, 29), 196 x 26. Point 3 is at (0.6, 0.6).
			double x = 3 + (196 * 0.6);
			double y = 3 + (26 * 0.6);
			demo.OnMouseDown((int)Math.Round(x), (int)Math.Round(y), AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove((int)Math.Round(x), (int)Math.Round(y) + 5, AggInputFlags.MouseLeft);
			demo.OnMouseUp((int)Math.Round(x), (int)Math.Round(y) + 5, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.AlphaSpline.ActivePoint).IsEqualTo(3);
			await Assert.That(demo.AlphaSpline.GetY(3)).IsEqualTo(0.6 + (5 / 26.0)).Within(1e-9);
			await Assert.That(demo.CornerX[0]).IsEqualTo(257.0);
		}

		private static ImageBuffer Render(AlphaGradientDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
