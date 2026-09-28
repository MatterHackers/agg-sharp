// gradients.cpp headless reproduction (default state: 512x400, the example's built-in spline points and
// gamma values - it reads them from settings.dat, which a fresh run does not have).
// Params (all optional): gradient (rbox item 0-5), center_x center_y scale angle, then the profile's
// gamma values kx1 ky1 kx2 ky2, then y of the alpha spline's inner points 1-4 (one value for all four).
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_transform.h"
#include "agg_ellipse.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_spline_ctrl.h"
#include "ctrl/agg_gamma_ctrl.h"

#include "common.h"

namespace {

typedef agg::rgba8 color_type;

class gradient_polymorphic_wrapper_base {
public:
    virtual int calculate(int x, int y, int) const = 0;
};

template<class GradientF>
class gradient_polymorphic_wrapper : public gradient_polymorphic_wrapper_base {
public:
    gradient_polymorphic_wrapper() : m_adaptor(m_gradient) {}
    virtual int calculate(int x, int y, int d) const { return m_adaptor.calculate(x, y, d); }
    GradientF m_gradient;
    agg::gradient_reflect_adaptor<GradientF> m_adaptor;
};

struct color_function_profile {
    color_function_profile(const color_type* colors, const agg::int8u* profile)
        : m_colors(colors), m_profile(profile) {}
    static unsigned size() { return 256; }
    const color_type& operator[](unsigned v) const { return m_colors[m_profile[v]]; }
    const color_type* m_colors;
    const agg::int8u* m_profile;
};

void default_points(agg::spline_ctrl<color_type>& s, bool alpha) {
    s.point(0, 0.0, 1.0);
    for (int i = 1; i < 5; i++) s.point(i, i / 5.0, alpha ? 1.0 : 1.0 - i / 5.0);
    s.point(5, 1.0, alpha ? 1.0 : 0.0);
    s.update_spline();
}

} // namespace

int render_gradients(unsigned w, unsigned h,
                     const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;

    // enum flip_y = true => every ctrl gets !flip_y = false.
    agg::gamma_ctrl<color_type> m_profile(10.0, 10.0, 200.0, 170.0 - 5.0, false);
    agg::spline_ctrl<color_type> m_spline_r(210, 10, 210 + 250, 5 + 40, 6, false);
    agg::spline_ctrl<color_type> m_spline_g(210, 10 + 40, 210 + 250, 5 + 80, 6, false);
    agg::spline_ctrl<color_type> m_spline_b(210, 10 + 80, 210 + 250, 5 + 120, 6, false);
    agg::spline_ctrl<color_type> m_spline_a(210, 10 + 120, 210 + 250, 5 + 160, 6, false);
    agg::rbox_ctrl<color_type> m_rbox(10.0, 180.0, 200.0, 300.0, false);

    m_profile.border_width(2.0, 2.0);
    m_spline_r.background_color(agg::rgba(1.0, 0.8, 0.8));
    m_spline_g.background_color(agg::rgba(0.8, 1.0, 0.8));
    m_spline_b.background_color(agg::rgba(0.8, 0.8, 1.0));
    m_spline_a.background_color(agg::rgba(1.0, 1.0, 1.0));
    m_spline_r.border_width(1.0, 2.0);
    m_spline_g.border_width(1.0, 2.0);
    m_spline_b.border_width(1.0, 2.0);
    m_spline_a.border_width(1.0, 2.0);
    m_rbox.border_width(2.0, 2.0);
    default_points(m_spline_r, false);
    default_points(m_spline_g, false);
    default_points(m_spline_b, false);
    default_points(m_spline_a, true);
    m_rbox.add_item("Circular");
    m_rbox.add_item("Diamond");
    m_rbox.add_item("Linear");
    m_rbox.add_item("XY");
    m_rbox.add_item("sqrt(XY)");
    m_rbox.add_item("Conic");
    m_rbox.cur_item(0);

    double m_center_x = 350;
    double m_center_y = 280;
    double m_scale = 1.0;
    double m_angle = 0.0;
    double m_scale_x = 1.0;
    double m_scale_y = 1.0;
    if (params.size() >= 1) m_rbox.cur_item(int(params[0]));
    if (params.size() >= 5) {
        m_center_x = params[1];
        m_center_y = params[2];
        m_scale = params[3];
        m_angle = params[4];
    }
    if (params.size() >= 9) m_profile.values(params[5], params[6], params[7], params[8]);
    if (params.size() >= 10) {
        for (int i = 1; i < 5; i++) m_spline_a.point(i, i / 5.0, params[9]);
        m_spline_a.update_spline();
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);

    // on_draw(); trans_affine_resizing() is identity at the initial size.
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;
    rb.clear(agg::rgba(0, 0, 0));

    m_profile.text_size(8.0);
    agg::render_ctrl(ras, sl, rb, m_profile);
    agg::render_ctrl(ras, sl, rb, m_spline_r);
    agg::render_ctrl(ras, sl, rb, m_spline_g);
    agg::render_ctrl(ras, sl, rb, m_spline_b);
    agg::render_ctrl(ras, sl, rb, m_spline_a);
    agg::render_ctrl(ras, sl, rb, m_rbox);

    double ini_scale = 1.0;
    agg::trans_affine mtx1;
    mtx1 *= agg::trans_affine_scaling(ini_scale, ini_scale);
    mtx1 *= agg::trans_affine_rotation(agg::deg2rad(0.0));
    mtx1 *= agg::trans_affine_translation(350, 280);

    agg::ellipse e1;
    e1.init(0.0, 0.0, 110.0, 110.0, 64);

    agg::trans_affine mtx_g1;
    mtx_g1 *= agg::trans_affine_scaling(ini_scale, ini_scale);
    mtx_g1 *= agg::trans_affine_scaling(m_scale, m_scale);
    mtx_g1 *= agg::trans_affine_scaling(m_scale_x, m_scale_y);
    mtx_g1 *= agg::trans_affine_rotation(m_angle);
    mtx_g1 *= agg::trans_affine_translation(m_center_x, m_center_y);
    mtx_g1.invert();

    color_type color_profile[256];
    for (int i = 0; i < 256; i++) {
        color_profile[i] = color_type(agg::rgba(m_spline_r.spline()[i],
                                                m_spline_g.spline()[i],
                                                m_spline_b.spline()[i],
                                                m_spline_a.spline()[i]));
    }

    agg::conv_transform<agg::ellipse, agg::trans_affine> t1(e1, mtx1);

    gradient_polymorphic_wrapper<agg::gradient_radial> gr_circle;
    gradient_polymorphic_wrapper<agg::gradient_diamond> gr_diamond;
    gradient_polymorphic_wrapper<agg::gradient_x> gr_x;
    gradient_polymorphic_wrapper<agg::gradient_xy> gr_xy;
    gradient_polymorphic_wrapper<agg::gradient_sqrt_xy> gr_sqrt_xy;
    gradient_polymorphic_wrapper<agg::gradient_conic> gr_conic;
    gradient_polymorphic_wrapper_base* gr_ptr = &gr_circle;
    switch (m_rbox.cur_item()) {
        case 1: gr_ptr = &gr_diamond; break;
        case 2: gr_ptr = &gr_x; break;
        case 3: gr_ptr = &gr_xy; break;
        case 4: gr_ptr = &gr_sqrt_xy; break;
        case 5: gr_ptr = &gr_conic; break;
    }

    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_gradient<color_type, interpolator_type,
                               gradient_polymorphic_wrapper_base,
                               color_function_profile> gradient_span_gen;
    agg::span_allocator<color_type> span_alloc;
    color_function_profile colors(color_profile, m_profile.gamma());
    interpolator_type inter(mtx_g1);
    gradient_span_gen span_gen(inter, *gr_ptr, colors, 0, 150);

    ras.add_path(t1);
    agg::render_scanlines_aa(ras, sl, rb, span_alloc, span_gen);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
