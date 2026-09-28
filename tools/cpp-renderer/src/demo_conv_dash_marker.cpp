// conv_dash_marker.cpp headless reproduction (default state).
// Params (all optional): x0 y0 x1 y1 x2 y2, cap (0 butt, 1 square, 2 round), width, close, even_odd,
// smooth, skip_controls, skip_smooth. skip_smooth=1 leaves out the smoothed fill and outline and dashes
// the raw path, so dashes and arrowheads can be compared before conv_smooth_poly1 is ported;
// skip_controls=1 leaves out the ctrls.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_conv_stroke.h"
#include "agg_conv_dash.h"
#include "agg_conv_curve.h"
#include "agg_conv_smooth_poly1.h"
#include "agg_conv_marker.h"
#include "agg_arrowhead.h"
#include "agg_vcgen_markers_term.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_path_storage.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

int render_conv_dash_marker(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> ren_base;

    // Triangle vertices (defaults from the demo constructor).
    double m_x[3], m_y[3];
    m_x[0] = 57 + 100; m_y[0] = 60;
    m_x[1] = 369 + 100; m_y[1] = 170;
    m_x[2] = 143 + 100; m_y[2] = 310;
    for (int i = 0; i < 3; ++i) {
        if (params.size() > (size_t)(i * 2))     m_x[i] = params[i * 2];
        if (params.size() > (size_t)(i * 2 + 1)) m_y[i] = params[i * 2 + 1];
    }
    int cap_item = params.size() > 6 ? (int)params[6] : 0;
    double width_val = params.size() > 7 ? params[7] : 3.0;
    bool close_status = params.size() > 8 ? params[8] > 0.5 : false;
    bool even_odd_status = params.size() > 9 ? params[9] > 0.5 : false;
    double smooth_val = params.size() > 10 ? params[10] : 1.0;
    bool skip_controls = params.size() > 11 ? params[11] > 0.5 : false;
    bool skip_smooth = params.size() > 12 ? params[12] > 0.5 : false;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    ren_base renb(pixf);
    renb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    agg::line_cap_e cap = agg::butt_cap;
    if (cap_item == 1) cap = agg::square_cap;
    if (cap_item == 2) cap = agg::round_cap;

    agg::path_storage path;
    path.move_to(m_x[0], m_y[0]);
    path.line_to(m_x[1], m_y[1]);
    path.line_to((m_x[0] + m_x[1] + m_x[2]) / 3.0, (m_y[0] + m_y[1] + m_y[2]) / 3.0);
    path.line_to(m_x[2], m_y[2]);
    if (close_status) path.close_polygon();

    path.move_to((m_x[0] + m_x[1]) / 2, (m_y[0] + m_y[1]) / 2);
    path.line_to((m_x[1] + m_x[2]) / 2, (m_y[1] + m_y[2]) / 2);
    path.line_to((m_x[2] + m_x[0]) / 2, (m_y[2] + m_y[0]) / 2);
    if (close_status) path.close_polygon();

    if (even_odd_status) ras.filling_rule(agg::fill_even_odd);

    // (1) filled triangles
    ras.add_path(path);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0.7, 0.5, 0.1, 0.5));

    // (2, 3, 4) smooth
    agg::conv_smooth_poly1<agg::path_storage> smooth(path);
    smooth.smooth_value(smooth_val);

    if (!skip_smooth) {
        ras.add_path(smooth);
        agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0.1, 0.5, 0.7, 0.1));

        agg::conv_stroke<agg::conv_smooth_poly1<agg::path_storage> > smooth_outline(smooth);
        ras.add_path(smooth_outline);
        agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0.0, 0.6, 0.0, 0.8));
    }

    agg::conv_curve<agg::conv_smooth_poly1<agg::path_storage> > smooth_curve(smooth);
    agg::conv_curve<agg::path_storage> raw_curve(path);
    typedef agg::conv_dash<agg::conv_curve<agg::conv_smooth_poly1<agg::path_storage> >,
                           agg::vcgen_markers_term> smooth_dash_type;
    typedef agg::conv_dash<agg::conv_curve<agg::path_storage>, agg::vcgen_markers_term> raw_dash_type;
    smooth_dash_type smooth_dash(smooth_curve);
    raw_dash_type raw_dash(raw_curve);
    agg::conv_stroke<smooth_dash_type> smooth_stroke(smooth_dash);
    agg::conv_stroke<raw_dash_type> raw_stroke(raw_dash);
    smooth_stroke.line_cap(cap);
    smooth_stroke.width(width_val);
    raw_stroke.line_cap(cap);
    raw_stroke.width(width_val);

    double k = ::pow(width_val, 0.7);

    agg::arrowhead ah;
    ah.head(4 * k, 4 * k, 3 * k, 2 * k);
    if (!close_status) ah.tail(1 * k, 1.5 * k, 3 * k, 5 * k);

    agg::conv_marker<agg::vcgen_markers_term, agg::arrowhead> smooth_arrow(smooth_dash.markers(), ah);
    agg::conv_marker<agg::vcgen_markers_term, agg::arrowhead> raw_arrow(raw_dash.markers(), ah);

    smooth_dash.add_dash(20.0, 5.0);
    smooth_dash.add_dash(5.0, 5.0);
    smooth_dash.add_dash(5.0, 5.0);
    smooth_dash.dash_start(10);
    raw_dash.add_dash(20.0, 5.0);
    raw_dash.add_dash(5.0, 5.0);
    raw_dash.add_dash(5.0, 5.0);
    raw_dash.dash_start(10);

    if (skip_smooth) {
        ras.add_path(raw_stroke);
        ras.add_path(raw_arrow);
    } else {
        ras.add_path(smooth_stroke);
        ras.add_path(smooth_arrow);
    }
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0.0, 0.0, 0.0));

    ras.filling_rule(agg::fill_non_zero);

    if (skip_controls) return headless::write_raw(out, pixf, w, h) ? 0 : 1;

    // Controls (enum flip_y = true => ctrl flip = !flip_y = false).
    agg::rbox_ctrl<agg::rgba8> m_cap(10.0, 10.0, 130.0, 80.0, false);
    m_cap.add_item("Butt Cap");
    m_cap.add_item("Square Cap");
    m_cap.add_item("Round Cap");
    m_cap.cur_item(cap_item);
    m_cap.no_transform();

    agg::slider_ctrl<agg::rgba8> m_width(130 + 10.0, 10.0 + 4.0, 130 + 150.0, 10.0 + 8.0 + 4.0, false);
    m_width.range(0.0, 10.0);
    m_width.value(width_val);
    m_width.label("Width=%1.2f");
    m_width.no_transform();

    agg::slider_ctrl<agg::rgba8> m_smooth(130 + 150.0 + 10.0, 10.0 + 4.0, 500 - 10.0, 10.0 + 8.0 + 4.0, false);
    m_smooth.range(0.0, 2.0);
    m_smooth.value(smooth_val);
    m_smooth.label("Smooth=%1.2f");
    m_smooth.no_transform();

    agg::cbox_ctrl<agg::rgba8> m_close(130 + 10.0, 10.0 + 4.0 + 16.0, "Close Polygons", false);
    m_close.status(close_status);
    m_close.no_transform();

    agg::cbox_ctrl<agg::rgba8> m_even_odd(130 + 150.0 + 10.0, 10.0 + 4.0 + 16.0, "Even-Odd Fill", false);
    m_even_odd.status(even_odd_status);
    m_even_odd.no_transform();

    agg::render_ctrl(ras, sl, renb, m_cap);
    agg::render_ctrl(ras, sl, renb, m_width);
    agg::render_ctrl(ras, sl, renb, m_smooth);
    agg::render_ctrl(ras, sl, renb, m_close);
    agg::render_ctrl(ras, sl, renb, m_even_odd);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
