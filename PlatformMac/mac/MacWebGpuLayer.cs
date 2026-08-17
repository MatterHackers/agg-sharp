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
using MatterHackers.RenderGl.Scene;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The macOS sibling of <c>WebGpuControl</c>: it owns the wgpu device, the swapchain over a
	/// <c>CAMetalLayer</c>, and the <see cref="GlCompatContext"/> the whole 2D stack draws through.
	/// <para>
	/// <b>What is different from the Windows control.</b> There is no <c>Control</c> underneath, so there
	/// is no handle to wait for and no deferred-initialization dance: the layer is created by
	/// <see cref="MacSystemWindow"/> before this type is constructed, and a <c>CAMetalLayer</c> is a valid
	/// surface source the moment it exists. Sizing is pushed in explicitly by the host (see
	/// <see cref="Resize"/>) rather than arriving as an <c>OnResize</c> event, and it is always in
	/// <em>device pixels</em> - the host has already multiplied by <c>backingScaleFactor</c>.
	/// </para>
	/// <para>
	/// <b>Frame shape</b> is identical to Windows'. <see cref="BeginFrame"/> acquires the swapchain texture
	/// and points the compat context at it plus a depth buffer; widget paint draws through
	/// <see cref="Gl"/>; <see cref="Present"/> submits and presents. A frame the swapchain cannot hand out
	/// is drawn into a scratch texture and never presented, because widget paint has no way to be told
	/// "not this time".
	/// </para>
	/// </summary>
	public class MacWebGpuLayer : IDisposable
	{
		private readonly IntPtr metalLayer;

		private WebGpuRenderDevice device;
		private WebGpuSurfaceTarget surface;
		private GlCompatContext compat;
		private WebGpuSceneRenderer sceneRenderer;
		private IGpuTexture depthTarget;

		/// <summary>Guards against re-entering recovery from a failure raised by recovery itself.</summary>
		private bool isRecoveringDevice;

		/// <summary>How many times this layer has rebuilt its device after a loss. Diagnostics and tests.</summary>
		private int deviceRecoveryCount;

		/// <summary>A present mode set before the swapchain existed, replayed onto it when it does.</summary>
		private WGPUPresentMode? requestedPresentMode;

		/// <summary>
		/// Where a frame goes when the swapchain has none to give. Drawing has to land somewhere legal or
		/// every widget draw in that frame throws; this is that somewhere.
		/// </summary>
		private IGpuTexture scratchTarget;

		private uint pixelWidth;
		private uint pixelHeight;

		private bool isInitialized;
		private bool isDisposed;
		private bool frameIsPresentable;

		/// <summary>Creates the host for a layer that already exists.</summary>
		/// <param name="metalLayer">The <c>CAMetalLayer*</c> wgpu will make its surface over.</param>
		/// <param name="pixelWidth">Initial swapchain width in device pixels.</param>
		/// <param name="pixelHeight">Initial swapchain height in device pixels.</param>
		public MacWebGpuLayer(IntPtr metalLayer, uint pixelWidth, uint pixelHeight)
		{
			if (metalLayer == IntPtr.Zero)
			{
				throw new ArgumentNullException(nameof(metalLayer));
			}

			this.metalLayer = metalLayer;
			this.pixelWidth = Math.Max(1u, pixelWidth);
			this.pixelHeight = Math.Max(1u, pixelHeight);
		}

		/// <summary>
		/// Gets or sets a value indicating whether to demand wgpu's software (fallback) adapter rather than
		/// real hardware. Must be set before <see cref="InitializeWebGpu"/>.
		/// </summary>
		public bool UseSoftwareAdapter { get; set; }

		/// <summary>The facade the 2D stack draws through, or null before initialization.</summary>
		public MatterHackers.RenderGl.OpenGl.GL Gl { get; private set; }

		/// <summary>The compat context under the facade, for diagnostics.</summary>
		public GlCompatContext Compat => this.compat;

		/// <summary>The 3D scene compositor, or null before initialization.</summary>
		public INativeSceneRenderer SceneRenderer => this.sceneRenderer;

		/// <summary>The wgpu device, for diagnostics and error reporting.</summary>
		public WebGpuRenderDevice Device => this.device;

		/// <summary>The swapchain.</summary>
		public WebGpuSurfaceTarget Surface => this.surface;

		/// <summary>True once the device and swapchain exist.</summary>
		public bool IsWebGpuInitialized => this.isInitialized;

		/// <summary>True once <see cref="Dispose"/> has run.</summary>
		public bool IsDisposed => this.isDisposed;

		/// <summary>The backend wgpu chose (Metal on macOS), or Undefined before initialization.</summary>
		public WGPUBackendType BackendType => this.device?.AdapterBackend ?? WGPUBackendType.Undefined;

		/// <summary>
		/// The first thing wgpu complained about - a validation error or a lost device - or null while
		/// everything is well. A smoke run turns this into a non-zero exit code.
		/// </summary>
		public string LastError => this.device?.DeviceLostMessage ?? this.device?.LastUncapturedError;

		/// <summary>How many times this layer has rebuilt its device after a loss; zero on a healthy run.</summary>
		public int DeviceRecoveryCount => this.deviceRecoveryCount;

		/// <summary>The swapchain's current width in device pixels.</summary>
		public uint PixelWidth => this.pixelWidth;

		/// <summary>The swapchain's current height in device pixels.</summary>
		public uint PixelHeight => this.pixelHeight;

		/// <summary>
		/// How the swapchain paces presents. Defaults to <c>AGG_PRESENT_MODE</c> (Fifo when unset); the
		/// automation harness sets Immediate, because a vsync wait per frame is wall time a test suite pays
		/// for nothing.
		/// </summary>
		public WGPUPresentMode PresentMode
		{
			get => this.surface?.PresentMode ?? PresentModeSettings.FromEnvironment();

			set
			{
				this.requestedPresentMode = value;
				if (this.surface != null)
				{
					this.surface.PresentMode = value;
				}
			}
		}

		/// <summary>
		/// Creates the device, the swapchain over the <c>CAMetalLayer</c>, and the compat context. Safe to
		/// call more than once.
		/// </summary>
		public void InitializeWebGpu()
		{
			if (this.isInitialized || this.isDisposed)
			{
				return;
			}

			// The surface is described to the constructor rather than made afterwards so that it exists
			// before the adapter is requested and can be passed as compatibleSurface - without that, wgpu
			// may pick an adapter that cannot present to this layer at all.
			//
			// Undefined rather than the Windows host's hardcoded D3D12: on macOS it resolves to Metal
			// unambiguously (MoltenVK is not present), so naming a backend would only be a way to be wrong.
			this.device = new WebGpuRenderDevice(
				this.UseSoftwareAdapter,
				WGPUBackendType.Undefined,
				"MacWebGpuLayer",
				WindowSurfaceRequest.ForMetalLayer(this.metalLayer, this.pixelWidth, this.pixelHeight, "window"));

			this.surface = this.device.WindowSurface;

			if (this.requestedPresentMode.HasValue)
			{
				this.surface.PresentMode = this.requestedPresentMode.Value;
			}

			this.compat = new GlCompatContext(this.device);
			this.Gl = new MatterHackers.RenderGl.OpenGl.GL(this.compat);

			// The scene compositor is a separate object from the context here, so the context forwards
			// INativeSceneRenderer to it - which is how RenderHelper and the editors find it - and it is
			// handed the facade the mesh render-data caches are keyed on.
			this.sceneRenderer = new WebGpuSceneRenderer(this.compat) { OwnerGl = this.Gl };
			this.compat.SceneRenderer = this.sceneRenderer;

			// Textures, display lists and tessellations cached against a previous context belong to a
			// device that no longer exists - the readers only notice through this generation bump.
			Graphics2DGpu.InvalidateGlCaches();

			this.CreateSizedTargets();
			this.isInitialized = true;
		}

		/// <summary>
		/// Acquires the frame's swapchain texture and points the compat context at it. Idempotent within
		/// a frame, because the window host calls it from every <c>NewGraphics2D</c>.
		/// </summary>
		public void BeginFrame()
		{
			if (!this.isInitialized)
			{
				return;
			}

			// wgpu reports device loss through a callback, not by failing the call that hit it, so the top
			// of a frame is the first place it can be acted on - and the only place where nothing is
			// half-recorded.
			if (this.device.IsDeviceLost && !this.TryRecoverDevice())
			{
				return;
			}

			if (this.compat.Passes.ColorTarget != null)
			{
				return;
			}

			IGpuTexture frame;
			try
			{
				using (FrameProfiler.Time("AcquireTexture"))
				{
					frame = this.surface.AcquireCurrentTexture();
				}
			}
			catch (Exception) when (this.TryRecoverIfDeviceLost())
			{
				// Recovered; this frame is skipped and the next one draws on the new device.
				return;
			}

			this.frameIsPresentable = frame != null;
			this.compat.SetRenderTarget(frame ?? this.EnsureScratchTarget(), this.depthTarget);
		}

		/// <summary>Ends the frame: submits everything recorded and presents it.</summary>
		public void Present()
		{
			if (!this.isInitialized)
			{
				return;
			}

			try
			{
				if (this.frameIsPresentable)
				{
					using (FrameProfiler.Time("PresentSwapchain"))
					{
						this.compat.Present(this.surface);
					}
				}
				else
				{
					// Nothing to show, but the recorded commands still have to reach the queue or the next
					// frame inherits a half-recorded encoder.
					this.compat.Submit();
				}
			}
			catch (Exception) when (this.TryRecoverIfDeviceLost())
			{
				return;
			}

			// Forgetting the target is what makes BeginFrame acquire again next time; the texture it
			// referred to was released by the present.
			this.compat.SetRenderTarget(null, null);
		}

		/// <summary>
		/// Reconfigures the swapchain and the sized targets for a new drawable size.
		/// </summary>
		/// <param name="newPixelWidth">The new width in device pixels (points times backingScaleFactor).</param>
		/// <param name="newPixelHeight">The new height in device pixels.</param>
		public void Resize(uint newPixelWidth, uint newPixelHeight)
		{
			newPixelWidth = Math.Max(1u, newPixelWidth);
			newPixelHeight = Math.Max(1u, newPixelHeight);

			this.pixelWidth = newPixelWidth;
			this.pixelHeight = newPixelHeight;

			if (!this.isInitialized)
			{
				return;
			}

			// A resize can arrive with a frame already open (AppKit can deliver a live-resize notification
			// from inside a nested tracking loop). Everything below frees the textures that frame is drawing
			// into: Configure drops the acquired swapchain texture, CreateSizedTargets disposes the depth and
			// scratch ones. So the frame's recorded work is submitted and the targets let go of first, and
			// the frame is marked unpresentable - its swapchain texture is gone.
			bool frameWasOpen = this.compat.Passes.ColorTarget != null;
			if (frameWasOpen)
			{
				this.compat.Submit();
				this.compat.SetRenderTarget(null, null);
				this.frameIsPresentable = false;
			}

			this.surface.Configure(newPixelWidth, newPixelHeight);
			this.CreateSizedTargets();

			if (frameWasOpen)
			{
				// Whatever is left of this frame's paint still has to land somewhere legal, and the scratch
				// target is exactly the "drawn but never shown" destination BeginFrame uses.
				this.compat.SetRenderTarget(this.EnsureScratchTarget(), this.depthTarget);
			}
		}

		/// <summary>
		/// Rebuilds the device, swapchain and compat context after a device loss.
		/// </summary>
		/// <returns>True if a working device now exists.</returns>
		public bool TryRecoverDevice()
		{
			if (this.isRecoveringDevice || this.isDisposed)
			{
				return false;
			}

			try
			{
				this.isRecoveringDevice = true;
				this.DisposeDeviceResources();
				this.InitializeWebGpu();
				this.deviceRecoveryCount++;

				return this.isInitialized;
			}
			catch
			{
				return false;
			}
			finally
			{
				this.isRecoveringDevice = false;
			}
		}

		/// <summary>
		/// Reads the frame currently being drawn back into a PNG at <paramref name="path"/>. Must be
		/// called after the widget draw and before <see cref="Present"/> - once presented, the frame's
		/// texture is gone.
		/// </summary>
		/// <param name="path">File to write; an existing file is replaced.</param>
		public async Task SaveCurrentFrameAsync(string path)
		{
			if (!this.isInitialized || this.compat.Passes.ColorTarget == null)
			{
				return;
			}

			var target = this.compat.Passes.ColorTarget;
			if ((target.Descriptor.Usage & TextureUsage.CopySrc) == 0)
			{
				throw new InvalidOperationException(
					"This swapchain's textures were not created with CopySrc, so the window cannot be read back.");
			}

			// The pass has to be closed before a copy can be recorded; ReadTextureAsync submits the rest.
			this.compat.Submit();

			int width = (int)target.Descriptor.Width;
			int height = (int)target.Descriptor.Height;
			uint rowStride = TextureFormatInfo.AlignedRowStride(target.Descriptor.Format, (uint)width);
			var bytes = new byte[rowStride * (long)height];
			var read = await this.device.ReadTextureAsync(target, bytes);

			var image = new ImageBuffer(width, height, 32, new BlenderBGRA());
			var buffer = image.GetBuffer();

			// wgpu rows run top down and agg's run bottom up.
			for (int y = 0; y < height; y++)
			{
				long sourceOffset = (height - 1 - y) * (long)read.RowStride;
				Array.Copy(bytes, sourceOffset, buffer, image.GetBufferOffsetY(y), width * 4);
			}

			image.MarkImageChanged();

			// ImageIO.SaveImageData will not overwrite, and a stale screenshot that looks fresh is worse
			// than no screenshot.
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}

			ImageIO.SaveImageData(path, image);
		}

		public void Dispose()
		{
			if (this.isDisposed)
			{
				return;
			}

			this.isDisposed = true;
			this.DisposeDeviceResources();

			// Same reason as on creation: everything cached against this device is about to be a handle to
			// freed memory.
			Graphics2DGpu.InvalidateGlCaches();
		}

		/// <summary>
		/// An exception filter: recovers and swallows the exception when wgpu has reported the device lost,
		/// and lets anything else propagate. Used as <c>catch (Exception) when (...)</c> so a genuine bug
		/// still throws with its original stack.
		/// </summary>
		private bool TryRecoverIfDeviceLost()
		{
			return this.device != null && this.device.IsDeviceLost && this.TryRecoverDevice();
		}

		private void DisposeDeviceResources()
		{
			this.isInitialized = false;
			this.frameIsPresentable = false;

			this.sceneRenderer?.Dispose();
			this.compat?.Dispose();
			this.depthTarget?.Dispose();
			this.scratchTarget?.Dispose();

			// The surface belongs to the device now (it was made before the adapter), so the device's
			// Dispose releases it - releasing it here as well would be a double free.
			this.device?.Dispose();

			this.sceneRenderer = null;
			this.compat = null;
			this.depthTarget = null;
			this.scratchTarget = null;
			this.surface = null;
			this.device = null;
			this.Gl = null;
		}

		/// <summary>
		/// Rebuilds the depth (and any scratch) target at the swapchain's current size. The caller must
		/// have let go of any open frame first (see <see cref="Resize"/>): this disposes textures a live
		/// pass could still be drawing into.
		/// </summary>
		private void CreateSizedTargets()
		{
			this.depthTarget?.Dispose();
			this.depthTarget = null;

			this.scratchTarget?.Dispose();
			this.scratchTarget = null;

			if (this.surface.Width == 0 || this.surface.Height == 0)
			{
				return;
			}

			this.depthTarget = this.device.CreateTexture(new TextureDescriptor(
				this.surface.Width,
				this.surface.Height,
				TextureFormat.Depth32Float,
				TextureUsage.RenderAttachment,
				1,
				1,
				"windowDepth"));
		}

		private IGpuTexture EnsureScratchTarget()
		{
			if (this.scratchTarget == null)
			{
				this.scratchTarget = this.device.CreateTexture(new TextureDescriptor(
					Math.Max(1u, this.surface.Width),
					Math.Max(1u, this.surface.Height),
					this.surface.Format,
					TextureUsage.RenderAttachment | TextureUsage.CopySrc,
					1,
					1,
					"windowScratch"));
			}

			return this.scratchTarget;
		}
	}
}
