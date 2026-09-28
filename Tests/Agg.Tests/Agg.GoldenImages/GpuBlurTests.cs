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
	/// <see cref="IBlurGraphics"/> on <c>Graphics2DGpu</c> against a real device, compared with software
	/// <see cref="stack_blur"/> run over the device's own unblurred render of the same draws.
	/// </summary>
	/// <remarks>
	/// Blurring the device's render, not software's, keeps the GPU's anti-aliasing (which is not AGG's) out of
	/// the comparison. The shader sums in floats and rounds once in its 8-bit middle layer, stack_blur in integers
	/// through its mul/shr tables, so the two differ by a unit or two; the tolerance is 3 per channel.
	/// </remarks>
	[NotInParallel]
	public class GpuBlurTests
	{
		private const int Width = 120;
		private const int Height = 80;
		private const int Radius = 6;
		private const int Tolerance = 3;

		private static readonly Color Shade = new Color(30, 60, 90);

		private static readonly IVertexSource Shape = new Ellipse(60, 40, 22.3, 17.7, 100);

		[Test]
		public async Task BlurredShapeOnWhiteMatchesStackBlur()
		{
			ImageBuffer gpu = await Capture(graphics =>
			{
				graphics.FillRectangle(0, 0, Width, Height, Color.White);
				((IBlurGraphics)graphics).DrawBlurred(Radius, () => graphics.Render(Shape, Shade));
			});

			ImageBuffer software = await Capture(graphics =>
			{
				graphics.FillRectangle(0, 0, Width, Height, Color.White);
				graphics.Render(Shape, Shade);
			});
			new stack_blur().blur(software, Radius);

			await AssertNear(gpu, software);
		}

		[Test]
		public async Task ColorTableColoursTheBlurredAlpha()
		{
			var table = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				table[i] = new Color(i, 255 - i, 128, 255);
			}

			ImageBuffer gpu = await Capture(graphics =>
			{
				graphics.FillRectangle(0, 0, Width, Height, Color.White);
				((IBlurGraphics)graphics).DrawBlurred(Radius, () => graphics.Render(Shape, Color.White), table);
			});

			// blend_color's software path: coverage as gray, stack-blurred, coloured through the table.
			ImageBuffer coverage = await Capture(graphics => graphics.Render(Shape, Color.White));
			var gray = new ImageBuffer(Width, Height, 8, new BlenderGrayExact(1));
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					gray.GetBuffer()[gray.GetBufferOffsetXY(x, y)] = coverage.GetPixel(x, y).alpha;
				}
			}

			stack_blur.BlurGray8(gray, Radius, Radius);
			var software = new ImageBuffer(Width, Height);
			software.NewGraphics2D().FillRectangle(0, 0, Width, Height, Color.White);
			software.BlendFromLut(gray, table, 0, 0);

			await AssertNear(gpu, software);
		}

		/// <summary>
		/// BlurUnder blurs what is already drawn, inside a pixel-aligned region only: inside it the frame is the
		/// stack blur of the whole unblurred frame (taps outside the region included), outside it is untouched.
		/// </summary>
		[Test]
		public async Task BlurUnderBlursTheFrameInsideTheRegionOnly()
		{
			void DrawScene(Graphics2D graphics)
			{
				graphics.FillRectangle(0, 0, Width, Height, Color.White);
				graphics.Render(Shape, Shade);
				for (int x = 4; x < Width; x += 9)
				{
					graphics.FillRectangle(x, 0, x + 3, Height, new Color(200, 40, 40));
				}
			}

			const int Left = 30, Bottom = 20, Right = 90, Top = 60;
			ImageBuffer gpu = await Capture(graphics =>
			{
				DrawScene(graphics);
				((IBlurGraphics)graphics).BlurUnder(new RoundedRect(Left, Bottom, Right, Top, 0), Radius);
			});

			ImageBuffer unblurred = await Capture(DrawScene);
			var blurred = new ImageBuffer(unblurred);
			new stack_blur().blur(blurred, Radius);

			var expected = new ImageBuffer(unblurred);
			for (int y = Bottom; y < Top; y++)
			{
				for (int x = Left; x < Right; x++)
				{
					expected.SetPixel(x, y, blurred.GetPixel(x, y));
				}
			}

			await AssertNear(gpu, expected);
		}

		private static async Task<ImageBuffer> Capture(Action<Graphics2D> draw)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			draw(capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0)));
			return await capture.CaptureAsync();
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
