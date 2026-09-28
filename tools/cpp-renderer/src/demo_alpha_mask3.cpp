// alpha_mask3.cpp headless reproduction (default state: Great Britain and
// Spiral, AND, the spiral in the middle of a 640x520 bgr24 window cleared to
// white). Left out: the "Generate AlphaMask: %.3fms" and "Render with
// AlphaMask: %.3fms" timings, which differ every frame.
//
// Params (all optional): polygons, operation (the two rbox items), x, y (m_x,
// m_y: where the drag put the moving shape).
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_curve.h"
#include "agg_conv_stroke.h"
#include "agg_conv_transform.h"
#include "agg_path_storage.h"
#include "agg_pixfmt_rgb.h"
#include "agg_pixfmt_gray.h"
#include "agg_pixfmt_amask_adaptor.h"
#include "agg_alpha_mask_u8.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

void make_gb_poly(agg::path_storage& ps);
void make_arrows(agg::path_storage& ps);

namespace {

typedef agg::pixfmt_bgr24 pixfmt;
typedef agg::rgba8 color_type;
typedef agg::amask_no_clip_gray8 alpha_mask_type;

const unsigned initial_width = 640;
const unsigned initial_height = 520;

class spiral {
public:
    spiral(double x, double y, double r1, double r2, double step, double start_angle = 0)
        : m_x(x), m_y(y), m_r1(r1), m_r2(r2), m_step(step), m_start_angle(start_angle),
          m_angle(start_angle), m_da(agg::deg2rad(4.0)), m_dr(m_step / 90.0) {}

    void rewind(unsigned) {
        m_angle = m_start_angle;
        m_curr_r = m_r1;
        m_start = true;
    }

    unsigned vertex(double* x, double* y) {
        if (m_curr_r > m_r2) return agg::path_cmd_stop;
        *x = m_x + cos(m_angle) * m_curr_r;
        *y = m_y + sin(m_angle) * m_curr_r;
        m_curr_r += m_dr;
        m_angle += m_da;
        if (m_start) {
            m_start = false;
            return agg::path_cmd_move_to;
        }
        return agg::path_cmd_line_to;
    }

private:
    double m_x, m_y, m_r1, m_r2, m_step, m_start_angle;
    double m_angle, m_curr_r, m_da, m_dr;
    bool m_start;
};

struct app {
    agg::rendering_buffer& rbuf_window;
    int operation;
    double m_x, m_y;
    std::vector<unsigned char> m_alpha_buf;
    agg::rendering_buffer m_alpha_mask_rbuf;
    alpha_mask_type m_alpha_mask;
    agg::rasterizer_scanline_aa<> m_ras;
    agg::scanline_p8 m_sl;

    app(agg::rendering_buffer& rbuf, int op, double x, double y)
        : rbuf_window(rbuf), operation(op), m_x(x), m_y(y), m_alpha_mask(m_alpha_mask_rbuf) {}

    template<class VertexSource> void generate_alpha_mask(VertexSource& vs) {
        unsigned cx = rbuf_window.width();
        unsigned cy = rbuf_window.height();
        m_alpha_buf.assign(cx * cy, 0);
        m_alpha_mask_rbuf.attach(&m_alpha_buf[0], cx, cy, cx);

        typedef agg::renderer_base<agg::pixfmt_sgray8> ren_base;
        typedef agg::renderer_scanline_aa_solid<ren_base> renderer;

        agg::pixfmt_sgray8 pixf(m_alpha_mask_rbuf);
        ren_base rb(pixf);
        renderer ren(rb);

        if (operation == 0) {
            rb.clear(agg::sgray8(0));
            ren.color(agg::sgray8(255));
        } else {
            rb.clear(agg::sgray8(255));
            ren.color(agg::sgray8(0));
        }
        m_ras.add_path(vs);
        agg::render_scanlines(m_ras, m_sl, ren);
    }

    template<class VertexSource> void perform_rendering(VertexSource& vs) {
        pixfmt pixf(rbuf_window);

        typedef agg::pixfmt_amask_adaptor<pixfmt, alpha_mask_type> pixfmt_amask_type;
        typedef agg::renderer_base<pixfmt_amask_type> amask_ren_type;

        pixfmt_amask_type pixfa(pixf, m_alpha_mask);
        amask_ren_type rbase(pixfa);
        agg::renderer_scanline_aa_solid<amask_ren_type> ren(rbase);

        ren.color(agg::rgba(0.5, 0.0, 0, 0.5));

        m_ras.reset();
        m_ras.add_path(vs);
        agg::render_scanlines(m_ras, m_sl, ren);
    }

    void render(int polygons) {
        pixfmt pf(rbuf_window);
        agg::renderer_base<pixfmt> rb(pf);
        agg::renderer_scanline_aa_solid<agg::renderer_base<pixfmt> > ren(rb);

        switch (polygons) {
            case 0: {
                agg::path_storage ps1;
                agg::path_storage ps2;

                double x = m_x - initial_width / 2 + 100;
                double y = m_y - initial_height / 2 + 100;
                ps1.move_to(x + 140, y + 145);
                ps1.line_to(x + 225, y + 44);
                ps1.line_to(x + 296, y + 219);
                ps1.close_polygon();

                ps1.line_to(x + 226, y + 289);
                ps1.line_to(x + 82, y + 292);

                ps1.move_to(x + 220, y + 222);
                ps1.line_to(x + 363, y + 249);
                ps1.line_to(x + 265, y + 331);

                ps1.move_to(x + 242, y + 243);
                ps1.line_to(x + 268, y + 309);
                ps1.line_to(x + 325, y + 261);

                ps1.move_to(x + 259, y + 259);
                ps1.line_to(x + 273, y + 288);
                ps1.line_to(x + 298, y + 266);

                ps2.move_to(100 + 32, 100 + 77);
                ps2.line_to(100 + 473, 100 + 263);
                ps2.line_to(100 + 351, 100 + 290);
                ps2.line_to(100 + 354, 100 + 374);

                m_ras.reset();
                m_ras.add_path(ps1);
                ren.color(agg::rgba(0, 0, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                m_ras.reset();
                m_ras.add_path(ps2);
                ren.color(agg::rgba(0, 0.6, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                generate_alpha_mask(ps1);
                perform_rendering(ps2);
            } break;

            case 1: {
                agg::path_storage ps1;
                agg::path_storage ps2;
                agg::conv_stroke<agg::path_storage> stroke(ps2);
                stroke.width(10.0);

                double x = m_x - initial_width / 2 + 100;
                double y = m_y - initial_height / 2 + 100;
                ps1.move_to(x + 140, y + 145);
                ps1.line_to(x + 225, y + 44);
                ps1.line_to(x + 296, y + 219);
                ps1.close_polygon();

                ps1.line_to(x + 226, y + 289);
                ps1.line_to(x + 82, y + 292);

                ps1.move_to(x + 220 - 50, y + 222);
                ps1.line_to(x + 265 - 50, y + 331);
                ps1.line_to(x + 363 - 50, y + 249);
                ps1.close_polygon(agg::path_flags_ccw);

                ps2.move_to(100 + 32, 100 + 77);
                ps2.line_to(100 + 473, 100 + 263);
                ps2.line_to(100 + 351, 100 + 290);
                ps2.line_to(100 + 354, 100 + 374);
                ps2.close_polygon();

                m_ras.reset();
                m_ras.add_path(ps1);
                ren.color(agg::rgba(0, 0, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                m_ras.reset();
                m_ras.add_path(stroke);
                ren.color(agg::rgba(0, 0.6, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                generate_alpha_mask(ps1);
                perform_rendering(stroke);
            } break;

            case 2: {
                agg::path_storage gb_poly;
                agg::path_storage arrows;
                make_gb_poly(gb_poly);
                make_arrows(arrows);

                agg::trans_affine mtx1;
                agg::trans_affine mtx2;
                mtx1 *= agg::trans_affine_translation(-1150, -1150);
                mtx1 *= agg::trans_affine_scaling(2.0);

                mtx2 = mtx1;
                mtx2 *= agg::trans_affine_translation(m_x - initial_width / 2, m_y - initial_height / 2);

                agg::conv_transform<agg::path_storage> trans_gb_poly(gb_poly, mtx1);
                agg::conv_transform<agg::path_storage> trans_arrows(arrows, mtx2);

                m_ras.add_path(trans_gb_poly);
                ren.color(agg::rgba(0.5, 0.5, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                agg::conv_stroke<agg::conv_transform<agg::path_storage> > stroke_gb_poly(trans_gb_poly);
                stroke_gb_poly.width(0.1);
                m_ras.add_path(stroke_gb_poly);
                ren.color(agg::rgba(0, 0, 0));
                agg::render_scanlines(m_ras, m_sl, ren);

                m_ras.add_path(trans_arrows);
                ren.color(agg::rgba(0.0, 0.5, 0.5, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                generate_alpha_mask(trans_gb_poly);
                perform_rendering(trans_arrows);
            } break;

            case 3: {
                spiral sp(m_x, m_y, 10, 150, 30, 0.0);
                agg::conv_stroke<spiral> stroke(sp);
                stroke.width(15.0);

                agg::path_storage gb_poly;
                make_gb_poly(gb_poly);

                agg::trans_affine mtx;
                mtx *= agg::trans_affine_translation(-1150, -1150);
                mtx *= agg::trans_affine_scaling(2.0);

                agg::conv_transform<agg::path_storage> trans_gb_poly(gb_poly, mtx);

                m_ras.add_path(trans_gb_poly);
                ren.color(agg::rgba(0.5, 0.5, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                agg::conv_stroke<agg::conv_transform<agg::path_storage> > stroke_gb_poly(trans_gb_poly);
                stroke_gb_poly.width(0.1);
                m_ras.add_path(stroke_gb_poly);
                ren.color(agg::rgba(0, 0, 0));
                agg::render_scanlines(m_ras, m_sl, ren);

                m_ras.add_path(stroke);
                ren.color(agg::rgba(0.0, 0.5, 0.5, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                generate_alpha_mask(trans_gb_poly);
                perform_rendering(stroke);
            } break;

            case 4: {
                spiral sp(m_x, m_y, 10, 150, 30, 0.0);
                agg::conv_stroke<spiral> stroke(sp);
                stroke.width(15.0);

                agg::path_storage glyph;
                glyph.move_to(28.47, 6.45);
                glyph.curve3(21.58, 1.12, 19.82, 0.29);
                glyph.curve3(17.19, -0.93, 14.21, -0.93);
                glyph.curve3(9.57, -0.93, 6.57, 2.25);
                glyph.curve3(3.56, 5.42, 3.56, 10.60);
                glyph.curve3(3.56, 13.87, 5.03, 16.26);
                glyph.curve3(7.03, 19.58, 11.99, 22.51);
                glyph.curve3(16.94, 25.44, 28.47, 29.64);
                glyph.line_to(28.47, 31.40);
                glyph.curve3(28.47, 38.09, 26.34, 40.58);
                glyph.curve3(24.22, 43.07, 20.17, 43.07);
                glyph.curve3(17.09, 43.07, 15.28, 41.41);
                glyph.curve3(13.43, 39.75, 13.43, 37.60);
                glyph.line_to(13.53, 34.77);
                glyph.curve3(13.53, 32.52, 12.38, 31.30);
                glyph.curve3(11.23, 30.08, 9.38, 30.08);
                glyph.curve3(7.57, 30.08, 6.42, 31.35);
                glyph.curve3(5.27, 32.62, 5.27, 34.81);
                glyph.curve3(5.27, 39.01, 9.57, 42.53);
                glyph.curve3(13.87, 46.04, 21.63, 46.04);
                glyph.curve3(27.59, 46.04, 31.40, 44.04);
                glyph.curve3(34.28, 42.53, 35.64, 39.31);
                glyph.curve3(36.52, 37.21, 36.52, 30.71);
                glyph.line_to(36.52, 15.53);
                glyph.curve3(36.52, 9.13, 36.77, 7.69);
                glyph.curve3(37.01, 6.25, 37.57, 5.76);
                glyph.curve3(38.13, 5.27, 38.87, 5.27);
                glyph.curve3(39.65, 5.27, 40.23, 5.62);
                glyph.curve3(41.26, 6.25, 44.19, 9.18);
                glyph.line_to(44.19, 6.45);
                glyph.curve3(38.72, -0.88, 33.74, -0.88);
                glyph.curve3(31.35, -0.88, 29.93, 0.78);
                glyph.curve3(28.52, 2.44, 28.47, 6.45);
                glyph.close_polygon();

                glyph.move_to(28.47, 9.62);
                glyph.line_to(28.47, 26.66);
                glyph.curve3(21.09, 23.73, 18.95, 22.51);
                glyph.curve3(15.09, 20.36, 13.43, 18.02);
                glyph.curve3(11.77, 15.67, 11.77, 12.89);
                glyph.curve3(11.77, 9.38, 13.87, 7.06);
                glyph.curve3(15.97, 4.74, 18.70, 4.74);
                glyph.curve3(22.41, 4.74, 28.47, 9.62);
                glyph.close_polygon();

                agg::trans_affine mtx;
                mtx *= agg::trans_affine_scaling(4.0);
                mtx *= agg::trans_affine_translation(220, 200);
                agg::conv_transform<agg::path_storage> trans(glyph, mtx);
                agg::conv_curve<agg::conv_transform<agg::path_storage> > curve(trans);

                m_ras.reset();
                m_ras.add_path(stroke);
                ren.color(agg::rgba(0, 0, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                m_ras.reset();
                m_ras.add_path(curve);
                ren.color(agg::rgba(0, 0.6, 0, 0.1));
                agg::render_scanlines(m_ras, m_sl, ren);

                generate_alpha_mask(stroke);
                perform_rendering(curve);
            } break;
        }
    }
};

}  // namespace

int render_alpha_mask3(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    int polygons = params.size() > 0 ? (int)params[0] : 3;
    int operation = params.size() > 1 ? (int)params[1] : 0;
    double m_x = params.size() > 2 ? params[2] : w / 2.0;
    double m_y = params.size() > 3 ? params[3] : h / 2.0;

    // flip_y = true, so every control gets !flip_y = false.
    agg::rbox_ctrl<color_type> m_polygons(5.0, 5.0, 5.0 + 205.0, 110.0, false);
    agg::rbox_ctrl<color_type> m_operation(555.0, 5.0, 555.0 + 80.0, 55.0, false);
    m_operation.add_item("AND");
    m_operation.add_item("SUB");
    m_operation.cur_item(operation);
    m_polygons.add_item("Two Simple Paths");
    m_polygons.add_item("Closed Stroke");
    m_polygons.add_item("Great Britain and Arrows");
    m_polygons.add_item("Great Britain and Spiral");
    m_polygons.add_item("Spiral and Glyph");
    m_polygons.cur_item(polygons);

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    agg::renderer_base<pixfmt> ren_base(pf);
    ren_base.clear(agg::rgba(1, 1, 1));

    app a(cv.rbuf, operation, m_x, m_y);
    a.render(polygons);

    agg::render_ctrl(a.m_ras, a.m_sl, ren_base, m_polygons);
    agg::render_ctrl(a.m_ras, a.m_sl, ren_base, m_operation);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
