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

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// Tests the comparison the golden suites are judged by. Needs no GPU.
	/// </summary>
	/// <remarks>
	/// Without these, a comparison that quietly matched everything would make every golden suite pass
	/// forever and nobody would notice until the port shipped visibly wrong pixels.
	/// </remarks>
	public class GoldenImageCompareTests
	{
		private static ImageBuffer Filled(int width, int height, byte value)
		{
			var image = new ImageBuffer(width, height, 32, new BlenderBGRA());
			var buffer = image.GetBuffer();
			for (int index = 0; index < buffer.Length; index++)
			{
				buffer[index] = value;
			}

			image.MarkImageChanged();
			return image;
		}

		[Test]
		public async Task IdenticalImagesHaveNoDifference()
		{
			var difference = GoldenImage.Compare(Filled(8, 4, 40), Filled(8, 4, 40), 0);

			await Assert.That(difference.SameSize).IsTrue();
			await Assert.That(difference.DifferingPixels).IsEqualTo(0L);
			await Assert.That(difference.MaxChannelDelta).IsEqualTo(0);
			await Assert.That(difference.PercentDiffering).IsEqualTo(0d);
		}

		[Test]
		public async Task EveryDifferingPixelIsCountedAtToleranceZero()
		{
			var golden = Filled(8, 4, 40);
			var rendered = Filled(8, 4, 43);

			var difference = GoldenImage.Compare(golden, rendered, 0);

			await Assert.That(difference.DifferingPixels).IsEqualTo(32L);
			await Assert.That(difference.TotalPixels).IsEqualTo(32L);
			await Assert.That(difference.MaxChannelDelta).IsEqualTo(3);
			await Assert.That(difference.PercentDiffering).IsEqualTo(100d);
		}

		[Test]
		public async Task ToleranceIsInclusiveOfItsOwnDelta()
		{
			var golden = Filled(8, 4, 40);
			var rendered = Filled(8, 4, 43);

			await Assert.That(GoldenImage.Compare(golden, rendered, 2).DifferingPixels).IsEqualTo(32L);
			await Assert.That(GoldenImage.Compare(golden, rendered, 3).DifferingPixels).IsEqualTo(0L);
		}

		[Test]
		public async Task ASingleChangedChannelIsEnoughToFlagThePixel()
		{
			var golden = Filled(8, 4, 40);
			var rendered = Filled(8, 4, 40);

			// One channel of one pixel, so a comparison that averaged channels or sampled would miss it.
			rendered.GetBuffer()[rendered.GetBufferOffsetXY(3, 2) + 1] = 90;
			rendered.MarkImageChanged();

			var difference = GoldenImage.Compare(golden, rendered, 0);

			await Assert.That(difference.DifferingPixels).IsEqualTo(1L);
			await Assert.That(difference.MaxChannelDelta).IsEqualTo(50);
		}

		[Test]
		public async Task DifferentSizesAreReportedRatherThanCompared()
		{
			var difference = GoldenImage.Compare(Filled(8, 4, 40), Filled(8, 5, 40), 0);

			await Assert.That(difference.SameSize).IsFalse();
			await Assert.That(difference.Describe()).Contains("sizes differ");
		}
	}
}
