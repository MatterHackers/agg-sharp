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
	/// <see cref="IGammaGraphics"/> on <c>Graphics2DGpu</c> against a real device, compared with what the software
	/// surface does: a rasterizer gamma on a fill's coverage, and pixfmt apply_gamma_inv over the frame.
	/// </summary>
	[NotInParallel]
	public class GpuGammaTests
	{
		private const int Width = 120;
		private const int Height = 80;

		private static readonly Color Shade = new Color(30, 60, 200);

		/// <summary>
		/// The GPU's anti-aliasing is its own (an outward halo, not the scanline rasterizer's cell coverage), so the
		/// gamma is checked on the GPU's own coverage: every pixel of the gamma'd fill is the plain fill's coverage
		/// through the curve, as a rasterizer with that gamma maps its cover.
		/// </summary>
		[Test]
		public async Task CoverageGammaMapsTheFillCoverageThroughTheCurve()
		{
			var gamma = new gamma_power(3.0);
			var shape = new VertexStorage();
			shape.MoveTo(5.3, 4.1);
			shape.LineTo(114.6, 30.7);
			shape.LineTo(20.2, 76.4);
			shape.ClosePolygon();

			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D plainGraphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			plainGraphics.FillRectangle(0, 0, Width, Height, Color.White);
			plainGraphics.Render(shape, Shade);
			ImageBuffer plain = await capture.CaptureAsync();

			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			((IGammaGraphics)graphics).DrawWithCoverageGamma(gamma, Shade, () => graphics.Render(shape, Color.White));
			ImageBuffer gammaed = await capture.CaptureAsync();

			int partial = 0;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					// Red runs from 255 (white, no cover) to Shade's 30 (full cover).
					double cover = (255 - plain.GetPixel(x, y).red) / (double)(255 - Shade.red);
					if (cover > .05 && cover < .95)
					{
						partial++;
					}

					int expected = (int)Math.Round(255 - (gamma.GetGamma(cover) * (255 - Shade.red)));
					int actual = gammaed.GetPixel(x, y).red;
					if (Math.Abs(actual - expected) > 4)
					{
						await Assert.That(actual).IsEqualTo(expected).Because($"({x}, {y}): plain cover {cover:0.000}");
					}
				}
			}

			// The edges have to be anti-aliased for the curve to show at all.
			await Assert.That(partial).IsGreaterThan(50);
		}

		[Test]
		public async Task MapChannelsMatchesApplyGammaInv()
		{
			var table = new GammaLookUpTable(1.8);
			var inverse = new byte[256];
			for (int i = 0; i < 256; i++)
			{
				inverse[i] = table.inv(i);
			}

			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			DrawBands(graphics);
			((IGammaGraphics)graphics).MapChannels(new RectangleDouble(0, 0, Width, Height), inverse, inverse, inverse);
			ImageBuffer gpu = await capture.CaptureAsync();

			var software = new ImageBuffer(Width, Height);
			DrawBands(software.NewGraphics2D());
			software.ApplyGammaInv(table);

			await AssertNear(gpu, software, 1);
		}

		/// <summary>Opaque pixel-aligned bands of many channel values, so both surfaces hold the same bytes before the map.</summary>
		private static void DrawBands(Graphics2D graphics)
		{
			for (int x = 0; x < Width; x++)
			{
				int v = x * 255 / (Width - 1);
				graphics.FillRectangle(x, 0, x + 1, Height / 2, new Color(v, 255 - v, (v * 7) & 255));
				graphics.FillRectangle(x, Height / 2, x + 1, Height, new Color((v * 3) & 255, v, 255 - v));
			}
		}

		private static async Task AssertNear(ImageBuffer gpu, ImageBuffer software, int tolerance)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = software.GetPixel(x, y);
					int worst = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Max(Math.Abs(g.blue - s.blue), Math.Abs(g.alpha - s.alpha)));
					if (worst > tolerance)
					{
						await Assert.That(worst).IsLessThanOrEqualTo(tolerance)
							.Because($"({x}, {y}): GPU {g} vs software {s}");
					}
				}
			}
		}
	}
}
