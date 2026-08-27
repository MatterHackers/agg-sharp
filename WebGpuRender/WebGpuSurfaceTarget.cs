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
using System.Collections.Generic;
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
	/// <para>
	/// <b>Sibling port.</b> This is the same object as <c>Gpu</c> in agg-gui-wgpu's <c>gpu.rs</c>, and the
	/// acquire/clamp/present-mode policies are deliberately kept in step with it - see
	/// <see cref="ActionFor"/>, <see cref="ClampSurfaceSize"/> and
	/// <see cref="ResolvePresentMode(WGPUPresentMode, IReadOnlyList{WGPUPresentMode})"/>. The
	/// alpha mode is the one intentional divergence (see <see cref="Configure"/>).
	/// </para>
	/// </summary>
	public sealed unsafe class WebGpuSurfaceTarget : ISurfaceTarget
	{
		/// <summary>
		/// <c>WGPUSurfaceGetCurrentTextureStatus_Occluded</c>, from wgpu-native's own <c>wgpu.h</c> rather
		/// than the standard <c>webgpu.h</c> - which is why it is a constant here instead of an enum member:
		/// the generated binding only covers the standard header. Metal-only, and only when the NSWindow
		/// reports itself occluded. Same policy row as agg-gui-wgpu's <c>Occluded</c> (<c>gpu.rs</c>): a
		/// plain skip with no redraw request.
		/// </summary>
		public const WGPUSurfaceGetCurrentTextureStatus OccludedStatus = (WGPUSurfaceGetCurrentTextureStatus)0x00030001;

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
		/// <para>
		/// The size is clamped by <see cref="ClampSurfaceSize"/> and the clamped values are what
		/// <see cref="Width"/> and <see cref="Height"/> report, so the depth and scratch targets the host
		/// sizes from them stay the same size as the swapchain. Mirrors
		/// <c>clamp_surface_size</c> in agg-gui-wgpu's <c>gpu.rs</c>.
		/// </para>
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

			(width, height) = ClampSurfaceSize(width, height, this.owner.Limits.MaxTextureDimension2D);

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
				// alpha-respecting composition would render text as see-through. This is an intentional
				// divergence from agg-gui-wgpu's pick_alpha_mode (which takes the surface's first
				// preference) - do not "align" it, it would break text on this stack.
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
		public IGpuTexture AcquireCurrentTexture() => this.AcquireCurrentTexture(out _);

		/// <summary>
		/// The texture this frame draws into, or null when the swapchain could not produce one.
		/// The C# side of <c>Gpu::acquire_frame</c> in agg-gui-wgpu's <c>gpu.rs</c>.
		/// </summary>
		/// <param name="redrawRequested">
		/// True when the frame was dropped for a reason that can still clear itself (a Timeout, or a
		/// swapchain that is still not ready after being reconfigured). A host that only paints on demand
		/// has to ask for another frame, or it waits for an unrelated event to wake it up and the window
		/// sits stale. False for the skips that would only repeat - an occluded window, a validation
		/// error - where a self-requested redraw is a busy loop.
		/// </param>
		/// <exception cref="InvalidOperationException">The swapchain failed for a reason a retry cannot fix.</exception>
		public IGpuTexture AcquireCurrentTexture(out bool redrawRequested)
		{
			redrawRequested = false;
			this.ThrowIfDisposed();
			if (this.frameTexture != null)
			{
				return this.frameTexture;
			}

			if (this.Width == 0 || this.Height == 0)
			{
				return null;
			}

			var acquired = this.TryAcquire(out SurfaceAcquireAction action);
			if (action == SurfaceAcquireAction.Reconfigure)
			{
				// Outdated/Lost is what a resize looks like from here; reconfiguring at the size we already
				// know rebuilds the swapchain against the window's current state.
				this.Configure(this.Width, this.Height);
				acquired = this.TryAcquire(out _);
				redrawRequested = acquired == null;
			}
			else if (action == SurfaceAcquireAction.SkipAndRetry)
			{
				redrawRequested = true;
			}

			this.frameTexture = acquired;
			return this.frameTexture;
		}

		/// <summary>
		/// Clamps a swapchain size to <c>[1, maxTextureDimension2D]</c> on both axes, so an over-large
		/// window - or a restored window size that arrived corrupted - degrades to what the GPU can do
		/// instead of failing wgpu's validation. Pure, so the policy is testable without a live surface.
		/// Mirrors <c>clamp_surface_size</c> in agg-gui-wgpu's <c>gpu.rs</c>.
		/// </summary>
		/// <param name="width">Requested width in pixels.</param>
		/// <param name="height">Requested height in pixels.</param>
		/// <param name="maxDimension">The device's max 2D texture dimension; zero is treated as one.</param>
		/// <returns>The size to configure the swapchain with.</returns>
		public static (uint Width, uint Height) ClampSurfaceSize(uint width, uint height, uint maxDimension)
		{
			maxDimension = Math.Max(1u, maxDimension);
			return (Math.Clamp(width, 1u, maxDimension), Math.Clamp(height, 1u, maxDimension));
		}

		/// <summary>
		/// How to handle one <c>wgpuSurfaceGetCurrentTexture</c> status. Pure, so the recovery policy is
		/// testable without a live GPU surface. The C# side of <c>surface_acquire_action</c> in
		/// agg-gui-wgpu's <c>gpu.rs</c>, and it answers the same way for every status both APIs share.
		/// <para>
		/// Outdated/Lost fire right after a resize, and after a driver reset (TDR), a display-mode change
		/// or an RDP reconnect: wgpu documents both as "reconfigure and try again", and treating them as a
		/// plain skip is what leaves a window black after a resize. Timeout is deliberately <em>not</em>
		/// one of them - the swapchain is still valid, so tearing it down and rebuilding it is both
		/// wasteful and a way to provoke the next Timeout.
		/// </para>
		/// </summary>
		/// <param name="status">The status the acquire reported.</param>
		public static SurfaceAcquireAction ActionFor(WGPUSurfaceGetCurrentTextureStatus status)
		{
			switch (status)
			{
				case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
				case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
					return SurfaceAcquireAction.Present;

				case WGPUSurfaceGetCurrentTextureStatus.Outdated:
				case WGPUSurfaceGetCurrentTextureStatus.Lost:
					return SurfaceAcquireAction.Reconfigure;

				case WGPUSurfaceGetCurrentTextureStatus.Timeout:
					return SurfaceAcquireAction.SkipAndRetry;

				// Occluded: the NSWindow is minimized or fully covered and asking Metal for a drawable
				// would block for up to a vsync. Error: the C API's validation status, which a retry
				// answers identically - the app has a bug to fix, and taking the window down over it (the
				// way an unknown status does) helps nobody.
				case OccludedStatus:
				case WGPUSurfaceGetCurrentTextureStatus.Error:
					return SurfaceAcquireAction.Skip;

				default:
					return SurfaceAcquireAction.Fail;
			}
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

			// Everything but the browser presents explicitly. In the browser the canvas is presented by the
			// page: whatever was drawn into the current texture appears when the animation-frame task that
			// acquired it ends, and there is no call to make it happen sooner. emdawnwebgpu still exports
			// wgpuSurfacePresent, but its body is an abort() - calling it does not fail the frame, it kills
			// the wasm module. The rest of this method is deliberately shared: a browser frame is just as
			// finished as a desktop one, its texture is released here, and hosts that pace themselves off
			// PresentedFrameCount must see it move on every platform.
			if (!OperatingSystem.IsBrowser())
			{
				wgpuSurfacePresent(this.surface);
			}

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

		/// <summary>The requested mode if this surface supports it, Fifo otherwise.</summary>
		/// <param name="requested">The mode asked for.</param>
		private WGPUPresentMode ResolvePresentMode(WGPUPresentMode requested)
			=> ResolvePresentMode(requested, this.supportedPresentModes);

		/// <summary>
		/// The requested mode if the surface supports it, Fifo otherwise - Fifo being the one mode WebGPU
		/// guarantees every surface has, which is why it is returned without being looked for. Pure, so the
		/// policy is testable without a live surface. Mirrors <c>pick_present_mode</c> in agg-gui-wgpu's
		/// <c>gpu.rs</c>, minus the <c>Auto*</c> modes - wgpu resolves those itself and the C API has no
		/// equivalent.
		/// <para>
		/// An empty or null <paramref name="supported"/> list therefore answers Fifo for everything rather
		/// than failing, which is what a browser surface needs: <c>AGG_PRESENT_MODE=immediate</c> travels
		/// into a wasm build's environment as easily as a desktop one, and a canvas paces itself off
		/// requestAnimationFrame no matter what is asked for.
		/// </para>
		/// </summary>
		/// <param name="requested">The mode asked for.</param>
		/// <param name="supported">Every mode the surface reports, or an empty list if it reports none.</param>
		public static WGPUPresentMode ResolvePresentMode(WGPUPresentMode requested, IReadOnlyList<WGPUPresentMode> supported)
		{
			if (requested == WGPUPresentMode.Fifo)
			{
				return requested;
			}

			if (supported != null)
			{
				foreach (var mode in supported)
				{
					if (mode == requested)
					{
						return requested;
					}
				}
			}

			return WGPUPresentMode.Fifo;
		}

		/// <summary>One acquire attempt. The status is turned into an <see cref="SurfaceAcquireAction"/> by
		/// <see cref="ActionFor"/>; everything but Present drops whatever texture came back and returns
		/// null, leaving the caller to act on <paramref name="action"/>.</summary>
		private WebGpuTexture TryAcquire(out SurfaceAcquireAction action)
		{
			var surfaceTexture = default(WGPUSurfaceTexture);
			wgpuSurfaceGetCurrentTexture(this.surface, &surfaceTexture);

			action = ActionFor(surfaceTexture.status);
			if (action != SurfaceAcquireAction.Present)
			{
				// Even a failed acquire can hand back a texture (wgpu-native does for Suboptimal-adjacent
				// statuses); it is ours to release either way, and the frame is skipped.
				if (!surfaceTexture.texture.IsNull)
				{
					wgpuTextureRelease(surfaceTexture.texture);
				}

				if (action == SurfaceAcquireAction.Fail)
				{
					throw new InvalidOperationException(
						$"wgpuSurfaceGetCurrentTexture on '{this.Label}' returned {surfaceTexture.status}. "
						+ (this.owner.DeviceLostMessage ?? this.owner.LastUncapturedError ?? "No wgpu error was reported."));
				}

				return null;
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
