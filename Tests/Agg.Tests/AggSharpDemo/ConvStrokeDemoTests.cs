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
	// conv_stroke.cpp's port against C++ AGG (demo_conv_stroke.cpp): conv_stroke's joins, caps and miter limit,
	// a dash of a stroke stroked again, and the two rboxes and two sliders.
	public class ConvStrokeDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new ConvStrokeDemo()), "conv_stroke_500x330");
		}

		/// <summary>Miter joins, square caps, width 34, miter limit 3, on a moved triangle whose miter reaches past the canvas.</summary>
		[Test]
		public async Task MiterSquareWideMovedFrameMatchesCppAgg()
		{
			var demo = new ConvStrokeDemo();
			demo.JoinRbox.CurrentItem = 0;
			demo.CapRbox.CurrentItem = 1;
			demo.WidthSlider.Value = 34;
			demo.MiterLimitSlider.Value = 3;
			demo.SetVertex(0, 120, 40);
			demo.SetVertex(1, 460, 200);
			demo.SetVertex(2, 200, 300);

			await AggReference.Check(Render(demo), "conv_stroke_500x330_miter_square_wide_moved");
		}

		/// <summary>A press inside the triangle drags all three corners.</summary>
		[Test]
		public async Task DraggingInsideTheTriangleMovesIt()
		{
			var demo = new ConvStrokeDemo();
			demo.OnMouseDown(300, 180, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(310, 190, AggInputFlags.MouseLeft);
			demo.OnMouseUp(310, 190, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.GetVertex(0)).IsEqualTo(new Vector2(167, 70));
			await Assert.That(demo.GetVertex(2)).IsEqualTo(new Vector2(253, 320));
		}

		private static ImageBuffer Render(ConvStrokeDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
