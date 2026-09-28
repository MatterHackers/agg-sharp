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
	// blend_color.cpp's port against C++ AGG (demo_blend_color.cpp).
	public class BlendColorDemoTests
	{
		/// <summary>The default frame: the shadow blurred at radius 15 and coloured through the colour table.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new BlendColorDemo()), "blend_color_440x330");
		}

		/// <summary>One colour at a fractional radius (rounded to 8), the shadow hanging off the window's bottom left.</summary>
		[Test]
		public async Task SingleColorOffTheEdgeMatchesCppAgg()
		{
			var demo = new BlendColorDemo();
			demo.MethodRbox.CurrentItem = 0;
			demo.RadiusSlider.Value = 8.4;
			demo.ShadowQuad.SetPoint(0, -30, -20);
			demo.ShadowQuad.SetPoint(1, 250, 40);
			demo.ShadowQuad.SetPoint(2, 400, 260);
			demo.ShadowQuad.SetPoint(3, 90, 310);

			await AggReference.Check(Render(demo), "blend_color_440x330_single_color_radius8_off_edge");
		}

		[Test]
		public async Task DraggingAShadowCornerMovesItAndRedraws()
		{
			var demo = new BlendColorDemo();
			int invalidations = 0;
			demo.Invalidated += (s, e) => invalidations++;
			var corner = demo.ShadowQuad.GetPoint(2);
			int x = (int)corner.X;
			int y = (int)corner.Y;

			demo.OnMouseDown(x, y, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseMove(x - 20, y + 15, AggInputFlags.MouseLeft);
			demo.OnMouseUp(x - 20, y + 15, AggInputFlags.MouseLeft, AggInputFlags.None);

			var moved = demo.ShadowQuad.GetPoint(2);
			await Assert.That(moved.X - corner.X).IsEqualTo(-20.0);
			await Assert.That(moved.Y - corner.Y).IsEqualTo(15.0);
			await Assert.That(invalidations).IsGreaterThan(0);
		}

		private static ImageBuffer Render(BlendColorDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
