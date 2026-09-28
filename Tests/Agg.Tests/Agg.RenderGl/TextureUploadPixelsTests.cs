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
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The texture bytes <see cref="ImageTexturePlugin"/> uploads: level 0 is the image's straight-alpha pixels, and
	/// every mip level below it stays straight alpha.
	/// </summary>
	public class TextureUploadPixelsTests
	{
		[Test]
		public async Task MipLevelAveragesStraightTexels()
		{
			// Four translucent texels of one alpha: the level below is their plain average, not premultiplied.
			byte[] level0 =
			{
				200, 0, 40, 128,   100, 60, 0, 128,
				0, 100, 80, 128,   20, 0, 240, 128,
			};

			byte[] level1 = TextureUploadPixels.DownsampleStraightAlpha(level0, 2, 2, out int width, out int height);

			await Assert.That(width).IsEqualTo(1);
			await Assert.That(height).IsEqualTo(1);
			await Assert.That(level1).IsEquivalentTo(new byte[] { 80, 40, 90, 128 }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task TransparentTexelsAddNoColorToTheMip()
		{
			byte[] level0 =
			{
				255, 0, 0, 255,   0, 255, 0, 0,
				255, 0, 0, 255,   0, 255, 0, 0,
			};

			byte[] level1 = TextureUploadPixels.DownsampleStraightAlpha(level0, 2, 2, out _, out _);

			await Assert.That(level1).IsEquivalentTo(new byte[] { 255, 0, 0, 128 }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task UploadIgnoresOriginOffsetAndClearsInvisibleColor()
		{
			var image = new ImageBuffer(2, 1, 32, new BlenderBGRA());
			byte[] pixels = image.GetBuffer();
			int left = image.GetBufferOffsetXY(0, 0);
			int right = image.GetBufferOffsetXY(1, 0);
			pixels[left + ImageBuffer.OrderR] = 10;
			pixels[left + ImageBuffer.OrderG] = 20;
			pixels[left + ImageBuffer.OrderB] = 30;
			pixels[left + ImageBuffer.OrderA] = 40;
			pixels[right + ImageBuffer.OrderG] = 255;
			image.OriginOffset = new VectorMath.Vector2(-3, 5);

			byte[] rgba = TextureUploadPixels.FromImage(image, 4, 1);

			await Assert.That(rgba).IsEquivalentTo(new byte[] { 10, 20, 30, 40, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, CollectionOrdering.Matching);
		}
	}
}
