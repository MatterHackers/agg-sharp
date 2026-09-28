// gamma_ctrl.cpp headless reproduction (default state: 500x400, gamma values 1 1 1 1 - the example
// reads them from gamma.txt, which a fresh run does not have).
// Params (all optional): kx1 ky1 kx2 ky2, then p2_active (1 makes point 2 the active one).
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_gsv_text.h"
#include "agg_conv_stroke.h"
#include "agg_conv_transform.h"
#include "agg_ellipse.h"
#include "agg_path_storage.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_gamma_ctrl.h"

#include "common.h"

int render_gamma_ctrl(unsigned w, unsigned h,
                      const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> ren_base;

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::gamma_ctrl<agg::rgba8> g_ctrl(10.0, 10.0, 300.0, 200.0, false);
    if (params.size() >= 4) g_ctrl.values(params[0], params[1], params[2], params[3]);
    if (params.size() >= 5 && params[4] != 0) g_ctrl.change_active_point();

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    ren_base rb(pixf);

    // on_draw(); trans_affine_resizing() is identity at the initial size.
    double ewidth = w / 2 - 10;
    double ecenter = w / 2;
    agg::srgba8 color;
    rb.clear(agg::rgba(1, 1, 1));
    g_ctrl.text_size(10.0, 12.0);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;
    agg::render_ctrl(ras, sl, rb, g_ctrl);
    ras.gamma(g_ctrl);

    agg::ellipse ellipse;
    agg::conv_stroke<agg::ellipse> poly(ellipse);
    agg::trans_affine resizing;
    agg::conv_transform<agg::conv_stroke<agg::ellipse> > tpoly(poly, resizing);

    auto draw = [&](double y, double rx, double ry, double width) {
        ellipse.init(ecenter, y, rx, ry, 100);
        poly.width(width);
        ras.add_path(tpoly, 0);
        agg::render_scanlines_aa_solid(ras, sl, rb, color);
    };

    color = agg::srgba8(0, 0, 0);
    draw(220, ewidth, 15, 2.0);
    draw(220, 11, 11, 2.0);
    color = agg::srgba8(127, 127, 127);
    draw(260, ewidth, 15, 2.0);
    draw(260, 11, 11, 2.0);
    color = agg::srgba8(192, 192, 192);
    draw(300, ewidth, 15, 2.0);
    draw(300, 11, 11, 2.0);
    color = agg::rgba(0.0, 0.0, 0.4);
    draw(340, ewidth, 15.5, 1.0);
    draw(340, 10.5, 10.5, 1.0);
    draw(380, ewidth, 15.5, 0.4);
    draw(380, 10.5, 10.5, 0.4);
    draw(420, ewidth, 15.5, 0.1);
    draw(420, 10.5, 10.5, 0.1);

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_skewing(0.15, 0.0);
    mtx *= resizing;
    agg::gsv_text text;
    agg::gsv_text_outline<agg::trans_affine> text1(text, mtx);
    text.text("Text 2345");
    text.size(50, 20);
    text1.width(2.0);
    text.start_point(320, 10);
    color = agg::rgba(0.0, 0.5, 0.0);
    ras.add_path(text1, 0);
    agg::render_scanlines_aa_solid(ras, sl, rb, color);

    color = agg::rgba(0.5, 0.0, 0.0);
    agg::path_storage path;
    path.move_to(30, -1.0);
    path.line_to(60, 0.0);
    path.line_to(30, 1.0);
    path.move_to(27, -1.0);
    path.line_to(10, 0.0);
    path.line_to(27, 1.0);
    agg::conv_transform<agg::path_storage> trans(path, mtx);
    for (int i = 0; i < 35; i++) {
        mtx.reset();
        mtx *= agg::trans_affine_rotation(double(i) / 35.0 * agg::pi * 2.0);
        mtx *= agg::trans_affine_translation(400, 130);
        mtx *= resizing;
        ras.add_path(trans, 0);
        agg::render_scanlines_aa_solid(ras, sl, rb, color);
    }

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
