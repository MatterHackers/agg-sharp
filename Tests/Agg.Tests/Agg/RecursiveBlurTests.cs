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
	/// The recursive (Gaussian) blur's calculators round and clamp a channel back to a byte. C++ AGG 2.6 truncates,
	/// and its weights sum to just under 1, so a white area came out 254 after one pass and 253 after both; the
	/// patched C++ reference (tools/cpp-renderer/patches/agg_blur.h) rounds and clamps as agg-sharp does.
	/// </summary>
	public class RecursiveBlurTests
	{
		[Test]
		[Arguments(3.0)]
		[Arguments(15.0)]
		[Arguments(25.0)]
		public async Task FlatWhiteStaysWhiteAndOpaque(double radius)
		{
			var image = new ImageBuffer(60, 40);
			image.NewGraphics2D().Clear(Color.White);

			new RecursiveBlur(new recursive_blur_calc_rgb()).blur(image, radius);

			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					await Assert.That(image.GetPixel(x, y)).IsEqualTo(Color.White);
				}
			}
		}

		/// <summary>A grey calculator over one channel of a colour image writes back the value it blurred, so a flat
		/// channel stays flat and the other channels are untouched.</summary>
		[Test]
		public async Task GrayBlurOfOneChannelKeepsAFlatChannelAndTheOthers()
		{
			var image = new ImageBuffer(30, 20);
			image.NewGraphics2D().Clear(new Color(200, 100, 50, 255));
			var green = new ImageBuffer();
			green.AttachBuffer(image.GetBuffer(), ImageBuffer.OrderG, image.Width, image.Height, image.StrideInBytes(), 8, 4);
			green.SetRecieveBlender(new blender_gray(4));

			new RecursiveBlur(new recursive_blur_calc_gray()).blur(green, 10.0);

			await Assert.That(image.GetPixel(15, 10)).IsEqualTo(new Color(200, 100, 50, 255));
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(200, 100, 50, 255));
		}
	}
}
