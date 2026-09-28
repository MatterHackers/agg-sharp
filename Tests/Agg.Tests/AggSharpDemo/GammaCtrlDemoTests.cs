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
	// gamma_ctrl.cpp's port against C++ AGG (demo_gamma_ctrl.cpp): the gamma_ctrl itself (curve, grid,
	// points, the values as text) and its curve as the rasterizer gamma of everything drawn after it.
	public class GammaCtrlDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GammaCtrlDemo()), "gamma_ctrl_500x400");
		}

		/// <summary>A bent curve with point 2 active (drawn red), so the gamma and both point colors differ.</summary>
		[Test]
		public async Task CurvedPoint2ActiveFrameMatchesCppAgg()
		{
			var demo = new GammaCtrlDemo();
			demo.Gamma.SetValues(0.4, 1.6, 1.2, 0.5);
			demo.Gamma.ChangeActivePoint();

			await AggReference.Check(Render(demo), "gamma_ctrl_500x400_curved_p2active");
		}

		/// <summary>Dragging point 1 by (+29, +18) - a quarter-unit step each way in the 286 x 169 spline box.</summary>
		[Test]
		public async Task DraggingPoint1ChangesItsValues()
		{
			var demo = new GammaCtrlDemo();

			// The default point 1 is a quarter of the way into the spline box (12, 12)-(298, 181).
			demo.OnMouseDown(84, 54, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(84 + 71, 54, AggInputFlags.MouseLeft);
			demo.OnMouseUp(84 + 71, 54, AggInputFlags.MouseLeft, AggInputFlags.None);

			demo.Gamma.GetValues(out double kx1, out double ky1, out double kx2, out double ky2);
			await Assert.That(kx1).IsEqualTo(1.0 + (71 * 4.0 / 286.0)).Within(1e-9);
			await Assert.That(ky1).IsEqualTo(1.0).Within(1e-9);
			await Assert.That(kx2).IsEqualTo(1.0).Within(1e-9);
			await Assert.That(demo.Gamma.Point1Active).IsTrue();
		}

		/// <summary>
		/// A click on point 2 makes it active, and the arrow keys then move it: its x value grows on Left, as
		/// C++. (Like C++, keys go to the ctrl last pressed, so the click is also what routes them.)
		/// </summary>
		[Test]
		public async Task ArrowKeysMoveTheClickedPoint()
		{
			var demo = new GammaCtrlDemo();

			// The default point 2 is a quarter of the way in from the top right of the spline box.
			demo.OnMouseDown(226, 139, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(226, 139, AggInputFlags.MouseLeft, AggInputFlags.None);
			demo.OnKeyDown(Keys.Left, AggInputFlags.None);

			demo.Gamma.GetValues(out double kx1, out _, out double kx2, out _);
			await Assert.That(demo.Gamma.Point1Active).IsFalse();
			await Assert.That(kx1).IsEqualTo(1.0).Within(1e-9);
			await Assert.That(kx2).IsEqualTo(1.005).Within(1e-9);
		}

		private static ImageBuffer Render(GammaCtrlDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
