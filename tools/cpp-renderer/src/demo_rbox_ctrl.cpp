// rbox_ctrl on its own (no AGG example draws nothing but one): a white 240x160
// bgr24 frame with one rbox_ctrl of four items, rendered as render_ctrl does.
//
// Params (all optional): cur_item (default 1), styled (default 0). styled=1
// sets a thicker border with extra background, a larger text size and custom
// colors, as the examples that restyle their rboxes do.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

int render_rbox_ctrl(unsigned w, unsigned h,
                     const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;

    int cur_item = params.size() > 0 ? int(params[0]) : 1;
    bool styled = params.size() > 1 ? params[1] > 0.5 : false;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    agg::rbox_ctrl<agg::rgba8> rbox(10.0, 10.0, 150.0, 120.0, false);
    rbox.add_item("Zero");
    rbox.add_item("One");
    rbox.add_item("Two");
    rbox.add_item("Three");
    rbox.cur_item(cur_item);
    if (styled) {
        rbox.border_width(2.0, 3.0);
        rbox.text_size(11.0, 7.0);
        rbox.text_thickness(1.0);
        rbox.background_color(agg::rgba(0.9, 0.95, 1.0));
        rbox.active_color(agg::rgba(0.0, 0.3, 0.6));
    }

    agg::render_ctrl(ras, sl, rb, rbox);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
