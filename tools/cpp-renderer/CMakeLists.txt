cmake_minimum_required(VERSION 3.15)
project(agg_render CXX)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release)
endif()

# The original AGG C++ sources are not vendored here; point AGG_ROOT at a checkout
# (by default the copy agg-rust keeps under cpp-references/agg-src).
set(AGG_ROOT "$ENV{HOME}/Development/rust-apps/agg-rust/cpp-references/agg-src"
    CACHE PATH "Root of the original AGG C++ sources (holds include/, src/, examples/)")
if(NOT EXISTS "${AGG_ROOT}/include/agg_basics.h")
    message(FATAL_ERROR "AGG sources not found at AGG_ROOT='${AGG_ROOT}'. "
        "Pass -DAGG_ROOT=/path/to/agg-src (see README.md).")
endif()

# patches/ holds copies of the few AGG headers and sources the goldens need fixed (see README.md): the
# headers come first on the include path so they shadow the originals, and a patched .cpp is compiled in
# place of its original. Each copy was taken from one exact original (listed by its path under AGG_ROOT):
# if AGG_ROOT's differs, the copy would silently replace a different file, so the build stops instead.
set(AGG_PATCHED_ORIGINALS
    "include/agg_blur.h=35772fc6edd3f9bbd5bd30c7ab7b6c32e7d3cc649e06ff2eea3cc9d0a68dc9c0"
    "include/agg_gradient_lut.h=d0ff30e078ba5ae1f91cd3c8e71d8a9d77bae2df64995fc14ca2da23cf9ea0d8"
    "include/agg_gsv_text.h=5af795fc4e0f9bb7e8a5c1d7378593e72f6cab7135998d47a60757696201ed04"
    "include/agg_pattern_filters_rgba.h=cb13614684ed2e3b7d401827f2823bc3d3b693045e283dad9f25284364f800e5"
    "include/agg_pixfmt_amask_adaptor.h=701941835ed5749e3c1d067681309552b596c10197d3d51cae0fade54a73c91b"
    "include/agg_pixfmt_rgb.h=667b029005f78bd5effc44b40e55b4c38613cd8867ee33ee5c003c9d2ef8a7d9"
    "include/agg_pixfmt_rgba.h=d0a59e0f1974dfd72a8a870ce172c436c79e508df168b891281f3fed7ca74464"
    "include/agg_renderer_markers.h=21535691708ba842ad42500fcc908ef7030f3c34046fc1d8865db31b3c255571"
    "include/agg_shorten_path.h=248f468bb9b0bd578278c1833dc48d308a22917f1431b6479d71173ed5e2e9a4"
    "include/agg_span_image_filter_rgb.h=66a11afda5e4b95a47424c659775fb951e6eefbdbe47b3f7c7a357f59038cbb9"
    "include/agg_span_image_filter_rgba.h=af6b1a151e6cea7fb5779fcdbb34cc58af09b264f3a207b16ce25a648374bc0e"
    "src/agg_gsv_text.cpp=5f2eb0dab300a64951f97322e39b2302b8138c6f4d24335f91d3ba37e844f1a3"
    "src/agg_trans_warp_magnifier.cpp=36529044b7da5aa7164fb3ac24297b06e9504d387bcae2f0f1500df9fbe2f17a"
)
foreach(entry ${AGG_PATCHED_ORIGINALS})
    string(REPLACE "=" ";" entry_parts "${entry}")
    list(GET entry_parts 0 patched_original)
    list(GET entry_parts 1 expected_sha256)
    get_filename_component(patched_name "${patched_original}" NAME)
    file(SHA256 "${AGG_ROOT}/${patched_original}" actual_sha256)
    if(NOT actual_sha256 STREQUAL expected_sha256)
        message(FATAL_ERROR "${AGG_ROOT}/${patched_original} is not the version "
            "patches/${patched_name} was made from (SHA256 ${actual_sha256}, expected ${expected_sha256}). "
            "Re-apply the 'agg-sharp patch' changes to a fresh copy of the new file and update its hash "
            "in AGG_PATCHED_ORIGINALS (see README.md).")
    endif()
endforeach()
include_directories(
    "${CMAKE_CURRENT_SOURCE_DIR}/patches"
    "${AGG_ROOT}/include"
    "${AGG_ROOT}/examples"
)

# Core AGG implementation files (everything under src/ except the platform/ GUI
# backends, which we replace with a headless harness).
file(GLOB AGG_CORE_SRC "${AGG_ROOT}/src/*.cpp")
# Patched sources replace their originals.
file(GLOB AGG_PATCHED_SRC "${CMAKE_CURRENT_SOURCE_DIR}/patches/*.cpp")
foreach(patched_src ${AGG_PATCHED_SRC})
    get_filename_component(patched_name "${patched_src}" NAME)
    list(REMOVE_ITEM AGG_CORE_SRC "${AGG_ROOT}/src/${patched_name}")
endforeach()
file(GLOB AGG_CTRL_SRC "${AGG_ROOT}/src/ctrl/*.cpp")

# Example helper translation units reused by the demos.
set(AGG_EXAMPLE_HELPERS
    "${AGG_ROOT}/examples/parse_lion.cpp"
    "${AGG_ROOT}/examples/interactive_polygon.cpp"
    "${AGG_ROOT}/examples/make_gb_poly.cpp"
    "${AGG_ROOT}/examples/make_arrows.cpp"
)

file(GLOB RENDERER_SRC "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp")

add_executable(agg-render
    ${RENDERER_SRC}
    ${AGG_CORE_SRC}
    ${AGG_PATCHED_SRC}
    ${AGG_CTRL_SRC}
    ${AGG_EXAMPLE_HELPERS}
)

# Demo data assets (shapes.txt, spheres.ppm, compositing.ppm live under art/;
# spheres.bmp is agg-sharp's own copy under examples/).
target_compile_definitions(agg-render PRIVATE
    AGG_ASSET_DIR="${AGG_ROOT}/examples/art"
    AGG_SPHERES_BMP="${CMAKE_CURRENT_SOURCE_DIR}/../../examples/spheres.bmp"
    AGG_LINE_PATTERNS_DIR="${CMAKE_CURRENT_SOURCE_DIR}/../../examples/AggSharpDemo/AggSharpDemo/Images/LinePatterns"
    AGG_MOL_VIEW_SDF="${CMAKE_CURRENT_SOURCE_DIR}/../../examples/AggSharpDemo/AggSharpDemo/Art/1.sdf")

if(MSVC)
    target_compile_options(agg-render PRIVATE /wd4244 /wd4267 /wd4996 /EHsc)
    target_compile_definitions(agg-render PRIVATE _CRT_SECURE_NO_WARNINGS)
else()
    # clang (and gcc on arm64) fuse a * b + c into one FMA instruction by default, rounding once where the
    # C++ source (and .NET, which never contracts on its own) rounds twice. conv_bspline and the curves
    # after conv_smooth_poly1 land a few pixels one level off that way, so the goldens are built unfused.
    target_compile_options(agg-render PRIVATE -ffp-contract=off)
endif()
