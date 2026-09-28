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
	// image_perspective.cpp's port against C++ AGG (demo_image_perspective.cpp).
	public class ImagePerspectiveDemoTests
	{
		/// <summary>The default frame: the perspective mapping onto the quad inset by 100.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImagePerspectiveDemo()), "image_perspective_600x600");
		}

		/// <summary>The bilinear mapping onto a skewed quad, so the 2x2 filter blends between pixels.</summary>
		[Test]
		public async Task BilinearSkewedFrameMatchesCppAgg()
		{
			var demo = new ImagePerspectiveDemo();
			demo.TransTypeRbox.CurrentItem = 1;
			demo.Quad.SetPoint(0, 80, 120);
			demo.Quad.SetPoint(1, 520, 90);
			demo.Quad.SetPoint(2, 470, 500);
			demo.Quad.SetPoint(3, 130, 460);

			await AggReference.Check(Render(demo), "image_perspective_600x600_bilinear_skewed");
		}

		private static ImageBuffer Render(ImagePerspectiveDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
