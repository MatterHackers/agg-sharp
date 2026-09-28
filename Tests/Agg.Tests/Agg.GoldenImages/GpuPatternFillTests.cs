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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="IPatternFillGraphics.FillPathWithImage"/> on a real device against <see cref="ImageGraphics2D"/>: the
	/// same tiles, mirrored or repeated, land on the same pixels inside the shape.
	/// </summary>
	[NotInParallel]
	public class GpuPatternFillTests
	{
		[Test]
		public async Task ScaledPatternMatchesSoftware()
		{
			// Pixel centers fall a quarter of a source pixel from every source pixel edge, so nearest sampling has no ties.
			var imageToScreen = Affine.NewScaling(3) * Affine.NewTranslation(1.25, 2.25);
			await CompareInterior(imageToScreen, ImageWrapMode.Reflect, ImageWrapMode.Repeat, 0);
		}

		[Test]
		public async Task RotatedPatternMatchesSoftware()
		{
			// Turned, some pixel centers land within the software interpolator's 1/256 pixel of a source edge and
			// round the other way; those few may differ.
			var imageToScreen = Affine.NewScaling(4) * Affine.NewRotation(Math.PI / 7) * Affine.NewTranslation(3.5, 1.5);
			await CompareInterior(imageToScreen, ImageWrapMode.Repeat, ImageWrapMode.Reflect, 0.01);
		}

		private static async Task CompareInterior(Affine imageToScreen, ImageWrapMode wrapX, ImageWrapMode wrapY, double allowedFraction)
		{
			ImageBuffer pattern = BuildPattern();
			var star = new VertexStorage();
			for (int i = 0; i < 10; i++)
			{
				double angle = (i * Math.PI / 5) + 0.1;
				double radius = (i & 1) == 0 ? 150 : 70;
				if (i == 0)
				{
					star.MoveTo(radius * Math.Cos(angle), radius * Math.Sin(angle));
				}
				else
				{
					star.LineTo(radius * Math.Cos(angle), radius * Math.Sin(angle));
				}
			}

			star.ClosePolygon();

			// The star is moved by the current transform; the pattern stays where imageToScreen puts it.
			var placement = Affine.NewTranslation(256, 192);

			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			gpuGraphics.SetTransform(placement);
			gpuGraphics.FillPathWithImage(star, pattern, imageToScreen, wrapX, wrapY);
			var gpuImage = await capture.CaptureAsync();

			var softwareImage = new ImageBuffer(gpuImage.Width, gpuImage.Height, 32, new BlenderBGRA());
			var softwareGraphics = (ImageGraphics2D)softwareImage.NewGraphics2D();
			softwareGraphics.Clear(Color.White);
			softwareGraphics.SetTransform(placement);
			softwareGraphics.FillPathWithImage(star, pattern, imageToScreen, wrapX, wrapY);

			// Where the star fully covers a pixel in software; the edges are anti-aliased differently by the two.
			var coverage = new ImageBuffer(gpuImage.Width, gpuImage.Height, 32, new BlenderBGRA());
			var coverageGraphics = coverage.NewGraphics2D();
			coverageGraphics.SetTransform(placement);
			coverageGraphics.Render(star, Color.White);

			int compared = 0;
			int differing = 0;
			for (int y = 0; y < gpuImage.Height; y++)
			{
				for (int x = 0; x < gpuImage.Width; x++)
				{
					if (coverage.GetPixel(x, y).alpha != 255)
					{
						continue;
					}

					compared++;
					Color gpu = gpuImage.GetPixel(x, y);
					Color software = softwareImage.GetPixel(x, y);
					if (Math.Abs(gpu.red - software.red) > 2
						|| Math.Abs(gpu.green - software.green) > 2
						|| Math.Abs(gpu.blue - software.blue) > 2)
					{
						differing++;
					}
				}
			}

			await Assert.That(compared).IsGreaterThan(20000);
			await Assert.That(differing).IsLessThanOrEqualTo((int)(compared * allowedFraction));
		}

		/// <summary>A 7 by 5 tile (neither a power of two) of distinct colors, some translucent, so a flip, a wrap
		/// off by one or a doubled alpha all show.</summary>
		private static ImageBuffer BuildPattern()
		{
			var pattern = new ImageBuffer(7, 5, 32, new BlenderBGRA());
			byte[] pixels = pattern.GetBuffer();
			for (int y = 0; y < 5; y++)
			{
				for (int x = 0; x < 7; x++)
				{
					int at = pattern.GetBufferOffsetXY(x, y);
					pixels[at + ImageBuffer.OrderR] = (byte)(x * 36);
					pixels[at + ImageBuffer.OrderG] = (byte)(y * 60);
					pixels[at + ImageBuffer.OrderB] = (byte)(255 - (x * 20) - (y * 10));
					pixels[at + ImageBuffer.OrderA] = (byte)((x + y) % 3 == 0 ? 120 : 255);
				}
			}

			pattern.MarkImageChanged();
			return pattern;
		}
	}
}
