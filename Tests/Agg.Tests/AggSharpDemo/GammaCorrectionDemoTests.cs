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
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// gamma_correction.cpp's port against C++ AGG (demo_gamma_correction.cpp): everything drawn through
	// pixfmt_bgr24_gamma, which the port matches with BlenderGammaBGRA on the frame.
	public class GammaCorrectionDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GammaCorrectionDemo()), "gamma_correction_400x320");
		}

		/// <summary>Gamma 1.8 (so every blend goes through the LUT), thinner lines, less contrast, smaller ellipses.</summary>
		[Test]
		public async Task ThinGamma18ResizedFrameMatchesCppAgg()
		{
			var demo = new GammaCorrectionDemo();
			demo.ThicknessSlider.Value = 0.6;
			demo.ContrastSlider.Value = 0.7;
			demo.GammaSlider.Value = 1.8;
			demo.SetRadii(150, 90);

			await AggReference.Check(Render(demo), "gamma_correction_400x320_thin_gamma18_resized");
		}

		/// <summary>The frame's own blender is put back after the draw.</summary>
		[Test]
		public async Task DrawingRestoresTheFramesBlender()
		{
			var demo = new GammaCorrectionDemo();
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			IRecieveBlenderByte blender = frame.GetRecieveBlender();
			AggDemoView.DrawReferenceFrame(demo, frame);

			await Assert.That(frame.GetRecieveBlender()).IsSameReferenceAs(blender);
		}

		private static ImageBuffer Render(GammaCorrectionDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
