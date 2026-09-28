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
	// BlenderGammaBGR against C++ blender_rgb_gamma (agg_pixfmt_rgb.h) on a 24-bit buffer, with a
	// gamma_lut<int8u, int8u, 8, 8>(2.0): dir(100) = 39, inv(19) = 70. The same expectations as
	// BlenderGammaBGRATests, worked by hand from the C++ source.
	public class BlenderGammaBGRTests
	{
		/// <summary>Black at half cover over 100 gray is inv((0 - 39) * 128 &gt;&gt; 8 + 39) = inv(19) = 70.</summary>
		[Test]
		public async Task PartialCoverBlendsThroughDirAndInvAsCpp()
		{
			ImageBuffer image = GrayWithGamma2(100);

			image.blend_hline(0, 0, 0, new Color(0, 0, 0, 255), 128);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(70, 70, 70, 255));
		}

		/// <summary>An opaque color at full cover is copied untouched - no inv on the way in.</summary>
		[Test]
		public async Task OpaqueFullCoverCopiesTheColor()
		{
			ImageBuffer image = GrayWithGamma2(100);

			image.blend_hline(0, 0, 0, new Color(200, 10, 30, 255), 255);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(200, 10, 30, 255));
		}

		/// <summary>C++ copy_hline writes the color as is to every pixel of the run.</summary>
		[Test]
		public async Task CopyHlineCopiesTheColorToEveryPixel()
		{
			ImageBuffer image = GrayWithGamma2(100);

			image.copy_hline(0, 0, 3, new Color(200, 10, 30, 255));

			for (int x = 0; x < 3; x++)
			{
				await Assert.That(image.GetPixel(x, 0)).IsEqualTo(new Color(200, 10, 30, 255));
			}
		}

		/// <summary>
		/// A color whose alpha a cover brings to 0 (alpha 1, cover 1; alpha 100, cover 1) leaves the pixel
		/// alone, where C++ would write inv(dir(10)) = 0 (the C++ bug BlenderGammaBGRA also fixes).
		/// </summary>
		[Test]
		public async Task CoverThatZeroesTheAlphaLeavesThePixel()
		{
			ImageBuffer image = GrayWithGamma2(10);

			image.blend_hline(0, 0, 0, new Color(0, 0, 0, 1), 1);
			image.blend_hline(1, 0, 1, new Color(0, 0, 0, 100), 1);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(10, 10, 10, 255));
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(new Color(10, 10, 10, 255));
		}

		/// <summary>C++ blend_color_hspan: a translucent color blends, a transparent one is skipped, an opaque one copied.</summary>
		[Test]
		public async Task ColorSpanBlendsEachPixelByItsAlpha()
		{
			ImageBuffer image = GrayWithGamma2(100);
			var colors = new[] { new Color(0, 0, 0, 128), new Color(0, 0, 0, 0), new Color(200, 10, 30, 255) };

			image.blend_color_hspan(0, 0, 3, colors, 0, new byte[] { 255 }, 0, true);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(70, 70, 70, 255));
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(new Color(100, 100, 100, 255));
			await Assert.That(image.GetPixel(2, 0)).IsEqualTo(new Color(200, 10, 30, 255));
		}

		private static ImageBuffer GrayWithGamma2(byte level)
		{
			var image = new ImageBuffer(3, 1, 24, new BlenderBGR());
			byte[] buffer = image.GetBuffer();
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = level;
			}

			image.SetRecieveBlender(new BlenderGammaBGR(new GammaLookUpTable(2.0)));
			return image;
		}
	}
}
