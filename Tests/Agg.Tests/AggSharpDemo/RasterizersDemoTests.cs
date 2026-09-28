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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// rasterizers.cpp's port against C++ AGG (demo_rasterizers.cpp): the anti-aliased triangle under a
	// gamma_power, the aliased one through scanline_bin under a gamma_threshold (the port's threshold fill on
	// the anti-aliased scanline has to land the same bytes), and the two sliders and the cbox.
	public class RasterizersDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new RasterizersDemo()), "rasterizers_500x330");
		}

		/// <summary>Gamma 0.3 (a 0.6 power, and a low threshold) and 0.6 alpha, on a moved triangle.</summary>
		[Test]
		public async Task Gamma3Alpha6MovedFrameMatchesCppAgg()
		{
			var demo = new RasterizersDemo();
			demo.GammaSlider.Value = 0.3;
			demo.AlphaSlider.Value = 0.6;
			demo.SetVertex(0, 240, 50);
			demo.SetVertex(1, 470, 190);
			demo.SetVertex(2, 280, 300);

			await AggReference.Check(Render(demo), "rasterizers_500x330_gamma3_alpha6_moved");
		}

		/// <summary>A press on a corner of the aliased copy drags that corner of both triangles.</summary>
		[Test]
		public async Task DraggingTheAliasedCopysCornerMovesTheCorner()
		{
			var demo = new RasterizersDemo();
			demo.OnMouseDown(22, 62, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(32, 72, AggInputFlags.MouseLeft);
			demo.OnMouseUp(32, 72, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.GetVertex(0)).IsEqualTo(new Vector2(230, 70));
			await Assert.That(demo.GetVertex(1)).IsEqualTo(new Vector2(489, 170));
		}

		/// <summary>Checking "Test Performance" unchecks it again, as C++ does once its timing run ends.</summary>
		[Test]
		public async Task TestPerformanceUnchecksItself()
		{
			var demo = new RasterizersDemo();
			demo.OnMouseDown(143, 33, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);

			await Assert.That(demo.TestCbox.Checked).IsFalse();
		}

		private static ImageBuffer Render(RasterizersDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
