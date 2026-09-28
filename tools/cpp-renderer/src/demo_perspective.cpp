// perspective.cpp headless reproduction. Default state renders the lion under a
// rect->quad Bilinear transform (m_trans_type default item 0), plus the quad
// tool and the rbox control, exactly as the original on_init() and on_draw().
// Params (all optional): the quad's four corners x0 y0 .. x3 y3, then trans_type.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_bounding_rect.h"
#include "agg_ellipse.h"
#include "agg_trans_bilinear.h"
#include "agg_trans_perspective.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "interactive_polygon.h"

#include "common.h"

// From examples/parse_lion.cpp
unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

int render_perspective(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    agg::path_storage path;
    agg::srgba8 colors[100];
    unsigned path_idx[100];
    unsigned npaths = parse_lion(path, colors, path_idx);

    double x1, y1, x2, y2;
    agg::pod_array_adaptor<unsigned> pia(path_idx, 100);
    agg::bounding_rect(path, pia, 0, npaths, &x1, &y1, &x2, &y2);
    // parse_lion() in perspective.cpp mirrors the lion in both axes.
    path.flip_x(x1, x2);
    path.flip_y(y1, y2);

    // Default quad: the bounding rect corners, then on_init()'s shift, which moves corner 0 by half the
    // window less half the lion (so the lion's own bbox origin stays in the offset, as in the example).
    agg::interactive_polygon quad(4, 5.0);
    quad.xn(0) = x1; quad.yn(0) = y1;
    quad.xn(1) = x2; quad.yn(1) = y1;
    quad.xn(2) = x2; quad.yn(2) = y2;
    quad.xn(3) = x1; quad.yn(3) = y2;
    double dx = w / 2.0 - (quad.xn(1) - quad.xn(0)) / 2.0;
    double dy = h / 2.0 - (quad.yn(2) - quad.yn(0)) / 2.0;
    for (int i = 0; i < 4; ++i) {
        quad.xn(i) += dx;
        quad.yn(i) += dy;
    }

    // Optional param overrides: 0..7 = quad corners, 8 = trans_type.
    for (int i = 0; i < 4; ++i) {
        if (params.size() > (size_t)(i * 2))     quad.xn(i) = params[i * 2];
        if (params.size() > (size_t)(i * 2 + 1)) quad.yn(i) = params[i * 2 + 1];
    }
    int trans_type = params.size() > 8 ? (int)params[8] : 0;
    const double* quad_poly = quad.polygon();

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid r(rb);
    rb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;
    ras.clip_box(0, 0, w, h);

    if (trans_type == 0) {
        agg::trans_bilinear tr(x1, y1, x2, y2, quad_poly);
        if (tr.is_valid()) {
            agg::conv_transform<agg::path_storage, agg::trans_bilinear> trans(path, tr);
            agg::render_all_paths(ras, sl, r, trans, colors, path_idx, npaths);

            agg::ellipse ell((x1 + x2) * 0.5, (y1 + y2) * 0.5,
                             (x2 - x1) * 0.5, (y2 - y1) * 0.5, 200);
            agg::conv_stroke<agg::ellipse> ell_stroke(ell);
            ell_stroke.width(3.0);
            agg::conv_transform<agg::ellipse, agg::trans_bilinear> trans_ell(ell, tr);
            agg::conv_transform<agg::conv_stroke<agg::ellipse>, agg::trans_bilinear>
                trans_ell_stroke(ell_stroke, tr);

            ras.add_path(trans_ell);
            r.color(agg::rgba(0.5, 0.3, 0.0, 0.3));
            agg::render_scanlines(ras, sl, r);

            ras.add_path(trans_ell_stroke);
            r.color(agg::rgba(0.0, 0.3, 0.2, 1.0));
            agg::render_scanlines(ras, sl, r);
        }
    } else {
        agg::trans_perspective tr(x1, y1, x2, y2, quad_poly);
        if (tr.is_valid()) {
            agg::conv_transform<agg::path_storage, agg::trans_perspective> trans(path, tr);
            agg::render_all_paths(ras, sl, r, trans, colors, path_idx, npaths);

            agg::ellipse ell((x1 + x2) * 0.5, (y1 + y2) * 0.5,
                             (x2 - x1) * 0.5, (y2 - y1) * 0.5, 200);
            agg::conv_stroke<agg::ellipse> ell_stroke(ell);
            ell_stroke.width(3.0);
            agg::conv_transform<agg::ellipse, agg::trans_perspective> trans_ell(ell, tr);
            agg::conv_transform<agg::conv_stroke<agg::ellipse>, agg::trans_perspective>
                trans_ell_stroke(ell_stroke, tr);

            ras.add_path(trans_ell);
            r.color(agg::rgba(0.5, 0.3, 0.0, 0.3));
            agg::render_scanlines(ras, sl, r);

            ras.add_path(trans_ell_stroke);
            r.color(agg::rgba(0.0, 0.3, 0.2, 1.0));
            agg::render_scanlines(ras, sl, r);
        }
    }

    // The "quad" tool: interactive_polygon's closed outline plus a circle at each corner, in one pass.
    ras.add_path(quad);
    r.color(agg::rgba(0, 0.3, 0.5, 0.6));
    agg::render_scanlines(ras, sl, r);

    // rbox control (flip_y = true in the demo, so ctrl flip = !flip_y = false).

    agg::rbox_ctrl<agg::rgba8> trans_type_ctrl(420, 5.0, 420 + 130.0, 55.0, false);
    trans_type_ctrl.add_item("Bilinear");
    trans_type_ctrl.add_item("Perspective");
    trans_type_ctrl.cur_item(trans_type);
    agg::render_ctrl(ras, sl, rb, trans_type_ctrl);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
