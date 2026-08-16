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
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.RenderGl.Scene;
using MatterHackers.VectorMath;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The wgpu twin of <see cref="D3D11OffscreenCapture"/>: a <see cref="WebGpuRenderDevice"/> with an
	/// off-screen colour and depth target, a <see cref="GlCompatContext"/> over it, and the same
	/// <see cref="GL"/> facade the whole 2D stack draws through - so one golden scene can be rendered on
	/// either backend and the pixels compared.
	/// </summary>
	/// <remarks>
	/// Deliberately mirrors the classic capture member for member, including <see cref="BeginWidgetFrame"/>'s
	/// odd double clear, because any divergence in frame setup would show up in the diff images as a port bug
	/// that isn't one.
	/// <para>
	/// The colour target is <see cref="TextureFormat.Bgra8Unorm"/> - the format the classic off-screen target
	/// uses (<c>Format.B8G8R8A8_UNorm</c>) and the byte order agg's 32-bit <see cref="ImageBuffer"/> stores -
	/// so the read-back bytes land in the golden image without a swizzle that could hide a channel bug.
	/// </para>
	/// <para>
	/// D3D12 is requested explicitly, exactly as the wgpu unit-test harness does: a machine that silently
	/// picked Vulkan would turn "wrong backend" into an unexplained pixel diff against goldens captured on
	/// D3D11 hardware.
	/// </para>
	/// </remarks>
	public sealed class WebGpuOffscreenCapture : IDisposable
	{
		/// <summary>Capture size for the golden suites; must match <see cref="D3D11OffscreenCapture.DefaultWidth"/>
		/// or the comparison is against a different image.</summary>
		public const int DefaultWidth = D3D11OffscreenCapture.DefaultWidth;

		public const int DefaultHeight = D3D11OffscreenCapture.DefaultHeight;

		private WebGpuRenderDevice device;
		private GlCompatContext context;
		private WebGpuSceneRenderer sceneRenderer;
		private IGpuTexture colorTarget;
		private IGpuTexture depthTarget;

		private WebGpuOffscreenCapture(int width, int height)
		{
			Width = width;
			Height = height;

			// Every texture and display list the caches hold belongs to a context that is gone by now (each
			// capture makes its own), and the readers only notice through this generation bump.
			Graphics2DGpu.InvalidateGlCaches();

			device = new WebGpuRenderDevice(false, WGPUBackendType.D3D12, "WebGpuOffscreenCapture");

			try
			{
				colorTarget = device.CreateTexture(new TextureDescriptor(
					(uint)width,
					(uint)height,
					TextureFormat.Bgra8Unorm,
					TextureUsage.RenderAttachment | TextureUsage.CopySrc,
					1,
					1,
					"goldenColor"));

				depthTarget = device.CreateTexture(new TextureDescriptor(
					(uint)width,
					(uint)height,
					TextureFormat.Depth32Float,
					TextureUsage.RenderAttachment,
					1,
					1,
					"goldenDepth"));

				context = new GlCompatContext(device);
				context.SetRenderTarget(colorTarget, depthTarget);

				Gl = new GL(context);

				// The scene compositor is a separate object here (the classic backend is one class that is
				// both), so the context forwards INativeSceneRenderer to it and it is handed the facade the
				// mesh render-data caches are keyed on - the same wiring D3D11OffscreenCapture does with
				// VorticeD3DGl.OwnerGl.
				sceneRenderer = new WebGpuSceneRenderer(context) { OwnerGl = Gl };
				context.SceneRenderer = sceneRenderer;
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		public int Width { get; }

		public int Height { get; }

		/// <summary>The facade every drawing call in the suites goes through.</summary>
		public GL Gl { get; private set; }

		/// <summary>The compat context under the facade, for diagnostics (pipeline counts, pass counts).</summary>
		public GlCompatContext Context => context;

		/// <summary>The modern 3D seam, for the mesh suites.</summary>
		public INativeSceneRenderer SceneRenderer => sceneRenderer;

		/// <summary>The device, so a test can assert no wgpu validation error was reported.</summary>
		public WebGpuRenderDevice Device => device;

		/// <summary>The whole capture area.</summary>
		public RectangleDouble Viewport => new RectangleDouble(0, 0, Width, Height);

		public static WebGpuOffscreenCapture Create(int width = DefaultWidth, int height = DefaultHeight)
			=> new WebGpuOffscreenCapture(width, height);

		/// <summary>Clears colour and depth to known values, as the classic capture does.</summary>
		public void ClearTo(ColorF color)
		{
			Gl.ClearColor(color.red, color.green, color.blue, color.alpha);
			Gl.ClearDepth(1.0);
			Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}

		/// <summary>
		/// Sets up the frame the way <c>D3D11SystemWindow.SetAndClearViewPort</c> does - and the way
		/// <see cref="D3D11OffscreenCapture.BeginWidgetFrame"/> does - and returns the widget-facing
		/// <see cref="Graphics2D"/>.
		/// </summary>
		public Graphics2DGpu BeginWidgetFrame(ColorF background)
		{
			Gl.Viewport(0, 0, Width, Height);

			Gl.MatrixMode(MatrixMode.Projection);
			Gl.LoadIdentity();

			Gl.MatrixMode(MatrixMode.Modelview);
			Gl.LoadIdentity();

			Gl.Scissor(0, 0, Width, Height);

			ClearTo(background);

			var graphics = new Graphics2DGpu(Gl, Width, Height, 1);

			graphics.Clear(background);
			graphics.PushTransform();

			return graphics;
		}

		/// <summary>
		/// Installs the 3D frame state (<see cref="RenderHelper.SetGlContext"/>) and opens a scene, runs
		/// <paramref name="drawScene"/>, then closes both - member for member what
		/// <see cref="D3D11OffscreenCapture.RenderScene"/> does, so one scene definition drives both
		/// backends.
		/// </summary>
		/// <param name="world">The camera.</param>
		/// <param name="lighting">The frame's lights.</param>
		/// <param name="drawScene">Draws the scene through <see cref="Gl"/>.</param>
		/// <param name="supersample">Routes the frame through the 3x full-frame capture. Phase 3 leg B;
		/// the WebGPU renderer throws rather than silently rendering an unsupersampled frame.</param>
		public void RenderScene(WorldView world, LightingData lighting, Action drawScene, bool supersample = false)
		{
			if (supersample)
			{
				sceneRenderer.BeginFullFrameCapture(Viewport);
			}

			RenderHelper.SetGlContext(Gl, world, Viewport, lighting);
			sceneRenderer.BeginSceneRendering(new SceneRenderContext(world, Viewport, lighting));

			try
			{
				drawScene();
			}
			finally
			{
				sceneRenderer.EndSceneRendering();
				RenderHelper.UnsetGlContext(Gl);

				if (supersample)
				{
					sceneRenderer.EndFullFrameCapture();
					sceneRenderer.DownsampleAndBlitFullFrame();
				}
			}
		}

		/// <summary>
		/// Ends the frame and copies the colour target into an <see cref="ImageBuffer"/> (BGRA, bottom-up),
		/// in the same orientation <see cref="D3D11OffscreenCapture.Capture"/> produces.
		/// </summary>
		/// <remarks>
		/// Asynchronous because WebGPU read-back is <c>mapAsync</c>-only by contract (the native backend
		/// completes before returning, so this costs nothing here). <c>ReadTextureAsync</c> submits whatever
		/// is recorded, but the pass still has to be closed first, which is what the <c>Submit</c> is for.
		/// </remarks>
		public async Task<ImageBuffer> CaptureAsync()
		{
			context.Submit();

			uint rowStride = TextureFormatInfo.AlignedRowStride(TextureFormat.Bgra8Unorm, (uint)Width);
			var bytes = new byte[rowStride * (long)Height];
			var read = await device.ReadTextureAsync(colorTarget, bytes);

			var image = new ImageBuffer(Width, Height, 32, new BlenderBGRA());
			var buffer = image.GetBuffer();

			// wgpu rows run top down and agg's run bottom up, so the copy walks the source backwards - the
			// same flip the classic capture does, which is what keeps a mirrored render detectable.
			for (int y = 0; y < Height; y++)
			{
				long sourceOffset = (Height - 1 - y) * (long)read.RowStride;
				Array.Copy(bytes, sourceOffset, buffer, image.GetBufferOffsetY(y), Width * 4);
			}

			image.MarkImageChanged();
			return image;
		}

		public void Dispose()
		{
			sceneRenderer?.Dispose();
			sceneRenderer = null;
			context?.Dispose();
			colorTarget?.Dispose();
			depthTarget?.Dispose();
			device?.Dispose();

			context = null;
			colorTarget = null;
			depthTarget = null;
			device = null;
			Gl = null;

			// The caches this capture filled are keyed on a GL whose device is now gone; leaving them live
			// would hand the next capture display list ids minted by a dead device.
			Graphics2DGpu.InvalidateGlCaches();
		}
	}
}
