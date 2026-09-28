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
	// image_fltr_graph.cpp's port against C++ AGG (demo_image_fltr_graph.cpp).
	public class ImageFltrGraphDemoTests
	{
		/// <summary>The first frame: no filter checked, only the grid and the checkboxes.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImageFltrGraphDemo()), "image_fltr_graph_780x300");
		}

		/// <summary>Bicubic, spline36, kaiser, bessel and sinc at radius 4.5, which also shows the radius slider.</summary>
		[Test]
		public async Task FiveFiltersRadius45MatchesCppAgg()
		{
			var demo = new ImageFltrGraphDemo();
			demo.RadiusSlider.Value = 4.5;
			foreach (int i in new[] { 1, 3, 7, 11, 13 })
			{
				demo.FilterCboxes[i].Checked = true;
			}

			await AggReference.Check(Render(demo), "image_fltr_graph_780x300_bicubic_spline36_kaiser_bessel_sinc_r45");
		}

		/// <summary>All 16 filters, the variable-radius ones at 3.3.</summary>
		[Test]
		public async Task AllFiltersRadius33MatchesCppAgg()
		{
			var demo = new ImageFltrGraphDemo();
			demo.RadiusSlider.Value = 3.3;
			foreach (var cbox in demo.FilterCboxes)
			{
				cbox.Checked = true;
			}

			await AggReference.Check(Render(demo), "image_fltr_graph_780x300_all_r33");
		}

		private static ImageBuffer Render(ImageFltrGraphDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
