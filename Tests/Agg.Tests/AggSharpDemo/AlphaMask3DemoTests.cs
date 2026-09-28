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
	// alpha_mask3.cpp's port against C++ AGG (demo_alpha_mask3.cpp).
	public class AlphaMask3DemoTests
	{
		/// <summary>The default frame: the spiral through a mask of Great Britain (AND).</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new AlphaMask3Demo()), "alpha_mask3_640x520");
		}

		/// <summary>The open path through a mask of the two simple paths, the first path dragged.</summary>
		[Test]
		public async Task PathsFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(0, 0, 300, 250)), "alpha_mask3_640x520_paths_moved");
		}

		/// <summary>The closed stroke with the path (one contour closed counter-clockwise) cut out of it (SUB).</summary>
		[Test]
		public async Task ClosedStrokeSubFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(1, 1)), "alpha_mask3_640x520_closed_stroke_sub");
		}

		/// <summary>The arrows with Great Britain cut out of them (SUB), the arrows dragged.</summary>
		[Test]
		public async Task ArrowsSubFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(2, 1, 360, 230)), "alpha_mask3_640x520_arrows_sub_moved");
		}

		/// <summary>The glyph through a mask of the spiral (AND), the spiral dragged.</summary>
		[Test]
		public async Task SpiralGlyphFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(4, 0, 250, 280)), "alpha_mask3_640x520_spiral_glyph_moved");
		}

		private static AlphaMask3Demo Configure(int polygons, int operation, double? x = null, double? y = null)
		{
			var demo = new AlphaMask3Demo();
			demo.PolygonsRbox.CurrentItem = polygons;
			demo.OperationRbox.CurrentItem = operation;
			if (x != null && y != null)
			{
				demo.MoveTo(x.Value, y.Value);
			}

			return demo;
		}

		private static ImageBuffer Render(AlphaMask3Demo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
