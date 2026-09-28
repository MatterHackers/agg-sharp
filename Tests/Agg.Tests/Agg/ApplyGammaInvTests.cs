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
	public class ApplyGammaInvTests
	{
		/// <summary>C++ pixfmt apply_gamma_inv: every pixel's red, green and blue through gamma.inv; alpha is left alone.</summary>
		[Test]
		[Arguments(24)]
		[Arguments(32)]
		public async Task MapsColorChannelsThroughTheInverseGamma(int bitDepth)
		{
			var image = bitDepth == 24 ? new ImageBuffer(3, 2, 24, new BlenderBGR()) : new ImageBuffer(3, 2);
			image.SetPixel(1, 1, new Color(10, 100, 200, 77));
			var gamma = new GammaLookUpTable(2.0);

			image.ApplyGammaInv(gamma);

			Color pixel = image.GetPixel(1, 1);
			await Assert.That((int)pixel.red).IsEqualTo((int)gamma.inv(10));
			await Assert.That((int)pixel.green).IsEqualTo((int)gamma.inv(100));
			await Assert.That((int)pixel.blue).IsEqualTo((int)gamma.inv(200));
			if (bitDepth == 32)
			{
				await Assert.That((int)pixel.alpha).IsEqualTo(77);
			}
		}

		/// <summary>C++ pixfmt apply_gamma_dir (pattern_resample's picture): red, green and blue through gamma.dir.</summary>
		[Test]
		public async Task MapsColorChannelsThroughTheDirectGamma()
		{
			var image = new ImageBuffer(3, 2, 24, new BlenderBGR());
			image.SetPixel(1, 1, new Color(10, 100, 200, 255));
			var gamma = new GammaLookUpTable(2.0);

			ImageGammaInverse.ApplyDir(image, gamma);

			Color pixel = image.GetPixel(1, 1);
			await Assert.That((int)pixel.red).IsEqualTo((int)gamma.dir(10));
			await Assert.That((int)pixel.green).IsEqualTo((int)gamma.dir(100));
			await Assert.That((int)pixel.blue).IsEqualTo((int)gamma.dir(200));
		}
	}
}
