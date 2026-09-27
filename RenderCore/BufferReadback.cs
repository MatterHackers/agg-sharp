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

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// The argument rules of <see cref="IRenderDevice.ReadBufferAsync"/>, in one place so the native
	/// device and the recording double refuse exactly the same calls.
	/// </summary>
	public static class BufferReadback
	{
		/// <summary>
		/// The alignment WebGPU requires of a buffer-to-buffer copy's offset and size
		/// (<c>COPY_BUFFER_ALIGNMENT</c>). Checked here because wgpu would otherwise report the misaligned
		/// copy out of band and the read would return stale bytes.
		/// </summary>
		public const ulong CopyAlignment = 4;

		/// <summary>Throws if a read of <paramref name="length"/> bytes at <paramref name="offset"/> is not a legal readback.</summary>
		/// <param name="source">The buffer to read.</param>
		/// <param name="offset">Byte offset into it.</param>
		/// <param name="length">Bytes to read.</param>
		/// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
		/// <exception cref="ArgumentException">The buffer lacks CopySrc, or the range is misaligned or out of bounds.</exception>
		public static void Validate(IGpuBuffer source, ulong offset, int length)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			if ((source.Usage & BufferUsage.CopySrc) == 0)
			{
				throw new ArgumentException(
					$"Buffer '{source.Label}' was created without BufferUsage.CopySrc, so it cannot be read back.",
					nameof(source));
			}

			if (offset % CopyAlignment != 0 || (ulong)length % CopyAlignment != 0)
			{
				throw new ArgumentException(
					$"A buffer read's offset ({offset}) and length ({length}) must both be multiples of {CopyAlignment}.",
					nameof(offset));
			}

			if (offset > source.SizeInBytes || (ulong)length > source.SizeInBytes - offset)
			{
				throw new ArgumentException(
					$"Reading {length} bytes at offset {offset} runs past the end of the {source.SizeInBytes} byte buffer '{source.Label}'.",
					nameof(offset));
			}
		}
	}
}
