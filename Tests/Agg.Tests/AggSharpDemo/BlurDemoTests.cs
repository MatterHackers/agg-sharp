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
	// blur.cpp's port against C++ AGG (demo_blur.cpp), with each blur method.
	public class BlurDemoTests
	{
		/// <summary>The default frame: the shadow stack-blurred at radius 15, the quad, the glyph and the ctrls.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new BlurDemo()), "blur_440x330");
		}

		/// <summary>Stack blur at a fractional radius (rounded to 4) with the shadow quad moved.</summary>
		[Test]
		public async Task StackBlurMovedShadowMatchesCppAgg()
		{
			var demo = new BlurDemo();
			demo.RadiusSlider.Value = 4.2;
			demo.ShadowQuad.SetPoint(0, 160, 60);
			demo.ShadowQuad.SetPoint(1, 350, 110);
			demo.ShadowQuad.SetPoint(2, 330, 300);
			demo.ShadowQuad.SetPoint(3, 190, 250);

			await AggReference.Check(Render(demo), "blur_440x330_stack_radius4_moved");
		}

		/// <summary>The recursive (Gaussian) blur at radius 25.</summary>
		[Test]
		public async Task RecursiveBlurMatchesCppAgg()
		{
			var demo = new BlurDemo();
			demo.MethodRbox.CurrentItem = 1;
			demo.RadiusSlider.Value = 25;

			await AggReference.Check(Render(demo), "blur_440x330_recursive_radius25");
		}

		/// <summary>"Channels": only red and blue blurred, at radius 32 - its bounds clipped by the window top.</summary>
		[Test]
		public async Task RedAndBlueChannelsMatchCppAgg()
		{
			var demo = new BlurDemo();
			demo.MethodRbox.CurrentItem = 2;
			demo.RadiusSlider.Value = 32;
			demo.RedCbox.Checked = true;
			demo.GreenCbox.Checked = false;
			demo.BlueCbox.Checked = true;

			await AggReference.Check(Render(demo), "blur_440x330_channels_red_blue_radius32");
		}

		[Test]
		public async Task DraggingAShadowCornerMovesItAndRedraws()
		{
			var demo = new BlurDemo();
			int invalidations = 0;
			demo.Invalidated += (s, e) => invalidations++;
			var corner = demo.ShadowQuad.GetPoint(1);
			int x = (int)corner.X;
			int y = (int)corner.Y;

			demo.OnMouseDown(x, y, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(x + 20, y + 15, AggInputFlags.MouseLeft);
			demo.OnMouseUp(x + 20, y + 15, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = demo.ShadowQuad.GetPoint(1);
			await Assert.That(moved.X - corner.X).IsEqualTo(20.0);
			await Assert.That(moved.Y - corner.Y).IsEqualTo(15.0);
			await Assert.That(invalidations).IsGreaterThan(0);
		}

		private static ImageBuffer Render(BlurDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
