/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// HSV &lt;-&gt; RGB conversions for <see cref="ColorPicker"/> (agg-gui's hsv_math). Hue is in degrees
	/// [0, 360); saturation, value and the RGB channels are in [0, 1].
	/// </summary>
	/// <remarks>
	/// Any byte colour survives <see cref="ToHsv"/> then <see cref="FromHsv"/> exactly: the doubles carry
	/// far more precision than a byte and <see cref="FromHsv"/> rounds to the nearest byte.
	/// </remarks>
	public static class HsvColor
	{
		/// <summary>
		/// Converts RGB in [0, 1] to HSV. A grey (no chroma) has hue 0 and saturation 0; black also has
		/// value 0. Channels are clamped to [0, 1] and NaN is treated as 0.
		/// </summary>
		public static void RgbToHsv(double r, double g, double b, out double hue, out double saturation, out double value)
		{
			r = Clamp01(r);
			g = Clamp01(g);
			b = Clamp01(b);
			var max = Math.Max(r, Math.Max(g, b));
			var min = Math.Min(r, Math.Min(g, b));
			var delta = max - min;
			value = max;
			saturation = max <= 0 ? 0 : delta / max;

			if (delta <= 0)
			{
				hue = 0;
			}
			else if (max == r)
			{
				hue = 60 * ((g - b) / delta);
			}
			else if (max == g)
			{
				hue = 60 * ((b - r) / delta + 2);
			}
			else
			{
				hue = 60 * ((r - g) / delta + 4);
			}

			hue = NormalizeHue(hue);
		}

		/// <summary>
		/// Converts HSV to RGB in [0, 1]. Hue wraps (360 is 0, -30 is 330); saturation and value are clamped
		/// to [0, 1]; NaN inputs are treated as 0.
		/// </summary>
		public static void HsvToRgb(double hue, double saturation, double value, out double r, out double g, out double b)
		{
			var h = NormalizeHue(hue) / 60;
			var s = Clamp01(saturation);
			var v = Clamp01(value);
			var c = v * s;
			var x = c * (1 - Math.Abs(h % 2 - 1));
			var m = v - c;
			(double r1, double g1, double b1) = ((int)Math.Floor(h)) switch
			{
				0 => (c, x, 0.0),
				1 => (x, c, 0.0),
				2 => (0.0, c, x),
				3 => (0.0, x, c),
				4 => (x, 0.0, c),
				_ => (c, 0.0, x),
			};
			r = r1 + m;
			g = g1 + m;
			b = b1 + m;
		}

		/// <summary>The HSV of a byte colour (its alpha is ignored).</summary>
		public static void ToHsv(Color color, out double hue, out double saturation, out double value)
		{
			RgbToHsv(color.red / 255.0, color.green / 255.0, color.blue / 255.0, out hue, out saturation, out value);
		}

		/// <summary>
		/// The byte colour nearest to <paramref name="hue"/>, <paramref name="saturation"/> and
		/// <paramref name="value"/>, with <paramref name="alpha"/> in [0, 1].
		/// </summary>
		public static Color FromHsv(double hue, double saturation, double value, double alpha = 1)
		{
			HsvToRgb(hue, saturation, value, out var r, out var g, out var b);
			return new Color(ToByte(r), ToByte(g), ToByte(b), ToByte(alpha));
		}

		/// <summary>Formats as <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> when the colour is not opaque.</summary>
		public static string ToHex(Color color)
		{
			return color.alpha == 255
				? $"#{color.red:X2}{color.green:X2}{color.blue:X2}"
				: $"#{color.red:X2}{color.green:X2}{color.blue:X2}{color.alpha:X2}";
		}

		/// <summary>Maps any hue to [0, 360); NaN and infinities become 0.</summary>
		public static double NormalizeHue(double hue)
		{
			if (!double.IsFinite(hue))
			{
				return 0;
			}

			hue %= 360;
			if (hue < 0)
			{
				hue += 360;
			}

			// A tiny negative hue plus 360 rounds to exactly 360.
			return hue >= 360 ? 0 : hue;
		}

		private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1);

		private static int ToByte(double v) => (int)Math.Round(Clamp01(v) * 255, MidpointRounding.AwayFromZero);
	}
}
