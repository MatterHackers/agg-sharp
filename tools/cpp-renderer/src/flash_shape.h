// The compound_shape reader of AGG's flash_rasterizer.cpp and flash_rasterizer2.cpp, as they wrote it:
// one "=======BeginShape" block of shapes.txt per read_next(), each path with its left fill, right fill
// and line style, drawn through conv_curve then the viewport transform that fits the shape to the window.
// flash_rasterizer2's copy adds min_style()/max_style(); flash_rasterizer's never calls them, so the one
// header serves both. Also msvc_rand, the rand() both examples draw their palette from.
#ifndef HEADLESS_FLASH_SHAPE_H
#define HEADLESS_FLASH_SHAPE_H

#include <cstdio>
#include <cstring>
#include <cstdlib>
#include <limits>

#include "agg_basics.h"
#include "agg_path_storage.h"
#include "agg_conv_curve.h"
#include "agg_conv_transform.h"
#include "agg_bounding_rect.h"
#include "agg_trans_viewport.h"
#include "agg_math.h"
#include "agg_array.h"
#include "agg_color_rgba.h"

namespace agg {

struct path_style {
    unsigned path_id;
    int left_fill;
    int right_fill;
    int line;
};

class compound_shape {
public:
    ~compound_shape() { if (m_fd) fclose(m_fd); }
    compound_shape()
        : m_path(), m_affine(), m_curve(m_path), m_trans(m_curve, m_affine), m_styles(),
          m_min_style(std::numeric_limits<int>::max()),
          m_max_style(std::numeric_limits<int>::min()),
          m_fd(0) {}

    bool open(const char* fname) { m_fd = fopen(fname, "r"); return m_fd != 0; }

    bool read_next() {
        m_path.remove_all();
        m_styles.remove_all();
        m_min_style = std::numeric_limits<int>::max();
        m_max_style = std::numeric_limits<int>::min();
        const char space[] = " \t\n\r";
        double ax, ay, cx, cy;
        if (m_fd) {
            char buf[1024];
            char* ts;
            for (;;) {
                if (fgets(buf, 1022, m_fd) == 0) return false;
                if (buf[0] == '=') break;
            }
            while (fgets(buf, 1022, m_fd)) {
                if (buf[0] == '!') break;
                if (buf[0] == 'P') {
                    // BeginPath
                    path_style style;
                    style.path_id = m_path.start_new_path();
                    ts = strtok(buf, space); // Path;
                    ts = strtok(0, space); style.left_fill = atoi(ts);
                    ts = strtok(0, space); style.right_fill = atoi(ts);
                    ts = strtok(0, space); style.line = atoi(ts);
                    ts = strtok(0, space); ax = atof(ts);
                    ts = strtok(0, space); ay = atof(ts);
                    m_path.move_to(ax, ay);
                    m_styles.add(style);
                    if (style.left_fill >= 0) {
                        if (style.left_fill < m_min_style) m_min_style = style.left_fill;
                        if (style.left_fill > m_max_style) m_max_style = style.left_fill;
                    }
                    if (style.right_fill >= 0) {
                        if (style.right_fill < m_min_style) m_min_style = style.right_fill;
                        if (style.right_fill > m_max_style) m_max_style = style.right_fill;
                    }
                }
                if (buf[0] == 'C') {
                    ts = strtok(buf, space); // Curve;
                    ts = strtok(0, space); cx = atof(ts);
                    ts = strtok(0, space); cy = atof(ts);
                    ts = strtok(0, space); ax = atof(ts);
                    ts = strtok(0, space); ay = atof(ts);
                    m_path.curve3(cx, cy, ax, ay);
                }
                if (buf[0] == 'L') {
                    ts = strtok(buf, space); // Line;
                    ts = strtok(0, space); ax = atof(ts);
                    ts = strtok(0, space); ay = atof(ts);
                    m_path.line_to(ax, ay);
                }
            }
            return true;
        }
        return false;
    }

    unsigned operator[](unsigned i) const { return m_styles[i].path_id; }
    unsigned paths() const { return m_styles.size(); }
    const path_style& style(unsigned i) const { return m_styles[i]; }
    int min_style() const { return m_min_style; }
    int max_style() const { return m_max_style; }
    void rewind(unsigned path_id) { m_trans.rewind(path_id); }
    unsigned vertex(double* x, double* y) { return m_trans.vertex(x, y); }
    double scale() const { return m_affine.scale(); }

    void scale(double w, double h) {
        m_affine.reset();
        double x1, y1, x2, y2;
        bounding_rect(m_path, *this, 0, m_styles.size(), &x1, &y1, &x2, &y2);
        if (x1 < x2 && y1 < y2) {
            trans_viewport vp;
            vp.preserve_aspect_ratio(0.5, 0.5, aspect_ratio_meet);
            vp.world_viewport(x1, y1, x2, y2);
            vp.device_viewport(0, 0, w, h);
            m_affine = vp.to_affine();
        }
        m_curve.approximation_scale(m_affine.scale());
    }

    void approximation_scale(double s) { m_curve.approximation_scale(m_affine.scale() * s); }

private:
    path_storage m_path;
    trans_affine m_affine;
    conv_curve<path_storage> m_curve;
    conv_transform<conv_curve<path_storage> > m_trans;
    pod_bvector<path_style> m_styles;
    int m_min_style;
    int m_max_style;
    FILE* m_fd;
};

// MSVC/C89 rand(): the examples' palette comes from rand(), whose sequence differs between C libraries,
// so the reference renderer and the port (MsvcRand) both use this one.
struct msvc_rand {
    unsigned holdrand = 1;
    int next() { holdrand = holdrand * 214013u + 2531011u; return (holdrand >> 16) & 0x7fff; }
};

// The examples' m_colors: 100 srgba8(rand() & 0xFF, rand() & 0xFF, rand() & 0xFF, 230), premultiplied.
// The rand() calls are argument order, which C++ leaves unspecified; they are taken red, green, blue here
// (the port does the same).
inline void flash_palette(rgba8* colors) {
    msvc_rand rng;
    for (int i = 0; i < 100; ++i) {
        unsigned r = rng.next() & 0xFF;
        unsigned g = rng.next() & 0xFF;
        unsigned b = rng.next() & 0xFF;
        colors[i] = srgba8(r, g, b, 230);
        colors[i].premultiply();
    }
}

// The examples' on_key zoom and rotation, each about (x, y): m_scale *= translation(-x, -y), then the
// scaling (1.1 per step in, 1/1.1 out) or rotation (pi/20 per step right, -pi/20 left), then
// translation(x, y). Zoom steps are applied first, then rotation steps.
inline void flash_keys(trans_affine& m_scale, double x, double y, int zoom_steps, int rotate_steps) {
    for (int i = 0; i < (zoom_steps < 0 ? -zoom_steps : zoom_steps); ++i) {
        m_scale *= trans_affine_translation(-x, -y);
        m_scale *= trans_affine_scaling(zoom_steps > 0 ? 1.1 : 1 / 1.1);
        m_scale *= trans_affine_translation(x, y);
    }
    for (int i = 0; i < (rotate_steps < 0 ? -rotate_steps : rotate_steps); ++i) {
        m_scale *= trans_affine_translation(-x, -y);
        m_scale *= trans_affine_rotation(rotate_steps > 0 ? pi / 20.0 : -pi / 20.0);
        m_scale *= trans_affine_translation(x, y);
    }
}

} // namespace agg

#endif
