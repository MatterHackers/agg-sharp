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

namespace MatterHackers.RenderCore.Testing
{
	/// <summary>
	/// The encoder <see cref="RecordingRenderDevice"/> hands out. Every call lands in the device's one
	/// command list rather than a per-pass list, so a test reads draws in the order they happened
	/// relative to buffer writes, pass boundaries and submits - the interleaving is usually the thing
	/// under test.
	/// </summary>
	public class RecordingRenderEncoder : IRenderEncoder
	{
		private readonly RecordingRenderDevice device;

		/// <summary>Creates an encoder. Called by <see cref="RecordingRenderDevice.BeginRenderPass"/>.</summary>
		/// <param name="device">The device recording this pass.</param>
		/// <param name="label">Readable name for dumps.</param>
		/// <param name="descriptor">The descriptor the pass was opened with.</param>
		internal RecordingRenderEncoder(RecordingRenderDevice device, string label, in RenderPassDescriptor descriptor)
		{
			this.device = device;
			this.Label = label;
			this.Descriptor = descriptor;
		}

		/// <summary>Readable name for dumps.</summary>
		public string Label { get; }

		/// <summary>The descriptor this pass was opened with.</summary>
		public RenderPassDescriptor Descriptor { get; }

		/// <summary>True once the pass has ended.</summary>
		public bool IsEnded { get; private set; }

		/// <inheritdoc/>
		public void SetPipeline(IRenderPipeline pipeline)
		{
			this.ThrowIfEnded();
			this.device.Record(new SetPipelineCommand(this, pipeline));
		}

		/// <inheritdoc/>
		public void SetBindGroup(int index, IBindGroup bindGroup)
		{
			this.ThrowIfEnded();
			this.device.Record(new SetBindGroupCommand(this, index, bindGroup));
		}

		/// <inheritdoc/>
		public void SetVertexBuffer(int slot, IGpuBuffer buffer, ulong offset = 0)
		{
			this.ThrowIfEnded();
			this.device.Record(new SetVertexBufferCommand(this, slot, buffer, offset));
		}

		/// <inheritdoc/>
		public void SetIndexBuffer(IGpuBuffer buffer, IndexFormat format, ulong offset = 0)
		{
			this.ThrowIfEnded();
			this.device.Record(new SetIndexBufferCommand(this, buffer, format, offset));
		}

		/// <inheritdoc/>
		public void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
		{
			this.ThrowIfEnded();
			this.device.Record(new SetViewportCommand(this, x, y, width, height, minDepth, maxDepth));
		}

		/// <inheritdoc/>
		public void SetScissor(int x, int y, int width, int height)
		{
			this.ThrowIfEnded();
			this.device.Record(new SetScissorCommand(this, x, y, width, height));
		}

		/// <inheritdoc/>
		public void Draw(int vertexCount, int firstVertex = 0)
		{
			this.ThrowIfEnded();
			this.device.Record(new DrawCommand(this, vertexCount, firstVertex));
		}

		/// <inheritdoc/>
		public void DrawIndexed(int indexCount, int firstIndex = 0, int baseVertex = 0)
		{
			this.ThrowIfEnded();
			this.device.Record(new DrawIndexedCommand(this, indexCount, firstIndex, baseVertex));
		}

		/// <summary>Ends the pass. Disposing twice is a no-op, matching a <c>using</c> inside a helper.</summary>
		public void Dispose()
		{
			if (this.IsEnded)
			{
				return;
			}

			this.IsEnded = true;
			this.device.EndPass(this);
		}

		/// <inheritdoc/>
		public override string ToString() => this.Label;

		private void ThrowIfEnded()
		{
			if (this.IsEnded)
			{
				throw new InvalidOperationException($"Render pass '{this.Label}' has already ended.");
			}
		}
	}
}
