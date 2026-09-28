// conv_contour.cpp headless reproduction (default state: 440x330, "Close", width 0, autodetect off).
// Params (all optional): close (0 close, 1 close CW, 2 close CCW), width, auto_detect (0/1).
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_conv_curve.h"
#include "agg_conv_contour.h"
#include "agg_conv_transform.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_path_storage.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

int render_conv_contour(unsigned w, unsigned h,
                        const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> ren_base;

    int close_item = params.size() > 0 ? int(params[0]) : 0;
    double width = params.size() > 1 ? params[1] : 0.0;
    bool auto_detect = params.size() > 2 && params[2] != 0;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    ren_base renb(pixf);
    renb.clear(agg::rgba(1, 1, 1));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    // compose_path()
    unsigned flag = 0;
    if (close_item == 1) flag = agg::path_flags_cw;
    if (close_item == 2) flag = agg::path_flags_ccw;
    agg::path_storage path;
    path.remove_all();
    path.move_to(28.47, 6.45);
    path.curve3(21.58, 1.12, 19.82, 0.29);
    path.curve3(17.19, -0.93, 14.21, -0.93);
    path.curve3(9.57, -0.93, 6.57, 2.25);
    path.curve3(3.56, 5.42, 3.56, 10.60);
    path.curve3(3.56, 13.87, 5.03, 16.26);
    path.curve3(7.03, 19.58, 11.99, 22.51);
    path.curve3(16.94, 25.44, 28.47, 29.64);
    path.line_to(28.47, 31.40);
    path.curve3(28.47, 38.09, 26.34, 40.58);
    path.curve3(24.22, 43.07, 20.17, 43.07);
    path.curve3(17.09, 43.07, 15.28, 41.41);
    path.curve3(13.43, 39.75, 13.43, 37.60);
    path.line_to(13.53, 34.77);
    path.curve3(13.53, 32.52, 12.38, 31.30);
    path.curve3(11.23, 30.08, 9.38, 30.08);
    path.curve3(7.57, 30.08, 6.42, 31.35);
    path.curve3(5.27, 32.62, 5.27, 34.81);
    path.curve3(5.27, 39.01, 9.57, 42.53);
    path.curve3(13.87, 46.04, 21.63, 46.04);
    path.curve3(27.59, 46.04, 31.40, 44.04);
    path.curve3(34.28, 42.53, 35.64, 39.31);
    path.curve3(36.52, 37.21, 36.52, 30.71);
    path.line_to(36.52, 15.53);
    path.curve3(36.52, 9.13, 36.77, 7.69);
    path.curve3(37.01, 6.25, 37.57, 5.76);
    path.curve3(38.13, 5.27, 38.87, 5.27);
    path.curve3(39.65, 5.27, 40.23, 5.62);
    path.curve3(41.26, 6.25, 44.19, 9.18);
    path.line_to(44.19, 6.45);
    path.curve3(38.72, -0.88, 33.74, -0.88);
    path.curve3(31.35, -0.88, 29.93, 0.78);
    path.curve3(28.52, 2.44, 28.47, 6.45);
    path.close_polygon(flag);

    path.move_to(28.47, 9.62);
    path.line_to(28.47, 26.66);
    path.curve3(21.09, 23.73, 18.95, 22.51);
    path.curve3(15.09, 20.36, 13.43, 18.02);
    path.curve3(11.77, 15.67, 11.77, 12.89);
    path.curve3(11.77, 9.38, 13.87, 7.06);
    path.curve3(15.97, 4.74, 18.70, 4.74);
    path.curve3(22.41, 4.74, 28.47, 9.62);
    path.close_polygon(flag);

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_scaling(4.0);
    mtx *= agg::trans_affine_translation(150, 100);

    agg::conv_transform<agg::path_storage> trans(path, mtx);
    agg::conv_curve<agg::conv_transform<agg::path_storage> > curve(trans);
    agg::conv_contour<agg::conv_curve<agg::conv_transform<agg::path_storage> > > contour(curve);
    contour.width(width);
    contour.auto_detect_orientation(auto_detect);

    ras.add_path(contour);
    agg::render_scanlines_aa_solid(ras, sl, renb, agg::rgba(0, 0, 0));

    // enum flip_y = true => ctrl flip = !flip_y = false.
    agg::rbox_ctrl<agg::rgba8> m_close(10.0, 10.0, 130.0, 80.0, false);
    agg::slider_ctrl<agg::rgba8> m_width(130 + 10.0, 10.0 + 4.0, 130 + 300.0, 10.0 + 8.0 + 4.0, false);
    agg::cbox_ctrl<agg::rgba8> m_auto_detect(130 + 10.0, 10.0 + 4.0 + 16.0, "Autodetect orientation if not defined", false);
    m_close.add_item("Close");
    m_close.add_item("Close CW");
    m_close.add_item("Close CCW");
    m_close.cur_item(close_item);
    m_width.range(-100.0, 100.0);
    m_width.value(width);
    m_width.label("Width=%1.2f");
    m_auto_detect.status(auto_detect);

    agg::render_ctrl(ras, sl, renb, m_close);
    agg::render_ctrl(ras, sl, renb, m_width);
    agg::render_ctrl(ras, sl, renb, m_auto_detect);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
