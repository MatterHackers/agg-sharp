// bezier_div.cpp headless reproduction (default state: 655x520, Subdiv, width 50, points and outline shown).
// Params (all optional): x1 y1 x2 y2 x3 y3 x4 y4 (the curve), width, show_points, show_outline,
// angle_tolerance (deg), approximation_scale, cusp_limit (deg), curve_type (0 incremental, 1 subdiv),
// case_type (-1 none, 0-8; only which rbox item is lit - the curve comes from the first eight params),
// inner_join (0-3), line_join (0-4), line_cap (0-2).
//
// C++ prints "Time=%.2fmks", the flattening measured with the wall clock; that is left out here (and
// in the port) so the frame is repeatable: the first line reads just "Num Points=%d".
#include <cmath>
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_math.h"
#include "agg_array.h"
#include "agg_curves.h"
#include "agg_ellipse.h"
#include "agg_gsv_text.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_conv_stroke.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_path_storage.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_bezier_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

namespace {

void bezier4_point(double x1, double y1, double x2, double y2,
                   double x3, double y3, double x4, double y4,
                   double mu, double* x, double* y) {
    double mum1 = 1 - mu;
    double mum13 = mum1 * mum1 * mum1;
    double mu3 = mu * mu * mu;
    *x = mum13 * x1 + 3 * mu * mum1 * mum1 * x2 + 3 * mu * mu * mum1 * x3 + mu3 * x4;
    *y = mum13 * y1 + 3 * mu * mum1 * mum1 * y2 + 3 * mu * mu * mum1 * y3 + mu3 * y4;
}

template <class Path>
void find_point(const Path& path, double dist, unsigned* i, unsigned* j) {
    int k;
    *j = path.size() - 1;
    for (*i = 0; (*j - *i) > 1;) {
        if (dist < path[k = (*i + *j) >> 1].dist) *j = k;
        else *i = k;
    }
}

struct curve_point {
    curve_point() {}
    curve_point(double x1, double y1, double mu1) : x(x1), y(y1), mu(mu1) {}
    double x, y, dist, mu;
};

// bezier_div.cpp's calc_max_error, verbatim but for the ctrl reads.
double calc_max_error(agg::curve4& curve, const double* p, double approximation_scale, double scale,
                      double* max_angle_error) {
    curve.approximation_scale(approximation_scale * scale);
    curve.init(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);

    agg::pod_bvector<agg::vertex_dist, 8> curve_points;
    unsigned cmd;
    double x, y;
    curve.rewind(0);
    while (!agg::is_stop(cmd = curve.vertex(&x, &y))) {
        if (agg::is_vertex(cmd)) curve_points.add(agg::vertex_dist(x, y));
    }
    unsigned i;
    double curve_dist = 0;
    for (i = 1; i < curve_points.size(); i++) {
        curve_points[i - 1].dist = curve_dist;
        curve_dist += agg::calc_distance(curve_points[i - 1].x, curve_points[i - 1].y,
                                         curve_points[i].x, curve_points[i].y);
    }
    curve_points[curve_points.size() - 1].dist = curve_dist;

    agg::pod_bvector<curve_point, 8> reference_points;
    for (i = 0; i < 4096; i++) {
        double mu = i / 4095.0;
        bezier4_point(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], mu, &x, &y);
        reference_points.add(curve_point(x, y, mu));
    }

    double reference_dist = 0;
    for (i = 1; i < reference_points.size(); i++) {
        reference_points[i - 1].dist = reference_dist;
        reference_dist += agg::calc_distance(reference_points[i - 1].x, reference_points[i - 1].y,
                                             reference_points[i].x, reference_points[i].y);
    }
    reference_points[reference_points.size() - 1].dist = reference_dist;

    unsigned idx1 = 0;
    unsigned idx2 = 1;
    double max_error = 0;
    for (i = 0; i < reference_points.size(); i++) {
        find_point(curve_points, reference_points[i].dist, &idx1, &idx2);
        double err = std::fabs(agg::calc_line_point_distance(curve_points[idx1].x, curve_points[idx1].y,
                                                             curve_points[idx2].x, curve_points[idx2].y,
                                                             reference_points[i].x, reference_points[i].y));
        if (err > max_error) max_error = err;
    }

    double aerr = 0;
    for (i = 2; i < curve_points.size(); i++) {
        double a1 = std::atan2(curve_points[i - 1].y - curve_points[i - 2].y,
                               curve_points[i - 1].x - curve_points[i - 2].x);
        double a2 = std::atan2(curve_points[i].y - curve_points[i - 1].y,
                               curve_points[i].x - curve_points[i - 1].x);
        double da = std::fabs(a1 - a2);
        if (da >= agg::pi) da = 2 * agg::pi - da;
        if (da > aerr) aerr = da;
    }

    *max_angle_error = aerr * 180.0 / agg::pi;
    return max_error * scale;
}

}  // namespace

int render_bezier_div(unsigned w, unsigned h,
                      const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_scanline;

    double p[8] = { 170, 424, 13, 87, 488, 423, 26, 333 };
    for (int i = 0; i < 8; i++) if (params.size() > (size_t)i) p[i] = params[i];
    double width = params.size() > 8 ? params[8] : 50.0;
    bool show_points = params.size() > 9 ? params[9] > 0.5 : true;
    bool show_outline = params.size() > 10 ? params[10] > 0.5 : true;
    double angle_tolerance = params.size() > 11 ? params[11] : 15.0;
    double approximation_scale = params.size() > 12 ? params[12] : 1.0;
    double cusp_limit = params.size() > 13 ? params[13] : 0.0;
    int curve_type = params.size() > 14 ? int(params[14]) : 1;
    int case_type = params.size() > 15 ? int(params[15]) : -1;
    int inner_join = params.size() > 16 ? int(params[16]) : 3;
    int line_join = params.size() > 17 ? int(params[17]) : 1;
    int line_cap = params.size() > 18 ? int(params[18]) : 0;

    // The ctrls first, as the constructor sets them up: the sliders clamp their values into range, and the
    // scene reads them back from the ctrls just as on_draw does.
    const bool flip = false;  // enum flip_y = true, ctrls get !flip_y
    agg::srgba8 ctrl_color(agg::rgba(0, 0.3, 0.5, 0.8));
    agg::bezier_ctrl<agg::rgba8> m_curve1;
    agg::slider_ctrl<agg::rgba8> m_angle_tolerance(5.0, 5.0, 240.0, 12.0, flip);
    agg::slider_ctrl<agg::rgba8> m_approximation_scale(5.0, 17 + 5.0, 240.0, 17 + 12.0, flip);
    agg::slider_ctrl<agg::rgba8> m_cusp_limit(5.0, 17 + 17 + 5.0, 240.0, 17 + 17 + 12.0, flip);
    agg::slider_ctrl<agg::rgba8> m_width(245.0, 5.0, 495.0, 12.0, flip);
    agg::cbox_ctrl<agg::rgba8> m_show_points(250.0, 15 + 5, "Show Points", flip);
    agg::cbox_ctrl<agg::rgba8> m_show_outline(250.0, 30 + 5, "Show Stroke Outline", flip);
    agg::rbox_ctrl<agg::rgba8> m_curve_type(535.0, 5.0, 535.0 + 115.0, 55.0, flip);
    agg::rbox_ctrl<agg::rgba8> m_case_type(535.0, 60.0, 535.0 + 115.0, 195.0, flip);
    agg::rbox_ctrl<agg::rgba8> m_inner_join(535.0, 200.0, 535.0 + 115.0, 290.0, flip);
    agg::rbox_ctrl<agg::rgba8> m_line_join(535.0, 295.0, 535.0 + 115.0, 385.0, flip);
    agg::rbox_ctrl<agg::rgba8> m_line_cap(535.0, 395.0, 535.0 + 115.0, 455.0, flip);

    m_curve1.line_color(ctrl_color);
    m_curve1.curve(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
    m_curve1.no_transform();

    m_angle_tolerance.label("Angle Tolerance=%.0f deg");
    m_angle_tolerance.range(0, 90);
    m_angle_tolerance.value(angle_tolerance);
    m_angle_tolerance.no_transform();

    m_approximation_scale.label("Approximation Scale=%.3f");
    m_approximation_scale.range(0.1, 5);
    m_approximation_scale.value(approximation_scale);
    m_approximation_scale.no_transform();

    m_cusp_limit.label("Cusp Limit=%.0f deg");
    m_cusp_limit.range(0, 90);
    m_cusp_limit.value(cusp_limit);
    m_cusp_limit.no_transform();

    m_width.label("Width=%.2f");
    m_width.range(-50, 100);
    m_width.value(width);
    m_width.no_transform();

    m_show_points.no_transform();
    m_show_points.status(show_points);

    m_show_outline.no_transform();
    m_show_outline.status(show_outline);

    m_curve_type.add_item("Incremental");
    m_curve_type.add_item("Subdiv");
    m_curve_type.cur_item(curve_type);
    m_curve_type.no_transform();

    m_case_type.text_size(7);
    m_case_type.text_thickness(1.0);
    m_case_type.add_item("Random");
    m_case_type.add_item("13---24");
    m_case_type.add_item("Smooth Cusp 1");
    m_case_type.add_item("Smooth Cusp 2");
    m_case_type.add_item("Real Cusp 1");
    m_case_type.add_item("Real Cusp 2");
    m_case_type.add_item("Fancy Stroke");
    m_case_type.add_item("Jaw");
    m_case_type.add_item("Ugly Jaw");
    m_case_type.cur_item(case_type);
    m_case_type.no_transform();

    m_inner_join.text_size(8);
    m_inner_join.add_item("Inner Bevel");
    m_inner_join.add_item("Inner Miter");
    m_inner_join.add_item("Inner Jag");
    m_inner_join.add_item("Inner Round");
    m_inner_join.cur_item(inner_join);
    m_inner_join.no_transform();

    m_line_join.text_size(8);
    m_line_join.add_item("Miter Join");
    m_line_join.add_item("Miter Revert");
    m_line_join.add_item("Round Join");
    m_line_join.add_item("Bevel Join");
    m_line_join.add_item("Miter Round");
    m_line_join.cur_item(line_join);
    m_line_join.no_transform();

    m_line_cap.text_size(8);
    m_line_cap.add_item("Butt Cap");
    m_line_cap.add_item("Square Cap");
    m_line_cap.add_item("Round Cap");
    m_line_cap.cur_item(line_cap);
    m_line_cap.no_transform();

    // on_draw
    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    renderer_base ren_base(pf);
    ren_base.clear(agg::rgba(1.0, 1.0, 0.95));
    renderer_scanline ren(ren_base);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    agg::path_storage path;
    double x, y;

    agg::curve4 curve;
    curve.approximation_method(agg::curve_approximation_method_e(m_curve_type.cur_item()));
    curve.approximation_scale(m_approximation_scale.value());
    curve.angle_tolerance(agg::deg2rad(m_angle_tolerance.value()));
    curve.cusp_limit(agg::deg2rad(m_cusp_limit.value()));
    double max_angle_error_01 = 0, max_angle_error_1 = 0, max_angle_error1 = 0;
    double max_angle_error_10 = 0, max_angle_error_100 = 0;
    double as = m_approximation_scale.value();
    double max_error_01 = calc_max_error(curve, p, as, 0.01, &max_angle_error_01);
    double max_error_1 = calc_max_error(curve, p, as, 0.1, &max_angle_error_1);
    double max_error1 = calc_max_error(curve, p, as, 1, &max_angle_error1);
    double max_error_10 = calc_max_error(curve, p, as, 10, &max_angle_error_10);
    double max_error_100 = calc_max_error(curve, p, as, 100, &max_angle_error_100);

    curve.approximation_scale(m_approximation_scale.value());
    curve.angle_tolerance(agg::deg2rad(m_angle_tolerance.value()));
    curve.cusp_limit(agg::deg2rad(m_cusp_limit.value()));
    curve.init(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);

    path.concat_path(curve);

    agg::conv_stroke<agg::path_storage> stroke(path);
    stroke.width(m_width.value());
    stroke.line_join(agg::line_join_e(m_line_join.cur_item()));
    stroke.line_cap(agg::line_cap_e(m_line_cap.cur_item()));
    stroke.inner_join(agg::inner_join_e(m_inner_join.cur_item()));
    stroke.inner_miter_limit(1.01);

    ras.add_path(stroke);
    ren.color(agg::rgba(0, 0.5, 0, 0.5));
    agg::render_scanlines(ras, sl, ren);

    unsigned cmd;
    unsigned num_points1 = 0;
    path.rewind(0);
    while (!agg::is_stop(cmd = path.vertex(&x, &y))) {
        if (m_show_points.status()) {
            agg::ellipse ell(x, y, 1.5, 1.5, 8);
            ras.add_path(ell);
            ren.color(agg::rgba(0, 0, 0, 0.5));
            agg::render_scanlines(ras, sl, ren);
        }
        ++num_points1;
    }

    if (m_show_outline.status()) {
        agg::conv_stroke<agg::conv_stroke<agg::path_storage> > stroke2(stroke);
        ras.add_path(stroke2);
        ren.color(agg::rgba(0, 0, 0, 0.5));
        agg::render_scanlines(ras, sl, ren);
    }

    char buf[512];
    agg::gsv_text t;
    t.size(8.0);

    agg::conv_stroke<agg::gsv_text> pt(t);
    pt.line_cap(agg::round_cap);
    pt.line_join(agg::round_join);
    pt.width(1.5);

    snprintf(buf, sizeof(buf), "Num Points=%d\n\n"
             " Dist Error: x0.01=%.5f x0.1=%.5f x1=%.5f x10=%.5f x100=%.5f\n\n"
             "Angle Error: x0.01=%.1f x0.1=%.1f x1=%.1f x10=%.1f x100=%.1f",
             num_points1,
             max_error_01, max_error_1, max_error1, max_error_10, max_error_100,
             max_angle_error_01, max_angle_error_1, max_angle_error1, max_angle_error_10, max_angle_error_100);

    t.start_point(10.0, 85.0);
    t.text(buf);

    ras.add_path(pt);
    ren.color(agg::rgba(0, 0, 0));
    agg::render_scanlines(ras, sl, ren);

    agg::render_ctrl(ras, sl, ren_base, m_curve1);
    agg::render_ctrl(ras, sl, ren_base, m_angle_tolerance);
    agg::render_ctrl(ras, sl, ren_base, m_approximation_scale);
    agg::render_ctrl(ras, sl, ren_base, m_cusp_limit);
    agg::render_ctrl(ras, sl, ren_base, m_width);
    agg::render_ctrl(ras, sl, ren_base, m_show_points);
    agg::render_ctrl(ras, sl, ren_base, m_show_outline);
    agg::render_ctrl(ras, sl, ren_base, m_curve_type);
    agg::render_ctrl(ras, sl, ren_base, m_case_type);
    agg::render_ctrl(ras, sl, ren_base, m_inner_join);
    agg::render_ctrl(ras, sl, ren_base, m_line_join);
    agg::render_ctrl(ras, sl, ren_base, m_line_cap);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
