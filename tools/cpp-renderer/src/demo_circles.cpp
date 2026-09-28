// circles.cpp headless reproduction (default state: 400x400, 10000 points, scale 0.3..0.7, selectivity
// and size 0.5).
// Params (all optional): value1 value2 (the scale ctrl), selectivity, size, idle_steps (how many on_idle
// jitters run before the frame), num_points.
//
// circles.cpp draws its points with rand(), whose sequence differs between C libraries. This uses the
// MSVC rand() generator (the same LCG flash_shape.h's msvc_rand is), and the port uses the same one, so
// both scatter the same points.
#include <cmath>
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_conv_transform.h"
#include "agg_bspline.h"
#include "agg_ellipse.h"
#include "agg_gsv_text.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_scale_ctrl.h"

#include "common.h"

namespace {

// MSVC's rand(): holdrand = holdrand * 214013 + 2531011, returning bits 16..30. Seeded 1, as rand() is.
unsigned g_holdrand = 1;

int msvc_rand()
{
    g_holdrand = g_holdrand * 214013u + 2531011u;
    return (g_holdrand >> 16) & 0x7fff;
}

double spline_r_x[] = { 0.000000, 0.200000, 0.400000, 0.910484, 0.957258, 1.000000 };
double spline_r_y[] = { 1.000000, 0.800000, 0.600000, 0.066667, 0.169697, 0.600000 };

double spline_g_x[] = { 0.000000, 0.292244, 0.485655, 0.564859, 0.795607, 1.000000 };
double spline_g_y[] = { 0.000000, 0.607260, 0.964065, 0.892558, 0.435571, 0.000000 };

double spline_b_x[] = { 0.000000, 0.055045, 0.143034, 0.433082, 0.764859, 1.000000 };
double spline_b_y[] = { 0.385480, 0.128493, 0.021416, 0.271507, 0.713974, 1.000000 };

struct scatter_point
{
    double     x;
    double     y;
    double     z;
    agg::rgba  color;
};

double random_dbl(double start, double end)
{
    unsigned r = msvc_rand() & 0x7FFF;
    return double(r) * (end - start) / 32768.0 + start;
}

} // namespace

int render_circles(unsigned w, unsigned h,
                   const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::rgba8 color_type;

    double value1      = params.size() > 0 ? params[0] : 0.3;
    double value2      = params.size() > 1 ? params[1] : 0.7;
    double selectivity = params.size() > 2 ? params[2] : 0.5;
    double size        = params.size() > 3 ? params[3] : 0.5;
    int idle_steps     = params.size() > 4 ? (int)params[4] : 0;
    unsigned m_num_points = params.size() > 5 ? (unsigned)params[5] : 10000;

    g_holdrand = 1;

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::scale_ctrl<color_type>  m_scale_ctrl_z    (5, 5,  w-5, 12, false);
    agg::slider_ctrl<color_type> m_slider_ctrl_sel (5, 20, w-5, 27, false);
    agg::slider_ctrl<color_type> m_slider_ctrl_size(5, 35, w-5, 42, false);
    m_slider_ctrl_size.label("Size");
    m_slider_ctrl_sel.label("Selectivity");
    // value2 first: value1 is limited to value2 - min_delta.
    m_scale_ctrl_z.value2(value2);
    m_scale_ctrl_z.value1(value1);
    m_slider_ctrl_sel.value(selectivity);
    m_slider_ctrl_size.value(size);

    agg::bspline m_spline_r;
    agg::bspline m_spline_g;
    agg::bspline m_spline_b;
    m_spline_r.init(6, spline_r_x, spline_r_y);
    m_spline_g.init(6, spline_g_x, spline_g_y);
    m_spline_b.init(6, spline_b_x, spline_b_y);

    // generate(), from on_init: the initial size is the window size.
    std::vector<scatter_point> m_points(m_num_points);
    double rx = w/3.5;
    double ry = h/3.5;
    for(unsigned i = 0; i < m_num_points; i++)
    {
        double z = m_points[i].z = random_dbl(0.0, 1.0);
        double x = cos(z * 2.0 * agg::pi) * rx;
        double y = sin(z * 2.0 * agg::pi) * ry;

        double dist  = random_dbl(0.0, rx/2.0);
        double angle = random_dbl(0.0, agg::pi * 2.0);

        m_points[i].x = w/2.0 + x + cos(angle) * dist;
        m_points[i].y = h/2.0 + y + sin(angle) * dist;
        m_points[i].color = agg::rgba(m_spline_r.get(z)*0.8,
                                      m_spline_g.get(z)*0.8,
                                      m_spline_b.get(z)*0.8,
                                      1.0);
    }

    // on_idle, idle_steps times.
    for(int s = 0; s < idle_steps; s++)
    {
        for(unsigned i = 0; i < m_num_points; i++)
        {
            m_points[i].x += random_dbl(0, m_slider_ctrl_sel.value()) - m_slider_ctrl_sel.value()*0.5;
            m_points[i].y += random_dbl(0, m_slider_ctrl_sel.value()) - m_slider_ctrl_sel.value()*0.5;
            m_points[i].z += random_dbl(0, m_slider_ctrl_sel.value()*0.01) - m_slider_ctrl_sel.value()*0.005;
            if(m_points[i].z < 0.0) m_points[i].z = 0.0;
            if(m_points[i].z > 1.0) m_points[i].z = 1.0;
        }
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);

    // on_draw. trans_affine_resizing() is identity at the initial window size.
    agg::trans_affine resizing;
    agg::rasterizer_scanline_aa<> pf;
    agg::scanline_p8 sl;

    rb.clear(agg::rgba(1,1,1));

    agg::ellipse e1;
    agg::conv_transform<agg::ellipse> t1(e1, resizing);

    unsigned n_drawn = 0;
    for(unsigned i = 0; i < m_num_points; i++)
    {
        double z = m_points[i].z;
        double alpha = 1.0;
        if(z < m_scale_ctrl_z.value1())
        {
            alpha = 1.0 - (m_scale_ctrl_z.value1() - z) * m_slider_ctrl_sel.value() * 100.0;
        }

        if(z > m_scale_ctrl_z.value2())
        {
            alpha = 1.0 - (z - m_scale_ctrl_z.value2()) * m_slider_ctrl_sel.value() * 100.0;
        }

        if(alpha > 1.0) alpha = 1.0;
        if(alpha < 0.0) alpha = 0.0;

        if(alpha > 0.0)
        {
            e1.init(m_points[i].x,
                    m_points[i].y,
                    m_slider_ctrl_size.value() * 5.0,
                    m_slider_ctrl_size.value() * 5.0,
                    8);
            pf.add_path(t1);

            agg::render_scanlines_aa_solid(
                pf, sl, rb,
                agg::rgba(m_points[i].color.r,
                          m_points[i].color.g,
                          m_points[i].color.b,
                          alpha));
            n_drawn++;
        }
    }

    agg::render_ctrl(pf, sl, rb, m_scale_ctrl_z);
    agg::render_ctrl(pf, sl, rb, m_slider_ctrl_sel);
    agg::render_ctrl(pf, sl, rb, m_slider_ctrl_size);

    char buf[10];
    snprintf(buf, sizeof(buf), "%08u", n_drawn);

    agg::gsv_text txt;
    txt.size(15.0);
    txt.text(buf);
    txt.start_point(10.0, h - 20.0);
    agg::gsv_text_outline<> txt_o(txt, resizing);
    pf.add_path(txt_o);
    agg::render_scanlines_aa_solid(pf, sl, rb, agg::rgba(0,0,0));

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
