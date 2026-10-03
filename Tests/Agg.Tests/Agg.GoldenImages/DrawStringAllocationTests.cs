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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The browser build's Mono GC collects whenever its nursery fills, mid-frame, so text - drawn every frame
	/// by live readouts and every label that re-rasters - should not churn short-lived objects per draw.
	/// Measured on the software rasterizer and on the GPU path, after a warm-up that fills the glyph caches.
	/// </summary>
	[NotInParallel]
	public class DrawStringAllocationTests
	{
		private const int WarmupFrames = 5;
		private const int MeasuredFrames = 50;

		// Ten short readouts and a two line label, roughly what one frame of the GUI demo's overlay draws.
		private static readonly string[] Readouts =
		{
			"FPS: 60.0", "Frame: 16.67 ms", "Draw: 3.21 ms", "GC: 0 / 0 / 0", "Alloc: 12.3 KB",
			"Widgets: 142", "Scale: 1.50", "Backend: WebGPU", "Mouse: 120, 340", "Zoom: 100%",
		};

		/// <summary>
		/// The bytes one frame of the draws below may allocate on the software rasterizer. It was 838 KB while
		/// every glyph rebuilt its scale and curve-flattening chain per draw, and about 7 KB once unstyled glyphs
		/// were replayed from PlainGlyphCache; what is left is each DrawString's printer, face and vertex iterator.
		/// </summary>
		private const long ImagePerFrameBudgetBytes = 10 * 1024;

		/// <summary>
		/// The same on the GPU, where the glyph masks were already cached: 57 KB before, about 40 KB after. Nearly
		/// all of the rest is the compat layer's immediate-mode submission of each run's mask quad
		/// (ImageTexturePlugin.DrawToGL), which this budget does not yet ask to go.
		/// </summary>
		private const long GpuPerFrameBudgetBytes = 44 * 1024;

		private static TextWidget MakeLabel()
		{
			var label = new TextWidget("Two line\nlabel text", pointSize: 10)
			{
				DoubleBuffer = false,
			};
			label.Width = 200;
			return label;
		}

		private static void DrawFrame(Graphics2D graphics, TextWidget label)
		{
			for (int i = 0; i < Readouts.Length; i++)
			{
				graphics.DrawString(Readouts[i], 10.25, 20 + i * 14, 9, color: Color.Black);
			}

			graphics.DrawString("Centered", 150, 200, 12, Justification.Center, Baseline.BoundsCenter, Color.Blue);
			label.OnDraw(graphics);
		}

		private static long MeasurePerFrame(Graphics2D graphics, TextWidget label)
		{
			for (int frame = 0; frame < WarmupFrames; frame++)
			{
				DrawFrame(graphics, label);
			}

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int frame = 0; frame < MeasuredFrames; frame++)
			{
				DrawFrame(graphics, label);
			}

			return (GC.GetAllocatedBytesForCurrentThread() - before) / MeasuredFrames;
		}

		[Test]
		public async Task ImageBufferTextFrameStaysUnderItsAllocationBudget()
		{
			var image = new ImageBuffer(320, 240);
			var graphics = image.NewGraphics2D();
			long perFrame = MeasurePerFrame(graphics, MakeLabel());
			Console.WriteLine($"ImageBuffer text frame allocates {perFrame} bytes");

			await Assert.That(perFrame).IsLessThan(ImagePerFrameBudgetBytes);
		}

		[Test]
		public async Task GpuTextFrameStaysUnderItsAllocationBudget()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
			long perFrame = MeasurePerFrame(graphics, MakeLabel());
			Console.WriteLine($"Graphics2DGpu text frame allocates {perFrame} bytes");

			await Assert.That(perFrame).IsLessThan(GpuPerFrameBudgetBytes);
		}
	}
}
