// bspline.cpp headless reproduction (default state: 600x600, six points, 20 intermediate points, open).
// Params (all optional): num_points, close, skip_controls. skip_controls=1 leaves out the interactive
// polygon's outline and handles and the ctrls, so only the conv_bspline stroke is drawn.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_bspline.h"
#include "agg_conv_stroke.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_cbox_ctrl.h"
#include "ctrl/agg_slider_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

int render_bspline(unsigned w, unsigned h,
                   const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;

    double num_points = params.size() > 0 ? params[0] : 20.0;
    bool close = params.size() > 1 ? params[1] > 0.5 : false;
    bool skip_controls = params.size() > 2 ? params[2] > 0.5 : false;

    // on_init() with m_flip = 0.
    agg::interactive_polygon poly(6, 5.0);
    double width = w;
    double height = h;
    poly.xn(0) = 100;           poly.yn(0) = 100;
    poly.xn(1) = width - 100;   poly.yn(1) = 100;
    poly.xn(2) = width - 100;   poly.yn(2) = height - 100;
    poly.xn(3) = 100;           poly.yn(3) = height - 100;
    poly.xn(4) = width / 2;     poly.yn(4) = height / 2;
    poly.xn(5) = width / 2;     poly.yn(5) = height / 3;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba(1, 1, 1));

    agg::scanline_p8 sl;
    agg::rasterizer_scanline_aa<> ras;

    agg::simple_polygon_vertex_source path(poly.polygon(), poly.num_points(), false, close);

    typedef agg::conv_bspline<agg::simple_polygon_vertex_source> conv_bspline_type;
    conv_bspline_type bspline(path);
    bspline.interpolation_step(1.0 / num_points);

    agg::conv_stroke<conv_bspline_type> stroke(bspline);
    stroke.width(2.0);

    ras.add_path(stroke);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::rgba(0, 0, 0));

    if (skip_controls) return headless::write_raw(out, pixf, w, h) ? 0 : 1;

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::slider_ctrl<agg::rgba8> m_num_points(5.0, 5.0, 340.0, 12.0, false);
    agg::cbox_ctrl<agg::rgba8> m_close(350, 5.0, "Close", false);
    m_num_points.range(1.0, 40.0);
    m_num_points.value(num_points);
    m_num_points.label("Number of intermediate Points = %.3f");
    m_close.status(close);

    // The example never passes the Close box to the polygon tool, whose outline is always closed.
    ras.add_path(poly);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::rgba(0, 0.3, 0.5, 0.6));

    agg::render_ctrl(ras, sl, rb, m_close);
    agg::render_ctrl(ras, sl, rb, m_num_points);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
