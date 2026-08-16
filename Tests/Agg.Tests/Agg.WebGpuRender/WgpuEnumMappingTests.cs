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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The enum table and the shader contract - the two places where a silent, invisible mistake is
	/// possible and no GPU is needed to catch it.
	/// <para>
	/// RenderCore documents four of its enums as mirroring their WGPU counterparts bit for bit, and
	/// <see cref="WgpuEnums"/> takes that at its word by casting instead of switching. These tests are
	/// what makes that claim load bearing: if a future header revision renumbers a usage bit, the cast
	/// would keep compiling and start corrupting every resource creation.
	/// </para>
	/// </summary>
	public class WgpuEnumMappingTests
	{
		[Test]
		public async Task TheCastFlagEnumsRoundTripValueForValue()
		{
			foreach (BufferUsage usage in Enum.GetValues<BufferUsage>())
			{
				await Assert.That(WgpuEnums.ToRenderCore(WgpuEnums.ToWgpu(usage))).IsEqualTo(usage);
				await Assert.That((ulong)WgpuEnums.ToWgpu(usage)).IsEqualTo((ulong)usage);
			}

			foreach (TextureUsage usage in Enum.GetValues<TextureUsage>())
			{
				await Assert.That(WgpuEnums.ToRenderCore(WgpuEnums.ToWgpu(usage))).IsEqualTo(usage);
				await Assert.That((ulong)WgpuEnums.ToWgpu(usage)).IsEqualTo((ulong)usage);
			}

			foreach (ColorWriteMask mask in Enum.GetValues<ColorWriteMask>())
			{
				await Assert.That(WgpuEnums.ToRenderCore(WgpuEnums.ToWgpu(mask))).IsEqualTo(mask);
				await Assert.That((ulong)WgpuEnums.ToWgpu(mask)).IsEqualTo((ulong)mask);
			}

			foreach (ShaderStage stage in Enum.GetValues<ShaderStage>())
			{
				await Assert.That(WgpuEnums.ToRenderCore(WgpuEnums.ToWgpu(stage))).IsEqualTo(stage);
				await Assert.That((ulong)WgpuEnums.ToWgpu(stage)).IsEqualTo((ulong)stage);
			}
		}

		[Test]
		public async Task NamedFlagsCombineTheWayTheResourceCreationPathAssumes()
		{
			// The combination the compat layer's vertex buffers actually use, and the one the port plan
			// singles out as a thing the device must accept.
			var vertexUsage = WgpuEnums.ToWgpu(BufferUsage.Vertex | BufferUsage.CopyDst);
			await Assert.That(vertexUsage).IsEqualTo(WGPUBufferUsage.Vertex | WGPUBufferUsage.CopyDst);

			var readback = WgpuEnums.ToWgpu(BufferUsage.CopyDst | BufferUsage.MapRead);
			await Assert.That(readback).IsEqualTo(WGPUBufferUsage.CopyDst | WGPUBufferUsage.MapRead);

			var target = WgpuEnums.ToWgpu(TextureUsage.RenderAttachment | TextureUsage.CopySrc);
			await Assert.That(target).IsEqualTo(WGPUTextureUsage.RenderAttachment | WGPUTextureUsage.CopySrc);

			await Assert.That(WgpuEnums.ToWgpu(ColorWriteMask.All)).IsEqualTo(WGPUColorWriteMask.All);
			await Assert.That(WgpuEnums.ToWgpu(ShaderStage.Vertex | ShaderStage.Fragment))
				.IsEqualTo(WGPUShaderStage.Vertex | WGPUShaderStage.Fragment);
		}

		[Test]
		public async Task TheSwitchedEnumsMapEveryValueAndNoneOfThemToUndefined()
		{
			// These enums cannot be cast: WGPU reserves zero for Undefined and RenderCore uses zero for a
			// real value, so every one of them is off by one. A mapping that produced Undefined would be
			// accepted by wgpu (it substitutes a default) and render almost right, which is the worst
			// possible failure mode.
			await AssertNotUndefined<TextureFormat, WGPUTextureFormat>(
				value => WgpuEnums.ToWgpu(value),
				TextureFormat.Undefined,
				WGPUTextureFormat.Undefined);

			await AssertNotUndefined<BlendFactor, WGPUBlendFactor>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUBlendFactor.Undefined);

			await AssertNotUndefined<BlendOperation, WGPUBlendOperation>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUBlendOperation.Undefined);

			await AssertNotUndefined<CompareFunction, WGPUCompareFunction>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUCompareFunction.Undefined);

			await AssertNotUndefined<PrimitiveTopology, WGPUPrimitiveTopology>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUPrimitiveTopology.Undefined);

			await AssertNotUndefined<IndexFormat, WGPUIndexFormat>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUIndexFormat.Undefined);

			await AssertNotUndefined<VertexStepMode, WGPUVertexStepMode>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUVertexStepMode.Undefined);

			await AssertNotUndefined<CullMode, WGPUCullMode>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUCullMode.Undefined);

			await AssertNotUndefined<FrontFace, WGPUFrontFace>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUFrontFace.Undefined);

			await AssertNotUndefined<FilterMode, WGPUFilterMode>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUFilterMode.Undefined);

			await AssertNotUndefined<AddressMode, WGPUAddressMode>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUAddressMode.Undefined);

			await AssertNotUndefined<LoadOp, WGPULoadOp>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPULoadOp.Undefined);

			await AssertNotUndefined<StoreOp, WGPUStoreOp>(
				value => WgpuEnums.ToWgpu(value),
				null,
				WGPUStoreOp.Undefined);

			// The one enum that is genuinely a subset of a much larger WGPU enum, so it is checked by
			// round trip rather than by exhaustion of the WGPU side.
			foreach (TextureFormat format in Enum.GetValues<TextureFormat>())
			{
				await Assert.That(WgpuEnums.ToRenderCore(WgpuEnums.ToWgpu(format))).IsEqualTo(format);
			}

			// Vertex formats have no reverse mapping (nothing needs one), so they are checked for
			// distinctness only.
			var vertexFormats = Enum.GetValues<VertexFormat>().Select(value => WgpuEnums.ToWgpu(value)).ToList();
			await Assert.That(vertexFormats.Distinct().Count()).IsEqualTo(vertexFormats.Count);
			await Assert.That(vertexFormats.Contains(WGPUVertexFormat.Float32x3)).IsTrue();
		}

		[Test]
		public async Task TheBackendServesExactlyTheShaderKeysTheCompatLayerAsksFor()
		{
			// WebGpuRender cannot reference RenderGl (that would point the backend at the layer above it),
			// so the key strings are duplicated as literals. This is the test that keeps the duplicate
			// honest, and the one place the two lists are ever compared.
			await Assert.That(WgslShaderSources.AllModuleKeys.OrderBy(key => key, StringComparer.Ordinal).ToList())
				.IsEquivalentTo(GlShaderKeys.AllModuleKeys.OrderBy(key => key, StringComparer.Ordinal).ToList());

			var provider = new WgslShaderSources();
			foreach (string key in GlShaderKeys.AllModuleKeys)
			{
				string source = provider.TryGetSource(key);
				await Assert.That(source).IsNotNull();

				// Each module declares one vertex entry point and both fragment entry points; that is the
				// "12 canned combos" the port plan counts, and a missing one only shows up as a pipeline
				// creation failure much later.
				await Assert.That(source.Contains("fn " + GlShaderKeys.VertexEntryPoint)).IsTrue();
				await Assert.That(source.Contains("fn " + GlShaderKeys.SmoothFragmentEntryPoint)).IsTrue();
				await Assert.That(source.Contains("fn " + GlShaderKeys.FlatFragmentEntryPoint)).IsTrue();

				// Every member of the uniform block, by the name GlUniformBlock publishes it under. The
				// offsets themselves are proved by the rendering tests; this catches a renamed or dropped
				// member at unit-test speed.
				foreach (string member in GlUniformBlock.Offsets.Keys)
				{
					await Assert.That(source.Contains(member)).IsTrue();
				}
			}

			await Assert.That(provider.TryGetSource("NoSuchShader")).IsNull();
		}

		private static async Task AssertNotUndefined<TSource, TTarget>(
			Func<TSource, TTarget> map,
			TSource? skip,
			TTarget undefined)
			where TSource : struct, Enum
			where TTarget : struct, Enum
		{
			var mapped = new List<TTarget>();
			foreach (TSource value in Enum.GetValues<TSource>())
			{
				if (skip.HasValue && value.Equals(skip.Value))
				{
					continue;
				}

				TTarget target = map(value);
				await Assert.That(target).IsNotEqualTo(undefined);
				mapped.Add(target);
			}

			// Distinct as well as defined: two RenderCore values collapsing onto one WGPU value would be
			// just as invisible.
			await Assert.That(mapped.Distinct().Count()).IsEqualTo(mapped.Count);
		}
	}
}
