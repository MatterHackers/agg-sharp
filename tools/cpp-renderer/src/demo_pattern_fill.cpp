// pattern_fill.cpp headless reproduction: on_draw() at the window size the
// example opens at (640x480). Params (all optional): polygon angle, polygon
// scale, pattern angle, pattern size, pattern alpha, rotate polygon, rotate
// pattern, tie pattern, polygon center x/y.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_path_storage.h"
#include "agg_trans_affine.h"
#include "agg_conv_transform.h"
#include "agg_conv_smooth_poly1.h"
#include "agg_conv_stroke.h"
#include "agg_pixfmt_rgba.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_span_pattern_rgba.h"
#include "agg_image_accessors.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

namespace {

void pattern_fill_create_star(agg::path_storage& ps, double xc, double yc,
                              double r1, double r2, unsigned n, double start_angle = 0.0)
{
    ps.remove_all();
    start_angle *= agg::pi / 180.0;
    for (unsigned i = 0; i < n; i++) {
        double a = agg::pi * 2.0 * i / n - agg::pi / 2.0;
        double dx = cos(a + start_angle);
        double dy = sin(a + start_angle);
        if (i & 1) {
            ps.line_to(xc + dx * r1, yc + dy * r1);
        } else {
            if (i) ps.line_to(xc + dx * r2, yc + dy * r2);
            else   ps.move_to(xc + dx * r2, yc + dy * r2);
        }
    }
    ps.close_polygon();
}

} // namespace

int render_pattern_fill(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::pixfmt_bgra32_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_base<pixfmt_pre> renderer_base_pre;

    double W = w, H = h;
    double poly_cx = W / 2.0, poly_cy = H / 2.0;

    // The constructor: flip_y = true, so the ctrls get !flip_y.
    agg::slider_ctrl<agg::rgba> m_polygon_angle(5, 5, 145, 12, false);
    agg::slider_ctrl<agg::rgba> m_polygon_scale(5, 5 + 14, 145, 12 + 14, false);
    agg::slider_ctrl<agg::rgba> m_pattern_angle(155, 5, 300, 12, false);
    agg::slider_ctrl<agg::rgba> m_pattern_size(155, 5 + 14, 300, 12 + 14, false);
    agg::slider_ctrl<agg::rgba> m_pattern_alpha(310, 5, 460, 12, false);
    agg::cbox_ctrl<agg::rgba> m_rotate_polygon(5, 5 + 14 + 14, "Rotate Polygon", false);
    agg::cbox_ctrl<agg::rgba> m_rotate_pattern(5, 5 + 14 + 14 + 14, "Rotate Pattern", false);
    agg::cbox_ctrl<agg::rgba> m_tie_pattern(155, 5 + 14 + 14, "Tie pattern to polygon", false);

    m_polygon_angle.label("Polygon Angle=%3.2f");
    m_polygon_angle.range(-180.0, 180.0);
    m_polygon_scale.label("Polygon Scale=%3.2f");
    m_polygon_scale.range(0.1, 5.0);
    m_polygon_scale.value(1.0);
    m_pattern_angle.label("Pattern Angle=%3.2f");
    m_pattern_angle.range(-180.0, 180.0);
    m_pattern_size.label("Pattern Size=%3.2f");
    m_pattern_size.range(10, 40);
    m_pattern_size.value(30);
    m_pattern_alpha.label("Background Alpha=%.2f");
    m_pattern_alpha.value(0.1);

    if (params.size() > 0) m_polygon_angle.value(params[0]);
    if (params.size() > 1) m_polygon_scale.value(params[1]);
    if (params.size() > 2) m_pattern_angle.value(params[2]);
    if (params.size() > 3) m_pattern_size.value(params[3]);
    if (params.size() > 4) m_pattern_alpha.value(params[4]);
    if (params.size() > 5) m_rotate_polygon.status(params[5] > 0.5);
    if (params.size() > 6) m_rotate_pattern.status(params[6] > 0.5);
    if (params.size() > 7) m_tie_pattern.status(params[7] > 0.5);
    if (params.size() > 8) poly_cx = params[8];
    if (params.size() > 9) poly_cy = params[9];

    agg::rasterizer_scanline_aa<> m_ras;
    agg::scanline_p8 m_sl;
    agg::path_storage m_ps;

    // generate_pattern
    unsigned size = unsigned(m_pattern_size.value());
    pattern_fill_create_star(m_ps,
                             m_pattern_size.value() / 2.0,
                             m_pattern_size.value() / 2.0,
                             m_pattern_size.value() / 2.5,
                             m_pattern_size.value() / 6.0,
                             6,
                             m_pattern_angle.value());

    agg::conv_smooth_poly1_curve<agg::path_storage> smooth(m_ps);
    agg::conv_stroke<agg::conv_smooth_poly1_curve<agg::path_storage> > stroke(smooth);
    smooth.smooth_value(1.0);
    smooth.approximation_scale(4.0);
    stroke.width(m_pattern_size.value() / 15.0);

    std::vector<agg::int8u> pattern(size * size * pixfmt::pix_width);
    agg::rendering_buffer pattern_rbuf(&pattern[0], size, size, size * pixfmt::pix_width);
    {
        pixfmt pixf(pattern_rbuf);
        renderer_base rb(pixf);
        agg::renderer_scanline_aa_solid<renderer_base> rs(rb);

        rb.clear(agg::rgba_pre(0.4, 0.0, 0.1, m_pattern_alpha.value()));

        m_ras.add_path(smooth);
        rs.color(agg::srgba8(110, 130, 50));
        agg::render_scanlines(m_ras, m_sl, rs);

        m_ras.add_path(stroke);
        rs.color(agg::srgba8(0, 50, 80));
        agg::render_scanlines(m_ras, m_sl, rs);
    }

    // on_draw
    headless::canvas cv(w, h, 4);
    pixfmt pixf(cv.rbuf);
    pixfmt_pre pixf_pre(cv.rbuf);
    renderer_base rb(pixf);
    renderer_base_pre rb_pre(pixf_pre);
    rb.clear(agg::rgba(1.0, 1.0, 1.0));

    agg::trans_affine polygon_mtx;
    polygon_mtx *= agg::trans_affine_translation(-poly_cx, -poly_cy);
    polygon_mtx *= agg::trans_affine_rotation(m_polygon_angle.value() * agg::pi / 180.0);
    polygon_mtx *= agg::trans_affine_scaling(m_polygon_scale.value());
    polygon_mtx *= agg::trans_affine_translation(poly_cx, poly_cy);

    // initial_width(): the window agg_main opens.
    double r = 640 / 3.0 - 8.0;
    pattern_fill_create_star(m_ps, poly_cx, poly_cy, r, r / 1.45, 14);

    agg::conv_transform<agg::path_storage> tr(m_ps, polygon_mtx);

    typedef agg::wrap_mode_reflect_auto_pow2 wrap_x_type;
    typedef agg::wrap_mode_reflect_auto_pow2 wrap_y_type;
    typedef agg::image_accessor_wrap<pixfmt, wrap_x_type, wrap_y_type> img_source_type;
    typedef agg::span_pattern_rgba<img_source_type> span_gen_type;

    unsigned offset_x = 0;
    unsigned offset_y = 0;
    if (m_tie_pattern.status()) {
        offset_x = unsigned(W - poly_cx);
        offset_y = unsigned(H - poly_cy);
    }

    agg::span_allocator<agg::rgba8> sa;
    pixfmt img_pixf(pattern_rbuf);
    img_source_type img_src(img_pixf);
    span_gen_type sg(img_src, offset_x, offset_y);
    sg.alpha(span_gen_type::value_type(m_pattern_alpha.value() * 255.0));

    m_ras.add_path(tr);
    agg::render_scanlines_aa(m_ras, m_sl, rb_pre, sa, sg);

    agg::render_ctrl(m_ras, m_sl, rb, m_polygon_angle);
    agg::render_ctrl(m_ras, m_sl, rb, m_polygon_scale);
    agg::render_ctrl(m_ras, m_sl, rb, m_pattern_angle);
    agg::render_ctrl(m_ras, m_sl, rb, m_pattern_size);
    agg::render_ctrl(m_ras, m_sl, rb, m_pattern_alpha);
    agg::render_ctrl(m_ras, m_sl, rb, m_rotate_polygon);
    agg::render_ctrl(m_ras, m_sl, rb, m_rotate_pattern);
    agg::render_ctrl(m_ras, m_sl, rb, m_tie_pattern);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
