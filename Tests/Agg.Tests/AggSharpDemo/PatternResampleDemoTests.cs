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
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// pattern_resample.cpp's port against C++ AGG (demo_pattern_resample.cpp).
	public class PatternResampleDemoTests
	{
		/// <summary>The default frame: perspective resampling with the lerp interpolator, gamma 2, blur 1.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new PatternResampleDemo()), "pattern_resample_600x600");
		}

		/// <summary>A small warped quad, so the pattern shrinks: the exact interpolator's resampling, blurred, at gamma 1.</summary>
		[Test]
		public async Task SmallExactBlurredFrameMatchesCppAgg()
		{
			var demo = new PatternResampleDemo();
			SetQuad(demo, 200, 190, 370, 220, 350, 380, 180, 340);
			demo.TransTypeRbox.CurrentItem = 5;
			demo.GammaSlider.Value = 1.0;
			demo.BlurSlider.Value = 1.5;

			await AggReference.Check(Render(demo), "pattern_resample_600x600_small_exact_blur15_gamma1");
		}

		/// <summary>A small skewed parallelogram through the affine resampler, less blur, at gamma 2.6.</summary>
		[Test]
		public async Task AffineResampleSmallFrameMatchesCppAgg()
		{
			var demo = new PatternResampleDemo();
			SetQuad(demo, 220, 200, 380, 240, 360, 360, 200, 320);
			demo.TransTypeRbox.CurrentItem = 1;
			demo.GammaSlider.Value = 2.6;
			demo.BlurSlider.Value = 0.7;

			await AggReference.Check(Render(demo), "pattern_resample_600x600_affine_resample_small_blur07_gamma26");
		}

		/// <summary>A warped quad through the exact per-pixel perspective (span_interpolator_trans) and the 2x2 filter.</summary>
		[Test]
		public async Task TransWarpedFrameMatchesCppAgg()
		{
			var demo = new PatternResampleDemo();
			SetQuad(demo, 120, 90, 530, 150, 470, 540, 60, 470);
			demo.TransTypeRbox.CurrentItem = 3;

			await AggReference.Check(Render(demo), "pattern_resample_600x600_trans_warped");
		}

		private static void SetQuad(PatternResampleDemo demo, params double[] corners)
		{
			for (int i = 0; i < 4; i++)
			{
				demo.Quad.SetPoint(i, corners[i * 2], corners[(i * 2) + 1]);
			}
		}

		private static ImageBuffer Render(PatternResampleDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
