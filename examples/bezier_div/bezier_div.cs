using System;
using agg.ui;

namespace agg
{
    public class bezier_div_application : agg.ui.win32.platform_support
    {
        rgba8 m_ctrl_color;
        bezier_ctrl m_curve1;
        ui.slider_ctrl m_angle_tolerance;
        ui.slider_ctrl m_approximation_scale;
        ui.slider_ctrl m_cusp_limit;
        ui.slider_ctrl m_width;
        ui.cbox_ctrl   m_show_points;
        ui.cbox_ctrl   m_show_outline;
        ui.rbox_ctrl   m_curve_type;
        ui.rbox_ctrl   m_case_type;
        ui.rbox_ctrl   m_inner_join;
        ui.rbox_ctrl   m_line_join;
        ui.rbox_ctrl   m_line_cap;

        int m_cur_case_type;

//typedef pixfmt_bgr24 pixfmt;
        void bezier4_point(double x1, double y1, double x2, double y2,
                           double x3, double y3, double x4, double y4,
                           double mu,
                           out double x, out double y)
        {
           double mum1, mum13, mu3;

           mum1 = 1 - mu;
           mum13 = mum1 * mum1 * mum1;
           mu3 = mu * mu * mu;

           x = mum13*x1 + 3*mu*mum1*mum1*x2 + 3*mu*mu*mum1*x3 + mu3*x4;
           y = mum13*y1 + 3*mu*mum1*mum1*y2 + 3*mu*mu*mum1*y3 + mu3*y4;
        }
    //typedef renderer_base<pixfmt> renderer_base;
    //typedef renderer_scanline_aa_solid<renderer_base> renderer_scanline;
    //typedef rasterizer_scanline_aa<> rasterizer_scanline;
    //typedef scanline_u8 scanline;


        public bezier_div_application(pix_format_e format, platform_support_abstract.ERenderOrigin RenderOrigin)
            : base(format, RenderOrigin)
    {
        m_ctrl_color = new rgba8(new rgba(0, 0.3, 0.5, 0.8));
        m_angle_tolerance    = new slider_ctrl(5.0,       5.0, 240.0,       12.0);
        m_approximation_scale = new slider_ctrl(5.0,    17+5.0, 240.0,    17+12.0);
        m_cusp_limit         = new slider_ctrl(5.0, 17+17+5.0, 240.0, 17+17+12.0);
        m_width              = new slider_ctrl(245.0,     5.0, 495.0,       12.0);
        m_show_points        = new cbox_ctrl(250.0, 15+5, "Show Points");
        m_show_outline       = new cbox_ctrl(250.0, 30+5, "Show Stroke Outline");
        m_curve_type         = new rbox_ctrl(535.0,   5.0, 535.0+115.0,   55.0);
        m_case_type          = new rbox_ctrl(535.0,  60.0, 535.0+115.0,   195.0);
        m_inner_join         = new rbox_ctrl(535.0, 200.0, 535.0+115.0,   290.0);
        m_line_join          = new rbox_ctrl(535.0, 295.0, 535.0+115.0,   385.0);
        m_line_cap           = new rbox_ctrl(535.0, 395.0, 535.0+115.0,   455.0);
        m_cur_case_type = (-1);

        m_curve1.line_color(m_ctrl_color);

        m_curve1.curve(170, 424, 13, 87, 488, 423, 26, 333);
        //m_curve1.curve(26.000, 333.000, 276.000, 126.000, 402.000, 479.000, 26.000, 333.000); // Loop with p1==p4
        //m_curve1.curve(378.000, 439.000, 378.000, 497.000, 487.000, 432.000, 14.000, 338.000); // Narrow loop
        //m_curve1.curve(288.000, 283.000, 232.000, 89.000, 66.000, 197.000, 456.000, 241.000); // Loop
        //m_curve1.curve(519.000, 142.000, 97.000, 147.000, 69.000, 147.000, 30.000, 144.000); // Almost straight
        //m_curve1.curve(100, 100, 200, 100, 100, 200, 200, 200); // A "Z" case
        //m_curve1.curve(150, 150, 350, 150, 150, 150, 350, 150); // Degenerate
        //m_curve1.curve(409, 330, 300, 200, 200, 200, 401, 263); // Strange cusp
        //m_curve1.curve(129, 233, 172, 320, 414, 253, 344, 236); // Curve cap
        //m_curve1.curve(100,100, 100,200, 100,100, 110,100); // A "boot"
        //m_curve1.curve(225, 150, 60, 150, 460, 150, 295, 150); // 2----1----4----3
        //m_curve1.curve(162.2, 248.801, 162.2, 248.801, 266, 284, 394, 335);  // Coinciding 1-2
        //m_curve1.curve(162.200, 248.801, 162.200, 248.801, 257.000, 301.000, 394.000, 335.000); // Coinciding 1-2
        //m_curve1.curve(394.000, 335.000, 257.000, 301.000, 162.200, 248.801, 162.200, 248.801); // Coinciding 3-4
        //m_curve1.curve(84.200000,302.80100, 84.200000,302.80100, 79.000000,292.40100, 97.001000,304.40100); // From tiger.svg
        //m_curve1.curve(97.001000,304.40100, 79.000000,292.40100, 84.200000,302.80100, 84.200000,302.80100); // From tiger.svg opposite dir
        //m_curve1.curve(475, 157, 200, 100, 453, 100, 222, 157); // Cusp, failure for Adobe SVG
        add_ctrl(m_curve1);
        m_curve1.no_transform();

        m_angle_tolerance.label("Angle Tolerance=%.0f deg");
        m_angle_tolerance.range(0, 90);
        m_angle_tolerance.value(15);
        add_ctrl(m_angle_tolerance);
        m_angle_tolerance.no_transform();

        m_approximation_scale.label("Approximation Scale=%.3f");
        m_approximation_scale.range(0.1, 5);
        m_approximation_scale.value(1.0);
        add_ctrl(m_approximation_scale);
        m_approximation_scale.no_transform();

        m_cusp_limit.label("Cusp Limit=%.0f deg");
        m_cusp_limit.range(0, 90);
        m_cusp_limit.value(0);
        add_ctrl(m_cusp_limit);
        m_cusp_limit.no_transform();

        m_width.label("Width=%.2f");
        m_width.range(-50, 100);
        m_width.value(50.0);
        add_ctrl(m_width);
        m_width.no_transform();

        add_ctrl(m_show_points);
        m_show_points.no_transform();
        m_show_points.status(true);

        add_ctrl(m_show_outline);
        m_show_outline.no_transform();
        m_show_outline.status(true);

        m_curve_type.add_item("Incremental");
        m_curve_type.add_item("Subdiv");
        m_curve_type.cur_item(1);
        add_ctrl(m_curve_type);
        m_curve_type.no_transform();

        m_case_type.text_size(7);
        m_case_type.text_thickness(1.0);
        m_case_type.add_item("Random");
        m_case_type.add_item("13---24");
        m_case_type.add_item("Smooth Cusp 1");
        m_case_type.add_item("Smooth Cusp 2");
        m_case_type.add_item("Real Cusp 1");
        m_case_type.add_item("Real Cusp 2");
        m_case_type.add_item("Fancy Stroke");
        m_case_type.add_item("Jaw");
        m_case_type.add_item("Ugly Jaw");
        add_ctrl(m_case_type);
        m_case_type.no_transform();

        m_inner_join.text_size(8);
        m_inner_join.add_item("Inner Bevel");
        m_inner_join.add_item("Inner Miter");
        m_inner_join.add_item("Inner Jag");
        m_inner_join.add_item("Inner Round");
        m_inner_join.cur_item(3);
        add_ctrl(m_inner_join);
        m_inner_join.no_transform();

        m_line_join.text_size(8);
        m_line_join.add_item("Miter Join");
        m_line_join.add_item("Miter Revert");
        m_line_join.add_item("Round Join");
        m_line_join.add_item("Bevel Join");
        m_line_join.add_item("Miter Round");

        m_line_join.cur_item(1);
        add_ctrl(m_line_join);
        m_line_join.no_transform();

        m_line_cap.text_size(8);
        m_line_cap.add_item("Butt Cap");
        m_line_cap.add_item("Square Cap");
        m_line_cap.add_item("Round Cap");
        m_line_cap.cur_item(0);
        add_ctrl(m_line_cap);
        m_line_cap.no_transform();
    }


        public double measure_time(curve4 curve)
    {
        start_timer();
        for(int i = 0; i < 100; i++)
        {
            double x, y;
            curve.init(m_curve1.x1(), m_curve1.y1(),
                       m_curve1.x2(), m_curve1.y2(),
                       m_curve1.x3(), m_curve1.y3(),
                       m_curve1.x4(), m_curve1.y4());
            curve.rewind(0);
            while(!Path.is_stop(curve.vertex(out x, out y)));
        }
        return elapsed_time() * 10;
    }


        public bool find_point(pod_vector<vertex_dist> path, double dist, out uint i, out uint j)
    {
        int k;
        j = path.size() - 1;
          
        for(i = 0; (j - i) > 1; ) 
        {
            if(dist < path[k = (int)(i + j) >> 1].dist) 
                j = (uint)k; 
            else                                     
                i = (uint)k;
        }
        return true;
    }

    public struct curve_point
    {
        public curve_point(double x1, double y1, double mu1) 
        {
            x=(x1);
            y=(y1);
            mu=(mu1);
        }
        public double x, y, dist, mu;
    };

    public double calc_max_error(curve4 curve, double scale, out double max_angle_error)
    {
        curve.approximation_scale(m_approximation_scale.value() * scale);
        curve.init(m_curve1.x1(), m_curve1.y1(),
                   m_curve1.x2(), m_curve1.y2(),
                   m_curve1.x3(), m_curve1.y3(),
                   m_curve1.x4(), m_curve1.y4());

        pod_vector<vertex_dist> curve_points = new pod_vector<vertex_dist>();
        uint cmd;
        double x, y;
        curve.rewind(0);
        while(!Path.is_stop(cmd = curve.vertex(out x, out y)))
        {
            if(Path.is_vertex(cmd))
            {
                curve_points.add(new vertex_dist(x, y));
            }
        }
        uint i;
        double curve_dist = 0;
        for(i = 1; i < curve_points.size(); i++)
        {
            curve_points.Array[i - 1].dist = curve_dist;
            curve_dist += agg_math.calc_distance(curve_points[i-1].x, curve_points[i-1].y, 
                                             curve_points[i].x,   curve_points[i].y);
        }
        curve_points.Array[curve_points.size() - 1].dist = curve_dist;
        
        pod_vector<curve_point> reference_points = new pod_vector<curve_point>();
        for(i = 0; i < 4096; i++)
        {
            double mu = i / 4095.0;
            bezier4_point(m_curve1.x1(), m_curve1.y1(),
                          m_curve1.x2(), m_curve1.y2(),
                          m_curve1.x3(), m_curve1.y3(),
                          m_curve1.x4(), m_curve1.y4(),
                          mu, out x, out y);
            reference_points.add(new curve_point(x, y, mu));
        }

        double reference_dist = 0;
        for(i = 1; i < reference_points.size(); i++)
        {
            reference_points.Array[i - 1].dist = reference_dist;
            reference_dist += agg_math.calc_distance(reference_points[i-1].x, reference_points[i-1].y, 
                                                 reference_points[i].x,   reference_points[i].y);
        }
        reference_points.Array[reference_points.size() - 1].dist = reference_dist;


        uint idx1 = 0;
        uint idx2 = 1;
        double max_error = 0;
        for(i = 0; i < reference_points.size(); i++)
        {
            if(find_point(curve_points, reference_points[i].dist, out idx1, out idx2))
            {
                double err = Math.Abs(agg_math.calc_line_point_distance(curve_points[idx1].x,  curve_points[idx1].y,
                                                                curve_points[idx2].x,  curve_points[idx2].y,
                                                                reference_points[i].x, reference_points[i].y));
                if(err > max_error) max_error = err;
            }
        }

        double aerr = 0;
        for(i = 2; i < curve_points.size(); i++)
        {
            double a1 = Math.Atan2(curve_points[i-1].y - curve_points[i-2].y, 
                              curve_points[i-1].x - curve_points[i-2].x);
            double a2 = Math.Atan2(curve_points[i].y - curve_points[i - 1].y, 
                              curve_points[i].x - curve_points[i-1].x);

            double da = Math.Abs(a1 - a2);
            if (da >= Math.PI) da = 2 * Math.PI - da;
            if(da > aerr) aerr = da;
        }


        max_angle_error = aerr * 180.0 / Math.PI;
        return max_error * scale;
    }



    public override void on_draw()
    {
        pixfmt_alpha_blend_rgb pf = new pixfmt_alpha_blend_rgb(rbuf_window(), new blender_bgr());
        renderer_base ren_base = new renderer_base(pf);
        ren_base.clear(new rgba(1.0, 1.0, 0.95));
        renderer_scanline_aa_solid ren = new renderer_scanline_aa_solid(ren_base);

        rasterizer_scanline_aa ras = new rasterizer_scanline_aa();
        scanline_unpacked_8 sl = new scanline_unpacked_8();

        path_storage path = new path_storage(new vertex_block_storage());

        double x, y;
        double curve_time = 0;

        path.remove_all();
        curve4 curve;
        curve.approximation_method(curve_approximation_method_e(m_curve_type.cur_item()));
        curve.approximation_scale(m_approximation_scale.value());
        curve.angle_tolerance(deg2rad(m_angle_tolerance.value()));
        curve.cusp_limit(deg2rad(m_cusp_limit.value()));
        curve_time = measure_time(curve);
        double max_angle_error_01 = 0;
        double max_angle_error_1 = 0;
        double max_angle_error1 = 0;
        double max_angle_error_10 = 0;
        double max_angle_error_100 = 0;
        double max_error_01 = 0;
        double max_error_1 = 0;
        double max_error1 = 0;
        double max_error_10 = 0;
        double max_error_100 = 0;

        max_error_01   = calc_max_error(curve, 0.01, &max_angle_error_01);
        max_error_1    = calc_max_error(curve, 0.1,  &max_angle_error_1);
        max_error1     = calc_max_error(curve, 1,    &max_angle_error1);
        max_error_10   = calc_max_error(curve, 10,   &max_angle_error_10);
        max_error_100  = calc_max_error(curve, 100,  &max_angle_error_100);

        curve.approximation_scale(m_approximation_scale.value());
        curve.angle_tolerance(deg2rad(m_angle_tolerance.value()));
        curve.cusp_limit(deg2rad(m_cusp_limit.value()));
        curve.init(m_curve1.x1(), m_curve1.y1(),
                   m_curve1.x2(), m_curve1.y2(),
                   m_curve1.x3(), m_curve1.y3(),
                   m_curve1.x4(), m_curve1.y4());

        path.concat_path(curve);
//path.move_to(m_curve1.x1(), m_curve1.y1());
//path.line_to(m_curve1.x2(), m_curve1.y2());
//path.line_to(m_curve1.x3(), m_curve1.y3());
//path.line_to(m_curve1.x4(), m_curve1.y4());


        conv_stroke stroke = new conv_stroke(path);
        stroke.width(m_width.value());
        stroke.line_join(line_join_e(m_line_join.cur_item()));
        stroke.line_cap(line_cap_e(m_line_cap.cur_item()));
        stroke.inner_join(inner_join_e(m_inner_join.cur_item()));
        stroke.inner_miter_limit(1.01);

        ras.add_path(stroke);
        ren.color(rgba(0, 0.5, 0, 0.5));
        render_scanlines(ras, sl, ren);

        uint cmd;
        uint num_points1 = 0;
        path.rewind(0);
        while(!is_stop(cmd = path.vertex(&x, &y)))
        {
            if(m_show_points.status())
            {
                Shape.Ellipse ell = new agg.Shape.Ellipse(x, y, 1.5, 1.5, 8);
                ras.add_path(ell);
                ren.color(rgba(0,0,0, 0.5));
                render_scanlines(ras, sl, ren);
            }
            ++num_points1;
        }

        if(m_show_outline.status())
        {
            // Draw a stroke of the stroke to see the internals
            //--------------
            conv_stroke stroke2 = new conv_stroke(stroke);
            ras.add_path(stroke2);
            ren.color(rgba(0,0,0, 0.5));
            render_scanlines(ras, sl, ren);
        }

        // Check ellipse and arc for the number of points
        //---------------
        //ellipse a(100, 100, m_width.value(), m_width.value(), 0);
        //ras.add_path(a);
        //ren.color(rgba(0.5,0,0, 0.5));
        //render_scanlines(ras, sl, ren);
        //a.rewind(0);
        //while(!is_stop(cmd = a.vertex(&x, &y)))
        //{
        //    if(is_vertex(cmd))
        //    {
        //        ellipse ell(x, y, 1.5, 1.5, 8);
        //        ras.add_path(ell);
        //        ren.color(rgba(0,0,0,0.5));
        //        render_scanlines(ras, sl, ren);
        //    }
        //}


        // Check a circle with huge radius (10,000,000) and high approximation accuracy
        //---------------
        //double circle_pnt_count = 0;
        //bezier_arc ell(0,0, 10000000, 10000000, 0, 2*pi);
        //conv_curve<bezier_arc, curve3_div, curve4_div3> crv(ell);
        //crv.approximation_scale(10.0);
        //crv.rewind(0);
        //while(crv.vertex(&x, &y)) ++circle_pnt_count;


        string buf;
        gsv_text t;
        t.size(8.0);

        conv_stroke pt = new conv_stroke(t);
        pt.line_cap(round_cap);
        pt.line_join(round_join);
        pt.width(1.5);

        /*
        sprintf(buf, "Num Points=%d Time=%.2fmks\n\n"
                     " Dist Error: x0.01=%.5f x0.1=%.5f x1=%.5f x10=%.5f x100=%.5f\n\n"
                     "Angle Error: x0.01=%.1f x0.1=%.1f x1=%.1f x10=%.1f x100=%.1f", 
                num_points1, curve_time, 
                max_error_01,  
                max_error_1,   
                max_error1,   
                max_error_10,  
                max_error_100,
                max_angle_error_01,
                max_angle_error_1,
                max_angle_error1,
                max_angle_error_10,
                max_angle_error_100);
         */

        t.start_point(10.0, 85.0);
        t.text(buf);

        ras.add_path(pt);
        ren.color(rgba(0,0,0));
        render_scanlines(ras, sl, ren);

        render_ctrl(ras, sl, ren_base, m_curve1);
        render_ctrl(ras, sl, ren_base, m_angle_tolerance);
        render_ctrl(ras, sl, ren_base, m_approximation_scale);
        render_ctrl(ras, sl, ren_base, m_cusp_limit);
        render_ctrl(ras, sl, ren_base, m_width);
        render_ctrl(ras, sl, ren_base, m_show_points);
        render_ctrl(ras, sl, ren_base, m_show_outline);
        render_ctrl(ras, sl, ren_base, m_curve_type);
        render_ctrl(ras, sl, ren_base, m_case_type);
        render_ctrl(ras, sl, ren_base, m_inner_join);
        render_ctrl(ras, sl, ren_base, m_line_join);
        render_ctrl(ras, sl, ren_base, m_line_cap);
    }


        public override void on_key(int x, int y, uint key, uint flags)
    {
        if(key == ' ')
        {
            FILE* fd = fopen(full_file_name("coord"), "w");
            fprintf(fd, "%.3f, %.3f, %.3f, %.3f, %.3f, %.3f, %.3f, %.3f", 
                         m_curve1.x1(), m_curve1.y1(), 
                         m_curve1.x2(), m_curve1.y2(), 
                         m_curve1.x3(), m_curve1.y3(), 
                         m_curve1.x4(), m_curve1.y4());
            fclose(fd);
        }
    }

        public override void on_ctrl_change()
        {
            if (m_case_type.cur_item() != m_cur_case_type)
            {
                switch (m_case_type.cur_item())
                {
                    case 0: //m_case_type.add_item("Random");
                        {
                            int w = (int)(width() - 120);
                            int h = (int)(height() - 80);
                            m_curve1.curve(rand() % w, rand() % h + 80, rand() % w, rand() % h + 80,
                                           rand() % w, rand() % h + 80, rand() % w, rand() % h + 80);
                        }
                        break;

                    case 1: //m_case_type.add_item("13---24");
                        m_curve1.curve(150, 150, 350, 150, 150, 150, 350, 150);
                        //m_curve1.curve(252, 227, 16, 227, 506, 227, 285, 227);
                        //m_curve1.curve(252, 227, 16, 227, 387, 227, 285, 227);
                        break;

                    case 2: //m_case_type.add_item("Smooth Cusp 1");
                        m_curve1.curve(50, 142, 483, 251, 496, 62, 26, 333);
                        break;

                    case 3: //m_case_type.add_item("Smooth Cusp 2");
                        m_curve1.curve(50, 142, 484, 251, 496, 62, 26, 333);
                        break;

                    case 4: //m_case_type.add_item("Real Cusp 1");
                        m_curve1.curve(100, 100, 300, 200, 200, 200, 200, 100);
                        break;

                    case 5: //m_case_type.add_item("Real Cusp 2");
                        m_curve1.curve(475, 157, 200, 100, 453, 100, 222, 157);
                        break;

                    case 6: //m_case_type.add_item("Fancy Stroke");
                        m_curve1.curve(129, 233, 32, 283, 258, 285, 159, 232);
                        m_width.value(100);
                        break;

                    case 7: //m_case_type.add_item("Jaw");
                        m_curve1.curve(100, 100, 300, 200, 264, 286, 264, 284);
                        break;

                    case 8: //m_case_type.add_item("Ugly Jaw");
                        m_curve1.curve(100, 100, 413, 304, 264, 286, 264, 284);
                        break;
                }
                force_redraw();
                m_cur_case_type = m_case_type.cur_item();
            }
        }
 
        public static void StartDemo()
        {
            bezier_div_application app = new bezier_div_application(pix_format_e.pix_format_rgb, platform_support_abstract.ERenderOrigin.OriginBottomLeft);
            app.caption("AGG Example. Bezier Div.");

            if(app.init(655, 520, (uint)agg.ui.platform_support_abstract.window_flag_e.window_resize))
            {
                app.run();
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
        	StartDemo();
        }
    };
}




