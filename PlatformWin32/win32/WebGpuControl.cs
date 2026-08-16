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
using MatterHackers.Agg.Image;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
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
	/// <b>Device loss.</b> Recorded and reported (<see cref="LastError"/>), not repaired; recreating the
	/// device and invalidating every cache keyed on it is Phase 4 work.
	/// </para>
	/// </summary>
	public class WebGpuControl : Control
	{
		private WebGpuRenderDevice device;
		private WebGpuSurfaceTarget surface;
		private GlCompatContext compat;
		private IGpuTexture depthTarget;

		/// <summary>
		/// Where a frame goes when the swapchain has none to give. Drawing has to land somewhere legal or
		/// every widget draw in that frame throws; this is that somewhere.
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

		/// <summary>The facade the 2D stack draws through, or null before initialization.</summary>
		public MatterHackers.RenderGl.OpenGl.GL Gl { get; private set; }

		/// <summary>The compat context under the facade, for diagnostics.</summary>
		public GlCompatContext Compat => this.compat;

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
		public string LastError => this.device?.DeviceLostMessage ?? this.device?.LastUncapturedError;

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

			this.device = new WebGpuRenderDevice(false, WGPUBackendType.D3D12, "WebGpuControl");
			this.surface = this.device.CreateSurfaceTarget(
				this.Handle,
				IntPtr.Zero,
				(uint)Math.Max(1, this.ClientSize.Width),
				(uint)Math.Max(1, this.ClientSize.Height),
				"window");

			this.compat = new GlCompatContext(this.device);
			this.Gl = new MatterHackers.RenderGl.OpenGl.GL(this.compat);

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

			if (this.compat.Passes.ColorTarget != null)
			{
				return;
			}

			var frame = this.surface.AcquireCurrentTexture();
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

			if (this.frameIsPresentable)
			{
				this.compat.Present(this.surface);
			}
			else
			{
				// Nothing to show, but the recorded commands still have to reach the queue or the next
				// frame inherits a half-recorded encoder.
				this.compat.Submit();
			}

			// Forgetting the target is what makes BeginFrame acquire again next time; the texture it
			// referred to was released by the present.
			this.compat.SetRenderTarget(null, null);
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
				this.isInitialized = false;

				this.compat?.Dispose();
				this.depthTarget?.Dispose();
				this.scratchTarget?.Dispose();
				this.surface?.Dispose();
				this.device?.Dispose();

				this.compat = null;
				this.depthTarget = null;
				this.scratchTarget = null;
				this.surface = null;
				this.device = null;
				this.Gl = null;

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
