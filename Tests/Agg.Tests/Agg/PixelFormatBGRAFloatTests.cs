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
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// PixelFormatBGRAFloat with blender_rgba and blender_rgba_pre pinned bit for bit to C++
	/// pixfmt_alpha_blend_rgba&lt;blender, rendering_buffer&gt; over rgba32 (AGG_BGRA128). One sequence touches each
	/// operation and its full-cover, opaque and transparent paths; the expected buffers are the float bit patterns a
	/// trace printed, built against the reference renderer's patched headers with -ffp-contract=off.
	/// </summary>
	public class PixelFormatBGRAFloatTests
	{
		[Test]
		public async Task StraightBlenderMatchesCpp()
		{
			await AssertSequenceMatches(new BlenderRgbaFloat(), StraightExpected);
		}

		[Test]
		public async Task PremultipliedBlenderMatchesCpp()
		{
			await AssertSequenceMatches(new BlenderRgbaPreFloat(), PreExpected);
		}

		private static async Task AssertSequenceMatches(IBlenderRgbaFloat blender, uint[] expected)
		{
			var pf = new PixelFormatBGRAFloat(new ImageBufferFloat(8, 3, 128, new BlenderBGRAFloat()), blender);
			var pf2 = new PixelFormatBGRAFloat(new ImageBufferFloat(8, 3, 128, new BlenderBGRAFloat()), blender);

			pf.Clear(SrgbLut.Rgba32FromSrgba8(255, 255, 255));
			pf.CopyBar(1, 0, 3, 1, SrgbLut.Rgba32FromSrgba8(0xdf, 0xdf, 0xdf));
			pf.CopyBar(-2, 2, 1, 5, new ColorF(0.2f, 0.3f, 0.4f, 0.5f));
			pf.BlendHline(0, 0, 8, new ColorF(0.8f, 0.4f, 0.2f, 0.7f), 255);
			pf.BlendHline(2, 1, 5, new ColorF(0.1f, 0.9f, 0.3f, 0.6f), 128);
			pf.BlendHline(4, 2, 3, new ColorF(0.5f, 0.5f, 0.5f, 1.0f), 200);
			byte[] covers = { 0, 1, 64, 128, 200, 254, 255, 255 };
			pf.BlendSolidHspan(0, 1, 8, new ColorF(0.3f, 0.6f, 0.9f, 0.45f), covers, 0);
			pf.BlendSolidHspan(0, 2, 8, new ColorF(0.9f, 0.1f, 0.4f, 1.0f), covers, 0);
			ColorF[] cs =
			{
				new ColorF(0, 0, 0, 0), new ColorF(1, 0, 0, 1), new ColorF(0.2f, 0.4f, 0.6f, 0.8f), new ColorF(0.9f, 0.8f, 0.7f, 0.3f),
				new ColorF(0.05f, 0.1f, 0.15f, 0.2f), new ColorF(0.5f, 0.25f, 0.125f, 0.5f), new ColorF(0.3f, 0.3f, 0.3f, 1), new ColorF(0.7f, 0.2f, 0.9f, 0.9f),
			};
			pf.BlendColorHspan(0, 0, 8, cs, 0, covers, 0, 255);
			pf.BlendColorHspan(0, 1, 8, cs, 0, null, 0, 255);
			pf.BlendColorHspan(0, 2, 8, cs, 0, null, 0, 100);
			pf2.Clear(new ColorF(0, 0, 0, 0));
			pf2.BlendColorHspan(0, 0, 8, cs, 0, null, 0, 255);
			pf2.BlendColorHspan(0, 1, 8, cs, 0, covers, 0, 255);
			pf2.BlendColorHspan(1, 2, 6, cs, 0, null, 0, 255);
			pf.BlendFrom(pf2, 0, 0, 191);
			pf.BlendFrom(pf2, 1, 1, 255);

			float[] buffer = pf.Image.GetBuffer();
			await Assert.That(buffer.Length).IsEqualTo(expected.Length);
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(BitConverter.SingleToUInt32Bits(buffer[i])).IsEqualTo(expected[i]).Because($"float {i}");
			}
		}

		private static readonly uint[] StraightExpected =
		{
			0x3EE147AEu, 0x3F147AE2u, 0x3F5C28F6u, 0x3F800000u, 0x3DB9052Au, 0x3E00597Bu, 0x3F720213u, 0x3F800000u,
			0x3EE73FD4u, 0x3EC4E1A0u, 0x3EB97933u, 0x3F800000u, 0x3EBBD974u, 0x3EF47BA3u, 0x3F2E2914u, 0x3F800000u,
			0x3EAE0801u, 0x3EDD3B92u, 0x3F1FE886u, 0x3F800000u, 0x3E4D4E57u, 0x3E9D147Bu, 0x3F04F78Cu, 0x3F800000u,
			0x3E99999Au, 0x3E99999Au, 0x3E99999Au, 0x3F800000u, 0x3F5307DEu, 0x3E4BACC0u, 0x3F287462u, 0x3F800000u,
			0x3F800000u, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x00000000u, 0x00000000u, 0x3F7F4041u, 0x3F800000u,
			0x00000000u, 0x00000000u, 0x3F800000u, 0x3F800000u, 0x3F01D55Eu, 0x3EC99DF4u, 0x3E759E6Cu, 0x3F800000u,
			0x3EFC51E2u, 0x3F001D5Du, 0x3EBF1AA2u, 0x3F800000u, 0x3E885815u, 0x3E9C6B41u, 0x3EAC2F11u, 0x3F800000u,
			0x3E39999Au, 0x3E59999Au, 0x3E8CCCCDu, 0x3F800000u, 0x3E99999Au, 0x3E99999Au, 0x3E99999Au, 0x3F800000u,
			0x3ECCCCCDu, 0x3E99999Au, 0x3E4CCCCDu, 0x3F000000u, 0x3E78F8FAu, 0x3E3A3DC2u, 0x3F03F0DEu, 0x3F32804Eu,
			0x3E456A4Au, 0x3E281FEFu, 0x3F6DD5CFu, 0x3F800000u, 0x3EF4B5ABu, 0x3EB57CF6u, 0x3EC62AC7u, 0x3F800000u,
			0x3EAAD671u, 0x3E48FDA4u, 0x3F151DE2u, 0x3F800000u, 0x3E831659u, 0x3DCA8C8Au, 0x3F173C08u, 0x3F800000u,
			0x3E1FC685u, 0x3E10EDB6u, 0x3EC26A57u, 0x3F800000u, 0x3E99999Au, 0x3E99999Au, 0x3E99999Au, 0x3F800000u,
		};

		private static readonly uint[] PreExpected =
		{
			0x3F000000u, 0x3F333334u, 0x3F8CCCCDu, 0x3F800000u, 0x3DD7BD5Eu, 0x3E1F11AEu, 0x3F80AF16u, 0x3F800000u,
			0x3F250D74u, 0x3F09F3DEu, 0x3EFE8164u, 0x3F800000u, 0x3F898028u, 0x3FA8ED9Au, 0x3FD936E0u, 0x3F800000u,
			0x3F121F6Du, 0x3F24B3A9u, 0x3F5BFB8Eu, 0x3F800000u, 0x3EA83038u, 0x3F10134Au, 0x3F8404D2u, 0x3F800000u,
			0x3E99999Au, 0x3E99999Au, 0x3E99999Au, 0x3F800000u, 0x3F7BD426u, 0x3E737FCBu, 0x3F49CC9Du, 0x3F800000u,
			0x3F800000u, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x00000000u, 0x00000000u, 0x3F800000u, 0x3F800000u,
			0x00000000u, 0x00000000u, 0x3F800000u, 0x3F800000u, 0x3F65A330u, 0x3F3B87B3u, 0x3F001FE9u, 0x3F800000u,
			0x3FBCC6EEu, 0x3FC06F5Cu, 0x3FA75410u, 0x3F800000u, 0x3F21316Au, 0x3F2F06E9u, 0x3F471DF7u, 0x3F800000u,
			0x3E8CCCCDu, 0x3ECCCCCDu, 0x3F266666u, 0x3F800000u, 0x3E99999Au, 0x3E99999Au, 0x3E99999Au, 0x3F800000u,
			0x3ECCCCCDu, 0x3E99999Au, 0x3E4CCCCDu, 0x3F000000u, 0x3E78F8FAu, 0x3E3A3DC2u, 0x3F03F0DEu, 0x3F32804Eu,
			0x3E51764Au, 0x3E3027EFu, 0x3F6FD6CEu, 0x3F800000u, 0x3F4B925Eu, 0x3F186458u, 0x3F0D2994u, 0x3F800000u,
			0x3F8D7ECEu, 0x3F882214u, 0x3FC5CA7Cu, 0x3F800000u, 0x3EF4FD54u, 0x3E8A90EFu, 0x3F3AB5C4u, 0x3F800000u,
			0x3E91CF18u, 0x3ECC4E86u, 0x3F650CD7u, 0x3F800000u, 0x3E99999Au, 0x3E99999Au, 0x3E99999Au, 0x3F800000u,
		};
	}
}
