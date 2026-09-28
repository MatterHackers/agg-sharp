// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// SsaaRenderTarget's composite: box-downsamples an N x N supersampled texture onto a rectangle of the
// destination, premultiplied source-over (the pipeline's blend state). Ported from agg-gui's
// agg-gui-wgpu ssaa.rs, which uses a bilinear tap at 2x and dedicated 3x3/4x4 box pipelines; one loop
// over the N x N block is the same filter at every factor, and at 1x it is an exact copy.
//
// No MSAA: every attachment involved has sample count 1. Supersampling is done purely by size.

struct SsaaSettings
{
	// The destination rectangle in clip space: left, bottom, right, top.
	destination : vec4<f32>,
	// x = the linear supersample factor N (1..4); yzw unused.
	factor : vec4<f32>,
};

@group(0) @binding(0) var source : texture_2d<f32>;
@group(0) @binding(1) var<uniform> settings : SsaaSettings;

struct SsaaVertexOutput
{
	@builtin(position) position : vec4<f32>,
	@location(0) texCoord : vec2<f32>,
};

// Two triangles over the destination rectangle; uv runs top-down like the texture, whose row 0 is the top
// of the picture.
@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> SsaaVertexOutput
{
	var corners = array<vec2<f32>, 6>(
		vec2<f32>(0.0, 0.0),
		vec2<f32>(1.0, 0.0),
		vec2<f32>(0.0, 1.0),
		vec2<f32>(0.0, 1.0),
		vec2<f32>(1.0, 0.0),
		vec2<f32>(1.0, 1.0));
	let corner = corners[vertexIndex];
	let rect = settings.destination;

	var output : SsaaVertexOutput;
	output.position = vec4<f32>(mix(rect.x, rect.z, corner.x), mix(rect.y, rect.w, corner.y), 0.0, 1.0);
	output.texCoord = vec2<f32>(corner.x, 1.0 - corner.y);
	return output;
}

// The colour the fragment entry point returns. WgslShaderSources' linear-light variant of this module (the key with
// "+LinearPremultiplied") swaps this one line to convert the premultiplied sRGB average to linear light, for a
// downsample composited into a linear-light layer; keep it on one line, exactly as written.
fn outputColor(c : vec4<f32>) -> vec4<f32> { return c; }

// The unweighted average of the N x N source block behind the destination pixel. At a 1:1 composite the
// pixel centre lands at N * i + N / 2 source texels, so subtracting N / 2 (and rounding) gives exactly the
// block's first texel; textureLoad reads texels directly, so no sampler filtering blurs the box.
@fragment
fn fragmentMain(input : SsaaVertexOutput) -> @location(0) vec4<f32>
{
	let n = max(1, i32(settings.factor.x));
	let size = vec2<i32>(textureDimensions(source));
	let first = vec2<i32>(floor(input.texCoord * vec2<f32>(size) - vec2<f32>(f32(n) * 0.5) + vec2<f32>(0.5)));

	var sum = vec4<f32>(0.0);
	for (var y = 0; y < n; y = y + 1)
	{
		for (var x = 0; x < n; x = x + 1)
		{
			let texel = clamp(first + vec2<i32>(x, y), vec2<i32>(0), size - vec2<i32>(1));
			sum = sum + textureLoad(source, texel, 0);
		}
	}

	return outputColor(sum / f32(n * n));
}
