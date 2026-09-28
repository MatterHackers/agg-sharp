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
	/// <summary>C++ AGG's stack_blur_gray8: the stack blur of one byte per pixel, as blend_color blurs its shadow.</summary>
	public class StackBlurGray8Tests
	{
		/// <summary>A single white pixel at radius 1: the [1 2 1] / 4 kernel each way, truncated after each pass.</summary>
		[Test]
		public async Task ImpulseSpreadsByTheTruncatedKernel()
		{
			var image = new ImageBuffer(5, 5, 8, new blender_gray(1));
			image.GetBuffer()[image.GetBufferOffsetXY(2, 2)] = 255;

			stack_blur.BlurGray8(image, 1, 1);

			// Across: 63 127 63. Then down each column: 127 -> 31 63 31, 63 -> 15 31 15.
			var expected = new int[,]
			{
				{ 0, 0, 0, 0, 0 },
				{ 0, 15, 31, 15, 0 },
				{ 0, 31, 63, 31, 0 },
				{ 0, 15, 31, 15, 0 },
				{ 0, 0, 0, 0, 0 },
			};
			for (int y = 0; y < 5; y++)
			{
				for (int x = 0; x < 5; x++)
				{
					await Assert.That((int)image.GetBuffer()[image.GetBufferOffsetXY(x, y)]).IsEqualTo(expected[y, x]);
				}
			}
		}

		/// <summary>
		/// The gray blur is the colour stack blur's arithmetic on one channel, so a random image's green channel blurred
		/// as gray matches the colour blur's green - edges and a radius wider than the image included.
		/// </summary>
		[Test]
		[Arguments(1)]
		[Arguments(3)]
		[Arguments(7)]
		[Arguments(40)]
		public async Task MatchesTheColourStackBlurPerChannel(int radius)
		{
			var random = new Random(radius);
			var color = new ImageBuffer(23, 17);
			var gray = new ImageBuffer(23, 17, 8, new blender_gray(1));
			for (int y = 0; y < color.Height; y++)
			{
				for (int x = 0; x < color.Width; x++)
				{
					byte v = (byte)random.Next(256);
					color.SetPixel(x, y, new Color(0, v, 0, 255));
					gray.GetBuffer()[gray.GetBufferOffsetXY(x, y)] = v;
				}
			}

			new stack_blur().blur(color, radius);
			stack_blur.BlurGray8(gray, radius, radius);

			for (int y = 0; y < color.Height; y++)
			{
				for (int x = 0; x < color.Width; x++)
				{
					await Assert.That((int)gray.GetBuffer()[gray.GetBufferOffsetXY(x, y)]).IsEqualTo((int)color.GetPixel(x, y).green);
				}
			}
		}
	}
}
