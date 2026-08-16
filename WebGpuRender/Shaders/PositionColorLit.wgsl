// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// Lit, per-vertex color. Ported from VorticeD3D/Shaders/PositionColorLit.hlsl; see PositionColor.wgsl for
// the two rules that apply to every canned module (vec * mat, and the projection already carries the
// clip-depth remap).
//
// The lighting is the fixed-function-ish model the classic path settled on rather than anything
// principled: a flat 0.2 ambient term on the base color, plus each enabled light's ambient and its
// N.L diffuse, saturated. Phase 3 refines it; until then, faithfulness to the oracle is the point,
// because the golden images were captured from the oracle.

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

struct VertexInput
{
	@location(0) position : vec3<f32>,
	@location(1) normal : vec3<f32>,
	@location(2) color : vec4<f32>,
};

struct VertexOutput
{
	@builtin(position) clipPosition : vec4<f32>,
	@location(0) color : vec4<f32>,
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
	@location(2) viewNormal : vec3<f32>,
};

struct SmoothFragmentInput
{
	@location(0) color : vec4<f32>,
	@location(2) viewNormal : vec3<f32>,
};

struct FlatFragmentInput
{
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
	@location(2) viewNormal : vec3<f32>,
};

@vertex
fn vertexMain(input : VertexInput) -> VertexOutput
{
	var output : VertexOutput;
	let viewPosition = uniforms.modelViewMatrix * vec4<f32>(input.position, 1.0);
	output.clipPosition = uniforms.projectionMatrix * viewPosition;

	// w = 0 so the model-view translation does not move a direction.
	output.viewNormal = normalize((uniforms.modelViewMatrix * vec4<f32>(input.normal, 0.0)).xyz);
	output.color = input.color;
	output.flatColor = input.color;
	return output;
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
	return vec4<f32>(applyLighting(input.color.rgb, input.viewNormal), input.color.a);
}

@fragment
fn fragmentMainFlat(input : FlatFragmentInput) -> @location(0) vec4<f32>
{
	return vec4<f32>(applyLighting(input.flatColor.rgb, input.viewNormal), input.flatColor.a);
}
