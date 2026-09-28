// image1.cpp headless reproduction: on_draw() at the window size the example
// opens at (the spheres image's plus 20 by 60, 340x360). Params: angle
// (degrees), scale.
//
// The example draws into BGR24; this renders into BGRA32 so the frame is the
// 32-bit one agg-sharp's reference frame is. Every pixel is blended over
// opaque white, so the colour channels match and alpha stays 255. The image
// stays 24-bit, since the span generator is the rgb one.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_ellipse.h"
#include "agg_trans_affine.h"
#include "agg_conv_transform.h"
#include "agg_pixfmt_rgb.h"
#include "agg_pixfmt_rgba.h"
#include "agg_span_image_filter_rgb.h"
#include "agg_span_interpolator_linear.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

int render_image1(unsigned w, unsigned h,
                  const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::pixfmt_bgra32_pre pixfmt_pre;
    typedef agg::pixfmt_bgr24 img_pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_base<pixfmt_pre> renderer_base_pre;

    double angle = params.size() > 0 ? params[0] : 0.0;
    double scale = params.size() > 1 ? params[1] : 1.0;

    headless::image_rgba img = headless::load_spheres_flip_y();
    if (!img.ok) {
        fprintf(stderr, "image1: failed to load spheres image\n");
        return 1;
    }
    std::vector<unsigned char> img_bytes;
    agg::rendering_buffer img_rbuf;
    headless::pack_image(img, "bgr", img_bytes, img_rbuf);

    // initial_width()/initial_height(): the window agg_main opens.
    double iw = img.width + 20, ih = img.height + 40 + 20;

    headless::canvas cv(w, h, 4);
    pixfmt pixf(cv.rbuf);
    pixfmt_pre pixf_pre(cv.rbuf);
    renderer_base rb(pixf);
    renderer_base_pre rb_pre(pixf_pre);
    rb.clear(agg::rgba(1.0, 1.0, 1.0));

    // trans_affine_resizing() is the identity at the initial size.
    agg::trans_affine src_mtx;
    src_mtx *= agg::trans_affine_translation(-iw / 2 - 10, -ih / 2 - 20 - 10);
    src_mtx *= agg::trans_affine_rotation(angle * agg::pi / 180.0);
    src_mtx *= agg::trans_affine_scaling(scale);
    src_mtx *= agg::trans_affine_translation(iw / 2, ih / 2 + 20);

    agg::trans_affine img_mtx;
    img_mtx *= agg::trans_affine_translation(-iw / 2 + 10, -ih / 2 + 20 + 10);
    img_mtx *= agg::trans_affine_rotation(angle * agg::pi / 180.0);
    img_mtx *= agg::trans_affine_scaling(scale);
    img_mtx *= agg::trans_affine_translation(iw / 2, ih / 2 + 20);
    img_mtx.invert();

    agg::span_allocator<agg::rgba8> sa;
    typedef agg::span_interpolator_linear<> interpolator_type;
    interpolator_type interpolator(img_mtx);

    img_pixfmt img_pixf(img_rbuf);
    typedef agg::span_image_filter_rgb_bilinear_clip<img_pixfmt, interpolator_type> span_gen_type;
    span_gen_type sg(img_pixf, agg::rgba_pre(0, 0.4, 0, 0.5), interpolator);

    agg::rasterizer_scanline_aa<> ras;
    ras.clip_box(0, 0, w, h);
    agg::scanline_u8 sl;
    double r = iw;
    if (ih - 60 < r) r = ih - 60;
    agg::ellipse ell(iw / 2.0 + 10, ih / 2.0 + 20 + 10, r / 2.0 + 16.0, r / 2.0 + 16.0, 200);
    agg::conv_transform<agg::ellipse> tr(ell, src_mtx);
    ras.add_path(tr);
    agg::render_scanlines_aa(ras, sl, rb_pre, sa, sg);

    // Controls, as the example builds them (flip_y = true, so !flip_y).
    agg::slider_ctrl<agg::rgba8> m_angle(5, 5, 300, 12, false);
    agg::slider_ctrl<agg::rgba8> m_scale(5, 5 + 15, 300, 12 + 15, false);
    m_angle.label("Angle=%3.2f");
    m_scale.label("Scale=%3.2f");
    m_angle.range(-180.0, 180.0);
    m_angle.value(angle);
    m_scale.range(0.1, 5.0);
    m_scale.value(scale);
    agg::render_ctrl(ras, sl, rb, m_angle);
    agg::render_ctrl(ras, sl, rb, m_scale);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
