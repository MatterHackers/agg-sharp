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
	/// <see cref="IBlurGraphics.BlurBox"/> on <c>Graphics2DGpu</c> against the software blur it reproduces, run over
	/// the same box of the device's own unblurred render (so the GPU's anti-aliasing stays out of the comparison).
	/// </summary>
	/// <remarks>
	/// The stack and 3x3 box blurs sum in integers as software does and must match exactly; the recursive and slight blurs sum
	/// in f32 where software uses doubles, so a sum within a hair of a half may round the other way: 1 per channel.
	/// </remarks>
	[NotInParallel]
	public class GpuBlurBoxTests
	{
		private const int Width = 120;
		private const int Height = 80;

		// The box, inclusive, and off the frame's edges so the box's own edges are what the blur clamps to.
		private const int Left = 17, Bottom = 9, Right = 101, Top = 66;

		[Test]
		[Arguments(BlurKind.Stack, ColorChannels.All, 0)]
		[Arguments(BlurKind.Recursive, ColorChannels.All, 1)]
		[Arguments(BlurKind.Recursive, ColorChannels.Green, 1)]
		[Arguments(BlurKind.Slight, ColorChannels.All, 1)]
		[Arguments(BlurKind.Box3x3, ColorChannels.All, 0)]
		public async Task BlurBoxMatchesTheSoftwareBlurOfTheBox(BlurKind kind, ColorChannels channels, int tolerance)
		{
			double radius = kind == BlurKind.Slight ? 1.5 : 7.3;
			ImageBuffer gpu = await Capture(graphics =>
			{
				DrawScene(graphics);
				((IBlurGraphics)graphics).BlurBox(new RoundedRect(Left, Bottom, Right + 1, Top + 1, 0), radius, kind, channels);
			});

			ImageBuffer unblurred = await Capture(DrawScene);
			var expected = new ImageBuffer(unblurred);
			var box = new ImageBuffer();
			box.AttachBuffer(expected.GetBuffer(), expected.GetBufferOffsetXY(Left, Bottom), Right - Left + 1, Top - Bottom + 1, expected.StrideInBytes(), 32, 4);
			box.SetRecieveBlender(expected.GetRecieveBlender());
			switch (kind)
			{
				case BlurKind.Stack:
					new stack_blur().blur(box, Util.uround(radius));
					break;

				case BlurKind.Recursive:
					new RecursiveBlur(new recursive_blur_calc_rgb()).blur(box, radius);
					break;

				case BlurKind.Box3x3:
					// The truncated mean of each pixel's 3x3 block (simple_blur's span generator); the box is well inside
					// the frame, so every block is whole.
					for (int y = Bottom; y <= Top; y++)
					{
						for (int x = Left; x <= Right; x++)
						{
							int r = 0, g = 0, b = 0;
							for (int dy = -1; dy <= 1; dy++)
							{
								for (int dx = -1; dx <= 1; dx++)
								{
									Color c = unblurred.GetPixel(x + dx, y + dy);
									r += c.red;
									g += c.green;
									b += c.blue;
								}
							}

							expected.SetPixel(x, y, new Color(r / 9, g / 9, b / 9));
						}
					}

					break;

				case BlurKind.Slight:
					new SlightBlur(radius).Blur(expected, new RectangleInt(Left, Bottom, Right, Top));
					break;
			}

			if (channels != ColorChannels.All)
			{
				// Only green blurred: red and blue are the frame's own.
				for (int y = 0; y < Height; y++)
				{
					for (int x = 0; x < Width; x++)
					{
						Color blurred = expected.GetPixel(x, y);
						Color original = unblurred.GetPixel(x, y);
						expected.SetPixel(x, y, new Color(original.red, blurred.green, original.blue, original.alpha));
					}
				}
			}

			int worst = 0;
			string at = null;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = expected.GetPixel(x, y);
					int difference = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Max(Math.Abs(g.blue - s.blue), Math.Abs(g.alpha - s.alpha)));
					if (difference > worst)
					{
						worst = difference;
						at = $"({x}, {y}): GPU {g} vs software {s}";
					}
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(tolerance).Because(at ?? string.Empty);
		}

		/// <summary>
		/// A box past wgpu's default storage binding limit (128 MiB) at 16 bytes a pixel, the size of a near-4K window,
		/// blurred as the software blurs it: the stack blur whole, the recursive one in several batches of lines.
		/// </summary>
		[Test]
		[Arguments(BlurKind.Stack, 0)]
		[Arguments(BlurKind.Recursive, 1)]
		public async Task BlurBoxOfALargeBoxMatchesTheSoftwareBlur(BlurKind kind, int tolerance)
		{
			const int LargeWidth = 4104, LargeHeight = 2104;
			const int BoxLeft = 2, BoxBottom = 2, BoxRight = LargeWidth - 3, BoxTop = LargeHeight - 3;
			const double Radius = 5.2;
			ImageBuffer gpu = await Capture(LargeWidth, LargeHeight, graphics =>
			{
				DrawScene(graphics, LargeWidth, LargeHeight);
				((IBlurGraphics)graphics).BlurBox(new RoundedRect(BoxLeft, BoxBottom, BoxRight + 1, BoxTop + 1, 0), Radius, kind, ColorChannels.All);
			});

			ImageBuffer expected = await Capture(LargeWidth, LargeHeight, graphics => DrawScene(graphics, LargeWidth, LargeHeight));
			var box = new ImageBuffer();
			box.AttachBuffer(expected.GetBuffer(), expected.GetBufferOffsetXY(BoxLeft, BoxBottom), BoxRight - BoxLeft + 1, BoxTop - BoxBottom + 1, expected.StrideInBytes(), 32, 4);
			box.SetRecieveBlender(expected.GetRecieveBlender());
			if (kind == BlurKind.Stack)
			{
				new stack_blur().blur(box, Util.uround(Radius));
			}
			else
			{
				new RecursiveBlur(new recursive_blur_calc_rgb()).blur(box, Radius);
			}

			// Byte by byte over the whole frame: both captures are packed the same way, and GetPixel over 8.6M pixels
			// would be most of the test's time.
			byte[] gpuBytes = gpu.GetBuffer();
			byte[] expectedBytes = expected.GetBuffer();
			int worst = 0;
			string at = null;
			for (int y = 0; y < LargeHeight; y++)
			{
				int gpuRow = gpu.GetBufferOffsetY(y);
				int expectedRow = expected.GetBufferOffsetY(y);
				for (int i = 0; i < LargeWidth * 4; i++)
				{
					int difference = Math.Abs(gpuBytes[gpuRow + i] - expectedBytes[expectedRow + i]);
					if (difference > worst)
					{
						worst = difference;
						at = $"({i / 4}, {y}) byte {i % 4}: GPU {gpuBytes[gpuRow + i]} vs software {expectedBytes[expectedRow + i]}";
					}
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(tolerance).Because(at ?? string.Empty);
		}

		private static void DrawScene(Graphics2D graphics) => DrawScene(graphics, Width, Height);

		private static void DrawScene(Graphics2D graphics, int width, int height)
		{
			graphics.FillRectangle(0, 0, width, height, Color.White);
			graphics.Render(new Ellipse(60, 40, 22.3, 17.7, 100), new Color(30, 60, 90));
			for (int x = 4; x < width; x += 9)
			{
				graphics.FillRectangle(x, 0, x + 3, height, new Color(200, 40, 40));
			}

			graphics.Render(new Stroke(new Ellipse(40, 30, 25, 12, 60), 1.3), new Color(20, 160, 60));
		}

		private static Task<ImageBuffer> Capture(Action<Graphics2D> draw) => Capture(Width, Height, draw);

		private static async Task<ImageBuffer> Capture(int width, int height, Action<Graphics2D> draw)
		{
			using var capture = WebGpuOffscreenCapture.Create(width, height);
			draw(capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1)));
			return await capture.CaptureAsync();
		}
	}
}
