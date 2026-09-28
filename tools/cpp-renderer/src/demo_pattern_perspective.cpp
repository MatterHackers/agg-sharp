// pattern_perspective.cpp headless reproduction: on_draw() at the window size the example opens at (600x600),
// with art/agg.ppm loaded as the flip_y = true example sees it, into the example's bgr24 window. Left out: the
// click-outside-the-quad benchmark, which only shows a message box.
// Params (all optional): the quad's four corners x0 y0 .. x3 y3, then trans_type.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_trans_affine.h"
#include "agg_trans_bilinear.h"
#include "agg_trans_perspective.h"
#include "agg_span_allocator.h"
#include "agg_span_interpolator_linear.h"
#include "agg_pixfmt_rgb.h"
#include "agg_image_accessors.h"
#include "agg_span_image_filter_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

int render_pattern_perspective(unsigned w, unsigned h,
                               const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::pixfmt_bgr24_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_base<pixfmt_pre> renderer_base_pre;

    headless::image_rgba img = headless::load_agg_flip_y();
    if (!img.ok) {
        fprintf(stderr, "pattern_perspective: failed to load agg.ppm\n");
        return 1;
    }
    std::vector<unsigned char> img_bytes;
    agg::rendering_buffer img_rbuf;
    headless::pack_image(img, "bgr", img_bytes, img_rbuf);

    // on_init
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
    int trans_type = params.size() > 8 ? (int)params[8] : 2;

    agg::rbox_ctrl<agg::rgba> m_trans_type(460, 5.0, 420 + 170.0, 60.0, false);
    m_trans_type.text_size(8);
    m_trans_type.text_thickness(1);
    m_trans_type.add_item("Affine");
    m_trans_type.add_item("Bilinear");
    m_trans_type.add_item("Perspective");
    m_trans_type.cur_item(trans_type);

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    pixfmt_pre pixf_pre(cv.rbuf);
    renderer_base rb(pixf);
    renderer_base_pre rb_pre(pixf_pre);
    agg::rasterizer_scanline_aa<> g_rasterizer;
    agg::scanline_u8 g_scanline;

    rb.clear(agg::rgba(1, 1, 1));

    if (m_trans_type.cur_item() == 0) {
        // The affine parallelogram's implicit 4th point.
        m_quad.xn(3) = m_quad.xn(0) + (m_quad.xn(2) - m_quad.xn(1));
        m_quad.yn(3) = m_quad.yn(0) + (m_quad.yn(2) - m_quad.yn(1));
    }

    g_rasterizer.add_path(m_quad);
    agg::render_scanlines_aa_solid(g_rasterizer, g_scanline, rb, agg::rgba(0, 0.3, 0.5, 0.6));
    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_trans_type);

    g_rasterizer.clip_box(0, 0, w, h);
    g_rasterizer.reset();
    g_rasterizer.move_to_d(m_quad.xn(0), m_quad.yn(0));
    g_rasterizer.line_to_d(m_quad.xn(1), m_quad.yn(1));
    g_rasterizer.line_to_d(m_quad.xn(2), m_quad.yn(2));
    g_rasterizer.line_to_d(m_quad.xn(3), m_quad.yn(3));

    agg::span_allocator<agg::rgba8> sa;
    agg::image_filter<agg::image_filter_hanning> filter;

    typedef agg::wrap_mode_reflect_auto_pow2 remainder_type;
    typedef agg::image_accessor_wrap<pixfmt, remainder_type, remainder_type> img_source_type;
    pixfmt img_pixf(img_rbuf);
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
            agg::trans_bilinear tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            if (tr.is_valid()) {
                typedef agg::span_interpolator_linear<agg::trans_bilinear> interpolator_type;
                interpolator_type interpolator(tr);
                typedef agg::span_image_filter_rgb_2x2<img_source_type, interpolator_type> span_gen_type;
                span_gen_type sg(img_src, interpolator, filter);
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
        case 2: {
            agg::trans_perspective tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            if (tr.is_valid()) {
                typedef agg::span_interpolator_linear_subdiv<agg::trans_perspective, 8> interpolator_type;
                interpolator_type interpolator(tr);
                typedef agg::span_image_filter_rgb_2x2<img_source_type, interpolator_type> span_gen_type;
                span_gen_type sg(img_src, interpolator, filter);
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
    }

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
