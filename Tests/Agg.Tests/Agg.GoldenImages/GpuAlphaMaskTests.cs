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
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="IAlphaMaskGraphics"/> on <c>Graphics2DGpu</c> against a real device, compared with the software
	/// surface drawing the same fill through an <see cref="AlphaMaskAdaptor"/> over <see cref="AlphaMaskByteClipped"/>.
	/// </summary>
	/// <remarks>
	/// The fill is a pixel-aligned rectangle, so both surfaces cover the same pixels exactly and only the mask's
	/// multiply is compared: the blend rounds once in 8 bits, software's cover product once more, so a unit or two.
	/// </remarks>
	[NotInParallel]
	public class GpuAlphaMaskTests
	{
		private const int Width = 120;
		private const int Height = 80;
		private const int Tolerance = 3;

		private static readonly Color Shade = new Color(30, 60, 200);

		[Test]
		public async Task MaskedFillMatchesTheSoftwareMaskAdaptor()
		{
			// Smaller than the frame, so its outside (which hides everything) is covered too.
			ImageBuffer mask = RampMask(100, 60);

			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IAlphaMaskGraphics)graphics).DrawMasked(mask, () =>
			{
				graphics.Render(new RoundedRect(10, 10, 115, 75, 0), Shade);
			});
			// Drawing goes back to the frame, unmasked.
			graphics.Render(new RoundedRect(0, 0, 4, 4, 0), Shade);
			ImageBuffer gpu = await capture.CaptureAsync();

			var software = new ImageBuffer(Width, Height);
			software.NewGraphics2D().FillRectangle(0, 0, Width, Height, Color.White);
			var masked = new ImageGraphics2D(new ImageClippingProxy(new AlphaMaskAdaptor(software, new AlphaMaskByteClipped(mask, 1, 0))), new ScanlineRasterizer(), new ScanlineCachePacked8());
			masked.Render(new RoundedRect(10, 10, 115, 75, 0), Shade);
			software.NewGraphics2D().Render(new RoundedRect(0, 0, 4, 4, 0), Shade);

			await AssertNear(gpu, software);
		}

		/// <summary>A horizontal ramp of coverage with an empty band across it.</summary>
		private static ImageBuffer RampMask(int width, int height)
		{
			var mask = new ImageBuffer(width, height, 8, new BlenderGrayExact(1));
			byte[] buffer = mask.GetBuffer();
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					buffer[mask.GetBufferOffsetXY(x, y)] = (byte)(y >= 30 && y < 35 ? 0 : x * 255 / (width - 1));
				}
			}

			return mask;
		}

		private static async Task AssertNear(ImageBuffer gpu, ImageBuffer software)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = software.GetPixel(x, y);
					int worst = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Max(Math.Abs(g.blue - s.blue), Math.Abs(g.alpha - s.alpha)));
					if (worst > Tolerance)
					{
						await Assert.That(worst).IsLessThanOrEqualTo(Tolerance)
							.Because($"({x}, {y}): GPU {g} vs software {s}");
					}
				}
			}
		}
	}
}
