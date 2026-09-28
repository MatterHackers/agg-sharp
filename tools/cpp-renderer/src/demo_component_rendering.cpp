// component_rendering.cpp headless reproduction (default state: alpha 255,
// 320x320 bgr24 window cleared to white).
//
// Params (all optional): alpha (0..255).
//
// Three black circles, each drawn into a single channel of the bgr24 window
// through a gray8 pixel format that steps 3 bytes per pixel: the red channel
// (offset 2), the green (offset 1) and the blue (offset 0), so where a circle
// covers, that channel goes to 0 and the overlaps mix subtractively.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_ellipse.h"
#include "agg_pixfmt_gray.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

int render_component_rendering(unsigned w, unsigned h,
                               const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;
    typedef agg::gray8 gray_type;
    typedef agg::blender_gray<gray_type> gray_blender;

    // flip_y = true, so the slider gets !flip_y = false.
    agg::slider_ctrl<color_type> m_alpha(5, 5, 320 - 5, 10 + 5, false);
    m_alpha.label("Alpha=%1.0f");
    m_alpha.range(0, 255);
    m_alpha.value(params.size() > 0 ? params[0] : 255.0);

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);

    typedef agg::pixfmt_alpha_blend_gray<gray_blender, agg::rendering_buffer, 3, 2> pixfmt_r;
    typedef agg::pixfmt_alpha_blend_gray<gray_blender, agg::rendering_buffer, 3, 1> pixfmt_g;
    typedef agg::pixfmt_alpha_blend_gray<gray_blender, agg::rendering_buffer, 3, 0> pixfmt_b;

    pixfmt_r pfr(cv.rbuf);
    pixfmt_g pfg(cv.rbuf);
    pixfmt_b pfb(cv.rbuf);

    agg::renderer_base<pixfmt>   rbase(pf);
    agg::renderer_base<pixfmt_r> rbr(pfr);
    agg::renderer_base<pixfmt_g> rbg(pfg);
    agg::renderer_base<pixfmt_b> rbb(pfb);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    rbase.clear(agg::rgba(1, 1, 1));

    double width = w;
    double height = h;
    unsigned alpha = unsigned(m_alpha.value());

    agg::ellipse er(width / 2 - 0.87 * 50, height / 2 - 0.5 * 50, 100, 100, 100);
    ras.add_path(er);
    agg::render_scanlines_aa_solid(ras, sl, rbr, gray_type(0, alpha));

    agg::ellipse eg(width / 2 + 0.87 * 50, height / 2 - 0.5 * 50, 100, 100, 100);
    ras.add_path(eg);
    agg::render_scanlines_aa_solid(ras, sl, rbg, gray_type(0, alpha));

    agg::ellipse eb(width / 2, height / 2 + 50, 100, 100, 100);
    ras.add_path(eb);
    agg::render_scanlines_aa_solid(ras, sl, rbb, gray_type(0, alpha));

    agg::render_ctrl(ras, sl, rbase, m_alpha);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
