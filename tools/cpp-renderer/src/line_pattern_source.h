// The line pattern examples' shared pieces (line_patterns.cpp, line_patterns_clip.cpp): the
// brightness_to_alpha table, pattern_src_brightness_to_alpha, and 1.ppm..9.ppm as load_img with
// flip_y = true gives them, read from agg-sharp's copies (AGG_LINE_PATTERNS_DIR).
#pragma once

#include <algorithm>
#include <string>

#include "agg_basics.h"
#include "agg_color_rgba.h"

#include "common.h"

namespace line_patterns {


static const agg::int8u brightness_to_alpha[256 * 3] =
{
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 254, 254, 254, 254, 254, 254, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
    254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 253, 253, 
    253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 252, 
    252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 251, 251, 251, 251, 251, 
    251, 251, 251, 251, 250, 250, 250, 250, 250, 250, 250, 250, 249, 249, 249, 249, 
    249, 249, 249, 248, 248, 248, 248, 248, 248, 248, 247, 247, 247, 247, 247, 246, 
    246, 246, 246, 246, 246, 245, 245, 245, 245, 245, 244, 244, 244, 244, 243, 243, 
    243, 243, 243, 242, 242, 242, 242, 241, 241, 241, 241, 240, 240, 240, 239, 239, 
    239, 239, 238, 238, 238, 238, 237, 237, 237, 236, 236, 236, 235, 235, 235, 234, 
    234, 234, 233, 233, 233, 232, 232, 232, 231, 231, 230, 230, 230, 229, 229, 229, 
    228, 228, 227, 227, 227, 226, 226, 225, 225, 224, 224, 224, 223, 223, 222, 222, 
    221, 221, 220, 220, 219, 219, 219, 218, 218, 217, 217, 216, 216, 215, 214, 214, 
    213, 213, 212, 212, 211, 211, 210, 210, 209, 209, 208, 207, 207, 206, 206, 205, 
    204, 204, 203, 203, 202, 201, 201, 200, 200, 199, 198, 198, 197, 196, 196, 195, 
    194, 194, 193, 192, 192, 191, 190, 190, 189, 188, 188, 187, 186, 186, 185, 184, 
    183, 183, 182, 181, 180, 180, 179, 178, 177, 177, 176, 175, 174, 174, 173, 172, 
    171, 171, 170, 169, 168, 167, 166, 166, 165, 164, 163, 162, 162, 161, 160, 159, 
    158, 157, 156, 156, 155, 154, 153, 152, 151, 150, 149, 148, 148, 147, 146, 145, 
    144, 143, 142, 141, 140, 139, 138, 137, 136, 135, 134, 133, 132, 131, 130, 129, 
    128, 128, 127, 125, 124, 123, 122, 121, 120, 119, 118, 117, 116, 115, 114, 113, 
    112, 111, 110, 109, 108, 107, 106, 105, 104, 102, 101, 100,  99,  98,  97,  96,  
     95,  94,  93,  91,  90,  89,  88,  87,  86,  85,  84,  82,  81,  80,  79,  78, 
     77,  75,  74,  73,  72,  71,  70,  69,  67,  66,  65,  64,  63,  61,  60,  59, 
     58,  57,  56,  54,  53,  52,  51,  50,  48,  47,  46,  45,  44,  42,  41,  40, 
     39,  37,  36,  35,  34,  33,  31,  30,  29,  28,  27,  25,  24,  23,  22,  20, 
     19,  18,  17,  15,  14,  13,  12,  11,   9,   8,   7,   6,   4,   3,   2,   1
};

// C++ pattern_src_brightness_to_alpha, over a loaded image rather than a pixfmt.
class pattern_src_brightness_to_alpha {
public:
    typedef agg::rgba8 color_type;
    explicit pattern_src_brightness_to_alpha(const headless::image_rgba& img) : m_img(&img) {}
    unsigned width() const { return m_img->width; }
    unsigned height() const { return m_img->height; }
    color_type pixel(int x, int y) const {
        const unsigned char* p = &m_img->rgba[(static_cast<size_t>(y) * m_img->width + x) * 4];
        color_type c(p[0], p[1], p[2], 255);
        color_type::calc_type sum = c.r + c.g + c.b;
        int i = int(sum * sizeof(brightness_to_alpha) / (3 * color_type::full_value()));
        // agg-sharp fix: a white pixel (sum 765) indexes 768, one past the table, and the original reads
        // whatever byte follows it. Clamp to the last entry, the table's own value for the brightest pixels
        // (BrightnessToAlphaSource.cs does the same).
        if (i > int(sizeof(brightness_to_alpha)) - 1) i = int(sizeof(brightness_to_alpha)) - 1;
        agg::cover_type cover = brightness_to_alpha[i];
        c.a = color_type::mult_cover(color_type::full_value(), cover);
        return c;
    }
private:
    const headless::image_rgba* m_img;
};

// platform_support::load_img with flip_y = true: rbuf_img row 0 is the picture's bottom row.
inline headless::image_rgba load_pattern_flip_y(int n) {
    std::string path = std::string(AGG_LINE_PATTERNS_DIR) + "/" + std::to_string(n) + ".ppm";
    headless::image_rgba img = headless::load_ppm_rgba(path);
    if (!img.ok) return img;
    size_t stride = static_cast<size_t>(img.width) * 4;
    for (unsigned y = 0; y < img.height / 2; ++y) {
        std::swap_ranges(img.rgba.begin() + y * stride,
                         img.rgba.begin() + (y + 1) * stride,
                         img.rgba.begin() + (img.height - 1 - y) * stride);
    }
    return img;
}

} // namespace line_patterns
