// flash_rasterizer.cpp headless reproduction: on_draw() in a 655x520 bgra32 window (flip_y = false), with
// the first shape of art/shapes.txt read and scaled to the window as agg_main does. The shape's fills go
// through one compound rasterizer (rasterizer_sl_clip_dbl, clipped to the window) and
// render_scanlines_compound into the premultiplied pixel format; each path with a line style is then
// stroked, and the help text drawn. Left out: the right-button hit test (no button is down), and the
// "Fill=%.2fms (%dFPS) ..." timer line, which differs every frame - the text starts with its line breaks
// so the help lines stay where they were.
//
// Params (all optional): shape index (read_next() calls past the first), then x, y, zoom steps, rotate
// steps for the on_key zoom/rotation about (x, y) (see flash_keys).
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_rasterizer_compound_aa.h"
#include "agg_scanline_u.h"
#include "agg_scanline_bin.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_curve.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_span_allocator.h"
#include "agg_gsv_text.h"
#include "agg_pixfmt_rgba.h"

#include "common.h"
#include "flash_shape.h"

namespace {

typedef agg::rgba8 color_type;

// The example's test_styles with is_solid() always true, as it ships; its gradient span is never used.
class test_styles {
public:
    test_styles(const color_type* solid_colors) : m_solid_colors(solid_colors) {}
    bool is_solid(unsigned) const { return true; }
    const color_type& color(unsigned style) const { return m_solid_colors[style]; }
    void generate_span(color_type*, int, int, unsigned, unsigned) {}
private:
    const color_type* m_solid_colors;
};

} // namespace

int render_flash_rasterizer(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt_pre> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_scanline;

    int shape_index = params.size() > 0 ? (int)params[0] : 0;

    agg::compound_shape m_shape;
    if (!m_shape.open(headless::asset_path("shapes.txt").c_str())) {
        fprintf(stderr, "flash_rasterizer: failed to open shapes.txt\n");
        return 1;
    }
    // agg_main's read_next(), then one Space key (read_next() and scale()) per shape past the first. The
    // scale matters: the next shape's bounding box is measured with the curves flattened at its scale.
    for (int i = 0; i <= shape_index; ++i) {
        m_shape.read_next();
        m_shape.scale(w, h);
    }

    color_type m_colors[100];
    agg::flash_palette(m_colors);

    agg::trans_affine m_scale;
    if (params.size() > 4) {
        agg::flash_keys(m_scale, params[1], params[2], (int)params[3], (int)params[4]);
    }

    headless::canvas cv(w, h, 4);
    pixfmt_pre pixf(cv.rbuf);
    renderer_base ren_base(pixf);
    ren_base.clear(agg::rgba(1.0, 1.0, 0.95));
    renderer_scanline ren(ren_base);

    unsigned i;

    agg::rasterizer_scanline_aa<agg::rasterizer_sl_clip_dbl> ras;
    agg::rasterizer_compound_aa<agg::rasterizer_sl_clip_dbl> rasc;
    agg::scanline_u8 sl;
    agg::scanline_bin sl_bin;
    agg::conv_transform<agg::compound_shape> shape(m_shape, m_scale);
    agg::conv_stroke<agg::conv_transform<agg::compound_shape> > stroke(shape);

    test_styles style_handler(m_colors);
    agg::span_allocator<color_type> alloc;

    m_shape.approximation_scale(m_scale.scale());

    // Fill shape
    rasc.clip_box(0, 0, w, h);
    rasc.reset();
    for (i = 0; i < m_shape.paths(); i++) {
        if (m_shape.style(i).left_fill >= 0 ||
            m_shape.style(i).right_fill >= 0) {
            rasc.styles(m_shape.style(i).left_fill,
                        m_shape.style(i).right_fill);
            rasc.add_path(shape, m_shape.style(i).path_id);
        }
    }
    agg::render_scanlines_compound(rasc, sl, sl_bin, ren_base, alloc, style_handler);

    // Draw strokes
    ras.clip_box(0, 0, w, h);
    stroke.width(sqrt(m_scale.scale()));
    stroke.line_join(agg::round_join);
    stroke.line_cap(agg::round_cap);
    for (i = 0; i < m_shape.paths(); i++) {
        ras.reset();
        if (m_shape.style(i).line >= 0) {
            ras.add_path(stroke, m_shape.style(i).path_id);
            ren.color(agg::srgba8(0, 0, 0, 128));
            agg::render_scanlines(ras, sl, ren);
        }
    }

    agg::gsv_text t;
    t.size(8.0);
    t.flip(true);

    agg::conv_stroke<agg::gsv_text> ts(t);
    ts.width(1.6);
    ts.line_cap(agg::round_cap);

    t.start_point(10.0, 20.0);
    t.text("\n\n"
           "Space: Next Shape\n\n"
           "+/- : ZoomIn/ZoomOut (with respect to the mouse pointer)");

    ras.add_path(ts);
    ren.color(agg::rgba(0, 0, 0));
    agg::render_scanlines(ras, sl, ren);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
