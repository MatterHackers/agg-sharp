// Copyright (c) 2026, Lars Brubaker. All rights reserved. See the license in the repository root.
//
// GpuCompOp's composite: a layer of premultiplied colour (what the draws put there, source-over, coverage
// already multiplied in) is composited onto a copy of the destination through one SVG compositing operator,
// with agg-sharp's BlenderCompOpBGRA formulas (C++ comp_op_rgba_*, with its SrcAtop blue and ColorBurn fixes).
// The pipeline does not blend: the result replaces the destination pixel.
//
// Cover as BlenderCompOpBGRA takes it: most operators run on the cover-scaled source, which the layer already is.
// Clear, src, src-in, dst-in, src-out and dst-atop instead lerp the destination to the full-cover result by the cover,
// so for those the coverage texture carries the draws' coverage alone (drawn white) in its alpha.
//
// The same pass also composites a linear-light retained layer (rgba16float, linear premultiplied) onto its
// destination: source-over, with the layer scaled by an opacity and read at an offset from the destination
// texel. Onto an sRGB byte destination it decodes the destination to linear light, mixes, and encodes the result
// again - C++ AGG's rgba32 window shown through srgba8 - or, where the destination cannot be read, encodes the
// layer alone for the pipeline to blend (mode 2).

struct CompOpSettings
{
	// x = the CompOp value; y = 1 when the coverage texture is bound; z = the linear layer mode (0 none, 1 linear
	// source-over onto sRGB bytes, 2 the linear layer encoded alone, 3 linear source-over onto linear); w = opacity.
	op : vec4<f32>,
	// xy = the destination texel of the layer's texel (0, 0); zw unused.
	offset : vec4<f32>,
};

@group(0) @binding(0) var layer : texture_2d<f32>;
@group(0) @binding(1) var coverage : texture_2d<f32>;
@group(0) @binding(2) var destination : texture_2d<f32>;
@group(0) @binding(3) var<uniform> settings : CompOpSettings;

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4<f32>
{
	// One triangle over the whole target; the scissor limits it.
	var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
	return vec4<f32>(corners[vertexIndex], 0.0, 1.0);
}

// clip(rgba&): alpha into 0..1, each colour channel into 0..alpha.
fn clipColor(c : vec4<f32>) -> vec4<f32>
{
	let a = clamp(c.a, 0.0, 1.0);
	return vec4<f32>(clamp(c.rgb, vec3<f32>(0.0), vec3<f32>(a)), a);
}

// Overlay (by the destination) and hard light (by the source).
fn lightCalc(overlay : bool, dca : f32, sca : f32, da : f32, sa : f32) -> f32
{
	let sada = sa * da;
	var multiply = 2.0 * sca < sa;
	if (overlay)
	{
		multiply = 2.0 * dca <= da;
	}

	if (multiply)
	{
		return (2.0 * sca * dca) + (sca * (1.0 - da)) + (dca * (1.0 - sa));
	}

	return sada - (2.0 * (da - dca) * (sa - sca)) + (sca * (1.0 - da)) + (dca * (1.0 - sa));
}

fn colorDodgeCalc(dca : f32, sca : f32, da : f32, sa : f32) -> f32
{
	let sada = sa * da;
	if (sca < sa)
	{
		return (sada * min(1.0, dca / da * sa / (sa - sca))) + (sca * (1.0 - da)) + (dca * (1.0 - sa));
	}

	if (dca > 0.0)
	{
		return sada + (sca * (1.0 - da)) + (dca * (1.0 - sa));
	}

	return sca * (1.0 - da);
}

fn colorBurnCalc(dca : f32, sca : f32, da : f32, sa : f32) -> f32
{
	let sada = sa * da;
	if (sca > 0.0)
	{
		return (sada * (1.0 - min(1.0, (1.0 - (dca / da)) * sa / sca))) + (sca * (1.0 - da)) + (dca * (1.0 - sa));
	}

	// C++ tested dca > da, which premultiplied colours never reach; agg-sharp keeps a full channel.
	if (dca >= da)
	{
		return sada + (dca * (1.0 - sa));
	}

	return dca * (1.0 - sa);
}

// C++'s soft light, formula for formula (it is not the SVG spec's).
fn softLightCalc(dca : f32, sca : f32, da : f32, sa : f32) -> f32
{
	let sada = sa * da;
	let dcasa = dca * sa;
	let rest = (sca * (1.0 - da)) + (dca * (1.0 - sa));
	if (2.0 * sca <= sa)
	{
		return dcasa - ((sada - (2.0 * sca * da)) * dcasa * (sada - dcasa)) + rest;
	}

	if (4.0 * dca <= da)
	{
		return dcasa + (((2.0 * sca * da) - sada) * (((((16.0 * dcasa) - 12.0) * dcasa + 4.0) * dca * da) - (dca * da))) + rest;
	}

	return dcasa + (((2.0 * sca * da) - sada) * (sqrt(dcasa) - dcasa)) + rest;
}

fn channelCalc(op : i32, dca : f32, sca : f32, da : f32, sa : f32) -> f32
{
	switch (op)
	{
		case 15: { return lightCalc(true, dca, sca, da, sa); }
		case 20: { return lightCalc(false, dca, sca, da, sa); }
		case 18: { return colorDodgeCalc(dca, sca, da, sa); }
		case 19: { return colorBurnCalc(dca, sca, da, sa); }
		default: { return softLightCalc(dca, sca, da, sa); }
	}
}

// sRGB's curves, as SrgbLut computes them; the encode rounds to the nearest level on the way into bytes, which is
// where C++'s float inverse table puts its thresholds.
fn srgbToLinear(c : vec3<f32>) -> vec3<f32>
{
	return select(pow((c + vec3<f32>(0.055)) / 1.055, vec3<f32>(2.4)), c / 12.92, c <= vec3<f32>(0.04045));
}

fn linearToSrgb(c : vec3<f32>) -> vec3<f32>
{
	return select((1.055 * pow(c, vec3<f32>(1.0 / 2.4))) - vec3<f32>(0.055), c * 12.92, c <= vec3<f32>(0.0031308));
}

// Premultiplied sRGB to premultiplied linear and back: the colour is unpremultiplied around the curve.
fn decodePremultiplied(c : vec4<f32>) -> vec4<f32>
{
	if (c.a <= 0.0)
	{
		return vec4<f32>(0.0);
	}

	return vec4<f32>(srgbToLinear(clamp(c.rgb / c.a, vec3<f32>(0.0), vec3<f32>(1.0))) * c.a, c.a);
}

fn encodePremultiplied(c : vec4<f32>) -> vec4<f32>
{
	if (c.a <= 0.0)
	{
		return vec4<f32>(0.0);
	}

	return vec4<f32>(linearToSrgb(clamp(c.rgb / c.a, vec3<f32>(0.0), vec3<f32>(1.0))) * c.a, c.a);
}

fn compositeLinearLayer(texel : vec2<i32>, mode : i32) -> vec4<f32>
{
	let layerTexel = texel - vec2<i32>(settings.offset.xy);
	let size = vec2<i32>(textureDimensions(layer));
	var s = vec4<f32>(0.0);
	if (all(layerTexel >= vec2<i32>(0)) && all(layerTexel < size))
	{
		s = clamp(textureLoad(layer, layerTexel, 0), vec4<f32>(0.0), vec4<f32>(1.0)) * settings.op.w;
	}

	if (mode == 2)
	{
		return encodePremultiplied(s);
	}

	let d = textureLoad(destination, texel, 0);
	if (mode == 3)
	{
		return s + (d * (1.0 - s.a));
	}

	return encodePremultiplied(s + (decodePremultiplied(d) * (1.0 - s.a)));
}

@fragment
fn fragmentMain(@builtin(position) position : vec4<f32>) -> @location(0) vec4<f32>
{
	let texel = vec2<i32>(position.xy);
	let linearMode = i32(settings.op.z);
	if (linearMode != 0)
	{
		return compositeLinearLayer(texel, linearMode);
	}

	let s = textureLoad(layer, texel, 0);
	let d = textureLoad(destination, texel, 0);
	var c = 0.0;
	if (settings.op.y > 0.5)
	{
		c = textureLoad(coverage, texel, 0).a;
	}

	let sa = s.a;
	let da = d.a;
	let unionAlpha = da + sa - (sa * da);
	switch (i32(settings.op.x))
	{
		// Clear
		case 0: { return d * (1.0 - c); }
		// Src
		case 1: { return s + (d * (1.0 - c)); }
		// Dst
		case 2: { return d; }
		// SrcOver
		case 3: { return s + (d * (1.0 - sa)); }
		// DstOver
		case 4: { return d + (s * (1.0 - da)); }
		// SrcIn
		case 5: { return (d * (1.0 - c)) + (s * da); }
		// DstIn: Dca.(1 - cover) + Dca.cover.Sa, and cover.Sa is the layer's alpha.
		case 6: { return (d * (1.0 - c)) + (d * sa); }
		// SrcOut
		case 7: { return (d * (1.0 - c)) + (s * (1.0 - da)); }
		// DstOut
		case 8: { return d * (1.0 - sa); }
		// SrcAtop
		case 9: { return vec4<f32>((s.rgb * da) + (d.rgb * (1.0 - sa)), da); }
		// DstAtop
		case 10:
		{
			return vec4<f32>((d.rgb * (1.0 - c)) + (d.rgb * sa) + (s.rgb * (1.0 - da)), (da * (1.0 - c)) + sa);
		}
		// Xor
		case 11: { return vec4<f32>((s.rgb * (1.0 - da)) + (d.rgb * (1.0 - sa)), sa + da - (2.0 * sa * da)); }
		default: { }
	}

	// The rest leave the destination alone where the (cover-scaled) source is transparent.
	if (sa <= 0.0)
	{
		return d;
	}

	let rest = (s.rgb * (1.0 - da)) + (d.rgb * (1.0 - sa));
	switch (i32(settings.op.x))
	{
		// Plus
		case 12:
		{
			let a = min(da + sa, 1.0);
			return clipColor(vec4<f32>(min(d.rgb + s.rgb, vec3<f32>(a)), a));
		}
		// Multiply
		case 13: { return clipColor(vec4<f32>((s.rgb * d.rgb) + rest, unionAlpha)); }
		// Screen
		case 14: { return clipColor(vec4<f32>(d.rgb + s.rgb - (s.rgb * d.rgb), unionAlpha)); }
		// Darken
		case 16: { return clipColor(vec4<f32>(min(s.rgb * da, d.rgb * sa) + rest, unionAlpha)); }
		// Lighten
		case 17: { return clipColor(vec4<f32>(max(s.rgb * da, d.rgb * sa) + rest, unionAlpha)); }
		// Difference
		case 22: { return clipColor(vec4<f32>(d.rgb + s.rgb - (2.0 * min(s.rgb * da, d.rgb * sa)), unionAlpha)); }
		// Exclusion
		case 23: { return clipColor(vec4<f32>((s.rgb * da) + (d.rgb * sa) - (2.0 * s.rgb * d.rgb) + rest, unionAlpha)); }
		// Minus
		case 24: { return clipColor(vec4<f32>(max(d.rgb - s.rgb, vec3<f32>(0.0)), unionAlpha)); }
		// ColorDodge, ColorBurn and SoftLight put the source down as it is on a transparent destination.
		case 18, 19, 21:
		{
			if (da <= 0.0)
			{
				return s;
			}
		}
		default: { }
	}

	// Overlay, hard light, colour dodge, colour burn and soft light: per channel.
	let op = i32(settings.op.x);
	return clipColor(vec4<f32>(
		channelCalc(op, d.r, s.r, da, sa),
		channelCalc(op, d.g, s.g, da, sa),
		channelCalc(op, d.b, s.b, da, sa),
		unionAlpha));
}
