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
//
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.VertexSource
{
	//=======================================================span_gouraud_rgba
	public sealed class span_gouraud_rgba : span_gouraud, ISpanGenerator
	{
		private bool m_swap;
		private int m_y2;
		private rgba_calc m_rgba1;
		private rgba_calc m_rgba2;
		private rgba_calc m_rgba3;

		public enum subpixel_scale_e
		{
			subpixel_shift = 4,
			subpixel_scale = 1 << subpixel_shift
		};

		//--------------------------------------------------------------------
		public struct rgba_calc
		{
			public void init(span_gouraud.coord_type c1, span_gouraud.coord_type c2)
			{
				m_x1 = c1.x - 0.5;
				m_y1 = c1.y - 0.5;
				m_dx = c2.x - c1.x;
				double dy = c2.y - c1.y;
				m_1dy = (dy < 1e-5) ? 1e5 : 1.0 / dy;
				m_r1 = (int)c1.color.red;
				m_g1 = (int)c1.color.green;
				m_b1 = (int)c1.color.blue;
				m_a1 = (int)c1.color.alpha;
				m_dr = (int)c2.color.red - m_r1;
				m_dg = (int)c2.color.green - m_g1;
				m_db = (int)c2.color.blue - m_b1;
				m_da = (int)c2.color.alpha - m_a1;
			}

			public void calc(double y)
			{
				double k = (y - m_y1) * m_1dy;
				if (k < 0.0) k = 0.0;
				if (k > 1.0) k = 1.0;
				m_r = m_r1 + agg_basics.iround(m_dr * k);
				m_g = m_g1 + agg_basics.iround(m_dg * k);
				m_b = m_b1 + agg_basics.iround(m_db * k);
				m_a = m_a1 + agg_basics.iround(m_da * k);
				m_x = agg_basics.iround((m_x1 + m_dx * k) * (double)subpixel_scale_e.subpixel_scale);
			}

			public double m_x1;
			public double m_y1;
			public double m_dx;
			public double m_1dy;
			public int m_r1;
			public int m_g1;
			public int m_b1;
			public int m_a1;
			public int m_dr;
			public int m_dg;
			public int m_db;
			public int m_da;
			public int m_r;
			public int m_g;
			public int m_b;
			public int m_a;
			public int m_x;
		};

		//--------------------------------------------------------------------
		public span_gouraud_rgba()
		{
		}

		public span_gouraud_rgba(RGBA_Bytes c1,
						  RGBA_Bytes c2,
						  RGBA_Bytes c3,
						  double x1, double y1,
						  double x2, double y2,
						  double x3, double y3)
			: this(c1, c2, c3, x1, y1, x2, y2, x3, y3, 0)
		{ }

		public span_gouraud_rgba(RGBA_Bytes c1,
						  RGBA_Bytes c2,
						  RGBA_Bytes c3,
						  double x1, double y1,
						  double x2, double y2,
						  double x3, double y3,
						  double d)
			: base(c1, c2, c3, x1, y1, x2, y2, x3, y3, d)
		{ }

		//--------------------------------------------------------------------
		public void prepare()
		{
			coord_type[] coord = new coord_type[3];
			base.arrange_vertices(coord);

			m_y2 = (int)(coord[1].y);

			m_swap = agg_math.cross_product(coord[0].x, coord[0].y,
								   coord[2].x, coord[2].y,
								   coord[1].x, coord[1].y) < 0.0;

			m_rgba1.init(coord[0], coord[2]);
			m_rgba2.init(coord[0], coord[1]);
			m_rgba3.init(coord[1], coord[2]);
		}

		public void generate(RGBA_Bytes[] span, int spanIndex, int x, int y, int len)
		{
			m_rgba1.calc(y);//(m_rgba1.m_1dy > 2) ? m_rgba1.m_y1 : y);
			rgba_calc pc1 = m_rgba1;
			rgba_calc pc2 = m_rgba2;

			if (y <= m_y2)
			{
				// Bottom part of the triangle (first subtriangle)
				//-------------------------
				m_rgba2.calc(y + m_rgba2.m_1dy);
			}
			else
			{
				// Upper part (second subtriangle)
				m_rgba3.calc(y - m_rgba3.m_1dy);
				//-------------------------
				pc2 = m_rgba3;
			}

			if (m_swap)
			{
				// It means that the triangle is oriented clockwise,
				// so that we need to swap the controlling structures
				//-------------------------
				rgba_calc t = pc2;
				pc2 = pc1;
				pc1 = t;
			}

			// Get the horizontal length with subpixel accuracy
			// and protect it from division by zero
			//-------------------------
			int nlen = Math.Abs(pc2.m_x - pc1.m_x);
			if (nlen <= 0) nlen = 1;

			dda_line_interpolator r = new dda_line_interpolator(pc1.m_r, pc2.m_r, nlen, 14);
			dda_line_interpolator g = new dda_line_interpolator(pc1.m_g, pc2.m_g, nlen, 14);
			dda_line_interpolator b = new dda_line_interpolator(pc1.m_b, pc2.m_b, nlen, 14);
			dda_line_interpolator a = new dda_line_interpolator(pc1.m_a, pc2.m_a, nlen, 14);

			// Calculate the starting point of the gradient with subpixel
			// accuracy and correct (roll back) the interpolators.
			// This operation will also clip the beginning of the span
			// if necessary.
			//-------------------------
			int start = pc1.m_x - (x << (int)subpixel_scale_e.subpixel_shift);
			r.Prev(start);
			g.Prev(start);
			b.Prev(start);
			a.Prev(start);
			nlen += start;

			int vr, vg, vb, va;
			uint lim = 255;

			// Beginning part of the span. Since we rolled back the
			// interpolators, the color values may have overflowed.
			// So that, we render the beginning part with checking
			// for overflow. It lasts until "start" is positive;
			// typically it's 1-2 pixels, but may be more in some cases.
			//-------------------------
			while (len != 0 && start > 0)
			{
				vr = r.y();
				vg = g.y();
				vb = b.y();
				va = a.y();
				if (vr < 0) vr = 0; if (vr > lim) vr = (int)lim;
				if (vg < 0) vg = 0; if (vg > lim) vg = (int)lim;
				if (vb < 0) vb = 0; if (vb > lim) vb = (int)lim;
				if (va < 0) va = 0; if (va > lim) va = (int)lim;
				span[spanIndex].red = (byte)vr;
				span[spanIndex].green = (byte)vg;
				span[spanIndex].blue = (byte)vb;
				span[spanIndex].alpha = (byte)va;
				r.Next((int)subpixel_scale_e.subpixel_scale);
				g.Next((int)subpixel_scale_e.subpixel_scale);
				b.Next((int)subpixel_scale_e.subpixel_scale);
				a.Next((int)subpixel_scale_e.subpixel_scale);
				nlen -= (int)subpixel_scale_e.subpixel_scale;
				start -= (int)subpixel_scale_e.subpixel_scale;
				++spanIndex;
				--len;
			}

			// Middle part, no checking for overflow.
			// Actual spans can be longer than the calculated length
			// because of anti-aliasing, thus, the interpolators can
			// overflow. But while "nlen" is positive we are safe.
			//-------------------------
			while (len != 0 && nlen > 0)
			{
				span[spanIndex].red = ((byte)r.y());
				span[spanIndex].green = ((byte)g.y());
				span[spanIndex].blue = ((byte)b.y());
				span[spanIndex].alpha = ((byte)a.y());
				r.Next((int)subpixel_scale_e.subpixel_scale);
				g.Next((int)subpixel_scale_e.subpixel_scale);
				b.Next((int)subpixel_scale_e.subpixel_scale);
				a.Next((int)subpixel_scale_e.subpixel_scale);
				nlen -= (int)subpixel_scale_e.subpixel_scale;
				++spanIndex;
				--len;
			}

			// Ending part; checking for overflow.
			// Typically it's 1-2 pixels, but may be more in some cases.
			//-------------------------
			while (len != 0)
			{
				vr = r.y();
				vg = g.y();
				vb = b.y();
				va = a.y();
				if (vr < 0) vr = 0; if (vr > lim) vr = (int)lim;
				if (vg < 0) vg = 0; if (vg > lim) vg = (int)lim;
				if (vb < 0) vb = 0; if (vb > lim) vb = (int)lim;
				if (va < 0) va = 0; if (va > lim) va = (int)lim;
				span[spanIndex].red = ((byte)vr);
				span[spanIndex].green = ((byte)vg);
				span[spanIndex].blue = ((byte)vb);
				span[spanIndex].alpha = ((byte)va);
				r.Next((int)subpixel_scale_e.subpixel_scale);
				g.Next((int)subpixel_scale_e.subpixel_scale);
				b.Next((int)subpixel_scale_e.subpixel_scale);
				a.Next((int)subpixel_scale_e.subpixel_scale);
				++spanIndex;
				--len;
			}
		}
	};
}