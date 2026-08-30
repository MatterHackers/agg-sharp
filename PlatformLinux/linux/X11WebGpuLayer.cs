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
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.Scene;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The X11 sibling of <c>WebGpuControl</c> and <c>MacWebGpuLayer</c>: it owns the wgpu device, the
	/// swapchain over an X11 window, and the <see cref="GlCompatContext"/> the whole 2D stack draws through.
	/// <para>
	/// <b>What is different from the Windows control.</b> There is no <c>Control</c> underneath, so there
	/// is no handle to wait for and no deferred-initialization dance: the window is created by the X11 host
	/// before this type is constructed, and an X11 drawable is a valid surface source the moment the server
	/// has it. Sizing is pushed in explicitly by the host (see <see cref="Resize"/>) rather than arriving as
	/// an <c>OnResize</c> event, and it is always in <em>device pixels</em> - X11 has no scaling of its own,
	/// so on X11 that is simply the window's size in pixels.
	/// </para>
	/// <para>
	/// <b>Frame shape</b> is identical to Windows' and macOS'. <see cref="BeginFrame"/> acquires the
	/// swapchain texture and points the compat context at it plus a depth buffer; widget paint draws through
	/// <see cref="Gl"/>; <see cref="Present"/> submits and presents. A frame the swapchain cannot hand out
	/// is drawn into a scratch texture and never presented, because widget paint has no way to be told
	/// "not this time".
	/// </para>
	/// </summary>
	public class X11WebGpuLayer : IDisposable
	{
		private readonly IntPtr display;
		private readonly ulong window;

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

		/// <summary>Creates the host for a window that already exists.</summary>
		/// <param name="display">The <c>Display*</c> the window lives on. Must stay open for this layer's life.</param>
		/// <param name="window">The X11 window XID wgpu will make its surface over.</param>
		/// <param name="pixelWidth">Initial swapchain width in device pixels.</param>
		/// <param name="pixelHeight">Initial swapchain height in device pixels.</param>
		public X11WebGpuLayer(IntPtr display, ulong window, uint pixelWidth, uint pixelHeight)
		{
			if (display == IntPtr.Zero)
			{
				throw new ArgumentNullException(nameof(display));
			}

			// Zero is X11's None, never a real window - see WindowSurfaceRequest.ForXlibWindow.
			if (window == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(window), "An X11 surface needs a window XID; zero is None.");
			}

			this.display = display;
			this.window = window;
			this.pixelWidth = Math.Max(1u, pixelWidth);
			this.pixelHeight = Math.Max(1u, pixelHeight);
		}

		/// <summary>
		/// Gets or sets a value indicating whether to demand wgpu's software (fallback) adapter rather than
		/// real hardware. Must be set before <see cref="InitializeWebGpu"/>.
		/// </summary>
		public bool UseSoftwareAdapter { get; set; }

		/// <summary>
		/// Gets or sets what to call when a frame was dropped for a reason that can still clear itself, or
		/// when the device was rebuilt after a loss - the host's "paint again soon". The Windows control
		/// calls <c>Control.Invalidate</c> here; the host sets this to whatever schedules its next paint.
		/// Unset means the host paints continuously and does not need waking.
		/// </summary>
		public Action RequestRedraw { get; set; }

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

		/// <summary>The backend wgpu chose (Vulkan on Linux), or Undefined before initialization.</summary>
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
		/// Creates the device, the swapchain over the X11 window, and the compat context. Safe to call more
		/// than once.
		/// </summary>
		public void InitializeWebGpu()
		{
			if (this.isInitialized || this.isDisposed)
			{
				return;
			}

			// The surface is described to the constructor rather than made afterwards so that it exists
			// before the adapter is requested and can be passed as compatibleSurface - without that, wgpu
			// may pick an adapter that cannot present to this window at all.
			//
			// Undefined rather than the Windows host's hardcoded D3D12: on Linux it resolves to Vulkan,
			// which is the only backend wgpu can present an Xlib surface through, so naming a backend would
			// only be a way to be wrong.
			this.device = new WebGpuRenderDevice(
				this.UseSoftwareAdapter,
				WGPUBackendType.Undefined,
				"X11WebGpuLayer",
				WindowSurfaceRequest.ForXlibWindow(this.display, this.window, this.pixelWidth, this.pixelHeight, "window"));

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
			bool redrawRequested;
			try
			{
				using (FrameProfiler.Time("AcquireTexture"))
				{
					frame = this.surface.AcquireCurrentTexture(out redrawRequested);
				}
			}
			catch (Exception) when (this.TryRecoverIfDeviceLost())
			{
				// Recovered; this frame is skipped and the next one draws on the new device.
				return;
			}

			if (redrawRequested)
			{
				// The swapchain dropped this frame for something that clears itself (a Timeout, or a
				// reconfigure that has not taken yet). Without asking for a paint, the window would sit on
				// the last presented frame until some unrelated event happened to invalidate it.
				this.RequestRedraw?.Invoke();
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
		/// <param name="newPixelWidth">The new width in device pixels.</param>
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

			// A resize can arrive with a frame already open (the host paints straight out of a
			// ConfigureNotify while a resize burst is in flight). Everything below frees the textures that
			// frame is drawing into: Configure drops the acquired swapchain texture, CreateSizedTargets
			// disposes the depth and scratch ones. So the frame's recorded work is submitted and the targets
			// let go of first, and the frame is marked unpresentable - its swapchain texture is gone.
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

				if (this.isInitialized)
				{
					// The frame that hit the loss was abandoned and the new swapchain has never presented,
					// so ask for a paint on the new device rather than waiting to be invalidated.
					this.RequestRedraw?.Invoke();
				}

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
		public Task SaveCurrentFrameAsync(string path)
		{
			if (!this.isInitialized)
			{
				return Task.CompletedTask;
			}

			// The flip and the encode are shared with every other host (see GpuFrameCapture): a capture that
			// differed per platform would fail goldens for a reason nobody would look for in a window host.
			return GpuFrameCapture.SaveColorTargetAsync(this.compat, path);
		}

		public void Dispose()
		{
			if (this.isDisposed)
			{
				return;
			}

			this.isDisposed = true;

			// Budgeted: this runs on the UI thread as the window closes, and releasing the swapchain waits
			// for everything still submitted (see GpuTeardown). A software Vulkan ICD (lavapipe, which is
			// what a headless CI box has) is exactly the case where that wait is minutes rather than
			// microseconds.
			this.DisposeDeviceResources(budgetTheGpuDrain: true);

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

		/// <summary>
		/// Releases the device and everything built on it.
		/// </summary>
		/// <param name="budgetTheGpuDrain">
		/// True on the window-close path. The wait for the GPU to finish what it was given is then paid on
		/// another thread with a time budget (see <see cref="GpuTeardown"/>), and if the budget expires
		/// nothing is released at all. That matters most here: <c>X11SystemWindow.DestroyNativeWindow</c>
		/// calls <c>XDestroyWindow</c> on the line after this, and the surface release is a set of X
		/// requests on a display this process shares without <c>XInitThreads</c> - issuing them from another
		/// thread would tear the protocol stream and reach Xlib's fatal handler, which aborts the process
		/// where a leak only leaks. False for device-loss recovery, which is not on a deadline, has no
		/// window being destroyed, and wants the old device really gone before it builds the next one.
		/// </param>
		private void DisposeDeviceResources(bool budgetTheGpuDrain = false)
		{
			this.isInitialized = false;
			this.frameIsPresentable = false;

			// Before the drain below. None of these submits - they end passes and release resources - but a
			// wgpu release only marks an object for destruction, which a poll is what actually performs.
			// Letting go of them first is therefore what gives the drain something to clean up, instead of
			// leaving every buffer and texture pending behind a device that is never polled again.
			this.sceneRenderer?.Dispose();
			this.compat?.Dispose();
			this.depthTarget?.Dispose();
			this.scratchTarget?.Dispose();

			// The surface belongs to the device now (it was made before the adapter), so the device's
			// Dispose releases it - releasing it here as well would be a double free.
			var closingDevice = this.device;

			this.sceneRenderer = null;
			this.compat = null;
			this.depthTarget = null;
			this.scratchTarget = null;
			this.surface = null;
			this.device = null;
			this.Gl = null;

			if (closingDevice == null)
			{
				return;
			}

			// The drain is the only part that may be abandoned, and the only part that speaks no X: it is a
			// queue fence wait. Once it has returned the release below finds an idle queue and nothing to
			// wait for, so it finishes here, on the thread that owns the display, before the window is
			// destroyed.
			if (!budgetTheGpuDrain
				|| GpuTeardown.DrainWithinBudget(closingDevice.WaitForGpuIdle, "X11WebGpuLayer device"))
			{
				closingDevice.Dispose();
			}
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
