// scanline_boolean2.cpp headless reproduction (default state: Great Britain and
// Spiral, Non Zero, scanline_u, AND, the spiral in the middle of a 655x520 bgr24
// window cleared to white). Left out: the "Combine=%.3fms" and "Render=%.3fms"
// timings, which differ every frame, and the ten-fold combine loop that times
// them; their text lines stay blank so num_spans sits where the example puts it.
// trans_affine_resizing() is identity at the initial size and is dropped.
//
// Params (all optional): polygons, fill_rule, scanline_type, operation (the
// four rbox items), x, y (m_x, m_y: where the drag put the moving shape).
#include <cmath>
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_scanline_p.h"
#include "agg_scanline_bin.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_curve.h"
#include "agg_conv_stroke.h"
#include "agg_conv_transform.h"
#include "agg_gsv_text.h"
#include "agg_path_storage.h"
#include "agg_scanline_boolean_algebra.h"
#include "agg_scanline_storage_aa.h"
#include "agg_scanline_storage_bin.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

void make_gb_poly(agg::path_storage& ps);
void make_arrows(agg::path_storage& ps);

namespace {

typedef agg::pixfmt_bgr24 pixfmt;
typedef agg::rgba8 color_type;
typedef agg::renderer_base<pixfmt> renderer_base;

const unsigned initial_width = 655;
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

template<class Rasterizer, class Scanline>
unsigned count_spans(Rasterizer& ras, Scanline& sl) {
    unsigned n = 0;
    if (ras.rewind_scanlines()) {
        sl.reset(ras.min_x(), ras.max_x());
        while (ras.sweep_scanline(sl)) n += sl.num_spans();
    }
    return n;
}

struct app {
    renderer_base& rb;
    int polygons, scanline_type, operation;
    double m_x, m_y;

    template<class Rasterizer>
    void render_scanline_boolean(Rasterizer& ras1, Rasterizer& ras2) {
        if (operation <= 0) return;
        agg::sbool_op_e op = agg::sbool_or;
        switch (operation) {
            case 1: op = agg::sbool_or; break;
            case 2: op = agg::sbool_and; break;
            case 3: op = agg::sbool_xor; break;
            case 4: op = agg::sbool_xor_saddle; break;
            case 5: op = agg::sbool_a_minus_b; break;
            case 6: op = agg::sbool_b_minus_a; break;
        }

        unsigned num_spans = 0;
        switch (scanline_type) {
            case 0: {
                typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;
                renderer_solid ren(rb);
                agg::scanline_p8 sl, sl1, sl2;
                agg::scanline_storage_aa8 storage, storage1, storage2;
                agg::render_scanlines(ras1, sl, storage1);
                agg::render_scanlines(ras2, sl, storage2);
                agg::sbool_combine_shapes_aa(op, storage1, storage2, sl1, sl2, sl, storage);
                ren.color(agg::rgba(0.5, 0.0, 0, 0.5));
                agg::render_scanlines(storage, sl, ren);
                num_spans = count_spans(storage, sl);
            } break;
            case 1: {
                typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;
                renderer_solid ren(rb);
                agg::scanline_u8 sl, sl1, sl2;
                agg::scanline_storage_aa8 storage, storage1, storage2;
                agg::render_scanlines(ras1, sl, storage1);
                agg::render_scanlines(ras2, sl, storage2);
                agg::sbool_combine_shapes_aa(op, storage1, storage2, sl1, sl2, sl, storage);
                ren.color(agg::rgba(0.5, 0.0, 0, 0.5));
                agg::render_scanlines(storage, sl, ren);
                num_spans = count_spans(storage, sl);
            } break;
            case 2: {
                typedef agg::renderer_scanline_bin_solid<renderer_base> renderer_solid;
                renderer_solid ren(rb);
                agg::scanline_bin sl, sl1, sl2;
                agg::scanline_storage_bin storage, storage1, storage2;
                agg::render_scanlines(ras1, sl, storage1);
                agg::render_scanlines(ras2, sl, storage2);
                agg::sbool_combine_shapes_bin(op, storage1, storage2, sl1, sl2, sl, storage);
                ren.color(agg::rgba(0.5, 0.0, 0, 0.5));
                agg::render_scanlines(storage, sl, ren);
                num_spans = count_spans(storage, sl);
            } break;
        }

        char buf[100];
        sprintf(buf, "\n\n\n\nnum_spans=%d", num_spans);
        agg::renderer_scanline_aa_solid<renderer_base> ren(rb);
        agg::scanline_p8 sl;
        agg::gsv_text txt;
        agg::conv_stroke<agg::gsv_text> txt_stroke(txt);
        txt_stroke.width(1.0);
        txt_stroke.line_cap(agg::round_cap);
        txt.size(8.0);
        txt.start_point(420, 40);
        txt.text(buf);
        ras1.add_path(txt_stroke);
        ren.color(agg::rgba(0.0, 0.0, 0.0));
        agg::render_scanlines(ras1, sl, ren);
    }

    template<class Rasterizer>
    void render_sbool(Rasterizer& ras1, Rasterizer& ras2, int fill_rule) {
        agg::renderer_scanline_aa_solid<renderer_base> ren(rb);
        agg::scanline_p8 sl;

        ras1.filling_rule(fill_rule ? agg::fill_non_zero : agg::fill_even_odd);
        ras2.filling_rule(fill_rule ? agg::fill_non_zero : agg::fill_even_odd);

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
                ps1.line_to(x + 325, y + 261);
                ps1.line_to(x + 268, y + 309);
                ps1.move_to(x + 259, y + 259);
                ps1.line_to(x + 273, y + 288);
                ps1.line_to(x + 298, y + 266);

                ps2.move_to(100 + 32, 100 + 77);
                ps2.line_to(100 + 473, 100 + 263);
                ps2.line_to(100 + 351, 100 + 290);
                ps2.line_to(100 + 354, 100 + 374);

                ras1.reset();
                ras1.add_path(ps1);
                ren.color(agg::rgba(0, 0, 0, 0.1));
                agg::render_scanlines(ras1, sl, ren);

                ras2.reset();
                ras2.add_path(ps2);
                ren.color(agg::rgba(0, 0.6, 0, 0.1));
                agg::render_scanlines(ras2, sl, ren);

                render_scanline_boolean(ras1, ras2);
            } break;

            case 1: {
                agg::path_storage ps1;
                agg::path_storage ps2;
                agg::conv_stroke<agg::path_storage> stroke(ps2);
                stroke.width(15.0);
                double x = m_x - initial_width / 2 + 100;
                double y = m_y - initial_height / 2 + 100;
                ps1.move_to(x + 140, y + 145);
                ps1.line_to(x + 225, y + 44);
                ps1.line_to(x + 296, y + 219);
                ps1.close_polygon();
                ps1.line_to(x + 226, y + 289);
                ps1.line_to(x + 82, y + 292);
                ps1.move_to(x + 220 - 50, y + 222);
                ps1.line_to(x + 363 - 50, y + 249);
                ps1.line_to(x + 265 - 50, y + 331);
                ps1.close_polygon();

                ps2.move_to(100 + 32, 100 + 77);
                ps2.line_to(100 + 473, 100 + 263);
                ps2.line_to(100 + 351, 100 + 290);
                ps2.line_to(100 + 354, 100 + 374);
                ps2.close_polygon();

                ras1.reset();
                ras1.add_path(ps1);
                ren.color(agg::rgba(0, 0, 0, 0.1));
                agg::render_scanlines(ras1, sl, ren);

                ras2.reset();
                ras2.add_path(stroke);
                ren.color(agg::rgba(0, 0.6, 0, 0.1));
                agg::render_scanlines(ras2, sl, ren);

                render_scanline_boolean(ras1, ras2);
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

                ras2.add_path(trans_gb_poly);
                ren.color(agg::rgba(0.5, 0.5, 0, 0.1));
                agg::render_scanlines(ras2, sl, ren);

                agg::conv_stroke<agg::conv_transform<agg::path_storage> > stroke_gb_poly(trans_gb_poly);
                stroke_gb_poly.width(0.1);
                ras1.add_path(stroke_gb_poly);
                ren.color(agg::rgba(0, 0, 0));
                agg::render_scanlines(ras1, sl, ren);

                ras2.add_path(trans_arrows);
                ren.color(agg::rgba(0.0, 0.5, 0.5, 0.1));
                agg::render_scanlines(ras2, sl, ren);

                ras1.reset();
                ras1.add_path(trans_gb_poly);

                render_scanline_boolean(ras1, ras2);
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

                ras1.add_path(trans_gb_poly);
                ren.color(agg::rgba(0.5, 0.5, 0, 0.1));
                agg::render_scanlines(ras1, sl, ren);

                agg::conv_stroke<agg::conv_transform<agg::path_storage> > stroke_gb_poly(trans_gb_poly);
                stroke_gb_poly.width(0.1);
                ras1.reset();
                ras1.add_path(stroke_gb_poly);
                ren.color(agg::rgba(0, 0, 0));
                agg::render_scanlines(ras1, sl, ren);

                ras2.reset();
                ras2.add_path(stroke);
                ren.color(agg::rgba(0.0, 0.5, 0.5, 0.1));
                agg::render_scanlines(ras2, sl, ren);

                ras1.reset();
                ras1.add_path(trans_gb_poly);
                render_scanline_boolean(ras1, ras2);
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

                ras1.reset();
                ras1.add_path(stroke);
                ren.color(agg::rgba(0, 0, 0, 0.1));
                agg::render_scanlines(ras1, sl, ren);

                ras2.reset();
                ras2.add_path(curve);
                ren.color(agg::rgba(0, 0.6, 0, 0.1));
                agg::render_scanlines(ras2, sl, ren);

                render_scanline_boolean(ras1, ras2);
            } break;
        }
    }
};

}  // namespace

int render_scanline_boolean2(unsigned w, unsigned h,
                             const std::vector<double>& params, const char* out) {
    int polygons = params.size() > 0 ? (int)params[0] : 3;
    int fill_rule = params.size() > 1 ? (int)params[1] : 1;
    int scanline_type = params.size() > 2 ? (int)params[2] : 1;
    int operation = params.size() > 3 ? (int)params[3] : 2;
    double m_x = params.size() > 4 ? params[4] : w / 2.0;
    double m_y = params.size() > 5 ? params[5] : h / 2.0;

    // flip_y = true, so every control gets !flip_y = false.
    agg::rbox_ctrl<color_type> m_polygons(5.0, 5.0, 5.0 + 205.0, 110.0, false);
    agg::rbox_ctrl<color_type> m_fill_rule(200, 5.0, 200 + 105.0, 50.0, false);
    agg::rbox_ctrl<color_type> m_scanline_type(300, 5.0, 300 + 115.0, 70.0, false);
    agg::rbox_ctrl<color_type> m_operation(535.0, 5.0, 535.0 + 115.0, 145.0, false);
    m_operation.add_item("None");
    m_operation.add_item("OR");
    m_operation.add_item("AND");
    m_operation.add_item("XOR Linear");
    m_operation.add_item("XOR Saddle");
    m_operation.add_item("A-B");
    m_operation.add_item("B-A");
    m_operation.cur_item(operation);
    m_fill_rule.add_item("Even-Odd");
    m_fill_rule.add_item("Non Zero");
    m_fill_rule.cur_item(fill_rule);
    m_scanline_type.add_item("scanline_p");
    m_scanline_type.add_item("scanline_u");
    m_scanline_type.add_item("scanline_bin");
    m_scanline_type.cur_item(scanline_type);
    m_polygons.add_item("Two Simple Paths");
    m_polygons.add_item("Closed Stroke");
    m_polygons.add_item("Great Britain and Arrows");
    m_polygons.add_item("Great Britain and Spiral");
    m_polygons.add_item("Spiral and Glyph");
    m_polygons.cur_item(polygons);

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    renderer_base ren_base(pf);
    ren_base.clear(agg::rgba(1, 1, 1));

    agg::scanline_u8 sl;
    agg::rasterizer_scanline_aa<> ras;
    agg::rasterizer_scanline_aa<> ras2;

    agg::render_ctrl(ras, sl, ren_base, m_polygons);
    agg::render_ctrl(ras, sl, ren_base, m_fill_rule);
    agg::render_ctrl(ras, sl, ren_base, m_scanline_type);
    agg::render_ctrl(ras, sl, ren_base, m_operation);

    app a = {ren_base, polygons, scanline_type, operation, m_x, m_y};
    a.render_sbool(ras, ras2, fill_rule);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
