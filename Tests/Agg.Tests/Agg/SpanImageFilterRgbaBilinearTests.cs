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
	// The plain span_image_filter_rgba_bilinear (image_filters.cpp's bilinear on 32-bit images), sampling frame
	// pixel (x, y) through a translation of (tx, ty): the filter takes back the half pixel the interpolator adds, so
	// it reads the image at (x + tx, y + ty). Like C++, it reads through the image accessor, so a clamp or wrap
	// accessor decides what lies past the edges.
	public class SpanImageFilterRgbaBilinearTests
	{
		/// <summary>A translucent image keeps its alpha: the filter used to force every sample opaque.</summary>
		[Test]
		public async Task TranslucentSourceKeepsItsAlpha()
		{
			var translucent = new Color(100, 60, 3, 128);
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

			await Assert.That(Sample(new ImageBufferAccessorClamp(image), 1, 1, 0.3, 0.6)).IsEqualTo(translucent);
		}

		/// <summary>
		/// Sampling past the edges through a clamp accessor repeats the edge pixels. The filter used to index the
		/// buffer directly, reading past its end (or before its start) there.
		/// </summary>
		[Test]
		public async Task SamplingPastTheEdgesGoesThroughTheAccessor()
		{
			var image = new ImageBuffer(4, 4);
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					image.SetPixel(x, y, new Color(10, 20, 30, 255));
				}
			}

			var accessor = new ImageBufferAccessorClamp(image);
			await Assert.That(Sample(accessor, 3, 3, 0.5, 0.5)).IsEqualTo(new Color(10, 20, 30, 255));
			await Assert.That(Sample(accessor, -2, -2, 0.25, 0.75)).IsEqualTo(new Color(10, 20, 30, 255));
		}

		private static Color Sample(IImageBufferAccessor accessor, int x, int y, double tx, double ty)
		{
			var interpolator = new span_interpolator_linear(Affine.NewTranslation(tx, ty));
			var filter = new span_image_filter_rgba_bilinear(accessor, interpolator);
			var span = new Color[1];
			filter.generate(span, 0, x, y, 1);
			return span[0];
		}
	}
}
