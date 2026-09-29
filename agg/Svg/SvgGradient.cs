/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// What a paint server resolves to for one shape: nothing, one solid (premultiplied) colour, or a span
	/// generator that colours each pixel.
	/// </summary>
	public readonly struct SvgServerPaint
	{
		public SvgServerPaint(Color? solid, ISpanGenerator spans)
		{
			this.Solid = solid;
			this.Spans = spans;
		}

		public Color? Solid { get; }

		public ISpanGenerator Spans { get; }

		public bool IsNone => this.Solid == null && this.Spans == null;
	}

	/// <summary>
	/// linearGradient and radialGradient, drawn through agg's span_gradient. Attributes and stops a gradient
	/// leaves out are taken from the gradient its href names (SVG 1.1 section 13.2.2), and the gradient's own
	/// space - its bounding box or user space, then gradientTransform - is folded into the span interpolator's
	/// pixel-to-gradient matrix.
	/// </summary>
	public static class SvgGradient
	{
		/// <summary>
		/// The gradient's length in span_gradient units. span_gradient works in 1/16ths of an integer unit, so the
		/// gradient vector (or radius) is scaled up to this many units to keep its colour steps sub-level.
		/// </summary>
		private const double GradientUnits = 1024;

		/// <summary>Colour table entries across the gradient vector; enough that neighbours differ by under a level.</summary>
		private const int ColorTableSize = 1024;

		private const int MaxHrefDepth = 16;

		/// <summary>Rust's f32::EPSILON, which usvg moves equal offsets by (C#'s float.Epsilon is the smallest subnormal).</summary>
		private const float Epsilon = 1.1920929e-7f;

		/// <summary>
		/// Resolves <paramref name="server"/> (a linearGradient or radialGradient) for a shape whose user-space
		/// bounding box is <paramref name="bounds"/>, drawn with <paramref name="userToPixels"/>, at
		/// <paramref name="opacity"/>. Returns null when <paramref name="server"/> is not a gradient, so the
		/// caller uses the paint's fallback.
		/// </summary>
		public static SvgServerPaint? Resolve(SvgDocument document, SvgElement server, RectangleDouble bounds, Affine userToPixels, double opacity, double viewportWidth, double viewportHeight)
		{
			if (server == null || (server.Name != "linearGradient" && server.Name != "radialGradient"))
			{
				return null;
			}

			List<SvgElement> chain = HrefChain(document, server);

			// Geometry (x1.., cx..) only comes from a gradient of this one's kind, and the search stops at the first
			// link of the other kind; units, spread and transform come from either (usvg's resolve_lg_attr).
			string Get(string name, bool geometry = false)
			{
				foreach (SvgElement e in chain)
				{
					if (geometry && e.Name != server.Name)
					{
						break;
					}

					if (e[name] != null)
					{
						return e[name];
					}
				}

				return null;
			}

			List<(double Offset, Color Color)> stops = Stops(chain, opacity);
			if (stops.Count == 0)
			{
				return new SvgServerPaint(null, null);
			}

			if (stops.Count == 1)
			{
				return new SvgServerPaint(Premultiply(stops[0].Color), null);
			}

			bool boundingBoxUnits = Get("gradientUnits") != "userSpaceOnUse";
			Affine units = Affine.NewIdentity();
			double percentWidth = viewportWidth, percentHeight = viewportHeight;
			double percentDiagonal = Math.Sqrt((viewportWidth * viewportWidth + viewportHeight * viewportHeight) / 2);
			if (boundingBoxUnits)
			{
				// A box with no area has no bounding-box space to paint in (SVG 1.1 section 13.2.2).
				if (bounds.Width <= 0 || bounds.Height <= 0)
				{
					return new SvgServerPaint(null, null);
				}

				units = new Affine(bounds.Width, 0, 0, bounds.Height, bounds.Left, bounds.Bottom);
				percentWidth = percentHeight = percentDiagonal = 1;
			}

			// agg's a * b applies a first: gradient space, then gradientTransform, then its units, then to pixels.
			Affine toPixels = SvgTransform.Resolve(Get("gradientTransform"), Get("transform-origin"), viewportWidth, viewportHeight) * units * userToPixels;

			Affine gradientToUser;
			IGradient function;
			SvgFocalCone cone = null;
			if (server.Name == "linearGradient")
			{
				double x1 = SvgLength.Parse(Get("x1", true), 0, percentWidth);
				double y1 = SvgLength.Parse(Get("y1", true), 0, percentHeight);
				double x2 = SvgLength.Parse(Get("x2", true), percentWidth, percentWidth);
				double y2 = SvgLength.Parse(Get("y2", true), 0, percentHeight);
				double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
				if (length <= 0)
				{
					// A zero-length vector paints the last stop (SVG 1.1 section 13.2.2).
					return new SvgServerPaint(Premultiply(stops[stops.Count - 1].Color), null);
				}

				gradientToUser = Affine.NewScaling(length / GradientUnits) * Affine.NewRotation(Math.Atan2(y2 - y1, x2 - x1)) * Affine.NewTranslation(x1, y1);
				function = new gradient_x();
			}
			else
			{
				double cx = SvgLength.Parse(Get("cx", true), percentWidth / 2, percentWidth);
				double cy = SvgLength.Parse(Get("cy", true), percentHeight / 2, percentHeight);
				double r = SvgLength.Parse(Get("r", true), percentDiagonal / 2, percentDiagonal);
				double fx = SvgLength.Parse(Get("fx", true), cx, percentWidth);
				double fy = SvgLength.Parse(Get("fy", true), cy, percentHeight);

				// SVG 2's focal radius. A negative one is an error (UB in the suite); it is read as none.
				double fr = Math.Max(0, SvgLength.Parse(Get("fr", true), 0, percentDiagonal));
				if (r <= 0)
				{
					return new SvgServerPaint(Premultiply(stops[stops.Count - 1].Color), null);
				}

				// The focus in gradient units. One on or near the circle, or outside it, is drawn as the two-point cone
				// resvg draws rather than pulled inside (SVG 1.1 section 13.2.3's correction is not what resvg does),
				// since agg's gradient_radial_focus degenerates there.
				double focusX = (fx - cx) / r * GradientUnits;
				double focusY = (fy - cy) / r * GradientUnits;
				gradientToUser = Affine.NewScaling(r / GradientUnits) * Affine.NewTranslation(cx, cy);
				if (fr > 0 || Math.Sqrt(focusX * focusX + focusY * focusY) > GradientUnits * .99)
				{
					cone = new SvgFocalCone(GradientUnits, focusX, focusY, fr / r * GradientUnits);
					function = cone;
				}
				else
				{
					function = new gradient_radial_focus(GradientUnits, focusX, focusY);
				}
			}

			switch (Get("spreadMethod"))
			{
				case "reflect":
					function = new gradient_reflect_adaptor(function);
					break;
				case "repeat":
					function = new gradient_repeat_adaptor(function);
					break;
			}

			Affine pixelsToGradient = gradientToUser * toPixels;
			if (Math.Abs(pixelsToGradient.sx * pixelsToGradient.sy - pixelsToGradient.shx * pixelsToGradient.shy) < 1e-12)
			{
				return new SvgServerPaint(null, null);
			}

			pixelsToGradient.invert();
			var spans = new span_gradient(new span_interpolator_linear(pixelsToGradient), function, new StopColors(stops), 0, GradientUnits);
			return new SvgServerPaint(null, cone == null ? spans : new SvgFocalCone.Spans(spans, cone, new span_interpolator_linear(pixelsToGradient)));
		}

		/// <summary><paramref name="server"/>, then each gradient its href leads to, stopping at a loop or a non-gradient.</summary>
		private static List<SvgElement> HrefChain(SvgDocument document, SvgElement server)
		{
			var chain = new List<SvgElement>();
			for (SvgElement e = server; e != null && !chain.Contains(e) && chain.Count < MaxHrefDepth; e = document.GetElementById(e["href"]))
			{
				if (e.Name != "linearGradient" && e.Name != "radialGradient")
				{
					break;
				}

				chain.Add(e);
			}

			return chain;
		}

		/// <summary>
		/// The first gradient in the chain that has &lt;stop&gt; children supplies them all, read as usvg reads them
		/// (convert_stops): a missing or unreadable offset repeats the previous one, offsets clamp to 0..1, a run of
		/// three or more equal offsets keeps only its ends, a second offset of 0 moves up an epsilon (so the padded
		/// area before the vector keeps the first stop's colour), and an offset that does not advance moves the one
		/// before it back an epsilon so the step stays hard. Alpha is the colour's alpha times stop-opacity times the
		/// paint's opacity.
		/// </summary>
		private static List<(double Offset, Color Color)> Stops(List<SvgElement> chain, double opacity)
		{
			var stops = new List<(float Offset, Color Color)>();
			SvgElement owner = chain.FirstOrDefault(e => e.Children.Any(c => c.Name == "stop"));
			if (owner == null)
			{
				return new List<(double Offset, Color Color)>();
			}

			double previous = 0;
			foreach (SvgElement stop in owner.Children.Where(c => c.Name == "stop"))
			{
				// previous stays unclamped, as usvg's prev_offset does.
				double offset = ParseOffset(stop["offset"], previous);
				previous = offset;
				Color color = StopColor(stop);
				double stopOpacity = Math.Min(1, Math.Max(0, SvgLength.ParseNumber(stop["stop-opacity"], 1)));
				color.alpha = (byte)Math.Round(color.alpha * stopOpacity * opacity);
				stops.Add(((float)Math.Min(1, Math.Max(0, offset)), color));
			}

			for (int i = 0; stops.Count >= 3 && i < stops.Count - 2;)
			{
				if (NearlyEqual(stops[i].Offset, stops[i + 1].Offset) && NearlyEqual(stops[i + 1].Offset, stops[i + 2].Offset))
				{
					stops.RemoveAt(i + 1);
				}
				else
				{
					i++;
				}
			}

			for (int i = 0; i < stops.Count - 1; i++)
			{
				if (NearlyEqual(stops[i].Offset, 0) && NearlyEqual(stops[i + 1].Offset, 0))
				{
					stops[i + 1] = (Math.Min(1, stops[i].Offset + Epsilon), stops[i + 1].Color);
				}
			}

			for (int i = 1; i < stops.Count; i++)
			{
				float before = stops[i - 1].Offset;
				if (before > stops[i].Offset || NearlyEqual(before, stops[i].Offset))
				{
					stops[i - 1] = (Math.Max(0, before - Epsilon), stops[i - 1].Color);
					stops[i] = (before, stops[i].Color);
				}
			}

			return stops.Select(s => ((double)s.Offset, s.Color)).ToList();
		}

		/// <summary>Equal within 4 units in the last place, as usvg compares stop offsets (float_cmp's approx_eq_ulps).</summary>
		private static bool NearlyEqual(float a, float b)
		{
			if (a == b)
			{
				return true;
			}

			int ia = BitConverter.SingleToInt32Bits(a), ib = BitConverter.SingleToInt32Bits(b);
			return (ia < 0) == (ib < 0) && Math.Abs((long)ia - ib) <= 4;
		}

		/// <summary>A number or a percentage; anything else (or nothing) repeats <paramref name="previous"/>.</summary>
		private static double ParseOffset(string text, double previous)
		{
			string value = text?.Trim();
			if (string.IsNullOrEmpty(value))
			{
				return previous;
			}

			bool percent = value.EndsWith("%");
			return double.TryParse(percent ? value.Substring(0, value.Length - 1) : value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
				? (percent ? number / 100 : number)
				: previous;
		}

		/// <summary>
		/// stop-color, where "inherit" takes the parent's stop-color (it is not an inherited property) and
		/// "currentColor" the nearest color property; black when missing or unreadable.
		/// </summary>
		private static Color StopColor(SvgElement stop)
		{
			string value = stop["stop-color"];
			if (value == "inherit")
			{
				value = stop.Parent?["stop-color"];
			}

			if (value == "currentColor")
			{
				value = null;
				for (SvgElement e = stop; e != null && (value == null || value == "inherit"); e = e.Parent)
				{
					value = e["color"];
				}
			}

			return value != null && SvgColor.TryParse(value, out Color parsed) ? parsed : Color.Black;
		}

		private static Color Premultiply(Color c)
		{
			int Scale(int channel) => (channel * c.alpha + 127) / 255;
			return new Color(Scale(c.red), Scale(c.green), Scale(c.blue), c.alpha);
		}

		/// <summary>
		/// The stops as a premultiplied colour table: colours are interpolated straight, as SVG specifies, and
		/// premultiplied after, since the renderer draws premultiplied.
		/// </summary>
		private sealed class StopColors : IColorFunction
		{
			private readonly Color[] table = new Color[ColorTableSize];

			public StopColors(List<(double Offset, Color Color)> stops)
			{
				for (int i = 0; i < ColorTableSize; i++)
				{
					double t = i / (double)(ColorTableSize - 1);
					int next = stops.FindIndex(s => s.Offset > t);
					Color color;
					if (next == -1)
					{
						color = stops[stops.Count - 1].Color;
					}
					else if (next == 0)
					{
						color = stops[0].Color;
					}
					else
					{
						(double Offset, Color Color) a = stops[next - 1], b = stops[next];
						double f = (t - a.Offset) / (b.Offset - a.Offset);
						int Lerp(int from, int to) => (int)Math.Round(from + (to - from) * f);
						color = new Color(Lerp(a.Color.red, b.Color.red), Lerp(a.Color.green, b.Color.green), Lerp(a.Color.blue, b.Color.blue), Lerp(a.Color.alpha, b.Color.alpha));
					}

					this.table[i] = Premultiply(color);
				}
			}

			public Color this[int v] => this.table[v];

			public int size() => ColorTableSize;
		}
	}
}
