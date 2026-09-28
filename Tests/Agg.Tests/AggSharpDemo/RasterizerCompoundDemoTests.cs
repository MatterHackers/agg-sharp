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
	// rasterizer_compound.cpp's port against C++ AGG (demo_rasterizer_compound.cpp).
	public class RasterizerCompoundDemoTests
	{
		/// <summary>The default frame: four opaque layers, the highest style on top.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new RasterizerCompoundDemo()), "rasterizer_compound_440x330");
		}

		/// <summary>Wide strokes, every layer translucent and the order inverted, so coverage is shared out
		/// between overlapping translucent layers bottom style first.</summary>
		[Test]
		public async Task WideTranslucentInvertedFrameMatchesCppAgg()
		{
			var demo = new RasterizerCompoundDemo();
			demo.WidthSlider.Value = 30;
			demo.Alpha1Slider.Value = 0.6;
			demo.Alpha2Slider.Value = 0.4;
			demo.Alpha3Slider.Value = 0.7;
			demo.Alpha4Slider.Value = 0.5;
			demo.InvertOrderBox.Checked = true;

			await AggReference.Check(Render(demo), "rasterizer_compound_440x330_wide_translucent_inverted");
		}

		private static ImageBuffer Render(RasterizerCompoundDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
