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
	/// Texture and render-target pixel formats. Deliberately a subset of WebGPU's
	/// <c>WGPUTextureFormat</c> - only the formats the renderer actually uses - but the names and
	/// meanings are WebGPU's, so a backend maps each one with a single switch arm.
	/// </summary>
	public enum TextureFormat
	{
		/// <summary>No format. Only valid where a format is optional (a pass with no depth attachment).</summary>
		Undefined = 0,

		/// <summary>Single 8-bit unsigned normalized channel. Glyph coverage masks.</summary>
		R8Unorm,

		/// <summary>Two 8-bit unsigned normalized channels.</summary>
		Rg8Unorm,

		/// <summary>Four 8-bit unsigned normalized channels in R, G, B, A order. agg's ImageBuffer order.</summary>
		Rgba8Unorm,

		/// <summary>Four 8-bit unsigned normalized channels in B, G, R, A order. The usual swapchain format on Windows.</summary>
		Bgra8Unorm,

		/// <summary>Two 32-bit floats. The dual depth peeling front/back accumulation targets need this renderable.</summary>
		Rg32Float,

		/// <summary>Four 32-bit floats.</summary>
		Rgba32Float,

		/// <summary>Four 16-bit floats. HDR-ish accumulation without the bandwidth of Rgba32Float.</summary>
		Rgba16Float,

		/// <summary>32-bit float depth, no stencil. The scene depth buffer.</summary>
		Depth32Float,
	}

	/// <summary>
	/// What a buffer may be used for. Mirrors <c>WGPUBufferUsage</c> bit-for-bit so a backend can cast.
	/// WebGPU rejects any use not declared at creation, so this is not a hint.
	/// </summary>
	[Flags]
	public enum BufferUsage
	{
		/// <summary>No usage declared.</summary>
		None = 0,

		/// <summary>May be mapped for CPU reads. Readback staging buffers only.</summary>
		MapRead = 0x0001,

		/// <summary>May be mapped for CPU writes.</summary>
		MapWrite = 0x0002,

		/// <summary>May be the source of a copy.</summary>
		CopySrc = 0x0004,

		/// <summary>May be the destination of a copy - required for <see cref="IRenderDevice.WriteBuffer"/>.</summary>
		CopyDst = 0x0008,

		/// <summary>May be bound as an index buffer.</summary>
		Index = 0x0010,

		/// <summary>May be bound as a vertex buffer.</summary>
		Vertex = 0x0020,

		/// <summary>May be bound as a uniform buffer.</summary>
		Uniform = 0x0040,

		/// <summary>May be bound as a storage buffer.</summary>
		Storage = 0x0080,

		/// <summary>May supply indirect draw arguments.</summary>
		Indirect = 0x0100,
	}

	/// <summary>
	/// What a texture may be used for. Mirrors <c>WGPUTextureUsage</c> bit-for-bit. As with buffers,
	/// WebGPU validates uses against this at draw time, so readback targets must declare
	/// <see cref="CopySrc"/> when they are created, not when they are read.
	/// </summary>
	[Flags]
	public enum TextureUsage
	{
		/// <summary>No usage declared.</summary>
		None = 0,

		/// <summary>May be the source of a copy - required to read the texture back.</summary>
		CopySrc = 0x0001,

		/// <summary>May be the destination of a copy - required for <see cref="IRenderDevice.WriteTexture"/>.</summary>
		CopyDst = 0x0002,

		/// <summary>May be sampled by a shader.</summary>
		TextureBinding = 0x0004,

		/// <summary>May be bound as a storage texture.</summary>
		StorageBinding = 0x0008,

		/// <summary>May be a color or depth attachment of a render pass.</summary>
		RenderAttachment = 0x0010,
	}

	/// <summary>Blend source/destination factors. Names and semantics are <c>WGPUBlendFactor</c>'s.</summary>
	public enum BlendFactor
	{
		/// <summary>0</summary>
		Zero = 0,

		/// <summary>1</summary>
		One,

		/// <summary>Source color.</summary>
		Src,

		/// <summary>1 - source color.</summary>
		OneMinusSrc,

		/// <summary>Source alpha.</summary>
		SrcAlpha,

		/// <summary>1 - source alpha.</summary>
		OneMinusSrcAlpha,

		/// <summary>Destination color.</summary>
		Dst,

		/// <summary>1 - destination color.</summary>
		OneMinusDst,

		/// <summary>Destination alpha.</summary>
		DstAlpha,

		/// <summary>1 - destination alpha.</summary>
		OneMinusDstAlpha,

		/// <summary>min(source alpha, 1 - destination alpha).</summary>
		SrcAlphaSaturated,
	}

	/// <summary>
	/// How the weighted source and destination are combined. Names and semantics are
	/// <c>WGPUBlendOperation</c>'s. <see cref="Min"/> and <see cref="Max"/> are here because dual depth
	/// peeling is written in terms of MAX blending, not because anything else uses them.
	/// </summary>
	public enum BlendOperation
	{
		/// <summary>src * srcFactor + dst * dstFactor</summary>
		Add = 0,

		/// <summary>src * srcFactor - dst * dstFactor</summary>
		Subtract,

		/// <summary>dst * dstFactor - src * srcFactor</summary>
		ReverseSubtract,

		/// <summary>min(src, dst). Factors are ignored.</summary>
		Min,

		/// <summary>max(src, dst). Factors are ignored - this is the depth peeling formulation.</summary>
		Max,
	}

	/// <summary>Depth test comparison. Names and semantics are <c>WGPUCompareFunction</c>'s.</summary>
	public enum CompareFunction
	{
		/// <summary>The test never passes.</summary>
		Never = 0,

		/// <summary>Passes when the new value is less than the stored one.</summary>
		Less,

		/// <summary>Passes when the values are equal.</summary>
		Equal,

		/// <summary>Passes when the new value is less than or equal to the stored one.</summary>
		LessEqual,

		/// <summary>Passes when the new value is greater than the stored one.</summary>
		Greater,

		/// <summary>Passes when the values differ.</summary>
		NotEqual,

		/// <summary>Passes when the new value is greater than or equal to the stored one.</summary>
		GreaterEqual,

		/// <summary>The test always passes.</summary>
		Always,
	}

	/// <summary>
	/// How vertices are assembled into primitives. Mirrors <c>WGPUPrimitiveTopology</c>. Note the
	/// absence of quads and polygons - the immediate-mode compat layer converts those itself.
	/// </summary>
	public enum PrimitiveTopology
	{
		/// <summary>One point per vertex.</summary>
		PointList = 0,

		/// <summary>One line per vertex pair.</summary>
		LineList,

		/// <summary>A connected polyline.</summary>
		LineStrip,

		/// <summary>One triangle per vertex triple.</summary>
		TriangleList,

		/// <summary>A connected triangle strip.</summary>
		TriangleStrip,
	}

	/// <summary>Index buffer element width. Mirrors <c>WGPUIndexFormat</c>.</summary>
	public enum IndexFormat
	{
		/// <summary>16-bit unsigned indices.</summary>
		Uint16 = 0,

		/// <summary>32-bit unsigned indices.</summary>
		Uint32,
	}

	/// <summary>
	/// Per-attribute vertex element format. A subset of <c>WGPUVertexFormat</c> covering what the
	/// canned pipelines feed in: positions, normals, texture coordinates and packed colors.
	/// </summary>
	public enum VertexFormat
	{
		/// <summary>One 32-bit float.</summary>
		Float32 = 0,

		/// <summary>Two 32-bit floats.</summary>
		Float32x2,

		/// <summary>Three 32-bit floats.</summary>
		Float32x3,

		/// <summary>Four 32-bit floats.</summary>
		Float32x4,

		/// <summary>Four bytes normalized to 0..1 - a packed RGBA color.</summary>
		Unorm8x4,

		/// <summary>One 32-bit unsigned integer.</summary>
		Uint32,
	}

	/// <summary>Whether a vertex buffer advances per vertex or per instance. Mirrors <c>WGPUVertexStepMode</c>.</summary>
	public enum VertexStepMode
	{
		/// <summary>Advance once per vertex.</summary>
		Vertex = 0,

		/// <summary>Advance once per instance.</summary>
		Instance,
	}

	/// <summary>Which triangle faces are discarded. Mirrors <c>WGPUCullMode</c>.</summary>
	public enum CullMode
	{
		/// <summary>Nothing is culled.</summary>
		None = 0,

		/// <summary>Front faces are discarded.</summary>
		Front,

		/// <summary>Back faces are discarded.</summary>
		Back,
	}

	/// <summary>Which winding is the front face. Mirrors <c>WGPUFrontFace</c>.</summary>
	public enum FrontFace
	{
		/// <summary>Counter-clockwise winding is front facing.</summary>
		Ccw = 0,

		/// <summary>Clockwise winding is front facing.</summary>
		Cw,
	}

	/// <summary>Sampler minification/magnification filter. Mirrors <c>WGPUFilterMode</c>.</summary>
	public enum FilterMode
	{
		/// <summary>Nearest texel.</summary>
		Nearest = 0,

		/// <summary>Linear interpolation between texels.</summary>
		Linear,
	}

	/// <summary>Sampler wrap behavior outside 0..1. Mirrors <c>WGPUAddressMode</c>.</summary>
	public enum AddressMode
	{
		/// <summary>Coordinates clamp to the edge texel.</summary>
		ClampToEdge = 0,

		/// <summary>Coordinates wrap.</summary>
		Repeat,

		/// <summary>Coordinates wrap, mirrored on alternate repeats.</summary>
		MirrorRepeat,
	}

	/// <summary>What a render pass does with an attachment's existing contents. Mirrors <c>WGPULoadOp</c>.</summary>
	public enum LoadOp
	{
		/// <summary>Keep what is already in the attachment. This is what a re-opened pass uses.</summary>
		Load = 0,

		/// <summary>Replace the contents with the attachment's clear value.</summary>
		Clear,
	}

	/// <summary>What a render pass does with an attachment when it ends. Mirrors <c>WGPUStoreOp</c>.</summary>
	public enum StoreOp
	{
		/// <summary>Write the results out.</summary>
		Store = 0,

		/// <summary>Throw the results away (a depth buffer nobody reads afterwards).</summary>
		Discard,
	}

	/// <summary>
	/// Which color channels a pipeline writes. Mirrors <c>WGPUColorWriteMask</c>. In WebGPU this is
	/// baked into the pipeline rather than settable as dynamic state, which is why LCD subpixel text
	/// needs one pre-created pipeline permutation per channel instead of three ColorMask calls.
	/// </summary>
	[Flags]
	public enum ColorWriteMask
	{
		/// <summary>Writes nothing.</summary>
		None = 0,

		/// <summary>Writes red.</summary>
		Red = 0x1,

		/// <summary>Writes green.</summary>
		Green = 0x2,

		/// <summary>Writes blue.</summary>
		Blue = 0x4,

		/// <summary>Writes alpha.</summary>
		Alpha = 0x8,

		/// <summary>Writes every channel.</summary>
		All = Red | Green | Blue | Alpha,
	}

	/// <summary>Which shader stages can see a binding. Mirrors <c>WGPUShaderStage</c>.</summary>
	[Flags]
	public enum ShaderStage
	{
		/// <summary>No stage.</summary>
		None = 0,

		/// <summary>The vertex stage.</summary>
		Vertex = 0x1,

		/// <summary>The fragment stage.</summary>
		Fragment = 0x2,

		/// <summary>The compute stage.</summary>
		Compute = 0x4,
	}

	/// <summary>
	/// The kind of resource a bind group slot holds. WGSL has no runtime reflection, so bind group
	/// layouts are authored as data (this enum) rather than discovered from the shader - that is the
	/// replacement for D3D11's <c>ReflectUniforms</c>.
	/// </summary>
	public enum BindingType
	{
		/// <summary>A uniform buffer.</summary>
		UniformBuffer = 0,

		/// <summary>A read-write storage buffer.</summary>
		StorageBuffer,

		/// <summary>A read-only storage buffer.</summary>
		ReadOnlyStorageBuffer,

		/// <summary>A sampler.</summary>
		Sampler,

		/// <summary>A sampled color texture.</summary>
		Texture,

		/// <summary>A sampled depth texture (depth textures have their own binding type in WebGPU).</summary>
		DepthTexture,
	}
}
