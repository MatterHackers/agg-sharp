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
	/// Stack blur keeps a flat field exactly. C++ AGG 2.6 divides by the (radius + 1)^2 weight with a multiply and
	/// shift that falls just short of it, so a white area came out 254; agg-sharp (and the patched C++ reference,
	/// tools/cpp-renderer/patches/agg_blur.h) divide exactly, rounding to nearest.
	/// </summary>
	public class StackBlurTests
	{
		[Test]
		[Arguments(1, 255)]
		[Arguments(4, 255)]
		[Arguments(15, 255)]
		[Arguments(40, 255)]
		[Arguments(254, 255)]
		[Arguments(15, 1)]
		[Arguments(15, 128)]
		[Arguments(40, 77)]
		[Arguments(200, 200)]
		[Arguments(254, 128)]
		public async Task FlatFieldStaysExact(int radius, int value)
		{
			var color = new Color(value, 255 - value, value / 2, 255);
			var image = new ImageBuffer(60, 40);
			image.NewGraphics2D().Clear(color);

			new stack_blur().blur(image, radius);

			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					await Assert.That(image.GetPixel(x, y)).IsEqualTo(color);
				}
			}
		}
	}
}
