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

namespace MatterHackers.RenderCore.Testing
{
	/// <summary>
	/// Base for the resources <see cref="RecordingRenderDevice"/> hands out. They hold no GPU state -
	/// their whole job is to have an identity (a distinct object with a readable
	/// <see cref="IGpuResource.Label"/>) so a recorded command stream can be read and asserted.
	/// </summary>
	public abstract class StubResource : IGpuResource
	{
		/// <summary>Creates a stub resource.</summary>
		/// <param name="label">The readable name this resource appears under in a dump.</param>
		protected StubResource(string label)
		{
			this.Label = label ?? string.Empty;
		}

		/// <inheritdoc/>
		public string Label { get; }

		/// <summary>True once <see cref="Dispose"/> has been called - the leak check for a test.</summary>
		public bool IsDisposed { get; private set; }

		/// <inheritdoc/>
		public void Dispose()
		{
			this.IsDisposed = true;
		}

		/// <inheritdoc/>
		public override string ToString() => this.Label;
	}

	/// <summary>A recorded stand-in for a GPU buffer.</summary>
	public sealed class StubBuffer : StubResource, IGpuBuffer
	{
		/// <summary>Creates a stub buffer.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="usage">Declared usages.</param>
		/// <param name="sizeInBytes">Requested size.</param>
		public StubBuffer(string label, BufferUsage usage, ulong sizeInBytes)
			: base(label)
		{
			this.Usage = usage;
			this.SizeInBytes = sizeInBytes;
		}

		/// <inheritdoc/>
		public ulong SizeInBytes { get; }

		/// <inheritdoc/>
		public BufferUsage Usage { get; }
	}

	/// <summary>A recorded stand-in for a GPU texture.</summary>
	public sealed class StubTexture : StubResource, IGpuTexture
	{
		/// <summary>Creates a stub texture.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="descriptor">The descriptor it stands in for.</param>
		public StubTexture(string label, in TextureDescriptor descriptor)
			: base(label)
		{
			this.Descriptor = descriptor;
		}

		/// <inheritdoc/>
		public TextureDescriptor Descriptor { get; }
	}

	/// <summary>A recorded stand-in for a sampler.</summary>
	public sealed class StubSampler : StubResource, ISampler
	{
		/// <summary>Creates a stub sampler.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="descriptor">The descriptor it stands in for.</param>
		public StubSampler(string label, in SamplerDescriptor descriptor)
			: base(label)
		{
			this.Descriptor = descriptor;
		}

		/// <inheritdoc/>
		public SamplerDescriptor Descriptor { get; }
	}

	/// <summary>A recorded stand-in for a compiled shader module.</summary>
	public sealed class StubShaderModule : StubResource, IShaderModule
	{
		/// <summary>Creates a stub shader module.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="sourceKey">The key it was resolved from.</param>
		/// <param name="source">The resolved source, if a provider supplied one.</param>
		public StubShaderModule(string label, string sourceKey, string source)
			: base(label)
		{
			this.SourceKey = sourceKey;
			this.Source = source;
		}

		/// <inheritdoc/>
		public string SourceKey { get; }

		/// <summary>
		/// The text a registered <see cref="IShaderSourceProvider"/> returned, or null when the device
		/// was left in its default permissive mode and resolved the key to nothing.
		/// </summary>
		public string Source { get; }
	}

	/// <summary>A recorded stand-in for a render pipeline.</summary>
	public sealed class StubRenderPipeline : StubResource, IRenderPipeline
	{
		/// <summary>Creates a stub pipeline.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="descriptor">The descriptor it stands in for.</param>
		public StubRenderPipeline(string label, in RenderPipelineDescriptor descriptor)
			: base(label)
		{
			this.Descriptor = descriptor;
		}

		/// <inheritdoc/>
		public RenderPipelineDescriptor Descriptor { get; }
	}

	/// <summary>A recorded stand-in for a bind group.</summary>
	public sealed class StubBindGroup : StubResource, IBindGroup
	{
		/// <summary>Creates a stub bind group.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="descriptor">The descriptor it stands in for.</param>
		public StubBindGroup(string label, in BindGroupDescriptor descriptor)
			: base(label)
		{
			this.Descriptor = descriptor;
		}

		/// <summary>The descriptor this group was created from.</summary>
		public BindGroupDescriptor Descriptor { get; }
	}

	/// <summary>
	/// A recorded stand-in for a window surface. Tests construct one directly and hand it to
	/// <see cref="IRenderDevice.Present"/>; it always yields the same backing texture, which is what
	/// lets a test assert that a frame drew into the surface it presented.
	/// </summary>
	public sealed class StubSurfaceTarget : StubResource, ISurfaceTarget
	{
		private readonly StubTexture currentTexture;

		/// <summary>Creates a stub surface.</summary>
		/// <param name="label">Readable name.</param>
		/// <param name="width">Surface width in pixels.</param>
		/// <param name="height">Surface height in pixels.</param>
		/// <param name="format">Surface format.</param>
		public StubSurfaceTarget(string label = "surface", uint width = 256, uint height = 256, TextureFormat format = TextureFormat.Bgra8Unorm)
			: base(label)
		{
			this.Width = width;
			this.Height = height;
			this.Format = format;
			this.currentTexture = new StubTexture(
				label + ".texture",
				new TextureDescriptor(width, height, format, TextureUsage.RenderAttachment | TextureUsage.CopySrc));
		}

		/// <inheritdoc/>
		public TextureFormat Format { get; }

		/// <inheritdoc/>
		public uint Width { get; }

		/// <inheritdoc/>
		public uint Height { get; }

		/// <summary>How many times a frame has acquired this surface's texture.</summary>
		public int AcquireCount { get; private set; }

		/// <inheritdoc/>
		public IGpuTexture AcquireCurrentTexture()
		{
			this.AcquireCount++;
			return this.currentTexture;
		}
	}
}
