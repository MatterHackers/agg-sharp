// conv_stroke.cpp headless reproduction (default state: 500x330, round join, round cap, width 20, miter 4).
// Params (all optional): join (0 miter, 1 miter revert, 2 round, 3 bevel), cap (0 butt, 1 square, 2 round),
// width, miter_limit, then x0 y0 x1 y1 x2 y2 for the triangle's corners.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_conv_stroke.h"
#include "agg_conv_dash.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_path_storage.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

int render_conv_stroke(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> ren_base;

    int join_item = params.size() > 0 ? int(params[0]) : 2;
    int cap_item = params.size() > 1 ? int(params[1]) : 2;
    double width = params.size() > 2 ? params[2] : 20.0;
    double miter_limit = params.size() > 3 ? params[3] : 4.0;
    double m_x[3] = { 57 + 100, 369 + 100, 143 + 100 };
    double m_y[3] = { 60, 170, 310 };
    if (params.size() >= 10) {
        for (int i = 0; i < 3; i++) { m_x[i] = params[4 + i * 2]; m_y[i] = params[5 + i * 2]; }
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    ren_base renb(pixf);
    renb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    agg::path_storage path;
    path.move_to(m_x[0], m_y[0]);
    path.line_to((m_x[0] + m_x[1]) / 2, (m_y[0] + m_y[1]) / 2);
    path.line_to(m_x[1], m_y[1]);
    path.line_to(m_x[2], m_y[2]);
    path.line_to(m_x[2], m_y[2]);

    path.move_to((m_x[0] + m_x[1]) / 2, (m_y[0] + m_y[1]) / 2);
    path.line_to((m_x[1] + m_x[2]) / 2, (m_y[1] + m_y[2]) / 2);
    path.line_to((m_x[2] + m_x[0]) / 2, (m_y[2] + m_y[0]) / 2);
    path.close_polygon();

    agg::line_cap_e cap = agg::butt_cap;
    if (cap_item == 1) cap = agg::square_cap;
    if (cap_item == 2) cap = agg::round_cap;

    agg::line_join_e join = agg::miter_join;
    if (join_item == 1) join = agg::miter_join_revert;
    if (join_item == 2) join = agg::round_join;
    if (join_item == 3) join = agg::bevel_join;

    agg::conv_stroke<agg::path_storage> stroke(path);
    stroke.line_join(join);
    stroke.line_cap(cap);
    stroke.miter_limit(miter_limit);
    stroke.width(width);
    ras.add_path(stroke);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0.8, 0.7, 0.6));

    agg::conv_stroke<agg::path_storage> poly1(path);
    poly1.width(1.5);
    ras.add_path(poly1);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0, 0, 0));

    agg::conv_dash<agg::conv_stroke<agg::path_storage> > poly2_dash(stroke);
    agg::conv_stroke<agg::conv_dash<agg::conv_stroke<agg::path_storage> > > poly2(poly2_dash);
    poly2.miter_limit(4.0);
    poly2.width(width / 5.0);
    poly2.line_cap(cap);
    poly2.line_join(join);
    poly2_dash.add_dash(20.0, width / 2.5);
    ras.add_path(poly2);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0, 0, 0.3));

    ras.add_path(path);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0.0, 0.0, 0.0, 0.2));

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::rbox_ctrl<agg::rgba8> m_join(10.0, 10.0, 133.0, 80.0, false);
    agg::rbox_ctrl<agg::rgba8> m_cap(10.0, 80.0 + 10.0, 133.0, 80.0 + 80.0, false);
    agg::slider_ctrl<agg::rgba8> m_width(130 + 10.0, 10.0 + 4.0, 500.0 - 10.0, 10.0 + 8.0 + 4.0, false);
    agg::slider_ctrl<agg::rgba8> m_miter_limit(130 + 10.0, 20.0 + 10.0 + 4.0, 500.0 - 10.0, 20.0 + 10.0 + 8.0 + 4.0, false);
    m_join.text_size(7.5);
    m_join.text_thickness(1.0);
    m_join.add_item("Miter Join");
    m_join.add_item("Miter Join Revert");
    m_join.add_item("Round Join");
    m_join.add_item("Bevel Join");
    m_join.cur_item(join_item);
    m_cap.add_item("Butt Cap");
    m_cap.add_item("Square Cap");
    m_cap.add_item("Round Cap");
    m_cap.cur_item(cap_item);
    m_width.range(3.0, 40.0);
    m_width.value(width);
    m_width.label("Width=%1.2f");
    m_miter_limit.range(1.0, 10.0);
    m_miter_limit.value(miter_limit);
    m_miter_limit.label("Miter Limit=%1.2f");

    agg::render_ctrl(ras, sl, renb, m_join);
    agg::render_ctrl(ras, sl, renb, m_cap);
    agg::render_ctrl(ras, sl, renb, m_width);
    agg::render_ctrl(ras, sl, renb, m_miter_limit);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
