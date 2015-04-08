using MatterHackers.Agg.Image;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
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

namespace MatterHackers.Agg
{
#if true

	//===================================================distance_interpolator0
	public class distance_interpolator0
	{
		private int m_dx;
		private int m_dy;
		private int m_dist;

		//---------------------------------------------------------------------
		public distance_interpolator0()
		{
		}

		public distance_interpolator0(int x1, int y1, int x2, int y2, int x, int y)
		{
			unchecked
			{
				m_dx = (LineAABasics.line_mr(x2) - LineAABasics.line_mr(x1));
				m_dy = (LineAABasics.line_mr(y2) - LineAABasics.line_mr(y1));
				m_dist = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(x2)) * m_dy -
					   (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(y2)) * m_dx);

				m_dx <<= LineAABasics.line_mr_subpixel_shift;
				m_dy <<= LineAABasics.line_mr_subpixel_shift;
			}
		}

		//---------------------------------------------------------------------
		public void inc_x()
		{
			m_dist += m_dy;
		}

		public int dist()
		{
			return m_dist;
		}
	};

	//==================================================distance_interpolator00
	public class distance_interpolator00
	{
		private int m_dx1;
		private int m_dy1;
		private int m_dx2;
		private int m_dy2;
		private int m_dist1;
		private int m_dist2;

		//---------------------------------------------------------------------
		public distance_interpolator00()
		{
		}

		public distance_interpolator00(int xc, int yc,
								int x1, int y1, int x2, int y2,
								int x, int y)
		{
			m_dx1 = (LineAABasics.line_mr(x1) - LineAABasics.line_mr(xc));
			m_dy1 = (LineAABasics.line_mr(y1) - LineAABasics.line_mr(yc));
			m_dx2 = (LineAABasics.line_mr(x2) - LineAABasics.line_mr(xc));
			m_dy2 = (LineAABasics.line_mr(y2) - LineAABasics.line_mr(yc));
			m_dist1 = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(x1)) * m_dy1 -
					(LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(y1)) * m_dx1);
			m_dist2 = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(x2)) * m_dy2 -
					(LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(y2)) * m_dx2);

			m_dx1 <<= LineAABasics.line_mr_subpixel_shift;
			m_dy1 <<= LineAABasics.line_mr_subpixel_shift;
			m_dx2 <<= LineAABasics.line_mr_subpixel_shift;
			m_dy2 <<= LineAABasics.line_mr_subpixel_shift;
		}

		//---------------------------------------------------------------------
		public void inc_x()
		{
			m_dist1 += m_dy1; m_dist2 += m_dy2;
		}

		public int dist1()
		{
			return m_dist1;
		}

		public int dist2()
		{
			return m_dist2;
		}
	};

	//===================================================distance_interpolator1
	public class distance_interpolator1
	{
		private int m_dx;
		private int m_dy;
		private int m_dist;

		//---------------------------------------------------------------------
		public distance_interpolator1()
		{
		}

		public distance_interpolator1(int x1, int y1, int x2, int y2, int x, int y)
		{
			m_dx = (x2 - x1);
			m_dy = (y2 - y1);
			m_dist = (agg_basics.iround((double)(x + LineAABasics.line_subpixel_scale / 2 - x2) * (double)(m_dy) -
						  (double)(y + LineAABasics.line_subpixel_scale / 2 - y2) * (double)(m_dx)));

			m_dx <<= LineAABasics.line_subpixel_shift;
			m_dy <<= LineAABasics.line_subpixel_shift;
		}

		//---------------------------------------------------------------------
		public void inc_x()
		{
			m_dist += m_dy;
		}

		public void dec_x()
		{
			m_dist -= m_dy;
		}

		public void inc_y()
		{
			m_dist -= m_dx;
		}

		public void dec_y()
		{
			m_dist += m_dx;
		}

		//---------------------------------------------------------------------
		public void inc_x(int dy)
		{
			m_dist += m_dy;
			if (dy > 0) m_dist -= m_dx;
			if (dy < 0) m_dist += m_dx;
		}

		//---------------------------------------------------------------------
		public void dec_x(int dy)
		{
			m_dist -= m_dy;
			if (dy > 0) m_dist -= m_dx;
			if (dy < 0) m_dist += m_dx;
		}

		//---------------------------------------------------------------------
		public void inc_y(int dx)
		{
			m_dist -= m_dx;
			if (dx > 0) m_dist += m_dy;
			if (dx < 0) m_dist -= m_dy;
		}

		public void dec_y(int dx)
		//---------------------------------------------------------------------
		{
			m_dist += m_dx;
			if (dx > 0) m_dist += m_dy;
			if (dx < 0) m_dist -= m_dy;
		}

		//---------------------------------------------------------------------
		public int dist()
		{
			return m_dist;
		}

		public int dx()
		{
			return m_dx;
		}

		public int dy()
		{
			return m_dy;
		}
	};

	//===================================================distance_interpolator2
	public class distance_interpolator2
	{
		private int m_dx;
		private int m_dy;
		private int m_dx_start;
		private int m_dy_start;

		private int m_dist;
		private int m_dist_start;

		//---------------------------------------------------------------------
		public distance_interpolator2()
		{
		}

		public distance_interpolator2(int x1, int y1, int x2, int y2,
							   int sx, int sy, int x, int y)
		{
			m_dx = (x2 - x1);
			m_dy = (y2 - y1);
			m_dx_start = (LineAABasics.line_mr(sx) - LineAABasics.line_mr(x1));
			m_dy_start = (LineAABasics.line_mr(sy) - LineAABasics.line_mr(y1));

			m_dist = (agg_basics.iround((double)(x + LineAABasics.line_subpixel_scale / 2 - x2) * (double)(m_dy) -
						  (double)(y + LineAABasics.line_subpixel_scale / 2 - y2) * (double)(m_dx)));

			m_dist_start = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sx)) * m_dy_start -
						 (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sy)) * m_dx_start);

			m_dx <<= LineAABasics.line_subpixel_shift;
			m_dy <<= LineAABasics.line_subpixel_shift;
			m_dx_start <<= LineAABasics.line_mr_subpixel_shift;
			m_dy_start <<= LineAABasics.line_mr_subpixel_shift;
		}

		public distance_interpolator2(int x1, int y1, int x2, int y2,
							   int ex, int ey, int x, int y, int none)
		{
			m_dx = (x2 - x1);
			m_dy = (y2 - y1);
			m_dx_start = (LineAABasics.line_mr(ex) - LineAABasics.line_mr(x2));
			m_dy_start = (LineAABasics.line_mr(ey) - LineAABasics.line_mr(y2));

			m_dist = (agg_basics.iround((double)(x + LineAABasics.line_subpixel_scale / 2 - x2) * (double)(m_dy) -
						  (double)(y + LineAABasics.line_subpixel_scale / 2 - y2) * (double)(m_dx)));

			m_dist_start = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ex)) * m_dy_start -
						 (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ey)) * m_dx_start);

			m_dx <<= LineAABasics.line_subpixel_shift;
			m_dy <<= LineAABasics.line_subpixel_shift;
			m_dx_start <<= LineAABasics.line_mr_subpixel_shift;
			m_dy_start <<= LineAABasics.line_mr_subpixel_shift;
		}

		//---------------------------------------------------------------------
		public void inc_x()
		{
			m_dist += m_dy; m_dist_start += m_dy_start;
		}

		public void dec_x()
		{
			m_dist -= m_dy; m_dist_start -= m_dy_start;
		}

		public void inc_y()
		{
			m_dist -= m_dx; m_dist_start -= m_dx_start;
		}

		public void dec_y()
		{
			m_dist += m_dx; m_dist_start += m_dx_start;
		}

		//---------------------------------------------------------------------
		public void inc_x(int dy)
		{
			m_dist += m_dy;
			m_dist_start += m_dy_start;
			if (dy > 0)
			{
				m_dist -= m_dx;
				m_dist_start -= m_dx_start;
			}
			if (dy < 0)
			{
				m_dist += m_dx;
				m_dist_start += m_dx_start;
			}
		}

		//---------------------------------------------------------------------
		public void dec_x(int dy)
		{
			m_dist -= m_dy;
			m_dist_start -= m_dy_start;
			if (dy > 0)
			{
				m_dist -= m_dx;
				m_dist_start -= m_dx_start;
			}
			if (dy < 0)
			{
				m_dist += m_dx;
				m_dist_start += m_dx_start;
			}
		}

		//---------------------------------------------------------------------
		public void inc_y(int dx)
		{
			m_dist -= m_dx;
			m_dist_start -= m_dx_start;
			if (dx > 0)
			{
				m_dist += m_dy;
				m_dist_start += m_dy_start;
			}
			if (dx < 0)
			{
				m_dist -= m_dy;
				m_dist_start -= m_dy_start;
			}
		}

		//---------------------------------------------------------------------
		public void dec_y(int dx)
		{
			m_dist += m_dx;
			m_dist_start += m_dx_start;
			if (dx > 0)
			{
				m_dist += m_dy;
				m_dist_start += m_dy_start;
			}
			if (dx < 0)
			{
				m_dist -= m_dy;
				m_dist_start -= m_dy_start;
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

		public int dist_end()
		{
			return m_dist_start;
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

		public int dx_end()
		{
			return m_dx_start;
		}

		public int dy_end()
		{
			return m_dy_start;
		}
	};

	//===================================================distance_interpolator3
	public class distance_interpolator3
	{
		private int m_dx;
		private int m_dy;
		private int m_dx_start;
		private int m_dy_start;
		private int m_dx_end;
		private int m_dy_end;

		private int m_dist;
		private int m_dist_start;
		private int m_dist_end;

		//---------------------------------------------------------------------
		public distance_interpolator3()
		{
		}

		public distance_interpolator3(int x1, int y1, int x2, int y2,
							   int sx, int sy, int ex, int ey,
							   int x, int y)
		{
			unchecked
			{
				m_dx = (x2 - x1);
				m_dy = (y2 - y1);
				m_dx_start = (LineAABasics.line_mr(sx) - LineAABasics.line_mr(x1));
				m_dy_start = (LineAABasics.line_mr(sy) - LineAABasics.line_mr(y1));
				m_dx_end = (LineAABasics.line_mr(ex) - LineAABasics.line_mr(x2));
				m_dy_end = (LineAABasics.line_mr(ey) - LineAABasics.line_mr(y2));

				m_dist = (agg_basics.iround((double)(x + LineAABasics.line_subpixel_scale / 2 - x2) * (double)(m_dy) -
							  (double)(y + LineAABasics.line_subpixel_scale / 2 - y2) * (double)(m_dx)));

				m_dist_start = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sx)) * m_dy_start -
							 (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sy)) * m_dx_start);

				m_dist_end = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ex)) * m_dy_end -
						   (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ey)) * m_dx_end);

				m_dx <<= LineAABasics.line_subpixel_shift;
				m_dy <<= LineAABasics.line_subpixel_shift;
				m_dx_start <<= LineAABasics.line_mr_subpixel_shift;
				m_dy_start <<= LineAABasics.line_mr_subpixel_shift;
				m_dx_end <<= LineAABasics.line_mr_subpixel_shift;
				m_dy_end <<= LineAABasics.line_mr_subpixel_shift;
			}
		}

		private void inc_x()
		{
			m_dist += m_dy; m_dist_start += m_dy_start; m_dist_end += m_dy_end;
		}

		private void dec_x()
		{
			m_dist -= m_dy; m_dist_start -= m_dy_start; m_dist_end -= m_dy_end;
		}

		private void inc_y()
		{
			m_dist -= m_dx; m_dist_start -= m_dx_start; m_dist_end -= m_dx_end;
		}

		private void dec_y()
		{
			m_dist += m_dx; m_dist_start += m_dx_start; m_dist_end += m_dx_end;
		}

		public void inc_x(int dy)
		{
			m_dist += m_dy;
			m_dist_start += m_dy_start;
			m_dist_end += m_dy_end;
			if (dy > 0)
			{
				m_dist -= m_dx;
				m_dist_start -= m_dx_start;
				m_dist_end -= m_dx_end;
			}
			if (dy < 0)
			{
				m_dist += m_dx;
				m_dist_start += m_dx_start;
				m_dist_end += m_dx_end;
			}
		}

		public void dec_x(int dy)
		{
			m_dist -= m_dy;
			m_dist_start -= m_dy_start;
			m_dist_end -= m_dy_end;
			if (dy > 0)
			{
				m_dist -= m_dx;
				m_dist_start -= m_dx_start;
				m_dist_end -= m_dx_end;
			}
			if (dy < 0)
			{
				m_dist += m_dx;
				m_dist_start += m_dx_start;
				m_dist_end += m_dx_end;
			}
		}

		public void inc_y(int dx)
		{
			m_dist -= m_dx;
			m_dist_start -= m_dx_start;
			m_dist_end -= m_dx_end;
			if (dx > 0)
			{
				m_dist += m_dy;
				m_dist_start += m_dy_start;
				m_dist_end += m_dy_end;
			}
			if (dx < 0)
			{
				m_dist -= m_dy;
				m_dist_start -= m_dy_start;
				m_dist_end -= m_dy_end;
			}
		}

		public void dec_y(int dx)
		{
			m_dist += m_dx;
			m_dist_start += m_dx_start;
			m_dist_end += m_dx_end;
			if (dx > 0)
			{
				m_dist += m_dy;
				m_dist_start += m_dy_start;
				m_dist_end += m_dy_end;
			}
			if (dx < 0)
			{
				m_dist -= m_dy;
				m_dist_start -= m_dy_start;
				m_dist_end -= m_dy_end;
			}
		}

		public int dist()
		{
			return m_dist;
		}

		public int dist_start()
		{
			return m_dist_start;
		}

		public int dist_end()
		{
			return m_dist_end;
		}

		private int dx()
		{
			return m_dx;
		}

		private int dy()
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

		public int dx_end()
		{
			return m_dx_end;
		}

		public int dy_end()
		{
			return m_dy_end;
		}
	};

	//================================================line_interpolator_aa_base
	public class line_interpolator_aa_base
	{
		protected line_parameters m_lp;
		protected dda2_line_interpolator m_li;
		protected OutlineRenderer m_ren;
		private int m_len;
		protected int m_x;
		protected int m_y;
		protected int m_old_x;
		protected int m_old_y;
		protected int m_count;
		protected int m_width;
		protected int m_max_extent;
		protected int m_step;
		protected int[] m_dist = new int[max_half_width + 1];
		protected byte[] m_covers = new byte[max_half_width * 2 + 4];
		//typedef Renderer renderer_type;

		protected const int max_half_width = 64;

		public line_interpolator_aa_base(OutlineRenderer ren, line_parameters lp)
		{
			m_lp = lp;
			m_li = new dda2_line_interpolator(lp.vertical ? LineAABasics.line_dbl_hr(lp.x2 - lp.x1) : LineAABasics.line_dbl_hr(lp.y2 - lp.y1),
				lp.vertical ? Math.Abs(lp.y2 - lp.y1) : Math.Abs(lp.x2 - lp.x1) + 1);
			m_ren = ren;
			m_len = ((lp.vertical == (lp.inc > 0)) ? -lp.len : lp.len);
			m_x = (lp.x1 >> LineAABasics.line_subpixel_shift);
			m_y = (lp.y1 >> LineAABasics.line_subpixel_shift);
			m_old_x = (m_x);
			m_old_y = (m_y);
			m_count = ((lp.vertical ? Math.Abs((lp.y2 >> LineAABasics.line_subpixel_shift) - m_y) :
								   Math.Abs((lp.x2 >> LineAABasics.line_subpixel_shift) - m_x)));
			m_width = (ren.subpixel_width());
			//m_max_extent(m_width >> (line_subpixel_shift - 2));
			m_max_extent = ((m_width + LineAABasics.line_subpixel_mask) >> LineAABasics.line_subpixel_shift);
			m_step = 0;

			dda2_line_interpolator li = new dda2_line_interpolator(0,
				lp.vertical ? (lp.dy << LineAABasics.line_subpixel_shift) : (lp.dx << LineAABasics.line_subpixel_shift),
				lp.len);

			int i;
			int stop = m_width + LineAABasics.line_subpixel_scale * 2;
			for (i = 0; i < max_half_width; ++i)
			{
				m_dist[i] = li.y();
				if (m_dist[i] >= stop) break;
				li.Next();
			}
			m_dist[i++] = 0x7FFF0000;
		}

		public int step_hor_base(distance_interpolator1 di)
		{
			m_li.Next();
			m_x += m_lp.inc;
			m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0) di.inc_x(m_y - m_old_y);
			else di.dec_x(m_y - m_old_y);

			m_old_y = m_y;

			return di.dist() / m_len;
		}

		public int step_hor_base(distance_interpolator2 di)
		{
			m_li.Next();
			m_x += m_lp.inc;
			m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0) di.inc_x(m_y - m_old_y);
			else di.dec_x(m_y - m_old_y);

			m_old_y = m_y;

			return di.dist() / m_len;
		}

		public int step_hor_base(distance_interpolator3 di)
		{
			m_li.Next();
			m_x += m_lp.inc;
			m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0) di.inc_x(m_y - m_old_y);
			else di.dec_x(m_y - m_old_y);

			m_old_y = m_y;

			return di.dist() / m_len;
		}

		public int step_ver_base(distance_interpolator1 di)
		{
			m_li.Next();
			m_y += m_lp.inc;
			m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0) di.inc_y(m_x - m_old_x);
			else di.dec_y(m_x - m_old_x);

			m_old_x = m_x;

			return di.dist() / m_len;
		}

		public int step_ver_base(distance_interpolator2 di)
		{
			m_li.Next();
			m_y += m_lp.inc;
			m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0) di.inc_y(m_x - m_old_x);
			else di.dec_y(m_x - m_old_x);

			m_old_x = m_x;

			return di.dist() / m_len;
		}

		public int step_ver_base(distance_interpolator3 di)
		{
			m_li.Next();
			m_y += m_lp.inc;
			m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

			if (m_lp.inc > 0) di.inc_y(m_x - m_old_x);
			else di.dec_y(m_x - m_old_x);

			m_old_x = m_x;

			return di.dist() / m_len;
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
	};

	//====================================================line_interpolator_aa0
	public class line_interpolator_aa0 : line_interpolator_aa_base
	{
		private distance_interpolator1 m_di;
		//typedef Renderer renderer_type;
		//typedef line_interpolator_aa_base<Renderer> base_type;

		//---------------------------------------------------------------------
		public line_interpolator_aa0(OutlineRenderer ren, line_parameters lp)
			: base(ren, lp)
		{
			m_di = new distance_interpolator1(lp.x1, lp.y1, lp.x2, lp.y2,
				 lp.x1 & ~LineAABasics.line_subpixel_mask, lp.y1 & ~LineAABasics.line_subpixel_mask);

			m_li.adjust_forward();
		}

		//---------------------------------------------------------------------
		public bool step_hor()
		{
			int dist;
			int dy;
			int s1 = step_hor_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			m_covers[Offset1++] = (byte)m_ren.cover(s1);

			dy = 1;
			while ((dist = base.m_dist[dy] - s1) <= base.m_width)
			{
				m_covers[Offset1++] = (byte)base.m_ren.cover(dist);
				++dy;
			}

			dy = 1;
			while ((dist = base.m_dist[dy] + s1) <= base.m_width)
			{
				m_covers[--Offset0] = (byte)base.m_ren.cover(dist);
				++dy;
			}
			base.m_ren.blend_solid_vspan(base.m_x,
											   base.m_y - dy + 1,
											   Offset1 - Offset0,
											   m_covers, Offset0);
			return ++base.m_step < base.m_count;
		}

		//---------------------------------------------------------------------
		public bool step_ver()
		{
			int dist;
			int dx;
			int s1 = base.step_ver_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			m_covers[Offset1++] = (byte)m_ren.cover(s1);

			dx = 1;
			while ((dist = base.m_dist[dx] - s1) <= base.m_width)
			{
				m_covers[Offset1++] = (byte)base.m_ren.cover(dist);
				++dx;
			}

			dx = 1;
			while ((dist = base.m_dist[dx] + s1) <= base.m_width)
			{
				m_covers[--Offset0] = (byte)base.m_ren.cover(dist);
				++dx;
			}
			base.m_ren.blend_solid_hspan(base.m_x - dx + 1,
											   base.m_y,
											   Offset1 - Offset0,
											   m_covers, Offset0);
			return ++base.m_step < base.m_count;
		}
	};

	//====================================================line_interpolator_aa1
	public class line_interpolator_aa1 : line_interpolator_aa_base
	{
		private distance_interpolator2 m_di;
		//typedef Renderer renderer_type;
		//typedef line_interpolator_aa_base<Renderer> base_type;

		//---------------------------------------------------------------------
		public line_interpolator_aa1(OutlineRenderer ren, line_parameters lp,
							  int sx, int sy)
			:
			base(ren, lp)
		{
			m_di = new distance_interpolator2(lp.x1, lp.y1, lp.x2, lp.y2, sx, sy,
				 lp.x1 & ~LineAABasics.line_subpixel_mask, lp.y1 & ~LineAABasics.line_subpixel_mask);

			int dist1_start;
			int dist2_start;

			int npix = 1;

			if (lp.vertical)
			{
				do
				{
					base.m_li.Prev();
					base.m_y -= lp.inc;
					base.m_x = (base.m_lp.x1 + base.m_li.y()) >> LineAABasics.line_subpixel_shift;

					if (lp.inc > 0) m_di.dec_y(base.m_x - base.m_old_x);
					else m_di.inc_y(base.m_x - base.m_old_x);

					base.m_old_x = base.m_x;

					dist1_start = dist2_start = m_di.dist_start();

					int dx = 0;
					if (dist1_start < 0) ++npix;
					do
					{
						dist1_start += m_di.dy_start();
						dist2_start -= m_di.dy_start();
						if (dist1_start < 0) ++npix;
						if (dist2_start < 0) ++npix;
						++dx;
					}
					while (base.m_dist[dx] <= base.m_width);
					--base.m_step;
					if (npix == 0) break;
					npix = 0;
				}
				while (base.m_step >= -base.m_max_extent);
			}
			else
			{
				do
				{
					base.m_li.Prev();
					base.m_x -= lp.inc;
					base.m_y = (base.m_lp.y1 + base.m_li.y()) >> LineAABasics.line_subpixel_shift;

					if (lp.inc > 0) m_di.dec_x(base.m_y - base.m_old_y);
					else m_di.inc_x(base.m_y - base.m_old_y);

					base.m_old_y = base.m_y;

					dist1_start = dist2_start = m_di.dist_start();

					int dy = 0;
					if (dist1_start < 0) ++npix;
					do
					{
						dist1_start -= m_di.dx_start();
						dist2_start += m_di.dx_start();
						if (dist1_start < 0) ++npix;
						if (dist2_start < 0) ++npix;
						++dy;
					}
					while (base.m_dist[dy] <= base.m_width);
					--base.m_step;
					if (npix == 0) break;
					npix = 0;
				}
				while (base.m_step >= -base.m_max_extent);
			}
			base.m_li.adjust_forward();
		}

		//---------------------------------------------------------------------
		public bool step_hor()
		{
			int dist_start;
			int dist;
			int dy;
			int s1 = base.step_hor_base(m_di);

			dist_start = m_di.dist_start();
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			m_covers[Offset1] = 0;
			if (dist_start <= 0)
			{
				m_covers[Offset1] = (byte)base.m_ren.cover(s1);
			}
			++Offset1;

			dy = 1;
			while ((dist = base.m_dist[dy] - s1) <= base.m_width)
			{
				dist_start -= m_di.dx_start();
				m_covers[Offset1] = 0;
				if (dist_start <= 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(dist);
				}
				++Offset1;
				++dy;
			}

			dy = 1;
			dist_start = m_di.dist_start();
			while ((dist = base.m_dist[dy] + s1) <= base.m_width)
			{
				dist_start += m_di.dx_start();
				m_covers[--Offset0] = 0;
				if (dist_start <= 0)
				{
					m_covers[Offset0] = (byte)base.m_ren.cover(dist);
				}
				++dy;
			}

			int len = Offset1 - Offset0;
			base.m_ren.blend_solid_vspan(base.m_x,
											   base.m_y - dy + 1,
											   len, m_covers,
											   Offset0);
			return ++base.m_step < base.m_count;
		}

		//---------------------------------------------------------------------
		public bool step_ver()
		{
			int dist_start;
			int dist;
			int dx;
			int s1 = base.step_ver_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			dist_start = m_di.dist_start();

			m_covers[Offset1] = 0;
			if (dist_start <= 0)
			{
				m_covers[Offset1] = (byte)base.m_ren.cover(s1);
			}
			++Offset1;

			dx = 1;
			while ((dist = base.m_dist[dx] - s1) <= base.m_width)
			{
				dist_start += m_di.dy_start();
				m_covers[Offset1] = 0;
				if (dist_start <= 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(dist);
				}
				++Offset1;
				++dx;
			}

			dx = 1;
			dist_start = m_di.dist_start();
			while ((dist = base.m_dist[dx] + s1) <= base.m_width)
			{
				dist_start -= m_di.dy_start();
				m_covers[--Offset0] = 0;
				if (dist_start <= 0)
				{
					m_covers[Offset0] = (byte)base.m_ren.cover(dist);
				}
				++dx;
			}
			base.m_ren.blend_solid_hspan(base.m_x - dx + 1,
											   base.m_y,
											   Offset1 - Offset0, m_covers,
											   Offset0);

			return ++base.m_step < base.m_count;
		}
	};

	//====================================================line_interpolator_aa2
	public class line_interpolator_aa2 : line_interpolator_aa_base
	{
		private distance_interpolator2 m_di;
		//typedef Renderer renderer_type;
		//typedef line_interpolator_aa_base<Renderer> base_type;

		//---------------------------------------------------------------------
		public line_interpolator_aa2(OutlineRenderer ren, line_parameters lp,
							  int ex, int ey)
			:
			base(ren, lp)
		{
			m_di = new distance_interpolator2(lp.x1, lp.y1, lp.x2, lp.y2, ex, ey,
				 lp.x1 & ~LineAABasics.line_subpixel_mask, lp.y1 & ~LineAABasics.line_subpixel_mask,
				 0);
			base.m_li.adjust_forward();
			base.m_step -= base.m_max_extent;
		}

		//---------------------------------------------------------------------
		public bool step_hor()
		{
			int dist_end;
			int dist;
			int dy;
			int s1 = base.step_hor_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			dist_end = m_di.dist_end();

			int npix = 0;
			m_covers[Offset1] = 0;
			if (dist_end > 0)
			{
				m_covers[Offset1] = (byte)base.m_ren.cover(s1);
				++npix;
			}
			++Offset1;

			dy = 1;
			while ((dist = base.m_dist[dy] - s1) <= base.m_width)
			{
				dist_end -= m_di.dx_end();
				m_covers[Offset1] = 0;
				if (dist_end > 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++Offset1;
				++dy;
			}

			dy = 1;
			dist_end = m_di.dist_end();
			while ((dist = base.m_dist[dy] + s1) <= base.m_width)
			{
				dist_end += m_di.dx_end();
				m_covers[--Offset0] = 0;
				if (dist_end > 0)
				{
					m_covers[Offset0] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++dy;
			}
			base.m_ren.blend_solid_vspan(base.m_x,
											   base.m_y - dy + 1,
											   Offset1 - Offset0, m_covers,
											   Offset0);
			return npix != 0 && ++base.m_step < base.m_count;
		}

		//---------------------------------------------------------------------
		public bool step_ver()
		{
			int dist_end;
			int dist;
			int dx;
			int s1 = base.step_ver_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			dist_end = m_di.dist_end();

			int npix = 0;
			m_covers[Offset1] = 0;
			if (dist_end > 0)
			{
				m_covers[Offset1] = (byte)base.m_ren.cover(s1);
				++npix;
			}
			++Offset1;

			dx = 1;
			while ((dist = base.m_dist[dx] - s1) <= base.m_width)
			{
				dist_end += m_di.dy_end();
				m_covers[Offset1] = 0;
				if (dist_end > 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++Offset1;
				++dx;
			}

			dx = 1;
			dist_end = m_di.dist_end();
			while ((dist = base.m_dist[dx] + s1) <= base.m_width)
			{
				dist_end -= m_di.dy_end();
				m_covers[--Offset0] = 0;
				if (dist_end > 0)
				{
					m_covers[Offset0] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++dx;
			}
			base.m_ren.blend_solid_hspan(base.m_x - dx + 1,
											   base.m_y,
											   Offset1 - Offset0, m_covers,
											   Offset0);
			return npix != 0 && ++base.m_step < base.m_count;
		}
	};

	//====================================================line_interpolator_aa3
	public class line_interpolator_aa3 : line_interpolator_aa_base
	{
		private distance_interpolator3 m_di;
		//typedef Renderer renderer_type;
		//typedef line_interpolator_aa_base<Renderer> base_type;

		//---------------------------------------------------------------------
		public line_interpolator_aa3(OutlineRenderer ren, line_parameters lp,
							  int sx, int sy, int ex, int ey)
			:
			base(ren, lp)
		{
			m_di = new distance_interpolator3(lp.x1, lp.y1, lp.x2, lp.y2, sx, sy, ex, ey,
				 lp.x1 & ~LineAABasics.line_subpixel_mask, lp.y1 & ~LineAABasics.line_subpixel_mask);
			int dist1_start;
			int dist2_start;
			int npix = 1;
			if (lp.vertical)
			{
				do
				{
					base.m_li.Prev();
					base.m_y -= lp.inc;
					base.m_x = (base.m_lp.x1 + base.m_li.y()) >> LineAABasics.line_subpixel_shift;

					if (lp.inc > 0) m_di.dec_y(base.m_x - base.m_old_x);
					else m_di.inc_y(base.m_x - base.m_old_x);

					base.m_old_x = base.m_x;

					dist1_start = dist2_start = m_di.dist_start();

					int dx = 0;
					if (dist1_start < 0) ++npix;
					do
					{
						dist1_start += m_di.dy_start();
						dist2_start -= m_di.dy_start();
						if (dist1_start < 0) ++npix;
						if (dist2_start < 0) ++npix;
						++dx;
					}
					while (base.m_dist[dx] <= base.m_width);
					if (npix == 0) break;
					npix = 0;
				}
				while (--base.m_step >= -base.m_max_extent);
			}
			else
			{
				do
				{
					base.m_li.Prev();
					base.m_x -= lp.inc;
					base.m_y = (base.m_lp.y1 + base.m_li.y()) >> LineAABasics.line_subpixel_shift;

					if (lp.inc > 0) m_di.dec_x(base.m_y - base.m_old_y);
					else m_di.inc_x(base.m_y - base.m_old_y);

					base.m_old_y = base.m_y;

					dist1_start = dist2_start = m_di.dist_start();

					int dy = 0;
					if (dist1_start < 0) ++npix;
					do
					{
						dist1_start -= m_di.dx_start();
						dist2_start += m_di.dx_start();
						if (dist1_start < 0) ++npix;
						if (dist2_start < 0) ++npix;
						++dy;
					}
					while (base.m_dist[dy] <= base.m_width);
					if (npix == 0) break;
					npix = 0;
				}
				while (--base.m_step >= -base.m_max_extent);
			}
			base.m_li.adjust_forward();
			base.m_step -= base.m_max_extent;
		}

		//---------------------------------------------------------------------
		public bool step_hor()
		{
			int dist_start;
			int dist_end;
			int dist;
			int dy;
			int s1 = base.step_hor_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			dist_start = m_di.dist_start();
			dist_end = m_di.dist_end();

			int npix = 0;
			m_covers[Offset1] = 0;
			if (dist_end > 0)
			{
				if (dist_start <= 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(s1);
				}
				++npix;
			}
			++Offset1;

			dy = 1;
			while ((dist = base.m_dist[dy] - s1) <= base.m_width)
			{
				dist_start -= m_di.dx_start();
				dist_end -= m_di.dx_end();
				m_covers[Offset1] = 0;
				if (dist_end > 0 && dist_start <= 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++Offset1;
				++dy;
			}

			dy = 1;
			dist_start = m_di.dist_start();
			dist_end = m_di.dist_end();
			while ((dist = base.m_dist[dy] + s1) <= base.m_width)
			{
				dist_start += m_di.dx_start();
				dist_end += m_di.dx_end();
				m_covers[--Offset0] = 0;
				if (dist_end > 0 && dist_start <= 0)
				{
					m_covers[Offset0] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++dy;
			}
			base.m_ren.blend_solid_vspan(base.m_x,
											   base.m_y - dy + 1,
											   Offset1 - Offset0, m_covers,
											   Offset0);
			return npix != 0 && ++base.m_step < base.m_count;
		}

		//---------------------------------------------------------------------
		public bool step_ver()
		{
			int dist_start;
			int dist_end;
			int dist;
			int dx;
			int s1 = base.step_ver_base(m_di);
			int Offset0 = max_half_width + 2;
			int Offset1 = Offset0;

			dist_start = m_di.dist_start();
			dist_end = m_di.dist_end();

			int npix = 0;
			m_covers[Offset1] = 0;
			if (dist_end > 0)
			{
				if (dist_start <= 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(s1);
				}
				++npix;
			}
			++Offset1;

			dx = 1;
			while ((dist = base.m_dist[dx] - s1) <= base.m_width)
			{
				dist_start += m_di.dy_start();
				dist_end += m_di.dy_end();
				m_covers[Offset1] = 0;
				if (dist_end > 0 && dist_start <= 0)
				{
					m_covers[Offset1] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++Offset1;
				++dx;
			}

			dx = 1;
			dist_start = m_di.dist_start();
			dist_end = m_di.dist_end();
			while ((dist = base.m_dist[dx] + s1) <= base.m_width)
			{
				dist_start -= m_di.dy_start();
				dist_end -= m_di.dy_end();
				m_covers[--Offset0] = 0;
				if (dist_end > 0 && dist_start <= 0)
				{
					m_covers[Offset0] = (byte)base.m_ren.cover(dist);
					++npix;
				}
				++dx;
			}
			base.m_ren.blend_solid_hspan(base.m_x - dx + 1,
											   base.m_y,
											   Offset1 - Offset0, m_covers,
											   Offset0);
			return npix != 0 && ++base.m_step < base.m_count;
		}
	};

	//==========================================================line_profile_aa
	//
	// See Implementation agg_line_profile_aa.cpp
	//
	public class LineProfileAnitAlias
	{
		private const int subpixel_shift = 8;
		private const int subpixel_scale = 1 << subpixel_shift;
		private const int subpixel_mask = subpixel_scale - 1;

		private const int aa_shift = 8;
		private const int aa_scale = 1 << aa_shift;
		private const int aa_mask = aa_scale - 1;

		private ArrayPOD<byte> m_profile = new ArrayPOD<byte>();
		private byte[] m_gamma = new byte[aa_scale];
		private int m_subpixel_width;
		private double m_min_width;
		private double m_smoother_width;

		//---------------------------------------------------------------------

		//---------------------------------------------------------------------
		public LineProfileAnitAlias()
		{
			m_subpixel_width = (0);
			m_min_width = (1.0);
			m_smoother_width = (1.0);

			int i;
			for (i = 0; i < aa_scale; i++) m_gamma[i] = (byte)i;
		}

		//---------------------------------------------------------------------
		public LineProfileAnitAlias(double w, IGammaFunction gamma_function)
		{
			m_subpixel_width = (0);
			m_min_width = (1.0);
			m_smoother_width = (1.0);
			gamma(gamma_function);
			width(w);
		}

		//---------------------------------------------------------------------
		public void min_width(double w)
		{
			m_min_width = w;
		}

		public void smoother_width(double w)
		{
			m_smoother_width = w;
		}

		//---------------------------------------------------------------------
		public void gamma(IGammaFunction gamma_function)
		{
			int i;
			for (i = 0; i < aa_scale; i++)
			{
				m_gamma[i] = (byte)(agg_basics.uround(gamma_function.GetGamma((double)(i) / aa_mask) * aa_mask));
			}
		}

		public void width(double w)
		{
			if (w < 0.0) w = 0.0;

			if (w < m_smoother_width) w += w;
			else w += m_smoother_width;

			w *= 0.5;

			w -= m_smoother_width;
			double s = m_smoother_width;
			if (w < 0.0)
			{
				s += w;
				w = 0.0;
			}
			set(w, s);
		}

		public int profile_size()
		{
			return m_profile.Size();
		}

		public int subpixel_width()
		{
			return m_subpixel_width;
		}

		//---------------------------------------------------------------------
		public double min_width()
		{
			return m_min_width;
		}

		public double smoother_width()
		{
			return m_smoother_width;
		}

		//---------------------------------------------------------------------
		public byte value(int dist)
		{
			return m_profile.Array[dist + subpixel_scale * 2];
		}

		private byte[] profile(double w)
		{
			m_subpixel_width = (int)agg_basics.uround(w * subpixel_scale);
			int size = m_subpixel_width + subpixel_scale * 6;
			if (size > m_profile.Size())
			{
				m_profile.Resize(size);
			}
			return m_profile.Array;
		}

		private void set(double center_width, double smoother_width)
		{
			double base_val = 1.0;
			if (center_width == 0.0) center_width = 1.0 / subpixel_scale;
			if (smoother_width == 0.0) smoother_width = 1.0 / subpixel_scale;

			double width = center_width + smoother_width;
			if (width < m_min_width)
			{
				double k = width / m_min_width;
				base_val *= k;
				center_width /= k;
				smoother_width /= k;
			}

			byte[] ch = profile(center_width + smoother_width);
			int chIndex = 0;

			int subpixel_center_width = (int)(center_width * subpixel_scale);
			int subpixel_smoother_width = (int)(smoother_width * subpixel_scale);

			int ch_center = subpixel_scale * 2;
			int ch_smoother = ch_center + subpixel_center_width;

			int i;

			int val = m_gamma[(int)(base_val * aa_mask)];
			chIndex = ch_center;
			for (i = 0; i < subpixel_center_width; i++)
			{
				ch[chIndex++] = (byte)val;
			}

			for (i = 0; i < subpixel_smoother_width; i++)
			{
				ch[ch_smoother++] =
					m_gamma[(int)((base_val -
									  base_val *
									  ((double)(i) / subpixel_smoother_width)) * aa_mask)];
			}

			int n_smoother = ch.Length -
								  subpixel_smoother_width -
								  subpixel_center_width -
								  subpixel_scale * 2;

			val = m_gamma[0];
			for (i = 0; i < n_smoother; i++)
			{
				ch[ch_smoother++] = (byte)val;
			}

			chIndex = ch_center;
			for (i = 0; i < subpixel_scale * 2; i++)
			{
				ch[--chIndex] = ch[ch_center++];
			}

			for (i = 0; i < ch.Length; i++)
			{
				m_profile.Array[i] = ch[i];
			}
		}
	};

	public class ellipse_bresenham_interpolator
	{
		private int m_rx2;
		private int m_ry2;
		private int m_two_rx2;
		private int m_two_ry2;
		private int m_dx;
		private int m_dy;
		private int m_inc_x;
		private int m_inc_y;
		private int m_cur_f;

		public ellipse_bresenham_interpolator(int rx, int ry)
		{
			m_rx2 = (rx * rx);
			m_ry2 = (ry * ry);
			m_two_rx2 = (m_rx2 << 1);
			m_two_ry2 = (m_ry2 << 1);
			m_dx = (0);
			m_dy = (0);
			m_inc_x = (0);
			m_inc_y = (-ry * m_two_rx2);
			m_cur_f = (0);
		}

		public int dx()
		{
			return m_dx;
		}

		public int dy()
		{
			return m_dy;
		}

		public void Next()
		{
			int mx, my, mxy, min_m;
			int fx, fy, fxy;

			mx = fx = m_cur_f + m_inc_x + m_ry2;
			if (mx < 0) mx = -mx;

			my = fy = m_cur_f + m_inc_y + m_rx2;
			if (my < 0) my = -my;

			mxy = fxy = m_cur_f + m_inc_x + m_ry2 + m_inc_y + m_rx2;
			if (mxy < 0) mxy = -mxy;

			min_m = mx;
			bool flag = true;

			if (min_m > my)
			{
				min_m = my;
				flag = false;
			}

			m_dx = m_dy = 0;

			if (min_m > mxy)
			{
				m_inc_x += m_two_ry2;
				m_inc_y += m_two_rx2;
				m_cur_f = fxy;
				m_dx = 1;
				m_dy = 1;
				return;
			}

			if (flag)
			{
				m_inc_x += m_two_ry2;
				m_cur_f = fx;
				m_dx = 1;
				return;
			}

			m_inc_y += m_two_rx2;
			m_cur_f = fy;
			m_dy = 1;
		}
	};

	public abstract class LineRenderer
	{
		private RGBA_Bytes m_color;

		public delegate bool CompareFunction(int value);

		public RGBA_Bytes color()
		{
			return m_color;
		}

		public void color(IColorType c)
		{
			m_color = c.GetAsRGBA_Bytes();
		}

		public abstract void semidot(CompareFunction cmp, int xc1, int yc1, int xc2, int yc2);

		public abstract void semidot_hline(CompareFunction cmp, int xc1, int yc1, int xc2, int yc2, int x1, int y1, int x2);

		public abstract void pie(int xc, int yc, int x1, int y1, int x2, int y2);

		public abstract void line0(line_parameters lp);

		public abstract void line1(line_parameters lp, int sx, int sy);

		public abstract void line2(line_parameters lp, int ex, int ey);

		public abstract void line3(line_parameters lp, int sx, int sy, int ex, int ey);
	}

	//======================================================renderer_outline_aa
	public class OutlineRenderer : LineRenderer
	{
		private IImageByte destImageSurface;
		private LineProfileAnitAlias lineProfile;
		private RectangleInt clippingRectangle;
		private bool doClipping;
		protected const int max_half_width = 64;

#if false
        public int min_x() { throw new System.NotImplementedException(); }
        public int min_y() { throw new System.NotImplementedException(); }
        public int max_x() { throw new System.NotImplementedException(); }
        public int max_y() { throw new System.NotImplementedException(); }
        public void gamma(IGammaFunction gamma_function) { throw new System.NotImplementedException(); }
        public bool sweep_scanline(IScanlineCache sl) { throw new System.NotImplementedException(); }
        public void reset() { throw new System.NotImplementedException(); }
#endif

		//---------------------------------------------------------------------
		public OutlineRenderer(IImageByte destImage, LineProfileAnitAlias profile)
		{
			destImageSurface = destImage;
			lineProfile = profile;
			clippingRectangle = new RectangleInt(0, 0, 0, 0);
			doClipping = false;
		}

		public void attach(IImageByte ren)
		{
			destImageSurface = ren;
		}

		//---------------------------------------------------------------------
		public void profile(LineProfileAnitAlias prof)
		{
			lineProfile = prof;
		}

		public LineProfileAnitAlias profile()
		{
			return lineProfile;
		}

		//---------------------------------------------------------------------
		public int subpixel_width()
		{
			return lineProfile.subpixel_width();
		}

		//---------------------------------------------------------------------
		public void reset_clipping()
		{
			doClipping = false;
		}

		public void clip_box(double x1, double y1, double x2, double y2)
		{
			clippingRectangle.Left = line_coord_sat.conv(x1);
			clippingRectangle.Bottom = line_coord_sat.conv(y1);
			clippingRectangle.Right = line_coord_sat.conv(x2);
			clippingRectangle.Top = line_coord_sat.conv(y2);
			doClipping = true;
		}

		//---------------------------------------------------------------------
		public int cover(int d)
		{
			return lineProfile.value(d);
		}

		public void blend_solid_hspan(int x, int y, int len, byte[] covers, int coversOffset)
		{
			destImageSurface.blend_solid_hspan(x, y, len, color(), covers, coversOffset);
		}

		public void blend_solid_vspan(int x, int y, int len, byte[] covers, int coversOffset)
		{
			destImageSurface.blend_solid_vspan(x, y, len, color(), covers, coversOffset);
		}

		public static bool accurate_join_only()
		{
			return false;
		}

		public override void semidot_hline(CompareFunction cmp,
						   int xc1, int yc1, int xc2, int yc2,
						   int x1, int y1, int x2)
		{
			byte[] covers = new byte[max_half_width * 2 + 4];
			int Offset0 = 0;
			int Offset1 = 0;
			int x = x1 << LineAABasics.line_subpixel_shift;
			int y = y1 << LineAABasics.line_subpixel_shift;
			int w = subpixel_width();
			distance_interpolator0 di = new distance_interpolator0(xc1, yc1, xc2, yc2, x, y);
			x += LineAABasics.line_subpixel_scale / 2;
			y += LineAABasics.line_subpixel_scale / 2;

			int x0 = x1;
			int dx = x - xc1;
			int dy = y - yc1;
			do
			{
				int d = (int)(agg_math.fast_sqrt(dx * dx + dy * dy));
				covers[Offset1] = 0;
				if (cmp(di.dist()) && d <= w)
				{
					covers[Offset1] = (byte)cover(d);
				}
				++Offset1;
				dx += LineAABasics.line_subpixel_scale;
				di.inc_x();
			}
			while (++x1 <= x2);
			destImageSurface.blend_solid_hspan(x0, y1,
									 Offset1 - Offset0,
									 color(), covers,
									 Offset0);
		}

		public override void semidot(CompareFunction cmp, int xc1, int yc1, int xc2, int yc2)
		{
			if (doClipping && ClipLiangBarsky.clipping_flags(xc1, yc1, clippingRectangle) != 0) return;

			int r = ((subpixel_width() + LineAABasics.line_subpixel_mask) >> LineAABasics.line_subpixel_shift);
			if (r < 1) r = 1;
			ellipse_bresenham_interpolator ei = new ellipse_bresenham_interpolator(r, r);
			int dx = 0;
			int dy = -r;
			int dy0 = dy;
			int dx0 = dx;
			int x = xc1 >> LineAABasics.line_subpixel_shift;
			int y = yc1 >> LineAABasics.line_subpixel_shift;

			do
			{
				dx += ei.dx();
				dy += ei.dy();

				if (dy != dy0)
				{
					semidot_hline(cmp, xc1, yc1, xc2, yc2, x - dx0, y + dy0, x + dx0);
					semidot_hline(cmp, xc1, yc1, xc2, yc2, x - dx0, y - dy0, x + dx0);
				}
				dx0 = dx;
				dy0 = dy;
				ei.Next();
			}
			while (dy < 0);
			semidot_hline(cmp, xc1, yc1, xc2, yc2, x - dx0, y + dy0, x + dx0);
		}

		public void pie_hline(int xc, int yc, int xp1, int yp1, int xp2, int yp2,
					   int xh1, int yh1, int xh2)
		{
			if (doClipping && ClipLiangBarsky.clipping_flags(xc, yc, clippingRectangle) != 0) return;

			byte[] covers = new byte[max_half_width * 2 + 4];
			int index0 = 0;
			int index1 = 0;
			int x = xh1 << LineAABasics.line_subpixel_shift;
			int y = yh1 << LineAABasics.line_subpixel_shift;
			int w = subpixel_width();

			distance_interpolator00 di = new distance_interpolator00(xc, yc, xp1, yp1, xp2, yp2, x, y);
			x += LineAABasics.line_subpixel_scale / 2;
			y += LineAABasics.line_subpixel_scale / 2;

			int xh0 = xh1;
			int dx = x - xc;
			int dy = y - yc;
			do
			{
				int d = (int)(agg_math.fast_sqrt(dx * dx + dy * dy));
				covers[index1] = 0;
				if (di.dist1() <= 0 && di.dist2() > 0 && d <= w)
				{
					covers[index1] = (byte)cover(d);
				}
				++index1;
				dx += LineAABasics.line_subpixel_scale;
				di.inc_x();
			}
			while (++xh1 <= xh2);
			destImageSurface.blend_solid_hspan(xh0, yh1, index1 - index0, color(), covers, index0);
		}

		public override void pie(int xc, int yc, int x1, int y1, int x2, int y2)
		{
			int r = ((subpixel_width() + LineAABasics.line_subpixel_mask) >> LineAABasics.line_subpixel_shift);
			if (r < 1) r = 1;
			ellipse_bresenham_interpolator ei = new ellipse_bresenham_interpolator(r, r);
			int dx = 0;
			int dy = -r;
			int dy0 = dy;
			int dx0 = dx;
			int x = xc >> LineAABasics.line_subpixel_shift;
			int y = yc >> LineAABasics.line_subpixel_shift;

			do
			{
				dx += ei.dx();
				dy += ei.dy();

				if (dy != dy0)
				{
					pie_hline(xc, yc, x1, y1, x2, y2, x - dx0, y + dy0, x + dx0);
					pie_hline(xc, yc, x1, y1, x2, y2, x - dx0, y - dy0, x + dx0);
				}
				dx0 = dx;
				dy0 = dy;
				ei.Next();
			}
			while (dy < 0);
			pie_hline(xc, yc, x1, y1, x2, y2, x - dx0, y + dy0, x + dx0);
		}

		public void line0_no_clip(line_parameters lp)
		{
			if (lp.len > LineAABasics.line_max_length)
			{
				line_parameters lp1, lp2;
				lp.divide(out lp1, out lp2);
				line0_no_clip(lp1);
				line0_no_clip(lp2);
				return;
			}

			line_interpolator_aa0 li = new line_interpolator_aa0(this, lp);
			if (li.count() != 0)
			{
				if (li.vertical())
				{
					while (li.step_ver()) ;
				}
				else
				{
					while (li.step_hor()) ;
				}
			}
		}

		public override void line0(line_parameters lp)
		{
			if (doClipping)
			{
				int x1 = lp.x1;
				int y1 = lp.y1;
				int x2 = lp.x2;
				int y2 = lp.y2;
				int flags = ClipLiangBarsky.clip_line_segment(ref x1, ref y1, ref x2, ref y2, clippingRectangle);
				if ((flags & 4) == 0)
				{
					if (flags != 0)
					{
						line_parameters lp2 = new line_parameters(x1, y1, x2, y2,
										   agg_basics.uround(agg_math.calc_distance(x1, y1, x2, y2)));
						line0_no_clip(lp2);
					}
					else
					{
						line0_no_clip(lp);
					}
				}
			}
			else
			{
				line0_no_clip(lp);
			}
		}

		public void line1_no_clip(line_parameters lp, int sx, int sy)
		{
			if (lp.len > LineAABasics.line_max_length)
			{
				line_parameters lp1, lp2;
				lp.divide(out lp1, out lp2);
				line1_no_clip(lp1, (lp.x1 + sx) >> 1, (lp.y1 + sy) >> 1);
				line1_no_clip(lp2, lp1.x2 + (lp1.y2 - lp1.y1), lp1.y2 - (lp1.x2 - lp1.x1));
				return;
			}

			LineAABasics.fix_degenerate_bisectrix_start(lp, ref sx, ref sy);
			line_interpolator_aa1 li = new line_interpolator_aa1(this, lp, sx, sy);
			if (li.vertical())
			{
				while (li.step_ver()) ;
			}
			else
			{
				while (li.step_hor()) ;
			}
		}

		public override void line1(line_parameters lp, int sx, int sy)
		{
			if (doClipping)
			{
				int x1 = lp.x1;
				int y1 = lp.y1;
				int x2 = lp.x2;
				int y2 = lp.y2;
				int flags = ClipLiangBarsky.clip_line_segment(ref x1, ref y1, ref x2, ref y2, clippingRectangle);
				if ((flags & 4) == 0)
				{
					if (flags != 0)
					{
						line_parameters lp2 = new line_parameters(x1, y1, x2, y2,
										   agg_basics.uround(agg_math.calc_distance(x1, y1, x2, y2)));
						if (((int)flags & 1) != 0)
						{
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
						line1_no_clip(lp2, sx, sy);
					}
					else
					{
						line1_no_clip(lp, sx, sy);
					}
				}
			}
			else
			{
				line1_no_clip(lp, sx, sy);
			}
		}

		public void line2_no_clip(line_parameters lp, int ex, int ey)
		{
			if (lp.len > LineAABasics.line_max_length)
			{
				line_parameters lp1, lp2;
				lp.divide(out lp1, out lp2);
				line2_no_clip(lp1, lp1.x2 + (lp1.y2 - lp1.y1), lp1.y2 - (lp1.x2 - lp1.x1));
				line2_no_clip(lp2, (lp.x2 + ex) >> 1, (lp.y2 + ey) >> 1);
				return;
			}

			LineAABasics.fix_degenerate_bisectrix_end(lp, ref ex, ref ey);
			line_interpolator_aa2 li = new line_interpolator_aa2(this, lp, ex, ey);
			if (li.vertical())
			{
				while (li.step_ver()) ;
			}
			else
			{
				while (li.step_hor()) ;
			}
		}

		public override void line2(line_parameters lp, int ex, int ey)
		{
			if (doClipping)
			{
				int x1 = lp.x1;
				int y1 = lp.y1;
				int x2 = lp.x2;
				int y2 = lp.y2;
				int flags = ClipLiangBarsky.clip_line_segment(ref x1, ref y1, ref x2, ref y2, clippingRectangle);
				if ((flags & 4) == 0)
				{
					if (flags != 0)
					{
						line_parameters lp2 = new line_parameters(x1, y1, x2, y2,
										   agg_basics.uround(agg_math.calc_distance(x1, y1, x2, y2)));
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
						line2_no_clip(lp2, ex, ey);
					}
					else
					{
						line2_no_clip(lp, ex, ey);
					}
				}
			}
			else
			{
				line2_no_clip(lp, ex, ey);
			}
		}

		public void line3_no_clip(line_parameters lp,
						   int sx, int sy, int ex, int ey)
		{
			if (lp.len > LineAABasics.line_max_length)
			{
				line_parameters lp1, lp2;
				lp.divide(out lp1, out lp2);
				int mx = lp1.x2 + (lp1.y2 - lp1.y1);
				int my = lp1.y2 - (lp1.x2 - lp1.x1);
				line3_no_clip(lp1, (lp.x1 + sx) >> 1, (lp.y1 + sy) >> 1, mx, my);
				line3_no_clip(lp2, mx, my, (lp.x2 + ex) >> 1, (lp.y2 + ey) >> 1);
				return;
			}

			LineAABasics.fix_degenerate_bisectrix_start(lp, ref sx, ref sy);
			LineAABasics.fix_degenerate_bisectrix_end(lp, ref ex, ref ey);
			line_interpolator_aa3 li = new line_interpolator_aa3(this, lp, sx, sy, ex, ey);
			if (li.vertical())
			{
				while (li.step_ver()) ;
			}
			else
			{
				while (li.step_hor()) ;
			}
		}

		public override void line3(line_parameters lp,
				   int sx, int sy, int ex, int ey)
		{
			if (doClipping)
			{
				int x1 = lp.x1;
				int y1 = lp.y1;
				int x2 = lp.x2;
				int y2 = lp.y2;
				int flags = ClipLiangBarsky.clip_line_segment(ref x1, ref y1, ref x2, ref y2, clippingRectangle);
				if ((flags & 4) == 0)
				{
					if (flags != 0)
					{
						line_parameters lp2 = new line_parameters(x1, y1, x2, y2,
							agg_basics.uround(agg_math.calc_distance(x1, y1, x2, y2)));
						if ((flags & 1) != 0)
						{
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
			}
			else
			{
				line3_no_clip(lp, sx, sy, ex, ey);
			}
		}
	};

#endif
}