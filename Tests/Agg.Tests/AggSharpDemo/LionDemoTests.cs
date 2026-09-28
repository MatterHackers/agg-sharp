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
	// lion.cpp's port: its reference render against C++ AGG, and the input routing that puts the slider first.
	public class LionDemoTests
	{
		/// <summary>
		/// The default frame - lion at alpha 0.1 plus the alpha slider (demo_lion.cpp) - rendered through the
		/// software reference path is byte-identical to C++ AGG's bgr24 render.
		/// </summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			var demo = new LionDemo();
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);

			await AggReference.Check(frame, "lion_512x400");
		}

		[Test]
		public async Task DraggingTheSliderSetsAlphaAndLeavesTheLionAlone()
		{
			var demo = new LionDemo();
			int invalidations = 0;
			demo.Invalidated += (s, e) => invalidations++;
			ImageBuffer before = Render(demo);

			// The pointer sits at xs1 + (xs2 - xs1) * 0.1 = 6 + 500 * 0.1 = 56, y 8.5.
			demo.OnMouseDown(56, 8, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(256, 8, AggInputFlags.MouseLeft);
			demo.OnMouseUp(256, 8, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.AlphaSlider.Value).IsEqualTo(0.5);
			await Assert.That(invalidations).IsGreaterThan(0);

			// Back at 0.1 the frame is the default one again: the drag never reached the lion.
			demo.AlphaSlider.Value = 0.1;
			await Assert.That(AggReference.Compare(before, Render(demo)).Identical).IsTrue();
		}

		[Test]
		public async Task DraggingOutsideTheSliderTurnsTheLion()
		{
			var demo = new LionDemo();
			ImageBuffer before = Render(demo);

			// 100 pixels straight above the middle: a quarter turn at scale 1.
			demo.OnMouseDown(256, 300, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);

			await Assert.That(AggReference.Compare(before, Render(demo)).Identical).IsFalse();
			await Assert.That(demo.AlphaSlider.Value).IsEqualTo(0.1);
		}

		[Test]
		public async Task ReleasingTheRightButtonMidSliderDragKeepsDragging()
		{
			var demo = new LionDemo();

			demo.OnMouseDown(56, 8, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseDown(56, 8, AggInputFlags.MouseRight, AggInputFlags.MouseLeft | AggInputFlags.MouseRight);
			demo.OnMouseUp(56, 8, AggInputFlags.MouseRight, AggInputFlags.MouseLeft);
			demo.OnMouseMove(256, 8, AggInputFlags.MouseLeft);
			demo.OnMouseUp(256, 8, AggInputFlags.MouseLeft, AggInputFlags.None);

			await Assert.That(demo.AlphaSlider.Value).IsEqualTo(0.5);
		}

		[Test]
		public async Task ALeftPressOnTheSliderWhileRightIsHeldDragsTheSliderAndLeavesTheLion()
		{
			var demo = new LionDemo();
			demo.OnMouseDown(300, 200, AggInputFlags.MouseRight, AggInputFlags.MouseRight);

			demo.OnMouseDown(56, 8, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft | AggInputFlags.MouseRight);
			demo.OnMouseMove(256, 8, AggInputFlags.MouseLeft | AggInputFlags.MouseRight);
			demo.OnMouseUp(256, 8, AggInputFlags.MouseLeft, AggInputFlags.MouseRight);

			await Assert.That(demo.AlphaSlider.Value).IsEqualTo(0.5);

			// The lion keeps the skew of the right press alone.
			var expected = new LionDemo();
			expected.OnMouseDown(300, 200, AggInputFlags.MouseRight, AggInputFlags.MouseRight);
			expected.AlphaSlider.Value = 0.5;
			await Assert.That(AggReference.Compare(Render(expected), Render(demo)).Identical).IsTrue();
		}

		private static ImageBuffer Render(LionDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
