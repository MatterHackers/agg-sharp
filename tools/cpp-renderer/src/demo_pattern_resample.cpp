// pattern_resample.cpp headless reproduction: on_draw() at the window size the example opens at (600x600),
// with art/agg.ppm loaded as the flip_y = true example sees it and taken into linear light (apply_gamma_dir), into
// the example's bgr24 window. Left out: the "%3.2f ms" timer text, which differs every frame.
// Params (all optional): the quad's four corners x0 y0 .. x3 y3, then trans_type, gamma, blur.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_trans_affine.h"
#include "agg_trans_perspective.h"
#include "agg_span_allocator.h"
#include "agg_span_interpolator_linear.h"
#include "agg_span_interpolator_trans.h"
#include "agg_span_interpolator_persp.h"
#include "agg_span_subdiv_adaptor.h"
#include "agg_pixfmt_rgb.h"
#include "agg_image_accessors.h"
#include "agg_gamma_lut.h"
#include "agg_span_image_filter_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_slider_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

int render_pattern_resample(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::pixfmt_bgr24_pre pixfmt_pre;
    typedef pixfmt::color_type color_type;
    typedef color_type::value_type value_type;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_base<pixfmt_pre> renderer_base_pre;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    // the_application(): the controls.
    agg::rbox_ctrl<agg::rgba> m_trans_type(400, 5.0, 430 + 170.0, 100.0, false);
    m_trans_type.text_size(7);
    m_trans_type.add_item("Affine No Resample");
    m_trans_type.add_item("Affine Resample");
    m_trans_type.add_item("Perspective No Resample LERP");
    m_trans_type.add_item("Perspective No Resample Exact");
    m_trans_type.add_item("Perspective Resample LERP");
    m_trans_type.add_item("Perspective Resample Exact");
    m_trans_type.cur_item(4);

    agg::slider_ctrl<agg::rgba> m_gamma(5.0, 5.0 + 15 * 0, 400 - 5, 10.0 + 15 * 0, false);
    m_gamma.range(0.5, 3.0);
    m_gamma.value(2.0);
    m_gamma.label("Gamma=%.3f");

    agg::slider_ctrl<agg::rgba> m_blur(5.0, 5.0 + 15 * 1, 400 - 5, 10.0 + 15 * 1, false);
    m_blur.range(0.5, 2.0);
    m_blur.value(1.0);
    m_blur.label("Blur=%.3f");

    // on_init: a 400 pixel square centered in the window.
    double g_x1 = -150, g_y1 = -150, g_x2 = 150, g_y2 = 150;
    double trans_x1 = -200, trans_y1 = -200, trans_x2 = 200, trans_y2 = 200;
    double dx = w / 2.0 - (trans_x2 + trans_x1) / 2.0;
    double dy = h / 2.0 - (trans_y2 + trans_y1) / 2.0;
    agg::interactive_polygon m_quad(4, 5.0);
    m_quad.xn(0) = floor(trans_x1 + dx); m_quad.yn(0) = floor(trans_y1 + dy);
    m_quad.xn(1) = floor(trans_x2 + dx); m_quad.yn(1) = floor(trans_y1 + dy);
    m_quad.xn(2) = floor(trans_x2 + dx); m_quad.yn(2) = floor(trans_y2 + dy);
    m_quad.xn(3) = floor(trans_x1 + dx); m_quad.yn(3) = floor(trans_y2 + dy);
    for (unsigned i = 0; i < 8 && i < params.size(); ++i) {
        if (i & 1) m_quad.yn(i / 2) = params[i]; else m_quad.xn(i / 2) = params[i];
    }
    if (params.size() > 8) m_trans_type.cur_item((int)params[8]);
    if (params.size() > 9) m_gamma.value(params[9]);
    if (params.size() > 10) m_blur.value(params[10]);

    // on_init / on_draw: the picture, loaded and taken through the gamma's direct table at the slider's gamma.
    agg::gamma_lut<value_type, value_type, color_type::base_shift, color_type::base_shift> m_gamma_lut(2.0);
    m_gamma_lut.gamma(m_gamma.value());
    headless::image_rgba img = headless::load_agg_flip_y();
    if (!img.ok) {
        fprintf(stderr, "pattern_resample: failed to load agg.ppm\n");
        return 1;
    }
    std::vector<unsigned char> img_bytes;
    agg::rendering_buffer img_rbuf;
    headless::pack_image(img, "bgr", img_bytes, img_rbuf);
    pixfmt img_pixf(img_rbuf);
    img_pixf.apply_gamma_dir(m_gamma_lut);

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    pixfmt_pre pixf_pre(cv.rbuf);
    renderer_base rb(pixf);
    renderer_base_pre rb_pre(pixf_pre);
    renderer_solid r(rb);
    agg::rasterizer_scanline_aa<> g_rasterizer;
    agg::scanline_u8 g_scanline;

    rb.clear(agg::rgba(1, 1, 1));

    if (m_trans_type.cur_item() < 2) {
        // The affine parallelogram's implicit 4th point.
        m_quad.xn(3) = m_quad.xn(0) + (m_quad.xn(2) - m_quad.xn(1));
        m_quad.yn(3) = m_quad.yn(0) + (m_quad.yn(2) - m_quad.yn(1));
    }

    g_rasterizer.add_path(m_quad);
    r.color(agg::rgba(0, 0.3, 0.5, 0.1));
    agg::render_scanlines(g_rasterizer, g_scanline, r);

    g_rasterizer.clip_box(0, 0, w, h);
    g_rasterizer.reset();
    g_rasterizer.move_to_d(m_quad.xn(0), m_quad.yn(0));
    g_rasterizer.line_to_d(m_quad.xn(1), m_quad.yn(1));
    g_rasterizer.line_to_d(m_quad.xn(2), m_quad.yn(2));
    g_rasterizer.line_to_d(m_quad.xn(3), m_quad.yn(3));

    agg::span_allocator<color_type> sa;
    agg::image_filter_hanning filter_kernel;
    agg::image_filter_lut filter(filter_kernel, true);

    typedef agg::wrap_mode_reflect_auto_pow2 wrap_type;
    typedef agg::image_accessor_wrap<pixfmt, wrap_type, wrap_type> img_source_type;
    img_source_type img_src(img_pixf);

    switch (m_trans_type.cur_item()) {
        case 0: {
            agg::trans_affine tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            typedef agg::span_interpolator_linear<agg::trans_affine> interpolator_type;
            interpolator_type interpolator(tr);
            typedef agg::span_image_filter_rgb_2x2<img_source_type, interpolator_type> span_gen_type;
            span_gen_type sg(img_src, interpolator, filter);
            agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            break;
        }
        case 1: {
            agg::trans_affine tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            typedef agg::span_interpolator_linear<agg::trans_affine> interpolator_type;
            typedef agg::span_image_resample_rgb_affine<img_source_type> span_gen_type;
            interpolator_type interpolator(tr);
            span_gen_type sg(img_src, interpolator, filter);
            sg.blur(m_blur.value());
            agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            break;
        }
        case 2: {
            agg::trans_perspective tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            if (tr.is_valid()) {
                typedef agg::span_interpolator_linear_subdiv<agg::trans_perspective> interpolator_type;
                interpolator_type interpolator(tr);
                typedef agg::span_image_filter_rgb_2x2<img_source_type, interpolator_type> span_gen_type;
                span_gen_type sg(img_src, interpolator, filter);
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
        case 3: {
            agg::trans_perspective tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            if (tr.is_valid()) {
                typedef agg::span_interpolator_trans<agg::trans_perspective> interpolator_type;
                interpolator_type interpolator(tr);
                typedef agg::span_image_filter_rgb_2x2<img_source_type, interpolator_type> span_gen_type;
                span_gen_type sg(img_src, interpolator, filter);
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
        case 4: {
            typedef agg::span_interpolator_persp_lerp<> interpolator_type;
            typedef agg::span_subdiv_adaptor<interpolator_type> subdiv_adaptor_type;
            interpolator_type interpolator(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            subdiv_adaptor_type subdiv_adaptor(interpolator);
            if (interpolator.is_valid()) {
                typedef agg::span_image_resample_rgb<img_source_type, subdiv_adaptor_type> span_gen_type;
                span_gen_type sg(img_src, subdiv_adaptor, filter);
                sg.blur(m_blur.value());
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
        case 5: {
            typedef agg::span_interpolator_persp_exact<> interpolator_type;
            typedef agg::span_subdiv_adaptor<interpolator_type> subdiv_adaptor_type;
            interpolator_type interpolator(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            subdiv_adaptor_type subdiv_adaptor(interpolator);
            if (interpolator.is_valid()) {
                typedef agg::span_image_resample_rgb<img_source_type, subdiv_adaptor_type> span_gen_type;
                span_gen_type sg(img_src, subdiv_adaptor, filter);
                sg.blur(m_blur.value());
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
    }
    pixf.apply_gamma_inv(m_gamma_lut);

    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_trans_type);
    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_gamma);
    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_blur);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
