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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg's built-in icon font, Font Awesome 6 Free Solid, read by agg-sharp's own TrueType engine.
	public class IconFontTests
	{
		[Test]
		public async Task EveryNamedConstantIsAGlyphInTheFont()
		{
			var named = NamedGlyphs().ToList();
			var missing = named
				.Where(n => n.Glyph.Length != 1 || !IconFont.TypeFace.HasGlyph(n.Glyph[0]))
				.Select(n => n.Name)
				.ToList();

			await Assert.That(named.Count).IsGreaterThanOrEqualTo(30);
			await Assert.That(missing).IsEmpty();
		}

		[Test]
		public async Task EveryNamedGlyphRendersInkInItsEmBox()
		{
			foreach ((string name, string glyph) in NamedGlyphs())
			{
				ImageBuffer image = IconFont.Render(glyph, Color.Black, 16);

				await Assert.That(image).IsNotNull().Because(name);
				await Assert.That(image.Width).IsEqualTo(16);
				await Assert.That(image.Height).IsEqualTo(16);
				await Assert.That(LowestInkRow(image)).IsGreaterThanOrEqualTo(0).Because(name);
			}
		}

		/// <summary>
		/// The em box puts every glyph on one baseline at one scale: Font Awesome's B and I both stand on the
		/// baseline, so their lowest inked rows match, and the short caret sits above that row rather than being
		/// centred in the square as <see cref="GlyphIcon.Render"/> would.
		/// </summary>
		[Test]
		public async Task GlyphsShareOneBaselineInTheEmBox()
		{
			int boldBottom = LowestInkRow(IconFont.Render(IconFont.Bold, Color.Black, 32));
			int italicBottom = LowestInkRow(IconFont.Render(IconFont.Italic, Color.Black, 32));

			await Assert.That(boldBottom).IsEqualTo(italicBottom);
		}

		private static IEnumerable<(string Name, string Glyph)> NamedGlyphs()
		{
			return typeof(IconFont).GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(f => f.IsLiteral && f.FieldType == typeof(string))
				.Select(f => (f.Name, (string)f.GetRawConstantValue()));
		}

		// ImageBuffer row 0 is the bottom; -1 for an image with no ink
		private static int LowestInkRow(ImageBuffer image)
		{
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					if (image.GetPixel(x, y).alpha > 128)
					{
						return y;
					}
				}
			}

			return -1;
		}
	}
}
