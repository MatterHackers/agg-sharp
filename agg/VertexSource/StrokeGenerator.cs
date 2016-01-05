using MatterHackers.VectorMath;

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
using System;

namespace MatterHackers.Agg.VertexSource
{
	internal class Vector2Container : VectorPOD<Vector2>, IVertexDest
	{
	}

	//============================================================vcgen_stroke
	internal class StrokeGenerator : IGenerator
	{
		private StrokeMath m_stroker;

		private VertexSequence m_src_vertices;
		private Vector2Container m_out_vertices;

		private double m_shorten;
		private int m_closed;
		private StrokeMath.status_e m_status;
		private StrokeMath.status_e m_prev_status;

		private int m_src_vertex;
		private int m_out_vertex;

		public StrokeGenerator()
		{
			m_stroker = new StrokeMath();
			m_src_vertices = new VertexSequence();
			m_out_vertices = new Vector2Container();
			m_status = StrokeMath.status_e.initial;
		}

		public void line_cap(LineCap lc)
		{
			m_stroker.line_cap(lc);
		}

		public void line_join(LineJoin lj)
		{
			m_stroker.line_join(lj);
		}

		public void inner_join(InnerJoin ij)
		{
			m_stroker.inner_join(ij);
		}

		public LineCap line_cap()
		{
			return m_stroker.line_cap();
		}

		public LineJoin line_join()
		{
			return m_stroker.line_join();
		}

		public InnerJoin inner_join()
		{
			return m_stroker.inner_join();
		}

		public void width(double w)
		{
			m_stroker.width(w);
		}

		public void miter_limit(double ml)
		{
			m_stroker.miter_limit(ml);
		}

		public void miter_limit_theta(double t)
		{
			m_stroker.miter_limit_theta(t);
		}

		public void inner_miter_limit(double ml)
		{
			m_stroker.inner_miter_limit(ml);
		}

		public void approximation_scale(double approx_scale)
		{
			m_stroker.approximation_scale(approx_scale);
		}

		public double width()
		{
			return m_stroker.width();
		}

		public double miter_limit()
		{
			return m_stroker.miter_limit();
		}

		public double inner_miter_limit()
		{
			return m_stroker.inner_miter_limit();
		}

		public double approximation_scale()
		{
			return m_stroker.approximation_scale();
		}

		public void auto_detect_orientation(bool v)
		{
			throw new Exception();
		}

		public bool auto_detect_orientation()
		{
			throw new Exception();
		}

		public void shorten(double s)
		{
			m_shorten = s;
		}

		public double shorten()
		{
			return m_shorten;
		}

		// Vertex Generator Interface
		public void RemoveAll()
		{
			m_src_vertices.remove_all();
			m_closed = 0;
			m_status = StrokeMath.status_e.initial;
		}

		public void AddVertex(double x, double y, ShapePath.FlagsAndCommand cmd)
		{
			m_status = StrokeMath.status_e.initial;
			if (ShapePath.is_move_to(cmd))
			{
				m_src_vertices.modify_last(new VertexDistance(x, y));
			}
			else
			{
				if (ShapePath.is_vertex(cmd))
				{
					m_src_vertices.add(new VertexDistance(x, y));
				}
				else
				{
					m_closed = (int)ShapePath.get_close_flag(cmd);
				}
			}
		}

		// Vertex Source Interface
		public void Rewind(int idx)
		{
			if (m_status == StrokeMath.status_e.initial)
			{
				m_src_vertices.close(m_closed != 0);
				ShapePath.shorten_path(m_src_vertices, m_shorten, m_closed);
				if (m_src_vertices.size() < 3) m_closed = 0;
			}
			m_status = StrokeMath.status_e.ready;
			m_src_vertex = 0;
			m_out_vertex = 0;
		}

		public ShapePath.FlagsAndCommand Vertex(ref double x, ref double y)
		{
			ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandLineTo;
			while (!ShapePath.is_stop(cmd))
			{
				switch (m_status)
				{
					case StrokeMath.status_e.initial:
						Rewind(0);
						goto case StrokeMath.status_e.ready;

					case StrokeMath.status_e.ready:
						if (m_src_vertices.size() < 2 + (m_closed != 0 ? 1 : 0))
						{
							cmd = ShapePath.FlagsAndCommand.CommandStop;
							break;
						}
						m_status = (m_closed != 0) ? StrokeMath.status_e.outline1 : StrokeMath.status_e.cap1;
						cmd = ShapePath.FlagsAndCommand.CommandMoveTo;
						m_src_vertex = 0;
						m_out_vertex = 0;
						break;

					case StrokeMath.status_e.cap1:
						m_stroker.calc_cap(m_out_vertices, m_src_vertices[0], m_src_vertices[1],
							m_src_vertices[0].dist);
						m_src_vertex = 1;
						m_prev_status = StrokeMath.status_e.outline1;
						m_status = StrokeMath.status_e.out_vertices;
						m_out_vertex = 0;
						break;

					case StrokeMath.status_e.cap2:
						m_stroker.calc_cap(m_out_vertices,
							m_src_vertices[m_src_vertices.size() - 1],
							m_src_vertices[m_src_vertices.size() - 2],
							m_src_vertices[m_src_vertices.size() - 2].dist);
						m_prev_status = StrokeMath.status_e.outline2;
						m_status = StrokeMath.status_e.out_vertices;
						m_out_vertex = 0;
						break;

					case StrokeMath.status_e.outline1:
						if (m_closed != 0)
						{
							if (m_src_vertex >= m_src_vertices.size())
							{
								m_prev_status = StrokeMath.status_e.close_first;
								m_status = StrokeMath.status_e.end_poly1;
								break;
							}
						}
						else
						{
							if (m_src_vertex >= m_src_vertices.size() - 1)
							{
								m_status = StrokeMath.status_e.cap2;
								break;
							}
						}
						m_stroker.calc_join(m_out_vertices,
							m_src_vertices.prev(m_src_vertex),
							m_src_vertices.curr(m_src_vertex),
							m_src_vertices.next(m_src_vertex),
							m_src_vertices.prev(m_src_vertex).dist,
							m_src_vertices.curr(m_src_vertex).dist);
						++m_src_vertex;
						m_prev_status = m_status;
						m_status = StrokeMath.status_e.out_vertices;
						m_out_vertex = 0;
						break;

					case StrokeMath.status_e.close_first:
						m_status = StrokeMath.status_e.outline2;
						cmd = ShapePath.FlagsAndCommand.CommandMoveTo;
						goto case StrokeMath.status_e.outline2;

					case StrokeMath.status_e.outline2:
						if (m_src_vertex <= (m_closed == 0 ? 1 : 0))
						{
							m_status = StrokeMath.status_e.end_poly2;
							m_prev_status = StrokeMath.status_e.stop;
							break;
						}

						--m_src_vertex;
						m_stroker.calc_join(m_out_vertices,
							m_src_vertices.next(m_src_vertex),
							m_src_vertices.curr(m_src_vertex),
							m_src_vertices.prev(m_src_vertex),
							m_src_vertices.curr(m_src_vertex).dist,
							m_src_vertices.prev(m_src_vertex).dist);

						m_prev_status = m_status;
						m_status = StrokeMath.status_e.out_vertices;
						m_out_vertex = 0;
						break;

					case StrokeMath.status_e.out_vertices:
						if (m_out_vertex >= m_out_vertices.size())
						{
							m_status = m_prev_status;
						}
						else
						{
							Vector2 c = m_out_vertices[(int)m_out_vertex++];
							x = c.x;
							y = c.y;
							return cmd;
						}
						break;

					case StrokeMath.status_e.end_poly1:
						m_status = m_prev_status;
						return ShapePath.FlagsAndCommand.CommandEndPoly
							| ShapePath.FlagsAndCommand.FlagClose
							| ShapePath.FlagsAndCommand.FlagCCW;

					case StrokeMath.status_e.end_poly2:
						m_status = m_prev_status;
						return ShapePath.FlagsAndCommand.CommandEndPoly
							| ShapePath.FlagsAndCommand.FlagClose
							| ShapePath.FlagsAndCommand.FlagCW;

					case StrokeMath.status_e.stop:
						cmd = ShapePath.FlagsAndCommand.CommandStop;
						break;
				}
			}
			return cmd;
		}
	}
}