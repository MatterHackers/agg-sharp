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
	//============================================================vcgen_stroke
	internal class StrokeGenerator : IGenerator
	{
		private int m_closed;
		private int m_out_vertex;
		private Vector2Container m_out_vertices;
		private StrokeMath.status_e m_prev_status;
		private double m_shorten;
		private int m_src_vertex;
		private VertexSequence m_src_vertices;
		private StrokeMath.status_e m_status;
		private StrokeMath m_stroker;

		public StrokeGenerator()
		{
			m_stroker = new StrokeMath();
			m_src_vertices = new VertexSequence();
			m_out_vertices = new Vector2Container();
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

		public double ApproximationScale
		{
			get => m_stroker.approximation_scale();
			set => m_stroker.approximation_scale(value);
		}

		// TODO: Needs review - we previously implemented this interface element but threw when accessed. Propose having no effect instead of being destructive
		public bool AutoDetectOrientation { get; set; }
		
		public InnerJoin InnerJoin
		{
			get => m_stroker.inner_join();
			set => m_stroker.inner_join(value);
		}

		public double InnerMiterLimit
		{
			get => m_stroker.inner_miter_limit();
			set => m_stroker.inner_miter_limit(value);
		}

		public LineCap LineCap
		{
			get => m_stroker.line_cap();
			set => m_stroker.line_cap(value);
		}

		public LineJoin LineJoin
		{
			get => m_stroker.line_join();
			set => m_stroker.line_join(value);
		}

		public double MiterLimit
		{
			get => m_stroker.miter_limit();
			set => m_stroker.miter_limit(value);
		}

		public double Shorten
		{
			get => m_shorten;
			set => m_shorten = value;
		}

		public double Width
		{
			get => m_stroker.width();
			set => m_stroker.width(value);
		}

		public void MiterLimitTheta(double t)
		{
			m_stroker.miter_limit_theta(t);
		}

		// Vertex Generator Interface
		public void RemoveAll()
		{
			m_src_vertices.remove_all();
			m_closed = 0;
			m_status = StrokeMath.status_e.initial;
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
			ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.LineTo;
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
							cmd = ShapePath.FlagsAndCommand.Stop;
							break;
						}
						m_status = (m_closed != 0) ? StrokeMath.status_e.outline1 : StrokeMath.status_e.cap1;
						cmd = ShapePath.FlagsAndCommand.MoveTo;
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
						cmd = ShapePath.FlagsAndCommand.MoveTo;
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
							x = c.X;
							y = c.Y;
							return cmd;
						}
						break;

					case StrokeMath.status_e.end_poly1:
						m_status = m_prev_status;
						return ShapePath.FlagsAndCommand.EndPoly
							| ShapePath.FlagsAndCommand.FlagClose
							| ShapePath.FlagsAndCommand.FlagCCW;

					case StrokeMath.status_e.end_poly2:
						m_status = m_prev_status;
						return ShapePath.FlagsAndCommand.EndPoly
							| ShapePath.FlagsAndCommand.FlagClose
							| ShapePath.FlagsAndCommand.FlagCW;

					case StrokeMath.status_e.stop:
						cmd = ShapePath.FlagsAndCommand.Stop;
						break;
				}
			}
			return cmd;
		}
	}

	internal class Vector2Container : VectorPOD<Vector2>, IVertexDest
	{
	}
}