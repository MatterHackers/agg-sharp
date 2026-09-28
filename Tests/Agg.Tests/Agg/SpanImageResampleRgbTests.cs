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
	// The 24-bit resampling span generators (pattern_resample.cpp's). C++ AGG sums from 0 and divides by the total
	// weight, truncating and so darkening by half a unit on average; agg-sharp (and the reference renderer's
	// patched agg_span_image_filter_rgb.h) add half the total weight first and round.
	public class SpanImageResampleRgbTests
	{
		/// <summary>
		/// At scale 1 and half a pixel over, the bilinear table weighs the column of ones just over half, which rounds
		/// to 1 where C++ truncates to 0.
		/// </summary>
		[Test]
		public async Task AffineResampleRoundsJustOverHalf()
		{
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(0.5, 0.0));
			var filter = new span_image_resample_rgb_affine(Accessor(ColumnOfOnes()), interpolator, new ImageFilterLookUpTable(new image_filter_bilinear(), false));

			Color sample = Sample(filter);

			await Assert.That((int)sample.red).IsEqualTo(1);
			await Assert.That((int)sample.green).IsEqualTo(1);
			await Assert.That((int)sample.blue).IsEqualTo(1);
			await Assert.That((int)sample.alpha).IsEqualTo(255);
		}

		/// <summary>The same through the perspective resampler, its scale from a lerp interpolator under the subdiv adaptor.</summary>
		[Test]
		public async Task PerspectiveResampleRoundsJustOverHalf()
		{
			var filter = new span_image_resample_rgb(Accessor(ColumnOfOnes()), TranslatedByHalf(), new ImageFilterLookUpTable(new image_filter_bilinear(), false));

			Color sample = Sample(filter);

			await Assert.That((int)sample.red).IsEqualTo(1);
		}

		/// <summary>A flat image keeps its exact value through both resamplers, shrunk and blurred: no weight is skipped.</summary>
		[Test]
		public async Task FlatColorKeepsItsValueShrunkAndBlurred()
		{
			var flat = new Color(255, 128, 3, 255);
			var image = new ImageBuffer(64, 64, 24, new BlenderBGR());
			for (int y = 0; y < 64; y++)
			{
				for (int x = 0; x < 64; x++)
				{
					image.SetPixel(x, y, flat);
				}
			}

			var lookUpTable = new ImageFilterLookUpTable(new image_filter_hanning(), true);
			var affine = new span_image_resample_rgb_affine(Accessor(image), new span_interpolator_linear(Affine.NewScaling(3.7, 2.2)), lookUpTable);
			affine.blur(1.5);
			await Assert.That(Sample(affine)).IsEqualTo(flat);

			var shrink = new span_interpolator_persp_lerp(new double[] { 0, 0, 5, 0, 5, 5, 0, 5 }, 0, 0, 20, 20);
			var perspective = new span_image_resample_rgb(Accessor(image), new span_subdiv_adaptor(shrink), lookUpTable);
			perspective.blur(1.5);
			await Assert.That(Sample(perspective)).IsEqualTo(flat);
		}

		/// <summary>
		/// The affine resampler's footprint follows the transform's scale, times the blur, and never drops below a pixel:
		/// C++ span_image_resample_affine::prepare.
		/// </summary>
		[Test]
		public async Task AffinePrepareScalesTheFilterRadius()
		{
			var lookUpTable = new ImageFilterLookUpTable(new image_filter_hanning(), true);
			var filter = new span_image_resample_rgb_affine(Accessor(ColumnOfOnes()), new span_interpolator_linear(Affine.NewScaling(3.0, 0.5)), lookUpTable);
			filter.blur(1.5);

			filter.prepare();

			await Assert.That(filter.ScaleX).IsEqualTo(Util.uround(3.0 * 1.5 * 256));
			await Assert.That(filter.ScaleY).IsEqualTo(Util.uround(1.5 * 256));
		}

		// Frame pixel (x, y) -> (x + 0.5, y) as a perspective, quad to rectangle.
		private static span_subdiv_adaptor TranslatedByHalf()
		{
			var interpolator = new span_interpolator_persp_lerp(new double[] { -0.5, 0, 7.5, 0, 7.5, 8, -0.5, 8 }, 0, 0, 8, 8);
			return new span_subdiv_adaptor(interpolator);
		}

		private static ImageBufferAccessorClip Accessor(ImageBuffer image) => new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0));

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

		// Frame pixel (3, 3), after the span generator's prepare as the scanline renderer calls it.
		private static Color Sample(span_image_filter filter)
		{
			filter.prepare();
			var span = new Color[1];
			filter.generate(span, 0, 3, 3, 1);
			return span[0];
		}
	}
}
