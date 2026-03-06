cbuffer TransformBuffer : register(b0)
{
    row_major float4x4 ModelView;
    row_major float4x4 Projection;
};

cbuffer LightBuffer : register(b1)
{
    float4 Light0Position;
    float4 Light0Ambient;
    float4 Light0Diffuse;
    float4 Light1Position;
    float4 Light1Ambient;
    float4 Light1Diffuse;
    float4 LightFlags;
};

struct VS_INPUT
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float4 vertColor : COLOR;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float3 ViewNormal : TEXCOORD0;
    float4 Color : COLOR;
};

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    float4 viewPosition = mul(float4(input.Position, 1.0), ModelView);
    output.Position = mul(viewPosition, Projection);
    output.ViewNormal = normalize(mul(float4(input.Normal, 0.0), ModelView).xyz);
    output.Color = input.vertColor;
    return output;
}

float3 ApplyLighting(float3 baseColor, float3 viewNormal)
{
    float3 normal = normalize(viewNormal);
    float3 litColor = baseColor * 0.2;

    if (LightFlags.x > 0.5)
    {
        float diffuse = max(0.0, dot(normal, normalize(Light0Position.xyz)));
        litColor += baseColor * (Light0Ambient.rgb + Light0Diffuse.rgb * diffuse);
    }

    if (LightFlags.y > 0.5)
    {
        float diffuse = max(0.0, dot(normal, normalize(Light1Position.xyz)));
        litColor += baseColor * (Light1Ambient.rgb + Light1Diffuse.rgb * diffuse);
    }

    return saturate(litColor);
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    return float4(ApplyLighting(input.Color.rgb, input.ViewNormal), input.Color.a);
}
