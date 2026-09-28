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
	/// C++ AGG's slight_blur (line_thickness.cpp's filter): a 3x3 separable Gaussian whose channels go back to the
	/// pixel's type rounded. C++ AGG truncates, which on 8-bit channels darkens blurred pixels by up to a level
	/// (line_thickness itself uses a float pixel format, where it does not matter); the patched C++ reference
	/// (tools/cpp-renderer/patches/agg_blur.h) rounds as agg-sharp does.
	/// </summary>
	public class SlightBlurTests
	{
		/// <summary>
		/// One white pixel on black at radius 1 (weights 0.0177, 0.9647, 0.0177): across, 255 becomes 5, 246, 5;
		/// down, the center column becomes 4, 237, 4 and the pixels beside the center 5. Truncating gives 4, 245, 4
		/// across, then 236 at the center and 3 beside it.
		/// </summary>
		[Test]
		public async Task ImpulseSpreadsToItsNeighboursRounded()
		{
			var image = new ImageBuffer(5, 5);
			image.NewGraphics2D().Clear(Color.Black);
			image.SetPixel(2, 2, Color.White);

			new SlightBlur(1.0).Blur(image, new RectangleInt(0, 0, 4, 4));

			await Assert.That(image.GetPixel(2, 2)).IsEqualTo(new Color(237, 237, 237, 255));
			await Assert.That(image.GetPixel(1, 2)).IsEqualTo(new Color(5, 5, 5, 255));
			await Assert.That(image.GetPixel(3, 2)).IsEqualTo(new Color(5, 5, 5, 255));
			await Assert.That(image.GetPixel(2, 1)).IsEqualTo(new Color(4, 4, 4, 255));
			await Assert.That(image.GetPixel(2, 3)).IsEqualTo(new Color(4, 4, 4, 255));
			await Assert.That(image.GetPixel(1, 1)).IsEqualTo(new Color(0, 0, 0, 255));
			await Assert.That(image.GetPixel(0, 2)).IsEqualTo(new Color(0, 0, 0, 255));
		}

		/// <summary>The edges repeat their own pixels, so a flat image - white included - comes back unchanged.</summary>
		[Test]
		[Arguments(0.5)]
		[Arguments(1.5)]
		[Arguments(2.0)]
		public async Task FlatImageIsUnchanged(double radius)
		{
			var image = new ImageBuffer(7, 6);
			image.NewGraphics2D().Clear(Color.White);

			new SlightBlur(radius).Blur(image, new RectangleInt(0, 0, image.Width - 1, image.Height - 1));

			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					await Assert.That(image.GetPixel(x, y)).IsEqualTo(Color.White);
				}
			}
		}

		/// <summary>Only the inclusive bounds are blurred, and a box under three pixels wide or high not at all, as in C++.</summary>
		[Test]
		public async Task BlursOnlyInsideTheBounds()
		{
			var image = new ImageBuffer(8, 8);
			image.NewGraphics2D().Clear(Color.Black);
			image.SetPixel(4, 4, Color.White);
			image.SetPixel(1, 1, Color.White);

			new SlightBlur(1.0).Blur(image, new RectangleInt(3, 3, 5, 5));
			new SlightBlur(1.0).Blur(image, new RectangleInt(0, 0, 1, 7));

			await Assert.That(image.GetPixel(4, 4)).IsEqualTo(new Color(237, 237, 237, 255));
			await Assert.That(image.GetPixel(4, 5)).IsEqualTo(new Color(4, 4, 4, 255));
			await Assert.That(image.GetPixel(4, 6)).IsEqualTo(new Color(0, 0, 0, 255));
			await Assert.That(image.GetPixel(1, 1)).IsEqualTo(Color.White);
			await Assert.That(image.GetPixel(2, 1)).IsEqualTo(new Color(0, 0, 0, 255));
		}
	}
}
