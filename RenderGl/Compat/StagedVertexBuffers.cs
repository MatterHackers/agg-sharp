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

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// Per-draw vertex data for a whole submit window, appended into a CPU array and pushed to a small
	/// number of big GPU buffers with one queue write per buffer per flush.
	/// <para>
	/// The reasoning is <see cref="StagedUniformBuffers"/>'s, applied to the other half of a 2D draw:
	/// every batch still needs its own range - a queue write is ordered against the submit, not against
	/// the draws recorded into an open pass, so two batches sharing a range would both render whichever
	/// write landed last - but it does not need its own <i>write</i>. wgpuQueueWriteBuffer costs ~13 us a
	/// call, and a busy MatterCAD frame made ~320 of them for immediate mode vertices alone.
	/// </para>
	/// <para>
	/// Unlike a uniform block, batch sizes vary wildly, so ranges are appended at a moving cursor rather
	/// than laid out in fixed slots. Buffers are appended rather than reallocated for the same reason as
	/// the uniform pool: a range's buffer and offset must never move once handed out, because a draw has
	/// already been recorded against it.
	/// </para>
	/// </summary>
	public sealed class StagedVertexBuffers : IDisposable
	{
		/// <summary>
		/// WebGPU requires a bound vertex buffer offset, and both the offset and size of a queue write, to
		/// be a multiple of 4. The immediate mode interleaves are 28 and 36 bytes, so in practice nothing
		/// is ever padded - but the alignment is a rule of the API, not of the layouts, so it is enforced
		/// here rather than assumed.
		/// </summary>
		private const int OffsetAlignment = 4;

		private readonly IRenderDevice device;
		private readonly int bytesPerBuffer;
		private readonly string createCounterName;
		private readonly List<Block> blocks = new List<Block>();

		// One flat address space across every block, so a range is a single slice of one array.
		private byte[] staging = Array.Empty<byte>();
		private int cursor;
		private int current;

		/// <summary>Creates a staging pool.</summary>
		/// <param name="device">The device the buffers are created on and the writes are queued to.</param>
		/// <param name="bytesPerBuffer">
		/// Bytes per buffer, clamped down if the device's maxBufferSize is smaller. A batch bigger than
		/// this gets a buffer of its own rather than being split.
		/// </param>
		/// <param name="createCounterName">The <see cref="FrameProfiler"/> counter to bump per buffer created.</param>
		public StagedVertexBuffers(IRenderDevice device, int bytesPerBuffer, string createCounterName)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.createCounterName = createCounterName;

			int affordable = (int)Math.Min(int.MaxValue, device.Limits.MaxBufferSize);
			this.bytesPerBuffer = AlignUp(Math.Max(OffsetAlignment, Math.Min(bytesPerBuffer, affordable)));
		}

		/// <summary>Total bytes of GPU buffer created so far. Reported by the frame profiler.</summary>
		public int ByteCapacity => this.staging.Length;

		/// <summary>
		/// A range of staging bytes ready to be filled, plus where it will live on the GPU. Nothing
		/// reaches the device until <see cref="Flush"/>.
		/// <para>
		/// The span must be consumed before the next <see cref="Stage"/> call: adding a buffer resizes the
		/// staging array, and a span handed out earlier would then point into the orphaned copy and
		/// swallow the writes made through it.
		/// </para>
		/// </summary>
		/// <param name="sizeInBytes">How many bytes the batch needs.</param>
		/// <param name="buffer">The buffer the range lives in.</param>
		/// <param name="offset">The byte offset of the range within that buffer.</param>
		public Span<byte> Stage(int sizeInBytes, out IGpuBuffer buffer, out ulong offset)
		{
			if (sizeInBytes <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "A staged range needs at least one byte.");
			}

			int start = AlignUp(this.cursor);
			if (this.blocks.Count == 0 || start + sizeInBytes > this.blocks[this.current].End)
			{
				this.MoveToBlockThatFits(sizeInBytes);
				start = this.blocks[this.current].Start;
			}

			var block = this.blocks[this.current];
			this.cursor = start + sizeInBytes;
			block.Used = this.cursor - block.Start;

			buffer = block.Buffer;
			offset = (ulong)(start - block.Start);
			return this.staging.AsSpan(start, sizeInBytes);
		}

		/// <summary>
		/// Pushes everything staged since the last flush, one write per buffer it spans. Must be called
		/// before the device submit that consumes the draws those ranges belong to - queue writes are
		/// ordered against the submit, not against the draws, so this is exactly equivalent to the
		/// per-batch writes it replaces.
		/// </summary>
		public void Flush()
		{
			foreach (var block in this.blocks)
			{
				if (block.Used <= block.Flushed)
				{
					continue;
				}

				// Rounded up because a queue write is a whole number of 4 byte words; the padding bytes are
				// past the last vertex of the last batch in this buffer, so their content does not matter.
				int end = AlignUp(block.Used);
				this.device.WriteBuffer(
					block.Buffer,
					(ulong)block.Flushed,
					this.staging.AsSpan(block.Start + block.Flushed, end - block.Flushed));

				block.Flushed = end;
			}
		}

		/// <summary>
		/// Forgets what has been staged, because the owner is about to hand the first range out again.
		/// Safe only immediately after a submit, which is what makes a range's bytes rewritable at all.
		/// </summary>
		public void Reset()
		{
			this.cursor = 0;
			this.current = 0;
			foreach (var block in this.blocks)
			{
				block.Used = 0;
				block.Flushed = 0;
			}
		}

		/// <summary>Releases every buffer.</summary>
		public void Dispose()
		{
			foreach (var block in this.blocks)
			{
				block.Buffer.Dispose();
			}

			this.blocks.Clear();
			this.staging = Array.Empty<byte>();
			this.cursor = 0;
			this.current = 0;
		}

		private static int AlignUp(int value) => (value + (OffsetAlignment - 1)) & ~(OffsetAlignment - 1);

		/// <summary>
		/// Moves the cursor to the next buffer that can hold a batch, creating one when none can. An
		/// existing buffer too small for the batch is skipped rather than replaced, because replacing it
		/// would move the ranges already handed out of the buffers that follow it.
		/// </summary>
		/// <param name="sizeInBytes">How many bytes the batch needs.</param>
		private void MoveToBlockThatFits(int sizeInBytes)
		{
			int next = this.blocks.Count == 0 ? 0 : this.current + 1;
			while (next < this.blocks.Count && this.blocks[next].Capacity < sizeInBytes)
			{
				next++;
			}

			if (next == this.blocks.Count)
			{
				this.AddBlock(sizeInBytes);
			}

			this.current = next;
			this.cursor = this.blocks[next].Start;
		}

		private void AddBlock(int sizeInBytes)
		{
			FrameProfiler.Count(this.createCounterName);

			int capacity = Math.Max(this.bytesPerBuffer, AlignUp(sizeInBytes));
			int start = this.staging.Length;
			Array.Resize(ref this.staging, start + capacity);

			this.blocks.Add(new Block(
				this.device.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, (ulong)capacity),
				start,
				capacity));
		}

		/// <summary>One GPU buffer and the window of the staging array that mirrors it.</summary>
		private sealed class Block
		{
			internal Block(IGpuBuffer buffer, int start, int capacity)
			{
				this.Buffer = buffer;
				this.Start = start;
				this.Capacity = capacity;
			}

			internal IGpuBuffer Buffer { get; }

			/// <summary>Index in the staging array of this buffer's byte 0.</summary>
			internal int Start { get; }

			internal int Capacity { get; }

			/// <summary>Index in the staging array just past this buffer's last byte.</summary>
			internal int End => this.Start + this.Capacity;

			/// <summary>Bytes staged into this buffer during the current submit window.</summary>
			internal int Used { get; set; }

			/// <summary>How much of <see cref="Used"/> has already been queued to the device.</summary>
			internal int Flushed { get; set; }
		}
	}
}
