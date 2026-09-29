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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The filter property: the element is drawn onto its own layer, which each referenced &lt;filter&gt; turns into
	/// a new image, primitive by primitive, at device resolution, before it is clipped, masked and composited.
	/// Supported primitives: feGaussianBlur, feOffset, feFlood, feComposite, feMerge, feColorMatrix, feBlend,
	/// feDropShadow, and (through <see cref="SvgFilterRun"/>) feComponentTransfer, feMorphology, feTile,
	/// feConvolveMatrix, feDisplacementMap, feTurbulence, feDiffuseLighting, feSpecularLighting and feImage; any
	/// other child is skipped. As in resvg: a reference to something
	/// that is not a filter with primitives, or objectBoundingBox units on an element with no box, draws nothing
	/// (unless another filter in the list is valid). Filter functions such as blur() run as the one-primitive filters
	/// <see cref="SvgFilterFunctions"/> makes of them.
	/// </summary>
	internal static class SvgFilter
	{
		private static readonly HashSet<string> Supported = new HashSet<string>
		{
			"feGaussianBlur", "feOffset", "feFlood", "feComposite", "feMerge", "feColorMatrix", "feBlend", "feDropShadow",
			"feComponentTransfer", "feMorphology", "feTile", "feConvolveMatrix", "feDisplacementMap", "feTurbulence", "feDiffuseLighting", "feSpecularLighting", "feImage",
		};

		public static bool Applies(SvgElement element)
		{
			string value = element["filter"];
			return !string.IsNullOrWhiteSpace(value) && value.Trim() != "none";
		}

		/// <summary>
		/// Runs <paramref name="draw"/> onto a layer, filters it as <paramref name="element"/> says and composites
		/// the result over <paramref name="target"/>. <paramref name="transform"/> is the element's user space to pixels.
		/// </summary>
		public static void Draw(SvgRenderer.Context context, SvgElement element, Affine transform, ImageBuffer target, Action<ImageBuffer> draw)
		{
			RectangleDouble? bounds = SvgClipMask.ObjectBounds(context, element, 0);
			var filters = new List<(SvgElement Filter, SvgElement Primitives, RectangleDouble Region)>();
			bool invalid = false;
			foreach (object entry in SvgFilterFunctions.Parse(element, element["filter"]) ?? new List<object>())
			{
				SvgElement filter = entry as SvgElement ?? context.Document.GetElementById((string)entry);
				SvgElement primitives = filter?.Name == "filter" ? WithPrimitives(context, filter) : null;
				if (primitives != null && Region(context, filter, bounds) is RectangleDouble region)
				{
					filters.Add((filter, primitives, region));
				}
				else
				{
					invalid = true;
				}
			}

			if (filters.Count == 0)
			{
				if (!invalid)
				{
					draw(target);
				}

				return;
			}

			ImageBuffer layer = new ImageBuffer(target.Width, target.Height, 32, new BlenderPreMultBGRA());
			draw(layer);
			byte[] pixels = layer.GetBuffer();
			var source = new SvgFilterImage((byte[])pixels.Clone(), false);
			foreach ((SvgElement filter, SvgElement primitives, RectangleDouble region) in filters)
			{
				source = Apply(context, filter, primitives, region, bounds, transform, source, target.Width, target.Height);
				if (source == null)
				{
					return;
				}
			}

			Array.Copy(SvgFilterPrimitives.InColorSpace(source, false).Pixels, pixels, pixels.Length);
			layer.MarkImageChanged();
			SvgRenderer.CompositeLayer(target, layer, 1);
		}

		/// <summary>An attribute of a filter, else of the filters it references through href.</summary>
		private static string Attribute(SvgRenderer.Context context, SvgElement filter, string name)
		{
			for (int depth = 0; filter?.Name == "filter" && depth < 16; depth++)
			{
				if (filter[name] is string value)
				{
					return value;
				}

				filter = context.Document.GetElementById(filter["href"]);
			}

			return null;
		}

		/// <summary>The first filter along the href chain that has children: whose primitives are used.</summary>
		private static SvgElement WithPrimitives(SvgRenderer.Context context, SvgElement filter)
		{
			for (int depth = 0; filter?.Name == "filter" && depth < 16; depth++)
			{
				if (filter.Children.Count > 0)
				{
					return filter;
				}

				filter = context.Document.GetElementById(filter["href"]);
			}

			return null;
		}

		/// <summary>The filter region in user space; null when it is empty or needs a box the element does not have.</summary>
		private static RectangleDouble? Region(SvgRenderer.Context context, SvgElement filter, RectangleDouble? bounds)
		{
			bool boxUnits = Attribute(context, filter, "filterUnits") != "userSpaceOnUse";
			if (boxUnits && (!(bounds is RectangleDouble b) || b.Width <= 0 || b.Height <= 0))
			{
				return null;
			}

			double Length(string name, double fallback, double viewport) => boxUnits
				? SvgLength.Parse(Attribute(context, filter, name), fallback, 1)
				: SvgLength.Parse(Attribute(context, filter, name), fallback * viewport, viewport);

			double x = Length("x", -.1, context.ViewportWidth);
			double y = Length("y", -.1, context.ViewportHeight);
			double width = Length("width", 1.2, context.ViewportWidth);
			double height = Length("height", 1.2, context.ViewportHeight);
			if (width <= 0 || height <= 0)
			{
				return null;
			}

			if (boxUnits)
			{
				RectangleDouble box = bounds.Value;
				(x, y, width, height) = (box.Left + x * box.Width, box.Bottom + y * box.Height, width * box.Width, height * box.Height);
			}

			return new RectangleDouble(x, y, x + width, y + height);
		}

		/// <summary>The pixels <paramref name="rect"/> (user space) covers under <paramref name="transform"/>, within the canvas.</summary>
		private static SvgPixelRect ToPixels(RectangleDouble rect, Affine transform, int width, int height)
		{
			double left = double.MaxValue, bottom = double.MaxValue, right = double.MinValue, top = double.MinValue;
			foreach ((double x, double y) in new[] { (rect.Left, rect.Bottom), (rect.Right, rect.Bottom), (rect.Right, rect.Top), (rect.Left, rect.Top) })
			{
				double px = x, py = y;
				transform.Transform(ref px, ref py);
				left = Math.Min(left, px);
				right = Math.Max(right, px);
				bottom = Math.Min(bottom, py);
				top = Math.Max(top, py);
			}

			// The epsilon keeps an edge that lands on a pixel boundary, up to rounding, from taking in the next pixel.
			return new SvgPixelRect((int)Math.Floor(left + 1e-6), (int)Math.Floor(bottom + 1e-6), (int)Math.Ceiling(right - 1e-6), (int)Math.Ceiling(top - 1e-6))
				.Intersect(new SvgPixelRect(0, 0, width, height));
		}

		/// <summary>
		/// Runs one filter's primitives on <paramref name="source"/> (sRGB) and returns the last one's result, or
		/// transparent black when there is none.
		/// </summary>
		private static SvgFilterImage Apply(SvgRenderer.Context context, SvgElement filter, SvgElement owner, RectangleDouble region, RectangleDouble? bounds, Affine transform, SvgFilterImage source, int width, int height)
		{
			bool boxPrimitives = Attribute(context, filter, "primitiveUnits") == "objectBoundingBox";
			if (boxPrimitives && (!(bounds is RectangleDouble b) || b.Width <= 0 || b.Height <= 0))
			{
				return null;
			}

			RectangleDouble box = bounds ?? default;
			SvgPixelRect regionPixels = ToPixels(region, transform, width, height);
			var sourceGraphic = new SvgFilterImage(SvgFilterPrimitives.Crop(source.Pixels, width, regionPixels), false) { Region = regionPixels };
			SvgFilterImage sourceAlpha = null;
			var results = new Dictionary<string, SvgFilterImage>();
			SvgFilterImage previous = null;

			// A primitive's number is user units, or with objectBoundingBox primitiveUnits box fractions; scale* is
			// that in pixels along x or y.
			double unitX = boxPrimitives ? box.Width : 1;
			double unitY = boxPrimitives ? box.Height : 1;
			double scaleX = Math.Sqrt(transform.sx * transform.sx + transform.shy * transform.shy) * unitX;
			double scaleY = Math.Sqrt(transform.shx * transform.shx + transform.sy * transform.sy) * unitY;

			foreach (SvgElement primitive in owner.Children)
			{
				if (!Supported.Contains(primitive.Name))
				{
					continue;
				}

				if (!(Subregion(context, primitive, boxPrimitives, box, region) is RectangleDouble subregion))
				{
					break;
				}

				SvgPixelRect pixels = ToPixels(subregion, transform, width, height).Intersect(regionPixels);
				bool linear = Linear(primitive);

				// in / in2: a named earlier result, SourceGraphic or SourceAlpha; missing or unknown, the previous result.
				SvgFilterImage InputImage(SvgElement node, string attribute)
				{
					string name = node[attribute];
					return name == "SourceAlpha"
						? sourceAlpha ??= new SvgFilterImage(SvgFilterPrimitives.AlphaOnly(sourceGraphic.Pixels), false) { Region = regionPixels }
						: name != null && results.TryGetValue(name, out SvgFilterImage named) ? named
						: name == "SourceGraphic" || name == "BackgroundImage" || name == "BackgroundAlpha" || name == "FillPaint" || name == "StrokePaint" ? sourceGraphic
						: previous ?? sourceGraphic;
				}

				byte[] Input(SvgElement node, string attribute) => SvgFilterPrimitives.InColorSpace(InputImage(node, attribute), linear).Pixels;

				var run = new SvgFilterRun(context, primitive, pixels, linear, width, height, (unitX, unitY, scaleX, scaleY), transform, Input, (node, attribute) => InputImage(node, attribute).Region, subregion,
					boxPrimitives ? box : (RectangleDouble?)null);
				SvgFilterImage result = Run(run);
				result.Region = pixels;
				previous = result;
				if (primitive["result"] is string resultName && resultName.Length > 0)
				{
					results[resultName] = result;
				}
			}

			return previous ?? new SvgFilterImage(new byte[width * height * 4], false);
		}

		private static SvgFilterImage Run(SvgFilterRun run)
		{
			(SvgElement primitive, SvgPixelRect pixels, bool linear, int width, int height, Affine transform, Func<SvgElement, string, byte[]> input) =
				(run.Primitive, run.Pixels, run.Linear, run.Width, run.Height, run.Transform, run.Input);
			(double unitX, double unitY, double scaleX, double scaleY) = run.Units;
			switch (primitive.Name)
			{
				case "feGaussianBlur":
					(double sx, double sy) = StdDeviation(primitive, "0");
					return new SvgFilterImage(SvgFilterPrimitives.Blur(input(primitive, "in"), width, pixels, sx * scaleX, sy * scaleY), linear);
				case "feOffset":
					// Offsets move whole pixels, rounded toward zero as resvg does; the vector goes through the
					// transform's linear part so SVG's y-down becomes agg's y-up.
					(int dx, int dy) = DeviceOffset(primitive, 0, unitX, unitY, transform);
					return new SvgFilterImage(SvgFilterPrimitives.Offset(input(primitive, "in"), width, height, pixels, dx, dy), linear);
				case "feFlood":
					// The flood colour is sRGB; tagging the result sRGB converts it wherever it is used.
					return new SvgFilterImage(SvgFilterPrimitives.Flood(width, height, pixels, FloodColor(primitive)), false);
				case "feComposite":
					double K(string name) => SvgLength.ParseNumber(primitive[name], 0);
					return new SvgFilterImage(SvgFilterPrimitives.Composite(input(primitive, "in"), input(primitive, "in2"), width, pixels, primitive["operator"] ?? "over", K("k1"), K("k2"), K("k3"), K("k4")), linear);
				case "feMerge":
					var layers = primitive.Children.Where(c => c.Name == "feMergeNode").Select(c => input(c, "in")).ToList();
					return new SvgFilterImage(SvgFilterPrimitives.Merge(layers, width, height, pixels), linear);
				case "feColorMatrix":
					return new SvgFilterImage(SvgFilterPrimitives.ColorMatrix(input(primitive, "in"), width, pixels, ColorMatrix(primitive)), linear);
				case "feBlend":
					return new SvgFilterImage(SvgFilterPrimitives.Blend(input(primitive, "in"), input(primitive, "in2"), width, pixels, primitive["mode"] ?? "normal"), linear);
				case "feDropShadow":
					// The input's alpha, blurred, moved and coloured with the flood colour, under the input.
					byte[] graphic = input(primitive, "in");
					(double shadowX, double shadowY) = StdDeviation(primitive, "2");
					(int shadowDx, int shadowDy) = DeviceOffset(primitive, 2, unitX, unitY, transform);
					byte[] shadow = SvgFilterPrimitives.Blur(SvgFilterPrimitives.AlphaOnly(graphic), width, pixels, shadowX * scaleX, shadowY * scaleY);
					shadow = SvgFilterPrimitives.Offset(shadow, width, height, pixels, shadowDx, shadowDy);
					byte[] flood = SvgFilterPrimitives.InColorSpace(new SvgFilterImage(SvgFilterPrimitives.Flood(width, height, pixels, FloodColor(primitive)), false), linear).Pixels;
					shadow = SvgFilterPrimitives.Composite(flood, shadow, width, pixels, "in", 0, 0, 0, 0);
					return new SvgFilterImage(SvgFilterPrimitives.Merge(new[] { shadow, graphic }, width, height, pixels), linear);
				default:
					return SvgFilterRun.RunMore(run) ?? new SvgFilterImage(new byte[width * height * 4], linear);
			}
		}

		/// <summary>stdDeviation's one or two numbers; anything else, or a negative, is 0 0.</summary>
		private static (double X, double Y) StdDeviation(SvgElement primitive, string fallback)
		{
			List<double> values = SvgLength.ParseList(primitive["stdDeviation"] ?? fallback);
			(double x, double y) = values.Count == 1 ? (values[0], values[0]) : values.Count == 2 ? (values[0], values[1]) : (0, 0);
			return (Math.Max(0, x), Math.Max(0, y));
		}

		/// <summary>dx, dy (in units of <paramref name="unitX"/>, <paramref name="unitY"/> user units) as a pixel vector.</summary>
		private static (int X, int Y) DeviceOffset(SvgElement primitive, double fallback, double unitX, double unitY, Affine transform)
		{
			double userX = PlainNumber(primitive["dx"], fallback) * unitX;
			double userY = PlainNumber(primitive["dy"], fallback) * unitY;
			double x = userX * transform.sx + userY * transform.shx;
			double y = userX * transform.shy + userY * transform.sy;
			return ((int)Math.Truncate(x + (x > 0 ? 1e-6 : -1e-6)), (int)Math.Truncate(y + (y > 0 ? 1e-6 : -1e-6)));
		}

		/// <summary>
		/// A number as usvg reads a number attribute: the whole value must be one number, so "20%" or "1px" is
		/// <paramref name="fallback"/> (SvgLength.ParseNumber would read "20%" as .2).
		/// </summary>
		internal static double PlainNumber(string text, double fallback)
		{
			string value = text?.Trim();
			return !string.IsNullOrEmpty(value) && SvgLength.NumberEnd(value, 0) == value.Length
				&& double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : fallback;
		}

		/// <summary>
		/// A number list as usvg reads one: anything that is not a number makes the whole list empty, where
		/// SvgLength.ParseList keeps the numbers before it ("1px" would be 1).
		/// </summary>
		internal static List<double> NumberList(string text)
		{
			var numbers = new List<double>();
			int index = 0;
			while (text != null)
			{
				while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
				{
					index++;
				}

				if (index >= text.Length)
				{
					break;
				}

				int end = SvgLength.NumberEnd(text, index);
				if (end == index || !double.TryParse(text.Substring(index, end - index), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
				{
					return new List<double>();
				}

				numbers.Add(number);
				index = end;
			}

			return numbers;
		}

		/// <summary>flood-color (black by default) at flood-opacity, premultiplied.</summary>
		private static Color FloodColor(SvgElement primitive)
		{
			Color color = PrimitiveColor(primitive, "flood-color", new Color(0, 0, 0, 255));
			double opacity = Math.Max(0, Math.Min(1, SvgLength.ParseNumber(primitive["flood-opacity"], 1)));
			int alpha = (int)Math.Round(color.alpha * opacity);
			int Premultiply(int channel) => (channel * alpha + 127) / 255;
			return new Color(Premultiply(color.red), Premultiply(color.green), Premultiply(color.blue), alpha);
		}

		/// <summary>
		/// A primitive's flood-color or lighting-color as usvg resolves it: neither is inherited, but "inherit" takes
		/// the parent's value, and "currentColor" is the nearest color property - black when there is none.
		/// <paramref name="fallback"/> when missing or unreadable.
		/// </summary>
		internal static Color PrimitiveColor(SvgElement primitive, string property, Color fallback)
		{
			string value = primitive[property];
			for (SvgElement e = primitive.Parent; value == "inherit"; e = e.Parent)
			{
				if (e == null)
				{
					return fallback;
				}

				value = e[property];
			}

			if (value == "currentColor")
			{
				value = null;
				for (SvgElement e = primitive; e != null && (value == null || value == "inherit" || value == "currentColor"); e = e.Parent)
				{
					value = e["color"];
				}

				return value != null && SvgColor.TryParse(value, out Color current) ? current : new Color(0, 0, 0, 255);
			}

			return value != null && SvgColor.TryParse(value, out Color parsed) ? parsed : fallback;
		}

		/// <summary>color-interpolation-filters, inherited: linearRGB unless it says sRGB.</summary>
		private static bool Linear(SvgElement primitive)
		{
			for (SvgElement e = primitive; e != null; e = e.Parent)
			{
				if (e["color-interpolation-filters"] is string value && value != "inherit")
				{
					return value != "sRGB";
				}
			}

			return true;
		}

		/// <summary>
		/// A primitive's x, y, width and height (the filter region where not given); null when empty. usvg gives
		/// feImage and feFlood under objectBoundingBox primitiveUnits the element's box instead, as 0 0 1 1.
		/// </summary>
		private static RectangleDouble? Subregion(SvgRenderer.Context context, SvgElement primitive, bool boxUnits, RectangleDouble box, RectangleDouble region)
		{
			if (boxUnits && primitive.Name is "feImage" or "feFlood")
			{
				region = box;
			}

			double Length(string name, double regionValue, double boxOrigin, double boxSize, double viewport) => primitive[name] == null
				? regionValue
				: boxUnits ? boxOrigin + SvgLength.Parse(primitive[name], 0, 1) * boxSize : SvgLength.Parse(primitive[name], regionValue, viewport);

			double x = Length("x", region.Left, box.Left, box.Width, context.ViewportWidth);
			double y = Length("y", region.Bottom, box.Bottom, box.Height, context.ViewportHeight);
			double width = Length("width", region.Width, 0, box.Width, context.ViewportWidth);
			double height = Length("height", region.Height, 0, box.Height, context.ViewportHeight);
			return width > 0 && height > 0 ? new RectangleDouble(x, y, x + width, y + height) : (RectangleDouble?)null;
		}

		/// <summary>feColorMatrix's 4x5 matrix for its type and values; identity when they do not make one.</summary>
		private static double[] ColorMatrix(SvgElement primitive)
		{
			List<double> values = SvgLength.ParseList(primitive["values"]);
			switch (primitive["type"])
			{
				case "saturate":
					double s = values.Count > 0 ? Math.Max(0, Math.Min(1, values[0])) : 1;
					return new[]
					{
						.213 + .787 * s, .715 - .715 * s, .072 - .072 * s, 0, 0,
						.213 - .213 * s, .715 + .285 * s, .072 - .072 * s, 0, 0,
						.213 - .213 * s, .715 - .715 * s, .072 + .928 * s, 0, 0,
						0, 0, 0, 1, 0,
					};
				case "hueRotate":
					double angle = (values.Count > 0 ? values[0] : 0) * Math.PI / 180;
					double cos = Math.Cos(angle), sin = Math.Sin(angle);
					return new[]
					{
						.213 + cos * .787 - sin * .213, .715 - cos * .715 - sin * .715, .072 - cos * .072 + sin * .928, 0, 0,
						.213 - cos * .213 + sin * .143, .715 + cos * .285 + sin * .140, .072 - cos * .072 - sin * .283, 0, 0,
						.213 - cos * .213 - sin * .787, .715 - cos * .715 + sin * .715, .072 + cos * .928 + sin * .072, 0, 0,
						0, 0, 0, 1, 0,
					};
				case "luminanceToAlpha":
					return new double[]
					{
						0, 0, 0, 0, 0,
						0, 0, 0, 0, 0,
						0, 0, 0, 0, 0,
						.2125, .7154, .0721, 0, 0,
					};
				default:
					return values.Count == 20 ? values.ToArray() : new double[] { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0 };
			}
		}
	}
}
