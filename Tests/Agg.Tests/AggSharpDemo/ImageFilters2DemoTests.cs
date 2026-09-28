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
	// image_filters2.cpp's port against C++ AGG (demo_image_filters2.cpp).
	public class ImageFilters2DemoTests
	{
		/// <summary>The first frame: the 4x4 image through the normalized bilinear filter.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImageFilters2Demo()), "image_filters2_500x340");
		}

		/// <summary>Nearest neighbour: no lookup table, so no graph.</summary>
		[Test]
		public async Task NearestNeighbourMatchesCppAgg()
		{
			var demo = new ImageFilters2Demo();
			demo.FiltersRbox.CurrentItem = 0;

			await AggReference.Check(Render(demo), "image_filters2_500x340_nn");
		}

		/// <summary>Spline36 with its weights left unnormalized.</summary>
		[Test]
		public async Task Spline36UnnormalizedMatchesCppAgg()
		{
			var demo = new ImageFilters2Demo();
			demo.FiltersRbox.CurrentItem = 4;
			demo.NormalizeCbox.Checked = false;

			await AggReference.Check(Render(demo), "image_filters2_500x340_spline36_unnormalized");
		}

		/// <summary>Sinc at radius 3, which also shows the radius slider.</summary>
		[Test]
		public async Task SincRadius3MatchesCppAgg()
		{
			var demo = new ImageFilters2Demo();
			demo.FiltersRbox.CurrentItem = 14;
			demo.RadiusSlider.Value = 3;

			await AggReference.Check(Render(demo), "image_filters2_500x340_sinc_r3");
		}

		private static ImageBuffer Render(ImageFilters2Demo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
