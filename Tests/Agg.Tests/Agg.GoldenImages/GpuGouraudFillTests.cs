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
	/// <see cref="IGouraudGraphics.FillGouraud"/> on <c>Graphics2DGpu</c> against a real device, compared pixel for
	/// pixel (edges included) with a scanline rasterizer rendering the same span_gouraud_rgba.
	/// </summary>
	/// <remarks>
	/// Interiors agree to a level or two; the few pixels at 3 are on edges, where the rasterizer rounds a cover down
	/// or up by the outline's winding and blends it in integers, the shader in floats.
	/// </remarks>
	[NotInParallel]
	public class GpuGouraudFillTests
	{
		private const int Width = 120;
		private const int Height = 90;
		private const int Tolerance = 3;

		/// <summary>
		/// Corners in each order (so both of generate's edge swaps run), a flat top, a sliver and a triangle past the
		/// frame; with and without dilation and a coverage gamma.
		/// </summary>
		[Test]
		[Arguments(5.3, 4.1, 110.7, 30.2, 40.5, 85.9, 0.0, 1.0)]
		[Arguments(40.5, 85.9, 110.7, 30.2, 5.3, 4.1, 0.0, 1.0)]
		[Arguments(10.0, 20.0, 100.0, 20.0, 60.0, 80.0, 0.4, 0.6)]
		[Arguments(3.0, 3.0, 117.0, 12.0, 20.0, 9.0, 0.175, 0.809)]
		[Arguments(-30.0, 10.0, 150.0, 50.0, 60.0, 120.0, 0.0, 1.0)]
		public async Task FillMatchesSpanGouraudRgba(double x1, double y1, double x2, double y2, double x3, double y3, double dilation, double gamma)
		{
			var red = new Color(255, 0, 0, 230);
			var green = new Color(0, 255, 0, 160);
			var blue = new Color(20, 40, 255, 255);
			var triangle = new span_gouraud_rgba();
			triangle.colors(red, green, blue);
			triangle.triangle(x1, y1, x2, y2, x3, y3, dilation);
			var coverageGamma = new gamma_linear(0.0, gamma);

			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			((IGouraudGraphics)capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1))).FillGouraud(new[] { triangle }, coverageGamma);
			ImageBuffer gpu = await capture.CaptureAsync();

			var software = new ImageBuffer(Width, Height);
			software.NewGraphics2D().Clear(Color.White);
			var rasterizer = new ScanlineRasterizer();
			rasterizer.SetVectorClipBox(0, 0, Width, Height);
			rasterizer.gamma(coverageGamma);
			rasterizer.add_path(triangle);
			triangle.prepare();
			new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), software, new span_allocator(), triangle);

			int bad = 0;
			string first = null;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					Color g = gpu.GetPixel(x, y);
					Color s = software.GetPixel(x, y);
					int worst = Math.Max(Math.Max(Math.Abs(g.red - s.red), Math.Abs(g.green - s.green)), Math.Abs(g.blue - s.blue));
					if (worst > Tolerance)
					{
						bad++;
						first ??= $"({x}, {y}): GPU {g} vs software {s}";
					}
				}
			}

			await Assert.That(bad).IsEqualTo(0).Because(first ?? string.Empty);
		}
	}
}
