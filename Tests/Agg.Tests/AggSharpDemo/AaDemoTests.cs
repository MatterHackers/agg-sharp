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
	// aa_demo.cpp's port against C++ AGG (demo_aa_demo.cpp): renderer_enlarged's squares, the small triangle,
	// its outline and the pixel size slider.
	public class AaDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new AaDemo()), "aa_demo_600x400");
		}

		/// <summary>A smaller pixel size (more, smaller squares) and every vertex moved off its default.</summary>
		[Test]
		public async Task MovedTriangleAtPixelSize16MatchesCppAgg()
		{
			var demo = new AaDemo();
			demo.SetVertex(0, 70, 90);
			demo.SetVertex(1, 380, 190);
			demo.SetVertex(2, 120, 330);
			demo.PixelSizeSlider.Value = 16;

			await AggReference.Check(Render(demo), "aa_demo_600x400_size16_moved");
		}

		[Test]
		public async Task DraggingInsideTheTriangleMovesEveryVertex()
		{
			var demo = new AaDemo();
			demo.OnMouseDown(200, 200, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(210, 205, AggInputFlags.MouseLeft);
			demo.OnMouseUp(210, 205, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = new AaDemo();
			moved.SetVertex(0, 67, 105);
			moved.SetVertex(1, 379, 175);
			moved.SetVertex(2, 153, 315);

			await Assert.That(Render(demo).GetBuffer()).IsEquivalentTo(Render(moved).GetBuffer());
		}

		private static ImageBuffer Render(AaDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
