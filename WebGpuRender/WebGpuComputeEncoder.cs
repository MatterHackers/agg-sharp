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
	/// One open compute pass. Every member is a direct call onto <c>wgpuComputePassEncoder*</c>, with the
	/// same error discipline as <see cref="WebGpuRenderEncoder"/>: using an ended pass throws here rather
	/// than surfacing as an out-of-band validation error several submits later. A disposed pipeline or bind
	/// group, or an oversize dispatch, is refused here too: a released handle reaches wgpu-native as null,
	/// which panics and aborts the process rather than raising anything catchable.
	/// </summary>
	public sealed unsafe class WebGpuComputeEncoder : IComputeEncoder
	{
		private readonly WebGpuRenderDevice device;
		private WGPUComputePassEncoder handle;

		internal WebGpuComputeEncoder(WebGpuRenderDevice device, WGPUComputePassEncoder handle, string label)
		{
			this.device = device;
			this.handle = handle;
			this.Label = label ?? string.Empty;
		}

		/// <summary>Readable name. Used in the pass-rule messages.</summary>
		public string Label { get; }

		/// <summary>True once the pass has ended.</summary>
		public bool IsEnded { get; private set; }

		/// <inheritdoc/>
		public void SetPipeline(IComputePipeline pipeline)
		{
			this.ThrowIfEnded();
			var computePipeline = Require<WebGpuComputePipeline>(pipeline, nameof(pipeline));
			if (computePipeline.IsDisposed)
			{
				throw new ObjectDisposedException(computePipeline.Label, $"Compute pipeline '{computePipeline.Label}' has been disposed.");
			}

			wgpuComputePassEncoderSetPipeline(this.handle, computePipeline.Handle);
		}

		/// <inheritdoc/>
		public void SetBindGroup(int index, IBindGroup bindGroup)
		{
			this.ThrowIfEnded();
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), index, "A bind group index cannot be negative.");
			}

			var group = Require<WebGpuBindGroup>(bindGroup, nameof(bindGroup));
			if (group.IsDisposed)
			{
				throw new ObjectDisposedException(group.Label, $"Bind group '{group.Label}' has been disposed.");
			}

			wgpuComputePassEncoderSetBindGroup(this.handle, (uint)index, group.Handle, 0, null);
		}

		/// <inheritdoc/>
		public void Dispatch(uint workgroupCountX, uint workgroupCountY = 1, uint workgroupCountZ = 1)
		{
			this.ThrowIfEnded();
			ComputeDispatch.Validate(this.device.Limits, workgroupCountX, workgroupCountY, workgroupCountZ);
			wgpuComputePassEncoderDispatchWorkgroups(this.handle, workgroupCountX, workgroupCountY, workgroupCountZ);
		}

		/// <summary>
		/// Ends the pass (<c>wgpuComputePassEncoderEnd</c>) and releases the encoder. Disposing twice is a
		/// no-op, matching the render encoder.
		/// </summary>
		public void Dispose()
		{
			if (this.IsEnded)
			{
				return;
			}

			this.IsEnded = true;
			wgpuComputePassEncoderEnd(this.handle);
			wgpuComputePassEncoderRelease(this.handle);
			this.handle = default;
			this.device.EndComputePass(this);
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
				throw new InvalidOperationException($"Compute pass '{this.Label}' has already ended.");
			}
		}
	}
}
