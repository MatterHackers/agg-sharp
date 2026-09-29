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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>How a render measured against its reference under <see cref="SvgTolerantCompare"/>.</summary>
	/// <param name="Pass">Whether the render counts as drawn right.</param>
	/// <param name="Ratio">The share of all pixels that broke their bound (interior or edge).</param>
	/// <param name="BadPixels">How many pixels broke their bound.</param>
	/// <param name="CoverageDelta">How far the rendered alpha sum is off the reference's, over the reference's (its area); pixels within the interior tolerance do not count.</param>
	public readonly record struct SvgTolerantCompareResult(bool Pass, double Ratio, int BadPixels, double CoverageDelta);

	/// <summary>The bounds <see cref="SvgTolerantCompare"/> applies; <see cref="Default"/> is the calibrated pass rule.</summary>
	/// <param name="InteriorTolerance">How far (0-255, premultiplied, any channel) a pixel away from any reference edge may be off.</param>
	/// <param name="EdgeTolerance">How far a pixel within 1 px of a reference edge may be off.</param>
	/// <param name="EdgeRange">A reference pixel is an edge when some channel spans more than this over its 3x3 neighbourhood.</param>
	/// <param name="BadPixelRatio">The share of pixels that may break their bound.</param>
	/// <param name="CoverageTolerance">How far total coverage (the alpha sum) may be off, as a share of the reference's.</param>
	/// <param name="CoverageSlackPixels">Coverage slack in whole pixels on top of that, so a shape a few pixels big is not held to 1% of itself.</param>
	public readonly record struct SvgTolerance(int InteriorTolerance, int EdgeTolerance, int EdgeRange, double BadPixelRatio, double CoverageTolerance, double CoverageSlackPixels)
	{
		public static SvgTolerance Default { get; } = new SvgTolerance(3, 64, 32, .001, .01, 2);
	}

	/// <summary>
	/// agg-sharp's pass rule for the resvg suite: the drawing must be right, but antialiased edges and gradients may
	/// differ by a few shades. agg keeps its exact-area edge coverage where resvg (tiny-skia) supersamples, and the two
	/// round gradients differently, so <see cref="SvgCompare"/>'s exact rule (agg-gui's) fails renders that look the
	/// same. Missing, misplaced, wrong-colour or wrong-shape content must still fail.
	///
	/// Pixels are compared premultiplied, so colour under zero alpha does not count. A pixel is an edge when the
	/// reference changes within 1 px of it; only there may a pixel be off by more than a few shades. A render passes
	/// when few pixels break their bound and its total coverage matches the reference's area, so a shape shifted
	/// past the 1 px edge band, or a missing, extra or recoloured one, still fails.
	/// </summary>
	public static class SvgTolerantCompare
	{
		/// <summary>Compares two straight-alpha RGBA byte arrays of the same size, <paramref name="width"/> pixels wide (see <see cref="SvgCompare.ToRgba"/>).</summary>
		public static SvgTolerantCompareResult Compare(byte[] rendered, byte[] reference, int width) => Compare(rendered, reference, width, SvgTolerance.Default);

		/// <summary>Compares under explicit bounds; the suite report's pass rule is <see cref="SvgTolerance.Default"/>.</summary>
		public static SvgTolerantCompareResult Compare(byte[] rendered, byte[] reference, int width, SvgTolerance tolerance)
		{
			int pixels = rendered.Length / 4;
			if (rendered.Length != reference.Length || rendered.Length % 4 != 0 || width <= 0 || pixels % width != 0)
			{
				return new SvgTolerantCompareResult(false, 1, pixels, 1);
			}

			int height = pixels / width;
			int[] ours = Premultiplied(rendered);
			int[] theirs = Premultiplied(reference);
			int bad = 0;
			long coverageError = 0;
			long theirCoverage = 0;
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int i = (y * width + x) * 4;
					theirCoverage += theirs[i + 3];
					int delta = 0;
					for (int c = 0; c < 4; c++)
					{
						delta = Math.Max(delta, Math.Abs(ours[i + c] - theirs[i + c]));
					}

					if (delta <= tolerance.InteriorTolerance)
					{
						// Within a few shades everywhere is a match, and does not count toward the coverage guard either:
						// a large translucent fill one alpha level off (allowed) would otherwise add up to several percent.
						continue;
					}

					coverageError += ours[i + 3] - theirs[i + 3];
					if (delta > tolerance.EdgeTolerance || !IsEdge(theirs, width, height, x, y, tolerance.EdgeRange))
					{
						bad++;
					}
				}
			}

			double area = theirCoverage / 255.0;
			double coverageDelta = Math.Abs(coverageError) / 255.0;
			double ratio = bad / (double)Math.Max(1, pixels);
			bool pass = ratio <= tolerance.BadPixelRatio
				&& coverageDelta <= tolerance.CoverageTolerance * area + tolerance.CoverageSlackPixels;
			return new SvgTolerantCompareResult(pass, ratio, bad, area > 0 ? coverageDelta / area : coverageDelta);
		}

		/// <summary>Whether the reference changes within 1 px of (x, y): some premultiplied channel spans more than <paramref name="edgeRange"/> over the 3x3 neighbourhood.</summary>
		private static bool IsEdge(int[] image, int width, int height, int x, int y, int edgeRange)
		{
			for (int c = 0; c < 4; c++)
			{
				int min = 255;
				int max = 0;
				for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
				{
					for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
					{
						int value = image[(ny * width + nx) * 4 + c];
						min = Math.Min(min, value);
						max = Math.Max(max, value);
					}
				}

				if (max - min > edgeRange)
				{
					return true;
				}
			}

			return false;
		}

		private static int[] Premultiplied(byte[] rgba)
		{
			var result = new int[rgba.Length];
			for (int i = 0; i < rgba.Length; i += 4)
			{
				int alpha = rgba[i + 3];
				result[i] = (rgba[i] * alpha + 127) / 255;
				result[i + 1] = (rgba[i + 1] * alpha + 127) / 255;
				result[i + 2] = (rgba[i + 2] * alpha + 127) / 255;
				result[i + 3] = alpha;
			}

			return result;
		}
	}
}
