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

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// The names of the canned shaders the compat layer draws with, and the vertex and bind group
	/// layouts that go with them. Authored as data because WGSL cannot be reflected at runtime - this
	/// file plus <see cref="GlUniformBlock"/> is the whole contract between the compat layer and
	/// whatever supplies the shader text.
	/// <para>
	/// There are four modules and three entry points each, which is the "12 canned combos" the port
	/// plan counts: a vertex entry point plus a smooth and a flat fragment entry point. Flat shading is
	/// a separate <em>entry point</em> rather than a separate module because WGSL expresses it as
	/// <c>@interpolate(flat)</c> on the varying, and because the alternative - baking provoking-vertex
	/// colors into the vertex buffer - is what the classic D3D11 path already does on the CPU (see
	/// <see cref="GlImmediateModeBuffer.ColorIndexForFlatShading"/>) and we keep that too.
	/// </para>
	/// </summary>
	public static class GlShaderKeys
	{
		/// <summary>Unlit, per-vertex color. The 2D UI path's workhorse.</summary>
		public const string PositionColor = "PositionColor";

		/// <summary>Lit, per-vertex color.</summary>
		public const string PositionColorLit = "PositionColorLit";

		/// <summary>Unlit, textured and modulated by per-vertex color.</summary>
		public const string PositionTexture = "PositionTexture";

		/// <summary>Lit, textured and modulated by per-vertex color.</summary>
		public const string PositionTextureLit = "PositionTextureLit";

		/// <summary>The vertex entry point every canned module declares.</summary>
		public const string VertexEntryPoint = "vertexMain";

		/// <summary>The fragment entry point that interpolates the vertex color.</summary>
		public const string SmoothFragmentEntryPoint = "fragmentMain";

		/// <summary>The fragment entry point that takes the provoking vertex's color unchanged.</summary>
		public const string FlatFragmentEntryPoint = "fragmentMainFlat";

		/// <summary>The one bind group index the canned shaders use.</summary>
		public const uint BindGroupIndex = 0;

		/// <summary><c>@binding</c> of the uniform block described by <see cref="GlUniformBlock"/>.</summary>
		public const uint UniformBinding = 0;

		/// <summary><c>@binding</c> of the sampled texture, in the textured modules only.</summary>
		public const uint TextureBinding = 1;

		/// <summary><c>@binding</c> of the sampler, in the textured modules only.</summary>
		public const uint SamplerBinding = 2;

		private static readonly VertexBufferLayout ColoredLayout = new VertexBufferLayout(
			28,
			new[]
			{
				new VertexAttribute(0, VertexFormat.Float32x3, 0),
				new VertexAttribute(1, VertexFormat.Float32x4, 12),
			});

		private static readonly VertexBufferLayout TexturedLayout = new VertexBufferLayout(
			36,
			new[]
			{
				new VertexAttribute(0, VertexFormat.Float32x3, 0),
				new VertexAttribute(1, VertexFormat.Float32x2, 12),
				new VertexAttribute(2, VertexFormat.Float32x4, 20),
			});

		private static readonly VertexBufferLayout ColoredLitLayout = new VertexBufferLayout(
			40,
			new[]
			{
				new VertexAttribute(0, VertexFormat.Float32x3, 0),
				new VertexAttribute(1, VertexFormat.Float32x3, 12),
				new VertexAttribute(2, VertexFormat.Float32x4, 24),
			});

		private static readonly VertexBufferLayout TexturedLitLayout = new VertexBufferLayout(
			48,
			new[]
			{
				new VertexAttribute(0, VertexFormat.Float32x3, 0),
				new VertexAttribute(1, VertexFormat.Float32x3, 12),
				new VertexAttribute(2, VertexFormat.Float32x2, 24),
				new VertexAttribute(3, VertexFormat.Float32x4, 32),
			});

		private static readonly BindGroupLayoutEntry[] UntexturedBindings =
		{
			new BindGroupLayoutEntry(BindGroupIndex, UniformBinding, ShaderStage.Vertex | ShaderStage.Fragment, BindingType.UniformBuffer),
		};

		private static readonly BindGroupLayoutEntry[] TexturedBindings =
		{
			new BindGroupLayoutEntry(BindGroupIndex, UniformBinding, ShaderStage.Vertex | ShaderStage.Fragment, BindingType.UniformBuffer),
			new BindGroupLayoutEntry(BindGroupIndex, TextureBinding, ShaderStage.Fragment, BindingType.Texture),
			new BindGroupLayoutEntry(BindGroupIndex, SamplerBinding, ShaderStage.Fragment, BindingType.Sampler),
		};

		private static readonly string[] Modules =
		{
			PositionColor,
			PositionColorLit,
			PositionTexture,
			PositionTextureLit,
		};

		/// <summary>Every canned module key. A backend registers WGSL for exactly these.</summary>
		public static IReadOnlyList<string> AllModuleKeys => Modules;

		/// <summary>
		/// Position (float3) then color (float4), 28 bytes - the interleave
		/// <see cref="GlImmediateModeBuffer.BuildColoredVertices"/> writes.
		/// </summary>
		public static VertexBufferLayout ColoredVertexLayout => ColoredLayout;

		/// <summary>
		/// Position (float3), texture coordinate (float2) then color (float4), 36 bytes - the interleave
		/// <see cref="GlImmediateModeBuffer.BuildTexturedVertices"/> writes.
		/// </summary>
		public static VertexBufferLayout TexturedVertexLayout => TexturedLayout;

		/// <summary>Position, normal then color, 40 bytes. For the lit modules.</summary>
		public static VertexBufferLayout ColoredLitVertexLayout => ColoredLitLayout;

		/// <summary>Position, normal, texture coordinate then color, 48 bytes. For the lit modules.</summary>
		public static VertexBufferLayout TexturedLitVertexLayout => TexturedLitLayout;

		/// <summary>The bind group layout of the untextured modules: the uniform block alone.</summary>
		public static BindGroupLayoutEntry[] UntexturedBindGroupLayout => UntexturedBindings;

		/// <summary>The bind group layout of the textured modules: uniform block, texture, sampler.</summary>
		public static BindGroupLayoutEntry[] TexturedBindGroupLayout => TexturedBindings;

		/// <summary>Picks the module key for a draw.</summary>
		/// <param name="textured">True when a texture is bound and texture coordinates were supplied.</param>
		/// <param name="lit">True when fixed function lighting is on.</param>
		public static string ModuleKey(bool textured, bool lit)
		{
			if (textured)
			{
				return lit ? PositionTextureLit : PositionTexture;
			}

			return lit ? PositionColorLit : PositionColor;
		}

		/// <summary>Picks the fragment entry point for a draw.</summary>
		/// <param name="flatShading">True when <c>glShadeModel(GL_FLAT)</c> is in effect.</param>
		public static string FragmentEntryPoint(bool flatShading)
			=> flatShading ? FlatFragmentEntryPoint : SmoothFragmentEntryPoint;

		/// <summary>Picks the vertex buffer layout that matches a module.</summary>
		/// <param name="textured">True for the textured modules.</param>
		/// <param name="lit">True for the lit modules.</param>
		public static VertexBufferLayout VertexLayout(bool textured, bool lit)
		{
			if (textured)
			{
				return lit ? TexturedLitLayout : TexturedLayout;
			}

			return lit ? ColoredLitLayout : ColoredLayout;
		}

		/// <summary>Picks the bind group layout that matches a module.</summary>
		/// <param name="textured">True for the textured modules.</param>
		public static BindGroupLayoutEntry[] BindGroupLayout(bool textured)
			=> textured ? TexturedBindings : UntexturedBindings;
	}
}
