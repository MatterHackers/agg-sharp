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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.ImageProcessing
{
	/// <summary>
	/// The tinting helpers repaint an icon in the theme's ink. They used to write that ink into every
	/// gray pixel including the fully transparent surround, which is invisible when the icon lands on a
	/// straight alpha surface but paints a solid rectangle of ink on a premultiplied one - the
	/// premultiplied blender adds the source color outright (dst * (1 - srcA) + srcColor), so an alpha
	/// zero pixel that still carries color is not transparent at all. Every helper here has to leave
	/// alpha zero pixels completely clear.
	/// </summary>
	public class IconTintTransparencyTests
	{
		/// <summary>
		/// A 4x4 stand-in for an icon: an opaque 2x2 glyph in the middle, an antialiased edge, and a
		/// transparent surround that still carries the color it had before it was made transparent -
		/// exactly what a PNG with a white background decodes to.
		/// </summary>
		private static ImageBuffer IconWithColoredTransparentSurround(Color surroundColor)
		{
			var image = new ImageBuffer(4, 4);

			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					image.SetPixel(x, y, surroundColor);
				}
			}

			// gray glyph, fully opaque
			image.SetPixel(1, 1, new Color(128, 128, 128, 255));
			image.SetPixel(2, 2, new Color(128, 128, 128, 255));
			// gray glyph, antialiased edge
			image.SetPixel(2, 1, new Color(128, 128, 128, 128));
			image.SetPixel(1, 2, new Color(128, 128, 128, 128));

			return image;
		}

		private static async Task AssertTransparentPixelsAreClear(ImageBuffer image, string because)
		{
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					var pixel = image.GetPixel(x, y);
					if (pixel.alpha == 0)
					{
						await Assert.That(pixel.red + pixel.green + pixel.blue).IsEqualTo(0)
							.Because($"{because} - pixel {x},{y} is transparent but carries "
								+ $"{pixel.red},{pixel.green},{pixel.blue}");
					}
				}
			}
		}

		[Test]
		public async Task GrayToColorLeavesTransparentSurroundClear()
		{
			var tinted = IconWithColoredTransparentSurround(new Color(255, 255, 255, 0))
				.GrayToColor(new Color(200, 30, 40));

			await AssertTransparentPixelsAreClear(tinted, "GrayToColor must not ink transparent pixels");
		}

		[Test]
		public async Task WhiteToAlphaClearsTheWhiteItTurnsTransparent()
		{
			var image = IconWithColoredTransparentSurround(new Color(255, 255, 255, 255));

			await AssertTransparentPixelsAreClear(image.WhiteToAlpha(),
				"WhiteToAlpha must clear the white it just made transparent");
		}

		[Test]
		public async Task WhiteToAlphaGreyToColorLeavesTransparentSurroundClear()
		{
			// The path most themed icons take: a white background becomes transparent, then everything
			// gray becomes theme ink.
			var (tinted, _) = IconWithColoredTransparentSurround(new Color(255, 255, 255, 255))
				.WhiteToAlpha_GreyToColor(new Color(200, 30, 40));

			await AssertTransparentPixelsAreClear(tinted,
				"WhiteToAlpha_GreyToColor must not ink transparent pixels");
		}

		[Test]
		public async Task AjustAlphaLeavesTransparentSurroundClear()
		{
			var faded = IconWithColoredTransparentSurround(new Color(255, 255, 255, 0)).AjustAlpha(0.3);

			await AssertTransparentPixelsAreClear(faded,
				"AjustAlpha stamps its result premultiplied, so transparent pixels must be clear");
		}

		[Test]
		public async Task GrayToColorPremultipliesPartialAlphaForAPremultipliedDestination()
		{
			var source = IconWithColoredTransparentSurround(new Color(255, 255, 255, 0));
			var destination = new ImageBuffer(4, 4, 32, new BlenderPreMultBGRA());

			GrayToColorProcess.GrayToColor(destination, source, new Color(200, 30, 40), DestIntensity.FromColor);

			// The half covered edge pixel: a premultiplied buffer wants the color scaled by its alpha.
			var edge = destination.GetPixel(2, 1);
			await Assert.That((int)edge.alpha).IsEqualTo(128);
			await Assert.That((int)edge.red).IsEqualTo(200 * 128 / 255);
			await Assert.That((int)edge.green).IsEqualTo(30 * 128 / 255);
			await Assert.That((int)edge.blue).IsEqualTo(40 * 128 / 255);

			await AssertTransparentPixelsAreClear(destination,
				"GrayToColor must not ink transparent pixels");
		}

		[Test]
		public async Task GrayToColorLeavesPartialAlphaStraightForAStraightAlphaDestination()
		{
			// Straight alpha callers must be untouched by the premultiplying above - their blender
			// scales the source color by alpha itself, so scaling it here would darken every edge.
			var tinted = IconWithColoredTransparentSurround(new Color(255, 255, 255, 0))
				.GrayToColor(new Color(200, 30, 40));

			var edge = tinted.GetPixel(2, 1);
			await Assert.That((int)edge.alpha).IsEqualTo(128);
			await Assert.That((int)edge.red).IsEqualTo(200);
			await Assert.That((int)edge.green).IsEqualTo(30);
			await Assert.That((int)edge.blue).IsEqualTo(40);
		}
	}
}
