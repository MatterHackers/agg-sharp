#include <metal_stdlib>
using namespace metal;

struct VertexInput
{
    float2 Position[[attribute(0)]];
    float4 Color[[attribute(1)]];
};

struct PixelInput
{
    float4 Position[[position]];
    float4 Color;
};

vertex PixelInput VS(VertexInput input[[stage_in]])
{
    PixelInput output;
    output.Position = float4(input.Position, 0, 1);
    output.Color = input.Color;
    return output;
}