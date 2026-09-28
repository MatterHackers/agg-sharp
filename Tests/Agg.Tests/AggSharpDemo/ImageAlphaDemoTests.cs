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
	// image_alpha.cpp's port against C++ AGG (demo_image_alpha.cpp).
	public class ImageAlphaDemoTests
	{
		/// <summary>
		/// The default frame: dark and bright pixels opaque, mid-tones half transparent over the random ellipses,
		/// and the ellipse reaching past the image's sides onto the filter's black.
		/// </summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ImageAlphaDemo()), "image_alpha_320x300");
		}

		/// <summary>A curve from opaque darks to clear brights, so the bright spheres let the ellipses through.</summary>
		[Test]
		public async Task DarkOpaqueBrightClearFrameMatchesCppAgg()
		{
			var demo = new ImageAlphaDemo();
			double[] ys = { 0, 0.3, 0.6, 0.9, 0.4, 0 };
			for (int i = 0; i < ys.Length; i++)
			{
				demo.AlphaSpline.SetValue(i, ys[i]);
			}

			demo.AlphaSpline.UpdateSpline();

			await AggReference.Check(Render(demo), "image_alpha_320x300_dark_opaque_bright_clear");
		}

		private static ImageBuffer Render(ImageAlphaDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
