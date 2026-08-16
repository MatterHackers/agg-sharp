// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// Unlit, per-vertex color - the 2D UI path's workhorse. Ported from VorticeD3D/Shaders/PositionColor.hlsl.
//
// Two things about this file are not obvious and are the same in all four canned modules:
//
//  1. Matrices multiply vectors from the LEFT (mat * vec), even though this codebase uses row-vector
//     matrices and the HLSL original writes mul(vec, mat). The transposition is already paid for by the
//     storage: GlUniformBlock.WriteMatrix emits Row0..Row3 in order, and a WGSL mat4x4<f32> reads those
//     same bytes as COLUMN 0..3, so the matrix the shader sees is the transpose of the one that was
//     written. Transposed twice is not transposed, so `M * v` here computes exactly the `v * M` the
//     matrix stack means.
//
//     The integration tests are the evidence: with vec * mat every transform is transposed and an
//     orthographic 2D quad lands off screen entirely. (GlUniformBlock's <summary> said the opposite
//     until Phase 2 leg B; it now agrees with this file.)
//
//  2. The projection matrix has already been mapped into 0..w clip depth by
//     GlUniformBlock.ToClipSpaceProjection, so unlike the HLSL original there is no
//     `position.z = (z + w) * 0.5` fixup here. Doing it twice halves the depth range.
//
// The uniform struct must match GlUniformBlock byte for byte: 304 bytes, every member 16-byte aligned.
// WGSL's natural layout for this declaration produces exactly the published offsets
// (mv 0, proj 64, tex 128, light0 192/208/224, light1 240/256/272, flags 288).

struct Uniforms
{
	modelViewMatrix : mat4x4<f32>,
	projectionMatrix : mat4x4<f32>,
	// Declared so the block layout matches, but not applied: GL's texture matrix is unreachable through
	// IGpuContext (there is no MatrixMode.Texture) and the compat layer always writes identity. The
	// classic D3D11 shaders ignore it too, so applying it here would be a gratuitous divergence.
	textureMatrix : mat4x4<f32>,
	light0Position : vec4<f32>,
	light0Ambient : vec4<f32>,
	light0Diffuse : vec4<f32>,
	light1Position : vec4<f32>,
	light1Ambient : vec4<f32>,
	light1Diffuse : vec4<f32>,
	// x = light 0 enabled, y = light 1 enabled, z = lighting enabled, w = texture env is GL_REPLACE.
	flags : vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms : Uniforms;

struct VertexInput
{
	@location(0) position : vec3<f32>,
	@location(1) color : vec4<f32>,
};

// The color is emitted twice, once interpolated and once flat. WebGPU requires a fragment input's
// interpolation to match the vertex output it reads, so one vertex entry point cannot feed both a smooth
// and a flat fragment entry point through a single location - and GlShaderKeys deliberately declares one
// vertexMain per module. A second varying is the cheap way out; extra vertex outputs a fragment stage
// does not read are legal.
struct VertexOutput
{
	@builtin(position) clipPosition : vec4<f32>,
	@location(0) color : vec4<f32>,
	@location(1) @interpolate(flat) flatColor : vec4<f32>,
};

@vertex
fn vertexMain(input : VertexInput) -> VertexOutput
{
	var output : VertexOutput;
	let viewPosition = uniforms.modelViewMatrix * vec4<f32>(input.position, 1.0);
	output.clipPosition = uniforms.projectionMatrix * viewPosition;
	output.color = input.color;
	output.flatColor = input.color;
	return output;
}

@fragment
fn fragmentMain(@location(0) color : vec4<f32>) -> @location(0) vec4<f32>
{
	return color;
}

@fragment
fn fragmentMainFlat(@location(1) @interpolate(flat) flatColor : vec4<f32>) -> @location(0) vec4<f32>
{
	return flatColor;
}
