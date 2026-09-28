// lion_lens.cpp headless reproduction (default state: magnification 3,
// radius 70, lens at 200,150, 500x600 bgr24 window cleared to white).
//
// Params (all optional): magnification, radius, lens_x, lens_y.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_trans_warp_magnifier.h"
#include "agg_conv_segmentator.h"
#include "agg_bounding_rect.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_lion_lens(unsigned w, unsigned h,
                     const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double magn = params.size() > 0 ? params[0] : 3.0;
    double radius = params.size() > 1 ? params[1] : 70.0;
    double lens_x = params.size() > 2 ? params[2] : 200.0;
    double lens_y = params.size() > 3 ? params[3] : 150.0;

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

    // flip_y = true in the demo => ctrl flip = !flip_y = false.
    agg::slider_ctrl<agg::rgba8> m_magn_slider(5, 5, 495, 12, false);
    agg::slider_ctrl<agg::rgba8> m_radius_slider(5, 20, 495, 27, false);
    m_magn_slider.no_transform();
    m_magn_slider.range(0.01, 4.0);
    m_magn_slider.value(magn);
    m_magn_slider.label("Scale=%3.2f");
    m_radius_slider.no_transform();
    m_radius_slider.range(0.0, 100.0);
    m_radius_slider.value(radius);
    m_radius_slider.label("Radius=%3.2f");

    agg::trans_warp_magnifier lens;
    lens.center(lens_x, lens_y);
    lens.magnification(m_magn_slider.value());
    lens.radius(m_radius_slider.value() / m_magn_slider.value());

    agg::conv_segmentator<agg::path_storage> segm(path);

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-base_dx, -base_dy);
    mtx *= agg::trans_affine_rotation(0.0 + agg::pi);
    // width() / 2 is a double here, unlike lion.cpp's ints.
    mtx *= agg::trans_affine_translation(w / 2.0, h / 2.0);

    agg::conv_transform<agg::conv_segmentator<agg::path_storage> > trans_mtx(segm, mtx);
    agg::conv_transform<
        agg::conv_transform<agg::conv_segmentator<agg::path_storage> >,
        agg::trans_warp_magnifier> trans_lens(trans_mtx, lens);

    agg::render_all_paths(g_rasterizer, g_scanline, r, trans_lens, colors, path_idx, npaths);

    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_magn_slider);
    agg::render_ctrl(g_rasterizer, g_scanline, rb, m_radius_slider);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
