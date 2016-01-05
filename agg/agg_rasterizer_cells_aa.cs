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

using poly_subpixel_scale_e = MatterHackers.Agg.agg_basics.poly_subpixel_scale_e;

namespace MatterHackers.Agg
{
	//-----------------------------------------------------------------cell_aa
	// A pixel cell. There are no constructors defined and it was done
	// intentionally in order to avoid extra overhead when allocating an
	// array of cells.
	public struct cell_aa
	{
		public int x;
		public int y;
		public int cover;
		public int area;
		public int left, right;

		public void initial()
		{
			x = 0x7FFFFFFF;
			y = 0x7FFFFFFF;
			cover = 0;
			area = 0;
			left = -1;
			right = -1;
		}

		public void Set(cell_aa cellB)
		{
			x = cellB.x;
			y = cellB.y;
			cover = cellB.cover;
			area = cellB.area;
			left = cellB.left;
			right = cellB.right;
		}

		public void style(cell_aa cellB)
		{
			left = cellB.left;
			right = cellB.right;
		}

		public bool not_equal(int ex, int ey, cell_aa cell)
		{
			unchecked
			{
				return ((ex - x) | (ey - y) | (left - cell.left) | (right - cell.right)) != 0;
			}
		}
	};

	//-----------------------------------------------------rasterizer_cells_aa
	// An internal class that implements the main rasterization algorithm.
	// Used in the rasterizer. Should not be used directly.
	public sealed class rasterizer_cells_aa
	{
		private int m_num_used_cells;
		private VectorPOD<cell_aa> m_cells;
		private VectorPOD<cell_aa> m_sorted_cells;
		private VectorPOD<sorted_y> m_sorted_y;
		private QuickSort_cell_aa m_QSorter;

		private cell_aa m_curr_cell;
		private cell_aa m_style_cell;
		private int m_min_x;
		private int m_min_y;
		private int m_max_x;
		private int m_max_y;
		private bool m_sorted;

		private enum cell_block_scale_e
		{
			cell_block_shift = 12,
			cell_block_size = 1 << cell_block_shift,
			cell_block_mask = cell_block_size - 1,
			cell_block_pool = 256,
			cell_block_limit = 1024 * cell_block_size
		};

		private struct sorted_y
		{
			internal int start;
			internal int num;
		};

		public rasterizer_cells_aa()
		{
			m_QSorter = new QuickSort_cell_aa();
			m_sorted_cells = new VectorPOD<cell_aa>();
			m_sorted_y = new VectorPOD<sorted_y>();
			m_min_x = (0x7FFFFFFF);
			m_min_y = (0x7FFFFFFF);
			m_max_x = (-0x7FFFFFFF);
			m_max_y = (-0x7FFFFFFF);
			m_sorted = (false);

			m_style_cell.initial();
			m_curr_cell.initial();
		}

		public void reset()
		{
			m_num_used_cells = 0;

			m_curr_cell.initial();
			m_style_cell.initial();
			m_sorted = false;
			m_min_x = 0x7FFFFFFF;
			m_min_y = 0x7FFFFFFF;
			m_max_x = -0x7FFFFFFF;
			m_max_y = -0x7FFFFFFF;
		}

		public void style(cell_aa style_cell)
		{
			m_style_cell.style(style_cell);
		}

		private enum dx_limit_e { dx_limit = 16384 << agg_basics.poly_subpixel_scale_e.poly_subpixel_shift };

		public void line(int x1, int y1, int x2, int y2)
		{
			int poly_subpixel_shift = (int)agg_basics.poly_subpixel_scale_e.poly_subpixel_shift;
			int poly_subpixel_mask = (int)agg_basics.poly_subpixel_scale_e.poly_subpixel_mask;
			int poly_subpixel_scale = (int)agg_basics.poly_subpixel_scale_e.poly_subpixel_scale;
			int dx = x2 - x1;

			if (dx >= (int)dx_limit_e.dx_limit || dx <= -(int)dx_limit_e.dx_limit)
			{
				int cx = (x1 + x2) >> 1;
				int cy = (y1 + y2) >> 1;
				line(x1, y1, cx, cy);
				line(cx, cy, x2, y2);
			}

			int dy = y2 - y1;
			int ex1 = x1 >> poly_subpixel_shift;
			int ex2 = x2 >> poly_subpixel_shift;
			int ey1 = y1 >> poly_subpixel_shift;
			int ey2 = y2 >> poly_subpixel_shift;
			int fy1 = y1 & poly_subpixel_mask;
			int fy2 = y2 & poly_subpixel_mask;

			int x_from, x_to;
			int p, rem, mod, lift, delta, first, incr;

			if (ex1 < m_min_x) m_min_x = ex1;
			if (ex1 > m_max_x) m_max_x = ex1;
			if (ey1 < m_min_y) m_min_y = ey1;
			if (ey1 > m_max_y) m_max_y = ey1;
			if (ex2 < m_min_x) m_min_x = ex2;
			if (ex2 > m_max_x) m_max_x = ex2;
			if (ey2 < m_min_y) m_min_y = ey2;
			if (ey2 > m_max_y) m_max_y = ey2;

			set_curr_cell(ex1, ey1);

			//everything is on a single horizontal line
			if (ey1 == ey2)
			{
				render_hline(ey1, x1, fy1, x2, fy2);
				return;
			}

			//Vertical line - we have to calculate start and end cells,
			//and then - the common values of the area and coverage for
			//all cells of the line. We know exactly there's only one
			//cell, so, we don't have to call render_hline().
			incr = 1;
			if (dx == 0)
			{
				int ex = x1 >> poly_subpixel_shift;
				int two_fx = (x1 - (ex << poly_subpixel_shift)) << 1;
				int area;

				first = poly_subpixel_scale;
				if (dy < 0)
				{
					first = 0;
					incr = -1;
				}

				x_from = x1;

				delta = first - fy1;
				m_curr_cell.cover += delta;
				m_curr_cell.area += two_fx * delta;

				ey1 += incr;
				set_curr_cell(ex, ey1);

				delta = first + first - poly_subpixel_scale;
				area = two_fx * delta;
				while (ey1 != ey2)
				{
					m_curr_cell.cover = delta;
					m_curr_cell.area = area;
					ey1 += incr;
					set_curr_cell(ex, ey1);
				}
				delta = fy2 - poly_subpixel_scale + first;
				m_curr_cell.cover += delta;
				m_curr_cell.area += two_fx * delta;
				return;
			}

			//ok, we have to render several hlines
			p = (poly_subpixel_scale - fy1) * dx;
			first = poly_subpixel_scale;

			if (dy < 0)
			{
				p = fy1 * dx;
				first = 0;
				incr = -1;
				dy = -dy;
			}

			delta = p / dy;
			mod = p % dy;

			if (mod < 0)
			{
				delta--;
				mod += dy;
			}

			x_from = x1 + delta;
			render_hline(ey1, x1, fy1, x_from, first);

			ey1 += incr;
			set_curr_cell(x_from >> poly_subpixel_shift, ey1);

			if (ey1 != ey2)
			{
				p = poly_subpixel_scale * dx;
				lift = p / dy;
				rem = p % dy;

				if (rem < 0)
				{
					lift--;
					rem += dy;
				}
				mod -= dy;

				while (ey1 != ey2)
				{
					delta = lift;
					mod += rem;
					if (mod >= 0)
					{
						mod -= dy;
						delta++;
					}

					x_to = x_from + delta;
					render_hline(ey1, x_from, poly_subpixel_scale - first, x_to, first);
					x_from = x_to;

					ey1 += incr;
					set_curr_cell(x_from >> poly_subpixel_shift, ey1);
				}
			}
			render_hline(ey1, x_from, poly_subpixel_scale - first, x2, fy2);
		}

		public int min_x()
		{
			return m_min_x;
		}

		public int min_y()
		{
			return m_min_y;
		}

		public int max_x()
		{
			return m_max_x;
		}

		public int max_y()
		{
			return m_max_y;
		}

		public void sort_cells()
		{
			if (m_sorted) return; //Perform sort only the first time.

			add_curr_cell();
			m_curr_cell.x = 0x7FFFFFFF;
			m_curr_cell.y = 0x7FFFFFFF;
			m_curr_cell.cover = 0;
			m_curr_cell.area = 0;

			if (m_num_used_cells == 0) return;

			// Allocate the array of cell pointers
			m_sorted_cells.Allocate(m_num_used_cells);

			// Allocate and zero the Y array
			m_sorted_y.Allocate((int)(m_max_y - m_min_y + 1));
			m_sorted_y.zero();
			cell_aa[] cells = m_cells.Array;
			sorted_y[] sortedYData = m_sorted_y.Array;
			cell_aa[] sortedCellsData = m_sorted_cells.Array;

			// Create the Y-histogram (count the numbers of cells for each Y)
			for (int i = 0; i < m_num_used_cells; i++)
			{
				int Index = cells[i].y - m_min_y;
				sortedYData[Index].start++;
			}

			// Convert the Y-histogram into the array of starting indexes
			int start = 0;
			int SortedYSize = m_sorted_y.size();
			for (int i = 0; i < SortedYSize; i++)
			{
				int v = sortedYData[i].start;
				sortedYData[i].start = start;
				start += v;
			}

			// Fill the cell pointer array sorted by Y
			for (int i = 0; i < m_num_used_cells; i++)
			{
				int SortedIndex = cells[i].y - m_min_y;
				int curr_y_start = sortedYData[SortedIndex].start;
				int curr_y_num = sortedYData[SortedIndex].num;
				sortedCellsData[curr_y_start + curr_y_num] = cells[i];
				++sortedYData[SortedIndex].num;
			}

			// Finally arrange the X-arrays
			for (int i = 0; i < SortedYSize; i++)
			{
				if (sortedYData[i].num != 0)
				{
					m_QSorter.Sort(sortedCellsData, sortedYData[i].start, sortedYData[i].start + sortedYData[i].num - 1);
				}
			}
			m_sorted = true;
		}

		public int total_cells()
		{
			return m_num_used_cells;
		}

		public int scanline_num_cells(int y)
		{
			return (int)m_sorted_y.data()[y - m_min_y].num;
		}

		public void scanline_cells(int y, out cell_aa[] CellData, out int Offset)
		{
			CellData = m_sorted_cells.data();
			Offset = m_sorted_y[y - m_min_y].start;
		}

		public bool sorted()
		{
			return m_sorted;
		}

		private void set_curr_cell(int x, int y)
		{
			if (m_curr_cell.not_equal(x, y, m_style_cell))
			{
				add_curr_cell();
				m_curr_cell.style(m_style_cell);
				m_curr_cell.x = x;
				m_curr_cell.y = y;
				m_curr_cell.cover = 0;
				m_curr_cell.area = 0;
			}
		}

		private void add_curr_cell()
		{
			if ((m_curr_cell.area | m_curr_cell.cover) != 0)
			{
				if (m_num_used_cells >= (int)cell_block_scale_e.cell_block_limit)
				{
					return;
				}

				allocate_cells_if_required();
				m_cells.data()[m_num_used_cells].Set(m_curr_cell);
				m_num_used_cells++;

#if false
                if(m_num_used_cells == 281)
                {
                    int a = 12;
                }

                DebugFile.Print(m_num_used_cells.ToString()
                    + ". x=" + m_curr_cell.m_x.ToString()
                    + " y=" + m_curr_cell.m_y.ToString()
                    + " area=" + m_curr_cell.m_area.ToString()
                    + " cover=" + m_curr_cell.m_cover.ToString()
                    + "\n");
#endif
			}
		}

		private void allocate_cells_if_required()
		{
			if (m_cells == null || (m_num_used_cells + 1) >= m_cells.Capacity())
			{
				if (m_num_used_cells >= (int)cell_block_scale_e.cell_block_limit)
				{
					return;
				}

				int new_num_allocated_cells = m_num_used_cells + (int)cell_block_scale_e.cell_block_size;
				VectorPOD<cell_aa> new_cells = new VectorPOD<cell_aa>(new_num_allocated_cells);
				if (m_cells != null)
				{
					new_cells.CopyFrom(m_cells);
				}
				m_cells = new_cells;
			}
		}

		private void render_hline(int ey, int x1, int y1, int x2, int y2)
		{
			int ex1 = x1 >> (int)poly_subpixel_scale_e.poly_subpixel_shift;
			int ex2 = x2 >> (int)poly_subpixel_scale_e.poly_subpixel_shift;
			int fx1 = x1 & (int)poly_subpixel_scale_e.poly_subpixel_mask;
			int fx2 = x2 & (int)poly_subpixel_scale_e.poly_subpixel_mask;

			int delta, p, first, dx;
			int incr, lift, mod, rem;

			//trivial case. Happens often
			if (y1 == y2)
			{
				set_curr_cell(ex2, ey);
				return;
			}

			//everything is located in a single cell.  That is easy!
			if (ex1 == ex2)
			{
				delta = y2 - y1;
				m_curr_cell.cover += delta;
				m_curr_cell.area += (fx1 + fx2) * delta;
				return;
			}

			//ok, we'll have to render a run of adjacent cells on the same hline...
			p = ((int)poly_subpixel_scale_e.poly_subpixel_scale - fx1) * (y2 - y1);
			first = (int)poly_subpixel_scale_e.poly_subpixel_scale;
			incr = 1;

			dx = x2 - x1;

			if (dx < 0)
			{
				p = fx1 * (y2 - y1);
				first = 0;
				incr = -1;
				dx = -dx;
			}

			delta = p / dx;
			mod = p % dx;

			if (mod < 0)
			{
				delta--;
				mod += dx;
			}

			m_curr_cell.cover += delta;
			m_curr_cell.area += (fx1 + first) * delta;

			ex1 += incr;
			set_curr_cell(ex1, ey);
			y1 += delta;

			if (ex1 != ex2)
			{
				p = (int)poly_subpixel_scale_e.poly_subpixel_scale * (y2 - y1 + delta);
				lift = p / dx;
				rem = p % dx;

				if (rem < 0)
				{
					lift--;
					rem += dx;
				}

				mod -= dx;

				while (ex1 != ex2)
				{
					delta = lift;
					mod += rem;
					if (mod >= 0)
					{
						mod -= dx;
						delta++;
					}

					m_curr_cell.cover += delta;
					m_curr_cell.area += (int)poly_subpixel_scale_e.poly_subpixel_scale * delta;
					y1 += delta;
					ex1 += incr;
					set_curr_cell(ex1, ey);
				}
			}
			delta = y2 - y1;
			m_curr_cell.cover += delta;
			m_curr_cell.area += (fx2 + (int)poly_subpixel_scale_e.poly_subpixel_scale - first) * delta;
		}

		private static void swap_cells(cell_aa a, cell_aa b)
		{
			cell_aa temp = a;
			a = b;
			b = temp;
		}

		private enum qsort { qsort_threshold = 9 };
	}

	//------------------------------------------------------scanline_hit_test
	public class scanline_hit_test : IScanlineCache
	{
		private int m_x;
		private bool m_hit;

		public scanline_hit_test(int x)
		{
			m_x = x;
			m_hit = false;
		}

		public void ResetSpans()
		{
		}

		public void finalize(int nothing)
		{
		}

		public void add_cell(int x, int nothing)
		{
			if (m_x == x) m_hit = true;
		}

		public void add_span(int x, int len, int nothing)
		{
			if (m_x >= x && m_x < x + len) m_hit = true;
		}

		public int num_spans()
		{
			return 1;
		}

		public bool hit()
		{
			return m_hit;
		}

		public void reset(int min_x, int max_x)
		{
			throw new System.NotImplementedException();
		}

		public ScanlineSpan begin()
		{
			throw new System.NotImplementedException();
		}

		public ScanlineSpan GetNextScanlineSpan()
		{
			throw new System.NotImplementedException();
		}

		public int y()
		{
			throw new System.NotImplementedException();
		}

		public byte[] GetCovers()
		{
			throw new System.NotImplementedException();
		}
	}
}