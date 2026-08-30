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
using System.Windows.Forms;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.Scene;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The WebGPU sibling of <c>D3D11Control</c>: a WinForms control that owns the wgpu device, the
	/// swapchain over its own HWND, and the <see cref="GlCompatContext"/> the whole 2D stack draws
	/// through.
	/// <para>
	/// <b>Frame shape.</b> <see cref="BeginFrame"/> acquires the swapchain texture and points the compat
	/// context at it (plus a depth buffer, <see cref="TextureFormat.Depth32Float"/> to match the
	/// offscreen golden captures); widget paint then draws through <see cref="Gl"/>; <see cref="Present"/>
	/// submits and presents. A frame the swapchain cannot hand out - mid-resize, minimized - is drawn
	/// into a scratch texture and never presented, because widget paint has no way to be told "not this
	/// time".
	/// </para>
	/// <para>
	/// <b>Device loss.</b> wgpu reports it out of band through the device-lost callback, so it is noticed
	/// at the top of a frame (and when an acquire or present throws) rather than at the call that caused
	/// it. <see cref="TryRecoverDevice"/> then does what <c>D3D11Control.TryRecoverDevice</c> does: throw
	/// the device and everything hanging off it away, build a new one over the same HWND, and bump
	/// <see cref="Graphics2DGpu.InvalidateGlCaches"/> so every texture, tessellation and display list
	/// cached against the dead device is abandoned.
	/// </para>
	/// </summary>
	public class WebGpuControl : Control
	{
		private WebGpuRenderDevice device;
		private WebGpuSurfaceTarget surface;
		private GlCompatContext compat;
		private WebGpuSceneRenderer sceneRenderer;
		private IGpuTexture depthTarget;

		/// <summary>Guards against re-entering recovery from a failure raised by recovery itself.</summary>
		private bool isRecoveringDevice;

		/// <summary>
		/// Why this control has no device, when initialization gave up rather than throwing. Reported
		/// through <see cref="LastError"/>, which is the only channel a smoke run or an automation run has.
		/// </summary>
		private string deviceStartupError;

		/// <summary>How many times this control has rebuilt its device after a loss. Diagnostics and tests.</summary>
		private int deviceRecoveryCount;

		/// <summary>A present mode set before the swapchain existed, replayed onto it when it does.</summary>
		private WGPUPresentMode? requestedPresentMode;

		/// <summary>
		/// Where a frame goes when the swapchain has none to give. Drawing has to land somewhere legal or
		/// every widget draw in that frame throws; this is that somewhere.
		/// <para>
		/// agg-gui-wgpu has no equivalent yet - its shell can tell the paint "not this time", ours cannot -
		/// so this is the reference implementation of the idea, which the Rust side plans to adopt
		/// (agg-gui's <c>porting_update.md</c>). Do not delete it to "match" that port.
		/// </para>
		/// </summary>
		private IGpuTexture scratchTarget;

		private bool isInitialized;
		private bool frameIsPresentable;

		/// <summary>
		/// True while initialization is waiting for the HWND. Keeps the deferred retry hooked up exactly
		/// once, however many times the host calls <see cref="InitializeWebGpu"/> before the handle exists.
		/// </summary>
		private bool initializeWhenHandleCreated;

		public WebGpuControl()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
		}

		/// <summary>
		/// Gets or sets a value indicating whether to demand wgpu's software (fallback) adapter rather than
		/// real hardware. Must be set before <see cref="InitializeWebGpu"/>; changing it later does nothing
		/// until the device is rebuilt.
		/// </summary>
		/// <remarks>
		/// This is where <see cref="SystemWindow.UseGpu"/> - and therefore the FORCE_SOFTWARE_RENDERING
		/// command-line switch - actually lands. The deleted D3D11 host spent it on a WARP device request;
		/// wgpu's equivalent is <c>forceFallbackAdapter</c>, which is opt-in for the same reason WARP was:
		/// it costs roughly 100x the frame time, so it must never happen by accident.
		/// </remarks>
		[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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

		/// <summary>The backend wgpu chose (D3D12 on Windows), or Undefined before initialization.</summary>
		public WGPUBackendType BackendType => this.device?.AdapterBackend ?? WGPUBackendType.Undefined;

		/// <summary>
		/// The first thing wgpu complained about - a validation error or a lost device - or null while
		/// everything is well. A smoke run turns this into a non-zero exit code.
		/// </summary>
		public string LastError => this.deviceStartupError
			?? this.device?.DeviceLostMessage
			?? this.device?.LastUncapturedError;

		/// <summary>How many times this control has rebuilt its device after a loss; zero on a healthy run.</summary>
		public int DeviceRecoveryCount => this.deviceRecoveryCount;

		/// <summary>
		/// How the swapchain paces presents. Defaults to <c>AGG_PRESENT_MODE</c> (Fifo when unset); the
		/// automation harness sets Immediate, because a vsync wait per frame is wall time a test suite pays
		/// for nothing.
		/// </summary>
		[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
		/// Creates the device, the swapchain over this control's HWND, and the compat context. Safe to
		/// call more than once; a call made before the handle exists is retried when it does.
		/// </summary>
		public void InitializeWebGpu()
		{
			if (this.isInitialized || this.IsDisposed)
			{
				return;
			}

			if (!this.IsHandleCreated)
			{
				// There is no window to make a surface over yet. The host initializes from the form's
				// OnLoad, which can run before this child control has an HWND; simply returning left Gl
				// null and turned up much later as an NRE inside the first paint, so the attempt is
				// deferred to the handle instead of dropped.
				if (!this.initializeWhenHandleCreated)
				{
					this.initializeWhenHandleCreated = true;
					this.HandleCreated += this.InitializeOnHandleCreated;
				}

				return;
			}

			// A retry - device-loss recovery calls straight back in here - starts from a clean slate rather
			// than reporting the previous attempt's failure forever.
			this.deviceStartupError = null;

			// The surface is described to the constructor rather than made afterwards so that it exists
			// before the adapter is requested and can be passed as compatibleSurface - without that, wgpu
			// may pick an adapter that cannot present to this window at all.
			// Budgeted, because this runs from the form's Load - inside Show(), on the UI thread - and
			// acquiring an adapter and opening a device are synchronous native calls that have been seen not
			// to return on a loaded software rasterizer. A Show() that never returns is a window that never
			// appears and never paints, with nothing to report; see GpuStartup for the trade. The handle and
			// client size are read here, on the UI thread, rather than from the build thread.
			var surfaceRequest = new WindowSurfaceRequest(
				this.Handle,
				IntPtr.Zero,
				(uint)Math.Max(1, this.ClientSize.Width),
				(uint)Math.Max(1, this.ClientSize.Height),
				"window");

			this.device = GpuStartup.CreateWithinBudget(
				() => new WebGpuRenderDevice(
					this.UseSoftwareAdapter,
					WGPUBackendType.D3D12,
					"WebGpuControl",
					surfaceRequest),
				"WebGpuControl device");

			if (this.device == null)
			{
				// No device: leave this control uninitialized so every paint skips rather than throwing per
				// frame. LastError is what the host reports - a smoke run turns it into an exit code, and an
				// automation run prints it - so the failure is loud where silence used to be.
				this.deviceStartupError = "The GPU device could not be created: the adapter or device request "
					+ $"did not return within {GpuStartup.DefaultBudget.TotalSeconds:0.#}s.";
				Console.Error.WriteLine($"WebGpuControl: {this.deviceStartupError}");
				return;
			}

			this.surface = this.device.WindowSurface;

			if (this.requestedPresentMode.HasValue)
			{
				this.surface.PresentMode = this.requestedPresentMode.Value;
			}

			this.compat = new GlCompatContext(this.device);
			this.Gl = new MatterHackers.RenderGl.OpenGl.GL(this.compat);

			// The scene compositor is a separate object from the context here (the classic backend is one
			// class that is both), so the context forwards INativeSceneRenderer to it - which is how
			// RenderHelper and the editors find it - and it is handed the facade the mesh render-data
			// caches are keyed on, exactly as D3D11Control sets VorticeD3DGl.OwnerGl.
			this.sceneRenderer = new WebGpuSceneRenderer(this.compat) { OwnerGl = this.Gl };
			this.compat.SceneRenderer = this.sceneRenderer;

			// Textures, display lists and tessellations cached against a previous context belong to a
			// device that no longer exists - the readers only notice through this generation bump.
			Graphics2DGpu.InvalidateGlCaches();

			this.CreateSizedTargets();
			this.isInitialized = true;
		}

		private void InitializeOnHandleCreated(object sender, EventArgs e)
		{
			this.HandleCreated -= this.InitializeOnHandleCreated;
			this.initializeWhenHandleCreated = false;
			this.InitializeWebGpu();
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
				this.Invalidate();
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
		/// Rebuilds the device, swapchain and compat context after a device loss, the way
		/// <c>D3D11Control.TryRecoverDevice</c> does.
		/// </summary>
		/// <remarks>
		/// Everything cached against the old device - textures, tessellations, display lists, mesh vertex
		/// buffers - is keyed on the <see cref="Gl"/> facade and invalidated by the generation bump inside
		/// <see cref="InitializeWebGpu"/>, which is why a new facade is made rather than the old one
		/// re-pointed.
		/// </remarks>
		/// <returns>True if a working device now exists.</returns>
		public bool TryRecoverDevice()
		{
			if (this.isRecoveringDevice || this.IsDisposed || !this.IsHandleCreated)
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
					// so the window is showing whatever the compositor kept. Ask for a paint on the new
					// device rather than waiting for the next thing to invalidate.
					this.Invalidate();
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
		/// another thread with a time budget (see <see cref="GpuTeardown"/>) - on a software rasterizer it
		/// is minutes, and this runs on the UI thread inside <c>WM_CLOSE</c>. If the budget expires nothing
		/// is released at all: this control's window handle is destroyed moments later by
		/// <c>base.Dispose</c>, and a swapchain release racing that is a native crash where a leak is only
		/// a leak.
		/// <para>
		/// False for device-loss recovery, which is not on a deadline, has no window being destroyed, and
		/// wants the old device really gone before it builds the next one.
		/// </para>
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

			// The drain is the only part that may be abandoned, and the only part that does not touch this
			// control's window handle. Once it has returned the release below finds an idle queue and
			// nothing to wait for, so it finishes here, on this thread, before the handle goes away.
			if (!budgetTheGpuDrain
				|| GpuTeardown.DrainWithinBudget(closingDevice.WaitForGpuIdle, "WebGpuControl device"))
			{
				closingDevice.Dispose();
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

		// System.Windows.Forms.Keys spelled out: MatterHackers.Agg.UI has a Keys of its own, and this
		// namespace is that one.
		protected override bool IsInputKey(System.Windows.Forms.Keys keyData)
		{
			switch (keyData & System.Windows.Forms.Keys.KeyCode)
			{
				case System.Windows.Forms.Keys.Up:
				case System.Windows.Forms.Keys.Down:
				case System.Windows.Forms.Keys.Left:
				case System.Windows.Forms.Keys.Right:
				case System.Windows.Forms.Keys.Tab:
					return true;
			}

			return base.IsInputKey(keyData);
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			if (this.isInitialized && this.ClientSize.Width > 0 && this.ClientSize.Height > 0)
			{
				// A resize can arrive with a frame already open (WinForms pumps WM_SIZE from inside a
				// drag-resize's modal loop, and the widget draw is on the same thread). Everything below
				// frees the textures that frame is drawing into: Configure drops the acquired swapchain
				// texture, CreateSizedTargets disposes the depth and scratch ones. So the frame's recorded
				// work is submitted and the targets let go of first, and the frame is marked unpresentable
				// - its swapchain texture is gone, and Present must not try to show it.
				bool frameWasOpen = this.compat.Passes.ColorTarget != null;
				if (frameWasOpen)
				{
					this.compat.Submit();
					this.compat.SetRenderTarget(null, null);
					this.frameIsPresentable = false;
				}

				this.surface.Configure((uint)this.ClientSize.Width, (uint)this.ClientSize.Height);
				this.CreateSizedTargets();

				if (frameWasOpen)
				{
					// Whatever is left of this frame's paint still has to land somewhere legal, and the
					// scratch target is exactly the "drawn but never shown" destination BeginFrame uses.
					this.compat.SetRenderTarget(this.EnsureScratchTarget(), this.depthTarget);
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Budgeted: this runs from the form's OnClosed, on the UI thread, inside WM_CLOSE. A wait
				// that outlasts the budget there is a window that never finishes closing. base.Dispose below
				// destroys this control's window handle, which is exactly why an over-budget drain releases
				// nothing rather than releasing on another thread.
				this.DisposeDeviceResources(budgetTheGpuDrain: true);

				// Same reason as on creation: everything cached against this device is about to be a
				// handle to freed memory.
				Graphics2DGpu.InvalidateGlCaches();
			}

			base.Dispose(disposing);
		}

		/// <summary>
		/// Rebuilds the depth (and any scratch) target at the swapchain's current size. The caller must
		/// have let go of any open frame first (see <see cref="OnResize"/>): this disposes textures a live
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
