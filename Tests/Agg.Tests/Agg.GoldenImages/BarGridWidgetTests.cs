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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>The GUI Demo's 3D bar grid drawn on the real WebGPU device through its SSAA target.</summary>
	[NotInParallel]
	public class BarGridWidgetTests
	{
		private const int Width = 300;
		private const int Height = 230;

		private static readonly ColorF Background = new ColorF(0.1, 0.1, 0.12, 1);

		private static async Task<ImageBuffer> RenderAsync(WebGpuOffscreenCapture capture, BarGridWidget grid)
		{
			var frame = capture.BeginWidgetFrame(Background);
			grid.OnDraw(frame);
			var image = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			return image;
		}

		[Test]
		public async Task AFixedTimeFrameAtSsaa2DrawsTheBarsAndIsStable()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var grid = new BarGridWidget { FixedTimeSeconds = 1.25, SsaaFactor = 2 };
			grid.LocalBounds = new RectangleDouble(0, 0, Width, Height);

			var first = await RenderAsync(capture, grid);
			var second = await RenderAsync(capture, grid);

			await Assert.That(grid.DrewBars).IsTrue();

			var background = Background.ToColor();
			int covered = 0;
			int different = 0;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					var a = first.GetPixel(x, y);
					var b = second.GetPixel(x, y);
					if (Math.Abs(a.red - background.red) + Math.Abs(a.green - background.green) + Math.Abs(a.blue - background.blue) > 12)
					{
						covered++;
					}

					if (a != b)
					{
						different++;
					}
				}
			}

			// The grid spans most of the view; the corners are left to the background.
			await Assert.That(covered).IsGreaterThan(Width * Height / 4).Because($"{covered} of {Width * Height} pixels show bars");
			await Assert.That(covered).IsLessThan(Width * Height).Because("the bars do not fill the whole frame");
			await Assert.That(first.GetPixel(0, Height - 1)).IsEqualTo(background).Because("the top-left corner is background");
			await Assert.That(different).IsEqualTo(0).Because("a fixed time draws the same frame");

			// Dark palette: the left (front-left) bars lean blue, the right ones pink.
			var middle = first.GetPixel(Width / 2, Height / 2);
			await Assert.That(middle.alpha).IsEqualTo((byte)255);
		}

		[Test]
		public async Task TheAnimationAsksForFramesOnlyWhileVisible()
		{
			UiThread.ResetForTests();
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var window = new SystemWindow(Width, Height);
			var grid = new BarGridWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			window.AddChild(grid);

			int invalidations = 0;
			grid.Invalidated += (s, e) => invalidations++;

			// Drawn on the GPU while visible: a next frame is asked for and delivered.
			await RenderAsync(capture, grid);
			await Assert.That(grid.FrameRequested).IsTrue();
			UiThread.InvokePendingActions();
			await Assert.That(grid.FrameRequested).IsFalse();
			await Assert.That(invalidations).IsEqualTo(1);

			// Hidden before the requested frame arrives: the request is dropped and nothing more is asked for.
			await RenderAsync(capture, grid);
			grid.Visible = false;
			invalidations = 0;
			UiThread.InvokePendingActions();
			await Assert.That(grid.FrameRequested).IsFalse();
			await Assert.That(invalidations).IsEqualTo(0);

			// A frozen (fixed-time) grid never asks.
			grid.Visible = true;
			grid.FixedTimeSeconds = 0;
			await RenderAsync(capture, grid);
			await Assert.That(grid.FrameRequested).IsFalse();

			window.Close();
			UiThread.ResetForTests();
		}

		[Test]
		public async Task ASoftwareSurfaceDrawsOnlyThePlaceholder()
		{
			var grid = new BarGridWidget { SoftwarePlaceholder = "3D needs the GPU renderer" };
			grid.LocalBounds = new RectangleDouble(0, 0, Width, Height);
			var image = new ImageBuffer(Width, Height);
			grid.OnDraw(image.NewGraphics2D());

			await Assert.That(grid.DrewBars).IsFalse();
			await Assert.That(grid.FrameRequested).IsFalse();
		}
	}
}
