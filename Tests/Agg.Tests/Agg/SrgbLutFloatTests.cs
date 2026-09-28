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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class SrgbLutFloatTests
	{
		/// <summary>
		/// C++ <c>rgba32(srgba8(r, g, b, a))</c> - the compositing demo's colors among them - pinned bit for bit to
		/// a trace built against the reference renderer's headers.
		/// </summary>
		[Test]
		[Arguments(0xFD, 0xF0, 0x6F, 255, 0x3F7B74C6u, 0x3F5F11EAu, 0x3E22C6A1u, 0x3F800000u)]
		[Arguments(0xFE, 0x9F, 0x34, 127, 0x3F7DB8DEu, 0x3EB18332u, 0x3D0CA7E6u, 0x3EFEFEFFu)]
		[Arguments(0x7F, 0xC1, 0xFF, 191, 0x3E595305u, 0x3F0884CDu, 0x3F800000u, 0x3F3FBFC0u)]
		[Arguments(0x05, 0x00, 0x5F, 191, 0x3AC6EB61u, 0x00000000u, 0x3DEA5D18u, 0x3F3FBFC0u)]
		[Arguments(0xDF, 0xDF, 0xDF, 255, 0x3F3CE7B2u, 0x3F3CE7B2u, 0x3F3CE7B2u, 0x3F800000u)]
		[Arguments(0x00, 0x00, 0x00, 0, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u)]
		[Arguments(0x01, 0x0A, 0x0B, 1, 0x399F22B4u, 0x3B46EB61u, 0x3B5B518Eu, 0x3B808081u)]
		public async Task Rgba32FromSrgba8MatchesCpp(int r, int g, int b, int a, uint er, uint eg, uint eb, uint ea)
		{
			ColorF c = SrgbLut.Rgba32FromSrgba8(r, g, b, a);

			await Assert.That(BitConverter.SingleToUInt32Bits(c.red)).IsEqualTo(er);
			await Assert.That(BitConverter.SingleToUInt32Bits(c.green)).IsEqualTo(eg);
			await Assert.That(BitConverter.SingleToUInt32Bits(c.blue)).IsEqualTo(eb);
			await Assert.That(BitConverter.SingleToUInt32Bits(c.alpha)).IsEqualTo(ea);
		}

		/// <summary>
		/// C++ <c>srgba8(rgba32)</c> - how the compositing demo's float frame becomes 8-bit pixels - pinned to the same
		/// trace: channels by the binary search over the float inverse table (clamped at both ends), alpha
		/// <c>int8u(0.5 + a * 255)</c>.
		/// </summary>
		[Test]
		[Arguments(0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0, 0, 0, 0)]
		[Arguments(0x3F800000u, 0x3F800000u, 0x3F800000u, 0x3F800000u, 255, 255, 255, 255)]
		[Arguments(0x3F000000u, 0x3E800000u, 0x3F400000u, 0x3F000000u, 188, 137, 225, 128)]
		[Arguments(0x3A83126Fu, 0x3C23D70Au, 0x3E4CCCCDu, 0x3E99999Au, 3, 25, 124, 77)]
		[Arguments(0x3F666666u, 0x3F4CCCCDu, 0x3B4D2E1Cu, 0x3F7F7CEEu, 243, 231, 10, 254)]
		[Arguments(0xBDCCCCCDu, 0x3F99999Au, 0x3E5B2D97u, 0x3F333333u, 0, 255, 127, 179)]
		public async Task Srgba8FromRgba32MatchesCpp(uint r, uint g, uint b, uint a, int er, int eg, int eb, int ea)
		{
			var c = new ColorF(BitConverter.UInt32BitsToSingle(r), BitConverter.UInt32BitsToSingle(g), BitConverter.UInt32BitsToSingle(b), BitConverter.UInt32BitsToSingle(a));

			Color s = SrgbLut.Srgba8FromRgba32(c);

			await Assert.That(s.red).IsEqualTo((byte)er);
			await Assert.That(s.green).IsEqualTo((byte)eg);
			await Assert.That(s.blue).IsEqualTo((byte)eb);
			await Assert.That(s.alpha).IsEqualTo((byte)ea);
		}
	}
}
