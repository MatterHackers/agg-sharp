// scanline_boolean.cpp headless reproduction (default state: Union, both
// opacities 1, the two quads as on_init() places them; 800x600 bgr24 window
// cleared to white).
//
// Params (all optional): operation (the rbox item, cast to sbool_op_e as the
// example does), opacity1, opacity2.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_scanline_boolean_algebra.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_ellipse.h"
#include "agg_gamma_functions.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"
#include "ctrl/agg_slider_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

static void generate_circles(agg::path_storage& ps, const double* quad,
                             unsigned num_circles, double radius) {
    ps.remove_all();
    for (unsigned i = 0; i < 4; ++i) {
        unsigned n1 = i * 2;
        unsigned n2 = (i < 3) ? i * 2 + 2 : 0;
        for (unsigned j = 0; j < num_circles; j++) {
            agg::ellipse ell(quad[n1]     + (quad[n2]     - quad[n1])     * j / num_circles,
                             quad[n1 + 1] + (quad[n2 + 1] - quad[n1 + 1]) * j / num_circles,
                             radius, radius, 100);
            ps.concat_path(ell);
        }
    }
}

int render_scanline_boolean(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    int op_item = params.size() > 0 ? (int)params[0] : 0;
    double mul1 = params.size() > 1 ? params[1] : 1.0;
    double mul2 = params.size() > 2 ? params[2] : 1.0;

    // flip_y = true, so every control gets !flip_y = false.
    agg::rbox_ctrl<color_type> m_trans_type(420, 5.0, 420 + 130.0, 145.0, false);
    agg::cbox_ctrl<color_type> m_reset(350, 5.0, "Reset", false);
    agg::slider_ctrl<color_type> m_mul1(5.0, 5.0, 340.0, 12.0, false);
    agg::slider_ctrl<color_type> m_mul2(5.0, 20.0, 340.0, 27.0, false);
    m_trans_type.add_item("Union");
    m_trans_type.add_item("Intersection");
    m_trans_type.add_item("Linear XOR");
    m_trans_type.add_item("Saddle XOR");
    m_trans_type.add_item("Abs Diff XOR");
    m_trans_type.add_item("A-B");
    m_trans_type.add_item("B-A");
    m_trans_type.cur_item(op_item);
    m_mul1.value(mul1);
    m_mul2.value(mul2);
    m_mul1.label("Opacity1=%.3f");
    m_mul2.label("Opacity2=%.3f");

    agg::interactive_polygon m_quad1(4, 5.0);
    agg::interactive_polygon m_quad2(4, 5.0);
    m_quad1.xn(0) = 50;          m_quad1.yn(0) = 200 - 20;
    m_quad1.xn(1) = w / 2 - 25;  m_quad1.yn(1) = 200;
    m_quad1.xn(2) = w / 2 - 25;  m_quad1.yn(2) = h - 50 - 20;
    m_quad1.xn(3) = 50;          m_quad1.yn(3) = h - 50;
    m_quad2.xn(0) = w / 2 + 25;  m_quad2.yn(0) = 200 - 20;
    m_quad2.xn(1) = w - 50;      m_quad2.yn(1) = 200;
    m_quad2.xn(2) = w - 50;      m_quad2.yn(2) = h - 50 - 20;
    m_quad2.xn(3) = w / 2 + 25;  m_quad2.yn(3) = h - 50;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid r(rb);
    rb.clear(agg::rgba(1, 1, 1));

    agg::scanline_p8 sl;
    agg::rasterizer_scanline_aa<> ras;
    agg::rasterizer_scanline_aa<> ras1;
    agg::rasterizer_scanline_aa<> ras2;

    agg::sbool_op_e op = (agg::sbool_op_e)m_trans_type.cur_item();

    ras1.gamma(agg::gamma_multiply(m_mul1.value()));
    ras2.gamma(agg::gamma_multiply(m_mul2.value()));

    ras.clip_box(0, 0, w, h);

    agg::path_storage ps1;
    generate_circles(ps1, m_quad1.polygon(), 5, 20);

    agg::path_storage ps2;
    generate_circles(ps2, m_quad2.polygon(), 5, 20);

    ras1.filling_rule(agg::fill_even_odd);

    r.color(agg::srgba8(240, 255, 200, 100));
    ras1.add_path(ps1);
    agg::render_scanlines(ras1, sl, r);

    r.color(agg::srgba8(255, 240, 240, 100));
    ras2.add_path(ps2);
    agg::render_scanlines(ras2, sl, r);

    agg::scanline_p8 sl_result;
    agg::scanline_p8 sl1;
    agg::scanline_p8 sl2;
    renderer_solid sren(rb);
    sren.color(agg::srgba8(0, 0, 0));
    agg::sbool_combine_shapes_aa(op, ras1, ras2, sl1, sl2, sl_result, sren);

    r.color(agg::rgba(0, 0.3, 0.5, 0.6));
    ras.add_path(m_quad1);
    agg::render_scanlines(ras, sl, r);
    ras.add_path(m_quad2);
    agg::render_scanlines(ras, sl, r);
    agg::render_ctrl(ras, sl, rb, m_trans_type);
    agg::render_ctrl(ras, sl, rb, m_reset);
    agg::render_ctrl(ras, sl, rb, m_mul1);
    agg::render_ctrl(ras, sl, rb, m_mul2);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
