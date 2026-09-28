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

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The filter property's entries: url(...) references, and CSS filter functions (blur, drop-shadow, grayscale,
	/// sepia, saturate, hue-rotate, invert, opacity, brightness, contrast) turned into a one-primitive &lt;filter&gt;
	/// each, as usvg does - sRGB, in a box-relative region of -50% 200% for blur and drop-shadow, -10% 120% otherwise.
	/// </summary>
	internal static class SvgFilterFunctions
	{
		/// <summary>
		/// Each entry is a url string or a synthetic filter element. Null when the list does not parse (a negative
		/// amount, say): usvg then skips the whole property, drawing the element unfiltered.
		/// </summary>
		public static List<object> Parse(SvgElement element, string value)
		{
			var entries = new List<object>();
			int index = 0;
			while (true)
			{
				while (index < value.Length && (char.IsWhiteSpace(value[index]) || value[index] == ','))
				{
					index++;
				}

				if (index >= value.Length)
				{
					return entries;
				}

				int open = value.IndexOf('(', index);
				if (open < 0)
				{
					return null;
				}

				string name = value.Substring(index, open - index).Trim().ToLowerInvariant();
				int depth = 0, close = open;
				for (; close < value.Length; close++)
				{
					depth += value[close] == '(' ? 1 : value[close] == ')' ? -1 : 0;
					if (depth == 0)
					{
						break;
					}
				}

				if (close >= value.Length)
				{
					return null;
				}

				string arguments = value.Substring(open + 1, close - open - 1).Trim();
				index = close + 1;
				if (name == "url")
				{
					entries.Add(value.Substring(open - 3, close - open + 4));
					continue;
				}

				SvgElement filter = Function(element, name, arguments);
				if (filter == null)
				{
					return null;
				}

				entries.Add(filter);
			}
		}

		private static SvgElement Function(SvgElement element, string name, string arguments)
		{
			bool blurry = name == "blur" || name == "drop-shadow";
			var filter = new SvgElement("filter", null);
			filter.Attributes["x"] = blurry ? "-.5" : "-.1";
			filter.Attributes["y"] = blurry ? "-.5" : "-.1";
			filter.Attributes["width"] = blurry ? "2" : "1.2";
			filter.Attributes["height"] = blurry ? "2" : "1.2";
			filter.Attributes["color-interpolation-filters"] = "sRGB";
			SvgElement primitive;
			switch (name)
			{
				case "blur":
					double deviation = arguments.Length == 0 ? 0 : SvgLength.Parse(arguments, -1);
					if (deviation < 0)
					{
						return null;
					}

					primitive = Primitive(filter, "feGaussianBlur", ("stdDeviation", Format(deviation)));
					break;
				case "drop-shadow":
					primitive = DropShadow(element, filter, arguments);
					break;
				case "grayscale":
				case "sepia":
				case "saturate":
					double? amount = Amount(arguments);
					if (amount == null)
					{
						return null;
					}

					primitive = Primitive(filter, "feColorMatrix", ("values", string.Join(" ", Matrix(name, amount.Value).Select(Format))));
					break;
				case "hue-rotate":
					double? degrees = Angle(arguments);
					if (degrees == null)
					{
						return null;
					}

					primitive = Primitive(filter, "feColorMatrix", ("type", "hueRotate"), ("values", Format(degrees.Value)));
					break;
				case "invert":
				case "opacity":
				case "brightness":
				case "contrast":
					double? a = Amount(arguments);
					if (a == null)
					{
						return null;
					}

					primitive = Transfer(filter, name, a.Value);
					break;
				default:
					return null;
			}

			return primitive == null ? null : filter;
		}

		/// <summary>An amount: a number or percentage, 1 when missing; null for a negative or unreadable one.</summary>
		private static double? Amount(string text)
		{
			if (text.Length == 0)
			{
				return 1;
			}

			bool percent = text.EndsWith("%", StringComparison.Ordinal);
			if (!double.TryParse(percent ? text.Substring(0, text.Length - 1) : text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
			{
				return null;
			}

			return percent ? value / 100 : value;
		}

		/// <summary>An angle in degrees (deg, rad, grad or turn; a bare number is degrees), 0 when missing.</summary>
		private static double? Angle(string text)
		{
			if (text.Length == 0)
			{
				return 0;
			}

			(string Unit, double Scale)[] units = { ("deg", 1), ("grad", .9), ("rad", 180 / Math.PI), ("turn", 360) };
			foreach ((string unit, double scale) in units)
			{
				if (text.EndsWith(unit, StringComparison.OrdinalIgnoreCase)
					&& double.TryParse(text.Substring(0, text.Length - unit.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
				{
					return v * scale;
				}
			}

			return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain) ? plain : (double?)null;
		}

		/// <summary>grayscale, sepia (amounts capped at 1) and saturate as 4x5 matrices, with usvg's coefficients.</summary>
		private static double[] Matrix(string name, double amount)
		{
			switch (name)
			{
				case "grayscale":
					double g = 1 - Math.Min(1, amount);
					return new[]
					{
						.2126 + .7874 * g, .7152 - .7152 * g, .0722 - .0722 * g, 0, 0,
						.2126 - .2126 * g, .7152 + .2848 * g, .0722 - .0722 * g, 0, 0,
						.2126 - .2126 * g, .7152 - .7152 * g, .0722 + .9278 * g, 0, 0,
						0, 0, 0, 1, 0,
					};
				case "sepia":
					double s = 1 - Math.Min(1, amount);
					return new[]
					{
						.393 + .607 * s, .769 - .769 * s, .189 - .189 * s, 0, 0,
						.349 - .349 * s, .686 + .314 * s, .168 - .168 * s, 0, 0,
						.272 - .272 * s, .534 - .534 * s, .131 + .869 * s, 0, 0,
						0, 0, 0, 1, 0,
					};
				default:
					// saturate: feColorMatrix's saturate matrix, but not capped at 1 - saturate(2) oversaturates.
					double a = amount;
					return new[]
					{
						.213 + .787 * a, .715 - .715 * a, .072 - .072 * a, 0, 0,
						.213 - .213 * a, .715 + .285 * a, .072 - .072 * a, 0, 0,
						.213 - .213 * a, .715 - .715 * a, .072 + .928 * a, 0, 0,
						0, 0, 0, 1, 0,
					};
			}
		}

		/// <summary>invert, opacity (capped at 1), brightness and contrast as feComponentTransfer functions.</summary>
		private static SvgElement Transfer(SvgElement filter, string name, double amount)
		{
			SvgElement transfer = Primitive(filter, "feComponentTransfer");
			double capped = Math.Min(1, amount);
			(string Channel, (string, string)[] Attributes)[] functions = name switch
			{
				"invert" => new[] { "R", "G", "B" }.Select(c => (c, new[] { ("type", "table"), ("tableValues", $"{Format(capped)} {Format(1 - capped)}") })).ToArray(),
				"opacity" => new[] { ("A", new[] { ("type", "table"), ("tableValues", $"0 {Format(capped)}") }) },
				"brightness" => new[] { "R", "G", "B" }.Select(c => (c, new[] { ("type", "linear"), ("slope", Format(amount)) })).ToArray(),
				_ => new[] { "R", "G", "B" }.Select(c => (c, new[] { ("type", "linear"), ("slope", Format(amount)), ("intercept", Format(.5 - .5 * amount)) })).ToArray(),
			};
			foreach ((string channel, (string, string)[] attributes) in functions)
			{
				Primitive(transfer, "feFunc" + channel, attributes);
			}

			return transfer;
		}

		/// <summary>drop-shadow(color? dx dy blur?): the colour (the element's color, else black, when missing) may lead or trail.</summary>
		private static SvgElement DropShadow(SvgElement element, SvgElement filter, string arguments)
		{
			var lengths = new List<double>();
			string color = null;
			foreach (string token in Tokens(arguments))
			{
				char first = token[0];
				if (char.IsDigit(first) || first == '-' || first == '+' || first == '.')
				{
					lengths.Add(SvgLength.Parse(token, double.NaN));
				}
				else
				{
					color = token;
				}
			}

			if (lengths.Count < 2 || lengths.Count > 3 || lengths.Any(double.IsNaN) || (lengths.Count == 3 && lengths[2] < 0))
			{
				return null;
			}

			for (SvgElement e = element; color == null && e != null; e = e.Parent)
			{
				color = e["color"];
			}

			Color parsed = SvgColor.TryParse(color, out Color c) ? c : new Color(0, 0, 0, 255);
			return Primitive(filter, "feDropShadow", ("dx", Format(lengths[0])), ("dy", Format(lengths[1])),
				("stdDeviation", Format(lengths.Count == 3 ? lengths[2] : 0)),
				("flood-color", $"rgb({parsed.red},{parsed.green},{parsed.blue})"), ("flood-opacity", Format(parsed.alpha / 255.0)));
		}

		/// <summary>Whitespace-separated tokens, keeping rgb(...) and the like whole.</summary>
		private static IEnumerable<string> Tokens(string text)
		{
			int depth = 0, start = 0;
			for (int i = 0; i <= text.Length; i++)
			{
				char c = i < text.Length ? text[i] : ' ';
				depth += c == '(' ? 1 : c == ')' ? -1 : 0;
				if (depth == 0 && char.IsWhiteSpace(c))
				{
					if (i > start)
					{
						yield return text.Substring(start, i - start);
					}

					start = i + 1;
				}
			}
		}

		private static SvgElement Primitive(SvgElement parent, string name, params (string Name, string Value)[] attributes)
		{
			var primitive = new SvgElement(name, parent);
			foreach ((string key, string value) in attributes)
			{
				primitive.Attributes[key] = value;
			}

			parent.AddChild(primitive);
			return primitive;
		}

		private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
	}
}
