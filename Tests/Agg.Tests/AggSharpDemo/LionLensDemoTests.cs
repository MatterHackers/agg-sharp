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
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// lion_lens.cpp's port: its reference render against C++ AGG (demo_lion_lens.cpp).
	public class LionLensDemoTests
	{
		/// <summary>The default frame - magnification 3, radius 70, lens at 200,150 - matches C++ AGG byte for byte.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new LionLensDemo()), "lion_lens_500x600");
		}

		/// <summary>A weaker, wider lens dragged elsewhere (params 1.7 95 310 330) matches C++ AGG too.</summary>
		[Test]
		public async Task WeakWideMovedLensMatchesCppAgg()
		{
			var demo = new LionLensDemo();
			demo.MagnificationSlider.Value = 1.7;
			demo.RadiusSlider.Value = 95;

			// A left press away from the sliders moves the lens, as in C++.
			demo.OnMouseDown(310, 330, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(310, 330, AggInputFlags.MouseLeft, AggInputFlags.None);

			await AggReference.Check(Render(demo), "lion_lens_500x600_weak_wide_moved");
		}

		private static ImageBuffer Render(LionLensDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
