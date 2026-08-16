// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// The scene compositor's full-screen passes. Ported from VorticeD3D/Shaders/NodeDesignerPostProcess.hlsl
// (untouched oracle): the copy, the dual-peel resolve every frame runs (an identity when nothing is
// transparent, because the classic path always resolves through it), the selection outline composite,
// the 3x supersample downsample and the bed shadow blur and composite.
//
// Every binding in this module is numbered uniquely rather than reusing slots per pass: one WGSL module
// serves several pipelines, and two entry points cannot declare different resource types at the same
// @binding. Each pipeline declares only the subset it uses (a layout may be a superset of what the entry
// point touches, never a subset).

@group(0) @binding(0) var pointSampler : sampler;
@group(0) @binding(1) var texture0 : texture_2d<f32>;
@group(0) @binding(2) var texture1 : texture_2d<f32>;
@group(0) @binding(3) var texture2 : texture_2d<f32>;
@group(0) @binding(4) var texture3 : texture_2d<f32>;

struct OutlineSettings
{
	// x = outline width in target pixels, y = alpha applied where the outline is occluded,
	// zw = target resolution. Matches OutlineCompositeBuffer's first float4.
	settings : vec4<f32>,
	padding : vec4<f32>,
};

@group(0) @binding(5) var<uniform> outline : OutlineSettings;
@group(0) @binding(6) var selectionDepth : texture_depth_2d;
@group(0) @binding(7) var sceneDepth : texture_depth_2d;

struct DownsampleSettings
{
	// xy = one source texel in uv, so the 9 taps land on the 3x3 block behind each destination pixel.
	texelSize : vec4<f32>,
};

@group(0) @binding(8) var<uniform> downsample : DownsampleSettings;

struct BedShadowSettings
{
	// xy = blur direction in uv (one axis per pass), z = shadow strength, w unused.
	settings : vec4<f32>,
	// The colour the bed fill is tinted toward where an object shadows it; alpha is how far.
	shadowColor : vec4<f32>,
};

@group(0) @binding(9) var linearSampler : sampler;
@group(0) @binding(10) var<uniform> bedShadow : BedShadowSettings;

struct FullScreenOutput
{
	@builtin(position) position : vec4<f32>,
	@location(0) texCoord : vec2<f32>,
};

// The classic FullScreenVS: three vertices covering the target, uv running top-down like the texture.
@vertex
fn fullscreenVertexMain(@builtin(vertex_index) vertexIndex : u32) -> FullScreenOutput
{
	var output : FullScreenOutput;
	let texCoord = vec2<f32>(f32((vertexIndex << 1u) & 2u), f32(vertexIndex & 2u));
	output.texCoord = texCoord;
	output.position = vec4<f32>(texCoord * vec2<f32>(2.0, -2.0) + vec2<f32>(-1.0, 1.0), 0.0, 1.0);
	return output;
}

@fragment
fn copyTextureMain(input : FullScreenOutput) -> @location(0) vec4<f32>
{
	return textureSample(texture0, pointSampler, input.texCoord);
}

// The transparency resolve. With no transparent geometry the accumulation targets are cleared to their
// identity values (front alpha 1, back 0) and this reduces to "pass the opaque scene through", which is
// exactly what the classic path does for an opaque frame - so the opaque goldens go through it too.
@fragment
fn resolveDualPeelMain(input : FullScreenOutput) -> @location(0) vec4<f32>
{
	let sceneColor = textureSample(texture0, pointSampler, input.texCoord);
	let frontAccum = textureSample(texture1, pointSampler, input.texCoord);
	let backAccum = textureSample(texture2, pointSampler, input.texCoord);
	let transparentOverlay = textureSample(texture3, pointSampler, input.texCoord);

	// WGSL has no saturate(); clamp to 0..1 is the same thing HLSL's does.
	let remainingTransmittance = clamp(frontAccum.a * (1.0 - backAccum.a), 0.0, 1.0);
	let transparentAlpha = 1.0 - remainingTransmittance;
	let combinedAlpha = sceneColor.a + (1.0 - sceneColor.a) * transparentAlpha;
	let sceneWeight = sceneColor.a * remainingTransmittance;
	let premultipliedColor = frontAccum.rgb + frontAccum.a * backAccum.rgb + sceneWeight * sceneColor.rgb;
	if (combinedAlpha <= 1e-6)
	{
		return vec4<f32>(0.0);
	}

	let resolvedColor = vec4<f32>(premultipliedColor / combinedAlpha, combinedAlpha);
	let overlayWeight = transparentOverlay.a;
	return vec4<f32>(
		mix(resolvedColor.rgb, transparentOverlay.rgb, overlayWeight),
		resolvedColor.a + (1.0 - resolvedColor.a) * overlayWeight);
}

// The 9-tap box filter behind the 3x full-frame supersample. The destination pixel centre maps to the
// centre texel of its 3x3 source block, so taps at +/- one texel cover the block exactly - which is why
// this is a plain unweighted average rather than a reconstruction filter.
@fragment
fn downsample3x3Main(input : FullScreenOutput) -> @location(0) vec4<f32>
{
	var sum = vec4<f32>(0.0);
	for (var dy = -1; dy <= 1; dy = dy + 1)
	{
		for (var dx = -1; dx <= 1; dx = dx + 1)
		{
			let offset = vec2<f32>(f32(dx), f32(dy)) * downsample.texelSize.xy;
			sum = sum + textureSample(texture0, pointSampler, input.texCoord + offset);
		}
	}

	return sum * (1.0 / 9.0);
}

// ---- Bed shadow -----------------------------------------------------------------------------------
// The scene's objects are rasterized into a mask from straight above the bed, blurred separably (five
// taps, the classic weights) and composited under the bed's own texture. Run only when the bed's
// silhouette signature changes, so this is not per-frame work.

@fragment
fn bedShadowBlurMain(input : FullScreenOutput) -> @location(0) vec4<f32>
{
	let direction = bedShadow.settings.xy;
	var color = textureSample(texture0, linearSampler, input.texCoord) * 0.227027;
	color = color + textureSample(texture0, linearSampler, input.texCoord + direction * 1.384615) * 0.316216;
	color = color + textureSample(texture0, linearSampler, input.texCoord - direction * 1.384615) * 0.316216;
	color = color + textureSample(texture0, linearSampler, input.texCoord + direction * 3.230769) * 0.070270;
	color = color + textureSample(texture0, linearSampler, input.texCoord - direction * 3.230769) * 0.070270;
	return color;
}

@fragment
fn bedShadowCompositeMain(input : FullScreenOutput) -> @location(0) vec4<f32>
{
	let baseColor = textureSample(texture0, linearSampler, input.texCoord);

	// The mask is rendered upside down relative to the bed texture, hence the v flip - the analytic grid
	// in NodeDesignerScene.wgsl flips the same way for the same reason.
	let shadowUv = vec2<f32>(input.texCoord.x, 1.0 - input.texCoord.y);
	let shadowAmount = clamp(textureSample(texture1, linearSampler, shadowUv).a * bedShadow.settings.z, 0.0, 1.0);
	let shadowTintAmount = clamp(shadowAmount * bedShadow.shadowColor.a, 0.0, 1.0);
	let shadowRgb = mix(baseColor.rgb, bedShadow.shadowColor.rgb, shadowTintAmount);
	let shadowAlpha = clamp(baseColor.a + shadowAmount * (1.0 - baseColor.a), 0.0, 1.0);
	return vec4<f32>(shadowRgb, shadowAlpha);
}

// ---- Selection outline composite ------------------------------------------------------------------
// Point sampling is spelled as textureLoad rather than as a nearest sampler: the depth attachments are
// depth textures, whose sampling rules are their own, and a load at the texel the oracle's point sampler
// would have picked is both exact and free of them. floor(uv * resolution) is what a point sampler
// computes, so the taps land on the same texels the HLSL reads.

fn texelAt(uv : vec2<f32>, resolution : vec2<f32>) -> vec2<i32>
{
	let coordinate = floor(uv * resolution);
	let limit = resolution - vec2<f32>(1.0);
	return vec2<i32>(clamp(coordinate, vec2<f32>(0.0), limit));
}

fn loadColor(uv : vec2<f32>, resolution : vec2<f32>) -> vec4<f32>
{
	return textureLoad(texture0, texelAt(uv, resolution), 0);
}

fn loadSelectionDepth(uv : vec2<f32>, resolution : vec2<f32>) -> f32
{
	return textureLoad(selectionDepth, texelAt(uv, resolution), 0);
}

@fragment
fn outlineCompositeMain(input : FullScreenOutput) -> @location(0) vec4<f32>
{
	let outlineWidth = outline.settings.x;
	let occludedAlpha = outline.settings.y;
	let resolution = outline.settings.zw;
	let texel = vec2<f32>(1.0) / resolution;
	let uv = input.texCoord;

	let rightUv = uv + vec2<f32>(texel.x * outlineWidth, 0.0);
	let leftUv = uv - vec2<f32>(texel.x * outlineWidth, 0.0);
	let upUv = uv + vec2<f32>(0.0, texel.y * outlineWidth);
	let downUv = uv - vec2<f32>(0.0, texel.y * outlineWidth);

	let center = loadColor(uv, resolution);
	let right = loadColor(rightUv, resolution);
	let left = loadColor(leftUv, resolution);
	let up = loadColor(upUv, resolution);
	let down = loadColor(downUv, resolution);
	let topRight = loadColor(uv + vec2<f32>(texel.x, texel.y) * outlineWidth * 0.707, resolution);
	let topLeft = loadColor(uv + vec2<f32>(-texel.x, texel.y) * outlineWidth * 0.707, resolution);
	let bottomRight = loadColor(uv + vec2<f32>(texel.x, -texel.y) * outlineWidth * 0.707, resolution);
	let bottomLeft = loadColor(uv + vec2<f32>(-texel.x, -texel.y) * outlineWidth * 0.707, resolution);

	let hasNeighbor = right.a > 0.0 || left.a > 0.0 || up.a > 0.0 || down.a > 0.0
		|| topRight.a > 0.0 || topLeft.a > 0.0 || bottomRight.a > 0.0 || bottomLeft.a > 0.0;
	let hasEmptyNeighbor = right.a == 0.0 || left.a == 0.0 || up.a == 0.0 || down.a == 0.0
		|| topRight.a == 0.0 || topLeft.a == 0.0 || bottomRight.a == 0.0 || bottomLeft.a == 0.0;

	if (!(hasNeighbor && hasEmptyNeighbor))
	{
		discard;
	}

	var selectedDepth = 1.0;
	if (center.a > 0.0)
	{
		selectedDepth = min(selectedDepth, loadSelectionDepth(uv, resolution));
	}

	if (right.a > 0.0)
	{
		selectedDepth = min(selectedDepth, loadSelectionDepth(rightUv, resolution));
	}

	if (left.a > 0.0)
	{
		selectedDepth = min(selectedDepth, loadSelectionDepth(leftUv, resolution));
	}

	if (up.a > 0.0)
	{
		selectedDepth = min(selectedDepth, loadSelectionDepth(upUv, resolution));
	}

	if (down.a > 0.0)
	{
		selectedDepth = min(selectedDepth, loadSelectionDepth(downUv, resolution));
	}

	let frameDepth = textureLoad(sceneDepth, texelAt(uv, resolution), 0);
	let occluded = selectedDepth > frameDepth + 1e-4;

	var outlineColor = bottomLeft;
	if (center.a > 0.0) { outlineColor = center; }
	else if (right.a > 0.0) { outlineColor = right; }
	else if (left.a > 0.0) { outlineColor = left; }
	else if (up.a > 0.0) { outlineColor = up; }
	else if (down.a > 0.0) { outlineColor = down; }
	else if (topRight.a > 0.0) { outlineColor = topRight; }
	else if (topLeft.a > 0.0) { outlineColor = topLeft; }
	else if (bottomRight.a > 0.0) { outlineColor = bottomRight; }

	if (occluded)
	{
		outlineColor.a = outlineColor.a * occludedAlpha;
	}

	return outlineColor;
}
