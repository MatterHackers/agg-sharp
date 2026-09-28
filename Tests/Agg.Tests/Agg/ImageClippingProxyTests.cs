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
	public class ImageClippingProxyTests
	{
		/// <summary>
		/// A color span with one cover for all (C++'s covers == 0, as renderer_outline_image passes) that starts
		/// left of the clip box skips its clipped colors but keeps its one cover; stepping the cover index past the
		/// one-entry cover array read out of bounds.
		/// </summary>
		[Test]
		public async Task ColorHspanWithOneCoverClipsItsColorsNotItsCover()
		{
			var image = new ImageBuffer(6, 1);
			var proxy = new ImageClippingProxy(image);
			proxy.SetClippingBox(2, 0, 5, 0);
			var colors = new Color[5];
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = new Color(10 * i, 0, 0, 255);
			}

			proxy.blend_color_hspan(0, 0, 5, colors, 0, new byte[] { 255 }, 0, true);

			await Assert.That(image.GetPixel(1, 0).red).IsEqualTo((byte)0);
			await Assert.That(image.GetPixel(2, 0).red).IsEqualTo((byte)20);
			await Assert.That(image.GetPixel(4, 0).red).IsEqualTo((byte)40);
		}

		/// <summary>The same for a vertical span starting below the clip box.</summary>
		[Test]
		public async Task ColorVspanWithOneCoverClipsItsColorsNotItsCover()
		{
			var image = new ImageBuffer(1, 6);
			var proxy = new ImageClippingProxy(image);
			proxy.SetClippingBox(0, 2, 0, 5);
			var colors = new Color[5];
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = new Color(10 * i, 0, 0, 255);
			}

			proxy.blend_color_vspan(0, 0, 5, colors, 0, new byte[] { 255 }, 0, true);

			await Assert.That(image.GetPixel(0, 1).red).IsEqualTo((byte)0);
			await Assert.That(image.GetPixel(0, 2).red).IsEqualTo((byte)20);
			await Assert.That(image.GetPixel(0, 4).red).IsEqualTo((byte)40);
		}

		/// <summary>
		/// copy_hline's third argument is a length (IImageByte's contract, C++ pixfmt copy_hline): a proxy clips the
		/// run to its box and hands its image the clipped length. It took an end x and handed on a length, so a run
		/// was cut short and, through a proxy over a proxy, the length was read as an end x.
		/// </summary>
		[Test]
		public async Task CopyHlineTakesALengthThroughAProxyAndAProxyOverAProxy()
		{
			var image = new ImageBuffer(10, 1);
			var proxy = new ImageClippingProxy(image);
			proxy.SetClippingBox(2, 0, 8, 0);
			proxy.copy_hline(0, 0, 6, Color.Black);
			await Assert.That(RowAlphas(image)).IsEqualTo("0011110000");

			image = new ImageBuffer(10, 1);
			var outer = new ImageClippingProxy(new ImageClippingProxy(image));
			outer.SetClippingBox(3, 0, 9, 0);
			outer.copy_hline(4, 0, 3, Color.Black);
			await Assert.That(RowAlphas(image)).IsEqualTo("0000111000");
		}

		/// <summary>The same for copy_vline, whose third argument is a height.</summary>
		[Test]
		public async Task CopyVlineTakesALengthThroughAProxyAndAProxyOverAProxy()
		{
			var image = new ImageBuffer(1, 10);
			var proxy = new ImageClippingProxy(image);
			proxy.SetClippingBox(0, 2, 0, 8);
			proxy.copy_vline(0, 0, 6, Color.Black);
			await Assert.That(ColumnAlphas(image)).IsEqualTo("0011110000");

			image = new ImageBuffer(1, 10);
			var outer = new ImageClippingProxy(new ImageClippingProxy(image));
			outer.SetClippingBox(0, 3, 0, 9);
			outer.copy_vline(0, 4, 3, Color.Black);
			await Assert.That(ColumnAlphas(image)).IsEqualTo("0000111000");
		}

		private static string RowAlphas(ImageBuffer image)
		{
			var row = new char[image.Width];
			for (int x = 0; x < image.Width; x++)
			{
				row[x] = image.GetPixel(x, 0).alpha == 0 ? '0' : '1';
			}

			return new string(row);
		}

		private static string ColumnAlphas(ImageBuffer image)
		{
			var column = new char[image.Height];
			for (int y = 0; y < image.Height; y++)
			{
				column[y] = image.GetPixel(0, y).alpha == 0 ? '0' : '1';
			}

			return new string(column);
		}
	}
}
