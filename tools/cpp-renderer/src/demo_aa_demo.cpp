// aa_demo.cpp headless reproduction (default state: triangle (57,100),
// (369,170), (143,310), pixel size 32; 600x400 bgr24). renderer_enlarged and
// square are copied from the example, plus the ++span it omits (harmless
// there: every row of a triangle is one scanline_u8 span).
//
// Params (all optional): x0, y0, x1, y1, x2, y2, pixel_size.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_path_storage.h"
#include "agg_conv_stroke.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

namespace {

class square {
public:
    square(double size) : m_size(size) {}

    template<class Rasterizer, class Scanline, class Renderer, class ColorT>
    void draw(Rasterizer& ras, Scanline& sl, Renderer& ren, ColorT color,
              double x, double y) {
        ras.reset();
        ras.move_to_d(x*m_size,        y*m_size);
        ras.line_to_d(x*m_size+m_size, y*m_size);
        ras.line_to_d(x*m_size+m_size, y*m_size+m_size);
        ras.line_to_d(x*m_size,        y*m_size+m_size);
        agg::render_scanlines_aa_solid(ras, sl, ren, color);
    }

private:
    double m_size;
};

template<class Renderer> class renderer_enlarged {
public:
    renderer_enlarged(Renderer& ren, double size) :
        m_ren(ren), m_square(size), m_size(size) {}

    void color(agg::srgba8 c) { m_color = c; }
    void prepare() {}

    template<class Scanline> void render(const Scanline& sl) {
        int y = sl.y();
        unsigned num_spans = sl.num_spans();
        typename Scanline::const_iterator span = sl.begin();
        do {
            int x = span->x;
            const typename Scanline::cover_type* covers = span->covers;
            int num_pix = span->len;
            do {
                int a = (*covers++ * m_color.a) >> 8;
                m_square.draw(m_ras, m_sl, m_ren,
                              agg::srgba8(m_color.r, m_color.g, m_color.b, a),
                              x, y);
                ++x;
            } while(--num_pix);
            ++span;
        } while(--num_spans);
    }

private:
    agg::rasterizer_scanline_aa<> m_ras;
    agg::scanline_u8 m_sl;
    Renderer&   m_ren;
    square      m_square;
    agg::srgba8 m_color;
    double      m_size;
};

} // namespace

int render_aa_demo(unsigned w, unsigned h,
                   const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::rgba8 color_type;
    typedef agg::renderer_base<pixfmt> ren_base;

    double m_x[3] = { params.size() > 0 ? params[0] : 57.0,
                      params.size() > 2 ? params[2] : 369.0,
                      params.size() > 4 ? params[4] : 143.0 };
    double m_y[3] = { params.size() > 1 ? params[1] : 100.0,
                      params.size() > 3 ? params[3] : 170.0,
                      params.size() > 5 ? params[5] : 310.0 };
    double pixel_size = params.size() > 6 ? params[6] : 32.0;

    // flip_y = true in the demo => the slider gets !flip_y = false.
    agg::slider_ctrl<color_type> m_slider1(80, 10, 600-10, 19, false);
    m_slider1.range(8.0, 100.0);
    m_slider1.num_steps(23);
    m_slider1.value(pixel_size);
    m_slider1.label("Pixel size=%1.0f");
    m_slider1.no_transform();

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    ren_base ren(pixf);
    agg::scanline_u8 sl;

    ren.clear(agg::rgba(1,1,1));

    agg::rasterizer_scanline_aa<> ras;

    int size_mul = int(m_slider1.value());

    renderer_enlarged<ren_base> ren_en(ren, size_mul);

    ras.reset();
    ras.move_to_d(m_x[0]/size_mul, m_y[0]/size_mul);
    ras.line_to_d(m_x[1]/size_mul, m_y[1]/size_mul);
    ras.line_to_d(m_x[2]/size_mul, m_y[2]/size_mul);
    ren_en.color(agg::srgba8(0,0,0, 255));
    agg::render_scanlines(ras, sl, ren_en);

    // The rasterizer still holds the small triangle, so this draws it again at
    // its natural size in the corner.
    agg::render_scanlines_aa_solid(ras, sl, ren, agg::srgba8(0,0,0));

    agg::path_storage ps;
    agg::conv_stroke<agg::path_storage> pg(ps);
    pg.width(2.0);

    for (int i = 0; i < 3; i++) {
        int j = (i + 1) % 3;
        ps.remove_all();
        ps.move_to(m_x[i], m_y[i]);
        ps.line_to(m_x[j], m_y[j]);
        ras.add_path(pg);
        agg::render_scanlines_aa_solid(ras, sl, ren, agg::srgba8(0,150,160, 200));
    }

    agg::render_ctrl(ras, sl, ren, m_slider1);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
