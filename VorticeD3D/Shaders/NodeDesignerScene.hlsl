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
Texture2D dualDepthTexture : register(t2);

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

struct DualPeelOutput
{
    float2 DepthRange : SV_TARGET0;
    float4 FrontColor : SV_TARGET1;
    float4 BackColor : SV_TARGET2;
};

static const float DepthPeelBias = 1e-5;

float2 GetScreenUv(float4 position)
{
    return position.xy / ResolutionAndWidth.xy;
}

void ApplyDepthPeeling(float4 position)
{
    if (EffectFlags.z < 0.5)
    {
        return;
    }

    float2 screenUv = GetScreenUv(position);
    float opaqueDepth = opaqueDepthTexture.Sample(pointSampler, screenUv).r;
    if (opaqueDepth < position.z - DepthPeelBias)
    {
        discard;
    }

    if (EffectFlags.w < 0.5)
    {
        float nearDepth = dualDepthTexture.Sample(pointSampler, screenUv).r;
        if (nearDepth >= position.z - DepthPeelBias)
        {
            discard;
        }
    }
}

bool RejectBehindOpaque(float4 position)
{
    float2 screenUv = GetScreenUv(position);
    float opaqueDepth = opaqueDepthTexture.Sample(pointSampler, screenUv).r;
    return opaqueDepth < position.z - DepthPeelBias;
}

void DiscardIfInvisible(float alpha)
{
    if (alpha <= DepthPeelBias)
    {
        discard;
    }
}

float GetEffectiveTextureAlpha(float2 texCoord)
{
    return diffuseTexture.Sample(linearSampler, texCoord).a * MeshColor.a;
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

DualPeelOutput CreateEmptyDualPeelOutput()
{
    DualPeelOutput output;
    output.DepthRange = float2(-1.0, -1.0);
    output.FrontColor = float4(0.0, 0.0, 0.0, 0.0);
    output.BackColor = float4(0.0, 0.0, 0.0, 0.0);
    return output;
}

DualPeelOutput ApplyDualDepthPeeling(float4 position, float4 shadedColor)
{
    if (RejectBehindOpaque(position))
    {
        discard;
    }

    float2 screenUv = GetScreenUv(position);
    float2 previousDepth = dualDepthTexture.Sample(pointSampler, screenUv).rg;
    float frontDepth = -previousDepth.x;
    float backDepth = previousDepth.y;
    float currentDepth = position.z;

    DualPeelOutput output = CreateEmptyDualPeelOutput();

    if (currentDepth + DepthPeelBias < frontDepth || currentDepth - DepthPeelBias > backDepth)
    {
        discard;
    }

    if (currentDepth - DepthPeelBias > frontDepth && currentDepth + DepthPeelBias < backDepth)
    {
        output.DepthRange = float2(-currentDepth, currentDepth);
        return output;
    }

    if (abs(currentDepth - frontDepth) <= DepthPeelBias)
    {
        output.FrontColor = float4(shadedColor.rgb * shadedColor.a, shadedColor.a);
    }
    else
    {
        output.BackColor = shadedColor;
    }

    return output;
}

float4 SceneColorPS(PS_INPUT input) : SV_TARGET
{
    ApplyDepthPeeling(input.Position);
    float4 baseColor = MeshColor;
    DiscardIfInvisible(baseColor.a);
    float3 litColor = ApplyLighting(baseColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(litColor, baseColor.a), input.Barycentric);
}

float4 SceneTexturePS(PS_INPUT input) : SV_TARGET
{
    ApplyDepthPeeling(input.Position);
    float4 sampledColor = diffuseTexture.Sample(linearSampler, input.TexCoord) * MeshColor;
    DiscardIfInvisible(sampledColor.a);
    float3 litColor = ApplyLighting(sampledColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(litColor, sampledColor.a), input.Barycentric);
}

float2 DualDepthInitPS(PS_INPUT input) : SV_TARGET0
{
    DiscardIfInvisible(GetEffectiveTextureAlpha(input.TexCoord));

    if (RejectBehindOpaque(input.Position))
    {
        discard;
    }

    return float2(-input.Position.z, input.Position.z);
}

DualPeelOutput SceneColorDualPeelPS(PS_INPUT input)
{
    float4 baseColor = MeshColor;
    DiscardIfInvisible(baseColor.a);
    float3 litColor = ApplyLighting(baseColor.rgb, input.ViewNormal);
    float4 shadedColor = ComposeSceneColor(float4(litColor, baseColor.a), input.Barycentric);
    return ApplyDualDepthPeeling(input.Position, shadedColor);
}

DualPeelOutput SceneTextureDualPeelPS(PS_INPUT input)
{
    float4 sampledColor = diffuseTexture.Sample(linearSampler, input.TexCoord) * MeshColor;
    DiscardIfInvisible(sampledColor.a);
    float3 litColor = ApplyLighting(sampledColor.rgb, input.ViewNormal);
    float4 shadedColor = ComposeSceneColor(float4(litColor, sampledColor.a), input.Barycentric);
    return ApplyDualDepthPeeling(input.Position, shadedColor);
}

float4 SelectionMaskPS(PS_INPUT input) : SV_TARGET
{
    return MeshColor;
}

float4 DepthOnlyPS(PS_INPUT input) : SV_TARGET
{
    DiscardIfInvisible(GetEffectiveTextureAlpha(input.TexCoord));
    return 0.0;
}
