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
//
// The author gratefully acknowledges the support of David Turner,
// Robert Wilhelm, and Werner Lemberg - the authors of the FreeType
// library - in producing this work. See http://www.freetype.org for details.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// Adaptation for 32-bit screen coordinates has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using filling_rule_e = MatterHackers.Agg.agg_basics.filling_rule_e;
using poly_subpixel_scale_e = MatterHackers.Agg.agg_basics.poly_subpixel_scale_e;

namespace MatterHackers.Agg
{
	//==================================================rasterizer_scanline_aa
	// Polygon rasterizer that is used to render filled polygons with
	// high-quality Anti-Aliasing. Internally, by default, the class uses
	// integer coordinates in format 24.8, i.e. 24 bits for integer part
	// and 8 bits for fractional - see poly_subpixel_shift. This class can be
	// used in the following  way:
	//
	// 1. filling_rule(filling_rule_e ft) - optional.
	//
	// 2. gamma() - optional.
	//
	// 3. reset()
	//
	// 4. move_to(x, y) / line_to(x, y) - make the polygon. One can create
	//    more than one contour, but each contour must consist of at least 3
	//    vertices, i.e. move_to(x1, y1); line_to(x2, y2); line_to(x3, y3);
	//    is the absolute minimum of vertices that define a triangle.
	//    The algorithm does not check either the number of vertices nor
	//    coincidence of their coordinates, but in the worst case it just
	//    won't draw anything.
	//    The order of the vertices (clockwise or counterclockwise)
	//    is important when using the non-zero filling rule (fill_non_zero).
	//    In this case the vertex order of all the contours must be the same
	//    if you want your intersecting polygons to be without "holes".
	//    You actually can use different vertices order. If the contours do not
	//    intersect each other the order is not important anyway. If they do,
	//    contours with the same vertex order will be rendered without "holes"
	//    while the intersecting contours with different orders will have "holes".
	//
	// filling_rule() and gamma() can be called anytime before "sweeping".
	//------------------------------------------------------------------------
	public interface IRasterizer
	{
		int min_x();

		int min_y();

		int max_x();

		int max_y();

		void gamma(IGammaFunction gamma_function);

		bool sweep_scanline(IScanlineCache sl);

		void reset();

		void add_path(IVertexSource vs);

		void add_path(IVertexSource vs, int pathID);

		bool rewind_scanlines();
	}

	public sealed class ScanlineRasterizer : IRasterizer
	{
		private rasterizer_cells_aa m_outline;
		private VectorClipper m_VectorClipper;
		private int[] m_gamma = new int[(int)aa_scale_e.aa_scale];
		private agg_basics.filling_rule_e m_filling_rule;
		private bool m_auto_close;
		private int m_start_x;
		private int m_start_y;
		private status m_status;
		private int m_scan_y;

		public enum status
		{
			status_initial,
			status_move_to,
			status_line_to,
			status_closed
		};

		public enum aa_scale_e
		{
			aa_shift = 8,
			aa_scale = 1 << aa_shift,
			aa_mask = aa_scale - 1,
			aa_scale2 = aa_scale * 2,
			aa_mask2 = aa_scale2 - 1
		};

		public ScanlineRasterizer()
			: this(new VectorClipper())
		{
		}

		//--------------------------------------------------------------------
		public ScanlineRasterizer(VectorClipper rasterizer_sl_clip)
		{
			m_outline = new rasterizer_cells_aa();
			m_VectorClipper = rasterizer_sl_clip;
			m_filling_rule = filling_rule_e.fill_non_zero;
			m_auto_close = true;
			m_start_x = 0;
			m_start_y = 0;
			m_status = status.status_initial;

			for (int i = 0; i < (int)aa_scale_e.aa_scale; i++)
			{
				m_gamma[i] = i;
			}
		}

		/*
		//--------------------------------------------------------------------
		public rasterizer_scanline_aa(IClipper rasterizer_sl_clip, IGammaFunction gamma_function)
		{
			m_outline = new rasterizer_cells_aa();
			m_clipper = rasterizer_sl_clip;
			m_filling_rule = filling_rule_e.fill_non_zero;
			m_auto_close = true;
			m_start_x = 0;
			m_start_y = 0;
			m_status = status.status_initial;

			gamma(gamma_function);
		}*/

		//--------------------------------------------------------------------
		public void reset()
		{
			m_outline.reset();
			m_status = status.status_initial;
		}

		public void reset_clipping()
		{
			reset();
			m_VectorClipper.reset_clipping();
		}

		public RectangleDouble GetVectorClipBox()
		{
			return new RectangleDouble(
				m_VectorClipper.downscale(m_VectorClipper.clipBox.Left),
				m_VectorClipper.downscale(m_VectorClipper.clipBox.Bottom),
				m_VectorClipper.downscale(m_VectorClipper.clipBox.Right),
				m_VectorClipper.downscale(m_VectorClipper.clipBox.Top));
		}

		public void SetVectorClipBox(RectangleDouble clippingRect)
		{
			SetVectorClipBox(clippingRect.Left, clippingRect.Bottom, clippingRect.Right, clippingRect.Top);
		}

		public void SetVectorClipBox(double x1, double y1, double x2, double y2)
		{
			reset();
			m_VectorClipper.clip_box(m_VectorClipper.upscale(x1), m_VectorClipper.upscale(y1),
							   m_VectorClipper.upscale(x2), m_VectorClipper.upscale(y2));
		}

		public void filling_rule(agg_basics.filling_rule_e filling_rule)
		{
			m_filling_rule = filling_rule;
		}

		public void auto_close(bool flag)
		{
			m_auto_close = flag;
		}

		//--------------------------------------------------------------------
		public void gamma(IGammaFunction gamma_function)
		{
			for (int i = 0; i < (int)aa_scale_e.aa_scale; i++)
			{
				m_gamma[i] = (int)agg_basics.uround(gamma_function.GetGamma((double)(i) / (int)aa_scale_e.aa_mask) * (int)aa_scale_e.aa_mask);
			}
		}

		/*
		//--------------------------------------------------------------------
		public int apply_gamma(int cover)
		{
			return (int)m_gamma[cover];
		}
		 */

		//--------------------------------------------------------------------
		private void move_to(int x, int y)
		{
			if (m_outline.sorted()) reset();
			if (m_auto_close) close_polygon();
			m_VectorClipper.move_to(m_start_x = m_VectorClipper.downscale(x),
							  m_start_y = m_VectorClipper.downscale(y));
			m_status = status.status_move_to;
		}

		//------------------------------------------------------------------------
		private void line_to(int x, int y)
		{
			m_VectorClipper.line_to(m_outline,
							  m_VectorClipper.downscale(x),
							  m_VectorClipper.downscale(y));
			m_status = status.status_line_to;
		}

		//------------------------------------------------------------------------
		public void move_to_d(double x, double y)
		{
			if (m_outline.sorted()) reset();
			if (m_auto_close) close_polygon();
			m_VectorClipper.move_to(m_start_x = m_VectorClipper.upscale(x),
							  m_start_y = m_VectorClipper.upscale(y));
			m_status = status.status_move_to;
		}

		//------------------------------------------------------------------------
		public void line_to_d(double x, double y)
		{
			m_VectorClipper.line_to(m_outline,
							  m_VectorClipper.upscale(x),
							  m_VectorClipper.upscale(y));
			m_status = status.status_line_to;
		}

		public void close_polygon()
		{
			if (m_status == status.status_line_to)
			{
				m_VectorClipper.line_to(m_outline, m_start_x, m_start_y);
				m_status = status.status_closed;
			}
		}

		private void AddVertex(VertexData vertexData)
		{
			if (ShapePath.is_move_to(vertexData.command))
			{
				move_to_d(vertexData.position.x, vertexData.position.y);
			}
			else
			{
				if (ShapePath.is_vertex(vertexData.command))
				{
					line_to_d(vertexData.position.x, vertexData.position.y);
				}
				else
				{
					if (ShapePath.is_close(vertexData.command))
					{
						close_polygon();
					}
				}
			}
		}

		//------------------------------------------------------------------------
		private void edge(int x1, int y1, int x2, int y2)
		{
			if (m_outline.sorted()) reset();
			m_VectorClipper.move_to(m_VectorClipper.downscale(x1), m_VectorClipper.downscale(y1));
			m_VectorClipper.line_to(m_outline,
							  m_VectorClipper.downscale(x2),
							  m_VectorClipper.downscale(y2));
			m_status = status.status_move_to;
		}

		//------------------------------------------------------------------------
		private void edge_d(double x1, double y1, double x2, double y2)
		{
			if (m_outline.sorted()) reset();
			m_VectorClipper.move_to(m_VectorClipper.upscale(x1), m_VectorClipper.upscale(y1));
			m_VectorClipper.line_to(m_outline,
							  m_VectorClipper.upscale(x2),
							  m_VectorClipper.upscale(y2));
			m_status = status.status_move_to;
		}

		//-------------------------------------------------------------------
		public void add_path(IVertexSource vs)
		{
			add_path(vs, 0);
		}

		public void add_path(IVertexSource vs, int pathID)
		{
#if false
            if (m_outline.sorted())
            {
                reset();
            }

            foreach (VertexData vertexData in vs.Vertices())
            {
                if(!ShapePath.is_stop(vertexData.command))
                {
                    AddVertex(new VertexData(vertexData.command, new Vector2(vertexData.position.x, vertexData.position.y)));
                }
            }
#else
			double x = 0;
			double y = 0;

			ShapePath.FlagsAndCommand PathAndFlags;
			vs.rewind(pathID);
			if (m_outline.sorted())
			{
				reset();
			}

			while (!ShapePath.is_stop(PathAndFlags = vs.vertex(out x, out y)))
			{
				AddVertex(new VertexData(PathAndFlags, new Vector2(x, y)));
			}
#endif
		}

		//--------------------------------------------------------------------
		public int min_x()
		{
			return m_outline.min_x();
		}

		public int min_y()
		{
			return m_outline.min_y();
		}

		public int max_x()
		{
			return m_outline.max_x();
		}

		public int max_y()
		{
			return m_outline.max_y();
		}

		//--------------------------------------------------------------------
		private void sort()
		{
			if (m_auto_close) close_polygon();
			m_outline.sort_cells();
		}

		//------------------------------------------------------------------------
		public bool rewind_scanlines()
		{
			if (m_auto_close) close_polygon();
			m_outline.sort_cells();
			if (m_outline.total_cells() == 0)
			{
				return false;
			}
			m_scan_y = m_outline.min_y();
			return true;
		}

		//------------------------------------------------------------------------
		private bool navigate_scanline(int y)
		{
			if (m_auto_close) close_polygon();
			m_outline.sort_cells();
			if (m_outline.total_cells() == 0 ||
			   y < m_outline.min_y() ||
			   y > m_outline.max_y())
			{
				return false;
			}
			m_scan_y = y;
			return true;
		}

		//--------------------------------------------------------------------
		public int calculate_alpha(int area)
		{
			int cover = area >> ((int)poly_subpixel_scale_e.poly_subpixel_shift * 2 + 1 - (int)aa_scale_e.aa_shift);

			if (cover < 0)
			{
				cover = -cover;
			}

			if (m_filling_rule == filling_rule_e.fill_even_odd)
			{
				cover &= (int)aa_scale_e.aa_mask2;
				if (cover > (int)aa_scale_e.aa_scale)
				{
					cover = (int)aa_scale_e.aa_scale2 - cover;
				}
			}

			if (cover > (int)aa_scale_e.aa_mask)
			{
				cover = (int)aa_scale_e.aa_mask;
			}

			return (int)m_gamma[cover];
		}

		//--------------------------------------------------------------------
		public bool sweep_scanline(IScanlineCache scanlineCache)
		{
			for (; ; )
			{
				if (m_scan_y > m_outline.max_y())
				{
					return false;
				}

				scanlineCache.ResetSpans();
				int num_cells = (int)m_outline.scanline_num_cells(m_scan_y);
				cell_aa[] cells;
				int Offset;
				m_outline.scanline_cells(m_scan_y, out cells, out Offset);
				int cover = 0;

				while (num_cells != 0)
				{
					cell_aa cur_cell = cells[Offset];
					int x = cur_cell.x;
					int area = cur_cell.area;
					int alpha;

					cover += cur_cell.cover;

					//accumulate all cells with the same X
					while (--num_cells != 0)
					{
						Offset++;
						cur_cell = cells[Offset];
						if (cur_cell.x != x)
						{
							break;
						}

						area += cur_cell.area;
						cover += cur_cell.cover;
					}

					if (area != 0)
					{
						alpha = calculate_alpha((cover << ((int)poly_subpixel_scale_e.poly_subpixel_shift + 1)) - area);
						if (alpha != 0)
						{
							scanlineCache.add_cell(x, alpha);
						}
						x++;
					}

					if ((num_cells != 0) && (cur_cell.x > x))
					{
						alpha = calculate_alpha(cover << ((int)poly_subpixel_scale_e.poly_subpixel_shift + 1));
						if (alpha != 0)
						{
							scanlineCache.add_span(x, (cur_cell.x - x), alpha);
						}
					}
				}

				if (scanlineCache.num_spans() != 0) break;
				++m_scan_y;
			}

			scanlineCache.finalize(m_scan_y);
			++m_scan_y;
			return true;
		}

		//--------------------------------------------------------------------
		private bool hit_test(int tx, int ty)
		{
			if (!navigate_scanline(ty)) return false;
			//scanline_hit_test sl(tx);
			//sweep_scanline(sl);
			//return sl.hit();
			return true;
		}
	};
}