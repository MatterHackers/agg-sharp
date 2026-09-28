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
	// scanline_boolean2.cpp's port against C++ AGG (demo_scanline_boolean2.cpp).
	public class ScanlineBoolean2DemoTests
	{
		/// <summary>The default frame: Great Britain AND the spiral, non-zero, through scanline_u8 stores.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ScanlineBoolean2Demo()), "scanline_boolean2_655x520");
		}

		/// <summary>Great Britain saddle-xor the arrows, even-odd, through scanline_p8 stores, the arrows dragged.</summary>
		[Test]
		public async Task ArrowsSaddleXorPackedFrameMatchesCppAgg()
		{
			var demo = new ScanlineBoolean2Demo();
			demo.PolygonsRbox.CurrentItem = 2;
			demo.FillRuleRbox.CurrentItem = 0;
			demo.ScanlineTypeRbox.CurrentItem = 0;
			demo.OperationRbox.CurrentItem = 4;
			demo.MoveTo(360, 230);

			await AggReference.Check(Render(demo), "scanline_boolean2_655x520_arrows_evenodd_p_xor_saddle_moved");
		}

		/// <summary>The two simple paths ORed, non-zero, scanline_u8 stores, the first path dragged.</summary>
		[Test]
		public async Task PathsOrFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(0, 1, 1, 1, 300, 250)), "scanline_boolean2_655x520_paths_or_moved");
		}

		/// <summary>The closed stroke subtracted from the path, even-odd, scanline_p8 stores.</summary>
		[Test]
		public async Task ClosedStrokeAMinusBFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(1, 0, 0, 5)), "scanline_boolean2_655x520_closed_stroke_evenodd_p_a_minus_b");
		}

		/// <summary>The spiral linear-xor the glyph, non-zero, scanline_u8 stores, the spiral dragged.</summary>
		[Test]
		public async Task SpiralGlyphXorFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(4, 1, 1, 3, 250, 280)), "scanline_boolean2_655x520_spiral_glyph_xor_linear_moved");
		}

		/// <summary>The spiral minus Great Britain through scanline_bin stores and the binary renderer.</summary>
		[Test]
		public async Task SpiralBinBMinusAFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(Configure(3, 1, 2, 6)), "scanline_boolean2_655x520_spiral_bin_b_minus_a");
		}

		private static ScanlineBoolean2Demo Configure(int polygons, int fillRule, int scanlineType, int operation, double? x = null, double? y = null)
		{
			var demo = new ScanlineBoolean2Demo();
			demo.PolygonsRbox.CurrentItem = polygons;
			demo.FillRuleRbox.CurrentItem = fillRule;
			demo.ScanlineTypeRbox.CurrentItem = scanlineType;
			demo.OperationRbox.CurrentItem = operation;
			if (x != null && y != null)
			{
				demo.MoveTo(x.Value, y.Value);
			}

			return demo;
		}

		private static ImageBuffer Render(ScanlineBoolean2Demo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
