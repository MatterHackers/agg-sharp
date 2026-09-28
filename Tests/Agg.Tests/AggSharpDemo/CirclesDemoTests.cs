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
	// circles.cpp's port against C++ AGG (demo_circles.cpp): 10000 eight-step circles scattered by the MSVC
	// rand() generator both sides use, the scale ctrl, the two sliders and the gsv_text count.
	public class CirclesDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new CirclesDemo()), "circles_400x400");
		}

		/// <summary>Scale 0.4..0.6, selectivity 0.2, size 0.3, after three idle jitters.</summary>
		[Test]
		public async Task NarrowSmallJitteredFrameMatchesCppAgg()
		{
			var demo = new CirclesDemo();
			demo.ScaleZ.Value2 = 0.6;
			demo.ScaleZ.Value1 = 0.4;
			demo.SelectivitySlider.Value = 0.2;
			demo.SizeSlider.Value = 0.3;
			for (int i = 0; i < 3; i++)
			{
				demo.OnIdle();
			}

			await AggReference.Check(Render(demo), "circles_400x400_narrow_small_jittered");
		}

		/// <summary>Dragging the bar between the scale's pointers moves both ends by the same amount.</summary>
		[Test]
		public async Task DraggingTheScaleBarMovesTheRange()
		{
			var demo = new CirclesDemo();

			// The bar spans x 122.4 (0.3) to 277.6 (0.7) of the 388 from 6 to 394; 39 pixels is 39/388.
			demo.OnMouseDown(200, 8, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(239, 8, AggInputFlags.MouseLeft);
			demo.OnMouseUp(239, 8, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.ScaleZ.Value1).IsEqualTo(0.3 + (39.0 / 388)).Within(1e-12);
			await Assert.That(demo.ScaleZ.Value2).IsEqualTo(0.7 + (39.0 / 388)).Within(1e-12);
		}

		/// <summary>A right press off the ctrls starts the jitter animation, and another stops it.</summary>
		[Test]
		public async Task RightClickTogglesAnimation()
		{
			var demo = new CirclesDemo();
			demo.OnMouseDown(200, 200, AggInputFlags.MouseRight, AggInputFlags.MouseRight);
			await Assert.That(demo.WaitMode).IsFalse();

			demo.OnMouseDown(200, 200, AggInputFlags.MouseRight, AggInputFlags.MouseRight);
			await Assert.That(demo.WaitMode).IsTrue();
		}

		private static ImageBuffer Render(CirclesDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
