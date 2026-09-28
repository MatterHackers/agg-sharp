// gamma_tuner.cpp headless reproduction (default state: 500x500, gamma 2.2, R G B 1, Checkered pattern).
// Params (all optional): gamma r g b pattern (0 Horizontal, 1 Vertical, 2 Checkered).
// The example defines AGG_SBGR24 ("only makes any sense in uncorrected sRGB"), so, as pixel_formats.h has it,
// color_type is srgba8 and the pixel format pixfmt_sbgr24_gamma: every color, the ctrls' included, is
// converted rgba -> srgba8 and its bytes are stored as they are.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_gamma_lut.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

int render_gamma_tuner(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::srgba8 color_type;
    typedef agg::gamma_lut<agg::int8u, agg::int8u, 8, 8> gamma_type;
    typedef agg::pixfmt_sbgr24_gamma<gamma_type> pixfmt_type;
    typedef agg::renderer_base<pixfmt_type> ren_base;

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::slider_ctrl<color_type> m_r    (5, 5,    350-5, 11,    false);
    agg::slider_ctrl<color_type> m_g    (5, 5+15, 350-5, 11+15, false);
    agg::slider_ctrl<color_type> m_b    (5, 5+30, 350-5, 11+30, false);
    agg::slider_ctrl<color_type> m_gamma(5, 5+45, 350-5, 11+45, false);
    agg::rbox_ctrl<agg::srgba8>  m_pattern(355, 1, 495, 60, false);
    m_r.value(1.0);
    m_r.label("R=%.2f");
    m_g.value(1.0);
    m_g.label("G=%.2f");
    m_b.value(1.0);
    m_b.label("B=%.2f");
    m_gamma.range(0.5, 4.0);
    m_gamma.value(2.2);
    m_gamma.label("Gamma=%.2f");
    m_pattern.text_size(8);
    m_pattern.add_item("Horizontal");
    m_pattern.add_item("Vertical");
    m_pattern.add_item("Checkered");
    m_pattern.cur_item(2);

    if (params.size() > 0) m_gamma.value(params[0]);
    if (params.size() > 1) m_r.value(params[1]);
    if (params.size() > 2) m_g.value(params[2]);
    if (params.size() > 3) m_b.value(params[3]);
    if (params.size() > 4) m_pattern.cur_item(int(params[4]));

    headless::canvas cv(w, h, 3);

    // on_draw()
    double g = m_gamma.value();
    gamma_type gamma(g);
    pixfmt_type pixf(cv.rbuf, gamma);
    ren_base renb(pixf);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    enum { square_size = 400, ver_strips = 5 };
    color_type span1[square_size];
    color_type span2[square_size];
    color_type color(agg::rgba(m_r.value(), m_g.value(), m_b.value()));
    unsigned i, j;

    // Vertical gradient.
    for (i = 0; i < h; i++) {
        double k = (i - 80) / double(square_size - 1);
        if (i < 80)              k = 0.0;
        if (i >= 80+square_size) k = 1.0;
        k = 1 - pow(k/2, 1/m_gamma.value());
        color_type c = color.gradient(color_type(0,0,0), k);
        renb.copy_hline(0, i, w-1, c);
    }

    // Spans.
    switch (m_pattern.cur_item()) {
    case 0:
        for (i = 0; i < square_size; i++) {
            span1[i] = span2[i] = color;
            span1[i].a = i * color_type::full_value() / square_size;
            span2[i].a = color_type::full_value() - span1[i].a;
        }
        break;
    case 1:
        for (i = 0; i < square_size; i++) {
            span1[i] = span2[i] = color;
            if (i & 1) {
                span1[i].a = i * color_type::full_value() / square_size;
                span2[i].a = span1[i].a;
            } else {
                span1[i].a = color_type::full_value() - i * color_type::full_value() / square_size;
                span2[i].a = span1[i].a;
            }
        }
        break;
    case 2:
        for (i = 0; i < square_size; i++) {
            span1[i] = span2[i] = color;
            if (i & 1) {
                span1[i].a = i * color_type::full_value() / square_size;
                span2[i].a = color_type::full_value() - span1[i].a;
            } else {
                span2[i].a = i * color_type::full_value() / square_size;
                span1[i].a = color_type::full_value() - span2[i].a;
            }
        }
        break;
    }

    renb.copy_bar(50, 80, 50+square_size-1, 80+square_size-1, agg::rgba(0,0,0));

    // The pattern.
    for (i = 0; i < square_size; i += 2) {
        double k = i / double(square_size - 1);
        k = 1 - pow(k, 1/m_gamma.value());
        color_type c = color.gradient(agg::rgba(0,0,0), k);
        for (j = 0; j < square_size; j++) {
            span1[j].r = span2[j].r = c.r;
            span1[j].g = span2[j].g = c.g;
            span1[j].b = span2[j].b = c.b;
        }
        renb.blend_color_hspan(50, i + 80 + 0, square_size, span1, 0, 255);
        renb.blend_color_hspan(50, i + 80 + 1, square_size, span2, 0, 255);
    }

    // Vertical strips.
    for (i = 0; i < square_size; i++) {
        double k = i / double(square_size - 1);
        k = 1 - pow(k/2, 1/m_gamma.value());
        color_type c = color.gradient(color_type(0,0,0), k);
        for (j = 0; j < ver_strips; j++) {
            int xc = square_size * (j + 1) / (ver_strips + 1);
            renb.copy_hline(50+xc-10, i+80, 50+xc+10, c);
        }
    }

    agg::render_ctrl(ras, sl, renb, m_gamma);
    agg::render_ctrl(ras, sl, renb, m_r);
    agg::render_ctrl(ras, sl, renb, m_g);
    agg::render_ctrl(ras, sl, renb, m_b);
    agg::render_ctrl(ras, sl, renb, m_pattern);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
