// rasterizers.cpp headless reproduction (default state: 500x330, gamma 0.5, alpha 1).
// Params (all optional): gamma, alpha, then x0 y0 x1 y1 x2 y2 for the anti-aliased triangle's corners
// (the aliased one is the same triangle 200 to the left).
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_scanline_bin.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

int render_rasterizers(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_aa;
    typedef agg::renderer_scanline_bin_solid<renderer_base> renderer_bin;

    double gamma = params.size() > 0 ? params[0] : 0.5;
    double alpha = params.size() > 1 ? params[1] : 1.0;
    double m_x[3] = { 100 + 120, 369 + 120, 143 + 120 };
    double m_y[3] = { 60, 170, 310 };
    if (params.size() >= 8) {
        for (int i = 0; i < 3; i++) { m_x[i] = params[2 + i * 2]; m_y[i] = params[3 + i * 2]; }
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> m_ras;
    agg::scanline_p8 m_sl_p8;
    agg::scanline_bin m_sl_bin;

    // draw_anti_aliased()
    {
        renderer_aa ren_aa(rb);
        agg::path_storage path;
        path.move_to(m_x[0], m_y[0]);
        path.line_to(m_x[1], m_y[1]);
        path.line_to(m_x[2], m_y[2]);
        path.close_polygon();
        ren_aa.color(agg::rgba(0.7, 0.5, 0.1, alpha));
        m_ras.gamma(agg::gamma_power(gamma * 2.0));
        m_ras.add_path(path);
        agg::render_scanlines(m_ras, m_sl_p8, ren_aa);
    }

    // draw_aliased()
    {
        renderer_bin ren_bin(rb);
        agg::path_storage path;
        path.move_to(m_x[0] - 200, m_y[0]);
        path.line_to(m_x[1] - 200, m_y[1]);
        path.line_to(m_x[2] - 200, m_y[2]);
        path.close_polygon();
        ren_bin.color(agg::rgba(0.1, 0.5, 0.7, alpha));
        m_ras.gamma(agg::gamma_threshold(gamma));
        m_ras.add_path(path);
        agg::render_scanlines(m_ras, m_sl_bin, ren_bin);
    }

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::slider_ctrl<agg::rgba8> m_gamma(130 + 10.0, 10.0 + 4.0, 130 + 150.0, 10.0 + 8.0 + 4.0, false);
    agg::slider_ctrl<agg::rgba8> m_alpha(130 + 150.0 + 10.0, 10.0 + 4.0, 500 - 10.0, 10.0 + 8.0 + 4.0, false);
    agg::cbox_ctrl<agg::rgba8> m_test(130 + 10.0, 10.0 + 4.0 + 16.0, "Test Performance", false);
    m_gamma.range(0.0, 1.0);
    m_gamma.value(gamma);
    m_gamma.label("Gamma=%1.2f");
    m_alpha.range(0.0, 1.0);
    m_alpha.value(alpha);
    m_alpha.label("Alpha=%1.2f");

    agg::rasterizer_scanline_aa<> ras_aa;
    agg::render_ctrl(ras_aa, m_sl_p8, rb, m_gamma);
    agg::render_ctrl(ras_aa, m_sl_p8, rb, m_alpha);
    agg::render_ctrl(ras_aa, m_sl_p8, rb, m_test);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
