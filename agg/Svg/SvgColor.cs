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

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// SVG/CSS colour values: the 148 named colours, #rgb / #rgba / #rrggbb / #rrggbbaa, rgb() / rgba() with
	/// numbers or percentages, and hsl() / hsla(). currentColor is resolved by the caller, which knows the
	/// inherited <c>color</c>.
	/// </summary>
	public static class SvgColor
	{
		/// <summary>Parses <paramref name="text"/> as a colour; false when it is not one (an unknown name, a bad hex, ...).</summary>
		public static bool TryParse(string text, out Color color)
		{
			color = default;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			string value = text.Trim();
			if (value.StartsWith("#"))
			{
				return TryParseHex(value.Substring(1), out color);
			}

			int open = value.IndexOf('(');
			if (open > 0 && value.EndsWith(")"))
			{
				string function = value.Substring(0, open).Trim().ToLowerInvariant();
				string[] args = value.Substring(open + 1, value.Length - open - 2)
					.Split(new[] { ',', ' ', '/', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				if ((function == "rgb" || function == "rgba") && (args.Length == 3 || args.Length == 4))
				{
					// As svgtypes: the first channel decides. After a percentage the others may be either (a bare
					// number is then a fraction); after a number a percentage makes the colour invalid.
					bool percent = args[0].EndsWith("%");
					if (!percent && (args[1].EndsWith("%") || args[2].EndsWith("%")))
					{
						return false;
					}

					color = new Color(Channel(args[0], percent), Channel(args[1], percent), Channel(args[2], percent), args.Length == 4 ? Alpha(args[3]) : 255);
					return true;
				}

				if ((function == "hsl" || function == "hsla") && (args.Length == 3 || args.Length == 4))
				{
					color = FromHsl(Number(args[0]), Percent(args[1]), Percent(args[2]), args.Length == 4 ? Alpha(args[3]) : 255);
					return true;
				}

				return false;
			}

			// CSS's transparent, which svgtypes names alongside the 147 SVG colours.
			if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
			{
				color = new Color(0, 0, 0, 0);
				return true;
			}

			if (NamedColors.TryGetValue(value.ToLowerInvariant(), out int rgb))
			{
				color = new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff, 255);
				return true;
			}

			return false;
		}

		private static bool TryParseHex(string hex, out Color color)
		{
			color = default;
			if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int _))
			{
				return false;
			}

			int Digit(int i) => Convert.ToInt32(hex.Substring(i, 1), 16) * 17;
			int Pair(int i) => Convert.ToInt32(hex.Substring(i, 2), 16);
			switch (hex.Length)
			{
				case 3:
					color = new Color(Digit(0), Digit(1), Digit(2), 255);
					return true;
				case 4:
					color = new Color(Digit(0), Digit(1), Digit(2), Digit(3));
					return true;
				case 6:
					color = new Color(Pair(0), Pair(2), Pair(4), 255);
					return true;
				case 8:
					color = new Color(Pair(0), Pair(2), Pair(4), Pair(6));
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// An rgb() channel: 0..255, or in a percentage colour a percentage (or a fraction) of 255; clamped. A
		/// percentage is scaled by 255.0 / 100 first, as resvg's reference images were rendered: in doubles 2.55 * 50
		/// is just under 127.5, so 50% is 127, where svgtypes 0.15's (50 / 100) * 255 rounds to 128.
		/// </summary>
		private static int Channel(string arg, bool percentColour)
		{
			double value = arg.EndsWith("%") ? 255.0 / 100 * Number(arg.TrimEnd('%')) : percentColour ? Number(arg) * 255 : Number(arg);
			return (int)Math.Round(Math.Max(0, Math.Min(255, value)), MidpointRounding.AwayFromZero);
		}

		/// <summary>An alpha: 0..1, or a percentage; as 0..255.</summary>
		private static int Alpha(string arg)
		{
			double value = arg.EndsWith("%") ? Number(arg.TrimEnd('%')) / 100 : Number(arg);
			return (int)Math.Round(Math.Max(0, Math.Min(1, value)) * 255);
		}

		private static double Percent(string arg) => Math.Max(0, Math.Min(1, Number(arg.TrimEnd('%')) / 100));

		private static double Number(string arg)
		{
			double.TryParse(arg.Trim().TrimEnd("deg".ToCharArray()), NumberStyles.Float, CultureInfo.InvariantCulture, out double value);
			return value;
		}

		/// <summary>CSS hsl(): hue in degrees, saturation and lightness 0..1.</summary>
		private static Color FromHsl(double hue, double saturation, double lightness, int alpha)
		{
			hue = ((hue % 360) + 360) % 360 / 360;
			double q = lightness < .5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
			double p = 2 * lightness - q;
			double HueToRgb(double t)
			{
				t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
				if (t < 1.0 / 6)
				{
					return p + (q - p) * 6 * t;
				}

				if (t < .5)
				{
					return q;
				}

				if (t < 2.0 / 3)
				{
					return p + (q - p) * (2.0 / 3 - t) * 6;
				}

				return p;
			}

			int To255(double v) => (int)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
			return new Color(To255(HueToRgb(hue + 1.0 / 3)), To255(HueToRgb(hue)), To255(HueToRgb(hue - 1.0 / 3)), alpha);
		}

		private static readonly Dictionary<string, int> NamedColors = new Dictionary<string, int>
		{
			["aliceblue"] = 0xf0f8ff,
			["antiquewhite"] = 0xfaebd7,
			["aqua"] = 0x00ffff,
			["aquamarine"] = 0x7fffd4,
			["azure"] = 0xf0ffff,
			["beige"] = 0xf5f5dc,
			["bisque"] = 0xffe4c4,
			["black"] = 0x000000,
			["blanchedalmond"] = 0xffebcd,
			["blue"] = 0x0000ff,
			["blueviolet"] = 0x8a2be2,
			["brown"] = 0xa52a2a,
			["burlywood"] = 0xdeb887,
			["cadetblue"] = 0x5f9ea0,
			["chartreuse"] = 0x7fff00,
			["chocolate"] = 0xd2691e,
			["coral"] = 0xff7f50,
			["cornflowerblue"] = 0x6495ed,
			["cornsilk"] = 0xfff8dc,
			["crimson"] = 0xdc143c,
			["cyan"] = 0x00ffff,
			["darkblue"] = 0x00008b,
			["darkcyan"] = 0x008b8b,
			["darkgoldenrod"] = 0xb8860b,
			["darkgray"] = 0xa9a9a9,
			["darkgreen"] = 0x006400,
			["darkgrey"] = 0xa9a9a9,
			["darkkhaki"] = 0xbdb76b,
			["darkmagenta"] = 0x8b008b,
			["darkolivegreen"] = 0x556b2f,
			["darkorange"] = 0xff8c00,
			["darkorchid"] = 0x9932cc,
			["darkred"] = 0x8b0000,
			["darksalmon"] = 0xe9967a,
			["darkseagreen"] = 0x8fbc8f,
			["darkslateblue"] = 0x483d8b,
			["darkslategray"] = 0x2f4f4f,
			["darkslategrey"] = 0x2f4f4f,
			["darkturquoise"] = 0x00ced1,
			["darkviolet"] = 0x9400d3,
			["deeppink"] = 0xff1493,
			["deepskyblue"] = 0x00bfff,
			["dimgray"] = 0x696969,
			["dimgrey"] = 0x696969,
			["dodgerblue"] = 0x1e90ff,
			["firebrick"] = 0xb22222,
			["floralwhite"] = 0xfffaf0,
			["forestgreen"] = 0x228b22,
			["fuchsia"] = 0xff00ff,
			["gainsboro"] = 0xdcdcdc,
			["ghostwhite"] = 0xf8f8ff,
			["gold"] = 0xffd700,
			["goldenrod"] = 0xdaa520,
			["gray"] = 0x808080,
			["grey"] = 0x808080,
			["green"] = 0x008000,
			["greenyellow"] = 0xadff2f,
			["honeydew"] = 0xf0fff0,
			["hotpink"] = 0xff69b4,
			["indianred"] = 0xcd5c5c,
			["indigo"] = 0x4b0082,
			["ivory"] = 0xfffff0,
			["khaki"] = 0xf0e68c,
			["lavender"] = 0xe6e6fa,
			["lavenderblush"] = 0xfff0f5,
			["lawngreen"] = 0x7cfc00,
			["lemonchiffon"] = 0xfffacd,
			["lightblue"] = 0xadd8e6,
			["lightcoral"] = 0xf08080,
			["lightcyan"] = 0xe0ffff,
			["lightgoldenrodyellow"] = 0xfafad2,
			["lightgray"] = 0xd3d3d3,
			["lightgreen"] = 0x90ee90,
			["lightgrey"] = 0xd3d3d3,
			["lightpink"] = 0xffb6c1,
			["lightsalmon"] = 0xffa07a,
			["lightseagreen"] = 0x20b2aa,
			["lightskyblue"] = 0x87cefa,
			["lightslategray"] = 0x778899,
			["lightslategrey"] = 0x778899,
			["lightsteelblue"] = 0xb0c4de,
			["lightyellow"] = 0xffffe0,
			["lime"] = 0x00ff00,
			["limegreen"] = 0x32cd32,
			["linen"] = 0xfaf0e6,
			["magenta"] = 0xff00ff,
			["maroon"] = 0x800000,
			["mediumaquamarine"] = 0x66cdaa,
			["mediumblue"] = 0x0000cd,
			["mediumorchid"] = 0xba55d3,
			["mediumpurple"] = 0x9370db,
			["mediumseagreen"] = 0x3cb371,
			["mediumslateblue"] = 0x7b68ee,
			["mediumspringgreen"] = 0x00fa9a,
			["mediumturquoise"] = 0x48d1cc,
			["mediumvioletred"] = 0xc71585,
			["midnightblue"] = 0x191970,
			["mintcream"] = 0xf5fffa,
			["mistyrose"] = 0xffe4e1,
			["moccasin"] = 0xffe4b5,
			["navajowhite"] = 0xffdead,
			["navy"] = 0x000080,
			["oldlace"] = 0xfdf5e6,
			["olive"] = 0x808000,
			["olivedrab"] = 0x6b8e23,
			["orange"] = 0xffa500,
			["orangered"] = 0xff4500,
			["orchid"] = 0xda70d6,
			["palegoldenrod"] = 0xeee8aa,
			["palegreen"] = 0x98fb98,
			["paleturquoise"] = 0xafeeee,
			["palevioletred"] = 0xdb7093,
			["papayawhip"] = 0xffefd5,
			["peachpuff"] = 0xffdab9,
			["peru"] = 0xcd853f,
			["pink"] = 0xffc0cb,
			["plum"] = 0xdda0dd,
			["powderblue"] = 0xb0e0e6,
			["purple"] = 0x800080,
			["rebeccapurple"] = 0x663399,
			["red"] = 0xff0000,
			["rosybrown"] = 0xbc8f8f,
			["royalblue"] = 0x4169e1,
			["saddlebrown"] = 0x8b4513,
			["salmon"] = 0xfa8072,
			["sandybrown"] = 0xf4a460,
			["seagreen"] = 0x2e8b57,
			["seashell"] = 0xfff5ee,
			["sienna"] = 0xa0522d,
			["silver"] = 0xc0c0c0,
			["skyblue"] = 0x87ceeb,
			["slateblue"] = 0x6a5acd,
			["slategray"] = 0x708090,
			["slategrey"] = 0x708090,
			["snow"] = 0xfffafa,
			["springgreen"] = 0x00ff7f,
			["steelblue"] = 0x4682b4,
			["tan"] = 0xd2b48c,
			["teal"] = 0x008080,
			["thistle"] = 0xd8bfd8,
			["tomato"] = 0xff6347,
			["turquoise"] = 0x40e0d0,
			["violet"] = 0xee82ee,
			["wheat"] = 0xf5deb3,
			["white"] = 0xffffff,
			["whitesmoke"] = 0xf5f5f5,
			["yellow"] = 0xffff00,
			["yellowgreen"] = 0x9acd32,
		};
	}
}
