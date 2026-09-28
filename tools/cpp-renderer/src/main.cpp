// agg-render: headless C++ AGG reference renderer (brought over from agg-rust).
//
// Usage: agg-render <demo_name> <width> <height> <output.raw> [params...]
//
// Renders the named AGG example demo (in its default interactive state, or with
// optional numeric params) into a raw RGBA file that agg-sharp's AggReference
// tests byte-compare against production output (see ../README.md).
#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

struct demo_entry {
    const char* name;
    // Returns 0 on success, nonzero on failure (asset load or I/O error).
    int (*fn)(unsigned, unsigned, const std::vector<double>&, const char*);
};

// Per-demo render functions (defined in their own translation units).
#define DEMO(n) int render_##n(unsigned, unsigned, const std::vector<double>&, const char*);
DEMO(simple_line)
DEMO(perspective)
DEMO(conv_dash_marker)
DEMO(bspline)
DEMO(lion)
DEMO(lion_outline)
DEMO(lion_lens)
DEMO(simple_blur)
DEMO(rasterizers2)
DEMO(line_patterns)
DEMO(line_patterns_clip)
DEMO(image_perspective)
DEMO(image_transforms)
DEMO(image_filters)
DEMO(compositing)
DEMO(compositing2)
DEMO(flash_rasterizer)
DEMO(flash_rasterizer2)
DEMO(rounded_rect)
DEMO(rbox_ctrl)
DEMO(aa_demo)
DEMO(gouraud)
DEMO(rasterizers)
DEMO(trans_curve1)
DEMO(trans_curve2)
DEMO(conv_stroke)
DEMO(conv_contour)
DEMO(gamma_ctrl)
DEMO(gamma_correction)
DEMO(alpha_mask)
DEMO(alpha_mask2)
DEMO(idea)
DEMO(circles)
DEMO(gamma_tuner)
DEMO(gradients)
DEMO(gradient_focal)
DEMO(alpha_gradient)
DEMO(blur)
DEMO(aa_test)
DEMO(line_thickness)
DEMO(component_rendering)
DEMO(multi_clip)
DEMO(image1)
DEMO(image_alpha)
DEMO(scanline_boolean)
DEMO(rasterizer_compound)
DEMO(scanline_boolean2)
DEMO(pattern_fill)
DEMO(pattern_perspective)
DEMO(trans_polar)
DEMO(distortions)
DEMO(bezier_div)
DEMO(pattern_resample)
DEMO(alpha_mask3)
DEMO(gouraud_mesh)
DEMO(gsv_text)
DEMO(image_fltr_graph)
DEMO(image_filters2)
DEMO(blend_color)
DEMO(mol_view)
DEMO(polymorphic_renderer)
DEMO(raster_text)
DEMO(image_resample)
DEMO(graph_test)
#undef DEMO

static const demo_entry g_demos[] = {
    {"simple_line", render_simple_line},
    {"perspective", render_perspective},
    {"conv_dash_marker", render_conv_dash_marker},
    {"bspline", render_bspline},
    {"lion", render_lion},
    {"lion_outline", render_lion_outline},
    {"lion_lens", render_lion_lens},
    {"simple_blur", render_simple_blur},
    {"rasterizers2", render_rasterizers2},
    {"line_patterns", render_line_patterns},
    {"line_patterns_clip", render_line_patterns_clip},
    {"image_perspective", render_image_perspective},
    {"image_transforms", render_image_transforms},
    {"image_filters", render_image_filters},
    {"compositing", render_compositing},
    {"compositing2", render_compositing2},
    {"flash_rasterizer", render_flash_rasterizer},
    {"flash_rasterizer2", render_flash_rasterizer2},
    {"rounded_rect", render_rounded_rect},
    {"rbox_ctrl", render_rbox_ctrl},
    {"aa_demo", render_aa_demo},
    {"gouraud", render_gouraud},
    {"rasterizers", render_rasterizers},
    {"trans_curve1", render_trans_curve1},
    {"trans_curve2", render_trans_curve2},
    {"conv_stroke", render_conv_stroke},
    {"conv_contour", render_conv_contour},
    {"gamma_ctrl", render_gamma_ctrl},
    {"gamma_correction", render_gamma_correction},
    {"alpha_mask", render_alpha_mask},
    {"alpha_mask2", render_alpha_mask2},
    {"idea", render_idea},
    {"circles", render_circles},
    {"gamma_tuner", render_gamma_tuner},
    {"gradients", render_gradients},
    {"gradient_focal", render_gradient_focal},
    {"alpha_gradient", render_alpha_gradient},
    {"blur", render_blur},
    {"aa_test", render_aa_test},
    {"line_thickness", render_line_thickness},
    {"component_rendering", render_component_rendering},
    {"multi_clip", render_multi_clip},
    {"image1", render_image1},
    {"image_alpha", render_image_alpha},
    {"scanline_boolean", render_scanline_boolean},
    {"rasterizer_compound", render_rasterizer_compound},
    {"scanline_boolean2", render_scanline_boolean2},
    {"pattern_fill", render_pattern_fill},
    {"pattern_perspective", render_pattern_perspective},
    {"trans_polar", render_trans_polar},
    {"distortions", render_distortions},
    {"bezier_div", render_bezier_div},
    {"pattern_resample", render_pattern_resample},
    {"alpha_mask3", render_alpha_mask3},
    {"gouraud_mesh", render_gouraud_mesh},
    {"gsv_text", render_gsv_text},
    {"image_fltr_graph", render_image_fltr_graph},
    {"image_filters2", render_image_filters2},
    {"blend_color", render_blend_color},
    {"mol_view", render_mol_view},
    {"polymorphic_renderer", render_polymorphic_renderer},
    {"raster_text", render_raster_text},
    {"image_resample", render_image_resample},
    {"graph_test", render_graph_test},
};

static const demo_entry* find_demo(const char* name) {
    for (const auto& d : g_demos) {
        if (strcmp(name, d.name) == 0) return &d;
    }
    return nullptr;
}

// In-process benchmark mode: time just the render call (no file output), so the
// numbers are not dominated by process startup or disk I/O.
// Usage: agg-render bench <demo> <width> <height> [params...] [--iters N]
static int run_bench(int argc, char** argv) {
    if (argc < 5) {
        fprintf(stderr, "Usage: %s bench <demo> <width> <height> [params...] [--iters N]\n", argv[0]);
        return 2;
    }
    const char* demo = argv[2];
    unsigned width = (unsigned)strtoul(argv[3], nullptr, 10);
    unsigned height = (unsigned)strtoul(argv[4], nullptr, 10);
    std::vector<double> params;
    int iters = 10;
    for (int i = 5; i < argc; ++i) {
        if (strcmp(argv[i], "--iters") == 0 && i + 1 < argc) {
            iters = (int)strtol(argv[i + 1], nullptr, 10);
            ++i;
        } else {
            params.push_back(strtod(argv[i], nullptr));
        }
    }
    if (iters < 1) {
        fprintf(stderr, "--iters must be at least 1\n");
        return 2;
    }

    const demo_entry* d = find_demo(demo);
    if (!d) {
        fprintf(stderr, "Unknown demo: %s\n", demo);
        return 1;
    }

    // Passing a null output path renders into the demo's in-memory buffer and
    // skips the file write (see headless::write_raw). `sink` keeps the optimizer
    // from discarding the render calls.
    volatile int sink = 0;

    // 2 untimed warmup iterations.
    for (int w = 0; w < 2; ++w) sink += d->fn(width, height, params, nullptr);

    std::vector<double> times;
    times.reserve((size_t)iters);
    for (int it = 0; it < iters; ++it) {
        auto start = std::chrono::steady_clock::now();
        int rc = d->fn(width, height, params, nullptr);
        auto end = std::chrono::steady_clock::now();
        sink += rc;
        double ms = std::chrono::duration<double, std::milli>(end - start).count();
        times.push_back(ms);
        printf("iter %3d: %.2f ms\n", it + 1, ms);
    }
    (void)sink;

    std::vector<double> sorted = times;
    std::sort(sorted.begin(), sorted.end());
    double best = sorted.front();
    size_t n = sorted.size();
    double median = (n % 2 == 1) ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    double sum = 0.0;
    for (double t : times) sum += t;
    double mean = sum / (double)n;
    printf("best= %.2f ms  median= %.2f ms  mean= %.2f ms\n", best, median, mean);
    return 0;
}

int main(int argc, char** argv) {
    if (argc >= 2 && strcmp(argv[1], "bench") == 0) {
        return run_bench(argc, argv);
    }
    if (argc < 5) {
        fprintf(stderr, "Usage: %s <demo> <width> <height> <output.raw> [params...]\n", argv[0]);
        fprintf(stderr, "Demos:");
        for (const auto& d : g_demos) fprintf(stderr, " %s", d.name);
        fprintf(stderr, "\n");
        return 2;
    }
    const char* demo = argv[1];
    unsigned width = (unsigned)strtoul(argv[2], nullptr, 10);
    unsigned height = (unsigned)strtoul(argv[3], nullptr, 10);
    const char* out = argv[4];
    std::vector<double> params;
    for (int i = 5; i < argc; ++i) params.push_back(strtod(argv[i], nullptr));

    for (const auto& d : g_demos) {
        if (strcmp(demo, d.name) == 0) {
            return d.fn(width, height, params, out);
        }
    }
    fprintf(stderr, "Unknown demo: %s\n", demo);
    return 1;
}
