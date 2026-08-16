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
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// A window swapchain: one <c>WGPUSurface</c> over a native window handle, plus the configuration
	/// that has to be redone every time the window changes size.
	/// <para>
	/// <b>Frame shape.</b> <see cref="AcquireCurrentTexture"/> once per frame, draw into it, then
	/// <see cref="IRenderDevice.Present"/>. The acquired texture is owned by the surface and released by
	/// the present; a caller must not dispose it or hold it past the present.
	/// </para>
	/// <para>
	/// <b>A frame can legitimately be unavailable.</b> Between a window resize and the reconfigure that
	/// follows it, wgpu answers Outdated. This class reconfigures and retries once, and then returns
	/// null rather than throwing - a dropped frame during a drag-resize is normal, not an error.
	/// </para>
	/// <para>
	/// <b>Device loss is not recovered here.</b> The surface only reports it (through the device's
	/// device-lost callback and a clear exception from the next acquire); recreating the device and the
	/// caches hanging off it is the window host's job, and Phase 4's.
	/// </para>
	/// </summary>
	public sealed unsafe class WebGpuSurfaceTarget : ISurfaceTarget
	{
		private readonly WebGpuRenderDevice owner;
		private readonly WGPUPresentMode[] supportedPresentModes;
		private WGPUSurface surface;
		private WebGpuTexture frameTexture;
		private WGPUPresentMode presentMode;

		internal WebGpuSurfaceTarget(
			WebGpuRenderDevice owner,
			WGPUSurface surface,
			WGPUTextureFormat surfaceFormat,
			TextureUsage usage,
			WGPUPresentMode[] supportedPresentModes,
			string label)
		{
			this.owner = owner;
			this.surface = surface;
			this.SurfaceFormat = surfaceFormat;
			this.Format = WgpuEnums.ToRenderCore(surfaceFormat);
			this.Usage = usage;
			this.supportedPresentModes = supportedPresentModes ?? Array.Empty<WGPUPresentMode>();
			this.Label = label ?? "surface";

			// Fifo unless the environment asks otherwise: it is the only mode WebGPU guarantees, and it is
			// what an interactive window wants (vsync, no tearing, no spinning the GPU). The automation
			// harness sets AGG_PRESENT_MODE=immediate, where waiting for the display is pure wall time.
			this.presentMode = this.ResolvePresentMode(PresentModeSettings.FromEnvironment());
		}

		/// <inheritdoc/>
		public string Label { get; }

		/// <inheritdoc/>
		public TextureFormat Format { get; }

		/// <summary>The wgpu-level format the swapchain was configured with.</summary>
		public WGPUTextureFormat SurfaceFormat { get; }

		/// <summary>The usage flags the swapchain textures carry; includes CopySrc so a frame can be read back.</summary>
		public TextureUsage Usage { get; }

		/// <inheritdoc/>
		public uint Width { get; private set; }

		/// <inheritdoc/>
		public uint Height { get; private set; }

		/// <summary>True once <see cref="Dispose"/> has been called.</summary>
		public bool IsDisposed { get; private set; }

		/// <summary>
		/// The texture acquired for the frame in progress, or null when no frame is in flight. The window
		/// host reads this when it needs the frame's pixels (a screenshot) before presenting.
		/// </summary>
		public IGpuTexture CurrentTexture => this.frameTexture;

		/// <summary>How many frames this surface has presented. Frame pacing checks read it.</summary>
		public long PresentedFrameCount { get; private set; }

		/// <summary>
		/// How the swapchain paces presents. Defaults to what <c>AGG_PRESENT_MODE</c> asks for, falling
		/// back to Fifo; setting it reconfigures the swapchain, and a mode this surface does not support is
		/// silently downgraded to Fifo rather than failing the window.
		/// </summary>
		public WGPUPresentMode PresentMode
		{
			get => this.presentMode;

			set
			{
				var resolved = this.ResolvePresentMode(value);
				if (resolved == this.presentMode)
				{
					return;
				}

				this.presentMode = resolved;
				if (this.Width != 0 && this.Height != 0)
				{
					this.Configure(this.Width, this.Height);
				}
			}
		}

		internal WGPUSurface Handle => this.surface;

		/// <summary>
		/// (Re)configures the swapchain for a new size. WebGPU has no implicit resize: once the window no
		/// longer matches the configured size, every acquire answers Outdated until this is called.
		/// </summary>
		/// <param name="width">Width in pixels; zero (a minimized window) is ignored.</param>
		/// <param name="height">Height in pixels; zero is ignored.</param>
		public void Configure(uint width, uint height)
		{
			this.ThrowIfDisposed();
			if (width == 0 || height == 0)
			{
				return;
			}

			// A configure while a frame is acquired would leave that texture pointing at a swapchain that
			// no longer exists, so the frame is dropped first.
			this.ReleaseFrameTexture();

			var configuration = new WGPUSurfaceConfiguration
			{
				device = this.owner.DeviceHandle,
				format = this.SurfaceFormat,
				usage = WgpuEnums.ToWgpu(this.Usage),
				width = width,
				height = height,

				// Opaque, not Auto: the LCD text passes deliberately never write alpha, so any
				// alpha-respecting composition would render text as see-through.
				alphaMode = WGPUCompositeAlphaMode.Opaque,
				presentMode = this.presentMode,
			};

			wgpuSurfaceConfigure(this.surface, &configuration);
			this.Width = width;
			this.Height = height;
		}

		/// <summary>
		/// The texture this frame draws into, or null when the swapchain could not produce one (the
		/// window is mid-resize or minimized) - the caller should skip the frame in that case.
		/// </summary>
		/// <exception cref="InvalidOperationException">The swapchain failed for a reason a retry cannot fix.</exception>
		public IGpuTexture AcquireCurrentTexture()
		{
			this.ThrowIfDisposed();
			if (this.frameTexture != null)
			{
				return this.frameTexture;
			}

			if (this.Width == 0 || this.Height == 0)
			{
				return null;
			}

			var acquired = this.TryAcquire(out bool retryWorthwhile);
			if (acquired == null && retryWorthwhile)
			{
				// Outdated/Lost is what a resize looks like from here; reconfiguring at the size we already
				// know rebuilds the swapchain against the window's current state.
				this.Configure(this.Width, this.Height);
				acquired = this.TryAcquire(out _);
			}

			this.frameTexture = acquired;
			return this.frameTexture;
		}

		/// <summary>
		/// Presents the acquired frame and releases it. Called by
		/// <see cref="WebGpuRenderDevice.Present"/>, which has already submitted the frame's commands.
		/// </summary>
		internal void PresentFrame()
		{
			this.ThrowIfDisposed();
			if (this.frameTexture == null)
			{
				// Presenting without a frame is not an error worth throwing over: a host that skipped a
				// dropped frame's draws still calls Present in its normal end-of-frame path.
				return;
			}

			wgpuSurfacePresent(this.surface);
			this.ReleaseFrameTexture();
			this.PresentedFrameCount++;
		}

		internal bool BelongsTo(WebGpuRenderDevice device) => ReferenceEquals(this.owner, device);

		/// <summary>Drops any frame in flight, unconfigures the swapchain and releases the surface.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			this.ReleaseFrameTexture();

			if (!this.surface.IsNull)
			{
				wgpuSurfaceUnconfigure(this.surface);
				wgpuSurfaceRelease(this.surface);
				this.surface = default;
			}
		}

		/// <summary>The requested mode if this surface supports it, Fifo otherwise (the one mode WebGPU
		/// guarantees every surface has).</summary>
		/// <param name="requested">The mode asked for.</param>
		private WGPUPresentMode ResolvePresentMode(WGPUPresentMode requested)
		{
			if (requested == WGPUPresentMode.Fifo)
			{
				return requested;
			}

			foreach (var supported in this.supportedPresentModes)
			{
				if (supported == requested)
				{
					return requested;
				}
			}

			return WGPUPresentMode.Fifo;
		}

		private WebGpuTexture TryAcquire(out bool retryWorthwhile)
		{
			retryWorthwhile = false;

			var surfaceTexture = default(WGPUSurfaceTexture);
			wgpuSurfaceGetCurrentTexture(this.surface, &surfaceTexture);

			switch (surfaceTexture.status)
			{
				case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
				case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
					break;

				case WGPUSurfaceGetCurrentTextureStatus.Timeout:
				case WGPUSurfaceGetCurrentTextureStatus.Outdated:
				case WGPUSurfaceGetCurrentTextureStatus.Lost:
					if (!surfaceTexture.texture.IsNull)
					{
						wgpuTextureRelease(surfaceTexture.texture);
					}

					retryWorthwhile = true;
					return null;

				default:
					throw new InvalidOperationException(
						$"wgpuSurfaceGetCurrentTexture on '{this.Label}' returned {surfaceTexture.status}. "
						+ (this.owner.DeviceLostMessage ?? this.owner.LastUncapturedError ?? "No wgpu error was reported."));
			}

			WGPUTextureView view = wgpuTextureCreateView(surfaceTexture.texture, null);

			// The descriptor is authored rather than queried: it has to describe the texture the compat
			// layer will render into (size, format, usage), and those are exactly what this surface was
			// configured with.
			var descriptor = new TextureDescriptor(
				this.Width,
				this.Height,
				this.Format,
				this.Usage,
				1,
				1,
				this.Label);

			return new WebGpuTexture(surfaceTexture.texture, view, descriptor);
		}

		private void ReleaseFrameTexture()
		{
			this.frameTexture?.Dispose();
			this.frameTexture = null;
		}

		private void ThrowIfDisposed()
		{
			if (this.IsDisposed)
			{
				throw new ObjectDisposedException(nameof(WebGpuSurfaceTarget));
			}
		}
	}
}
