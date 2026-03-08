cbuffer OutlineCompositeBuffer : register(b0)
{
    float4 OutlineSettings;
    float4 OutlinePadding;
};

cbuffer BedShadowPostProcessBuffer : register(b1)
{
    float4 BedShadowSettings;
};

cbuffer FxaaBuffer : register(b2)
{
    float4 FxaaTexelSize; // xy = 1/width, 1/height
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

// FXAA 3.11 Quality - based on Timothy Lottes' algorithm
static const float FXAA_EDGE_THRESHOLD = 0.125;
static const float FXAA_EDGE_THRESHOLD_MIN = 0.0312;
static const float FXAA_SUBPIX_QUALITY = 0.75;
static const int FXAA_SEARCH_STEPS = 12;
static const float FXAA_SEARCH_ACCELERATION[] = {
    1.0, 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0, 8.0
};

float FxaaLuma(float3 rgb)
{
    return dot(rgb, float3(0.299, 0.587, 0.114));
}

float4 FxaaPS(VS_OUTPUT input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    float2 texel = FxaaTexelSize.xy;

    float3 rgbM = texture0.Sample(linearSampler, uv).rgb;
    float3 rgbN = texture0.Sample(linearSampler, uv + float2(0, -texel.y)).rgb;
    float3 rgbS = texture0.Sample(linearSampler, uv + float2(0, texel.y)).rgb;
    float3 rgbE = texture0.Sample(linearSampler, uv + float2(texel.x, 0)).rgb;
    float3 rgbW = texture0.Sample(linearSampler, uv + float2(-texel.x, 0)).rgb;

    float lumaM = FxaaLuma(rgbM);
    float lumaN = FxaaLuma(rgbN);
    float lumaS = FxaaLuma(rgbS);
    float lumaE = FxaaLuma(rgbE);
    float lumaW = FxaaLuma(rgbW);

    float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaE, lumaW)));
    float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaE, lumaW)));
    float lumaRange = lumaMax - lumaMin;

    if (lumaRange < max(FXAA_EDGE_THRESHOLD_MIN, lumaMax * FXAA_EDGE_THRESHOLD))
    {
        return float4(rgbM, texture0.Sample(linearSampler, uv).a);
    }

    float3 rgbNW = texture0.Sample(linearSampler, uv + float2(-texel.x, -texel.y)).rgb;
    float3 rgbNE = texture0.Sample(linearSampler, uv + float2(texel.x, -texel.y)).rgb;
    float3 rgbSW = texture0.Sample(linearSampler, uv + float2(-texel.x, texel.y)).rgb;
    float3 rgbSE = texture0.Sample(linearSampler, uv + float2(texel.x, texel.y)).rgb;

    float lumaNW = FxaaLuma(rgbNW);
    float lumaNE = FxaaLuma(rgbNE);
    float lumaSW = FxaaLuma(rgbSW);
    float lumaSE = FxaaLuma(rgbSE);

    float lumaAvg = (lumaN + lumaS + lumaE + lumaW) * 0.25;
    float subpixOffset = saturate(abs(lumaAvg - lumaM) / lumaRange);
    subpixOffset = (-2.0 * subpixOffset + 3.0) * subpixOffset * subpixOffset;
    float subpixBlend = subpixOffset * subpixOffset * FXAA_SUBPIX_QUALITY;

    float edgeH = abs(lumaNW + lumaN * -2.0 + lumaNE)
        + abs(lumaW + lumaM * -2.0 + lumaE) * 2.0
        + abs(lumaSW + lumaS * -2.0 + lumaSE);
    float edgeV = abs(lumaNW + lumaW * -2.0 + lumaSW)
        + abs(lumaN + lumaM * -2.0 + lumaS) * 2.0
        + abs(lumaNE + lumaE * -2.0 + lumaSE);
    bool isHorizontal = edgeH >= edgeV;

    float luma1 = isHorizontal ? lumaN : lumaW;
    float luma2 = isHorizontal ? lumaS : lumaE;
    float gradient1 = abs(luma1 - lumaM);
    float gradient2 = abs(luma2 - lumaM);

    bool is1Steeper = gradient1 >= gradient2;
    float gradientScaled = 0.25 * max(gradient1, gradient2);

    float stepLength = isHorizontal ? texel.y : texel.x;
    float lumaLocalAvg;

    if (is1Steeper)
    {
        stepLength = -stepLength;
        lumaLocalAvg = 0.5 * (luma1 + lumaM);
    }
    else
    {
        lumaLocalAvg = 0.5 * (luma2 + lumaM);
    }

    float2 currentUV = uv;
    if (isHorizontal)
    {
        currentUV.y += stepLength * 0.5;
    }
    else
    {
        currentUV.x += stepLength * 0.5;
    }

    float2 offset = isHorizontal ? float2(texel.x, 0.0) : float2(0.0, texel.y);

    float2 uv1 = currentUV - offset;
    float2 uv2 = currentUV + offset;

    float lumaEnd1 = FxaaLuma(texture0.Sample(linearSampler, uv1).rgb) - lumaLocalAvg;
    float lumaEnd2 = FxaaLuma(texture0.Sample(linearSampler, uv2).rgb) - lumaLocalAvg;

    bool reached1 = abs(lumaEnd1) >= gradientScaled;
    bool reached2 = abs(lumaEnd2) >= gradientScaled;
    bool reachedBoth = reached1 && reached2;

    if (!reached1) uv1 -= offset;
    if (!reached2) uv2 += offset;

    [unroll]
    for (int i = 2; i < FXAA_SEARCH_STEPS && !reachedBoth; i++)
    {
        if (!reached1)
        {
            lumaEnd1 = FxaaLuma(texture0.Sample(linearSampler, uv1).rgb) - lumaLocalAvg;
        }
        if (!reached2)
        {
            lumaEnd2 = FxaaLuma(texture0.Sample(linearSampler, uv2).rgb) - lumaLocalAvg;
        }

        reached1 = reached1 || abs(lumaEnd1) >= gradientScaled;
        reached2 = reached2 || abs(lumaEnd2) >= gradientScaled;
        reachedBoth = reached1 && reached2;

        if (!reached1) uv1 -= offset * FXAA_SEARCH_ACCELERATION[i];
        if (!reached2) uv2 += offset * FXAA_SEARCH_ACCELERATION[i];
    }

    float dist1 = isHorizontal ? (uv.x - uv1.x) : (uv.y - uv1.y);
    float dist2 = isHorizontal ? (uv2.x - uv.x) : (uv2.y - uv.y);

    bool isDirection1 = dist1 < dist2;
    float distFinal = min(dist1, dist2);
    float edgeLength = dist1 + dist2;

    float pixelOffset = -distFinal / edgeLength + 0.5;

    bool isLumaCenterSmaller = lumaM < lumaLocalAvg;
    bool correctVariation = ((isDirection1 ? lumaEnd1 : lumaEnd2) < 0.0) != isLumaCenterSmaller;
    float finalOffset = correctVariation ? pixelOffset : 0.0;

    finalOffset = max(finalOffset, subpixBlend);

    float2 finalUV = uv;
    if (isHorizontal)
    {
        finalUV.y += finalOffset * stepLength;
    }
    else
    {
        finalUV.x += finalOffset * stepLength;
    }

    float4 result = texture0.Sample(linearSampler, finalUV);
    return result;
}
