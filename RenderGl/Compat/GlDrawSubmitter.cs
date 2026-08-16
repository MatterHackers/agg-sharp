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
		private readonly IRenderDevice device;
		private readonly GlStateShadow state;
		private readonly GlMatrixStacks matrices;
		private readonly GlPipelineCache pipelines;
		private readonly GlTextureStore textures;
		private readonly GlRenderPassScope passes;

		// One uniform buffer per draw, recycled between submits. A single shared buffer would be wrong:
		// queue writes are ordered against submits, not against the draws recorded into an open pass, so
		// every draw in a pass would end up reading whichever write landed last.
		private readonly List<IGpuBuffer> uniformPool = new List<IGpuBuffer>();
		private int uniformPoolInUse;

		// The same pool, and the same rule, for the immediate mode vertex data. A slot is handed out at
		// most once per submit window, so no two draws in one pass ever share a buffer.
		private readonly List<IGpuBuffer> vertexPool = new List<IGpuBuffer>();
		private int vertexPoolInUse;

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
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.state = state ?? throw new ArgumentNullException(nameof(state));
			this.matrices = matrices ?? throw new ArgumentNullException(nameof(matrices));
			this.pipelines = pipelines ?? throw new ArgumentNullException(nameof(pipelines));
			this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
			this.passes = passes ?? throw new ArgumentNullException(nameof(passes));
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
		public void Draw(IGpuBuffer vertexBuffer, int vertexCount, BeginMode mode, bool textured)
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

			IGpuBuffer uniformBuffer;
			using (FrameProfiler.Time("Draw.Uniform"))
			{
				uniformBuffer = this.AcquireUniformBuffer();
				this.device.WriteBuffer(uniformBuffer, 0, this.BuildUniformBlock());
			}

			IBindGroup bindGroup;
			using (FrameProfiler.Time("Draw.BindGroup"))
			{
				var entries = new List<BindGroupEntry>
				{
					BindGroupEntry.ForBuffer(GlShaderKeys.UniformBinding, uniformBuffer, 0, GlUniformBlock.SizeInBytes),
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
				encoder.SetVertexBuffer(0, vertexBuffer);
				encoder.Draw(vertexCount);
			}
		}

		/// <summary>
		/// Takes a pooled vertex buffer big enough for <paramref name="vertices"/> and fills it. The
		/// buffer belongs to the pool, not the caller, and stays alive until the next
		/// <see cref="ResetPerDrawPools"/> makes it available again - which is the whole point: creating
		/// one buffer per flush leaked a GPU allocation for every batch of every frame.
		/// <para>
		/// The write happens while a pass may be open, which is only safe because a slot is handed out at
		/// most once between submits: queue writes are ordered against the submit rather than against the
		/// draws around them, so reusing a buffer inside one submit window would let the later batch's
		/// vertices appear in the earlier batch's draw.
		/// </para>
		/// </summary>
		/// <param name="vertices">The interleaved vertex bytes to upload.</param>
		public IGpuBuffer AcquireVertexBuffer(byte[] vertices)
		{
			if (vertices == null)
			{
				throw new ArgumentNullException(nameof(vertices));
			}

			var buffer = this.AcquireVertexBufferOfAtLeast(vertices.Length);
			this.device.WriteBuffer(buffer, 0, vertices);
			return buffer;
		}

		/// <summary>
		/// Makes the per-draw uniform and vertex buffers reusable again. Safe only immediately after a
		/// submit, because queue writes issued after a submit are ordered after it.
		/// </summary>
		public void ResetPerDrawPools()
		{
			this.uniformPoolInUse = 0;
			this.vertexPoolInUse = 0;
		}

		/// <summary>
		/// Fills the uniform block from the current matrices and lights, in the layout
		/// <see cref="GlUniformBlock"/> declares.
		/// </summary>
		public byte[] BuildUniformBlock()
		{
			var bytes = new byte[GlUniformBlock.SizeInBytes];
			var span = bytes.AsSpan();

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

			return bytes;
		}

		/// <summary>Releases the pooled uniform and vertex buffers.</summary>
		public void Dispose()
		{
			foreach (var buffer in this.uniformPool)
			{
				buffer.Dispose();
			}

			foreach (var buffer in this.vertexPool)
			{
				buffer.Dispose();
			}

			this.uniformPool.Clear();
			this.vertexPool.Clear();
			this.uniformPoolInUse = 0;
			this.vertexPoolInUse = 0;
		}

		private IGpuBuffer AcquireUniformBuffer()
		{
			if (this.uniformPoolInUse < this.uniformPool.Count)
			{
				return this.uniformPool[this.uniformPoolInUse++];
			}

			FrameProfiler.Count("UniformBufferCreate");
			var buffer = this.device.CreateBuffer(
				BufferUsage.Uniform | BufferUsage.CopyDst,
				GlUniformBlock.SizeInBytes);
			this.uniformPool.Add(buffer);
			this.uniformPoolInUse++;
			return buffer;
		}

		/// <summary>
		/// The vertex half of the pool. Slots grow but never shrink, and capacities are rounded up to a
		/// power of two so a slot that sees steadily larger batches settles instead of being recreated on
		/// every flush - unlike the uniform block, immediate mode batch sizes vary from draw to draw.
		/// </summary>
		/// <param name="sizeInBytes">How many bytes the batch needs.</param>
		private IGpuBuffer AcquireVertexBufferOfAtLeast(int sizeInBytes)
		{
			int slot = this.vertexPoolInUse++;
			if (slot < this.vertexPool.Count)
			{
				var pooled = this.vertexPool[slot];
				if (pooled.SizeInBytes >= (ulong)sizeInBytes)
				{
					return pooled;
				}

				pooled.Dispose();
				this.vertexPool[slot] = this.CreateVertexBuffer(sizeInBytes);
				return this.vertexPool[slot];
			}

			var buffer = this.CreateVertexBuffer(sizeInBytes);
			this.vertexPool.Add(buffer);
			return buffer;
		}

		private IGpuBuffer CreateVertexBuffer(int sizeInBytes)
		{
			FrameProfiler.Count("VertexBufferCreate");
			return this.device.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, RoundUpCapacity(sizeInBytes));
		}

		private static ulong RoundUpCapacity(int sizeInBytes)
		{
			// 256 is the floor because a buffer that small costs nothing and most 2D batches are quads.
			ulong capacity = 256;
			while (capacity < (ulong)sizeInBytes)
			{
				capacity *= 2;
			}

			return capacity;
		}
	}
}
