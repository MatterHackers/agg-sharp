// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuGradientFill's pass: C++ AGG's span_gradient evaluated per pixel. The path has already been drawn in white
// into a coverage layer (the same halo-anti-aliased fill as any solid fill); each pixel here takes its gradient
// colour from the lookup table, scales it by that coverage and is drawn source-over (premultiplied) onto the
// target. The gradient runs in span_gradient's 1/16 subpixel units, truncated as its integer maths truncates.
// An optional second gradient replaces the alpha (span_gradient_alpha).

struct Gradient
{
	// Device pixel (x, y, 1) to gradient space: x' = dot(row0.xyz, p), y' = dot(row1.xyz, p).
	row0 : vec4<f32>,
	row1 : vec4<f32>,
	// x = shape (GradientShape), y = spread (GradientSpread), z = d1, w = d2, both in subpixels.
	range : vec4<f32>,
	// RadialFocus: x = r, y = fx, z = fy (subpixels), w = gradient_radial_focus's m_mul.
	focus : vec4<f32>,
	// x = the table's size, y = 1 when this gradient is used.
	table : vec4<f32>,
};

struct Settings
{
	color : Gradient,
	alpha : Gradient,
};

@group(0) @binding(0) var coverage : texture_2d<f32>;
@group(0) @binding(1) var colorTable : texture_2d<f32>;
@group(0) @binding(2) var alphaTable : texture_2d<f32>;
@group(0) @binding(3) var<uniform> settings : Settings;

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it to the path's bounds.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

fn gradientDistance(g : Gradient, pixel : vec2<f32>) -> f32
{
	let p = vec3<f32>(pixel, 1.0);
	let x = floor(dot(g.row0.xyz, p) * 16.0);
	let y = floor(dot(g.row1.xyz, p) * 16.0);
	let shape = i32(g.range.x);
	let d = g.range.w;
	var v = 0.0;
	switch shape
	{
		case 0: { v = x; }
		case 1: { v = y; }
		case 2: { v = floor(sqrt((x * x) + (y * y))); }
		case 3: { v = max(abs(x), abs(y)); }
		case 4: { v = trunc(abs(x) * abs(y) / d); }
		case 5: { v = floor(sqrt(abs(x) * abs(y))); }
		case 6: { v = round(abs(atan2(y, x)) * d / 3.14159265358979); }
		default:
		{
			let dx = x - g.focus.y;
			let dy = y - g.focus.z;
			let d2 = (dx * g.focus.z) - (dy * g.focus.y);
			let d3 = (g.focus.x * g.focus.x * ((dx * dx) + (dy * dy))) - (d2 * d2);
			v = round(((dx * g.focus.y) + (dy * g.focus.z) + sqrt(abs(d3))) * g.focus.w);
		}
	}

	let spread = i32(g.range.y);
	if (spread == 1)
	{
		v = v - (floor(v / d) * d);
	}
	else if (spread == 2)
	{
		let d2 = d * 2.0;
		v = v - (floor(v / d2) * d2);
		if (v >= d)
		{
			v = d2 - v;
		}
	}

	return v;
}

fn lookUp(g : Gradient, table : texture_2d<f32>, pixel : vec2<f32>) -> vec4<f32>
{
	let size = g.table.x;
	let dd = max(g.range.w - g.range.z, 1.0);
	let index = clamp(trunc((gradientDistance(g, pixel) - g.range.z) * size / dd), 0.0, size - 1.0);
	return textureLoad(table, vec2<i32>(i32(index), 0), 0);
}

// The colour a fragment entry point returns. WgslShaderSources' linear-light variants of this module (the key with
// "+LinearStraight" or "+LinearPremultiplied") replace this one line with an sRGB-to-linear conversion, for draws
// into a linear float target (a GpuRetainedLayer made with linearLight).
fn outputColor(c : vec4<f32>) -> vec4<f32> { return c; }

@fragment
fn fragmentMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let cover = textureLoad(coverage, vec2<i32>(position.xy), 0).a;
	var color = lookUp(settings.color, colorTable, position.xy);
	if (settings.alpha.table.y > 0.5)
	{
		color.a = lookUp(settings.alpha, alphaTable, position.xy).a;
	}

	let alpha = color.a * cover;
	return outputColor(vec4<f32>(color.rgb * alpha, alpha));
}
