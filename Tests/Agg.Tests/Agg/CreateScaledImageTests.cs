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
	// CreateScaledImage resizes an image into its own bounds, so the filter reaching past the source edge must
	// extend the edge pixels (ImageBufferAccessorClamp), not fade to the transparent background a draw onto a
	// canvas uses: an opaque icon scaled up for a HiDPI screen must not get a see-through frame.
	public class CreateScaledImageTests
	{
		[Test]
		[Arguments(16, 32)]
		[Arguments(16, 23)]
		[Arguments(16, 10)]
		[Arguments(64, 20)]
		public async Task AnOpaqueImageStaysOpaqueAtEveryEdge(int from, int to)
		{
			var source = new ImageBuffer(from, from);
			for (int y = 0; y < from; y++)
			{
				for (int x = 0; x < from; x++)
				{
					source.SetPixel(x, y, new Color(x * 255 / from, y * 255 / from, 128, 255));
				}
			}

			ImageBuffer scaled = source.CreateScaledImage(to, to);

			await Assert.That(scaled.Width).IsEqualTo(to);
			for (int i = 0; i < to; i++)
			{
				foreach ((int x, int y) in new[] { (i, 0), (i, to - 1), (0, i), (to - 1, i) })
				{
					await Assert.That((int)scaled.GetPixel(x, y).alpha).IsEqualTo(255).Because($"pixel ({x}, {y}) of {from} -> {to}");
				}
			}
		}
	}
}
