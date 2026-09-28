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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// conv_contour.cpp's port against C++ AGG (demo_conv_contour.cpp): conv_contour over flattened curve3s,
	// its orientation flags and autodetection, and the rbox, slider and cbox.
	public class ConvContourDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ConvContourDemo()), "conv_contour_440x330");
		}

		/// <summary>Unmarked polygons grown by 20 with orientation autodetection on, so the hole shrinks.</summary>
		[Test]
		public async Task AutodetectWidth20FrameMatchesCppAgg()
		{
			var demo = new ConvContourDemo();
			demo.WidthSlider.Value = 20;
			demo.AutoDetectCbox.Checked = true;

			await AggReference.Check(Render(demo), "conv_contour_440x330_autodetect_width20");
		}

		/// <summary>A click on the "Close CCW" item selects it.</summary>
		[Test]
		public async Task ClickingAnItemSelectsIt()
		{
			var demo = new ConvContourDemo();
			demo.OnMouseDown(25, 61, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);

			await Assert.That(demo.CloseRbox.CurrentItem).IsEqualTo(2);
		}

		private static ImageBuffer Render(ConvContourDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
