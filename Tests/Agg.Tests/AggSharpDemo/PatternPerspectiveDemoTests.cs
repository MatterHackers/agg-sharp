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
	// pattern_perspective.cpp's port against C++ AGG (demo_pattern_perspective.cpp).
	public class PatternPerspectiveDemoTests
	{
		/// <summary>The default frame: the mirrored pattern over the centered square, perspective (linear subdiv).</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new PatternPerspectiveDemo()), "pattern_perspective_600x600");
		}

		/// <summary>Every corner moved: a real perspective, the subdivided interpolator's dda steps in play.</summary>
		[Test]
		public async Task WarpedFrameMatchesCppAgg()
		{
			var demo = new PatternPerspectiveDemo();
			SetQuad(demo, 120, 90, 530, 150, 470, 540, 60, 470);

			await AggReference.Check(Render(demo), "pattern_perspective_600x600_warped");
		}

		/// <summary>A skewed quad through the bilinear transform.</summary>
		[Test]
		public async Task BilinearSkewedFrameMatchesCppAgg()
		{
			var demo = new PatternPerspectiveDemo();
			SetQuad(demo, 80, 120, 520, 90, 470, 500, 130, 460);
			demo.TransTypeRbox.CurrentItem = 1;

			await AggReference.Check(Render(demo), "pattern_perspective_600x600_bilinear_skewed");
		}

		private static void SetQuad(PatternPerspectiveDemo demo, params double[] corners)
		{
			for (int i = 0; i < 4; i++)
			{
				demo.Quad.SetPoint(i, corners[i * 2], corners[(i * 2) + 1]);
			}
		}

		private static ImageBuffer Render(PatternPerspectiveDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
