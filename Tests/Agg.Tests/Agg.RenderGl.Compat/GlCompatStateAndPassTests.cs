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
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The state machine half of the compat layer: matrices reaching the shader, dynamic state that has
	/// become pipeline permutations, and the pass lifetime that GL never had to think about.
	/// </summary>
	public class GlCompatStateAndPassTests
	{
		[Test]
		public async Task PushTranslatePopIsVisibleInTheUniformWriteAndThenUndone()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Modelview);
			harness.Context.LoadIdentity();
			harness.Context.PushMatrix();
			harness.Context.Translate(5, 7, 0);
			harness.DrawTriangle();
			harness.Context.PopMatrix();
			harness.DrawTriangle();

			// Submit rather than FlushPass: per-draw uniform blocks are staged and pushed in one write
			// just before the device submit, so nothing is readable until then.
			harness.Context.Submit();

			var translated = harness.UniformModelView(0);
			await Assert.That(translated.Row3.X).IsEqualTo(5.0).Within(1e-5);
			await Assert.That(translated.Row3.Y).IsEqualTo(7.0).Within(1e-5);

			var restored = harness.UniformModelView(1);
			await Assert.That(restored.Row3.X).IsEqualTo(0.0).Within(1e-5);
			await Assert.That(restored.Row3.Y).IsEqualTo(0.0).Within(1e-5);
		}

		[Test]
		public async Task EachDrawGetsItsOwnUniformRangeSoOneWriteCannotOverwriteAnother()
		{
			// If every draw shared a uniform range, both draws in a pass would read whichever write landed
			// last - queue writes are ordered against submits, not against draws.
			var harness = GlCompatTestHarness.Create();

			harness.Context.Translate(1, 0, 0);
			harness.DrawTriangle();
			harness.Context.Translate(1, 0, 0);
			harness.DrawTriangle();
			harness.Context.Submit();

			// One write for both draws, holding both draws' blocks.
			await Assert.That(harness.UniformWrites().Count).IsEqualTo(1);
			await Assert.That(harness.UniformModelView(0).Row3.X).IsEqualTo(1.0).Within(1e-5);
			await Assert.That(harness.UniformModelView(1).Row3.X).IsEqualTo(2.0).Within(1e-5);

			// Each draw's range is its own, and they are the ranges the draws were bound to.
			var boundOffsets = harness.Device.CommandsOf<CreateBindGroupCommand>()
				.SelectMany(command => command.Descriptor.Entries)
				.Where(entry => entry.Buffer != null && (entry.Buffer.Usage & BufferUsage.Uniform) != 0)
				.Select(entry => entry.Offset)
				.ToList();
			await Assert.That(boundOffsets).IsEquivalentTo(new ulong[] { 0, GlDrawSubmitter.UniformStride });

			// After a submit the slots are safe to reuse, so a third draw goes back to the first range.
			harness.DrawTriangle();
			harness.Context.Submit();

			await Assert.That(harness.UniformWrites().Count).IsEqualTo(2);
			await Assert.That(harness.UniformWrites()[1].Offset).IsEqualTo(0UL);
		}

		[Test]
		public async Task TheProjectionMatrixIsRemappedIntoTheBackendsClipSpace()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Projection);
			harness.Context.LoadIdentity();
			harness.Context.Ortho(0, 100, 0, 50, -1, 1);
			harness.Context.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Modelview);
			harness.DrawTriangle();
			harness.Context.Submit();

			var projection = harness.UniformProjection(0);

			// GL's ortho maps z to -1..1; the remap halves the z scale and biases by half, which puts
			// the near plane at 0 and the far plane at 1 the way D3D and WebGPU want.
			await Assert.That(projection.Row2.Z).IsEqualTo(-0.5).Within(1e-5);
			await Assert.That(projection.Row3.Z).IsEqualTo(0.5).Within(1e-5);

			// Y is deliberately not flipped: WebGPU's normalized device Y points up, exactly like GL's.
			await Assert.That(projection.Row1.Y).IsEqualTo(2.0 / 50).Within(1e-5);
		}

		[Test]
		public async Task ViewportAndScissorAreFlippedIntoTopLeftCoordinates()
		{
			var harness = GlCompatTestHarness.Create(100, 50);

			harness.Context.Viewport(0, 0, 100, 50);
			harness.Context.Enable((int)EnableCap.ScissorTest);
			harness.Context.Scissor(10, 5, 20, 10);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var viewport = harness.Device.CommandsOf<SetViewportCommand>().Single();
			await Assert.That(viewport.X).IsEqualTo(0f);
			await Assert.That(viewport.Y).IsEqualTo(0f);
			await Assert.That(viewport.Width).IsEqualTo(100f);
			await Assert.That(viewport.Height).IsEqualTo(50f);

			// GL measures y up from the bottom, so a box 5 up from the bottom of a 50 tall target starts
			// 35 down from the top.
			var scissor = harness.Device.CommandsOf<SetScissorCommand>().Single();
			await Assert.That(scissor.X).IsEqualTo(10);
			await Assert.That(scissor.Y).IsEqualTo(35);
			await Assert.That(scissor.Width).IsEqualTo(20);
			await Assert.That(scissor.Height).IsEqualTo(10);
		}

		[Test]
		public async Task DisablingScissorRestoresTheFullAttachmentBecauseWebGpuCannotTurnItOff()
		{
			var harness = GlCompatTestHarness.Create(100, 50);

			harness.Context.Enable((int)EnableCap.ScissorTest);
			harness.Context.Scissor(10, 5, 20, 10);
			harness.DrawTriangle();
			harness.Context.Disable((int)EnableCap.ScissorTest);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var scissors = harness.Device.CommandsOf<SetScissorCommand>();
			await Assert.That(scissors.Count).IsEqualTo(2);
			await Assert.That(scissors[1].Width).IsEqualTo(100);
			await Assert.That(scissors[1].Height).IsEqualTo(50);
		}

		[Test]
		public async Task BlendStateChangesProduceDistinctCachedPipelines()
		{
			var harness = GlCompatTestHarness.Create();

			harness.DrawTriangle();

			harness.Context.Enable((int)EnableCap.Blend);
			harness.Context.BlendFunc((int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha);
			harness.DrawTriangle();

			// Same state again: this must be a cache hit, not a second pipeline object.
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var bound = harness.BoundPipelines();
			await Assert.That(bound.Count).IsEqualTo(3);
			await Assert.That(ReferenceEquals(bound[0], bound[1])).IsFalse();
			await Assert.That(ReferenceEquals(bound[1], bound[2])).IsTrue();

			await Assert.That(harness.Device.CommandsOf<CreateRenderPipelineCommand>().Count).IsEqualTo(2);
			await Assert.That(harness.Context.Pipelines.PipelineCount).IsEqualTo(2);

			var blended = bound[1].Descriptor.ColorTargets[0];
			await Assert.That(blended.BlendEnabled).IsTrue();
			await Assert.That(blended.Color.SourceFactor).IsEqualTo(BlendFactor.SrcAlpha);
			await Assert.That(blended.Color.DestinationFactor).IsEqualTo(BlendFactor.OneMinusSrcAlpha);
		}

		[Test]
		public async Task ColorMaskPassesBecomeThreePipelinePermutationsAndTheRestoreIsACacheHit()
		{
			// The LCD subpixel text composite draws the same quad three times, one channel each, then
			// restores the full mask in a finally. Color write mask is pipeline state in WebGPU, so that
			// has to be four pipeline lookups over three distinct permutations plus the original.
			var harness = GlCompatTestHarness.Create();

			harness.DrawTriangle();

			for (int channel = 0; channel < 3; channel++)
			{
				harness.Context.ColorMask(channel == 0, channel == 1, channel == 2, false);
				harness.DrawTriangle();
			}

			harness.Context.ColorMask(true, true, true, true);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var bound = harness.BoundPipelines();
			await Assert.That(bound.Count).IsEqualTo(5);

			var masks = bound.Select(pipeline => pipeline.Descriptor.ColorTargets[0].WriteMask).ToList();
			await Assert.That(masks[0]).IsEqualTo(ColorWriteMask.All);
			await Assert.That(masks[1]).IsEqualTo(ColorWriteMask.Red);
			await Assert.That(masks[2]).IsEqualTo(ColorWriteMask.Green);
			await Assert.That(masks[3]).IsEqualTo(ColorWriteMask.Blue);
			await Assert.That(masks[4]).IsEqualTo(ColorWriteMask.All);

			// The three channel passes are three new pipelines; the restore comes back to the first one.
			await Assert.That(harness.Device.CommandsOf<CreateRenderPipelineCommand>().Count).IsEqualTo(4);
			await Assert.That(ReferenceEquals(bound[4], bound[0])).IsTrue();
		}

		[Test]
		public async Task DepthAndCullStateReachThePipelineAndDisabledDepthBecomesAlways()
		{
			var harness = GlCompatTestHarness.Create(withDepth: true);

			harness.Context.Enable((int)EnableCap.DepthTest);
			harness.Context.DepthFunc((int)DepthFunction.Lequal);
			harness.Context.Enable((int)EnableCap.CullFace);
			harness.Context.CullFace(CullFaceMode.Front);
			harness.DrawTriangle();

			harness.Context.Disable((int)EnableCap.DepthTest);
			harness.Context.DepthMask(false);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var bound = harness.BoundPipelines();
			await Assert.That(bound[0].Descriptor.DepthStencil.DepthCompare).IsEqualTo(CompareFunction.LessEqual);
			await Assert.That(bound[0].Descriptor.DepthStencil.DepthWriteEnabled).IsTrue();
			await Assert.That(bound[0].Descriptor.CullMode).IsEqualTo(CullMode.Front);

			// A disabled depth test keeps the attachment but always passes - the pipeline still has to
			// agree with the pass about having a depth buffer.
			await Assert.That(bound[1].Descriptor.DepthStencil.HasDepth).IsTrue();
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthCompare).IsEqualTo(CompareFunction.Always);
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthWriteEnabled).IsFalse();
		}

		[Test]
		public async Task DisablingTheDepthTestStopsDepthWritesEvenWithTheMaskLeftTrue()
		{
			// The discriminating case: the mask is never touched, only the test is turned off. D3D11
			// ignores DepthWriteMask whenever DepthEnable is false, so the classic path writes no depth
			// here; WebGPU has no such coupling, and a pipeline left with depthWriteEnabled and an Always
			// comparison would stamp depth on every fragment of every unclipped overlay.
			var harness = GlCompatTestHarness.Create(withDepth: true);

			harness.Context.Enable((int)EnableCap.DepthTest);
			harness.DrawTriangle();

			harness.Context.Disable((int)EnableCap.DepthTest);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var bound = harness.BoundPipelines();
			await Assert.That(bound[0].Descriptor.DepthStencil.DepthWriteEnabled).IsTrue();
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthWriteEnabled).IsFalse();
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthCompare).IsEqualTo(CompareFunction.Always);

			// And the mask really was left alone, so this is not the same case as disabling both.
			await Assert.That(harness.Context.State.DepthMask).IsTrue();
		}

		[Test]
		public async Task AScissorHangingOffTheTargetIsClampedToTheAttachment()
		{
			// GL ignores the out-of-bounds part and D3D11 forgave it, but WebGPU validates: a scissor that
			// is not wholly inside the attachment kills the pass. Scrolled widgets really do push these.
			var harness = GlCompatTestHarness.Create(100, 50);

			harness.Context.Enable((int)EnableCap.ScissorTest);

			// 30 up from the bottom of a 50 tall target, 40 tall: the top 20 rows are off the target, and
			// the left edge is off it too.
			harness.Context.Scissor(-10, 30, 60, 40);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var scissor = harness.Device.CommandsOf<SetScissorCommand>().Single();
			await Assert.That(scissor.X).IsEqualTo(0);
			await Assert.That(scissor.Y).IsEqualTo(0);
			await Assert.That(scissor.Width).IsEqualTo(50);
			await Assert.That(scissor.Height).IsEqualTo(20);
		}

		[Test]
		public async Task PolygonOffsetReachesThePipelineAsDepthBiasAndOnlyWhileEnabled()
		{
			// Coplanar overlays (RenderHelper's selection outlines) call glPolygonOffset(1, 1) between an
			// Enable/Disable pair. WebGPU has no dynamic bias, so it must become depth stencil state -
			// and the offset must vanish again when the cap is disabled or the overlay pipeline would be
			// handed back to the ordinary geometry that follows it.
			var harness = GlCompatTestHarness.Create(withDepth: true);

			harness.Context.Enable((int)EnableCap.DepthTest);
			harness.DrawTriangle();

			harness.Context.Enable((int)EnableCap.PolygonOffsetFill);
			harness.Context.PolygonOffset(2, 3);
			harness.DrawTriangle();

			harness.Context.Disable((int)EnableCap.PolygonOffsetFill);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var bound = harness.BoundPipelines();
			await Assert.That(bound.Count).IsEqualTo(3);

			await Assert.That(bound[0].Descriptor.DepthStencil.HasDepthBias).IsFalse();

			// units -> the integer depthBias, factor -> depthBiasSlopeScale, the same split the D3D11
			// backend makes onto RasterizerDescription. Nothing in GL feeds depthBiasClamp.
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthBias).IsEqualTo(3);
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthBiasSlopeScale).IsEqualTo(2f);
			await Assert.That(bound[1].Descriptor.DepthStencil.DepthBiasClamp).IsEqualTo(0f);

			// Disabling the cap comes back to the very first pipeline, not a third one.
			await Assert.That(ReferenceEquals(bound[2], bound[0])).IsTrue();
			await Assert.That(harness.Device.CommandsOf<CreateRenderPipelineCommand>().Count).IsEqualTo(2);
		}

		[Test]
		public async Task AMipmappedTexImage2DChainBecomesOneTextureAndAWritePerLevel()
		{
			// ImageTexturePlugin sets a mipmapped min filter, uploads level 0, then walks the chain down
			// to 1x1. WebGPU fixes mipLevelCount at creation, so the whole chain has to land in one
			// texture with one wgpuQueueWriteTexture per level rather than a texture per level.
			var harness = GlCompatTestHarness.Create();

			int texture = harness.Context.GenTexture();
			harness.Context.BindTexture((int)TextureTarget.Texture2D, texture);
			harness.Context.TexParameter(
				TextureTarget.Texture2D,
				TextureParameterName.TextureMinFilter,
				(int)TextureMinFilter.LinearMipmapLinear);

			int[] sizes = { 4, 2, 1 };
			for (int level = 0; level < sizes.Length; level++)
			{
				harness.Context.TexImage2D(0, level, 0, sizes[level], sizes[level], 0, 0x1908, 0, new byte[sizes[level] * sizes[level] * 4]);
			}

			var creations = harness.Device.CommandsOf<CreateTextureCommand>();
			await Assert.That(creations.Count).IsEqualTo(1);
			await Assert.That(creations[0].Descriptor.MipLevelCount).IsEqualTo(3u);
			await Assert.That(creations[0].Descriptor.Width).IsEqualTo(4u);

			var writes = harness.Device.CommandsOf<WriteTextureCommand>();
			await Assert.That(writes.Count).IsEqualTo(3);
			for (int level = 0; level < sizes.Length; level++)
			{
				await Assert.That(writes[level].MipLevel).IsEqualTo((uint)level);
				await Assert.That(writes[level].BytesPerRow).IsEqualTo((uint)(sizes[level] * 4));
				await Assert.That(writes[level].Data.Length).IsEqualTo(sizes[level] * sizes[level] * 4);
				await Assert.That(ReferenceEquals(writes[level].Texture, creations[0].Texture)).IsTrue();
			}
		}

		[Test]
		public async Task AnUnmipmappedTexImage2DAllocatesOneLevelAndDropsAnyMipsPushedAtIt()
		{
			// Without a mipmapped min filter there is nothing to say a chain is coming, so only level 0
			// is allocated. Extra levels are dropped rather than throwing - the 2D path pushes them
			// unconditionally and refusing would break drawing that works fine at full resolution.
			var harness = GlCompatTestHarness.Create();

			int texture = harness.Context.GenTexture();
			harness.Context.BindTexture((int)TextureTarget.Texture2D, texture);
			harness.Context.TexImage2D(0, 0, 0, 4, 4, 0, 0x1908, 0, new byte[4 * 4 * 4]);
			harness.Context.TexImage2D(0, 1, 0, 2, 2, 0, 0x1908, 0, new byte[2 * 2 * 4]);

			var creations = harness.Device.CommandsOf<CreateTextureCommand>();
			await Assert.That(creations.Count).IsEqualTo(1);
			await Assert.That(creations[0].Descriptor.MipLevelCount).IsEqualTo(1u);

			var writes = harness.Device.CommandsOf<WriteTextureCommand>();
			await Assert.That(writes.Count).IsEqualTo(1);
			await Assert.That(writes[0].MipLevel).IsEqualTo(0u);
		}

		[Test]
		public async Task AttribPushAndPopRestoreTheViewport()
		{
			var harness = GlCompatTestHarness.Create(100, 50);

			harness.Context.Viewport(0, 0, 100, 50);
			harness.DrawTriangle();

			harness.Context.PushAttrib(AttribMask.ViewportBit);
			harness.Context.Viewport(10, 10, 20, 20);
			harness.DrawTriangle();
			harness.Context.PopAttrib();
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var viewports = harness.Device.CommandsOf<SetViewportCommand>();
			await Assert.That(viewports.Count).IsEqualTo(3);
			await Assert.That(viewports[0].Width).IsEqualTo(100f);
			await Assert.That(viewports[1].X).IsEqualTo(10f);
			await Assert.That(viewports[1].Y).IsEqualTo(20f);
			await Assert.That(viewports[2].X).IsEqualTo(0f);
			await Assert.That(viewports[2].Width).IsEqualTo(100f);
			await Assert.That(viewports[2].Height).IsEqualTo(50f);
		}

		[Test]
		public async Task AMidFrameTextureWriteEndsThePassAndTheNextDrawReopensItLoading()
		{
			var harness = GlCompatTestHarness.Create();

			harness.DrawTriangle();
			await Assert.That(harness.Context.Passes.IsPassOpen).IsTrue();

			int texture = harness.Context.GenTexture();
			harness.Context.BindTexture((int)TextureTarget.Texture2D, texture);
			harness.Context.TexImage2D(0, 0, 0, 4, 4, 0, 0x1908, 0, new byte[4 * 4 * 4]);
			await Assert.That(harness.Context.Passes.IsPassOpen).IsFalse();

			harness.DrawTriangle();
			harness.Context.FlushPass();

			var passes = harness.Device.CommandsOf<BeginRenderPassCommand>();
			await Assert.That(passes.Count).IsEqualTo(2);
			await Assert.That(harness.Context.Passes.PassOpenCount).IsEqualTo(2);

			// The second pass loads rather than clears, so the first draw's pixels survive.
			await Assert.That(passes[1].Descriptor.ColorAttachments[0].LoadOp).IsEqualTo(LoadOp.Load);

			// And the upload really did land between the two passes, not inside one.
			int endIndex = harness.Device.Commands.ToList().FindIndex(command => command is EndRenderPassCommand);
			int writeIndex = harness.Device.Commands.ToList().FindIndex(command => command is WriteTextureCommand);
			int reopenIndex = harness.Device.Commands.ToList().FindLastIndex(command => command is BeginRenderPassCommand);
			await Assert.That(endIndex).IsLessThan(writeIndex);
			await Assert.That(writeIndex).IsLessThan(reopenIndex);
		}

		[Test]
		public async Task SubmitAndReadbackNeverHappenWithAPassOpen()
		{
			// RecordingRenderDevice throws when a pass is open, so this passing is the proof that the
			// EnsurePassOpen/FlushPass pattern actually holds.
			var harness = GlCompatTestHarness.Create();

			harness.DrawTriangle();
			harness.Context.Submit();
			await Assert.That(harness.Context.Passes.IsPassOpen).IsFalse();

			harness.DrawTriangle();
			harness.Context.FlushPass();

			var destination = new byte[64 * 1024];
			await harness.Device.ReadTextureAsync(harness.Target, destination);

			await Assert.That(harness.Device.CommandsOf<ReadTextureCommand>().Count).IsEqualTo(1);
		}

		[Test]
		public async Task ClearOpensAPassWithAClearLoadOpEvenWithNothingDrawn()
		{
			var harness = GlCompatTestHarness.Create();

			harness.Context.ClearColor(0.25, 0.5, 0.75, 1);
			harness.Context.Clear(0x00004000);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			var passes = harness.Device.CommandsOf<BeginRenderPassCommand>();
			await Assert.That(passes.Count).IsEqualTo(1);

			var attachment = passes[0].Descriptor.ColorAttachments[0];
			await Assert.That(attachment.LoadOp).IsEqualTo(LoadOp.Clear);
			await Assert.That(attachment.ClearValue.Red).IsEqualTo(0.25).Within(1e-9);
			await Assert.That(attachment.ClearValue.Blue).IsEqualTo(0.75).Within(1e-9);
		}

		[Test]
		public async Task DrawingWithNoRenderTargetSaysSoRatherThanFailingLater()
		{
			var device = new RecordingRenderDevice();
			var context = new GlCompatContext(device);

			await Assert.That(() =>
			{
				context.Begin(BeginMode.Triangles);
				context.Vertex2(0, 0);
				context.Vertex2(1, 0);
				context.Vertex2(1, 1);
				context.End();
			}).Throws<InvalidOperationException>();
		}
	}
}
