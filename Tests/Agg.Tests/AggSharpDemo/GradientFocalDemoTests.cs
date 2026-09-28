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
	// gradient_focal.cpp's port against C++ AGG (demo_gradient_focal.cpp): gradient_radial_focus through the
	// srgba8 gradient_lut (patched: segments reach their stops), the boundary circle, the slider and the inverse
	// gamma over the frame.
	public class GradientFocalDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GradientFocalDemo()), "gradient_focal_600x400");
		}

		/// <summary>The focus off center and gamma 1.8, so the LUT colors and the inverse gamma both change.</summary>
		[Test]
		public async Task FocusMovedGamma18FrameMatchesCppAgg()
		{
			var demo = new GradientFocalDemo();
			demo.FocusX = 350;
			demo.FocusY = 240;
			demo.GammaSlider.Value = 1.8;

			await AggReference.Check(Render(demo), "gradient_focal_600x400_focus_moved_gamma18");
		}

		/// <summary>A left click away from the slider moves the focus there; one on the slider does not.</summary>
		[Test]
		public async Task ClickMovesTheFocus()
		{
			var demo = new GradientFocalDemo();
			demo.OnMouseDown(420, 150, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(430, 160, AggInputFlags.MouseLeft);
			demo.OnMouseUp(430, 160, AggInputFlags.MouseLeft, AggInputFlags.None);
			await Assert.That(demo.FocusX).IsEqualTo(430.0);
			await Assert.That(demo.FocusY).IsEqualTo(160.0);

			demo.OnMouseDown(100, 8, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(100, 8, AggInputFlags.MouseLeft, AggInputFlags.None);
			await Assert.That(demo.FocusX).IsEqualTo(430.0);
		}

		private static ImageBuffer Render(GradientFocalDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
