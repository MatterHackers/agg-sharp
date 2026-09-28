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
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <c>Graphics2DGpu.Clear</c> against a real device: as the software <see cref="ImageGraphics2D"/>, it fills
	/// the clip rect (intersected with the rect given, in destination pixels) whatever the transform.
	/// </summary>
	[NotInParallel]
	public class GpuClearTests
	{
		private static readonly RectangleDouble Clip = new RectangleDouble(100, 80, 300, 240);

		/// <summary>AggSharpDemo's frame for a y-down demo: y flipped over the demo's height, then placed on the
		/// page (AggDemoView.DrawDirect). Clear there left the frame unfilled.</summary>
		private static readonly Affine YFlipped = Affine.NewScaling(1, -1) * Affine.NewTranslation(0, 160) * Affine.NewTranslation(100, 80);

		[Test]
		public async Task ClearFillsTheClipRectUnderAYFlip()
		{
			await AssertFillsExactly(YFlipped, graphics => graphics.Clear(Color.Red), Clip);
		}

		[Test]
		public async Task ClearFillsTheClipRectUnderAScale()
		{
			await AssertFillsExactly(Affine.NewScaling(2) * Affine.NewTranslation(30, 20), graphics => graphics.Clear(Color.Red), Clip);
		}

		[Test]
		public async Task ClearRectIsInDestinationPixelsUnderAYFlip()
		{
			var rect = new RectangleDouble(150, 50, 250, 200);
			await AssertFillsExactly(YFlipped, graphics => graphics.Clear(rect, Color.Red), new RectangleDouble(150, 80, 250, 200));
		}

		/// <summary>Draws <paramref name="clear"/> under <paramref name="transform"/> with <see cref="Clip"/> set, on
		/// the GPU and in software, and checks each is red exactly inside <paramref name="expected"/>.</summary>
		private static async Task AssertFillsExactly(Affine transform, System.Action<Graphics2D> clear, RectangleDouble expected)
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 1));
			gpuGraphics.SetClippingRect(Clip);
			gpuGraphics.SetTransform(transform);
			clear(gpuGraphics);
			var gpuImage = await capture.CaptureAsync();

			var softwareImage = new ImageBuffer(gpuImage.Width, gpuImage.Height, 32, new BlenderBGRA());
			var softwareGraphics = softwareImage.NewGraphics2D();
			softwareGraphics.Clear(Color.Black);
			softwareGraphics.SetClippingRect(Clip);
			softwareGraphics.SetTransform(transform);
			clear(softwareGraphics);

			await Assert.That(RedBounds(softwareImage)).IsEqualTo(expected);
			await Assert.That(RedBounds(gpuImage)).IsEqualTo(expected);
		}

		/// <summary>The bounds of the red pixels, or an empty rect when there are none.</summary>
		private static RectangleDouble RedBounds(ImageBuffer image)
		{
			int left = int.MaxValue, bottom = int.MaxValue, right = int.MinValue, top = int.MinValue;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					var c = image.GetPixel(x, y);
					if (c.red > 200 && c.green < 60 && c.blue < 60)
					{
						left = System.Math.Min(left, x);
						bottom = System.Math.Min(bottom, y);
						right = System.Math.Max(right, x + 1);
						top = System.Math.Max(top, y + 1);
					}
				}
			}

			return left == int.MaxValue ? new RectangleDouble(0, 0, 0, 0) : new RectangleDouble(left, bottom, right, top);
		}
	}
}
