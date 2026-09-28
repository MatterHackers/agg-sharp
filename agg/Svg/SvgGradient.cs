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
			string Get(string name) => chain.Select(e => e[name]).FirstOrDefault(v => v != null);

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
			Affine toPixels = SvgTransform.Parse(Get("gradientTransform")) * units * userToPixels;

			Affine gradientToUser;
			IGradient function;
			if (server.Name == "linearGradient")
			{
				double x1 = SvgLength.Parse(Get("x1"), 0, percentWidth);
				double y1 = SvgLength.Parse(Get("y1"), 0, percentHeight);
				double x2 = SvgLength.Parse(Get("x2"), percentWidth, percentWidth);
				double y2 = SvgLength.Parse(Get("y2"), 0, percentHeight);
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
				double cx = SvgLength.Parse(Get("cx"), percentWidth / 2, percentWidth);
				double cy = SvgLength.Parse(Get("cy"), percentHeight / 2, percentHeight);
				double r = SvgLength.Parse(Get("r"), percentDiagonal / 2, percentDiagonal);
				double fx = SvgLength.Parse(Get("fx"), cx, percentWidth);
				double fy = SvgLength.Parse(Get("fy"), cy, percentHeight);
				if (r <= 0)
				{
					return new SvgServerPaint(Premultiply(stops[stops.Count - 1].Color), null);
				}

				// The focus in gradient units. One on or outside the circle is pulled just inside it, as resvg
				// does (SVG 1.1 section 13.2.3 moves it onto the circle, where the cone degenerates).
				double focusX = (fx - cx) / r * GradientUnits;
				double focusY = (fy - cy) / r * GradientUnits;
				double focusDistance = Math.Sqrt(focusX * focusX + focusY * focusY);
				double maxFocus = GradientUnits * .99;
				if (focusDistance > maxFocus)
				{
					focusX *= maxFocus / focusDistance;
					focusY *= maxFocus / focusDistance;
				}

				gradientToUser = Affine.NewScaling(r / GradientUnits) * Affine.NewTranslation(cx, cy);
				function = new gradient_radial_focus(GradientUnits, focusX, focusY);
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
			return new SvgServerPaint(null, spans);
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
		/// The first gradient in the chain that has &lt;stop&gt; children supplies them all. Offsets are clamped to
		/// 0..1 and never go back (SVG 1.1 section 13.2.4); stop-opacity and the paint's opacity scale alpha.
		/// </summary>
		private static List<(double Offset, Color Color)> Stops(List<SvgElement> chain, double opacity)
		{
			var stops = new List<(double Offset, Color Color)>();
			SvgElement owner = chain.FirstOrDefault(e => e.Children.Any(c => c.Name == "stop"));
			if (owner == null)
			{
				return stops;
			}

			double previous = 0;
			foreach (SvgElement stop in owner.Children.Where(c => c.Name == "stop"))
			{
				double offset = Math.Max(previous, Math.Min(1, Math.Max(0, SvgLength.ParseNumber(stop["offset"], 0))));
				previous = offset;
				Color color = SvgColor.TryParse(stop["stop-color"] ?? "black", out Color parsed) ? parsed : Color.Black;
				double stopOpacity = Math.Min(1, Math.Max(0, SvgLength.ParseNumber(stop["stop-opacity"], 1)));
				color.alpha = (byte)Math.Round(color.alpha * stopOpacity * opacity);
				stops.Add((offset, color));
			}

			return stops;
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
