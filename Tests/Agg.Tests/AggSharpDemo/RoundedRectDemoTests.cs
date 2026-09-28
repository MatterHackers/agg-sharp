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
	// rounded_rect.cpp's port against C++ AGG (demo_rounded_rect.cpp), plus its cbox.
	public class RoundedRectDemoTests
	{
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new RoundedRectDemo()), "rounded_rect_600x400");
		}

		/// <summary>Moved handles, a fractional radius and offset, and the checkbox on (so its X draws).</summary>
		[Test]
		public async Task WhiteOnBlackFrameMatchesCppAgg()
		{
			var demo = new RoundedRectDemo();
			demo.SetHandle(0, 120, 80);
			demo.SetHandle(1, 480, 330);
			demo.RadiusSlider.Value = 12.5;
			demo.OffsetSlider.Value = 0.3;
			demo.WhiteOnBlack.Checked = true;

			await AggReference.Check(Render(demo), "rounded_rect_600x400_whiteonblack");
		}

		[Test]
		public async Task ClickingTheCheckboxTogglesIt()
		{
			var demo = new RoundedRectDemo();

			demo.OnMouseDown(15, 55, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			demo.OnMouseUp(15, 55, AggInputFlags.MouseLeft, AggInputFlags.None);
			await Assert.That(demo.WhiteOnBlack.Checked).IsTrue();

			demo.OnMouseDown(15, 55, AggInputFlags.MouseLeft, AggInputFlags.MouseLeft);
			await Assert.That(demo.WhiteOnBlack.Checked).IsFalse();
		}

		private static ImageBuffer Render(RoundedRectDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
