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
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The handle-owning half of the backend: one small class per <c>IGpu*</c> interface, each holding
	/// exactly the wgpu handles that object needs and releasing them on Dispose.
	/// <para>
	/// They are public so that a host (the eventual swapchain control) can hand a texture it created
	/// itself across the seam, but the handles stay internal: application code above
	/// <see cref="IRenderDevice"/> is not supposed to see a native pointer, which is half the reason
	/// the seam exists.
	/// </para>
	/// </summary>
	public sealed class WebGpuBuffer : IGpuBuffer
	{
		internal WebGpuBuffer(WGPUBuffer handle, BufferUsage usage, ulong sizeInBytes, string label)
		{
			this.Handle = handle;
			this.Usage = usage;
			this.SizeInBytes = sizeInBytes;
			this.Label = label ?? string.Empty;
		}

		/// <inheritdoc/>
		public string Label { get; }

		/// <inheritdoc/>
		public ulong SizeInBytes { get; }

		/// <inheritdoc/>
		public BufferUsage Usage { get; }

		/// <summary>True once the buffer has been released.</summary>
		public bool IsDisposed { get; private set; }

		internal WGPUBuffer Handle { get; private set; }

		/// <summary>Releases the wgpu buffer.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			wgpuBufferRelease(this.Handle);
			this.Handle = default;
		}
	}

	/// <summary>
	/// A texture and the default view over it. The view is created once with the texture rather than
	/// per pass, because every use this backend has for a texture - color attachment, depth attachment,
	/// sampled binding - wants the same whole-resource view, and creating one per frame is pure garbage.
	/// </summary>
	public sealed class WebGpuTexture : IGpuTexture
	{
		internal WebGpuTexture(WGPUTexture handle, WGPUTextureView view, in TextureDescriptor descriptor)
		{
			this.Handle = handle;
			this.View = view;
			this.Descriptor = descriptor;
		}

		/// <inheritdoc/>
		public string Label => this.Descriptor.Label;

		/// <inheritdoc/>
		public TextureDescriptor Descriptor { get; }

		/// <summary>True once the texture has been released.</summary>
		public bool IsDisposed { get; private set; }

		internal WGPUTexture Handle { get; private set; }

		internal WGPUTextureView View { get; private set; }

		/// <summary>Releases the view and the texture, in that order.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			wgpuTextureViewRelease(this.View);
			wgpuTextureRelease(this.Handle);
			this.View = default;
			this.Handle = default;
		}
	}

	/// <summary>A wgpu sampler.</summary>
	public sealed class WebGpuSampler : ISampler
	{
		internal WebGpuSampler(WGPUSampler handle, in SamplerDescriptor descriptor)
		{
			this.Handle = handle;
			this.Descriptor = descriptor;
		}

		/// <inheritdoc/>
		public string Label => this.Descriptor.Label;

		/// <inheritdoc/>
		public SamplerDescriptor Descriptor { get; }

		/// <summary>True once the sampler has been released.</summary>
		public bool IsDisposed { get; private set; }

		internal WGPUSampler Handle { get; private set; }

		/// <summary>Releases the wgpu sampler.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			wgpuSamplerRelease(this.Handle);
			this.Handle = default;
		}
	}

	/// <summary>A compiled WGSL module, remembering the key it was resolved from.</summary>
	public sealed class WebGpuShaderModule : IShaderModule
	{
		internal WebGpuShaderModule(WGPUShaderModule handle, string sourceKey)
		{
			this.Handle = handle;
			this.SourceKey = sourceKey;
		}

		/// <inheritdoc/>
		public string Label => this.SourceKey;

		/// <inheritdoc/>
		public string SourceKey { get; }

		/// <summary>True once the module has been released.</summary>
		public bool IsDisposed { get; private set; }

		internal WGPUShaderModule Handle { get; private set; }

		/// <summary>Releases the wgpu shader module.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			wgpuShaderModuleRelease(this.Handle);
			this.Handle = default;
		}
	}

	/// <summary>
	/// A render pipeline, its pipeline layout, and the bind group layouts that layout was built from.
	/// <para>
	/// The layouts are kept rather than re-fetched with <c>wgpuRenderPipelineGetBindGroupLayout</c>
	/// because this backend authors them explicitly from
	/// <see cref="RenderPipelineDescriptor.BindGroupLayout"/> - WGSL cannot be reflected, so the layout
	/// is data either way, and holding the objects we already created is both cheaper and the only
	/// option that works when a group index has no bindings.
	/// </para>
	/// </summary>
	public sealed class WebGpuRenderPipeline : IRenderPipeline
	{
		private readonly Dictionary<uint, WGPUBindGroupLayout> bindGroupLayouts;
		private WGPUPipelineLayout pipelineLayout;

		internal WebGpuRenderPipeline(
			WGPURenderPipeline handle,
			WGPUPipelineLayout pipelineLayout,
			Dictionary<uint, WGPUBindGroupLayout> bindGroupLayouts,
			in RenderPipelineDescriptor descriptor)
		{
			this.Handle = handle;
			this.pipelineLayout = pipelineLayout;
			this.bindGroupLayouts = bindGroupLayouts;
			this.Descriptor = descriptor;
		}

		/// <inheritdoc/>
		public string Label => this.Descriptor.Label;

		/// <inheritdoc/>
		public RenderPipelineDescriptor Descriptor { get; }

		/// <summary>True once the pipeline has been released.</summary>
		public bool IsDisposed { get; private set; }

		internal WGPURenderPipeline Handle { get; private set; }

		/// <summary>The layout declared for one group index.</summary>
		/// <param name="group">The shader's <c>@group</c> index.</param>
		/// <exception cref="ArgumentException">The pipeline declares no bindings in that group.</exception>
		internal WGPUBindGroupLayout LayoutForGroup(uint group)
		{
			if (!this.bindGroupLayouts.TryGetValue(group, out var layout))
			{
				throw new ArgumentException(
					$"Pipeline '{this.Label}' declares no bindings in group {group}, so no bind group can be created for it.",
					nameof(group));
			}

			return layout;
		}

		/// <summary>Releases the pipeline, its layout and every bind group layout behind it.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			wgpuRenderPipelineRelease(this.Handle);
			this.Handle = default;

			foreach (var layout in this.bindGroupLayouts.Values)
			{
				wgpuBindGroupLayoutRelease(layout);
			}

			this.bindGroupLayouts.Clear();

			wgpuPipelineLayoutRelease(this.pipelineLayout);
			this.pipelineLayout = default;
		}
	}

	/// <summary>A bound set of resources.</summary>
	public sealed class WebGpuBindGroup : IBindGroup
	{
		internal WebGpuBindGroup(WGPUBindGroup handle, string label)
		{
			this.Handle = handle;
			this.Label = label ?? string.Empty;
		}

		/// <inheritdoc/>
		public string Label { get; }

		/// <summary>True once the bind group has been released.</summary>
		public bool IsDisposed { get; private set; }

		internal WGPUBindGroup Handle { get; private set; }

		/// <summary>Releases the wgpu bind group.</summary>
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;
			wgpuBindGroupRelease(this.Handle);
			this.Handle = default;
		}
	}
}
