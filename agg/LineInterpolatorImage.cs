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

	//===================================================distance_interpolator4
	public class distance_interpolator4
	{
		private int m_dx;
		private int m_dy;
		private int m_dx_start;
		private int m_dy_start;
		private int m_dx_pict;
		private int m_dy_pict;
		private int m_dx_end;
		private int m_dy_end;

		private int m_dist;
		private int m_dist_start;
		private int m_dist_pict;
		private int m_dist_end;
		private int m_len;

		//---------------------------------------------------------------------
		public distance_interpolator4()
		{
		}

		public distance_interpolator4(int x1, int y1, int x2, int y2,
							   int sx, int sy, int ex, int ey,
							   int len, double scale, int x, int y)
		{
			m_dx = (x2 - x1);
			m_dy = (y2 - y1);
			m_dx_start = (LineAABasics.line_mr(sx) - LineAABasics.line_mr(x1));
			m_dy_start = (LineAABasics.line_mr(sy) - LineAABasics.line_mr(y1));
			m_dx_end = (LineAABasics.line_mr(ex) - LineAABasics.line_mr(x2));
			m_dy_end = (LineAABasics.line_mr(ey) - LineAABasics.line_mr(y2));

			m_dist = (Util.iround((double)(x + LineAABasics.line_subpixel_scale / 2 - x2) * (double)(m_dy) -
						  (double)(y + LineAABasics.line_subpixel_scale / 2 - y2) * (double)(m_dx)));

			m_dist_start = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sx)) * m_dy_start -
						 (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sy)) * m_dx_start);

			m_dist_end = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ex)) * m_dy_end -
					   (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ey)) * m_dx_end);
			m_len = (int)(Util.uround(len / scale));

			double d = len * scale;
			int dx = Util.iround(((x2 - x1) << LineAABasics.line_subpixel_shift) / d);
			int dy = Util.iround(((y2 - y1) << LineAABasics.line_subpixel_shift) / d);
			m_dx_pict = -dy;
			m_dy_pict = dx;
			m_dist_pict = ((x + LineAABasics.line_subpixel_scale / 2 - (x1 - dy)) * m_dy_pict -
							(y + LineAABasics.line_subpixel_scale / 2 - (y1 + dx)) * m_dx_pict) >>
						   LineAABasics.line_subpixel_shift;

			m_dx <<= LineAABasics.line_subpixel_shift;
			m_dy <<= LineAABasics.line_subpixel_shift;
			m_dx_start <<= LineAABasics.line_mr_subpixel_shift;
			m_dy_start <<= LineAABasics.line_mr_subpixel_shift;
			m_dx_end <<= LineAABasics.line_mr_subpixel_shift;
			m_dy_end <<= LineAABasics.line_mr_subpixel_shift;
		}

		//---------------------------------------------------------------------
		public void inc_x()
		{
			m_dist += m_dy;
			m_dist_start += m_dy_start;
			m_dist_pict += m_dy_pict;
			m_dist_end += m_dy_end;
		}

		//---------------------------------------------------------------------
		public void dec_x()
		{
			m_dist -= m_dy;
			m_dist_start -= m_dy_start;
			m_dist_pict -= m_dy_pict;
			m_dist_end -= m_dy_end;
		}

		//---------------------------------------------------------------------
		public void inc_y()
		{
			m_dist -= m_dx;
			m_dist_start -= m_dx_start;
			m_dist_pict -= m_dx_pict;
			m_dist_end -= m_dx_end;
		}

		//---------------------------------------------------------------------
		public void dec_y()
		{
			m_dist += m_dx;
			m_dist_start += m_dx_start;
			m_dist_pict += m_dx_pict;
			m_dist_end += m_dx_end;
		}

		//---------------------------------------------------------------------
		public void inc_x(int dy)
		{
			m_dist += m_dy;
			m_dist_start += m_dy_start;
			m_dist_pict += m_dy_pict;
			m_dist_end += m_dy_end;
			if (dy > 0)
			{
				m_dist -= m_dx;
				m_dist_start -= m_dx_start;
				m_dist_pict -= m_dx_pict;
				m_dist_end -= m_dx_end;
			}
			if (dy < 0)
			{
				m_dist += m_dx;
				m_dist_start += m_dx_start;
				m_dist_pict += m_dx_pict;
				m_dist_end += m_dx_end;
			}
		}

		//---------------------------------------------------------------------
		public void dec_x(int dy)
		{
			m_dist -= m_dy;
			m_dist_start -= m_dy_start;
			m_dist_pict -= m_dy_pict;
			m_dist_end -= m_dy_end;
			if (dy > 0)
			{
				m_dist -= m_dx;
				m_dist_start -= m_dx_start;
				m_dist_pict -= m_dx_pict;
				m_dist_end -= m_dx_end;
			}
			if (dy < 0)
			{
				m_dist += m_dx;
				m_dist_start += m_dx_start;
				m_dist_pict += m_dx_pict;
				m_dist_end += m_dx_end;
			}
		}

		//---------------------------------------------------------------------
		public void inc_y(int dx)
		{
			m_dist -= m_dx;
			m_dist_start -= m_dx_start;
			m_dist_pict -= m_dx_pict;
			m_dist_end -= m_dx_end;
			if (dx > 0)
			{
				m_dist += m_dy;
				m_dist_start += m_dy_start;
				m_dist_pict += m_dy_pict;
				m_dist_end += m_dy_end;
			}
			if (dx < 0)
			{
				m_dist -= m_dy;
				m_dist_start -= m_dy_start;
				m_dist_pict -= m_dy_pict;
				m_dist_end -= m_dy_end;
			}
		}

		//---------------------------------------------------------------------
		public void dec_y(int dx)
		{
			m_dist += m_dx;
			m_dist_start += m_dx_start;
			m_dist_pict += m_dx_pict;
			m_dist_end += m_dx_end;
			if (dx > 0)
			{
				m_dist += m_dy;
				m_dist_start += m_dy_start;
				m_dist_pict += m_dy_pict;
				m_dist_end += m_dy_end;
			}
			if (dx < 0)
			{
				m_dist -= m_dy;
				m_dist_start -= m_dy_start;
				m_dist_pict -= m_dy_pict;
				m_dist_end -= m_dy_end;
			}
		}

		//---------------------------------------------------------------------
		public int dist()
		{
			return m_dist;
		}

		public int dist_start()
		{
			return m_dist_start;
		}

		public int dist_pict()
		{
			return m_dist_pict;
		}

		public int dist_end()
		{
			return m_dist_end;
		}

		//---------------------------------------------------------------------
		public int dx()
		{
			return m_dx;
		}

		public int dy()
		{
			return m_dy;
		}

		public int dx_start()
		{
			return m_dx_start;
		}

		public int dy_start()
		{
			return m_dy_start;
		}

		public int dx_pict()
		{
			return m_dx_pict;
		}

		public int dy_pict()
		{
			return m_dy_pict;
		}

		public int dx_end()
		{
			return m_dx_end;
		}

		public int dy_end()
		{
			return m_dy_end;
		}

		public int len()
		{
			return m_len;
		}
	};

	//==================================================line_interpolator_image
	/// <summary>
	/// C++ <c>line_interpolator_image</c>: steps one line segment a pixel at a time along its major axis, filling the
	/// span across it with pattern colors - <c>dist_pict</c> is the distance along the pattern, the distance from the
	/// center line its row - and clipping the span to the bisectrices at the segment's ends.
	/// </summary>
	public class line_interpolator_image
	{
		private const int max_half_width = 64;

		private readonly line_parameters m_lp;
		private readonly dda2_line_interpolator m_li;
		private readonly distance_interpolator4 m_di;
		private readonly ImageLineRenderer m_ren;
		private int m_x;
		private int m_y;
		private int m_old_x;
		private int m_old_y;
		private readonly int m_count;
		private readonly int m_width;
		private readonly int m_max_extent;
		private readonly int m_start;
		private int m_step;
		private readonly int[] m_dist_pos = new int[max_half_width + 1];
		private readonly Color[] m_colors = new Color[(max_half_width * 2) + 4];

		public line_interpolator_image(ImageLineRenderer ren, line_parameters lp, int sx, int sy, int ex, int ey, int pattern_start, double scale_x)
		{
			m_lp = lp;
			m_li = new dda2_line_interpolator(
				lp.vertical ? LineAABasics.line_dbl_hr(lp.x2 - lp.x1) : LineAABasics.line_dbl_hr(lp.y2 - lp.y1),
				lp.vertical ? Math.Abs(lp.y2 - lp.y1) : Math.Abs(lp.x2 - lp.x1) + 1);
			m_di = new distance_interpolator4(lp.x1, lp.y1, lp.x2, lp.y2, sx, sy, ex, ey, lp.len, scale_x,
				lp.x1 & ~LineAABasics.line_subpixel_mask, lp.y1 & ~LineAABasics.line_subpixel_mask);
			m_ren = ren;
			m_x = lp.x1 >> LineAABasics.line_subpixel_shift;
			m_y = lp.y1 >> LineAABasics.line_subpixel_shift;
			m_old_x = m_x;
			m_old_y = m_y;
			m_count = lp.vertical ? Math.Abs((lp.y2 >> LineAABasics.line_subpixel_shift) - m_y) : Math.Abs((lp.x2 >> LineAABasics.line_subpixel_shift) - m_x);
			m_width = ren.subpixel_width();
			m_max_extent = (m_width + LineAABasics.line_subpixel_scale) >> LineAABasics.line_subpixel_shift;
			m_start = pattern_start + ((m_max_extent + 2) * ren.pattern_width());
			m_step = 0;

			var li = new dda2_line_interpolator(0, lp.vertical ? (lp.dy << LineAABasics.line_subpixel_shift) : (lp.dx << LineAABasics.line_subpixel_shift), lp.len);

			int i;
			int stop = m_width + (LineAABasics.line_subpixel_scale * 2);
			for (i = 0; i < max_half_width; ++i)
			{
				m_dist_pos[i] = li.y();
				if (m_dist_pos[i] >= stop)
				{
					break;
				}

				li.Next();
			}

			m_dist_pos[i] = 0x7FFF0000;

			// Step back from the start until a column (or row) has no pixel inside the start bisectrix, so a
			// segment that begins mid-join still paints the pixels its predecessor left.
			int dist1_start;
			int dist2_start;
			int npix = 1;
			if (lp.vertical)
			{
				do
				{
					m_li.Prev();
					m_y -= lp.inc;
					m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

					if (lp.inc > 0)
					{
						m_di.dec_y(m_x - m_old_x);
					}
					else
					{
						m_di.inc_y(m_x - m_old_x);
					}

					m_old_x = m_x;

					dist1_start = dist2_start = m_di.dist_start();

					int dx = 0;
					if (dist1_start < 0)
					{
						++npix;
					}

					do
					{
						dist1_start += m_di.dy_start();
						dist2_start -= m_di.dy_start();
						if (dist1_start < 0)
						{
							++npix;
						}

						if (dist2_start < 0)
						{
							++npix;
						}

						++dx;
					}
					while (m_dist_pos[dx] <= m_width);

					if (npix == 0)
					{
						break;
					}

					npix = 0;
				}
				while (--m_step >= -m_max_extent);
			}
			else
			{
				do
				{
					m_li.Prev();
					m_x -= lp.inc;
					m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

					if (lp.inc > 0)
					{
						m_di.dec_x(m_y - m_old_y);
					}
					else
					{
						m_di.inc_x(m_y - m_old_y);
					}

					m_old_y = m_y;

					dist1_start = dist2_start = m_di.dist_start();

					int dy = 0;
					if (dist1_start < 0)
					{
						++npix;
					}

					do
					{
						dist1_start -= m_di.dx_start();
						dist2_start += m_di.dx_start();
						if (dist1_start < 0)
						{
							++npix;
						}

						if (dist2_start < 0)
						{
							++npix;
						}

						++dy;
					}
					while (m_dist_pos[dy] <= m_width);

					if (npix == 0)
					{
						break;
					}

					npix = 0;
				}
				while (--m_step >= -m_max_extent);
			}

			m_li.adjust_forward();
			m_step -= m_max_extent;
		}

		public bool step_hor()
		{
			m_li.Next();
			m_x += m_lp.inc;
			m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0)
			{
				m_di.inc_x(m_y - m_old_y);
			}
			else
			{
				m_di.dec_x(m_y - m_old_y);
			}

			m_old_y = m_y;

			int s1 = m_di.dist() / m_lp.len;
			int s2 = -s1;

			if (m_lp.inc < 0)
			{
				s1 = -s1;
			}

			int dist_start = m_di.dist_start();
			int dist_pict = m_di.dist_pict() + m_start;
			int dist_end = m_di.dist_end();
			int p0 = max_half_width + 2;
			int p1 = p0;

			int npix = 0;
			m_colors[p1] = default;
			if (dist_end > 0)
			{
				if (dist_start <= 0)
				{
					m_ren.pixel(m_colors, p1, dist_pict, s2);
				}

				++npix;
			}

			++p1;

			int dy = 1;
			int dist;
			while ((dist = m_dist_pos[dy]) - s1 <= m_width)
			{
				dist_start -= m_di.dx_start();
				dist_pict -= m_di.dx_pict();
				dist_end -= m_di.dx_end();
				m_colors[p1] = default;
				if (dist_end > 0 && dist_start <= 0)
				{
					if (m_lp.inc > 0)
					{
						dist = -dist;
					}

					m_ren.pixel(m_colors, p1, dist_pict, s2 - dist);
					++npix;
				}

				++p1;
				++dy;
			}

			dy = 1;
			dist_start = m_di.dist_start();
			dist_pict = m_di.dist_pict() + m_start;
			dist_end = m_di.dist_end();
			while ((dist = m_dist_pos[dy]) + s1 <= m_width)
			{
				dist_start += m_di.dx_start();
				dist_pict += m_di.dx_pict();
				dist_end += m_di.dx_end();
				--p0;
				m_colors[p0] = default;
				if (dist_end > 0 && dist_start <= 0)
				{
					if (m_lp.inc > 0)
					{
						dist = -dist;
					}

					m_ren.pixel(m_colors, p0, dist_pict, s2 + dist);
					++npix;
				}

				++dy;
			}

			m_ren.blend_color_vspan(m_x, m_y - dy + 1, p1 - p0, m_colors, p0);
			return npix != 0 && ++m_step < m_count;
		}

		public bool step_ver()
		{
			m_li.Next();
			m_y += m_lp.inc;
			m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0)
			{
				m_di.inc_y(m_x - m_old_x);
			}
			else
			{
				m_di.dec_y(m_x - m_old_x);
			}

			m_old_x = m_x;

			int s1 = m_di.dist() / m_lp.len;
			int s2 = -s1;

			if (m_lp.inc > 0)
			{
				s1 = -s1;
			}

			int dist_start = m_di.dist_start();
			int dist_pict = m_di.dist_pict() + m_start;
			int dist_end = m_di.dist_end();
			int p0 = max_half_width + 2;
			int p1 = p0;

			int npix = 0;
			m_colors[p1] = default;
			if (dist_end > 0)
			{
				if (dist_start <= 0)
				{
					m_ren.pixel(m_colors, p1, dist_pict, s2);
				}

				++npix;
			}

			++p1;

			int dx = 1;
			int dist;
			while ((dist = m_dist_pos[dx]) - s1 <= m_width)
			{
				dist_start += m_di.dy_start();
				dist_pict += m_di.dy_pict();
				dist_end += m_di.dy_end();
				m_colors[p1] = default;
				if (dist_end > 0 && dist_start <= 0)
				{
					if (m_lp.inc > 0)
					{
						dist = -dist;
					}

					m_ren.pixel(m_colors, p1, dist_pict, s2 + dist);
					++npix;
				}

				++p1;
				++dx;
			}

			dx = 1;
			dist_start = m_di.dist_start();
			dist_pict = m_di.dist_pict() + m_start;
			dist_end = m_di.dist_end();
			while ((dist = m_dist_pos[dx]) + s1 <= m_width)
			{
				dist_start -= m_di.dy_start();
				dist_pict -= m_di.dy_pict();
				dist_end -= m_di.dy_end();
				--p0;
				m_colors[p0] = default;
				if (dist_end > 0 && dist_start <= 0)
				{
					if (m_lp.inc > 0)
					{
						dist = -dist;
					}

					m_ren.pixel(m_colors, p0, dist_pict, s2 - dist);
					++npix;
				}

				++dx;
			}

			m_ren.blend_color_hspan(m_x - dx + 1, m_y, p1 - p0, m_colors, p0);
			return npix != 0 && ++m_step < m_count;
		}

		public int pattern_end()
		{
			return m_start + m_di.len();
		}

		public bool vertical()
		{
			return m_lp.vertical;
		}

		public int width()
		{
			return m_width;
		}

		public int count()
		{
			return m_count;
		}
	}

}
