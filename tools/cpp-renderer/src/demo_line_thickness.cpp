// line_thickness.cpp headless reproduction (default state: 640x480, thickness 1, blur radius 1.5,
// Monochrome checked, Invert not). The original renders into a float BGR96 buffer; this one uses bgr24,
// so slight_blur rounds each pass to 8 bits (patched agg_blur.h rounds where C++ truncates). Left out: the
// "Blur: %3.2f ms" timer text, which differs every frame.
// Params (all optional): thickness, blur radius, monochrome (0/1), invert (0/1).
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_stroke.h"
#include "agg_pixfmt_rgb.h"
#include "agg_blur.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

namespace {

template<class T>
void set_ctrl_color(T& ctrl, const agg::rgba& clr) {
    ctrl.text_color(clr);
    ctrl.inactive_color(clr);
    ctrl.active_color(clr);
}

}

int render_line_thickness(unsigned w, unsigned h, const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;

    double thickness = params.size() > 0 ? params[0] : 1.0;
    double blur_radius = params.size() > 1 ? params[1] : 1.5;
    bool monochrome = params.size() > 2 ? params[2] != 0 : true;
    bool invert = params.size() > 3 ? params[3] != 0 : false;

    // flip_y = true, so every ctrl gets !flip_y = false.
    agg::slider_ctrl<color_type> m_slider1(10, 10, 640 - 10, 19, false);
    agg::slider_ctrl<color_type> m_slider2(10, 10 + 20, 640 - 10, 19 + 20, false);
    agg::cbox_ctrl<color_type> m_cbox1(10, 10 + 40, "Monochrome", false);
    agg::cbox_ctrl<color_type> m_cbox2(10, 10 + 60, "Invert", false);

    m_slider1.label("Line thickness=%1.2f");
    m_slider1.range(0.0, 5.0);
    m_slider1.value(thickness);

    m_slider2.label("Blur radius=%1.2f");
    m_slider2.range(0.0, 2.0);
    m_slider2.value(blur_radius);

    m_cbox1.status(monochrome);
    m_cbox2.status(invert);

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    agg::renderer_base<pixfmt> ren(pf);
    agg::scanline_u8 sl;
    agg::rasterizer_scanline_aa<> ras;
    agg::path_storage ps;
    agg::conv_stroke<agg::path_storage> pg(ps);

    agg::rgba clr1 = m_cbox1.status() ? agg::rgba(1, 1, 1) : agg::rgba(1, 0, 1);
    agg::rgba clr2 = m_cbox1.status() ? agg::rgba(0, 0, 0) : agg::rgba(0, 1, 0);
    agg::rgba foreground = m_cbox2.status() ? clr1 : clr2;
    agg::rgba background = m_cbox2.status() ? clr2 : clr1;

    set_ctrl_color(m_cbox1, foreground);
    set_ctrl_color(m_cbox2, foreground);

    ren.clear(background);

    // Draw row of straight lines.
    for (int i = 0; i < 20; ++i) {
        pg.width(m_slider1.value() * 0.3 * (i + 1));
        ps.remove_all();
        ps.move_to(20 + 30 * i, 310);
        ps.line_to(40 + 30 * i, 460);
        ras.add_path(pg);
        agg::render_scanlines_aa_solid(ras, sl, ren, foreground);
    }

    // Draw wheel of lines.
    for (int i = 0; i < 40; ++i) {
        pg.width(m_slider1.value());
        ps.remove_all();
        ps.move_to(320 + 20 * sin(i * agg::pi / 20), 180 + 20 * cos(i * agg::pi / 20));
        ps.line_to(320 + 100 * sin(i * agg::pi / 20), 180 + 100 * cos(i * agg::pi / 20));
        ras.add_path(pg);
        agg::render_scanlines_aa_solid(ras, sl, ren, foreground);
    }

    agg::apply_slight_blur(ren, m_slider2.value());

    agg::render_ctrl(ras, sl, ren, m_slider1);
    agg::render_ctrl(ras, sl, ren, m_slider2);
    agg::render_ctrl(ras, sl, ren, m_cbox1);
    agg::render_ctrl(ras, sl, ren, m_cbox2);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
