// trans_curve1.cpp and trans_curve2.cpp headless reproductions (default state: 600x600, six-point
// polygons, 200 intermediate points, Preserve X scale and Fixed Length on, Animate off).
//
// The originals draw the text with a Win32 TrueType font (Times New Roman, 40px). That engine has no
// headless or cross-platform equivalent, so the text here is AGG's built-in gsv_text vector font,
// stroked into outlines and then pushed through the same conv_segmentator + conv_transform pipeline the
// TrueType outlines go through. Glyphs are placed one at a time from pen position (0, 3), and the loop
// stops once the pen passes the path's total length, as the originals do. agg-sharp's GsvCurveTextFont
// draws the same text the same way; its goldens prove the transforms byte for byte, while the demos
// themselves draw Liberation Serif through agg-sharp's TrueType engine.
//
// trans_curve1 params (all optional): num_points, close, preserve_x_scale, fixed_len, then x0 y0 .. x5 y5.
// trans_curve2 params (all optional): num_points, preserve_x_scale, fixed_len, then poly1 x0 y0 .. x5 y5,
// then poly2 x0 y0 .. x5 y5.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_bspline.h"
#include "agg_conv_segmentator.h"
#include "agg_conv_stroke.h"
#include "agg_conv_transform.h"
#include "agg_gsv_text.h"
#include "agg_trans_single_path.h"
#include "agg_trans_double_path.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_cbox_ctrl.h"
#include "ctrl/agg_slider_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

namespace {

static const char g_text[] =
    "Anti-Grain Geometry is designed as a set of loosely coupled "
    "algorithms and class templates united with a common idea, "
    "so that all the components can be easily combined. Also, "
    "the template based design allows you to replace any part of "
    "the library without the necessity to modify a single byte in "
    "the existing code. ";

// Stand-ins for the TrueType font: glyph height, width and outline width of the gsv_text strokes.
const double text_height = 24.0;
const double text_width = 18.0;
const double text_stroke_width = 2.0;

typedef agg::pixfmt_bgr24 pixfmt;
typedef agg::renderer_base<pixfmt> renderer_base;

// Draws g_text along `trans` glyph by glyph, stopping when the pen passes `total_length`.
template <class Trans>
void draw_text(agg::rasterizer_scanline_aa<>& ras, agg::scanline_p8& sl, renderer_base& rb,
               Trans& trans, double total_length) {
    agg::gsv_text glyph;
    glyph.size(text_height, text_width);

    agg::conv_stroke<agg::gsv_text> outline(glyph);
    outline.width(text_stroke_width);
    outline.line_join(agg::round_join);
    outline.line_cap(agg::round_cap);

    agg::conv_segmentator<agg::conv_stroke<agg::gsv_text> > segm(outline);
    segm.approximation_scale(3.0);
    agg::conv_transform<agg::conv_segmentator<agg::conv_stroke<agg::gsv_text> >, Trans> warped(segm, trans);

    double x = 0.0;
    double y = 3.0;
    char one[2] = {0, 0};
    for (const char* p = g_text; *p; ++p) {
        if (x > total_length) break;
        one[0] = *p;
        glyph.text(one);
        glyph.start_point(x, y);

        ras.reset();
        ras.add_path(warped);
        agg::render_scanlines_aa_solid(ras, sl, rb, agg::srgba8(0, 0, 0));

        // gsv_text's last vertex is where its pen ends up: the next glyph's start. rewind() does not move
        // the pen back, so it is put back at the glyph's start before the glyph is walked again.
        double vx, vy;
        unsigned cmd;
        glyph.start_point(x, y);
        glyph.rewind(0);
        while (!agg::is_stop(cmd = glyph.vertex(&vx, &vy))) {
            if (agg::is_vertex(cmd)) x = vx;
        }
    }
}

double param(const std::vector<double>& params, size_t i, double fallback) {
    return params.size() > i ? params[i] : fallback;
}

} // namespace

int render_trans_curve1(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    double num_points = param(params, 0, 200.0);
    bool close = param(params, 1, 0.0) > 0.5;
    bool preserve_x_scale = param(params, 2, 1.0) > 0.5;
    bool fixed_len = param(params, 3, 1.0) > 0.5;

    // on_init()
    const double init[12] = { 50, 50, 150 + 20, 150 - 20, 250 - 20, 250 + 20,
                              350 + 20, 350 - 20, 450 - 20, 450 + 20, 550, 550 };
    agg::interactive_polygon poly(6, 5.0);
    for (unsigned i = 0; i < 6; i++) {
        poly.xn(i) = param(params, 4 + i * 2, init[i * 2]);
        poly.yn(i) = param(params, 5 + i * 2, init[i * 2 + 1]);
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba(1, 1, 1));

    agg::scanline_p8 sl;
    agg::rasterizer_scanline_aa<> ras;

    poly.close(close);
    agg::simple_polygon_vertex_source path(poly.polygon(), poly.num_points(), false, close);

    typedef agg::conv_bspline<agg::simple_polygon_vertex_source> conv_bspline_type;
    conv_bspline_type bspline(path);
    bspline.interpolation_step(1.0 / num_points);

    agg::trans_single_path tcurve;
    tcurve.add_path(bspline);
    tcurve.preserve_x_scale(preserve_x_scale);
    if (fixed_len) tcurve.base_length(1120);

    draw_text(ras, sl, rb, tcurve, tcurve.total_length());

    agg::conv_stroke<conv_bspline_type> stroke(bspline);
    stroke.width(2.0);
    ras.add_path(stroke);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::srgba8(170, 50, 20, 100));

    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::rgba(0, 0.3, 0.5, 0.3));

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::slider_ctrl<agg::rgba8> m_num_points(5.0, 5.0, 340.0, 12.0, false);
    agg::cbox_ctrl<agg::rgba8> m_close(350, 5.0, "Close", false);
    agg::cbox_ctrl<agg::rgba8> m_preserve_x_scale(460, 5.0, "Preserve X scale", false);
    agg::cbox_ctrl<agg::rgba8> m_fixed_len(350, 25.0, "Fixed Length", false);
    agg::cbox_ctrl<agg::rgba8> m_animate(460, 25.0, "Animate", false);
    m_close.status(close);
    m_preserve_x_scale.status(preserve_x_scale);
    m_fixed_len.status(fixed_len);
    m_num_points.range(10.0, 400.0);
    m_num_points.value(num_points);
    m_num_points.label("Number of intermediate Points = %.3f");

    agg::render_ctrl(ras, sl, rb, m_close);
    agg::render_ctrl(ras, sl, rb, m_preserve_x_scale);
    agg::render_ctrl(ras, sl, rb, m_fixed_len);
    agg::render_ctrl(ras, sl, rb, m_animate);
    agg::render_ctrl(ras, sl, rb, m_num_points);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}

int render_trans_curve2(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    double num_points = param(params, 0, 200.0);
    bool preserve_x_scale = param(params, 1, 1.0) > 0.5;
    bool fixed_len = param(params, 2, 1.0) > 0.5;

    // on_init(): the two polygons sit 10 units either side of trans_curve1's.
    const double init[12] = { 50, 50, 150 + 20, 150 - 20, 250 - 20, 250 + 20,
                              350 + 20, 350 - 20, 450 - 20, 450 + 20, 550, 550 };
    agg::interactive_polygon poly1(6, 5.0);
    agg::interactive_polygon poly2(6, 5.0);
    poly1.close(false);
    poly2.close(false);
    for (unsigned i = 0; i < 6; i++) {
        poly1.xn(i) = param(params, 3 + i * 2, 10 + init[i * 2]);
        poly1.yn(i) = param(params, 4 + i * 2, -10 + init[i * 2 + 1]);
        poly2.xn(i) = param(params, 15 + i * 2, -10 + init[i * 2]);
        poly2.yn(i) = param(params, 16 + i * 2, 10 + init[i * 2 + 1]);
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba(1, 1, 1));

    agg::scanline_p8 sl;
    agg::rasterizer_scanline_aa<> ras;

    agg::simple_polygon_vertex_source path1(poly1.polygon(), poly1.num_points(), false, false);
    agg::simple_polygon_vertex_source path2(poly2.polygon(), poly2.num_points(), false, false);

    typedef agg::conv_bspline<agg::simple_polygon_vertex_source> conv_bspline_type;
    conv_bspline_type bspline1(path1);
    conv_bspline_type bspline2(path2);
    bspline1.interpolation_step(1.0 / num_points);
    bspline2.interpolation_step(1.0 / num_points);

    agg::trans_double_path tcurve;
    tcurve.preserve_x_scale(preserve_x_scale);
    if (fixed_len) tcurve.base_length(1140.0);
    tcurve.base_height(30.0);
    tcurve.add_paths(bspline1, bspline2);

    draw_text(ras, sl, rb, tcurve, tcurve.total_length1());

    agg::conv_stroke<conv_bspline_type> stroke1(bspline1);
    agg::conv_stroke<conv_bspline_type> stroke2(bspline2);
    stroke1.width(2.0);
    stroke2.width(2.0);
    ras.add_path(stroke1);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::srgba8(170, 50, 20, 100));
    ras.add_path(stroke2);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::srgba8(170, 50, 20, 100));

    ras.add_path(poly1);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::rgba(0, 0.3, 0.5, 0.2));
    ras.add_path(poly2);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::rgba(0, 0.3, 0.5, 0.2));

    agg::slider_ctrl<agg::rgba8> m_num_points(5.0, 5.0, 340.0, 12.0, false);
    agg::cbox_ctrl<agg::rgba8> m_fixed_len(350, 5.0, "Fixed Length", false);
    agg::cbox_ctrl<agg::rgba8> m_preserve_x_scale(465, 5.0, "Preserve X scale", false);
    agg::cbox_ctrl<agg::rgba8> m_animate(350, 25.0, "Animate", false);
    m_fixed_len.status(fixed_len);
    m_preserve_x_scale.status(preserve_x_scale);
    m_num_points.range(10.0, 400.0);
    m_num_points.value(num_points);
    m_num_points.label("Number of intermediate Points = %.3f");

    agg::render_ctrl(ras, sl, rb, m_fixed_len);
    agg::render_ctrl(ras, sl, rb, m_preserve_x_scale);
    agg::render_ctrl(ras, sl, rb, m_animate);
    agg::render_ctrl(ras, sl, rb, m_num_points);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
