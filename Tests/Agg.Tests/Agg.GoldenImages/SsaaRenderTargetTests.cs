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
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="SsaaRenderTarget"/> on the real WebGPU device: content drawn at N x its size and
	/// box-downsampled onto the frame.
	/// </summary>
	[NotInParallel]
	public class SsaaRenderTargetTests
	{
		private const int FrameWidth = 48;
		private const int FrameHeight = 24;
		private const int TargetSize = 8;

		private static readonly ColorF Background = new ColorF(0.2f, 0.3f, 0.4f, 1);
		private static readonly Color CheckerA = new Color(200, 40, 0, 255);
		private static readonly Color CheckerB = new Color(0, 120, 60, 255);

		/// <summary>Flat, unblended, untextured GL in the target's logical pixels, z in -1..1 (1 nearest).</summary>
		private static void BeginFlatDraw(GL gl, bool depthTest)
		{
			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();
			gl.Ortho(0, TargetSize, 0, TargetSize, -1, 1);
			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
			gl.Disable(EnableCap.Texture2D);
			gl.Disable(EnableCap.Blend);
			gl.Disable(EnableCap.Lighting);
			if (depthTest)
			{
				gl.Enable(EnableCap.DepthTest);
				gl.DepthFunc(DepthFunction.Less);
				gl.DepthMask(true);
			}
			else
			{
				gl.Disable(EnableCap.DepthTest);
			}
		}

		private static void Quad(GL gl, double left, double bottom, double right, double top, double z, Color color)
		{
			gl.Color4(color.red, color.green, color.blue, color.alpha);
			gl.Begin(BeginMode.Triangles);
			gl.Vertex3(left, bottom, z);
			gl.Vertex3(right, bottom, z);
			gl.Vertex3(right, top, z);
			gl.Vertex3(left, bottom, z);
			gl.Vertex3(right, top, z);
			gl.Vertex3(left, top, z);
			gl.End();
		}

		/// <summary>A checker of one device pixel of the target's texture: 1 / scale logical pixels.</summary>
		private static void DrawDevicePixelChecker(GL gl, int scale)
		{
			BeginFlatDraw(gl, depthTest: false);
			Quad(gl, 0, 0, TargetSize, TargetSize, 0, CheckerB);
			double cell = 1.0 / scale;
			int cells = TargetSize * scale;
			for (int y = 0; y < cells; y++)
			{
				for (int x = 0; x < cells; x++)
				{
					if (((x + y) & 1) == 0)
					{
						Quad(gl, x * cell, y * cell, (x + 1) * cell, (y + 1) * cell, 0, CheckerA);
					}
				}
			}
		}

		private static async Task<ImageBuffer> RenderAsync(WebGpuOffscreenCapture capture, SsaaRenderTarget target, int scale, Action<GL> draw, double x, double y)
		{
			var frame = capture.BeginWidgetFrame(Background);
			using (target.BeginDraw(TargetSize, TargetSize, scale))
			{
				draw(capture.Gl);
			}

			capture.Gl.Disable(EnableCap.DepthTest);
			target.Composite(frame, x, y);
			var image = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			return image;
		}

		private static Color BackgroundColor => Background.ToColor();

		private static async Task AssertPixel(ImageBuffer image, int x, int y, Color expected, int tolerance, string what)
		{
			var actual = image.GetPixel(x, y);
			int delta = Math.Max(Math.Max(Math.Abs(actual.red - expected.red), Math.Abs(actual.green - expected.green)), Math.Abs(actual.blue - expected.blue));
			await Assert.That(delta).IsLessThanOrEqualTo(tolerance)
				.Because($"{what} at ({x}, {y}): expected {expected}, got {actual}");
		}

		[Test]
		public async Task ADevicePixelCheckerAt2xDownsamplesToTheBoxAverage()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			using var target = new SsaaRenderTarget(capture.Gl);

			var image = await RenderAsync(capture, target, 2, gl => DrawDevicePixelChecker(gl, 2), 0, 0);

			await Assert.That(target.Scale).IsEqualTo(2);
			await Assert.That(target.ColorTexture.Descriptor.Width).IsEqualTo((uint)(TargetSize * 2));

			// Every 2x2 block holds two of each color, so each output pixel is exactly their mean.
			var average = new Color((200 + 0) / 2, (40 + 120) / 2, (0 + 60) / 2, 255);
			for (int y = 0; y < TargetSize; y++)
			{
				for (int x = 0; x < TargetSize; x++)
				{
					await AssertPixel(image, x, y, average, 0, "the 2x box average");
				}
			}
		}

		[Test]
		public async Task AtScale1TheContentPassesThroughUnchanged()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			using var target = new SsaaRenderTarget(capture.Gl);

			var image = await RenderAsync(capture, target, 1, gl => DrawDevicePixelChecker(gl, 1), 0, 0);

			for (int y = 0; y < TargetSize; y++)
			{
				for (int x = 0; x < TargetSize; x++)
				{
					await AssertPixel(image, x, y, ((x + y) & 1) == 0 ? CheckerA : CheckerB, 0, "the 1x copy");
				}
			}
		}

		/// <summary>
		/// The nearer quad is drawn first, so only a depth test keeps it in front; drawing twice proves the
		/// depth buffer is cleared per draw (a stale one would reject the second draw's quads outright).
		/// </summary>
		[Test]
		public async Task TheDepthAttachmentOrdersOverlappingGeometry()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			using var target = new SsaaRenderTarget(capture.Gl);
			var near = new Color(255, 0, 0, 255);
			var far = new Color(0, 255, 0, 255);

			for (int pass = 0; pass < 2; pass++)
			{
				var image = await RenderAsync(
					capture,
					target,
					2,
					gl =>
					{
						BeginFlatDraw(gl, depthTest: true);
						Quad(gl, 0, 0, 6, 6, 0.5, near);
						Quad(gl, 2, 2, 8, 8, -0.5, far);
					},
					0,
					0);

				await AssertPixel(image, 1, 1, near, 0, $"pass {pass}: the near quad alone");
				await AssertPixel(image, 4, 4, near, 0, $"pass {pass}: the overlap, where the near quad must win");
				await AssertPixel(image, 7, 7, far, 0, $"pass {pass}: the far quad alone");
				await AssertPixel(image, 7, 1, BackgroundColor, 0, $"pass {pass}: uncovered, where the frame shows through");
			}
		}

		[Test]
		public async Task ItCompositesAtAnOffsetInsideTheFrame()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			using var target = new SsaaRenderTarget(capture.Gl);
			var ink = new Color(250, 250, 0, 255);

			// Only the left half is drawn: the right half stays transparent and shows the frame.
			var image = await RenderAsync(
				capture,
				target,
				3,
				gl =>
				{
					BeginFlatDraw(gl, depthTest: false);
					Quad(gl, 0, 0, TargetSize / 2, TargetSize, 0, ink);
				},
				20,
				5);

			await AssertPixel(image, 20, 5, ink, 0, "the target's bottom-left pixel");
			await AssertPixel(image, 23, 12, ink, 0, "the target's top of the drawn half");
			await AssertPixel(image, 24, 8, BackgroundColor, 0, "the undrawn half");
			await AssertPixel(image, 19, 5, BackgroundColor, 0, "left of the target");
			await AssertPixel(image, 20, 4, BackgroundColor, 0, "below the target");
			await AssertPixel(image, 20, 13, BackgroundColor, 0, "above the target");
		}

		/// <summary>
		/// A full-frame capture opened inside an SSAA draw supersamples the SSAA texture, so its coordinate
		/// scale composes with the target's (2 x 3) and its end hands the target's scale back - otherwise the
		/// captured quad and every later draw into the target land at the wrong size.
		/// </summary>
		[Test]
		public async Task AFullFrameCaptureInsideAnSsaaDrawComposesWithItsScale()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			using var target = new SsaaRenderTarget(capture.Gl);
			var captured = new Color(250, 250, 0, 255);
			var after = new Color(0, 0, 250, 255);

			var image = await RenderAsync(
				capture,
				target,
				2,
				gl =>
				{
					capture.SceneRenderer.BeginFullFrameCapture(new RectangleDouble(0, 0, TargetSize, TargetSize));
					BeginFlatDraw(gl, depthTest: false);
					Quad(gl, 0, 0, TargetSize / 2, TargetSize, 0, captured);
					capture.SceneRenderer.EndFullFrameCapture();
					capture.SceneRenderer.DownsampleAndBlitFullFrame();

					BeginFlatDraw(gl, depthTest: false);
					Quad(gl, TargetSize / 2, 0, TargetSize, TargetSize / 2, 0, after);
				},
				0,
				0);

			await Assert.That(capture.Context.CoordinateScale).IsEqualTo(1);
			await AssertPixel(image, 0, 0, captured, 0, "the captured quad's bottom left");
			await AssertPixel(image, 3, 7, captured, 0, "the captured quad's top right");
			await AssertPixel(image, 4, 7, BackgroundColor, 0, "right of the captured quad");
			await AssertPixel(image, 4, 0, after, 0, "the later quad's bottom left");
			await AssertPixel(image, 7, 3, after, 0, "the later quad's top right");
			await AssertPixel(image, 7, 4, BackgroundColor, 0, "above the later quad");
		}

		/// <summary>Supersampling is done by size alone: every texture involved has one sample.</summary>
		[Test]
		public async Task NoAttachmentIsMultisampled()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			using var target = new SsaaRenderTarget(capture.Gl);

			await RenderAsync(capture, target, 4, gl => DrawDevicePixelChecker(gl, 4), 0, 0);

			await Assert.That(target.ColorTexture.Descriptor.SampleCount).IsEqualTo(1u);
			await Assert.That(target.DepthTexture.Descriptor.SampleCount).IsEqualTo(1u);
			await Assert.That(target.ColorTexture.Descriptor.Width).IsEqualTo((uint)(TargetSize * 4));
		}

		/// <summary>
		/// The native scene renderer draws into the target at its factor: a whole-frame 3x SSAA target
		/// composited over the frame matches the scene renderer's own 3x full-frame capture, which averages
		/// the same 3x3 blocks.
		/// </summary>
		[Test]
		public async Task TheSceneRendererDrawsIntoTheTargetAtItsFactor()
		{
			const int Width = 128;
			const int Height = 96;
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);

			capture.ClearTo(Golden3DScenes.Background);
			capture.RenderScene(Golden3DScenes.CreateCamera(Width, Height), new LightingData(), () => Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 255), supersample: true);
			var expected = await capture.CaptureAsync();

			using var target = new SsaaRenderTarget(capture.Gl);
			var frame = capture.BeginWidgetFrame(Golden3DScenes.Background);
			using (target.BeginDraw(Width, Height, 3))
			{
				var world = Golden3DScenes.CreateCamera(Width, Height);
				var lighting = new LightingData();
				var viewport = new RectangleDouble(0, 0, Width, Height);
				RenderHelper.SetGlContext(capture.Gl, world, viewport, lighting);
				capture.SceneRenderer.BeginSceneRendering(new SceneRenderContext(world, viewport, lighting));
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 255);
				capture.SceneRenderer.EndSceneRendering();
				RenderHelper.UnsetGlContext(capture.Gl);
			}

			target.Composite(frame, 0, 0);
			var actual = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();

			int worst = 0;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					var a = expected.GetPixel(x, y);
					var b = actual.GetPixel(x, y);
					worst = Math.Max(worst, Math.Max(Math.Max(Math.Abs(a.red - b.red), Math.Abs(a.green - b.green)), Math.Abs(a.blue - b.blue)));
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(1)
				.Because("the SSAA target and the full-frame capture average the same 3x3 blocks");
		}
	}
}
