cbuffer TransformBuffer : register(b0)
{
    row_major float4x4 ModelView;
    row_major float4x4 Projection;
};

Texture2D diffuseTexture : register(t0);
SamplerState textureSampler : register(s0);

struct VS_INPUT
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR;
};

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    float4 worldPos = mul(float4(input.Position, 1.0), ModelView);
    output.Position = mul(worldPos, Projection);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    float4 texColor = diffuseTexture.Sample(textureSampler, input.TexCoord);
    return texColor * input.Color;
}
