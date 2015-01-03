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
// classes bezier_ctrl_impl, bezier_ctrl
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    //--------------------------------------------------------bezier_ctrl_impl
    public class bezier_ctrl_impl : SimpleVertexSourceWidget
    {
        Curve4 m_curve = new Curve4();
        VertexSource.Ellipse m_ellipse;
        Stroke m_stroke;
        polygon_ctrl_impl m_poly;
        int m_idx;

        public bezier_ctrl_impl()
            : base(new Vector2(0, 0))
        {
            m_stroke = new Stroke(m_curve);
            m_poly = new polygon_ctrl_impl(4, 5.0);
            m_idx = (0);
            m_ellipse = new MatterHackers.Agg.VertexSource.Ellipse();

            m_poly.in_polygon_check(false);
            m_poly.SetXN(0, 100.0);
            m_poly.SetYN(0, 0.0);
            m_poly.SetXN(1, 100.0);
            m_poly.SetYN(1, 50.0);
            m_poly.SetXN(2, 50.0);
            m_poly.SetYN(2, 100.0);
            m_poly.SetXN(3, 0.0);
            m_poly.SetYN(3, 100.0);
        }

        public void curve(double x1, double y1,
                                     double x2, double y2,
                                     double x3, double y3,
                                     double x4, double y4)
        {
            m_poly.SetXN(0, x1);
            m_poly.SetYN(0, y1);
            m_poly.SetXN(1, x2);
            m_poly.SetYN(1, y2);
            m_poly.SetXN(2, x3);
            m_poly.SetYN(2, y3);
            m_poly.SetXN(3, x4);
            m_poly.SetYN(3, y4);
            curve();
        }

        public Curve4 curve()
        {
            m_curve.init(m_poly.GetXN(0), m_poly.GetYN(0),
                         m_poly.GetXN(1), m_poly.GetYN(1),
                         m_poly.GetXN(2), m_poly.GetYN(2),
                         m_poly.GetXN(3), m_poly.GetYN(3));
            return m_curve;
        }

        public double x1() { return m_poly.GetXN(0); }
        public double y1() { return m_poly.GetYN(0); }
        public double x2() { return m_poly.GetXN(1); }
        public double y2() { return m_poly.GetYN(1); }
        public double x3() { return m_poly.GetXN(2); }
        public double y3() { return m_poly.GetYN(2); }
        public double x4() { return m_poly.GetXN(3); }
        public double y4() { return m_poly.GetYN(3); }

        public void x1(double x) { m_poly.SetXN(0, x); }
        public void y1(double y) { m_poly.SetYN(0, y); }
        public void x2(double x) { m_poly.SetXN(1, x); }
        public void y2(double y) { m_poly.SetYN(1, y); }
        public void x3(double x) { m_poly.SetXN(2, x); }
        public void y3(double y) { m_poly.SetYN(2, y); }
        public void x4(double x) { m_poly.SetXN(3, x); }
        public void y4(double y) { m_poly.SetYN(3, y); }

        public void line_width(double w) { m_stroke.width(w); }
        public double line_width() { return m_stroke.width(); }

        public void point_radius(double r) { m_poly.point_radius(r); }
        public double point_radius() { return m_poly.point_radius(); }

        /*
        public override bool InRect(double x, double y)
        {
            return false;
        }
         */

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            ParentToChildTransform.inverse_transform(ref x, ref y);
            m_poly.OnMouseDown(new MouseEventArgs(mouseEvent, x, y));
            Invalidate();
            base.OnMouseDown(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            m_poly.OnMouseUp(new MouseEventArgs(mouseEvent, x, y));
            Invalidate();
            base.OnMouseUp(mouseEvent);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            ParentToChildTransform.inverse_transform(ref x, ref y);
            m_poly.OnMouseMove(new MouseEventArgs(mouseEvent, x, y));
            Invalidate();
            BoundsRelativeToParent = m_poly.BoundsRelativeToParent;
            Invalidate();
            base.OnMouseMove(mouseEvent);
        }

        public override void OnKeyDown(KeyEventArgs keyEvent)
        {
            m_poly.OnKeyDown(keyEvent);
            base.OnKeyDown(keyEvent);
        }

        // Vertex source interface
        public override int num_paths() { return 7; }
        public override IEnumerable<VertexData> Vertices()
        {
            throw new NotImplementedException();
        }

        public override void rewind(int idx)
        {
            m_poly.rewind(0);
            m_idx = idx;

            m_curve.approximation_scale(1);
            switch (idx)
            {
                default:
                case 0:                 // Control line 1
                    m_curve.init(m_poly.GetXN(0), m_poly.GetYN(0),
                                (m_poly.GetXN(0) + m_poly.GetXN(1)) * 0.5,
                                (m_poly.GetYN(0) + m_poly.GetYN(1)) * 0.5,
                                (m_poly.GetXN(0) + m_poly.GetXN(1)) * 0.5,
                                (m_poly.GetYN(0) + m_poly.GetYN(1)) * 0.5,
                                 m_poly.GetXN(1), m_poly.GetYN(1));
                    m_stroke.rewind(0);
                    break;

                case 1:                 // Control line 2
                    m_curve.init(m_poly.GetXN(2), m_poly.GetYN(2),
                                (m_poly.GetXN(2) + m_poly.GetXN(3)) * 0.5,
                                (m_poly.GetYN(2) + m_poly.GetYN(3)) * 0.5,
                                (m_poly.GetXN(2) + m_poly.GetXN(3)) * 0.5,
                                (m_poly.GetYN(2) + m_poly.GetYN(3)) * 0.5,
                                 m_poly.GetXN(3), m_poly.GetYN(3));
                    m_stroke.rewind(0);
                    break;

                case 2:                 // Curve itself
                    m_curve.init(m_poly.GetXN(0), m_poly.GetYN(0),
                                 m_poly.GetXN(1), m_poly.GetYN(1),
                                 m_poly.GetXN(2), m_poly.GetYN(2),
                                 m_poly.GetXN(3), m_poly.GetYN(3));
                    m_stroke.rewind(0);
                    break;

                case 3:                 // Point 1
                    m_ellipse.init(m_poly.GetXN(0), m_poly.GetYN(0), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;

                case 4:                 // Point 2
                    m_ellipse.init(m_poly.GetXN(1), m_poly.GetYN(1), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;

                case 5:                 // Point 3
                    m_ellipse.init(m_poly.GetXN(2), m_poly.GetYN(2), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;

                case 6:                 // Point 4
                    m_ellipse.init(m_poly.GetXN(3), m_poly.GetYN(3), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;
            }
        }
        public override ShapePath.FlagsAndCommand vertex(out double x, out double y)
        {
            x = 0;
            y = 0;
            ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandStop;
            switch (m_idx)
            {
                case 0:
                case 1:
                case 2:
                    cmd = m_stroke.vertex(out x, out y);
                    break;

                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    cmd = m_ellipse.vertex(out x, out y);
                    break;
            }

            if (!ShapePath.is_stop(cmd))
            {
                ParentToChildTransform.transform(ref x, ref y);
            }
            return cmd;
        }
    };



    //----------------------------------------------------------bezier_ctrl
    //template<class IColorType> 
    public class bezier_ctrl : bezier_ctrl_impl
    {
        RGBA_Floats m_color;

        public bezier_ctrl()
        {
            m_color = new RGBA_Floats(0.0, 0.0, 0.0);
        }

        public void line_color(IColorType c) { m_color = c.GetAsRGBA_Floats(); }
        public override IColorType color(int i) { return m_color; }
    };


    //--------------------------------------------------------curve3_ctrl_impl
    public class curve3_ctrl_impl : SimpleVertexSourceWidget
    {
        Curve3 m_curve;
        VertexSource.Ellipse m_ellipse;
        Stroke m_stroke;
        polygon_ctrl_impl m_poly;
        int m_idx;

        public curve3_ctrl_impl()
            : base(new Vector2())
        {
            m_stroke = new Stroke(m_curve);
            m_poly = new polygon_ctrl_impl(3, 5.0);
            m_idx = 0;
            m_curve = new Curve3();
            m_ellipse = new MatterHackers.Agg.VertexSource.Ellipse();

            m_poly.in_polygon_check(false);
            m_poly.SetXN(0, 100.0);
            m_poly.SetYN(0, 0.0);
            m_poly.SetXN(1, 100.0);
            m_poly.SetYN(1, 50.0);
            m_poly.SetXN(2, 50.0);
            m_poly.SetYN(2, 100.0);
        }

        public void curve(double x1, double y1,
                                     double x2, double y2,
                                     double x3, double y3)
        {
            m_poly.SetXN(0, x1);
            m_poly.SetYN(0, y1);
            m_poly.SetXN(1, x2);
            m_poly.SetYN(1, y2);
            m_poly.SetXN(2, x3);
            m_poly.SetYN(2, y3);
            curve();
        }

        public Curve3 curve()
        {
            m_curve.init(m_poly.GetXN(0), m_poly.GetYN(0),
                         m_poly.GetXN(1), m_poly.GetYN(1),
                         m_poly.GetXN(2), m_poly.GetYN(2));
            return m_curve;
        }

        double x1() { return m_poly.GetXN(0); }
        double y1() { return m_poly.GetYN(0); }
        double x2() { return m_poly.GetXN(1); }
        double y2() { return m_poly.GetYN(1); }
        double x3() { return m_poly.GetXN(2); }
        double y3() { return m_poly.GetYN(2); }

        void x1(double x) { m_poly.SetXN(0, x); }
        void y1(double y) { m_poly.SetYN(0, y); }
        void x2(double x) { m_poly.SetXN(1, x); }
        void y2(double y) { m_poly.SetYN(1, y); }
        void x3(double x) { m_poly.SetXN(2, x); }
        void y3(double y) { m_poly.SetYN(2, y); }

        void line_width(double w) { m_stroke.width(w); }
        double line_width() { return m_stroke.width(); }

        void point_radius(double r) { m_poly.point_radius(r); }
        double point_radius() { return m_poly.point_radius(); }

        public override bool PositionWithinLocalBounds(double x, double y)
        {
            return false;
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            ParentToChildTransform.inverse_transform(ref x, ref y);
            m_poly.OnMouseDown(new MouseEventArgs(mouseEvent, x, y));
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            m_poly.OnMouseUp(new MouseEventArgs(mouseEvent, x, y));
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            ParentToChildTransform.inverse_transform(ref x, ref y);
            m_poly.OnMouseMove(new MouseEventArgs(mouseEvent, x, y));
        }

        public override void OnKeyDown(KeyEventArgs keyEvent)
        {
            m_poly.OnKeyDown(keyEvent);
        }

        // Vertex source interface
        public override int num_paths() { return 6; }

        public override IEnumerable<VertexData> Vertices()
        {
            throw new NotImplementedException();
        }

        public override void rewind(int idx)
        {
            m_idx = idx;

            switch (idx)
            {
                default:
                case 0:                 // Control line
                    m_curve.init(m_poly.GetXN(0), m_poly.GetYN(0),
                                (m_poly.GetXN(0) + m_poly.GetXN(1)) * 0.5,
                                (m_poly.GetYN(0) + m_poly.GetYN(1)) * 0.5,
                                 m_poly.GetXN(1), m_poly.GetYN(1));
                    m_stroke.rewind(0);
                    break;

                case 1:                 // Control line 2
                    m_curve.init(m_poly.GetXN(1), m_poly.GetYN(1),
                                (m_poly.GetXN(1) + m_poly.GetXN(2)) * 0.5,
                                (m_poly.GetYN(1) + m_poly.GetYN(2)) * 0.5,
                                 m_poly.GetXN(2), m_poly.GetYN(2));
                    m_stroke.rewind(0);
                    break;

                case 2:                 // Curve itself
                    m_curve.init(m_poly.GetXN(0), m_poly.GetYN(0),
                                 m_poly.GetXN(1), m_poly.GetYN(1),
                                 m_poly.GetXN(2), m_poly.GetYN(2));
                    m_stroke.rewind(0);
                    break;

                case 3:                 // Point 1
                    m_ellipse.init(m_poly.GetXN(0), m_poly.GetYN(0), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;

                case 4:                 // Point 2
                    m_ellipse.init(m_poly.GetXN(1), m_poly.GetYN(1), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;

                case 5:                 // Point 3
                    m_ellipse.init(m_poly.GetXN(2), m_poly.GetYN(2), point_radius(), point_radius(), 20);
                    m_ellipse.rewind(0);
                    break;
            }
        }

        public override ShapePath.FlagsAndCommand vertex(out double x, out double y)
        {
            x = 0;
            y = 0;
            ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandStop;
            switch (m_idx)
            {
                case 0:
                case 1:
                case 2:
                    cmd = m_stroke.vertex(out x, out y);
                    break;

                case 3:
                case 4:
                case 5:
                case 6:
                    cmd = m_ellipse.vertex(out x, out y);
                    break;
            }

            if (!ShapePath.is_stop(cmd))
            {
                ParentToChildTransform.transform(ref x, ref y);
            }
            return cmd;
        }

    };

    //----------------------------------------------------------curve3_ctrl
    //template<class IColorType> 
    public class curve3_ctrl : curve3_ctrl_impl
    {
        IColorType m_color;

        public curve3_ctrl()
        {
            m_color = new RGBA_Floats(0.0, 0.0, 0.0);
        }

        public void line_color(IColorType c) { m_color = c; }
        public override IColorType color(int i) { return m_color; }
    };
}
