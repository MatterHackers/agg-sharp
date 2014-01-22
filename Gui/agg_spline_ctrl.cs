//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
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
//
// classes spline_ctrl_impl, spline_ctrl
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{

    //------------------------------------------------------------------------
    // Class that can be used to create an interactive control to set up 
    // gamma arrays.
    //------------------------------------------------------------------------
    public class spline_ctrl : SimpleVertexSourceWidget
    {
        RGBA_Bytes m_background_color;
        RGBA_Bytes m_border_color;
        RGBA_Bytes m_curve_color;
        RGBA_Bytes m_inactive_pnt_color;
        RGBA_Bytes m_active_pnt_color;

        int m_num_pnt;
        double[] m_xp = new double[32];
        double[] m_yp = new double[32];
        bspline m_spline = new bspline();
        double[] m_spline_values = new double[256];
        byte[] m_spline_values8 = new byte[256];
        double m_border_width;
        double m_border_extra;
        double m_curve_width;
        double m_point_size;
        double m_xs1;
        double m_ys1;
        double m_xs2;
        double m_ys2;
        VertexSource.PathStorage m_curve_pnt;
        Stroke m_curve_poly;
        VertexSource.Ellipse m_ellipse;
        int m_idx;
        int m_vertex;
        double[] m_vx = new double[32];
        double[] m_vy = new double[32];
        int m_active_pnt;
        int m_move_pnt;
        double m_pdx;
        double m_pdy;
        Transform.Affine m_mtx = Affine.NewIdentity();

        public spline_ctrl(Vector2 location, Vector2 size, int num_pnt)
            : base(location, false)
        {
            LocalBounds = new RectangleDouble(0, 0, size.x, size.y);
            m_curve_pnt = new PathStorage();
            m_curve_poly = new Stroke(m_curve_pnt);
            m_ellipse = new Ellipse();

            m_background_color = new RGBA_Bytes(1.0, 1.0, 0.9);
            m_border_color = new RGBA_Bytes(0.0, 0.0, 0.0);
            m_curve_color = new RGBA_Bytes(0.0, 0.0, 0.0);
            m_inactive_pnt_color = new RGBA_Bytes(0.0, 0.0, 0.0);
            m_active_pnt_color = new RGBA_Bytes(1.0, 0.0, 0.0);

            m_num_pnt = (num_pnt);
            m_border_width = (1.0);
            m_border_extra = (0.0);
            m_curve_width = (1.0);
            m_point_size = (3.0);
            m_curve_poly = new Stroke(m_curve_pnt);
            m_idx = (0);
            m_vertex = (0);
            m_active_pnt = (-1);
            m_move_pnt = (-1);
            m_pdx = (0.0);
            m_pdy = (0.0);
            if (m_num_pnt < 4) m_num_pnt = 4;
            if (m_num_pnt > 32) m_num_pnt = 32;

            for (int i = 0; i < m_num_pnt; i++)
            {
                m_xp[i] = (double)(i) / (double)(m_num_pnt - 1);
                m_yp[i] = 0.5;
            }
            calc_spline_box();
            update_spline();
            {
                m_spline.init((int)m_num_pnt, m_xp, m_yp);
                for (int i = 0; i < 256; i++)
                {
                    m_spline_values[i] = m_spline.get((double)(i) / 255.0);
                    if (m_spline_values[i] < 0.0) m_spline_values[i] = 0.0;
                    if (m_spline_values[i] > 1.0) m_spline_values[i] = 1.0;
                    m_spline_values8[i] = (byte)(m_spline_values[i] * 255.0);
                }
            }

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
            LocalBounds = new RectangleDouble(-m_border_extra, -m_border_extra, Width + m_border_extra, Height + m_border_extra);
        }

        public void curve_width(double t) { m_curve_width = t; }
        public void point_size(double s) { m_point_size = s; }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            int i;
            for (i = 0; i < m_num_pnt; i++)
            {
                double xp = calc_xp(i);
                double yp = calc_yp(i);
                if (agg_math.calc_distance(x, y, xp, yp) <= m_point_size + 1)
                {
                    m_pdx = xp - x;
                    m_pdy = yp - y;
                    m_active_pnt = m_move_pnt = (int)(i);
                }
            }

            base.OnMouseDown(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            if (m_move_pnt >= 0)
            {
                m_move_pnt = -1;
            }

            base.OnMouseUp(mouseEvent);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;

            if (m_move_pnt >= 0)
            {
                double xp = x + m_pdx;
                double yp = y + m_pdy;

                set_xp((int)m_move_pnt, (xp - m_xs1) / (m_xs2 - m_xs1));
                set_yp((int)m_move_pnt, (yp - m_ys1) / (m_ys2 - m_ys1));

                update_spline();
                Invalidate();
            }

            base.OnMouseMove(mouseEvent);
        }

        public override void OnKeyDown(KeyEventArgs keyEvent)
        {
            double kx = 0.0;
            double ky = 0.0;
            bool ret = false;
            if (m_active_pnt >= 0)
            {
                kx = m_xp[m_active_pnt];
                ky = m_yp[m_active_pnt];
                if (keyEvent.KeyCode == Keys.Left) { kx -= 0.001; ret = true; }
                if (keyEvent.KeyCode == Keys.Right) { kx += 0.001; ret = true; }
                if (keyEvent.KeyCode == Keys.Down) { ky -= 0.001; ret = true; }
                if (keyEvent.KeyCode == Keys.Up) { ky += 0.001; ret = true; }
            }
            if (ret)
            {
                set_xp((int)m_active_pnt, kx);
                set_yp((int)m_active_pnt, ky);
                update_spline();
                keyEvent.Handled = true;
                Invalidate();
            }

            base.OnKeyDown(keyEvent);
        }

        public void active_point(int i)
        {
            m_active_pnt = i;
        }

        public double[] spline() { return m_spline_values; }
        public byte[] spline8() { return m_spline_values8; }
        public double value(double x)
        {
            x = m_spline.get(x);
            if (x < 0.0) x = 0.0;
            if (x > 1.0) x = 1.0;
            return x;
        }


        public void value(int idx, double y)
        {
            if (idx < m_num_pnt)
            {
                set_yp(idx, y);
            }
        }

        public void point(int idx, double x, double y)
        {
            if (idx < m_num_pnt)
            {
                set_xp(idx, x);
                set_yp(idx, y);
            }
        }

        public void x(int idx, double x) { m_xp[idx] = x; }
        public void y(int idx, double y) { m_yp[idx] = y; }
        public double x(int idx) { return m_xp[idx]; }
        public double y(int idx) { return m_yp[idx]; }
        public void update_spline()
        {
            m_spline.init((int)m_num_pnt, m_xp, m_yp);
            for (int i = 0; i < 256; i++)
            {
                m_spline_values[i] = m_spline.get((double)(i) / 255.0);
                if (m_spline_values[i] < 0.0) m_spline_values[i] = 0.0;
                if (m_spline_values[i] > 1.0) m_spline_values[i] = 1.0;
                m_spline_values8[i] = (byte)(m_spline_values[i] * 255.0);
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            int index = 0;
            rewind(index);
            graphics2D.Render(this, m_background_color);
            rewind(++index);
            graphics2D.Render(this, m_border_color);
            rewind(++index);
            graphics2D.Render(this, m_curve_color);
            rewind(++index);
            graphics2D.Render(this, m_inactive_pnt_color);
            rewind(++index);
            graphics2D.Render(this, m_active_pnt_color);

            base.OnDraw(graphics2D);
        }

        // Vertex soutce interface
        public override int num_paths() { return 5; }

        public override IEnumerable<VertexData> VertexIterator()
        {
            throw new NotImplementedException();
        }

        public override void rewind(int idx)
        {
            m_idx = idx;

            switch (idx)
            {
                default:

                case 0:                 // Background
                    m_vertex = 0;
                    m_vx[0] = -m_border_extra;
                    m_vy[0] = -m_border_extra;
                    m_vx[1] = Width + m_border_extra;
                    m_vy[1] = -m_border_extra;
                    m_vx[2] = Width + m_border_extra;
                    m_vy[2] = Height + m_border_extra;
                    m_vx[3] = -m_border_extra;
                    m_vy[3] = Height + m_border_extra;
                    break;

                case 1:                 // Border
                    m_vertex = 0;
                    m_vx[0] = 0;
                    m_vy[0] = 0;
                    m_vx[1] = Width - m_border_extra * 2;
                    m_vy[1] = 0;
                    m_vx[2] = Width - m_border_extra * 2;
                    m_vy[2] = Height - m_border_extra * 2;
                    m_vx[3] = 0;
                    m_vy[3] = Height - m_border_extra * 2;
                    m_vx[4] = +m_border_width;
                    m_vy[4] = +m_border_width;
                    m_vx[5] = +m_border_width;
                    m_vy[5] = Height - m_border_width - m_border_extra * 2;
                    m_vx[6] = Width - m_border_width - m_border_extra * 2;
                    m_vy[6] = Height - m_border_width - m_border_extra * 2;
                    m_vx[7] = Width - m_border_width - m_border_extra * 2;
                    m_vy[7] = +m_border_width;
                    break;

                case 2:                 // Curve
                    calc_curve();
                    m_curve_poly.width(m_curve_width);
                    m_curve_poly.rewind(0);
                    break;


                case 3:                 // Inactive points
                    m_curve_pnt.remove_all();
                    for (int i = 0; i < m_num_pnt; i++)
                    {
                        if (i != m_active_pnt)
                        {
                            m_ellipse.init(calc_xp(i), calc_yp(i),
                                           m_point_size, m_point_size, 32);
                            m_curve_pnt.concat_path(m_ellipse);
                        }
                    }
                    m_curve_poly.rewind(0);
                    break;


                case 4:                 // Active point
                    m_curve_pnt.remove_all();
                    if (m_active_pnt >= 0)
                    {
                        m_ellipse.init(calc_xp(m_active_pnt), calc_yp(m_active_pnt),
                                       m_point_size, m_point_size, 32);

                        m_curve_pnt.concat_path(m_ellipse);
                    }
                    m_curve_poly.rewind(0);
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
                    if (m_vertex == 0) cmd = ShapePath.FlagsAndCommand.CommandMoveTo;
                    if (m_vertex >= 4) cmd = ShapePath.FlagsAndCommand.CommandStop;
                    x = m_vx[m_vertex];
                    y = m_vy[m_vertex];
                    m_vertex++;
                    break;

                case 1:
                    if (m_vertex == 0 || m_vertex == 4) cmd = ShapePath.FlagsAndCommand.CommandMoveTo;
                    if (m_vertex >= 8) cmd = ShapePath.FlagsAndCommand.CommandStop;
                    x = m_vx[m_vertex];
                    y = m_vy[m_vertex];
                    m_vertex++;
                    break;

                case 2:
                    cmd = m_curve_poly.vertex(out x, out y);
                    break;

                case 3:
                case 4:
                    cmd = m_curve_pnt.vertex(out x, out y);
                    break;

                default:
                    cmd = ShapePath.FlagsAndCommand.CommandStop;
                    break;
            }

            if (!ShapePath.is_stop(cmd))
            {
                //OriginRelativeParentTransform.transform(ref x, ref y);
            }

            return cmd;
        }


        private void calc_spline_box()
        {
            m_xs1 = LocalBounds.Left + m_border_width;
            m_ys1 = LocalBounds.Bottom + m_border_width;
            m_xs2 = LocalBounds.Right - m_border_width;
            m_ys2 = LocalBounds.Top - m_border_width;
        }

        private void calc_curve()
        {
            int i;
            m_curve_pnt.remove_all();
            m_curve_pnt.MoveTo(m_xs1, m_ys1 + (m_ys2 - m_ys1) * m_spline_values[0]);
            for (i = 1; i < 256; i++)
            {
                m_curve_pnt.LineTo(m_xs1 + (m_xs2 - m_xs1) * (double)(i) / 255.0,
                                    m_ys1 + (m_ys2 - m_ys1) * m_spline_values[i]);
            }
        }

        private double calc_xp(int idx)
        {
            return m_xs1 + (m_xs2 - m_xs1) * m_xp[idx];
        }

        private double calc_yp(int idx)
        {
            return m_ys1 + (m_ys2 - m_ys1) * m_yp[idx];
        }

        private void set_xp(int idx, double val)
        {
            if (val < 0.0) val = 0.0;
            if (val > 1.0) val = 1.0;

            if (idx == 0)
            {
                val = 0.0;
            }
            else if (idx == m_num_pnt - 1)
            {
                val = 1.0;
            }
            else
            {
                if (val < m_xp[idx - 1] + 0.001) val = m_xp[idx - 1] + 0.001;
                if (val > m_xp[idx + 1] - 0.001) val = m_xp[idx + 1] - 0.001;
            }
            m_xp[idx] = val;
        }

        private void set_yp(int idx, double val)
        {
            if (val < 0.0) val = 0.0;
            if (val > 1.0) val = 1.0;
            m_yp[idx] = val;
        }

        // Set colors
        public void background_color(RGBA_Bytes c) { m_background_color = c; }
        public void border_color(RGBA_Bytes c) { m_border_color = c; }
        public void curve_color(RGBA_Bytes c) { m_curve_color = c; }
        public void inactive_pnt_color(RGBA_Bytes c) { m_inactive_pnt_color = c; }
        public void active_pnt_color(RGBA_Bytes c) { m_active_pnt_color = c; }
        public override IColorType color(int i)
        {
            switch (i)
            {
                case 0:
                    return m_background_color;

                case 1:
                    return m_border_color;

                case 2:
                    return m_curve_color;

                case 3:
                    return m_inactive_pnt_color;

                case 4:
                    return m_active_pnt_color;

                default:
                    throw new System.IndexOutOfRangeException("You asked for a color out of range.");
            }
        }
    }
}
