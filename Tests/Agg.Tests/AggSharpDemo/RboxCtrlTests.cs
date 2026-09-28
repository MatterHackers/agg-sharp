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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// rbox_ctrl against C++ AGG's (demo_rbox_ctrl.cpp: one rbox on white), and its input.
	public class RboxCtrlTests
	{
		[Test]
		public async Task DefaultRboxMatchesCppAgg()
		{
			await AggReference.Check(Render(NewRbox(1)), "rbox_ctrl_240x160");
		}

		[Test]
		public async Task StyledRboxMatchesCppAgg()
		{
			RboxCtrl rbox = NewRbox(3);
			rbox.SetBorderWidth(2.0, 3.0);
			rbox.SetTextSize(11.0, 7.0);
			rbox.TextThickness = 1.0;
			rbox.BackgroundColor = new Color(230, 242, 255);
			rbox.ActiveColor = new Color(0, 77, 153);

			await AggReference.Check(Render(rbox), "rbox_ctrl_240x160_styled");
		}

		[Test]
		public async Task PressingARingSelectsItAndArrowsCycle()
		{
			RboxCtrl rbox = NewRbox(1);

			// Item 2's ring: xs1 + dy / 1.3, ys1 + dy * 2 + dy / 1.3 with dy = 18.
			await Assert.That(rbox.OnMouseButtonDown(11 + (18 / 1.3), 11 + 36 + (18 / 1.3))).IsTrue();
			await Assert.That(rbox.CurrentItem).IsEqualTo(2);

			rbox.OnArrowKeys(false, false, false, true);
			rbox.OnArrowKeys(false, false, false, true);
			await Assert.That(rbox.CurrentItem).IsEqualTo(0);
			rbox.OnArrowKeys(true, false, false, false);
			await Assert.That(rbox.CurrentItem).IsEqualTo(3);
		}

		private static RboxCtrl NewRbox(int currentItem)
		{
			var rbox = new RboxCtrl(10.0, 10.0, 150.0, 120.0, false);
			rbox.AddItem("Zero");
			rbox.AddItem("One");
			rbox.AddItem("Two");
			rbox.AddItem("Three");
			rbox.CurrentItem = currentItem;
			return rbox;
		}

		private static ImageBuffer Render(RboxCtrl rbox)
		{
			var frame = new ImageBuffer(240, 160);
			Graphics2D graphics = frame.NewGraphics2D();
			graphics.Clear(Color.White);
			rbox.Render(graphics);
			return frame;
		}
	}
}
