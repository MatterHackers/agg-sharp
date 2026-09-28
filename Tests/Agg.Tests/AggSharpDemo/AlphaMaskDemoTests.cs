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
	// alpha_mask.cpp's port: its reference render against C++ AGG (demo_alpha_mask.cpp).
	public class AlphaMaskDemoTests
	{
		/// <summary>The lion through the random-ellipse gray8 mask, byte-identical to C++ AGG's bgr24 render.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new AlphaMaskDemo()), "alpha_mask_512x400");
		}

		/// <summary>A left press turns and scales the lion under the fixed mask; a right press skews it.</summary>
		[Test]
		public async Task TurnedAndSkewedFrameMatchesCppAgg()
		{
			var demo = new AlphaMaskDemo();
			demo.OnMouseDown(356, 300, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(356, 300, AggInputFlags.MouseLeft, AggInputFlags.None);
			demo.OnMouseDown(120, 60, AggInputFlags.MouseRight, AggInputFlags.MouseRight);

			await AggReference.Check(Render(demo), "alpha_mask_512x400_turned_skewed");
		}

		/// <summary>The C89 rand() sequence, which the C++ reference renderer's msvc_rand also produces.</summary>
		[Test]
		public async Task MsvcRandMatchesTheC89Sequence()
		{
			var random = new MsvcRand();
			await Assert.That(random.Next()).IsEqualTo(41);
			await Assert.That(random.Next()).IsEqualTo(18467);
			await Assert.That(random.Next()).IsEqualTo(6334);
		}

		private static ImageBuffer Render(AlphaMaskDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
