// line_patterns_clip.cpp headless reproduction.
//   params: [scale_x=1.0] [start_x=0.0] [zoom_steps=0] [zoom_x=250] [zoom_y=250] [x0 y0 .. x4 y4]
// zoom_steps presses of '+' (or of '-' when negative) with the mouse at (zoom_x, zoom_y), after
// the polyline's points are placed. The pattern is the example's 1.ppm (line_pattern_source.h).
#include <cmath>
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_math.h"
#include "agg_rendering_buffer.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_pattern_filters_rgba.h"
#include "agg_renderer_outline_aa.h"
#include "agg_renderer_outline_image.h"
#include "agg_path_storage.h"
#include "agg_pixfmt_rgba.h"
#include "agg_gsv_text.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_polygon_ctrl.h"

#include "common.h"
#include "line_pattern_source.h"

int render_line_patterns_clip(unsigned w, unsigned h,
                              const std::vector<double>& params, const char* out) {
    typedef agg::rgba8 color_type;
    typedef agg::pixfmt_bgra32 pixfmt; // the example's AGG_BGRA32
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_scanline;
    typedef agg::rasterizer_scanline_aa<agg::rasterizer_sl_clip_int_sat> rasterizer_scanline;
    typedef agg::scanline_p8 scanline;
    typedef agg::line_image_pattern<agg::pattern_filter_bilinear_rgba<color_type> > pattern_type;
    typedef agg::renderer_outline_image<renderer_base, pattern_type> renderer_img_type;
    typedef agg::rasterizer_outline_aa<renderer_img_type, agg::line_coord_sat> rasterizer_img_type;
    typedef agg::renderer_outline_aa<renderer_base> renderer_line_type;
    typedef agg::rasterizer_outline_aa<renderer_line_type, agg::line_coord_sat> rasterizer_line_type;

    double scale_x = params.size() > 0 ? params[0] : 1.0;
    double start_x = params.size() > 1 ? params[1] : 0.0;
    int zoom_steps = params.size() > 2 ? int(params[2]) : 0;
    double zoom_x = params.size() > 3 ? params[3] : 250.0;
    double zoom_y = params.size() > 4 ? params[4] : 250.0;

    headless::image_rgba image = line_patterns::load_pattern_flip_y(1);
    if (!image.ok) {
        fprintf(stderr, "line_patterns_clip: cannot load 1.ppm from %s\n", AGG_LINE_PATTERNS_DIR);
        return 1;
    }

    // The constructor (flip_y = true, so the sliders get !flip_y).
    agg::srgba8 ctrl_color(agg::rgba(0, 0.3, 0.5, 0.3));
    agg::polygon_ctrl<color_type> m_line1(5);
    agg::slider_ctrl<color_type> m_scale_x(5.0, 5.0, 240.0, 12.0, false);
    agg::slider_ctrl<color_type> m_start_x(250.0, 5.0, 495.0, 12.0, false);
    agg::trans_affine m_scale;

    m_line1.line_color(ctrl_color);
    m_line1.xn(0) = 20;       m_line1.yn(0) = 20;
    m_line1.xn(1) = 500 - 20; m_line1.yn(1) = 500 - 20;
    m_line1.xn(2) = 500 - 60; m_line1.yn(2) = 20;
    m_line1.xn(3) = 40;       m_line1.yn(3) = 500 - 40;
    m_line1.xn(4) = 100;      m_line1.yn(4) = 300;
    if (params.size() >= 15) {
        for (unsigned i = 0; i < 5; ++i) {
            m_line1.xn(i) = params[5 + i * 2];
            m_line1.yn(i) = params[6 + i * 2];
        }
    }
    m_line1.close(false);
    m_line1.transform(m_scale);

    m_scale_x.label("Scale X=%.2f");
    m_scale_x.range(0.2, 3.0);
    m_scale_x.value(scale_x);
    m_scale_x.no_transform();
    m_start_x.label("Start X=%.2f");
    m_start_x.range(0.0, 10.0);
    m_start_x.value(start_x);
    m_start_x.no_transform();

    // on_key's '+' / '-'.
    for (int i = 0; i < std::abs(zoom_steps); ++i) {
        m_scale *= agg::trans_affine_translation(-zoom_x, -zoom_y);
        m_scale *= agg::trans_affine_scaling(zoom_steps > 0 ? 1.1 : 1 / 1.1);
        m_scale *= agg::trans_affine_translation(zoom_x, zoom_y);
    }

    // on_draw.
    headless::canvas cv(w, h, 4);
    pixfmt pf(cv.rbuf);
    renderer_base ren_base(pf);
    ren_base.clear(agg::rgba(0.5, 0.75, 0.85));
    renderer_scanline ren(ren_base);

    rasterizer_scanline ras;
    scanline sl;
    ras.clip_box(0, 0, w, h);

    line_patterns::pattern_src_brightness_to_alpha p1(image);
    agg::pattern_filter_bilinear_rgba<color_type> fltr;
    pattern_type patt(fltr);
    patt.create(p1);
    renderer_img_type ren_img(ren_base, patt);
    rasterizer_img_type ras_img(ren_img);

    agg::line_profile_aa profile;
    profile.smoother_width(10.0);
    profile.width(8.0);
    renderer_line_type ren_line(ren_base, profile);
    ren_line.color(agg::srgba8(0, 0, 127));
    rasterizer_line_type ras_line(ren_line);
    ras_line.round_cap(true);

    double w2 = 9.0;
    ren_img.scale_x(m_scale_x.value());
    ren_img.start_x(m_start_x.value());
    ren_img.clip_box(50 - w2, 50 - w2, w - 50 + w2, h - 50 + w2);
    ren_line.clip_box(50 - w2, 50 - w2, w - 50 + w2, h - 50 + w2);

    // draw_polyline.
    agg::poly_plain_adaptor<double> vs(m_line1.polygon(), m_line1.num_points(), m_line1.close());
    agg::conv_transform<agg::poly_plain_adaptor<double> > trans(vs, m_scale);

    ras_line.add_path(trans);
    ras_img.add_path(trans);

    ren_base.blend_bar(0, 0, (int)w, (int)h, agg::rgba(1, 1, 1), 200);
    ren_base.clip_box(50, 50, (int)w - 50, (int)h - 50);
    ren_base.copy_bar(0, 0, (int)w, (int)h, agg::rgba(1, 1, 1));

    ren_img.scale_x(m_scale_x.value());
    ren_img.start_x(m_start_x.value());
    ras_line.add_path(trans);
    ras_img.add_path(trans);

    ren_base.reset_clipping(true);

    m_line1.line_width(1 / m_scale.scale());
    m_line1.point_radius(5 / m_scale.scale());

    agg::render_ctrl(ras, sl, ren_base, m_line1);
    agg::render_ctrl(ras, sl, ren_base, m_scale_x);
    agg::render_ctrl(ras, sl, ren_base, m_start_x);

    char buf[256];
    agg::gsv_text t;
    t.size(10.0);

    agg::conv_stroke<agg::gsv_text> pt(t);
    pt.width(1.5);
    pt.line_cap(agg::round_cap);

    const double* p = m_line1.polygon();
    snprintf(buf, sizeof(buf), "Len=%.2f", agg::calc_distance(p[0], p[1], p[2], p[3]) * m_scale.scale());

    t.start_point(10.0, 30.0);
    t.text(buf);

    ras.add_path(pt);
    ren.color(agg::rgba(0, 0, 0));
    agg::render_scanlines(ras, sl, ren);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
