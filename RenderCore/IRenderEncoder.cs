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
	/// Records draw work into one open render pass (<c>WGPURenderPassEncoder</c>). Obtained from
	/// <see cref="IRenderDevice.BeginRenderPass"/>; disposing it ends the pass
	/// (<c>wgpuRenderPassEncoderEnd</c>). Passes do not nest, and nothing that a pass forbids -
	/// readback, submit, present - may happen while one is alive, so the usual shape is a
	/// <c>using</c> block or a consumer-side EnsurePassOpen/FlushPass pair.
	/// </summary>
	public interface IRenderEncoder : IDisposable
	{
		/// <summary>Binds the pipeline subsequent draws use (<c>wgpuRenderPassEncoderSetPipeline</c>).</summary>
		/// <param name="pipeline">The pipeline to bind.</param>
		void SetPipeline(IRenderPipeline pipeline);

		/// <summary>Binds a bind group at a group index (<c>wgpuRenderPassEncoderSetBindGroup</c>).</summary>
		/// <param name="index">The shader's <c>@group</c> index.</param>
		/// <param name="bindGroup">The group to bind.</param>
		void SetBindGroup(int index, IBindGroup bindGroup);

		/// <summary>Binds a vertex buffer to a slot (<c>wgpuRenderPassEncoderSetVertexBuffer</c>).</summary>
		/// <param name="slot">Slot index, matching the pipeline's vertex buffer layouts.</param>
		/// <param name="buffer">The buffer to bind.</param>
		/// <param name="offset">Byte offset of the first vertex.</param>
		void SetVertexBuffer(int slot, IGpuBuffer buffer, ulong offset = 0);

		/// <summary>Binds the index buffer (<c>wgpuRenderPassEncoderSetIndexBuffer</c>).</summary>
		/// <param name="buffer">The buffer to bind.</param>
		/// <param name="format">Index element width.</param>
		/// <param name="offset">Byte offset of the first index.</param>
		void SetIndexBuffer(IGpuBuffer buffer, IndexFormat format, ulong offset = 0);

		/// <summary>
		/// Sets the viewport (<c>wgpuRenderPassEncoderSetViewport</c>). Floats, and depth bounds
		/// included, because that is webgpu.h's signature.
		/// </summary>
		/// <param name="x">Left edge in pixels.</param>
		/// <param name="y">Top edge in pixels.</param>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		/// <param name="minDepth">Near depth bound.</param>
		/// <param name="maxDepth">Far depth bound.</param>
		void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1);

		/// <summary>
		/// Sets the scissor rectangle (<c>wgpuRenderPassEncoderSetScissorRect</c>). Integers, unlike
		/// the viewport, again matching webgpu.h.
		/// </summary>
		/// <param name="x">Left edge in pixels.</param>
		/// <param name="y">Top edge in pixels.</param>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		void SetScissor(int x, int y, int width, int height);

		/// <summary>Draws non-indexed (<c>wgpuRenderPassEncoderDraw</c>).</summary>
		/// <param name="vertexCount">Number of vertices.</param>
		/// <param name="firstVertex">Index of the first vertex.</param>
		void Draw(int vertexCount, int firstVertex = 0);

		/// <summary>Draws indexed (<c>wgpuRenderPassEncoderDrawIndexed</c>).</summary>
		/// <param name="indexCount">Number of indices.</param>
		/// <param name="firstIndex">Index of the first index.</param>
		/// <param name="baseVertex">Value added to every index before fetching the vertex.</param>
		void DrawIndexed(int indexCount, int firstIndex = 0, int baseVertex = 0);
	}
}
