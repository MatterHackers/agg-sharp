# agg-render: the C++ AGG reference renderer

Renders AGG example demos headlessly with the original C++ AGG, so agg-sharp's
software renderer can be checked against it byte for byte
(`Tests/Agg.Tests/Agg.GoldenImages/AggReferenceTests.cs`). Brought over from
agg-rust's `tools/cpp-renderer`.

The AGG C++ sources are not vendored. CMake reads them from `AGG_ROOT`, which
defaults to `~/Development/rust-apps/agg-rust/cpp-references/agg-src`; pass
`-DAGG_ROOT=/path/to/agg-src` to use another checkout.

## Patched AGG sources

C++ AGG is the baseline, but where it is wrong agg-sharp fixes it, and the
goldens must pin the fixed behaviour. The external AGG checkout is never
edited: `patches/` holds copies of only the affected AGG headers (and sources), each fix
marked with an `agg-sharp patch` comment naming the C++ bug and the agg-sharp
test that pins it. `patches/` is on the include path ahead of
`AGG_ROOT/include`, so the copy shadows the original (AGG headers include
each other with quotes, which search the including file's directory first -
check that no built AGG source or header includes a patched header from
inside `AGG_ROOT` before relying on the shadow). A patched `.cpp` would be
compiled in place of its original instead.

Each patched copy records, in `AGG_PATCHED_ORIGINALS` in `CMakeLists.txt`,
its original's path under `AGG_ROOT` and the SHA256 of that original; configuring against an
`AGG_ROOT` whose header differs fails with a message naming it, rather than
shadowing a different AGG. To move to a new AGG, re-apply the `agg-sharp
patch` changes to a fresh copy of the header and update the hash.

- `agg_blur.h`: the `recursive_blur_calc_rgba`/`_rgb`/`_gray` `to_pix` round and clamp to the channel's
  range instead of truncating, so a white area stays 255 instead of coming out 253 (`RecursiveBlurTests`);
  `slight_blur::calc_value` rounds an integer channel instead of truncating, so an 8-bit pixel is not left
  up to a level dark (`SlightBlurTests`), and `calc_pixel` gains the `typename` clang requires to compile it.
- `agg_gradient_lut.h`: `gradient_lut::build_lut` sizes each segment's color interpolator
  `end - start - 1` (at least 1) instead of `end - start + 1`, so the last entry of a segment is the
  stop's color rather than two steps short of it (`GradientLutTests`).
- `agg_pixfmt_amask_adaptor.h`: `pixfmt_amask_adaptor::blend_hline`/`blend_vline` start the span at their
  cover instead of at `cover_full`, so a scanline_p8's solid span of partial cover (along a horizontal edge)
  is not drawn at full cover through the mask (`AlphaMask3DemoTests`).
- `agg_pattern_filters_rgba.h`: `pattern_filter_bilinear_rgba::pixel_high_res` starts its sums at half a unit
  (`line_subpixel_scale * line_subpixel_scale / 2`) instead of at 0, so the downshift rounds instead of
  truncating, as the image filters' do (`PatternFilterBilinearTests`).
- `agg_pixfmt_rgb.h`: `blender_rgb_gamma::blend_pix` returns at alpha 0
  instead of writing `inv(dir(p))` (`BlenderGammaBGRATests`).
- `agg_pixfmt_rgba.h`: `comp_op_rgba_src_atop` blends blue with the destination's blue, not its green
  (`d.b = s.b * d.a + d.g * s1a` was a typo); `comp_op_rgba_color_burn` keeps a full destination channel
  under a black source (`dca >= da`, where `dca > da` never held for premultiplied colors)
  (`BlenderCompOpBGRATests`). `agg_span_gradient_image.h` includes this header from inside `AGG_ROOT`, so a
  demo using it must include `agg_pixfmt_rgba.h` first.
- `agg_renderer_markers.h`: `renderer_markers::visible` builds the marker's box as `(x-r, y-r, x+r, y+r)`
  instead of `(x-r, y-r, x+y, y+r)`, which culled a marker left of the clip box whose right half reaches into
  it (`RendererMarkersTests`).
- `agg_shorten_path.h`: `shorten_path` tests the first segment against the remaining length too
  (`while(n >= 0)` instead of `while(n)`), so a shorten longer than the path empties it instead of moving
  the end point back past the start and reversing it (`ShortenPathTests`). `agg_vcgen_dash.cpp` and
  `agg_vcgen_stroke.cpp` pick up the copy through the include path; `agg_vcgen_vertex_sequence.h` includes
  the original from inside `AGG_ROOT`, so a demo using `conv_marker_adaptor` must include
  `agg_shorten_path.h` first.
- `agg_span_image_filter_rgb.h`: `span_image_filter_rgb_bilinear`, `span_image_filter_rgb_bilinear_clip`
  (both of its branches), `span_image_filter_rgb_2x2` and the general `span_image_filter_rgb` start their
  sums at half a unit (`image_subpixel_scale * image_subpixel_scale / 2`, or `image_filter_scale / 2` for
  the lookup-table filters) instead of at 0, so the downshift rounds instead of truncating
  (`SpanImageFilterRgbTests`); `span_image_resample_rgb_affine` and `span_image_resample_rgb` add half the
  total weight before dividing by it, so the result rounds instead of truncating (`SpanImageResampleRgbTests`).
- `agg_span_image_filter_rgba.h`: `span_image_filter_rgba_bilinear_clip` starts
  its sums at half a unit, so they round as the plain bilinear filter's do,
  instead of at 0 (`SpanImageFilterRgbaBilinearClipTests`); `span_image_filter_rgba_2x2`
  likewise starts at `image_filter_scale / 2` (`SpanImageFilterRgba2x2Tests`), as does the general
  lookup-table `span_image_filter_rgba`, which truncated a flat 255 to 254 (`SpanImageFilterRgbaTests`);
  `span_image_resample_rgba_affine` and `span_image_resample_rgba` add half the total weight before dividing
  by it, so the result rounds instead of truncating (`SpanImageResampleRgbaTests`).
- `agg_gsv_text.h` / `agg_gsv_text.cpp` (compiled instead of the original): `rewind` puts the pen back
  at the start point (the header gains `m_start_y` for it), where the original left it where the last
  pass ended, so reading the text a second time drew it shifted by its own width (`GsvTextTests`).
- `agg_trans_warp_magnifier.cpp` (compiled instead of the original): `transform` and
  `inverse_transform` leave the point alone for a zero radius or at the exact center, where the
  original divides 0 by 0 and returns NaN (`TransWarpMagnifierTests`).

A bug in an example's own code (not an AGG header) is fixed in its `src/demo_<name>.cpp`
rewrite, commented there and in the port:

- `line_pattern_source.h` (`demo_line_patterns.cpp`, `demo_line_patterns_clip.cpp`):
  `pattern_src_brightness_to_alpha` indexes `brightness_to_alpha` one past its end for a white pixel; the
  index is clamped to the last entry (`BrightnessToAlphaSource`).
- `demo_line_patterns.cpp`: `pattern_src_brightness_to_alpha` indexes `brightness_to_alpha` one past its
  end for a white pixel; the index is clamped to the last entry (`LinePatternsDemo`).
- `demo_image_alpha.cpp`: `span_conv_brightness_alpha` indexes its brightness table one past its end for a
  white pixel; the index is clamped to the last entry (`ImageAlphaDemo`). The spheres image has no pure white
  pixel, so the goldens do not reach it.
- `demo_mol_view.cpp`: `molecule::get_str` trims a field's leading spaces only while the field ends before the
  line does, and on an all-space field counts its length below zero and returns an unset buffer; a field is
  read as its columns clipped to the line, trimmed and cut at the first space (`MolViewDemo`). Every field
  1.sdf holds reads the same both ways, so the goldens do not reach it. The molecules are read from
  agg-sharp's copy of 1.sdf (`AGG_MOL_VIEW_SDF`), the one the port embeds.

## Regenerating the goldens

```bash
scripts/regen-agg-reference.sh                      # default AGG_ROOT
scripts/regen-agg-reference.sh -DAGG_ROOT=/path/to/agg-src
```

This builds `tools/cpp-renderer/build/agg-render` and writes
`TestData/AggReference/<demo>_<w>x<h>[_<variant>].raw`. The golden list
is the `GOLDENS` table in the script.

## Usage

```bash
agg-render <demo> <width> <height> <out.raw> [params...]
```

`.raw` is u32 LE width, u32 LE height, then top-down RGBA - row 0 is the
rendering buffer's y = 0, which is agg-sharp's y = 0 too (no flip either side).

## Adding a demo

Write `src/demo_<name>.cpp` with `int render_<name>(unsigned w, unsigned h,
const std::vector<double>& params, const char* out)` - a headless rewrite of
the example's `on_draw()` at its default state - register it in `src/main.cpp`,
and add its golden to the script.
