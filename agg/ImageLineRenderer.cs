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
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------

using System;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	//===================================================renderer_outline_image
	/// <summary>
	/// C++ <c>renderer_outline_image</c>: draws each line of a <see cref="rasterizer_outline_aa"/> as a strip of
	/// <see cref="line_image_pattern"/> repeating along it, continuing the pattern from one segment to the next.
	/// Only accurate (miter) joins are drawn, so the rasterizer is forced to them; caps, dots and pies draw nothing.
	/// </summary>
	public class ImageLineRenderer : LineRenderer
	{
		// C++ passes no covers: every color at full cover.
		private static readonly byte[] FullCover = { 255 };

		private IImageByte m_ren;
		private line_image_pattern m_pattern;
		private int m_start;
		private double m_scale_x;
		private RectangleInt m_clip_box;
		private bool m_clipping;

		public ImageLineRenderer(IImageByte ren, line_image_pattern patt)
		{
			m_ren = ren;
			m_pattern = patt;
			m_start = 0;
			m_scale_x = 1.0;
			m_clip_box = new RectangleInt(0, 0, 0, 0);
			m_clipping = false;
		}

		public override bool AccurateJoinOnly => true;

		public void attach(IImageByte ren)
		{
			m_ren = ren;
		}

		public void pattern(line_image_pattern p)
		{
			m_pattern = p;
		}

		public line_image_pattern pattern()
		{
			return m_pattern;
		}

		public void reset_clipping()
		{
			m_clipping = false;
		}

		public void clip_box(double x1, double y1, double x2, double y2)
		{
			m_clip_box.Left = line_coord_sat.conv(x1);
			m_clip_box.Bottom = line_coord_sat.conv(y1);
			m_clip_box.Right = line_coord_sat.conv(x2);
			m_clip_box.Top = line_coord_sat.conv(y2);
			m_clipping = true;
		}

		/// <summary>C++ <c>scale_x</c>: how much the pattern stretches along the line.</summary>
		public void scale_x(double s)
		{
			m_scale_x = s;
		}

		public double scale_x()
		{
			return m_scale_x;
		}

		/// <summary>C++ <c>start_x</c>: where along the pattern the next line starts.</summary>
		public void start_x(double s)
		{
			m_start = Util.iround(s * LineAABasics.line_subpixel_scale);
		}

		public double start_x()
		{
			return (double)m_start / LineAABasics.line_subpixel_scale;
		}

		public int subpixel_width()
		{
			return m_pattern.line_width();
		}

		public int pattern_width()
		{
			return m_pattern.pattern_width();
		}

		public double width()
		{
			return (double)subpixel_width() / LineAABasics.line_subpixel_scale;
		}

		public void pixel(Color[] p, int offset, int x, int y)
		{
			m_pattern.pixel(p, offset, x, y);
		}

		public void blend_color_hspan(int x, int y, int len, Color[] colors, int colorsOffset)
		{
			m_ren.blend_color_hspan(x, y, len, colors, colorsOffset, FullCover, 0, true);
		}

		public void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsOffset)
		{
			m_ren.blend_color_vspan(x, y, len, colors, colorsOffset, FullCover, 0, true);
		}

		public override void semidot(CompareFunction cmp, int xc1, int yc1, int xc2, int yc2)
		{
		}

		public override void semidot_hline(CompareFunction cmp, int xc1, int yc1, int xc2, int yc2, int x1, int y1, int x2)
		{
		}

		public override void pie(int xc, int yc, int x1, int y1, int x2, int y2)
		{
		}

		public override void line0(line_parameters lp)
		{
		}

		public override void line1(line_parameters lp, int sx, int sy)
		{
		}

		public override void line2(line_parameters lp, int ex, int ey)
		{
		}

		public void line3_no_clip(line_parameters lp, int sx, int sy, int ex, int ey)
		{
			if (lp.len > LineAABasics.line_max_length)
			{
				lp.divide(out line_parameters lp1, out line_parameters lp2);
				int mx = lp1.x2 + (lp1.y2 - lp1.y1);
				int my = lp1.y2 - (lp1.x2 - lp1.x1);
				line3_no_clip(lp1, (lp.x1 + sx) >> 1, (lp.y1 + sy) >> 1, mx, my);
				line3_no_clip(lp2, mx, my, (lp.x2 + ex) >> 1, (lp.y2 + ey) >> 1);
				return;
			}

			LineAABasics.fix_degenerate_bisectrix_start(lp, ref sx, ref sy);
			LineAABasics.fix_degenerate_bisectrix_end(lp, ref ex, ref ey);
			var li = new line_interpolator_image(this, lp, sx, sy, ex, ey, m_start, m_scale_x);
			if (li.vertical())
			{
				while (li.step_ver())
				{
				}
			}
			else
			{
				while (li.step_hor())
				{
				}
			}

			m_start += Util.uround(lp.len / m_scale_x);
		}

		public override void line3(line_parameters lp, int sx, int sy, int ex, int ey)
		{
			if (!m_clipping)
			{
				line3_no_clip(lp, sx, sy, ex, ey);
				return;
			}

			int x1 = lp.x1;
			int y1 = lp.y1;
			int x2 = lp.x2;
			int y2 = lp.y2;
			int flags = ClipLiangBarsky.clip_line_segment(ref x1, ref y1, ref x2, ref y2, m_clip_box);
			int start = m_start;
			if ((flags & 4) == 0)
			{
				if (flags != 0)
				{
					var lp2 = new line_parameters(x1, y1, x2, y2, Util.uround(agg_math.CalcDistance(x1, y1, x2, y2)));
					if ((flags & 1) != 0)
					{
						m_start += Util.uround(agg_math.CalcDistance(lp.x1, lp.y1, x1, y1) / m_scale_x);
						sx = x1 + (y2 - y1);
						sy = y1 - (x2 - x1);
					}
					else
					{
						while (Math.Abs(sx - lp.x1) + Math.Abs(sy - lp.y1) > lp2.len)
						{
							sx = (lp.x1 + sx) >> 1;
							sy = (lp.y1 + sy) >> 1;
						}
					}

					if ((flags & 2) != 0)
					{
						ex = x2 + (y2 - y1);
						ey = y2 - (x2 - x1);
					}
					else
					{
						while (Math.Abs(ex - lp.x2) + Math.Abs(ey - lp.y2) > lp2.len)
						{
							ex = (lp.x2 + ex) >> 1;
							ey = (lp.y2 + ey) >> 1;
						}
					}

					line3_no_clip(lp2, sx, sy, ex, ey);
				}
				else
				{
					line3_no_clip(lp, sx, sy, ex, ey);
				}
			}

			m_start = start + Util.uround(lp.len / m_scale_x);
		}
	}
}
