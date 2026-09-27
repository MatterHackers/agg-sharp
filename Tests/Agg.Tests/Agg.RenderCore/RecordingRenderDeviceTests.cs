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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The recording device is the harness every piece of retained rendering logic will be tested
	/// against, so its own recording has to be trustworthy: the right calls, in the right order, with
	/// the resources that were actually passed.
	/// </summary>
	public class RecordingRenderDeviceTests
	{
		[Test]
		public async Task AScriptedFrameRecordsItsCallsInOrder()
		{
			var device = new RecordingRenderDevice();
			var surface = new StubSurfaceTarget("window", 320, 240);

			var shader = device.CreateShaderModule("PositionColor");
			var vertices = device.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, 96);
			var uniforms = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, 64);
			var pipeline = device.CreateRenderPipeline(new RenderPipelineDescriptor(
				shader,
				"VertexMain",
				shader,
				"FragmentMain",
				new[] { new VertexBufferLayout(16, new[] { new VertexAttribute(0, VertexFormat.Float32x3, 0) }) },
				new[] { new ColorTargetState(TextureFormat.Bgra8Unorm) },
				new[] { new BindGroupLayoutEntry(0, 0, ShaderStage.Vertex, BindingType.UniformBuffer) }));
			var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, uniforms) }));

			device.WriteBuffer(uniforms, 0, new byte[64]);

			var backBuffer = surface.AcquireCurrentTexture();
			using (var pass = device.BeginRenderPass(new RenderPassDescriptor(backBuffer, LoadOp.Clear, ClearColor.Black)))
			{
				pass.SetPipeline(pipeline);
				pass.SetBindGroup(0, bindGroup);
				pass.SetVertexBuffer(0, vertices);
				pass.SetViewport(0, 0, 320, 240);
				pass.SetScissor(0, 0, 320, 240);
				pass.Draw(6);
			}

			device.Submit();
			device.Present(surface);

			var expected = string.Join(
				Environment.NewLine,
				"CreateShaderModule PositionColor",
				"CreateBuffer buffer1 CopyDst, Vertex 96 bytes",
				"CreateBuffer buffer2 CopyDst, Uniform 64 bytes",
				"CreateRenderPipeline pipeline1 " + pipeline.Descriptor,
				"CreateBindGroup bindGroup1 BindGroup 0 [@binding(0) buffer buffer2+0]",
				"WriteBuffer buffer2+0 64 bytes",
				"BeginRenderPass pass1 Pass [color window.texture Clear (0, 0, 0, 1) Store] no depth",
				"  SetPipeline pipeline1",
				"  SetBindGroup 0 bindGroup1",
				"  SetVertexBuffer 0 buffer1+0",
				"  SetViewport 0,0 320x240 depth 0..1",
				"  SetScissor 0,0 320x240",
				"  Draw 6 from 0",
				"EndRenderPass pass1",
				"Submit",
				"Present window");

			await Assert.That(device.Dump()).IsEqualTo(expected);
		}

		[Test]
		public async Task RecordedCommandsCarryTheResourcesThatWerePassed()
		{
			var device = new RecordingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(8, 8, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment));
			var indices = device.CreateBuffer(BufferUsage.Index | BufferUsage.CopyDst, 12);

			using (var pass = device.BeginRenderPass(new RenderPassDescriptor(target)))
			{
				pass.SetIndexBuffer(indices, IndexFormat.Uint16, 4);
				pass.DrawIndexed(6, 1, 2);
			}

			var setIndex = device.CommandsOf<SetIndexBufferCommand>().Single();
			await Assert.That(setIndex.Buffer).IsSameReferenceAs(indices);
			await Assert.That(setIndex.Format).IsEqualTo(IndexFormat.Uint16);
			await Assert.That(setIndex.Offset).IsEqualTo(4ul);

			var drawIndexed = device.CommandsOf<DrawIndexedCommand>().Single();
			await Assert.That(drawIndexed.IndexCount).IsEqualTo(6);
			await Assert.That(drawIndexed.FirstIndex).IsEqualTo(1);
			await Assert.That(drawIndexed.BaseVertex).IsEqualTo(2);
		}

		[Test]
		public async Task WrittenBytesAreCopiedNotAliased()
		{
			// Callers hand over scratch buffers they reuse; a recording that kept the reference would
			// report whatever the caller wrote last.
			var device = new RecordingRenderDevice();
			var buffer = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, 4);
			var scratch = new byte[] { 1, 2, 3, 4 };

			device.WriteBuffer(buffer, 0, scratch);
			scratch[0] = 99;

			var write = device.CommandsOf<WriteBufferCommand>().Single();
			await Assert.That(write.Data[0]).IsEqualTo((byte)1);
		}

		[Test]
		public async Task PassesDoNotNest()
		{
			var device = new RecordingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(8, 8, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment));

			using (device.BeginRenderPass(new RenderPassDescriptor(target)))
			{
				await Assert.That(() => device.BeginRenderPass(new RenderPassDescriptor(target)))
					.Throws<InvalidOperationException>();
			}

			// The FlushPass pattern: end the pass, then re-open it loading what was already drawn.
			using (device.BeginRenderPass(new RenderPassDescriptor(target, LoadOp.Load)))
			{
			}

			await Assert.That(device.OpenPass).IsNull();
			await Assert.That(device.CommandsOf<BeginRenderPassCommand>().Count).IsEqualTo(2);
		}

		[Test]
		public async Task SubmitAndPresentAreRefusedWhileAPassIsOpen()
		{
			var device = new RecordingRenderDevice();
			var surface = new StubSurfaceTarget();

			using (device.BeginRenderPass(new RenderPassDescriptor(surface.AcquireCurrentTexture())))
			{
				await Assert.That(() => device.Submit()).Throws<InvalidOperationException>();
				await Assert.That(() => device.Present(surface)).Throws<InvalidOperationException>();
			}

			device.Submit();
			device.Present(surface);
			await Assert.That(device.CommandsOf<PresentCommand>().Single().Target).IsSameReferenceAs(surface);
		}

		[Test]
		public async Task DrawingIntoAnEndedPassThrows()
		{
			var device = new RecordingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(8, 8, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment));
			var pass = device.BeginRenderPass(new RenderPassDescriptor(target));
			pass.Dispose();

			await Assert.That(() => pass.Draw(3)).Throws<InvalidOperationException>();

			// Disposing again is a no-op rather than a second EndRenderPass record.
			pass.Dispose();
			await Assert.That(device.CommandsOf<EndRenderPassCommand>().Count).IsEqualTo(1);
		}

		[Test]
		public async Task ShaderModulesResolveThroughRegisteredSources()
		{
			var device = new RecordingRenderDevice();
			device.RegisterShaderSources(new DictionaryShaderSourceProvider().Add("PositionColor", "// wgsl goes here"));

			var module = (StubShaderModule)device.CreateShaderModule("PositionColor");
			await Assert.That(module.Source).IsEqualTo("// wgsl goes here");
			await Assert.That(module.SourceKey).IsEqualTo("PositionColor");

			// Once sources are registered an unknown key is an error, exactly as it would be natively.
			await Assert.That(() => device.CreateShaderModule("NotAShader")).Throws<ArgumentException>();
		}

		[Test]
		public async Task BindGroupsRefuseStorageRangesOverTheBindingLimit()
		{
			// The native device shares this validator (BindGroupValidation); left to wgpu-native, an
			// oversized storage binding aborts the process at submit.
			var device = new RecordingRenderDevice { Limits = new DeviceLimits(DeviceLimits.DefaultMaxBufferSize, maxStorageBufferBindingSize: 1024) };
			var pipeline = ComputePipelineWith(device, BindingType.StorageBuffer, BindingType.ReadOnlyStorageBuffer);
			var big = device.CreateBuffer(BufferUsage.Storage, 2048);

			// Whole buffer (size 0) and an explicit size, on both storage binding types.
			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, big) })))
				.Throws<ArgumentOutOfRangeException>();
			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(1, big, 0, 1028) })))
				.Throws<ArgumentOutOfRangeException>();

			// At the limit is fine, including "the rest of the buffer" from an offset that leaves exactly the limit.
			device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, big, 0, 1024) }));
			device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(1, big, 1024) }));
			await Assert.That(device.CommandsOf<CreateBindGroupCommand>().Count).IsEqualTo(2);
		}

		[Test]
		public async Task BindGroupsRefuseUniformRangesOverTheBindingLimit()
		{
			var device = new RecordingRenderDevice();
			var pipeline = RenderPipelineWith(device, BindingType.UniformBuffer);
			var big = device.CreateBuffer(BufferUsage.Uniform, DeviceLimits.DefaultMaxUniformBufferBindingSize + 256);

			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, big) })))
				.Throws<ArgumentOutOfRangeException>();
			device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, big, 256) }));
		}

		[Test]
		public async Task BindGroupsRefuseMisalignedBufferOffsets()
		{
			var device = new RecordingRenderDevice
			{
				Limits = new DeviceLimits(DeviceLimits.DefaultMaxBufferSize, minStorageBufferOffsetAlignment: 64, minUniformBufferOffsetAlignment: 128),
			};
			var compute = ComputePipelineWith(device, BindingType.StorageBuffer);
			var render = RenderPipelineWith(device, BindingType.UniformBuffer);
			var buffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.Uniform, 1024);

			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(compute, 0, new[] { BindGroupEntry.ForBuffer(0, buffer, 32, 64) })))
				.Throws<ArgumentException>();
			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(render, 0, new[] { BindGroupEntry.ForBuffer(0, buffer, 64, 64) })))
				.Throws<ArgumentException>();

			// Each type is held to its own alignment.
			device.CreateBindGroup(new BindGroupDescriptor(compute, 0, new[] { BindGroupEntry.ForBuffer(0, buffer, 64, 64) }));
			device.CreateBindGroup(new BindGroupDescriptor(render, 0, new[] { BindGroupEntry.ForBuffer(0, buffer, 128, 64) }));
		}

		[Test]
		public async Task BindGroupsRefuseRangesPastTheEndOfTheBuffer()
		{
			var device = new RecordingRenderDevice();
			var pipeline = ComputePipelineWith(device, BindingType.StorageBuffer);
			var buffer = device.CreateBuffer(BufferUsage.Storage, 512);

			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, buffer, 768) })))
				.Throws<ArgumentException>();
			await Assert.That(() => device.CreateBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForBuffer(0, buffer, 256, 512) })))
				.Throws<ArgumentException>();
		}

		private static IComputePipeline ComputePipelineWith(RecordingRenderDevice device, params BindingType[] types)
			=> device.CreateComputePipeline(new ComputePipelineDescriptor(
				null,
				"main",
				types.Select((type, binding) => new BindGroupLayoutEntry(0, (uint)binding, ShaderStage.Compute, type)).ToArray(),
				"bindingLimits"));

		private static IRenderPipeline RenderPipelineWith(RecordingRenderDevice device, BindingType type)
		{
			var shader = device.CreateShaderModule("PositionColor");
			return device.CreateRenderPipeline(new RenderPipelineDescriptor(
				shader,
				"VertexMain",
				shader,
				"FragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(TextureFormat.Bgra8Unorm) },
				new[] { new BindGroupLayoutEntry(0, 0, ShaderStage.Vertex, type) }));
		}
	}
}
