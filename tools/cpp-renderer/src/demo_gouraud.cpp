// gouraud.cpp headless reproduction (default state: 400x320, dilation 0.175, linear gamma 0.809, opacity 1).
// Params (all optional): dilation, gamma, alpha, then x0 y0 x1 y1 x2 y2 for the three corners.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_span_gouraud_rgba.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

int render_gouraud(unsigned w, unsigned h,
                   const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::rgba8 color_type;

    double dilation = params.size() > 0 ? params[0] : 0.175;
    double gamma = params.size() > 1 ? params[1] : 0.809;
    double alpha = params.size() > 2 ? params[2] : 1.0;
    double m_x[3] = { 57, 369, 143 };
    double m_y[3] = { 60, 170, 310 };
    if (params.size() >= 9) {
        for (int i = 0; i < 3; i++) { m_x[i] = params[3 + i * 2]; m_y[i] = params[4 + i * 2]; }
    }

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    renderer_base ren_base(pf);
    ren_base.clear(agg::rgba(1, 1, 1));

    agg::scanline_u8 sl;
    agg::rasterizer_scanline_aa<> ras;

    // render_gouraud()
    {
        double brc = 1;
        typedef agg::span_gouraud_rgba<color_type> span_gen_type;
        typedef agg::span_allocator<color_type> span_alloc_type;
        span_alloc_type span_alloc;
        span_gen_type span_gen;

        ras.gamma(agg::gamma_linear(0.0, gamma));
        double d = dilation;

        double xc = (m_x[0] + m_x[1] + m_x[2]) / 3.0;
        double yc = (m_y[0] + m_y[1] + m_y[2]) / 3.0;
        double x1 = (m_x[1] + m_x[0]) / 2 - (xc - (m_x[1] + m_x[0]) / 2);
        double y1 = (m_y[1] + m_y[0]) / 2 - (yc - (m_y[1] + m_y[0]) / 2);
        double x2 = (m_x[2] + m_x[1]) / 2 - (xc - (m_x[2] + m_x[1]) / 2);
        double y2 = (m_y[2] + m_y[1]) / 2 - (yc - (m_y[2] + m_y[1]) / 2);
        double x3 = (m_x[0] + m_x[2]) / 2 - (xc - (m_x[0] + m_x[2]) / 2);
        double y3 = (m_y[0] + m_y[2]) / 2 - (yc - (m_y[0] + m_y[2]) / 2);

        span_gen.colors(agg::rgba(1, 0, 0, alpha), agg::rgba(0, 1, 0, alpha), agg::rgba(brc, brc, brc, alpha));
        span_gen.triangle(m_x[0], m_y[0], m_x[1], m_y[1], xc, yc, d);
        ras.add_path(span_gen);
        agg::render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);

        span_gen.colors(agg::rgba(0, 1, 0, alpha), agg::rgba(0, 0, 1, alpha), agg::rgba(brc, brc, brc, alpha));
        span_gen.triangle(m_x[1], m_y[1], m_x[2], m_y[2], xc, yc, d);
        ras.add_path(span_gen);
        agg::render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);

        span_gen.colors(agg::rgba(0, 0, 1, alpha), agg::rgba(1, 0, 0, alpha), agg::rgba(brc, brc, brc, alpha));
        span_gen.triangle(m_x[2], m_y[2], m_x[0], m_y[0], xc, yc, d);
        ras.add_path(span_gen);
        agg::render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);

        brc = 1 - brc;
        span_gen.colors(agg::rgba(1, 0, 0, alpha), agg::rgba(0, 1, 0, alpha), agg::rgba(brc, brc, brc, alpha));
        span_gen.triangle(m_x[0], m_y[0], m_x[1], m_y[1], x1, y1, d);
        ras.add_path(span_gen);
        agg::render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);

        span_gen.colors(agg::rgba(0, 1, 0, alpha), agg::rgba(0, 0, 1, alpha), agg::rgba(brc, brc, brc, alpha));
        span_gen.triangle(m_x[1], m_y[1], m_x[2], m_y[2], x2, y2, d);
        ras.add_path(span_gen);
        agg::render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);

        span_gen.colors(agg::rgba(0, 0, 1, alpha), agg::rgba(1, 0, 0, alpha), agg::rgba(brc, brc, brc, alpha));
        span_gen.triangle(m_x[2], m_y[2], m_x[0], m_y[0], x3, y3, d);
        ras.add_path(span_gen);
        agg::render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);
    }

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::slider_ctrl<agg::rgba8> m_dilation(5, 5, 400 - 5, 11, false);
    agg::slider_ctrl<agg::rgba8> m_gamma(5, 5 + 15, 400 - 5, 11 + 15, false);
    agg::slider_ctrl<agg::rgba8> m_alpha(5, 5 + 30, 400 - 5, 11 + 30, false);
    m_dilation.label("Dilation=%3.2f");
    m_gamma.label("Linear gamma=%3.2f");
    m_alpha.label("Opacity=%3.2f");
    m_dilation.value(dilation);
    m_gamma.value(gamma);
    m_alpha.value(alpha);

    ras.gamma(agg::gamma_none());
    agg::render_ctrl(ras, sl, ren_base, m_dilation);
    agg::render_ctrl(ras, sl, ren_base, m_gamma);
    agg::render_ctrl(ras, sl, ren_base, m_alpha);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
