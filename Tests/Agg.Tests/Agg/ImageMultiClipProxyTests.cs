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

namespace MatterHackers.Agg.Tests.Agg
{
	public class ImageMultiClipProxyTests
	{
		/// <summary>
		/// A span across two clip boxes and the gap between them lands only inside the boxes, as C++
		/// renderer_mclip draws it once per box.
		/// </summary>
		[Test]
		public async Task SpanDrawsOnlyInsideTheBoxes()
		{
			var image = new ImageBuffer(10, 4);
			var clipped = new ImageMultiClipProxy(image);
			clipped.reset_clipping(false);
			clipped.add_clip_box(1, 0, 2, 3);
			clipped.add_clip_box(6, 0, 7, 3);

			clipped.blend_hline(0, 1, 9, Color.Black, 255);

			await Assert.That(RowAlphas(image, 1)).IsEqualTo("0110001100");
		}

		/// <summary>
		/// A filled square marker straddling two boxes is filled in both, which it would not be if its bar were
		/// clipped to the last box alone; and no box at all draws nothing, while reset_clipping(true) draws
		/// everywhere.
		/// </summary>
		[Test]
		public async Task MarkerBarFillsEveryBoxAndVisibilityResets()
		{
			var image = new ImageBuffer(10, 10);
			var clipped = new ImageMultiClipProxy(image);
			clipped.reset_clipping(false);
			clipped.add_clip_box(0, 0, 3, 9);
			clipped.add_clip_box(6, 0, 9, 9);
			var markers = new RendererMarkers(clipped) { LineColor = Color.Black, FillColor = Color.Black };

			markers.Marker(5, 5, 4, MarkerType.Square);

			await Assert.That(RowAlphas(image, 5)).IsEqualTo("0111001111");

			clipped.reset_clipping(false);
			clipped.blend_hline(0, 0, 9, Color.Black, 255);
			await Assert.That(RowAlphas(image, 0)).IsEqualTo("0000000000");

			clipped.reset_clipping(true);
			clipped.blend_hline(0, 0, 9, Color.Black, 255);
			await Assert.That(RowAlphas(image, 0)).IsEqualTo("1111111111");
		}

		/// <summary>copy_hline takes a length (IImageByte's contract) and copies it into every box it crosses.</summary>
		[Test]
		public async Task CopyHlineTakesALengthAndCopiesIntoEveryBox()
		{
			var image = new ImageBuffer(10, 1);
			var clipped = new ImageMultiClipProxy(image);
			clipped.reset_clipping(false);
			clipped.add_clip_box(1, 0, 2, 0);
			clipped.add_clip_box(6, 0, 9, 0);

			clipped.copy_hline(0, 0, 8, Color.Black);

			await Assert.That(RowAlphas(image, 0)).IsEqualTo("0110001100");
		}

		private static string RowAlphas(ImageBuffer image, int y)
		{
			var row = new char[image.Width];
			for (int x = 0; x < image.Width; x++)
			{
				row[x] = image.GetPixel(x, y).alpha == 0 ? '0' : '1';
			}

			return new string(row);
		}
	}
}
