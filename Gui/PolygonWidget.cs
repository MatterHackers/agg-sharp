using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

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
//
// classes polygon_ctrl_impl, polygon_ctrl
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	internal class simple_polygon_vertex_source : IVertexSource
	{
		private double[] m_polygon;
		private int m_num_points;
		private int m_vertex;
		private bool m_roundoff;
		private bool m_close;

		public simple_polygon_vertex_source(double[] polygon, int np)
			: this(polygon, np, false, true)
		{
		}

		public simple_polygon_vertex_source(double[] polygon, int np,
									 bool roundoff)
			: this(polygon, np, roundoff, true)
		{
		}

		public simple_polygon_vertex_source(double[] polygon, int np, bool roundoff, bool close)
		{
			m_polygon = (polygon);
			m_num_points = (np);
			m_vertex = (0);
			m_roundoff = (roundoff);
			m_close = (close);
		}

		public void close(bool f)
		{
			m_close = f;
		}

		public bool close()
		{
			return m_close;
		}

		public IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}

		public void rewind(int idx)
		{
			m_vertex = 0;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (m_vertex > m_num_points)
			{
				return ShapePath.FlagsAndCommand.CommandStop;
			}

			if (m_vertex == m_num_points)
			{
				++m_vertex;
				return ShapePath.FlagsAndCommand.CommandEndPoly | (m_close ? ShapePath.FlagsAndCommand.FlagClose : 0);
			}
			x = m_polygon[m_vertex * 2];
			y = m_polygon[m_vertex * 2 + 1];
			if (m_roundoff)
			{
				x = Math.Floor(x) + 0.5;
				y = Math.Floor(y) + 0.5;
			}
			++m_vertex;
			return (m_vertex == 1) ? ShapePath.FlagsAndCommand.CommandMoveTo : ShapePath.FlagsAndCommand.CommandLineTo;
		}
	};

	public class polygon_ctrl_impl : UI.SimpleVertexSourceWidget
	{
		private ArrayPOD<double> m_polygon;
		private int m_num_points;
		private int m_node;
		private int m_edge;
		private simple_polygon_vertex_source m_vs;
		private Stroke m_stroke;
		private VertexSource.Ellipse m_ellipse;
		private double m_point_radius;
		private int m_status;
		private double m_dx;
		private double m_dy;
		private bool m_in_polygon_check;
		private bool needToRecalculateBounds = true;

		public event EventHandler Changed;

		public polygon_ctrl_impl(int np)
			: this(np, 5)
		{
		}

		public polygon_ctrl_impl(int np, double point_radius)
			: base(new Vector2())
		{
			m_ellipse = new MatterHackers.Agg.VertexSource.Ellipse();
			m_polygon = new ArrayPOD<double>(np * 2);
			m_num_points = (np);
			m_node = (-1);
			m_edge = (-1);
			m_vs = new simple_polygon_vertex_source(m_polygon.Array, m_num_points, false);
			m_stroke = new Stroke(m_vs);
			m_point_radius = (point_radius);
			m_status = (0);
			m_dx = (0.0);
			m_dy = (0.0);
			m_in_polygon_check = (true);
			m_stroke.width(1.0);
		}

		public override void OnParentChanged(EventArgs e)
		{
			if (needToRecalculateBounds)
			{
				RecalculateBounds();
			}
			base.OnParentChanged(e);
		}

		public int num_points()
		{
			return m_num_points;
		}

		public double GetXN(int n)
		{
			return m_polygon.Array[n * 2];
		}

		public void SetXN(int n, double newXN)
		{
			needToRecalculateBounds = true; m_polygon.Array[n * 2] = newXN;
		}

		public void AddXN(int n, double newXN)
		{
			needToRecalculateBounds = true; m_polygon.Array[n * 2] += newXN;
		}

		public double GetYN(int n)
		{
			return m_polygon.Array[n * 2 + 1];
		}

		public void SetYN(int n, double newYN)
		{
			needToRecalculateBounds = true; m_polygon.Array[n * 2 + 1] = newYN;
		}

		public void AddYN(int n, double newYN)
		{
			needToRecalculateBounds = true; m_polygon.Array[n * 2 + 1] += newYN;
		}

		public double[] polygon()
		{
			return m_polygon.Array;
		}

		public void line_width(double w)
		{
			m_stroke.width(w);
		}

		public double line_width()
		{
			return m_stroke.width();
		}

		public void point_radius(double r)
		{
			m_point_radius = r;
		}

		public double point_radius()
		{
			return m_point_radius;
		}

		public void in_polygon_check(bool f)
		{
			m_in_polygon_check = f;
		}

		public bool in_polygon_check()
		{
			return m_in_polygon_check;
		}

		public void close(bool f)
		{
			m_vs.close(f);
		}

		public bool close()
		{
			return m_vs.close();
		}

		// Vertex source interface
		public override IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}

		public override int num_paths()
		{
			return 1;
		}

		public override void rewind(int path_id)
		{
			if (needToRecalculateBounds)
			{
				RecalculateBounds();
			}
			m_status = 0;
			m_stroke.rewind(0);
		}

		private void RecalculateBounds()
		{
			needToRecalculateBounds = false;
			return;

#if false
            double extraForControlPoints = m_point_radius * 1.3;
            RectangleDouble newBounds = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
            for (int i = 0; i < m_num_points; i++)
            {
                newBounds.Left = Math.Min(GetXN(i) - extraForControlPoints, newBounds.Left);
                newBounds.Right = Math.Max(GetXN(i) + extraForControlPoints, newBounds.Right);
                newBounds.Bottom = Math.Min(GetYN(i) - extraForControlPoints, newBounds.Bottom);
                newBounds.Top = Math.Max(GetYN(i) + extraForControlPoints, newBounds.Top);
            }

            Invalidate();
            LocalBounds = newBounds;
            Invalidate();
#endif
		}

		public override ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandStop;
			double r = m_point_radius;
			if (m_status == 0)
			{
				cmd = m_stroke.vertex(out x, out y);
				if (!ShapePath.is_stop(cmd))
				{
					ParentToChildTransform.transform(ref x, ref y);
					return cmd;
				}
				if (m_node >= 0 && m_node == (int)(m_status)) r *= 1.2;
				m_ellipse.init(GetXN(m_status), GetYN(m_status), r, r, 32);
				++m_status;
			}
			cmd = m_ellipse.vertex(out x, out y);
			if (!ShapePath.is_stop(cmd))
			{
				ParentToChildTransform.transform(ref x, ref y);
				return cmd;
			}
			if (m_status >= m_num_points) return ShapePath.FlagsAndCommand.CommandStop;
			if (m_node >= 0 && m_node == (int)(m_status)) r *= 1.2;
			m_ellipse.init(GetXN(m_status), GetYN(m_status), r, r, 32);
			++m_status;
			cmd = m_ellipse.vertex(out x, out y);
			if (!ShapePath.is_stop(cmd))
			{
				ParentToChildTransform.transform(ref x, ref y);
			}
			return cmd;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			bool ret = false;
			m_node = -1;
			m_edge = -1;
			double x = mouseEvent.X;
			double y = mouseEvent.Y;
			ParentToChildTransform.inverse_transform(ref x, ref y);
			for (int i = 0; i < m_num_points; i++)
			{
				if (Math.Sqrt((x - GetXN(i)) * (x - GetXN(i)) + (y - GetYN(i)) * (y - GetYN(i))) < m_point_radius)
				{
					m_dx = x - GetXN(i);
					m_dy = y - GetYN(i);
					m_node = (int)(i);
					ret = true;
					break;
				}
			}

			if (!ret)
			{
				for (int i = 0; i < m_num_points; i++)
				{
					if (check_edge(i, x, y))
					{
						m_dx = x;
						m_dy = y;
						m_edge = (int)(i);
						ret = true;
						break;
					}
				}
			}

			if (!ret)
			{
				if (point_in_polygon(x, y))
				{
					m_dx = x;
					m_dy = y;
					m_node = (int)(m_num_points);
					ret = true;
				}
			}

			Invalidate();
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool ret = (m_node >= 0) || (m_edge >= 0);
			m_node = -1;
			m_edge = -1;

			Invalidate();
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			bool handled = false;
			double dx;
			double dy;
			double x = mouseEvent.X;
			double y = mouseEvent.Y;
			ParentToChildTransform.inverse_transform(ref x, ref y);
			if (m_node == (int)(m_num_points))
			{
				dx = x - m_dx;
				dy = y - m_dy;
				for (int i = 0; i < m_num_points; i++)
				{
					SetXN(i, GetXN(i) + dx);
					SetYN(i, GetYN(i) + dy);
				}
				m_dx = x;
				m_dy = y;
				handled = true;
			}
			else
			{
				if (m_edge >= 0)
				{
					int n1 = (int)m_edge;
					int n2 = (n1 + m_num_points - 1) % m_num_points;
					dx = x - m_dx;
					dy = y - m_dy;
					SetXN(n1, GetXN(n1) + dx);
					SetYN(n1, GetYN(n1) + dy);
					SetXN(n2, GetXN(n2) + dx);
					SetYN(n2, GetYN(n2) + dy);
					m_dx = x;
					m_dy = y;
					handled = true;
				}
				else
				{
					if (m_node >= 0)
					{
						SetXN((int)m_node, x - m_dx);
						SetYN((int)m_node, y - m_dy);
						handled = true;
					}
				}
			}

			// TODO: set bounds correctly and invalidate
			if (handled)
			{
				if (Changed != null)
				{
					Changed(this, null);
				}
				RecalculateBounds();
			}

			base.OnMouseMove(mouseEvent);
		}

		private bool check_edge(int i, double x, double y)
		{
			bool ret = false;

			int n1 = i;
			int n2 = (i + m_num_points - 1) % m_num_points;
			double x1 = GetXN(n1);
			double y1 = GetYN(n1);
			double x2 = GetXN(n2);
			double y2 = GetYN(n2);

			double dx = x2 - x1;
			double dy = y2 - y1;

			if (Math.Sqrt(dx * dx + dy * dy) > 0.0000001)
			{
				double x3 = x;
				double y3 = y;
				double x4 = x3 - dy;
				double y4 = y3 + dx;

				double den = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
				double u1 = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / den;

				double xi = x1 + u1 * (x2 - x1);
				double yi = y1 + u1 * (y2 - y1);

				dx = xi - x;
				dy = yi - y;

				if (u1 > 0.0 && u1 < 1.0 && Math.Sqrt(dx * dx + dy * dy) <= m_point_radius)
				{
					ret = true;
				}
			}
			return ret;
		}

		//======= Crossings Multiply algorithm of InsideTest ========================
		//
		// By Eric Haines, 3D/Eye Inc, erich@eye.com
		//
		// This version is usually somewhat faster than the original published in
		// Graphics Gems IV; by turning the division for testing the X axis crossing
		// into a tricky multiplication test this part of the test became faster,
		// which had the additional effect of making the test for "both to left or
		// both to right" a bit slower for triangles than simply computing the
		// intersection each time.  The main increase is in triangle testing speed,
		// which was about 15% faster; all other polygon complexities were pretty much
		// the same as before.  On machines where division is very expensive (not the
		// case on the HP 9000 series on which I tested) this test should be much
		// faster overall than the old code.  Your mileage may (in fact, will) vary,
		// depending on the machine and the test data, but in general I believe this
		// code is both shorter and faster.  This test was inspired by unpublished
		// Graphics Gems submitted by Joseph Samosky and Mark Haigh-Hutchinson.
		// Related work by Samosky is in:
		//
		// Samosky, Joseph, "SectionView: A system for interactively specifying and
		// visualizing sections through three-dimensional medical image data",
		// M.S. Thesis, Department of Electrical Engineering and Computer Science,
		// Massachusetts Institute of Technology, 1993.
		//
		// Shoot a test ray along +X axis.  The strategy is to compare vertex Y values
		// to the testing point's Y and quickly discard edges which are entirely to one
		// side of the test ray.  Note that CONVEX and WINDING code can be added as
		// for the CrossingsTest() code; it is left out here for clarity.
		//
		// Input 2D polygon _pgon_ with _numverts_ number of vertices and test point
		// _point_, returns 1 if inside, 0 if outside.
		private bool point_in_polygon(double tx, double ty)
		{
			if (m_num_points < 3) return false;
			if (!m_in_polygon_check) return false;

			int j;
			bool yflag0, yflag1, inside_flag;
			double vtx0, vty0, vtx1, vty1;

			vtx0 = GetXN(m_num_points - 1);
			vty0 = GetYN(m_num_points - 1);

			// get test bit for above/below X axis
			yflag0 = (vty0 >= ty);

			vtx1 = GetXN(0);
			vty1 = GetYN(0);

			inside_flag = false;
			for (j = 1; j <= m_num_points; ++j)
			{
				yflag1 = (vty1 >= ty);
				// Check if endpoints straddle (are on opposite sides) of X axis
				// (i.e. the Y's differ); if so, +X ray could intersect this edge.
				// The old test also checked whether the endpoints are both to the
				// right or to the left of the test point.  However, given the faster
				// intersection point computation used below, this test was found to
				// be a break-even proposition for most polygons and a loser for
				// triangles (where 50% or more of the edges which survive this test
				// will cross quadrants and so have to have the X intersection computed
				// anyway).  I credit Joseph Samosky with inspiring me to try dropping
				// the "both left or both right" part of my code.
				if (yflag0 != yflag1)
				{
					// Check intersection of pgon segment with +X ray.
					// Note if >= point's X; if so, the ray hits it.
					// The division operation is avoided for the ">=" test by checking
					// the sign of the first vertex wrto the test point; idea inspired
					// by Joseph Samosky's and Mark Haigh-Hutchinson's different
					// polygon inclusion tests.
					if (((vty1 - ty) * (vtx0 - vtx1) >=
						  (vtx1 - tx) * (vty0 - vty1)) == yflag1)
					{
						inside_flag = !inside_flag;
					}
				}

				// Move to the next pair of vertices, retaining info as possible.
				yflag0 = yflag1;
				vtx0 = vtx1;
				vty0 = vty1;

				int k = (j >= m_num_points) ? j - m_num_points : j;
				vtx1 = GetXN(k);
				vty1 = GetYN(k);
			}
			return inside_flag;
		}
	};

	//----------------------------------------------------------polygon_ctrl
	//template<class ColorT>
	public class PolygonEditWidget : polygon_ctrl_impl
	{
		private IColorType m_color;

		public PolygonEditWidget(int np)
			: this(np, 5)
		{
		}

		public PolygonEditWidget(int np, double point_radius)
			: base(np, point_radius)
		{
			m_color = new RGBA_Floats(0.0, 0.0, 0.0);
		}

		public void line_color(IColorType c)
		{
			m_color = c;
		}

		public override IColorType color(int i)
		{
			return m_color;
		}
	};
}