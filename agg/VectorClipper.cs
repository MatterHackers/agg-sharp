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
//#ifndef AGG_RASTERIZER_SL_CLIP_INCLUDED
//#define AGG_RASTERIZER_SL_CLIP_INCLUDED

//#include "agg_clip_liang_barsky.h"

using poly_subpixel_scale_e = MatterHackers.Agg.agg_basics.poly_subpixel_scale_e;

namespace MatterHackers.Agg
{
	//--------------------------------------------------------poly_max_coord_e
	internal enum poly_max_coord_e
	{
		poly_max_coord = (1 << 30) - 1 //----poly_max_coord
	};

	public class VectorClipper
	{
		public RectangleInt clipBox;
		private int m_x1;
		private int m_y1;
		private int m_f1;
		private bool m_clipping;

		private int mul_div(double a, double b, double c)
		{
			return agg_basics.iround(a * b / c);
		}

		private int xi(int v)
		{
			return v;
		}

		private int yi(int v)
		{
			return v;
		}

		public int upscale(double v)
		{
			return agg_basics.iround(v * (int)poly_subpixel_scale_e.poly_subpixel_scale);
		}

		public int downscale(int v)
		{
			return v / (int)poly_subpixel_scale_e.poly_subpixel_scale;
		}

		//--------------------------------------------------------------------
		public VectorClipper()
		{
			clipBox = new RectangleInt(0, 0, 0, 0);
			m_x1 = (0);
			m_y1 = (0);
			m_f1 = (0);
			m_clipping = (false);
		}

		//--------------------------------------------------------------------
		public void reset_clipping()
		{
			m_clipping = false;
		}

		//--------------------------------------------------------------------
		public void clip_box(int x1, int y1, int x2, int y2)
		{
			clipBox = new RectangleInt(x1, y1, x2, y2);
			clipBox.normalize();
			m_clipping = true;
		}

		//--------------------------------------------------------------------
		public void move_to(int x1, int y1)
		{
			m_x1 = x1;
			m_y1 = y1;
			if (m_clipping)
			{
				m_f1 = ClipLiangBarsky.clipping_flags(x1, y1, clipBox);
			}
		}

		//------------------------------------------------------------------------
		private void line_clip_y(rasterizer_cells_aa ras,
									int x1, int y1,
									int x2, int y2,
									int f1, int f2)
		{
			f1 &= 10;
			f2 &= 10;
			if ((f1 | f2) == 0)
			{
				// Fully visible
				ras.line(x1, y1, x2, y2);
			}
			else
			{
				if (f1 == f2)
				{
					// Invisible by Y
					return;
				}

				int tx1 = x1;
				int ty1 = y1;
				int tx2 = x2;
				int ty2 = y2;

				if ((f1 & 8) != 0) // y1 < clip.y1
				{
					tx1 = x1 + mul_div(clipBox.Bottom - y1, x2 - x1, y2 - y1);
					ty1 = clipBox.Bottom;
				}

				if ((f1 & 2) != 0) // y1 > clip.y2
				{
					tx1 = x1 + mul_div(clipBox.Top - y1, x2 - x1, y2 - y1);
					ty1 = clipBox.Top;
				}

				if ((f2 & 8) != 0) // y2 < clip.y1
				{
					tx2 = x1 + mul_div(clipBox.Bottom - y1, x2 - x1, y2 - y1);
					ty2 = clipBox.Bottom;
				}

				if ((f2 & 2) != 0) // y2 > clip.y2
				{
					tx2 = x1 + mul_div(clipBox.Top - y1, x2 - x1, y2 - y1);
					ty2 = clipBox.Top;
				}

				ras.line(tx1, ty1, tx2, ty2);
			}
		}

		//--------------------------------------------------------------------
		public void line_to(rasterizer_cells_aa ras, int x2, int y2)
		{
			if (m_clipping)
			{
				int f2 = ClipLiangBarsky.clipping_flags(x2, y2, clipBox);

				if ((m_f1 & 10) == (f2 & 10) && (m_f1 & 10) != 0)
				{
					// Invisible by Y
					m_x1 = x2;
					m_y1 = y2;
					m_f1 = f2;
					return;
				}

				int x1 = m_x1;
				int y1 = m_y1;
				int f1 = m_f1;
				int y3, y4;
				int f3, f4;

				switch (((f1 & 5) << 1) | (f2 & 5))
				{
					case 0: // Visible by X
						line_clip_y(ras, x1, y1, x2, y2, f1, f2);
						break;

					case 1: // x2 > clip.x2
						y3 = y1 + mul_div(clipBox.Right - x1, y2 - y1, x2 - x1);
						f3 = ClipLiangBarsky.clipping_flags_y(y3, clipBox);
						line_clip_y(ras, x1, y1, clipBox.Right, y3, f1, f3);
						line_clip_y(ras, clipBox.Right, y3, clipBox.Right, y2, f3, f2);
						break;

					case 2: // x1 > clip.x2
						y3 = y1 + mul_div(clipBox.Right - x1, y2 - y1, x2 - x1);
						f3 = ClipLiangBarsky.clipping_flags_y(y3, clipBox);
						line_clip_y(ras, clipBox.Right, y1, clipBox.Right, y3, f1, f3);
						line_clip_y(ras, clipBox.Right, y3, x2, y2, f3, f2);
						break;

					case 3: // x1 > clip.x2 && x2 > clip.x2
						line_clip_y(ras, clipBox.Right, y1, clipBox.Right, y2, f1, f2);
						break;

					case 4: // x2 < clip.x1
						y3 = y1 + mul_div(clipBox.Left - x1, y2 - y1, x2 - x1);
						f3 = ClipLiangBarsky.clipping_flags_y(y3, clipBox);
						line_clip_y(ras, x1, y1, clipBox.Left, y3, f1, f3);
						line_clip_y(ras, clipBox.Left, y3, clipBox.Left, y2, f3, f2);
						break;

					case 6: // x1 > clip.x2 && x2 < clip.x1
						y3 = y1 + mul_div(clipBox.Right - x1, y2 - y1, x2 - x1);
						y4 = y1 + mul_div(clipBox.Left - x1, y2 - y1, x2 - x1);
						f3 = ClipLiangBarsky.clipping_flags_y(y3, clipBox);
						f4 = ClipLiangBarsky.clipping_flags_y(y4, clipBox);
						line_clip_y(ras, clipBox.Right, y1, clipBox.Right, y3, f1, f3);
						line_clip_y(ras, clipBox.Right, y3, clipBox.Left, y4, f3, f4);
						line_clip_y(ras, clipBox.Left, y4, clipBox.Left, y2, f4, f2);
						break;

					case 8: // x1 < clip.x1
						y3 = y1 + mul_div(clipBox.Left - x1, y2 - y1, x2 - x1);
						f3 = ClipLiangBarsky.clipping_flags_y(y3, clipBox);
						line_clip_y(ras, clipBox.Left, y1, clipBox.Left, y3, f1, f3);
						line_clip_y(ras, clipBox.Left, y3, x2, y2, f3, f2);
						break;

					case 9:  // x1 < clip.x1 && x2 > clip.x2
						y3 = y1 + mul_div(clipBox.Left - x1, y2 - y1, x2 - x1);
						y4 = y1 + mul_div(clipBox.Right - x1, y2 - y1, x2 - x1);
						f3 = ClipLiangBarsky.clipping_flags_y(y3, clipBox);
						f4 = ClipLiangBarsky.clipping_flags_y(y4, clipBox);
						line_clip_y(ras, clipBox.Left, y1, clipBox.Left, y3, f1, f3);
						line_clip_y(ras, clipBox.Left, y3, clipBox.Right, y4, f3, f4);
						line_clip_y(ras, clipBox.Right, y4, clipBox.Right, y2, f4, f2);
						break;

					case 12: // x1 < clip.x1 && x2 < clip.x1
						line_clip_y(ras, clipBox.Left, y1, clipBox.Left, y2, f1, f2);
						break;
				}
				m_f1 = f2;
			}
			else
			{
				ras.line(m_x1, m_y1,
						 x2, y2);
			}
			m_x1 = x2;
			m_y1 = y2;
		}
	}
}