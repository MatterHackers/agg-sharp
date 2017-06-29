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
// class gamma_ctrl
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	//------------------------------------------------------------------------
	// Class that can be used to create an interactive control to set up
	// gamma arrays.
	//------------------------------------------------------------------------
	public class gamma_ctrl : SimpleVertexSourceWidget
	{
		private gamma_spline m_gamma_spline = new gamma_spline();
		private double m_border_width;
		private double m_border_extra;
		private double m_curve_width;
		private double m_grid_width;
		private double m_text_thickness;
		private double m_point_size;
		private double m_text_height;
		private double m_xc1;
		private double m_yc1;
		private double m_xc2;
		private double m_yc2;
		private double m_xs1;
		private double m_ys1;
		private double m_xs2;
		private double m_ys2;
		private double m_xt1;
		private double m_yt1;
		private double m_xt2;
		private double m_yt2;
		private Stroke m_curve_poly;
		private VertexSource.Ellipse m_ellipse = new MatterHackers.Agg.VertexSource.Ellipse();
		private gsv_text m_text = new gsv_text();
		private Stroke m_text_poly;
		private int m_idx;
		private int m_vertex;
		private double[] gridVertexX = new double[32];
		private double[] gridVertexY = new double[32];
		private double m_xp1;
		private double m_yp1;
		private double m_xp2;
		private double m_yp2;
		private bool m_p1_active;
		private int m_mouse_point;
		private double m_pdx;
		private double m_pdy;

		private RGBA_Bytes m_background_color;
		private RGBA_Bytes m_border_color;
		private RGBA_Bytes m_curve_color;
		private RGBA_Bytes m_grid_color;
		private RGBA_Bytes m_inactive_pnt_color;
		private RGBA_Bytes m_active_pnt_color;
		private RGBA_Bytes m_text_color;
		private RGBA_Bytes[] m_colors = new RGBA_Bytes[7];

		// Set colors
		public void background_color(RGBA_Bytes c)
		{
			m_background_color = c;
		}

		public void border_color(RGBA_Bytes c)
		{
			m_border_color = c;
		}

		public void curve_color(RGBA_Bytes c)
		{
			m_curve_color = c;
		}

		public void grid_color(RGBA_Bytes c)
		{
			m_grid_color = c;
		}

		public void inactive_pnt_color(RGBA_Bytes c)
		{
			m_inactive_pnt_color = c;
		}

		public void active_pnt_color(RGBA_Bytes c)
		{
			m_active_pnt_color = c;
		}

		public void text_color(RGBA_Bytes c)
		{
			m_text_color = c;
		}

		public override IColorType color(int i)
		{
			return m_colors[i];
		}

		public gamma_ctrl(Vector2 position, Vector2 size)
			: base(position, false)
		{
			Vector2 location = position;// Vector2.Zero;
			LocalBounds = new RectangleDouble(0, 0, size.x, size.y);

			m_border_width = (2.0);
			m_border_extra = (0.0);
			m_curve_width = (2.0);
			m_grid_width = (0.2);
			m_text_thickness = (1.5);
			m_point_size = (5.0);
			m_text_height = (9.0);

			double x2 = location.x + size.x;
			double y2 = location.y + size.y;
			m_xc1 = location.x;
			m_yc1 = location.y;
			m_xc2 = (x2);
			m_yc2 = (y2 - m_text_height * 2.0);
			m_xt1 = location.x;
			m_yt1 = (y2 - m_text_height * 2.0);
			m_xt2 = (x2);
			m_yt2 = (y2);

			m_curve_poly = new Stroke(m_gamma_spline);
			m_text_poly = new Stroke(m_text);
			m_idx = (0);
			m_vertex = (0);
			m_p1_active = (true);
			m_mouse_point = (0);
			m_pdx = (0.0);
			m_pdy = (0.0);
			calc_spline_box();

			m_background_color = new RGBA_Bytes(1.0, 1.0, 0.9);
			m_border_color = new RGBA_Bytes(0.0, 0.0, 0.0);
			m_curve_color = new RGBA_Bytes(0.0, 0.0, 0.0);
			m_grid_color = new RGBA_Bytes(0.2, 0.2, 0.0);
			m_inactive_pnt_color = new RGBA_Bytes(0.0, 0.0, 0.0);
			m_active_pnt_color = new RGBA_Bytes(1.0, 0.0, 0.0);
			m_text_color = new RGBA_Bytes(0.0, 0.0, 0.0);

			m_colors[0] = m_curve_color;
			m_colors[1] = m_grid_color;
			m_colors[2] = m_inactive_pnt_color;
			m_colors[3] = m_active_pnt_color;
			m_colors[4] = m_text_color;
		}

		// Set other parameters
		public void border_width(double t)
		{
			border_width(t, 0);
		}

		public void border_width(double t, double extra)
		{
			m_border_width = t;
			m_border_extra = extra;
			calc_spline_box();
		}

		public void curve_width(double t)
		{
			m_curve_width = t;
		}

		public void grid_width(double t)
		{
			m_grid_width = t;
		}

		public void text_thickness(double t)
		{
			m_text_thickness = t;
		}

		public void text_size(double h)
		{
			m_text_height = h;
			m_yc2 = BoundsRelativeToParent.Top - m_text_height * 2.0;
			m_yt1 = BoundsRelativeToParent.Top - m_text_height * 2.0;
			calc_spline_box();
		}

		public void point_size(double s)
		{
			m_point_size = s;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			double x = mouseEvent.X;
			double y = mouseEvent.Y;
			calc_points();

			if (agg_math.calc_distance(x, y, m_xp1, m_yp1) <= m_point_size + 1)
			{
				m_mouse_point = 1;
				m_pdx = m_xp1 - x;
				m_pdy = m_yp1 - y;
				m_p1_active = true;
			}

			if (agg_math.calc_distance(x, y, m_xp2, m_yp2) <= m_point_size + 1)
			{
				m_mouse_point = 2;
				m_pdx = m_xp2 - x;
				m_pdy = m_yp2 - y;
				m_p1_active = false;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (m_mouse_point != 0)
			{
				m_mouse_point = 0;
			}
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			double x = mouseEvent.X;
			double y = mouseEvent.Y;

			if (m_mouse_point == 1)
			{
				m_xp1 = x + m_pdx;
				m_yp1 = y + m_pdy;
				calc_values();
			}
			if (m_mouse_point == 2)
			{
				m_xp2 = x + m_pdx;
				m_yp2 = y + m_pdy;
				calc_values();
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// this must be called first to ensure we get the correct Handled state
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled)
			{
				double kx1, ky1, kx2, ky2;
				bool ret = false;
				m_gamma_spline.values(out kx1, out ky1, out kx2, out ky2);
				if (m_p1_active)
				{
					if (keyEvent.KeyCode == Keys.Left) { kx1 -= 0.005; ret = true; }
					if (keyEvent.KeyCode == Keys.Right) { kx1 += 0.005; ret = true; }
					if (keyEvent.KeyCode == Keys.Down) { ky1 -= 0.005; ret = true; }
					if (keyEvent.KeyCode == Keys.Up) { ky1 += 0.005; ret = true; }
				}
				else
				{
					if (keyEvent.KeyCode == Keys.Left) { kx2 += 0.005; ret = true; }
					if (keyEvent.KeyCode == Keys.Right) { kx2 -= 0.005; ret = true; }
					if (keyEvent.KeyCode == Keys.Down) { ky2 += 0.005; ret = true; }
					if (keyEvent.KeyCode == Keys.Up) { ky2 -= 0.005; ret = true; }
				}
				if (ret)
				{
					m_gamma_spline.values(kx1, ky1, kx2, ky2);
					keyEvent.Handled = true;
				}
			}
		}

		public void change_active_point()
		{
			m_p1_active = m_p1_active ? false : true;
		}

		// A copy of agg::gamma_spline interface
		public void values(double kx1, double ky1, double kx2, double ky2)
		{
			m_gamma_spline.values(kx1, ky1, kx2, ky2);
		}

		public void values(out double kx1, out double ky1, out double kx2, out double ky2)
		{
			m_gamma_spline.values(out kx1, out ky1, out kx2, out ky2);
		}

		public byte[] gamma()
		{
			return m_gamma_spline.gamma();
		}

		public double y(double x)
		{
			return m_gamma_spline.y(x);
		}

		//public double operator() (double x) { return m_gamma_spline.y(x); }
		public gamma_spline get_gamma_spline()
		{
			return m_gamma_spline;
		}

		// Vertex soutce interface
		public override int num_paths()
		{
			return 5;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble backgroundRect = new RectangleDouble(LocalBounds.Left - m_border_extra,
				LocalBounds.Bottom - m_border_extra,
				LocalBounds.Right + m_border_extra,
				LocalBounds.Top + m_border_extra);
			graphics2D.FillRectangle(backgroundRect, m_background_color);

			PathStorage border = new PathStorage();
			border.LineTo(LocalBounds.Left, LocalBounds.Bottom);
			border.LineTo(LocalBounds.Right, LocalBounds.Bottom);
			border.LineTo(LocalBounds.Right, LocalBounds.Top);
			border.LineTo(LocalBounds.Left, LocalBounds.Top);
			border.LineTo(LocalBounds.Left + m_border_width, LocalBounds.Bottom + m_border_width);
			border.LineTo(LocalBounds.Left + m_border_width, LocalBounds.Top - m_border_width);
			border.LineTo(LocalBounds.Right - m_border_width, LocalBounds.Top - m_border_width);
			border.LineTo(LocalBounds.Right - m_border_width, LocalBounds.Bottom + m_border_width);

			graphics2D.Render(border, m_border_color);

			rewind(0);
			graphics2D.Render(this, m_curve_color);
			rewind(1);
			graphics2D.Render(this, m_grid_color);
			rewind(2);
			graphics2D.Render(this, m_inactive_pnt_color);
			rewind(3);
			graphics2D.Render(this, m_active_pnt_color);
			rewind(4);
			graphics2D.Render(this, m_text_color);

			base.OnDraw(graphics2D);
		}

		public override IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}

		public override void rewind(int idx)
		{
			double kx1, ky1, kx2, ky2;
			string tbuf;

			m_idx = idx;

			switch (idx)
			{
				default:

				case 0:                 // Curve
					m_gamma_spline.box(m_xs1, m_ys1, m_xs2, m_ys2);
					m_curve_poly.width(m_curve_width);
					m_curve_poly.rewind(0);
					break;

				case 1:                 // Grid
					m_vertex = 0;
					gridVertexX[0] = m_xs1;
					gridVertexY[0] = (m_ys1 + m_ys2) * 0.5 - m_grid_width * 0.5;
					gridVertexX[1] = m_xs2;
					gridVertexY[1] = (m_ys1 + m_ys2) * 0.5 - m_grid_width * 0.5;
					gridVertexX[2] = m_xs2;
					gridVertexY[2] = (m_ys1 + m_ys2) * 0.5 + m_grid_width * 0.5;
					gridVertexX[3] = m_xs1;
					gridVertexY[3] = (m_ys1 + m_ys2) * 0.5 + m_grid_width * 0.5;
					gridVertexX[4] = (m_xs1 + m_xs2) * 0.5 - m_grid_width * 0.5;
					gridVertexY[4] = m_ys1;
					gridVertexX[5] = (m_xs1 + m_xs2) * 0.5 - m_grid_width * 0.5;
					gridVertexY[5] = m_ys2;
					gridVertexX[6] = (m_xs1 + m_xs2) * 0.5 + m_grid_width * 0.5;
					gridVertexY[6] = m_ys2;
					gridVertexX[7] = (m_xs1 + m_xs2) * 0.5 + m_grid_width * 0.5;
					gridVertexY[7] = m_ys1;
					calc_points();
					gridVertexX[8] = m_xs1;
					gridVertexY[8] = m_yp1 - m_grid_width * 0.5;
					gridVertexX[9] = m_xp1 - m_grid_width * 0.5;
					gridVertexY[9] = m_yp1 - m_grid_width * 0.5;
					gridVertexX[10] = m_xp1 - m_grid_width * 0.5;
					gridVertexY[10] = m_ys1;
					gridVertexX[11] = m_xp1 + m_grid_width * 0.5;
					gridVertexY[11] = m_ys1;
					gridVertexX[12] = m_xp1 + m_grid_width * 0.5;
					gridVertexY[12] = m_yp1 + m_grid_width * 0.5;
					gridVertexX[13] = m_xs1;
					gridVertexY[13] = m_yp1 + m_grid_width * 0.5;
					gridVertexX[14] = m_xs2;
					gridVertexY[14] = m_yp2 + m_grid_width * 0.5;
					gridVertexX[15] = m_xp2 + m_grid_width * 0.5;
					gridVertexY[15] = m_yp2 + m_grid_width * 0.5;
					gridVertexX[16] = m_xp2 + m_grid_width * 0.5;
					gridVertexY[16] = m_ys2;
					gridVertexX[17] = m_xp2 - m_grid_width * 0.5;
					gridVertexY[17] = m_ys2;
					gridVertexX[18] = m_xp2 - m_grid_width * 0.5;
					gridVertexY[18] = m_yp2 - m_grid_width * 0.5;
					gridVertexX[19] = m_xs2;
					gridVertexY[19] = m_yp2 - m_grid_width * 0.5;
					break;

				case 2:                 // Point1
					calc_points();
					if (m_p1_active) m_ellipse.init(m_xp2, m_yp2, m_point_size, m_point_size, 32);
					else m_ellipse.init(m_xp1, m_yp1, m_point_size, m_point_size, 32);
					break;

				case 3:                 // Point2
					calc_points();
					if (m_p1_active) m_ellipse.init(m_xp1, m_yp1, m_point_size, m_point_size, 32);
					else m_ellipse.init(m_xp2, m_yp2, m_point_size, m_point_size, 32);
					break;

				case 4:                 // Text
					m_gamma_spline.values(out kx1, out ky1, out kx2, out ky2);
					tbuf = string.Format("{0:F3} {1:F3} {2:F3} {3:F3}", kx1, ky1, kx2, ky2);
					m_text.text(tbuf);
					m_text.SetFontSize(m_text_height);
					m_text.start_point(m_xt1 + m_border_width * 2.0, (m_yt1 + m_yt2) * 0.5 - m_text_height * 0.5);
					m_text_poly.width(m_text_thickness);
					m_text_poly.line_join(LineJoin.Round);
					m_text_poly.line_cap(LineCap.Round);
					m_text_poly.rewind(0);
					break;
			}
		}

		public override ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandLineTo;
			switch (m_idx)
			{
				case 0:
					cmd = m_curve_poly.vertex(out x, out y);
					break;

				case 1:
					if (m_vertex == 0 ||
					   m_vertex == 4 ||
					   m_vertex == 8 ||
					   m_vertex == 14) cmd = ShapePath.FlagsAndCommand.CommandMoveTo;

					if (m_vertex >= 20) cmd = ShapePath.FlagsAndCommand.CommandStop;
					x = gridVertexX[m_vertex];
					y = gridVertexY[m_vertex];
					m_vertex++;
					break;

				case 2:                 // Point1
				case 3:                 // Point2
					cmd = m_ellipse.vertex(out x, out y);
					break;

				case 4:
					cmd = m_text_poly.vertex(out x, out y);
					break;

				default:
					cmd = ShapePath.FlagsAndCommand.CommandStop;
					break;
			}

			return cmd;
		}

		private void calc_spline_box()
		{
			m_xs1 = m_xc1 + m_border_width;
			m_ys1 = m_yc1 + m_border_width;
			m_xs2 = m_xc2 - m_border_width;
			m_ys2 = m_yc2 - m_border_width * 0.5;
		}

		private void calc_points()
		{
			double kx1, ky1, kx2, ky2;
			m_gamma_spline.values(out kx1, out ky1, out kx2, out ky2);
			m_xp1 = m_xs1 + (m_xs2 - m_xs1) * kx1 * 0.25;
			m_yp1 = m_ys1 + (m_ys2 - m_ys1) * ky1 * 0.25;
			m_xp2 = m_xs2 - (m_xs2 - m_xs1) * kx2 * 0.25;
			m_yp2 = m_ys2 - (m_ys2 - m_ys1) * ky2 * 0.25;
		}

		private void calc_values()
		{
			double kx1, ky1, kx2, ky2;

			kx1 = (m_xp1 - m_xs1) * 4.0 / (m_xs2 - m_xs1);
			ky1 = (m_yp1 - m_ys1) * 4.0 / (m_ys2 - m_ys1);
			kx2 = (m_xs2 - m_xp2) * 4.0 / (m_xs2 - m_xs1);
			ky2 = (m_ys2 - m_yp2) * 4.0 / (m_ys2 - m_ys1);
			m_gamma_spline.values(kx1, ky1, kx2, ky2);
		}
	}
}