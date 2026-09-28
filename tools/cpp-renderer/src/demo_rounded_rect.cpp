// rounded_rect.cpp headless reproduction (default state: handles at (100,100)
// and (500,350), radius 25, subpixel offset 0.5, white on black off; 600x400
// bgr24).
//
// Params (all optional): x0, y0, x1, y1, radius, offset, white_on_black.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_ellipse.h"
#include "agg_rounded_rect.h"
#include "agg_conv_stroke.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

int render_rounded_rect(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double m_x[2] = { params.size() > 0 ? params[0] : 100.0, params.size() > 2 ? params[2] : 500.0 };
    double m_y[2] = { params.size() > 1 ? params[1] : 100.0, params.size() > 3 ? params[3] : 350.0 };
    double radius = params.size() > 4 ? params[4] : 25.0;
    // The example never sets m_offset, so it keeps slider_ctrl's 0.5 of its
    // [-2, 3] range: 0.5 as well.
    double offset = params.size() > 5 ? params[5] : 0.5;
    bool white_on_black = params.size() > 6 ? params[6] > 0.5 : false;

    // flip_y = true in the demo => the sliders get !flip_y = false; the cbox
    // takes the default flip_y = false.
    agg::slider_ctrl<color_type> m_radius(10, 10, 600-10, 19, false);
    agg::slider_ctrl<color_type> m_offset(10, 10+20, 600-10, 19+20, false);
    agg::cbox_ctrl<color_type> m_white_on_black(10, 10+40, "White on black");

    m_radius.label("radius=%4.3f");
    m_radius.range(0.0, 50.0);
    m_radius.value(radius);

    m_offset.label("subpixel offset=%4.3f");
    m_offset.range(-2.0, 3.0);
    m_offset.value(offset);

    // srgba8 -> rgba8 is an implicit sRGB-to-linear conversion.
    m_white_on_black.text_color(agg::srgba8(127, 127, 127));
    m_white_on_black.inactive_color(agg::srgba8(127, 127, 127));
    m_white_on_black.status(white_on_black);

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid ren(rb);

    rb.clear(m_white_on_black.status() ? agg::rgba(0,0,0) : agg::rgba(1,1,1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    agg::ellipse e;

    ren.color(agg::srgba8(127,127,127));
    e.init(m_x[0], m_y[0], 3, 3, 16);
    ras.add_path(e);
    agg::render_scanlines(ras, sl, ren);
    e.init(m_x[1], m_y[1], 3, 3, 16);
    ras.add_path(e);
    agg::render_scanlines(ras, sl, ren);

    double d = m_offset.value();

    agg::rounded_rect r(m_x[0]+d, m_y[0]+d, m_x[1]+d, m_y[1]+d, m_radius.value());
    r.normalize_radius();

    agg::conv_stroke<agg::rounded_rect> p(r);
    p.width(1.0);
    ras.add_path(p);
    ren.color(m_white_on_black.status() ? agg::rgba(1,1,1) : agg::rgba(0,0,0));
    agg::render_scanlines(ras, sl, ren);

    agg::render_ctrl(ras, sl, rb, m_radius);
    agg::render_ctrl(ras, sl, rb, m_offset);
    agg::render_ctrl(ras, sl, rb, m_white_on_black);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
