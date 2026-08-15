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
    float4 ExtraFlags; // x = useVertexColor, y = alphaMultiplier, z = isBedGrid, w = bed shadow strength
    float4 BedGridBounds; // xy = bed left/bottom in mm, zw = bed width/height in mm
    float4 BedGridColor;
    float4 BedAxisColorX; // the horizontal line at world y == 0
    float4 BedAxisColorY; // the vertical line at world x == 0
    float4 BedAxisColorZ; // the short stub at the origin
    float4 BedGridParams; // x = spacing mm, y = grid half width px, z = axis half width px, w = axis height mm
    float4 BedGridShadowColor; // matches BedShadowColor in NodeDesignerPostProcess.hlsl
};

Texture2D diffuseTexture : register(t0);
Texture2D opaqueDepthTexture : register(t1);
Texture2D dualDepthTexture : register(t2);
Texture2D bedShadowTexture : register(t3); // blurred bed shadow mask, bound only for bed draws

SamplerState linearSampler : register(s0);
SamplerState pointSampler : register(s1);

struct VS_INPUT
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float3 EdgeHints : TEXCOORD1;
    float4 VertexColor : COLOR0;
    uint VertexId : SV_VertexID;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float3 ViewNormal : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float3 Barycentric : TEXCOORD2;
    float3 EdgeHints : TEXCOORD3;
    float4 VertexColor : COLOR0;
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
    output.EdgeHints = input.EdgeHints;
    output.VertexColor = input.VertexColor;
    return output;
}

struct SELECTION_VS_INPUT
{
    float3 Position : POSITION;
};

float4 SelectionVS(SELECTION_VS_INPUT input) : SV_POSITION
{
    float4 viewPosition = mul(float4(input.Position, 1.0), ModelView);
    return mul(viewPosition, Projection);
}

float3 WireframeEdgeFactors(float3 barycentric, float width)
{
    float3 derivatives = fwidth(barycentric);
    return 1.0 - smoothstep(float3(0.0, 0.0, 0.0), derivatives * max(width, 0.375), barycentric);
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

float4 ComposeSceneColor(float4 shadedColor, float3 barycentric, float3 edgeHints)
{
    if (EffectFlags.x < 0.5)
    {
        return shadedColor;
    }

    float3 edgeFactors = WireframeEdgeFactors(barycentric, ResolutionAndWidth.z);
    float3 visibleEdges = edgeFactors * step(float3(0.5, 0.5, 0.5), edgeHints);
    float edge = max(max(visibleEdges.x, visibleEdges.y), visibleEdges.z);

    if (edge <= 1e-5)
    {
        if (EffectFlags.y > 0.5)
        {
            discard;
        }

        return shadedColor;
    }

    float3 highlightedEdges = edgeFactors * step(float3(1.5, 1.5, 1.5), edgeHints);
    float highlight = max(max(highlightedEdges.x, highlightedEdges.y), highlightedEdges.z);
    float4 wireColor = WireframeColor;
    if (highlight > 1e-5)
    {
        wireColor.rgb = float3(1.0, 0.0, 0.0);
    }

    if (EffectFlags.y > 0.5)
    {
        return float4(wireColor.rgb, edge * max(shadedColor.a, wireColor.a));
    }

    return float4(lerp(shadedColor.rgb, wireColor.rgb, edge), max(shadedColor.a, edge * wireColor.a));
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

float4 GetEffectiveColor(float4 vertexColor)
{
    float4 color;
    // When useVertexColor flag is set, use per-vertex face colors (including alpha)
    if (ExtraFlags.x > 0.5)
    {
        color = vertexColor;
    }
    else
    {
        color = MeshColor;
    }
    // Apply alpha multiplier to scale transparency (e.g. for subtract component preview)
    color.a *= ExtraFlags.y;
    return color;
}

// Composites one analytic line over the bed. The baked lines it replaces were fully
// opaque over an 80/255 fill, so the line has to raise alpha the same way or the bed
// grid would read as translucent where the texture used to be solid.
float4 BlendBedLine(float4 color, float4 lineColor, float coverage)
{
    float weight = saturate(coverage) * lineColor.a;
    color.rgb = lerp(color.rgb, lineColor.rgb, weight);
    color.a = lerp(color.a, 1.0, weight);
    return color;
}

// Tints a line color toward the bed shadow color exactly as BedShadowCompositePS tints
// the bed base color, so a line crossing an object's shadow darkens with the fill.
float4 ShadowTintBedLine(float4 lineColor, float shadowTint)
{
    lineColor.rgb = lerp(lineColor.rgb, BedGridShadowColor.rgb, shadowTint);
    return lineColor;
}

// Draws the bed grid analytically instead of sampling it from the bed texture. Texture
// space lines are magnified under perspective and bilinearly smeared, so they can never
// stay one screen pixel wide; solving for the distance to the nearest line in world mm
// and converting through fwidth gives a constant on-screen thickness at any depth.
//
// The UV to world reconstruction is exact: the bed is a single axis aligned quad whose
// UVs span 0..1 across the bed bounds and whose transform is translation only, so
// world = bounds.origin + uv * bounds.size holds at every pixel.
float4 ApplyBedGrid(float4 baseColor, float2 uv)
{
    if (ExtraFlags.z < 0.5)
    {
        return baseColor;
    }

    float2 world = BedGridBounds.xy + uv * BedGridBounds.zw;
    float2 deriv = max(fwidth(world), 1e-6); // mm per screen pixel
    float spacing = max(BedGridParams.x, 1e-6);

    // The baked lines were part of the bed base texture, so BedShadowCompositePS tinted
    // them along with the fill. Analytic lines are drawn after that composite, so sample
    // the same blurred mask (with the same v flip, since the mask is rendered upside down
    // relative to the bed texture) and apply the identical tint to every line color.
    float2 shadowUv = float2(uv.x, 1.0 - uv.y);
    float shadowAmount = saturate(bedShadowTexture.Sample(linearSampler, shadowUv).a * ExtraFlags.w);
    float shadowTint = saturate(shadowAmount * BedGridShadowColor.a);

    // Each line family is only unresolvable along its own axis: at a grazing view the
    // lines running toward the horizon crowd together while the ones crossing them stay
    // perfectly readable, so fade the two families independently.
    float2 gridPx = abs(frac(world / spacing + 0.5) - 0.5) * spacing / deriv;
    float2 spacingPx = spacing / deriv;
    float2 gridCoverage = saturate(BedGridParams.y + 0.5 - gridPx)
        * saturate((spacingPx - 3.0) / 3.0);

    float4 color = BlendBedLine(baseColor, ShadowTintBedLine(BedGridColor, shadowTint), max(gridCoverage.x, gridCoverage.y));

    // Match the baked draw order: grid, then the Y axis, then the X axis, then the Z stub.
    float axisHalfPx = BedGridParams.z;
    float axisYCoverage = saturate(axisHalfPx + 0.5 - abs(world.x) / deriv.x);
    color = BlendBedLine(color, ShadowTintBedLine(BedAxisColorY, shadowTint), axisYCoverage);

    float axisXCoverage = saturate(axisHalfPx + 0.5 - abs(world.y) / deriv.y);
    color = BlendBedLine(color, ShadowTintBedLine(BedAxisColorX, shadowTint), axisXCoverage);

    // The Z axis has nowhere to go on a flat bed, so it shows as a short bar at the
    // origin covering the same +/- AxisHeight mm the baked texture used. Feather the two
    // ends over a pixel so they read the same as the anti-aliased sides.
    float zEndCoverage = saturate((BedGridParams.w - abs(world.y)) / deriv.y + 0.5);
    color = BlendBedLine(color, ShadowTintBedLine(BedAxisColorZ, shadowTint), axisYCoverage * zEndCoverage);

    return color;
}

float4 SceneColorPS(PS_INPUT input) : SV_TARGET
{
    ApplyDepthPeeling(input.Position);
    float4 baseColor = GetEffectiveColor(input.VertexColor);
    DiscardIfInvisible(baseColor.a);
    float3 color = ResolutionAndWidth.w > 0.5 ? baseColor.rgb : ApplyLighting(baseColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(color, baseColor.a), input.Barycentric, input.EdgeHints);
}

float4 SceneTexturePS(PS_INPUT input) : SV_TARGET
{
    ApplyDepthPeeling(input.Position);
    float4 effectiveColor = GetEffectiveColor(input.VertexColor);
    float4 sampledColor = diffuseTexture.Sample(linearSampler, input.TexCoord) * effectiveColor;
    DiscardIfInvisible(sampledColor.a);
    float3 color = ResolutionAndWidth.w > 0.5 ? sampledColor.rgb : ApplyLighting(sampledColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(color, sampledColor.a), input.Barycentric, input.EdgeHints);
}

float4 SceneColorAlphaBlendPS(PS_INPUT input) : SV_TARGET
{
    float4 baseColor = GetEffectiveColor(input.VertexColor);
    DiscardIfInvisible(baseColor.a);
    float3 color = ResolutionAndWidth.w > 0.5 ? baseColor.rgb : ApplyLighting(baseColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(color, baseColor.a), input.Barycentric, input.EdgeHints);
}

float4 SceneTextureAlphaBlendPS(PS_INPUT input) : SV_TARGET
{
    float4 effectiveColor = GetEffectiveColor(input.VertexColor);
    float4 sampledColor = diffuseTexture.Sample(linearSampler, input.TexCoord) * effectiveColor;
    DiscardIfInvisible(sampledColor.a);
    sampledColor = ApplyBedGrid(sampledColor, input.TexCoord);
    float3 color = ResolutionAndWidth.w > 0.5 ? sampledColor.rgb : ApplyLighting(sampledColor.rgb, input.ViewNormal);
    return ComposeSceneColor(float4(color, sampledColor.a), input.Barycentric, input.EdgeHints);
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
    float4 baseColor = GetEffectiveColor(input.VertexColor);
    DiscardIfInvisible(baseColor.a);
    float3 color = ResolutionAndWidth.w > 0.5 ? baseColor.rgb : ApplyLighting(baseColor.rgb, input.ViewNormal);
    float4 shadedColor = ComposeSceneColor(float4(color, baseColor.a), input.Barycentric, input.EdgeHints);
    return ApplyDualDepthPeeling(input.Position, shadedColor);
}

DualPeelOutput SceneTextureDualPeelPS(PS_INPUT input)
{
    float4 effectiveColor = GetEffectiveColor(input.VertexColor);
    float4 sampledColor = diffuseTexture.Sample(linearSampler, input.TexCoord) * effectiveColor;
    DiscardIfInvisible(sampledColor.a);
    sampledColor = ApplyBedGrid(sampledColor, input.TexCoord);
    float3 color = ResolutionAndWidth.w > 0.5 ? sampledColor.rgb : ApplyLighting(sampledColor.rgb, input.ViewNormal);
    float4 shadedColor = ComposeSceneColor(float4(color, sampledColor.a), input.Barycentric, input.EdgeHints);
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
