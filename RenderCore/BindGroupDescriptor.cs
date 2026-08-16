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
	/// One resource bound into a bind group (<c>WGPUBindGroupEntry</c>). Exactly one of the buffer,
	/// texture or sampler is set - use the static factories rather than the constructor so the
	/// combination cannot be got wrong.
	/// </summary>
	public readonly struct BindGroupEntry : IEquatable<BindGroupEntry>
	{
		private BindGroupEntry(uint binding, IGpuBuffer buffer, ulong offset, ulong size, IGpuTexture texture, ISampler sampler)
		{
			this.Binding = binding;
			this.Buffer = buffer;
			this.Offset = offset;
			this.Size = size;
			this.Texture = texture;
			this.Sampler = sampler;
		}

		/// <summary>The shader's <c>@binding</c> index within the group.</summary>
		public uint Binding { get; }

		/// <summary>The bound buffer, or null.</summary>
		public IGpuBuffer Buffer { get; }

		/// <summary>Byte offset into <see cref="Buffer"/>.</summary>
		public ulong Offset { get; }

		/// <summary>Bound byte range of <see cref="Buffer"/>; 0 means to the end.</summary>
		public ulong Size { get; }

		/// <summary>The bound texture, or null.</summary>
		public IGpuTexture Texture { get; }

		/// <summary>The bound sampler, or null.</summary>
		public ISampler Sampler { get; }

		/// <summary>Binds a range of a buffer - the route every uniform takes, paired with <see cref="IRenderDevice.WriteBuffer"/>.</summary>
		/// <param name="binding">The shader's <c>@binding</c> index.</param>
		/// <param name="buffer">The buffer to bind.</param>
		/// <param name="offset">Byte offset into the buffer.</param>
		/// <param name="size">Byte length to bind; 0 binds to the end of the buffer.</param>
		public static BindGroupEntry ForBuffer(uint binding, IGpuBuffer buffer, ulong offset = 0, ulong size = 0)
			=> new BindGroupEntry(binding, buffer, offset, size, null, null);

		/// <summary>Binds a texture.</summary>
		/// <param name="binding">The shader's <c>@binding</c> index.</param>
		/// <param name="texture">The texture to bind.</param>
		public static BindGroupEntry ForTexture(uint binding, IGpuTexture texture)
			=> new BindGroupEntry(binding, null, 0, 0, texture, null);

		/// <summary>Binds a sampler.</summary>
		/// <param name="binding">The shader's <c>@binding</c> index.</param>
		/// <param name="sampler">The sampler to bind.</param>
		public static BindGroupEntry ForSampler(uint binding, ISampler sampler)
			=> new BindGroupEntry(binding, null, 0, 0, null, sampler);

		/// <inheritdoc/>
		public bool Equals(BindGroupEntry other)
			=> this.Binding == other.Binding
			&& ReferenceEquals(this.Buffer, other.Buffer)
			&& this.Offset == other.Offset
			&& this.Size == other.Size
			&& ReferenceEquals(this.Texture, other.Texture)
			&& ReferenceEquals(this.Sampler, other.Sampler);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is BindGroupEntry other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(this.Binding, this.Buffer, this.Offset, this.Size, this.Texture, this.Sampler);

		/// <inheritdoc/>
		public override string ToString()
		{
			if (this.Buffer != null)
			{
				return $"@binding({this.Binding}) buffer {this.Buffer.Label}+{this.Offset}";
			}

			if (this.Texture != null)
			{
				return $"@binding({this.Binding}) texture {this.Texture.Label}";
			}

			return $"@binding({this.Binding}) sampler {this.Sampler?.Label}";
		}
	}

	/// <summary>
	/// Everything needed to create a bind group (<c>WGPUBindGroupDescriptor</c>). The layout is not
	/// passed as its own object - it is taken from <see cref="Pipeline"/>'s declared layout at
	/// <see cref="Group"/>, which is <c>wgpuRenderPipelineGetBindGroupLayout</c> and keeps the layout
	/// authored in exactly one place (the pipeline descriptor).
	/// <para>Bind groups are cached like pipelines, so this compares by value with resources by reference.</para>
	/// </summary>
	public readonly struct BindGroupDescriptor : IEquatable<BindGroupDescriptor>
	{
		private readonly BindGroupEntry[] entries;

		/// <summary>Creates a bind group descriptor.</summary>
		/// <param name="pipeline">The pipeline whose layout this group must satisfy.</param>
		/// <param name="group">The shader's <c>@group</c> index.</param>
		/// <param name="entries">The resources bound, one per binding in the layout.</param>
		/// <param name="label">Optional debug name. Not part of equality.</param>
		public BindGroupDescriptor(IRenderPipeline pipeline, uint group, BindGroupEntry[] entries, string label = null)
		{
			this.Pipeline = pipeline;
			this.Group = group;
			this.entries = entries ?? Array.Empty<BindGroupEntry>();
			this.Label = label ?? string.Empty;
		}

		/// <summary>The pipeline whose layout this group satisfies.</summary>
		public IRenderPipeline Pipeline { get; }

		/// <summary>The shader's <c>@group</c> index.</summary>
		public uint Group { get; }

		/// <summary>The resources bound. Never null.</summary>
		public BindGroupEntry[] Entries => this.entries ?? Array.Empty<BindGroupEntry>();

		/// <summary>Debug name. Not part of equality.</summary>
		public string Label { get; }

		/// <inheritdoc/>
		public bool Equals(BindGroupDescriptor other)
			=> ReferenceEquals(this.Pipeline, other.Pipeline)
			&& this.Group == other.Group
			&& DescriptorEquality.ArrayEquals(this.Entries, other.Entries);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is BindGroupDescriptor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(this.Pipeline, this.Group, DescriptorEquality.ArrayHash(this.Entries));

		/// <inheritdoc/>
		public override string ToString()
			=> $"BindGroup {this.Group} [{string.Join(", ", this.Entries)}]"
			+ (string.IsNullOrEmpty(this.Label) ? string.Empty : $" '{this.Label}'");
	}
}
