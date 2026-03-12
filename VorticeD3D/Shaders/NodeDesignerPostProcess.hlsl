cbuffer OutlineCompositeBuffer : register(b0)
{
    float4 OutlineSettings;
    float4 OutlinePadding;
};

cbuffer BedShadowPostProcessBuffer : register(b1)
{
    float4 BedShadowSettings;
};

Texture2D texture0 : register(t0);
Texture2D texture1 : register(t1);
Texture2D texture2 : register(t2);
Texture2D texture3 : register(t3);

SamplerState pointSampler : register(s0);
SamplerState linearSampler : register(s1);

struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT FullScreenVS(uint vertexId : SV_VertexID)
{
    VS_OUTPUT output;
    float2 texCoord = float2((vertexId << 1) & 2, vertexId & 2);
    output.TexCoord = texCoord;
    output.Position = float4(texCoord * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}

float4 CopyTexturePS(VS_OUTPUT input) : SV_TARGET
{
    return texture0.Sample(pointSampler, input.TexCoord);
}

float4 BedShadowBlurPS(VS_OUTPUT input) : SV_TARGET
{
    float2 direction = BedShadowSettings.xy;
    float4 color = texture0.Sample(linearSampler, input.TexCoord) * 0.227027f;
    color += texture0.Sample(linearSampler, input.TexCoord + direction * 1.384615f) * 0.316216f;
    color += texture0.Sample(linearSampler, input.TexCoord - direction * 1.384615f) * 0.316216f;
    color += texture0.Sample(linearSampler, input.TexCoord + direction * 3.230769f) * 0.070270f;
    color += texture0.Sample(linearSampler, input.TexCoord - direction * 3.230769f) * 0.070270f;
    return color;
}

float4 BedShadowCompositePS(VS_OUTPUT input) : SV_TARGET
{
    float4 baseColor = texture0.Sample(linearSampler, input.TexCoord);
    float2 shadowUv = float2(input.TexCoord.x, 1.0f - input.TexCoord.y);
    float shadowAmount = saturate(texture1.Sample(linearSampler, shadowUv).a * BedShadowSettings.z);
    return float4(baseColor.rgb * (1.0f - shadowAmount), baseColor.a);
}

float4 ResolveDualPeelPS(VS_OUTPUT input) : SV_TARGET
{
    float4 sceneColor = texture0.Sample(pointSampler, input.TexCoord);
    float4 frontAccum = texture1.Sample(pointSampler, input.TexCoord);
    float4 backAccum = texture2.Sample(pointSampler, input.TexCoord);
    float4 transparentOverlay = texture3.Sample(pointSampler, input.TexCoord);

    float remainingTransmittance = saturate(frontAccum.a * (1.0 - backAccum.a));
    float transparentAlpha = 1.0 - remainingTransmittance;
    float combinedAlpha = sceneColor.a + (1.0 - sceneColor.a) * transparentAlpha;
    float sceneWeight = sceneColor.a * remainingTransmittance;
    float3 premultipliedColor = frontAccum.rgb + frontAccum.a * backAccum.rgb + sceneWeight * sceneColor.rgb;
    if (combinedAlpha <= 1e-6)
    {
        return 0.0;
    }

    float4 resolvedColor = float4(premultipliedColor / combinedAlpha, combinedAlpha);
    float overlayWeight = transparentOverlay.a;
    return float4(
        lerp(resolvedColor.rgb, transparentOverlay.rgb, overlayWeight),
        resolvedColor.a + (1.0 - resolvedColor.a) * overlayWeight);
}

// Progressive accumulation: blends a new sample into the running average.
// texture0 = new jittered sample, texture1 = previous accumulation result.
cbuffer AccumulationBuffer : register(b2)
{
    float4 AccumSettings; // x = blend weight (1/N), yzw = unused
};

float4 AccumulatePS(VS_OUTPUT input) : SV_TARGET
{
    float4 newSample = texture0.Sample(pointSampler, input.TexCoord);
    float4 prevAccum = texture1.Sample(pointSampler, input.TexCoord);
    return lerp(prevAccum, newSample, AccumSettings.x);
}

float4 OutlineCompositePS(VS_OUTPUT input) : SV_TARGET
{
    float outlineWidth = OutlineSettings.x;
    float occludedAlpha = OutlineSettings.y;
    float2 resolution = OutlineSettings.zw;
    float2 texel = 1.0 / resolution;

    float4 center = texture0.Sample(pointSampler, input.TexCoord);
    float4 right = texture0.Sample(pointSampler, input.TexCoord + float2(texel.x * outlineWidth, 0.0));
    float4 left = texture0.Sample(pointSampler, input.TexCoord - float2(texel.x * outlineWidth, 0.0));
    float4 up = texture0.Sample(pointSampler, input.TexCoord + float2(0.0, texel.y * outlineWidth));
    float4 down = texture0.Sample(pointSampler, input.TexCoord - float2(0.0, texel.y * outlineWidth));
    float4 topRight = texture0.Sample(pointSampler, input.TexCoord + float2(texel.x, texel.y) * outlineWidth * 0.707);
    float4 topLeft = texture0.Sample(pointSampler, input.TexCoord + float2(-texel.x, texel.y) * outlineWidth * 0.707);
    float4 bottomRight = texture0.Sample(pointSampler, input.TexCoord + float2(texel.x, -texel.y) * outlineWidth * 0.707);
    float4 bottomLeft = texture0.Sample(pointSampler, input.TexCoord + float2(-texel.x, -texel.y) * outlineWidth * 0.707);

    bool hasNeighbor = right.a > 0.0 || left.a > 0.0 || up.a > 0.0 || down.a > 0.0 ||
        topRight.a > 0.0 || topLeft.a > 0.0 || bottomRight.a > 0.0 || bottomLeft.a > 0.0;
    bool hasEmptyNeighbor = right.a == 0.0 || left.a == 0.0 || up.a == 0.0 || down.a == 0.0 ||
        topRight.a == 0.0 || topLeft.a == 0.0 || bottomRight.a == 0.0 || bottomLeft.a == 0.0;

    if (!(hasNeighbor && hasEmptyNeighbor))
    {
        discard;
    }

    float selectedDepth = 1.0;
    if (center.a > 0.0) selectedDepth = min(selectedDepth, texture1.Sample(pointSampler, input.TexCoord).r);
    if (right.a > 0.0) selectedDepth = min(selectedDepth, texture1.Sample(pointSampler, input.TexCoord + float2(texel.x * outlineWidth, 0.0)).r);
    if (left.a > 0.0) selectedDepth = min(selectedDepth, texture1.Sample(pointSampler, input.TexCoord - float2(texel.x * outlineWidth, 0.0)).r);
    if (up.a > 0.0) selectedDepth = min(selectedDepth, texture1.Sample(pointSampler, input.TexCoord + float2(0.0, texel.y * outlineWidth)).r);
    if (down.a > 0.0) selectedDepth = min(selectedDepth, texture1.Sample(pointSampler, input.TexCoord - float2(0.0, texel.y * outlineWidth)).r);

    float sceneDepth = texture2.Sample(pointSampler, input.TexCoord).r;
    bool occluded = selectedDepth > sceneDepth + 1e-4;

    float4 outlineColor = center.a > 0.0 ? center : right.a > 0.0 ? right : left.a > 0.0 ? left : up.a > 0.0 ? up : down.a > 0.0 ? down : topRight.a > 0.0 ? topRight : topLeft.a > 0.0 ? topLeft : bottomRight.a > 0.0 ? bottomRight : bottomLeft;
    outlineColor.a = occluded ? outlineColor.a * occludedAlpha : outlineColor.a;
    return outlineColor;
}
