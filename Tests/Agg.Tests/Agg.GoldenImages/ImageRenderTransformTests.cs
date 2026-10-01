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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The <see cref="Graphics2D.Render(IImageByte, double, double, double, double, double)"/> contract: the
	/// image lands where a vector fill of its rectangle would, under the full graphics transform composed
	/// with the image's own placement - on the software rasterizer and on the real WebGPU device alike.
	/// </summary>
	[NotInParallel]
	public class ImageRenderTransformTests
	{
		private const int FrameSize = 96;

		private const int ImageWidth = 16;

		private const int ImageHeight = 8;

		/// <summary>One placement of the image under one graphics transform.</summary>
		public record Placement(string Name, Affine GraphicsTransform, double X, double Y, double AngleRadians, double ScaleX, double ScaleY);

		public static IEnumerable<Func<Placement>> Placements()
		{
			yield return () => new Placement("identity", Affine.NewIdentity(), 5, 6, 0, 1, 1);
			yield return () => new Placement("uniform scale 2", Affine.NewScaling(2), 5, 6, 0, 1, 1);
			yield return () => new Placement("translate and scale", Affine.NewScaling(1.5, 2) * Affine.NewTranslation(10, 4), 3, 2, 0, 1.25, 1);
			yield return () => new Placement("rotation", Affine.NewRotation(MathHelper.DegreesToRadians(30)) * Affine.NewTranslation(40, 5), 4, 3, 0.2, 1.5, 1.5);
			yield return () => new Placement("y flip", Affine.NewScaling(1, -1) * Affine.NewTranslation(10, 80), 6, 20, 0.3, 2, 2);
		}

		[Test]
		[MethodDataSource(nameof(Placements))]
		public async Task ImageFollowsTheTransformLikeAVectorFill(Placement placement)
		{
			var reference = VectorReference(placement);

			var software = NewFrame();
			var softwareGraphics = software.NewGraphics2D();
			softwareGraphics.SetTransform(placement.GraphicsTransform);
			softwareGraphics.Render(TwoToneImage(), placement.X, placement.Y, placement.AngleRadians, placement.ScaleX, placement.ScaleY);
			await AssertMatchesReference(reference, software, $"software, {placement.Name}");

			using var capture = WebGpuOffscreenCapture.Create(FrameSize, FrameSize);
			var gpuGraphics = capture.BeginWidgetFrame(ColorF.White);
			gpuGraphics.SetTransform(placement.GraphicsTransform);
			gpuGraphics.Render(TwoToneImage(), placement.X, placement.Y, placement.AngleRadians, placement.ScaleX, placement.ScaleY);
			var gpu = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			await AssertMatchesReference(reference, gpu, $"gpu, {placement.Name}");
		}

		/// <summary>Left half red, right half blue, so a flip or a turn shows as well as a size.</summary>
		private static ImageBuffer TwoToneImage()
		{
			var image = new ImageBuffer(ImageWidth, ImageHeight);
			var graphics = image.NewGraphics2D();
			graphics.FillRectangle(0, 0, ImageWidth / 2, ImageHeight, Color.Red);
			graphics.FillRectangle(ImageWidth / 2, 0, ImageWidth, ImageHeight, Color.Blue);
			return image;
		}

		private static ImageBuffer NewFrame()
		{
			var frame = new ImageBuffer(FrameSize, FrameSize);
			frame.NewGraphics2D().Clear(Color.White);
			return frame;
		}

		/// <summary>The image's two halves drawn as vector rectangles under the same composed transform.</summary>
		private static ImageBuffer VectorReference(Placement placement)
		{
			var imageToSurface = Affine.NewScaling(placement.ScaleX, placement.ScaleY)
				* Affine.NewRotation(placement.AngleRadians)
				* Affine.NewTranslation(placement.X, placement.Y)
				* placement.GraphicsTransform;

			var frame = NewFrame();
			var graphics = frame.NewGraphics2D();
			graphics.SetTransform(imageToSurface);
			graphics.FillRectangle(0, 0, ImageWidth / 2, ImageHeight, Color.Red);
			graphics.FillRectangle(ImageWidth / 2, 0, ImageWidth, ImageHeight, Color.Blue);
			return frame;
		}

		/// <summary>
		/// Every pixel whose 3x3 neighbourhood is one solid colour in the vector reference (well inside a half,
		/// or well outside the image) must be that colour in <paramref name="actual"/>. The one pixel band along
		/// every edge is left free, since anti-aliasing and image filtering legitimately differ there.
		/// </summary>
		private static async Task AssertMatchesReference(ImageBuffer reference, ImageBuffer actual, string what)
		{
			int checkedPixels = 0;
			for (int y = 1; y < FrameSize - 1; y++)
			{
				for (int x = 1; x < FrameSize - 1; x++)
				{
					var expected = reference.GetPixel(x, y);
					if (!NeighbourhoodIsSolid(reference, x, y, expected))
					{
						continue;
					}

					checkedPixels++;
					var got = actual.GetPixel(x, y);
					int delta = Math.Max(Math.Max(Math.Abs(expected.red - got.red), Math.Abs(expected.green - got.green)), Math.Abs(expected.blue - got.blue));
					await Assert.That(delta).IsLessThanOrEqualTo(8)
						.Because($"{what}: at ({x}, {y}) expected {expected} got {got}");
				}
			}

			await Assert.That(checkedPixels).IsGreaterThan(FrameSize * FrameSize / 2);
		}

		private static bool NeighbourhoodIsSolid(ImageBuffer image, int x, int y, Color color)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					if (image.GetPixel(x + dx, y + dy) != color)
					{
						return false;
					}
				}
			}

			return true;
		}
	}
}
