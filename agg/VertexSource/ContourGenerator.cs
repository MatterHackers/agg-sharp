using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	internal class ContourGenerator : IGenerator
	{
		private bool m_auto_detect;
		private bool m_closed;
		private ShapePath.FlagsAndCommand m_orientation;
		private int m_out_vertex;
		private Vector2Container m_out_vertices;
		private double m_shorten;
		private int m_src_vertex;
		private VertexSequence m_src_vertices;
		private StrokeMath.status_e m_status;
		private StrokeMath m_stroker;
		private double m_width;

		public ContourGenerator()
		{
			m_stroker = new StrokeMath();
			m_width = 1;
			m_src_vertices = new VertexSequence();
			m_out_vertices = new Vector2Container();
			m_status = StrokeMath.status_e.initial;
			m_src_vertex = 0;
			m_closed = false;
			m_orientation = 0;
			m_auto_detect = false;
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
					if (ShapePath.is_end_poly(cmd))
					{
						m_closed = (ShapePath.get_close_flag(cmd) == ShapePath.FlagsAndCommand.FlagClose);
						if (m_orientation == ShapePath.FlagsAndCommand.FlagNone)
						{
							m_orientation = ShapePath.get_orientation(cmd);
						}
					}
				}
			}
		}

		public double ApproximationScale
		{
			get => m_stroker.approximation_scale();
			set => m_stroker.approximation_scale(value);
		}

		public bool AutoDetectOrientation
		{
			get => m_auto_detect;
			set => m_auto_detect = value;
		}

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

		// Generator interface
		public void RemoveAll()
		{
			m_src_vertices.remove_all();
			m_closed = false;
			m_status = StrokeMath.status_e.initial;
		}

		// Vertex Source Interface
		public void Rewind(int idx)
		{
			if (m_status == StrokeMath.status_e.initial)
			{
				m_src_vertices.close(true);
				if (m_auto_detect)
				{
					if (!ShapePath.is_oriented(m_orientation))
					{
						m_orientation = (agg_math.calc_polygon_area(m_src_vertices) > 0.0) ?
										ShapePath.FlagsAndCommand.FlagCCW :
										ShapePath.FlagsAndCommand.FlagCW;
					}
				}
				if (ShapePath.is_oriented(m_orientation))
				{
					m_stroker.width(ShapePath.is_ccw(m_orientation) ? m_width : -m_width);
				}
			}
			m_status = StrokeMath.status_e.ready;
			m_src_vertex = 0;
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
						if (m_src_vertices.size() < 2 + (m_closed ? 1 : 0))
						{
							cmd = ShapePath.FlagsAndCommand.Stop;
							break;
						}
						m_status = StrokeMath.status_e.outline1;
						cmd = ShapePath.FlagsAndCommand.MoveTo;
						m_src_vertex = 0;
						m_out_vertex = 0;
						goto case StrokeMath.status_e.outline1;

					case StrokeMath.status_e.outline1:
						if (m_src_vertex >= m_src_vertices.size())
						{
							m_status = StrokeMath.status_e.end_poly1;
							break;
						}
						m_stroker.calc_join(m_out_vertices,
											m_src_vertices.prev(m_src_vertex),
											m_src_vertices.curr(m_src_vertex),
											m_src_vertices.next(m_src_vertex),
											m_src_vertices.prev(m_src_vertex).dist,
											m_src_vertices.curr(m_src_vertex).dist);
						++m_src_vertex;
						m_status = StrokeMath.status_e.out_vertices;
						m_out_vertex = 0;
						goto case StrokeMath.status_e.out_vertices;

					case StrokeMath.status_e.out_vertices:
						if (m_out_vertex >= m_out_vertices.size())
						{
							m_status = StrokeMath.status_e.outline1;
						}
						else
						{
							Vector2 c = m_out_vertices[m_out_vertex++];
							x = c.X;
							y = c.Y;
							return cmd;
						}
						break;

					case StrokeMath.status_e.end_poly1:
						if (!m_closed) return ShapePath.FlagsAndCommand.Stop;
						m_status = StrokeMath.status_e.stop;
						return ShapePath.FlagsAndCommand.EndPoly | ShapePath.FlagsAndCommand.FlagClose | ShapePath.FlagsAndCommand.FlagCCW;

					case StrokeMath.status_e.stop:
						return ShapePath.FlagsAndCommand.Stop;
				}
			}
			return cmd;
		}
	}
}