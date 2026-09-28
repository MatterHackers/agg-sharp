// alpha_gradient.cpp headless reproduction (default state: 400x320). The 100 background ellipses come from
// the MSVC rand() generator seeded 1234 (the example's srand(1234)), each value taken in argument order and
// RAND_MAX 32767, so the port can draw the same ones.
// Params (all optional): the parallelogram's three corners x0 y0 x1 y1 x2 y2, then the alpha spline's point 2
// y, then its active point.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_gradient.h"
#include "agg_span_gradient_alpha.h"
#include "agg_span_interpolator_linear.h"
#include "agg_span_allocator.h"
#include "agg_span_converter.h"
#include "agg_ellipse.h"
#include "agg_pixfmt_rgb.h"
#include "agg_vcgen_stroke.h"
#include "ctrl/agg_spline_ctrl.h"

#include "common.h"

namespace {

typedef agg::rgba8 color_type;

unsigned g_alpha_holdrand = 1;

int alpha_rand()
{
    g_alpha_holdrand = g_alpha_holdrand * 214013u + 2531011u;
    return (g_alpha_holdrand >> 16) & 0x7fff;
}

const double msvc_rand_max = 32767;

template<class ColorArrayT>
void fill_color_array(ColorArrayT& array, color_type begin, color_type middle, color_type end)
{
    unsigned i;
    for (i = 0; i < 128; ++i) array[i] = begin.gradient(middle, i / 128.0);
    for (; i < 256; ++i) array[i] = middle.gradient(end, (i - 128) / 128.0);
}

} // namespace

int render_alpha_gradient(unsigned w, unsigned h,
                          const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> base_ren_type;

    double m_x[3] = { 257, 369, 143 };
    double m_y[3] = { 60, 170, 310 };
    if (params.size() >= 6) {
        for (int i = 0; i < 3; i++) {
            m_x[i] = params[i * 2];
            m_y[i] = params[i * 2 + 1];
        }
    }

    // enum flip_y = true => the ctrl gets !flip_y = false.
    agg::spline_ctrl<color_type> m_alpha(2, 2, 200, 30, 6, false);
    m_alpha.point(0, 0.0, 0.0);
    m_alpha.point(1, 1.0 / 5.0, 1.0 - 4.0 / 5.0);
    m_alpha.point(2, 2.0 / 5.0, 1.0 - 3.0 / 5.0);
    m_alpha.point(3, 3.0 / 5.0, 1.0 - 2.0 / 5.0);
    m_alpha.point(4, 4.0 / 5.0, 1.0 - 1.0 / 5.0);
    m_alpha.point(5, 1.0, 1.0);
    if (params.size() >= 7) m_alpha.point(2, 2.0 / 5.0, params[6]);
    if (params.size() >= 8) m_alpha.active_point(int(params[7]));
    m_alpha.update_spline();

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    base_ren_type ren_base(pf);

    // on_draw()
    ren_base.clear(agg::rgba(1, 1, 1));
    agg::scanline_u8 sl;
    agg::rasterizer_scanline_aa<> ras;
    agg::ellipse ell;

    g_alpha_holdrand = 1234;
    unsigned i;
    for (i = 0; i < 100; i++) {
        double x = alpha_rand() % w;
        double y = alpha_rand() % h;
        double rx = alpha_rand() % 60 + 5;
        double ry = alpha_rand() % 60 + 5;
        ell.init(x, y, rx, ry, 50);
        ras.add_path(ell);
        double r = alpha_rand() / msvc_rand_max;
        double g = alpha_rand() / msvc_rand_max;
        double b = alpha_rand() / msvc_rand_max;
        double a = alpha_rand() / msvc_rand_max / 2.0;
        agg::render_scanlines_aa_solid(ras, sl, ren_base, agg::rgba(r, g, b, a));
    }

    double parallelogram[6] = { m_x[0], m_y[0], m_x[1], m_y[1], m_x[2], m_y[2] };

    typedef agg::gradient_circle gradient_func_type;
    typedef agg::gradient_xy gradient_alpha_func_type;
    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_allocator<color_type> span_allocator_type;
    typedef agg::pod_auto_array<color_type, 256> gradient_colors_type;
    typedef agg::span_gradient<color_type, interpolator_type, gradient_func_type, gradient_colors_type>
        span_gradient_type;
    typedef agg::pod_auto_array<color_type::value_type, 256> gradient_alpha_type;
    typedef agg::span_gradient_alpha<color_type, interpolator_type, gradient_alpha_func_type, gradient_alpha_type>
        span_gradient_alpha_type;
    typedef agg::span_converter<span_gradient_type, span_gradient_alpha_type> span_conv_type;

    gradient_func_type gradient_func;
    gradient_alpha_func_type alpha_func;
    agg::trans_affine gradient_mtx;
    agg::trans_affine alpha_mtx;
    interpolator_type span_interpolator(gradient_mtx);
    interpolator_type span_interpolator_alpha(alpha_mtx);
    span_allocator_type span_allocator;
    gradient_colors_type color_array;
    span_gradient_type span_gradient(span_interpolator, gradient_func, color_array, 0, 150);
    gradient_alpha_type alpha_array;
    span_gradient_alpha_type span_gradient_alpha(span_interpolator_alpha, alpha_func, alpha_array, 0, 100);
    span_conv_type span_conv(span_gradient, span_gradient_alpha);

    gradient_mtx *= agg::trans_affine_scaling(0.75, 1.2);
    gradient_mtx *= agg::trans_affine_rotation(-agg::pi / 3.0);
    gradient_mtx *= agg::trans_affine_translation(w / 2, h / 2);
    gradient_mtx.invert();
    alpha_mtx.parl_to_rect(parallelogram, -100, -100, 100, 100);

    fill_color_array(color_array,
                     agg::rgba(0, 0.19, 0.19),
                     agg::rgba(0.7, 0.7, 0.19),
                     agg::rgba(0.31, 0, 0));

    for (i = 0; i < 256; i++) {
        alpha_array[i] = color_type::from_double(m_alpha.value(i / 255.0));
    }

    ell.init(w / 2, h / 2, 150, 150, 100);
    ras.add_path(ell);
    agg::render_scanlines_aa(ras, sl, ren_base, span_allocator, span_conv);

    agg::rgba color_pnt(0, 0.4, 0.4, 0.31);
    for (i = 0; i < 3; i++) {
        ell.init(m_x[i], m_y[i], 5, 5, 20);
        ras.add_path(ell);
        agg::render_scanlines_aa_solid(ras, sl, ren_base, color_pnt);
    }

    agg::vcgen_stroke stroke;
    stroke.add_vertex(m_x[0], m_y[0], agg::path_cmd_move_to);
    stroke.add_vertex(m_x[1], m_y[1], agg::path_cmd_line_to);
    stroke.add_vertex(m_x[2], m_y[2], agg::path_cmd_line_to);
    stroke.add_vertex(m_x[0] + m_x[2] - m_x[1], m_y[0] + m_y[2] - m_y[1], agg::path_cmd_line_to);
    stroke.add_vertex(0, 0, agg::path_cmd_end_poly | agg::path_flags_close);
    ras.add_path(stroke);
    agg::render_scanlines_aa_solid(ras, sl, ren_base, agg::rgba(0, 0, 0));

    agg::render_ctrl(ras, sl, ren_base, m_alpha);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
