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
using System.Runtime.InteropServices;
using MatterHackers.Agg.Image;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// Drives the classic D3D11 render path with no window: creates a device, initialises
	/// <see cref="VorticeD3DGl"/> off-screen, hands out the <see cref="GL"/> facade the whole 2D and 3D
	/// stack draws through, and reads the finished render target back as an <see cref="ImageBuffer"/>.
	/// </summary>
	/// <remarks>
	/// This is the golden harness's capture end. It mirrors <c>MatterCADLib\Library\D3D11ThumbnailRenderer</c>
	/// - the established off-screen pattern - deliberately rather than adding an entry point to the renderer:
	/// the classic path is the parity oracle for the wgpu port and has to stay byte-for-byte untouched while
	/// the port is built, so the harness reaches around it instead of into it.
	/// <para>
	/// Hardware only, by design. <c>D3D11Control</c> can fall back to the WARP software rasterizer, but WARP
	/// rasterizes differently from a GPU, so a silent fallback would compare this machine's goldens against a
	/// different renderer's pixels and blame the port. Missing hardware throws instead.
	/// </para>
	/// </remarks>
	public sealed class D3D11OffscreenCapture : IDisposable
	{
		/// <summary>Capture size for the golden suites. Small enough to keep the PNGs reasonable in git,
		/// large enough that text and mesh silhouettes carry real detail.</summary>
		public const int DefaultWidth = 512;

		public const int DefaultHeight = 384;

		private ID3D11Device device;
		private ID3D11DeviceContext deviceContext;
		private VorticeD3DGl backend;

		private D3D11OffscreenCapture(int width, int height)
		{
			Width = width;
			Height = height;

			var featureLevels = new[]
			{
				FeatureLevel.Level_11_1,
				FeatureLevel.Level_11_0,
				FeatureLevel.Level_10_1,
				FeatureLevel.Level_10_0,
			};

			var result = D3D11.D3D11CreateDevice(
				(IDXGIAdapter)null,
				DriverType.Hardware,
				DeviceCreationFlags.BgraSupport,
				featureLevels,
				out device,
				out deviceContext);

			if (result.Failure)
			{
				throw new InvalidOperationException(
					"The golden image suites need a real D3D11 GPU; D3D11CreateDevice(Hardware) failed with"
					+ $" 0x{result.Code:X8}. There is deliberately no WARP fallback - software rasterization"
					+ " would not match the goldens anyway.");
			}

			// Every texture and display list the caches hold belongs to a device that is gone by now (each
			// capture makes its own), and the readers only notice through this generation bump.
			Graphics2DGpu.InvalidateGlCaches();

			backend = new VorticeD3DGl();
			backend.InitializeOffscreen(device, deviceContext, width, height);

			Gl = new GL(backend);
			backend.OwnerGl = Gl;
		}

		public int Width { get; }

		public int Height { get; }

		/// <summary>The facade every drawing call in the suites goes through - the same object type
		/// <c>D3D11Control</c> hands the running application.</summary>
		public GL Gl { get; private set; }

		/// <summary>The modern 3D seam, for the mesh suites.</summary>
		public INativeSceneRenderer SceneRenderer => (INativeSceneRenderer)Gl.GpuContext;

		/// <summary>The whole capture area, in the form the scene renderer wants it.</summary>
		public RectangleDouble Viewport => new RectangleDouble(0, 0, Width, Height);

		public static D3D11OffscreenCapture Create(int width = DefaultWidth, int height = DefaultHeight)
			=> new D3D11OffscreenCapture(width, height);

		/// <summary>
		/// Clears colour and depth to known values.
		/// </summary>
		/// <remarks>
		/// Always call this before drawing. The off-screen render target is created without initial data, so
		/// any pixel a scene does not cover would otherwise hold whatever the driver left there - the exact
		/// shape of nondeterminism that makes a tolerance-zero suite unusable.
		/// </remarks>
		public void ClearTo(ColorF color)
		{
			Gl.ClearColor(color.red, color.green, color.blue, color.alpha);
			Gl.ClearDepth(1.0);
			Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}

		/// <summary>
		/// Sets up the frame the way <c>D3D11SystemWindow.SetAndClearViewPort</c> does and returns the
		/// widget-facing <see cref="Graphics2D"/>, so the 2D suites exercise the production entry path.
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

			// Clear again through the 2D path, because that is what the window does and it is the only thing
			// that paints the framebuffer alpha the widget stack expects.
			graphics.Clear(background);
			graphics.PushTransform();

			return graphics;
		}

		/// <summary>
		/// Installs the 3D frame state (<see cref="RenderHelper.SetGlContext"/>) and opens a scene, runs
		/// <paramref name="drawScene"/>, then closes both.
		/// </summary>
		/// <param name="supersample">When true the frame is routed through the renderer's 3x full-frame
		/// capture target and box-downsampled back, exactly as the viewport and thumbnails do it.</param>
		public void RenderScene(WorldView world, LightingData lighting, Action drawScene, bool supersample = false)
		{
			var sceneRenderer = SceneRenderer;

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
		/// Copies the finished render target into an <see cref="ImageBuffer"/> (BGRA, bottom-up).
		/// </summary>
		public ImageBuffer Capture()
		{
			var sourceTexture = backend.MainRenderTarget
				?? throw new InvalidOperationException("The off-screen render target does not exist.");

			var description = sourceTexture.Description;
			description.Usage = ResourceUsage.Staging;
			description.BindFlags = BindFlags.None;
			description.CPUAccessFlags = CpuAccessFlags.Read;

			using var stagingTexture = device.CreateTexture2D(description);
			deviceContext.CopyResource(stagingTexture, sourceTexture);

			var mapped = deviceContext.Map(stagingTexture, 0, MapMode.Read);
			try
			{
				var image = new ImageBuffer(Width, Height, 32, new BlenderBGRA());
				var buffer = image.GetBuffer();

				// D3D11 rows run top down and agg's run bottom up, so the copy walks the source backwards.
				for (int y = 0; y < Height; y++)
				{
					var sourceRow = IntPtr.Add(mapped.DataPointer, (Height - 1 - y) * (int)mapped.RowPitch);
					Marshal.Copy(sourceRow, buffer, image.GetBufferOffsetY(y), Width * 4);
				}

				image.MarkImageChanged();
				return image;
			}
			finally
			{
				deviceContext.Unmap(stagingTexture, 0);
			}
		}

		public void Dispose()
		{
			backend?.Dispose();
			deviceContext?.Dispose();
			device?.Dispose();

			backend = null;
			deviceContext = null;
			device = null;
			Gl = null;

			// The caches this capture filled are keyed on a GL whose device is now gone; leaving them live
			// would hand the next capture display list ids minted by a dead device.
			Graphics2DGpu.InvalidateGlCaches();
		}
	}
}
