// gamma_correction.cpp headless reproduction (default state: 400x320, thickness 1, contrast 1, gamma 1,
// radii width/3 and height/3). Params (all optional): thickness contrast gamma, then rx ry.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_stroke.h"
#include "agg_ellipse.h"
#include "agg_gamma_lut.h"
#include "agg_gamma_functions.h"
#include "agg_path_storage.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

int render_gamma_correction(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::gamma_lut<agg::int8u, agg::int8u, 8, 8> gamma_type;
    typedef agg::pixfmt_bgr24_gamma<gamma_type> pixfmt_type;
    typedef agg::renderer_base<pixfmt_type> ren_base;

    double thickness = params.size() > 0 ? params[0] : 1.0;
    double contrast = params.size() > 1 ? params[1] : 1.0;
    double g = params.size() > 2 ? params[2] : 1.0;
    double width = w;
    double height = h;
    double m_rx = width / 3.0;
    double m_ry = height / 3.0;
    if (params.size() >= 5) { m_rx = params[3]; m_ry = params[4]; }

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::slider_ctrl<agg::rgba8> m_thickness(5, 5,    400-5, 11,    false);
    agg::slider_ctrl<agg::rgba8> m_contrast (5, 5+15, 400-5, 11+15, false);
    agg::slider_ctrl<agg::rgba8> m_gamma    (5, 5+30, 400-5, 11+30, false);
    m_thickness.label("Thickness=%3.2f");
    m_thickness.range(0.0, 3.0);
    m_thickness.value(thickness);
    m_contrast.label("Contrast");
    m_contrast.range(0.0, 1.0);
    m_contrast.value(contrast);
    m_gamma.label("Gamma=%3.2f");
    m_gamma.range(0.5, 3.0);
    m_gamma.value(g);

    headless::canvas cv(w, h, 3);

    // on_draw()
    g = m_gamma.value();
    gamma_type gamma(g);
    pixfmt_type pixf(cv.rbuf, gamma);
    ren_base renb(pixf);
    renb.clear(agg::rgba(1, 1, 1));

    double dark = 1.0 - m_contrast.value();
    double light = m_contrast.value();
    renb.copy_bar(0,0,int(width)/2, int(height),                agg::rgba(dark,dark,dark));
    renb.copy_bar(int(width)/2+1,0, int(width), int(height),    agg::rgba(light,light,light));
    renb.copy_bar(0,int(height)/2+1, int(width), int(height),   agg::rgba(1.0,dark,dark));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;
    agg::path_storage path;

    double x = (width - 256.0) / 2.0;
    double y = 50.0;
    agg::gamma_power gp(g);
    for (unsigned i = 0; i < 256; i++) {
        double v = double(i) / 255.0;
        double gval = gp(v);
        double dy = gval * 255.0;
        if (i == 0) path.move_to(x + i, y + dy);
        else        path.line_to(x + i, y + dy);
    }
    agg::conv_stroke<agg::path_storage> gpoly(path);
    gpoly.width(2.0);
    ras.reset();
    ras.add_path(gpoly);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(80,127,80));

    agg::ellipse ell(width / 2, height / 2, m_rx, m_ry, 150);
    agg::conv_stroke<agg::ellipse> poly(ell);
    poly.width(m_thickness.value());
    ras.reset();
    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(255,0,0));
    ell.init(width / 2, height / 2, m_rx-5.0, m_ry-5.0, 150);
    ras.reset();
    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(0,255,0));
    ell.init(width / 2, height / 2, m_rx-10.0, m_ry-10.0, 150);
    ras.reset();
    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(0,0,255));
    ell.init(width / 2, height / 2, m_rx-15.0, m_ry-15.0, 150);
    ras.reset();
    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(0,0,0));
    ell.init(width / 2, height / 2, m_rx-20.0, m_ry-20.0, 150);
    ras.reset();
    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::srgba8(255,255,255));

    agg::render_ctrl(ras, sl, renb, m_thickness);
    agg::render_ctrl(ras, sl, renb, m_contrast);
    agg::render_ctrl(ras, sl, renb, m_gamma);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
