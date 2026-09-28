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
	/// C++ AGG's pattern_filter_bilinear_rgba, the filter the image line renderer samples its pattern through. C++
	/// starts its weighted sums at 0 and truncates the downshift, darkening a pattern by half a level on average;
	/// agg-sharp starts at half a unit so it rounds, as the image span filters do, and the patched C++ reference
	/// (tools/cpp-renderer/patches/agg_pattern_filters_rgba.h) does the same.
	/// </summary>
	public class PatternFilterBilinearTests
	{
		/// <summary>Halfway between a 0 and a 1 is 0.5, which rounds to 1 (C++ truncates it to 0).</summary>
		[Test]
		public async Task HalfwaySampleRoundsInsteadOfTruncating()
		{
			var source = new ImageBuffer(2, 2);
			byte[] buffer = source.GetBuffer();
			for (int y = 0; y < 2; y++)
			{
				// Each row: a 0 pixel then a 1 pixel, in every channel.
				int offset = source.GetBufferOffsetXY(1, y);
				for (int channel = 0; channel < 4; channel++)
				{
					buffer[offset + channel] = 1;
				}
			}

			var result = new Color[1];
			new pattern_filter_bilinear_RGBA_Bytes().pixel_high_res(source, result, 0, LineAABasics.line_subpixel_scale / 2, 0);

			await Assert.That(result[0].red).IsEqualTo((byte)1);
			await Assert.That(result[0].alpha).IsEqualTo((byte)1);
		}
	}
}
