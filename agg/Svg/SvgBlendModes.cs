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
using System.Linq;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The non-separable CSS blend modes (Compositing and Blending Level 1, section 10.2): hue, saturation, color
	/// and luminosity, on straight RGB 0..1, with the spec's Lum, ClipColor, SetLum, Sat and SetSat.
	/// </summary>
	internal static class SvgBlendModes
	{
		/// <summary>B(backdrop, source) for a non-separable mode, or null when <paramref name="mode"/> is not one.</summary>
		public static Func<double[], double[], double[]> NonSeparable(string mode)
		{
			switch (mode)
			{
				case "hue":
					return (b, s) => SetLum(SetSat(s, Sat(b)), Lum(b));
				case "saturation":
					return (b, s) => SetLum(SetSat(b, Sat(s)), Lum(b));
				case "color":
					return (b, s) => SetLum(s, Lum(b));
				case "luminosity":
					return (b, s) => SetLum(b, Lum(s));
				default:
					return null;
			}
		}

		private static double Lum(double[] c) => .3 * c[0] + .59 * c[1] + .11 * c[2];

		private static double Sat(double[] c) => c.Max() - c.Min();

		private static double[] SetLum(double[] c, double l)
		{
			double d = l - Lum(c);
			return ClipColor(new[] { c[0] + d, c[1] + d, c[2] + d });
		}

		/// <summary>Pulls a colour whose channels left 0..1 back in, keeping its luminosity.</summary>
		private static double[] ClipColor(double[] c)
		{
			double l = Lum(c);
			double n = c.Min();
			double x = c.Max();
			var result = (double[])c.Clone();
			for (int i = 0; i < 3; i++)
			{
				if (n < 0)
				{
					result[i] = l + (result[i] - l) * l / (l - n);
				}

				if (x > 1)
				{
					result[i] = l + (result[i] - l) * (1 - l) / (x - l);
				}
			}

			return result;
		}

		/// <summary>The colour with its max-min spread set to <paramref name="s"/>, the middle channel kept in proportion.</summary>
		private static double[] SetSat(double[] c, double s)
		{
			int[] order = Enumerable.Range(0, 3).OrderBy(i => c[i]).ToArray();
			int min = order[0], mid = order[1], max = order[2];
			var result = new double[3];
			if (c[max] > c[min])
			{
				result[mid] = (c[mid] - c[min]) * s / (c[max] - c[min]);
				result[max] = s;
			}

			return result;
		}
	}
}
