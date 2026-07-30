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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="LcdComposite"/>: the third stage of the LCD pipeline, where the draw color meets
	/// the destination and each subpixel blends by its own coverage. Ported from the agg-gui Rust
	/// reference's <c>composite_lcd_mask</c> tests (<c>lcd_coverage\tests.rs</c>).
	/// </summary>
	public class LcdCompositeTests
	{
		/// <summary>
		/// The whole formula on one pixel, hand-computed in float and pinned to exact bytes:
		/// <c>cover = mask / 255 * srcAlpha</c>, then <c>dst = src * cover + dst * (1 - cover)</c> per
		/// channel, quantized with a half-up round. The mask's three coverages differ deliberately, which is
		/// also the byte-order check: coverage 200 belongs to red and coverage 0 to blue, so a BGRA/RGBA mixup
		/// would swap 202 and 250.
		/// </summary>
		[Test]
		public async Task SinglePixelBlendsEachChannelByItsOwnCoverage()
		{
			ImageBuffer destination = Destination(1, 1, new Color(10, 200, 250, 137));
			var mask = new LcdMask(new byte[] { 200, 100, 0 }, 1, 1);

			LcdComposite.Composite(destination, mask, new Color(255, 128, 64, 255), 0, 0);

			// R: 1.0 * (200/255) + (10/255) * (55/255) = 0.792772 -> 202.157 -> 202
			// G: (128/255) * (100/255) + (200/255) * (155/255) = 0.673587 -> 171.765 -> 172
			// B: coverage 0, so the destination byte survives its own round trip exactly.
			await AssertPixel(new Color(202, 172, 250, 137), destination, 0, 0);
		}

		/// <summary>
		/// The source alpha scales every channel's coverage, and the destination's alpha is never written.
		/// Ported from the reference's <c>test_composite_lcd_mask_honours_src_alpha</c>, which exists because
		/// a partial-alpha draw color (a half-opacity "dim" placeholder text color) blitted at full strength
		/// without the modulation.
		/// </summary>
		[Test]
		public async Task SourceAlphaScalesCoverageWhileDestinationAlphaSurvives()
		{
			var fullCoverage = new LcdMask(new byte[] { 255, 255, 255 }, 1, 1);

			ImageBuffer opaque = Destination(1, 1, new Color(255, 255, 255, 137));
			LcdComposite.Composite(opaque, fullCoverage, new Color(0, 0, 0, 255), 0, 0);
			await AssertPixel(new Color(0, 0, 0, 137), opaque, 0, 0);

			// Half alpha over white: cover = 128/255, so the result is 1 - 128/255 = 127/255 -> 127.
			ImageBuffer half = Destination(1, 1, new Color(255, 255, 255, 137));
			LcdComposite.Composite(half, fullCoverage, new Color(0, 0, 0, 128), 0, 0);
			await AssertPixel(new Color(127, 127, 127, 137), half, 0, 0);

			// Zero alpha zeroes every coverage, which is the reference's skip case: nothing is written.
			ImageBuffer transparent = Destination(1, 1, new Color(255, 255, 255, 137));
			LcdComposite.Composite(transparent, fullCoverage, new Color(0, 0, 0, 0), 0, 0);
			await AssertPixel(new Color(255, 255, 255, 137), transparent, 0, 0);
		}

		/// <summary>
		/// Both polarities of a real path composite, against the same mid-gray background so neither
		/// direction is pinned by saturation: dark ink may only ever darken a channel, light ink may only
		/// ever lighten one, and each has to actually move something. The mask carries no destination
		/// knowledge, so one mask must work over either background - that is what the reference's
		/// <c>test_composite_dark_on_light_and_light_on_dark</c> guards, tightened here from "summed
		/// brightness moved" to a per-channel no-inversion check.
		/// </summary>
		[Test]
		public async Task DarkAndLightInkMoveOnlyTowardTheSourceColor()
		{
			LcdMask mask = TriangleMask(20, 20);
			const byte Background = 128;

			ImageBuffer darkOnGray = Destination(20, 20, new Color(Background, Background, Background, 255));
			LcdComposite.Composite(darkOnGray, mask, new Color(0, 0, 0, 255), 0, 0);

			ImageBuffer lightOnGray = Destination(20, 20, new Color(Background, Background, Background, 255));
			LcdComposite.Composite(lightOnGray, mask, new Color(255, 255, 255, 255), 0, 0);

			bool darkened = false;
			bool lightened = false;
			for (int y = 0; y < mask.Height; y++)
			{
				for (int x = 0; x < mask.Width; x++)
				{
					// Alpha is deliberately not checked here - the composite never writes it, and
					// SourceAlphaScalesCoverageWhileDestinationAlphaSurvives covers that.
					Color dark = ReadPixel(darkOnGray, x, y);
					Color light = ReadPixel(lightOnGray, x, y);
					byte[] darkChannels = { dark.red, dark.green, dark.blue };
					byte[] lightChannels = { light.red, light.green, light.blue };

					for (int channel = 0; channel < darkChannels.Length; channel++)
					{
						await Assert.That(darkChannels[channel]).IsLessThanOrEqualTo(Background)
							.Because($"dark ink brightened {"RGB"[channel]} at ({x}, {y})");
						await Assert.That(lightChannels[channel]).IsGreaterThanOrEqualTo(Background)
							.Because($"light ink darkened {"RGB"[channel]} at ({x}, {y})");
						darkened |= darkChannels[channel] < Background;
						lightened |= lightChannels[channel] > Background;
					}
				}
			}

			await Assert.That(darkened).IsTrue().Because("dark ink must darken some channel");
			await Assert.That(lightened).IsTrue().Because("light ink must lighten some channel");
		}

		/// <summary>
		/// A mask hanging off the destination is clipped, not an error: only the overlap composites, the rest
		/// of the destination is untouched, and a placement entirely off the destination writes nothing at
		/// all. An unclipped implementation would throw or corrupt the row above/below through the stride.
		/// </summary>
		[Test]
		public async Task MaskOverlappingTheDestinationEdgeCompositesOnlyTheOverlap()
		{
			var fullCoverage = new LcdMask(4, 4);
			for (int i = 0; i < fullCoverage.Data.Length; i++)
			{
				fullCoverage.Data[i] = 255;
			}

			// Bottom-left overhang: only the mask's upper-right 2x2 corner lands on the destination.
			ImageBuffer destination = Destination(4, 4, new Color(255, 255, 255, 255));
			LcdComposite.Composite(destination, fullCoverage, new Color(0, 0, 0, 255), -2, -2);

			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					Color expected = x < 2 && y < 2
						? new Color(0, 0, 0, 255)
						: new Color(255, 255, 255, 255);
					await AssertPixel(expected, destination, x, y);
				}
			}

			// Top-right overhang: one pixel of overlap.
			ImageBuffer corner = Destination(4, 4, new Color(255, 255, 255, 255));
			LcdComposite.Composite(corner, fullCoverage, new Color(0, 0, 0, 255), 3, 3);
			await AssertPixel(new Color(0, 0, 0, 255), corner, 3, 3);
			await AssertPixel(new Color(255, 255, 255, 255), corner, 2, 3);
			await AssertPixel(new Color(255, 255, 255, 255), corner, 3, 2);

			// Entirely off the destination, in both directions: nothing written, nothing thrown.
			ImageBuffer missed = Destination(4, 4, new Color(255, 255, 255, 255));
			LcdComposite.Composite(missed, fullCoverage, new Color(0, 0, 0, 255), -10, 0);
			LcdComposite.Composite(missed, fullCoverage, new Color(0, 0, 0, 255), 0, 100);
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					await AssertPixel(new Color(255, 255, 255, 255), missed, x, y);
				}
			}
		}

		/// <summary>
		/// All three stages end to end, on the property that justifies the whole pipeline: a
		/// <b>neutral</b> draw color over a neutral background still leaves <b>colored</b> destination
		/// pixels, because each subpixel blended by its own coverage. A whole-pixel coverage path (or a mask
		/// composited to the wrong channels) could not produce that.
		/// <para>
		/// This also pins <see cref="BoundedMaskBuilder"/>'s reported origin against
		/// <see cref="LcdComposite"/>'s: a mask placed at the wrong origin would land the fringing in the
		/// wrong place, which the untouched-corner assertion catches.
		/// </para>
		/// </summary>
		[Test]
		public async Task BoundedMaskCompositesFringingAtTheReportedOrigin()
		{
			ImageBuffer destination = Destination(20, 20, new Color(255, 255, 255, 255));

			bool built = BoundedMaskBuilder.TryBuild(
				20, 20, FractionalTriangle(), Affine.NewIdentity(), out LcdMask mask, out int originX, out int originY);
			await Assert.That(built).IsTrue();

			LcdComposite.Composite(destination, mask, new Color(0, 0, 0, 255), originX, originY);

			bool sawFringing = false;
			for (int y = 0; y < 20 && !sawFringing; y++)
			{
				for (int x = 0; x < 20; x++)
				{
					Color pixel = ReadPixel(destination, x, y);
					if (pixel.red != pixel.green || pixel.green != pixel.blue)
					{
						sawFringing = true;
						break;
					}
				}
			}

			await Assert.That(sawFringing).IsTrue()
				.Because("a neutral color composited through an LCD mask must leave per-channel color fringing");

			// The triangle lives at x 10..15, y 10..15 and the mask is padded by 2px, so the far corners of
			// the destination cannot be touched wherever the origin lands correctly.
			await AssertPixel(new Color(255, 255, 255, 255), destination, 0, 0);
			await AssertPixel(new Color(255, 255, 255, 255), destination, 19, 19);
		}

		/// <summary>
		/// A 20x20 triangle mask with anti-aliased edges on every side, so composites have both partial and
		/// full coverage to work with.
		/// </summary>
		private static LcdMask TriangleMask(int width, int height)
		{
			var builder = new LcdMaskBuilder(width, height);
			builder.AddPath(Affine.NewIdentity(), FractionalTriangle());

			return builder.FinalizeMask();
		}

		/// <summary>
		/// The same deliberately fractional triangle the mask-builder tests raster, so both stages are pinned
		/// against one path rather than two that could drift apart.
		/// </summary>
		private static VertexStorage FractionalTriangle()
		{
			return LcdMaskBuilderTests.FractionalTriangle();
		}

		/// <summary>
		/// A 32 bit-per-pixel destination filled with <paramref name="fill"/>, in the straight
		/// (non-premultiplied) convention <see cref="LcdComposite"/> requires.
		/// </summary>
		private static ImageBuffer Destination(int width, int height, Color fill)
		{
			var image = new ImageBuffer(width, height, 32, new BlenderBGRA());
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					WritePixel(image, x, y, fill);
				}
			}

			return image;
		}

		/// <summary>
		/// Reads and writes go straight to the bytes rather than through the blender, so these assertions
		/// pin the actual memory layout <see cref="LcdComposite"/> writes.
		/// </summary>
		private static void WritePixel(ImageBuffer image, int x, int y, Color color)
		{
			byte[] buffer = image.GetBuffer();
			int offset = image.GetBufferOffsetXY(x, y);
			buffer[offset + ImageBuffer.OrderR] = color.red;
			buffer[offset + ImageBuffer.OrderG] = color.green;
			buffer[offset + ImageBuffer.OrderB] = color.blue;
			buffer[offset + ImageBuffer.OrderA] = color.alpha;
		}

		private static Color ReadPixel(ImageBuffer image, int x, int y)
		{
			byte[] buffer = image.GetBuffer();
			int offset = image.GetBufferOffsetXY(x, y);

			return new Color(
				buffer[offset + ImageBuffer.OrderR],
				buffer[offset + ImageBuffer.OrderG],
				buffer[offset + ImageBuffer.OrderB],
				buffer[offset + ImageBuffer.OrderA]);
		}

		private static async Task AssertPixel(Color expected, ImageBuffer image, int x, int y)
		{
			Color actual = ReadPixel(image, x, y);
			await Assert.That(actual.red).IsEqualTo(expected.red).Because($"red at ({x}, {y})");
			await Assert.That(actual.green).IsEqualTo(expected.green).Because($"green at ({x}, {y})");
			await Assert.That(actual.blue).IsEqualTo(expected.blue).Because($"blue at ({x}, {y})");
			await Assert.That(actual.alpha).IsEqualTo(expected.alpha).Because($"alpha at ({x}, {y})");
		}
	}
}
