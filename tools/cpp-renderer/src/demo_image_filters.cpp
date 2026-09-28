// image_filters.cpp headless reproduction: the example's on_draw() after its start-up transform_image(0.0) and
// num_steps single steps, with the spheres image loaded as the flip_y = true example sees it. transform_image
// renders the image, rotated about its center and clipped to a circle, through the chosen filter into
// rbuf_img(0), reading the previous result from rbuf_img(1); on_draw copies rbuf_img(0) to (110, 35) and draws
// the "NSteps=%d" text and the ctrls. Choosing a filter (on_ctrl_change) restarts from the original image with
// transform_image(0.0), so filter f after n steps is the same whether f was chosen first or later. Left out:
// the "%3.2f Kpix/sec" benchmark text, which only a RUN Test! pass shows and which differs every run.
// Params (all optional): filter item (1 = bilinear), step degrees (5), normalize (1), filter radius (4),
// number of steps (0).
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_ellipse.h"
#include "agg_trans_affine.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_scanline_u.h"
#include "agg_scanline_p.h"
#include "agg_image_accessors.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_span_interpolator_linear.h"
#include "agg_span_image_filter_rgb.h"
#include "agg_image_filters.h"
#include "agg_gsv_text.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

namespace {

typedef agg::pixfmt_bgr24 pixfmt;
typedef agg::pixfmt_bgr24_pre pixfmt_pre;
typedef agg::renderer_base<pixfmt> renderer_base;
typedef agg::renderer_base<pixfmt_pre> renderer_base_pre;
typedef pixfmt::color_type color_type;

// The example's transform_image(angle): rbuf_img(1) rotated by angle into rbuf_img(0).
void transform_image(agg::rendering_buffer& img0, agg::rendering_buffer& img1, double angle,
                     int filter_item, bool norm, double radius) {
    double width = img0.width();
    double height = img0.height();

    pixfmt pixf(img0);
    pixfmt_pre pixf_pre(img0);
    renderer_base rb(pixf);
    renderer_base_pre rb_pre(pixf_pre);

    rb.clear(agg::rgba(1.0, 1.0, 1.0));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;
    agg::span_allocator<color_type> sa;

    agg::trans_affine src_mtx;
    src_mtx *= agg::trans_affine_translation(-width / 2.0, -height / 2.0);
    src_mtx *= agg::trans_affine_rotation(angle * agg::pi / 180.0);
    src_mtx *= agg::trans_affine_translation(width / 2.0, height / 2.0);

    agg::trans_affine img_mtx = src_mtx;
    img_mtx.invert();

    double r = width;
    if (height < r) r = height;

    r *= 0.5;
    r -= 4.0;
    agg::ellipse ell(width / 2.0, height / 2.0, r, r, 200);
    agg::conv_transform<agg::ellipse> tr(ell, src_mtx);

    typedef agg::span_interpolator_linear<> interpolator_type;
    interpolator_type interpolator(img_mtx);

    agg::image_filter_lut filter;

    typedef agg::image_accessor_clip<pixfmt> source_type;
    pixfmt pixf_img(img1);
    source_type source(pixf_img, agg::rgba_pre(0, 0, 0, 0));

    switch (filter_item) {
    case 0: {
        typedef agg::span_image_filter_rgb_nn<source_type, interpolator_type> span_gen_type;
        span_gen_type sg(source, interpolator);
        ras.add_path(tr);
        agg::render_scanlines_aa(ras, sl, rb_pre, sa, sg);
    } break;

    case 1: {
        typedef agg::span_image_filter_rgb_bilinear_clip<pixfmt, interpolator_type> span_gen_type;
        span_gen_type sg(pixf_img, agg::rgba_pre(0, 0, 0, 0), interpolator);
        ras.add_path(tr);
        agg::render_scanlines_aa(ras, sl, rb_pre, sa, sg);
    } break;

    case 5:
    case 6:
    case 7: {
        switch (filter_item) {
        case 5: filter.calculate(agg::image_filter_hanning(), norm); break;
        case 6: filter.calculate(agg::image_filter_hamming(), norm); break;
        case 7: filter.calculate(agg::image_filter_hermite(), norm); break;
        }

        typedef agg::span_image_filter_rgb_2x2<source_type, interpolator_type> span_gen_type;
        span_gen_type sg(source, interpolator, filter);
        ras.add_path(tr);
        agg::render_scanlines_aa(ras, sl, rb_pre, sa, sg);
    } break;

    default: {
        switch (filter_item) {
        case 2: filter.calculate(agg::image_filter_bicubic(), norm); break;
        case 3: filter.calculate(agg::image_filter_spline16(), norm); break;
        case 4: filter.calculate(agg::image_filter_spline36(), norm); break;
        case 8: filter.calculate(agg::image_filter_kaiser(), norm); break;
        case 9: filter.calculate(agg::image_filter_quadric(), norm); break;
        case 10: filter.calculate(agg::image_filter_catrom(), norm); break;
        case 11: filter.calculate(agg::image_filter_gaussian(), norm); break;
        case 12: filter.calculate(agg::image_filter_bessel(), norm); break;
        case 13: filter.calculate(agg::image_filter_mitchell(), norm); break;
        case 14: filter.calculate(agg::image_filter_sinc(radius), norm); break;
        case 15: filter.calculate(agg::image_filter_lanczos(radius), norm); break;
        case 16: filter.calculate(agg::image_filter_blackman(radius), norm); break;
        }

        typedef agg::span_image_filter_rgb<source_type, interpolator_type> span_gen_type;
        span_gen_type sg(source, interpolator, filter);
        ras.add_path(tr);
        agg::render_scanlines_aa(ras, sl, rb_pre, sa, sg);
    } break;
    }
}

}  // namespace

int render_image_filters(unsigned w, unsigned h,
                         const std::vector<double>& params, const char* out) {
    int filter_item = params.size() > 0 ? (int)params[0] : 1;
    double step = params.size() > 1 ? params[1] : 5.0;
    bool normalize = params.size() > 2 ? params[2] > 0.5 : true;
    double radius = params.size() > 3 ? params[3] : 4.0;
    int num_steps = params.size() > 4 ? (int)params[4] : 0;

    agg::slider_ctrl<agg::rgba> m_step(115, 5, 400, 11, false);
    agg::slider_ctrl<agg::rgba> m_radius(115, 5 + 15, 400, 11 + 15, false);
    agg::rbox_ctrl<agg::rgba> m_filters(0.0, 0.0, 110.0, 210.0, false);
    agg::cbox_ctrl<agg::rgba> m_normalize(8.0, 215.0, "Normalize Filter", false);
    agg::cbox_ctrl<agg::rgba> m_run(8.0, 245.0, "RUN Test!", false);
    agg::cbox_ctrl<agg::rgba> m_single_step(8.0, 230.0, "Single Step", false);
    agg::cbox_ctrl<agg::rgba> m_refresh(8.0, 265.0, "Refresh", false);
    m_run.text_size(7.5);
    m_single_step.text_size(7.5);
    m_normalize.text_size(7.5);
    m_refresh.text_size(7.5);
    m_normalize.status(normalize);

    m_radius.label("Filter Radius=%.3f");
    m_step.label("Step=%3.2f");
    m_radius.range(2.0, 8.0);
    m_radius.value(radius);
    m_step.range(1.0, 10.0);
    m_step.value(step);

    const char* filter_names[] = {
        "simple (NN)", "bilinear", "bicubic", "spline16", "spline36",
        "hanning", "hamming", "hermite", "kaiser", "quadric", "catrom",
        "gaussian", "bessel", "mitchell", "sinc", "lanczos", "blackman"};
    for (const char* n : filter_names) m_filters.add_item(n);
    m_filters.cur_item(filter_item);

    m_filters.border_width(0, 0);
    m_filters.background_color(agg::rgba(0.0, 0.0, 0.0, 0.1));
    m_filters.text_size(6.0);
    m_filters.text_thickness(0.85);

    // load_img(0), then copy_img_to_img(1, 0): rbuf_img(1) is the source transform_image reads.
    headless::image_rgba img = headless::load_spheres_flip_y();
    if (!img.ok) {
        fprintf(stderr, "image_filters: failed to load spheres image\n");
        return 1;
    }
    std::vector<unsigned char> img0_bytes, img1_bytes;
    agg::rendering_buffer img0, img1;
    headless::pack_image(img, "bgr", img0_bytes, img0);
    headless::pack_image(img, "bgr", img1_bytes, img1);

    // agg_main's transform_image(0.0) (or on_ctrl_change's, after choosing the filter), then each single step:
    // copy_img_to_img(1, 0) and transform_image(step), the previous result turned again.
    transform_image(img0, img1, 0.0, filter_item, normalize, radius);
    for (int i = 0; i < num_steps; ++i) {
        img1_bytes = img0_bytes;
        transform_image(img0, img1, step, filter_item, normalize, radius);
    }

    // on_draw
    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);

    rb.clear(agg::rgba(1.0, 1.0, 1.0));
    rb.copy_from(img0, 0, 110, 35);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    char buf[64];
    sprintf(buf, "NSteps=%d", num_steps);
    agg::gsv_text t;
    t.start_point(10.0, 295.0);
    t.size(10.0);
    t.text(buf);

    agg::conv_stroke<agg::gsv_text> pt(t);
    pt.width(1.5);

    ras.add_path(pt);
    agg::render_scanlines_aa_solid(ras, sl, rb, agg::rgba(0, 0, 0));

    if (m_filters.cur_item() >= 14) {
        agg::render_ctrl(ras, sl, rb, m_radius);
    }
    agg::render_ctrl(ras, sl, rb, m_step);
    agg::render_ctrl(ras, sl, rb, m_filters);
    agg::render_ctrl(ras, sl, rb, m_run);
    agg::render_ctrl(ras, sl, rb, m_normalize);
    agg::render_ctrl(ras, sl, rb, m_single_step);
    agg::render_ctrl(ras, sl, rb, m_refresh);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
