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
	// image_resample.cpp's port against C++ AGG (demo_image_resample.cpp).
	public class ImageResampleDemoTests
	{
		/// <summary>The default frame: the image centered at its own size, perspective resampling with the lerp interpolator.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImageResampleDemo()), "image_resample_600x600");
		}

		/// <summary>A small skewed parallelogram through the affine resampler, blurred twice over.</summary>
		[Test]
		public async Task AffineResampleSmallFrameMatchesCppAgg()
		{
			var demo = new ImageResampleDemo();
			SetQuad(demo, 230, 220, 380, 250, 360, 370, 200, 340);
			demo.TransTypeRbox.CurrentItem = 1;
			demo.BlurSlider.Value = 2;

			await AggReference.Check(Render(demo), "image_resample_600x600_affine_resample_small_blur2");
		}

		/// <summary>A small warped quad, so the image shrinks: the exact interpolator's resampling, blurred.</summary>
		[Test]
		public async Task ExactSmallWarpedFrameMatchesCppAgg()
		{
			var demo = new ImageResampleDemo();
			SetQuad(demo, 200, 190, 370, 220, 350, 380, 180, 340);
			demo.TransTypeRbox.CurrentItem = 5;
			demo.BlurSlider.Value = 1.5;

			await AggReference.Check(Render(demo), "image_resample_600x600_exact_small_warped_blur15");
		}

		/// <summary>A turned parallelogram through the 2x2 filter, the image enlarged.</summary>
		[Test]
		public async Task Affine2x2TurnedFrameMatchesCppAgg()
		{
			var demo = new ImageResampleDemo();
			SetQuad(demo, 150, 120, 470, 200, 400, 480, 80, 400);
			demo.TransTypeRbox.CurrentItem = 0;

			await AggReference.Check(Render(demo), "image_resample_600x600_affine_2x2_turned");
		}

		private static void SetQuad(ImageResampleDemo demo, params double[] corners)
		{
			for (int i = 0; i < 4; i++)
			{
				demo.Quad.SetPoint(i, corners[i * 2], corners[(i * 2) + 1]);
			}
		}

		private static ImageBuffer Render(ImageResampleDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
