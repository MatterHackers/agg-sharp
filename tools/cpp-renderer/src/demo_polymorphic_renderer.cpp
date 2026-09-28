// polymorphic_renderer.cpp headless reproduction (default state: 400x330, pix_format_rgb555).
// Params (all optional): format (the rbox item, default 0):
//   0 rgb555, 1 rgb565, 2 rgb24, 3 bgr24, 4 rgba32, 5 argb32, 6 abgr32, 7 bgra32.
//
// polymorphic_renderer.cpp picks its pixel format once, at startup (the static pix_fmt); agg-sharp's port
// adds an rbox listing the example's eight formats so the user can switch, and this reproduction draws that
// rbox too (flip_y = true, so the ctrl gets !flip_y = false). The window buffer is the chosen format, so the
// triangle and the rbox are both rendered into it and quantised by it; the raw output is that buffer read
// back through pixf.pixel(), which is how a packed 16-bit pixel becomes RGBA (make_color: no bit
// replication, so white reads 248).
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_path_storage.h"
#include "agg_renderer_scanline.h"
#include "agg_pixfmt_rgb.h"
#include "agg_pixfmt_rgb_packed.h"
#include "agg_pixfmt_rgba.h"
#include "ctrl/agg_rbox_ctrl.h"

#include "common.h"

namespace {

// polymorphic_renderer.cpp's classes, unchanged.
class polymorphic_renderer_solid_rgba8_base
{
public:
    typedef agg::srgba8      color_type;
    typedef agg::scanline_p8 scanline_type;

    virtual ~polymorphic_renderer_solid_rgba8_base() {}

    virtual void clear(const color_type& c) = 0;
    virtual void color(const color_type& c) = 0;
    virtual const color_type color() const = 0;
    virtual void prepare() = 0;
    virtual void render(const scanline_type&) = 0;
};

template<class PixFmt> class polymorphic_renderer_solid_rgba8_adaptor :
public polymorphic_renderer_solid_rgba8_base
{
public:
    polymorphic_renderer_solid_rgba8_adaptor(agg::rendering_buffer& rbuf) :
        m_pixfmt(rbuf),
        m_ren_base(m_pixfmt),
        m_ren(m_ren_base)
    {}

    virtual void clear(const color_type& c) { m_ren_base.clear(c); }
    virtual void color(const color_type& c) { m_ren.color(c); }
    virtual const color_type color() const { return m_ren.color(); }
    virtual void prepare() { m_ren.prepare(); }
    virtual void render(const scanline_type& sl) { m_ren.render(sl); }

private:
    PixFmt                                                         m_pixfmt;
    agg::renderer_base<PixFmt>                                     m_ren_base;
    agg::renderer_scanline_aa_solid<agg::renderer_base<PixFmt> > m_ren;
};

// on_draw for one pixel format, then the rbox, then the buffer written out as RGBA.
template<class PixFmt>
int render_as(unsigned w, unsigned h, agg::rbox_ctrl<agg::rgba8>& format_ctrl, const char* out)
{
    headless::canvas cv(w, h, PixFmt::pix_width);

    double m_x[3] = { 100, 369, 143 };
    double m_y[3] = { 60, 170, 310 };

    agg::path_storage path;
    path.move_to(m_x[0], m_y[0]);
    path.line_to(m_x[1], m_y[1]);
    path.line_to(m_x[2], m_y[2]);
    path.close_polygon();

    polymorphic_renderer_solid_rgba8_base* ren = new polymorphic_renderer_solid_rgba8_adaptor<PixFmt>(cv.rbuf);

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;
    ren->clear(agg::srgba8(255, 255, 255));
    ren->color(agg::srgba8(80, 30, 20));
    ras.add_path(path);
    agg::render_scanlines(ras, sl, *ren);
    delete ren;

    PixFmt pf(cv.rbuf);
    agg::renderer_base<PixFmt> rb(pf);
    agg::render_ctrl(ras, sl, rb, format_ctrl);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}

} // namespace

int render_polymorphic_renderer(unsigned w, unsigned h,
                                const std::vector<double>& params, const char* out) {
    int format = params.size() > 0 ? int(params[0]) : 0;

    agg::rbox_ctrl<agg::rgba8> format_ctrl(10.0, 10.0, 90.0, 170.0, false);
    format_ctrl.add_item("rgb555");
    format_ctrl.add_item("rgb565");
    format_ctrl.add_item("rgb24");
    format_ctrl.add_item("bgr24");
    format_ctrl.add_item("rgba32");
    format_ctrl.add_item("argb32");
    format_ctrl.add_item("abgr32");
    format_ctrl.add_item("bgra32");
    format_ctrl.cur_item(format);

    // polymorphic_renderer.cpp's class factory.
    switch (format) {
    case 0: return render_as<agg::pixfmt_rgb555>(w, h, format_ctrl, out);
    case 1: return render_as<agg::pixfmt_rgb565>(w, h, format_ctrl, out);
    case 2: return render_as<agg::pixfmt_rgb24>(w, h, format_ctrl, out);
    case 3: return render_as<agg::pixfmt_bgr24>(w, h, format_ctrl, out);
    case 4: return render_as<agg::pixfmt_rgba32>(w, h, format_ctrl, out);
    case 5: return render_as<agg::pixfmt_argb32>(w, h, format_ctrl, out);
    case 6: return render_as<agg::pixfmt_abgr32>(w, h, format_ctrl, out);
    case 7: return render_as<agg::pixfmt_bgra32>(w, h, format_ctrl, out);
    }
    fprintf(stderr, "polymorphic_renderer: format %d is not 0..7\n", format);
    return 1;
}
