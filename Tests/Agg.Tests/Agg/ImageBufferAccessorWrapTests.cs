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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// ImageBufferAccessorWrap is C++'s image_accessor_wrap: reads past any edge fold back into the image through its
	// wrap modes (C++'s wrap_mode_repeat/reflect and their pow2 variants), so a pattern tiles the plane.
	public class ImageBufferAccessorWrapTests
	{
		[Test]
		public async Task RepeatAndReflectFoldEveryCoordinateIntoTheImage()
		{
			// Each mode's call operator at -4, then its increment stepping on from there.
			await Assert.That(Walk(new WrapModeRepeat(3), -4, 6)).IsEquivalentTo(new[] { 2, 0, 1, 2, 0, 1 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeRepeatPow2(4), -4, 6)).IsEquivalentTo(new[] { 0, 1, 2, 3, 0, 1 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeRepeatAutoPow2(3), -4, 6)).IsEquivalentTo(new[] { 2, 0, 1, 2, 0, 1 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeRepeatAutoPow2(4), -4, 6)).IsEquivalentTo(new[] { 0, 1, 2, 3, 0, 1 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeReflect(3), -4, 8)).IsEquivalentTo(new[] { 2, 2, 1, 0, 0, 1, 2, 2 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeReflectPow2(4), -4, 10)).IsEquivalentTo(new[] { 3, 2, 1, 0, 0, 1, 2, 3, 3, 2 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeReflectAutoPow2(3), -4, 8)).IsEquivalentTo(new[] { 2, 2, 1, 0, 0, 1, 2, 2 }, CollectionOrdering.Matching);
			await Assert.That(Walk(new WrapModeReflectAutoPow2(4), -4, 10)).IsEquivalentTo(new[] { 3, 2, 1, 0, 0, 1, 2, 3, 3, 2 }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task SpansReadTheWrappedPixels()
		{
			// A 3x2 image whose red channel is x and green channel is y.
			var image = new ImageBuffer(3, 2);
			byte[] pixels = image.GetBuffer();
			for (int y = 0; y < 2; y++)
			{
				for (int x = 0; x < 3; x++)
				{
					int at = image.GetBufferOffsetXY(x, y);
					pixels[at + ImageBuffer.OrderR] = (byte)x;
					pixels[at + ImageBuffer.OrderG] = (byte)y;
				}
			}

			var accessor = new ImageBufferAccessorWrap(image, new WrapModeRepeat(3), new WrapModeReflect(2));

			byte[] buffer = accessor.span(-1, 2, 3, out int offset);
			await Assert.That((buffer[offset + ImageBuffer.OrderR], buffer[offset + ImageBuffer.OrderG])).IsEqualTo(((byte)2, (byte)1));
			buffer = accessor.next_x(out offset);
			await Assert.That((buffer[offset + ImageBuffer.OrderR], buffer[offset + ImageBuffer.OrderG])).IsEqualTo(((byte)0, (byte)1));

			// The next row down reflects back to row 0 and starts again at the span's first x.
			buffer = accessor.next_y(out offset);
			await Assert.That((buffer[offset + ImageBuffer.OrderR], buffer[offset + ImageBuffer.OrderG])).IsEqualTo(((byte)2, (byte)0));
		}

		private static int[] Walk(IWrapMode mode, int start, int count)
		{
			var values = new List<int> { mode.Wrap(start) };
			while (values.Count < count)
			{
				values.Add(mode.Next());
			}

			return values.ToArray();
		}
	}
}
