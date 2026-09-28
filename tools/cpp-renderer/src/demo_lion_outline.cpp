// lion_outline.cpp headless reproduction (default state: width slider = 1.0,
// "Use Scanline Rasterizer" unchecked -> anti-aliased outline rasterizer).
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_renderer_outline_aa.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_bounding_rect.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_lion_outline(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double angle = params.size() > 0 ? params[0] : 0.0;
    double scale = params.size() > 1 ? params[1] : 1.0;
    double skew_x = params.size() > 2 ? params[2] : 0.0;
    double skew_y = params.size() > 3 ? params[3] : 0.0;
    double width_val = params.size() > 4 ? params[4] : 1.0;
    bool use_scanline = params.size() > 5 ? params[5] > 0.5 : false;

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
    rb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> g_rasterizer;
    agg::scanline_p8 g_scanline;

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-base_dx, -base_dy);
    mtx *= agg::trans_affine_scaling(scale, scale);
    mtx *= agg::trans_affine_rotation(angle + agg::pi);
    mtx *= agg::trans_affine_skewing(skew_x / 1000.0, skew_y / 1000.0);
    mtx *= agg::trans_affine_translation(w / 2.0, h / 2.0);

    if (use_scanline) {
        agg::conv_stroke<agg::path_storage> stroke(path);
        stroke.width(width_val);
        stroke.line_join(agg::round_join);
        agg::conv_transform<agg::conv_stroke<agg::path_storage> > trans(stroke, mtx);
        agg::render_all_paths(g_rasterizer, g_scanline, r, trans, colors, path_idx, npaths);
    } else {
        typedef agg::renderer_outline_aa<renderer_base> renderer_type;
        typedef agg::rasterizer_outline_aa<renderer_type> rasterizer_type;

        double lw = width_val * mtx.scale();
        agg::line_profile_aa profile(lw, agg::gamma_none());
        renderer_type ren(rb, profile);
        rasterizer_type ras(ren);

        agg::conv_transform<agg::path_storage> trans(path, mtx);
        ras.render_all_paths(trans, colors, path_idx, npaths);
    }

    // Controls (flip_y = true in the demo => ctrl flip = !flip_y = false).
    agg::slider_ctrl<agg::rgba8> m_width_slider(5, 5, 150, 12, false);
    m_width_slider.no_transform();
    m_width_slider.range(0.0, 4.0);
    m_width_slider.value(width_val);
    m_width_slider.label("Width %3.2f");

    agg::cbox_ctrl<agg::rgba8> m_scanline(160, 5, "Use Scanline Rasterizer", false);
    m_scanline.no_transform();
    m_scanline.status(use_scanline);

    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_width_slider);
    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_scanline);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
