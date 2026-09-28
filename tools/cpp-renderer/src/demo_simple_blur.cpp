// simple_blur.cpp headless reproduction (default state: blur circle at
// 100,102, 512x400 bgr24 window cleared to white).
//
// Params (all optional): circle_x, circle_y.
//
// agg-sharp fix: the example's span_simple_blur_rgb24 gives no_color where the
// 3x3 block leaves the image at the top or bottom, but opaque black at the
// left or right (its sums stay 0). Here the side columns get no_color too, as
// agg-sharp's SimpleBlurDemo does.
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_path_storage.h"
#include "agg_conv_stroke.h"
#include "agg_conv_transform.h"
#include "agg_bounding_rect.h"
#include "agg_scanline_u.h"
#include "agg_scanline_p.h"
#include "agg_pixfmt_rgb.h"
#include "agg_renderer_base.h"
#include "agg_renderer_outline_aa.h"
#include "agg_rasterizer_outline_aa.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_ellipse.h"

#include "common.h"

unsigned parse_lion(agg::path_storage& ps, agg::srgba8* colors, unsigned* path_idx);

namespace {

template<class Order> class span_simple_blur_rgb24 {
public:
    typedef agg::rgba8 color_type;

    span_simple_blur_rgb24(const agg::rendering_buffer& src) : m_source_image(&src) {}

    void prepare() {}

    void generate(color_type* span, int x, int y, int len) {
        if (y < 1 || y >= int(m_source_image->height() - 1)) {
            do { *span++ = color_type::no_color(); } while (--len);
            return;
        }
        do {
            // agg-sharp patch: C++ leaves color at 0 and writes opaque black here.
            if (!(x > 0 && x < int(m_source_image->width() - 1))) {
                *span++ = color_type::no_color();
                ++x;
                continue;
            }
            color_type::calc_type color[3];
            color[0] = color[1] = color[2] = 0;
            int i = 3;
            do {
                const agg::int8u* ptr = m_source_image->row_ptr(y - i + 2) + (x - 1) * 3;
                color[0] += *ptr++; color[1] += *ptr++; color[2] += *ptr++;
                color[0] += *ptr++; color[1] += *ptr++; color[2] += *ptr++;
                color[0] += *ptr++; color[1] += *ptr++; color[2] += *ptr++;
            } while (--i);
            color[0] /= 9;
            color[1] /= 9;
            color[2] /= 9;
            *span++ = color_type(color[Order::R], color[Order::G], color[Order::B]);
            ++x;
        } while (--len);
    }

private:
    const agg::rendering_buffer* m_source_image;
};

}  // namespace

int render_simple_blur(unsigned w, unsigned h,
                       const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;

    double cx = params.size() > 0 ? params[0] : 100.0;
    double cy = params.size() > 1 ? params[1] : 102.0;

    agg::path_storage path;
    agg::srgba8 colors[100];
    unsigned path_idx[100];
    unsigned npaths = parse_lion(path, colors, path_idx);

    double x1, y1, x2, y2;
    agg::pod_array_adaptor<unsigned> pia(path_idx, 100);
    agg::bounding_rect(path, pia, 0, npaths, &x1, &y1, &x2, &y2);
    double base_dx = (x2 - x1) / 2.0;
    double base_dy = (y2 - y1) / 2.0;

    headless::canvas cv(w, h, 3);
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid rs(rb);
    rb.clear(agg::rgba(1, 1, 1));

    // At the initial size trans_affine_resizing() is identity.
    agg::trans_affine mtx;
    agg::conv_transform<agg::path_storage, agg::trans_affine> trans(path, mtx);
    mtx *= agg::trans_affine_translation(-base_dx, -base_dy);
    mtx *= agg::trans_affine_scaling(1.0, 1.0);
    mtx *= agg::trans_affine_rotation(0.0 + agg::pi);
    mtx *= agg::trans_affine_skewing(0.0, 0.0);
    // initial_width() / 4 and initial_height() / 2 are unsigned divisions.
    mtx *= agg::trans_affine_translation(w / 4, h / 2);

    agg::rasterizer_scanline_aa<> ras2;
    agg::scanline_p8 sl;
    agg::scanline_u8 sl2;

    agg::render_all_paths(ras2, sl, rs, trans, colors, path_idx, npaths);

    mtx *= agg::trans_affine_translation(w / 2, 0);

    agg::line_profile_aa profile;
    profile.width(1.0);
    agg::renderer_outline_aa<renderer_base> rp(rb, profile);
    agg::rasterizer_outline_aa<agg::renderer_outline_aa<renderer_base> > ras(rp);
    ras.round_cap(true);
    ras.render_all_paths(trans, colors, path_idx, npaths);

    agg::ellipse ell(cx, cy, 100.0, 100.0, 100);
    agg::conv_stroke<agg::ellipse> ell_stroke1(ell);
    ell_stroke1.width(6.0);
    agg::conv_stroke<agg::conv_stroke<agg::ellipse> > ell_stroke2(ell_stroke1);
    ell_stroke2.width(2.0);
    rs.color(agg::rgba(0, 0.2, 0));
    ras2.add_path(ell_stroke2);
    agg::render_scanlines(ras2, sl, rs);

    // copy_window_to_img(0): a copy of the frame so far, in the window's orientation.
    headless::canvas img(w, h, 3);
    img.rbuf.copy_from(cv.rbuf);

    typedef span_simple_blur_rgb24<agg::order_bgr> span_blur_gen;
    agg::span_allocator<agg::rgba8> sa;
    span_blur_gen sg(img.rbuf);
    ras2.add_path(ell);
    agg::render_scanlines_aa(ras2, sl2, rb, sa, sg);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
