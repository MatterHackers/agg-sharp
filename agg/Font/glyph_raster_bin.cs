//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's glyph_raster_bin: reads glyphs out of a 1 bit per pixel raster font such as the
	/// <see cref="EmbeddedRasterFonts"/>.
	/// </summary>
	/// <remarks>
	/// A font is: height, base line, first char code, glyph count (a byte each); a little-endian u16 offset per
	/// glyph; then at each offset the glyph's width in pixels followed by its rows, top row first, each
	/// (width + 7) / 8 bytes, most significant bit leftmost.
	/// Unlike C++, which reads past the table, a char outside the font's range is an empty glyph with no advance.
	/// </remarks>
	public class glyph_raster_bin
	{
		/// <summary>Where <see cref="prepare"/> places a glyph: the inclusive pixel box and the pen advance.</summary>
		public struct glyph_rect
		{
			public int x1, y1, x2, y2;
			public double dx, dy;
		}

		private byte[] m_font;

		// Sized for the widest glyph a byte-wide width can describe; C++ keeps 32 and overruns past that.
		private readonly byte[] m_span = new byte[256];

		private int m_bits;
		private int m_glyph_width;
		private int m_glyph_byte_width;

		public glyph_raster_bin(byte[] font)
		{
			m_font = font;
		}

		public byte[] font()
		{
			return m_font;
		}

		public void font(byte[] f)
		{
			m_font = f;
		}

		/// <summary>The font's line height in pixels.</summary>
		public double height()
		{
			return m_font[0];
		}

		/// <summary>Pixels from the glyph box's bottom row up to the base line.</summary>
		public double base_line()
		{
			return m_font[1];
		}

		/// <summary>The summed advance of <paramref name="str"/>'s glyphs.</summary>
		public double width(string str)
		{
			int w = 0;
			foreach (char c in str)
			{
				int offset = GlyphOffset(c);
				if (offset >= 0)
				{
					w += m_font[offset];
				}
			}

			return w;
		}

		/// <summary>
		/// Places <paramref name="glyph"/> with its pen at (<paramref name="x"/>, <paramref name="y"/>) and makes it
		/// the glyph <see cref="span"/> reads. Unflipped (y up), y is the base line row; flipped (y down), the box
		/// hangs from it the other way.
		/// </summary>
		public void prepare(out glyph_rect r, double x, double y, int glyph, bool flip)
		{
			int offset = GlyphOffset(glyph);
			if (offset < 0)
			{
				m_glyph_width = 0;
				m_glyph_byte_width = 0;
				m_bits = 0;
			}
			else
			{
				m_glyph_width = m_font[offset];
				m_glyph_byte_width = (m_glyph_width + 7) >> 3;
				m_bits = offset + 1;
			}

			r.x1 = (int)x;
			r.x2 = r.x1 + m_glyph_width - 1;
			if (flip)
			{
				r.y1 = (int)y - m_font[0] + m_font[1];
				r.y2 = r.y1 + m_font[0] - 1;
			}
			else
			{
				r.y1 = (int)y - m_font[1] + 1;
				r.y2 = r.y1 + m_font[0] - 1;
			}

			r.dx = m_glyph_width;
			r.dy = 0;
		}

		/// <summary>
		/// The covers (255 set, 0 clear) of the prepared glyph's row <paramref name="i"/> counted from the bottom;
		/// valid until the next call. The first <c>x2 - x1 + 1</c> entries are the row.
		/// </summary>
		public byte[] span(int i)
		{
			i = m_font[0] - i - 1;
			int bits = m_bits + (i * m_glyph_byte_width);
			int val = m_glyph_width > 0 ? m_font[bits] : 0;
			int nb = 0;
			for (int j = 0; j < m_glyph_width; ++j)
			{
				m_span[j] = (byte)((val & 0x80) != 0 ? 255 : 0);
				val <<= 1;
				if (++nb >= 8)
				{
					// C++ reads the byte after the row's last here too; only its first bits are ever used, and at the
					// font's very end there is none, so stop at the row's end instead.
					bits++;
					val = j + 1 < m_glyph_width ? m_font[bits] : 0;
					nb = 0;
				}
			}

			return m_span;
		}

		// The index of the glyph's width byte, or -1 for a char the font does not have.
		private int GlyphOffset(int glyph)
		{
			int start_char = m_font[2];
			int num_chars = m_font[3];
			int index = glyph - start_char;
			if (index < 0 || index >= num_chars)
			{
				return -1;
			}

			int table = 4 + (index * 2);
			return 4 + (num_chars * 2) + (m_font[table] | (m_font[table + 1] << 8));
		}
	}
}
