// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuRegionBlur's passes: agg's software blurs (stack_blur, recursive_blur, slight_blur and simple_blur's 3x3 box)
// run over a box of the frame as over a sub-image, in the software's own arithmetic so the bytes match. The box's
// pixels live in `pixels` as packed rgb bytes (4 bytes a pixel, so a 4K box fits a default 128 MiB binding; every value
// kept between passes is a byte, so the packing is exact), row-major from the box's top device row; the software's row 0 is
// the box's bottom device row, which matters only to the recursive blur's column pass (the IIR is not symmetric at
// its ends). The across pass writes each pixel blurred along its row, rounded to a byte as the software writes it
// back; the down pass (recursive) or the show pass (stack, slight) blurs along the columns; the show pass mixes the
// result over the frame by the region's coverage, blurring only the masked colour channels.

struct RegionBlurSettings
{
	// The box's top-left device pixel and its size.
	origin : vec2<i32>,
	size : vec2<i32>,
	// 0 stack, 1 recursive, 2 box 3x3, 3 slight.
	kind : i32,
	// Stack: the whole radius, and stack_blur's multiply-and-shift division (mul 0: divide).
	radius : i32,
	stackMul : u32,
	stackShr : u32,
	// 1 for each colour channel to blur.
	channels : vec4<f32>,
	// Recursive: b, b1, b2, b3. Slight: the center and neighbour weights.
	weights : vec4<f32>,
	// Recursive: the first line (row or column) of this batch and the line past its last; `scratch` holds the batch.
	lines : vec2<i32>,
};

@group(0) @binding(0) var frame : texture_2d<f32>;
@group(0) @binding(1) var coverage : texture_2d<f32>;
@group(0) @binding(2) var<uniform> settings : RegionBlurSettings;
@group(0) @binding(3) var<storage, read_write> pixels : array<u32>;
// Recursive: each line's causal pass, unrounded, for its anti-causal pass - one batch of lines at a time, as many as
// the device's storage binding limit allows.
@group(0) @binding(5) var<storage, read_write> scratch : array<vec4<f32>>;

fn packBytes(value : vec3<f32>) -> u32
{
	let bytes = vec3<u32>(value);
	return bytes.r | (bytes.g << 8u) | (bytes.b << 16u);
}

fn unpackBytes(packed : u32) -> vec3<f32>
{
	return vec3<f32>(vec3<u32>(packed & 0xffu, (packed >> 8u) & 0xffu, (packed >> 16u) & 0xffu));
}

fn frameBytes(column : i32, row : i32) -> vec4<f32>
{
	return round(textureLoad(frame, settings.origin + vec2<i32>(column, row), 0) * 255.0);
}

// stack_blur's CalcPix: the table's multiply and shift, wrapping in 32 bits as C++'s unsigned product does.
fn stackDivide(sum : vec3<u32>) -> vec3<f32>
{
	if (settings.stackMul != 0u)
	{
		return vec3<f32>((sum * settings.stackMul) >> vec3<u32>(settings.stackShr));
	}

	let div = u32((settings.radius + 1) * (settings.radius + 1));
	return vec3<f32>(sum / div);
}

// SlightBlur.Weigh, in its order of operations, rounded half up.
fn slightWeigh(v1 : vec3<f32>, v2 : vec3<f32>, v3 : vec3<f32>) -> vec3<f32>
{
	let g0 = settings.weights.x;
	let g1 = settings.weights.y;
	return floor((((g1 * v1) + (g0 * v2)) + (g1 * v3)) + 0.5);
}

// Stack and slight: one pixel of the box blurred along its row.
@compute @workgroup_size(8, 8)
fn acrossPixels(@builtin(global_invocation_id) id : vec3<u32>)
{
	let column = i32(id.x);
	let row = i32(id.y);
	let width = settings.size.x;
	if (column >= width || row >= settings.size.y)
	{
		return;
	}

	var result = vec3<f32>(0.0);
	if (settings.kind == 0)
	{
		let radius = settings.radius;
		var sum = vec3<u32>(0u);
		for (var i = -radius; i <= radius; i = i + 1)
		{
			let tap = frameBytes(clamp(column + i, 0, width - 1), row);
			sum = sum + (vec3<u32>(tap.rgb) * u32(radius + 1 - abs(i)));
		}

		result = stackDivide(sum);
	}
	else
	{
		let left = frameBytes(max(column - 1, 0), row).rgb;
		let right = frameBytes(min(column + 1, width - 1), row).rgb;
		result = slightWeigh(left, frameBytes(column, row).rgb, right);
	}

	pixels[(row * width) + column] = packBytes(result);
}

// RecursizeBlurCalculator.ToByte: rounded, clamped where the filter rings past the byte range.
fn toByte(value : vec3<f32>) -> vec3<f32>
{
	return clamp(floor(value + 0.5), vec3<f32>(0.0), vec3<f32>(255.0));
}

// RecursiveBlur.blur_x along one line of `pixels` (start, then step apart), in place: the causal pass forward into
// `scratch` (from scratchStart), then the anti-causal pass back, each seeded as the software seeds its ends. Below 3
// pixels the line is left alone.
fn recursiveLine(start : i32, step : i32, length : i32, scratchStart : i32)
{
	if (length < 3)
	{
		return;
	}

	let b = settings.weights.x;
	let b1 = settings.weights.y;
	let b2 = settings.weights.z;
	let b3 = settings.weights.w;

	let first = unpackBytes(pixels[start]);
	var s = (((b * first) + (b1 * first)) + (b2 * first)) + (b3 * first);
	scratch[scratchStart] = vec4<f32>(s, 0.0);
	var p1 = s;
	var p2 = s;
	var p3 = s;
	for (var i = 1; i < length; i = i + 1)
	{
		s = (((b * unpackBytes(pixels[start + (i * step)])) + (b1 * p1)) + (b2 * p2)) + (b3 * p3);
		scratch[scratchStart + i] = vec4<f32>(s, 0.0);
		p3 = p2;
		p2 = p1;
		p1 = s;
	}

	let last = scratch[scratchStart + length - 1].rgb;
	var t = (((b * last) + (b1 * last)) + (b2 * last)) + (b3 * last);
	pixels[start + ((length - 1) * step)] = packBytes(toByte(t));
	var q1 = t;
	var q2 = t;
	var q3 = t;
	for (var i = length - 2; i >= 0; i = i - 1)
	{
		t = (((b * scratch[scratchStart + i].rgb) + (b1 * q1)) + (b2 * q2)) + (b3 * q3);
		pixels[start + (i * step)] = packBytes(toByte(t));
		q3 = q2;
		q2 = q1;
		q1 = t;
	}
}

// Recursive: one row of the batch, the frame's bytes first loaded into it.
@compute @workgroup_size(64)
fn acrossRows(@builtin(global_invocation_id) id : vec3<u32>)
{
	let row = settings.lines.x + i32(id.x);
	let width = settings.size.x;
	if (row >= settings.lines.y)
	{
		return;
	}

	for (var column = 0; column < width; column = column + 1)
	{
		pixels[(row * width) + column] = packBytes(frameBytes(column, row).rgb);
	}

	recursiveLine(row * width, 1, width, i32(id.x) * width);
}

// Recursive: one column of the batch, from the software's row 0 - the bottom device row - up.
@compute @workgroup_size(64)
fn downColumns(@builtin(global_invocation_id) id : vec3<u32>)
{
	let column = settings.lines.x + i32(id.x);
	let width = settings.size.x;
	let height = settings.size.y;
	if (column >= settings.lines.y)
	{
		return;
	}

	recursiveLine(((height - 1) * width) + column, -width, height, i32(id.x) * height);
}

@group(0) @binding(4) var<storage, read> blurred : array<u32>;

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it to the box.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

fn across(column : i32, row : i32) -> vec3<f32>
{
	return unpackBytes(blurred[(row * settings.size.x) + column]);
}

@fragment
fn showMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let pixel = vec2<i32>(position.xy);
	let original = textureLoad(frame, pixel, 0);
	let column = pixel.x - settings.origin.x;
	let row = pixel.y - settings.origin.y;
	let width = settings.size.x;
	let height = settings.size.y;

	var result = vec3<f32>(0.0);
	if (settings.kind == 0)
	{
		let radius = settings.radius;
		var sum = vec3<u32>(0u);
		for (var i = -radius; i <= radius; i = i + 1)
		{
			sum = sum + (vec3<u32>(across(column, clamp(row + i, 0, height - 1))) * u32(radius + 1 - abs(i)));
		}

		result = stackDivide(sum);
	}
	else if (settings.kind == 1)
	{
		result = across(column, row);
	}
	else if (settings.kind == 2)
	{
		// simple_blur leaves a pixel whose block leaves the image as it is.
		if (column < 1 || row < 1 || column >= width - 1 || row >= height - 1)
		{
			return original;
		}

		var sum = vec3<i32>(0);
		for (var dy = -1; dy <= 1; dy = dy + 1)
		{
			for (var dx = -1; dx <= 1; dx = dx + 1)
			{
				sum = sum + vec3<i32>(frameBytes(column + dx, row + dy).rgb);
			}
		}

		result = vec3<f32>(sum / 9);
	}
	else
	{
		// The software's row below is its y - 1: the device row after this one.
		let below = across(column, min(row + 1, height - 1));
		let above = across(column, max(row - 1, 0));
		result = slightWeigh(below, across(column, row), above);
	}

	let masked = mix(original.rgb, result / 255.0, settings.channels.rgb);
	let cover = textureLoad(coverage, pixel, 0).a;
	return vec4<f32>(mix(original.rgb, masked, cover), original.a);
}
