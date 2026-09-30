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

using System.Globalization;

namespace MatterHackers.Agg
{
	/// <summary>
	/// A number as the user sees and types it - the one place that decides both. What is shown is in the user's
	/// culture ("1,5" on a comma-decimal machine) and never grouped; what is typed is read invariant first, so
	/// "1.5" is 1.5 for everyone, then in the culture, so "1,5" is 1.5 where that is the decimal. Neither read
	/// allows group separators: a culture read with them takes the '.' of "1.5" as a thousands mark under de-DE
	/// (15), and an invariant one takes the ',' of "2,5" as one (25). Stored text (files, settings, formulas)
	/// never goes through here; it is always invariant.
	/// </summary>
	public static class DisplayNumber
	{
		/// <summary>
		/// <paramref name="value"/> as shown to the user: <paramref name="format"/> (a custom pattern such as
		/// "0.###", never one that groups) in the current culture, or the shortest round-trip text without one.
		/// </summary>
		public static string Format(double value, string format = null)
		{
			return value.ToString(string.IsNullOrEmpty(format) ? "R" : format, CultureInfo.CurrentCulture);
		}

		/// <summary>
		/// Reads text the user typed, invariant first and then in <paramref name="culture"/>, never grouped. Text
		/// that <see cref="IsAmbiguousGrouping"/> is refused rather than guessed: "1.000" under de-DE is a thousand
		/// to the user and one to an invariant read.
		/// </summary>
		public static bool TryRead(string text, CultureInfo culture, out double value)
		{
			if (IsAmbiguousGrouping(text, culture))
			{
				value = 0;
				return false;
			}

			return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
				|| double.TryParse(text, NumberStyles.Float, culture, out value);
		}

		/// <summary>
		/// True for typed text that is exactly a thousands-grouped whole number in <paramref name="culture"/>:
		/// one to three digits not starting with 0, then one or more groups of its group separator and exactly three digits ("1.000",
		/// "12.500", "1.000.000" under de-DE; "1,000" under en-US). Under de-DE "1.000" is a thousand to the user
		/// but one to an invariant read, and under en-US "1,000" would be stored as typed and read back as 1, so
		/// both are one rule: never guessed. "1.5", "2.25" and "1.0000" are not groups and stay decimals.
		/// </summary>
		public static bool IsAmbiguousGrouping(string text, CultureInfo culture)
		{
			var numberFormat = culture.NumberFormat;
			var group = numberFormat.NumberGroupSeparator;
			if (text == null
				|| string.IsNullOrEmpty(group)
				|| group == numberFormat.NumberDecimalSeparator)
			{
				return false;
			}

			return System.Text.RegularExpressions.Regex.IsMatch(text.Trim(),
				@"^[+-]?[1-9]\d{0,2}(" + System.Text.RegularExpressions.Regex.Escape(group) + @"\d{3})+$");
		}

		/// <summary>Reads text the user typed in their own culture (<see cref="CultureInfo.CurrentCulture"/>).</summary>
		public static bool TryRead(string text, out double value) => TryRead(text, CultureInfo.CurrentCulture, out value);
	}
}
