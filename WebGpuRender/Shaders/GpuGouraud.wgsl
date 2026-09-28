// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuGouraudFill's passes: C++ AGG's span_gouraud_rgba evaluated per pixel, at the coverage a scanline rasterizer
// gives the pixel. Each triangle is a quad over its outline's bounds (six vertices, the triangle's index being
// vertex_index / 6). The fragment works in the Graphics2D's pixels (y up, as the rasterizer's rows are):
// - the colour is span_gouraud_rgba::generate's for that row and column - rgba_calc along the edges, rounded to
//   1/16 subpixels, then dda_line_interpolator's integer steps along the row, clamped outside the span's own
//   run and wrapped to a byte inside it, as the software does;
// - the coverage is the area of the pixel square inside the outline (a convex triangle or dilated hexagon),
//   in 1/256ths as the rasterizer's cells give it, then through the rasterizer's gamma table.
// The colour is drawn premultiplied: source-over for a plain fill, added up into a layer for a compound one.

struct Edge
{
	// rgba_calc: x = m_x1, y = m_y1 (both less 0.5), z = m_dx, w = m_1dy.
	geometry : vec4<f32>,
	start : vec4<i32>,
	delta : vec4<i32>,
};

struct Triangle
{
	// rgba1 (corner 0 to 2), rgba2 (0 to 1), rgba3 (1 to 2), the corners sorted by y.
	edges : array<Edge, 3>,
	// x = m_y2, y = 1 when m_swap, z = the outline's vertex count (3 or 6).
	info : vec4<f32>,
	// The outline, two points per vector, in the Graphics2D's pixels.
	outline : array<vec4<f32>, 3>,
	// The quad drawn: min x, min y, max x, max y.
	bounds : vec4<f32>,
};

struct Settings
{
	// Device pixel (x, y, 1) to the Graphics2D's pixels.
	deviceToScreen0 : vec4<f32>,
	deviceToScreen1 : vec4<f32>,
	// The Graphics2D's pixels to clip space.
	screenToClip0 : vec4<f32>,
	screenToClip1 : vec4<f32>,
	// The rasterizer gamma: 256 mapped covers (0..1), four to a vector.
	gamma : array<vec4<f32>, 64>,
};

@group(0) @binding(0) var<uniform> settings : Settings;
@group(0) @binding(1) var<storage, read> triangles : array<Triangle>;
@group(0) @binding(2) var layer : texture_2d<f32>;

struct Varyings
{
	@builtin(position) position : vec4<f32>,
	@location(0) @interpolate(flat) index : u32,
};

struct Calc
{
	x : i32,
	color : vec4<i32>,
};

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> Varyings
{
	let index = vertexIndex / 6u;
	let b = triangles[index].bounds;
	var xs = array<f32, 6>(b.x, b.z, b.x, b.x, b.z, b.z);
	var ys = array<f32, 6>(b.y, b.y, b.w, b.w, b.y, b.w);
	let corner = vertexIndex % 6u;
	let p = vec3<f32>(xs[corner], ys[corner], 1.0);
	var out : Varyings;
	out.position = vec4<f32>(dot(settings.screenToClip0.xyz, p), dot(settings.screenToClip1.xyz, p), 0.0, 1.0);
	out.index = index;
	return out;
}

// AGG iround: halves away from zero, then truncated.
fn iround(v : f32) -> i32
{
	return i32(select(v + 0.5, v - 0.5, v < 0.0));
}

// rgba_calc::calc at row y.
fn calc(e : Edge, y : f32) -> Calc
{
	let k = clamp((y - e.geometry.y) * e.geometry.w, 0.0, 1.0);
	var c : Calc;
	let d = vec4<f32>(e.delta) * k;
	c.color = e.start + vec4<i32>(iround(d.x), iround(d.y), iround(d.z), iround(d.w));
	c.x = iround((e.geometry.x + (e.geometry.z * k)) * 16.0);
	return c;
}

// span_gouraud_rgba::generate's colour at pixel (px, py).
fn shade(t : Triangle, px : i32, py : i32) -> vec4<i32>
{
	let y = f32(py);
	var pc1 = calc(t.edges[0], y);
	var pc2 : Calc;
	if (py <= i32(t.info.x))
	{
		pc2 = calc(t.edges[1], y + t.edges[1].geometry.w);
	}
	else
	{
		pc2 = calc(t.edges[2], y - t.edges[2].geometry.w);
	}

	if (t.info.y > 0.5)
	{
		let swapped = pc1;
		pc1 = pc2;
		pc2 = swapped;
	}

	let nlen = max(abs(pc2.x - pc1.x), 1);
	let inc = ((pc2.color - pc1.color) << vec4<u32>(14u)) / vec4<i32>(nlen);

	// The dda has stepped (px << 4) - pc1.x subpixels from pc1 by this pixel; i32 products wrap as the
	// software's running sum does.
	let k = (px << 4u) - pc1.x;
	let v = pc1.color + ((inc * vec4<i32>(k)) >> vec4<u32>(14u));
	if (k >= 0 && k < nlen)
	{
		// Inside the span's run the software casts to a byte without clamping.
		return v & vec4<i32>(255);
	}

	return clamp(v, vec4<i32>(0), vec4<i32>(255));
}

fn outlinePoint(t : Triangle, i : u32) -> vec2<f32>
{
	let pair = t.outline[i / 2u];
	return select(pair.xy, pair.zw, (i % 2u) == 1u);
}

fn cross2(a : vec2<f32>, b : vec2<f32>) -> f32
{
	return (a.x * b.y) - (a.y * b.x);
}

// The area of the unit pixel square at origin inside the outline: the square clipped by each outline edge.
fn coverage(t : Triangle, origin : vec2<f32>) -> f32
{
	let n = u32(t.info.z);
	var winding = 0.0;
	for (var i = 0u; i < n; i++)
	{
		winding += cross2(outlinePoint(t, i) - origin, outlinePoint(t, (i + 1u) % n) - origin);
	}

	if (winding == 0.0)
	{
		return 0.0;
	}

	let s = sign(winding);
	var poly = array<vec2<f32>, 12>();
	poly[0] = vec2<f32>(0.0, 0.0);
	poly[1] = vec2<f32>(1.0, 0.0);
	poly[2] = vec2<f32>(1.0, 1.0);
	poly[3] = vec2<f32>(0.0, 1.0);
	var count = 4u;
	for (var i = 0u; i < n; i++)
	{
		let a = outlinePoint(t, i) - origin;
		let edge = outlinePoint(t, (i + 1u) % n) - origin - a;
		var clipped = array<vec2<f32>, 12>();
		var clippedCount = 0u;
		for (var j = 0u; j < count; j++)
		{
			let p = poly[j];
			let q = poly[(j + 1u) % count];
			let dp = s * cross2(edge, p - a);
			let dq = s * cross2(edge, q - a);
			if (dp >= 0.0)
			{
				clipped[clippedCount] = p;
				clippedCount++;
			}

			if ((dp >= 0.0) != (dq >= 0.0) && clippedCount < 12u)
			{
				clipped[clippedCount] = p + ((q - p) * (dp / (dp - dq)));
				clippedCount++;
			}
		}

		poly = clipped;
		count = clippedCount;
		if (count < 3u)
		{
			return 0.0;
		}
	}

	var area = 0.0;
	for (var j = 0u; j < count; j++)
	{
		area += cross2(poly[j], poly[(j + 1u) % count]);
	}

	return abs(area) * 0.5;
}

@fragment
fn fragmentMain(input : Varyings) -> @location(0) vec4<f32>
{
	let t = triangles[input.index];
	let p = vec3<f32>(input.position.xy, 1.0);
	let screen = vec2<f32>(dot(settings.deviceToScreen0.xyz, p), dot(settings.deviceToScreen1.xyz, p));
	let pixel = floor(screen);

	// The rasterizer's cover: area in 1/256ths, a full pixel capped at 255.
	let cover = min(u32(coverage(t, pixel) * 256.0), 255u);
	if (cover == 0u)
	{
		discard;
	}

	let mapped = settings.gamma[cover / 4u][cover % 4u];
	let color = vec4<f32>(shade(t, i32(pixel.x), i32(pixel.y))) / 255.0;
	let alpha = color.a * mapped;
	return vec4<f32>(color.rgb * alpha, alpha);
}

@vertex
fn compositeVertex(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it to the triangles' bounds.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

// A compound fill's summed layer, already premultiplied, drawn source-over.
@fragment
fn compositeFragment(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	return textureLoad(layer, vec2<i32>(position.xy), 0);
}
