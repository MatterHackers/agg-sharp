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
	// BlenderGammaBGRA against C++ blender_rgb_gamma (agg_pixfmt_rgb.h) with a gamma_lut<int8u, int8u, 8, 8>(2.0):
	// dir(100) = 39, inv(19) = 70. The expected bytes are worked by hand from the C++ source.
	public class BlenderGammaBGRATests
	{
		/// <summary>
		/// C++ blends in the LUT's linear space: inv(downscale((dir(c) - dir(p)) * alpha) + dir(p)). Black at
		/// half cover over 100 gray is inv((0 - 39) * 128 &gt;&gt; 8 + 39) = inv(19) = 70.
		/// </summary>
		[Test]
		public async Task PartialCoverBlendsThroughDirAndInvAsCpp()
		{
			ImageBuffer image = Gray100WithGamma2();

			image.blend_hline(0, 0, 0, new Color(0, 0, 0, 255), 128);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(70, 70, 70, 255));
		}

		/// <summary>An opaque color at full cover is copied untouched, as C++ copy_or_blend_pix does.</summary>
		[Test]
		public async Task OpaqueFullCoverCopiesTheColor()
		{
			ImageBuffer image = Gray100WithGamma2();

			image.blend_hline(0, 0, 0, new Color(200, 10, 30, 255), 255);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(200, 10, 30, 255));
		}

		/// <summary>C++ blend_color_hspan with no covers: each color blended by its own alpha.</summary>
		[Test]
		public async Task ColorSpanBlendsEachPixelByItsAlpha()
		{
			ImageBuffer image = Gray100WithGamma2();
			var colors = new[] { new Color(0, 0, 0, 128), new Color(0, 0, 0, 0), new Color(200, 10, 30, 255) };

			image.blend_color_hspan(0, 0, 3, colors, 0, new byte[] { 255 }, 0, true);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(70, 70, 70, 255));
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(new Color(100, 100, 100, 255));
			await Assert.That(image.GetPixel(2, 0)).IsEqualTo(new Color(200, 10, 30, 255));
		}

		/// <summary>
		/// A translucent color whose cover brings the alpha to 0 (alpha 100, cover 1) leaves the pixel alone. C++
		/// blend_pix still blends it and writes inv(dir(p)), which is not p at the dark end (gamma 2: dir(10) = 0,
		/// inv(0) = 0, so C++ turns 10 into 0); the port and the patched C++ reference treat it as the no-op it is.
		/// </summary>
		[Test]
		public async Task CoverThatZeroesTheAlphaLeavesThePixel()
		{
			ImageBuffer image = GrayWithGamma2(10);

			image.blend_hline(0, 0, 0, new Color(0, 0, 0, 100), 1);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(10, 10, 10, 255));
		}

		/// <summary>
		/// blend_color_hspan with covers: neither a cover that zeroes a translucent color's alpha nor a transparent
		/// color touches the pixel (10 stays); a real blend still darkens it.
		/// </summary>
		[Test]
		public async Task ColorSpanSkipsZeroEffectiveAlpha()
		{
			ImageBuffer image = GrayWithGamma2(10);
			var colors = new[] { new Color(0, 0, 0, 100), new Color(0, 0, 0, 0), new Color(0, 0, 0, 255) };

			image.blend_color_hspan(0, 0, 3, colors, 0, new byte[] { 0, 255, 255 }, 0, false);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(10, 10, 10, 255));
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(new Color(10, 10, 10, 255));
			await Assert.That(image.GetPixel(2, 0)).IsEqualTo(new Color(0, 0, 0, 255));
		}

		/// <summary>
		/// blend_color_vspan (DoCopyOrBlend) skips a transparent color as C++ copy_or_blend_pix does, where it used
		/// to blend it - through the gamma blender that darkened the pixel.
		/// </summary>
		[Test]
		public async Task ColorVspanSkipsTransparentColors()
		{
			ImageBuffer image = GrayWithGamma2(10);
			var colors = new[] { new Color(0, 0, 0, 0) };

			image.blend_color_vspan(0, 0, 1, colors, 0, new byte[] { 255 }, 0, true);
			image.blend_color_vspan(1, 0, 1, colors, 0, new byte[] { 128 }, 0, false);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(10, 10, 10, 255));
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(new Color(10, 10, 10, 255));
		}

		/// <summary>BlenderBGRAExactCopy copies every color, a transparent one included, in a vspan as in an hspan.</summary>
		[Test]
		public async Task ExactCopyVspanStillCopiesTransparentColors()
		{
			var image = new ImageBuffer(1, 1, 32, new BlenderBGRAExactCopy());
			image.SetPixel(0, 0, new Color(10, 20, 30, 255));

			image.blend_color_vspan(0, 0, 1, new[] { new Color(1, 2, 3, 0) }, 0, new byte[] { 255 }, 0, true);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(1, 2, 3, 0));
		}

		private static ImageBuffer Gray100WithGamma2() => GrayWithGamma2(100);

		private static ImageBuffer GrayWithGamma2(byte level)
		{
			var image = new ImageBuffer(3, 1, 32, new BlenderBGRA());
			byte[] buffer = image.GetBuffer();
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = (i % 4) == ImageBuffer.OrderA ? (byte)255 : level;
			}

			image.SetRecieveBlender(new BlenderGammaBGRA(new GammaLookUpTable(2.0)));
			return image;
		}
	}
}
