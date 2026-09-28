// gsv_text headless reproduction (default state: 600x500, text size 24, stroke width 1).
// Params (all optional): text size, stroke width.
//
// There is no gsv_text.cpp in AGG: this is agg-rust's custom gsv_text demo (demo/wasm/src/render/basic.rs
// gsv_text_demo, before agg-rust dropped it), written against C++ AGG so the port has a reference. AGG's
// built-in gsv_text vector font, stroked with round caps and joins: a title, a subtitle, the letters and
// digits in five colours, a large "Aa Bb Cc" and a size / stroke readout top right.
//
// The layout is y down (title at y = 40), so the demo runs as flip_y = false: the glyphs are flipped
// (gsv_text::flip) and the two sliders along the bottom get !flip_y = true. agg-rust took its sliders from
// the web page; these are AGG slider_ctrls so the port can be driven on its own. agg-rust's subtitle em
// dash is a plain hyphen here, and its title names C#: gsv_text's font is ASCII only.
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_stroke.h"
#include "agg_gsv_text.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

namespace {

typedef agg::pixfmt_bgr24 gsv_pixfmt;
typedef agg::renderer_base<gsv_pixfmt> gsv_ren_base;

void draw_gsv_line(agg::rasterizer_scanline_aa<>& ras, agg::scanline_p8& sl, gsv_ren_base& rb,
                   const char* text, double size, double x, double y, double stroke_width,
                   const agg::rgba8& color) {
    agg::gsv_text txt;
    txt.flip(true);
    txt.size(size, 0.0);
    txt.start_point(x, y);
    txt.text(text);
    agg::conv_stroke<agg::gsv_text> stroke(txt);
    stroke.width(stroke_width);
    stroke.line_cap(agg::round_cap);
    stroke.line_join(agg::round_join);
    ras.reset();
    ras.add_path(stroke);
    agg::render_scanlines_aa_solid(ras, sl, rb, color);
}

} // namespace

int render_gsv_text(unsigned w, unsigned h,
                    const std::vector<double>& params, const char* out) {
    typedef agg::rgba8 color_type;

    // flip_y = false => ctrl flip = !flip_y = true.
    agg::slider_ctrl<color_type> m_size(10, h - 40.0, w - 10.0, h - 33.0, true);
    m_size.label("Text Size=%.0f");
    m_size.range(8, 64);
    m_size.value(params.size() > 0 ? params[0] : 24.0);
    agg::slider_ctrl<color_type> m_stroke(10, h - 20.0, w - 10.0, h - 13.0, true);
    m_stroke.label("Stroke Width=%.1f");
    m_stroke.range(0.3, 4);
    m_stroke.value(params.size() > 1 ? params[1] : 1.0);

    headless::canvas cv(w, h, 3);
    gsv_pixfmt pf(cv.rbuf);
    gsv_ren_base rb(pf);
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    rb.clear(agg::rgba8(255, 255, 255));

    double text_size = m_size.value();
    double stroke_w = m_stroke.value();
    double x_off = 20.0;
    double y_off = 40.0;

    draw_gsv_line(ras, sl, rb, "AGG for C# - GSV Text", text_size * 1.5, x_off, y_off, stroke_w * 1.5,
                  agg::rgba8(0, 50, 120));
    draw_gsv_line(ras, sl, rb, "Built-in vector font - no dependencies", text_size * 0.7, x_off,
                  y_off + text_size * 2.0, stroke_w * 0.7, agg::rgba8(100, 100, 100));

    struct sample { const char* text; agg::rgba8 color; };
    const sample samples[] = {
        {"ABCDEFGHIJKLM", agg::rgba8(200, 0, 0)},
        {"NOPQRSTUVWXYZ", agg::rgba8(0, 150, 0)},
        {"abcdefghijklm", agg::rgba8(0, 0, 200)},
        {"nopqrstuvwxyz", agg::rgba8(150, 100, 0)},
        {"0123456789 !@#$%", agg::rgba8(0, 100, 150)},
    };
    const unsigned sample_count = sizeof(samples) / sizeof(samples[0]);

    double base_y = y_off + text_size * 4.0;
    for (unsigned i = 0; i < sample_count; i++) {
        double y = base_y + i * (text_size * 1.5);
        if (y + text_size > h) break;
        draw_gsv_line(ras, sl, rb, samples[i].text, text_size, x_off, y, stroke_w, samples[i].color);
    }

    double large_y = base_y + sample_count * (text_size * 1.5) + text_size;
    if (large_y + text_size * 3.0 < h) {
        draw_gsv_line(ras, sl, rb, "Aa Bb Cc", text_size * 2.5, x_off, large_y, stroke_w * 2.0,
                      agg::rgba8(30, 30, 30));
    }

    char label[64];
    snprintf(label, sizeof(label), "Size: %.0fpx  Stroke: %.1f", text_size, stroke_w);
    draw_gsv_line(ras, sl, rb, label, 12.0, w - 200.0, 20.0, 0.8, agg::rgba8(140, 140, 140, 200));

    agg::render_ctrl(ras, sl, rb, m_size);
    agg::render_ctrl(ras, sl, rb, m_stroke);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
