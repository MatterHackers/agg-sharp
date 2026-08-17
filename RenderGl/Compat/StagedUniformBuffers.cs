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
	/// Per-draw uniform data for a whole submit window, staged in a CPU array and pushed to a small
	/// number of big GPU buffers with one queue write per buffer per flush.
	/// <para>
	/// Every draw still needs its own range - queue writes are ordered against submits, not against the
	/// draws recorded into an open pass, so two draws sharing a range would both read whichever write
	/// landed last - but it does not need its own <i>write</i>. That distinction is the whole point:
	/// wgpuQueueWriteBuffer costs ~13 us a call in wgpu-native, and a busy MatterCAD frame made ~2,650 of
	/// them, 35 ms of a 64 ms frame.
	/// </para>
	/// <para>
	/// Slots live in fixed size buffers rather than in one buffer that is reallocated as it grows.
	/// Reallocating would strand the draws already recorded against the old buffer, and would also need
	/// the device's maxBufferSize watched; appending a buffer instead means a slot's buffer and offset
	/// never move once handed out, so nothing has to be copied and the bind groups built over a slot stay
	/// valid for the life of the owner.
	/// </para>
	/// </summary>
	public sealed class StagedUniformBuffers : IDisposable
	{
		private readonly IRenderDevice device;
		private readonly int slotStride;
		private readonly int slotsPerBuffer;
		private readonly string createCounterName;
		private readonly List<IGpuBuffer> buffers = new List<IGpuBuffer>();

		private byte[] staging = Array.Empty<byte>();

		// The first slot whose staged bytes have not reached the GPU yet. A frame can submit several
		// times, and each flush only writes what was staged since the previous one.
		private int flushBase;

		/// <summary>Creates a staging pool.</summary>
		/// <param name="device">The device the buffers are created on and the writes are queued to.</param>
		/// <param name="slotStride">
		/// Bytes per draw slot. Must be a multiple of WebGPU's guaranteed minUniformBufferOffsetAlignment
		/// (256), because a slot's offset is what gets bound.
		/// </param>
		/// <param name="slotsPerBuffer">
		/// Slots per buffer, clamped down if the device's maxBufferSize will not hold that many.
		/// </param>
		/// <param name="createCounterName">The <see cref="FrameProfiler"/> counter to bump per buffer created.</param>
		public StagedUniformBuffers(IRenderDevice device, int slotStride, int slotsPerBuffer, string createCounterName)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.slotStride = slotStride;
			this.createCounterName = createCounterName;

			int affordable = (int)Math.Min(int.MaxValue, device.Limits.MaxBufferSize / (ulong)slotStride);
			this.slotsPerBuffer = Math.Max(1, Math.Min(slotsPerBuffer, affordable));
		}

		/// <summary>How many slots the buffers created so far hold. Reported by the frame profiler.</summary>
		public int SlotCapacity => this.buffers.Count * this.slotsPerBuffer;

		/// <summary>
		/// The staging bytes of one block of one slot, ready to be filled. Nothing reaches the GPU until
		/// <see cref="Flush"/>.
		/// <para>
		/// The span must be consumed before the next <see cref="Stage"/> call: growing the pool resizes
		/// the staging array, and a span handed out earlier would then point into the orphaned copy and
		/// swallow the writes made through it.
		/// </para>
		/// </summary>
		/// <param name="slot">The draw's slot index.</param>
		/// <param name="blockOffset">Byte offset of the block within the slot.</param>
		/// <param name="sizeInBytes">Size of the block.</param>
		public Span<byte> Stage(int slot, int blockOffset, int sizeInBytes)
		{
			// Loudly, because the failure it prevents is silent: a block running past the stride would
			// scribble over the next draw's slot, and the only symptom would be one draw's uniforms
			// showing up in another's. The obvious way to get here is growing a block past the stride.
			if (blockOffset < 0 || sizeInBytes < 0 || blockOffset + sizeInBytes > this.slotStride)
			{
				throw new ArgumentOutOfRangeException(
					nameof(sizeInBytes),
					$"A {sizeInBytes} byte block at offset {blockOffset} does not fit a {this.slotStride} byte slot.");
			}

			this.EnsureSlot(slot);
			return this.staging.AsSpan((slot * this.slotStride) + blockOffset, sizeInBytes);
		}

		/// <summary>The buffer a slot lives in.</summary>
		/// <param name="slot">The draw's slot index.</param>
		public IGpuBuffer BufferFor(int slot)
		{
			this.EnsureSlot(slot);
			return this.buffers[slot / this.slotsPerBuffer];
		}

		/// <summary>The byte offset of a slot within <see cref="BufferFor"/>'s buffer.</summary>
		/// <param name="slot">The draw's slot index.</param>
		public ulong OffsetFor(int slot) => (ulong)((slot % this.slotsPerBuffer) * this.slotStride);

		/// <summary>
		/// Pushes everything staged since the last flush, one write per buffer it spans. Must be called
		/// before the device submit that consumes the draws those slots belong to - queue writes are
		/// ordered against the submit, not against the draws, so this is exactly equivalent to the
		/// per-draw writes it replaces.
		/// </summary>
		/// <param name="slotsUsed">How many slots have been handed out in this submit window.</param>
		public void Flush(int slotsUsed)
		{
			while (this.flushBase < slotsUsed)
			{
				int bufferIndex = this.flushBase / this.slotsPerBuffer;
				int endSlot = Math.Min(slotsUsed, (bufferIndex + 1) * this.slotsPerBuffer);

				int start = this.flushBase * this.slotStride;
				int end = endSlot * this.slotStride;
				this.device.WriteBuffer(
					this.buffers[bufferIndex],
					this.OffsetFor(this.flushBase),
					this.staging.AsSpan(start, end - start));

				this.flushBase = endSlot;
			}
		}

		/// <summary>
		/// Forgets what has been flushed, because the owner is about to hand slot 0 out again. Safe only
		/// immediately after a submit, which is what makes a slot's bytes rewritable at all.
		/// </summary>
		public void Reset() => this.flushBase = 0;

		/// <summary>Releases every buffer.</summary>
		public void Dispose()
		{
			foreach (var buffer in this.buffers)
			{
				buffer.Dispose();
			}

			this.buffers.Clear();
			this.staging = Array.Empty<byte>();
			this.flushBase = 0;
		}

		private void EnsureSlot(int slot)
		{
			while (slot >= this.SlotCapacity)
			{
				FrameProfiler.Count(this.createCounterName);
				this.buffers.Add(this.device.CreateBuffer(
					BufferUsage.Uniform | BufferUsage.CopyDst,
					(ulong)this.slotsPerBuffer * (ulong)this.slotStride));

				Array.Resize(ref this.staging, this.SlotCapacity * this.slotStride);
			}
		}
	}
}
