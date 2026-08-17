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
	/// a native drawable - an HWND on Windows, a <c>CAMetalLayer</c> on macOS - and <see cref="Present"/>
	/// presents it. Which of those is used is a runtime decision, not a build-time one: this assembly is
	/// built once for all platforms. What is still Phase 4's is <em>recovery</em>: a lost device is
	/// recorded (<see cref="DeviceLostMessage"/>) and then reported as a clear failure, not repaired.
	/// </para>
	/// <para>
	/// <b>Threading.</b> wgpu is internally synchronized, so device-level calls need no external lock.
	/// The pass and command-encoder bookkeeping in this class is not thread safe, which matches how the
	/// renderer uses it: one device, one thread recording a frame.
	/// </para>
	/// </summary>
	public sealed unsafe class WebGpuRenderDevice : IRenderDevice
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
		/// Creates an instance, picks an adapter and opens a device, synchronously.
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

			// Legal while a pass is open, and the compat layer relies on that for its per-draw pooled
			// buffers - but only because a pooled slot is handed out once per submit window. Queue writes
			// are ordered against submits, not against the draws in a pass.
			fixed (byte* source = data)
			{
				wgpuQueueWriteBuffer(this.queue, target.Handle, offset, source, (nuint)data.Length);
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
		/// Reads a texture back. Completes before the <see cref="ValueTask"/> is returned - the native
		/// recipe is <c>wgpuDevicePoll(device, wait: true)</c>, which <c>wgpuInstanceProcessEvents</c>
		/// alone cannot substitute for - so a desktop caller pays no allocation and no thread hop.
		/// <para>
		/// <b>This submits.</b> A readback that did not flush the recorded commands would return the
		/// texture as it was before this frame's draws, which is never what a caller means. Everything
		/// recorded since the last submit therefore goes to the queue along with the copy.
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

				this.MapAndCopy(readback, result, destination.Span);
			}
			finally
			{
				wgpuBufferRelease(readback);
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
		/// The platform's native surface handle: an HWND on Windows, a <c>CAMetalLayer*</c> on macOS. See
		/// <see cref="WindowSurfaceRequest"/>.
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
		/// should prefer: built through <see cref="WindowSurfaceRequest.ForMetalLayer"/> or
		/// <see cref="WindowSurfaceRequest.ForWindowsHwnd"/>, the call site says which kind of handle it is
		/// holding instead of leaving two bare <see cref="IntPtr"/>s to be read the wrong way.
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
		/// <exception cref="PlatformNotSupportedException">
		/// The OS has no surface source wired up here yet (Linux/X11/Wayland is Phase 8).
		/// </exception>
		private static WGPUSurface CreateRawSurface(WGPUInstance instance, WindowSurfaceRequest request)
		{
			if (request.NativeSurfaceHandle == IntPtr.Zero)
			{
				throw new ArgumentException("A surface needs a native surface handle.", nameof(request));
			}

			// The chained source struct is the one genuinely per-OS thing left in this backend, and the
			// branch is deliberately a runtime check: these assemblies are built once as cross-platform
			// net10.0 and shipped to every OS, so a #if would bake in whichever machine did the build.
			// Both branches take the address of a local, so both stack-allocate the source in this frame
			// and pass it while it is still alive - wgpu copies out of the descriptor during the call.
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

			throw new PlatformNotSupportedException(
				$"No wgpu surface source is implemented for {RuntimeInformation.OSDescription}. "
				+ "Windows (HWND) and macOS (CAMetalLayer) can present; X11/Wayland is not wired up yet.");
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

		/// <summary>Ends any open pass and releases every wgpu object this device owns.</summary>
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
		/// Reads the surface's capabilities and picks the format and usage to configure it with. Bgra8 is
		/// preferred when supported (see <see cref="CreateSurfaceTarget(WindowSurfaceRequest)"/>); CopySrc is requested when the
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
				// wgpu lists the surface's preferred format first.
				WGPUTextureFormat chosen = capabilities.formats[0];
				for (nuint index = 0; index < capabilities.formatCount; index++)
				{
					if (capabilities.formats[index] == WGPUTextureFormat.BGRA8Unorm)
					{
						chosen = WGPUTextureFormat.BGRA8Unorm;
						break;
					}
				}

				usage = TextureUsage.RenderAttachment;
				if ((capabilities.usages & WGPUTextureUsage.CopySrc) != 0)
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
				// this "async" method able to complete before it returns on the desktop.
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

		private WGPUAdapter RequestAdapter(
			bool forceFallbackAdapter,
			WGPUBackendType preferredBackend,
			WGPUSurface compatibleSurface)
		{
			var options = new WGPURequestAdapterOptions
			{
				featureLevel = WGPUFeatureLevel.Core,
				powerPreference = WGPUPowerPreference.HighPerformance,
				forceFallbackAdapter = forceFallbackAdapter,
				backendType = preferredBackend,
				compatibleSurface = compatibleSurface,
			};

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
			this.limits = new DeviceLimits(
				wgpuDeviceGetLimits(this.device, &deviceLimits) == WGPUStatus.Success && deviceLimits.maxBufferSize > 0
					? deviceLimits.maxBufferSize
					: DeviceLimits.DefaultMaxBufferSize);
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
			void* self = (void*)GCHandle.ToIntPtr(this.selfHandle);

			// Pinned heap cell, not a stack local - see PinnedCallbackCell.
			using (var cell = new PinnedCallbackCell<DeviceResult>())
			using (var labelText = new Utf8Buffer(this.label))
			{
				var descriptor = new WGPUDeviceDescriptor
				{
					label = labelText.View,
					requiredFeatureCount = 0,
					requiredFeatures = null,
					defaultQueue = new WGPUQueueDescriptor { label = WgpuStrings.Null },
					deviceLostCallbackInfo = new WGPUDeviceLostCallbackInfo
					{
						mode = WGPUCallbackMode.AllowProcessEvents,
						callback = &OnDeviceLost,
						userdata1 = self,
					},
					uncapturedErrorCallbackInfo = new WGPUUncapturedErrorCallbackInfo
					{
						callback = &OnUncapturedError,
						userdata1 = self,
					},
				};

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

				if (cell.Value.Status != (int)WGPURequestDeviceStatus.Success || cell.Value.Device.IsNull)
				{
					throw new InvalidOperationException($"wgpuAdapterRequestDevice failed (status {cell.Value.Status}).");
				}

				return cell.Value.Device;
			}
		}

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
