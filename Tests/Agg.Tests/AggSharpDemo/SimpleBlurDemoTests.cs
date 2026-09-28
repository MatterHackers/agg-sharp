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
	// simple_blur.cpp's port: its reference render against C++ AGG (demo_simple_blur.cpp, which carries the
	// side-column fix described on SimpleBlurDemo's span generator).
	public class SimpleBlurDemoTests
	{
		/// <summary>The default frame - blur circle at 100,102, reaching the left edge - matches C++ AGG byte for byte.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new SimpleBlurDemo()), "simple_blur_512x400");
		}

		/// <summary>The circle dragged over the outlined lion (params 330 280) matches C++ AGG too.</summary>
		[Test]
		public async Task MovedCircleMatchesCppAgg()
		{
			var demo = new SimpleBlurDemo();
			demo.OnMouseDown(330, 280, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);

			await AggReference.Check(Render(demo), "simple_blur_512x400_moved");
		}

		/// <summary>
		/// C++'s example blur paints opaque black where its 3x3 block would leave the image at the left or right;
		/// the port leaves those pixels as they were, as C++ does at the top and bottom.
		/// </summary>
		[Test]
		public async Task BlurLeavesTheImageEdgeColumnsAlone()
		{
			ImageBuffer frame = Render(new SimpleBlurDemo());

			// Column 0 at the circle's middle height is inside the circle and was white before the blur.
			await Assert.That(frame.GetPixel(0, 102)).IsEqualTo(Color.White);
		}

		private static ImageBuffer Render(SimpleBlurDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
