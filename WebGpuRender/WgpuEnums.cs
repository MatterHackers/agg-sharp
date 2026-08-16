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
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The single place RenderCore's vocabulary becomes webgpu.h's.
	/// <para>
	/// Two kinds of mapping live here, and the difference matters. The four flag enums
	/// (<see cref="BufferUsage"/>, <see cref="TextureUsage"/>, <see cref="ColorWriteMask"/>,
	/// <see cref="ShaderStage"/>) are documented in RenderCore as mirroring their WGPU counterparts
	/// bit for bit, so they are cast rather than switched - and the round-trip tests exist precisely
	/// so that claim cannot quietly stop being true. Everything else is an explicit switch, because
	/// the WGPU value enums reserve zero for <c>Undefined</c> and RenderCore's do not: a cast would
	/// turn every enum off by one, silently, in a way that renders <em>almost</em> correctly.
	/// </para>
	/// </summary>
	public static class WgpuEnums
	{
		/// <summary>Buffer usages, cast: the two enums share bit values.</summary>
		/// <param name="usage">The RenderCore usage flags.</param>
		public static WGPUBufferUsage ToWgpu(BufferUsage usage) => (WGPUBufferUsage)(ulong)usage;

		/// <summary>Buffer usages, cast back.</summary>
		/// <param name="usage">The WGPU usage flags.</param>
		public static BufferUsage ToRenderCore(WGPUBufferUsage usage) => (BufferUsage)(ulong)usage;

		/// <summary>Texture usages, cast: the two enums share bit values.</summary>
		/// <param name="usage">The RenderCore usage flags.</param>
		public static WGPUTextureUsage ToWgpu(TextureUsage usage) => (WGPUTextureUsage)(ulong)usage;

		/// <summary>Texture usages, cast back.</summary>
		/// <param name="usage">The WGPU usage flags.</param>
		public static TextureUsage ToRenderCore(WGPUTextureUsage usage) => (TextureUsage)(ulong)usage;

		/// <summary>Color write masks, cast: the two enums share bit values.</summary>
		/// <param name="mask">The RenderCore mask.</param>
		public static WGPUColorWriteMask ToWgpu(ColorWriteMask mask) => (WGPUColorWriteMask)(ulong)mask;

		/// <summary>Color write masks, cast back.</summary>
		/// <param name="mask">The WGPU mask.</param>
		public static ColorWriteMask ToRenderCore(WGPUColorWriteMask mask) => (ColorWriteMask)(ulong)mask;

		/// <summary>Shader stage visibility, cast: the two enums share bit values.</summary>
		/// <param name="stage">The RenderCore stages.</param>
		public static WGPUShaderStage ToWgpu(ShaderStage stage) => (WGPUShaderStage)(ulong)stage;

		/// <summary>Shader stage visibility, cast back.</summary>
		/// <param name="stage">The WGPU stages.</param>
		public static ShaderStage ToRenderCore(WGPUShaderStage stage) => (ShaderStage)(ulong)stage;

		/// <summary>Pixel formats. Only the formats RenderCore names are mappable.</summary>
		/// <param name="format">The RenderCore format.</param>
		/// <exception cref="ArgumentOutOfRangeException">The format has no WGPU counterpart.</exception>
		public static WGPUTextureFormat ToWgpu(TextureFormat format)
		{
			switch (format)
			{
				case TextureFormat.Undefined:
					return WGPUTextureFormat.Undefined;

				case TextureFormat.R8Unorm:
					return WGPUTextureFormat.R8Unorm;

				case TextureFormat.Rg8Unorm:
					return WGPUTextureFormat.RG8Unorm;

				case TextureFormat.Rgba8Unorm:
					return WGPUTextureFormat.RGBA8Unorm;

				case TextureFormat.Bgra8Unorm:
					return WGPUTextureFormat.BGRA8Unorm;

				case TextureFormat.Rg32Float:
					return WGPUTextureFormat.RG32Float;

				case TextureFormat.Rgba32Float:
					return WGPUTextureFormat.RGBA32Float;

				case TextureFormat.Rgba16Float:
					return WGPUTextureFormat.RGBA16Float;

				case TextureFormat.Depth32Float:
					return WGPUTextureFormat.Depth32Float;

				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, "No WGPU texture format for this value.");
			}
		}

		/// <summary>Pixel formats, back. Formats RenderCore does not name map to Undefined.</summary>
		/// <param name="format">The WGPU format.</param>
		public static TextureFormat ToRenderCore(WGPUTextureFormat format)
		{
			switch (format)
			{
				case WGPUTextureFormat.R8Unorm:
					return TextureFormat.R8Unorm;

				case WGPUTextureFormat.RG8Unorm:
					return TextureFormat.Rg8Unorm;

				case WGPUTextureFormat.RGBA8Unorm:
					return TextureFormat.Rgba8Unorm;

				case WGPUTextureFormat.BGRA8Unorm:
					return TextureFormat.Bgra8Unorm;

				case WGPUTextureFormat.RG32Float:
					return TextureFormat.Rg32Float;

				case WGPUTextureFormat.RGBA32Float:
					return TextureFormat.Rgba32Float;

				case WGPUTextureFormat.RGBA16Float:
					return TextureFormat.Rgba16Float;

				case WGPUTextureFormat.Depth32Float:
					return TextureFormat.Depth32Float;

				default:
					return TextureFormat.Undefined;
			}
		}

		/// <summary>Blend factors.</summary>
		/// <param name="factor">The RenderCore factor.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown factor.</exception>
		public static WGPUBlendFactor ToWgpu(BlendFactor factor)
		{
			switch (factor)
			{
				case BlendFactor.Zero:
					return WGPUBlendFactor.Zero;

				case BlendFactor.One:
					return WGPUBlendFactor.One;

				case BlendFactor.Src:
					return WGPUBlendFactor.Src;

				case BlendFactor.OneMinusSrc:
					return WGPUBlendFactor.OneMinusSrc;

				case BlendFactor.SrcAlpha:
					return WGPUBlendFactor.SrcAlpha;

				case BlendFactor.OneMinusSrcAlpha:
					return WGPUBlendFactor.OneMinusSrcAlpha;

				case BlendFactor.Dst:
					return WGPUBlendFactor.Dst;

				case BlendFactor.OneMinusDst:
					return WGPUBlendFactor.OneMinusDst;

				case BlendFactor.DstAlpha:
					return WGPUBlendFactor.DstAlpha;

				case BlendFactor.OneMinusDstAlpha:
					return WGPUBlendFactor.OneMinusDstAlpha;

				case BlendFactor.SrcAlphaSaturated:
					return WGPUBlendFactor.SrcAlphaSaturated;

				default:
					throw new ArgumentOutOfRangeException(nameof(factor), factor, "No WGPU blend factor for this value.");
			}
		}

		/// <summary>Blend operations.</summary>
		/// <param name="operation">The RenderCore operation.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown operation.</exception>
		public static WGPUBlendOperation ToWgpu(BlendOperation operation)
		{
			switch (operation)
			{
				case BlendOperation.Add:
					return WGPUBlendOperation.Add;

				case BlendOperation.Subtract:
					return WGPUBlendOperation.Subtract;

				case BlendOperation.ReverseSubtract:
					return WGPUBlendOperation.ReverseSubtract;

				case BlendOperation.Min:
					return WGPUBlendOperation.Min;

				case BlendOperation.Max:
					return WGPUBlendOperation.Max;

				default:
					throw new ArgumentOutOfRangeException(nameof(operation), operation, "No WGPU blend operation for this value.");
			}
		}

		/// <summary>Depth comparisons.</summary>
		/// <param name="compare">The RenderCore comparison.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown comparison.</exception>
		public static WGPUCompareFunction ToWgpu(CompareFunction compare)
		{
			switch (compare)
			{
				case CompareFunction.Never:
					return WGPUCompareFunction.Never;

				case CompareFunction.Less:
					return WGPUCompareFunction.Less;

				case CompareFunction.Equal:
					return WGPUCompareFunction.Equal;

				case CompareFunction.LessEqual:
					return WGPUCompareFunction.LessEqual;

				case CompareFunction.Greater:
					return WGPUCompareFunction.Greater;

				case CompareFunction.NotEqual:
					return WGPUCompareFunction.NotEqual;

				case CompareFunction.GreaterEqual:
					return WGPUCompareFunction.GreaterEqual;

				case CompareFunction.Always:
					return WGPUCompareFunction.Always;

				default:
					throw new ArgumentOutOfRangeException(nameof(compare), compare, "No WGPU compare function for this value.");
			}
		}

		/// <summary>Primitive topologies.</summary>
		/// <param name="topology">The RenderCore topology.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown topology.</exception>
		public static WGPUPrimitiveTopology ToWgpu(PrimitiveTopology topology)
		{
			switch (topology)
			{
				case PrimitiveTopology.PointList:
					return WGPUPrimitiveTopology.PointList;

				case PrimitiveTopology.LineList:
					return WGPUPrimitiveTopology.LineList;

				case PrimitiveTopology.LineStrip:
					return WGPUPrimitiveTopology.LineStrip;

				case PrimitiveTopology.TriangleList:
					return WGPUPrimitiveTopology.TriangleList;

				case PrimitiveTopology.TriangleStrip:
					return WGPUPrimitiveTopology.TriangleStrip;

				default:
					throw new ArgumentOutOfRangeException(nameof(topology), topology, "No WGPU topology for this value.");
			}
		}

		/// <summary>Index element widths.</summary>
		/// <param name="format">The RenderCore index format.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown format.</exception>
		public static WGPUIndexFormat ToWgpu(IndexFormat format)
		{
			switch (format)
			{
				case IndexFormat.Uint16:
					return WGPUIndexFormat.Uint16;

				case IndexFormat.Uint32:
					return WGPUIndexFormat.Uint32;

				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, "No WGPU index format for this value.");
			}
		}

		/// <summary>Vertex attribute formats.</summary>
		/// <param name="format">The RenderCore vertex format.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown format.</exception>
		public static WGPUVertexFormat ToWgpu(VertexFormat format)
		{
			switch (format)
			{
				case VertexFormat.Float32:
					return WGPUVertexFormat.Float32;

				case VertexFormat.Float32x2:
					return WGPUVertexFormat.Float32x2;

				case VertexFormat.Float32x3:
					return WGPUVertexFormat.Float32x3;

				case VertexFormat.Float32x4:
					return WGPUVertexFormat.Float32x4;

				case VertexFormat.Unorm8x4:
					return WGPUVertexFormat.Unorm8x4;

				case VertexFormat.Uint32:
					return WGPUVertexFormat.Uint32;

				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, "No WGPU vertex format for this value.");
			}
		}

		/// <summary>Vertex step modes.</summary>
		/// <param name="stepMode">The RenderCore step mode.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown step mode.</exception>
		public static WGPUVertexStepMode ToWgpu(VertexStepMode stepMode)
		{
			switch (stepMode)
			{
				case VertexStepMode.Vertex:
					return WGPUVertexStepMode.Vertex;

				case VertexStepMode.Instance:
					return WGPUVertexStepMode.Instance;

				default:
					throw new ArgumentOutOfRangeException(nameof(stepMode), stepMode, "No WGPU step mode for this value.");
			}
		}

		/// <summary>Face culling.</summary>
		/// <param name="cullMode">The RenderCore cull mode.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown cull mode.</exception>
		public static WGPUCullMode ToWgpu(CullMode cullMode)
		{
			switch (cullMode)
			{
				case CullMode.None:
					return WGPUCullMode.None;

				case CullMode.Front:
					return WGPUCullMode.Front;

				case CullMode.Back:
					return WGPUCullMode.Back;

				default:
					throw new ArgumentOutOfRangeException(nameof(cullMode), cullMode, "No WGPU cull mode for this value.");
			}
		}

		/// <summary>Front face winding.</summary>
		/// <param name="frontFace">The RenderCore winding.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown winding.</exception>
		public static WGPUFrontFace ToWgpu(FrontFace frontFace)
		{
			switch (frontFace)
			{
				case FrontFace.Ccw:
					return WGPUFrontFace.CCW;

				case FrontFace.Cw:
					return WGPUFrontFace.CW;

				default:
					throw new ArgumentOutOfRangeException(nameof(frontFace), frontFace, "No WGPU front face for this value.");
			}
		}

		/// <summary>Magnification and minification filters.</summary>
		/// <param name="filter">The RenderCore filter.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown filter.</exception>
		public static WGPUFilterMode ToWgpu(FilterMode filter)
		{
			switch (filter)
			{
				case FilterMode.Nearest:
					return WGPUFilterMode.Nearest;

				case FilterMode.Linear:
					return WGPUFilterMode.Linear;

				default:
					throw new ArgumentOutOfRangeException(nameof(filter), filter, "No WGPU filter mode for this value.");
			}
		}

		/// <summary>
		/// Mip filters. WebGPU splits what GL calls one filter into a separate enum for the mip level
		/// step, so <see cref="FilterMode"/> maps into both.
		/// </summary>
		/// <param name="filter">The RenderCore filter.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown filter.</exception>
		public static WGPUMipmapFilterMode ToWgpuMipmap(FilterMode filter)
		{
			switch (filter)
			{
				case FilterMode.Nearest:
					return WGPUMipmapFilterMode.Nearest;

				case FilterMode.Linear:
					return WGPUMipmapFilterMode.Linear;

				default:
					throw new ArgumentOutOfRangeException(nameof(filter), filter, "No WGPU mipmap filter for this value.");
			}
		}

		/// <summary>Texture coordinate wrapping.</summary>
		/// <param name="addressMode">The RenderCore address mode.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown address mode.</exception>
		public static WGPUAddressMode ToWgpu(AddressMode addressMode)
		{
			switch (addressMode)
			{
				case AddressMode.ClampToEdge:
					return WGPUAddressMode.ClampToEdge;

				case AddressMode.Repeat:
					return WGPUAddressMode.Repeat;

				case AddressMode.MirrorRepeat:
					return WGPUAddressMode.MirrorRepeat;

				default:
					throw new ArgumentOutOfRangeException(nameof(addressMode), addressMode, "No WGPU address mode for this value.");
			}
		}

		/// <summary>Attachment load ops.</summary>
		/// <param name="loadOp">The RenderCore load op.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown load op.</exception>
		public static WGPULoadOp ToWgpu(LoadOp loadOp)
		{
			switch (loadOp)
			{
				case LoadOp.Load:
					return WGPULoadOp.Load;

				case LoadOp.Clear:
					return WGPULoadOp.Clear;

				default:
					throw new ArgumentOutOfRangeException(nameof(loadOp), loadOp, "No WGPU load op for this value.");
			}
		}

		/// <summary>Attachment store ops.</summary>
		/// <param name="storeOp">The RenderCore store op.</param>
		/// <exception cref="ArgumentOutOfRangeException">Unknown store op.</exception>
		public static WGPUStoreOp ToWgpu(StoreOp storeOp)
		{
			switch (storeOp)
			{
				case StoreOp.Store:
					return WGPUStoreOp.Store;

				case StoreOp.Discard:
					return WGPUStoreOp.Discard;

				default:
					throw new ArgumentOutOfRangeException(nameof(storeOp), storeOp, "No WGPU store op for this value.");
			}
		}
	}
}
