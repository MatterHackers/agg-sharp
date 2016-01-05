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
using System.Collections.Generic;

namespace MatterHackers.Agg.VertexSource
{
	//============================================================span_gouraud
	public class span_gouraud : IVertexSource
	{
		private coord_type[] m_coord = new coord_type[3];
		private double[] m_x = new double[8];
		private double[] m_y = new double[8];
		private ShapePath.FlagsAndCommand[] m_cmd = new ShapePath.FlagsAndCommand[8];
		private int m_vertex;

		public struct coord_type
		{
			public double x;
			public double y;
			public RGBA_Bytes color;
		};

		public span_gouraud()
		{
			m_vertex = (0);
			m_cmd[0] = ShapePath.FlagsAndCommand.CommandStop;
		}

		public span_gouraud(RGBA_Bytes c1,
					 RGBA_Bytes c2,
					 RGBA_Bytes c3,
					 double x1, double y1,
					 double x2, double y2,
					 double x3, double y3,
					 double d)
		{
			m_vertex = (0);
			colors(c1, c2, c3);
			triangle(x1, y1, x2, y2, x3, y3, d);
		}

		public void colors(IColorType c1, IColorType c2, IColorType c3)
		{
			m_coord[0].color = c1.GetAsRGBA_Bytes();
			m_coord[1].color = c2.GetAsRGBA_Bytes();
			m_coord[2].color = c3.GetAsRGBA_Bytes();
		}

		//--------------------------------------------------------------------
		// Sets the triangle and dilates it if needed.
		// The trick here is to calculate beveled joins in the vertices of the
		// triangle and render it as a 6-vertex polygon.
		// It's necessary to achieve numerical stability.
		// However, the coordinates to interpolate colors are calculated
		// as miter joins (calc_intersection).
		public void triangle(double x1, double y1,
					  double x2, double y2,
					  double x3, double y3,
					  double dilation = 0)
		{
			m_coord[0].x = m_x[0] = x1;
			m_coord[0].y = m_y[0] = y1;
			m_coord[1].x = m_x[1] = x2;
			m_coord[1].y = m_y[1] = y2;
			m_coord[2].x = m_x[2] = x3;
			m_coord[2].y = m_y[2] = y3;
			m_cmd[0] = ShapePath.FlagsAndCommand.CommandMoveTo;
			m_cmd[1] = ShapePath.FlagsAndCommand.CommandLineTo;
			m_cmd[2] = ShapePath.FlagsAndCommand.CommandLineTo;
			m_cmd[3] = ShapePath.FlagsAndCommand.CommandStop;

			if (dilation != 0.0)
			{
				agg_math.dilate_triangle(m_coord[0].x, m_coord[0].y,
								m_coord[1].x, m_coord[1].y,
								m_coord[2].x, m_coord[2].y,
								m_x, m_y, dilation);

				agg_math.calc_intersection(m_x[4], m_y[4], m_x[5], m_y[5],
								  m_x[0], m_y[0], m_x[1], m_y[1],
								  out m_coord[0].x, out m_coord[0].y);

				agg_math.calc_intersection(m_x[0], m_y[0], m_x[1], m_y[1],
								  m_x[2], m_y[2], m_x[3], m_y[3],
								  out m_coord[1].x, out m_coord[1].y);

				agg_math.calc_intersection(m_x[2], m_y[2], m_x[3], m_y[3],
								  m_x[4], m_y[4], m_x[5], m_y[5],
								  out m_coord[2].x, out m_coord[2].y);
				m_cmd[3] = ShapePath.FlagsAndCommand.CommandLineTo;
				m_cmd[4] = ShapePath.FlagsAndCommand.CommandLineTo;
				m_cmd[5] = ShapePath.FlagsAndCommand.CommandLineTo;
				m_cmd[6] = ShapePath.FlagsAndCommand.CommandStop;
			}
		}

		public IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}

		// Vertex Source Interface to feed the coordinates to the rasterizer
		public void rewind(int idx)
		{
			m_vertex = 0;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = m_x[m_vertex];
			y = m_y[m_vertex];
			return m_cmd[m_vertex++];
		}

		protected void arrange_vertices(coord_type[] coord)
		{
			coord[0] = m_coord[0];
			coord[1] = m_coord[1];
			coord[2] = m_coord[2];

			if (m_coord[0].y > m_coord[2].y)
			{
				coord[0] = m_coord[2];
				coord[2] = m_coord[0];
			}

			coord_type tmp;
			if (coord[0].y > coord[1].y)
			{
				tmp = coord[1];
				coord[1] = coord[0];
				coord[0] = tmp;
			}

			if (coord[1].y > coord[2].y)
			{
				tmp = coord[2];
				coord[2] = coord[1];
				coord[1] = tmp;
			}
		}
	}
}