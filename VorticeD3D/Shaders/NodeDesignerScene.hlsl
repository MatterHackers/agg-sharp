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

cbuffer SceneEffectBuffer : register(b2)
{
    float4 MeshColor;
    float4 WireframeColor;
    float4 EffectFlags;
    float4 ResolutionAndWidth;
};

Texture2D diffuseTexture : register(t0);
Texture2D opaqueDepthTexture : register(t1);
Texture2D nearDepthTexture : register(t2);

SamplerState linearSampler : register(s0);
SamplerState pointSampler : register(s1);

struct VS_INPUT
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    uint VertexId : SV_VertexID;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float3 ViewNormal : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float3 Barycentric : TEXCOORD2;
};

float3 GetBarycentric(uint vertexId)
{
    uint localVertex = vertexId % 3;
    if (localVertex == 0) return float3(1.0, 0.0, 0.0);
    if (localVertex == 1) return float3(0.0, 1.0, 0.0);
    return float3(0.0, 0.0, 1.0);
}

PS_INPUT SceneVS(VS_INPUT input)
{
    PS_INPUT output;
    float4 viewPosition = mul(float4(input.Position, 1.0), ModelView);
    output.Position = mul(viewPosition, Projection);
    output.ViewNormal = normalize(mul(float4(input.Normal, 0.0), ModelView).xyz);
    output.TexCoord = input.TexCoord;
    output.Barycentric = GetBarycentric(input.VertexId);
    return output;
}

float WireframeEdge(float3 barycentric, float width)
{
    float3 derivatives = fwidth(barycentric);
    float3 edge = smoothstep(float3(0.0, 0.0, 0.0), derivatives * max(width, 0.375), barycentric);
    return 1.0 - min(min(edge.x, edge.y), edge.z);
}

void ApplyDepthPeeling(float4 position)
{
    if (EffectFlags.z < 0.5)
    {
        return;
    }

    float2 screenUv = position.xy / ResolutionAndWidth.xy;
    float opaqueDepth = opaqueDepthTexture.Sample(pointSampler, screenUv).r;
    if (opaqueDepth < position.z)
    {
        discard;
    }

    if (EffectFlags.w < 0.5)
    {
        float nearDepth = nearDepthTexture.Sample(pointSampler, screenUv).r;
        if (nearDepth >= position.z - 1e-5)
        {
            discard;
        }
    }
}

float3 ApplyLighting(float3 baseColor, float3 viewNormal)
{
    float3 normal = normalize(viewNormal);
    float3 litColor = baseColor * 0.2;

    if (LightFlags.x > 0.5)
    {
        float3 lightDirection = normalize(Light0Position.xyz);
        float diffuse = max(0.0, dot(normal, lightDirection));
        litColor += baseColor * (Light0Ambient.rgb + Light0Diffuse.rgb * diffuse);
    }

    if (LightFlags.y > 0.5)
    {
        float3 lightDirection = normalize(Light1Position.xyz);
        float diffuse = max(0.0, dot(normal, lightDirection));
        litColor += baseColor * (Light1Ambient.rgb + Light1Diffuse.rgb * diffuse);
    }

    return saturate(litColor);
}

float4 ComposeSceneColor(float4 shadedColor, float3 barycentric)
{
    if (EffectFlags.x < 0.5)
    {
        return shadedColor;
    }

    float edge = WireframeEdge(barycentric, ResolutionAndWidth.z);
    float4 wireColor = WireframeColor;
    if (EffectFlags.y > 0.5)
    {
        return float4(wireColor.rgb, edge * shadedColor.a);
    }

    return float4(lerp(shadedColor.rgb, wireColor.rgb, edge), shadedColor.a);
}

float4 SceneColorPS(PS_INPUT input) : SV_TARGET
{
    ApplyDepthPeeling(input.Position);
    float4 baseColor = MeshColor;
    float3 litColor = ApplyLighting(baseColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(litColor, baseColor.a), input.Barycentric);
}

float4 SceneTexturePS(PS_INPUT input) : SV_TARGET
{
    ApplyDepthPeeling(input.Position);
    float4 sampledColor = diffuseTexture.Sample(linearSampler, input.TexCoord) * MeshColor;
    float3 litColor = ApplyLighting(sampledColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(litColor, sampledColor.a), input.Barycentric);
}

float4 SelectionMaskPS(PS_INPUT input) : SV_TARGET
{
    return MeshColor;
}

float4 DepthOnlyPS(PS_INPUT input) : SV_TARGET
{
    discard;
    return 0.0;
}
