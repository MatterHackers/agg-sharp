// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuBlur's two passes: a stack blur (agg's stack_blur - each tap weighted radius + 1 - |offset|, the sum divided
// by (radius + 1)^2) along one axis of a premultiplied layer. The horizontal pass writes a second layer; the
// vertical pass is drawn source-over onto the target, optionally colouring each pixel by its blurred alpha from a
// 256-entry table (BlendFromLut on a blurred gray coverage image). Taps past the layer's edge repeat the edge
// texel, as stack_blur does.

struct BlurSettings
{
	// x = radius in texels; y = 1 for the vertical pass; z = 1 to colour by the table; w unused.
	values : vec4<f32>,
};

@group(0) @binding(0) var source : texture_2d<f32>;
@group(0) @binding(1) var colorTable : texture_2d<f32>;
@group(0) @binding(2) var<uniform> settings : BlurSettings;

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

@fragment
fn fragmentMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let last = vec2<i32>(textureDimensions(source)) - vec2<i32>(1, 1);
	let pixel = vec2<i32>(position.xy);
	let radius = i32(settings.values.x);
	var step = vec2<i32>(1, 0);
	if (settings.values.y > 0.5)
	{
		step = vec2<i32>(0, 1);
	}

	var sum = vec4<f32>(0.0);
	for (var i = -radius; i <= radius; i = i + 1)
	{
		let tap = clamp(pixel + (step * i), vec2<i32>(0, 0), last);
		sum = sum + (textureLoad(source, tap, 0) * f32(radius + 1 - abs(i)));
	}

	var color = sum / f32((radius + 1) * (radius + 1));
	if (settings.values.z > 0.5)
	{
		let entry = textureLoad(colorTable, vec2<i32>(i32(round(clamp(color.a, 0.0, 1.0) * 255.0)), 0), 0);
		color = vec4<f32>(entry.rgb * entry.a, entry.a);
	}

	return color;
}

// BlurUnder's vertical pass: the same stack blur down the horizontal pass's layer, then mixed over the frame's own
// copy by the region's coverage and written without blending, so outside the region the frame is left as it was.
@group(0) @binding(3) var coverage : texture_2d<f32>;
@group(0) @binding(4) var original : texture_2d<f32>;

@fragment
fn underMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let last = vec2<i32>(textureDimensions(source)) - vec2<i32>(1, 1);
	let pixel = vec2<i32>(position.xy);
	let radius = i32(settings.values.x);
	var sum = vec4<f32>(0.0);
	for (var i = -radius; i <= radius; i = i + 1)
	{
		let tap = clamp(pixel + vec2<i32>(0, i), vec2<i32>(0, 0), last);
		sum = sum + (textureLoad(source, tap, 0) * f32(radius + 1 - abs(i)));
	}

	let blurred = sum / f32((radius + 1) * (radius + 1));
	let cover = textureLoad(coverage, pixel, 0).a;
	return mix(textureLoad(original, pixel, 0), blurred, cover);
}
