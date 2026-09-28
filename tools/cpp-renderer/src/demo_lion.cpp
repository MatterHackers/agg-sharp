// lion.cpp headless reproduction (default state: alpha slider = 0.1, no
// rotation/scale/skew, 512x400 bgr24 window cleared to white).
//
// Params (all optional): angle, scale, skew_x, skew_y, alpha, skip_controls.
// skip_controls=1 leaves out the alpha slider so the lion alone can be compared.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_bounding_rect.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_lion(unsigned w, unsigned h,
                const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double angle = params.size() > 0 ? params[0] : 0.0;
    double scale = params.size() > 1 ? params[1] : 1.0;
    double skew_x = params.size() > 2 ? params[2] : 0.0;
    double skew_y = params.size() > 3 ? params[3] : 0.0;
    double alpha = params.size() > 4 ? params[4] : 0.1;
    bool skip_controls = params.size() > 5 ? params[5] > 0.5 : false;

    // The example keeps its colors as srgba8; parse_lion's rgba8 -> srgba8 store
    // and the renderer's srgba8 -> rgba8 read are both implicit conversions.
    agg::path_storage path;
    agg::srgba8 colors[100];
    unsigned path_idx[100];
    unsigned npaths = parse_lion(path, colors, path_idx);

    double x1, y1, x2, y2;
    agg::pod_array_adaptor<unsigned> pia(path_idx, 100);
    agg::bounding_rect(path, pia, 0, npaths, &x1, &y1, &x2, &y2);
    double base_dx = (x2 - x1) / 2.0;
    double base_dy = (y2 - y1) / 2.0;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid r(rb);
    // on_resize clears to white; on_draw then draws over it.
    rb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> g_rasterizer;
    agg::scanline_p8 g_scanline;

    for (unsigned i = 0; i < npaths; i++) {
        colors[i].a = agg::int8u(alpha * 255);
    }

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-base_dx, -base_dy);
    mtx *= agg::trans_affine_scaling(scale, scale);
    mtx *= agg::trans_affine_rotation(angle + agg::pi);
    mtx *= agg::trans_affine_skewing(skew_x / 1000.0, skew_y / 1000.0);
    // The example passes width/2 and height/2 as ints.
    mtx *= agg::trans_affine_translation(int(w) / 2, int(h) / 2);

    agg::conv_transform<agg::path_storage, agg::trans_affine> trans(path, mtx);
    agg::render_all_paths(g_rasterizer, g_scanline, r, trans, colors, path_idx, npaths);

    if (!skip_controls) {
        // flip_y = true in the demo => ctrl flip = !flip_y = false.
        agg::slider_ctrl<agg::rgba8> m_alpha_slider(5, 5, 512 - 5, 12, false);
        m_alpha_slider.no_transform();
        m_alpha_slider.label("Alpha%3.3f");
        m_alpha_slider.value(alpha);
        agg::render_ctrl(g_rasterizer, g_scanline, rb, m_alpha_slider);
    }

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
