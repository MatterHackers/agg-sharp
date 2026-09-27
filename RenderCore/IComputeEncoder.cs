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

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// Records dispatches into one open compute pass (<c>WGPUComputePassEncoder</c>). Obtained from
	/// <see cref="IRenderDevice.BeginComputePass"/>; disposing it ends the pass
	/// (<c>wgpuComputePassEncoderEnd</c>).
	/// <para>
	/// <b>Lifetime.</b> The same rules as <see cref="IRenderEncoder"/>, and for the same reason - both
	/// kinds of pass live on the device's one command encoder, and webgpu allows only one open pass on an
	/// encoder: a compute pass and a render pass never overlap, and readback, submit and present all throw
	/// while either is open. Recorded work reaches the GPU at the next <see cref="IRenderDevice.Submit"/>
	/// (or the submit a readback performs).
	/// </para>
	/// <para>
	/// <b>Ordering.</b> Each dispatch is its own synchronization scope in WebGPU, so storage buffer writes
	/// from one <see cref="Dispatch"/> are visible to every later dispatch - in this pass or a later one -
	/// with no barrier to spell out. An iterative algorithm can therefore dispatch step after step in one
	/// pass, each reading what the previous one wrote. Dispatches within one call are not ordered
	/// against each other: workgroups of the same dispatch run in any order.
	/// </para>
	/// <para>
	/// <see cref="IRenderDevice.WriteBuffer"/> is <i>not</i> ordered against those dispatches. It is a queue
	/// write (<c>wgpuQueueWriteBuffer</c>), so it lands before the next <see cref="IRenderDevice.Submit"/>
	/// executes - ahead of every dispatch that submit carries, including ones recorded before the write.
	/// Rewriting one uniform buffer between dispatches in a pass therefore leaves every dispatch seeing the
	/// last value written. Per-dispatch parameters need their own buffer (and bind group) per dispatch, or
	/// a submit between the writes; this seam has no dynamic offsets.
	/// </para>
	/// </summary>
	public interface IComputeEncoder : IDisposable
	{
		/// <summary>Binds the pipeline subsequent dispatches use (<c>wgpuComputePassEncoderSetPipeline</c>).</summary>
		/// <param name="pipeline">The pipeline to bind.</param>
		/// <exception cref="ObjectDisposedException">The pipeline has been disposed.</exception>
		void SetPipeline(IComputePipeline pipeline);

		/// <summary>Binds a bind group at a group index (<c>wgpuComputePassEncoderSetBindGroup</c>).</summary>
		/// <param name="index">The shader's <c>@group</c> index.</param>
		/// <param name="bindGroup">The group to bind; created against a compute pipeline's layout.</param>
		/// <exception cref="ObjectDisposedException">The bind group has been disposed.</exception>
		void SetBindGroup(int index, IBindGroup bindGroup);

		/// <summary>
		/// Dispatches a grid of workgroups (<c>wgpuComputePassEncoderDispatchWorkgroups</c>). The counts are
		/// workgroups, not invocations: the shader's <c>@workgroup_size</c> multiplies them. Each count must
		/// not exceed <see cref="DeviceLimits.MaxComputeWorkgroupsPerDimension"/>.
		/// </summary>
		/// <param name="workgroupCountX">Workgroups along X.</param>
		/// <param name="workgroupCountY">Workgroups along Y.</param>
		/// <param name="workgroupCountZ">Workgroups along Z.</param>
		/// <exception cref="ArgumentOutOfRangeException">A count is over the device's per-dimension limit.</exception>
		void Dispatch(uint workgroupCountX, uint workgroupCountY = 1, uint workgroupCountZ = 1);
	}
}
