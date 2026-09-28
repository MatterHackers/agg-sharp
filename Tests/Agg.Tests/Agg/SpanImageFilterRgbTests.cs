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
	// The 24-bit span image filters (image_filters.cpp's), sampling frame pixel (3, 3) of an 8x8 image through a
	// translation of (tx, ty): each filter takes back the half pixel the interpolator adds, so it reads the image
	// at (3 + tx, 3 + ty). C++ AGG starts every one of these sums at 0 and truncates, darkening by half a unit on
	// average; agg-sharp (and the reference renderer's patched agg_span_image_filter_rgb.h) start at half a unit
	// and round.
	public class SpanImageFilterRgbTests
	{
		/// <summary>Halfway between 0 and 1 rounds to 1 in the bilinear filter.</summary>
		[Test]
		public async Task BilinearClipRoundsHalfway()
		{
			ImageBuffer image = ColumnOfOnes();
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(0.5, 0.0));
			var filter = new span_image_filter_rgb_bilinear_clip(new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0)), new Color(0, 0, 0, 0), interpolator);

			Color sample = Sample(filter);

			await Assert.That((int)sample.red).IsEqualTo(1);
			await Assert.That((int)sample.alpha).IsEqualTo(255);
		}

		/// <summary>Halfway between 0 and 1 rounds to 1 in the 2x2 filter.</summary>
		[Test]
		public async Task TwoByTwoRoundsHalfway()
		{
			ImageBuffer image = ColumnOfOnes();
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(0.5, 0.0));
			var lookUpTable = new ImageFilterLookUpTable(new image_filter_bilinear(), false);
			var filter = new span_image_filter_rgb_2x2(new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0)), interpolator, lookUpTable);

			Color sample = Sample(filter);

			await Assert.That((int)sample.red).IsEqualTo(1);
			await Assert.That((int)sample.green).IsEqualTo(1);
			await Assert.That((int)sample.blue).IsEqualTo(1);
		}

		/// <summary>
		/// The general filter's bilinear table weighs the column of ones just over half (8256 of 16384), which rounds
		/// to 1 where C++ truncates to 0.
		/// </summary>
		[Test]
		public async Task GeneralFilterRoundsJustOverHalf()
		{
			ImageBuffer image = ColumnOfOnes();
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(0.5, 0.0));
			var lookUpTable = new ImageFilterLookUpTable(new image_filter_bilinear(), false);
			var filter = new span_image_filter_rgb(new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0)), interpolator, lookUpTable);

			Color sample = Sample(filter);

			await Assert.That((int)sample.red).IsEqualTo(1);
		}

		/// <summary>A flat image keeps its exact value through every filter at a fractional offset: no weight is skipped.</summary>
		[Test]
		public async Task FlatColorKeepsItsValueThroughEachFilter()
		{
			var flat = new Color(255, 128, 3, 255);
			var image = new ImageBuffer(8, 8, 24, new BlenderBGR());
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					image.SetPixel(x, y, flat);
				}
			}

			var accessor = new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0));
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(0.25, 0.75));
			await Assert.That(Sample(new span_image_filter_rgb_bilinear_clip(accessor, new Color(0, 0, 0, 0), interpolator))).IsEqualTo(flat);
			await Assert.That(Sample(new span_image_filter_rgb_2x2(accessor, interpolator, new ImageFilterLookUpTable(new image_filter_hanning(), true)))).IsEqualTo(flat);
			await Assert.That(Sample(new span_image_filter_rgb(accessor, interpolator, new ImageFilterLookUpTable(new image_filter_bicubic(), true)))).IsEqualTo(flat);
		}

		/// <summary>
		/// The plain bilinear filter reads through its accessor, as C++'s does: half on the image's last column and
		/// half past it takes half the clip accessor's background, not the next row's first pixel.
		/// </summary>
		[Test]
		public async Task BilinearReadsPastTheEdgeThroughItsAccessor()
		{
			var image = new ImageBuffer(8, 8, 24, new BlenderBGR());
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					image.SetPixel(x, y, new Color(200, 100, 50, 255));
				}
			}

			var accessor = new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0));
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(4.5, 0.0));

			await Assert.That(Sample(new span_image_filter_rgb_bilinear(accessor, interpolator))).IsEqualTo(new Color(100, 50, 25, 255));
		}

		// An 8x8 black image with column 4 at 1.
		private static ImageBuffer ColumnOfOnes()
		{
			var image = new ImageBuffer(8, 8, 24, new BlenderBGR());
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					image.SetPixel(x, y, x == 4 ? new Color(1, 1, 1, 255) : new Color(0, 0, 0, 255));
				}
			}

			return image;
		}

		private static Color Sample(span_image_filter filter)
		{
			var span = new Color[1];
			filter.generate(span, 0, 3, 3, 1);
			return span[0];
		}
	}
}
