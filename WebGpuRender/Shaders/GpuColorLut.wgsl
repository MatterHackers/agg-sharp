// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuColorLut's pass: every pixel of a copy of the frame has its red, green and blue replaced by their entries in
// a 256-entry table (red's in the table's red, green's in its green, blue's in its blue), alpha kept, written back
// without blending inside the scissor - pixfmt apply_gamma_inv, or a quantisation to a packed pixel format.

@group(0) @binding(0) var source : texture_2d<f32>;
@group(0) @binding(1) var table : texture_2d<f32>;

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

fn entry(value : f32) -> vec4<f32>
{
	return textureLoad(table, vec2<i32>(i32(round(clamp(value, 0.0, 1.0) * 255.0)), 0), 0);
}

@fragment
fn fragmentMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let color = textureLoad(source, vec2<i32>(position.xy), 0);
	return vec4<f32>(entry(color.r).r, entry(color.g).g, entry(color.b).b, color.a);
}
