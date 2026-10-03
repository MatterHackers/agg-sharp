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

namespace MatterHackers.VectorMath
{
	/// <summary>
	/// FNV-1a (64-bit) over a value's bytes, as <see cref="Vector4.ComputeHash"/> of <c>BitConverter.GetBytes</c>
	/// computes it, but with the bytes on the stack.
	/// </summary>
	/// <remarks>
	/// Vector hashes run per coordinate when a shape is hashed for the GPU tessellation cache, every frame,
	/// and a byte[] per value was most of what a steady-state frame allocated.
	/// </remarks>
	internal static class LongHash
	{
		public static ulong Of(double data, ulong hash)
		{
			Span<byte> bytes = stackalloc byte[sizeof(double)];
			BitConverter.TryWriteBytes(bytes, data);
			foreach (byte value in bytes)
			{
				hash = (hash ^ value) * 0x100000001b3;
			}

			return hash;
		}
	}
}
