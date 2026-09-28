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
	// image_filters.cpp's port against C++ AGG (demo_image_filters.cpp).
	public class ImageFiltersDemoTests
	{
		/// <summary>The first frame: the bilinear filter at no turn, and NSteps=0.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImageFiltersDemo()), "image_filters_430x340");
		}

		/// <summary>Bicubic, a general lookup-table filter, turned twice by 5 degrees.</summary>
		[Test]
		public async Task BicubicTwoStepsMatchesCppAgg()
		{
			var demo = new ImageFiltersDemo();
			demo.FiltersRbox.CurrentItem = 2;
			demo.OnCtrlChange();
			SingleStep(demo);
			SingleStep(demo);

			await AggReference.Check(Render(demo), "image_filters_430x340_bicubic_2steps");
		}

		/// <summary>Hanning, a 2x2 filter, turned once by 10 degrees.</summary>
		[Test]
		public async Task HanningOneStepMatchesCppAgg()
		{
			var demo = new ImageFiltersDemo();
			demo.StepSlider.Value = 10;
			demo.FiltersRbox.CurrentItem = 5;
			demo.OnCtrlChange();
			SingleStep(demo);

			await AggReference.Check(Render(demo), "image_filters_430x340_hanning_step10");
		}

		private static void SingleStep(ImageFiltersDemo demo)
		{
			demo.SingleStepCbox.Checked = true;
			demo.OnCtrlChange();
		}

		private static ImageBuffer Render(ImageFiltersDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
