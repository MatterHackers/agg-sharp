#include <metal_stdlib>
using namespace metal;

struct PixelInput
{
    float4 Position[[position]];
    float4 Color;
};

fragment float4 FS(PixelInput input[[stage_in]])
{
    return input.Color;
}