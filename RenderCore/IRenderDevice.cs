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

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// The retained rendering seam: create resources, open a render pass, draw, submit, present.
	/// <para>
	/// <b>Guiding rule:</b> every member here must be a near-pass-through to webgpu.h. If a member
	/// cannot be written as one, it does not belong on this interface - it belongs in the layer above
	/// (the compat layer or the scene renderer). This interface exists for exactly two reasons:
	/// headless testing against <c>RecordingRenderDevice</c>, and keeping application code off raw
	/// P/Invoke handles. It is not a backend-portability abstraction and must invent no vocabulary of
	/// its own.
	/// </para>
	/// <para>
	/// <b>Pass lifetime.</b> Render passes are explicit and <em>not nestable</em>: at most one
	/// <see cref="IRenderEncoder"/> may be open on a device at a time, and while one is open no
	/// readback and no submit may happen. Consumers that need to interleave drawing with resource
	/// updates or readback implement an <c>EnsurePassOpen</c>/<c>FlushPass</c> pattern above this
	/// interface - lazily open a pass on the first draw, end it before anything a pass forbids, and
	/// re-open it with <see cref="LoadOp.Load"/> so the pixels already drawn survive. Deliberately
	/// none of that juggling lives here: this interface only reports the rule (by throwing when it is
	/// broken), it does not implement a policy for it.
	/// </para>
	/// </summary>
	public interface IRenderDevice : IDisposable
	{
		/// <summary>
		/// Creates a buffer (<c>wgpuDeviceCreateBuffer</c>). Passing
		/// <paramref name="initialData"/> is the mapped-at-creation path and is the only way to fill a
		/// buffer that does not declare <see cref="BufferUsage.CopyDst"/>.
		/// </summary>
		/// <param name="usage">Every use the buffer will be put to.</param>
		/// <param name="sizeInBytes">Size in bytes.</param>
		/// <param name="initialData">Optional initial contents; must not be longer than the buffer.</param>
		IGpuBuffer CreateBuffer(BufferUsage usage, ulong sizeInBytes, ReadOnlySpan<byte> initialData = default);

		/// <summary>Creates a texture (<c>wgpuDeviceCreateTexture</c>).</summary>
		/// <param name="descriptor">Size, format and declared usages.</param>
		IGpuTexture CreateTexture(in TextureDescriptor descriptor);

		/// <summary>Creates a sampler (<c>wgpuDeviceCreateSampler</c>).</summary>
		/// <param name="descriptor">Filtering and addressing.</param>
		ISampler CreateSampler(in SamplerDescriptor descriptor);

		/// <summary>
		/// Compiles a shader module (<c>wgpuDeviceCreateShaderModule</c>) named by
		/// <paramref name="sourceKey"/>. Callers never carry shader text: the device resolves the key
		/// through its registered <see cref="IShaderSourceProvider"/>s, so the canned WGSL lives with
		/// the backend that can compile it and the test double can "compile" anything.
		/// </summary>
		/// <param name="sourceKey">The name of a registered shader source.</param>
		/// <exception cref="ArgumentException">No registered provider knows the key.</exception>
		IShaderModule CreateShaderModule(string sourceKey);

		/// <summary>
		/// Registers a source of shader text for <see cref="CreateShaderModule"/>. Providers are asked
		/// in registration order. Not itself a webgpu call - it is the plumbing that keeps shader text
		/// out of the caller's hands.
		/// </summary>
		/// <param name="provider">The provider to add.</param>
		void RegisterShaderSources(IShaderSourceProvider provider);

		/// <summary>
		/// Creates an immutable render pipeline (<c>wgpuDeviceCreateRenderPipeline</c>). Everything
		/// that used to be dynamic state - blend factors, color write mask, cull mode, topology - is
		/// baked in here, so callers keep a cache keyed by the descriptor rather than mutating state.
		/// </summary>
		/// <param name="descriptor">The full pipeline state, also usable as the cache key.</param>
		IRenderPipeline CreateRenderPipeline(in RenderPipelineDescriptor descriptor);

		/// <summary>
		/// Creates a bind group (<c>wgpuDeviceCreateBindGroup</c>) binding uniforms, textures and
		/// samplers to one group index of a pipeline's layout.
		/// </summary>
		/// <param name="descriptor">The pipeline, group index and bound resources.</param>
		IBindGroup CreateBindGroup(in BindGroupDescriptor descriptor);

		/// <summary>
		/// Opens a render pass (<c>wgpuCommandEncoderBeginRenderPass</c>) and returns the encoder that
		/// records draws into it. Disposing the encoder ends the pass; recorded work reaches the GPU at
		/// the next <see cref="Submit"/>.
		/// </summary>
		/// <param name="descriptor">Attachments and their load/store ops.</param>
		/// <exception cref="InvalidOperationException">A pass is already open - passes do not nest.</exception>
		IRenderEncoder BeginRenderPass(in RenderPassDescriptor descriptor);

		/// <summary>
		/// Writes bytes into a buffer (<c>wgpuQueueWriteBuffer</c>). This is how all uniform data
		/// flows: build the struct, write it, bind the buffer. The buffer must declare
		/// <see cref="BufferUsage.CopyDst"/>.
		/// </summary>
		/// <param name="buffer">Destination buffer.</param>
		/// <param name="offset">Byte offset into the buffer.</param>
		/// <param name="data">Bytes to write.</param>
		void WriteBuffer(IGpuBuffer buffer, ulong offset, ReadOnlySpan<byte> data);

		/// <summary>
		/// Uploads pixels into a texture (<c>wgpuQueueWriteTexture</c>). Unlike readback, uploads have
		/// no 256-byte row alignment requirement, so <paramref name="bytesPerRow"/> is usually the
		/// tightly packed stride. The texture must declare <see cref="TextureUsage.CopyDst"/>.
		/// <para>
		/// <paramref name="mipLevel"/> is <c>WGPUTexelCopyTextureInfo.mipLevel</c>: a mip chain is
		/// uploaded one level per call, each with the dimensions and stride of that level. The level
		/// must be less than the destination's <see cref="TextureDescriptor.MipLevelCount"/>, which is
		/// fixed at creation - WebGPU cannot grow a texture's mip chain after the fact.
		/// </para>
		/// </summary>
		/// <param name="texture">Destination texture.</param>
		/// <param name="data">Pixels, first row first.</param>
		/// <param name="bytesPerRow">Bytes from one source row to the next.</param>
		/// <param name="mipLevel">Destination mip level; 0 is the full resolution image.</param>
		void WriteTexture(IGpuTexture texture, ReadOnlySpan<byte> data, uint bytesPerRow, uint mipLevel = 0);

		/// <summary>
		/// Reads a texture back into <paramref name="destination"/>
		/// (<c>wgpuCommandEncoderCopyTextureToBuffer</c> then a buffer map).
		/// <para>
		/// Async by design, for two reasons: in the browser buffer mapping is only available
		/// asynchronously, and this repo bans sync-over-async. On the desktop backend the read
		/// completes before the <see cref="ValueTask"/> is returned - the confirmed native recipe is
		/// <c>wgpuDevicePoll(device, wait: true)</c>, which <c>wgpuInstanceProcessEvents</c> alone is
		/// not enough to substitute for - so desktop callers pay no allocation and no thread hop.
		/// </para>
		/// <para>
		/// The returned <see cref="TextureReadResult.RowStride"/> is authoritative and is normally
		/// larger than the tightly packed row: WebGPU requires texture-copy rows to be padded to
		/// <see cref="TextureFormatInfo.CopyBytesPerRowAlignment"/> bytes. Walking the destination as
		/// if it were tightly packed shears the image.
		/// </para>
		/// </summary>
		/// <param name="source">Texture to read; must declare <see cref="TextureUsage.CopySrc"/>.</param>
		/// <param name="destination">Buffer to fill; must hold RowStride * height bytes.</param>
		/// <exception cref="InvalidOperationException">A render pass is open - end it first.</exception>
		/// <exception cref="ArgumentException">The destination is too small for the padded rows.</exception>
		ValueTask<TextureReadResult> ReadTextureAsync(IGpuTexture source, Memory<byte> destination);

		/// <summary>
		/// Submits everything recorded since the last submit to the queue
		/// (<c>wgpuQueueSubmit</c>).
		/// </summary>
		/// <exception cref="InvalidOperationException">A render pass is open - end it first.</exception>
		void Submit();

		/// <summary>
		/// Presents the surface's current texture (<c>wgpuSurfacePresent</c>). The texture acquired
		/// from <paramref name="target"/> is invalid afterwards.
		/// </summary>
		/// <param name="target">The surface to present.</param>
		/// <exception cref="InvalidOperationException">A render pass is open - end it first.</exception>
		void Present(ISurfaceTarget target);
	}
}
