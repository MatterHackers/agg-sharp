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
	/// Everything a compute pipeline bakes in (<c>WGPUComputePipelineDescriptor</c>): one shader entry
	/// point and the bind group layout it reads and writes through.
	/// <para>
	/// The compute twin of <see cref="RenderPipelineDescriptor"/>, and authored the same way: WGSL cannot
	/// be reflected, so every binding the shader declares is listed as data in
	/// <see cref="BindGroupLayout"/> (with <see cref="ShaderStage.Compute"/> visibility), and bind groups
	/// for the pipeline take their layout from here. Equality compares the shader module by reference and
	/// everything else by value, so it can key a pipeline cache; <see cref="Label"/> is excluded.
	/// </para>
	/// </summary>
	public readonly struct ComputePipelineDescriptor : IEquatable<ComputePipelineDescriptor>
	{
		private readonly BindGroupLayoutEntry[] bindGroupLayout;

		/// <summary>Creates a compute pipeline descriptor.</summary>
		/// <param name="shader">Module holding the compute entry point.</param>
		/// <param name="entryPoint">Name of the <c>@compute</c> entry point function.</param>
		/// <param name="bindGroupLayout">Every binding the shader declares, across all groups.</param>
		/// <param name="label">Optional debug name. Not part of equality.</param>
		public ComputePipelineDescriptor(
			IShaderModule shader,
			string entryPoint,
			BindGroupLayoutEntry[] bindGroupLayout = null,
			string label = null)
		{
			this.Shader = shader;
			this.EntryPoint = entryPoint ?? string.Empty;
			this.bindGroupLayout = bindGroupLayout ?? Array.Empty<BindGroupLayoutEntry>();
			this.Label = label ?? string.Empty;
		}

		/// <summary>Module holding the compute entry point.</summary>
		public IShaderModule Shader { get; }

		/// <summary>Name of the <c>@compute</c> entry point function.</summary>
		public string EntryPoint { get; }

		/// <summary>Every binding the shader declares, across all groups. Never null.</summary>
		public BindGroupLayoutEntry[] BindGroupLayout => this.bindGroupLayout ?? Array.Empty<BindGroupLayoutEntry>();

		/// <summary>Debug name. Not part of equality.</summary>
		public string Label { get; }

		/// <inheritdoc/>
		public bool Equals(ComputePipelineDescriptor other)
			=> ReferenceEquals(this.Shader, other.Shader)
			&& string.Equals(this.EntryPoint, other.EntryPoint, StringComparison.Ordinal)
			&& DescriptorEquality.ArrayEquals(this.BindGroupLayout, other.BindGroupLayout);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is ComputePipelineDescriptor other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(
				this.Shader,
				StringComparer.Ordinal.GetHashCode(this.EntryPoint ?? string.Empty),
				DescriptorEquality.ArrayHash(this.BindGroupLayout));

		/// <inheritdoc/>
		public override string ToString()
			=> $"ComputePipeline {this.Shader?.SourceKey}:{this.EntryPoint}"
			+ (string.IsNullOrEmpty(this.Label) ? string.Empty : $" '{this.Label}'");
	}
}
