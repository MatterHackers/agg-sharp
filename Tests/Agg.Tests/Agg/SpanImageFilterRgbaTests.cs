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
	// The general lookup-table span_image_filter_rgba (image_filters2.cpp's) with the bilinear lookup table on a
	// 4x4 image, sampling frame pixel (1, 1) through a translation of (tx, ty): the filter takes back the half pixel
	// the interpolator adds, so it reads the image at (1 + tx, 1 + ty).
	public class SpanImageFilterRgbaTests
	{
		/// <summary>
		/// A translucent straight-alpha color keeps its full color. C++ clamps each color channel to alpha, right for
		/// premultiplied color, but ImageGraphics2D feeds this filter straight-alpha ImageBuffers, which that clamp
		/// would darken, here to (128, 100, 50).
		/// </summary>
		[Test]
		public async Task StraightAlphaColorIsNotClampedToAlpha()
		{
			var translucent = new Color(200, 100, 50, 128);
			var image = new ImageBuffer(4, 4);
			byte[] buffer = image.GetBuffer();
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					// Written straight to the buffer: SetPixel would blend the translucent color.
					int offset = image.GetBufferOffsetXY(x, y);
					buffer[offset + ImageBuffer.OrderR] = translucent.red;
					buffer[offset + ImageBuffer.OrderG] = translucent.green;
					buffer[offset + ImageBuffer.OrderB] = translucent.blue;
					buffer[offset + ImageBuffer.OrderA] = translucent.alpha;
				}
			}

			var lookUpTable = new ImageFilterLookUpTable();
			lookUpTable.calculate(new image_filter_spline16(), true);

			await Assert.That(Sample(image, 0.3, 0.6, lookUpTable)).IsEqualTo(translucent);
		}

		/// <summary>
		/// Halfway between 0 and 1 is 0.5, which rounds to 1. C++ AGG's general filter starts its sums at 0 and
		/// truncates it to 0, darkening by half a unit on average; agg-sharp (and the reference renderer's patched
		/// header) round as C++'s plain bilinear filter does.
		/// </summary>
		[Test]
		public async Task HalfwayBetweenTwoValuesRounds()
		{
			var image = new ImageBuffer(4, 4);
			for (int y = 0; y < 4; y++)
			{
				image.SetPixel(2, y, new Color(1, 1, 1, 1));
			}

			Color sample = Sample(image, 0.5, 0.0);

			await Assert.That((int)sample.red).IsEqualTo(1);
			await Assert.That((int)sample.alpha).IsEqualTo(1);
		}

		/// <summary>
		/// A flat image keeps its exact value at every fractional offset. Through spline36's normalized table the
		/// rounded weight products can sum to just under a whole, and truncating, C++ gives 254 for 255 there:
		/// image_filters2's solid squares came out a level dark.
		/// </summary>
		[Test]
		public async Task FlatColorKeepsItsValueAtEveryFractionalOffset()
		{
			var image = new ImageBuffer(4, 4);
			var flat = new Color(255, 128, 3, 255);
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					image.SetPixel(x, y, flat);
				}
			}

			var lookUpTable = new ImageFilterLookUpTable(new image_filter_spline36(), true);
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					Color sample = Sample(image, i / 16.0, j / 16.0, lookUpTable);

					await Assert.That(sample).IsEqualTo(flat);
				}
			}
		}

		private static Color Sample(ImageBuffer image, double tx, double ty)
		{
			return Sample(image, tx, ty, new ImageFilterLookUpTable(new image_filter_bilinear(), true));
		}

		private static Color Sample(ImageBuffer image, double tx, double ty, ImageFilterLookUpTable lookUpTable)
		{
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(tx, ty));
			var filter = new span_image_filter_rgba(new ImageBufferAccessorClamp(image), interpolator, lookUpTable);
			var span = new Color[1];
			filter.generate(span, 0, 1, 1, 1);
			return span[0];
		}
	}
}
