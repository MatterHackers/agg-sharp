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
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.RenderGl.Scene;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The full-frame capture's open/close bookkeeping. The reported failure was
	/// "A full-frame capture is already in progress." thrown out of a *top level* paint, which means the
	/// flag was left set by an earlier frame: both ends of the cycle touch the compat layer's render
	/// target, and a target that goes away mid frame makes those calls throw. Whatever throws, the
	/// renderer must not be left believing a capture is still open - that turns one bad frame into an
	/// exception on every frame after it.
	/// </summary>
	public class WebGpuFullFrameCaptureTests
	{
		private const uint Width = 80;

		private const uint Height = 60;

		private static readonly RectangleDouble Viewport = new RectangleDouble(0, 0, Width, Height);

		[Test]
		public async Task ACaptureCanBeStartedAgainAfterItIsEnded()
		{
			using var fixture = CaptureFixture.Create();

			fixture.Renderer.BeginFullFrameCapture(Viewport);
			fixture.Renderer.EndFullFrameCapture();

			// The ordinary frame-after-frame case: nothing threw, so nothing may be left open.
			fixture.Renderer.BeginFullFrameCapture(Viewport);
			await Assert.That(fixture.Context.Passes.ColorTarget).IsNotEqualTo(fixture.Target);

			fixture.Renderer.EndFullFrameCapture();
			await Assert.That(fixture.Context.Passes.ColorTarget).IsEqualTo(fixture.Target);
		}

		[Test]
		public async Task EndingACaptureAfterTheTargetWasReleasedDoesNotStrandTheNextFrame()
		{
			using var fixture = CaptureFixture.Create();

			fixture.Renderer.BeginFullFrameCapture(Viewport);

			// What a present, a resize or a device loss does to a paint that is already under way.
			fixture.Context.SetRenderTarget(null, null);
			fixture.Renderer.EndFullFrameCapture();

			// The next frame sets a target again and must be able to capture.
			fixture.Context.SetRenderTarget(fixture.Target, fixture.Depth);
			fixture.Renderer.BeginFullFrameCapture(Viewport);
			fixture.Renderer.EndFullFrameCapture();

			await Assert.That(fixture.Context.Passes.ColorTarget).IsEqualTo(fixture.Target);
		}

		[Test]
		public async Task ACaptureEndThatThrowsStillClosesTheCapture()
		{
			using var fixture = CaptureFixture.Create();

			fixture.Renderer.BeginFullFrameCapture(Viewport);

			// EndFullFrameCapture restores the target, which ends the open pass first. That is the call
			// that can throw with a target that has gone invalid underneath the frame.
			fixture.Context.Passes.EnsurePassOpen();
			fixture.Device.FailNextPassEnd = true;

			await Assert.That(() => fixture.Renderer.EndFullFrameCapture()).Throws<InvalidOperationException>();

			// SetTargets ends the open pass before it reassigns, so the throw came before the reassignment
			// and the compat layer was left aimed at the capture target. End retries the restore, so the
			// rest of this frame still draws where the caller meant it to.
			await Assert.That(fixture.Context.Passes.ColorTarget).IsEqualTo(fixture.Target);

			// The frame after the failure has to work; before the fix the capture stayed flagged open and
			// every later frame threw "already in progress" instead.
			fixture.Renderer.BeginFullFrameCapture(Viewport);
			fixture.Renderer.EndFullFrameCapture();
			await Assert.That(fixture.Context.Passes.ColorTarget).IsEqualTo(fixture.Target);
		}

		[Test]
		public async Task ACaptureThatFailsWhileOpeningLeavesNothingOpen()
		{
			using var fixture = CaptureFixture.Create();

			// The transparent clear of the capture target is the last thing Begin does, and it is past the
			// point where the capture counts as open. Every call site calls Begin outside its try, so a
			// throw here is never followed by an End.
			fixture.Device.FailPassLabel = "SupersampleClear";

			await Assert.That(() => fixture.Renderer.BeginFullFrameCapture(Viewport))
				.Throws<InvalidOperationException>();

			// The caller's target is back, so the rest of the frame draws where it meant to ...
			await Assert.That(fixture.Context.Passes.ColorTarget).IsEqualTo(fixture.Target);
			await Assert.That(fixture.Context.CoordinateScale).IsEqualTo(1);

			// ... and the next frame can still capture.
			fixture.Device.FailPassLabel = null;
			fixture.Renderer.BeginFullFrameCapture(Viewport);
			fixture.Renderer.EndFullFrameCapture();
			await Assert.That(fixture.Context.Passes.ColorTarget).IsEqualTo(fixture.Target);
		}

		[Test]
		public async Task AFailedCaptureIsNotFollowedByABlitOfTheLastFrame()
		{
			using var fixture = CaptureFixture.Create();

			// One good frame, so a capture target exists with last frame's content in it.
			fixture.Renderer.BeginFullFrameCapture(Viewport);
			fixture.Renderer.EndFullFrameCapture();
			fixture.Renderer.DownsampleAndBlitFullFrame();

			fixture.Device.ClearRecording();
			fixture.Device.FailPassLabel = "SupersampleClear";

			// The call sites pair Begin and End in a finally, so the failed Begin is still followed by the
			// end-and-blit. Nothing was rendered into the capture target this frame, so nothing may be
			// composited out of it.
			await Assert.That(() => fixture.Renderer.BeginFullFrameCapture(Viewport))
				.Throws<InvalidOperationException>();

			fixture.Device.FailPassLabel = null;
			fixture.Renderer.EndFullFrameCapture();
			fixture.Renderer.DownsampleAndBlitFullFrame();

			await Assert.That(fixture.Device.PassLabels()).DoesNotContain("SupersampleDownsample");
		}

		[Test]
		public async Task AGenuinelyNestedCaptureStillThrowsAndSaysWhereTheFirstOneOpened()
		{
			using var fixture = CaptureFixture.Create();

#if DEBUG
			// Off by default - the stack walk is only worth paying for when someone is chasing this - so
			// the diagnostic has to be switched on to be asserted on.
			bool traceWasEnabled = WebGpuSceneRenderer.CaptureTraceEnabled;
			WebGpuSceneRenderer.CaptureTraceEnabled = true;
			try
			{
#endif
				fixture.Renderer.BeginFullFrameCapture(Viewport);

				var exception = await Assert.That(() => fixture.Renderer.BeginFullFrameCapture(Viewport))
					.Throws<InvalidOperationException>();

				await Assert.That(exception.Message).Contains("A full-frame capture is already in progress.");
#if DEBUG
				// The diagnostic that tells a nested paint apart from a capture stranded by an earlier frame.
				await Assert.That(exception.Message).Contains("Opened at:");
			}
			finally
			{
				WebGpuSceneRenderer.CaptureTraceEnabled = traceWasEnabled;
			}
#endif

			fixture.Renderer.EndFullFrameCapture();
		}

		/// <summary>A scene renderer over a compat context on a failure-injecting recording device.</summary>
		private sealed class CaptureFixture : IDisposable
		{
			private CaptureFixture(
				FaultInjectingRenderDevice device,
				GlCompatContext context,
				WebGpuSceneRenderer renderer,
				IGpuTexture target,
				IGpuTexture depth)
			{
				this.Device = device;
				this.Context = context;
				this.Renderer = renderer;
				this.Target = target;
				this.Depth = depth;
			}

			public FaultInjectingRenderDevice Device { get; }

			public GlCompatContext Context { get; }

			public WebGpuSceneRenderer Renderer { get; }

			public IGpuTexture Target { get; }

			public IGpuTexture Depth { get; }

			public static CaptureFixture Create()
			{
				var device = new FaultInjectingRenderDevice();
				var target = device.CreateTexture(new TextureDescriptor(
					Width,
					Height,
					TextureFormat.Bgra8Unorm,
					TextureUsage.RenderAttachment | TextureUsage.CopySrc,
					1,
					1,
					"colorTarget"));

				var depth = device.CreateTexture(new TextureDescriptor(
					Width,
					Height,
					TextureFormat.Depth32Float,
					TextureUsage.RenderAttachment,
					1,
					1,
					"depthTarget"));

				var context = new GlCompatContext(device);
				context.SetRenderTarget(target, depth);

				var renderer = new WebGpuSceneRenderer(context) { OwnerGl = new GL(context) };
				context.SceneRenderer = renderer;

				return new CaptureFixture(device, context, renderer, target, depth);
			}

			public void Dispose()
			{
				this.Device.FailNextPassEnd = false;
				this.Device.FailPassLabel = null;
				this.Renderer.Dispose();
				this.Context.Dispose();
			}
		}

		/// <summary>
		/// A <see cref="RecordingRenderDevice"/> that can be made to fail the way a real device fails when
		/// the frame's target is pulled out from under a paint: opening a named pass, or ending one.
		/// </summary>
		private sealed class FaultInjectingRenderDevice : IRenderDevice
		{
			private readonly RecordingRenderDevice inner = new RecordingRenderDevice();

			/// <summary>When set, opening a pass with this label throws instead.</summary>
			public string FailPassLabel { get; set; }

			/// <summary>When set, the next pass to end throws after really ending. Cleared as it fires.</summary>
			public bool FailNextPassEnd { get; set; }

			/// <summary>Drops the recorded commands so a test can measure only what follows.</summary>
			public void ClearRecording() => this.inner.ClearRecording();

			/// <summary>The label of every pass opened since the last <see cref="ClearRecording"/>.</summary>
			public IReadOnlyList<string> PassLabels()
				=> this.inner.CommandsOf<BeginRenderPassCommand>()
					.Select(command => command.Descriptor.Label)
					.ToList();

			public DeviceLimits Limits => this.inner.Limits;

			public IGpuBuffer CreateBuffer(BufferUsage usage, ulong sizeInBytes, ReadOnlySpan<byte> initialData = default)
				=> this.inner.CreateBuffer(usage, sizeInBytes, initialData);

			public IGpuTexture CreateTexture(in TextureDescriptor descriptor) => this.inner.CreateTexture(descriptor);

			public ISampler CreateSampler(in SamplerDescriptor descriptor) => this.inner.CreateSampler(descriptor);

			public IShaderModule CreateShaderModule(string sourceKey) => this.inner.CreateShaderModule(sourceKey);

			public void RegisterShaderSources(IShaderSourceProvider provider) => this.inner.RegisterShaderSources(provider);

			public IRenderPipeline CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
				=> this.inner.CreateRenderPipeline(descriptor);

			public IBindGroup CreateBindGroup(in BindGroupDescriptor descriptor) => this.inner.CreateBindGroup(descriptor);

			public IRenderEncoder BeginRenderPass(in RenderPassDescriptor descriptor)
			{
				if (this.FailPassLabel != null && this.FailPassLabel == descriptor.Label)
				{
					throw new InvalidOperationException($"Injected failure opening pass '{descriptor.Label}'.");
				}

				return new FaultInjectingEncoder(this, this.inner.BeginRenderPass(descriptor));
			}

			public void WriteBuffer(IGpuBuffer buffer, ulong offset, ReadOnlySpan<byte> data)
				=> this.inner.WriteBuffer(buffer, offset, data);

			public void WriteTexture(IGpuTexture texture, ReadOnlySpan<byte> data, uint bytesPerRow, uint mipLevel = 0)
				=> this.inner.WriteTexture(texture, data, bytesPerRow, mipLevel);

			public ValueTask<TextureReadResult> ReadTextureAsync(IGpuTexture source, Memory<byte> destination)
				=> this.inner.ReadTextureAsync(source, destination);

			public void Submit() => this.inner.Submit();

			public void Present(ISurfaceTarget target) => this.inner.Present(target);

			public void Dispose() => this.inner.Dispose();

			/// <summary>
			/// Passes the whole encoder through and only interferes at the end - and even then the real pass
			/// is ended first, so the recording device is not left believing a pass is still open and the
			/// test can go on to open the next one.
			/// </summary>
			private sealed class FaultInjectingEncoder : IRenderEncoder
			{
				private readonly FaultInjectingRenderDevice owner;
				private readonly IRenderEncoder inner;

				public FaultInjectingEncoder(FaultInjectingRenderDevice owner, IRenderEncoder inner)
				{
					this.owner = owner;
					this.inner = inner;
				}

				public void SetPipeline(IRenderPipeline pipeline) => this.inner.SetPipeline(pipeline);

				public void SetBindGroup(int index, IBindGroup bindGroup) => this.inner.SetBindGroup(index, bindGroup);

				public void SetVertexBuffer(int slot, IGpuBuffer buffer, ulong offset = 0)
					=> this.inner.SetVertexBuffer(slot, buffer, offset);

				public void SetIndexBuffer(IGpuBuffer buffer, IndexFormat format, ulong offset = 0)
					=> this.inner.SetIndexBuffer(buffer, format, offset);

				public void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
					=> this.inner.SetViewport(x, y, width, height, minDepth, maxDepth);

				public void SetScissor(int x, int y, int width, int height) => this.inner.SetScissor(x, y, width, height);

				public void Draw(int vertexCount, int firstVertex = 0) => this.inner.Draw(vertexCount, firstVertex);

				public void DrawIndexed(int indexCount, int firstIndex = 0, int baseVertex = 0)
					=> this.inner.DrawIndexed(indexCount, firstIndex, baseVertex);

				public void Dispose()
				{
					this.inner.Dispose();

					if (this.owner.FailNextPassEnd)
					{
						this.owner.FailNextPassEnd = false;
						throw new InvalidOperationException("Injected failure ending a pass.");
					}
				}
			}
		}
	}
}
