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
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="IChannelMaskGraphics"/> on <c>Graphics2DGpu</c> against a real device: black drawn into one
	/// channel at a time darkens only that channel, so overlapping circles mix per channel, like inks.
	/// </summary>
	[NotInParallel]
	public class GpuChannelMaskTests
	{
		[Test]
		public async Task OverlappingChannelCirclesMixPerChannel()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			var masked = (IChannelMaskGraphics)graphics;
			int y = (int)graphics.Height / 2;

			// Red channel: a circle on the left; green: on the right; they overlap in the middle.
			masked.DrawWithChannelMask(ColorChannels.Red, () => graphics.Render(new Ellipse(80, y, 40, 40), Color.Black));
			masked.DrawWithChannelMask(ColorChannels.Green, () => graphics.Render(new Ellipse(130, y, 40, 40), Color.Black));

			// Drawing afterwards writes every channel again.
			graphics.Render(new Ellipse(250, y, 10, 10), new Color(0, 0, 255));

			var image = await capture.CaptureAsync();

			// Every sample lies on the middle row, a pixel at most from the circles' centers however the
			// capture is flipped.
			await Assert.That(Rgb(image.GetPixel(60, y))).IsEqualTo("0,255,255");
			await Assert.That(Rgb(image.GetPixel(105, y))).IsEqualTo("0,0,255");
			await Assert.That(Rgb(image.GetPixel(155, y))).IsEqualTo("255,0,255");
			await Assert.That(Rgb(image.GetPixel(200, y))).IsEqualTo("255,255,255");
			await Assert.That(Rgb(image.GetPixel(250, y))).IsEqualTo("0,0,255");
		}

		private static string Rgb(Color c) => $"{c.red},{c.green},{c.blue}";
	}
}
