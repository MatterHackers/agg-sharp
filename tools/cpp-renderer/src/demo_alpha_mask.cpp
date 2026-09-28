// alpha_mask.cpp headless reproduction (default state: no rotation/scale/skew,
// 512x400 bgr24 window cleared to white).
//
// Params (all optional): left_x, left_y, skew_x, skew_y. left_x/left_y is a left
// press, turned into angle and scale as the example's transform() does it;
// skew_x/skew_y is a right press.
//
// The example draws its ten mask ellipses from rand(), whose sequence is the C
// library's own; the headless version draws them from msvc_rand (the C89
// sequence, seeded 1 as rand() is unseeded) and takes each value in argument
// order, so the C# port can reproduce the same mask.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_bounding_rect.h"
#include "agg_ellipse.h"
#include "agg_pixfmt_rgb.h"
#include "agg_pixfmt_gray.h"
#include "agg_alpha_mask_u8.h"

#include "common.h"
#include "flash_shape.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_alpha_mask(unsigned w, unsigned h,
                      const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double angle = 0.0;
    double scale = 1.0;
    if (params.size() > 1) {
        double x = int(params[0]) - w / 2.0;
        double y = int(params[1]) - h / 2.0;
        angle = atan2(y, x);
        scale = sqrt(y * y + x * x) / 100.0;
    }
    double skew_x = params.size() > 2 ? params[2] : 0.0;
    double skew_y = params.size() > 3 ? params[3] : 0.0;

    agg::path_storage path;
    agg::srgba8 colors[100];
    unsigned path_idx[100];
    unsigned npaths = parse_lion(path, colors, path_idx);

    double x1, y1, x2, y2;
    agg::pod_array_adaptor<unsigned> pia(path_idx, 100);
    agg::bounding_rect(path, pia, 0, npaths, &x1, &y1, &x2, &y2);
    double base_dx = (x2 - x1) / 2.0;
    double base_dy = (y2 - y1) / 2.0;

    agg::rasterizer_scanline_aa<> g_rasterizer;

    // on_resize -> generate_alpha_mask(cx, cy).
    int cx = int(w);
    int cy = int(h);
    std::vector<unsigned char> alpha_buf(cx * cy);
    agg::rendering_buffer alpha_mask_rbuf(&alpha_buf[0], cx, cy, cx);
    agg::alpha_mask_gray8 alpha_mask(alpha_mask_rbuf);
    {
        typedef agg::renderer_base<agg::pixfmt_sgray8> ren_base;
        typedef agg::renderer_scanline_aa_solid<ren_base> renderer;

        agg::pixfmt_sgray8 pixf(alpha_mask_rbuf);
        ren_base rb(pixf);
        renderer r(rb);
        agg::scanline_p8 sl;

        rb.clear(agg::sgray8(0));

        agg::msvc_rand rng;
        agg::ellipse ell;
        for (int i = 0; i < 10; i++) {
            int ex = rng.next() % cx;
            int ey = rng.next() % cy;
            int erx = rng.next() % 100 + 20;
            int ery = rng.next() % 100 + 20;
            ell.init(ex, ey, erx, ery, 100);

            g_rasterizer.add_path(ell);
            int v = rng.next() & 0xFF;
            int a = rng.next() & 0xFF;
            r.color(agg::sgray8(v, a));
            agg::render_scanlines(g_rasterizer, sl, r);
        }
    }

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid r(rb);

    typedef agg::scanline_u8_am<agg::alpha_mask_gray8> scanline_type;
    scanline_type sl(alpha_mask);
    rb.clear(agg::srgba8(255, 255, 255));

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-base_dx, -base_dy);
    mtx *= agg::trans_affine_scaling(scale, scale);
    mtx *= agg::trans_affine_rotation(angle + agg::pi);
    mtx *= agg::trans_affine_skewing(skew_x / 1000.0, skew_y / 1000.0);
    // The example passes width/2 and height/2 as ints.
    mtx *= agg::trans_affine_translation(int(w) / 2, int(h) / 2);

    agg::conv_transform<agg::path_storage, agg::trans_affine> trans(path, mtx);
    agg::render_all_paths(g_rasterizer, sl, r, trans, colors, path_idx, npaths);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
