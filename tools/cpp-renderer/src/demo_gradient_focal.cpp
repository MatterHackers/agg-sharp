// gradient_focal.cpp headless reproduction (default state: 600x400, the focus at the window center,
// gamma 1.0). Left out: the "%3.2f ms" benchmark-timer text, which differs every frame. The gradient_lut
// comes from patches/agg_gradient_lut.h (segments reach their stop colors).
// Params (all optional): mouse_x mouse_y (the focus), then gamma.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_gradient_lut.h"
#include "agg_gamma_lut.h"
#include "agg_span_interpolator_linear.h"
#include "agg_conv_stroke.h"
#include "agg_ellipse.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

int render_gradient_focal(unsigned w, unsigned h,
                          const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::gamma_lut<agg::int8u, agg::int8u> gamma_lut_type;
    typedef agg::gradient_radial_focus gradient_func_type;
    typedef agg::gradient_reflect_adaptor<gradient_func_type> gradient_adaptor_type;
    typedef agg::gradient_lut<agg::color_interpolator<agg::srgba8>, 1024> color_func_type;
    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_allocator<color_type> span_allocator_type;
    typedef agg::span_gradient<color_type, interpolator_type, gradient_adaptor_type, color_func_type>
        span_gradient_type;

    // enum flip_y = true => the ctrl gets !flip_y = false.
    agg::slider_ctrl<color_type> m_gamma(5.0, 5.0, 340.0, 12.0, false);
    m_gamma.range(0.5, 2.5);
    m_gamma.value(1.0);
    m_gamma.label("Gamma = %.3f");
    m_gamma.no_transform();

    // on_init(): the focus starts at the window center.
    double m_mouse_x = w / 2;
    double m_mouse_y = h / 2;
    if (params.size() >= 2) {
        m_mouse_x = params[0];
        m_mouse_y = params[1];
    }
    if (params.size() >= 3) m_gamma.value(params[2]);

    gamma_lut_type m_gamma_lut;
    m_gamma_lut.gamma(m_gamma.value());
    color_func_type m_gradient_lut;
    m_gradient_lut.remove_all();
    m_gradient_lut.add_color(0.0, agg::rgba8_gamma_dir(agg::srgba8(0, 255, 0), m_gamma_lut));
    m_gradient_lut.add_color(0.2, agg::rgba8_gamma_dir(agg::srgba8(120, 0, 0), m_gamma_lut));
    m_gradient_lut.add_color(0.7, agg::rgba8_gamma_dir(agg::srgba8(120, 120, 0), m_gamma_lut));
    m_gradient_lut.add_color(1.0, agg::rgba8_gamma_dir(agg::srgba8(0, 0, 255), m_gamma_lut));
    m_gradient_lut.build_lut();

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    agg::scanline_u8 m_scanline;
    agg::rasterizer_scanline_aa<> m_rasterizer;
    span_allocator_type m_alloc;

    // on_draw(); trans_affine_resizing() is identity at the initial size.
    rb.clear(agg::rgba(1, 1, 1));

    double cx = w / 2;
    double cy = h / 2;
    double r = 100;
    double fx = m_mouse_x - cx;
    double fy = m_mouse_y - cy;

    gradient_func_type gradient_func(r, fx, fy);
    gradient_adaptor_type gradient_adaptor(gradient_func);
    agg::trans_affine gradient_mtx;
    gradient_mtx.translate(cx, cy);
    gradient_mtx.invert();

    interpolator_type span_interpolator(gradient_mtx);
    span_gradient_type span_gradient(span_interpolator, gradient_adaptor, m_gradient_lut, 0, r);

    m_rasterizer.reset();
    m_rasterizer.move_to_d(0, 0);
    m_rasterizer.line_to_d(w, 0);
    m_rasterizer.line_to_d(w, h);
    m_rasterizer.line_to_d(0, h);
    agg::render_scanlines_aa(m_rasterizer, m_scanline, rb, m_alloc, span_gradient);

    agg::ellipse e(cx, cy, r, r);
    agg::conv_stroke<agg::ellipse> estr(e);
    m_rasterizer.add_path(estr);
    agg::render_scanlines_aa_solid(m_rasterizer, m_scanline, rb, agg::rgba(1, 1, 1));

    agg::render_ctrl(m_rasterizer, m_scanline, rb, m_gamma);

    pixf.apply_gamma_inv(m_gamma_lut);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
