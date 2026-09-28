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
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	public class AlphaMaskAdaptorTests
	{
		/// <summary>
		/// C++ pixfmt_amask_adaptor copies the covers into its own span before multiplying the mask in, so a
		/// caller that reuses its covers (one scanline rendered to two targets) still has the unmasked values.
		/// </summary>
		[Test]
		public async Task BlendSolidHspanLeavesTheCallersCoversAlone()
		{
			var mask = new ImageBuffer(4, 1, 8, new BlenderGrayExact(1));
			mask.GetBuffer()[1] = 128;
			var destination = new ImageBuffer(4, 1);
			var adaptor = new AlphaMaskAdaptor(destination, new AlphaMaskByteClipped(mask, 1, 0));

			byte[] covers = { 7, 255, 255, 255, 9 };
			adaptor.blend_solid_hspan(0, 0, 4, Color.Black, covers, 1);

			await Assert.That(covers).IsEquivalentTo(new byte[] { 7, 255, 255, 255, 9 }, CollectionOrdering.Matching);

			// The mask still reaches the pixels: 0 blocks, 128 lets half through.
			await Assert.That(destination.GetPixel(0, 0).alpha).IsEqualTo((byte)0);
			await Assert.That(destination.GetPixel(1, 0).alpha).IsEqualTo((byte)128);
		}

		/// <summary>
		/// renderer_primitives and renderer_markers draw pixels, vertical lines and gradient spans through
		/// renderer_base&lt;pixfmt_amask_adaptor&gt;: each is masked, and a pixel off the clip box is dropped.
		/// </summary>
		[Test]
		public async Task PixelsVlinesAndColorSpansAreMaskedAndClipped()
		{
			var mask = new ImageBuffer(3, 3, 8, new BlenderGrayExact(1));
			mask.GetBuffer()[mask.GetBufferOffsetXY(0, 0)] = 255;
			mask.GetBuffer()[mask.GetBufferOffsetXY(1, 0)] = 128;
			mask.GetBuffer()[mask.GetBufferOffsetXY(2, 1)] = 255;
			var destination = new ImageBuffer(3, 3);
			var target = new ImageClippingProxy(new AlphaMaskAdaptor(destination, new AlphaMaskByteUnclipped(mask, 1, 0)));

			target.BlendPixel(0, 0, Color.Black, 255);
			target.BlendPixel(1, 0, Color.Black, 255);
			target.BlendPixel(-1, 0, Color.Black, 255);
			target.BlendPixel(0, 3, Color.Black, 255);
			await Assert.That(destination.GetPixel(0, 0).alpha).IsEqualTo((byte)255);
			await Assert.That(destination.GetPixel(1, 0).alpha).IsEqualTo((byte)128);

			// The vline covers rows 0 to 2 of column 2; only row 1 is open in the mask.
			target.blend_vline(2, 0, 2, Color.Black, 255);
			await Assert.That(destination.GetPixel(2, 0).alpha).IsEqualTo((byte)0);
			await Assert.That(destination.GetPixel(2, 1).alpha).IsEqualTo((byte)255);
			await Assert.That(destination.GetPixel(2, 2).alpha).IsEqualTo((byte)0);

			var colors = new[] { Color.Black, Color.Black, Color.Black };
			byte[] covers = { 255, 255, 255 };
			target.blend_color_hspan(0, 1, 3, colors, 0, covers, 0, false);
			await Assert.That(destination.GetPixel(0, 1).alpha).IsEqualTo((byte)0);
			await Assert.That(covers).IsEquivalentTo(new byte[] { 255, 255, 255 }, CollectionOrdering.Matching);
		}
	}
}
