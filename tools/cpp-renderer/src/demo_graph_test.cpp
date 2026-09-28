// graph_test.cpp headless reproduction (default state: 700x530 bgr24 window, "Solid lines", width 2,
// nodes and edges drawn, fine (not draft) and opaque).
//
// Params (all optional): type (0 Solid lines, 1 Bezier curves, 2 Dashed curves, 3 Polygons AA, 4 Polygons
// Bin), draft (0/1), translucent (0/1), width (the slider's value, 0 to 5).
//
// The graph and every edge's colour come from rand(), seeded 100 (the example's srand(100)), whose
// sequence differs between C libraries; this draws them from msvc_rand seeded 100, as the C# port does.
// Every rand() call is its own statement in the example, so their order is defined.
//
// The benchmark (on_ctrl_change with Benchmark checked) is left out: it only times draw_scene and
// reports the times in a message box.
#include <vector>

#include "agg_basics.h"
// First, so its include guard keeps agg_vcgen_vertex_sequence.h (inside AGG_ROOT) from pulling in the
// unpatched original for conv_marker_adaptor.
#include "agg_shorten_path.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_rasterizer_outline.h"
#include "agg_conv_stroke.h"
#include "agg_conv_dash.h"
#include "agg_conv_curve.h"
#include "agg_conv_marker.h"
#include "agg_conv_marker_adaptor.h"
#include "agg_conv_concat.h"
#include "agg_arrowhead.h"
#include "agg_vcgen_markers_term.h"
#include "agg_curves.h"
#include "agg_ellipse.h"
#include "agg_scanline_u.h"
#include "agg_renderer_scanline.h"
#include "agg_renderer_primitives.h"
#include "agg_span_allocator.h"
#include "agg_span_gradient.h"
#include "agg_span_interpolator_linear.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"
#include "ctrl/agg_rbox_ctrl.h"
#include "ctrl/agg_cbox_ctrl.h"

#include "common.h"
#include "flash_shape.h"

namespace {

typedef agg::pixfmt_bgr24 pixfmt;
typedef agg::rgba8 color_type;
typedef agg::renderer_base<pixfmt> base_renderer;
typedef agg::renderer_primitives<base_renderer> primitives_renderer;
typedef agg::renderer_scanline_aa_solid<base_renderer> solid_renderer;
typedef agg::renderer_scanline_bin_solid<base_renderer> draft_renderer;
typedef agg::gradient_radial_d gradient_function;
typedef agg::span_interpolator_linear<> interpolator;
typedef agg::pod_auto_array<color_type, 256> color_array_type;
typedef agg::span_gradient<color_type, interpolator, gradient_function, color_array_type> gradient_span_gen;
typedef agg::span_allocator<color_type> gradient_span_alloc;
typedef agg::renderer_scanline_aa<base_renderer, gradient_span_alloc, gradient_span_gen> gradient_renderer;
typedef agg::rasterizer_scanline_aa<> scanline_rasterizer;
typedef agg::rasterizer_outline<primitives_renderer> outline_rasterizer;

const double msvc_rand_max = 32767;

struct graph {
    struct node { double x, y; };
    struct edge { int node1, node2; };

    std::vector<node> m_nodes;
    std::vector<edge> m_edges;

    graph(int num_nodes, int num_edges, agg::msvc_rand& rng) : m_nodes(num_nodes), m_edges(num_edges) {
        for (int i = 0; i < num_nodes; i++) {
            m_nodes[i].x = (double(rng.next()) / msvc_rand_max) * 0.75 + 0.2;
            m_nodes[i].y = (double(rng.next()) / msvc_rand_max) * 0.85 + 0.1;
        }
        for (int i = 0; i < num_edges; i++) {
            m_edges[i].node1 = rng.next() % num_nodes;
            m_edges[i].node2 = rng.next() % num_nodes;
            if (m_edges[i].node1 == m_edges[i].node2) i--;
        }
    }

    node get_node(int idx, double w, double h) const {
        node p = m_nodes[idx];
        p.x *= w;
        p.y *= h;
        return p;
    }
};

struct line {
    double x1, y1, x2, y2;
    int f;
    line(double x1_, double y1_, double x2_, double y2_) : x1(x1_), y1(y1_), x2(x2_), y2(y2_), f(0) {}
    void rewind(unsigned) { f = 0; }
    unsigned vertex(double* x, double* y) {
        if (f == 0) { ++f; *x = x1; *y = y1; return agg::path_cmd_move_to; }
        if (f == 1) { ++f; *x = x2; *y = y2; return agg::path_cmd_line_to; }
        return agg::path_cmd_stop;
    }
};

struct curve {
    agg::curve4 c;
    curve(double x1, double y1, double x2, double y2, double k = 0.5) {
        c.init(x1, y1, x1 - (y2 - y1) * k, y1 + (x2 - x1) * k, x2 + (y2 - y1) * k, y2 - (x2 - x1) * k, x2, y2);
    }
    void rewind(unsigned path_id) { c.rewind(path_id); }
    unsigned vertex(double* x, double* y) { return c.vertex(x, y); }
};

template<class Source> struct stroke_draft {
    typedef agg::conv_marker_adaptor<Source, agg::vcgen_markers_term> stroke_type;
    typedef agg::conv_marker<typename stroke_type::marker_type, agg::arrowhead> marker_type;
    typedef agg::conv_concat<stroke_type, marker_type> concat_type;
    stroke_type s; agg::arrowhead ah; marker_type m; concat_type c;
    stroke_draft(Source& src, double) : s(src), ah(), m(s.markers(), ah), c(s, m) {
        ah.head(0, 10, 5, 0);
        s.shorten(10.0);
    }
    void rewind(unsigned path_id) { c.rewind(path_id); }
    unsigned vertex(double* x, double* y) { return c.vertex(x, y); }
};

template<class Source> struct dash_stroke_draft {
    typedef agg::conv_dash<Source, agg::vcgen_markers_term> dash_type;
    typedef agg::conv_marker<typename dash_type::marker_type, agg::arrowhead> marker_type;
    typedef agg::conv_concat<dash_type, marker_type> concat_type;
    dash_type d; agg::arrowhead ah; marker_type m; concat_type c;
    dash_stroke_draft(Source& src, double dash_len, double gap_len, double) : d(src), ah(), m(d.markers(), ah), c(d, m) {
        d.add_dash(dash_len, gap_len);
        ah.head(0, 10, 5, 0);
        d.shorten(10.0);
    }
    void rewind(unsigned path_id) { c.rewind(path_id); }
    unsigned vertex(double* x, double* y) { return c.vertex(x, y); }
};

template<class Source> struct stroke_fine {
    typedef agg::conv_stroke<Source, agg::vcgen_markers_term> stroke_type;
    typedef agg::conv_marker<typename stroke_type::marker_type, agg::arrowhead> marker_type;
    typedef agg::conv_concat<stroke_type, marker_type> concat_type;
    stroke_type s; agg::arrowhead ah; marker_type m; concat_type c;
    stroke_fine(Source& src, double w) : s(src), ah(), m(s.markers(), ah), c(s, m) {
        s.width(w);
        ah.head(0, 10, 5, 0);
        s.shorten(w * 2.0);
    }
    void rewind(unsigned path_id) { c.rewind(path_id); }
    unsigned vertex(double* x, double* y) { return c.vertex(x, y); }
};

template<class Source> struct dash_stroke_fine {
    typedef agg::conv_dash<Source, agg::vcgen_markers_term> dash_type;
    typedef agg::conv_stroke<dash_type> stroke_type;
    typedef agg::conv_marker<typename dash_type::marker_type, agg::arrowhead> marker_type;
    typedef agg::conv_concat<stroke_type, marker_type> concat_type;
    dash_type d; stroke_type s; agg::arrowhead ah; marker_type m; concat_type c;
    dash_stroke_fine(Source& src, double dash_len, double gap_len, double w) : d(src), s(d), ah(), m(d.markers(), ah), c(s, m) {
        d.add_dash(dash_len, gap_len);
        s.width(w);
        ah.head(0, 10, 5, 0);
        d.shorten(w * 2.0);
    }
    void rewind(unsigned path_id) { c.rewind(path_id); }
    unsigned vertex(double* x, double* y) { return c.vertex(x, y); }
};

struct app {
    agg::rendering_buffer& rbuf;
    double w, h;
    int type;
    bool draft;
    bool translucent;
    double width;
    graph& m_graph;
    color_array_type& m_gradient_colors;
    agg::msvc_rand rng;
    agg::scanline_u8 m_sl;

    app(agg::rendering_buffer& rbuf_, double w_, double h_, int type_, bool draft_, bool translucent_, double width_,
        graph& graph_, color_array_type& colors_)
        : rbuf(rbuf_), w(w_), h(h_), type(type_), draft(draft_), translucent(translucent_), width(width_),
          m_graph(graph_), m_gradient_colors(colors_) {}

    void edge_color(int& r, int& g, int& b, int& a) {
        r = rng.next() & 0x7F;
        g = rng.next() & 0x7F;
        b = rng.next() & 0x7F;
        a = translucent ? 80 : 255;
    }

    void draw_nodes_draft() {
        pixfmt pixf(rbuf);
        base_renderer rb(pixf);
        primitives_renderer prim(rb);
        for (size_t i = 0; i < m_graph.m_nodes.size(); i++) {
            graph::node n = m_graph.get_node(int(i), w, h);
            prim.fill_color(m_gradient_colors[147]);
            prim.line_color(m_gradient_colors[255]);
            prim.outlined_ellipse(int(n.x), int(n.y), 10, 10);
            prim.fill_color(m_gradient_colors[50]);
            prim.solid_ellipse(int(n.x), int(n.y), 4, 4);
        }
    }

    // m_draw is always 3 outside the benchmark.
    void draw_nodes_fine(scanline_rasterizer& ras) {
        gradient_span_alloc sa;
        pixfmt pixf(rbuf);
        base_renderer rb(pixf);
        for (size_t i = 0; i < m_graph.m_nodes.size(); i++) {
            graph::node n = m_graph.get_node(int(i), w, h);
            agg::ellipse ell(n.x, n.y, 5.0 * width, 5.0 * width);
            gradient_function gf;
            agg::trans_affine mtx;
            mtx *= agg::trans_affine_scaling(width / 2.0);
            mtx *= agg::trans_affine_translation(n.x, n.y);
            mtx.invert();
            interpolator inter(mtx);
            gradient_span_gen sg(inter, gf, m_gradient_colors, 0.0, 10.0);
            gradient_renderer ren(rb, sa, sg);
            ras.add_path(ell);
            agg::render_scanlines(ras, m_sl, ren);
        }
    }

    template<class Source>
    void render_edge_fine(scanline_rasterizer& ras, solid_renderer& ren_fine, draft_renderer& ren_draft, Source& src) {
        int r, g, b, a;
        edge_color(r, g, b, a);
        ras.add_path(src);
        if (type < 4) {
            ren_fine.color(agg::srgba8(r, g, b, a));
            agg::render_scanlines(ras, m_sl, ren_fine);
        } else {
            ren_draft.color(agg::srgba8(r, g, b, a));
            agg::render_scanlines(ras, m_sl, ren_draft);
        }
    }

    void draw_lines_draft() {
        pixfmt pixf(rbuf); base_renderer rb(pixf); primitives_renderer prim(rb); outline_rasterizer ras(prim);
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            line l(n1.x, n1.y, n2.x, n2.y);
            stroke_draft<line> s(l, width);
            int r, g, b, a;
            edge_color(r, g, b, a);
            prim.line_color(agg::srgba8(r, g, b, a));
            ras.add_path(s);
        }
    }

    void draw_curves_draft() {
        pixfmt pixf(rbuf); base_renderer rb(pixf); primitives_renderer prim(rb); outline_rasterizer ras(prim);
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            curve c(n1.x, n1.y, n2.x, n2.y);
            stroke_draft<curve> s(c, width);
            int r, g, b, a;
            edge_color(r, g, b, a);
            prim.line_color(agg::srgba8(r, g, b, a));
            ras.add_path(s);
        }
    }

    void draw_dashes_draft() {
        pixfmt pixf(rbuf); base_renderer rb(pixf); primitives_renderer prim(rb); outline_rasterizer ras(prim);
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            curve c(n1.x, n1.y, n2.x, n2.y);
            dash_stroke_draft<curve> s(c, 6.0, 3.0, width);
            int r, g, b, a;
            edge_color(r, g, b, a);
            prim.line_color(agg::srgba8(r, g, b, a));
            ras.add_path(s);
        }
    }

    void draw_lines_fine(scanline_rasterizer& ras, solid_renderer& solid, draft_renderer& dr) {
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            line l(n1.x, n1.y, n2.x, n2.y);
            stroke_fine<line> s(l, width);
            render_edge_fine(ras, solid, dr, s);
        }
    }

    void draw_curves_fine(scanline_rasterizer& ras, solid_renderer& solid, draft_renderer& dr) {
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            curve c(n1.x, n1.y, n2.x, n2.y);
            stroke_fine<curve> s(c, width);
            render_edge_fine(ras, solid, dr, s);
        }
    }

    void draw_dashes_fine(scanline_rasterizer& ras, solid_renderer& solid, draft_renderer& dr) {
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            curve c(n1.x, n1.y, n2.x, n2.y);
            dash_stroke_fine<curve> s(c, 6.0, 3.0, width);
            render_edge_fine(ras, solid, dr, s);
        }
    }

    void draw_polygons(scanline_rasterizer& ras, solid_renderer& solid, draft_renderer& dr) {
        if (type == 4) ras.gamma(agg::gamma_threshold(0.5));
        for (size_t i = 0; i < m_graph.m_edges.size(); i++) {
            graph::edge e = m_graph.m_edges[i];
            graph::node n1 = m_graph.get_node(e.node1, w, h);
            graph::node n2 = m_graph.get_node(e.node2, w, h);
            curve c(n1.x, n1.y, n2.x, n2.y);
            render_edge_fine(ras, solid, dr, c);
        }
        ras.gamma(agg::gamma_none());
    }

    void draw_scene(scanline_rasterizer& ras, solid_renderer& solid, draft_renderer& dr) {
        ras.gamma(agg::gamma_none());
        rng.holdrand = 100;
        if (draft) draw_nodes_draft(); else draw_nodes_fine(ras);
        if (draft) {
            switch (type) {
                case 0: draw_lines_draft(); break;
                case 1: draw_curves_draft(); break;
                case 2: draw_dashes_draft(); break;
            }
        } else {
            switch (type) {
                case 0: draw_lines_fine(ras, solid, dr); break;
                case 1: draw_curves_fine(ras, solid, dr); break;
                case 2: draw_dashes_fine(ras, solid, dr); break;
                case 3:
                case 4: draw_polygons(ras, solid, dr); break;
            }
        }
    }
};

} // namespace

int render_graph_test(unsigned w, unsigned h, const std::vector<double>& params, const char* out) {
    int type = params.size() > 0 ? int(params[0]) : 0;
    bool draft = params.size() > 1 && params[1] != 0.0;
    bool translucent = params.size() > 2 && params[2] != 0.0;
    double width_value = params.size() > 3 ? params[3] : 2.0;

    headless::canvas cv(w, h, 3);
    pixfmt pf(cv.rbuf);
    base_renderer rb(pf);

    // flip_y = true, so every control gets !flip_y = false.
    agg::rbox_ctrl<agg::rgba> m_type(-1, -1, -1, -1, false);
    agg::slider_ctrl<agg::rgba> m_width(110 + 80, 8.0, 110 + 200.0 + 80, 8.0 + 7.0, false);
    agg::cbox_ctrl<agg::rgba> m_benchmark(110 + 200 + 80 + 8, 8.0 - 2.0, "Benchmark", false);
    agg::cbox_ctrl<agg::rgba> m_draw_nodes(110 + 200 + 80 + 8, 8.0 - 2.0 + 15.0, "Draw Nodes", false);
    agg::cbox_ctrl<agg::rgba> m_draw_edges(200 + 200 + 80 + 8, 8.0 - 2.0 + 15.0, "Draw Edges", false);
    agg::cbox_ctrl<agg::rgba> m_draft(200 + 200 + 80 + 8, 8.0 - 2.0, "Draft Mode", false);
    agg::cbox_ctrl<agg::rgba> m_translucent(110 + 80, 8.0 - 2.0 + 15.0, "Translucent Mode", false);

    m_type.text_size(8.0);
    m_type.add_item("Solid lines");
    m_type.add_item("Bezier curves");
    m_type.add_item("Dashed curves");
    m_type.add_item("Poygons AA");
    m_type.add_item("Poygons Bin");
    m_type.cur_item(type);

    m_width.num_steps(20);
    m_width.range(0.0, 5.0);
    m_width.value(width_value);
    m_width.label("Width=%1.2f");

    m_benchmark.text_size(8.0);
    m_draw_nodes.text_size(8.0);
    m_draft.text_size(8.0);
    m_draw_nodes.status(true);
    m_draw_edges.status(true);
    m_draft.status(draft);
    m_translucent.status(translucent);

    color_array_type gradient_colors;
    agg::rgba c1(1, 1, 0, 0.25);
    agg::rgba c2(0, 0, 1);
    for (int i = 0; i < 256; i++) gradient_colors[i] = c1.gradient(c2, double(i) / 255.0);

    agg::msvc_rand graph_rng;
    graph_rng.holdrand = 100;
    graph g(200, 100, graph_rng);

    app a(cv.rbuf, double(w), double(h), type, draft, translucent, m_width.value(), g, gradient_colors);

    scanline_rasterizer ras;
    solid_renderer solid(rb);
    draft_renderer dr(rb);

    rb.clear(agg::rgba(1, 1, 1));
    a.draw_scene(ras, solid, dr);

    ras.filling_rule(agg::fill_non_zero);
    agg::render_ctrl(ras, a.m_sl, rb, m_type);
    agg::render_ctrl(ras, a.m_sl, rb, m_width);
    agg::render_ctrl(ras, a.m_sl, rb, m_benchmark);
    agg::render_ctrl(ras, a.m_sl, rb, m_draw_nodes);
    agg::render_ctrl(ras, a.m_sl, rb, m_draw_edges);
    agg::render_ctrl(ras, a.m_sl, rb, m_draft);
    agg::render_ctrl(ras, a.m_sl, rb, m_translucent);

    return headless::write_raw(out, pf, w, h) ? 0 : 1;
}
