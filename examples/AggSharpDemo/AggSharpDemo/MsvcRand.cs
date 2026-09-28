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

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// The MSVC / C89 reference <c>rand()</c>: a 32-bit LCG (x * 214013 + 2531011) returning bits 16..30.
	/// </summary>
	/// <remarks>
	/// C++ AGG's examples draw their random shapes from the C library's <c>rand()</c>, whose sequence differs
	/// between platforms. The headless C++ reference renderer draws them from this generator instead
	/// (<c>agg::msvc_rand</c> in tools/cpp-renderer/src/flash_shape.h), taking each value in argument order,
	/// so a demo that does the same lines up with its golden. A new instance starts where an unseeded
	/// <c>rand()</c> does (seed 1).
	/// </remarks>
	public class MsvcRand
	{
		private uint holdRand;

		public MsvcRand(uint seed = 1)
		{
			this.holdRand = seed;
		}

		/// <summary>The next value, 0 to 32767, as C <c>rand()</c> returns it.</summary>
		public int Next()
		{
			unchecked
			{
				this.holdRand = (this.holdRand * 214013u) + 2531011u;
			}

			return (int)((this.holdRand >> 16) & 0x7fff);
		}
	}
}
