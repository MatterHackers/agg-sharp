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
	// pattern_fill.cpp's port against C++ AGG (demo_pattern_fill.cpp).
	public class PatternFillDemoTests
	{
		/// <summary>The default frame: the 30 pixel tile mirrored over the star from the window's corner.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new PatternFillDemo()), "pattern_fill_640x480");
		}

		/// <summary>
		/// A turned, shrunk star moved off center, with a 17 pixel tile (odd, so the reflect wrap divides rather than
		/// masks), its star turned, a denser background, and the tiling tied to the star's center.
		/// </summary>
		[Test]
		public async Task TurnedSmallTiedMovedFrameMatchesCppAgg()
		{
			var demo = new PatternFillDemo();
			demo.PolygonAngleSlider.Value = 30;
			demo.PolygonScaleSlider.Value = 0.8;
			demo.PatternAngleSlider.Value = -40;
			demo.PatternSizeSlider.Value = 17;
			demo.PatternAlphaSlider.Value = 0.6;
			demo.TiePatternCbox.Checked = true;
			demo.PolygonCenterX = 280;
			demo.PolygonCenterY = 260;

			await AggReference.Check(Render(demo), "pattern_fill_640x480_turned_small_tied_moved");
		}

		private static ImageBuffer Render(PatternFillDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
