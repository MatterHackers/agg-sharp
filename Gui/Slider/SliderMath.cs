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

namespace MatterHackers.Agg.UI
{
	/// <summary>When a <see cref="Slider"/> clamps its value to its range (egui's SliderClamping).</summary>
	public enum SliderClamping
	{
		/// <summary>Never clamp: the value may sit outside the range, with the thumb pinned to the end.</summary>
		Never,

		/// <summary>Clamp values the user makes (drag, arrow keys); a value set in code is left as it is.</summary>
		Edits,

		/// <summary>Always clamp, including a value set in code. What a Slider has always done.</summary>
		Always,
	}

	/// <summary>The shape of a <see cref="Slider"/>'s thumb.</summary>
	public enum SliderHandleShape
	{
		/// <summary>An accent ring (the default).</summary>
		Circle,

		/// <summary>A rounded rectangle half as long along the track as it is across it.</summary>
		Rectangle,
	}

	/// <summary>
	/// The numbers behind a <see cref="Slider"/>: value to 0..1 position and back (linear or logarithmic,
	/// including ranges that span zero or reach infinity), clamping, step and integer rounding, and "smart aim"
	/// (the roundest value under the pointer). Pure functions, ported from agg-gui's slider_math.rs, itself
	/// egui's slider.rs and emath's smart_aim.rs, so they behave like egui's slider.
	/// </summary>
	public static class SliderMath
	{
		/// <summary>An infinitely large logarithmic range (e.g. from zero) spans this many orders of magnitude.</summary>
		private const double InfiniteRangeMagnitude = 10;

		private const int NumDecimals = 16;

		/// <summary>
		/// Clamps <paramref name="x"/> into the range, tolerating a reversed (high to low) range.
		/// A NaN end sorts above everything, as egui's total_cmp orders a positive NaN, so the value passes that side.
		/// </summary>
		public static double ClampToRange(double x, double min, double max)
		{
			// .NET's CompareTo sorts NaN below everything, the opposite of total_cmp, so a NaN bound is taken as
			// the infinity it acts as there.
			min = double.IsNaN(min) ? double.PositiveInfinity : min;
			max = double.IsNaN(max) ? double.PositiveInfinity : max;
			if (min.CompareTo(max) > 0)
			{
				(min, max) = (max, min);
			}

			if (x.CompareTo(min) <= 0)
			{
				return min;
			}

			return x.CompareTo(max) >= 0 ? max : x;
		}

		/// <summary>
		/// The 0..1 position of <paramref name="value"/>. Values outside the range pin to its ends; an empty range
		/// puts the value in the middle. <paramref name="smallestPositive"/> and <paramref name="largestFinite"/> set
		/// where a logarithmic range that reaches 0 or infinity starts and ends.
		/// </summary>
		public static double NormalizedFromValue(double value, double min, double max, bool logarithmic, double smallestPositive = 1e-6, double largestFinite = double.PositiveInfinity)
		{
			if (double.IsNaN(min) || double.IsNaN(max))
			{
				return double.NaN;
			}

			if (min == max)
			{
				return .5;
			}

			if (min > max)
			{
				return 1 - NormalizedFromValue(value, max, min, logarithmic, smallestPositive, largestFinite);
			}

			if (value <= min)
			{
				return 0;
			}

			if (value >= max)
			{
				return 1;
			}

			if (!logarithmic)
			{
				return RemapClamp(value, min, max, 0, 1);
			}

			if (max <= 0)
			{
				return NormalizedFromValue(-value, -min, -max, true, smallestPositive, largestFinite);
			}

			if (min >= 0)
			{
				(double minLog, double maxLog) = RangeLog10(min, max, smallestPositive, largestFinite);
				return RemapClamp(Math.Log10(value), minLog, maxLog, 0, 1);
			}

			double zeroCutoff = LogarithmicZeroCutoff(min, max);
			return value < 0
				? Remap(NormalizedFromValue(value, min, 0, true, smallestPositive, largestFinite), 0, 1, 0, zeroCutoff)
				: Remap(NormalizedFromValue(value, 0, max, true, smallestPositive, largestFinite), 0, 1, zeroCutoff, 1);
		}

		/// <summary>The inverse of <see cref="NormalizedFromValue"/>: the value at a 0..1 position.</summary>
		public static double ValueFromNormalized(double normalized, double min, double max, bool logarithmic, double smallestPositive = 1e-6, double largestFinite = double.PositiveInfinity)
		{
			if (double.IsNaN(min) || double.IsNaN(max))
			{
				return double.NaN;
			}

			if (min == max)
			{
				return min;
			}

			if (min > max)
			{
				return ValueFromNormalized(1 - normalized, max, min, logarithmic, smallestPositive, largestFinite);
			}

			if (normalized <= 0)
			{
				return min;
			}

			if (normalized >= 1)
			{
				return max;
			}

			if (!logarithmic)
			{
				return Lerp(min, max, normalized);
			}

			if (max <= 0)
			{
				return -ValueFromNormalized(normalized, -min, -max, true, smallestPositive, largestFinite);
			}

			if (min >= 0)
			{
				(double minLog, double maxLog) = RangeLog10(min, max, smallestPositive, largestFinite);
				return Math.Pow(10, Lerp(minLog, maxLog, normalized));
			}

			double zeroCutoff = LogarithmicZeroCutoff(min, max);
			return normalized < zeroCutoff
				? ValueFromNormalized(Remap(normalized, 0, zeroCutoff, 0, 1), min, 0, true, smallestPositive, largestFinite)
				: ValueFromNormalized(Remap(normalized, zeroCutoff, 1, 0, 1), 0, max, true, smallestPositive, largestFinite);
		}

		/// <summary>
		/// Applies a slider's rules to a new value: clamp to the range when <paramref name="clamp"/>, snap to
		/// multiples of <paramref name="step"/> counted from <paramref name="min"/> (when step is positive and both
		/// are finite, as an infinite origin has no multiples), then round to a whole number when
		/// <paramref name="integer"/>. Returns NaN unchanged; callers reject it.
		/// </summary>
		public static double Commit(double value, double min, double max, bool clamp, double step, bool integer)
		{
			if (double.IsNaN(value))
			{
				return value;
			}

			if (clamp)
			{
				value = ClampToRange(value, min, max);
			}

			if (step > 0 && double.IsFinite(step) && double.IsFinite(min))
			{
				value = min + Math.Round((value - min) / step, MidpointRounding.AwayFromZero) * step;
			}

			if (integer)
			{
				value = Math.Round(value, MidpointRounding.AwayFromZero);
			}

			return value;
		}

		/// <summary>
		/// The "simplest" number in [min, max] - the one with the fewest significant digits, preferring zero and
		/// fives: [0.83, 1.354] gives 1, [0.37, 0.48] gives 0.4. Dragging with smart aim snaps to this.
		/// </summary>
		public static double BestInRange(double min, double max)
		{
			if (double.IsNaN(min))
			{
				return max;
			}

			if (double.IsNaN(max))
			{
				return min;
			}

			if (max < min)
			{
				return BestInRange(max, min);
			}

			if (min == max)
			{
				return min;
			}

			if (min <= 0 && max >= 0)
			{
				return 0;
			}

			if (min < 0)
			{
				return -BestInRange(-max, -min);
			}

			if (!double.IsFinite(max))
			{
				return min;
			}

			double minExponent = Math.Log10(min);
			double maxExponent = Math.Log10(max);

			if (Math.Floor(minExponent) != Math.Floor(maxExponent))
			{
				// Different orders of magnitude: the power of ten nearest their geometric centre.
				double exponent = (minExponent + maxExponent) / 2;
				return Math.Pow(10, Math.Round(exponent, MidpointRounding.AwayFromZero));
			}

			if (Math.Round(minExponent) == minExponent)
			{
				return Math.Pow(10, minExponent);
			}

			if (Math.Round(maxExponent) == maxExponent)
			{
				return Math.Pow(10, maxExponent);
			}

			// Scale both to 16 digit integers of the same length; everything before the first differing digit is
			// kept, that digit becomes the roundest one available, and the rest are zero.
			int scale = NumDecimals - (int)Math.Floor(maxExponent) - 1;
			double scaleFactor = Math.Pow(10, scale);
			byte[] minDigits = ToDigits((ulong)Math.Round(min * scaleFactor, MidpointRounding.AwayFromZero));
			byte[] maxDigits = ToDigits((ulong)Math.Round(max * scaleFactor, MidpointRounding.AwayFromZero));
			var result = new byte[NumDecimals];

			for (int i = 0; i < NumDecimals; i++)
			{
				if (minDigits[i] == maxDigits[i])
				{
					result[i] = minDigits[i];
					continue;
				}

				int decidingMin = minDigits[i];
				int decidingMax = maxDigits[i];
				bool restOfMinIsZero = true;
				for (int j = i + 1; j < NumDecimals; j++)
				{
					restOfMinIsZero &= minDigits[j] == 0;
				}

				// More digits after the min's deciding digit put the true minimum one above it.
				if (!restOfMinIsZero)
				{
					decidingMin++;
				}

				int deciding = decidingMin == 0 ? 0
					: decidingMin <= 5 && decidingMax >= 5 ? 5
					: (decidingMin + decidingMax) / 2;
				result[i] = (byte)deciding;
				return FromDigits(result) / scaleFactor;
			}

			return min;
		}

		/// <summary>
		/// How many decimals a slider shows for <paramref name="value"/> when none were asked for: none for an
		/// integer slider, else from the step, else (no step, as on a logarithmic slider) from the value's
		/// magnitude so 10000 shows none and 0.001 shows several.
		/// </summary>
		public static int AutoDecimals(double value, double step, bool integer)
		{
			if (integer || step >= 1)
			{
				return 0;
			}

			if (step >= .1)
			{
				return 1;
			}

			if (step >= .01)
			{
				return 2;
			}

			if (step > 0)
			{
				return 3;
			}

			double magnitude = Math.Abs(value);
			if (magnitude == 0 || !double.IsFinite(magnitude))
			{
				return 2;
			}

			return Math.Clamp(3 - (int)Math.Floor(Math.Log10(magnitude)), 0, 6);
		}

		private static double Lerp(double a, double b, double t) => a + (b - a) * t;

		private static double Remap(double x, double fromLo, double fromHi, double toLo, double toHi) => Lerp(toLo, toHi, (x - fromLo) / (fromHi - fromLo));

		private static double RemapClamp(double x, double fromLo, double fromHi, double toLo, double toHi)
		{
			if (x <= Math.Min(fromLo, fromHi))
			{
				return fromLo <= fromHi ? toLo : toHi;
			}

			if (x >= Math.Max(fromLo, fromHi))
			{
				return fromLo <= fromHi ? toHi : toLo;
			}

			return Remap(x, fromLo, fromHi, toLo, toHi);
		}

		private static (double, double) RangeLog10(double min, double max, double smallestPositive, double largestFinite)
		{
			if (min == 0 && double.IsPositiveInfinity(max))
			{
				return (Math.Log10(smallestPositive), InfiniteRangeMagnitude);
			}

			if (min == 0)
			{
				return smallestPositive < max
					? (Math.Log10(smallestPositive), Math.Log10(max))
					: (Math.Log10(max) - InfiniteRangeMagnitude, Math.Log10(max));
			}

			if (double.IsPositiveInfinity(max))
			{
				return min < largestFinite
					? (Math.Log10(min), Math.Log10(largestFinite))
					: (Math.Log10(min), Math.Log10(min) + InfiniteRangeMagnitude);
			}

			return (Math.Log10(min), Math.Log10(max));
		}

		/// <summary>Where the zero crossing sits on a logarithmic slider whose range spans zero.</summary>
		private static double LogarithmicZeroCutoff(double min, double max)
		{
			double minMagnitude = double.IsNegativeInfinity(min) ? InfiniteRangeMagnitude : Math.Abs(Math.Log10(Math.Abs(min)));
			double maxMagnitude = double.IsPositiveInfinity(max) ? InfiniteRangeMagnitude : Math.Abs(Math.Log10(max));
			return minMagnitude / (minMagnitude + maxMagnitude);
		}

		private static byte[] ToDigits(ulong value)
		{
			var digits = new byte[NumDecimals];
			for (int i = NumDecimals - 1; i >= 0; i--)
			{
				digits[i] = (byte)(value % 10);
				value /= 10;
			}

			return digits;
		}

		private static double FromDigits(byte[] digits)
		{
			ulong value = 0;
			foreach (byte digit in digits)
			{
				value = value * 10 + digit;
			}

			return value;
		}
	}
}
