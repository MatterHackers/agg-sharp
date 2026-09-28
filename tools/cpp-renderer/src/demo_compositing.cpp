// compositing.cpp headless reproduction (AGG_BGRA128: an rgba32 float window).
// Params: comp_op (default 3, src-over), src alpha (0.75), dst alpha (1.0).
// The window is float; it is written out as the example shows it, each pixel
// through srgba8(rgba32). Left out: the "%3.2f ms" render time, which changes
// from run to run (other ports leave their timers out the same way).
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_renderer_base.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_rounded_rect.h"
#include "agg_ellipse.h"
#include "agg_pixfmt_rgba.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

namespace {
typedef agg::rgba32 color;
typedef agg::order_bgra order;
typedef agg::rendering_buffer rbuf_type;
typedef agg::blender_rgba<color, order> prim_blender_type;
typedef agg::pixfmt_alpha_blend_rgba<prim_blender_type, rbuf_type> prim_pixfmt_type;
typedef agg::renderer_base<prim_pixfmt_type> prim_ren_base_type;

agg::trans_affine gradient_affine(double x1, double y1, double x2, double y2, double gradient_d2 = 100.0) {
    agg::trans_affine mtx;
    double dx = x2 - x1;
    double dy = y2 - y1;
    mtx.reset();
    mtx *= agg::trans_affine_scaling(sqrt(dx * dx + dy * dy) / gradient_d2);
    mtx *= agg::trans_affine_rotation(atan2(dy, dx));
    mtx *= agg::trans_affine_translation(x1, y1);
    mtx.invert();
    return mtx;
}

typedef agg::gradient_x gradient_func_type;
typedef agg::gradient_linear_color<color> color_func_type;
typedef agg::span_interpolator_linear<> interpolator_type;
typedef agg::span_allocator<color> span_allocator_type;
typedef agg::span_gradient<color, interpolator_type, gradient_func_type, color_func_type> span_gradient_type;

template <class RenBase>
void circle(RenBase& rbase, color c1, color c2, double x1, double y1, double x2, double y2, double shadow_alpha) {
    gradient_func_type gradient_func;
    agg::trans_affine gradient_mtx = gradient_affine(x1, y1, x2, y2, 100);
    interpolator_type span_interpolator(gradient_mtx);
    span_allocator_type span_allocator;
    color_func_type color_func(c1, c2);
    span_gradient_type span_gradient(span_interpolator, gradient_func, color_func, 0, 100);
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    double r = agg::calc_distance(x1, y1, x2, y2) / 2;
    agg::ellipse ell((x1 + x2) / 2 + 5, (y1 + y2) / 2 - 3, r, r, 100);
    ras.add_path(ell);
    agg::render_scanlines_aa_solid(ras, sl, rbase, agg::rgba(0.6, 0.6, 0.6, 0.7 * shadow_alpha));

    ell.init((x1 + x2) / 2, (y1 + y2) / 2, r, r, 100);
    ras.add_path(ell);
    agg::render_scanlines_aa(ras, sl, rbase, span_allocator, span_gradient);
}

template <class RenBase>
void src_shape(RenBase& rbase, color c1, color c2, double x1, double y1, double x2, double y2) {
    gradient_func_type gradient_func;
    agg::trans_affine gradient_mtx = gradient_affine(x1, y1, x2, y2, 100);
    interpolator_type span_interpolator(gradient_mtx);
    span_allocator_type span_allocator;
    color_func_type color_func(c1, c2);
    span_gradient_type span_gradient(span_interpolator, gradient_func, color_func, 0, 100);
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    agg::rounded_rect shape(x1, y1, x2, y2, 40);
    ras.add_path(shape);
    agg::render_scanlines_aa(ras, sl, rbase, span_allocator, span_gradient);
}

// A float image the size of w x h, top-down like headless::canvas.
struct float_canvas {
    std::vector<float> data;
    rbuf_type rbuf;
    float_canvas(unsigned w, unsigned h) : data(static_cast<size_t>(w) * h * 4, 0.0f) {
        rbuf.attach(reinterpret_cast<unsigned char*>(data.data()), w, h, static_cast<int>(w * 4 * sizeof(float)));
    }
};
} // namespace

int render_compositing(unsigned w, unsigned h, const std::vector<double>& params, const char* out) {
    int comp_op_idx = params.size() > 0 ? (int)params[0] : 3;
    double src_alpha = params.size() > 1 ? params[1] : 0.75;
    double dst_alpha = params.size() > 2 ? params[2] : 1.0;

    // load_img(1, "compositing"): the srgb24 picture converted into the bgra128 image,
    // row 0 its bottom row (flip_y = true).
    headless::image_rgba img = headless::load_ppm_flip_y("compositing.ppm");
    if (!img.ok) {
        fprintf(stderr, "compositing: failed to load compositing.ppm\n");
        return 1;
    }
    float_canvas img1(img.width, img.height);
    {
        prim_pixfmt_type pf(img1.rbuf);
        for (unsigned y = 0; y < img.height; ++y) {
            for (unsigned x = 0; x < img.width; ++x) {
                const unsigned char* s = &img.rgba[(static_cast<size_t>(y) * img.width + x) * 4];
                pf.copy_pixel(x, y, color(agg::srgba8(s[0], s[1], s[2], 255)));
            }
        }
    }

    const char* items[] = {"clear", "src", "dst", "src-over", "dst-over", "src-in", "dst-in",
        "src-out", "dst-out", "src-atop", "dst-atop", "xor", "plus", "multiply", "screen",
        "overlay", "darken", "lighten", "color-dodge", "color-burn", "hard-light", "soft-light",
        "difference", "exclusion"};

    // on_draw
    float_canvas window(w, h);
    prim_pixfmt_type pixf(window.rbuf);
    prim_ren_base_type rb(pixf);
    rb.clear(agg::srgba8(255, 255, 255));
    for (unsigned y = 0; y < rb.height(); y += 8) {
        for (unsigned x = ((y >> 3) & 1) << 3; x < rb.width(); x += 16) {
            rb.copy_bar(x, y, x + 7, y + 7, agg::srgba8(0xdf, 0xdf, 0xdf));
        }
    }

    float_canvas img0(w, h);
    prim_pixfmt_type pixf2(img0.rbuf);
    prim_ren_base_type rb2(pixf2);
    rb2.clear(agg::srgba8(0, 0, 0, 0));

    typedef agg::blender_rgba_pre<color, order> blender_type_pre;
    typedef agg::pixfmt_alpha_blend_rgba<blender_type_pre, rbuf_type> pixfmt_pre;
    pixfmt_pre pixf_pre(window.rbuf);
    agg::renderer_base<pixfmt_pre> rb_pre(pixf_pre);

    // render_scene(rbuf_img(0), pixf2)
    {
        typedef agg::comp_op_adaptor_rgba<color, order> blender_type;
        typedef agg::pixfmt_custom_blend_rgba<blender_type, rbuf_type> pixfmt_type;
        pixfmt_type ren_pixf(img0.rbuf);
        agg::renderer_base<pixfmt_type> renderer(ren_pixf);

        prim_ren_base_type rbs(pixf2);
        rbs.blend_from(prim_pixfmt_type(img1.rbuf), 0, 250, 180, agg::cover_type(dst_alpha * agg::cover_full));

        circle(rbs,
               agg::srgba8(0xFD, 0xF0, 0x6F, unsigned(dst_alpha * 255)),
               agg::srgba8(0xFE, 0x9F, 0x34, unsigned(dst_alpha * 255)),
               70 * 3, 100 + 24 * 3, 37 * 3, 100 + 79 * 3, dst_alpha);

        ren_pixf.comp_op(comp_op_idx);
        src_shape(renderer,
                  agg::srgba8(0x7F, 0xC1, 0xFF, unsigned(src_alpha * 255)),
                  agg::srgba8(0x05, 0x00, 0x5F, unsigned(src_alpha * 255)),
                  300 + 50, 100 + 24 * 3, 107 + 50, 100 + 79 * 3);
    }

    rb_pre.blend_from(pixf2);

    // The float window as the example shows it: srgba8(rgba32) per pixel.
    headless::canvas cv(w, h, 4);
    agg::pixfmt_srgba32 out_pf(cv.rbuf);
    for (unsigned y = 0; y < h; ++y) {
        for (unsigned x = 0; x < w; ++x) {
            out_pf.copy_pixel(x, y, pixf.pixel(x, y).operator agg::srgba8());
        }
    }

    // Deviation: the example draws its ctrls into the float window with rgba32 colors (render_ctrl_rs). agg-sharp's
    // ctrls carry 8-bit colors, and rgba(1, 0.9, 0.8) as 230/255 comes out a level off after srgba8 over the whole
    // slider background, so both sides draw the ctrls as 8-bit rgba8 ctrls onto the converted window instead,
    // as compositing2 draws them.
    agg::pixfmt_rgba32 ctrl_pf(cv.rbuf);
    agg::renderer_base<agg::pixfmt_rgba32> ctrl_rb(ctrl_pf);
    agg::slider_ctrl<agg::rgba8> ctrl_src(5, 5, 400, 11, false);
    agg::slider_ctrl<agg::rgba8> ctrl_dst(5, 5 + 15, 400, 11 + 15, false);
    agg::rbox_ctrl<agg::rgba8> ctrl_op(420, 5.0, 420 + 170.0, 340.0, false);
    ctrl_src.label("Src Alpha=%.2f");
    ctrl_src.value(src_alpha);
    ctrl_dst.label("Dst Alpha=%.2f");
    ctrl_dst.value(dst_alpha);
    ctrl_op.text_size(6.8);
    for (const char* it : items) ctrl_op.add_item(it);
    ctrl_op.cur_item(comp_op_idx);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;
    agg::render_ctrl(ras, sl, ctrl_rb, ctrl_src);
    agg::render_ctrl(ras, sl, ctrl_rb, ctrl_dst);
    agg::render_ctrl(ras, sl, ctrl_rb, ctrl_op);

    return headless::write_raw(out, out_pf, w, h) ? 0 : 1;
}
