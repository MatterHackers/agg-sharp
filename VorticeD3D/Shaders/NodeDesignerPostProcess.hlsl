cbuffer OutlineCompositeBuffer : register(b0)
{
    float4 OutlineSettings;
    float4 OutlinePadding;
};

Texture2D inputTexture : register(t0);
Texture2D selectionDepthTexture : register(t1);
Texture2D sceneDepthTexture : register(t2);

SamplerState pointSampler : register(s0);

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
    return inputTexture.Sample(pointSampler, input.TexCoord);
}

float4 OutlineCompositePS(VS_OUTPUT input) : SV_TARGET
{
    float outlineWidth = OutlineSettings.x;
    float occludedAlpha = OutlineSettings.y;
    float2 resolution = OutlineSettings.zw;
    float2 texel = 1.0 / resolution;

    float4 center = inputTexture.Sample(pointSampler, input.TexCoord);
    float4 right = inputTexture.Sample(pointSampler, input.TexCoord + float2(texel.x * outlineWidth, 0.0));
    float4 left = inputTexture.Sample(pointSampler, input.TexCoord - float2(texel.x * outlineWidth, 0.0));
    float4 up = inputTexture.Sample(pointSampler, input.TexCoord + float2(0.0, texel.y * outlineWidth));
    float4 down = inputTexture.Sample(pointSampler, input.TexCoord - float2(0.0, texel.y * outlineWidth));
    float4 topRight = inputTexture.Sample(pointSampler, input.TexCoord + float2(texel.x, texel.y) * outlineWidth * 0.707);
    float4 topLeft = inputTexture.Sample(pointSampler, input.TexCoord + float2(-texel.x, texel.y) * outlineWidth * 0.707);
    float4 bottomRight = inputTexture.Sample(pointSampler, input.TexCoord + float2(texel.x, -texel.y) * outlineWidth * 0.707);
    float4 bottomLeft = inputTexture.Sample(pointSampler, input.TexCoord + float2(-texel.x, -texel.y) * outlineWidth * 0.707);

    bool hasNeighbor = right.a > 0.0 || left.a > 0.0 || up.a > 0.0 || down.a > 0.0 ||
        topRight.a > 0.0 || topLeft.a > 0.0 || bottomRight.a > 0.0 || bottomLeft.a > 0.0;
    bool hasEmptyNeighbor = right.a == 0.0 || left.a == 0.0 || up.a == 0.0 || down.a == 0.0 ||
        topRight.a == 0.0 || topLeft.a == 0.0 || bottomRight.a == 0.0 || bottomLeft.a == 0.0;

    if (!(hasNeighbor && hasEmptyNeighbor))
    {
        discard;
    }

    float selectedDepth = 1.0;
    if (center.a > 0.0) selectedDepth = min(selectedDepth, selectionDepthTexture.Sample(pointSampler, input.TexCoord).r);
    if (right.a > 0.0) selectedDepth = min(selectedDepth, selectionDepthTexture.Sample(pointSampler, input.TexCoord + float2(texel.x * outlineWidth, 0.0)).r);
    if (left.a > 0.0) selectedDepth = min(selectedDepth, selectionDepthTexture.Sample(pointSampler, input.TexCoord - float2(texel.x * outlineWidth, 0.0)).r);
    if (up.a > 0.0) selectedDepth = min(selectedDepth, selectionDepthTexture.Sample(pointSampler, input.TexCoord + float2(0.0, texel.y * outlineWidth)).r);
    if (down.a > 0.0) selectedDepth = min(selectedDepth, selectionDepthTexture.Sample(pointSampler, input.TexCoord - float2(0.0, texel.y * outlineWidth)).r);

    float sceneDepth = sceneDepthTexture.Sample(pointSampler, input.TexCoord).r;
    bool occluded = selectedDepth > sceneDepth + 1e-4;

    float4 outlineColor = center.a > 0.0 ? center : right.a > 0.0 ? right : left.a > 0.0 ? left : up.a > 0.0 ? up : down.a > 0.0 ? down : topRight.a > 0.0 ? topRight : topLeft.a > 0.0 ? topLeft : bottomRight.a > 0.0 ? bottomRight : bottomLeft;
    outlineColor.a = occluded ? outlineColor.a * occludedAlpha : outlineColor.a;
    return outlineColor;
}
