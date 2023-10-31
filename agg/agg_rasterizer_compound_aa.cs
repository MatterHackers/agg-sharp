//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.3
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

namespace MatterHackers.Agg
{
	//===========================================================layer_order_e
	public enum LayerOrder
	{
		Unsorted, //------layer_unsorted
		Direct,   //------layer_direct
		Inverse   //------layer_inverse
	};

	//==================================================rasterizer_compound_aa
	//template<class Clip=rasterizer_sl_clip_int>
	sealed public class rasterizer_compound_aa : IRasterizer
	{
		private RasterizerCellsAa rasterizerCellsAa;
		private VectorClipper vectorClipper;
		private Util.filling_rule_e fillingRule;
		private LayerOrder layerOrder;
		private VectorPOD<style_info> activeStyles;  // Active Styles
		private VectorPOD<int> activeStyleTable;     // Active Style Table (unique values)
		private VectorPOD<byte> activeStyleMask;     // Active Style Mask
		private VectorPOD<PixelCellAa> m_cells;
		private VectorPOD<byte> m_cover_buf;
		private VectorPOD<int> m_master_alpha;

		private int m_min_style;
		private int m_max_style;
		private int m_start_x;
		private int m_start_y;
		private int m_scan_y;
		private int m_sl_start;
		private int m_sl_len;

		private struct style_info
		{
			internal int start_cell;
			internal int num_cells;
			internal int last_x;
		};

		private const int aa_shift = 8;
		private const int aa_scale = 1 << aa_shift;
		private const int aa_mask = aa_scale - 1;
		private const int aa_scale2 = aa_scale * 2;
		private const int aa_mask2 = aa_scale2 - 1;

		private const int poly_subpixel_shift = (int)Util.poly_subpixel_scale_e.poly_subpixel_shift;

		public rasterizer_compound_aa()
		{
			rasterizerCellsAa = new RasterizerCellsAa();
			vectorClipper = new VectorClipper();
			fillingRule = Util.filling_rule_e.fill_non_zero;
			layerOrder = LayerOrder.Direct;
			activeStyles = new VectorPOD<style_info>();  // Active Styles
			activeStyleTable = new VectorPOD<int>();     // Active Style Table (unique values)
			activeStyleMask = new VectorPOD<byte>();     // Active Style Mask
			m_cells = new VectorPOD<PixelCellAa>();
			m_cover_buf = new VectorPOD<byte>();
			m_master_alpha = new VectorPOD<int>();
			m_min_style = (0x7FFFFFFF);
			m_max_style = (-0x7FFFFFFF);
			m_start_x = (0);
			m_start_y = (0);
			m_scan_y = (0x7FFFFFFF);
			m_sl_start = (0);
			m_sl_len = (0);
		}

		public void gamma(IGammaFunction gamma_function)
		{
			throw new System.NotImplementedException();
		}

		public void reset()
		{
			rasterizerCellsAa.reset();
			m_min_style = 0x7FFFFFFF;
			m_max_style = -0x7FFFFFFF;
			m_scan_y = 0x7FFFFFFF;
			m_sl_start = 0;
			m_sl_len = 0;
		}

		private void filling_rule(Util.filling_rule_e filling_rule)
		{
			fillingRule = filling_rule;
		}

		private void layer_order(LayerOrder order)
		{
			layerOrder = order;
		}

		private void clip_box(double x1, double y1,
													double x2, double y2)
		{
			reset();
			vectorClipper.clip_box(vectorClipper.upscale(x1), vectorClipper.upscale(y1),
							   vectorClipper.upscale(x2), vectorClipper.upscale(y2));
		}

		private void reset_clipping()
		{
			reset();
			vectorClipper.reset_clipping();
		}

		public void styles(int left, int right)
		{
			PixelCellAa cell = new PixelCellAa();
			cell.Initial();
			cell.left = (int)left;
			cell.right = (int)right;
			rasterizerCellsAa.style(cell);
			if (left >= 0 && left < m_min_style) m_min_style = left;
			if (left >= 0 && left > m_max_style) m_max_style = left;
			if (right >= 0 && right < m_min_style) m_min_style = right;
			if (right >= 0 && right > m_max_style) m_max_style = right;
		}

		public void move_to(int x, int y)
		{
			if (rasterizerCellsAa.sorted()) reset();
			vectorClipper.move_to(m_start_x = vectorClipper.downscale(x),
							  m_start_y = vectorClipper.downscale(y));
		}

		public void line_to(int x, int y)
		{
			vectorClipper.line_to(rasterizerCellsAa,
							  vectorClipper.downscale(x),
							  vectorClipper.downscale(y));
		}

		public void move_to_d(double x, double y)
		{
			if (rasterizerCellsAa.sorted()) reset();
			vectorClipper.move_to(m_start_x = vectorClipper.upscale(x),
							  m_start_y = vectorClipper.upscale(y));
		}

		public void line_to_d(double x, double y)
		{
			vectorClipper.line_to(rasterizerCellsAa,
							  vectorClipper.upscale(x),
							  vectorClipper.upscale(y));
		}

		private void add_vertex(double x, double y, FlagsAndCommand cmd)
		{
			if (ShapePath.IsMoveTo(cmd))
			{
				move_to_d(x, y);
			}
			else
				if (ShapePath.IsVertex(cmd))
				{
					line_to_d(x, y);
				}
				else
					if (ShapePath.IsClose(cmd))
					{
						vectorClipper.line_to(rasterizerCellsAa, m_start_x, m_start_y);
					}
		}

		private void edge(int x1, int y1, int x2, int y2)
		{
			if (rasterizerCellsAa.sorted()) reset();
			vectorClipper.move_to(vectorClipper.downscale(x1), vectorClipper.downscale(y1));
			vectorClipper.line_to(rasterizerCellsAa,
							  vectorClipper.downscale(x2),
							  vectorClipper.downscale(y2));
		}

		private void edge_d(double x1, double y1,
												  double x2, double y2)
		{
			if (rasterizerCellsAa.sorted()) reset();
			vectorClipper.move_to(vectorClipper.upscale(x1), vectorClipper.upscale(y1));
			vectorClipper.line_to(rasterizerCellsAa,
							  vectorClipper.upscale(x2),
							  vectorClipper.upscale(y2));
		}

		private void sort()
		{
			rasterizerCellsAa.sort_cells();
		}

		public bool rewind_scanlines()
		{
			rasterizerCellsAa.sort_cells();
			if (rasterizerCellsAa.total_cells() == 0)
			{
				return false;
			}
			if (m_max_style < m_min_style)
			{
				return false;
			}
			m_scan_y = rasterizerCellsAa.min_y();
			activeStyles.Allocate((int)(m_max_style - m_min_style + 2), 128);
			allocate_master_alpha();
			return true;
		}

		// Returns the number of styles
		public int sweep_styles()
		{
			for (; ; )
			{
				if (m_scan_y > rasterizerCellsAa.max_y()) return 0;
				int num_cells = (int)rasterizerCellsAa.scanline_num_cells(m_scan_y);
				PixelCellAa[] cells;
				int cellOffset = 0;
				int curCellOffset;
				rasterizerCellsAa.scanline_cells(m_scan_y, out cells, out cellOffset);
				int num_styles = (int)(m_max_style - m_min_style + 2);
				int style_id;
				int styleOffset = 0;

				m_cells.Allocate((int)num_cells * 2, 256); // Each cell can have two styles
				activeStyleTable.Capacity(num_styles, 64);
				activeStyleMask.Allocate((num_styles + 7) >> 3, 8);
				activeStyleMask.zero();

				if (num_cells > 0)
				{
					// Pre-add zero (for no-fill style, that is, -1).
					// We need that to ensure that the "-1 style" would go first.
					activeStyleMask.Array[0] |= 1;
					activeStyleTable.Add(0);
					activeStyles.Array[styleOffset].start_cell = 0;
					activeStyles.Array[styleOffset].num_cells = 0;
					activeStyles.Array[styleOffset].last_x = -0x7FFFFFFF;

					m_sl_start = cells[0].x;
					m_sl_len = (int)(cells[num_cells - 1].x - m_sl_start + 1);
					while (num_cells-- != 0)
					{
						curCellOffset = (int)cellOffset++;
						add_style(cells[curCellOffset].left);
						add_style(cells[curCellOffset].right);
					}

					// Convert the Y-histogram into the array of starting indexes
					int i;
					int start_cell = 0;
					style_info[] stylesArray = activeStyles.Array;
					for (i = 0; i < activeStyleTable.Count; i++)
					{
						int IndexToModify = (int)activeStyleTable[i];
						int v = stylesArray[IndexToModify].start_cell;
						stylesArray[IndexToModify].start_cell = start_cell;
						start_cell += v;
					}

					num_cells = (int)rasterizerCellsAa.scanline_num_cells(m_scan_y);
					rasterizerCellsAa.scanline_cells(m_scan_y, out cells, out cellOffset);

					while (num_cells-- > 0)
					{
						curCellOffset = (int)cellOffset++;
						style_id = (int)((cells[curCellOffset].left < 0) ? 0 :
									cells[curCellOffset].left - m_min_style + 1);

						styleOffset = (int)style_id;
						if (cells[curCellOffset].x == stylesArray[styleOffset].last_x)
						{
							cellOffset = stylesArray[styleOffset].start_cell + stylesArray[styleOffset].num_cells - 1;
							unchecked
							{
								cells[cellOffset].area += cells[curCellOffset].area;
								cells[cellOffset].cover += cells[curCellOffset].cover;
							}
						}
						else
						{
							cellOffset = stylesArray[styleOffset].start_cell + stylesArray[styleOffset].num_cells;
							cells[cellOffset].x = cells[curCellOffset].x;
							cells[cellOffset].area = cells[curCellOffset].area;
							cells[cellOffset].cover = cells[curCellOffset].cover;
							stylesArray[styleOffset].last_x = cells[curCellOffset].x;
							stylesArray[styleOffset].num_cells++;
						}

						style_id = (int)((cells[curCellOffset].right < 0) ? 0 :
									cells[curCellOffset].right - m_min_style + 1);

						styleOffset = (int)style_id;
						if (cells[curCellOffset].x == stylesArray[styleOffset].last_x)
						{
							cellOffset = stylesArray[styleOffset].start_cell + stylesArray[styleOffset].num_cells - 1;
							unchecked
							{
								cells[cellOffset].area -= cells[curCellOffset].area;
								cells[cellOffset].cover -= cells[curCellOffset].cover;
							}
						}
						else
						{
							cellOffset = stylesArray[styleOffset].start_cell + stylesArray[styleOffset].num_cells;
							cells[cellOffset].x = cells[curCellOffset].x;
							cells[cellOffset].area = -cells[curCellOffset].area;
							cells[cellOffset].cover = -cells[curCellOffset].cover;
							stylesArray[styleOffset].last_x = cells[curCellOffset].x;
							stylesArray[styleOffset].num_cells++;
						}
					}
				}
				if (activeStyleTable.Count > 1) break;
				++m_scan_y;
			}
			++m_scan_y;

			if (layerOrder != LayerOrder.Unsorted)
			{
				VectorPodRangeAdaptor ra = new VectorPodRangeAdaptor(activeStyleTable, 1, activeStyleTable.Count - 1);
				if (layerOrder == LayerOrder.Direct)
				{
					QuickSortRangeAdaptorUint m_QSorter = new QuickSortRangeAdaptorUint();
					m_QSorter.Sort(ra);
					//quick_sort(ra, uint_greater);
				}
				else
				{
					throw new System.NotImplementedException();
					//QuickSort_range_adaptor_uint m_QSorter = new QuickSort_range_adaptor_uint();
					//m_QSorter.Sort(ra);
					//quick_sort(ra, uint_less);
				}
			}

			return activeStyleTable.Count - 1;
		}

		// Returns style ID depending of the existing style index
		public int style(int style_idx)
		{
			return activeStyleTable[style_idx + 1] + (int)m_min_style - 1;
		}

		private bool navigate_scanline(int y)
		{
			rasterizerCellsAa.sort_cells();
			if (rasterizerCellsAa.total_cells() == 0)
			{
				return false;
			}
			if (m_max_style < m_min_style)
			{
				return false;
			}
			if (y < rasterizerCellsAa.min_y() || y > rasterizerCellsAa.max_y())
			{
				return false;
			}
			m_scan_y = y;
			activeStyles.Allocate((int)(m_max_style - m_min_style + 2), 128);
			allocate_master_alpha();
			return true;
		}

		private bool hit_test(int tx, int ty)
		{
			if (!navigate_scanline(ty))
			{
				return false;
			}

			int num_styles = sweep_styles();
			if (num_styles <= 0)
			{
				return false;
			}

			scanline_hit_test sl = new scanline_hit_test(tx);
			sweep_scanline(sl, -1);
			return sl.hit();
		}

		private byte[] allocate_cover_buffer(int len)
		{
			m_cover_buf.Allocate(len, 256);
			return m_cover_buf.Array;
		}

		private void master_alpha(int style, double alpha)
		{
			if (style >= 0)
			{
				while ((int)m_master_alpha.Count <= style)
				{
					m_master_alpha.Add(aa_mask);
				}
				m_master_alpha.Array[style] = Util.uround(alpha * aa_mask);
			}
		}

		public void add_path(IVertexSource vs)
		{
			add_path(vs, 0);
		}

		public void add_path(IVertexSource vs, int path_id)
		{
			double x;
			double y;

			FlagsAndCommand cmd;
			vs.Rewind(path_id);
			if (rasterizerCellsAa.sorted()) reset();
			while (!ShapePath.IsStop(cmd = vs.vertex(out x, out y)))
			{
				add_vertex(x, y, cmd);
			}
		}

		public int min_x()
		{
			return rasterizerCellsAa.min_x();
		}

		public int min_y()
		{
			return rasterizerCellsAa.min_y();
		}

		public int max_x()
		{
			return rasterizerCellsAa.max_x();
		}

		public int max_y()
		{
			return rasterizerCellsAa.max_y();
		}

		public int min_style()
		{
			return m_min_style;
		}

		public int max_style()
		{
			return m_max_style;
		}

		public int scanline_start()
		{
			return m_sl_start;
		}

		public int scanline_length()
		{
			return m_sl_len;
		}

		public int calculate_alpha(int area, int master_alpha)
		{
			int cover = area >> (poly_subpixel_shift * 2 + 1 - aa_shift);
			if (cover < 0) cover = -cover;
			if (fillingRule == Util.filling_rule_e.fill_even_odd)
			{
				cover &= aa_mask2;
				if (cover > aa_scale)
				{
					cover = aa_scale2 - cover;
				}
			}
			if (cover > aa_mask) cover = aa_mask;
			return (int)((cover * master_alpha + aa_mask) >> aa_shift);
		}

		public bool sweep_scanline(IScanlineCache sl)
		{
			throw new System.NotImplementedException();
		}

		// Sweeps one scanline with one style index. The style ID can be
		// determined by calling style().
		//template<class Scanline>
		public bool sweep_scanline(IScanlineCache sl, int style_idx)
		{
			int scan_y = m_scan_y - 1;
			if (scan_y > rasterizerCellsAa.max_y()) return false;

			sl.ResetSpans();

			int master_alpha = aa_mask;

			if (style_idx < 0)
			{
				style_idx = 0;
			}
			else
			{
				style_idx++;
				master_alpha = m_master_alpha[(int)(activeStyleTable[(int)style_idx] + m_min_style - 1)];
			}

			style_info st = activeStyles[activeStyleTable[style_idx]];

			int num_cells = (int)st.num_cells;
			int CellOffset = st.start_cell;
			PixelCellAa cell = m_cells[CellOffset];

			int cover = 0;
			while (num_cells-- != 0)
			{
				int alpha;
				int x = cell.x;
				int area = cell.area;

				cover += cell.cover;

				cell = m_cells[++CellOffset];

				if (area != 0)
				{
					alpha = calculate_alpha((cover << (poly_subpixel_shift + 1)) - area,
											master_alpha);
					sl.add_cell(x, alpha);
					x++;
				}

				if (num_cells != 0 && cell.x > x)
				{
					alpha = calculate_alpha(cover << (poly_subpixel_shift + 1),
											master_alpha);
					if (alpha != 0)
					{
						sl.add_span(x, cell.x - x, alpha);
					}
				}
			}

			if (sl.num_spans() == 0) return false;
			sl.finalize(scan_y);
			return true;
		}

		private void add_style(int style_id)
		{
			if (style_id < 0) style_id = 0;
			else style_id -= m_min_style - 1;

			int nbyte = (int)((int)style_id >> 3);
			int mask = (int)(1 << (style_id & 7));

			style_info[] stylesArray = activeStyles.Array;
			if ((activeStyleMask[nbyte] & mask) == 0)
			{
				activeStyleTable.Add((int)style_id);
				activeStyleMask.Array[nbyte] |= (byte)mask;
				stylesArray[style_id].start_cell = 0;
				stylesArray[style_id].num_cells = 0;
				stylesArray[style_id].last_x = -0x7FFFFFFF;
			}
			++stylesArray[style_id].start_cell;
		}

		private void allocate_master_alpha()
		{
			while ((int)m_master_alpha.Count <= m_max_style)
			{
				m_master_alpha.Add(aa_mask);
			}
		}
	};
}