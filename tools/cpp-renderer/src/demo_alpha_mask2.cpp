// alpha_mask2.cpp headless reproduction (default state: 10 mask ellipses, no
// rotation/scale/skew, 512x400 bgr24 window cleared to white).
//
// Params (all optional): num_ellipses, left_x, left_y, skew_x, skew_y.
// left_x/left_y is a left press, turned into angle and scale as the example's
// transform() does it; skew_x/skew_y is a right press.
//
// The example draws everything random from rand(), reseeded with srand(1432)
// each time it builds the mask; the first on_draw builds it (the slider value
// differs from the stored 0) and then carries the sequence on through the
// lines, markers, anti-aliased lines and gradient discs. The headless version
// draws from msvc_rand seeded 1432, taking each value in argument order, so the
// C# port can reproduce the same frame.
//
// The example's gradient_linear_color specializations for srgba8 and sgray8 are
// left out: its color_type is rgba8, so the generic template is what it uses.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_bounding_rect.h"
#include "agg_renderer_outline_aa.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_pixfmt_rgb.h"
#include "agg_pixfmt_gray.h"
#include "agg_pixfmt_amask_adaptor.h"
#include "agg_renderer_primitives.h"
#include "agg_renderer_markers.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "agg_alpha_mask_u8.h"
#include "agg_ellipse.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"
#include "flash_shape.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_alpha_mask2(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;
    typedef agg::amask_no_clip_gray8 alpha_mask_type;

    double num_ellipses = params.size() > 0 ? params[0] : 10.0;
    double angle = 0.0;
    double scale = 1.0;
    if (params.size() > 2) {
        double x = int(params[1]) - w / 2.0;
        double y = int(params[2]) - h / 2.0;
        angle = atan2(y, x);
        scale = sqrt(y * y + x * x) / 100.0;
    }
    double skew_x = params.size() > 3 ? params[3] : 0.0;
    double skew_y = params.size() > 4 ? params[4] : 0.0;

    // flip_y = true, so the slider gets !flip_y = false.
    agg::slider_ctrl<color_type> m_num_cb(5, 5, 150, 12, false);
    m_num_cb.range(5, 100);
    m_num_cb.value(num_ellipses);
    m_num_cb.label("N=%.2f");

    agg::path_storage g_path;
    agg::srgba8 g_colors[100];
    unsigned g_path_idx[100];
    unsigned g_npaths = parse_lion(g_path, g_colors, g_path_idx);

    double x1, y1, x2, y2;
    agg::pod_array_adaptor<unsigned> pia(g_path_idx, 100);
    agg::bounding_rect(g_path, pia, 0, g_npaths, &x1, &y1, &x2, &y2);
    double g_base_dx = (x2 - x1) / 2.0;
    double g_base_dy = (y2 - y1) / 2.0;

    agg::rasterizer_scanline_aa<> g_rasterizer;
    agg::scanline_u8 g_scanline;

    agg::msvc_rand rng;
    rng.holdrand = 1432;

    // generate_alpha_mask(width, height).
    int cx = int(w);
    int cy = int(h);
    std::vector<unsigned char> alpha_buf(cx * cy);
    agg::rendering_buffer alpha_mask_rbuf(&alpha_buf[0], cx, cy, cx);
    alpha_mask_type alpha_mask(alpha_mask_rbuf);
    {
        typedef agg::renderer_base<agg::pixfmt_sgray8> ren_base;
        typedef agg::renderer_scanline_aa_solid<ren_base> renderer;

        agg::pixfmt_sgray8 pixf(alpha_mask_rbuf);
        ren_base rb(pixf);
        renderer r(rb);
        agg::scanline_p8 sl;

        rb.clear(agg::sgray8(0));

        agg::ellipse ell;
        for (int i = 0; i < (int)m_num_cb.value(); i++) {
            int ex = rng.next() % cx;
            int ey = rng.next() % cy;
            int erx = rng.next() % 100 + 20;
            int ery = rng.next() % 100 + 20;
            ell.init(ex, ey, erx, ery, 100);

            g_rasterizer.add_path(ell);
            int v = (rng.next() & 127) + 128;
            int a = (rng.next() & 127) + 128;
            r.color(agg::sgray8(v, a));
            agg::render_scanlines(g_rasterizer, sl, r);
        }
    }

    int width = int(w);
    int height = int(h);

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);

    typedef agg::pixfmt_amask_adaptor<pixfmt, alpha_mask_type> pixfmt_amask_type;
    typedef agg::renderer_base<pixfmt_amask_type>              amask_ren_type;
    typedef agg::renderer_base<pixfmt>                         base_ren_type;

    pixfmt_amask_type pfa(pf, alpha_mask);
    amask_ren_type r(pfa);
    base_ren_type rbase(pf);

    agg::renderer_scanline_aa_solid<amask_ren_type> rs(r);

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-g_base_dx, -g_base_dy);
    mtx *= agg::trans_affine_scaling(scale, scale);
    mtx *= agg::trans_affine_rotation(angle + agg::pi);
    mtx *= agg::trans_affine_skewing(skew_x / 1000.0, skew_y / 1000.0);
    // The example passes width/2 and height/2 as ints.
    mtx *= agg::trans_affine_translation(width / 2, height / 2);

    rbase.clear(agg::rgba(1, 1, 1));

    // Render the lion
    agg::conv_transform<agg::path_storage, agg::trans_affine> trans(g_path, mtx);
    agg::render_all_paths(g_rasterizer, g_scanline, rs, trans, g_colors, g_path_idx, g_npaths);

    // Render random Bresenham lines and markers
    agg::renderer_markers<amask_ren_type> m(r);
    for (int i = 0; i < 50; i++) {
        int lr = rng.next() & 0x7F;
        int lg = rng.next() & 0x7F;
        int lb = rng.next() & 0x7F;
        int la = (rng.next() & 0x7F) + 0x7F;
        m.line_color(agg::srgba8(lr, lg, lb, la));
        int fr = rng.next() & 0x7F;
        int fg = rng.next() & 0x7F;
        int fb = rng.next() & 0x7F;
        int fa = (rng.next() & 0x7F) + 0x7F;
        m.fill_color(agg::srgba8(fr, fg, fb, fa));

        int lx1 = m.coord(rng.next() % width);
        int ly1 = m.coord(rng.next() % height);
        int lx2 = m.coord(rng.next() % width);
        int ly2 = m.coord(rng.next() % height);
        m.line(lx1, ly1, lx2, ly2);

        int mx = rng.next() % width;
        int my = rng.next() % height;
        int mr = rng.next() % 10 + 5;
        m.marker(mx, my, mr, agg::marker_e(rng.next() % agg::end_of_markers));
    }

    // Render random anti-aliased lines
    double lw = 5.0;
    agg::line_profile_aa profile;
    profile.width(lw);

    typedef agg::renderer_outline_aa<amask_ren_type> renderer_type;
    renderer_type ren(r, profile);

    typedef agg::rasterizer_outline_aa<renderer_type> rasterizer_type;
    rasterizer_type ras(ren);
    ras.round_cap(true);

    for (int i = 0; i < 50; i++) {
        int cr = rng.next() & 0x7F;
        int cg = rng.next() & 0x7F;
        int cb = rng.next() & 0x7F;
        int ca = (rng.next() & 0x7F) + 0x7F;
        ren.color(agg::srgba8(cr, cg, cb, ca));
        int ax1 = rng.next() % width;
        int ay1 = rng.next() % height;
        ras.move_to_d(ax1, ay1);
        int ax2 = rng.next() % width;
        int ay2 = rng.next() % height;
        ras.line_to_d(ax2, ay2);
        ras.render(false);
    }

    // Render random circles with gradient
    typedef agg::gradient_linear_color<color_type> grad_color;
    typedef agg::gradient_circle grad_func;
    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_gradient<color_type,
                               interpolator_type,
                               grad_func,
                               grad_color> span_grad_type;

    agg::trans_affine grm;
    grad_func grf;
    grad_color grc(agg::srgba8(0, 0, 0), agg::srgba8(0, 0, 0));
    agg::ellipse ell;
    agg::span_allocator<color_type> sa;
    interpolator_type inter(grm);
    span_grad_type sg(inter, grf, grc, 0, 10);
    agg::renderer_scanline_aa<amask_ren_type,
                              agg::span_allocator<color_type>,
                              span_grad_type> rg(r, sa, sg);
    for (int i = 0; i < 50; i++) {
        int x = rng.next() % width;
        int y = rng.next() % height;
        double cr = rng.next() % 10 + 5;
        grm.reset();
        grm *= agg::trans_affine_scaling(cr / 10.0);
        grm *= agg::trans_affine_translation(x, y);
        grm.invert();
        int gr = rng.next() & 0x7F;
        int gg = rng.next() & 0x7F;
        int gb = rng.next() & 0x7F;
        grc.colors(agg::srgba8(255, 255, 255, 0),
                   agg::srgba8(gr, gg, gb, 255));
        sg.color_function(grc);
        ell.init(x, y, cr, cr, 32);
        g_rasterizer.add_path(ell);
        agg::render_scanlines(g_rasterizer, g_scanline, rg);
    }

    agg::render_ctrl(g_rasterizer, g_scanline, rbase, m_num_cb);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
