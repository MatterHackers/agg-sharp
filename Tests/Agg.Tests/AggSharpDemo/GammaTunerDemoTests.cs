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
	// gamma_tuner.cpp's port against C++ AGG (demo_gamma_tuner.cpp): the ramps copied and the spans blended
	// through pixfmt_sbgr24_gamma (the example is built with AGG_SBGR24, so all colors are srgba8 bytes), which
	// the port matches with BlenderGammaBGRA on the frame.
	public class GammaTunerDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GammaTunerDemo()), "gamma_tuner_500x500");
		}

		/// <summary>Gamma 1.4, an orange color and the Horizontal pattern.</summary>
		[Test]
		public async Task Gamma14OrangeHorizontalFrameMatchesCppAgg()
		{
			var demo = new GammaTunerDemo();
			demo.GammaSlider.Value = 1.4;
			demo.GreenSlider.Value = 0.55;
			demo.BlueSlider.Value = 0.2;
			demo.PatternRbox.CurrentItem = 0;

			await AggReference.Check(Render(demo), "gamma_tuner_500x500_gamma14_orange_horizontal");
		}

		/// <summary>The frame's own blender is put back after the draw.</summary>
		[Test]
		public async Task DrawingRestoresTheFramesBlender()
		{
			var demo = new GammaTunerDemo();
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			IRecieveBlenderByte blender = frame.GetRecieveBlender();
			AggDemoView.DrawReferenceFrame(demo, frame);

			await Assert.That(frame.GetRecieveBlender()).IsSameReferenceAs(blender);
		}

		private static ImageBuffer Render(GammaTunerDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
