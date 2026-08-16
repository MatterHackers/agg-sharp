// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// The native 3D scene pipeline. Ported from VorticeD3D/Shaders/NodeDesignerScene.hlsl, which stays on
// disk untouched as the parity oracle the goldens were captured from.
//
// Rules that carry over from the 2D modules (see PositionColor.wgsl for the evidence):
//
//  1. Matrices multiply from the LEFT (mat * vec) even though this codebase uses row-vector matrices
//     and the HLSL original writes mul(vec, mat). The bytes are written row by row and WGSL reads them
//     as columns, so the matrix the shader sees is already the transpose; `M * v` therefore computes the
//     `v * M` the matrix stack means.
//  2. The projection uniform has already been mapped to 0..w clip depth by
//     GlUniformBlock.ToClipSpaceProjection, exactly as the classic path's UpdateTransformBuffer does, so
//     there is no z fixup here.
//
// Dual depth peeling (leg C) is here, reformulated: see the peel section at the bottom of this file.
//
// What is deliberately NOT ported: the ApplyDepthPeeling branch inside the opaque entry points. That is
// the *single* depth peeling mode, which the classic path only runs when EffectFlags.z is set, and no
// pass in this renderer sets it - dual peeling replaced it. Bindings a given pipeline does not use are
// simply left out of its bind group layout (a layout may be a superset of what an entry point touches,
// never a subset), which is how one module serves the opaque, peel and bed pipelines at once.

// ---- Uniform blocks -------------------------------------------------------------------------------
// One buffer per classic constant buffer, at the same byte layout, so the C# writers are a transcription
// of the oracle's rather than a redesign.

struct Transform
{
	// b0 in the HLSL: ModelView then Projection, 64 bytes each.
	modelView : mat4x4<f32>,
	projection : mat4x4<f32>,
};

struct Lights
{
	// b1: 7 float4s = 112 bytes. Positions are already in eye space (the classic path sets the lights
	// with an identity modelview so glLightfv's transform is a no-op).
	light0Position : vec4<f32>,
	light0Ambient : vec4<f32>,
	light0Diffuse : vec4<f32>,
	light1Position : vec4<f32>,
	light1Ambient : vec4<f32>,
	light1Diffuse : vec4<f32>,
	// x = light 0 enabled, y = light 1 enabled.
	flags : vec4<f32>,
};

struct SceneEffect
{
	// b2, at the classic SceneEffectBuffer's byte layout. The first five float4s drive every draw; the
	// seven that follow are the analytic bed grid's and are zeroed on every other one.
	meshColor : vec4<f32>,
	wireframeColor : vec4<f32>,
	// x = enable wireframe, y = wireframe only, z = enable depth peeling (always 0 here), w = first peel pass.
	effectFlags : vec4<f32>,
	// xy = target resolution in pixels, z = wireframe width, w = unlit.
	resolutionAndWidth : vec4<f32>,
	// x = use vertex color, y = alpha multiplier, z = is bed grid, w = bed shadow strength.
	extraFlags : vec4<f32>,
	// The analytic bed grid block, zeroed (and switched off by extraFlags.z) on every non-bed draw.
	// xy = bed left/bottom in mm, zw = bed size in mm.
	bedGridBounds : vec4<f32>,
	bedGridColor : vec4<f32>,
	// The horizontal line at world y == 0.
	bedAxisColorX : vec4<f32>,
	// The vertical line at world x == 0.
	bedAxisColorY : vec4<f32>,
	// The short stub at the origin.
	bedAxisColorZ : vec4<f32>,
	// x = spacing mm, y = grid half width px, z = axis half width px, w = axis height mm.
	bedGridParams : vec4<f32>,
	bedGridShadowColor : vec4<f32>,
};

@group(0) @binding(0) var<uniform> transform : Transform;
@group(0) @binding(1) var<uniform> lights : Lights;
@group(0) @binding(2) var<uniform> effect : SceneEffect;
@group(0) @binding(3) var linearSampler : sampler;
@group(0) @binding(4) var diffuseTexture : texture_2d<f32>;

// The blurred bed shadow mask. Declared here rather than beside the peel bindings because it belongs to
// the bed grid, not to peeling; the peel pipelines are simply the only ones that bind it, since the bed
// is drawn as transparent geometry.
@group(0) @binding(8) var bedShadowTexture : texture_2d<f32>;

// ---- Scene vertex stage ---------------------------------------------------------------------------

struct VertexInput
{
	@location(0) position : vec3<f32>,
	@location(1) normal : vec3<f32>,
	@location(2) texCoord : vec2<f32>,
	// Per-edge class from SceneEdgeShaderDataPlugin: 0 = no edge, 1 = drawn, 2 = highlighted.
	@location(3) edgeHints : vec3<f32>,
	@location(4) vertexColor : vec4<f32>,
};

struct VertexOutput
{
	@builtin(position) clipPosition : vec4<f32>,
	@location(0) viewNormal : vec3<f32>,
	@location(1) texCoord : vec2<f32>,
	@location(2) barycentric : vec3<f32>,
	@location(3) edgeHints : vec3<f32>,
	@location(4) vertexColor : vec4<f32>,
};

fn getBarycentric(vertexIndex : u32) -> vec3<f32>
{
	let localVertex = vertexIndex % 3u;
	if (localVertex == 0u)
	{
		return vec3<f32>(1.0, 0.0, 0.0);
	}

	if (localVertex == 1u)
	{
		return vec3<f32>(0.0, 1.0, 0.0);
	}

	return vec3<f32>(0.0, 0.0, 1.0);
}

@vertex
fn sceneVertexMain(input : VertexInput, @builtin(vertex_index) vertexIndex : u32) -> VertexOutput
{
	var output : VertexOutput;
	let viewPosition = transform.modelView * vec4<f32>(input.position, 1.0);
	output.clipPosition = transform.projection * viewPosition;

	// w = 0 so the model-view translation does not move a direction.
	output.viewNormal = normalize((transform.modelView * vec4<f32>(input.normal, 0.0)).xyz);
	output.texCoord = input.texCoord;
	output.barycentric = getBarycentric(vertexIndex);
	output.edgeHints = input.edgeHints;
	output.vertexColor = input.vertexColor;
	return output;
}

// ---- Shading --------------------------------------------------------------------------------------

struct FragmentInput
{
	@builtin(position) position : vec4<f32>,
	@location(0) viewNormal : vec3<f32>,
	@location(1) texCoord : vec2<f32>,
	@location(2) barycentric : vec3<f32>,
	@location(3) edgeHints : vec3<f32>,
	@location(4) vertexColor : vec4<f32>,
};

const DepthPeelBias : f32 = 1e-5;

fn applyLighting(baseColor : vec3<f32>, viewNormal : vec3<f32>) -> vec3<f32>
{
	let normal = normalize(viewNormal);
	var litColor = baseColor * 0.2;

	if (lights.flags.x > 0.5)
	{
		let diffuse = max(0.0, dot(normal, normalize(lights.light0Position.xyz)));
		litColor = litColor + baseColor * (lights.light0Ambient.rgb + lights.light0Diffuse.rgb * diffuse);
	}

	if (lights.flags.y > 0.5)
	{
		let diffuse = max(0.0, dot(normal, normalize(lights.light1Position.xyz)));
		litColor = litColor + baseColor * (lights.light1Ambient.rgb + lights.light1Diffuse.rgb * diffuse);
	}

	return clamp(litColor, vec3<f32>(0.0), vec3<f32>(1.0));
}

fn getEffectiveColor(vertexColor : vec4<f32>) -> vec4<f32>
{
	var color = effect.meshColor;
	if (effect.extraFlags.x > 0.5)
	{
		color = vertexColor;
	}

	// The alpha multiplier scales transparency for previews (subtract components and the like).
	color.a = color.a * effect.extraFlags.y;
	return color;
}

fn wireframeEdgeFactors(barycentric : vec3<f32>, width : f32) -> vec3<f32>
{
	let derivatives = fwidth(barycentric);
	return vec3<f32>(1.0) - smoothstep(vec3<f32>(0.0), derivatives * max(width, 0.375), barycentric);
}

// Returns the composed color; `keep` is false where the HLSL would have discarded. WGSL allows discard
// in a helper, but a shader that discards inside a function called before other work reads worse than
// one that returns the decision, and the two are equivalent here because nothing follows the call.
struct ComposedColor
{
	color : vec4<f32>,
	keep : bool,
};

fn composeSceneColor(shadedColor : vec4<f32>, barycentric : vec3<f32>, edgeHints : vec3<f32>) -> ComposedColor
{
	var result : ComposedColor;
	result.color = shadedColor;
	result.keep = true;

	if (effect.effectFlags.x < 0.5)
	{
		return result;
	}

	let edgeFactors = wireframeEdgeFactors(barycentric, effect.resolutionAndWidth.z);
	let visibleEdges = edgeFactors * step(vec3<f32>(0.5), edgeHints);
	let edge = max(max(visibleEdges.x, visibleEdges.y), visibleEdges.z);

	if (edge <= 1e-5)
	{
		if (effect.effectFlags.y > 0.5)
		{
			result.keep = false;
		}

		return result;
	}

	let highlightedEdges = edgeFactors * step(vec3<f32>(1.5), edgeHints);
	let highlight = max(max(highlightedEdges.x, highlightedEdges.y), highlightedEdges.z);
	var wireColor = effect.wireframeColor;
	if (highlight > 1e-5)
	{
		wireColor = vec4<f32>(1.0, 0.0, 0.0, wireColor.a);
	}

	if (effect.effectFlags.y > 0.5)
	{
		result.color = vec4<f32>(wireColor.rgb, edge * max(shadedColor.a, wireColor.a));
		return result;
	}

	result.color = vec4<f32>(
		mix(shadedColor.rgb, wireColor.rgb, edge),
		max(shadedColor.a, edge * wireColor.a));
	return result;
}

// Finishes a shaded fragment the way every scene entry point does: lighting (unless the draw is unlit),
// then the wireframe overlay. `alpha` is tested last on purpose rather than short-circuiting: an early
// return here would put composeSceneColor's fwidth into non-uniform control flow, and the HLSL's
// DiscardIfInvisible has no such constraint to trade against - the composed colour of a fragment that is
// dropped anyway is never looked at.
fn finishShading(input : FragmentInput, baseColor : vec4<f32>) -> ComposedColor
{
	var color = applyLighting(baseColor.rgb, input.viewNormal);
	if (effect.resolutionAndWidth.w > 0.5)
	{
		color = baseColor.rgb;
	}

	var result = composeSceneColor(vec4<f32>(color, baseColor.a), input.barycentric, input.edgeHints);
	if (baseColor.a <= DepthPeelBias)
	{
		result.keep = false;
	}

	return result;
}

// The shading half of SceneColorPS, factored out because the peel entry points need exactly the same
// colour - and the same discard decisions - before they decide which layer the fragment belongs to.
fn shadeFromColor(input : FragmentInput) -> ComposedColor
{
	return finishShading(input, getEffectiveColor(input.vertexColor));
}

// The shading half of SceneTexturePS.
fn shadeFromTexture(input : FragmentInput) -> ComposedColor
{
	return finishShading(input, textureSample(diffuseTexture, linearSampler, input.texCoord) * getEffectiveColor(input.vertexColor));
}

@fragment
fn sceneColorMain(input : FragmentInput) -> @location(0) vec4<f32>
{
	let composed = shadeFromColor(input);
	if (!composed.keep)
	{
		discard;
	}

	return composed.color;
}

@fragment
fn sceneTextureMain(input : FragmentInput) -> @location(0) vec4<f32>
{
	let composed = shadeFromTexture(input);
	if (!composed.keep)
	{
		discard;
	}

	return composed.color;
}

// Depth prepass: writes only depth, and drops fragments the color pass would have discarded for being
// fully transparent, so the depth buffer the outline composite reads agrees with the visible image.
@fragment
fn sceneDepthOnlyMain(input : FragmentInput)
{
	let alpha = textureSample(diffuseTexture, linearSampler, input.texCoord).a * effect.meshColor.a;
	if (alpha <= DepthPeelBias)
	{
		discard;
	}
}

// ---- Analytic bed grid ----------------------------------------------------------------------------
// Ported from ApplyBedGrid. A texture-space line is magnified and bilinearly smeared under perspective,
// so it can never stay one screen pixel wide; solving for the distance to the nearest line in world mm
// and converting through fwidth gives a constant on-screen thickness at any depth. The uv to world
// reconstruction is exact: the bed is one axis aligned quad whose uvs span 0..1 across its bounds and
// whose transform is translation only.

// Composites one analytic line over the bed. The baked lines it replaces were fully opaque over an
// 80/255 fill, so the line has to raise alpha the same way or the grid would read as translucent.
fn blendBedLine(color : vec4<f32>, lineColor : vec4<f32>, coverage : f32) -> vec4<f32>
{
	let weight = clamp(coverage, 0.0, 1.0) * lineColor.a;
	return vec4<f32>(mix(color.rgb, lineColor.rgb, weight), mix(color.a, 1.0, weight));
}

// Tints a line toward the bed shadow colour exactly as the composite pass tints the bed fill, so a line
// crossing an object's shadow darkens with it.
fn shadowTintBedLine(lineColor : vec4<f32>, shadowTint : f32) -> vec4<f32>
{
	return vec4<f32>(mix(lineColor.rgb, effect.bedGridShadowColor.rgb, shadowTint), lineColor.a);
}

fn applyBedGrid(baseColor : vec4<f32>, uv : vec2<f32>) -> vec4<f32>
{
	let world = effect.bedGridBounds.xy + uv * effect.bedGridBounds.zw;

	// mm per screen pixel. Taken unconditionally, so the derivative stays in uniform control flow; the
	// early out is on the result, not around this.
	let deriv = max(fwidth(world), vec2<f32>(1e-6));
	let spacing = max(effect.bedGridParams.x, 1e-6);

	// The baked lines were part of the bed base texture, so the composite pass tinted them along with
	// the fill. Analytic lines are drawn after that composite, so sample the same blurred mask (with the
	// same v flip, since the mask is rendered upside down relative to the bed texture).
	let shadowUv = vec2<f32>(uv.x, 1.0 - uv.y);
	let shadowAmount = clamp(textureSample(bedShadowTexture, linearSampler, shadowUv).a * effect.extraFlags.w, 0.0, 1.0);
	let shadowTint = clamp(shadowAmount * effect.bedGridShadowColor.a, 0.0, 1.0);

	if (effect.extraFlags.z < 0.5)
	{
		return baseColor;
	}

	// Each line family is only unresolvable along its own axis: at a grazing view the lines running
	// toward the horizon crowd together while the ones crossing them stay readable, so the two families
	// fade independently.
	let gridPx = abs(fract(world / spacing + vec2<f32>(0.5)) - vec2<f32>(0.5)) * spacing / deriv;
	let spacingPx = spacing / deriv;
	let gridCoverage = clamp(vec2<f32>(effect.bedGridParams.y + 0.5) - gridPx, vec2<f32>(0.0), vec2<f32>(1.0))
		* clamp((spacingPx - vec2<f32>(3.0)) / 3.0, vec2<f32>(0.0), vec2<f32>(1.0));

	var color = blendBedLine(
		baseColor,
		shadowTintBedLine(effect.bedGridColor, shadowTint),
		max(gridCoverage.x, gridCoverage.y));

	// Match the baked draw order: grid, then the Y axis, then the X axis, then the Z stub.
	let axisHalfPx = effect.bedGridParams.z;
	let axisYCoverage = clamp(axisHalfPx + 0.5 - abs(world.x) / deriv.x, 0.0, 1.0);
	color = blendBedLine(color, shadowTintBedLine(effect.bedAxisColorY, shadowTint), axisYCoverage);

	let axisXCoverage = clamp(axisHalfPx + 0.5 - abs(world.y) / deriv.y, 0.0, 1.0);
	color = blendBedLine(color, shadowTintBedLine(effect.bedAxisColorX, shadowTint), axisXCoverage);

	// The Z axis has nowhere to go on a flat bed, so it shows as a short bar at the origin covering the
	// same +/- axisHeight mm the baked texture used, feathered over a pixel at each end.
	let zEndCoverage = clamp((effect.bedGridParams.w - abs(world.y)) / deriv.y + 0.5, 0.0, 1.0);
	color = blendBedLine(color, shadowTintBedLine(effect.bedAxisColorZ, shadowTint), axisYCoverage * zEndCoverage);

	return color;
}

// SceneTextureDualPeelPS's shading: the textured path plus the analytic grid, which every transparent
// textured draw runs and only the bed draw switches on (extraFlags.z).
fn shadeFromBedTexture(input : FragmentInput) -> ComposedColor
{
	let sampledColor = textureSample(diffuseTexture, linearSampler, input.texCoord) * getEffectiveColor(input.vertexColor);
	let griddedColor = applyBedGrid(sampledColor, input.texCoord);

	// The HLSL tests the *pre-grid* alpha and discards before the grid runs; the grid can only raise
	// alpha, so testing it here on the pre-grid value keeps that decision identical.
	var result = finishShading(input, vec4<f32>(griddedColor.rgb, griddedColor.a));
	if (sampledColor.a <= DepthPeelBias)
	{
		result.keep = false;
	}

	return result;
}

// ---- Dual depth peeling ---------------------------------------------------------------------------
//
// The classic path keeps the peeled depth *range* in one Rg32Float target, blended with MAX: red holds
// max(-z) (the nearest remaining layer, negated so MAX finds a minimum) and green holds max(z) (the
// farthest). wgpu on D3D12 does not expose `float32-blendable`, so that target cannot be blended at all
// here. The reformulation (decided in Phase 3 leg B) computes the identical two numbers with hardware
// depth tests instead: one Depth32Float texture cleared to 1.0 and tested Less holds min(z) = the
// front depth; a second cleared to 0.0 and tested Greater holds max(z) = the back depth. Same values,
// same float32 precision, no optional device feature - at the cost of two depth-only passes per
// iteration, because the depth attachment a pass writes cannot also be a colour output.
//
// So one classic peel draw becomes three passes here, over the same geometry with the same discard
// predicate: peelDepthNear*, peelDepthFar* (which keep only the fragments the HLSL would have written a
// depth range for) and peelColor*/peelTexture* (which keep only the fragments it would have emitted a
// front or back colour for). The reads are all from the *previous* iteration's pair, so splitting the
// draw changes nothing about what any fragment sees.
//
// The empty-range clears agree too: the classic clear of (-1, -1) reads back as front = 1.0, back = -1,
// and here as front = 1.0, back = 0.0. Both make every fragment fail `current + bias < front` and so
// discard, which is how peeling terminates once every layer has been consumed.

@group(0) @binding(5) var opaqueDepthTexture : texture_depth_2d;
@group(0) @binding(6) var peelNearDepthTexture : texture_depth_2d;
@group(0) @binding(7) var peelFarDepthTexture : texture_depth_2d;

struct PeelOutput
{
	@location(0) frontColor : vec4<f32>,
	@location(1) backColor : vec4<f32>,
};

struct PeelRange
{
	front : f32,
	back : f32,
};

// The texel the classic path's point sampler would have picked: SV_POSITION.xy is a pixel centre, so
// its floor is that pixel's index. A load rather than a sample, for the reason the outline composite
// gives - depth textures have their own sampling rules and a load has none of them.
fn depthTexel(position : vec4<f32>) -> vec2<i32>
{
	return vec2<i32>(floor(position.xy));
}

fn rejectBehindOpaque(position : vec4<f32>) -> bool
{
	return textureLoad(opaqueDepthTexture, depthTexel(position), 0) < position.z - DepthPeelBias;
}

fn peelRange(position : vec4<f32>) -> PeelRange
{
	let texel = depthTexel(position);
	var range : PeelRange;
	range.front = textureLoad(peelNearDepthTexture, texel, 0);
	range.back = textureLoad(peelFarDepthTexture, texel, 0);
	return range;
}

// True for the fragments the HLSL's ApplyDualDepthPeeling writes a new depth range for: strictly
// between the layers already peeled. The comparisons are the HLSL's, bias included and strict as
// written - at a layer boundary the difference between `>` and `>=` is the difference between peeling a
// layer and peeling it twice.
fn peelIsInsideRange(position : vec4<f32>) -> bool
{
	let range = peelRange(position);
	let currentDepth = position.z;
	return currentDepth - DepthPeelBias > range.front && currentDepth + DepthPeelBias < range.back;
}

// The colour half of ApplyDualDepthPeeling. Fragments outside the remaining range are gone; fragments
// strictly inside it belong to a later iteration and contribute no colour now (the HLSL says the same
// thing by returning its zeroed output, which is the identity of both accumulation blends).
fn peelLayerColor(position : vec4<f32>, shadedColor : vec4<f32>) -> PeelOutput
{
	if (rejectBehindOpaque(position))
	{
		discard;
	}

	let range = peelRange(position);
	let currentDepth = position.z;

	if (currentDepth + DepthPeelBias < range.front || currentDepth - DepthPeelBias > range.back)
	{
		discard;
	}

	if (currentDepth - DepthPeelBias > range.front && currentDepth + DepthPeelBias < range.back)
	{
		discard;
	}

	var output : PeelOutput;
	output.frontColor = vec4<f32>(0.0);
	output.backColor = vec4<f32>(0.0);

	if (abs(currentDepth - range.front) <= DepthPeelBias)
	{
		// Premultiplied, because the front accumulation blend weights the source by the destination's
		// remaining transmittance and must not weight it by its own alpha a second time.
		output.frontColor = vec4<f32>(shadedColor.rgb * shadedColor.a, shadedColor.a);
	}
	else
	{
		output.backColor = shadedColor;
	}

	return output;
}

// DualDepthInitPS: the first depth range, seeded from every transparent fragment in front of the opaque
// scene. Its alpha test is the texture's alpha times the mesh colour's - not the shaded colour's - which
// is what the classic path tests here, vertex colours and all.
@fragment
fn peelInitMain(input : FragmentInput)
{
	let alpha = textureSample(diffuseTexture, linearSampler, input.texCoord).a * effect.meshColor.a;
	if (alpha <= DepthPeelBias)
	{
		discard;
	}

	if (rejectBehindOpaque(input.position))
	{
		discard;
	}
}

@fragment
fn peelDepthColorMain(input : FragmentInput)
{
	let composed = shadeFromColor(input);
	if (!composed.keep || rejectBehindOpaque(input.position) || !peelIsInsideRange(input.position))
	{
		discard;
	}
}

@fragment
fn peelDepthTextureMain(input : FragmentInput)
{
	let composed = shadeFromBedTexture(input);
	if (!composed.keep || rejectBehindOpaque(input.position) || !peelIsInsideRange(input.position))
	{
		discard;
	}
}

@fragment
fn peelColorMain(input : FragmentInput) -> PeelOutput
{
	let composed = shadeFromColor(input);
	if (!composed.keep)
	{
		discard;
	}

	return peelLayerColor(input.position, composed.color);
}

@fragment
fn peelTextureMain(input : FragmentInput) -> PeelOutput
{
	let composed = shadeFromBedTexture(input);
	if (!composed.keep)
	{
		discard;
	}

	return peelLayerColor(input.position, composed.color);
}

// ---- Selection mask -------------------------------------------------------------------------------
// Position only: the outline composite cares about silhouette and depth, not shading.

struct SelectionVertexOutput
{
	@builtin(position) clipPosition : vec4<f32>,
};

@vertex
fn selectionVertexMain(@location(0) position : vec3<f32>) -> SelectionVertexOutput
{
	var output : SelectionVertexOutput;
	let viewPosition = transform.modelView * vec4<f32>(position, 1.0);
	output.clipPosition = transform.projection * viewPosition;
	return output;
}

@fragment
fn selectionMaskMain() -> @location(0) vec4<f32>
{
	return effect.meshColor;
}
