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

using System;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A faded backbuffer (<see cref="GuiWidget.BackbufferOpacity"/> below 1) composited by the CPU onto a destination
	/// labelled premultiplied - a parent backbuffer, an image snapshot - lands half way at half opacity. The CPU blit
	/// reads the faded copy straight (ImageGraphics2D.StraightOverDestination), so the copy has to be straight: a
	/// premultiplied fade (colour and alpha both halved) read straight halves the colour again, white over black at
	/// 0.5 landing at 64 instead of 128.
	/// </summary>
	public class FadedBackbufferCompositeTests
	{
		[Test]
		public async Task HalfFadedWhiteOverBlackLandsHalfWay()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				// Off so the composite is the plain RGBA blit this is about, not an LCD one.
				LcdRenderSettings.Enabled = false;

				var parent = new GuiWidget(20, 20);
				parent.AddChild(new GuiWidget(20, 20)
				{
					DoubleBuffer = true,
					BackgroundColor = Color.White,
					BackbufferOpacity = 0.5,
				});
				parent.PerformLayout();

				var destination = new ImageBuffer(20, 20, 32, new BlenderPreMultBGRA());
				Graphics2D graphics = destination.NewGraphics2D();
				graphics.Clear(Color.Black);
				parent.OnDraw(graphics);

				Color pixel = destination.GetPixel(10, 10);
				await Assert.That(Math.Abs(pixel.red - 128)).IsLessThanOrEqualTo(2).Because($"got {pixel.red}");
				await Assert.That(pixel.alpha).IsEqualTo((byte)255);
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}
	}
}
