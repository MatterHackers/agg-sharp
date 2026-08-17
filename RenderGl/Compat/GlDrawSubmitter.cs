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
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// Turns an already interleaved vertex buffer plus the current GL state into one retained draw:
	/// pipeline, uniform write, bind group, encoder calls.
	/// <para>
	/// This is the part of the port that is genuinely new code rather than a move. The classic path's
	/// equivalent (its <c>FlushColoredVertices</c>/<c>FlushTexturedVertices</c>) is pure D3D11 -
	/// Map/WriteDiscard, IASetInputLayout, VS/PSSetShader, Draw - and none of it survives contact with
	/// an immutable-pipeline API. The accumulation above it, on the other hand, is ported verbatim.
	/// </para>
	/// </summary>
	public class GlDrawSubmitter : IDisposable
	{
		private readonly GlStateShadow state;
		private readonly GlMatrixStacks matrices;
		private readonly GlPipelineCache pipelines;
		private readonly GlTextureStore textures;
		private readonly GlRenderPassScope passes;

		/// <summary>
		/// Bytes between one draw's uniform block and the next. A bound range's offset must be a multiple
		/// of WebGPU's guaranteed minUniformBufferOffsetAlignment (256), and a block is
		/// <see cref="GlUniformBlock.SizeInBytes"/> = 304 bytes, so the next multiple up is 512.
		/// </summary>
		public const int UniformStride = 512;

		/// <summary>
		/// A compile time cross-check, not a value anything reads: growing
		/// <see cref="GlUniformBlock.SizeInBytes"/> past the stride would make each draw's block overwrite
		/// the start of the next draw's slot, and unsigned is what turns the resulting negative constant
		/// into a build error instead of a rendering mystery.
		/// </summary>
		private const uint UniformStrideHeadroom = UniformStride - GlUniformBlock.SizeInBytes;

		/// <summary>Draw slots per uniform buffer. A busy 2D frame records a few hundred draws.</summary>
		private const int UniformSlotsPerBuffer = 512;

		// One uniform slot per draw, staged in a CPU array and pushed to the GPU in one write per submit
		// rather than one per draw - see StagedUniformBuffers for why that is both safe and worth doing.
		private readonly StagedUniformBuffers uniforms;
		private int uniformSlotsInUse;

		/// <summary>
		/// Bytes per immediate mode vertex buffer. A busy 2D frame stages a few hundred KB across a few
		/// hundred batches, so one buffer normally holds the whole frame and the flush is one write.
		/// </summary>
		private const int VertexBytesPerBuffer = 1 << 20;

		// The same batching, and the same rule, for the immediate mode vertex data: every batch gets its
		// own range within a submit window, but all the ranges reach the GPU in one write per buffer.
		private readonly StagedVertexBuffers vertices;

		/// <summary>Creates a submitter over the pieces of context state a draw reads.</summary>
		/// <param name="device">The device draws are recorded on.</param>
		/// <param name="state">The shadowed GL state.</param>
		/// <param name="matrices">The matrix stacks.</param>
		/// <param name="pipelines">The pipeline and bind group caches.</param>
		/// <param name="textures">The texture store, for resolving the bound texture and its sampler.</param>
		/// <param name="passes">The pass scope a draw is recorded into.</param>
		public GlDrawSubmitter(
			IRenderDevice device,
			GlStateShadow state,
			GlMatrixStacks matrices,
			GlPipelineCache pipelines,
			GlTextureStore textures,
			GlRenderPassScope passes)
		{
			// The device is not held: the two stagers own every call this makes on it, and they null check it.
			this.state = state ?? throw new ArgumentNullException(nameof(state));
			this.matrices = matrices ?? throw new ArgumentNullException(nameof(matrices));
			this.pipelines = pipelines ?? throw new ArgumentNullException(nameof(pipelines));
			this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
			this.passes = passes ?? throw new ArgumentNullException(nameof(passes));
			this.uniforms = new StagedUniformBuffers(
				device,
				UniformStride,
				UniformSlotsPerBuffer,
				"UniformBufferCreate");
			this.vertices = new StagedVertexBuffers(device, VertexBytesPerBuffer, "VertexBufferCreate");
		}

		/// <summary>
		/// Records one draw, resolving pipeline and bind group from live state. Called both by the
		/// immediate mode flush and by display list replay - which is precisely why a display list can
		/// bake geometry without baking the pipeline.
		/// </summary>
		/// <param name="vertexBuffer">The interleaved vertices.</param>
		/// <param name="vertexCount">How many vertices to draw.</param>
		/// <param name="mode">The GL primitive mode, already fan-converted.</param>
		/// <param name="textured">Whether the draw samples the bound texture.</param>
		/// <param name="vertexOffset">
		/// Byte offset of the first vertex. Non-zero for immediate mode, whose batches share a staged
		/// buffer; zero for a display list, whose baked buffer holds one batch.
		/// </param>
		public void Draw(IGpuBuffer vertexBuffer, int vertexCount, BeginMode mode, bool textured, ulong vertexOffset = 0)
		{
			FrameProfiler.Count("Draws");

			IRenderPipeline pipeline;
			using (FrameProfiler.Time("Draw.Pipeline"))
			{
				var descriptor = this.pipelines.BuildPipelineDescriptor(
					this.state,
					this.passes.ColorFormat,
					this.passes.DepthFormat,
					GlStateShadow.MapTopology(mode),
					textured,
					false);

				pipeline = this.pipelines.GetPipeline(descriptor);
			}

			int uniformSlot;
			using (FrameProfiler.Time("Draw.Uniform"))
			{
				uniformSlot = this.uniformSlotsInUse++;
				this.BuildUniformBlock(this.uniforms.Stage(uniformSlot, 0, GlUniformBlock.SizeInBytes));
			}

			IBindGroup bindGroup;
			using (FrameProfiler.Time("Draw.BindGroup"))
			{
				var entries = new List<BindGroupEntry>
				{
					BindGroupEntry.ForBuffer(
						GlShaderKeys.UniformBinding,
						this.uniforms.BufferFor(uniformSlot),
						this.uniforms.OffsetFor(uniformSlot),
						GlUniformBlock.SizeInBytes),
				};

				if (textured)
				{
					var entry = this.textures.Find(this.state.BoundTexture(0));
					entries.Add(BindGroupEntry.ForTexture(GlShaderKeys.TextureBinding, entry.Texture));
					entries.Add(BindGroupEntry.ForSampler(GlShaderKeys.SamplerBinding, this.textures.GetSampler(entry)));
				}

				bindGroup = this.pipelines.GetBindGroup(
					new BindGroupDescriptor(pipeline, GlShaderKeys.BindGroupIndex, entries.ToArray()));
			}

			using (FrameProfiler.Time("Draw.Encode"))
			{
				var encoder = this.passes.EnsurePassOpen();
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup((int)GlShaderKeys.BindGroupIndex, bindGroup);
				encoder.SetVertexBuffer(0, vertexBuffer, vertexOffset);
				encoder.Draw(vertexCount);
			}
		}

		/// <summary>
		/// Stages a batch's vertex bytes and reports where they will live: the shared buffer they were
		/// appended to and their offset within it. Nothing is uploaded here - the whole submit window's
		/// vertices go up in one write per buffer at <see cref="FlushPendingWrites"/>.
		/// <para>
		/// Every batch still gets a range of its own, which is what makes deferring the write correct:
		/// queue writes are ordered against the submit rather than against the draws around them, so
		/// reusing a range inside one submit window would let the later batch's vertices appear in the
		/// earlier batch's draw.
		/// </para>
		/// </summary>
		/// <param name="vertices">The interleaved vertex bytes to upload.</param>
		/// <param name="offset">The byte offset the batch will occupy in the returned buffer.</param>
		public IGpuBuffer AcquireVertexBuffer(byte[] vertices, out ulong offset)
		{
			if (vertices == null)
			{
				throw new ArgumentNullException(nameof(vertices));
			}

			var span = this.vertices.Stage(vertices.Length, out var buffer, out offset);
			vertices.AsSpan().CopyTo(span);
			return buffer;
		}

		/// <summary>
		/// Makes the per-draw uniform and vertex ranges reusable again. Safe only immediately after a
		/// submit, because queue writes issued after a submit are ordered after it.
		/// </summary>
		public void ResetPerDrawPools()
		{
			this.uniformSlotsInUse = 0;
			this.uniforms.Reset();
			this.vertices.Reset();
		}

		/// <summary>
		/// Pushes every uniform block and vertex range staged since the last flush to the GPU. Must be
		/// called before the submit that consumes the draws they belong to.
		/// </summary>
		public void FlushPendingWrites()
		{
			this.uniforms.Flush(this.uniformSlotsInUse);
			this.vertices.Flush();
		}

		/// <summary>
		/// Fills the uniform block from the current matrices and lights, in the layout
		/// <see cref="GlUniformBlock"/> declares.
		/// </summary>
		/// <param name="span">Exactly <see cref="GlUniformBlock.SizeInBytes"/> bytes to fill.</param>
		public void BuildUniformBlock(Span<byte> span)
		{
			GlUniformBlock.WriteMatrix(span, GlUniformBlock.ModelViewMatrixOffset, this.matrices.ModelView);
			GlUniformBlock.WriteMatrix(
				span,
				GlUniformBlock.ProjectionMatrixOffset,
				GlUniformBlock.ToClipSpaceProjection(this.matrices.Projection));
			GlUniformBlock.WriteMatrix(span, GlUniformBlock.TextureMatrixOffset, this.matrices.Texture);

			GlUniformBlock.WriteVector4(span, GlUniformBlock.Light0PositionOffset, this.state.Lights[0].Position);
			GlUniformBlock.WriteVector4(span, GlUniformBlock.Light0AmbientOffset, this.state.Lights[0].Ambient);
			GlUniformBlock.WriteVector4(span, GlUniformBlock.Light0DiffuseOffset, this.state.Lights[0].Diffuse);
			GlUniformBlock.WriteVector4(span, GlUniformBlock.Light1PositionOffset, this.state.Lights[1].Position);
			GlUniformBlock.WriteVector4(span, GlUniformBlock.Light1AmbientOffset, this.state.Lights[1].Ambient);
			GlUniformBlock.WriteVector4(span, GlUniformBlock.Light1DiffuseOffset, this.state.Lights[1].Diffuse);

			GlUniformBlock.WriteVector4(
				span,
				GlUniformBlock.FlagsOffset,
				this.state.IsEnabled(EnableCap.Light0) ? 1f : 0f,
				this.state.IsEnabled(EnableCap.Light1) ? 1f : 0f,
				this.state.LightingEnabled ? 1f : 0f,
				this.state.TextureEnvironmentReplace ? 1f : 0f);
		}

		/// <summary>Releases the staged uniform and vertex buffers.</summary>
		public void Dispose()
		{
			this.uniforms.Dispose();
			this.vertices.Dispose();
			this.uniformSlotsInUse = 0;
		}
	}
}
