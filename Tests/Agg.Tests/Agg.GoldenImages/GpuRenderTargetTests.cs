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
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// A <see cref="GpuRenderTarget"/> painted in the middle of a frame and composited back into it, on the
	/// real WebGPU device. Everything drawn is pixel aligned and opaque or transparent, so the expected
	/// pixels are exact arithmetic rather than a golden image.
	/// </summary>
	[NotInParallel]
	public class GpuRenderTargetTests
	{
		private const int TargetWidth = 32;
		private const int TargetHeight = 16;

		private static readonly ColorF Background = new ColorF(0, 0, 1, 1);

		/// <summary>
		/// Opaque red on the left half, opaque white in the bottom of the right half, transparent above it -
		/// so a flip, a shift or a lost transparency each shows up at a different probe.
		/// </summary>
		private static void PaintTarget(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, 16, 16, Color.Red);
			graphics.FillRectangle(16, 0, 32, 8, Color.White);
		}

		private static async Task AssertPixel(ImageBuffer image, int x, int y, int red, int green, int blue)
		{
			var pixel = image.GetPixel(x, y);
			await Assert.That(Math.Abs(pixel.red - red) <= 1 && Math.Abs(pixel.green - green) <= 1 && Math.Abs(pixel.blue - blue) <= 1)
				.IsTrue()
				.Because($"pixel ({x}, {y}) is {pixel}, expected r{red} g{green} b{blue}");
		}

		[Test]
		public async Task PaintedMidFrameAndCompositedAtAnOffsetWithOpacity()
		{
			using var capture = WebGpuOffscreenCapture.Create(128, 64);
			using var target = new GpuRenderTarget(capture.Gl);

			var frame = capture.BeginWidgetFrame(Background);

			// Drawn before the redirect: must survive the pass being ended and re-opened.
			frame.FillRectangle(0, 0, 8, 8, Color.Green);

			var targetGraphics = target.BeginDraw(TargetWidth, TargetHeight).Graphics;
			PaintTarget(targetGraphics);
			target.EndDraw();

			target.Composite(frame, 10, 20, 1);
			target.Composite(frame, 60, 20, 0.5);

			// Drawn after: must land on the frame, not in the target.
			frame.FillRectangle(120, 0, 128, 8, Color.Green);

			var image = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();

			await AssertPixel(image, 4, 4, 0, 255, 0);
			await AssertPixel(image, 124, 4, 0, 255, 0);

			// Opacity 1 at (10, 20): red, white below-right, background through the transparent corner and
			// just outside the quad.
			await AssertPixel(image, 10 + 4, 20 + 4, 255, 0, 0);
			await AssertPixel(image, 10 + 4, 20 + 12, 255, 0, 0);
			await AssertPixel(image, 10 + 20, 20 + 4, 255, 255, 255);
			await AssertPixel(image, 10 + 20, 20 + 12, 0, 0, 255);
			await AssertPixel(image, 10 + 32, 20 + 4, 0, 0, 255);
			await AssertPixel(image, 10 - 1, 20 + 4, 0, 0, 255);
			await AssertPixel(image, 10 + 4, 20 - 1, 0, 0, 255);

			// Opacity 0.5 (128/255) at (60, 20), premultiplied over opaque blue.
			await AssertPixel(image, 60 + 4, 20 + 4, 128, 0, 127);
			await AssertPixel(image, 60 + 20, 20 + 4, 128, 128, 255);
			await AssertPixel(image, 60 + 20, 20 + 12, 0, 0, 255);
		}

		[Test]
		public async Task CleanTargetIsCompositedAgainWithoutRepainting()
		{
			using var capture = WebGpuOffscreenCapture.Create(64, 32);
			using var target = new GpuRenderTarget(capture.Gl);

			var frame = capture.BeginWidgetFrame(Background);
			PaintTarget(target.BeginDraw(TargetWidth, TargetHeight).Graphics);
			target.EndDraw();
			var texture = target.Texture;
			target.Composite(frame, 8, 8);
			await capture.CaptureAsync();

			// The second frame paints nothing into the target; its retained pixels are all it draws.
			frame = capture.BeginWidgetFrame(Background);
			target.Composite(frame, 8, 8);
			var image = await capture.CaptureAsync();

			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			await Assert.That(target.DrawCount).IsEqualTo(1);
			await AssertPixel(image, 8 + 4, 8 + 4, 255, 0, 0);
			await AssertPixel(image, 8 + 20, 8 + 4, 255, 255, 255);
			await AssertPixel(image, 8 + 20, 8 + 12, 0, 0, 255);

			// A repaint at the same size keeps the texture; a new size replaces it.
			target.BeginDraw(TargetWidth, TargetHeight);
			target.EndDraw();
			await Assert.That(ReferenceEquals(target.Texture, texture)).IsTrue();

			target.BeginDraw(TargetWidth + 1, TargetHeight);
			target.EndDraw();
			await Assert.That(ReferenceEquals(target.Texture, texture)).IsFalse();
			await Assert.That(target.DrawCount).IsEqualTo(3);
		}

		[Test]
		public async Task TargetsNestLikeBackbufferedWidgetsInsideEachOther()
		{
			using var capture = WebGpuOffscreenCapture.Create(64, 32);
			using var outer = new GpuRenderTarget(capture.Gl);
			using var inner = new GpuRenderTarget(capture.Gl);

			var frame = capture.BeginWidgetFrame(Background);

			var outerGraphics = outer.BeginDraw(40, 24).Graphics;
			outerGraphics.FillRectangle(0, 0, 40, 24, Color.White);

			PaintTarget(inner.BeginDraw(TargetWidth, TargetHeight).Graphics);
			inner.EndDraw();

			inner.Composite(outerGraphics, 4, 4);
			outer.EndDraw();

			outer.Composite(frame, 10, 4);
			var image = await capture.CaptureAsync();

			await Assert.That(capture.Device.LastUncapturedError).IsNull();

			// Inner red at outer (4..20, 4..20), so frame (14..30, 8..24); the inner's transparent corner
			// shows the outer's white, not the frame's blue.
			await AssertPixel(image, 10 + 8, 4 + 8, 255, 0, 0);
			await AssertPixel(image, 10 + 4 + 20, 4 + 4 + 12, 255, 255, 255);
			await AssertPixel(image, 10 + 2, 4 + 2, 255, 255, 255);
			await AssertPixel(image, 10 + 44, 4 + 2, 0, 0, 255);
		}
	
		[Test]
		public async Task ResizingDoesNotStrandBindGroups()
		{
			using var capture = WebGpuOffscreenCapture.Create(64, 32);
			using var target = new GpuRenderTarget(capture.Gl);
			var pipelines = capture.Context.Pipelines;

			int CompositeAtWidth(int width)
			{
				var frame = capture.BeginWidgetFrame(Background);
				using (var draw = target.BeginDraw(width, TargetHeight))
				{
					PaintTarget(draw.Graphics);
				}

				target.Composite(frame, 0, 0);
				capture.Context.Submit();
				return pipelines.BindGroupCount;
			}

			// Warm up so every other bind group the frame needs already exists.
			CompositeAtWidth(20);
			int settled = CompositeAtWidth(21);
			for (int width = 22; width < 30; width++)
			{
				CompositeAtWidth(width);
			}

			await Assert.That(pipelines.BindGroupCount).IsEqualTo(settled);
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
		}

		[Test]
		public async Task APaintThatThrowsStillGivesTheFrameItsTargetBack()
		{
			using var capture = WebGpuOffscreenCapture.Create(64, 32);
			using var target = new GpuRenderTarget(capture.Gl);

			var frame = capture.BeginWidgetFrame(Background);
			try
			{
				using var draw = target.BeginDraw(TargetWidth, TargetHeight);
				draw.Graphics.FillRectangle(0, 0, 8, 8, Color.White);
				throw new InvalidOperationException("paint failed");
			}
			catch (InvalidOperationException)
			{
			}

			await Assert.That(target.IsDrawing).IsFalse();

			// Lands on the frame, not in the target, with the frame's viewport.
			frame.FillRectangle(40, 0, 64, 32, Color.Red);
			var image = await capture.CaptureAsync();

			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			await AssertPixel(image, 50, 20, 255, 0, 0);
			await AssertPixel(image, 4, 4, 0, 0, 255);
		}

		[Test]
		public async Task DisposingMidDrawGivesTheFrameItsTargetBack()
		{
			using var capture = WebGpuOffscreenCapture.Create(64, 32);
			var frame = capture.BeginWidgetFrame(Background);

			var target = new GpuRenderTarget(capture.Gl);
			target.BeginDraw(TargetWidth, TargetHeight);
			target.Dispose();

			frame.FillRectangle(40, 0, 64, 32, Color.Red);
			var image = await capture.CaptureAsync();

			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			await AssertPixel(image, 50, 20, 255, 0, 0);
		}
	}
}
