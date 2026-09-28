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
	/// <summary>SVG numbers, number lists and lengths (with units and percentages) in user units.</summary>
	public static class SvgLength
	{
		/// <summary>The font size em and ex are relative to until text is supported: resvg's and browsers' default.</summary>
		public const double DefaultFontSize = 16;

		/// <summary>
		/// <paramref name="text"/> as a length in user units, or <paramref name="fallback"/> when it is missing or
		/// not a length. A percentage is of <paramref name="percentOf"/>.
		/// </summary>
		public static double Parse(string text, double fallback, double percentOf = 0)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return fallback;
			}

			string value = text.Trim();
			int end = NumberEnd(value, 0);
			if (end == 0 || !double.TryParse(value.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
			{
				return fallback;
			}

			switch (value.Substring(end).Trim().ToLowerInvariant())
			{
				case "":
				case "px":
					return number;
				case "%":
					return number / 100 * percentOf;
				case "pt":
					return number * 4 / 3;
				case "pc":
					return number * 16;
				case "mm":
					return number * 96 / 25.4;
				case "cm":
					return number * 96 / 2.54;
				case "in":
					return number * 96;
				case "em":
					return number * DefaultFontSize;
				case "ex":
					return number * DefaultFontSize / 2;
				default:
					return fallback;
			}
		}

		/// <summary>A plain number, or <paramref name="fallback"/>; a trailing '%' makes it a fraction (opacity="50%").</summary>
		public static double ParseNumber(string text, double fallback)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return fallback;
			}

			string value = text.Trim();
			bool percent = value.EndsWith("%");
			return double.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
				? (percent ? number / 100 : number)
				: fallback;
		}

		/// <summary>
		/// The numbers of a whitespace- and/or comma-separated list, as points="..." and viewBox="..." are written.
		/// Parsing stops at the first thing that is not a number; "1.5.5" is 1.5 then .5, as SVG reads it.
		/// </summary>
		public static List<double> ParseList(string text)
		{
			var numbers = new List<double>();
			if (text == null)
			{
				return numbers;
			}

			int index = 0;
			while (true)
			{
				while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
				{
					index++;
				}

				int end = NumberEnd(text, index);
				if (end == index
					|| !double.TryParse(text.Substring(index, end - index), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
				{
					return numbers;
				}

				numbers.Add(number);
				index = end;
			}
		}

		/// <summary>Where the number starting at <paramref name="start"/> ends: sign, digits, one '.', exponent.</summary>
		internal static int NumberEnd(string text, int start)
		{
			int i = start;
			if (i < text.Length && (text[i] == '+' || text[i] == '-'))
			{
				i++;
			}

			bool digits = false, dot = false;
			while (i < text.Length && (char.IsDigit(text[i]) || (text[i] == '.' && !dot)))
			{
				dot |= text[i] == '.';
				digits |= char.IsDigit(text[i]);
				i++;
			}

			if (!digits)
			{
				return start;
			}

			if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
			{
				int exponent = i + 1;
				if (exponent < text.Length && (text[exponent] == '+' || text[exponent] == '-'))
				{
					exponent++;
				}

				if (exponent < text.Length && char.IsDigit(text[exponent]))
				{
					i = exponent;
					while (i < text.Length && char.IsDigit(text[i]))
					{
						i++;
					}
				}
			}

			return i;
		}
	}
}
