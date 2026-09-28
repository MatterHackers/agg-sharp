// multi_clip.cpp headless reproduction (default state: N = 6, the slider's
// initial middle of 2..10; no rotation/scale/skew; 512x400 bgr24 window cleared
// to white).
//
// Params (all optional): n, left_x, left_y, skew_x, skew_y. left_x/left_y is a
// left press, turned into angle and scale as the example's transform() does it;
// skew_x/skew_y is a right press.
//
// Everything but the slider is drawn through renderer_mclip clipped to an N x N
// grid of boxes, each inset 5 pixels. The example draws its random lines,
// markers and gradient discs from rand(), unseeded, carrying the sequence on
// across redraws; the headless version draws the first frame's from msvc_rand
// seeded 1, taking each value in argument order, so the C# port can reproduce it.
//
// The example's gradient_linear_color specializations for srgba8 and sgray8 are
// left out: its color_type is rgba8, so the generic template is what it uses.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_renderer_mclip.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_bounding_rect.h"
#include "agg_renderer_outline_aa.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_pixfmt_rgb.h"
#include "agg_renderer_primitives.h"
#include "agg_renderer_markers.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "agg_ellipse.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"
#include "flash_shape.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_multi_clip(unsigned w, unsigned h,
                      const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;

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
    m_num_cb.range(2, 10);
    m_num_cb.label("N=%.2f");
    m_num_cb.no_transform();
    if (params.size() > 0) m_num_cb.value(params[0]);

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

    int width = int(w);
    int height = int(h);

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);

    typedef agg::renderer_mclip<pixfmt> base_ren_type;
    base_ren_type r(pf);
    agg::renderer_scanline_aa_solid<base_ren_type> rs(r);

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-g_base_dx, -g_base_dy);
    mtx *= agg::trans_affine_scaling(scale, scale);
    mtx *= agg::trans_affine_rotation(angle + agg::pi);
    mtx *= agg::trans_affine_skewing(skew_x / 1000.0, skew_y / 1000.0);
    // The example passes width/2 and height/2 as ints.
    mtx *= agg::trans_affine_translation(width / 2, height / 2);

    r.clear(agg::rgba(1, 1, 1));

    r.reset_clipping(false);  // Visibility: "false" means "no visible regions"
    double n = m_num_cb.value();
    for (int cx = 0; cx < n; cx++) {
        for (int cy = 0; cy < n; cy++) {
            int bx1 = int(width * cx / n);
            int by1 = int(height * cy / n);
            int bx2 = int(width * (cx + 1) / n);
            int by2 = int(height * (cy + 1) / n);
            r.add_clip_box(bx1 + 5, by1 + 5, bx2 - 5, by2 - 5);
        }
    }

    // Render the lion
    agg::conv_transform<agg::path_storage, agg::trans_affine> trans(g_path, mtx);
    agg::render_all_paths(g_rasterizer, g_scanline, rs, trans, g_colors, g_path_idx, g_npaths);

    // Render random Bresenham lines and markers
    agg::renderer_markers<base_ren_type> m(r);
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

    typedef agg::renderer_outline_aa<base_ren_type> renderer_type;
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
    agg::renderer_scanline_aa<base_ren_type,
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

    r.reset_clipping(true); // "true" means "all rendering buffer is visible".
    agg::render_ctrl(g_rasterizer, g_scanline, r, m_num_cb);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
