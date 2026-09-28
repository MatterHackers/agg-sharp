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
	// graph_test.cpp's port against C++ AGG (demo_graph_test.cpp).
	public class GraphTestDemoTests
	{
		/// <summary>Solid lines with arrowheads, width 2, over the gradient nodes.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new GraphTestDemo()), "graph_test_700x530");
		}

		/// <summary>Draft mode: renderer_primitives' ellipses and aliased lines through conv_marker_adaptor.</summary>
		[Test]
		public async Task DraftLinesMatchCppAgg()
		{
			await AggReference.Check(Render(Demo(0, draft: true)), "graph_test_700x530_lines_draft");
		}

		[Test]
		public async Task DraftTranslucentCurvesMatchCppAgg()
		{
			await AggReference.Check(Render(Demo(1, draft: true, translucent: true)), "graph_test_700x530_curves_draft_translucent");
		}

		[Test]
		public async Task DraftDashesMatchCppAgg()
		{
			await AggReference.Check(Render(Demo(2, draft: true)), "graph_test_700x530_dashes_draft");
		}

		/// <summary>Dashed curves stroked 3.5 wide, with bigger gradient nodes.</summary>
		[Test]
		public async Task WideDashesMatchCppAgg()
		{
			await AggReference.Check(Render(Demo(2, width: 3.5)), "graph_test_700x530_dashes_wide");
		}

		[Test]
		public async Task TranslucentAntiAliasedPolygonsMatchCppAgg()
		{
			await AggReference.Check(Render(Demo(3, translucent: true)), "graph_test_700x530_polygons_aa_translucent");
		}

		/// <summary>Polygons Bin: gamma_threshold(0.5) and renderer_scanline_bin_solid.</summary>
		[Test]
		public async Task ThresholdPolygonsMatchCppAgg()
		{
			await AggReference.Check(Render(Demo(4)), "graph_test_700x530_polygons_bin");
		}

		/// <summary>
		/// C++ builds the type box at (-1, -1, -1, -1): its items still sit in the corner and still take a click
		/// on their radio button, though the box itself is empty.
		/// </summary>
		[Test]
		public async Task TypeBoxItemsTakeClicksInTheCorner()
		{
			var demo = new GraphTestDemo();

			// Item 2's button is centered at (16 / 1.3, 16 * 2 + 16 / 1.3): the box's inside starts at (0, 0).
			demo.OnMouseDown(12, 44, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);

			await Assert.That(demo.TypeRbox.CurrentItem).IsEqualTo(2);
		}

		private static GraphTestDemo Demo(int type, bool draft = false, bool translucent = false, double width = 2.0)
		{
			var demo = new GraphTestDemo();
			demo.TypeRbox.CurrentItem = type;
			demo.DraftCbox.Checked = draft;
			demo.TranslucentCbox.Checked = translucent;
			demo.WidthSlider.Value = width;
			return demo;
		}

		private static ImageBuffer Render(GraphTestDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
