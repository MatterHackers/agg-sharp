// image_fltr_graph.cpp headless reproduction: the example's on_draw() at the initial 780x300 window, where
// trans_affine_resizing() is identity. For each checked filter it draws the filter's weight function (dark
// red), the sum of its weights over every whole-pixel offset (dark green) and the normalized integer lookup
// table image_filter_lut builds from it (dark blue), over a 16-column grid.
// Params (all optional): radius (4, used by sinc, lanczos and blackman), checked filters as a bit mask in
// cbox order, bit 0 = bilinear ... bit 15 = blackman (0: none, the example's start-up state).
#include <cmath>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_conv_stroke.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_path_storage.h"
#include "agg_image_filters.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"

namespace {

struct filter_base {
    virtual ~filter_base() {}
    virtual double radius() const = 0;
    virtual void set_radius(double r) = 0;
    virtual double calc_weight(double x) const = 0;
};

template <class Filter> struct image_filter_const_radius_adaptor : filter_base {
    virtual double radius() const { return m_filter.radius(); }
    virtual void set_radius(double) {}
    virtual double calc_weight(double x) const { return m_filter.calc_weight(fabs(x)); }
    Filter m_filter;
};

template <class Filter> struct image_filter_variable_radius_adaptor : filter_base {
    virtual double radius() const { return m_filter.radius(); }
    virtual double calc_weight(double x) const { return m_filter.calc_weight(fabs(x)); }
    virtual void set_radius(double r) { m_filter = Filter(r); }
    image_filter_variable_radius_adaptor() : m_filter(2.0) {}
    Filter m_filter;
};

}  // namespace

int render_image_fltr_graph(unsigned w, unsigned h,
                            const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double radius_value = params.size() > 0 ? params[0] : 4.0;
    unsigned checked = params.size() > 1 ? (unsigned)params[1] : 0;

    agg::slider_ctrl<agg::rgba> m_radius(5.0, 5.0, 780 - 5, 10.0, false);
    const char* names[] = {"bilinear", "bicubic ", "spline16", "spline36", "hanning ", "hamming ",
                           "hermite ", "kaiser  ", "quadric ", "catrom  ", "gaussian", "bessel  ",
                           "mitchell", "sinc    ", "lanczos ", "blackman"};
    std::vector<agg::cbox_ctrl<agg::rgba>*> m_filters;
    for (unsigned i = 0; i < 16; i++) {
        m_filters.push_back(new agg::cbox_ctrl<agg::rgba>(8.0, 30.0 + 15 * i, names[i], false));
        m_filters[i]->status((checked >> i) & 1);
    }

    image_filter_const_radius_adaptor<agg::image_filter_bilinear> f_bilinear;
    image_filter_const_radius_adaptor<agg::image_filter_bicubic> f_bicubic;
    image_filter_const_radius_adaptor<agg::image_filter_spline16> f_spline16;
    image_filter_const_radius_adaptor<agg::image_filter_spline36> f_spline36;
    image_filter_const_radius_adaptor<agg::image_filter_hanning> f_hanning;
    image_filter_const_radius_adaptor<agg::image_filter_hamming> f_hamming;
    image_filter_const_radius_adaptor<agg::image_filter_hermite> f_hermite;
    image_filter_const_radius_adaptor<agg::image_filter_kaiser> f_kaiser;
    image_filter_const_radius_adaptor<agg::image_filter_quadric> f_quadric;
    image_filter_const_radius_adaptor<agg::image_filter_catrom> f_catrom;
    image_filter_const_radius_adaptor<agg::image_filter_gaussian> f_gaussian;
    image_filter_const_radius_adaptor<agg::image_filter_bessel> f_bessel;
    image_filter_const_radius_adaptor<agg::image_filter_mitchell> f_mitchell;
    image_filter_variable_radius_adaptor<agg::image_filter_sinc> f_sinc;
    image_filter_variable_radius_adaptor<agg::image_filter_lanczos> f_lanczos;
    image_filter_variable_radius_adaptor<agg::image_filter_blackman> f_blackman;
    filter_base* m_filter_func[] = {&f_bilinear, &f_bicubic, &f_spline16, &f_spline36, &f_hanning, &f_hamming,
                                    &f_hermite, &f_kaiser, &f_quadric, &f_catrom, &f_gaussian, &f_bessel,
                                    &f_mitchell, &f_sinc, &f_lanczos, &f_blackman};
    const unsigned m_num_filters = 16;

    m_radius.range(2.0, 8.0);
    m_radius.value(radius_value);
    m_radius.label("Radius=%.3f");

    // on_draw
    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid rs(rb);

    rb.clear(agg::rgba(1.0, 1.0, 1.0));
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;

    // initial_width() / initial_height(): the window agg_main opens.
    double x_start = 125.0;
    double x_end = 780 - 15.0;
    double y_start = 10.0;
    double y_end = 300 - 10.0;
    double x_center = (x_start + x_end) / 2;

    unsigned i;

    agg::path_storage p;
    agg::conv_stroke<agg::path_storage> pl(p);

    for (i = 0; i <= 16; i++) {
        double x = x_start + (x_end - x_start) * i / 16.0;
        p.remove_all();
        p.move_to(x + 0.5, y_start);
        p.line_to(x + 0.5, y_end);
        ras.add_path(pl);
        rs.color(agg::srgba8(0, 0, 0, i == 8 ? 255 : 100));
        agg::render_scanlines(ras, sl, rs);
    }

    double ys = y_start + (y_end - y_start) / 6.0;

    p.remove_all();
    p.move_to(x_start, ys);
    p.line_to(x_end, ys);
    ras.add_path(pl);
    rs.color(agg::srgba8(0, 0, 0));
    agg::render_scanlines(ras, sl, rs);

    pl.width(1.5);

    for (i = 0; i < m_num_filters; i++) {
        if (m_filters[i]->status()) {
            m_filter_func[i]->set_radius(m_radius.value());
            unsigned j;

            double radius = m_filter_func[i]->radius();
            unsigned n = unsigned(radius * 256 * 2);
            double dy = y_end - ys;

            double xs = (x_end + x_start) / 2.0 - (radius * (x_end - x_start) / 16.0);
            double dx = (x_end - x_start) * radius / 8.0;

            p.remove_all();
            p.move_to(xs + 0.5, ys + dy * m_filter_func[i]->calc_weight(-radius));
            for (j = 1; j < n; j++) {
                p.line_to(xs + dx * j / n + 0.5, ys + dy * m_filter_func[i]->calc_weight(j / 256.0 - radius));
            }
            ras.add_path(pl);
            rs.color(agg::rgba(0.5, 0, 0));
            agg::render_scanlines(ras, sl, rs);

            p.remove_all();
            unsigned xint;
            int ir = int(ceil(radius) + 0.1);

            for (xint = 0; xint < 256; xint++) {
                int xfract;
                double sum = 0;
                for (xfract = -ir; xfract < ir; xfract++) {
                    double xf = xint / 256.0 + xfract;
                    if (xf >= -radius || xf <= radius) {
                        sum += m_filter_func[i]->calc_weight(xf);
                    }
                }

                double x = x_center + ((-128.0 + xint) / 128.0) * radius * (x_end - x_start) / 16.0;
                double y = ys + sum * 256 - 256;

                if (xint == 0) p.move_to(x, y);
                else p.line_to(x, y);
            }
            ras.add_path(pl);
            rs.color(agg::rgba(0, 0.5, 0));
            agg::render_scanlines(ras, sl, rs);

            agg::image_filter_lut normalized(*m_filter_func[i]);
            const agg::int16* weights = normalized.weight_array();

            xs = (x_end + x_start) / 2.0 - (normalized.diameter() * (x_end - x_start) / 32.0);
            unsigned nn = normalized.diameter() * 256;
            p.remove_all();
            p.move_to(xs + 0.5, ys + dy * weights[0] / agg::image_filter_scale);
            for (j = 1; j < nn; j++) {
                p.line_to(xs + dx * j / n + 0.5, ys + dy * weights[j] / agg::image_filter_scale);
            }
            ras.add_path(pl);
            rs.color(agg::rgba(0, 0, 0.5));
            agg::render_scanlines(ras, sl, rs);
        }
    }

    for (i = 0; i < m_num_filters; i++) {
        agg::render_ctrl(ras, sl, rb, *m_filters[i]);
    }
    if (m_filters[13]->status() || m_filters[14]->status() || m_filters[15]->status()) {
        agg::render_ctrl(ras, sl, rb, m_radius);
    }

    bool ok = headless::write_raw(out, pixf, w, h);
    for (auto* c : m_filters) delete c;
    return ok ? 0 : 1;
}
