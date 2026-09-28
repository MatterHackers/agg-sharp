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
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	/// <summary><see cref="ImageGraphics2D"/>'s <see cref="IImageFilterGraphics"/> as a draw among others.</summary>
	public class ImageFilteredFillTests
	{
		private const int Size = 16;

		[Test]
		public async Task DeferredDrawsLandBeforeTheFilteredImage()
		{
			var target = new ImageBuffer(Size, Size);
			Graphics2D graphics = target.NewGraphics2D();
			var image = new ImageBuffer(Size, Size);
			image.NewGraphics2D().Clear(Color.Blue);

			// Held until the next draw: it has to land under the image, not over it.
			graphics.DeferDraws(() => graphics.FillRectangle(0, 0, Size, Size, Color.Red));
			((IImageFilterGraphics)graphics).FillPathWithFilteredImage(new RoundedRect(0, 0, Size, Size, 0), image, new ImageFilterFill(Affine.NewIdentity()));
			graphics.FlushDeferredDraws();

			await Assert.That(target.GetPixel(Size / 2, Size / 2)).IsEqualTo(Color.Blue);
		}

		[Test]
		public async Task BrightnessPicksEachPixelsAlpha()
		{
			var target = new ImageBuffer(Size, Size);
			var image = new ImageBuffer(Size, Size);
			image.NewGraphics2D().Clear(new Color(100, 50, 150));
			var table = new byte[256 * 3];
			table[300 * table.Length / (3 * 255)] = 77;

			((IImageFilterGraphics)target.NewGraphics2D()).FillPathWithFilteredImage(new RoundedRect(0, 0, Size, Size, 0), image, new ImageFilterFill(Affine.NewIdentity()) { BrightnessToAlpha = table });

			// Onto transparent black the drawn alpha is the pixel's own: the table's entry for its brightness.
			await Assert.That(target.GetPixel(Size / 2, Size / 2).alpha).IsEqualTo((byte)77);
		}

		[Test]
		public async Task OpaqueGivesFullAlpha()
		{
			var target = new ImageBuffer(Size, Size);
			var image = new ImageBuffer(Size, Size);
			image.NewGraphics2D().Clear(new Color(100, 50, 150, 128));

			((IImageFilterGraphics)target.NewGraphics2D()).FillPathWithFilteredImage(new RoundedRect(0, 0, Size, Size, 0), image, new ImageFilterFill(Affine.NewIdentity()) { Opaque = true });

			// As the rgb generators give: the image's colour, whatever its alpha, fully opaque.
			await Assert.That(target.GetPixel(Size / 2, Size / 2)).IsEqualTo(new Color(100, 50, 150, 255));
		}

		[Test]
		[Arguments(ImageFilterKind.Filter)]
		[Arguments(ImageFilterKind.Resample)]
		public async Task OpaqueClampsColourOnlyToFull(ImageFilterKind kind)
		{
			var target = new ImageBuffer(Size, Size);
			var image = new ImageBuffer(Size, Size);
			image.NewGraphics2D().Clear(new Color(200, 100, 50, 128));
			var hermite = new ImageFilterLookUpTable();
			hermite.calculate(new image_filter_hermite(), true);

			((IImageFilterGraphics)target.NewGraphics2D()).FillPathWithFilteredImage(new RoundedRect(0, 0, Size, Size, 0), image, new ImageFilterFill(Affine.NewIdentity()) { Kind = kind, Filter = hermite, Opaque = true });

			// The rgb generators never read alpha, so no colour is held to it: hermite, 2 wide, runs the 2x2
			// generator, and it and the resample clamp colour to alpha unless told the result is opaque.
			await Assert.That(target.GetPixel(Size / 2, Size / 2)).IsEqualTo(new Color(200, 100, 50, 255));
		}
	}
}
