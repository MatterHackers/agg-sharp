// simple_line: synthetic outline-AA test, mirrored through agg-sharp by
// AggReferenceTests.SimpleLineMatchesCppAgg.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_pixfmt_rgba.h"
#include "agg_renderer_base.h"
#include "agg_path_storage.h"
#include "agg_conv_transform.h"
#include "agg_trans_affine.h"
#include "agg_renderer_outline_aa.h"
#include "agg_rasterizer_outline_aa.h"

#include "common.h"

int render_simple_line(unsigned w, unsigned h,
                       const std::vector<double>& /*params*/, const char* out) {
    typedef agg::pixfmt_rgba32 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_outline_aa<renderer_base> renderer_type;
    typedef agg::rasterizer_outline_aa<renderer_type> rasterizer_type;

    headless::canvas cv(w, h, 4);
    // Clear to opaque white.
    std::fill(cv.data.begin(), cv.data.end(), (unsigned char)255);

    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba8(255, 255, 255, 255));

    agg::line_profile_aa profile(1.0, agg::gamma_none());
    renderer_type ren(rb, profile);
    rasterizer_type ras(ren);
    ras.round_cap(false);
    ras.line_join(agg::outline_round_join);

    agg::path_storage path;
    path.move_to(50.0, 50.0);
    path.line_to(150.0, 50.0);
    path.line_to(100.0, 150.0);
    path.close_polygon();

    path.move_to(200.0, 100.0);
    path.line_to(250.0, 70.0);
    path.line_to(280.0, 110.0);
    path.line_to(260.0, 160.0);
    path.line_to(210.0, 150.0);
    path.close_polygon();

    path.move_to(50.0, 200.0);
    path.line_to(200.0, 250.0);
    path.close_polygon();

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_rotation(agg::pi);
    mtx *= agg::trans_affine_translation(256.0, 256.5);
    agg::conv_transform<agg::path_storage> transformed(path, mtx);

    ren.color(agg::rgba8(0, 0, 0, 255));
    ras.add_path(transformed);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
