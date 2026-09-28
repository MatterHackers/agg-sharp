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
	// span_image_filter_rgba_bilinear_clip on a 4x4 image, sampling frame pixel (1, 1) through a translation of
	// (tx, ty): the filter takes back the half pixel the interpolator adds, so it reads the image at
	// (1 + tx, 1 + ty), in 1/256ths of a pixel.
	public class SpanImageFilterRgbaBilinearClipTests
	{
		/// <summary>
		/// Halfway between 0 and 1 is 0.5, which rounds to 1. C++ AGG's bilinear_clip starts its sums at 0 and
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
		/// A flat image keeps its exact value at any fractional offset: every weight counts, even the tiny ones
		/// (1 x 255 and 1 x 1 here) the port used to skip, which took 255 down to 254.
		/// </summary>
		[Test]
		public async Task FlatColorKeepsItsValueAtAFractionalOffset()
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

			Color sample = Sample(image, 1.0 / 256, 255.0 / 256);

			await Assert.That(sample).IsEqualTo(flat);
		}

		/// <summary>
		/// Under an identity matrix a sample wholly outside the image is the background color, as in C++: the
		/// filter always samples, it does not copy source pixels straight through.
		/// </summary>
		[Test]
		public async Task SampleOutsideTheImageIsTheBackgroundUnderAnIdentityMatrix()
		{
			var image = new ImageBuffer(4, 4);
			image.NewGraphics2D().Clear(Color.Red);
			var background = new Color(10, 20, 30, 40);
			var interpolator = new span_interpolator_linear(Affine.NewIdentity());
			var filter = new span_image_filter_rgba_bilinear_clip(new ImageBufferAccessorClip(image, background), background, interpolator);
			var span = new Color[1];

			filter.generate(span, 0, 8, 1, 1);

			await Assert.That(span[0]).IsEqualTo(background);
		}

		private static Color Sample(ImageBuffer image, double tx, double ty)
		{
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(tx, ty));
			var filter = new span_image_filter_rgba_bilinear_clip(new ImageBufferAccessorClip(image, Color.White), Color.White, interpolator);
			var span = new Color[1];
			filter.generate(span, 0, 1, 1, 1);
			return span[0];
		}
	}
}
