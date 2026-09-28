// aa_test.cpp headless reproduction: its on_draw() frame (default window 480x350; the window is
// resizable, and the radial lines and the triangles follow width() and height()). aa_test has no ctrls
// and no params. Left out: the mouse-press "stress test" (random points, lines and Gouraud triangles,
// timed), which is a benchmark, not a frame.
//
// aa_test.cpp runs with flip_y = false: it draws y down, row 0 at the top of its window. The rows are
// written in their natural order; the port (AaTestDemo, which DrawsYDown) renders its reference frame y
// down too.
//
// The gradient color array is srgba8 while the pixel format is linear rgba8, so C++ converts silently
// twice: fill_color_array's rgba ends go to srgba8 through the float sRGB table, the 256 entries are
// interpolated in sRGB, and span_gradient's output goes back to rgba8 through the 8-bit table.
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_conv_dash.h"
#include "agg_conv_stroke.h"
#include "agg_ellipse.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "agg_span_allocator.h"

#include "common.h"

namespace {

typedef agg::pixfmt_bgr24 pixfmt;
typedef agg::rgba8 color_type;
typedef agg::renderer_base<pixfmt> renderer_base_type;
typedef agg::renderer_scanline_aa_solid<renderer_base_type> renderer_scanline_type;
typedef agg::scanline_u8 scanline_type;
typedef agg::rasterizer_scanline_aa<> rasterizer_type;

class simple_vertex_source
{
public:
    simple_vertex_source() : m_num_vertices(0), m_count(0) { m_cmd[0] = agg::path_cmd_stop; }

    void init(double x1, double y1, double x2, double y2)
    {
        m_num_vertices = 2;
        m_count = 0;
        m_x[0] = x1;
        m_y[0] = y1;
        m_x[1] = x2;
        m_y[1] = y2;
        m_cmd[0] = agg::path_cmd_move_to;
        m_cmd[1] = agg::path_cmd_line_to;
        m_cmd[2] = agg::path_cmd_stop;
    }

    void rewind(unsigned) { m_count = 0; }

    unsigned vertex(double* x, double* y)
    {
        *x = m_x[m_count];
        *y = m_y[m_count];
        return m_cmd[m_count++];
    }

private:
    unsigned m_num_vertices;
    unsigned m_count;
    double m_x[8];
    double m_y[8];
    unsigned m_cmd[8];
};

template<class Ras, class Ren, class Scanline> class dashed_line
{
public:
    dashed_line(Ras& ras, Ren& ren, Scanline& sl) :
        m_ras(ras), m_ren(ren), m_sl(sl), m_src(), m_dash(m_src), m_stroke(m_src), m_dash_stroke(m_dash)
    {}

    void draw(double x1, double y1, double x2, double y2, double line_width, double dash_length)
    {
        m_src.init(x1 + 0.5, y1 + 0.5, x2 + 0.5, y2 + 0.5);
        m_ras.reset();
        if(dash_length > 0.0)
        {
            m_dash.remove_all_dashes();
            m_dash.add_dash(dash_length, dash_length);
            m_dash_stroke.width(line_width);
            m_dash_stroke.line_cap(agg::round_cap);
            m_ras.add_path(m_dash_stroke);
        }
        else
        {
            m_stroke.width(line_width);
            m_stroke.line_cap(agg::round_cap);
            m_ras.add_path(m_stroke);
        }
        agg::render_scanlines(m_ras, m_sl, m_ren);
    }

private:
    Ras& m_ras;
    Ren& m_ren;
    Scanline& m_sl;
    simple_vertex_source m_src;
    agg::conv_dash<simple_vertex_source> m_dash;
    agg::conv_stroke<simple_vertex_source> m_stroke;
    agg::conv_stroke<agg::conv_dash<simple_vertex_source> > m_dash_stroke;
};

void calc_linear_gradient_transform(double x1, double y1, double x2, double y2,
                                    agg::trans_affine& mtx, double gradient_d2 = 100.0)
{
    double dx = x2 - x1;
    double dy = y2 - y1;
    mtx.reset();
    mtx *= agg::trans_affine_scaling(sqrt(dx * dx + dy * dy) / gradient_d2);
    mtx *= agg::trans_affine_rotation(atan2(dy, dx));
    mtx *= agg::trans_affine_translation(x1 + 0.5, y1 + 0.5);
    mtx.invert();
}

template<class ColorArrayT>
void fill_color_array(ColorArrayT& array, typename ColorArrayT::value_type begin, typename ColorArrayT::value_type end)
{
    for(unsigned i = 0; i < 256; ++i)
    {
        array[i] = begin.gradient(end, i / 255.0);
    }
}

template<class T> T min_of(T a, T b) { return (a < b) ? a : b; }

} // namespace

int render_aa_test(unsigned w, unsigned h, const std::vector<double>& params, const char* out) {
    (void)params;
    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base_type ren_base(pixf);
    renderer_scanline_type ren_sl(ren_base);
    scanline_type sl;
    rasterizer_type ras;

    ren_base.clear(agg::rgba(0,0,0));

    int i;
    double width = w;
    double height = h;

    // radial line test
    dashed_line<rasterizer_type, renderer_scanline_type, scanline_type> dash(ras, ren_sl, sl);

    double cx = width / 2.0;
    double cy = height / 2.0;

    ren_sl.color(agg::rgba(1.0, 1.0, 1.0, 0.2));
    for(i = 180; i > 0; i--)
    {
        double n = 2.0 * agg::pi * i / 180.0;
        dash.draw(cx + min_of(cx, cy) * sin(n), cy + min_of(cx, cy) * cos(n),
                  cx, cy,
                  1.0, (i < 90) ? i : 0.0);
    }

    typedef agg::gradient_x gradient_func_type;
    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_allocator<color_type> span_allocator_type;
    typedef agg::pod_auto_array<agg::srgba8, 256> color_array_type;
    typedef agg::span_gradient<color_type, interpolator_type, gradient_func_type, color_array_type> span_gradient_type;
    typedef agg::renderer_scanline_aa<renderer_base_type, span_allocator_type, span_gradient_type> renderer_gradient_type;

    gradient_func_type gradient_func;
    agg::trans_affine gradient_mtx;
    interpolator_type span_interpolator(gradient_mtx);
    span_allocator_type span_allocator;
    color_array_type gradient_colors;
    span_gradient_type span_gradient(span_interpolator, gradient_func, gradient_colors, 0, 100);

    renderer_gradient_type ren_gradient(ren_base, span_allocator, span_gradient);

    dashed_line<rasterizer_type, renderer_gradient_type, scanline_type> dash_gradient(ras, ren_gradient, sl);

    double x1, y1, x2, y2;

    for(i = 1; i <= 20; i++)
    {
        ren_sl.color(agg::rgba(1,1,1));

        // integral point sizes 1..20
        agg::ellipse ell;

        ell.init(20 + i * (i + 1) + 0.5, 20.5, i / 2.0, i / 2.0, 8 + i);
        ras.reset();
        ras.add_path(ell);
        agg::render_scanlines(ras, sl, ren_sl);

        // fractional point sizes 0..2
        ell.init(18 + i * 4 + 0.5, 33 + 0.5, i/20.0, i/20.0, 8);
        ras.reset();
        ras.add_path(ell);
        agg::render_scanlines(ras, sl, ren_sl);

        // fractional point positioning
        ell.init(18 + i * 4 + (i-1) / 10.0 + 0.5, 27 + (i - 1) / 10.0 + 0.5, 0.5, 0.5, 8);
        ras.reset();
        ras.add_path(ell);
        agg::render_scanlines(ras, sl, ren_sl);

        // integral line widths 1..20
        fill_color_array(gradient_colors, agg::rgba(1,1,1), agg::rgba(i % 2, (i % 3) * 0.5, (i % 5) * 0.25));

        x1 = 20 + i* (i + 1);
        y1 = 40.5;
        x2 = 20 + i * (i + 1) + (i - 1) * 4;
        y2 = 100.5;
        calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
        dash_gradient.draw(x1, y1, x2, y2, i, 0);

        fill_color_array(gradient_colors, agg::rgba(1,0,0), agg::rgba(0,0,1));

        // fractional line lengths H (red/blue)
        x1 = 17.5 + i * 4;
        y1 = 107;
        x2 = 17.5 + i * 4 + i/6.66666667;
        y2 = 107;
        calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
        dash_gradient.draw(x1, y1, x2, y2, 1.0, 0);

        // fractional line lengths V (red/blue)
        x1 = 18 + i * 4;
        y1 = 112.5;
        x2 = 18 + i * 4;
        y2 = 112.5 + i / 6.66666667;
        calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
        dash_gradient.draw(x1, y1, x2, y2, 1.0, 0);

        // fractional line positioning (red)
        fill_color_array(gradient_colors, agg::rgba(1,0,0), agg::rgba(1,1,1));
        x1 = 21.5;
        y1 = 120 + (i - 1) * 3.1;
        x2 = 52.5;
        y2 = 120 + (i - 1) * 3.1;
        calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
        dash_gradient.draw(x1, y1, x2, y2, 1.0, 0);

        // fractional line width 2..0 (green)
        fill_color_array(gradient_colors, agg::rgba(0,1,0), agg::rgba(1,1,1));
        x1 = 52.5;
        y1 = 118 + i * 3;
        x2 = 83.5;
        y2 = 118 + i * 3;
        calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
        dash_gradient.draw(x1, y1, x2, y2, 2.0 - (i - 1) / 10.0, 0);

        // stippled fractional width 2..0 (blue)
        fill_color_array(gradient_colors, agg::rgba(0,0,1), agg::rgba(1,1,1));
        x1 = 83.5;
        y1 = 119 + i * 3;
        x2 = 114.5;
        y2 = 119 + i * 3;
        calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
        dash_gradient.draw(x1, y1, x2, y2, 2.0 - (i - 1) / 10.0, 3.0);

        ren_sl.color(agg::rgba(1,1,1));
        if(i <= 10)
        {
            // integral line width, horz aligned (mipmap test)
            dash.draw(125.5, 119.5 + (i + 2) * (i / 2.0),
                      135.5, 119.5 + (i + 2) * (i / 2.0),
                      i, 0.0);
        }

        // fractional line width 0..2, 1 px H
        dash.draw(17.5 + i * 4, 192, 18.5 + i * 4, 192, i / 10.0, 0);

        // fractional line positioning, 1 px H
        dash.draw(17.5 + i * 4 + (i - 1) / 10.0, 186,
                  18.5 + i * 4 + (i - 1) / 10.0, 186,
                  1.0, 0);
    }

    // Triangles
    for (i = 1; i <= 13; i++)
    {
        fill_color_array(gradient_colors, agg::rgba(1,1,1), agg::rgba(i % 2, (i % 3) * 0.5, (i % 5) * 0.25));
        calc_linear_gradient_transform(width  - 150, height - 20 - i * (i + 1.5),
                                       width  - 20,  height - 20 - i * (i + 1),
                                       gradient_mtx);
        ras.reset();
        ras.move_to_d(width - 150, height - 20 - i * (i + 1.5));
        ras.line_to_d(width - 20,  height - 20 - i * (i + 1));
        ras.line_to_d(width - 20,  height - 20 - i * (i + 2));
        agg::render_scanlines(ras, sl, ren_gradient);
    }

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
