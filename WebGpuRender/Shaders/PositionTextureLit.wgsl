// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// Lit, textured, modulated by the vertex color. Ported from VorticeD3D/Shaders/PositionTextureLit.hlsl;
// see PositionColor.wgsl for the rules common to every canned module and PositionColorLit.wgsl for the
// lighting model.
//
// Note the order the original establishes and this keeps: the texel is combined with the vertex color
// FIRST and the result is what gets lit, so the light modulates the textured surface rather than the
// untextured one. The alpha comes through unlit.

struct Uniforms
{
	modelViewMatrix : mat4x4<f32>,
	projectionMatrix : mat4x4<f32>,
	textureMatrix : mat4x4<f32>,
	light0Position : vec4<f32>,
	light0Ambient : vec4<f32>,
	light0Diffuse : vec4<f32>,
	light1Position : vec4<f32>,
	light1Ambient : vec4<f32>,
	light1Diffuse : vec4<f32>,
	flags : vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms : Uniforms;
@group(0) @binding(1) var diffuseTexture : texture_2d<f32>;
@group(0) @binding(2) var textureSampler : sampler;

struct VertexInput
{
	@location(0) position : vec3<f32>,
	@location(1) normal : vec3<f32>,
	@location(2) texCoord : vec2<f32>,
	@location(3) color : vec4<f32>,
};

struct VertexOutput
{
	@builtin(position) clipPosition : vec4<f32>,
	@location(0) color : vec4<f32>,
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
	@location(2) texCoord : vec2<f32>,
	@location(3) viewNormal : vec3<f32>,
};

struct SmoothFragmentInput
{
	@location(0) color : vec4<f32>,
	@location(2) texCoord : vec2<f32>,
	@location(3) viewNormal : vec3<f32>,
};

struct FlatFragmentInput
{
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
	@location(2) texCoord : vec2<f32>,
	@location(3) viewNormal : vec3<f32>,
};

@vertex
fn vertexMain(input : VertexInput) -> VertexOutput
{
	var output : VertexOutput;
	let viewPosition = uniforms.modelViewMatrix * vec4<f32>(input.position, 1.0);
	output.clipPosition = uniforms.projectionMatrix * viewPosition;
	output.viewNormal = normalize((uniforms.modelViewMatrix * vec4<f32>(input.normal, 0.0)).xyz);
	output.texCoord = input.texCoord;
	output.color = input.color;
	output.flatColor = input.color;
	return output;
}

fn combine(texel : vec4<f32>, vertexColor : vec4<f32>) -> vec4<f32>
{
	if (uniforms.flags.w > 0.5)
	{
		return texel;
	}

	return texel * vertexColor;
}

fn applyLighting(baseColor : vec3<f32>, viewNormal : vec3<f32>) -> vec3<f32>
{
	let normal = normalize(viewNormal);
	var litColor = baseColor * 0.2;

	if (uniforms.flags.x > 0.5)
	{
		let diffuse = max(0.0, dot(normal, normalize(uniforms.light0Position.xyz)));
		litColor = litColor + baseColor * (uniforms.light0Ambient.rgb + uniforms.light0Diffuse.rgb * diffuse);
	}

	if (uniforms.flags.y > 0.5)
	{
		let diffuse = max(0.0, dot(normal, normalize(uniforms.light1Position.xyz)));
		litColor = litColor + baseColor * (uniforms.light1Ambient.rgb + uniforms.light1Diffuse.rgb * diffuse);
	}

	return clamp(litColor, vec3<f32>(0.0), vec3<f32>(1.0));
}

@fragment
fn fragmentMain(input : SmoothFragmentInput) -> @location(0) vec4<f32>
{
	let surface = combine(textureSample(diffuseTexture, textureSampler, input.texCoord), input.color);
	return vec4<f32>(applyLighting(surface.rgb, input.viewNormal), surface.a);
}

@fragment
fn fragmentMainFlat(input : FlatFragmentInput) -> @location(0) vec4<f32>
{
	let surface = combine(textureSample(diffuseTexture, textureSampler, input.texCoord), input.flatColor);
	return vec4<f32>(applyLighting(surface.rgb, input.viewNormal), surface.a);
}
