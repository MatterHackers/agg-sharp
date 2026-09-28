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
using MatterHackers.Agg.Transform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// span_image_filter_rgba_nn reads each pixel through its image accessor, as C++ does, so what lies outside the
	// image is the accessor's to say: the edge pixel for a clamp, the background for a clip.
	public class SpanImageFilterRgbaNnTests
	{
		/// <summary>Sampling left of the image through a clamp accessor gives the row's first pixel.</summary>
		[Test]
		public async Task OutsideTheImageReadsTheClampedEdge()
		{
			ImageBuffer image = Gradient();
			var filter = new span_image_filter_rgba_nn(new ImageBufferAccessorClamp(image), new span_interpolator_linear(Affine.NewTranslation(-10, 0)));

			await Assert.That(Sample(filter, 3, 2)).IsEqualTo(image.GetPixel(0, 2));
		}

		/// <summary>Sampling right of and below the image through a clip accessor gives its background color.</summary>
		[Test]
		public async Task OutsideTheImageReadsTheClipBackground()
		{
			var background = new Color(9, 8, 7, 6);
			var filter = new span_image_filter_rgba_nn(new ImageBufferAccessorClip(Gradient(), background), new span_interpolator_linear(Affine.NewTranslation(10, -10)));

			await Assert.That(Sample(filter, 1, 1)).IsEqualTo(background);
		}

		/// <summary>Inside the image the nearest pixel comes through unchanged.</summary>
		[Test]
		public async Task InsideTheImageReadsThePixel()
		{
			ImageBuffer image = Gradient();
			var filter = new span_image_filter_rgba_nn(new ImageBufferAccessorClamp(image), new span_interpolator_linear(Affine.NewTranslation(1, 1)));

			await Assert.That(Sample(filter, 2, 1)).IsEqualTo(image.GetPixel(3, 2));
		}

		// A 4x4 opaque image, every pixel different.
		private static ImageBuffer Gradient()
		{
			var image = new ImageBuffer(4, 4);
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					image.SetPixel(x, y, new Color(40 + (x * 50), 30 + (y * 50), 200, 255));
				}
			}

			return image;
		}

		private static Color Sample(span_image_filter filter, int x, int y)
		{
			filter.prepare();
			var span = new Color[1];
			filter.generate(span, 0, x, y, 1);
			return span[0];
		}
	}
}
