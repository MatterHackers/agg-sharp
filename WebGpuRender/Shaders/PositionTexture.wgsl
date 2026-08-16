// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// Unlit, textured, modulated by the vertex color. Ported from VorticeD3D/Shaders/PositionTexture.hlsl;
// see PositionColor.wgsl for the rules common to every canned module.
//
// One thing here has no HLSL original: flags.w carries GL's texture environment mode. GL_MODULATE (the
// default, flag 0) multiplies the texel by the vertex color, which is what the classic shader hard-codes;
// GL_REPLACE (flag 1) takes the texel alone. The compat layer's GlCompatContext.TexEnv sets the flag, so
// honoring it here is what makes glTexEnv mean anything at all on this path.

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
	@location(1) texCoord : vec2<f32>,
	@location(2) color : vec4<f32>,
};

struct VertexOutput
{
	@builtin(position) clipPosition : vec4<f32>,
	@location(0) color : vec4<f32>,
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
	@location(2) texCoord : vec2<f32>,
};

struct SmoothFragmentInput
{
	@location(0) color : vec4<f32>,
	@location(2) texCoord : vec2<f32>,
};

struct FlatFragmentInput
{
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
	@location(2) texCoord : vec2<f32>,
};

@vertex
fn vertexMain(input : VertexInput) -> VertexOutput
{
	var output : VertexOutput;
	let viewPosition = uniforms.modelViewMatrix * vec4<f32>(input.position, 1.0);
	output.clipPosition = uniforms.projectionMatrix * viewPosition;
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

@fragment
fn fragmentMain(input : SmoothFragmentInput) -> @location(0) vec4<f32>
{
	return combine(textureSample(diffuseTexture, textureSampler, input.texCoord), input.color);
}

@fragment
fn fragmentMainFlat(input : FlatFragmentInput) -> @location(0) vec4<f32>
{
	return combine(textureSample(diffuseTexture, textureSampler, input.texCoord), input.flatColor);
}
