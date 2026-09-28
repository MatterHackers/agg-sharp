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
	/// <see cref="RendererMarkers"/> drawn on <c>Graphics2DGpu</c> through a <see cref="Graphics2DSpanImage"/>,
	/// compared with the same markers and Bresenham line drawn into a software image.
	/// </summary>
	/// <remarks>
	/// Every span is a pixel-aligned rectangle, so both cover the same pixels; only the source-over blend of
	/// the translucent colors may round differently, by a unit or two.
	/// </remarks>
	[NotInParallel]
	public class GpuRendererMarkersTests
	{
		private const int Width = 160;
		private const int Height = 60;
		private const int Tolerance = 3;

		[Test]
		public async Task MarkersAndLinesMatchTheSoftwareRenderer()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			Graphics2D graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.FillRectangle(0, 0, Width, Height, Color.White);
			// No Flush: the frame's end issues the span image's pending runs, as a window host's does.
			Draw(new ImageClippingProxy(new Graphics2DSpanImage(graphics, Width, Height)));
			ImageBuffer gpu = await capture.CaptureAsync();

			var software = new ImageBuffer(Width, Height);
			software.NewGraphics2D().FillRectangle(0, 0, Width, Height, Color.White);
			Draw(new ImageClippingProxy(software));

			await AssertNear(gpu, software);
		}

		/// <summary>
		/// One of each outline-and-fill family (box, diamond, ellipse, semiellipse, triangle, rays, dot), a
		/// circle cut by the frame's edge, and a Bresenham line across them all.
		/// </summary>
		private static void Draw(ImageClippingProxy target)
		{
			var markers = new RendererMarkers(target)
			{
				LineColor = new Color(20, 40, 160, 200),
				FillColor = new Color(220, 120, 30, 160),
			};
			MarkerType[] types = { MarkerType.Square, MarkerType.Diamond, MarkerType.CrossedCircle, MarkerType.SemiellipseUp, MarkerType.TriangleRight, MarkerType.FourRays, MarkerType.Dot };
			for (int i = 0; i < types.Length; i++)
			{
				markers.Marker(12 + (i * 22), 30, 9, types[i]);
			}

			markers.Marker(Width - 3, 5, 8, MarkerType.Circle);
			markers.Line(RendererMarkers.Coord(2.3), RendererMarkers.Coord(3.7), RendererMarkers.Coord(150.2), RendererMarkers.Coord(55.9), true);
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
