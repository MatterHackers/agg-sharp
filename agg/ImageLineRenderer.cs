//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
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
#if true

using MatterHackers.Agg.Image;
using System;

namespace MatterHackers.Agg
{
	/*
	//========================================================line_image_scale
	public class line_image_scale
	{
		IImage m_source;
		double        m_height;
		double        m_scale;

		public line_image_scale(IImage src, double height)
		{
			m_source = (src);
			m_height = (height);
			m_scale = (src.height() / height);
		}

		public double width()  { return m_source.width(); }
		public double height() { return m_height; }

		public RGBA_Bytes pixel(int x, int y)
		{
			double src_y = (y + 0.5) * m_scale - 0.5;
			int h  = m_source.height() - 1;
			int y1 = ufloor(src_y);
			int y2 = y1 + 1;
			RGBA_Bytes pix1 = (y1 < 0) ? new no_color() : m_source.pixel(x, y1);
			RGBA_Bytes pix2 = (y2 > h) ? no_color() : m_source.pixel(x, y2);
			return pix1.gradient(pix2, src_y - y1);
		}
	};

	 */

	//======================================================line_image_pattern
	public class line_image_pattern : ImageBuffer
	{
		private IPatternFilter m_filter;
		private int m_dilation;
		private int m_dilation_hr;
		private ImageBuffer m_buf = new ImageBuffer();
		private byte[] m_data = null;
		private int m_DataSizeInBytes = 0;
		private int m_width;
		private int m_height;
		private int m_width_hr;
		private int m_half_height_hr;
		private int m_offset_y_hr;

		//--------------------------------------------------------------------
		public line_image_pattern(IPatternFilter filter)
		{
			m_filter = filter;
			m_dilation = (filter.dilation() + 1);
			m_dilation_hr = (m_dilation << LineAABasics.line_subpixel_shift);
			m_width = (0);
			m_height = (0);
			m_width_hr = (0);
			m_half_height_hr = (0);
			m_offset_y_hr = (0);
		}

		~line_image_pattern()
		{
			if (m_DataSizeInBytes > 0)
			{
				m_data = null;
			}
		}

		// Create
		//--------------------------------------------------------------------
		public line_image_pattern(IPatternFilter filter, line_image_pattern src)
		{
			m_filter = (filter);
			m_dilation = (filter.dilation() + 1);
			m_dilation_hr = (m_dilation << LineAABasics.line_subpixel_shift);
			m_width = 0;
			m_height = 0;
			m_width_hr = 0;
			m_half_height_hr = 0;
			m_offset_y_hr = (0);

			create(src);
		}

		// Create
		//--------------------------------------------------------------------
		public void create(IImageByte src)
		{
			// we are going to create a dilated image for filtering
			// we add m_dilation pixels to every side of the image and then copy the image in the x
			// direction into each end so that we can sample into this image to get filtering on x repeating
			// if the original image look like this
			//
			// 123456
			//
			// the new image would look like this
			//
			// 0000000000
			// 0000000000
			// 5612345612
			// 0000000000
			// 0000000000

			m_height = (int)agg_basics.uceil(src.Height);
			m_width = (int)agg_basics.uceil(src.Width);
			m_width_hr = (int)agg_basics.uround(src.Width * LineAABasics.line_subpixel_scale);
			m_half_height_hr = (int)agg_basics.uround(src.Height * LineAABasics.line_subpixel_scale / 2);
			m_offset_y_hr = m_dilation_hr + m_half_height_hr - LineAABasics.line_subpixel_scale / 2;
			m_half_height_hr += LineAABasics.line_subpixel_scale / 2;

			int bufferWidth = m_width + m_dilation * 2;
			int bufferHeight = m_height + m_dilation * 2;
			int bytesPerPixel = src.BitDepth / 8;
			int NewSizeInBytes = bufferWidth * bufferHeight * bytesPerPixel;
			if (m_DataSizeInBytes < NewSizeInBytes)
			{
				m_DataSizeInBytes = NewSizeInBytes;
				m_data = new byte[m_DataSizeInBytes];
			}

			m_buf.AttachBuffer(m_data, 0, bufferWidth, bufferHeight, bufferWidth * bytesPerPixel, src.BitDepth, bytesPerPixel);
			byte[] destBuffer = m_buf.GetBuffer();
			byte[] sourceBuffer = src.GetBuffer();

			// copy the image into the middle of the dest
			for (int y = 0; y < m_height; y++)
			{
				for (int x = 0; x < m_width; x++)
				{
					int sourceOffset = src.GetBufferOffsetXY(x, y);
					int destOffset = m_buf.GetBufferOffsetXY(m_dilation, y + m_dilation);
					for (int channel = 0; channel < bytesPerPixel; channel++)
					{
						destBuffer[destOffset++] = sourceBuffer[sourceOffset++];
					}
				}
			}

			// copy the first two pixels form the end into the beginning and from the beginning into the end
			for (int y = 0; y < m_height; y++)
			{
				int s1Offset = src.GetBufferOffsetXY(0, y);
				int d1Offset = m_buf.GetBufferOffsetXY(m_dilation + m_width, y);

				int s2Offset = src.GetBufferOffsetXY(m_width - m_dilation, y);
				int d2Offset = m_buf.GetBufferOffsetXY(0, y);

				for (int x = 0; x < m_dilation; x++)
				{
					for (int channel = 0; channel < bytesPerPixel; channel++)
					{
						destBuffer[d1Offset++] = sourceBuffer[s1Offset++];
						destBuffer[d2Offset++] = sourceBuffer[s2Offset++];
					}
				}
			}
		}

		//--------------------------------------------------------------------
		public int pattern_width()
		{
			return m_width_hr;
		}

		public int line_width()
		{
			return m_half_height_hr;
		}

		public double width()
		{
			return m_height;
		}

		//--------------------------------------------------------------------
		public void pixel(RGBA_Bytes[] destBuffer, int destBufferOffset, int x, int y)
		{
			m_filter.pixel_high_res(m_buf, destBuffer, destBufferOffset,
									 x % m_width_hr + m_dilation_hr,
									 y + m_offset_y_hr);
		}

		//--------------------------------------------------------------------
		public IPatternFilter filter()
		{
			return m_filter;
		}
	};

	/*

	//=================================================line_image_pattern_pow2
	public class line_image_pattern_pow2 :
		line_image_pattern<IPatternFilter>
	{
		uint m_mask;

		//--------------------------------------------------------------------
		public line_image_pattern_pow2(IPatternFilter filter) :
			line_image_pattern<IPatternFilter>(filter), m_mask(line_subpixel_mask) {}

		//--------------------------------------------------------------------
		public line_image_pattern_pow2(IPatternFilter filter, ImageBuffer src) :
			line_image_pattern<IPatternFilter>(filter), m_mask(line_subpixel_mask)
		{
			create(src);
		}

		//--------------------------------------------------------------------
		public void create(ImageBuffer src)
		{
			line_image_pattern<IPatternFilter>::create(src);
			m_mask = 1;
			while(m_mask < base_type::m_width)
			{
				m_mask <<= 1;
				m_mask |= 1;
			}
			m_mask <<= line_subpixel_shift - 1;
			m_mask |=  line_subpixel_mask;
			base_type::m_width_hr = m_mask + 1;
		}

		//--------------------------------------------------------------------
		public void pixel(RGBA_Bytes* p, int x, int y)
		{
			base_type::m_filter->pixel_high_res(
					base_type::m_buf.rows(),
					p,
					(x & m_mask) + base_type::m_dilation_hr,
					y + base_type::m_offset_y_hr);
		}
	};
	 */

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

			m_dist = (agg_basics.iround((double)(x + LineAABasics.line_subpixel_scale / 2 - x2) * (double)(m_dy) -
						  (double)(y + LineAABasics.line_subpixel_scale / 2 - y2) * (double)(m_dx)));

			m_dist_start = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sx)) * m_dy_start -
						 (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(sy)) * m_dx_start);

			m_dist_end = ((LineAABasics.line_mr(x + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ex)) * m_dy_end -
					   (LineAABasics.line_mr(y + LineAABasics.line_subpixel_scale / 2) - LineAABasics.line_mr(ey)) * m_dx_end);
			m_len = (int)(agg_basics.uround(len / scale));

			double d = len * scale;
			int dx = agg_basics.iround(((x2 - x1) << LineAABasics.line_subpixel_shift) / d);
			int dy = agg_basics.iround(((y2 - y1) << LineAABasics.line_subpixel_shift) / d);
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

#if true
#if false
    //==================================================line_interpolator_image
    public class line_interpolator_image
    {
        line_parameters m_lp;
        dda2_line_interpolator m_li;
        distance_interpolator4 m_di;
        IImageByte m_ren;
        int m_plen;
        int m_x;
        int m_y;
        int m_old_x;
        int m_old_y;
        int m_width;
        int m_max_extent;
        int m_start;
        int m_step;
        int[] m_dist_pos = new int[max_half_width + 1];
        RGBA_Bytes[] m_colors = new RGBA_Bytes[max_half_width * 2 + 4];

        //---------------------------------------------------------------------
        public const int max_half_width = 64;

        //---------------------------------------------------------------------
        public line_interpolator_image(renderer_outline_aa ren, line_parameters lp,
                                int sx, int sy, int ex, int ey,
                                int pattern_start,
                                double scale_x)
        {
            throw new NotImplementedException();
/*
            m_lp=(lp);
            m_li = new dda2_line_interpolator(lp.vertical ? LineAABasics.line_dbl_hr(lp.x2 - lp.x1) :
                               LineAABasics.line_dbl_hr(lp.y2 - lp.y1),
                 lp.vertical ? Math.Abs(lp.y2 - lp.y1) :
                               Math.Abs(lp.x2 - lp.x1) + 1);
            m_di = new distance_interpolator4(lp.x1, lp.y1, lp.x2, lp.y2, sx, sy, ex, ey, lp.len, scale_x,
                 lp.x1 & ~LineAABasics.line_subpixel_mask, lp.y1 & ~LineAABasics.line_subpixel_mask);
            m_ren=ren;
            m_x = (lp.x1 >> LineAABasics.line_subpixel_shift);
            m_y = (lp.y1 >> LineAABasics.line_subpixel_shift);
            m_old_x=(m_x);
            m_old_y=(m_y);
            m_count = ((lp.vertical ? Math.Abs((lp.y2 >> LineAABasics.line_subpixel_shift) - m_y) :
                                   Math.Abs((lp.x2 >> LineAABasics.line_subpixel_shift) - m_x)));
            m_width=(ren.subpixel_width());
            //m_max_extent(m_width >> (LineAABasics.line_subpixel_shift - 2));
            m_max_extent = ((m_width + LineAABasics.line_subpixel_scale) >> LineAABasics.line_subpixel_shift);
            m_start=(pattern_start + (m_max_extent + 2) * ren.pattern_width());
            m_step=(0);

            dda2_line_interpolator li = new dda2_line_interpolator(0, lp.vertical ?
                                              (lp.dy << LineAABasics.line_subpixel_shift) :
                                              (lp.dx << LineAABasics.line_subpixel_shift),
                                           lp.len);

            uint i;
            int stop = m_width + LineAABasics.line_subpixel_scale * 2;
            for(i = 0; i < max_half_width; ++i)
            {
                m_dist_pos[i] = li.y();
                if(m_dist_pos[i] >= stop) break;
                ++li;
            }
            m_dist_pos[i] = 0x7FFF0000;

            int dist1_start;
            int dist2_start;
            int npix = 1;

            if(lp.vertical)
            {
                do
                {
                    --m_li;
                    m_y -= lp.inc;
                    m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

                    if(lp.inc > 0) m_di.dec_y(m_x - m_old_x);
                    else           m_di.inc_y(m_x - m_old_x);

                    m_old_x = m_x;

                    dist1_start = dist2_start = m_di.dist_start();

                    int dx = 0;
                    if(dist1_start < 0) ++npix;
                    do
                    {
                        dist1_start += m_di.dy_start();
                        dist2_start -= m_di.dy_start();
                        if(dist1_start < 0) ++npix;
                        if(dist2_start < 0) ++npix;
                        ++dx;
                    }
                    while(m_dist_pos[dx] <= m_width);
                    if(npix == 0) break;

                    npix = 0;
                }
                while(--m_step >= -m_max_extent);
            }
            else
            {
                do
                {
                    --m_li;

                    m_x -= lp.inc;
                    m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

                    if(lp.inc > 0) m_di.dec_x(m_y - m_old_y);
                    else           m_di.inc_x(m_y - m_old_y);

                    m_old_y = m_y;

                    dist1_start = dist2_start = m_di.dist_start();

                    int dy = 0;
                    if(dist1_start < 0) ++npix;
                    do
                    {
                        dist1_start -= m_di.dx_start();
                        dist2_start += m_di.dx_start();
                        if(dist1_start < 0) ++npix;
                        if(dist2_start < 0) ++npix;
                        ++dy;
                    }
                    while(m_dist_pos[dy] <= m_width);
                    if(npix == 0) break;

                    npix = 0;
                }
                while(--m_step >= -m_max_extent);
            }
            m_li.adjust_forward();
            m_step -= m_max_extent;
 */
        }

        //---------------------------------------------------------------------
        public bool step_hor()
        {
            throw new NotImplementedException();
/*
            ++m_li;
            m_x += m_lp.inc;
            m_y = (m_lp.y1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

            if(m_lp.inc > 0) m_di.inc_x(m_y - m_old_y);
            else             m_di.dec_x(m_y - m_old_y);

            m_old_y = m_y;

            int s1 = m_di.dist() / m_lp.len;
            int s2 = -s1;

            if(m_lp.inc < 0) s1 = -s1;

            int dist_start;
            int dist_pict;
            int dist_end;
            int dy;
            int dist;

            dist_start = m_di.dist_start();
            dist_pict  = m_di.dist_pict() + m_start;
            dist_end   = m_di.dist_end();
            RGBA_Bytes* p0 = m_colors + max_half_width + 2;
            RGBA_Bytes* p1 = p0;

            int npix = 0;
            p1->clear();
            if(dist_end > 0)
            {
                if(dist_start <= 0)
                {
                    m_ren.pixel(p1, dist_pict, s2);
                }
                ++npix;
            }
            ++p1;

            dy = 1;
            while((dist = m_dist_pos[dy]) - s1 <= m_width)
            {
                dist_start -= m_di.dx_start();
                dist_pict  -= m_di.dx_pict();
                dist_end   -= m_di.dx_end();
                p1->clear();
                if(dist_end > 0 && dist_start <= 0)
                {
                    if(m_lp.inc > 0) dist = -dist;
                    m_ren.pixel(p1, dist_pict, s2 - dist);
                    ++npix;
                }
                ++p1;
                ++dy;
            }

            dy = 1;
            dist_start = m_di.dist_start();
            dist_pict  = m_di.dist_pict() + m_start;
            dist_end   = m_di.dist_end();
            while((dist = m_dist_pos[dy]) + s1 <= m_width)
            {
                dist_start += m_di.dx_start();
                dist_pict  += m_di.dx_pict();
                dist_end   += m_di.dx_end();
                --p0;
                p0->clear();
                if(dist_end > 0 && dist_start <= 0)
                {
                    if(m_lp.inc > 0) dist = -dist;
                    m_ren.pixel(p0, dist_pict, s2 + dist);
                    ++npix;
                }
                ++dy;
            }
            m_ren.blend_color_vspan(m_x,
                                    m_y - dy + 1,
                                    (uint)(p1 - p0),
                                    p0);
            return npix && ++m_step < m_count;
 */
        }

        //---------------------------------------------------------------------
        public bool step_ver()
        {
            throw new NotImplementedException();
/*
            ++m_li;
            m_y += m_lp.inc;
            m_x = (m_lp.x1 + m_li.y()) >> LineAABasics.line_subpixel_shift;

            if(m_lp.inc > 0) m_di.inc_y(m_x - m_old_x);
            else             m_di.dec_y(m_x - m_old_x);

            m_old_x = m_x;

            int s1 = m_di.dist() / m_lp.len;
            int s2 = -s1;

            if(m_lp.inc > 0) s1 = -s1;

            int dist_start;
            int dist_pict;
            int dist_end;
            int dist;
            int dx;

            dist_start = m_di.dist_start();
            dist_pict  = m_di.dist_pict() + m_start;
            dist_end   = m_di.dist_end();
            RGBA_Bytes* p0 = m_colors + max_half_width + 2;
            RGBA_Bytes* p1 = p0;

            int npix = 0;
            p1->clear();
            if(dist_end > 0)
            {
                if(dist_start <= 0)
                {
                    m_ren.pixel(p1, dist_pict, s2);
                }
                ++npix;
            }
            ++p1;

            dx = 1;
            while((dist = m_dist_pos[dx]) - s1 <= m_width)
            {
                dist_start += m_di.dy_start();
                dist_pict  += m_di.dy_pict();
                dist_end   += m_di.dy_end();
                p1->clear();
                if(dist_end > 0 && dist_start <= 0)
                {
                    if(m_lp.inc > 0) dist = -dist;
                    m_ren.pixel(p1, dist_pict, s2 + dist);
                    ++npix;
                }
                ++p1;
                ++dx;
            }

            dx = 1;
            dist_start = m_di.dist_start();
            dist_pict  = m_di.dist_pict() + m_start;
            dist_end   = m_di.dist_end();
            while((dist = m_dist_pos[dx]) + s1 <= m_width)
            {
                dist_start -= m_di.dy_start();
                dist_pict  -= m_di.dy_pict();
                dist_end   -= m_di.dy_end();
                --p0;
                p0->clear();
                if(dist_end > 0 && dist_start <= 0)
                {
                    if(m_lp.inc > 0) dist = -dist;
                    m_ren.pixel(p0, dist_pict, s2 - dist);
                    ++npix;
                }
                ++dx;
            }
            m_ren.blend_color_hspan(m_x - dx + 1,
                                    m_y,
                                    (uint)(p1 - p0),
                                    p0);
            return npix && ++m_step < m_count;
 */
        }

        //---------------------------------------------------------------------
        public int  pattern_end() { return m_start + m_di.len(); }

        //---------------------------------------------------------------------
        public bool vertical() { return m_lp.vertical; }
        public int  width() { return m_width; }
    }
#endif

	//===================================================renderer_outline_image
	//template<class BaseRenderer, class ImagePattern>
	public class ImageLineRenderer : LineRenderer
	{
		private IImageByte m_ren;
		private line_image_pattern m_pattern;
		private int m_start;
		private double m_scale_x;
		private RectangleInt m_clip_box;
		private bool m_clipping;

		//---------------------------------------------------------------------
		//typedef renderer_outline_image<BaseRenderer, ImagePattern> self_type;

		//---------------------------------------------------------------------
		public ImageLineRenderer(IImageByte ren, line_image_pattern patt)
		{
			m_ren = ren;
			m_pattern = patt;
			m_start = (0);
			m_scale_x = (1.0);
			m_clip_box = new RectangleInt(0, 0, 0, 0);
			m_clipping = (false);
		}

		public void attach(IImageByte ren)
		{
			m_ren = ren;
		}

		//---------------------------------------------------------------------
		public void pattern(line_image_pattern p)
		{
			m_pattern = p;
		}

		public line_image_pattern pattern()
		{
			return m_pattern;
		}

		//---------------------------------------------------------------------
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

		//---------------------------------------------------------------------
		public void scale_x(double s)
		{
			m_scale_x = s;
		}

		public double scale_x()
		{
			return m_scale_x;
		}

		//---------------------------------------------------------------------
		public void start_x(double s)
		{
			m_start = agg_basics.iround(s * LineAABasics.line_subpixel_scale);
		}

		public double start_x()
		{
			return (double)(m_start) / LineAABasics.line_subpixel_scale;
		}

		//---------------------------------------------------------------------
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
			return (double)(subpixel_width()) / LineAABasics.line_subpixel_scale;
		}

		public void pixel(RGBA_Bytes[] p, int offset, int x, int y)
		{
			throw new NotImplementedException();

			//m_pattern.pixel(p, x, y);
		}

		public void blend_color_hspan(int x, int y, uint len, RGBA_Bytes[] colors, int colorsOffset)
		{
			throw new NotImplementedException();
			//            m_ren.blend_color_hspan(x, y, len, colors, null, 0);
		}

		public void blend_color_vspan(int x, int y, uint len, RGBA_Bytes[] colors, int colorsOffset)
		{
			throw new NotImplementedException();
			//            m_ren.blend_color_vspan(x, y, len, colors, null, 0);
		}

		public static bool accurate_join_only()
		{
			return true;
		}

		public override void semidot(CompareFunction cmp, int xc1, int yc1, int xc2, int yc2)
		{
		}

		public override void semidot_hline(CompareFunction cmp,
						   int xc1, int yc1, int xc2, int yc2,
						   int x1, int y1, int x2)
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

		public void line3_no_clip(line_parameters lp,
						   int sx, int sy, int ex, int ey)
		{
			throw new NotImplementedException();
			/*
						if(lp.len > LineAABasics.line_max_length)
						{
							line_parameters lp1, lp2;
							lp.divide(lp1, lp2);
							int mx = lp1.x2 + (lp1.y2 - lp1.y1);
							int my = lp1.y2 - (lp1.x2 - lp1.x1);
							line3_no_clip(lp1, (lp.x1 + sx) >> 1, (lp.y1 + sy) >> 1, mx, my);
							line3_no_clip(lp2, mx, my, (lp.x2 + ex) >> 1, (lp.y2 + ey) >> 1);
							return;
						}

						LineAABasics.fix_degenerate_bisectrix_start(lp, ref sx, ref sy);
						LineAABasics.fix_degenerate_bisectrix_end(lp, ref ex, ref ey);
						line_interpolator_image li = new line_interpolator_image(this, lp,
															  sx, sy,
															  ex, ey,
															  m_start, m_scale_x);
						if(li.vertical())
						{
							while(li.step_ver());
						}
						else
						{
							while(li.step_hor());
						}
						m_start += uround(lp.len / m_scale_x);
			 */
		}

		public override void line3(line_parameters lp,
				   int sx, int sy, int ex, int ey)
		{
			throw new NotImplementedException();
			/*
						if(m_clipping)
						{
							int x1 = lp.x1;
							int y1 = lp.y1;
							int x2 = lp.x2;
							int y2 = lp.y2;
							uint flags = clip_line_segment(&x1, &y1, &x2, &y2, m_clip_box);
							int start = m_start;
							if((flags & 4) == 0)
							{
								if(flags)
								{
									line_parameters lp2(x1, y1, x2, y2,
													   uround(calc_distance(x1, y1, x2, y2)));
									if(flags & 1)
									{
										m_start += uround(calc_distance(lp.x1, lp.y1, x1, y1) / m_scale_x);
										sx = x1 + (y2 - y1);
										sy = y1 - (x2 - x1);
									}
									else
									{
										while(Math.Abs(sx - lp.x1) + Math.Abs(sy - lp.y1) > lp2.len)
										{
											sx = (lp.x1 + sx) >> 1;
											sy = (lp.y1 + sy) >> 1;
										}
									}
									if(flags & 2)
									{
										ex = x2 + (y2 - y1);
										ey = y2 - (x2 - x1);
									}
									else
									{
										while(Math.Abs(ex - lp.x2) + Math.Abs(ey - lp.y2) > lp2.len)
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
							m_start = start + uround(lp.len / m_scale_x);
						}
						else
						{
							line3_no_clip(lp, sx, sy, ex, ey);
						}
			 */
		}
	};

#endif
}

#endif