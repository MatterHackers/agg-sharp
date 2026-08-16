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
	/// One open render pass. Every member is a direct call onto
	/// <c>wgpuRenderPassEncoder*</c>; what this class adds is the error discipline the recording test
	/// double already enforces - using an ended pass throws here rather than raising an out-of-band
	/// validation error that surfaces as a blank frame several submits later.
	/// </summary>
	public sealed unsafe class WebGpuRenderEncoder : IRenderEncoder
	{
		private readonly WebGpuRenderDevice device;
		private WGPURenderPassEncoder handle;

		internal WebGpuRenderEncoder(WebGpuRenderDevice device, WGPURenderPassEncoder handle, string label)
		{
			this.device = device;
			this.handle = handle;
			this.Label = label ?? string.Empty;
		}

		/// <summary>Readable name, taken from the pass descriptor. Used in the pass-rule messages.</summary>
		public string Label { get; }

		/// <summary>True once the pass has ended.</summary>
		public bool IsEnded { get; private set; }

		/// <inheritdoc/>
		public void SetPipeline(IRenderPipeline pipeline)
		{
			this.ThrowIfEnded();
			wgpuRenderPassEncoderSetPipeline(this.handle, Require<WebGpuRenderPipeline>(pipeline, nameof(pipeline)).Handle);
		}

		/// <inheritdoc/>
		public void SetBindGroup(int index, IBindGroup bindGroup)
		{
			this.ThrowIfEnded();
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), index, "A bind group index cannot be negative.");
			}

			wgpuRenderPassEncoderSetBindGroup(
				this.handle,
				(uint)index,
				Require<WebGpuBindGroup>(bindGroup, nameof(bindGroup)).Handle,
				0,
				null);
		}

		/// <inheritdoc/>
		public void SetVertexBuffer(int slot, IGpuBuffer buffer, ulong offset = 0)
		{
			this.ThrowIfEnded();
			if (slot < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(slot), slot, "A vertex buffer slot cannot be negative.");
			}

			var wgpuBuffer = Require<WebGpuBuffer>(buffer, nameof(buffer));

			// WGPU_WHOLE_SIZE rather than SizeInBytes - offset: the pooled vertex buffers are rounded up
			// to a power of two, so the bound range is the rest of the buffer by definition, and letting
			// wgpu compute it removes an arithmetic mistake we would otherwise have to test for.
			wgpuRenderPassEncoderSetVertexBuffer(this.handle, (uint)slot, wgpuBuffer.Handle, offset, WGPUConstants.WGPU_WHOLE_SIZE);
		}

		/// <inheritdoc/>
		public void SetIndexBuffer(IGpuBuffer buffer, IndexFormat format, ulong offset = 0)
		{
			this.ThrowIfEnded();
			wgpuRenderPassEncoderSetIndexBuffer(
				this.handle,
				Require<WebGpuBuffer>(buffer, nameof(buffer)).Handle,
				WgpuEnums.ToWgpu(format),
				offset,
				WGPUConstants.WGPU_WHOLE_SIZE);
		}

		/// <inheritdoc/>
		public void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
		{
			this.ThrowIfEnded();
			wgpuRenderPassEncoderSetViewport(this.handle, x, y, width, height, minDepth, maxDepth);
		}

		/// <summary>
		/// Sets the scissor rectangle. WebGPU takes unsigned pixels and rejects a rectangle that is not
		/// wholly inside the attachment, so a negative edge - which GL and D3D11 both silently forgave,
		/// and which real widget code does produce when something scrolls off the top - is refused here
		/// with a message rather than passed on to become a wrapped-around unsigned value and an
		/// out-of-band validation error. Callers clamp before they get here.
		/// </summary>
		/// <param name="x">Left edge in pixels.</param>
		/// <param name="y">Top edge in pixels.</param>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		public void SetScissor(int x, int y, int width, int height)
		{
			this.ThrowIfEnded();
			if (x < 0 || y < 0 || width < 0 || height < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(x),
					$"Scissor ({x}, {y}, {width}, {height}) has a negative component; WebGPU scissors are unsigned and must lie inside the attachment.");
			}

			wgpuRenderPassEncoderSetScissorRect(this.handle, (uint)x, (uint)y, (uint)width, (uint)height);
		}

		/// <inheritdoc/>
		public void Draw(int vertexCount, int firstVertex = 0)
		{
			this.ThrowIfEnded();
			if (vertexCount < 0 || firstVertex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(vertexCount), "Vertex counts and offsets cannot be negative.");
			}

			wgpuRenderPassEncoderDraw(this.handle, (uint)vertexCount, 1, (uint)firstVertex, 0);
		}

		/// <inheritdoc/>
		public void DrawIndexed(int indexCount, int firstIndex = 0, int baseVertex = 0)
		{
			this.ThrowIfEnded();
			if (indexCount < 0 || firstIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(indexCount), "Index counts and offsets cannot be negative.");
			}

			wgpuRenderPassEncoderDrawIndexed(this.handle, (uint)indexCount, 1, (uint)firstIndex, baseVertex, 0);
		}

		/// <summary>
		/// Ends the pass (<c>wgpuRenderPassEncoderEnd</c>) and releases the encoder. Disposing twice is a
		/// no-op, matching the recording double, so a helper's <c>using</c> can sit inside a caller's.
		/// </summary>
		public void Dispose()
		{
			if (this.IsEnded)
			{
				return;
			}

			this.IsEnded = true;
			wgpuRenderPassEncoderEnd(this.handle);
			wgpuRenderPassEncoderRelease(this.handle);
			this.handle = default;
			this.device.EndPass(this);
		}

		/// <inheritdoc/>
		public override string ToString() => this.Label;

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

		private void ThrowIfEnded()
		{
			if (this.IsEnded)
			{
				throw new InvalidOperationException($"Render pass '{this.Label}' has already ended.");
			}
		}
	}
}
