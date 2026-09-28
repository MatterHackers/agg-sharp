// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuImageFilter's pass: C++ AGG's image span generators evaluated per pixel. The path has already been drawn in
// white into a coverage layer; each pixel here steps span_interpolator_linear's dda along software's span, or
// where there is none maps its centre through the device-to-image homography to 1/256 subpixels (as
// span_interpolator_trans does), runs the chosen generator's integer arithmetic over the
// image through the chosen accessor, scales the colour by the coverage and is drawn source-over (premultiplied)
// onto the target. The image's texels hold its straight-alpha bytes; image row 0 is texture row 0.

struct Settings
{
	// Device pixel (x, y, 1) to image (X, Y, W): x' = X / W, y' = Y / W.
	row0 : vec4<f32>,
	row1 : vec4<f32>,
	row2 : vec4<f32>,
	// The inverse, image to device: the perspective resample's local scale is its derivative.
	inv0 : vec4<f32>,
	inv1 : vec4<f32>,
	inv2 : vec4<f32>,
	// x = kind (0 nearest, 1 bilinear, 2 filter, 3 resample), y = edge (0 clamp, 1 clip, 2 repeat, 3 reflect),
	// z = image width, w = image height.
	mode : vec4<i32>,
	// x = diameter, y = start, z = 1 for the 2x2 generator's clamp of colour to alpha,
	// w = 1 when the resample's scale is the constant below (the affine generator).
	table : vec4<i32>,
	// The affine resample's rx, ry, rx_inv, ry_inv.
	resample : vec4<i32>,
	// The perspective resample's x = scale limit, y = blur x, z = blur y (in subpixels); w = 1 when the rows map
	// only to the screen and the bilinear quad transform below takes the screen to the image.
	resampleLimits : vec4<i32>,
	// The clip accessor's background, 0..255.
	background : vec4<i32>,
	// trans_bilinear: x' = x.x + x.y * x * y + x.z * x + x.w * y, and y' likewise.
	bilinearX : vec4<f32>,
	bilinearY : vec4<f32>,
	// x = 1 when each pixel's alpha comes from the brightness table (span_conv_brightness_alpha), y = its length;
	// z = 1 when the image is a retained layer's texture: premultiplied, with its top row in texture row 0;
	// w = 1 for ImageFilterFill.Opaque: full alpha and colour clamped to 0..255 only, as the rgb generators give.
	alphaTable : vec4<i32>,
};

@group(0) @binding(0) var coverage : texture_2d<f32>;
@group(0) @binding(1) var imageTexture : texture_2d<f32>;
@group(0) @binding(2) var weights : texture_2d<f32>;
@group(0) @binding(3) var<uniform> settings : Settings;
@group(0) @binding(4) var brightnessToAlpha : texture_2d<f32>;
// GpuImageFilterSpans: 8 header ints (enabled, first row, row count, device-to-screen x and y offsets), a
// (first span, span count) pair per screen row, then 6 ints a span (start x, length, x1, x2, y1, y2).
@group(0) @binding(5) var<storage, read> spans : array<i32>;

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it to the path's bounds.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

fn wrapped(v : i32, size : i32, edge : i32) -> i32
{
	if (edge == 2)
	{
		return ((v % size) + size) % size;
	}

	let size2 = size * 2;
	let folded = ((v % size2) + size2) % size2;
	if (folded >= size)
	{
		return size2 - folded - 1;
	}

	return folded;
}

// The image's bytes at (x, y), read as the accessor reads past the edge.
fn pixel(x : i32, y : i32) -> vec4<i32>
{
	let width = settings.mode.z;
	let height = settings.mode.w;
	let edge = settings.mode.y;
	var px = x;
	var py = y;
	if (edge == 1)
	{
		if (x < 0 || y < 0 || x >= width || y >= height)
		{
			return settings.background;
		}
	}
	else if (edge == 0)
	{
		px = clamp(x, 0, width - 1);
		py = clamp(y, 0, height - 1);
	}
	else
	{
		px = wrapped(x, width, edge);
		py = wrapped(y, height, edge);
	}

	if (settings.alphaTable.z == 1)
	{
		py = height - 1 - py;
	}

	return vec4<i32>(round(textureLoad(imageTexture, vec2<i32>(px, py), 0) * 255.0));
}

// ImageFilterLookUpTable.weight_array()[i]: 256 entries a row, each stored as weight + 32768 in red (low byte)
// and green (high byte).
fn weight(i : i32) -> i32
{
	let texel = round(textureLoad(weights, vec2<i32>(i & 255, i >> 8u), 0) * 255.0);
	return i32(texel.r) + (i32(texel.g) * 256) - 32768;
}

fn toImage(device : vec2<f32>) -> vec2<f32>
{
	let p = vec3<f32>(device, 1.0);
	let w = dot(settings.row2.xyz, p);
	let mapped = vec2<f32>(dot(settings.row0.xyz, p), dot(settings.row1.xyz, p)) / w;
	if (settings.resampleLimits.w == 1)
	{
		let terms = vec4<f32>(1.0, mapped.x * mapped.y, mapped.x, mapped.y);
		return vec2<f32>(dot(settings.bilinearX, terms), dot(settings.bilinearY, terms));
	}

	return mapped;
}

fn nearest(x_hr : i32, y_hr : i32) -> vec4<i32>
{
	return pixel(x_hr >> 8u, y_hr >> 8u);
}

fn bilinear(xh : i32, yh : i32) -> vec4<i32>
{
	let x_hr = xh - 128;
	let y_hr = yh - 128;
	let x_lr = x_hr >> 8u;
	let y_lr = y_hr >> 8u;
	let fx = x_hr & 255;
	let fy = y_hr & 255;
	var fg = vec4<i32>(32768);
	fg += pixel(x_lr, y_lr) * ((256 - fx) * (256 - fy));
	fg += pixel(x_lr + 1, y_lr) * (fx * (256 - fy));
	fg += pixel(x_lr, y_lr + 1) * ((256 - fx) * fy);
	fg += pixel(x_lr + 1, y_lr + 1) * (fx * fy);
	return fg >> vec4<u32>(16u);
}

// The alpha a generator gives, and clamps colour to: its own, or full as the rgb generators give (ImageFilterFill.Opaque).
fn outputAlpha(a : i32) -> i32
{
	if (settings.alphaTable.w == 1)
	{
		return 255;
	}

	return a;
}

// span_image_filter_rgba, and span_image_filter_rgba_2x2 (its middle two taps, colour clamped to alpha).
fn filtered(xh : i32, yh : i32) -> vec4<i32>
{
	let diameter = settings.table.x;
	let start = settings.table.y;
	let x_hr = xh - 128;
	let y_hr = yh - 128;
	let x_lr = x_hr >> 8u;
	let y_lr = y_hr >> 8u;
	let x_fract = x_hr & 255;
	var fg = vec4<i32>(8192);
	if (settings.table.z == 1)
	{
		// span_image_filter_rgba_2x2 indexes the table from the fraction, not its complement.
		let fy = y_hr & 255;
		fg += pixel(x_lr, y_lr) * (((weight(x_fract + 256) * weight(fy + 256)) + 8192) >> 14u);
		fg += pixel(x_lr + 1, y_lr) * (((weight(x_fract) * weight(fy + 256)) + 8192) >> 14u);
		fg += pixel(x_lr, y_lr + 1) * (((weight(x_fract + 256) * weight(fy)) + 8192) >> 14u);
		fg += pixel(x_lr + 1, y_lr + 1) * (((weight(x_fract) * weight(fy)) + 8192) >> 14u);
		fg = fg >> vec4<u32>(14u);
		let a = outputAlpha(min(fg.a, 255));
		return vec4<i32>(min(fg.rgb, vec3<i32>(a)), a);
	}

	var wy = 255 - (y_hr & 255);
	for (var j = 0; j < diameter; j++)
	{
		let weight_y = weight(wy);
		var wx = 255 - x_fract;
		for (var i = 0; i < diameter; i++)
		{
			let w = ((weight_y * weight(wx)) + 8192) >> 14u;
			fg += pixel(x_lr + start + i, y_lr + start + j) * w;
			wx += 256;
		}

		wy += 256;
	}

	fg = fg >> vec4<u32>(14u);
	fg = clamp(fg, vec4<i32>(0), vec4<i32>(255));
	fg.a = outputAlpha(fg.a);
	if (settings.alphaTable.z == 1)
	{
		// A layer is premultiplied, so, as C++ span_image_filter_rgba does, no channel may exceed alpha: a negative
		// lobe that lowers alpha more than colour would otherwise leave a bright halo. A straight-alpha image keeps
		// its colour, as ImageGraphics2D's generator does (see span_image_filter_rgba there).
		fg = vec4<i32>(min(fg.rgb, vec3<i32>(fg.a)), fg.a);
	}

	return fg;
}

// span_interpolator_persp_exact's local scale along one image axis: 256 over the device dist of a 1/256 step.
fn localScale(imagePoint : vec2<f32>, axis : vec2<f32>) -> i32
{
	let p = vec3<f32>(imagePoint, 1.0);
	let w = dot(settings.inv2.xyz, p);
	let x = dot(settings.inv0.xyz, p);
	let y = dot(settings.inv1.xyz, p);
	let dw = dot(settings.inv2.xy, axis);
	let dx = ((dot(settings.inv0.xy, axis) * w) - (x * dw)) / (w * w);
	let dy = ((dot(settings.inv1.xy, axis) * w) - (y * dw)) / (w * w);
	let dist = sqrt((dx * dx) + (dy * dy)) / 256.0;
	return i32(round(256.0 / dist)) >> 8u;
}

// span_image_resample's adjust_scale.
fn adjustScale(r : i32, limit : i32, blur : i32) -> i32
{
	var v = clamp(r, 256, 256 * limit);
	v = (v * blur) >> 8u;
	return max(v, 256);
}

// span_image_resample_rgba(_affine): the table widened to rx by ry subpixels per image pixel.
fn resampled(xh : i32, yh : i32, imagePoint : vec2<f32>) -> vec4<i32>
{
	var rx = settings.resample.x;
	var ry = settings.resample.y;
	var rx_inv = settings.resample.z;
	var ry_inv = settings.resample.w;
	if (settings.table.w == 0)
	{
		rx = adjustScale(localScale(imagePoint, vec2<f32>(1.0, 0.0)), settings.resampleLimits.x, settings.resampleLimits.y);
		ry = adjustScale(localScale(imagePoint, vec2<f32>(0.0, 1.0)), settings.resampleLimits.x, settings.resampleLimits.z);
		rx_inv = 65536 / rx;
		ry_inv = 65536 / ry;
	}

	let diameter = settings.table.x;
	let filter_scale = diameter << 8u;
	let radius_x = (diameter * rx) >> 1u;
	let radius_y = (diameter * ry) >> 1u;
	let x = xh + 128 - radius_x;
	let y = yh + 128 - radius_y;
	let x_lr = x >> 8u;
	let y_lr = y >> 8u;
	let x_hr2 = ((255 - (x & 255)) * rx_inv) >> 8u;
	var y_hr = ((255 - (y & 255)) * ry_inv) >> 8u;
	var sum = vec4<i32>(0);
	var total = 0;
	var j = 0;
	loop
	{
		let weight_y = weight(y_hr);
		var x_hr = x_hr2;
		var i = 0;
		loop
		{
			let w = ((weight_y * weight(x_hr)) + 8192) >> 14u;
			sum += pixel(x_lr + i, y_lr + j) * w;
			total += w;
			x_hr += rx_inv;
			if (x_hr >= filter_scale)
			{
				break;
			}

			i++;
		}

		y_hr += ry_inv;
		if (y_hr >= filter_scale)
		{
			break;
		}

		j++;
	}

	let halfTotal = total / 2;
	let a = outputAlpha(clamp((sum.a + halfTotal) / total, 0, 255));
	return vec4<i32>(min(clamp((sum.rgb + vec3<i32>(halfTotal)) / total, vec3<i32>(0), vec3<i32>(255)), vec3<i32>(a)), a);
}

// dda2_line_interpolator(y1, y2, count) after k steps, in closed form: count steps of lft and rem carries, rem
// taken into 1..count as its constructor takes it (a remainder of 0 too).
fn dda(y1 : i32, y2 : i32, count : i32, k : i32) -> i32
{
	let cnt = max(count, 1);
	var lft = (y2 - y1) / cnt;
	var rem = (y2 - y1) % cnt;
	if (rem <= 0)
	{
		rem += cnt;
		lft -= 1;
	}

	return y1 + (k * lft) + ((((k + 1) * rem) - 1) / cnt);
}

// span_interpolator_linear's subpixel coordinates for the device pixel at position, along the software span that
// holds it; false where there is no such span (the table is off, or the GPU's anti-aliasing reaches a pixel
// software's rasterizer does not).
fn spanCoordinates(position : vec2<f32>, hr : ptr<function, vec2<i32>>) -> bool
{
	if (spans[0] == 0)
	{
		return false;
	}

	let x = i32(floor(position.x)) + spans[3];
	let row = spans[4] - i32(floor(position.y)) - 1 - spans[1];
	if (row < 0 || row >= spans[2])
	{
		return false;
	}

	let spansAt = 8 + (spans[2] * 2);
	let first = spans[8 + (row * 2)];
	let count = spans[9 + (row * 2)];
	for (var i = 0; i < count; i++)
	{
		let s = spansAt + ((first + i) * 6);
		let k = x - spans[s];
		let len = spans[s + 1];
		if (k >= 0 && k < len)
		{
			*hr = vec2<i32>(dda(spans[s + 2], spans[s + 3], len, k), dda(spans[s + 4], spans[s + 5], len, k));
			return true;
		}
	}

	return false;
}

@fragment
fn fragmentMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let cover = textureLoad(coverage, vec2<i32>(position.xy), 0).a;
	let imagePoint = toImage(position.xy);
	var hr = vec2<i32>(i32(floor((imagePoint.x * 256.0) + 0.5)), i32(floor((imagePoint.y * 256.0) + 0.5)));
	spanCoordinates(position.xy, &hr);
	let x_hr = hr.x;
	let y_hr = hr.y;
	var bytes : vec4<i32>;
	switch settings.mode.x
	{
		case 0: { bytes = nearest(x_hr, y_hr); }
		case 1: { bytes = bilinear(x_hr, y_hr); }
		case 2: { bytes = filtered(x_hr, y_hr); }
		default: { bytes = resampled(x_hr, y_hr, imagePoint); }
	}

	if (settings.alphaTable.x == 1)
	{
		let levels = settings.alphaTable.y;
		let index = min((bytes.r + bytes.g + bytes.b) * levels / (3 * 255), levels - 1);
		bytes.a = i32(round(textureLoad(brightnessToAlpha, vec2<i32>(index, 0), 0).r * 255.0));
	}
	else
	{
		// Nearest and bilinear, which clamp nothing.
		bytes.a = outputAlpha(bytes.a);
	}

	let color = vec4<f32>(bytes) / 255.0;
	if (settings.alphaTable.z == 1)
	{
		// Already premultiplied: only the coverage scales it.
		return color * cover;
	}

	let alpha = color.a * cover;
	return vec4<f32>(color.rgb * alpha, alpha);
}
