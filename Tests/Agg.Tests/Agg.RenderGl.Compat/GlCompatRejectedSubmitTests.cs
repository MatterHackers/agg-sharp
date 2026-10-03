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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// What a submit the device rejects leaves behind. A window whose GPU refuses every frame (the X11
	/// black-window case) submits and fails each frame; if the per-frame recycling only ran after a
	/// successful submit, the per-draw pools and the deferred-release queue would grow every frame.
	/// </summary>
	public class GlCompatRejectedSubmitTests
	{
		[Test]
		public async Task ARejectedSubmitStillRecyclesThePerDrawPoolsAndRunsQueuedReleases()
		{
			var device = new SubmitRejectingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(
				100, 50, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment | TextureUsage.CopySrc, 1, 1, "colorTarget"));
			var context = new GlCompatContext(device);
			context.SetRenderTarget(target);

			// Claims this thread as the render thread, so a release from another thread is queued.
			context.Submit();
			device.Inner.ClearRecording();

			var released = new DisposeCounter();
			bool deferred = false;
			var releaser = new Thread(() => deferred = context.ReleaseOnRenderThread(released));
			releaser.Start();
			releaser.Join();
			await Assert.That(deferred).IsTrue();

			DrawTriangle(context);
			device.RejectSubmits = true;
			await Assert.That(() => context.Submit()).Throws<InvalidOperationException>();

			await Assert.That(released.DisposeCount).IsEqualTo(1)
				.Because("the release queue drains whether or not the device accepted the recording");

			// The rejected frame's uniform slot is free again: the next frame's first draw reuses it.
			device.RejectSubmits = false;
			DrawTriangle(context);
			context.Submit();

			var uniformWrites = device.Inner.CommandsOf<WriteBufferCommand>()
				.Where(command => (command.Buffer.Usage & BufferUsage.Uniform) != 0)
				.ToList();
			await Assert.That(uniformWrites.Count).IsEqualTo(2);
			await Assert.That(uniformWrites[1].Offset).IsEqualTo(0UL)
				.Because("a rejected submit must reset the per-draw pools, or a window failing every frame grows them every frame");

			context.Dispose();
		}

		[Test]
		public async Task ABeforeSubmitHookThatThrowsStillRecyclesThePerDrawPoolsAndRunsQueuedReleases()
		{
			var device = new SubmitRejectingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(
				100, 50, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment | TextureUsage.CopySrc, 1, 1, "colorTarget"));
			var context = new GlCompatContext(device);
			context.SetRenderTarget(target);

			// Claims this thread as the render thread, so a release from another thread is queued.
			context.Submit();
			device.Inner.ClearRecording();

			var released = new DisposeCounter();
			bool deferred = false;
			var releaser = new Thread(() => deferred = context.ReleaseOnRenderThread(released));
			releaser.Start();
			releaser.Join();
			await Assert.That(deferred).IsTrue();

			// A stager flushing through the hook (the scene renderer's uniform writes) fails this frame.
			bool hookThrows = true;
			context.BeforeSubmit += () =>
			{
				if (hookThrows)
				{
					throw new InvalidOperationException("Injected failure in a BeforeSubmit stager.");
				}
			};

			DrawTriangle(context);
			await Assert.That(() => context.Submit()).Throws<InvalidOperationException>();

			await Assert.That(released.DisposeCount).IsEqualTo(1)
				.Because("the release queue drains even when a pre-submit flush throws");

			hookThrows = false;
			DrawTriangle(context);
			context.Submit();

			var uniformWrites = device.Inner.CommandsOf<WriteBufferCommand>()
				.Where(command => (command.Buffer.Usage & BufferUsage.Uniform) != 0)
				.ToList();
			await Assert.That(uniformWrites.Count).IsEqualTo(2);
			await Assert.That(uniformWrites[1].Offset).IsEqualTo(0UL)
				.Because("a throwing BeforeSubmit hook must not stop the per-draw pools from being reset");

			context.Dispose();
		}

		private static void DrawTriangle(GlCompatContext context)
		{
			context.Begin(BeginMode.Triangles);
			context.Vertex2(0, 0);
			context.Vertex2(1, 0);
			context.Vertex2(1, 1);
			context.End();
		}

		private sealed class DisposeCounter : IDisposable
		{
			public int DisposeCount { get; private set; }

			public void Dispose() => this.DisposeCount++;
		}

		/// <summary>
		/// A <see cref="RecordingRenderDevice"/> whose submit can be made to throw, the way the WebGPU
		/// device throws when wgpu rejects a recording (and drops it, so none of it ever runs).
		/// </summary>
		private sealed class SubmitRejectingRenderDevice : IRenderDevice
		{
			public RecordingRenderDevice Inner { get; } = new RecordingRenderDevice();

			public bool RejectSubmits { get; set; }

			public DeviceLimits Limits => this.Inner.Limits;

			public IGpuBuffer CreateBuffer(BufferUsage usage, ulong sizeInBytes, ReadOnlySpan<byte> initialData = default)
				=> this.Inner.CreateBuffer(usage, sizeInBytes, initialData);

			public IGpuTexture CreateTexture(in TextureDescriptor descriptor) => this.Inner.CreateTexture(descriptor);

			public ISampler CreateSampler(in SamplerDescriptor descriptor) => this.Inner.CreateSampler(descriptor);

			public IShaderModule CreateShaderModule(string sourceKey) => this.Inner.CreateShaderModule(sourceKey);

			public void RegisterShaderSources(IShaderSourceProvider provider) => this.Inner.RegisterShaderSources(provider);

			public IRenderPipeline CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
				=> this.Inner.CreateRenderPipeline(descriptor);

			public IBindGroup CreateBindGroup(in BindGroupDescriptor descriptor) => this.Inner.CreateBindGroup(descriptor);

			public IComputePipeline CreateComputePipeline(in ComputePipelineDescriptor descriptor)
				=> this.Inner.CreateComputePipeline(descriptor);

			public IComputeEncoder BeginComputePass(string label = null) => this.Inner.BeginComputePass(label);

			public ValueTask ReadBufferAsync(IGpuBuffer source, ulong offset, Memory<byte> destination)
				=> this.Inner.ReadBufferAsync(source, offset, destination);

			public IRenderEncoder BeginRenderPass(in RenderPassDescriptor descriptor) => this.Inner.BeginRenderPass(descriptor);

			public void WriteBuffer(IGpuBuffer buffer, ulong offset, ReadOnlySpan<byte> data)
				=> this.Inner.WriteBuffer(buffer, offset, data);

			public void WriteTexture(IGpuTexture texture, ReadOnlySpan<byte> data, uint bytesPerRow, uint mipLevel = 0)
				=> this.Inner.WriteTexture(texture, data, bytesPerRow, mipLevel);

			public ValueTask<TextureReadResult> ReadTextureAsync(IGpuTexture source, Memory<byte> destination)
				=> this.Inner.ReadTextureAsync(source, destination);

			public void Submit()
			{
				if (this.RejectSubmits)
				{
					throw new InvalidOperationException("Injected rejection: the recorded commands are invalid and were not submitted.");
				}

				this.Inner.Submit();
			}

			public void Present(ISurfaceTarget target) => this.Inner.Present(target);

			public void Dispose() => this.Inner.Dispose();
		}
	}
}
