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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Descriptors are the keys of the pipeline and bind group caches, so their equality is not a
	/// nicety: a descriptor that compares by array reference misses every cache hit and creates a new
	/// pipeline per draw, and one that ignores a field it should not returns the wrong pipeline.
	/// These tests pin both directions.
	/// </summary>
	public class DescriptorEqualityTests
	{
		[Test]
		public async Task IdenticalTextureDescriptorsAreEqualAndHashAlike()
		{
			var first = new TextureDescriptor(64, 32, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst);
			var second = new TextureDescriptor(64, 32, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst);

			await Assert.That(first).IsEqualTo(second);
			await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
		}

		[Test]
		public async Task TextureDescriptorLabelIsNotPartOfIdentity()
		{
			// A debug name must not fragment a cache - two identically shaped textures are the same key.
			var unnamed = new TextureDescriptor(16, 16, TextureFormat.R8Unorm, TextureUsage.TextureBinding);
			var named = new TextureDescriptor(16, 16, TextureFormat.R8Unorm, TextureUsage.TextureBinding, label: "glyph atlas");

			await Assert.That(unnamed).IsEqualTo(named);
			await Assert.That(unnamed.GetHashCode()).IsEqualTo(named.GetHashCode());
		}

		[Test]
		public async Task TextureDescriptorsDifferingInAnyFieldAreNotEqual()
		{
			var baseline = new TextureDescriptor(64, 32, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding);

			await Assert.That(baseline).IsNotEqualTo(new TextureDescriptor(65, 32, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding));
			await Assert.That(baseline).IsNotEqualTo(new TextureDescriptor(64, 33, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding));
			await Assert.That(baseline).IsNotEqualTo(new TextureDescriptor(64, 32, TextureFormat.Bgra8Unorm, TextureUsage.TextureBinding));
			await Assert.That(baseline).IsNotEqualTo(new TextureDescriptor(64, 32, TextureFormat.Rgba8Unorm, TextureUsage.RenderAttachment));
			await Assert.That(baseline).IsNotEqualTo(new TextureDescriptor(64, 32, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding, mipLevelCount: 4));
			await Assert.That(baseline).IsNotEqualTo(new TextureDescriptor(64, 32, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding, sampleCount: 4));
		}

		[Test]
		public async Task SamplerDescriptorsCompareByValue()
		{
			var linearClamp = SamplerDescriptor.LinearClamp;
			var alsoLinearClamp = new SamplerDescriptor(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear);
			var repeatNearest = new SamplerDescriptor(AddressMode.Repeat, AddressMode.Repeat, FilterMode.Nearest, FilterMode.Nearest);

			await Assert.That(linearClamp).IsEqualTo(alsoLinearClamp);
			await Assert.That(linearClamp.GetHashCode()).IsEqualTo(alsoLinearClamp.GetHashCode());
			await Assert.That(linearClamp).IsNotEqualTo(repeatNearest);
		}

		[Test]
		public async Task AZeroInitializedSamplerDescriptorMatchesItsConstructedDefaults()
		{
			// A struct's optional constructor arguments are not what zero-init produces, so a descriptor
			// whose arguments are all optional has to have defaults that agree with default(T) - or the
			// same sampler ends up in a cache under two different keys.
			await Assert.That(default(SamplerDescriptor)).IsEqualTo(new SamplerDescriptor());
			await Assert.That(default(SamplerDescriptor)).IsEqualTo(SamplerDescriptor.NearestClamp);
		}

		[Test]
		public async Task AZeroInitializedDepthStencilStateMatchesNoneAndBiasesToZero()
		{
			// Same hazard as SamplerDescriptor: a struct's optional constructor defaults are not what
			// zero-init produces. The depth bias trio has to default to 0 in both, matching
			// WGPU_DEPTH_STENCIL_STATE_INIT, or the same pipeline lands in the cache under two keys.
			await Assert.That(default(DepthStencilState)).IsEqualTo(new DepthStencilState());
			await Assert.That(default(DepthStencilState)).IsEqualTo(DepthStencilState.None);
			await Assert.That(default(DepthStencilState).HasDepth).IsFalse();

			var depth = new DepthStencilState(TextureFormat.Depth32Float);
			await Assert.That(depth.DepthBias).IsEqualTo(0);
			await Assert.That(depth.DepthBiasSlopeScale).IsEqualTo(0f);
			await Assert.That(depth.DepthBiasClamp).IsEqualTo(0f);
			await Assert.That(depth.HasDepthBias).IsFalse();
			await Assert.That(depth).IsEqualTo(new DepthStencilState(TextureFormat.Depth32Float, true, CompareFunction.Less, 0, 0, 0));
			await Assert.That(depth.GetHashCode())
				.IsEqualTo(new DepthStencilState(TextureFormat.Depth32Float, true, CompareFunction.Less, 0, 0, 0).GetHashCode());
		}

		[Test]
		public async Task AZeroInitializedColorTargetStateWritesEveryChannel()
		{
			// Same hazard again, with a nastier failure: the semantic default is "write everything", which
			// is not the zero value, so a naively stored mask would make default(ColorTargetState) a target
			// that writes nothing - a pipeline that draws invisibly, and a second cache key for what is
			// meant to be the same state. The mask is stored complemented to make the two agree.
			await Assert.That(default(ColorTargetState).WriteMask).IsEqualTo(ColorWriteMask.All);
			await Assert.That(default(ColorTargetState)).IsEqualTo(new ColorTargetState(TextureFormat.Undefined));
			await Assert.That(default(ColorTargetState).GetHashCode())
				.IsEqualTo(new ColorTargetState(TextureFormat.Undefined).GetHashCode());

			// And the complement is a real round trip, not just a special case for All.
			foreach (var mask in new[] { ColorWriteMask.None, ColorWriteMask.Red, ColorWriteMask.Green, ColorWriteMask.Blue, ColorWriteMask.Alpha, ColorWriteMask.All })
			{
				var target = new ColorTargetState(TextureFormat.Bgra8Unorm, writeMask: mask);
				await Assert.That(target.WriteMask).IsEqualTo(mask);
				await Assert.That(target).IsEqualTo(new ColorTargetState(TextureFormat.Bgra8Unorm, writeMask: mask));
			}

			await Assert.That(new ColorTargetState(TextureFormat.Bgra8Unorm, writeMask: ColorWriteMask.Red))
				.IsNotEqualTo(new ColorTargetState(TextureFormat.Bgra8Unorm, writeMask: ColorWriteMask.Green));
		}

		[Test]
		public async Task AZeroInitializedDepthAttachmentMatchesItsConstructedDefaults()
		{
			// The clear value defaults to zero rather than to the far plane so the two agree, which is
			// also what WGPURenderPassDepthStencilAttachment's own zero-init means. Anything that actually
			// clears depth asks for DepthAttachment.FarClear by name.
			await Assert.That(default(DepthAttachment)).IsEqualTo(new DepthAttachment(null));
			await Assert.That(default(DepthAttachment)).IsEqualTo(DepthAttachment.None);
			await Assert.That(default(DepthAttachment).ClearValue).IsEqualTo(0f);
			await Assert.That(DepthAttachment.FarClear).IsEqualTo(1.0f);

			var texture = new RecordingRenderDevice().CreateTexture(
				new TextureDescriptor(4, 4, TextureFormat.Depth32Float, TextureUsage.RenderAttachment));
			await Assert.That(new DepthAttachment(texture, LoadOp.Clear, DepthAttachment.FarClear).ClearValue).IsEqualTo(1.0f);
		}

		[Test]
		public async Task DepthStencilStatesDifferingOnlyInBiasAreDistinctKeys()
		{
			// The whole point of putting glPolygonOffset here: a biased draw has to reach a different
			// pipeline than the unbiased one, or the coplanar overlay z-fights anyway.
			var baseline = new DepthStencilState(TextureFormat.Depth32Float);

			await Assert.That(baseline).IsNotEqualTo(new DepthStencilState(TextureFormat.Depth32Float, depthBias: 1));
			await Assert.That(baseline).IsNotEqualTo(new DepthStencilState(TextureFormat.Depth32Float, depthBiasSlopeScale: 1));
			await Assert.That(baseline).IsNotEqualTo(new DepthStencilState(TextureFormat.Depth32Float, depthBiasClamp: 1));

			var biased = new DepthStencilState(TextureFormat.Depth32Float, depthBias: 1, depthBiasSlopeScale: 1);
			var alsoBiased = new DepthStencilState(TextureFormat.Depth32Float, depthBias: 1, depthBiasSlopeScale: 1);
			await Assert.That(biased).IsEqualTo(alsoBiased);
			await Assert.That(biased.GetHashCode()).IsEqualTo(alsoBiased.GetHashCode());
		}

		[Test]
		public async Task PipelineDescriptorsWithSeparateButEqualArraysAreEqual()
		{
			// The reason this matters: the caller builds fresh attribute arrays on every draw. If array
			// identity leaked into equality, the pipeline cache would never hit.
			var device = new RecordingRenderDevice();
			var shader = device.CreateShaderModule("PositionColor");

			var first = MakePipeline(shader, ColorWriteMask.All);
			var second = MakePipeline(shader, ColorWriteMask.All);

			await Assert.That(first).IsEqualTo(second);
			await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());

			// And the permutation that LCD text depends on must be a distinct key.
			var redOnly = MakePipeline(shader, ColorWriteMask.Red);
			await Assert.That(first).IsNotEqualTo(redOnly);
		}

		[Test]
		public async Task PipelineDescriptorsAreUsableAsDictionaryKeys()
		{
			var device = new RecordingRenderDevice();
			var shader = device.CreateShaderModule("PositionColor");
			var cache = new Dictionary<RenderPipelineDescriptor, string>();

			cache[MakePipeline(shader, ColorWriteMask.All)] = "all";
			cache[MakePipeline(shader, ColorWriteMask.Red)] = "red";
			cache[MakePipeline(shader, ColorWriteMask.All)] = "all again";

			await Assert.That(cache.Count).IsEqualTo(2);
			await Assert.That(cache[MakePipeline(shader, ColorWriteMask.All)]).IsEqualTo("all again");
		}

		[Test]
		public async Task PipelineDescriptorsWithDifferentShaderModulesAreNotEqual()
		{
			// Shader modules are compared by reference: each one is created once and reused.
			var device = new RecordingRenderDevice();
			var first = MakePipeline(device.CreateShaderModule("PositionColor"), ColorWriteMask.All);
			var second = MakePipeline(device.CreateShaderModule("PositionColor"), ColorWriteMask.All);

			await Assert.That(first).IsNotEqualTo(second);
		}

		[Test]
		public async Task BindGroupDescriptorsCompareEntriesByValueAndResourcesByReference()
		{
			var device = new RecordingRenderDevice();
			var shader = device.CreateShaderModule("PositionTexture");
			var pipeline = device.CreateRenderPipeline(MakePipeline(shader, ColorWriteMask.All));
			var uniforms = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, 64);
			var otherUniforms = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, 64);

			var first = new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, uniforms) });
			var second = new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, uniforms) }, "debug name");
			var differentBuffer = new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, otherUniforms) });
			var differentGroup = new BindGroupDescriptor(pipeline, 1, new[] { BindGroupEntry.ForBuffer(0, uniforms) });

			await Assert.That(first).IsEqualTo(second);
			await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
			await Assert.That(first).IsNotEqualTo(differentBuffer);
			await Assert.That(first).IsNotEqualTo(differentGroup);
		}

		private static RenderPipelineDescriptor MakePipeline(IShaderModule shader, ColorWriteMask writeMask)
		{
			var attributes = new[]
			{
				new VertexAttribute(0, VertexFormat.Float32x3, 0),
				new VertexAttribute(1, VertexFormat.Unorm8x4, 12),
			};

			return new RenderPipelineDescriptor(
				shader,
				"VertexMain",
				shader,
				"FragmentMain",
				new[] { new VertexBufferLayout(16, attributes) },
				new[]
				{
					new ColorTargetState(
						TextureFormat.Bgra8Unorm,
						blendEnabled: true,
						color: BlendComponent.AlphaBlend,
						alpha: BlendComponent.AlphaBlend,
						writeMask: writeMask),
				},
				new[] { new BindGroupLayoutEntry(0, 0, ShaderStage.Vertex, BindingType.UniformBuffer) },
				new DepthStencilState(TextureFormat.Depth32Float));
		}
	}
}
