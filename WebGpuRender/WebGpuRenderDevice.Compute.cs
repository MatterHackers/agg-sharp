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
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The compute half of <see cref="WebGpuRenderDevice"/>: compute pipelines, compute passes, and buffer
	/// readback. Split from <c>WebGpuRenderDevice.cs</c> by responsibility only - it shares that file's
	/// command encoder, pass bookkeeping and map/wait machinery rather than having its own.
	/// <para>
	/// Compute work rides the same command encoder as rendering, so it is ordered with the draws and
	/// copies around it and reaches the queue at the same <see cref="Submit"/>. That is also why a compute
	/// pass is under a render pass's rules: webgpu allows one open pass per command encoder.
	/// </para>
	/// </summary>
	public sealed unsafe partial class WebGpuRenderDevice
	{
		/// <inheritdoc/>
		public IComputePipeline CreateComputePipeline(in ComputePipelineDescriptor descriptor)
		{
			this.ThrowIfDisposed();
			var shader = Require<WebGpuShaderModule>(descriptor.Shader, "descriptor.Shader");
			if (string.IsNullOrEmpty(descriptor.EntryPoint))
			{
				throw new ArgumentException("A compute pipeline needs an entry point name.", nameof(descriptor));
			}

			var bindGroupLayouts = this.CreateBindGroupLayouts(descriptor.BindGroupLayout);
			WGPUPipelineLayout pipelineLayout = default;

			try
			{
				pipelineLayout = this.CreatePipelineLayout(bindGroupLayouts);

				using (var labelText = new Utf8Buffer(descriptor.Label))
				using (var entryPoint = new Utf8Buffer(descriptor.EntryPoint))
				{
					var pipelineDescriptor = new WGPUComputePipelineDescriptor
					{
						label = labelText.View,
						layout = pipelineLayout,
						compute = new WGPUComputeState
						{
							module = shader.Handle,
							entryPoint = entryPoint.View,
						},
					};

					WGPUComputePipeline handle = wgpuDeviceCreateComputePipeline(this.device, &pipelineDescriptor);
					if (handle.IsNull)
					{
						throw new InvalidOperationException(
							$"wgpuDeviceCreateComputePipeline returned null for '{descriptor.Label}'. "
							+ (this.LastUncapturedError ?? "No uncaptured error was reported."));
					}

					return new WebGpuComputePipeline(handle, pipelineLayout, bindGroupLayouts, descriptor);
				}
			}
			catch
			{
				// Same ownership rule as CreateRenderPipeline: until the pipeline object exists, the
				// layouts made for it belong to nobody else.
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
		public IComputeEncoder BeginComputePass(string label = null)
		{
			this.ThrowIfDisposed();
			this.ThrowIfPassOpen("begin a compute pass");

			WGPUCommandEncoder encoder = this.EnsureCommandEncoder();
			using (var labelText = new Utf8Buffer(label ?? string.Empty))
			{
				var passDescriptor = new WGPUComputePassDescriptor { label = labelText.View };
				WGPUComputePassEncoder pass = wgpuCommandEncoderBeginComputePass(encoder, &passDescriptor);
				if (pass.IsNull)
				{
					throw new InvalidOperationException("wgpuCommandEncoderBeginComputePass returned null.");
				}

				this.openComputeEncoder = new WebGpuComputeEncoder(
					this,
					pass,
					string.IsNullOrEmpty(label) ? "computePass" : label);
				return this.openComputeEncoder;
			}
		}

		/// <summary>
		/// Reads a range of a buffer back: copy it into a fresh MapRead staging buffer, submit, map, copy
		/// out. The same shape and the same two wait strategies as <see cref="ReadTextureAsync"/> - see that
		/// method for why this submits, why the desktop leg completes before returning, and why the browser
		/// leg hands the staging buffer to a continuation. A staging copy is needed at all because WebGPU
		/// forbids MapRead alongside any usage but CopyDst, so a storage buffer can never be mapped itself.
		/// </summary>
		/// <param name="source">Buffer to read; must declare <see cref="BufferUsage.CopySrc"/>.</param>
		/// <param name="offset">Byte offset into <paramref name="source"/>; a multiple of 4.</param>
		/// <param name="destination">Where the bytes go; its length, a multiple of 4, is how many are read.</param>
		/// <exception cref="InvalidOperationException">A pass is open, or the map failed.</exception>
		/// <exception cref="ArgumentException">The range is misaligned, outside the buffer, or the buffer lacks CopySrc.</exception>
		/// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
		public ValueTask ReadBufferAsync(IGpuBuffer source, ulong offset, Memory<byte> destination)
		{
			this.ThrowIfDisposed();
			var buffer = Require<WebGpuBuffer>(source, nameof(source));
			if (buffer.IsDisposed)
			{
				// A released buffer's handle is null, and wgpu-native aborts the process on a null copy source.
				throw new ObjectDisposedException(buffer.Label, $"Buffer '{buffer.Label}' has been disposed.");
			}

			BufferReadback.Validate(buffer, offset, destination.Length);
			this.ThrowIfPassOpen("read a buffer back");

			ulong totalBytes = (ulong)destination.Length;
			if (totalBytes == 0)
			{
				// Still a submit, as the contract says: a caller may lean on any readback to flush what it
				// recorded, and an empty read should not be the one case that silently does not.
				this.Submit();
				return default;
			}

			var stagingDescriptor = new WGPUBufferDescriptor
			{
				label = WgpuStrings.Null,
				usage = WGPUBufferUsage.CopyDst | WGPUBufferUsage.MapRead,
				size = totalBytes,
				mappedAtCreation = false,
			};

			WGPUBuffer staging = wgpuDeviceCreateBuffer(this.device, &stagingDescriptor);
			if (staging.IsNull)
			{
				throw new InvalidOperationException("wgpuDeviceCreateBuffer returned null for the readback buffer.");
			}

			try
			{
				wgpuCommandEncoderCopyBufferToBuffer(this.EnsureCommandEncoder(), buffer.Handle, offset, staging, 0, totalBytes);
				this.Submit();

				if (OperatingSystem.IsBrowser())
				{
					// Ownership of the staging buffer moves to the continuation, exactly as in
					// ReadTextureAsync; clearing the local keeps the finally from releasing it under wgpu.
					var pending = this.MapAndCopyBrowserAsync(staging, totalBytes, destination);
					staging = default;
					return new ValueTask(pending);
				}

				this.MapAndCopy(staging, totalBytes, destination.Span);
			}
			finally
			{
				if (!staging.IsNull)
				{
					wgpuBufferRelease(staging);
				}
			}

			return default;
		}

		/// <summary>Called by the compute encoder when its pass ends, so the device knows the pass rules relax again.</summary>
		/// <param name="encoder">The encoder that ended.</param>
		internal void EndComputePass(WebGpuComputeEncoder encoder)
		{
			if (ReferenceEquals(this.openComputeEncoder, encoder))
			{
				this.openComputeEncoder = null;
			}
		}
	}
}
