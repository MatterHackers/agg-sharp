// gouraud_mesh.cpp headless reproduction (default state: 400x400 bgra32 window, a 20x20 mesh of 17-pixel
// cells from (40, 40), as on_init makes it).
//
// Params (all optional): idle_steps (how many on_idle steps - randomize_points(1.0) then rotate_colors() -
// run before the frame), time_ms (the time the report shows, default 1).
//
// The mesh's jitter and colours come from rand(), whose sequence differs between C libraries; this draws
// them from msvc_rand (the C89 sequence, seeded 1 as rand() is unseeded), each value in argument order,
// as the C# port does. The example reports how long the mesh took to draw, which no golden can hold, so
// the report shows time_ms instead.
#include <cstdio>
#include <vector>

#include "agg_basics.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_rasterizer_compound_aa.h"
#include "agg_conv_stroke.h"
#include "agg_gsv_text.h"
#include "agg_scanline_u.h"
#include "agg_scanline_bin.h"
#include "agg_renderer_scanline.h"
#include "agg_span_allocator.h"
#include "agg_span_gouraud_rgba.h"
#include "agg_pixfmt_rgba.h"

#include "common.h"
#include "flash_shape.h"

namespace {

typedef agg::rgba8 color_type;

struct mesh_point {
    double x, y;
    double dx, dy;
    agg::srgba8 color;
    agg::srgba8 dc;
};

struct mesh_triangle { unsigned p1, p2, p3; };

struct mesh_edge { unsigned p1, p2; int tl, tr; };

struct mesh_ctrl {
    unsigned m_cols = 0, m_rows = 0;
    double m_cell_w = 0, m_cell_h = 0, m_start_x = 0, m_start_y = 0;
    std::vector<mesh_point> m_vertices;
    std::vector<mesh_triangle> m_triangles;
    std::vector<mesh_edge> m_edges;
    agg::msvc_rand rng;

    double random(double v1, double v2) { return (v2 - v1) * (rng.next() % 1000) / 999.0 + v1; }

    // The example indexes vertex(x, y) by m_rows, not m_cols; the mesh is square, so it is the same.
    mesh_point& vertex(unsigned x, unsigned y) { return m_vertices[y * m_rows + x]; }

    void generate(unsigned cols, unsigned rows, double cell_w, double cell_h, double start_x, double start_y) {
        m_cols = cols; m_rows = rows;
        m_cell_w = cell_w; m_cell_h = cell_h;
        m_start_x = start_x; m_start_y = start_y;
        m_vertices.clear();
        for (unsigned i = 0; i < m_rows; i++) {
            double x = start_x;
            for (unsigned j = 0; j < m_cols; j++) {
                mesh_point p;
                p.x = x;
                p.y = start_y;
                p.dx = random(-0.5, 0.5);
                p.dy = random(-0.5, 0.5);
                unsigned r = rng.next() & 0xFF;
                unsigned g = rng.next() & 0xFF;
                unsigned b = rng.next() & 0xFF;
                p.color = agg::srgba8(r, g, b);
                unsigned dr = rng.next() & 1;
                unsigned dg = rng.next() & 1;
                unsigned db = rng.next() & 1;
                p.dc = agg::srgba8(dr, dg, db);
                m_vertices.push_back(p);
                x += cell_w;
            }
            start_y += cell_h;
        }

        m_triangles.clear();
        m_edges.clear();
        for (unsigned i = 0; i < m_rows - 1; i++) {
            for (unsigned j = 0; j < m_cols - 1; j++) {
                int p1 = i * m_cols + j;
                int p2 = p1 + 1;
                int p3 = p2 + m_cols;
                int p4 = p1 + m_cols;
                m_triangles.push_back({ (unsigned)p1, (unsigned)p2, (unsigned)p3 });
                m_triangles.push_back({ (unsigned)p3, (unsigned)p4, (unsigned)p1 });

                int curr_cell = i * (m_cols - 1) + j;
                int left_cell = j ? int(curr_cell - 1) : -1;
                int bott_cell = i ? int(curr_cell - (m_cols - 1)) : -1;
                int curr_t1 = curr_cell * 2;
                int curr_t2 = curr_t1 + 1;
                int left_t1 = (left_cell >= 0) ? left_cell * 2 : -1;
                int bott_t1 = (bott_cell >= 0) ? bott_cell * 2 : -1;
                int bott_t2 = (bott_cell >= 0) ? bott_t1 + 1 : -1;

                m_edges.push_back({ (unsigned)p1, (unsigned)p2, curr_t1, bott_t2 });
                m_edges.push_back({ (unsigned)p1, (unsigned)p3, curr_t2, curr_t1 });
                m_edges.push_back({ (unsigned)p1, (unsigned)p4, left_t1, curr_t2 });
                if (j == m_cols - 2) m_edges.push_back({ (unsigned)p2, (unsigned)p3, curr_t1, -1 });
                if (i == m_rows - 2) m_edges.push_back({ (unsigned)p3, (unsigned)p4, curr_t2, -1 });
            }
        }
    }

    void randomize_points(double) {
        for (unsigned i = 0; i < m_rows; i++) {
            for (unsigned j = 0; j < m_cols; j++) {
                double xc = j * m_cell_w + m_start_x;
                double yc = i * m_cell_h + m_start_y;
                double x1 = xc - m_cell_w / 4;
                double y1 = yc - m_cell_h / 4;
                double x2 = xc + m_cell_w / 4;
                double y2 = yc + m_cell_h / 4;
                mesh_point& p = vertex(j, i);
                p.x += p.dx;
                p.y += p.dy;
                if (p.x < x1) { p.x = x1; p.dx = -p.dx; }
                if (p.y < y1) { p.y = y1; p.dy = -p.dy; }
                if (p.x > x2) { p.x = x2; p.dx = -p.dx; }
                if (p.y > y2) { p.y = y2; p.dy = -p.dy; }
            }
        }
    }

    void rotate_colors() {
        for (unsigned i = 1; i < m_vertices.size(); i++) {
            agg::srgba8& c = m_vertices[i].color;
            agg::srgba8& dc = m_vertices[i].dc;
            int r = c.r + (dc.r ? 5 : -5);
            int g = c.g + (dc.g ? 5 : -5);
            int b = c.b + (dc.b ? 5 : -5);
            if (r < 0) { r = 0; dc.r ^= 1; } if (r > 255) { r = 255; dc.r ^= 1; }
            if (g < 0) { g = 0; dc.g ^= 1; } if (g > 255) { g = 255; dc.g ^= 1; }
            if (b < 0) { b = 0; dc.b ^= 1; } if (b > 255) { b = 255; dc.b ^= 1; }
            c.r = r;
            c.g = g;
            c.b = b;
        }
    }
};

class styles_gouraud {
public:
    typedef agg::span_gouraud_rgba<color_type> gouraud_type;

    explicit styles_gouraud(const mesh_ctrl& mesh) {
        for (const mesh_triangle& t : mesh.m_triangles) {
            const mesh_point& p1 = mesh.m_vertices[t.p1];
            const mesh_point& p2 = mesh.m_vertices[t.p2];
            const mesh_point& p3 = mesh.m_vertices[t.p3];
            // srgba8 to the linear rgba8 the span generator takes.
            color_type c1 = p1.color;
            color_type c2 = p2.color;
            color_type c3 = p3.color;
            gouraud_type gouraud(c1, c2, c3, p1.x, p1.y, p2.x, p2.y, p3.x, p3.y);
            gouraud.prepare();
            m_triangles.push_back(gouraud);
        }
    }

    bool is_solid(unsigned) const { return false; }
    color_type color(unsigned) const { return color_type::no_color(); }
    void generate_span(color_type* span, int x, int y, unsigned len, unsigned style) {
        m_triangles[style].generate(span, x, y, len);
    }

private:
    std::vector<gouraud_type> m_triangles;
};

} // namespace

int render_gouraud_mesh(unsigned w, unsigned h, const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgra32_pre pixfmt_pre;
    typedef agg::renderer_base<pixfmt_pre> renderer_base;

    int idle_steps = params.size() > 0 ? int(params[0]) : 0;
    double tm = params.size() > 1 ? params[1] : 1.0;

    mesh_ctrl m_mesh;
    m_mesh.generate(20, 20, 17, 17, 40, 40);
    for (int i = 0; i < idle_steps; i++) {
        m_mesh.randomize_points(1.0);
        m_mesh.rotate_colors();
    }

    headless::canvas cv(w, h, 4);
    pixfmt_pre pf(cv.rbuf);
    renderer_base ren_base(pf);
    ren_base.clear(agg::rgba(0, 0, 0));

    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_u8 sl;
    agg::scanline_bin sl_bin;
    agg::rasterizer_compound_aa<> rasc;
    agg::span_allocator<color_type> alloc;

    styles_gouraud styles(m_mesh);
    rasc.reset();
    for (const mesh_edge& e : m_mesh.m_edges) {
        const mesh_point& p1 = m_mesh.m_vertices[e.p1];
        const mesh_point& p2 = m_mesh.m_vertices[e.p2];
        rasc.styles(e.tl, e.tr);
        rasc.move_to_d(p1.x, p1.y);
        rasc.line_to_d(p2.x, p2.y);
    }
    agg::render_scanlines_compound(rasc, sl, sl_bin, ren_base, alloc, styles);

    char buf[256];
    agg::gsv_text t;
    t.size(10.0);

    agg::conv_stroke<agg::gsv_text> pt(t);
    pt.width(1.5);
    pt.line_cap(agg::round_cap);
    pt.line_join(agg::round_join);

    unsigned num_triangles = unsigned(m_mesh.m_triangles.size());
    snprintf(buf, sizeof(buf), "%3.2f ms, %d triangles, %.0f tri/sec", tm, num_triangles, num_triangles / tm * 1000.0);
    t.start_point(10.0, 10.0);
    t.text(buf);

    ras.add_path(pt);
    agg::render_scanlines_aa_solid(ras, sl, ren_base, agg::rgba(1, 1, 1));

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
