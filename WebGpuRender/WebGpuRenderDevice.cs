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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The real backend: <see cref="IRenderDevice"/> over wgpu-native. Nearly every member is a single
	/// webgpu.h call with its descriptor filled in, which is the whole design goal of the seam - if a
	/// member here ever needs real logic, the member belongs above the seam and not on it.
	/// <para>
	/// <b>What this class deliberately does not do.</b> It adds no buffer sharing or sub-allocation.
	/// <c>wgpuQueueWriteBuffer</c> orders against submits, not against the draws recorded into an open
	/// pass, so a device that quietly pooled buffers behind the caller's back would make every draw in a
	/// pass read whichever write landed last. The compat layer already pools one buffer per draw and
	/// recycles on submit; the device's job is to stay out of the way of that.
	/// </para>
	/// <para>
	/// <b>On-screen too.</b> <see cref="CreateSurfaceTarget(WindowSurfaceRequest)"/> makes a swapchain from
	/// a native drawable - an HWND on Windows, a <c>CAMetalLayer</c> on macOS, an X11 window on Linux - and <see cref="Present"/>
	/// presents it. Which of those is used is a runtime decision, not a build-time one: this assembly is
	/// built once for all platforms. What is still Phase 4's is <em>recovery</em>: a lost device is
	/// recorded (<see cref="DeviceLostMessage"/>) and then reported as a clear failure, not repaired.
	/// </para>
	/// <para>
	/// <b>Threading.</b> wgpu is internally synchronized, so device-level calls need no external lock.
	/// The pass and command-encoder bookkeeping in this class is not thread safe, which matches how the
	/// renderer uses it: one device, one thread recording a frame.
	/// </para>
	/// <para>
	/// <b>In the browser.</b> Everything wgpu answers by callback - the adapter, the device, a buffer map -
	/// is a JS Promise there, and there is no pump to spin: the browser resolves it only once managed code
	/// has returned to the event loop. So the browser legs are genuinely asynchronous
	/// (<see cref="CreateAsync"/>, and the pending half of <see cref="ReadTextureAsync"/>), while the
	/// desktop keeps its synchronous spin unchanged. Those legs live in
	/// <c>WebGpuRenderDevice.BrowserAsync.cs</c> - see that file for why they cannot live here.
	/// </para>
	/// </summary>
	public sealed unsafe partial class WebGpuRenderDevice : IRenderDevice
	{
		/// <summary>
		/// How many times a callback loop pumps before giving up. Every callback this backend waits on
		/// resolves in the first iteration or two; the bound only exists so a driver that never answers
		/// produces an exception rather than a hung UI thread.
		/// </summary>
		private const int MaxCallbackSpins = 1000;

		/// <summary>
		/// How many callback result cells have been leaked because their callback never arrived. See
		/// <see cref="PinnedCallbackCell{T}"/> for why leaking is the safe answer; this counter is the
		/// diagnostic that says it happened at all.
		/// </summary>
		private static int abandonedCallbackCells;

		private readonly List<IShaderSourceProvider> shaderSources = new List<IShaderSourceProvider>();

		/// <summary>
		/// The WGSL embedded in this assembly. Held apart from the registered providers rather than
		/// registered first, so that it acts as the fallback and cannot shadow a caller's override - see
		/// <see cref="CreateShaderModule"/>.
		/// </summary>
		private readonly WgslShaderSources cannedShaders = new WgslShaderSources();

		private readonly string label;

		private GCHandle selfHandle;
		private WGPUInstance instance;
		private WGPUAdapter adapter;
		private WGPUDevice device;
		private WGPUQueue queue;

		// The device's own limits, read once at creation. Only maxBufferSize is carried: it is the one
		// limit application data (a large mesh's vertex buffer) actually reaches.
		private DeviceLimits limits = new DeviceLimits(DeviceLimits.DefaultMaxBufferSize);
		private WGPUCommandEncoder commandEncoder;
		private WebGpuRenderEncoder openEncoder;

		/// <summary>
		/// Creates an instance, picks an adapter and opens a device, synchronously. Desktop only:
		/// the browser cannot answer any of those requests without returning to its event loop first, so
		/// there it throws and <see cref="CreateAsync"/> is the only way in.
		/// <para>
		/// The adapter request is the one place the backend has a preference: hardware first
		/// (<c>HighPerformance</c>), with the software adapter reachable only by explicitly asking, which
		/// is the WARP-equivalent fallback the port plan wants rather than something that can happen by
		/// accident and quietly cost 100x the frame time.
		/// </para>
		/// </summary>
		/// <param name="forceFallbackAdapter">
		/// True to demand the software (fallback) adapter. Opt-in only.
		/// </param>
		/// <param name="preferredBackend">
		/// Which wgpu backend to ask for. <c>Undefined</c> lets wgpu choose; tests pin D3D12 so that a
		/// machine silently falling back to another API shows up as a failure rather than as a pixel diff.
		/// </param>
		/// <param name="label">Optional debug label carried into wgpu's validation messages.</param>
		/// <exception cref="InvalidOperationException">The instance, adapter or device could not be created.</exception>
		/// <param name="windowSurface">
		/// The native drawable this device will present to, or null for an offscreen device. Supplied here
		/// rather than through <see cref="CreateSurfaceTarget(WindowSurfaceRequest)"/> so the surface exists <i>before</i> the adapter is
		/// requested and can be passed as <c>compatibleSurface</c>: without it wgpu may hand back an adapter
		/// that cannot present to the window at all (a hybrid laptop's discrete GPU with the display wired
		/// to the integrated one), which surfaces much later as a swapchain that will not configure.
		/// </param>
		public WebGpuRenderDevice(
			bool forceFallbackAdapter = false,
			WGPUBackendType preferredBackend = WGPUBackendType.Undefined,
			string label = null,
			WindowSurfaceRequest windowSurface = null)
		{
			// The loud fence for the browser. Nothing below would fail fast there: the adapter request
			// would spin MaxCallbackSpins times against a promise that cannot resolve while this frame is
			// on the stack, and report "the driver is not answering" for what is really the wrong entry
			// point. This is also the fence a device-loss recovery hits - every host's TryRecoverDevice
			// rebuilds its device by calling this constructor, so the browser's twin has to be an async
			// path built on CreateAsync, which is BrowserWebGpuLayer's work and not this type's.
			if (OperatingSystem.IsBrowser())
			{
				throw new PlatformNotSupportedException(
					"A WebGpuRenderDevice cannot be created synchronously in the browser: the adapter, device "
					+ "and buffer-map callbacks are Promises that only resolve once managed code returns to the "
					+ "JS event loop. Use WebGpuRenderDevice.CreateAsync.");
			}

			this.label = label ?? "WebGpuRenderDevice";

			// Allocated before the device request because the uncaptured-error and device-lost callbacks
			// are unmanaged entry points: they cannot close over anything, so the only way back to this
			// instance is a handle passed through wgpu's userdata slot, and wgpu copies the callback info
			// out of the descriptor at request time.
			this.selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

			try
			{
				this.instance = wgpuCreateInstance(null);
				if (this.instance.IsNull)
				{
					throw new InvalidOperationException("wgpuCreateInstance returned null.");
				}

				// Created before the adapter request on purpose - see the windowSurface parameter.
				WGPUSurface pendingSurface = windowSurface == null
					? default
					: CreateRawSurface(this.instance, windowSurface);

				try
				{
					this.adapter = this.RequestAdapter(forceFallbackAdapter, preferredBackend, pendingSurface);
					this.ReadAdapterInfo();
					this.device = this.RequestDevice();
					this.queue = wgpuDeviceGetQueue(this.device);
					this.ReadDeviceLimits();

					if (windowSurface != null)
					{
						this.WindowSurface = this.ConfigureSurfaceTarget(pendingSurface, windowSurface);
						pendingSurface = default;
					}
				}
				finally
				{
					if (!pendingSurface.IsNull)
					{
						wgpuSurfaceRelease(pendingSurface);
					}
				}
			}
			catch
			{
				this.Dispose();
				throw;
			}
		}

		/// <summary>
		/// The empty shell the browser leg fills in over several turns of the JS event loop. Everything the
		/// unmanaged callbacks need to find their way back to this instance - the label and the self handle -
		/// exists immediately; the instance, adapter, device and queue arrive from
		/// <c>InitializeBrowserAsync</c>. Private because a half-built device must never escape:
		/// <see cref="CreateAsync"/> is the only caller, and it disposes this on any failure.
		/// </summary>
		/// <param name="label">Optional debug label carried into wgpu's validation messages.</param>
		private WebGpuRenderDevice(string label)
		{
			this.label = label ?? "WebGpuRenderDevice";
			this.selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
		}

		/// <summary>
		/// Raised when wgpu reports an error out of band. wgpu does <em>not</em> fail the call that caused
		/// a validation error - the descriptor is rejected and rendering silently goes wrong later - so
		/// anything that cares about correctness listens here or checks
		/// <see cref="LastUncapturedError"/>.
		/// </summary>
		public event EventHandler<string> UncapturedError;

		/// <summary>
		/// Process-wide count of callback result cells that had to be leaked because wgpu never called
		/// back within <see cref="MaxCallbackSpins"/>. Zero on any healthy run; anything else means a
		/// driver stopped answering. See <see cref="PinnedCallbackCell{T}"/>.
		/// </summary>
		public static int AbandonedCallbackCellCount => Volatile.Read(ref abandonedCallbackCells);

		/// <summary>The backend wgpu chose: D3D12, Vulkan, Metal. Tests assert on this.</summary>
		public WGPUBackendType AdapterBackend { get; private set; }

		/// <summary>The adapter's device name, copied out of <c>WGPUAdapterInfo</c> before it is freed.</summary>
		public string AdapterName { get; private set; } = string.Empty;

		/// <summary>Whether the adapter wgpu chose is a software rasterizer.</summary>
		public bool IsFallbackAdapter { get; private set; }

		/// <summary>
		/// The most recent uncaptured error, or null. Not cleared automatically: a test asserts it is null
		/// at the end of a render, and a cleared-on-read property would make that assertion order
		/// dependent.
		/// </summary>
		public string LastUncapturedError { get; private set; }

		/// <summary>The reason and message wgpu gave when the device was lost, or null while it is alive.</summary>
		public string DeviceLostMessage { get; private set; }

		/// <summary>True once wgpu has reported the device lost. Recovery is Phase 4's job.</summary>
		public bool IsDeviceLost => this.DeviceLostMessage != null;

		/// <summary>True once <see cref="Dispose"/> has been called.</summary>
		public bool IsDisposed { get; private set; }

		/// <summary>The pass currently open, or null.</summary>
		public WebGpuRenderEncoder OpenPass => this.openEncoder;

		/// <summary>
		/// The swapchain built from the <c>windowSurface</c> the device was constructed with, or null for
		/// an offscreen device. Owned by this device and disposed with it.
		/// </summary>
		public WebGpuSurfaceTarget WindowSurface { get; private set; }

		/// <summary>The wgpu device, for the surface a swapchain has to be configured against.</summary>
		internal WGPUDevice DeviceHandle => this.device;

		/// <summary>Clears <see cref="LastUncapturedError"/> so a test can measure only what follows.</summary>
		public void ClearUncapturedError() => this.LastUncapturedError = null;

		/// <summary>
		/// Destroys the device, which makes wgpu raise the device-lost callback - the only way to reach the
		/// host's device-loss recovery on demand, since a real loss needs a driver reset. Everything created
		/// from this device is invalid afterwards.
		/// </summary>
		/// <remarks>
		/// The callback is delivered through <c>ProcessEvents</c>, so this pumps the instance until
		/// <see cref="IsDeviceLost"/> is set rather than returning while the loss is still in flight and
		/// leaving the caller to guess when it lands.
		/// </remarks>
		public void DestroyDeviceToSimulateLoss()
		{
			this.ThrowIfDisposed();

			// Both halves of the pump below are dead ends in the browser: emdawnwebgpu has no
			// wgpuDevicePoll at all (the browser build links a stub that reports "idle" and returns), and
			// the lost callback can only fire once this call has returned to the JS event loop. The loop
			// would therefore spin MaxCallbackSpins times doing nothing and then report a loss that had
			// not happened yet. An async twin belongs with BrowserWebGpuLayer, which is what would use it.
			if (OperatingSystem.IsBrowser())
			{
				throw new PlatformNotSupportedException(
					"DestroyDeviceToSimulateLoss cannot wait for the device-lost callback in the browser: it is "
					+ "delivered on the JS event loop, which cannot run while this call is on the stack.");
			}

			wgpuDeviceDestroy(this.device);

			for (int spin = 0; spin < MaxCallbackSpins && !this.IsDeviceLost; spin++)
			{
				// Both, for the reason ReadTextureAsync gives: wgpu-native resolves some callbacks only
				// under DevicePoll and others only under ProcessEvents.
				WgpuNative.wgpuDevicePoll(this.device, true, null);
				wgpuInstanceProcessEvents(this.instance);
			}

			if (!this.IsDeviceLost)
			{
				// wgpu-native 29 does not always deliver the lost callback for a caller-initiated destroy.
				// The device is genuinely dead either way, and the host's recovery keys off this flag, so
				// it is recorded rather than left to a caller that would then be testing nothing.
				this.ReportDeviceLost("Destroyed: wgpuDeviceDestroy called by DestroyDeviceToSimulateLoss.");
			}
		}

		/// <inheritdoc/>
		public DeviceLimits Limits => this.limits;

		/// <inheritdoc/>
		public IGpuBuffer CreateBuffer(BufferUsage usage, ulong sizeInBytes, ReadOnlySpan<byte> initialData = default)
		{
			FrameProfiler.Count("dev.CreateBuffer");
			this.ThrowIfDisposed();
			if (initialData.Length > (int)Math.Min(sizeInBytes, int.MaxValue))
			{
				throw new ArgumentException("Initial data is larger than the buffer.", nameof(initialData));
			}

			// WebGPU requires a mapped-at-creation buffer's size to be a multiple of 4. Rounding every
			// buffer rather than only that case keeps one rule instead of two, and costs at most 3 bytes.
			ulong size = (sizeInBytes + 3ul) & ~3ul;
			if (size == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "A buffer must have a non-zero size.");
			}

			bool mappedAtCreation = initialData.Length > 0;
			var descriptor = new WGPUBufferDescriptor
			{
				label = WgpuStrings.Null,
				usage = WgpuEnums.ToWgpu(usage),
				size = size,
				mappedAtCreation = mappedAtCreation,
			};

			// Checked here rather than left to wgpu: an over-limit buffer comes back as a valid-looking
			// error handle, and the next call on it (wgpuBufferGetMappedRange, or the first draw) fails
			// validation inside Rust, which panics non-unwinding and takes the whole process down with no
			// managed stack. A caller can survive an exception; nobody survives that. Found by a
			// half-gigabyte outline-geometry buffer from a thumbnail of a large mesh.
			if (size > this.limits.MaxBufferSize)
			{
				throw new InvalidOperationException(
					$"A {size:N0} byte buffer exceeds this device's maxBufferSize of {this.limits.MaxBufferSize:N0}"
					+ $" bytes ('{this.label}').");
			}

			WGPUBuffer handle = wgpuDeviceCreateBuffer(this.device, &descriptor);
			if (handle.IsNull)
			{
				throw new InvalidOperationException("wgpuDeviceCreateBuffer returned null.");
			}

			if (mappedAtCreation)
			{
				var mapped = new Span<byte>(wgpuBufferGetMappedRange(handle, 0, (nuint)size), (int)size);
				initialData.CopyTo(mapped);
				wgpuBufferUnmap(handle);
			}

			return new WebGpuBuffer(handle, usage, size, "buffer");
		}

		/// <summary>
		/// Buckets a resource label for the frame profiler. Trailing digits are dropped because the
		/// compat layer names its textures after GL texture ids, and one counter per id says nothing.
		/// </summary>
		private static string ProfileLabel(string label)
		{
			if (string.IsNullOrEmpty(label))
			{
				return "unlabeled";
			}

			int end = label.Length;
			while (end > 0 && char.IsDigit(label[end - 1]))
			{
				end--;
			}

			return end == 0 ? label : label.Substring(0, end);
		}

		/// <inheritdoc/>
		public IGpuTexture CreateTexture(in TextureDescriptor descriptor)
		{
			FrameProfiler.Count("dev.CreateTexture");
			FrameProfiler.Count("tex:" + ProfileLabel(descriptor.Label));
			this.ThrowIfDisposed();

			// Checked here for the same reason CreateBuffer checks its size, only worse: an over-limit
			// texture does not come back null. wgpuDeviceCreateTexture hands back a non-null *error*
			// texture, the null check below passes, wgpuTextureCreateView yields an invalid view, and the
			// next wgpuQueueSubmit fails validation inside Rust - where the panic cannot unwind across the
			// FFI boundary, so wgpu-native aborts the process with no managed stack. Found by the 3x
			// full-frame supersample of a fullscreen retina window: 3024x1898 x3 is 9072, past the 8192
			// WebGPU grants by default.
			if (descriptor.Width > this.limits.MaxTextureDimension2D
				|| descriptor.Height > this.limits.MaxTextureDimension2D)
			{
				throw new InvalidOperationException(
					$"A {descriptor.Width}x{descriptor.Height} texture exceeds this device's maxTextureDimension2D"
					+ $" of {this.limits.MaxTextureDimension2D} ('{descriptor.Label}' on '{this.label}').");
			}

			using (var labelText = new Utf8Buffer(descriptor.Label))
			{
				var textureDescriptor = new WGPUTextureDescriptor
				{
					label = labelText.View,
					usage = WgpuEnums.ToWgpu(descriptor.Usage),
					dimension = WGPUTextureDimension._2D,
					size = new WGPUExtent3D { width = descriptor.Width, height = descriptor.Height, depthOrArrayLayers = 1 },
					format = WgpuEnums.ToWgpu(descriptor.Format),
					mipLevelCount = Math.Max(1u, descriptor.MipLevelCount),
					sampleCount = Math.Max(1u, descriptor.SampleCount),
				};

				WGPUTexture handle = wgpuDeviceCreateTexture(this.device, &textureDescriptor);
				if (handle.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreateTexture returned null.");
				}

				// A null view descriptor means the whole resource, which is what every use here wants.
				WGPUTextureView view = wgpuTextureCreateView(handle, null);
				return new WebGpuTexture(handle, view, descriptor);
			}
		}

		/// <inheritdoc/>
		public ISampler CreateSampler(in SamplerDescriptor descriptor)
		{
			this.ThrowIfDisposed();
			using (var labelText = new Utf8Buffer(descriptor.Label))
			{
				var samplerDescriptor = WgpuDescriptors.Sampler(descriptor, labelText.View);
				WGPUSampler handle = wgpuDeviceCreateSampler(this.device, &samplerDescriptor);
				if (handle.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreateSampler returned null.");
				}

				return new WebGpuSampler(handle, descriptor);
			}
		}

		/// <inheritdoc/>
		public IShaderModule CreateShaderModule(string sourceKey)
		{
			this.ThrowIfDisposed();
			if (string.IsNullOrEmpty(sourceKey))
			{
				throw new ArgumentException("A shader source key is required.", nameof(sourceKey));
			}

			string source = null;
			foreach (var provider in this.shaderSources)
			{
				source = provider.TryGetSource(sourceKey);
				if (source != null)
				{
					break;
				}
			}

			// The canned WGSL is asked last, not registered first, for two reasons: IRenderDevice
			// documents that providers are consulted in registration order, and a caller that registers a
			// replacement for a canned module should win without having to unregister anything.
			source = source ?? this.cannedShaders.TryGetSource(sourceKey);

			if (source == null)
			{
				throw new ArgumentException($"No registered shader source for key '{sourceKey}'.", nameof(sourceKey));
			}

			using (var code = new Utf8Buffer(source))
			using (var labelText = new Utf8Buffer(sourceKey))
			{
				var wgsl = new WGPUShaderSourceWGSL
				{
					chain = new WGPUChainedStruct { sType = WGPUSType.ShaderSourceWGSL },
					code = code.View,
				};

				var descriptor = new WGPUShaderModuleDescriptor
				{
					nextInChain = (WGPUChainedStruct*)&wgsl,
					label = labelText.View,
				};

				WGPUShaderModule handle = wgpuDeviceCreateShaderModule(this.device, &descriptor);
				if (handle.IsNull)
				{
					throw new InvalidOperationException($"wgpuDeviceCreateShaderModule returned null for '{sourceKey}'.");
				}

				return new WebGpuShaderModule(handle, sourceKey);
			}
		}

		/// <inheritdoc/>
		public void RegisterShaderSources(IShaderSourceProvider provider)
		{
			if (provider == null)
			{
				throw new ArgumentNullException(nameof(provider));
			}

			this.shaderSources.Add(provider);
		}

		/// <inheritdoc/>
		public IRenderPipeline CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
		{
			this.ThrowIfDisposed();
			if (descriptor.VertexShader == null)
			{
				throw new ArgumentException("A pipeline needs a vertex shader module.", nameof(descriptor));
			}

			var bindGroupLayouts = this.CreateBindGroupLayouts(descriptor.BindGroupLayout);

			// Inside the try, not before it: CreatePipelineLayout rejects non-contiguous group indices,
			// and that throw used to leak every layout already created for this pipeline.
			WGPUPipelineLayout pipelineLayout = default;

			try
			{
				pipelineLayout = this.CreatePipelineLayout(bindGroupLayouts);
				return this.CreatePipelineCore(descriptor, pipelineLayout, bindGroupLayouts);
			}
			catch
			{
				foreach (var layout in bindGroupLayouts.Values)
				{
					wgpuBindGroupLayoutRelease(layout);
				}

				if (!pipelineLayout.IsNull)
				{
					wgpuPipelineLayoutRelease(pipelineLayout);
				}

				throw;
			}
		}

		/// <inheritdoc/>
		public IBindGroup CreateBindGroup(in BindGroupDescriptor descriptor)
		{
			FrameProfiler.Count("dev.CreateBindGroup");
			this.ThrowIfDisposed();
			if (!(descriptor.Pipeline is WebGpuRenderPipeline pipeline))
			{
				throw new ArgumentException("A bind group needs a pipeline created by this device.", nameof(descriptor));
			}

			var entries = descriptor.Entries;
			var wgpuEntries = new WGPUBindGroupEntry[entries.Length];
			for (int index = 0; index < entries.Length; index++)
			{
				var entry = entries[index];
				wgpuEntries[index] = new WGPUBindGroupEntry
				{
					binding = entry.Binding,
					offset = entry.Buffer == null ? 0 : entry.Offset,

					// RenderCore spells "to the end of the buffer" as 0; webgpu spells it WGPU_WHOLE_SIZE,
					// and a literal 0 here binds nothing at all.
					size = entry.Buffer == null ? 0 : (entry.Size == 0 ? WGPUConstants.WGPU_WHOLE_SIZE : entry.Size),
					buffer = entry.Buffer == null ? default : Require<WebGpuBuffer>(entry.Buffer, "entry.Buffer").Handle,
					sampler = entry.Sampler == null ? default : Require<WebGpuSampler>(entry.Sampler, "entry.Sampler").Handle,
					textureView = entry.Texture == null ? default : Require<WebGpuTexture>(entry.Texture, "entry.Texture").View,
				};
			}

			using (var labelText = new Utf8Buffer(descriptor.Label))
			fixed (WGPUBindGroupEntry* entriesPointer = wgpuEntries)
			{
				var bindGroupDescriptor = new WGPUBindGroupDescriptor
				{
					label = labelText.View,
					layout = pipeline.LayoutForGroup(descriptor.Group),
					entryCount = (nuint)wgpuEntries.Length,
					entries = entriesPointer,
				};

				WGPUBindGroup handle = wgpuDeviceCreateBindGroup(this.device, &bindGroupDescriptor);
				if (handle.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreateBindGroup returned null.");
				}

				return new WebGpuBindGroup(handle, descriptor.Label);
			}
		}

		/// <inheritdoc/>
		public IRenderEncoder BeginRenderPass(in RenderPassDescriptor descriptor)
		{
			this.ThrowIfDisposed();
			this.ThrowIfPassOpen("begin a render pass");

			var colorAttachments = descriptor.ColorAttachments;
			var wgpuColors = new WGPURenderPassColorAttachment[colorAttachments.Length];
			for (int index = 0; index < colorAttachments.Length; index++)
			{
				var attachment = colorAttachments[index];
				wgpuColors[index] = WgpuDescriptors.ColorAttachment(
					Require<WebGpuTexture>(attachment.Target, "attachment.Target").View,
					WgpuEnums.ToWgpu(attachment.LoadOp),
					WgpuEnums.ToWgpu(attachment.StoreOp),
					WgpuDescriptors.Color(attachment.ClearValue));
			}

			var depth = descriptor.Depth;
			var wgpuDepth = depth.Target == null
				? default
				: WgpuDescriptors.DepthAttachment(
					Require<WebGpuTexture>(depth.Target, "depth.Target").View,
					WgpuEnums.ToWgpu(depth.LoadOp),
					WgpuEnums.ToWgpu(depth.StoreOp),
					depth.ClearValue);

			WGPUCommandEncoder encoder = this.EnsureCommandEncoder();

			using (var labelText = new Utf8Buffer(descriptor.Label))
			fixed (WGPURenderPassColorAttachment* colorsPointer = wgpuColors)
			{
				var passDescriptor = new WGPURenderPassDescriptor
				{
					label = labelText.View,
					colorAttachmentCount = (nuint)wgpuColors.Length,
					colorAttachments = colorsPointer,
					depthStencilAttachment = depth.Target == null ? null : &wgpuDepth,
				};

				WGPURenderPassEncoder pass = wgpuCommandEncoderBeginRenderPass(encoder, &passDescriptor);
				if (pass.IsNull)
				{
					throw new InvalidOperationException("wgpuCommandEncoderBeginRenderPass returned null.");
				}

				this.openEncoder = new WebGpuRenderEncoder(
					this,
					pass,
					string.IsNullOrEmpty(descriptor.Label) ? "pass" : descriptor.Label);
				return this.openEncoder;
			}
		}

		/// <inheritdoc/>
		public void WriteBuffer(IGpuBuffer buffer, ulong offset, ReadOnlySpan<byte> data)
		{
			this.ThrowIfDisposed();
			var target = Require<WebGpuBuffer>(buffer, nameof(buffer));
			if (data.Length == 0)
			{
				return;
			}

			FrameProfiler.Count("dev.WriteBuffer");
			FrameProfiler.Count("dev.WriteBufferBytes", data.Length);

			// Legal while a pass is open, and the compat layer relies on that for its per-draw pooled
			// buffers - but only because a pooled slot is handed out once per submit window. Queue writes
			// are ordered against submits, not against the draws in a pass.
			using (FrameProfiler.Time("dev.WriteBuffer"))
			{
				fixed (byte* source = data)
				{
					wgpuQueueWriteBuffer(this.queue, target.Handle, offset, source, (nuint)data.Length);
				}
			}
		}

		/// <inheritdoc/>
		public void WriteTexture(IGpuTexture texture, ReadOnlySpan<byte> data, uint bytesPerRow, uint mipLevel = 0)
		{
			this.ThrowIfDisposed();
			var target = Require<WebGpuTexture>(texture, nameof(texture));
			if (mipLevel >= target.Descriptor.MipLevelCount)
			{
				throw new ArgumentOutOfRangeException(
					nameof(mipLevel),
					$"Texture '{target.Label}' has {target.Descriptor.MipLevelCount} mip level(s); level {mipLevel} does not exist.");
			}

			uint width = Math.Max(1u, target.Descriptor.Width >> (int)mipLevel);
			uint height = Math.Max(1u, target.Descriptor.Height >> (int)mipLevel);

			var destination = new WGPUTexelCopyTextureInfo
			{
				texture = target.Handle,
				mipLevel = mipLevel,
				origin = default,
				aspect = WGPUTextureAspect.All,
			};

			var layout = new WGPUTexelCopyBufferLayout
			{
				offset = 0,
				bytesPerRow = bytesPerRow,
				rowsPerImage = height,
			};

			var writeSize = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 };

			fixed (byte* source = data)
			{
				wgpuQueueWriteTexture(this.queue, &destination, source, (nuint)data.Length, &layout, &writeSize);
			}
		}

		/// <summary>
		/// Reads a texture back. On the desktop it completes before the <see cref="ValueTask"/> is returned -
		/// the native recipe is <c>wgpuDevicePoll(device, wait: true)</c>, which
		/// <c>wgpuInstanceProcessEvents</c> alone cannot substitute for - so a desktop caller pays no
		/// allocation and no thread hop.
		/// <para>
		/// <b>This submits.</b> A readback that did not flush the recorded commands would return the
		/// texture as it was before this frame's draws, which is never what a caller means. Everything
		/// recorded since the last submit therefore goes to the queue along with the copy.
		/// </para>
		/// <para>
		/// <b>Record now, wait later.</b> The half that touches <paramref name="source"/> - creating the
		/// readback buffer, recording the copy, submitting - always runs synchronously, before this method
		/// returns. Only the map is allowed to be pending. That split is what makes the browser leg legal at
		/// all: there a buffer map resolves on the JS event loop, so the returned ValueTask genuinely has not
		/// completed yet, and by the time it does the animation-frame task that owned the surface texture has
		/// ended and the canvas has already been presented.
		/// </para>
		/// <para>
		/// <b>The seam this creates.</b> A caller that touches a <em>surface</em> texture after awaiting this
		/// - drawing into it, presenting it, acquiring from it and expecting the old frame - is a browser-only
		/// bug, and an invisible one on the desktop where the await never actually yields. Read the surface
		/// texture, then await; never await, then use the surface.
		/// </para>
		/// </summary>
		/// <param name="source">Texture to read; must declare <see cref="TextureUsage.CopySrc"/>.</param>
		/// <param name="destination">Buffer to fill; must hold RowStride * height bytes.</param>
		/// <exception cref="InvalidOperationException">A render pass is open, or the map failed.</exception>
		/// <exception cref="ArgumentException">The destination is too small for the padded rows.</exception>
		public ValueTask<TextureReadResult> ReadTextureAsync(IGpuTexture source, Memory<byte> destination)
		{
			this.ThrowIfDisposed();
			var texture = Require<WebGpuTexture>(source, nameof(source));
			this.ThrowIfPassOpen("read a texture back");

			var descriptor = texture.Descriptor;
			uint rowStride = TextureFormatInfo.AlignedRowStride(descriptor.Format, descriptor.Width);
			var result = new TextureReadResult(descriptor.Width, descriptor.Height, rowStride);
			if ((ulong)destination.Length < result.TotalBytes)
			{
				throw new ArgumentException(
					$"Destination holds {destination.Length} bytes; the padded read needs {result.TotalBytes}.",
					nameof(destination));
			}

			var readbackDescriptor = new WGPUBufferDescriptor
			{
				label = WgpuStrings.Null,
				usage = WGPUBufferUsage.CopyDst | WGPUBufferUsage.MapRead,
				size = result.TotalBytes,
				mappedAtCreation = false,
			};

			WGPUBuffer readback = wgpuDeviceCreateBuffer(this.device, &readbackDescriptor);
			if (readback.IsNull)
			{
				throw new InvalidOperationException("wgpuDeviceCreateBuffer returned null for the readback buffer.");
			}

			try
			{
				var copySource = new WGPUTexelCopyTextureInfo
				{
					texture = texture.Handle,
					mipLevel = 0,
					origin = default,
					aspect = WGPUTextureAspect.All,
				};

				var copyDestination = new WGPUTexelCopyBufferInfo
				{
					layout = new WGPUTexelCopyBufferLayout { offset = 0, bytesPerRow = rowStride, rowsPerImage = descriptor.Height },
					buffer = readback,
				};

				var copySize = new WGPUExtent3D { width = descriptor.Width, height = descriptor.Height, depthOrArrayLayers = 1 };

				wgpuCommandEncoderCopyTextureToBuffer(this.EnsureCommandEncoder(), &copySource, &copyDestination, &copySize);
				this.Submit();

				if (OperatingSystem.IsBrowser())
				{
					// The map cannot resolve until this call returns, so the buffer has to outlive it:
					// ownership moves to the continuation, which unmaps and releases it whatever happens.
					// Clearing the local is what keeps the finally below from freeing a buffer wgpu is
					// still writing into.
					var pending = this.MapAndCopyBrowserAsync(readback, result, destination);
					readback = default;
					return new ValueTask<TextureReadResult>(pending);
				}

				this.MapAndCopy(readback, result, destination.Span);
			}
			finally
			{
				// Never null on the desktop; null only on the browser leg, which took the buffer with it.
				if (!readback.IsNull)
				{
					wgpuBufferRelease(readback);
				}
			}

			return new ValueTask<TextureReadResult>(result);
		}

		/// <inheritdoc/>
		public void Submit()
		{
			this.ThrowIfDisposed();
			this.ThrowIfPassOpen("submit");

			if (this.commandEncoder.IsNull)
			{
				return;
			}

			var finishDescriptor = new WGPUCommandBufferDescriptor { label = WgpuStrings.Null };
			WGPUCommandBuffer commands = wgpuCommandEncoderFinish(this.commandEncoder, &finishDescriptor);
			wgpuCommandEncoderRelease(this.commandEncoder);
			this.commandEncoder = default;

			wgpuQueueSubmit(this.queue, 1, &commands);
			wgpuCommandBufferRelease(commands);
		}

		/// <summary>
		/// Creates a swapchain for a native drawable. The surface is configured immediately, so the caller
		/// can acquire a texture from it on the very next frame.
		/// <para>
		/// The colour format is the swapchain's preferred one unless the window can have
		/// <see cref="TextureFormat.Bgra8Unorm"/>, which it is then given: that is the format the whole 2D
		/// path's goldens were captured in, so taking it wherever it is offered keeps the window and the
		/// golden images the same pixels rather than nearly the same.
		/// </para>
		/// </summary>
		/// <param name="nativeSurfaceHandle">
		/// The platform's native surface handle: an HWND on Windows, a <c>CAMetalLayer*</c> on macOS, the
		/// X11 <c>Display*</c> on Linux. See <see cref="WindowSurfaceRequest"/>. On Linux this overload
		/// cannot describe a whole drawable - X11 also needs the window XID, which has no parameter here -
		/// so a Linux host must use <see cref="WindowSurfaceRequest.ForXlibWindow"/> and the request
		/// overload instead.
		/// </param>
		/// <param name="moduleHandle">The module instance (HINSTANCE), or zero. Windows only.</param>
		/// <param name="width">Initial swapchain width in pixels.</param>
		/// <param name="height">Initial swapchain height in pixels.</param>
		/// <param name="label">Optional debug label.</param>
		/// <exception cref="InvalidOperationException">The surface could not be created or reports no usable format.</exception>
		/// <exception cref="PlatformNotSupportedException">This OS has no surface source implemented yet.</exception>
		public WebGpuSurfaceTarget CreateSurfaceTarget(
			IntPtr nativeSurfaceHandle,
			IntPtr moduleHandle,
			uint width,
			uint height,
			string label = null)
		{
			return this.CreateSurfaceTarget(new WindowSurfaceRequest(nativeSurfaceHandle, moduleHandle, width, height, label));
		}

		/// <summary>
		/// Creates a swapchain for an already-described native drawable. This is the overload window hosts
		/// should prefer: built through <see cref="WindowSurfaceRequest.ForMetalLayer"/>,
		/// <see cref="WindowSurfaceRequest.ForWindowsHwnd"/> or
		/// <see cref="WindowSurfaceRequest.ForXlibWindow"/>, the call site says which kind of handle it is
		/// holding instead of leaving bare <see cref="IntPtr"/>s to be read the wrong way. It is also the
		/// only overload that can describe an X11 drawable, which needs a display and a window rather than
		/// one handle.
		/// </summary>
		/// <param name="request">The native drawable to make a surface over.</param>
		/// <exception cref="InvalidOperationException">The surface could not be created or reports no usable format.</exception>
		/// <exception cref="PlatformNotSupportedException">This OS has no surface source implemented yet.</exception>
		public WebGpuSurfaceTarget CreateSurfaceTarget(WindowSurfaceRequest request)
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			this.ThrowIfDisposed();

			WGPUSurface surface = CreateRawSurface(this.instance, request);

			try
			{
				return this.ConfigureSurfaceTarget(surface, request);
			}
			catch
			{
				wgpuSurfaceRelease(surface);
				throw;
			}
		}

		/// <summary>
		/// Makes the raw <c>WGPUSurface</c> for a native drawable. Static and instance-free because it has to run
		/// before the adapter exists (the surface is the adapter request's <c>compatibleSurface</c>).
		/// </summary>
		/// <param name="instance">The wgpu instance.</param>
		/// <param name="request">The native drawable to make a surface over.</param>
		/// <exception cref="ArgumentException">
		/// The request does not name a whole drawable for this OS: no native handle at all, or on Linux a
		/// display without a window XID.
		/// </exception>
		/// <exception cref="PlatformNotSupportedException">
		/// The OS has no surface source wired up here. Windows, macOS and Linux/X11 present; Wayland does
		/// not, so a Linux host has to run under X11 or XWayland.
		/// </exception>
		private static WGPUSurface CreateRawSurface(WGPUInstance instance, WindowSurfaceRequest request)
		{
			// Ahead of the native-handle guard, not after it: a browser canvas is named, never handed over,
			// so its NativeSurfaceHandle is legitimately zero and the guard below would reject every one.
			if (OperatingSystem.IsBrowser())
			{
				if (string.IsNullOrWhiteSpace(request.CanvasSelector))
				{
					throw new ArgumentException(
						"A browser surface needs the CSS selector of its canvas; this request carries none. "
						+ "Build it with WindowSurfaceRequest.ForBrowserCanvas.",
						nameof(request));
				}

				using (var selector = new Utf8Buffer(request.CanvasSelector))
				{
					// emdawnwebgpu's stand-in for the three native window sources: the selector is resolved
					// by document.querySelector on the JS side, so nothing here is a pointer to a drawable.
					var canvasSource = new WGPUEmscriptenSurfaceSourceCanvasHTMLSelector
					{
						chain = new WGPUChainedStruct { sType = WGPUEmscriptenSType.EmscriptenSurfaceSourceCanvasHTMLSelector },
						selector = selector.View,
					};

					return CreateSurfaceFromSource(instance, (WGPUChainedStruct*)&canvasSource, request.Label);
				}
			}

			if (request.NativeSurfaceHandle == IntPtr.Zero)
			{
				throw new ArgumentException("A surface needs a native surface handle.", nameof(request));
			}

			// The chained source struct is the one genuinely per-OS thing left in this backend, and the
			// branch is deliberately a runtime check: these assemblies are built once as cross-platform
			// net10.0 and shipped to every OS, so a #if would bake in whichever machine did the build.
			// Every branch takes the address of a local, so each stack-allocates the source in this frame
			// and passes it while it is still alive - wgpu copies out of the descriptor during the call.
			if (OperatingSystem.IsWindows())
			{
				var windowsSource = new WGPUSurfaceSourceWindowsHWND
				{
					chain = new WGPUChainedStruct { sType = WGPUSType.SurfaceSourceWindowsHWND },
					hinstance = (void*)request.ModuleHandle,
					hwnd = (void*)request.NativeSurfaceHandle,
				};

				return CreateSurfaceFromSource(instance, (WGPUChainedStruct*)&windowsSource, request.Label);
			}

			if (OperatingSystem.IsMacOS())
			{
				// wgpu wants the CAMetalLayer itself, not the NSWindow or NSView that owns it: it calls
				// nextDrawable on this pointer directly and does no unwrapping, so handing it a view here
				// is not a validation error, it is a crash inside Metal.
				var metalSource = new WGPUSurfaceSourceMetalLayer
				{
					chain = new WGPUChainedStruct { sType = WGPUSType.SurfaceSourceMetalLayer },
					layer = (void*)request.NativeSurfaceHandle,
				};

				return CreateSurfaceFromSource(instance, (WGPUChainedStruct*)&metalSource, request.Label);
			}

			if (OperatingSystem.IsLinux())
			{
				// The XID is checked here and not only in ForXlibWindow because the plain constructor and
				// the two-IntPtr CreateSurfaceTarget overload can both reach this branch, and neither has
				// anywhere to put a window: they leave XlibWindow at zero. The overwhelmingly likely cause
				// is a host that passed its window as the single native handle, so say that rather than
				// letting wgpu take None and fail somewhere inside Vulkan.
				if (request.XlibWindow == 0)
				{
					throw new ArgumentException(
						"An X11 surface needs both a Display* and a window XID, and the XID is zero (None). "
						+ "NativeSurfaceHandle is the display on Linux, not the window - build the request "
						+ "with WindowSurfaceRequest.ForXlibWindow.",
						nameof(request));
				}

				// X11 is the only source here that needs two values, so the request splits them: the
				// Display* rides in NativeSurfaceHandle (checked above) and the XID has its own field.
				// This is the Xlib source, not the XCB one - wgpu treats them as different sTypes and
				// will not accept an xcb_connection_t* here, so the host must hand us XOpenDisplay's
				// pointer. Wayland has its own sType we do not build, so a Wayland session reaches this
				// through XWayland or not at all.
				var xlibSource = new WGPUSurfaceSourceXlibWindow
				{
					chain = new WGPUChainedStruct { sType = WGPUSType.SurfaceSourceXlibWindow },
					display = (void*)request.NativeSurfaceHandle,
					window = request.XlibWindow,
				};

				return CreateSurfaceFromSource(instance, (WGPUChainedStruct*)&xlibSource, request.Label);
			}

			throw new PlatformNotSupportedException(
				$"No wgpu surface source is implemented for {RuntimeInformation.OSDescription}. "
				+ "Windows (HWND), macOS (CAMetalLayer), Linux/X11 (Display* plus window) and the browser "
				+ "(a canvas selector) can present; native Wayland is not wired up.");
		}

		/// <summary>
		/// Shared tail of <see cref="CreateRawSurface"/>: wraps a platform surface source in a descriptor
		/// and creates the surface.
		/// </summary>
		/// <param name="instance">The wgpu instance.</param>
		/// <param name="source">
		/// The platform surface source (a <c>WGPUSurfaceSource*</c> cast to its chain head). Must stay alive
		/// for the duration of the call, which is why the caller keeps it on its own stack frame.
		/// </param>
		/// <param name="label">Optional debug label.</param>
		private static WGPUSurface CreateSurfaceFromSource(WGPUInstance instance, WGPUChainedStruct* source, string label)
		{
			using (var labelText = new Utf8Buffer(label))
			{
				var descriptor = new WGPUSurfaceDescriptor
				{
					nextInChain = source,
					label = labelText.View,
				};

				WGPUSurface surface = wgpuInstanceCreateSurface(instance, &descriptor);
				if (surface.IsNull)
				{
					throw new InvalidOperationException("wgpuInstanceCreateSurface returned null for the native surface handle.");
				}

				return surface;
			}
		}

		/// <summary>Picks the surface's format, usage and present mode and configures it at the requested
		/// size. Takes ownership of <paramref name="surface"/> on success.</summary>
		/// <param name="surface">A surface created by <see cref="CreateRawSurface"/>.</param>
		/// <param name="request">The window it was made over.</param>
		private WebGpuSurfaceTarget ConfigureSurfaceTarget(WGPUSurface surface, WindowSurfaceRequest request)
		{
			var format = this.ChooseSurfaceFormat(surface, out TextureUsage usage, out var presentModes);
			var target = new WebGpuSurfaceTarget(this, surface, format, usage, presentModes, request.Label);
			target.Configure(request.Width, request.Height);
			return target;
		}

		/// <summary>
		/// Submits anything still recorded and presents the surface's acquired frame
		/// (<c>wgpuSurfacePresent</c>), releasing that frame's texture.
		/// </summary>
		/// <param name="target">A surface created by <see cref="CreateSurfaceTarget(WindowSurfaceRequest)"/> on this device.</param>
		/// <exception cref="InvalidOperationException">A render pass is open - end it first.</exception>
		/// <exception cref="ArgumentException">The surface came from another device.</exception>
		public void Present(ISurfaceTarget target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			this.ThrowIfDisposed();
			this.ThrowIfPassOpen("present");

			var surface = Require<WebGpuSurfaceTarget>(target, nameof(target));
			if (!surface.BelongsTo(this))
			{
				throw new ArgumentException(
					$"Surface '{surface.Label}' was created by a different device; a frame can only be presented by the device that drew it.",
					nameof(target));
			}

			// A host that drew through the compat layer has already submitted, but one that recorded
			// straight onto the device may not have; presenting commands that never reached the queue
			// would show the previous frame with no hint why.
			this.Submit();
			surface.PresentFrame();
		}

		/// <summary>
		/// Blocks until everything already submitted to this device's queue has finished on the GPU.
		/// <para>
		/// <b>Unbounded, and the point of having it.</b> The same wait happens inside <see cref="Dispose"/>
		/// (a swapchain cannot be unconfigured while the GPU may still be reading its images), but there it
		/// is welded to native calls that talk to the window. Here it is on its own, so a window host can
		/// pay it with a time budget on a thread it is willing to abandon - see
		/// <see cref="RenderCore.GpuTeardown"/> - and reach <see cref="Dispose"/> with an idle queue, where
		/// it costs nothing.
		/// </para>
		/// <para>
		/// <b>It touches no window.</b> This is a queue fence wait: no surface, no swapchain, no HWND, no X
		/// drawable, no CAMetalLayer. That is what makes it the half of the teardown that is safe to leave
		/// running while the host destroys its native window.
		/// </para>
		/// <para>
		/// A no-op in the browser, where the wasm build links a stub that reports the device idle and
		/// returns - the same reason <see cref="ReadTextureAsync"/> never polls there.
		/// </para>
		/// </summary>
		public void WaitForGpuIdle()
		{
			if (this.IsDisposed || this.device.IsNull)
			{
				return;
			}

			WgpuNative.wgpuDevicePoll(this.device, true, null);
		}

		/// <summary>
		/// Ends any open pass and releases every wgpu object this device owns.
		/// <para>
		/// <b>How long this takes is up to the GPU.</b> Both the swapchain release below (see
		/// <see cref="WebGpuSurfaceTarget.Dispose"/>) and dropping the device itself wait for the work
		/// already submitted to finish, unbounded. That is milliseconds on hardware and minutes on a
		/// software rasterizer. <see cref="WaitForGpuIdle"/> beforehand is what makes it prompt: with the
		/// queue already idle these waits have nothing left to wait for.
		/// </para>
		/// <para>
		/// <b>This must not be abandoned, and must not outlive the window.</b> Unconfiguring and releasing
		/// the swapchain are real calls against the native window the surface was made over - X requests on
		/// a shared display, a DXGI swapchain over an HWND, a CAMetalLayer. Every host destroys that window
		/// immediately after its close path returns, so this has to have finished by then. A host that
		/// cannot afford to wait must leave this uncalled and leak the device instead (see
		/// <see cref="RenderCore.GpuTeardown"/>), never call it and walk away.
		/// </para>
		/// </summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;

			this.openEncoder?.Dispose();
			this.openEncoder = null;

			// Before the device: unconfiguring a swapchain needs the device that configured it alive.
			this.WindowSurface?.Dispose();
			this.WindowSurface = null;

			if (!this.commandEncoder.IsNull)
			{
				wgpuCommandEncoderRelease(this.commandEncoder);
				this.commandEncoder = default;
			}

			if (!this.queue.IsNull)
			{
				wgpuQueueRelease(this.queue);
				this.queue = default;
			}

			if (!this.device.IsNull)
			{
				wgpuDeviceRelease(this.device);
				this.device = default;
			}

			if (!this.adapter.IsNull)
			{
				wgpuAdapterRelease(this.adapter);
				this.adapter = default;
			}

			if (!this.instance.IsNull)
			{
				wgpuInstanceRelease(this.instance);
				this.instance = default;
			}

			if (this.selfHandle.IsAllocated)
			{
				this.selfHandle.Free();
			}
		}

		/// <summary>Called by the encoder when its pass ends, so the device knows the pass rules relax again.</summary>
		/// <param name="encoder">The encoder that ended.</param>
		internal void EndPass(WebGpuRenderEncoder encoder)
		{
			if (ReferenceEquals(this.openEncoder, encoder))
			{
				this.openEncoder = null;
			}
		}

		private static T Require<T>(object resource, string parameterName)
			where T : class
		{
			if (resource == null)
			{
				throw new ArgumentNullException(parameterName);
			}

			if (!(resource is T typed))
			{
				throw new ArgumentException(
					$"{resource.GetType().Name} was not created by a WebGpuRenderDevice; resources cannot be mixed across devices.",
					parameterName);
			}

			return typed;
		}

		private IRenderPipeline CreatePipelineCore(
			in RenderPipelineDescriptor descriptor,
			WGPUPipelineLayout pipelineLayout,
			Dictionary<uint, WGPUBindGroupLayout> bindGroupLayouts)
		{
			var layouts = descriptor.VertexBuffers;
			var wgpuBufferLayouts = new WGPUVertexBufferLayout[layouts.Length];

			int attributeCount = 0;
			foreach (var layout in layouts)
			{
				attributeCount += layout.Attributes.Length;
			}

			var wgpuAttributes = new WGPUVertexAttribute[attributeCount];
			int attributeIndex = 0;
			var attributeStarts = new int[layouts.Length];
			for (int index = 0; index < layouts.Length; index++)
			{
				attributeStarts[index] = attributeIndex;
				foreach (var attribute in layouts[index].Attributes)
				{
					wgpuAttributes[attributeIndex++] = new WGPUVertexAttribute
					{
						format = WgpuEnums.ToWgpu(attribute.Format),
						offset = attribute.Offset,
						shaderLocation = attribute.ShaderLocation,
					};
				}
			}

			var targets = descriptor.ColorTargets;
			var wgpuTargets = new WGPUColorTargetState[targets.Length];
			var wgpuBlends = new WGPUBlendState[targets.Length];

			var depthStencil = descriptor.DepthStencil.HasDepth
				? WgpuDescriptors.DepthStencil(descriptor.DepthStencil)
				: default;

			using (var labelText = new Utf8Buffer(descriptor.Label))
			using (var vertexEntry = new Utf8Buffer(descriptor.VertexEntryPoint))
			using (var fragmentEntry = new Utf8Buffer(descriptor.FragmentEntryPoint))
			fixed (WGPUVertexAttribute* attributesPointer = wgpuAttributes)
			fixed (WGPUVertexBufferLayout* bufferLayoutsPointer = wgpuBufferLayouts)
			fixed (WGPUColorTargetState* targetsPointer = wgpuTargets)
			fixed (WGPUBlendState* blendsPointer = wgpuBlends)
			{
				for (int index = 0; index < layouts.Length; index++)
				{
					wgpuBufferLayouts[index] = new WGPUVertexBufferLayout
					{
						stepMode = WgpuEnums.ToWgpu(layouts[index].StepMode),
						arrayStride = layouts[index].ArrayStride,
						attributeCount = (nuint)layouts[index].Attributes.Length,
						attributes = attributesPointer + attributeStarts[index],
					};
				}

				for (int index = 0; index < targets.Length; index++)
				{
					var target = targets[index];
					wgpuBlends[index] = new WGPUBlendState
					{
						color = ToWgpuBlend(target.Color),
						alpha = ToWgpuBlend(target.Alpha),
					};

					wgpuTargets[index] = new WGPUColorTargetState
					{
						format = WgpuEnums.ToWgpu(target.Format),

						// A null blend pointer is how webgpu spells "no blending"; there is no enable flag.
						blend = target.BlendEnabled ? blendsPointer + index : null,
						writeMask = WgpuEnums.ToWgpu(target.WriteMask),
					};
				}

				// Built unconditionally but pointed at only when there is a fragment shader: a depth-only
				// pipeline passes a null fragment state, and reaching into a null module to build one
				// would throw before we got the chance.
				var fragment = new WGPUFragmentState
				{
					module = descriptor.FragmentShader == null
						? default
						: Require<WebGpuShaderModule>(descriptor.FragmentShader, "descriptor.FragmentShader").Handle,
					entryPoint = fragmentEntry.View,
					targetCount = (nuint)wgpuTargets.Length,
					targets = targetsPointer,
				};

				var pipelineDescriptor = new WGPURenderPipelineDescriptor
				{
					label = labelText.View,
					layout = pipelineLayout,
					vertex = new WGPUVertexState
					{
						module = Require<WebGpuShaderModule>(descriptor.VertexShader, "descriptor.VertexShader").Handle,
						entryPoint = vertexEntry.View,
						bufferCount = (nuint)wgpuBufferLayouts.Length,
						buffers = bufferLayoutsPointer,
					},
					primitive = new WGPUPrimitiveState
					{
						topology = WgpuEnums.ToWgpu(descriptor.Topology),
						stripIndexFormat = WGPUIndexFormat.Undefined,
						frontFace = WgpuEnums.ToWgpu(descriptor.FrontFace),
						cullMode = WgpuEnums.ToWgpu(descriptor.CullMode),
					},
					depthStencil = descriptor.DepthStencil.HasDepth ? &depthStencil : null,
					multisample = WgpuDescriptors.Multisample(descriptor.SampleCount),
					fragment = descriptor.FragmentShader == null ? null : &fragment,
				};

				WGPURenderPipeline handle = wgpuDeviceCreateRenderPipeline(this.device, &pipelineDescriptor);
				if (handle.IsNull)
				{
					throw new InvalidOperationException(
						$"wgpuDeviceCreateRenderPipeline returned null for '{descriptor.Label}'. "
						+ (this.LastUncapturedError ?? "No uncaptured error was reported."));
				}

				return new WebGpuRenderPipeline(handle, pipelineLayout, bindGroupLayouts, descriptor);
			}
		}

		private static WGPUBlendComponent ToWgpuBlend(in BlendComponent component)
			=> new WGPUBlendComponent
			{
				operation = WgpuEnums.ToWgpu(component.Operation),
				srcFactor = WgpuEnums.ToWgpu(component.SourceFactor),
				dstFactor = WgpuEnums.ToWgpu(component.DestinationFactor),
			};

		/// <summary>
		/// Turns the flat array of layout entries into one <c>WGPUBindGroupLayout</c> per group index.
		/// The entries carry their own group, so the pipeline descriptor can declare every group in one
		/// value-comparable array; the grouping has to be undone here.
		/// </summary>
		/// <param name="entries">Every binding the shaders declare.</param>
		private Dictionary<uint, WGPUBindGroupLayout> CreateBindGroupLayouts(BindGroupLayoutEntry[] entries)
		{
			var byGroup = new SortedDictionary<uint, List<BindGroupLayoutEntry>>();
			foreach (var entry in entries)
			{
				if (!byGroup.TryGetValue(entry.Group, out var list))
				{
					list = new List<BindGroupLayoutEntry>();
					byGroup[entry.Group] = list;
				}

				list.Add(entry);
			}

			var layouts = new Dictionary<uint, WGPUBindGroupLayout>();
			bool complete = false;

			try
			{
				foreach (var pair in byGroup)
				{
					var wgpuEntries = new WGPUBindGroupLayoutEntry[pair.Value.Count];
					for (int index = 0; index < pair.Value.Count; index++)
					{
						wgpuEntries[index] = ToLayoutEntry(pair.Value[index]);
					}

					fixed (WGPUBindGroupLayoutEntry* entriesPointer = wgpuEntries)
					{
						var descriptor = new WGPUBindGroupLayoutDescriptor
						{
							label = WgpuStrings.Null,
							entryCount = (nuint)wgpuEntries.Length,
							entries = entriesPointer,
						};

						WGPUBindGroupLayout layout = wgpuDeviceCreateBindGroupLayout(this.device, &descriptor);
						if (layout.IsNull)
						{
							throw new InvalidOperationException($"wgpuDeviceCreateBindGroupLayout returned null for group {pair.Key}.");
						}

						layouts[pair.Key] = layout;
					}
				}

				complete = true;
			}
			finally
			{
				// A layout that failed part way through owns everything it already made: the caller only
				// gets the dictionary on success, so nobody else can release these.
				if (!complete)
				{
					foreach (var layout in layouts.Values)
					{
						wgpuBindGroupLayoutRelease(layout);
					}

					layouts.Clear();
				}
			}

			return layouts;
		}

		private static WGPUBindGroupLayoutEntry ToLayoutEntry(in BindGroupLayoutEntry entry)
		{
			// Every sub-layout webgpu.h offers is present in the struct at once, and the unused ones must
			// read BindingNotUsed - which is zero in all four enums, so zero-init already says it.
			var layoutEntry = new WGPUBindGroupLayoutEntry
			{
				binding = entry.Binding,
				visibility = WgpuEnums.ToWgpu(entry.Visibility),
			};

			switch (entry.Type)
			{
				case BindingType.UniformBuffer:
					layoutEntry.buffer = new WGPUBufferBindingLayout { type = WGPUBufferBindingType.Uniform };
					break;

				case BindingType.StorageBuffer:
					layoutEntry.buffer = new WGPUBufferBindingLayout { type = WGPUBufferBindingType.Storage };
					break;

				case BindingType.ReadOnlyStorageBuffer:
					layoutEntry.buffer = new WGPUBufferBindingLayout { type = WGPUBufferBindingType.ReadOnlyStorage };
					break;

				case BindingType.Sampler:
					layoutEntry.sampler = new WGPUSamplerBindingLayout { type = WGPUSamplerBindingType.Filtering };
					break;

				case BindingType.Texture:
					layoutEntry.texture = new WGPUTextureBindingLayout
					{
						sampleType = WGPUTextureSampleType.Float,
						viewDimension = WGPUTextureViewDimension._2D,
					};
					break;

				case BindingType.DepthTexture:
					layoutEntry.texture = new WGPUTextureBindingLayout
					{
						sampleType = WGPUTextureSampleType.Depth,
						viewDimension = WGPUTextureViewDimension._2D,
					};
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(entry), entry.Type, "No WGPU binding layout for this value.");
			}

			return layoutEntry;
		}

		/// <summary>
		/// Builds the pipeline layout from the per-group layouts. A pipeline with no declared bindings
		/// gets a null layout, which asks wgpu to derive one from the shader - correct only because it
		/// then has nothing to derive.
		/// </summary>
		/// <param name="bindGroupLayouts">The layouts, keyed by group index.</param>
		private WGPUPipelineLayout CreatePipelineLayout(Dictionary<uint, WGPUBindGroupLayout> bindGroupLayouts)
		{
			if (bindGroupLayouts.Count == 0)
			{
				return default;
			}

			uint highestGroup = 0;
			foreach (uint group in bindGroupLayouts.Keys)
			{
				highestGroup = Math.Max(highestGroup, group);
			}

			var ordered = new WGPUBindGroupLayout[highestGroup + 1];
			for (uint group = 0; group <= highestGroup; group++)
			{
				// A gap in the group indices would need an empty layout object; nothing authors one today,
				// so this refuses rather than inventing a layout nobody asked for.
				if (!bindGroupLayouts.TryGetValue(group, out var layout))
				{
					throw new ArgumentException(
						$"Bind group layouts must be contiguous from group 0; group {group} is missing.",
						nameof(bindGroupLayouts));
				}

				ordered[group] = layout;
			}

			fixed (WGPUBindGroupLayout* layoutsPointer = ordered)
			{
				var descriptor = new WGPUPipelineLayoutDescriptor
				{
					label = WgpuStrings.Null,
					bindGroupLayoutCount = (nuint)ordered.Length,
					bindGroupLayouts = layoutsPointer,
				};

				WGPUPipelineLayout pipelineLayout = wgpuDeviceCreatePipelineLayout(this.device, &descriptor);
				if (pipelineLayout.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreatePipelineLayout returned null.");
				}

				return pipelineLayout;
			}
		}

		/// <summary>
		/// Reads the surface's capabilities and picks the format and usage to configure it with. The format
		/// choice itself is <see cref="PickSurfaceFormat"/>; CopySrc is requested when the
		/// surface allows it, which is what makes a screenshot of the live window possible at all.
		/// </summary>
		/// <param name="surface">The surface to query.</param>
		/// <param name="usage">The usage flags the swapchain textures will be created with.</param>
		/// <param name="presentModes">Every present mode this surface supports; Fifo is the only one WebGPU
		/// guarantees, so anything else has to be checked against this list before it is configured.</param>
		private WGPUTextureFormat ChooseSurfaceFormat(
			WGPUSurface surface,
			out TextureUsage usage,
			out WGPUPresentMode[] presentModes)
		{
			var capabilities = default(WGPUSurfaceCapabilities);
			if (wgpuSurfaceGetCapabilities(surface, this.adapter, &capabilities) != WGPUStatus.Success
				|| capabilities.formatCount == 0)
			{
				throw new InvalidOperationException(
					"wgpuSurfaceGetCapabilities reported no supported formats - the adapter cannot present to this window.");
			}

			try
			{
				var offered = new WGPUTextureFormat[(int)capabilities.formatCount];
				for (nuint index = 0; index < capabilities.formatCount; index++)
				{
					offered[(int)index] = capabilities.formats[index];
				}

				WGPUTextureFormat chosen = PickSurfaceFormat(offered);

				usage = TextureUsage.RenderAttachment;

				// The browser is asked unconditionally, and it has to be: emdawnwebgpu's
				// wgpuSurfaceGetCapabilities never writes the usages field at all (see webgpu.cpp - it fills
				// formats, present modes and alpha modes and returns), so the zeroed struct that goes in comes
				// back reading "this surface supports nothing", and a canvas configured from it cannot be read
				// back. WebGPU itself allows any usage in a canvas configuration, and CopySrc on the swapchain
				// is the whole mechanism behind a browser screenshot, so the missing capability is taken as
				// present rather than absent.
				if (OperatingSystem.IsBrowser() || (capabilities.usages & WGPUTextureUsage.CopySrc) != 0)
				{
					usage |= TextureUsage.CopySrc;
				}

				presentModes = new WGPUPresentMode[(int)capabilities.presentModeCount];
				for (nuint index = 0; index < capabilities.presentModeCount; index++)
				{
					presentModes[(int)index] = capabilities.presentModes[index];
				}

				return chosen;
			}
			finally
			{
				wgpuSurfaceCapabilitiesFreeMembers(capabilities);
			}
		}

		/// <summary>
		/// Picks the format to configure a swapchain with, from the formats the surface offers (wgpu lists
		/// the surface's own preference first). Pure, so the choice is testable without a live surface;
		/// mirrors <c>pick_surface_format</c> in agg-gui-wgpu's <c>gpu.rs</c>, with the Bgra8Unorm
		/// preference added on top as a tie-break.
		/// <para>
		/// Bgra8Unorm first: it is what the golden images were captured in and what every Windows swapchain
		/// offers, so window pixels and golden pixels stay the same pixels. Failing that, any non-sRGB
		/// format beats the surface's first preference - this stack writes bytes that are already
		/// gamma-encoded, and an sRGB surface view would encode them a second time (visibly washed out).
		/// Only when everything on offer is sRGB does the surface's own preference win.
		/// </para>
		/// </summary>
		/// <param name="offered">The formats the surface reports, in the surface's preference order.</param>
		public static WGPUTextureFormat PickSurfaceFormat(IReadOnlyList<WGPUTextureFormat> offered)
		{
			if (offered == null || offered.Count == 0)
			{
				throw new InvalidOperationException(
					"wgpuSurfaceGetCapabilities reported no supported formats - the adapter cannot present to this window.");
			}

			foreach (var format in offered)
			{
				if (format == WGPUTextureFormat.BGRA8Unorm)
				{
					return format;
				}
			}

			foreach (var format in offered)
			{
				if (!IsSrgb(format))
				{
					return format;
				}
			}

			return offered[0];
		}

		/// <summary>
		/// Whether a texture format applies the sRGB transfer function on read/write. Decided by name
		/// rather than a list: the binding is generated from <c>webgpu.h</c>, where every such format is
		/// spelled <c>...UnormSrgb</c>, so a name test cannot go stale when the header gains formats.
		/// </summary>
		private static bool IsSrgb(WGPUTextureFormat format)
			=> format.ToString().EndsWith("Srgb", StringComparison.Ordinal);

		private WGPUCommandEncoder EnsureCommandEncoder()
		{
			if (this.commandEncoder.IsNull)
			{
				var descriptor = new WGPUCommandEncoderDescriptor { label = WgpuStrings.Null };
				this.commandEncoder = wgpuDeviceCreateCommandEncoder(this.device, &descriptor);
				if (this.commandEncoder.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreateCommandEncoder returned null.");
				}
			}

			return this.commandEncoder;
		}

		/// <summary>
		/// The desktop wait strategy for a buffer map, followed by the copy every platform shares. Blocks
		/// until wgpu answers, which is what lets <see cref="ReadTextureAsync"/> hand back an already
		/// completed ValueTask off the browser.
		/// </summary>
		/// <param name="readback">The mappable buffer the copy was recorded into.</param>
		/// <param name="result">The geometry of the read; its TotalBytes is the mapped range.</param>
		/// <param name="destination">Where the mapped bytes are copied to.</param>
		private void MapAndCopy(WGPUBuffer readback, in TextureReadResult result, Span<byte> destination)
		{
			// Pinned heap cell rather than a stack local: the loop below can give up while the callback is
			// still registered, and wgpu would then write the status through the pointer. See
			// PinnedCallbackCell.
			using (var mapCell = new PinnedCallbackCell<CallbackResult>())
			{
				var callbackInfo = new WGPUBufferMapCallbackInfo
				{
					mode = WGPUCallbackMode.AllowProcessEvents,
					callback = &OnBufferMapped,
					userdata1 = mapCell.Pointer,
				};

				wgpuBufferMapAsync(readback, WGPUMapMode.Read, 0, (nuint)result.TotalBytes, callbackInfo);

				// The Phase 0 finding: ProcessEvents alone never resolves a map. DevicePoll with wait: true is
				// what actually drives it, and it blocks until the GPU is done, which is exactly what makes
				// this "async" method able to complete before it returns on the desktop. Unreachable in the
				// browser by construction (ReadTextureAsync branches before here) and it has to stay that
				// way: emdawnwebgpu has no wgpuDevicePoll, so the browser build links a stub that returns
				// "idle" immediately and this loop would burn its whole spin budget and then report a
				// perfectly healthy device as not answering.
				for (int spin = 0; spin < MaxCallbackSpins && mapCell.Value.Status == 0; spin++)
				{
					WgpuNative.wgpuDevicePoll(this.device, true, null);
					wgpuInstanceProcessEvents(this.instance);
				}

				if (mapCell.Value.Status == 0)
				{
					mapCell.Abandon();
					throw new InvalidOperationException(
						$"wgpuBufferMapAsync never called back after {MaxCallbackSpins} polls; the device is not answering.");
				}

				if (mapCell.Value.Status != (int)WGPUMapAsyncStatus.Success)
				{
					throw new InvalidOperationException($"wgpuBufferMapAsync did not succeed (status {mapCell.Value.Status}).");
				}
			}

			CopyMappedRange(readback, result, destination);
		}

		/// <summary>
		/// Copies an already-mapped readback buffer out and unmaps it. Shared by both wait strategies: the
		/// desktop reaches it straight after its spin, the browser from the continuation of its map promise,
		/// and neither leg gets its own copy of the unmap rule.
		/// </summary>
		/// <param name="readback">A buffer that is currently mapped for read.</param>
		/// <param name="result">The geometry of the read; its TotalBytes is the mapped range.</param>
		/// <param name="destination">Where the mapped bytes are copied to.</param>
		private static void CopyMappedRange(WGPUBuffer readback, in TextureReadResult result, Span<byte> destination)
		{
			try
			{
				var mapped = wgpuBufferGetConstMappedRange(readback, 0, (nuint)result.TotalBytes);
				if (mapped == null)
				{
					throw new InvalidOperationException("wgpuBufferGetConstMappedRange returned null.");
				}

				new ReadOnlySpan<byte>(mapped, (int)result.TotalBytes).CopyTo(destination);
			}
			finally
			{
				// Releasing a still-mapped buffer is undefined, so a throw out of the copy must not skip this.
				wgpuBufferUnmap(readback);
			}
		}

		/// <summary>
		/// The adapter request's options, with no callback and no waiting: the half both wait strategies
		/// share, so the desktop's preferences cannot drift from the browser's by editing one of them.
		/// </summary>
		/// <param name="forceFallbackAdapter">True to demand the software adapter.</param>
		/// <param name="preferredBackend">The backend to ask for; Undefined lets wgpu choose.</param>
		/// <param name="compatibleSurface">The surface the adapter must be able to present to, or null.</param>
		private static WGPURequestAdapterOptions BuildAdapterOptions(
			bool forceFallbackAdapter,
			WGPUBackendType preferredBackend,
			WGPUSurface compatibleSurface)
			=> new WGPURequestAdapterOptions
			{
				featureLevel = WGPUFeatureLevel.Core,
				powerPreference = WGPUPowerPreference.HighPerformance,
				forceFallbackAdapter = forceFallbackAdapter,
				backendType = preferredBackend,
				compatibleSurface = compatibleSurface,
			};

		private WGPUAdapter RequestAdapter(
			bool forceFallbackAdapter,
			WGPUBackendType preferredBackend,
			WGPUSurface compatibleSurface)
		{
			WGPURequestAdapterOptions options = BuildAdapterOptions(forceFallbackAdapter, preferredBackend, compatibleSurface);

			// Pinned heap cell, not a stack local - see PinnedCallbackCell for why the timeout path below
			// cannot leave wgpu holding a pointer into this frame.
			using (var cell = new PinnedCallbackCell<AdapterResult>())
			{
				var callbackInfo = new WGPURequestAdapterCallbackInfo
				{
					mode = WGPUCallbackMode.AllowProcessEvents,
					callback = &OnAdapterRequested,
					userdata1 = cell.Pointer,
				};

				wgpuInstanceRequestAdapter(this.instance, &options, callbackInfo);

				for (int spin = 0; spin < MaxCallbackSpins && cell.Value.Status == 0; spin++)
				{
					wgpuInstanceProcessEvents(this.instance);
				}

				if (cell.Value.Status == 0)
				{
					cell.Abandon();
					throw new InvalidOperationException(
						$"wgpuInstanceRequestAdapter never called back after {MaxCallbackSpins} polls "
						+ $"(backend {preferredBackend}, fallback {forceFallbackAdapter}).");
				}

				if (cell.Value.Status != (int)WGPURequestAdapterStatus.Success || cell.Value.Adapter.IsNull)
				{
					throw new InvalidOperationException(
						$"wgpuInstanceRequestAdapter failed (status {cell.Value.Status}, backend {preferredBackend}, fallback {forceFallbackAdapter}).");
				}

				return cell.Value.Adapter;
			}
		}

		/// <summary>
		/// The instance, with wgpu's default descriptor. Wrapped because webgpu.h spells "default" as a null
		/// pointer, which the browser leg's non-unsafe half cannot write at a call site.
		/// </summary>
		private static WGPUInstance CreateInstance() => wgpuCreateInstance(null);

		/// <summary>
		/// The browser wait strategy for the adapter request: hand wgpu a spontaneous callback and a task to
		/// complete, then get out of the way so the JS event loop can resolve the promise. The returned task
		/// is genuinely pending when it is handed back.
		/// </summary>
		/// <param name="compatibleSurface">The canvas surface the adapter must be able to present to.</param>
		/// <remarks>
		/// There is no spin budget here and there cannot be one: a promise that never settles simply never
		/// settles, and the only honest report of that is a boot that never finishes. The desktop's
		/// <see cref="MaxCallbackSpins"/> has no browser equivalent.
		/// </remarks>
		private Task<AdapterResult> RequestAdapterBrowserAsync(WGPUSurface compatibleSurface)
		{
			// forceFallbackAdapter false and backendType Undefined, unconditionally: both are
			// wgpu-native-isms. The browser has exactly one WebGPU implementation and no software adapter to
			// fall back to, so asking for either can only turn a working adapter into no adapter.
			WGPURequestAdapterOptions options = BuildAdapterOptions(false, WGPUBackendType.Undefined, compatibleSurface);

			var completion = new TaskCompletionSource<AdapterResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			var callbackInfo = new WGPURequestAdapterCallbackInfo
			{
				// Spontaneous, not AllowProcessEvents: in the browser the promise resolves on the JS event
				// loop while no managed code is on the stack, and there is nothing to pump it from.
				mode = WGPUCallbackMode.AllowSpontaneous,
				callback = &OnAdapterRequestedSpontaneous,
				userdata1 = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(completion)),
			};

			wgpuInstanceRequestAdapter(this.instance, &options, callbackInfo);

			return completion.Task;
		}

		/// <summary>
		/// The browser wait strategy for the device request. Completes with whatever wgpu answered rather
		/// than throwing, so the caller can apply the same success-and-retry policy the desktop applies.
		/// </summary>
		/// <param name="requiredLimits">The limits to ask for. Taken by value so the address handed to wgpu
		/// belongs to this frame, which outlives the call.</param>
		private Task<DeviceResult> RequestDeviceBrowserAsync(WGPULimits requiredLimits)
		{
			var completion = new TaskCompletionSource<DeviceResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			using (var labelText = new Utf8Buffer(this.label))
			{
				WGPUDeviceDescriptor descriptor = this.BuildDeviceDescriptor(
					labelText.View,
					&requiredLimits,
					WGPUCallbackMode.AllowSpontaneous);

				var callbackInfo = new WGPURequestDeviceCallbackInfo
				{
					mode = WGPUCallbackMode.AllowSpontaneous,
					callback = &OnDeviceRequestedSpontaneous,
					userdata1 = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(completion)),
				};

				wgpuAdapterRequestDevice(this.adapter, &descriptor, callbackInfo);
			}

			return completion.Task;
		}

		/// <summary>
		/// The browser wait strategy for a buffer map. The copy itself is
		/// <see cref="CopyMappedRange"/>, shared with the desktop.
		/// </summary>
		/// <param name="readback">The mappable buffer the texture copy was recorded into.</param>
		/// <param name="totalBytes">The range to map, which is the whole padded read.</param>
		private static Task<CallbackResult> MapForReadBrowserAsync(WGPUBuffer readback, ulong totalBytes)
		{
			var completion = new TaskCompletionSource<CallbackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			var callbackInfo = new WGPUBufferMapCallbackInfo
			{
				mode = WGPUCallbackMode.AllowSpontaneous,
				callback = &OnBufferMappedSpontaneous,
				userdata1 = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(completion)),
			};

			wgpuBufferMapAsync(readback, WGPUMapMode.Read, 0, (nuint)totalBytes, callbackInfo);

			return completion.Task;
		}

		/// <summary>
		/// Reads the limits the device was actually created with, so oversized resources can be refused in
		/// managed code instead of tripping wgpu's validation.
		/// </summary>
		/// <remarks>
		/// A failed query leaves the conservative WebGPU default in place (256 MiB) rather than disabling the
		/// check: guessing high would put the process abort back.
		/// </remarks>
		private void ReadDeviceLimits()
		{
			var deviceLimits = default(WGPULimits);
			bool read = wgpuDeviceGetLimits(this.device, &deviceLimits) == WGPUStatus.Success;
			this.limits = new DeviceLimits(
				read && deviceLimits.maxBufferSize > 0
					? deviceLimits.maxBufferSize
					: DeviceLimits.DefaultMaxBufferSize,
				read && deviceLimits.maxTextureDimension2D > 0
					? deviceLimits.maxTextureDimension2D
					: DeviceLimits.DefaultMaxTextureDimension2D);
		}

		private void ReadAdapterInfo()
		{
			var info = default(WGPUAdapterInfo);
			if (wgpuAdapterGetInfo(this.adapter, &info) != WGPUStatus.Success)
			{
				throw new InvalidOperationException("wgpuAdapterGetInfo failed.");
			}

			// The info struct owns wgpu-allocated strings, so anything wanted from it is copied into
			// managed memory before the members are freed.
			try
			{
				this.AdapterBackend = info.backendType;
				this.AdapterName = WgpuStrings.ToManaged(info.device);
				this.IsFallbackAdapter = info.adapterType == WGPUAdapterType.CPU;
			}
			finally
			{
				wgpuAdapterInfoFreeMembers(info);
			}
		}

		/// <summary>
		/// Opens the device with no optional features.
		/// <para>
		/// Requesting a feature the adapter does not expose fails the whole device request, so anything
		/// added here has to be intersected with <c>wgpuAdapterHasFeature</c> first. The one feature that
		/// was ever wanted is <c>float32-blendable</c>, which is what would make blending a 32-bit float
		/// attachment - a dual depth peeling depth range kept in a MAX-blended Rg32Float target, the way
		/// the D3D11 path keeps it - legal. Measured 2026-08-15 on a GTX 1660 Ti: wgpu-native 29 exposes
		/// it on Vulkan but <b>not</b> on D3D12, even though the same adapter reports R32G32_FLOAT as
		/// Blendable through D3D11 natively. That is why this backend's peel is formulated against two
		/// hardware depth buffers and depth tests instead of a blended float target, and why nothing here
		/// needs the feature.
		/// </para>
		/// </summary>
		private WGPUDevice RequestDevice()
		{
			WGPULimits requiredLimits = this.RequiredLimits();
			DeviceResult result = this.RequestDeviceOnce(requiredLimits);

			if (!Succeeded(result) && RaisesALimit(requiredLimits))
			{
				// A raised maxTextureDimension2D is a want, never a need: without it a very large capture
				// is clamped, but with a refused limit there is no device at all and the window cannot
				// paint. wgpu-native grants whatever the adapter reported, so this is dead code on the
				// desktop; the browser is where an implementation may legitimately refuse a limit it just
				// told us it supports, and losing the whole canvas over it would be the wrong trade.
				result = this.RequestDeviceOnce(UndefinedLimits());
			}

			if (!Succeeded(result))
			{
				throw new InvalidOperationException($"wgpuAdapterRequestDevice failed (status {result.Status}).");
			}

			return result.Device;
		}

		/// <summary>Whether a device request answered with a usable device.</summary>
		/// <param name="result">What the request callback wrote.</param>
		private static bool Succeeded(in DeviceResult result)
			=> result.Status == (int)WGPURequestDeviceStatus.Success && !result.Device.IsNull;

		/// <summary>
		/// Whether a limit set asks for anything above the implementation's defaults - today only
		/// <c>maxTextureDimension2D</c>, which <see cref="RequiredLimits"/> raises. Anything left at the
		/// undefined sentinel is a default request and cannot be what a refusal is about.
		/// </summary>
		/// <param name="limits">The limits a device request carried.</param>
		private static bool RaisesALimit(in WGPULimits limits)
			=> limits.maxTextureDimension2D != WGPUConstants.WGPU_LIMIT_U32_UNDEFINED;

		/// <summary>
		/// The device descriptor both wait strategies share. Only the callback mode differs between them:
		/// the desktop's callbacks are drained by <c>wgpuInstanceProcessEvents</c>, the browser's arrive
		/// spontaneously off the JS event loop with nothing to drain them from.
		/// </summary>
		/// <param name="label">The device label, already UTF-8 and alive for the call.</param>
		/// <param name="requiredLimits">The limits to ask for; must outlive the request call.</param>
		/// <param name="callbackMode">How wgpu is allowed to deliver the device-lost callback.</param>
		private WGPUDeviceDescriptor BuildDeviceDescriptor(
			WGPUStringView label,
			WGPULimits* requiredLimits,
			WGPUCallbackMode callbackMode)
		{
			void* self = (void*)GCHandle.ToIntPtr(this.selfHandle);

			return new WGPUDeviceDescriptor
			{
				label = label,
				requiredFeatureCount = 0,
				requiredFeatures = null,
				requiredLimits = requiredLimits,
				defaultQueue = new WGPUQueueDescriptor { label = WgpuStrings.Null },
				deviceLostCallbackInfo = new WGPUDeviceLostCallbackInfo
				{
					mode = callbackMode,
					callback = &OnDeviceLost,
					userdata1 = self,
				},
				uncapturedErrorCallbackInfo = new WGPUUncapturedErrorCallbackInfo
				{
					callback = &OnUncapturedError,
					userdata1 = self,
				},
			};
		}

		/// <summary>
		/// One desktop device request: issue it and spin until wgpu answers. Whether the answer is a
		/// success is the caller's to judge - a refused limit is retried rather than thrown.
		/// </summary>
		/// <param name="requiredLimits">The limits to ask for. Taken by value so the address handed to
		/// wgpu is this frame's and cannot be a caller's field that moves.</param>
		/// <exception cref="InvalidOperationException">The adapter never called back at all.</exception>
		private DeviceResult RequestDeviceOnce(WGPULimits requiredLimits)
		{
			// Pinned heap cell, not a stack local - see PinnedCallbackCell.
			using (var cell = new PinnedCallbackCell<DeviceResult>())
			using (var labelText = new Utf8Buffer(this.label))
			{
				WGPUDeviceDescriptor descriptor = this.BuildDeviceDescriptor(
					labelText.View,
					&requiredLimits,
					WGPUCallbackMode.AllowProcessEvents);

				var callbackInfo = new WGPURequestDeviceCallbackInfo
				{
					mode = WGPUCallbackMode.AllowProcessEvents,
					callback = &OnDeviceRequested,
					userdata1 = cell.Pointer,
				};

				wgpuAdapterRequestDevice(this.adapter, &descriptor, callbackInfo);

				for (int spin = 0; spin < MaxCallbackSpins && cell.Value.Status == 0; spin++)
				{
					wgpuInstanceProcessEvents(this.instance);
				}

				if (cell.Value.Status == 0)
				{
					cell.Abandon();
					throw new InvalidOperationException(
						$"wgpuAdapterRequestDevice never called back after {MaxCallbackSpins} polls; the adapter is not answering.");
				}

				return cell.Value;
			}
		}

		/// <summary>
		/// The limits the device is opened with: every one left at its WebGPU default except
		/// <c>maxTextureDimension2D</c>, which is raised to whatever the adapter actually supports.
		/// </summary>
		/// <remarks>
		/// wgpu grants the defaults - 8192 pixels - unless a device asks for more, and every desktop adapter
		/// supports 16384. 8192 is not enough for the 3x full-frame supersample of a fullscreen retina
		/// window, so this is what keeps the capture at full quality instead of clamping it down. Only values
		/// read back <i>from the adapter</i> are requested: asking for a limit the adapter cannot grant fails
		/// device creation outright, and nothing else here wants a raised limit anyway (a raised
		/// <c>maxBufferSize</c>, for instance, would silently change the size the mesh path chunks at).
		/// A failed adapter query leaves the field undefined, which is "use the default".
		/// </remarks>
		private WGPULimits RequiredLimits()
		{
			var adapterLimits = default(WGPULimits);
			bool read = wgpuAdapterGetLimits(this.adapter, &adapterLimits) == WGPUStatus.Success;

			var required = UndefinedLimits();
			if (read && adapterLimits.maxTextureDimension2D > 0)
			{
				required.maxTextureDimension2D = adapterLimits.maxTextureDimension2D;
			}

			return required;
		}

		/// <summary>
		/// A limit set with every member at the specification's "undefined" sentinel, which asks the
		/// implementation for its default. <c>default(WGPULimits)</c> would instead ask for zero of
		/// everything and fail device creation.
		/// </summary>
		private static WGPULimits UndefinedLimits()
			=> new WGPULimits
			{
				nextInChain = null,
				maxTextureDimension1D = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxTextureDimension2D = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxTextureDimension3D = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxTextureArrayLayers = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxBindGroups = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxBindGroupsPlusVertexBuffers = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxBindingsPerBindGroup = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxDynamicUniformBuffersPerPipelineLayout = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxDynamicStorageBuffersPerPipelineLayout = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxSampledTexturesPerShaderStage = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxSamplersPerShaderStage = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxStorageBuffersPerShaderStage = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxStorageTexturesPerShaderStage = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxUniformBuffersPerShaderStage = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxUniformBufferBindingSize = WGPUConstants.WGPU_LIMIT_U64_UNDEFINED,
				maxStorageBufferBindingSize = WGPUConstants.WGPU_LIMIT_U64_UNDEFINED,
				minUniformBufferOffsetAlignment = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				minStorageBufferOffsetAlignment = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxVertexBuffers = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxBufferSize = WGPUConstants.WGPU_LIMIT_U64_UNDEFINED,
				maxVertexAttributes = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxVertexBufferArrayStride = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxInterStageShaderVariables = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxColorAttachments = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxColorAttachmentBytesPerSample = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxComputeWorkgroupStorageSize = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxComputeInvocationsPerWorkgroup = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxComputeWorkgroupSizeX = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxComputeWorkgroupSizeY = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxComputeWorkgroupSizeZ = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxComputeWorkgroupsPerDimension = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
				maxImmediateSize = WGPUConstants.WGPU_LIMIT_U32_UNDEFINED,
			};

		private void ThrowIfDisposed()
		{
			if (this.IsDisposed)
			{
				throw new ObjectDisposedException(nameof(WebGpuRenderDevice));
			}
		}

		private void ThrowIfPassOpen(string action)
		{
			if (this.openEncoder != null)
			{
				throw new InvalidOperationException(
					$"Cannot {action} while render pass '{this.openEncoder.Label}' is open. "
					+ "End the pass first and re-open it with LoadOp.Load.");
			}
		}

		private void ReportUncapturedError(string message)
		{
			this.LastUncapturedError = message;
			this.UncapturedError?.Invoke(this, message);
		}

		private void ReportDeviceLost(string message) => this.DeviceLostMessage = message;

		/// <summary>
		/// A one-element pinned heap cell that a native callback writes its result into, and which can
		/// outlive the call that created it.
		/// <para>
		/// The result used to be a local, handed to wgpu as a pointer into this frame's stack. That is
		/// correct only while the waiting loop is still on the stack - and every one of those loops can
		/// give up (<see cref="MaxCallbackSpins"/>) and throw with the callback still registered, at
		/// which point the next <c>wgpuInstanceProcessEvents</c> anywhere in the process writes a status
		/// through a pointer to a dead stack slot and corrupts whatever now lives there.
		/// </para>
		/// <para>
		/// <b>Why a timed-out cell is leaked on purpose.</b> There is no way to cancel a registered wgpu
		/// callback, so the only memory the late write can safely land in is memory nobody ever reuses.
		/// <see cref="Abandon"/> keeps the GCHandle allocated forever. The leak is one small pinned
		/// object per hung call, and a call only hangs on a driver that has stopped answering - a state
		/// this backend already reports as fatal - so bounded leakage is the cheap side of the trade.
		/// <see cref="AbandonedCallbackCellCount"/> makes it visible rather than silent.
		/// </para>
		/// </summary>
		/// <typeparam name="T">The result struct; must be blittable, since native code writes it.</typeparam>
		private sealed class PinnedCallbackCell<T> : IDisposable
			where T : unmanaged
		{
			private readonly T[] storage = new T[1];
			private GCHandle handle;
			private bool abandoned;

			public PinnedCallbackCell()
			{
				this.handle = GCHandle.Alloc(this.storage, GCHandleType.Pinned);
			}

			/// <summary>The address to hand wgpu as its userdata.</summary>
			public T* Pointer => (T*)this.handle.AddrOfPinnedObject();

			/// <summary>What the callback wrote, or the zero-initialized value if it has not run.</summary>
			public ref T Value => ref this.storage[0];

			/// <summary>
			/// Gives up on ever seeing the callback and keeps the cell pinned for the life of the process,
			/// so a late callback writes into memory that is still legally ours.
			/// </summary>
			public void Abandon()
			{
				this.abandoned = true;
				Interlocked.Increment(ref abandonedCallbackCells);
			}

			public void Dispose()
			{
				if (!this.abandoned && this.handle.IsAllocated)
				{
					this.handle.Free();
				}
			}
		}

		// The callbacks below are native entry points: they cannot close over anything, so per-request
		// state arrives as a pointer to a pinned heap cell and per-device state as a GCHandle.
		// Status starts at zero - not a legal WGPU status - so a polling loop can tell "not called yet"
		// from any real answer.
		private struct CallbackResult
		{
			public int Status;
		}

		private struct AdapterResult
		{
			public int Status;

			public WGPUAdapter Adapter;
		}

		private struct DeviceResult
		{
			public int Status;

			public WGPUDevice Device;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnAdapterRequested(WGPURequestAdapterStatus status, WGPUAdapter adapter, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (AdapterResult*)userdata1;
			result->Adapter = adapter;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceRequested(WGPURequestDeviceStatus status, WGPUDevice device, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (DeviceResult*)userdata1;
			result->Device = device;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnBufferMapped(WGPUMapAsyncStatus status, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (CallbackResult*)userdata1;
			result->Status = (int)status;
		}

		// The browser twins of the three callbacks above. Same results, different delivery: there is no
		// pinned result cell to poll, so the pinned thing is the TaskCompletionSource itself and the
		// callback is what hands the answer back to managed code. See TakeCompletion for the handle's
		// lifetime.
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnAdapterRequestedSpontaneous(WGPURequestAdapterStatus status, WGPUAdapter adapter, WGPUStringView message, void* userdata1, void* userdata2)
		{
			// An exception must not unwind into the C caller, so everything here is inside a catch-all.
			try
			{
				TakeCompletion<AdapterResult>(userdata1)?.TrySetResult(
					new AdapterResult { Status = (int)status, Adapter = adapter });
			}
			catch (Exception)
			{
			}
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceRequestedSpontaneous(WGPURequestDeviceStatus status, WGPUDevice device, WGPUStringView message, void* userdata1, void* userdata2)
		{
			try
			{
				TakeCompletion<DeviceResult>(userdata1)?.TrySetResult(
					new DeviceResult { Status = (int)status, Device = device });
			}
			catch (Exception)
			{
			}
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnBufferMappedSpontaneous(WGPUMapAsyncStatus status, WGPUStringView message, void* userdata1, void* userdata2)
		{
			try
			{
				TakeCompletion<CallbackResult>(userdata1)?.TrySetResult(new CallbackResult { Status = (int)status });
			}
			catch (Exception)
			{
			}
		}

		/// <summary>
		/// Recovers the completion source a spontaneous callback was registered with and frees its handle,
		/// which is what makes each of these a one-shot: a second call for the same request finds nothing
		/// and does nothing.
		/// </summary>
		/// <param name="userdata">The GCHandle the request put in wgpu's userdata slot.</param>
		/// <remarks>
		/// A promise that never settles leaks exactly one handle, which is the same bounded trade
		/// <see cref="PinnedCallbackCell{T}"/> makes on the desktop and for the same reason: a registered
		/// wgpu callback cannot be cancelled, so the memory it may still write to must stay ours.
		/// </remarks>
		private static TaskCompletionSource<T> TakeCompletion<T>(void* userdata)
		{
			if (userdata == null)
			{
				return null;
			}

			GCHandle handle = GCHandle.FromIntPtr((nint)userdata);
			if (!handle.IsAllocated)
			{
				return null;
			}

			var completion = handle.Target as TaskCompletionSource<T>;
			handle.Free();

			return completion;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnUncapturedError(WGPUDevice* device, WGPUErrorType type, WGPUStringView message, void* userdata1, void* userdata2)
		{
			// An exception must not unwind into Rust, so everything here is inside a catch-all.
			try
			{
				var target = FromUserdata(userdata1);
				target?.ReportUncapturedError($"{type}: {WgpuStrings.ToManaged(message)}");
			}
			catch (Exception)
			{
			}
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceLost(WGPUDevice* device, WGPUDeviceLostReason reason, WGPUStringView message, void* userdata1, void* userdata2)
		{
			try
			{
				var target = FromUserdata(userdata1);
				target?.ReportDeviceLost($"{reason}: {WgpuStrings.ToManaged(message)}");
			}
			catch (Exception)
			{
			}
		}

		private static WebGpuRenderDevice FromUserdata(void* userdata)
			=> userdata == null ? null : GCHandle.FromIntPtr((nint)userdata).Target as WebGpuRenderDevice;
	}
}
