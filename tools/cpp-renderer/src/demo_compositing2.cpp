// compositing2.cpp headless reproduction (default: comp-op src-over, alphas 1.0).
// The scene is scaled by the same resize matrix platform_support builds for a
// window_resize|window_keep_aspect_ratio window (trans_affine_resizing): a
// trans_viewport mapping the initial 600x400 world onto the current w x h device
// with aspect_ratio_meet, centered (0.5, 0.5). At 600x400 this is identity, so
// the fix is a no-op there; at other sizes the whole scene scales like upstream.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_ellipse.h"
#include "agg_conv_transform.h"
#include "agg_trans_affine.h"
#include "agg_trans_viewport.h"
#include "agg_pixfmt_rgba.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

namespace {
typedef agg::rgba8 color;
typedef agg::order_bgra order;

template <class Container>
void generate_color_ramp(Container& c, agg::rgba c1, agg::rgba c2, agg::rgba c3, agg::rgba c4) {
    // Match the upstream example (examples/compositing2.cpp:66-82): the stops are
    // agg::rgba, so the gradient is computed in DOUBLE precision (rgba::gradient)
    // and converted to rgba8 only on assignment into the pod_auto_array<rgba8>.
    // (Previously this pre-converted to rgba8 and used the fixed-point
    // rgba8::gradient, which diverges from the example at partial alpha.)
    unsigned i;
    for (i = 0; i < 85; i++) c[i] = c1.gradient(c2, i / 85.0);
    for (; i < 170; i++) c[i] = c2.gradient(c3, (i - 85) / 85.0);
    for (; i < 256; i++) c[i] = c3.gradient(c4, (i - 170) / 85.0);
}

template <class RenBase, class ColorRamp>
void radial_shape(agg::rasterizer_scanline_aa<>& ras, agg::scanline_u8& sl,
                  RenBase& rbase, ColorRamp& colors,
                  agg::trans_affine resize,
                  double x1, double y1, double x2, double y2) {
    typedef agg::gradient_radial gradient_func_type;
    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_allocator<color> span_allocator_type;
    typedef agg::span_gradient<color, interpolator_type, gradient_func_type, ColorRamp> span_gradient_type;

    gradient_func_type gradient_func;
    agg::trans_affine gradient_mtx;
    interpolator_type span_interpolator(gradient_mtx);
    span_allocator_type span_allocator;
    span_gradient_type span_gradient(span_interpolator, gradient_func, colors, 0, 100);

    double cx = (x1 + x2) / 2.0;
    double cy = (y1 + y2) / 2.0;
    double r = 0.5 * (((x2 - x1) < (y2 - y1)) ? (x2 - x1) : (y2 - y1));

    gradient_mtx *= agg::trans_affine_scaling(r / 100.0);
    gradient_mtx *= agg::trans_affine_translation(cx, cy);
    gradient_mtx *= resize;
    gradient_mtx.invert();

    agg::ellipse ell(cx, cy, r, r, 100);
    agg::conv_transform<agg::ellipse> trans(ell, resize);
    ras.add_path(trans);
    agg::render_scanlines_aa(ras, sl, rbase, span_allocator, span_gradient);
}
} // namespace

int render_compositing2(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::comp_op_adaptor_rgba<color, order> blender_type;
    typedef agg::pixfmt_custom_blend_rgba<blender_type, agg::rendering_buffer> pixfmt_comp;

    int comp_op_idx = params.size() > 0 ? (int)params[0] : 3;
    double src_alpha = params.size() > 1 ? params[1] : 1.0;
    double dst_alpha = params.size() > 2 ? params[2] : 1.0;

    headless::canvas cv(w, h, 4);
    // Base clear to opaque white via a plain blender.
    {
        pixfmt pf(cv.rbuf);
        agg::renderer_base<pixfmt> rb(pf);
        rb.clear(agg::srgba8(255, 255, 255));
    }

    agg::pod_auto_array<color, 256> ramp1;
    agg::pod_auto_array<color, 256> ramp2;
    generate_color_ramp(ramp1,
                        agg::rgba(0, 0, 0, dst_alpha), agg::rgba(0, 0, 1, dst_alpha),
                        agg::rgba(0, 1, 0, dst_alpha), agg::rgba(1, 0, 0, 0));
    generate_color_ramp(ramp2,
                        agg::rgba(0, 0, 0, src_alpha), agg::rgba(0, 0, 1, src_alpha),
                        agg::rgba(0, 1, 0, src_alpha), agg::rgba(1, 0, 0, 0));

    // Replicate platform_support::trans_affine_resizing() for the
    // window_keep_aspect_ratio case (agg_platform_support.h:588-608): a
    // trans_viewport from the initial 600x400 world to the current w x h device,
    // centered with aspect_ratio_meet. Identity at 600x400.
    agg::trans_viewport vp;
    vp.preserve_aspect_ratio(0.5, 0.5, agg::aspect_ratio_meet);
    vp.device_viewport(0, 0, w, h);
    vp.world_viewport(0, 0, 600, 400);
    agg::trans_affine resize = vp.to_affine();
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    pixfmt_comp pixf(cv.rbuf);
    agg::renderer_base<pixfmt_comp> ren(pixf);

    pixf.comp_op(agg::comp_op_difference);
    radial_shape(ras, sl, ren, ramp1, resize, 50, 50, 50 + 320, 50 + 320);

    pixf.comp_op(comp_op_idx);
    double cx = 50, cy = 50;
    radial_shape(ras, sl, ren, ramp2, resize, cx + 120 - 70, cy + 120 - 70, cx + 120 + 70, cy + 120 + 70);
    radial_shape(ras, sl, ren, ramp2, resize, cx + 200 - 70, cy + 120 - 70, cx + 200 + 70, cy + 120 + 70);
    radial_shape(ras, sl, ren, ramp2, resize, cx + 120 - 70, cy + 200 - 70, cx + 120 + 70, cy + 200 + 70);
    radial_shape(ras, sl, ren, ramp2, resize, cx + 200 - 70, cy + 200 - 70, cx + 200 + 70, cy + 200 + 70);

    // Controls on a plain (non-comp-op) view.
    pixfmt pf2(cv.rbuf);
    agg::renderer_base<pixfmt> rb2(pf2);

    agg::slider_ctrl<agg::rgba8> m_alpha_dst(5, 5, 400, 11, false);
    m_alpha_dst.label("Dst Alpha=%.2f"); m_alpha_dst.value(dst_alpha);
    agg::slider_ctrl<agg::rgba8> m_alpha_src(5, 5 + 15, 400, 11 + 15, false);
    m_alpha_src.label("Src Alpha=%.2f"); m_alpha_src.value(src_alpha);
    agg::rbox_ctrl<agg::rgba8> m_comp_op(420, 5.0, 420 + 170.0, 340.0, false);
    m_comp_op.text_size(6.8);
    const char* items[] = {"clear", "src", "dst", "src-over", "dst-over", "src-in", "dst-in",
        "src-out", "dst-out", "src-atop", "dst-atop", "xor", "plus", "multiply", "screen",
        "overlay", "darken", "lighten", "color-dodge", "color-burn", "hard-light", "soft-light",
        "difference", "exclusion"};
    for (const char* it : items) m_comp_op.add_item(it);
    m_comp_op.cur_item(comp_op_idx);

    agg::render_ctrl(ras, sl, rb2, m_alpha_dst);
    agg::render_ctrl(ras, sl, rb2, m_alpha_src);
    agg::render_ctrl(ras, sl, rb2, m_comp_op);

    pixfmt pf_out(cv.rbuf);
    return headless::write_raw(out, pf_out, w, h) ? 0 : 1;
}
