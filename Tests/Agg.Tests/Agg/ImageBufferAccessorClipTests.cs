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

namespace MatterHackers.Agg.Tests
{
	// ImageBufferAccessorClip (and its float twin) is C++'s image_accessor_clip: a read outside the image returns the background
	// colour, laid out as the image's pixels are, so a filter reading OrderR/OrderG/OrderB/OrderA gets it back.
	public class ImageBufferAccessorClipTests
	{
		private static readonly Color Inside = new Color(200, 150, 100, 255);

		private static readonly Color Background = new Color(10, 20, 30, 40);

		[Test]
		public async Task ReadsOutsideA32BitImageAreTheBackground()
		{
			var accessor = new ImageBufferAccessorClip(Filled(new ImageBuffer(2, 2)), Background);

			// A span starting left of the image: outside, inside, inside, outside on the right.
			await Assert.That(Read(accessor.span(-1, 0, 4, out int offset), offset)).IsEqualTo(Background);
			await Assert.That(Read(accessor.next_x(out offset), offset)).IsEqualTo(Inside);
			await Assert.That(Read(accessor.next_x(out offset), offset)).IsEqualTo(Inside);
			await Assert.That(Read(accessor.next_x(out offset), offset)).IsEqualTo(Background);

			// A span wholly inside, then the next row down runs off the top of the image.
			await Assert.That(Read(accessor.span(0, 1, 2, out offset), offset)).IsEqualTo(Inside);
			await Assert.That(Read(accessor.next_y(out offset), offset)).IsEqualTo(Background);
		}

		[Test]
		public async Task ReadsOutsideA24BitImageAreTheBackground()
		{
			var accessor = new ImageBufferAccessorClip(Filled(new ImageBuffer(2, 2, 24, new BlenderBGR())), Background);

			byte[] buffer = accessor.span(0, -1, 1, out int offset);
			await Assert.That(ReadRgb(buffer, offset)).IsEqualTo((Background.red, Background.green, Background.blue));
			buffer = accessor.next_y(out offset);
			await Assert.That(ReadRgb(buffer, offset)).IsEqualTo((Inside.red, Inside.green, Inside.blue));
		}

		[Test]
		public async Task ReadsOutsideAFloatImageAreTheBackground()
		{
			var image = new ImageBufferFloat(2, 2, 128, new BlenderBGRAFloat());
			float[] pixels = image.GetBuffer();
			for (int y = 0; y < 2; y++)
			{
				for (int x = 0; x < 2; x++)
				{
					int at = image.GetBufferOffsetXY(x, y);
					pixels[at + ImageBufferFloat.OrderR] = .8f;
					pixels[at + ImageBufferFloat.OrderG] = .6f;
					pixels[at + ImageBufferFloat.OrderB] = .4f;
					pixels[at + ImageBufferFloat.OrderA] = 1;
				}
			}

			var accessor = new ImageBufferAccessorClipFloat(image, new ColorF(.1, .2, .3, .4));

			float[] buffer = accessor.span(-1, 0, 3, out int offset);
			await Assert.That(ReadFloat(buffer, offset)).IsEqualTo((.1f, .2f, .3f, .4f));
			buffer = accessor.next_x(out offset);
			await Assert.That(ReadFloat(buffer, offset)).IsEqualTo((.8f, .6f, .4f, 1f));
			buffer = accessor.next_x(out offset);
			buffer = accessor.next_x(out offset);
			await Assert.That(ReadFloat(buffer, offset)).IsEqualTo((.1f, .2f, .3f, .4f));
		}

		/// <summary>
		/// span_image_filter_rgba (Best quality's spline16) sampling across the image's left edge, halfway between
		/// the transparent background and an opaque red column: the filter must read each pixel from the array the
		/// accessor handed back for it, the background's for the outside taps and the image's for the inside ones.
		/// </summary>
		[Test]
		public async Task TheRgbaFilterBlendsIntoTheBackgroundAcrossTheEdge()
		{
			var image = new ImageBuffer(8, 8);
			image.NewGraphics2D().Clear(Color.Red);
			var filter = new ImageFilterLookUpTable();
			filter.calculate(new image_filter_spline16(), true);

			// Frame x maps to source x + 0.5, so frame -1 samples the source at -0.5: half outside, half in.
			var interpolator = new span_interpolator_linear(Transform.Affine.NewTranslation(0.5, 0));
			var generator = new span_image_filter_rgba(new ImageBufferAccessorClip(image, new Color(0, 0, 0, 0)), interpolator, filter);
			var span = new Color[8];
			generator.generate(span, 0, -4, 4, 8);

			await Assert.That(span[0]).IsEqualTo(new Color(0, 0, 0, 0));
			await Assert.That((int)span[3].alpha).IsBetween(100, 156);
			await Assert.That(span[3].red).IsEqualTo(span[3].alpha);
			await Assert.That(span[3].green).IsEqualTo((byte)0);
			await Assert.That(span[7]).IsEqualTo(new Color(255, 0, 0, 255));
		}

		private static (float, float, float, float) ReadFloat(float[] buffer, int offset) => (
			buffer[offset + ImageBufferFloat.OrderR], buffer[offset + ImageBufferFloat.OrderG], buffer[offset + ImageBufferFloat.OrderB], buffer[offset + ImageBufferFloat.OrderA]);

		private static ImageBuffer Filled(ImageBuffer image)
		{
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					image.SetPixel(x, y, Inside);
				}
			}

			return image;
		}

		private static Color Read(byte[] buffer, int offset) => new Color(
			buffer[offset + ImageBuffer.OrderR], buffer[offset + ImageBuffer.OrderG], buffer[offset + ImageBuffer.OrderB], buffer[offset + ImageBuffer.OrderA]);

		private static (byte, byte, byte) ReadRgb(byte[] buffer, int offset) =>
			(buffer[offset + ImageBuffer.OrderR], buffer[offset + ImageBuffer.OrderG], buffer[offset + ImageBuffer.OrderB]);
	}
}
