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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <c>Graphics2DGpu.Render(IImageByte, x, y, angle, scaleX, scaleY)</c> against a real device: a rotated
	/// image lands where the software <see cref="ImageGraphics2D"/> puts it, turning the same way.
	/// </summary>
	[NotInParallel]
	public class GpuRotatedImageTests
	{
		[Test]
		public async Task RotatedImageTurnsTheSameWayAsSoftware()
		{
			// A white bar with a red marker in its top-right corner: a turn the wrong way sends the marker
			// well below where the software renderer puts it.
			var source = new ImageBuffer(40, 20, 32, new BlenderBGRA());
			source.NewGraphics2D().Clear(Color.White);
			source.NewGraphics2D().FillRectangle(32, 12, 40, 20, Color.Red);

			const double x = 200;
			const double y = 150;
			double angle = 30 * Math.PI / 180;

			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 1));
			gpuGraphics.Render(source, x, y, angle, 1, 1);
			var gpuImage = await capture.CaptureAsync();

			var softwareImage = new ImageBuffer(gpuImage.Width, gpuImage.Height, 32, new BlenderBGRA());
			var softwareGraphics = softwareImage.NewGraphics2D();
			softwareGraphics.Clear(Color.Black);
			softwareGraphics.Render(source, x, y, angle, 1, 1);

			var (softwareX, softwareY) = RedCentroid(softwareImage);
			var (gpuX, gpuY) = RedCentroid(gpuImage);

			await Assert.That(Math.Abs(gpuX - softwareX)).IsLessThanOrEqualTo(1.0);
			await Assert.That(Math.Abs(gpuY - softwareY)).IsLessThanOrEqualTo(1.0);
		}

		private static (double X, double Y) RedCentroid(ImageBuffer image)
		{
			double sumX = 0, sumY = 0;
			int count = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					var c = image.GetPixel(x, y);
					if (c.red > 200 && c.green < 60 && c.blue < 60)
					{
						sumX += x;
						sumY += y;
						count++;
					}
				}
			}

			if (count == 0)
			{
				throw new InvalidOperationException("No marker pixels were drawn.");
			}

			return (sumX / count, sumY / count);
		}
	}
}
