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

using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// C++ pixfmt_rgb555 / pixfmt_rgb565 (agg_pixfmt_rgb_packed.h): a copy, then blend_pixel, then pixel(). Every
	/// expected word and color below was printed by C++ AGG itself for the same inputs.
	/// </summary>
	public class BlenderRgbPackedTests
	{
		[Test]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 255, 0xE586, 200, 96, 48)]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 128, 0xF2B2, 224, 168, 144)]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 1, 0xFBDE, 240, 240, 240)]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 0, 0xFFFF, 248, 248, 248)]
		[Arguments(255, 255, 255, 200, 100, 50, 128, 255, 0xF2B2, 224, 168, 144)]
		[Arguments(10, 200, 90, 250, 3, 77, 255, 200, 0xE0A9, 192, 40, 72)]
		[Arguments(10, 200, 90, 250, 3, 77, 90, 77, 0x92CA, 32, 176, 80)]
		public async Task Rgb555BlendMatchesCppAgg(int dr, int dg, int db, int r, int g, int b, int a, int cover, int word, int er, int eg, int eb)
		{
			await CheckBlend(new BlenderRgb555(), dr, dg, db, r, g, b, a, cover, word, er, eg, eb);
		}

		[Test]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 255, 0xCB26, 200, 100, 48)]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 128, 0xE592, 224, 176, 144)]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 1, 0xF7DE, 240, 248, 240)]
		[Arguments(255, 255, 255, 200, 100, 50, 255, 0, 0xFFFF, 248, 252, 248)]
		[Arguments(255, 255, 255, 200, 100, 50, 128, 255, 0xE592, 224, 176, 144)]
		[Arguments(10, 200, 90, 250, 3, 77, 255, 200, 0xC169, 192, 44, 72)]
		[Arguments(10, 200, 90, 250, 3, 77, 90, 77, 0x258A, 32, 176, 80)]
		public async Task Rgb565BlendMatchesCppAgg(int dr, int dg, int db, int r, int g, int b, int a, int cover, int word, int er, int eg, int eb)
		{
			await CheckBlend(new BlenderRgb565(), dr, dg, db, r, g, b, a, cover, word, er, eg, eb);
		}

		/// <summary>A span through ImageBuffer (what the scanline renderer calls) lands the same words as blend_pixel.</summary>
		[Test]
		public async Task Rgb555SolidHspanMatchesBlendPixel()
		{
			var image = new ImageBuffer(3, 1, 16, new BlenderRgb555());
			image.copy_hline(0, 0, 3, new Color(255, 255, 255));
			image.blend_solid_hspan(0, 0, 3, new Color(200, 100, 50), new byte[] { 255, 128, 1 }, 0);
			byte[] buffer = image.GetBuffer();
			await Assert.That(buffer[0] | (buffer[1] << 8)).IsEqualTo(0xE586);
			await Assert.That(buffer[2] | (buffer[3] << 8)).IsEqualTo(0xF2B2);
			await Assert.That(buffer[4] | (buffer[5] << 8)).IsEqualTo(0xFBDE);
		}

		private static async Task CheckBlend(IRecieveBlenderByte blender, int dr, int dg, int db, int r, int g, int b, int a, int cover, int word, int er, int eg, int eb)
		{
			var image = new ImageBuffer(1, 1, 16, blender);
			image.copy_hline(0, 0, 1, new Color(dr, dg, db));
			image.BlendPixel(0, 0, new Color(r, g, b, a), (byte)cover);

			// The word is stored little-endian, as C++'s int16u is on every host the reference renderer runs on.
			byte[] buffer = image.GetBuffer();
			await Assert.That(buffer[0] | (buffer[1] << 8)).IsEqualTo(word);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(er, eg, eb, 255));
		}
	}
}
