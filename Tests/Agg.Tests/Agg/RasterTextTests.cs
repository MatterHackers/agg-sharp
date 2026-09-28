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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// glyph_raster_bin, the embedded raster fonts and the raster text renderer against C++ AGG. The expected bytes are
	// copied from agg_embedded_raster_fonts.cpp's verdana12 (header 12, 3, 32, 128-32; 'A' and 'W' below).
	public class RasterTextTests
	{
		// verdana12 'A': 8 wide, one byte per row, top row first.
		private static readonly byte[] VerdanaA = { 0x00, 0x00, 0x00, 0x18, 0x18, 0x24, 0x24, 0x7E, 0x42, 0x42, 0x00, 0x00 };

		// verdana12 'W': 9 wide, two bytes per row.
		private static readonly byte[] VerdanaW =
		{
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x41, 0x00, 0x49, 0x00, 0x49, 0x00, 0x55, 0x00, 0x55, 0x00, 0x22, 0x00, 0x22, 0x00, 0x00, 0x00, 0x00, 0x00,
		};

		/// <summary>All 34 C++ fonts are embedded, in the C++ order, with their C++ headers.</summary>
		[Test]
		public async Task EveryCppFontIsEmbedded()
		{
			await Assert.That(EmbeddedRasterFonts.Names.Count).IsEqualTo(34);
			await Assert.That(EmbeddedRasterFonts.Names.First()).IsEqualTo("gse4x6");
			await Assert.That(EmbeddedRasterFonts.Names.Last()).IsEqualTo("verdana18_bold");
			await Assert.That(EmbeddedRasterFonts.Get("verdana12").Take(4).ToArray()).IsEquivalentTo(new byte[] { 12, 3, 32, 128 - 32 });
			await Assert.That(EmbeddedRasterFonts.Get("gse4x6").Take(4).ToArray()).IsEquivalentTo(new byte[] { 6, 0, 32, 128 - 32 });
		}

		/// <summary>Height, base line, string width and a glyph's box and advance, as C++ glyph_raster_bin gives them.</summary>
		[Test]
		public async Task GlyphMetricsMatchCpp()
		{
			var glyph = new glyph_raster_bin(EmbeddedRasterFonts.Get("verdana12"));
			await Assert.That(glyph.height()).IsEqualTo(12.0);
			await Assert.That(glyph.base_line()).IsEqualTo(3.0);
			await Assert.That(glyph.width("AW")).IsEqualTo(17.0);

			glyph.prepare(out glyph_raster_bin.glyph_rect r, 5.7, 20.9, 'W', false);
			await Assert.That((r.x1, r.x2, r.y1, r.y2, r.dx, r.dy)).IsEqualTo((5, 13, 18, 29, 9.0, 0.0));

			glyph.prepare(out r, 5.7, 20.9, 'W', true);
			await Assert.That((r.y1, r.y2)).IsEqualTo((11, 22));
		}

		/// <summary>"AW" through renderer_raster_htext_solid lands each font bit on its C++ pixel, y up and y down,
		/// including rows that span two bytes.</summary>
		[Test]
		public async Task RenderedGlyphsMatchCpp()
		{
			foreach (bool flip in new[] { false, true })
			{
				var image = new ImageBuffer(40, 30);
				image.NewGraphics2D().Clear(Color.White);
				var glyph = new glyph_raster_bin(EmbeddedRasterFonts.Get("verdana12"));
				var text = new renderer_raster_htext_solid(new ImageClippingProxy(image), glyph) { color = Color.Black };
				text.render_text(2, 14, "AW", flip);

				// Bitmap row k (from the top) is image row y1 + 11 - k y up and y1 + k y down.
				int y1 = flip ? 14 - 12 + 3 : 14 - 3 + 1;
				int mismatches = 0;
				for (int y = 0; y < image.Height; y++)
				{
					for (int x = 0; x < image.Width; x++)
					{
						int k = flip ? y - y1 : y1 + 11 - y;
						bool set = false;
						if (k >= 0 && k < 12)
						{
							if (x >= 2 && x < 10)
							{
								set = (VerdanaA[k] & (0x80 >> (x - 2))) != 0;
							}
							else if (x >= 10 && x < 19)
							{
								int bit = x - 10;
								set = (VerdanaW[(k * 2) + (bit >> 3)] & (0x80 >> (bit & 7))) != 0;
							}
						}

						if (image.GetPixel(x, y) != (set ? Color.Black : Color.White))
						{
							mismatches++;
						}
					}
				}

				await Assert.That(mismatches).IsEqualTo(0);
			}
		}

		/// <summary>A char the font has no glyph for draws nothing and does not move the pen (C++ reads past its table).</summary>
		[Test]
		public async Task CharOutsideTheFontIsEmpty()
		{
			var glyph = new glyph_raster_bin(EmbeddedRasterFonts.Get("gse4x6"));
			glyph.prepare(out glyph_raster_bin.glyph_rect r, 3, 3, 0x2014, false);
			await Assert.That(r.x2 < r.x1).IsTrue();
			await Assert.That(r.dx).IsEqualTo(0.0);
			await Assert.That(glyph.width("a—b")).IsEqualTo(glyph.width("ab"));
		}
	}
}
