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
	/// The compute encoder <see cref="RecordingRenderDevice"/> hands out. Like
	/// <see cref="RecordingRenderEncoder"/>, every call lands in the device's one command list, so a test
	/// reads dispatches in order relative to buffer writes, pass boundaries, submits and readbacks.
	/// </summary>
	public class RecordingComputeEncoder : IComputeEncoder
	{
		private readonly RecordingRenderDevice device;

		/// <summary>Creates an encoder. Called by <see cref="RecordingRenderDevice.BeginComputePass"/>.</summary>
		/// <param name="device">The device recording this pass.</param>
		/// <param name="label">Readable name for dumps.</param>
		internal RecordingComputeEncoder(RecordingRenderDevice device, string label)
		{
			this.device = device;
			this.Label = label;
		}

		/// <summary>Readable name for dumps.</summary>
		public string Label { get; }

		/// <summary>True once the pass has ended.</summary>
		public bool IsEnded { get; private set; }

		/// <inheritdoc/>
		public void SetPipeline(IComputePipeline pipeline)
		{
			this.ThrowIfEnded();
			ThrowIfDisposed(pipeline ?? throw new ArgumentNullException(nameof(pipeline)));
			this.device.Record(new SetComputePipelineCommand(this, pipeline));
		}

		/// <inheritdoc/>
		public void SetBindGroup(int index, IBindGroup bindGroup)
		{
			this.ThrowIfEnded();
			ThrowIfDisposed(bindGroup ?? throw new ArgumentNullException(nameof(bindGroup)));
			this.device.Record(new SetComputeBindGroupCommand(this, index, bindGroup));
		}

		/// <inheritdoc/>
		public void Dispatch(uint workgroupCountX, uint workgroupCountY = 1, uint workgroupCountZ = 1)
		{
			this.ThrowIfEnded();
			ComputeDispatch.Validate(this.device.Limits, workgroupCountX, workgroupCountY, workgroupCountZ);
			this.device.Record(new DispatchCommand(this, workgroupCountX, workgroupCountY, workgroupCountZ));
		}

		/// <summary>Ends the pass. Disposing twice is a no-op, matching the render encoder.</summary>
		public void Dispose()
		{
			if (this.IsEnded)
			{
				return;
			}

			this.IsEnded = true;
			this.device.EndComputePass(this);
		}

		/// <inheritdoc/>
		public override string ToString() => this.Label;

		/// <summary>
		/// Refuses a disposed resource as the native encoder does - there, its released handle would reach
		/// wgpu-native as null and abort the process.
		/// </summary>
		/// <param name="resource">The resource about to be bound.</param>
		internal static void ThrowIfDisposed(IGpuResource resource)
		{
			if (resource is StubResource stub && stub.IsDisposed)
			{
				throw new ObjectDisposedException(stub.Label, $"'{stub.Label}' has been disposed.");
			}
		}

		private void ThrowIfEnded()
		{
			if (this.IsEnded)
			{
				throw new InvalidOperationException($"Compute pass '{this.Label}' has already ended.");
			}
		}
	}
}
