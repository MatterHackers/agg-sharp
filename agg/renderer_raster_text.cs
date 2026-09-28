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

using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's renderer_raster_htext_solid: draws a line of <see cref="glyph_raster_bin"/> text left to right in
	/// one solid color, a glyph row at a time. Clipping is the destination's (pass an <see cref="ImageClippingProxy"/>).
	/// </summary>
	public class renderer_raster_htext_solid
	{
		private readonly IImageByte m_ren;
		private readonly glyph_raster_bin m_glyph;

		public renderer_raster_htext_solid(IImageByte ren, glyph_raster_bin glyph)
		{
			m_ren = ren;
			m_glyph = glyph;
		}

		public Color color { get; set; }

		/// <summary>
		/// Draws <paramref name="str"/> with its first glyph's pen at (<paramref name="x"/>, <paramref name="y"/>);
		/// flip = true for a y-down destination.
		/// </summary>
		public void render_text(double x, double y, string str, bool flip = false)
		{
			foreach (char c in str)
			{
				m_glyph.prepare(out glyph_raster_bin.glyph_rect r, x, y, c, flip);
				if (r.x2 >= r.x1)
				{
					for (int i = r.y1; i <= r.y2; i++)
					{
						m_ren.blend_solid_hspan(r.x1, i, r.x2 - r.x1 + 1, color, m_glyph.span(flip ? r.y2 - i : i - r.y1), 0);
					}
				}

				x += r.dx;
				y += r.dy;
			}
		}
	}

	/// <summary>
	/// C++ AGG's renderer_raster_vtext_solid: <see cref="renderer_raster_htext_solid"/> turned a quarter, the text
	/// running up the destination's y axis (each glyph row becomes a column).
	/// </summary>
	public class renderer_raster_vtext_solid
	{
		private readonly IImageByte m_ren;
		private readonly glyph_raster_bin m_glyph;

		public renderer_raster_vtext_solid(IImageByte ren, glyph_raster_bin glyph)
		{
			m_ren = ren;
			m_glyph = glyph;
		}

		public Color color { get; set; }

		/// <summary>
		/// Draws <paramref name="str"/> with its first glyph's pen at (<paramref name="x"/>, <paramref name="y"/>):
		/// the glyphs advance along y (the pen's x) and their rows stack along x.
		/// </summary>
		public void render_text(double x, double y, string str, bool flip = false)
		{
			foreach (char c in str)
			{
				m_glyph.prepare(out glyph_raster_bin.glyph_rect r, x, y, c, !flip);
				if (r.x2 >= r.x1)
				{
					for (int i = r.y1; i <= r.y2; i++)
					{
						m_ren.blend_solid_vspan(i, r.x1, r.x2 - r.x1 + 1, color, m_glyph.span(flip ? i - r.y1 : r.y2 - i), 0);
					}
				}

				x += r.dx;
				y += r.dy;
			}
		}
	}

	/// <summary>
	/// C++ AGG's renderer_raster_htext over renderer_scanline_aa: raster text whose colors come from a span
	/// generator (a gradient, an image) instead of one solid color. Each glyph row is a one-span scanline.
	/// </summary>
	public class renderer_raster_htext
	{
		private readonly IImageByte m_ren;
		private readonly span_allocator m_alloc;
		private readonly ISpanGenerator m_span_gen;
		private readonly glyph_raster_bin m_glyph;

		public renderer_raster_htext(IImageByte ren, span_allocator alloc, ISpanGenerator spanGenerator, glyph_raster_bin glyph)
		{
			m_ren = ren;
			m_alloc = alloc;
			m_span_gen = spanGenerator;
			m_glyph = glyph;
		}

		/// <summary>As <see cref="renderer_raster_htext_solid.render_text"/>, each pixel colored by the span generator.</summary>
		public void render_text(double x, double y, string str, bool flip = false)
		{
			foreach (char c in str)
			{
				m_glyph.prepare(out glyph_raster_bin.glyph_rect r, x, y, c, flip);
				if (r.x2 >= r.x1)
				{
					// renderer_scanline_aa::prepare, once per glyph as in C++.
					m_span_gen.prepare();
					int len = r.x2 - r.x1 + 1;
					for (int i = r.y1; i <= r.y2; i++)
					{
						byte[] covers = m_glyph.span(flip ? r.y2 - i : i - r.y1);
						Color[] colors = m_alloc.allocate(len).Array;
						m_span_gen.generate(colors, 0, r.x1, i, len);
						m_ren.blend_color_hspan(r.x1, i, len, colors, 0, covers, 0, false);
					}
				}

				x += r.dx;
				y += r.dy;
			}
		}
	}
}
