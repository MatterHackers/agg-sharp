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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The browser sibling of <c>MacWebGpuLayer</c>: it owns the WebGPU device, the swapchain over the
	/// page's <c>&lt;canvas&gt;</c>, and the <see cref="GlCompatContext"/> the whole 2D stack draws through.
	/// </summary>
	/// <remarks>
	/// <para><b>The frame texture dies with the animation frame task.</b> This is the one rule that makes
	/// this type different from every desktop layer, and getting it wrong is not a dropped frame, it is a
	/// permanently dead canvas. In the browser the surface's current texture is only valid inside the
	/// <c>requestAnimationFrame</c> task that acquired it; the page presents whatever was drawn into it when
	/// that task returns, and the texture is invalid from then on. <see cref="WebGpuSurfaceTarget"/> caches
	/// an acquired texture and hands the same one back until it is presented, so a frame that ended without
	/// reaching <see cref="EndFrame"/> would leave a dead handle in the swapchain that every later frame
	/// would draw into. That is why <see cref="EndFrame"/> is unconditional, is called from a
	/// <c>finally</c>, and never throws: the very first paint exception must not be able to carry an
	/// acquired texture out of the frame. macOS may legally hold a texture across frames and its layer
	/// therefore has no such rule.</para>
	///
	/// <para><b>Initialization is asynchronous, and nothing else here is.</b> The adapter and the device are
	/// Promises (see <see cref="WebGpuRenderDevice.CreateAsync"/>), so a page has to have somewhere to be
	/// while it waits; the host gates on <see cref="IsWebGpuInitialized"/> and paints nothing until it turns
	/// true. Everything after that - begin, draw, end, resize - is synchronous, because it all has to fit
	/// inside one animation frame callback.</para>
	///
	/// <para><b>No present mode.</b> The desktop layers replay a requested <c>WGPUPresentMode</c> onto the
	/// swapchain. A canvas paces itself off <c>requestAnimationFrame</c> and WebGPU has no present call at
	/// all in the browser, so there is nothing to ask for.</para>
	/// </remarks>
	public class BrowserWebGpuLayer : IDisposable
	{
		private readonly string canvasSelector;

		private WebGpuRenderDevice device;
		private WebGpuSurfaceTarget surface;
		private GlCompatContext compat;
		private WebGpuSceneRenderer sceneRenderer;
		private IGpuTexture depthTarget;

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

		/// <summary>Set once a loss has been reported, so the host is asked to rebuild exactly once.</summary>
		private bool deviceLossReported;

		/// <summary>Creates the layer for a canvas that already exists in the page.</summary>
		/// <param name="canvasSelector">The CSS selector naming the canvas, e.g. <c>"#agg-canvas"</c>.</param>
		/// <param name="pixelWidth">Initial swapchain width in device pixels (the canvas's backing store).</param>
		/// <param name="pixelHeight">Initial swapchain height in device pixels.</param>
		public BrowserWebGpuLayer(string canvasSelector, uint pixelWidth, uint pixelHeight)
		{
			if (string.IsNullOrWhiteSpace(canvasSelector))
			{
				throw new ArgumentException("A browser render layer needs the CSS selector of its canvas.", nameof(canvasSelector));
			}

			this.canvasSelector = canvasSelector;
			this.pixelWidth = Math.Max(1u, pixelWidth);
			this.pixelHeight = Math.Max(1u, pixelHeight);
		}

		/// <summary>
		/// Gets or sets what to call when a frame was dropped for a reason that can still clear itself - the
		/// host's "paint again soon". Unset means the host paints continuously and does not need waking.
		/// </summary>
		public Action RequestRedraw { get; set; }

		/// <summary>
		/// Gets or sets what to call when the device has been lost and this layer has torn itself down. The
		/// host is expected to build a new layer through the same asynchronous path it used at start-up; a
		/// device cannot be rebuilt synchronously in the browser, so recovery cannot live here (see
		/// <see cref="WebGpuRenderDevice.CreateAsync"/>).
		/// </summary>
		public Action DeviceLost { get; set; }

		/// <summary>The facade the 2D stack draws through, or null before initialization.</summary>
		public MatterHackers.RenderGl.OpenGl.GL Gl { get; private set; }

		/// <summary>The compat context under the facade, for diagnostics.</summary>
		public GlCompatContext Compat => this.compat;

		/// <summary>The 3D scene compositor, or null before initialization.</summary>
		public INativeSceneRenderer SceneRenderer => this.sceneRenderer;

		/// <summary>The WebGPU device, for diagnostics and error reporting.</summary>
		public WebGpuRenderDevice Device => this.device;

		/// <summary>The swapchain over the canvas.</summary>
		public WebGpuSurfaceTarget Surface => this.surface;

		/// <summary>True once the device and swapchain exist. What the host's paint gate reads.</summary>
		public bool IsWebGpuInitialized => this.isInitialized;

		/// <summary>True once <see cref="Dispose"/> has run.</summary>
		public bool IsDisposed => this.isDisposed;

		/// <summary>
		/// The first thing WebGPU complained about - a validation error or a lost device - or null while
		/// everything is well.
		/// </summary>
		public string LastError => this.device?.DeviceLostMessage ?? this.device?.LastUncapturedError;

		/// <summary>The swapchain's current width in device pixels.</summary>
		public uint PixelWidth => this.pixelWidth;

		/// <summary>The swapchain's current height in device pixels.</summary>
		public uint PixelHeight => this.pixelHeight;

		/// <summary>
		/// Creates the device, the swapchain over the canvas, and the compat context. Safe to call more than
		/// once; the second call completes immediately.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// The browser reported no usable adapter or refused the device. The host turns this into the
		/// "this browser cannot run the application" message - there is no software fallback below WebGPU.
		/// </exception>
		public async Task InitializeWebGpuAsync()
		{
			if (this.isInitialized || this.isDisposed)
			{
				return;
			}

			VerifyWasm32StructLayouts();

			// The surface is described to CreateAsync rather than made afterwards so that it exists before
			// the adapter is requested and can be passed as compatibleSurface - the same ordering rule the
			// desktop layers follow, and the reason WindowSurfaceRequest is a constructor argument at all.
			WebGpuRenderDevice created = await WebGpuRenderDevice.CreateAsync(
				WindowSurfaceRequest.ForBrowserCanvas(this.canvasSelector, this.pixelWidth, this.pixelHeight, "canvas"),
				"BrowserWebGpuLayer");

			if (this.isDisposed)
			{
				// The window closed while the adapter promise was in flight. Nothing is going to draw
				// through this device, and leaving it alive would hold the canvas's WebGPU context.
				created.Dispose();
				return;
			}

			this.device = created;
			this.surface = this.device.WindowSurface;

			this.compat = new GlCompatContext(this.device);
			this.Gl = new MatterHackers.RenderGl.OpenGl.GL(this.compat);

			// The scene compositor is a separate object from the context, so the context forwards
			// INativeSceneRenderer to it - which is how RenderHelper and the editors find depth peeling -
			// and it is handed the facade the mesh render-data caches are keyed on.
			this.sceneRenderer = new WebGpuSceneRenderer(this.compat) { OwnerGl = this.Gl };
			this.compat.SceneRenderer = this.sceneRenderer;

			// Textures, display lists and tessellations cached against a previous context belong to a
			// device that no longer exists - the readers only notice through this generation bump.
			Graphics2DGpu.InvalidateGlCaches();

			this.CreateSizedTargets();
			this.isInitialized = true;
		}

		/// <summary>
		/// Acquires the frame's swapchain texture and points the compat context at it. Idempotent within a
		/// frame, because the window host calls it from every <c>NewGraphics2D</c>.
		/// </summary>
		public void BeginFrame()
		{
			if (!this.isInitialized)
			{
				return;
			}

			// WebGPU reports device loss through a callback, not by failing the call that hit it, so the top
			// of a frame is the first place it can be acted on - and the only place where nothing is
			// half-recorded. Unlike the desktop layers this cannot recover in place: rebuilding needs an
			// await, so all this does is stand down and tell the host to start a new layer.
			if (this.device.IsDeviceLost)
			{
				this.HandleDeviceLost();
				return;
			}

			if (this.compat.Passes.ColorTarget != null)
			{
				return;
			}

			IGpuTexture frame = this.surface.AcquireCurrentTexture(out bool redrawRequested);

			if (redrawRequested)
			{
				// The swapchain dropped this frame for something that clears itself. Without asking for a
				// paint, the canvas would sit on the last presented frame until some unrelated event
				// happened to invalidate it.
				this.RequestRedraw?.Invoke();
			}

			this.frameIsPresentable = frame != null;
			this.compat.SetRenderTarget(frame ?? this.EnsureScratchTarget(), this.depthTarget);
		}

		/// <summary>
		/// Ends the frame: submits everything recorded, lets go of the swapchain texture, and forgets the
		/// render target. <b>Must run on every frame, including one whose paint threw</b> - see the class
		/// remarks for what an acquired texture that outlives its animation frame costs. That is also why
		/// this reports rather than throws: it is called from a <c>finally</c>, and a throw here would both
		/// replace the paint's own exception and leave the swapchain holding a dead texture.
		/// </summary>
		public void EndFrame()
		{
			if (!this.isInitialized)
			{
				return;
			}

			try
			{
				if (this.frameIsPresentable)
				{
					// "Present" is a misnomer in the browser and deliberately still the call made: the page
					// presents the canvas by itself when this task ends, and what this does here is submit
					// the recorded work and release the frame texture. See WebGpuSurfaceTarget.PresentFrame.
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
			catch (Exception endFrameException)
			{
				Console.Error.WriteLine(
					$"BrowserWebGpuLayer.EndFrame failed; the frame is abandoned: {endFrameException}");

				this.AbandonFrameTexture();
			}
			finally
			{
				this.frameIsPresentable = false;

				// Forgetting the target is what makes BeginFrame acquire again next time.
				this.compat?.SetRenderTarget(null, null);
			}
		}

		/// <summary>
		/// Reconfigures the swapchain and the sized targets for a new canvas backing store.
		/// </summary>
		/// <param name="newPixelWidth">The new width in device pixels.</param>
		/// <param name="newPixelHeight">The new height in device pixels.</param>
		public void Resize(uint newPixelWidth, uint newPixelHeight)
		{
			newPixelWidth = Math.Max(1u, newPixelWidth);
			newPixelHeight = Math.Max(1u, newPixelHeight);

			if (newPixelWidth == this.pixelWidth && newPixelHeight == this.pixelHeight)
			{
				return;
			}

			this.pixelWidth = newPixelWidth;
			this.pixelHeight = newPixelHeight;

			if (!this.isInitialized)
			{
				return;
			}

			// A resize arrives from a queued resize event, which is drained at the top of a tick and so
			// never inside a frame - but the desktop hosts learned this the hard way (AppKit delivers live
			// resizes from a nested loop), and everything below frees the textures an open frame would be
			// drawing into. So an open frame is submitted and let go of first, exactly as on the mac.
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
		/// Measures the binding's structs against the wasm32 C layout and says so on the console, once, at
		/// bring-up.
		/// </summary>
		/// <remarks>
		/// Debug only, and a report rather than a throw. It is the browser's substitute for the validation
		/// wgpu-native does on the desktop: emdawnwebgpu reads fields out of wasm memory at fixed offsets
		/// and hands what it finds to the WebGPU IDL, so a wrong struct size is not a validation error, it
		/// is a nonsense value in an unrelated field several calls later. The table it checks against is
		/// generated beside the binding (never hand-kept); see <see cref="WGPUStructLayoutsWasm32"/>. On a
		/// 64 bit target the check is a no-op - the desktop table is the one that applies there, and the
		/// binding tests assert it.
		/// </remarks>
		private static void VerifyWasm32StructLayouts()
		{
#if DEBUG
			string mismatches = WGPUStructLayoutsWasm32.DescribeSizeMismatches();

			if (mismatches == null)
			{
				Console.WriteLine($"wasm32 struct layout self-check: clean ({WGPUStructLayoutsWasm32.All.Length} structs).");
				return;
			}

			Console.Error.WriteLine(
				"wasm32 struct layout self-check FAILED. Every WebGPU descriptor this process fills is "
				+ $"suspect, and the failures will look like anything but this: {mismatches}");
#endif
		}

		/// <summary>
		/// Stands the layer down after a lost device and asks the host for a new one. Reported once: the
		/// loss flag stays set on a dead device, and the host's rebuild is already in flight.
		/// </summary>
		private void HandleDeviceLost()
		{
			if (this.deviceLossReported)
			{
				return;
			}

			this.deviceLossReported = true;

			Console.Error.WriteLine(
				$"BrowserWebGpuLayer: the WebGPU device was lost ({this.device?.DeviceLostMessage ?? "no message"}). "
				+ "The canvas stops painting until a new device has been created.");

			this.DisposeDeviceResources();

			this.DeviceLost?.Invoke();
		}

		/// <summary>
		/// The last resort when <see cref="EndFrame"/>'s present threw: presenting is the only way to let go
		/// of the swapchain's frame texture, and letting go is what must happen before this animation frame
		/// task returns whatever else went wrong.
		/// </summary>
		private void AbandonFrameTexture()
		{
			try
			{
				// The pass has to be closed before a present is legal, and a failed submit may have left one
				// open.
				this.compat.FlushPass();
				this.device.Present(this.surface);
			}
			catch (Exception abandonException)
			{
				// Nothing further can be done - the device is in a state that will not even let a frame go.
				// The host's next tick finds it lost (or its draws keep failing loudly), which is the honest
				// outcome; swallowing this quietly would hide it.
				Console.Error.WriteLine(
					$"BrowserWebGpuLayer could not release the frame texture; this canvas is unlikely to paint again: {abandonException}");
			}
		}

		private void DisposeDeviceResources()
		{
			this.isInitialized = false;
			this.frameIsPresentable = false;

			this.sceneRenderer?.Dispose();
			this.compat?.Dispose();
			this.depthTarget?.Dispose();
			this.scratchTarget?.Dispose();

			// The surface belongs to the device (it was made before the adapter), so the device's Dispose
			// releases it - releasing it here as well would be a double free.
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
		/// Rebuilds the depth (and any scratch) target at the swapchain's current size. The caller must have
		/// let go of any open frame first (see <see cref="Resize"/>): this disposes textures a live pass
		/// could still be drawing into.
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
				"canvasDepth"));
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
					"canvasScratch"));
			}

			return this.scratchTarget;
		}
	}
}
