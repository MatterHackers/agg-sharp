// image_alpha.cpp headless reproduction: on_draw() at the window size the
// example opens at (the spheres image's, 320x300). The 50 background ellipses
// come from the MSVC rand() generator seeded 1 (C's rand() before any srand),
// each value taken in argument order and at the initial width and height, so
// the port can draw the same ones. Params (all optional): the alpha spline's
// six point y values.
//
// The example draws into BGR24; this renders into BGRA32 so the frame is the
// 32-bit one agg-sharp's reference frame is. Every pixel is blended over
// opaque white, so the colour channels match and alpha stays 255. The image
// stays 24-bit, since the span generator is the rgb one.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_ellipse.h"
#include "agg_trans_affine.h"
#include "agg_conv_transform.h"
#include "agg_pixfmt_rgb.h"
#include "agg_pixfmt_rgba.h"
#include "agg_span_image_filter_rgb.h"
#include "agg_image_accessors.h"
#include "agg_span_interpolator_linear.h"
#include "agg_span_converter.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "ctrl/agg_spline_ctrl.h"

#include "common.h"

namespace {

typedef agg::rgba8 color_type;

unsigned g_image_alpha_holdrand = 1;

int image_alpha_rand()
{
    g_image_alpha_holdrand = g_image_alpha_holdrand * 214013u + 2531011u;
    return (g_image_alpha_holdrand >> 16) & 0x7fff;
}

// The example's span_conv_brightness_alpha.
class span_conv_brightness_alpha
{
public:
    typedef agg::int8u alpha_type;
    enum array_size_e { array_size = 256 * 3 };

    span_conv_brightness_alpha(const alpha_type* alpha_array) : m_alpha_array(alpha_array) {}

    void prepare() {}

    void generate(color_type* span, int, int, unsigned len) const
    {
        do
        {
            color_type::calc_type x = span->r + span->g + span->b;
            // The example indexes with x * array_size / (3 * full_value()), which is array_size - one past the
            // end - for a white pixel; the index is clamped to the last entry (ImageAlphaDemo).
            int index = int(x * array_size / (3 * color_type::full_value()));
            if (index > array_size - 1) index = array_size - 1;
            agg::cover_type cover = m_alpha_array[index];
            span->a = color_type::mult_cover(color_type::full_value(), cover);
            ++span;
        }
        while (--len);
    }

private:
    const alpha_type* m_alpha_array;
};

} // namespace

int render_image_alpha(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32 pixfmt;
    typedef agg::pixfmt_bgr24 img_pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;

    headless::image_rgba img = headless::load_spheres_flip_y();
    if (!img.ok) {
        fprintf(stderr, "image_alpha: failed to load spheres image\n");
        return 1;
    }
    std::vector<unsigned char> img_bytes;
    agg::rendering_buffer img_rbuf;
    headless::pack_image(img, "bgr", img_bytes, img_rbuf);

    // initial_width()/initial_height(): the window agg_main opens.
    double iw = img.width, ih = img.height;

    // The constructor: flip_y = true, so the ctrl gets !flip_y.
    agg::spline_ctrl<color_type> m_alpha(2, 2, 200, 30, 6, false);
    double ys[6] = { 1.0, 1.0, 1.0, 0.5, 0.5, 1.0 };
    for (unsigned i = 0; i < 6 && i < params.size(); ++i) ys[i] = params[i];
    for (unsigned i = 0; i < 6; ++i) m_alpha.value(i, ys[i]);
    m_alpha.update_spline();

    // on_init
    double m_x[50], m_y[50], m_rx[50], m_ry[50];
    color_type m_colors[50];
    g_image_alpha_holdrand = 1;
    for (unsigned i = 0; i < 50; i++) {
        m_x[i] = image_alpha_rand() % int(iw);
        m_y[i] = image_alpha_rand() % int(ih);
        m_rx[i] = image_alpha_rand() % 60 + 10;
        m_ry[i] = image_alpha_rand() % 60 + 10;
        int r = image_alpha_rand() & 0xFF;
        int g = image_alpha_rand() & 0xFF;
        int b = image_alpha_rand() & 0xFF;
        int a = image_alpha_rand() & 0xFF;
        m_colors[i] = agg::srgba8(r, g, b, a);
    }

    headless::canvas cv(w, h, 4);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    rb.clear(agg::rgba(1.0, 1.0, 1.0));

    // trans_affine_resizing() is the identity at the initial size.
    agg::trans_affine src_mtx;
    src_mtx *= agg::trans_affine_translation(-iw / 2, -ih / 2);
    src_mtx *= agg::trans_affine_rotation(10.0 * agg::pi / 180.0);
    src_mtx *= agg::trans_affine_translation(iw / 2, ih / 2);

    agg::trans_affine img_mtx = src_mtx;
    img_mtx.invert();

    unsigned char brightness_alpha_array[span_conv_brightness_alpha::array_size];
    for (unsigned i = 0; i < span_conv_brightness_alpha::array_size; i++) {
        brightness_alpha_array[i] =
            agg::int8u(m_alpha.value(double(i) / double(span_conv_brightness_alpha::array_size)) * 255.0);
    }
    span_conv_brightness_alpha color_alpha(brightness_alpha_array);

    typedef agg::image_accessor_clip<img_pixfmt> img_source_type;
    typedef agg::span_interpolator_linear<> interpolator_type;
    typedef agg::span_image_filter_rgb_bilinear<img_source_type, interpolator_type> span_gen;
    typedef agg::span_converter<span_gen, span_conv_brightness_alpha> span_conv;

    agg::span_allocator<color_type> sa;
    interpolator_type interpolator(img_mtx);
    img_pixfmt img_pixf(img_rbuf);
    img_source_type img_src(img_pixf, agg::rgba(0, 0, 0, 0));
    span_gen sg(img_src, interpolator);
    span_conv sc(sg, color_alpha);

    agg::ellipse ell;
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;

    for (unsigned i = 0; i < 50; i++) {
        ell.init(m_x[i], m_y[i], m_rx[i], m_ry[i], 50);
        ras.add_path(ell);
        agg::render_scanlines_aa_solid(ras, sl, rb, m_colors[i]);
    }

    ell.init(iw / 2.0, ih / 2.0, iw / 1.9, ih / 1.9, 200);
    agg::conv_transform<agg::ellipse> tr(ell, src_mtx);
    ras.add_path(tr);
    agg::render_scanlines_aa(ras, sl, rb, sa, sc);

    agg::render_ctrl(ras, sl, rb, m_alpha);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
