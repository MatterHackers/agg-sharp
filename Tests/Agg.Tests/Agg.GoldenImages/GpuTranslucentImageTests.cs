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
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <c>Graphics2DGpu.Render(IImageByte, ...)</c> blends a translucent texel over the target once, as the software
	/// <see cref="ImageGraphics2D"/> does - whether the image holds straight alpha (<see cref="BlenderBGRA"/>) or
	/// premultiplied (<see cref="BlenderPreMultBGRA"/>).
	/// </summary>
	[NotInParallel]
	public class GpuTranslucentImageTests
	{
		private static readonly Color StraightColor = new Color(240, 120, 60, 128);

		[Test]
		public async Task StraightAlphaImageBlendsLikeSoftware()
		{
			var source = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			Fill(source, StraightColor.red, StraightColor.green, StraightColor.blue, StraightColor.alpha);

			await AssertGpuMatchesSoftware(source, new BlenderBGRA());
		}

		/// <summary>
		/// An image's receive blender says how things are drawn <i>into</i> it, not how its bytes are read when it is
		/// drawn: software hands them to the destination as they are, and so must the GPU. MatterCAD's icons rely on
		/// it - StaticData.LoadIcon stamps them <see cref="BlenderPreMultBGRA"/> over straight-alpha pixels.
		/// </summary>
		[Test]
		public async Task PremultipliedStampedImageBlendsLikeSoftware()
		{
			var source = new ImageBuffer(16, 16, 32, new BlenderPreMultBGRA());
			Fill(source, StraightColor.red, StraightColor.green, StraightColor.blue, StraightColor.alpha);

			await AssertGpuMatchesSoftware(source, new BlenderBGRA());
		}

		/// <summary>
		/// A fill without edge anti-aliasing draws through the alpha ramp textures, blended as premultiplied: the
		/// ramp has to hold premultiplied texels or half-transparent red lands at full strength.
		/// </summary>
		[Test]
		public async Task NonAntiAliasedTranslucentFillBlendsOnce()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(0, 0, 1, 1));
			gpuGraphics.DoEdgeAntiAliasing = false;
			var square = new VertexStorage();
			square.MoveTo(100.3, 80.3);
			square.LineTo(160.3, 80.3);
			square.LineTo(160.3, 140.3);
			square.LineTo(100.3, 140.3);
			square.ClosePolygon();
			gpuGraphics.Render(square, new Color(255, 0, 0, 128));
			var gpuImage = await capture.CaptureAsync();

			Color middle = gpuImage.GetPixel(130, 110);
			await Assert.That(Math.Abs(middle.red - 128)).IsLessThanOrEqualTo(2);
			await Assert.That((int)middle.green).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(middle.blue - 127)).IsLessThanOrEqualTo(2);
		}

		/// <summary>
		/// An image's OriginOffset places it once - by the texture quad, as the software renderer's placement does -
		/// whichever way it points.
		/// </summary>
		[Test]
		[Arguments(5, 3)]
		[Arguments(-4, -6)]
		public async Task OriginOffsetPlacesTheImageLikeSoftware(int offsetX, int offsetY)
		{
			var source = new ImageBuffer(16, 12, 32, new BlenderBGRA());
			source.NewGraphics2D().Clear(Color.Red);
			source.NewGraphics2D().FillRectangle(12, 8, 16, 12, Color.Green);
			source.OriginOffset = new VectorMath.Vector2(offsetX, offsetY);

			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			gpuGraphics.Render(source, 100, 80);
			var gpuImage = await capture.CaptureAsync();

			var softwareImage = new ImageBuffer(gpuImage.Width, gpuImage.Height, 32, new BlenderBGRA());
			var softwareGraphics = softwareImage.NewGraphics2D();
			softwareGraphics.Clear(Color.White);
			softwareGraphics.Render(source, 100, 80);

			int differing = 0;
			for (int y = 60; y < 110; y++)
			{
				for (int x = 80; x < 130; x++)
				{
					Color gpu = gpuImage.GetPixel(x, y);
					Color software = softwareImage.GetPixel(x, y);
					if (Math.Abs(gpu.red - software.red) > 2 || Math.Abs(gpu.green - software.green) > 2 || Math.Abs(gpu.blue - software.blue) > 2)
					{
						differing++;
					}
				}
			}

			await Assert.That(differing).IsEqualTo(0);
		}

		/// <summary>
		/// A straight-alpha image's fully transparent pixels contribute no colour when the texture is filtered: the
		/// invisible green beside an opaque red pixel must not tint the magnified edge between them.
		/// </summary>
		[Test]
		public async Task TransparentPixelsDoNotBleedWhenMagnified()
		{
			var source = new ImageBuffer(2, 1, 32, new BlenderBGRA());
			byte[] pixels = source.GetBuffer();
			int left = source.GetBufferOffsetXY(0, 0);
			int right = source.GetBufferOffsetXY(1, 0);
			pixels[left + ImageBuffer.OrderR] = 255;
			pixels[left + ImageBuffer.OrderA] = 255;
			pixels[right + ImageBuffer.OrderG] = 255;
			source.MarkImageChanged();

			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			gpuGraphics.Render(source, 100, 80, 0, 16, 16);
			var gpuImage = await capture.CaptureAsync();

			// Over white only red can drop out of green and blue, and it takes both down together.
			int worstTint = 0;
			for (int x = 100; x < 132; x++)
			{
				Color pixel = gpuImage.GetPixel(x, 88);
				worstTint = Math.Max(worstTint, pixel.green - pixel.blue);
			}

			await Assert.That(worstTint).IsLessThanOrEqualTo(2);
		}

		private static void Fill(ImageBuffer image, byte red, byte green, byte blue, byte alpha)
		{
			// Raw bytes: going through a blender would itself premultiply or blend the colour.
			byte[] pixels = image.GetBuffer();
			for (int at = 0; at + 3 < pixels.Length; at += 4)
			{
				pixels[at + ImageBuffer.OrderR] = red;
				pixels[at + ImageBuffer.OrderG] = green;
				pixels[at + ImageBuffer.OrderB] = blue;
				pixels[at + ImageBuffer.OrderA] = alpha;
			}

			image.MarkImageChanged();
		}

		private static async Task AssertGpuMatchesSoftware(ImageBuffer source, IRecieveBlenderByte softwareBlender)
		{
			const int x = 100;
			const int y = 80;

			using var capture = WebGpuOffscreenCapture.Create();
			var gpuGraphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			gpuGraphics.Render(source, x, y);
			var gpuImage = await capture.CaptureAsync();

			var softwareImage = new ImageBuffer(gpuImage.Width, gpuImage.Height, 32, softwareBlender);
			var softwareGraphics = softwareImage.NewGraphics2D();
			softwareGraphics.Clear(Color.White);
			softwareGraphics.Render(source, x, y);

			// The middle of the image, clear of any bilinear edge. Over white that is about (247, 187, 157).
			Color gpu = gpuImage.GetPixel(x + 8, y + 8);
			Color software = softwareImage.GetPixel(x + 8, y + 8);

			await Assert.That(Math.Abs(gpu.red - software.red)).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(gpu.green - software.green)).IsLessThanOrEqualTo(2);
			await Assert.That(Math.Abs(gpu.blue - software.blue)).IsLessThanOrEqualTo(2);
		}
	}
}
