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
	// perspective.cpp's port against C++ AGG (demo_perspective.cpp), and dragging its quad.
	public class PerspectiveDemoTests
	{
		/// <summary>
		/// The default frame: the mirrored lion and ellipse through trans_bilinear onto the lion's own bounds
		/// moved to the middle, then the quad tool and the rbox.
		/// </summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new PerspectiveDemo()), "perspective_600x600");
		}

		/// <summary>A warped quad through trans_perspective.</summary>
		[Test]
		public async Task WarpedPerspectiveFrameMatchesCppAgg()
		{
			var demo = new PerspectiveDemo();
			demo.Quad.SetPoint(0, 120, 110);
			demo.Quad.SetPoint(1, 520, 160);
			demo.Quad.SetPoint(2, 470, 520);
			demo.Quad.SetPoint(3, 90, 430);
			demo.TransTypeRbox.CurrentItem = 1;

			await AggReference.Check(Render(demo), "perspective_600x600_perspective_warped");
		}

		[Test]
		public async Task DraggingACornerMovesItAndRedraws()
		{
			var demo = new PerspectiveDemo();
			int invalidations = 0;
			demo.Invalidated += (s, e) => invalidations++;
			var corner = demo.Quad.GetPoint(2);
			int x = (int)corner.X;
			int y = (int)corner.Y;

			demo.OnMouseDown(x, y, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(x + 30, y + 20, AggInputFlags.MouseLeft);
			demo.OnMouseUp(x + 30, y + 20, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = demo.Quad.GetPoint(2);
			await Assert.That(moved.X - corner.X).IsEqualTo(30.0);
			await Assert.That(moved.Y - corner.Y).IsEqualTo(20.0);
			await Assert.That(invalidations).IsGreaterThan(0);
		}

		private static ImageBuffer Render(PerspectiveDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
