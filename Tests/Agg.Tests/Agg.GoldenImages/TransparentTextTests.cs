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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <c>Graphics2D.DrawString</c> with a fully transparent colour draws nothing, on the GPU and in software. It
	/// used to read alpha 0 as "no colour given" and draw black; only an omitted colour means black.
	/// </summary>
	[NotInParallel]
	public class TransparentTextTests
	{
		[Test]
		public async Task TransparentTextDrawsNothingInSoftware()
		{
			var image = new ImageBuffer(200, 60, 32, new BlenderBGRA());
			var graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			graphics.DrawString("Hidden text", 10, 20, 20, color: Color.White.WithAlpha(0));

			await Assert.That(CountNonWhite(image)).IsEqualTo(0);
		}

		[Test]
		public async Task OmittedColorStillDrawsBlack()
		{
			var image = new ImageBuffer(200, 60, 32, new BlenderBGRA());
			var graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			graphics.DrawString("Shown text", 10, 20, 20);

			await Assert.That(CountNonWhite(image)).IsGreaterThan(50);
		}

		[Test]
		public async Task TransparentTextDrawsNothingOnTheGpu()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			graphics.DrawString("Hidden text", 10, 20, 20, color: Color.White.WithAlpha(0));
			var image = await capture.CaptureAsync();

			await Assert.That(CountNonWhite(image)).IsEqualTo(0);
		}

		private static int CountNonWhite(ImageBuffer image)
		{
			int count = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (pixel.red < 250 || pixel.green < 250 || pixel.blue < 250)
					{
						count++;
					}
				}
			}

			return count;
		}
	}
}
