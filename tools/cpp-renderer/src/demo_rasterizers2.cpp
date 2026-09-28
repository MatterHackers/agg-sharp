// rasterizers2.cpp headless reproduction (default state).
//   m_step=0.1, m_width=3.0, m_scale_pattern=on, others off, start_angle=0.
#include <cmath>
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_rasterizer_outline.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_renderer_primitives.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_pattern_filters_rgba.h"
#include "agg_renderer_outline_aa.h"
#include "agg_renderer_outline_image.h"
#include "agg_gsv_text.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

static const agg::int32u pixmap_chain[] = {
    16, 7,
    0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0xb4c29999, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff,
    0x00ffffff, 0x00ffffff, 0x0cfbf9f9, 0xff9a5757, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff,
    0x00ffffff, 0x5ae0cccc, 0xffa46767, 0xff660000, 0xff975252, 0x7ed4b8b8, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0xa8c6a0a0, 0xff7f2929, 0xff670202, 0x9ecaa6a6, 0x5ae0cccc, 0x00ffffff,
    0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xa4c7a2a2, 0x3affff00, 0x3affff00, 0xff975151, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000,
    0x00ffffff, 0x5ae0cccc, 0xffa46767, 0xff660000, 0xff954f4f, 0x7ed4b8b8, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0x5ae0cccc, 0xa8c6a0a0, 0xff7f2929, 0xff670202, 0x9ecaa6a6, 0x5ae0cccc, 0x00ffffff,
    0x00ffffff, 0x00ffffff, 0x0cfbf9f9, 0xff9a5757, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xff660000, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff,
    0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0xb4c29999, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xff9a5757, 0xb4c29999, 0x00ffffff, 0x00ffffff, 0x00ffffff, 0x00ffffff
};

namespace agg {
    class pattern_pixmap_argb32 {
    public:
        typedef rgba color_type;
        pattern_pixmap_argb32(const int32u* pixmap) : m_pixmap(pixmap) {}
        unsigned width() const { return m_pixmap[0]; }
        unsigned height() const { return m_pixmap[1]; }
        rgba pixel(int x, int y) const {
            int32u p = m_pixmap[y * width() + x + 2];
            srgba8 c((p >> 16) & 0xFF, (p >> 8) & 0xFF, p & 0xFF, p >> 24);
            return rgba(c).premultiply();
        }
    private:
        const int32u* m_pixmap;
    };
}

namespace {
class spiral {
public:
    spiral(double x, double y, double r1, double r2, double step, double start_angle = 0)
        : m_x(x), m_y(y), m_r1(r1), m_r2(r2), m_step(step), m_start_angle(start_angle),
          m_angle(start_angle), m_da(agg::deg2rad(8.0)), m_dr(m_step / 45.0) {}
    void rewind(unsigned) { m_angle = m_start_angle; m_curr_r = m_r1; m_start = true; }
    unsigned vertex(double* x, double* y) {
        if (m_curr_r > m_r2) return agg::path_cmd_stop;
        *x = m_x + cos(m_angle) * m_curr_r;
        *y = m_y + sin(m_angle) * m_curr_r;
        m_curr_r += m_dr;
        m_angle += m_da;
        if (m_start) { m_start = false; return agg::path_cmd_move_to; }
        return agg::path_cmd_line_to;
    }
private:
    double m_x, m_y, m_r1, m_r2, m_step, m_start_angle, m_angle, m_curr_r, m_da, m_dr;
    bool m_start;
};

struct roundoff {
    void transform(double* x, double* y) const { *x = floor(*x); *y = floor(*y); }
};
} // namespace

int render_rasterizers2(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::pixfmt_bgr24_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt_pre> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_aa;
    typedef agg::renderer_primitives<renderer_base> renderer_prim;
    typedef agg::rasterizer_outline<renderer_prim> rasterizer_outline;
    typedef agg::rasterizer_scanline_aa<> rasterizer_scanline;
    typedef agg::scanline_p8 scanline;
    typedef agg::renderer_outline_aa<renderer_base> renderer_oaa;
    typedef agg::pattern_filter_bilinear_rgba<agg::rgba8> pattern_filter;
    typedef agg::line_image_pattern_pow2<pattern_filter> image_pattern;
    typedef agg::renderer_outline_image<renderer_base, image_pattern> renderer_img;
    typedef agg::rasterizer_outline_aa<renderer_oaa> rasterizer_outline_aa;
    typedef agg::rasterizer_outline_aa<renderer_img> rasterizer_outline_img;

    double m_step_val = params.size() > 0 ? params[0] : 0.1;
    double m_width_val = params.size() > 1 ? params[1] : 3.0;
    bool accurate_joins = params.size() > 2 ? params[2] > 0.5 : false;
    double start_angle_deg = params.size() > 3 ? params[3] : 0.0;
    bool scale_pattern = params.size() > 4 ? params[4] > 0.5 : true;
    bool rotate_status = params.size() > 5 ? params[5] > 0.5 : false;
    bool test_status = params.size() > 6 ? params[6] > 0.5 : false;
    double m_start_angle = agg::deg2rad(start_angle_deg);

    headless::canvas cv(w, h, 3);
    pixfmt_pre pf(cv.rbuf);
    renderer_base ren_base(pf);
    renderer_aa ren_aa(ren_base);
    renderer_prim ren_prim(ren_base);
    rasterizer_scanline ras_aa;
    scanline sl;
    rasterizer_outline ras_al(ren_prim);
    agg::line_profile_aa prof;
    prof.width(m_width_val);
    renderer_oaa ren_oaa(ren_base, prof);
    rasterizer_outline_aa ras_oaa(ren_oaa);
    ras_oaa.line_join(accurate_joins ? agg::outline_miter_accurate_join : agg::outline_round_join);
    ras_oaa.round_cap(true);

    pattern_filter filter;
    agg::pattern_pixmap_argb32 src(pixmap_chain);
    agg::line_image_scale<agg::pattern_pixmap_argb32> src_scaled(src, m_width_val);
    image_pattern pattern(filter);
    if (scale_pattern) pattern.create(src_scaled);
    else pattern.create(src);
    renderer_img ren_img(ren_base, pattern);
    if (scale_pattern) ren_img.scale_x(m_width_val / src.height());
    rasterizer_outline_img ras_img(ren_img);

    ren_base.clear(agg::rgba(1.0, 1.0, 0.95));

    // draw_aliased_pix_accuracy
    {
        spiral s1(w / 5.0, h / 4.0 + 50, 5, 70, 8, m_start_angle);
        roundoff rn;
        agg::conv_transform<spiral, roundoff> trans(s1, rn);
        ren_prim.line_color(agg::rgba(0.4, 0.3, 0.1));
        ras_al.add_path(trans);
    }
    // draw_aliased_subpix_accuracy
    {
        spiral s2(w / 2.0, h / 4.0 + 50, 5, 70, 8, m_start_angle);
        ren_prim.line_color(agg::rgba(0.4, 0.3, 0.1));
        ras_al.add_path(s2);
    }
    // draw_anti_aliased_outline
    {
        spiral s3(w / 5.0, h - h / 4.0 + 20, 5, 70, 8, m_start_angle);
        ren_oaa.color(agg::rgba(0.4, 0.3, 0.1));
        ras_oaa.add_path(s3);
    }
    // draw_anti_aliased_scanline
    {
        spiral s4(w / 2.0, h - h / 4.0 + 20, 5, 70, 8, m_start_angle);
        agg::conv_stroke<spiral> stroke(s4);
        stroke.width(m_width_val);
        stroke.line_cap(agg::round_cap);
        ren_aa.color(agg::rgba(0.4, 0.3, 0.1));
        ras_aa.add_path(stroke);
        agg::render_scanlines(ras_aa, sl, ren_aa);
    }
    // draw_anti_aliased_outline_img
    {
        spiral s5(w - w / 5.0, h - h / 4.0 + 20, 5, 70, 8, m_start_angle);
        ras_img.add_path(s5);
    }

    // Text labels.
    auto text = [&](double x, double y, const char* txt) {
        agg::gsv_text t;
        t.size(8);
        t.text(txt);
        t.start_point(x, y);
        agg::conv_stroke<agg::gsv_text> stroke(t);
        stroke.width(0.7);
        ras_aa.add_path(stroke);
        ren_aa.color(agg::rgba(0, 0, 0));
        agg::render_scanlines(ras_aa, sl, ren_aa);
    };
    text(50, 80, "Bresenham lines,\n\nregular accuracy");
    text(w / 2.0 - 50, 80, "Bresenham lines,\n\nsubpixel accuracy");
    text(50, h / 2.0 + 50, "Anti-aliased lines");
    text(w / 2.0 - 50, h / 2.0 + 50, "Scanline rasterizer");
    text(w - w / 5.0 - 50, h / 2.0 + 50, "Arbitrary Image Pattern");

    // Controls rendered on a plain (non-premultiplied) view of the same buffer.
    pixfmt pf2(cv.rbuf);
    agg::renderer_base<pixfmt> ren_base2(pf2);

    agg::slider_ctrl<agg::rgba8> m_step(10.0, 10.0 + 4.0, 150.0, 10.0 + 8.0 + 4.0, false);
    m_step.range(0.0, 2.0); m_step.value(m_step_val); m_step.label("Step=%1.2f"); m_step.no_transform();
    agg::slider_ctrl<agg::rgba8> m_width(150.0 + 10.0, 10.0 + 4.0, 400 - 10.0, 10.0 + 8.0 + 4.0, false);
    m_width.range(0.0, 14.0); m_width.value(m_width_val); m_width.label("Width=%1.2f"); m_width.no_transform();
    agg::cbox_ctrl<agg::rgba8> m_test(10.0, 10.0 + 4.0 + 16.0, "Test Performance", false);
    m_test.text_size(9.0, 7.0); m_test.no_transform(); m_test.status(test_status);
    agg::cbox_ctrl<agg::rgba8> m_rotate(130 + 10.0, 10.0 + 4.0 + 16.0, "Rotate", false);
    m_rotate.text_size(9.0, 7.0); m_rotate.no_transform(); m_rotate.status(rotate_status);
    agg::cbox_ctrl<agg::rgba8> m_accurate_joins(200 + 10.0, 10.0 + 4.0 + 16.0, "Accurate Joins", false);
    m_accurate_joins.text_size(9.0, 7.0); m_accurate_joins.no_transform(); m_accurate_joins.status(accurate_joins);
    agg::cbox_ctrl<agg::rgba8> m_scale_pattern(310 + 10.0, 10.0 + 4.0 + 16.0, "Scale Pattern", false);
    m_scale_pattern.text_size(9.0, 7.0); m_scale_pattern.no_transform(); m_scale_pattern.status(scale_pattern);

    agg::render_ctrl(ras_aa, sl, ren_base2, m_step);
    agg::render_ctrl(ras_aa, sl, ren_base2, m_width);
    agg::render_ctrl(ras_aa, sl, ren_base2, m_test);
    agg::render_ctrl(ras_aa, sl, ren_base2, m_rotate);
    agg::render_ctrl(ras_aa, sl, ren_base2, m_accurate_joins);
    agg::render_ctrl(ras_aa, sl, ren_base2, m_scale_pattern);

    pixfmt pf_out(cv.rbuf);
    return headless::write_raw(out, pf_out, w, h) ? 0 : 1;
}
