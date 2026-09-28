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
	/// <summary>
	/// C++ AGG's renderer_base::blend_from_color and blend_from_lut: a gray image used as coverage (one colour) or as
	/// an index (a 256-entry colour table), placed at an offset and clipped to the destination.
	/// </summary>
	public class ImageBlendFromTests
	{
		private static readonly Color Background = new Color(255, 242, 242, 255);

		[Test]
		public async Task BlendFromColorUsesEachGrayAsTheColoursCover()
		{
			ImageBuffer dest = NewDest();
			ImageBuffer gray = NewGray(0, 128, 255);
			var color = new Color(0, 100, 0, 255);

			dest.BlendFromColor(gray, color, 1, 2);

			await Assert.That(dest.GetPixel(1, 2)).IsEqualTo(Background);
			await Assert.That(dest.GetPixel(2, 2)).IsEqualTo(Lerped(Background, color, 128));
			await Assert.That(dest.GetPixel(3, 2)).IsEqualTo(color);
			await Assert.That(dest.GetPixel(4, 2)).IsEqualTo(Background);
			await Assert.That(dest.GetPixel(2, 1)).IsEqualTo(Background);
		}

		[Test]
		public async Task BlendFromColorScalesATranslucentColoursAlpha()
		{
			ImageBuffer dest = NewDest();
			var color = new Color(0, 100, 0, 128);

			dest.BlendFromColor(NewGray(255, 128), color, 0, 0);

			await Assert.That(dest.GetPixel(0, 0)).IsEqualTo(Lerped(Background, color, 128));
			await Assert.That(dest.GetPixel(1, 0)).IsEqualTo(Lerped(Background, color, Rgba8Math.Multiply(128, 128)));
		}

		[Test]
		public async Task BlendFromLutBlendsTheColourEachGrayIndexes()
		{
			ImageBuffer dest = NewDest();
			var lut = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				lut[i] = new Color(i, 255 - i, 7, i < 64 ? i * 4 : 255);
			}

			dest.BlendFromLut(NewGray(0, 10, 200), lut, 2, 0);

			await Assert.That(dest.GetPixel(2, 0)).IsEqualTo(Background);
			await Assert.That(dest.GetPixel(3, 0)).IsEqualTo(Lerped(Background, lut[10], 40));
			await Assert.That(dest.GetPixel(4, 0)).IsEqualTo(lut[200]);
		}

		/// <summary>A source hanging off every side of the destination draws only the overlap.</summary>
		[Test]
		public async Task PlacementIsClippedToTheDestination()
		{
			var dest = new ImageBuffer(3, 3);
			dest.NewGraphics2D().Clear(Background);
			var gray = new ImageBuffer(5, 5, 8, new blender_gray(1));
			for (int y = 0; y < 5; y++)
			{
				for (int x = 0; x < 5; x++)
				{
					gray.GetBuffer()[gray.GetBufferOffsetXY(x, y)] = (byte)(x == 1 && y == 1 ? 255 : 0);
				}
			}

			dest.BlendFromColor(gray, Color.Black, -1, -1);
			dest.BlendFromColor(gray, Color.Black, 2, 2);

			await Assert.That(dest.GetPixel(0, 0)).IsEqualTo(Color.Black);
			await Assert.That(dest.GetPixel(1, 1)).IsEqualTo(Background);
			await Assert.That(dest.GetPixel(2, 2)).IsEqualTo(Background);
		}

		private static ImageBuffer NewDest()
		{
			var dest = new ImageBuffer(6, 4);
			dest.NewGraphics2D().Clear(Background);
			return dest;
		}

		private static ImageBuffer NewGray(params int[] values)
		{
			var gray = new ImageBuffer(values.Length, 1, 8, new blender_gray(1));
			for (int x = 0; x < values.Length; x++)
			{
				gray.GetBuffer()[gray.GetBufferOffsetXY(x, 0)] = (byte)values[x];
			}

			return gray;
		}

		// C++ blender_rgb::blend_pix over an opaque pixel: each channel lerped by alpha, alpha kept.
		private static Color Lerped(Color p, Color c, int alpha)
		{
			return new Color(Rgba8Math.Lerp(p.red, c.red, alpha), Rgba8Math.Lerp(p.green, c.green, alpha), Rgba8Math.Lerp(p.blue, c.blue, alpha), 255);
		}
	}
}
