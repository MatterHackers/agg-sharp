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
	/// Anything the device handed out that has to be released. Opaque by design: the handle inside is
	/// the backend's business, and app code should never see a native pointer. <see cref="Label"/>
	/// exists because WebGPU carries labels into validation and debugger output, and because a
	/// recorded command stream is far easier to read when the resources have names.
	/// </summary>
	public interface IGpuResource : IDisposable
	{
		/// <summary>A human readable name for diagnostics. May be empty; never null.</summary>
		string Label { get; }
	}

	/// <summary>
	/// A GPU buffer (<c>WGPUBuffer</c>). Contents are written with
	/// <see cref="IRenderDevice.WriteBuffer"/> or at creation; there is deliberately no mapping API on
	/// this seam, because mapping is asynchronous in the browser and the only thing we need it for is
	/// readback, which <see cref="IRenderDevice.ReadTextureAsync"/> owns.
	/// </summary>
	public interface IGpuBuffer : IGpuResource
	{
		/// <summary>The size in bytes requested at creation.</summary>
		ulong SizeInBytes { get; }

		/// <summary>The usages declared at creation. WebGPU rejects any use not declared here.</summary>
		BufferUsage Usage { get; }
	}

	/// <summary>A GPU texture (<c>WGPUTexture</c>), created from a <see cref="TextureDescriptor"/>.</summary>
	public interface IGpuTexture : IGpuResource
	{
		/// <summary>The descriptor this texture was created from.</summary>
		TextureDescriptor Descriptor { get; }
	}

	/// <summary>A texture sampler (<c>WGPUSampler</c>), created from a <see cref="SamplerDescriptor"/>.</summary>
	public interface ISampler : IGpuResource
	{
		/// <summary>The descriptor this sampler was created from.</summary>
		SamplerDescriptor Descriptor { get; }
	}

	/// <summary>
	/// A compiled shader module (<c>WGPUShaderModule</c>). Created from a source <em>key</em> rather
	/// than source text - see <see cref="IRenderDevice.CreateShaderModule"/>.
	/// </summary>
	public interface IShaderModule : IGpuResource
	{
		/// <summary>The key this module was resolved from.</summary>
		string SourceKey { get; }
	}

	/// <summary>
	/// An immutable render pipeline (<c>WGPURenderPipeline</c>): shaders, vertex layout, blend, depth,
	/// raster and bind group layout baked into one object. Nothing about it can be changed after
	/// creation, so state that used to be dynamic (color write mask, blend factors) becomes a cache of
	/// pipeline permutations keyed by <see cref="RenderPipelineDescriptor"/>.
	/// </summary>
	public interface IRenderPipeline : IGpuResource
	{
		/// <summary>The descriptor this pipeline was created from - also its cache key.</summary>
		RenderPipelineDescriptor Descriptor { get; }
	}

	/// <summary>
	/// A bound set of resources (<c>WGPUBindGroup</c>): uniform buffers, textures and samplers matched
	/// to one bind group layout of a pipeline.
	/// </summary>
	public interface IBindGroup : IGpuResource
	{
	}

	/// <summary>
	/// Something that can be presented to - a window swapchain surface (<c>WGPUSurface</c>). The
	/// device does not create these; the platform window host does, and hands one over for
	/// <see cref="IRenderDevice.Present"/>.
	/// </summary>
	public interface ISurfaceTarget : IGpuResource
	{
		/// <summary>The format of the textures this surface hands out.</summary>
		TextureFormat Format { get; }

		/// <summary>Current surface width in pixels.</summary>
		uint Width { get; }

		/// <summary>Current surface height in pixels.</summary>
		uint Height { get; }

		/// <summary>
		/// The texture to render this frame into (<c>wgpuSurfaceGetCurrentTexture</c>). Valid only
		/// until the matching <see cref="IRenderDevice.Present"/>; do not cache it across frames.
		/// </summary>
		IGpuTexture AcquireCurrentTexture();
	}

	/// <summary>
	/// Resolves the <c>sourceKey</c> given to <see cref="IRenderDevice.CreateShaderModule"/> into
	/// actual shader source. Shader text lives with the backend that can compile it (WGSL next to the
	/// WebGPU device), so callers name shaders and never carry source around - which is also what lets
	/// the recording test double "create" shader modules with no compiler present.
	/// </summary>
	public interface IShaderSourceProvider
	{
		/// <summary>
		/// Returns the shader source for <paramref name="sourceKey"/>, or null if this provider does
		/// not know the key. A device with several providers asks each in registration order.
		/// </summary>
		/// <param name="sourceKey">The name the caller passed to <see cref="IRenderDevice.CreateShaderModule"/>.</param>
		string TryGetSource(string sourceKey);
	}
}
