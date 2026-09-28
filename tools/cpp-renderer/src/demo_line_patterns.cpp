// line_patterns.cpp headless reproduction.
//   params: [scale_x=1.0] [start_x=0.0]
// The nine patterns are the example's 1.ppm..9.ppm (line_pattern_source.h).
#include <algorithm>
#include <cstdio>
#include <string>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_pattern_filters_rgba.h"
#include "agg_renderer_outline_aa.h"
#include "agg_renderer_outline_image.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_bezier_ctrl.h"

#include "common.h"
#include "line_pattern_source.h"

using namespace line_patterns;

namespace {

typedef agg::rgba8 color_type;

} // namespace

int render_line_patterns(unsigned w, unsigned h,
                         const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::rasterizer_scanline_aa<> rasterizer_scanline;
    typedef agg::scanline_p8 scanline;
    typedef agg::line_image_pattern<agg::pattern_filter_bilinear_rgba<color_type> > pattern_type;
    typedef agg::renderer_outline_image<renderer_base, pattern_type> renderer_type;
    typedef agg::rasterizer_outline_aa<renderer_type> rasterizer_type;

    double scale_x = params.size() > 0 ? params[0] : 1.0;
    double start_x = params.size() > 1 ? params[1] : 0.0;

    headless::image_rgba images[9];
    for (int i = 0; i < 9; ++i) {
        images[i] = load_pattern_flip_y(i + 1);
        if (!images[i].ok) {
            fprintf(stderr, "line_patterns: cannot load %d.ppm from %s\n", i + 1, AGG_LINE_PATTERNS_DIR);
            return 1;
        }
    }

    // The ctrls are made as the example makes them (flip_y = true, so the sliders get !flip_y).
    agg::srgba8 ctrl_color(agg::rgba(0, 0.3, 0.5, 0.3));
    agg::bezier_ctrl<color_type> curves[9];
    static const double pts[9][8] = {
        {64, 19, 14, 126, 118, 266, 19, 265},
        {112, 113, 178, 32, 200, 132, 125, 438},
        {401, 24, 326, 149, 285, 11, 177, 77},
        {188, 427, 129, 295, 19, 283, 25, 410},
        {451, 346, 302, 218, 265, 441, 459, 400},
        {454, 198, 14, 13, 220, 291, 483, 283},
        {301, 398, 355, 231, 209, 211, 170, 353},
        {484, 101, 222, 33, 486, 435, 487, 138},
        {143, 147, 11, 45, 83, 427, 132, 197},
    };
    for (int i = 0; i < 9; ++i) {
        curves[i].line_color(ctrl_color);
        curves[i].curve(pts[i][0], pts[i][1], pts[i][2], pts[i][3], pts[i][4], pts[i][5], pts[i][6], pts[i][7]);
        curves[i].no_transform();
    }
    agg::slider_ctrl<color_type> m_scale_x(5.0, 5.0, 240.0, 12.0, false);
    agg::slider_ctrl<color_type> m_start_x(250.0, 5.0, 495.0, 12.0, false);
    m_scale_x.label("Scale X=%.2f");
    m_scale_x.range(0.2, 3.0);
    m_scale_x.value(scale_x);
    m_scale_x.no_transform();
    m_start_x.label("Start X=%.2f");
    m_start_x.range(0.0, 10.0);
    m_start_x.value(start_x);
    m_start_x.no_transform();

    // on_draw.
    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    renderer_base ren_base(pf);
    ren_base.clear(agg::rgba(1.0, 1.0, 0.95));

    rasterizer_scanline ras;
    scanline sl;

    agg::pattern_filter_bilinear_rgba<color_type> fltr;
    pattern_type patt(fltr);
    renderer_type ren_img(ren_base, patt);
    rasterizer_type ras_img(ren_img);

    for (int i = 0; i < 9; ++i) {
        pattern_src_brightness_to_alpha src(images[i]);
        patt.create(src);
        ren_img.scale_x(m_scale_x.value());
        ren_img.start_x(m_start_x.value());
        ras_img.add_path(curves[i].curve());
    }

    for (int i = 0; i < 9; ++i) agg::render_ctrl(ras, sl, ren_base, curves[i]);
    agg::render_ctrl(ras, sl, ren_base, m_scale_x);
    agg::render_ctrl(ras, sl, ren_base, m_start_x);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
