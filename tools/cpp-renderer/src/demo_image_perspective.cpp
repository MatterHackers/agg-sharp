// image_perspective.cpp headless reproduction: the example's on_draw() at its on_init() state (quad inset
// by 100, Perspective), with the spheres image loaded as the flip_y = true example sees it. The quad tool is
// drawn first and the image over it, through the premultiplied renderer, as on_draw() does. Left out: the
// "%3.2f ms" benchmark-timer text, which differs every frame.
// Params (all optional): the quad's four corners x0 y0 .. x3 y3, then trans_type.
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
#include "agg_span_interpolator_trans.h"
#include "agg_pixfmt_rgba.h"
#include "agg_image_accessors.h"
#include "agg_span_image_filter_rgba.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

int render_image_perspective(unsigned w, unsigned h,
                             const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::pixfmt_bgra32_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_base<pixfmt_pre> renderer_base_pre;

    headless::image_rgba img = headless::load_spheres_flip_y();
    if (!img.ok) {
        fprintf(stderr, "image_perspective: failed to load spheres image\n");
        return 1;
    }
    std::vector<unsigned char> img_bytes;
    agg::rendering_buffer img_rbuf;
    headless::pack_image(img, "bgra", img_bytes, img_rbuf);

    // on_init
    double g_x1 = 0, g_y1 = 0, g_x2 = img.width, g_y2 = img.height;
    agg::interactive_polygon m_quad(4, 5.0);
    m_quad.xn(0) = 100;     m_quad.yn(0) = 100;
    m_quad.xn(1) = w - 100; m_quad.yn(1) = 100;
    m_quad.xn(2) = w - 100; m_quad.yn(2) = h - 100;
    m_quad.xn(3) = 100;     m_quad.yn(3) = h - 100;
    for (unsigned i = 0; i < 8 && i < params.size(); ++i) {
        if (i & 1) m_quad.yn(i / 2) = params[i]; else m_quad.xn(i / 2) = params[i];
    }
    int trans_type = params.size() > 8 ? (int)params[8] : 2;

    agg::rbox_ctrl<agg::rgba8> m_trans_type(420, 5.0, 420 + 170.0, 70.0, false);
    m_trans_type.add_item("Affine Parallelogram");
    m_trans_type.add_item("Bilinear");
    m_trans_type.add_item("Perspective");
    m_trans_type.cur_item(trans_type);

    headless::canvas cv(w, h, 4);
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

    // The quad tool, drawn before the image (the image covers its inside).
    g_rasterizer.add_path(m_quad);
    agg::render_scanlines_aa_solid(g_rasterizer, g_scanline, rb, agg::rgba(0, 0.3, 0.5, 0.6));

    g_rasterizer.clip_box(0, 0, w, h);
    g_rasterizer.reset();
    g_rasterizer.move_to_d(m_quad.xn(0), m_quad.yn(0));
    g_rasterizer.line_to_d(m_quad.xn(1), m_quad.yn(1));
    g_rasterizer.line_to_d(m_quad.xn(2), m_quad.yn(2));
    g_rasterizer.line_to_d(m_quad.xn(3), m_quad.yn(3));

    agg::span_allocator<agg::rgba8> sa;
    agg::image_filter_bilinear filter_kernel;
    agg::image_filter_lut filter(filter_kernel, false);

    pixfmt pixf_img(img_rbuf);
    typedef agg::image_accessor_clone<pixfmt> img_accessor_type;
    img_accessor_type ia(pixf_img);

    switch (m_trans_type.cur_item()) {
        case 0: {
            agg::trans_affine tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            typedef agg::span_interpolator_linear<agg::trans_affine> interpolator_type;
            interpolator_type interpolator(tr);
            typedef agg::span_image_filter_rgba_nn<img_accessor_type, interpolator_type> span_gen_type;
            span_gen_type sg(ia, interpolator);
            agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            break;
        }
        case 1: {
            agg::trans_bilinear tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            if (tr.is_valid()) {
                typedef agg::span_interpolator_linear<agg::trans_bilinear> interpolator_type;
                interpolator_type interpolator(tr);
                typedef agg::span_image_filter_rgba_2x2<img_accessor_type, interpolator_type> span_gen_type;
                span_gen_type sg(ia, interpolator, filter);
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
        case 2: {
            agg::trans_perspective tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
            if (tr.is_valid()) {
                typedef agg::span_interpolator_trans<agg::trans_perspective> interpolator_type;
                interpolator_type interpolator(tr);
                typedef agg::span_image_filter_rgba_2x2<img_accessor_type, interpolator_type> span_gen_type;
                span_gen_type sg(ia, interpolator, filter);
                agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
            }
            break;
        }
    }

    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_trans_type);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
