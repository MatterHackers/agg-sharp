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
	/// Everything a render pipeline bakes in (<c>WGPURenderPipelineDescriptor</c>): shader entry
	/// points, vertex layout, color targets with their blending and write masks, depth state, raster
	/// state and the bind group layout.
	/// <para>
	/// This is the cache key for pipeline permutations. Equality compares shader modules by reference
	/// (a module is created once and reused) and everything else by value; <see cref="Label"/> is
	/// excluded so debug names cannot fragment the cache.
	/// </para>
	/// </summary>
	public readonly struct RenderPipelineDescriptor : IEquatable<RenderPipelineDescriptor>
	{
		private readonly VertexBufferLayout[] vertexBuffers;
		private readonly ColorTargetState[] colorTargets;
		private readonly BindGroupLayoutEntry[] bindGroupLayout;

		/// <summary>Creates a render pipeline descriptor.</summary>
		/// <param name="vertexShader">Module holding the vertex entry point.</param>
		/// <param name="vertexEntryPoint">Name of the vertex entry point function.</param>
		/// <param name="fragmentShader">Module holding the fragment entry point; null for a depth-only pipeline.</param>
		/// <param name="fragmentEntryPoint">Name of the fragment entry point function.</param>
		/// <param name="vertexBuffers">Layout of each vertex buffer slot, in slot order.</param>
		/// <param name="colorTargets">The color attachments written, in attachment order.</param>
		/// <param name="bindGroupLayout">Every binding the shaders declare, across all groups.</param>
		/// <param name="depthStencil">Depth state; default means no depth attachment.</param>
		/// <param name="topology">How vertices assemble into primitives.</param>
		/// <param name="cullMode">Which faces are discarded.</param>
		/// <param name="frontFace">Which winding is front facing.</param>
		/// <param name="sampleCount">MSAA sample count; must match the attachments.</param>
		/// <param name="label">Optional debug name. Not part of equality.</param>
		public RenderPipelineDescriptor(
			IShaderModule vertexShader,
			string vertexEntryPoint,
			IShaderModule fragmentShader,
			string fragmentEntryPoint,
			VertexBufferLayout[] vertexBuffers,
			ColorTargetState[] colorTargets,
			BindGroupLayoutEntry[] bindGroupLayout = null,
			DepthStencilState depthStencil = default,
			PrimitiveTopology topology = PrimitiveTopology.TriangleList,
			CullMode cullMode = CullMode.None,
			FrontFace frontFace = FrontFace.Ccw,
			uint sampleCount = 1,
			string label = null)
		{
			this.VertexShader = vertexShader;
			this.VertexEntryPoint = vertexEntryPoint ?? string.Empty;
			this.FragmentShader = fragmentShader;
			this.FragmentEntryPoint = fragmentEntryPoint ?? string.Empty;
			this.vertexBuffers = vertexBuffers ?? Array.Empty<VertexBufferLayout>();
			this.colorTargets = colorTargets ?? Array.Empty<ColorTargetState>();
			this.bindGroupLayout = bindGroupLayout ?? Array.Empty<BindGroupLayoutEntry>();
			this.DepthStencil = depthStencil;
			this.Topology = topology;
			this.CullMode = cullMode;
			this.FrontFace = frontFace;
			this.SampleCount = sampleCount;
			this.Label = label ?? string.Empty;
		}

		/// <summary>Module holding the vertex entry point.</summary>
		public IShaderModule VertexShader { get; }

		/// <summary>Name of the vertex entry point function.</summary>
		public string VertexEntryPoint { get; }

		/// <summary>Module holding the fragment entry point, or null for a depth-only pipeline.</summary>
		public IShaderModule FragmentShader { get; }

		/// <summary>Name of the fragment entry point function.</summary>
		public string FragmentEntryPoint { get; }

		/// <summary>Layout of each vertex buffer slot, in slot order. Never null.</summary>
		public VertexBufferLayout[] VertexBuffers => this.vertexBuffers ?? Array.Empty<VertexBufferLayout>();

		/// <summary>The color attachments written, in attachment order. Never null.</summary>
		public ColorTargetState[] ColorTargets => this.colorTargets ?? Array.Empty<ColorTargetState>();

		/// <summary>Every binding the shaders declare, across all groups. Never null.</summary>
		public BindGroupLayoutEntry[] BindGroupLayout => this.bindGroupLayout ?? Array.Empty<BindGroupLayoutEntry>();

		/// <summary>Depth state; <see cref="DepthStencilState.HasDepth"/> is false for no depth attachment.</summary>
		public DepthStencilState DepthStencil { get; }

		/// <summary>How vertices assemble into primitives.</summary>
		public PrimitiveTopology Topology { get; }

		/// <summary>Which faces are discarded.</summary>
		public CullMode CullMode { get; }

		/// <summary>Which winding is front facing.</summary>
		public FrontFace FrontFace { get; }

		/// <summary>MSAA sample count.</summary>
		public uint SampleCount { get; }

		/// <summary>Debug name. Not part of equality.</summary>
		public string Label { get; }

		/// <inheritdoc/>
		public bool Equals(RenderPipelineDescriptor other)
			=> ReferenceEquals(this.VertexShader, other.VertexShader)
			&& ReferenceEquals(this.FragmentShader, other.FragmentShader)
			&& string.Equals(this.VertexEntryPoint, other.VertexEntryPoint, StringComparison.Ordinal)
			&& string.Equals(this.FragmentEntryPoint, other.FragmentEntryPoint, StringComparison.Ordinal)
			&& this.DepthStencil.Equals(other.DepthStencil)
			&& this.Topology == other.Topology
			&& this.CullMode == other.CullMode
			&& this.FrontFace == other.FrontFace
			&& this.SampleCount == other.SampleCount
			&& DescriptorEquality.ArrayEquals(this.VertexBuffers, other.VertexBuffers)
			&& DescriptorEquality.ArrayEquals(this.ColorTargets, other.ColorTargets)
			&& DescriptorEquality.ArrayEquals(this.BindGroupLayout, other.BindGroupLayout);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is RenderPipelineDescriptor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			var hash = default(HashCode);
			hash.Add(this.VertexShader);
			hash.Add(this.FragmentShader);
			hash.Add(this.VertexEntryPoint, StringComparer.Ordinal);
			hash.Add(this.FragmentEntryPoint, StringComparer.Ordinal);
			hash.Add(this.DepthStencil);
			hash.Add(this.Topology);
			hash.Add(this.CullMode);
			hash.Add(this.FrontFace);
			hash.Add(this.SampleCount);
			hash.Add(DescriptorEquality.ArrayHash(this.VertexBuffers));
			hash.Add(DescriptorEquality.ArrayHash(this.ColorTargets));
			hash.Add(DescriptorEquality.ArrayHash(this.BindGroupLayout));
			return hash.ToHashCode();
		}

		/// <inheritdoc/>
		public override string ToString()
			=> $"Pipeline vs {this.VertexShader?.SourceKey}:{this.VertexEntryPoint}"
			+ $" fs {this.FragmentShader?.SourceKey}:{this.FragmentEntryPoint}"
			+ $" {this.Topology} cull {this.CullMode} depth [{this.DepthStencil}]"
			+ $" targets [{string.Join(", ", this.ColorTargets)}]"
			+ (string.IsNullOrEmpty(this.Label) ? string.Empty : $" '{this.Label}'");
	}
}
