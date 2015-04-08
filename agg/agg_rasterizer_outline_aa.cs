using MatterHackers.Agg.VertexSource;

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
	//-----------------------------------------------------------line_aa_vertex
	// Vertex (x, y) with the distance to the next one. The last vertex has
	// the distance between the last and the first points
	public struct line_aa_vertex
	{
		public int x;
		public int y;
		public int len;

		public line_aa_vertex(int x_, int y_)
		{
			x = (x_);
			y = (y_);
			len = (0);
		}

		public bool Compare(line_aa_vertex val)
		{
			double dx = val.x - x;
			double dy = val.y - y;
			return (len = agg_basics.uround(Math.Sqrt(dx * dx + dy * dy))) >
				   (LineAABasics.line_subpixel_scale + LineAABasics.line_subpixel_scale / 2);
		}
	};

	public class line_aa_vertex_sequence : VectorPOD<line_aa_vertex>
	{
		public override void add(line_aa_vertex val)
		{
			if (base.size() > 1)
			{
				if (!Array[base.size() - 2].Compare(Array[base.size() - 1]))
				{
					base.RemoveLast();
				}
			}
			base.add(val);
		}

		public void modify_last(line_aa_vertex val)
		{
			base.RemoveLast();
			add(val);
		}

		public void close(bool closed)
		{
			while (base.size() > 1)
			{
				if (Array[base.size() - 2].Compare(Array[base.size() - 1])) break;
				line_aa_vertex t = this[base.size() - 1];
				base.RemoveLast();
				modify_last(t);
			}

			if (closed)
			{
				while (base.size() > 1)
				{
					if (Array[base.size() - 1].Compare(Array[0])) break;
					base.RemoveLast();
				}
			}
		}

		internal line_aa_vertex prev(int idx)
		{
			return this[(idx + currentSize - 1) % currentSize];
		}

		internal line_aa_vertex curr(int idx)
		{
			return this[idx];
		}

		internal line_aa_vertex next(int idx)
		{
			return this[(idx + 1) % currentSize];
		}
	}

	//=======================================================rasterizer_outline_aa
	public class rasterizer_outline_aa
	{
		private LineRenderer m_ren;
		private line_aa_vertex_sequence m_src_vertices = new line_aa_vertex_sequence();
		private outline_aa_join_e m_line_join;
		private bool m_round_cap;
		private int m_start_x;
		private int m_start_y;

		public enum outline_aa_join_e
		{
			outline_no_join,             //-----outline_no_join
			outline_miter_join,          //-----outline_miter_join
			outline_round_join,          //-----outline_round_join
			outline_miter_accurate_join  //-----outline_accurate_join
		};

		public bool cmp_dist_start(int d)
		{
			return d > 0;
		}

		public bool cmp_dist_end(int d)
		{
			return d <= 0;
		}

		private struct draw_vars
		{
			public int idx;
			public int x1, y1, x2, y2;
			public line_parameters curr, next;
			public int lcurr, lnext;
			public int xb1, yb1, xb2, yb2;
			public int flags;
		};

		private void draw(ref draw_vars dv, int start, int end)
		{
			int i;

			for (i = start; i < end; i++)
			{
				if (m_line_join == outline_aa_join_e.outline_round_join)
				{
					dv.xb1 = dv.curr.x1 + (dv.curr.y2 - dv.curr.y1);
					dv.yb1 = dv.curr.y1 - (dv.curr.x2 - dv.curr.x1);
					dv.xb2 = dv.curr.x2 + (dv.curr.y2 - dv.curr.y1);
					dv.yb2 = dv.curr.y2 - (dv.curr.x2 - dv.curr.x1);
				}

				switch (dv.flags)
				{
					case 0: m_ren.line3(dv.curr, dv.xb1, dv.yb1, dv.xb2, dv.yb2); break;
					case 1: m_ren.line2(dv.curr, dv.xb2, dv.yb2); break;
					case 2: m_ren.line1(dv.curr, dv.xb1, dv.yb1); break;
					case 3: m_ren.line0(dv.curr); break;
				}

				if (m_line_join == outline_aa_join_e.outline_round_join && (dv.flags & 2) == 0)
				{
					m_ren.pie(dv.curr.x2, dv.curr.y2,
							   dv.curr.x2 + (dv.curr.y2 - dv.curr.y1),
							   dv.curr.y2 - (dv.curr.x2 - dv.curr.x1),
							   dv.curr.x2 + (dv.next.y2 - dv.next.y1),
							   dv.curr.y2 - (dv.next.x2 - dv.next.x1));
				}

				dv.x1 = dv.x2;
				dv.y1 = dv.y2;
				dv.lcurr = dv.lnext;
				dv.lnext = m_src_vertices[dv.idx].len;

				++dv.idx;
				if (dv.idx >= m_src_vertices.size()) dv.idx = 0;

				dv.x2 = m_src_vertices[dv.idx].x;
				dv.y2 = m_src_vertices[dv.idx].y;

				dv.curr = dv.next;
				dv.next = new line_parameters(dv.x1, dv.y1, dv.x2, dv.y2, dv.lnext);
				dv.xb1 = dv.xb2;
				dv.yb1 = dv.yb2;

				switch (m_line_join)
				{
					case outline_aa_join_e.outline_no_join:
						dv.flags = 3;
						break;

					case outline_aa_join_e.outline_miter_join:
						dv.flags >>= 1;
						dv.flags |= (dv.curr.diagonal_quadrant() ==
							dv.next.diagonal_quadrant() ? 1 : 0);
						if ((dv.flags & 2) == 0)
						{
							LineAABasics.bisectrix(dv.curr, dv.next, out dv.xb2, out dv.yb2);
						}
						break;

					case outline_aa_join_e.outline_round_join:
						dv.flags >>= 1;
						dv.flags |= (((dv.curr.diagonal_quadrant() ==
							dv.next.diagonal_quadrant()) ? 1 : 0) << 1);
						break;

					case outline_aa_join_e.outline_miter_accurate_join:
						dv.flags = 0;
						LineAABasics.bisectrix(dv.curr, dv.next, out dv.xb2, out dv.yb2);
						break;
				}
			}
		}

		public rasterizer_outline_aa(LineRenderer ren)
		{
			m_ren = ren;
			m_line_join = (OutlineRenderer.accurate_join_only() ?
							outline_aa_join_e.outline_miter_accurate_join :
							outline_aa_join_e.outline_round_join);
			m_round_cap = (false);
			m_start_x = (0);
			m_start_y = (0);
		}

		public void attach(LineRenderer ren)
		{
			m_ren = ren;
		}

		public void line_join(outline_aa_join_e join)
		{
			m_line_join = OutlineRenderer.accurate_join_only() ?
				outline_aa_join_e.outline_miter_accurate_join :
				join;
		}

		public outline_aa_join_e line_join()
		{
			return m_line_join;
		}

		public void round_cap(bool v)
		{
			m_round_cap = v;
		}

		public bool round_cap()
		{
			return m_round_cap;
		}

		public void move_to(int x, int y)
		{
			m_src_vertices.modify_last(new line_aa_vertex(m_start_x = x, m_start_y = y));
		}

		public void line_to(int x, int y)
		{
			m_src_vertices.add(new line_aa_vertex(x, y));
		}

		public void move_to_d(double x, double y)
		{
			move_to(line_coord_sat.conv(x), line_coord_sat.conv(y));
		}

		public void line_to_d(double x, double y)
		{
			line_to(line_coord_sat.conv(x), line_coord_sat.conv(y));
		}

		public void render(bool close_polygon)
		{
			m_src_vertices.close(close_polygon);
			draw_vars dv = new draw_vars();
			line_aa_vertex v;
			int x1;
			int y1;
			int x2;
			int y2;
			int lprev;

			if (close_polygon)
			{
				if (m_src_vertices.size() >= 3)
				{
					dv.idx = 2;

					v = m_src_vertices[m_src_vertices.size() - 1];
					x1 = v.x;
					y1 = v.y;
					lprev = v.len;

					v = m_src_vertices[0];
					x2 = v.x;
					y2 = v.y;
					dv.lcurr = v.len;
					line_parameters prev = new line_parameters(x1, y1, x2, y2, lprev);

					v = m_src_vertices[1];
					dv.x1 = v.x;
					dv.y1 = v.y;
					dv.lnext = v.len;
					dv.curr = new line_parameters(x2, y2, dv.x1, dv.y1, dv.lcurr);

					v = m_src_vertices[dv.idx];
					dv.x2 = v.x;
					dv.y2 = v.y;
					dv.next = new line_parameters(dv.x1, dv.y1, dv.x2, dv.y2, dv.lnext);

					dv.xb1 = 0;
					dv.yb1 = 0;
					dv.xb2 = 0;
					dv.yb2 = 0;

					switch (m_line_join)
					{
						case outline_aa_join_e.outline_no_join:
							dv.flags = 3;
							break;

						case outline_aa_join_e.outline_miter_join:
						case outline_aa_join_e.outline_round_join:
							dv.flags =
								(prev.diagonal_quadrant() == dv.curr.diagonal_quadrant() ? 1 : 0) |
									((dv.curr.diagonal_quadrant() == dv.next.diagonal_quadrant() ? 1 : 0) << 1);
							break;

						case outline_aa_join_e.outline_miter_accurate_join:
							dv.flags = 0;
							break;
					}

					if ((dv.flags & 1) == 0 && m_line_join != outline_aa_join_e.outline_round_join)
					{
						LineAABasics.bisectrix(prev, dv.curr, out dv.xb1, out dv.yb1);
					}

					if ((dv.flags & 2) == 0 && m_line_join != outline_aa_join_e.outline_round_join)
					{
						LineAABasics.bisectrix(dv.curr, dv.next, out dv.xb2, out dv.yb2);
					}
					draw(ref dv, 0, m_src_vertices.size());
				}
			}
			else
			{
				switch (m_src_vertices.size())
				{
					case 0:
					case 1:
						break;

					case 2:
						{
							v = m_src_vertices[0];
							x1 = v.x;
							y1 = v.y;
							lprev = v.len;
							v = m_src_vertices[1];
							x2 = v.x;
							y2 = v.y;
							line_parameters lp = new line_parameters(x1, y1, x2, y2, lprev);
							if (m_round_cap)
							{
								m_ren.semidot(cmp_dist_start, x1, y1, x1 + (y2 - y1), y1 - (x2 - x1));
							}
							m_ren.line3(lp,
										 x1 + (y2 - y1),
										 y1 - (x2 - x1),
										 x2 + (y2 - y1),
										 y2 - (x2 - x1));
							if (m_round_cap)
							{
								m_ren.semidot(cmp_dist_end, x2, y2, x2 + (y2 - y1), y2 - (x2 - x1));
							}
						}
						break;

					case 3:
						{
							int x3, y3;
							int lnext;
							v = m_src_vertices[0];
							x1 = v.x;
							y1 = v.y;
							lprev = v.len;
							v = m_src_vertices[1];
							x2 = v.x;
							y2 = v.y;
							lnext = v.len;
							v = m_src_vertices[2];
							x3 = v.x;
							y3 = v.y;
							line_parameters lp1 = new line_parameters(x1, y1, x2, y2, lprev);
							line_parameters lp2 = new line_parameters(x2, y2, x3, y3, lnext);

							if (m_round_cap)
							{
								m_ren.semidot(cmp_dist_start, x1, y1, x1 + (y2 - y1), y1 - (x2 - x1));
							}

							if (m_line_join == outline_aa_join_e.outline_round_join)
							{
								m_ren.line3(lp1, x1 + (y2 - y1), y1 - (x2 - x1),
												  x2 + (y2 - y1), y2 - (x2 - x1));

								m_ren.pie(x2, y2, x2 + (y2 - y1), y2 - (x2 - x1),
												   x2 + (y3 - y2), y2 - (x3 - x2));

								m_ren.line3(lp2, x2 + (y3 - y2), y2 - (x3 - x2),
												  x3 + (y3 - y2), y3 - (x3 - x2));
							}
							else
							{
								LineAABasics.bisectrix(lp1, lp2, out dv.xb1, out dv.yb1);
								m_ren.line3(lp1, x1 + (y2 - y1), y1 - (x2 - x1),
												  dv.xb1, dv.yb1);

								m_ren.line3(lp2, dv.xb1, dv.yb1,
												  x3 + (y3 - y2), y3 - (x3 - x2));
							}
							if (m_round_cap)
							{
								m_ren.semidot(cmp_dist_end, x3, y3, x3 + (y3 - y2), y3 - (x3 - x2));
							}
						}
						break;

					default:
						{
							dv.idx = 3;

							v = m_src_vertices[0];
							x1 = v.x;
							y1 = v.y;
							lprev = v.len;

							v = m_src_vertices[1];
							x2 = v.x;
							y2 = v.y;
							dv.lcurr = v.len;
							line_parameters prev = new line_parameters(x1, y1, x2, y2, lprev);

							v = m_src_vertices[2];
							dv.x1 = v.x;
							dv.y1 = v.y;
							dv.lnext = v.len;
							dv.curr = new line_parameters(x2, y2, dv.x1, dv.y1, dv.lcurr);

							v = m_src_vertices[dv.idx];
							dv.x2 = v.x;
							dv.y2 = v.y;
							dv.next = new line_parameters(dv.x1, dv.y1, dv.x2, dv.y2, dv.lnext);

							dv.xb1 = 0;
							dv.yb1 = 0;
							dv.xb2 = 0;
							dv.yb2 = 0;

							switch (m_line_join)
							{
								case outline_aa_join_e.outline_no_join:
									dv.flags = 3;
									break;

								case outline_aa_join_e.outline_miter_join:
								case outline_aa_join_e.outline_round_join:
									dv.flags =
										(prev.diagonal_quadrant() == dv.curr.diagonal_quadrant() ? 1 : 0) |
											((dv.curr.diagonal_quadrant() == dv.next.diagonal_quadrant() ? 1 : 0) << 1);
									break;

								case outline_aa_join_e.outline_miter_accurate_join:
									dv.flags = 0;
									break;
							}

							if (m_round_cap)
							{
								m_ren.semidot(cmp_dist_start, x1, y1, x1 + (y2 - y1), y1 - (x2 - x1));
							}
							if ((dv.flags & 1) == 0)
							{
								if (m_line_join == outline_aa_join_e.outline_round_join)
								{
									m_ren.line3(prev, x1 + (y2 - y1), y1 - (x2 - x1),
													   x2 + (y2 - y1), y2 - (x2 - x1));
									m_ren.pie(prev.x2, prev.y2,
											   x2 + (y2 - y1), y2 - (x2 - x1),
											   dv.curr.x1 + (dv.curr.y2 - dv.curr.y1),
											   dv.curr.y1 - (dv.curr.x2 - dv.curr.x1));
								}
								else
								{
									LineAABasics.bisectrix(prev, dv.curr, out dv.xb1, out dv.yb1);
									m_ren.line3(prev, x1 + (y2 - y1), y1 - (x2 - x1),
													   dv.xb1, dv.yb1);
								}
							}
							else
							{
								m_ren.line1(prev,
											 x1 + (y2 - y1),
											 y1 - (x2 - x1));
							}
							if ((dv.flags & 2) == 0 && m_line_join != outline_aa_join_e.outline_round_join)
							{
								LineAABasics.bisectrix(dv.curr, dv.next, out dv.xb2, out dv.yb2);
							}

							draw(ref dv, 1, m_src_vertices.size() - 2);

							if ((dv.flags & 1) == 0)
							{
								if (m_line_join == outline_aa_join_e.outline_round_join)
								{
									m_ren.line3(dv.curr,
												 dv.curr.x1 + (dv.curr.y2 - dv.curr.y1),
												 dv.curr.y1 - (dv.curr.x2 - dv.curr.x1),
												 dv.curr.x2 + (dv.curr.y2 - dv.curr.y1),
												 dv.curr.y2 - (dv.curr.x2 - dv.curr.x1));
								}
								else
								{
									m_ren.line3(dv.curr, dv.xb1, dv.yb1,
												 dv.curr.x2 + (dv.curr.y2 - dv.curr.y1),
												 dv.curr.y2 - (dv.curr.x2 - dv.curr.x1));
								}
							}
							else
							{
								m_ren.line2(dv.curr,
											 dv.curr.x2 + (dv.curr.y2 - dv.curr.y1),
											 dv.curr.y2 - (dv.curr.x2 - dv.curr.x1));
							}
							if (m_round_cap)
							{
								m_ren.semidot(cmp_dist_end, dv.curr.x2, dv.curr.y2,
											   dv.curr.x2 + (dv.curr.y2 - dv.curr.y1),
											   dv.curr.y2 - (dv.curr.x2 - dv.curr.x1));
							}
						}
						break;
				}
			}
			m_src_vertices.remove_all();
		}

		public void add_vertex(double x, double y, ShapePath.FlagsAndCommand cmd)
		{
			if (ShapePath.is_move_to(cmd))
			{
				render(false);
				move_to_d(x, y);
			}
			else
			{
				if (ShapePath.is_end_poly(cmd))
				{
					render(ShapePath.is_closed(cmd));
					if (ShapePath.is_closed(cmd))
					{
						move_to(m_start_x, m_start_y);
					}
				}
				else
				{
					line_to_d(x, y);
				}
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

			ShapePath.FlagsAndCommand cmd;
			vs.rewind(path_id);

			//int index = 0;
			//int start = 851;
			//int num = 5;

			while (!ShapePath.is_stop(cmd = vs.vertex(out x, out y)))
			{
				//index++;
				//if (index == 0
				//  || (index > start && index < start + num))
				add_vertex(x, y, cmd);
			}
			render(false);
		}

		public void RenderAllPaths(IVertexSource vs,
							  RGBA_Bytes[] colors,
							  int[] path_id,
							  int num_paths)
		{
			for (int i = 0; i < num_paths; i++)
			{
				m_ren.color(colors[i]);
				add_path(vs, path_id[i]);
			}
		}

		/* // for debugging only
		public void render_path_index(IVertexSource vs,
							  RGBA_Bytes[] colors,
							  int[] path_id,
							  int pathIndex)
		{
			m_ren.color(colors[pathIndex]);
			add_path(vs, path_id[pathIndex]);
		}
		 */
	};
}