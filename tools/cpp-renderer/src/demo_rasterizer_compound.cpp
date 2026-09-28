// rasterizer_compound.cpp headless reproduction (default state: 440x330 bgra32
// window, width 10, every alpha 1, direct layer order).
//
// Params (all optional): width, alpha1, alpha2, alpha3, alpha4, invert (0/1).
//
// A gradient background and two triangles, then four compound-rasterizer
// styles - a stroked ellipse, the ellipse, a stroked "a" glyph and the glyph -
// drawn as layers with render_scanlines_compound_layered into the
// premultiplied pixel format.
#include <vector>

#include "agg_basics.h"
#include "agg_ellipse.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_rasterizer_compound_aa.h"
#include "agg_conv_curve.h"
#include "agg_conv_stroke.h"
#include "agg_conv_transform.h"
#include "agg_path_storage.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_pixfmt_rgba.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

namespace {

typedef agg::rgba8 color_type;

class style_handler {
public:
    style_handler(const color_type* styles, unsigned count)
        : m_transparent(0, 0, 0, 0), m_styles(styles), m_count(count) {}
    bool is_solid(unsigned) const { return true; }
    const color_type& color(unsigned style) const {
        if (style < m_count) return m_styles[style];
        return m_transparent;
    }
    void generate_span(color_type*, int, int, unsigned, unsigned) {}
private:
    color_type m_transparent;
    const color_type* m_styles;
    unsigned m_count;
};

void compose_path(agg::path_storage& m_path) {
    m_path.remove_all();
    m_path.move_to(28.47, 6.45);
    m_path.curve3(21.58, 1.12, 19.82, 0.29);
    m_path.curve3(17.19, -0.93, 14.21, -0.93);
    m_path.curve3(9.57, -0.93, 6.57, 2.25);
    m_path.curve3(3.56, 5.42, 3.56, 10.60);
    m_path.curve3(3.56, 13.87, 5.03, 16.26);
    m_path.curve3(7.03, 19.58, 11.99, 22.51);
    m_path.curve3(16.94, 25.44, 28.47, 29.64);
    m_path.line_to(28.47, 31.40);
    m_path.curve3(28.47, 38.09, 26.34, 40.58);
    m_path.curve3(24.22, 43.07, 20.17, 43.07);
    m_path.curve3(17.09, 43.07, 15.28, 41.41);
    m_path.curve3(13.43, 39.75, 13.43, 37.60);
    m_path.line_to(13.53, 34.77);
    m_path.curve3(13.53, 32.52, 12.38, 31.30);
    m_path.curve3(11.23, 30.08, 9.38, 30.08);
    m_path.curve3(7.57, 30.08, 6.42, 31.35);
    m_path.curve3(5.27, 32.62, 5.27, 34.81);
    m_path.curve3(5.27, 39.01, 9.57, 42.53);
    m_path.curve3(13.87, 46.04, 21.63, 46.04);
    m_path.curve3(27.59, 46.04, 31.40, 44.04);
    m_path.curve3(34.28, 42.53, 35.64, 39.31);
    m_path.curve3(36.52, 37.21, 36.52, 30.71);
    m_path.line_to(36.52, 15.53);
    m_path.curve3(36.52, 9.13, 36.77, 7.69);
    m_path.curve3(37.01, 6.25, 37.57, 5.76);
    m_path.curve3(38.13, 5.27, 38.87, 5.27);
    m_path.curve3(39.65, 5.27, 40.23, 5.62);
    m_path.curve3(41.26, 6.25, 44.19, 9.18);
    m_path.line_to(44.19, 6.45);
    m_path.curve3(38.72, -0.88, 33.74, -0.88);
    m_path.curve3(31.35, -0.88, 29.93, 0.78);
    m_path.curve3(28.52, 2.44, 28.47, 6.45);
    m_path.close_polygon();

    m_path.move_to(28.47, 9.62);
    m_path.line_to(28.47, 26.66);
    m_path.curve3(21.09, 23.73, 18.95, 22.51);
    m_path.curve3(15.09, 20.36, 13.43, 18.02);
    m_path.curve3(11.77, 15.67, 11.77, 12.89);
    m_path.curve3(11.77, 9.38, 13.87, 7.06);
    m_path.curve3(15.97, 4.74, 18.70, 4.74);
    m_path.curve3(22.41, 4.74, 28.47, 9.62);
    m_path.close_polygon();
}

} // namespace

int render_rasterizer_compound(unsigned w, unsigned h,
                               const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::pixfmt_bgra32_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt> ren_base;
    typedef agg::renderer_base<pixfmt_pre> ren_base_pre;

    // flip_y = true, so the sliders get !flip_y = false.
    const bool flip_y = true;
    agg::slider_ctrl<color_type> m_width(180 + 10.0, 5.0, 130 + 300.0, 12, !flip_y);
    agg::slider_ctrl<color_type> m_alpha1(5, 5, 180, 12, !flip_y);
    agg::slider_ctrl<color_type> m_alpha2(5, 25, 180, 32, !flip_y);
    agg::slider_ctrl<color_type> m_alpha3(5, 45, 180, 52, !flip_y);
    agg::slider_ctrl<color_type> m_alpha4(5, 65, 180, 72, !flip_y);
    agg::cbox_ctrl<color_type> m_invert_order(190, 25, "Invert Z-Order");

    m_width.range(-20.0, 50.0);
    m_width.value(params.size() > 0 ? params[0] : 10.0);
    m_width.label("Width=%1.2f");
    m_alpha1.range(0, 1);
    m_alpha1.value(params.size() > 1 ? params[1] : 1.0);
    m_alpha1.label("Alpha1=%1.3f");
    m_alpha2.range(0, 1);
    m_alpha2.value(params.size() > 2 ? params[2] : 1.0);
    m_alpha2.label("Alpha2=%1.3f");
    m_alpha3.range(0, 1);
    m_alpha3.value(params.size() > 3 ? params[3] : 1.0);
    m_alpha3.label("Alpha3=%1.3f");
    m_alpha4.range(0, 1);
    m_alpha4.value(params.size() > 4 ? params[4] : 1.0);
    m_alpha4.label("Alpha4=%1.3f");
    m_invert_order.status(params.size() > 5 && params[5] != 0);

    headless::canvas cv(w, h, 4);
    pixfmt pixf(cv.rbuf);
    ren_base renb(pixf);
    pixfmt_pre pixf_pre(cv.rbuf);
    ren_base_pre renb_pre(pixf_pre);

    agg::path_storage m_path;

    // Clear the window with a gradient
    agg::pod_vector<color_type> gr(pixf_pre.width());
    unsigned i;
    for (i = 0; i < pixf.width(); i++) {
        gr.add(agg::srgba8(255, 255, 0).gradient(agg::srgba8(0, 255, 255),
                                                double(i) / pixf.width()));
    }
    for (i = 0; i < pixf.height(); i++) {
        renb.copy_color_hspan(0, i, pixf.width(), &gr[0]);
    }

    agg::rasterizer_scanline_aa<> ras;
    agg::rasterizer_compound_aa<agg::rasterizer_sl_clip_dbl> rasc;
    agg::scanline_u8 sl;
    agg::span_allocator<color_type> alloc;

    // Draw two triangles
    ras.move_to_d(0, 0);
    ras.line_to_d(w, 0);
    ras.line_to_d(w, h);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(0, 100, 0));

    ras.move_to_d(0, 0);
    ras.line_to_d(0, h);
    ras.line_to_d(w, 0);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(0, 100, 100));

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_scaling(4.0);
    mtx *= agg::trans_affine_translation(150, 100);

    agg::conv_transform<agg::path_storage> trans(m_path, mtx);
    agg::conv_curve<agg::conv_transform<agg::path_storage> > curve(trans);
    agg::conv_stroke<agg::conv_curve<agg::conv_transform<agg::path_storage> > > stroke(curve);

    compose_path(m_path);

    color_type styles[4];

    if (m_invert_order.status()) rasc.layer_order(agg::layer_inverse);
    else                         rasc.layer_order(agg::layer_direct);

    styles[3] = agg::srgba8(255, 0, 108);
    styles[2] = agg::srgba8(51, 0, 151);
    styles[1] = agg::srgba8(143, 90, 6);
    styles[0] = agg::srgba8(0, 0, 255);

    style_handler sh(styles, 4);

    stroke.width(m_width.value());

    styles[3].opacity(m_alpha1.value()).premultiply();
    styles[2].opacity(m_alpha2.value()).premultiply();
    styles[1].opacity(m_alpha3.value()).premultiply();
    styles[0].opacity(m_alpha4.value()).premultiply();

    agg::ellipse ell(220.0, 180.0, 120.0, 10.0, 128, false);
    agg::conv_stroke<agg::ellipse> str_ell(ell);
    str_ell.width(m_width.value() / 2);

    rasc.styles(3, -1);
    rasc.add_path(str_ell);

    rasc.styles(2, -1);
    rasc.add_path(ell);

    rasc.styles(1, -1);
    rasc.add_path(stroke);

    rasc.styles(0, -1);
    rasc.add_path(curve);

    agg::render_scanlines_compound_layered(rasc, sl, renb_pre, alloc, sh);
    agg::render_ctrl(ras, sl, renb, m_width);
    agg::render_ctrl(ras, sl, renb, m_alpha1);
    agg::render_ctrl(ras, sl, renb, m_alpha2);
    agg::render_ctrl(ras, sl, renb, m_alpha3);
    agg::render_ctrl(ras, sl, renb, m_alpha4);
    agg::render_ctrl(ras, sl, renb, m_invert_order);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
