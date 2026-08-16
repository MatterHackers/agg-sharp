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
	/// One vertex shader input pulled out of a vertex buffer (<c>WGPUVertexAttribute</c>). WGSL has no
	/// runtime reflection, so <see cref="ShaderLocation"/> is authored to match the shader's
	/// <c>@location</c> rather than looked up by name.
	/// </summary>
	public readonly struct VertexAttribute : IEquatable<VertexAttribute>
	{
		/// <summary>Creates a vertex attribute.</summary>
		/// <param name="shaderLocation">The shader's <c>@location</c> index.</param>
		/// <param name="format">Element format.</param>
		/// <param name="offset">Byte offset of this attribute within the vertex.</param>
		public VertexAttribute(uint shaderLocation, VertexFormat format, uint offset)
		{
			this.ShaderLocation = shaderLocation;
			this.Format = format;
			this.Offset = offset;
		}

		/// <summary>The shader's <c>@location</c> index.</summary>
		public uint ShaderLocation { get; }

		/// <summary>Element format.</summary>
		public VertexFormat Format { get; }

		/// <summary>Byte offset of this attribute within the vertex.</summary>
		public uint Offset { get; }

		/// <inheritdoc/>
		public bool Equals(VertexAttribute other)
			=> this.ShaderLocation == other.ShaderLocation && this.Format == other.Format && this.Offset == other.Offset;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is VertexAttribute other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.ShaderLocation, this.Format, this.Offset);

		/// <inheritdoc/>
		public override string ToString() => $"@{this.ShaderLocation} {this.Format}+{this.Offset}";
	}

	/// <summary>
	/// The layout of one vertex buffer slot (<c>WGPUVertexBufferLayout</c>): how far apart vertices
	/// are, whether the buffer steps per vertex or per instance, and what it feeds.
	/// </summary>
	public readonly struct VertexBufferLayout : IEquatable<VertexBufferLayout>
	{
		private readonly VertexAttribute[] attributes;

		/// <summary>Creates a vertex buffer layout.</summary>
		/// <param name="arrayStride">Bytes from one vertex to the next.</param>
		/// <param name="attributes">The attributes read out of this buffer.</param>
		/// <param name="stepMode">Whether the buffer advances per vertex or per instance.</param>
		public VertexBufferLayout(uint arrayStride, VertexAttribute[] attributes, VertexStepMode stepMode = VertexStepMode.Vertex)
		{
			this.ArrayStride = arrayStride;
			this.attributes = attributes ?? Array.Empty<VertexAttribute>();
			this.StepMode = stepMode;
		}

		/// <summary>Bytes from one vertex to the next.</summary>
		public uint ArrayStride { get; }

		/// <summary>Whether the buffer advances per vertex or per instance.</summary>
		public VertexStepMode StepMode { get; }

		/// <summary>The attributes read out of this buffer. Never null.</summary>
		public VertexAttribute[] Attributes => this.attributes ?? Array.Empty<VertexAttribute>();

		/// <inheritdoc/>
		public bool Equals(VertexBufferLayout other)
			=> this.ArrayStride == other.ArrayStride
			&& this.StepMode == other.StepMode
			&& DescriptorEquality.ArrayEquals(this.Attributes, other.Attributes);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is VertexBufferLayout other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(this.ArrayStride, this.StepMode, DescriptorEquality.ArrayHash(this.Attributes));

		/// <inheritdoc/>
		public override string ToString()
			=> $"stride {this.ArrayStride} {this.StepMode} [{string.Join(", ", this.Attributes)}]";
	}

	/// <summary>
	/// One half of a blend equation - color or alpha (<c>WGPUBlendComponent</c>). The factors are
	/// ignored when <see cref="Operation"/> is <see cref="BlendOperation.Min"/> or
	/// <see cref="BlendOperation.Max"/>.
	/// </summary>
	public readonly struct BlendComponent : IEquatable<BlendComponent>
	{
		/// <summary>Creates a blend component.</summary>
		/// <param name="operation">How the weighted source and destination combine.</param>
		/// <param name="sourceFactor">What the source is multiplied by.</param>
		/// <param name="destinationFactor">What the destination is multiplied by.</param>
		public BlendComponent(BlendOperation operation, BlendFactor sourceFactor, BlendFactor destinationFactor)
		{
			this.Operation = operation;
			this.SourceFactor = sourceFactor;
			this.DestinationFactor = destinationFactor;
		}

		/// <summary>Source replaces destination.</summary>
		public static BlendComponent Replace
			=> new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero);

		/// <summary>Classic non-premultiplied source-over alpha blending.</summary>
		public static BlendComponent AlphaBlend
			=> new BlendComponent(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha);

		/// <summary>How the weighted source and destination combine.</summary>
		public BlendOperation Operation { get; }

		/// <summary>What the source is multiplied by.</summary>
		public BlendFactor SourceFactor { get; }

		/// <summary>What the destination is multiplied by.</summary>
		public BlendFactor DestinationFactor { get; }

		/// <inheritdoc/>
		public bool Equals(BlendComponent other)
			=> this.Operation == other.Operation
			&& this.SourceFactor == other.SourceFactor
			&& this.DestinationFactor == other.DestinationFactor;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is BlendComponent other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Operation, this.SourceFactor, this.DestinationFactor);

		/// <inheritdoc/>
		public override string ToString() => $"{this.Operation}({this.SourceFactor}, {this.DestinationFactor})";
	}

	/// <summary>
	/// One color attachment a pipeline writes (<c>WGPUColorTargetState</c>): its format, its blending
	/// and its write mask. All three are immutable pipeline state in WebGPU, so changing any of them
	/// means a different pipeline object - which is exactly what the permutation caches key on.
	/// <para>
	/// Every argument but the format is optional, so <c>default(ColorTargetState)</c> has to describe
	/// the same target as <c>new ColorTargetState(format)</c> or the two would key different pipeline
	/// cache entries. The semantic default is "write every channel", which is not the zero value, so the
	/// mask is stored complemented: the zero field reads back as <see cref="ColorWriteMask.All"/> and a
	/// zero-initialized value is a fully writing target rather than an invisible one. The complement is
	/// an exact XOR round trip, so nothing else has to know.
	/// </para>
	/// </summary>
	public readonly struct ColorTargetState : IEquatable<ColorTargetState>
	{
		private readonly ColorWriteMask writeMaskComplement;

		/// <summary>Creates a color target state.</summary>
		/// <param name="format">The attachment's pixel format; must match the render pass.</param>
		/// <param name="blendEnabled">False writes the fragment output through unblended.</param>
		/// <param name="color">Color blend equation, used only when <paramref name="blendEnabled"/>.</param>
		/// <param name="alpha">Alpha blend equation, used only when <paramref name="blendEnabled"/>.</param>
		/// <param name="writeMask">Which channels are written. LCD text uses single-channel masks.</param>
		public ColorTargetState(
			TextureFormat format,
			bool blendEnabled = false,
			BlendComponent color = default,
			BlendComponent alpha = default,
			ColorWriteMask writeMask = ColorWriteMask.All)
		{
			this.Format = format;
			this.BlendEnabled = blendEnabled;
			this.Color = color;
			this.Alpha = alpha;
			this.writeMaskComplement = writeMask ^ ColorWriteMask.All;
		}

		/// <summary>The attachment's pixel format.</summary>
		public TextureFormat Format { get; }

		/// <summary>Whether blending is on. When false the blend components are ignored.</summary>
		public bool BlendEnabled { get; }

		/// <summary>Color blend equation.</summary>
		public BlendComponent Color { get; }

		/// <summary>Alpha blend equation.</summary>
		public BlendComponent Alpha { get; }

		/// <summary>Which channels are written. Stored complemented so zero-init means All.</summary>
		public ColorWriteMask WriteMask => this.writeMaskComplement ^ ColorWriteMask.All;

		/// <inheritdoc/>
		public bool Equals(ColorTargetState other)
			=> this.Format == other.Format
			&& this.BlendEnabled == other.BlendEnabled
			&& this.Color.Equals(other.Color)
			&& this.Alpha.Equals(other.Alpha)
			&& this.WriteMask == other.WriteMask;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is ColorTargetState other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(this.Format, this.BlendEnabled, this.Color, this.Alpha, this.WriteMask);

		/// <inheritdoc/>
		public override string ToString()
			=> $"{this.Format} mask {this.WriteMask}"
			+ (this.BlendEnabled ? $" blend color {this.Color} alpha {this.Alpha}" : " no blend");
	}

	/// <summary>
	/// Depth testing state (the depth half of <c>WGPUDepthStencilState</c>; there is no stencil here
	/// because nothing in the renderer uses one). A <see cref="Format"/> of
	/// <see cref="TextureFormat.Undefined"/> means the pipeline has no depth attachment at all.
	/// <para>
	/// The three bias fields default to zero in both the constructor and zero-init, matching
	/// <c>WGPU_DEPTH_STENCIL_STATE_INIT</c>, so they cannot split a pipeline cache entry the way a
	/// non-zero optional default would. <see cref="Format"/> is required precisely so nobody reaches
	/// for <c>new DepthStencilState()</c> and gets something that is not <see cref="None"/>.
	/// </para>
	/// </summary>
	public readonly struct DepthStencilState : IEquatable<DepthStencilState>
	{
		/// <summary>Creates depth state.</summary>
		/// <param name="format">Depth attachment format; must match the render pass.</param>
		/// <param name="depthWriteEnabled">Whether passing fragments update the depth buffer.</param>
		/// <param name="depthCompare">The depth test.</param>
		/// <param name="depthBias">Constant depth offset in minimum-representable-depth units.</param>
		/// <param name="depthBiasSlopeScale">Depth offset scaled by the polygon's depth slope.</param>
		/// <param name="depthBiasClamp">Magnitude limit on the total bias; 0 means no clamp.</param>
		public DepthStencilState(
			TextureFormat format,
			bool depthWriteEnabled = true,
			CompareFunction depthCompare = CompareFunction.Less,
			int depthBias = 0,
			float depthBiasSlopeScale = 0,
			float depthBiasClamp = 0)
		{
			this.Format = format;
			this.DepthWriteEnabled = depthWriteEnabled;
			this.DepthCompare = depthCompare;
			this.DepthBias = depthBias;
			this.DepthBiasSlopeScale = depthBiasSlopeScale;
			this.DepthBiasClamp = depthBiasClamp;
		}

		/// <summary>No depth attachment.</summary>
		public static DepthStencilState None => default;

		/// <summary>True when this pipeline actually has a depth attachment.</summary>
		public bool HasDepth => this.Format != TextureFormat.Undefined;

		/// <summary>Depth attachment format, or <see cref="TextureFormat.Undefined"/> for none.</summary>
		public TextureFormat Format { get; }

		/// <summary>Whether passing fragments update the depth buffer.</summary>
		public bool DepthWriteEnabled { get; }

		/// <summary>The depth test.</summary>
		public CompareFunction DepthCompare { get; }

		/// <summary>
		/// Constant depth offset, in units of the minimum representable depth value
		/// (<c>WGPUDepthStencilState.depthBias</c>). This is the integer half of GL's
		/// <c>glPolygonOffset</c> pair.
		/// </summary>
		public int DepthBias { get; }

		/// <summary>
		/// Depth offset scaled by the polygon's maximum depth slope
		/// (<c>WGPUDepthStencilState.depthBiasSlopeScale</c>) - GL's <c>glPolygonOffset</c> factor.
		/// </summary>
		public float DepthBiasSlopeScale { get; }

		/// <summary>
		/// Magnitude limit on the combined bias (<c>WGPUDepthStencilState.depthBiasClamp</c>). Zero,
		/// the webgpu default, means unclamped. GL has no equivalent, so the compat layer leaves it 0.
		/// </summary>
		public float DepthBiasClamp { get; }

		/// <summary>True when any of the three bias terms is non-zero.</summary>
		public bool HasDepthBias => this.DepthBias != 0 || this.DepthBiasSlopeScale != 0 || this.DepthBiasClamp != 0;

		/// <inheritdoc/>
		public bool Equals(DepthStencilState other)
			=> this.Format == other.Format
			&& this.DepthWriteEnabled == other.DepthWriteEnabled
			&& this.DepthCompare == other.DepthCompare
			&& this.DepthBias == other.DepthBias
			&& this.DepthBiasSlopeScale.Equals(other.DepthBiasSlopeScale)
			&& this.DepthBiasClamp.Equals(other.DepthBiasClamp);

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is DepthStencilState other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
			=> HashCode.Combine(
				this.Format,
				this.DepthWriteEnabled,
				this.DepthCompare,
				this.DepthBias,
				this.DepthBiasSlopeScale,
				this.DepthBiasClamp);

		/// <inheritdoc/>
		public override string ToString()
			=> this.HasDepth
				? $"{this.Format} {this.DepthCompare}{(this.DepthWriteEnabled ? " write" : " read only")}"
					+ (this.HasDepthBias ? $" bias {this.DepthBias}/{this.DepthBiasSlopeScale}/{this.DepthBiasClamp}" : string.Empty)
				: "no depth";
	}

	/// <summary>
	/// One slot of a bind group layout (<c>WGPUBindGroupLayoutEntry</c>), authored as data because
	/// WGSL cannot be reflected at runtime. <see cref="Group"/> is carried on the entry so a pipeline
	/// can declare every group in one flat array and still compare as a value.
	/// </summary>
	public readonly struct BindGroupLayoutEntry : IEquatable<BindGroupLayoutEntry>
	{
		/// <summary>Creates a bind group layout entry.</summary>
		/// <param name="group">The shader's <c>@group</c> index.</param>
		/// <param name="binding">The shader's <c>@binding</c> index within that group.</param>
		/// <param name="visibility">Which stages can see the binding.</param>
		/// <param name="type">What kind of resource the slot holds.</param>
		public BindGroupLayoutEntry(uint group, uint binding, ShaderStage visibility, BindingType type)
		{
			this.Group = group;
			this.Binding = binding;
			this.Visibility = visibility;
			this.Type = type;
		}

		/// <summary>The shader's <c>@group</c> index.</summary>
		public uint Group { get; }

		/// <summary>The shader's <c>@binding</c> index.</summary>
		public uint Binding { get; }

		/// <summary>Which stages can see the binding.</summary>
		public ShaderStage Visibility { get; }

		/// <summary>What kind of resource the slot holds.</summary>
		public BindingType Type { get; }

		/// <inheritdoc/>
		public bool Equals(BindGroupLayoutEntry other)
			=> this.Group == other.Group
			&& this.Binding == other.Binding
			&& this.Visibility == other.Visibility
			&& this.Type == other.Type;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is BindGroupLayoutEntry other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Group, this.Binding, this.Visibility, this.Type);

		/// <inheritdoc/>
		public override string ToString() => $"@group({this.Group}) @binding({this.Binding}) {this.Type} {this.Visibility}";
	}
}
