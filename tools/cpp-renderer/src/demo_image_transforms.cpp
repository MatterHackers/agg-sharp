// image_transforms.cpp headless reproduction: on_draw() at the window size the
// example opens at (the spheres image's, 320x300). Params: polygon angle,
// polygon scale, image angle, image scale, rotate polygon, rotate image,
// example, image center x/y, polygon center x/y.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_path_storage.h"
#include "agg_trans_affine.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_ellipse.h"
#include "agg_pixfmt_rgba.h"
#include "agg_span_image_filter_rgba.h"
#include "agg_span_interpolator_linear.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

int render_image_transforms(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double W = w, H = h;
    double poly_angle = params.size() > 0 ? params[0] : 0.0;
    double poly_scale = params.size() > 1 ? params[1] : 1.0;
    double img_angle = params.size() > 2 ? params[2] : 0.0;
    double img_scale = params.size() > 3 ? params[3] : 1.0;
    bool rotate_polygon = params.size() > 4 ? params[4] > 0.5 : false;
    bool rotate_image = params.size() > 5 ? params[5] > 0.5 : false;
    int example_idx = params.size() > 6 ? (int)params[6] : 0;
    double img_cx = params.size() > 7 ? params[7] : W / 2.0;
    double img_cy = params.size() > 8 ? params[8] : H / 2.0;
    double poly_cx = params.size() > 9 ? params[9] : W / 2.0;
    double poly_cy = params.size() > 10 ? params[10] : H / 2.0;

    headless::image_rgba img = headless::load_spheres_flip_y();
    if (!img.ok) {
        fprintf(stderr, "image_transforms: failed to load spheres image\n");
        return 1;
    }
    std::vector<unsigned char> img_bytes;
    agg::rendering_buffer img_rbuf;
    headless::pack_image(img, "bgra", img_bytes, img_rbuf);

    headless::canvas cv(w, h, 4);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid rs(rb);
    rb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;
    agg::span_allocator<agg::rgba8> sa;

    // 14-point star centered at the polygon center.
    double r = W; if (H < r) r = H;
    double r1 = r / 3.0 - 8.0;
    double r2 = r1 / 1.45;
    agg::path_storage star;
    for (int i = 0; i < 14; ++i) {
        double a = agg::pi * 2.0 * i / 14.0 - agg::pi / 2.0;
        double dx = cos(a), dy = sin(a);
        if (i & 1) star.line_to(poly_cx + dx * r1, poly_cy + dy * r1);
        else if (i) star.line_to(poly_cx + dx * r2, poly_cy + dy * r2);
        else star.move_to(poly_cx + dx * r2, poly_cy + dy * r2);
    }

    double pa = poly_angle * agg::pi / 180.0;
    agg::trans_affine poly_mtx;
    poly_mtx *= agg::trans_affine_translation(-poly_cx, -poly_cy);
    poly_mtx *= agg::trans_affine_rotation(pa);
    poly_mtx *= agg::trans_affine_scaling(poly_scale, poly_scale);
    poly_mtx *= agg::trans_affine_translation(poly_cx, poly_cy);

    // on_init: m_image_center is the window's center.
    double image_center_x = W / 2.0;
    double image_center_y = H / 2.0;
    double ia = img_angle * agg::pi / 180.0;
    agg::trans_affine image_mtx;
    switch (example_idx) {
    case 1:
        image_mtx *= agg::trans_affine_translation(-image_center_x, -image_center_y);
        image_mtx *= agg::trans_affine_rotation(pa);
        image_mtx *= agg::trans_affine_scaling(poly_scale, poly_scale);
        image_mtx *= agg::trans_affine_translation(poly_cx, poly_cy);
        image_mtx.invert();
        break;
    case 2:
        image_mtx *= agg::trans_affine_translation(-image_center_x, -image_center_y);
        image_mtx *= agg::trans_affine_rotation(ia);
        image_mtx *= agg::trans_affine_scaling(img_scale, img_scale);
        image_mtx *= agg::trans_affine_translation(img_cx, img_cy);
        image_mtx.invert();
        break;
    case 3:
        image_mtx *= agg::trans_affine_translation(-image_center_x, -image_center_y);
        image_mtx *= agg::trans_affine_rotation(ia);
        image_mtx *= agg::trans_affine_scaling(img_scale, img_scale);
        image_mtx *= agg::trans_affine_translation(poly_cx, poly_cy);
        image_mtx.invert();
        break;
    case 4:
        image_mtx *= agg::trans_affine_translation(-img_cx, -img_cy);
        image_mtx *= agg::trans_affine_rotation(pa);
        image_mtx *= agg::trans_affine_scaling(poly_scale, poly_scale);
        image_mtx *= agg::trans_affine_translation(poly_cx, poly_cy);
        image_mtx.invert();
        break;
    case 5:
        image_mtx *= agg::trans_affine_translation(-image_center_x, -image_center_y);
        image_mtx *= agg::trans_affine_rotation(ia);
        image_mtx *= agg::trans_affine_rotation(pa);
        image_mtx *= agg::trans_affine_scaling(img_scale, img_scale);
        image_mtx *= agg::trans_affine_scaling(poly_scale, poly_scale);
        image_mtx *= agg::trans_affine_translation(img_cx, img_cy);
        image_mtx.invert();
        break;
    case 6:
        image_mtx *= agg::trans_affine_translation(-img_cx, -img_cy);
        image_mtx *= agg::trans_affine_rotation(ia);
        image_mtx *= agg::trans_affine_scaling(img_scale, img_scale);
        image_mtx *= agg::trans_affine_translation(img_cx, img_cy);
        image_mtx.invert();
        break;
    default: break; // Example 0: identity
    }

    typedef agg::span_interpolator_linear<> interpolator_type;
    interpolator_type interpolator(image_mtx);
    pixfmt pixf_img(img_rbuf);
    typedef agg::span_image_filter_rgba_bilinear_clip<pixfmt, interpolator_type> span_gen_type;
    span_gen_type sg(pixf_img, agg::rgba(1, 1, 1), interpolator);

    agg::conv_transform<agg::path_storage> tr(star, poly_mtx);
    ras.add_path(tr);
    agg::render_scanlines_aa(ras, sl, rb, sa, sg);

    agg::ellipse e1(img_cx, img_cy, 5, 5, 20);
    agg::ellipse e2(img_cx, img_cy, 2, 2, 20);
    agg::conv_stroke<agg::ellipse> c1(e1);

    rs.color(agg::rgba(0.7, 0.8, 0));
    ras.add_path(e1);
    agg::render_scanlines(ras, sl, rs);

    rs.color(agg::rgba(0, 0, 0));
    ras.add_path(c1);
    agg::render_scanlines(ras, sl, rs);

    ras.add_path(e2);
    agg::render_scanlines(ras, sl, rs);

    // Controls, as the example builds them (flip_y = true, so !flip_y).
    agg::slider_ctrl<agg::rgba8> m_polygon_angle(5, 5, 145, 11, false);
    m_polygon_angle.label("Polygon Angle=%3.2f"); m_polygon_angle.range(-180, 180); m_polygon_angle.value(poly_angle);
    agg::slider_ctrl<agg::rgba8> m_polygon_scale(5, 19, 145, 26, false);
    m_polygon_scale.label("Polygon Scale=%3.2f"); m_polygon_scale.range(0.1, 5.0); m_polygon_scale.value(poly_scale);
    agg::slider_ctrl<agg::rgba8> m_image_angle(155, 5, 300, 12, false);
    m_image_angle.label("Image Angle=%3.2f"); m_image_angle.range(-180, 180); m_image_angle.value(img_angle);
    agg::slider_ctrl<agg::rgba8> m_image_scale(155, 19, 300, 26, false);
    m_image_scale.label("Image Scale=%3.2f"); m_image_scale.range(0.1, 5.0); m_image_scale.value(img_scale);
    agg::cbox_ctrl<agg::rgba8> m_rotate_polygon(5, 33, "Rotate Polygon", false);
    m_rotate_polygon.status(rotate_polygon);
    agg::cbox_ctrl<agg::rgba8> m_rotate_image(5, 47, "Rotate Image", false);
    m_rotate_image.status(rotate_image);
    // The example gives its rbox a degenerate box at (-3, 56): only the items show.
    agg::rbox_ctrl<agg::rgba8> m_example(-3.0, 56.0, -3.0, 56.0, false);
    for (int i = 0; i <= 6; ++i) { char b[2] = {char('0' + i), 0}; m_example.add_item(b); }
    m_example.cur_item(example_idx);

    agg::render_ctrl(ras, sl, rb, m_polygon_angle);
    agg::render_ctrl(ras, sl, rb, m_polygon_scale);
    agg::render_ctrl(ras, sl, rb, m_image_angle);
    agg::render_ctrl(ras, sl, rb, m_image_scale);
    agg::render_ctrl(ras, sl, rb, m_rotate_polygon);
    agg::render_ctrl(ras, sl, rb, m_rotate_image);
    agg::render_ctrl(ras, sl, rb, m_example);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
