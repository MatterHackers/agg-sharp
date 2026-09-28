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
	// multi_clip.cpp's port against C++ AGG (demo_multi_clip.cpp).
	public class MultiClipDemoTests
	{
		/// <summary>
		/// The default frame: a 6 x 6 grid of clip boxes, and the lion, Bresenham lines, markers, anti-aliased
		/// lines and gradient discs through them.
		/// </summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new MultiClipDemo()), "multi_clip_512x400");
		}

		/// <summary>
		/// A 3 x 3 grid; the lion turned and scaled by a left press and skewed by a right press.
		/// </summary>
		[Test]
		public async Task ThreeByThreeGridTurnedAndSkewedFrameMatchesCppAgg()
		{
			var demo = new MultiClipDemo();
			demo.NumBoxesSlider.Value = 3;
			demo.OnMouseDown(356, 300, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(356, 300, AggInputFlags.MouseLeft, AggInputFlags.None);
			demo.OnMouseDown(120, 60, AggInputFlags.MouseRight, AggInputFlags.MouseRight);

			await AggReference.Check(Render(demo), "multi_clip_512x400_n3_turned_skewed");
		}

		private static ImageBuffer Render(MultiClipDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
