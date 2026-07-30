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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Gray-buffer to packed-mask downsampling filters.
	/// </summary>
	/// <remarks>
	/// Ported byte-for-byte from the agg-gui Rust reference (<c>lcd_coverage\filter.rs</c>).
	/// <para>
	/// The rasterization stage writes AGG coverage into a 3x-horizontally-supersampled gray buffer
	/// (<c>grayWidth == maskWidth * 3</c>, one byte per subpixel, row stride == grayWidth, rows Y-up).
	/// This class owns the second stage: collapsing that buffer into the packed 3-byte-per-pixel mask
	/// that composites onto the framebuffer. Two flavours:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="Apply5TapFilter"/> - the LCD subpixel path: a phase-shifted 5-tap
	/// low-pass per channel, so R/G/B carry independent coverage aligned to the panel's physical
	/// subpixels.</description></item>
	/// <item><description><see cref="ApplyGrayCollapse"/> - the grayscale path: box-average each triple
	/// into one whole-pixel coverage value replicated across the channels, giving anti-aliased edges
	/// with no subpixel chroma.</description></item>
	/// </list>
	/// <para>
	/// A <b>freshly allocated</b> 8 bit-per-pixel <see cref="MatterHackers.Agg.Image.ImageBuffer"/>
	/// allocates its stride as <c>width * 1</c> with a zero first-pixel offset and Y-up rows, so its
	/// <c>GetBuffer()</c> can be handed straight to these methods. That does not hold in general: a
	/// buffer attached to a sub-region of another image carries a larger stride and a non-zero offset,
	/// and <c>InvertYLookupTable()</c> flips the row order out from under the Y-up assumption. Those
	/// cases need a compacted, Y-up copy first.
	/// </para>
	/// </remarks>
	public static class LcdFilter
	{
		/// <summary>
		/// FreeType-default 5-tap weights; sum = 9. Heavier filter weights reduce color fringing at the
		/// cost of sharpness; tuning against this table is the standard knob for "darker / lighter" LCD
		/// text. These are the legacy baked-in weights - still used as the fallback when the primary
		/// weight sits at its default 1/3 (at which point <see cref="LcdFilterWeights"/> reproduces
		/// <c>[1, 2, 3, 2, 1] / 9</c>).
		/// </summary>
		private static readonly int[] FilterWeights = new int[] { 1, 2, 3, 2, 1 };

		private const int FilterSum = 9;

		/// <summary>
		/// The primary (center) tap weight at which the parameterized filter reproduces the legacy
		/// integer <c>[1, 2, 3, 2, 1] / 9</c> kernel.
		/// </summary>
		public const double DefaultPrimaryWeight = 1.0 / 3.0;

		/// <summary>
		/// Gamma of 1 means no curve is applied to the post-filter coverage.
		/// </summary>
		public const double DefaultGamma = 1.0;

		/// <summary>
		/// Runs the 5-tap low-pass filter over <paramref name="gray"/> and produces the packed (R,G,B)
		/// coverage mask.
		/// </summary>
		/// <param name="gray">3x-wide 8-bit gray coverage, <c>grayWidth</c> x <c>maskHeight</c>, row
		/// stride <paramref name="grayWidth"/>, rows Y-up.</param>
		/// <param name="grayWidth">Subpixels per row; normally <c>maskWidth * 3</c>.</param>
		/// <param name="maskWidth">Output mask width in whole pixels.</param>
		/// <param name="maskHeight">Output mask height in rows.</param>
		/// <param name="primaryWeight">Center tap weight. At <see cref="DefaultPrimaryWeight"/> the
		/// byte-exact integer path is used.</param>
		/// <param name="gamma">Curve applied to the per-channel coverage after the filter sum. At
		/// <see cref="DefaultGamma"/> no curve is applied.</param>
		public static LcdMask Apply5TapFilter(
			byte[] gray,
			int grayWidth,
			int maskWidth,
			int maskHeight,
			double primaryWeight = DefaultPrimaryWeight,
			double gamma = DefaultGamma)
		{
			// Decide once whether the current parameters reproduce the legacy integer filter exactly.
			// When they do (primary = 1/3, gamma = 1), run the original byte-for-byte path so every
			// mask cached before any slider-driven raster produces the EXACT same bytes. This is a
			// correctness fast path, not just a performance one - double arithmetic on e.g.
			// (128 + 256 + 384 + 256 + 128) / 9 gives 127.999... which rounds down to 127, where the
			// integer version gives a clean 128. Sub-byte drift on cached masks is invisible in
			// isolation but accumulates into a faint "fade" across a paragraph of text, so the old
			// path is kept exact.
			bool isDefaultPrimary = Math.Abs(primaryWeight - DefaultPrimaryWeight) < 1e-6;
			bool isDefaultGamma = Math.Abs(gamma - DefaultGamma) < 1e-6;
			if (isDefaultPrimary && isDefaultGamma)
			{
				return Apply5TapFilterLegacy(gray, grayWidth, maskWidth, maskHeight);
			}

			ValidateGrayBuffer(gray, grayWidth, maskWidth, maskHeight);

			var mask = new LcdMask(maskWidth, maskHeight);
			byte[] data = mask.Data;
			int gw = grayWidth;

			// Parameterized path - double weights driven by the primary weight, plus a gamma curve
			// applied to the per-channel coverage AFTER the filter sum so light AA edges strengthen or
			// weaken uniformly.
			double[] w = LcdFilterWeights(primaryWeight);
			double invGamma = 1.0 / Math.Max(gamma, 1e-3);
			bool needGamma = !isDefaultGamma;

			for (int py = 0; py < maskHeight; py++)
			{
				int rowStart = py * grayWidth;
				for (int px = 0; px < maskWidth; px++)
				{
					int basePos = px * 3;

					// R samples [-2..=2], G shifts +1, B shifts +2 (phase offsets between the three
					// physical subpixels of the output pixel).
					double coverageRed = (w[0] * Sample(gray, rowStart, gw, basePos - 2))
						+ (w[1] * Sample(gray, rowStart, gw, basePos - 1))
						+ (w[2] * Sample(gray, rowStart, gw, basePos))
						+ (w[3] * Sample(gray, rowStart, gw, basePos + 1))
						+ (w[4] * Sample(gray, rowStart, gw, basePos + 2));
					double coverageGreen = (w[0] * Sample(gray, rowStart, gw, basePos - 1))
						+ (w[1] * Sample(gray, rowStart, gw, basePos))
						+ (w[2] * Sample(gray, rowStart, gw, basePos + 1))
						+ (w[3] * Sample(gray, rowStart, gw, basePos + 2))
						+ (w[4] * Sample(gray, rowStart, gw, basePos + 3));
					double coverageBlue = (w[0] * Sample(gray, rowStart, gw, basePos))
						+ (w[1] * Sample(gray, rowStart, gw, basePos + 1))
						+ (w[2] * Sample(gray, rowStart, gw, basePos + 2))
						+ (w[3] * Sample(gray, rowStart, gw, basePos + 3))
						+ (w[4] * Sample(gray, rowStart, gw, basePos + 4));

					int maskIndex = ((py * maskWidth) + px) * 3;

					// Rounding here (rather than a bare truncating cast) matches the integer filter's
					// rounding semantics more closely - minor but measurable difference near mid-gray.
					data[maskIndex] = RoundAndClamp(ApplyGamma(coverageRed, needGamma, invGamma));
					data[maskIndex + 1] = RoundAndClamp(ApplyGamma(coverageGreen, needGamma, invGamma));
					data[maskIndex + 2] = RoundAndClamp(ApplyGamma(coverageBlue, needGamma, invGamma));
				}
			}

			return mask;
		}

		/// <summary>
		/// Byte-exact legacy 5-tap filter - the default path (primary weight 1/3, gamma 1). Truncating
		/// divide by 9 then clamp to 255, exactly as the Rust reference.
		/// </summary>
		/// <remarks>
		/// Deliberately not public (the Rust reference's twin is private): callers must go through
		/// <see cref="Apply5TapFilter"/> so the parameterized dispatch always gets a say. Visible to
		/// Agg.Tests so the byte-exact path can be pinned directly.
		/// </remarks>
		internal static LcdMask Apply5TapFilterLegacy(byte[] gray, int grayWidth, int maskWidth, int maskHeight)
		{
			ValidateGrayBuffer(gray, grayWidth, maskWidth, maskHeight);

			var mask = new LcdMask(maskWidth, maskHeight);
			byte[] data = mask.Data;
			int gw = grayWidth;
			for (int py = 0; py < maskHeight; py++)
			{
				int rowStart = py * grayWidth;
				for (int px = 0; px < maskWidth; px++)
				{
					int basePos = px * 3;

					// Sums peak at 255 * 9 = 2295, so int cannot overflow; every term is
					// non-negative, so C#'s truncate-toward-zero divide matches Rust's u32 divide.
					int coverageRed = ((FilterWeights[0] * SampleInt(gray, rowStart, gw, basePos - 2))
						+ (FilterWeights[1] * SampleInt(gray, rowStart, gw, basePos - 1))
						+ (FilterWeights[2] * SampleInt(gray, rowStart, gw, basePos))
						+ (FilterWeights[3] * SampleInt(gray, rowStart, gw, basePos + 1))
						+ (FilterWeights[4] * SampleInt(gray, rowStart, gw, basePos + 2)))
						/ FilterSum;
					int coverageGreen = ((FilterWeights[0] * SampleInt(gray, rowStart, gw, basePos - 1))
						+ (FilterWeights[1] * SampleInt(gray, rowStart, gw, basePos))
						+ (FilterWeights[2] * SampleInt(gray, rowStart, gw, basePos + 1))
						+ (FilterWeights[3] * SampleInt(gray, rowStart, gw, basePos + 2))
						+ (FilterWeights[4] * SampleInt(gray, rowStart, gw, basePos + 3)))
						/ FilterSum;
					int coverageBlue = ((FilterWeights[0] * SampleInt(gray, rowStart, gw, basePos))
						+ (FilterWeights[1] * SampleInt(gray, rowStart, gw, basePos + 1))
						+ (FilterWeights[2] * SampleInt(gray, rowStart, gw, basePos + 2))
						+ (FilterWeights[3] * SampleInt(gray, rowStart, gw, basePos + 3))
						+ (FilterWeights[4] * SampleInt(gray, rowStart, gw, basePos + 4)))
						/ FilterSum;

					int maskIndex = ((py * maskWidth) + px) * 3;
					data[maskIndex] = (byte)Math.Min(coverageRed, 255);
					data[maskIndex + 1] = (byte)Math.Min(coverageGreen, 255);
					data[maskIndex + 2] = (byte)Math.Min(coverageBlue, 255);
				}
			}

			return mask;
		}

		/// <summary>
		/// Box-averages each triple of subpixels in the 3x-wide gray buffer into one coverage value and
		/// writes it into all three channels of the packed mask. The 3x horizontal supersampling gives
		/// real horizontal AA; AGG's scanline coverage already supplies vertical AA - so the plain
		/// average is the correct downsample, no low-pass phase filter needed.
		/// </summary>
		public static LcdMask ApplyGrayCollapse(byte[] gray, int grayWidth, int maskWidth, int maskHeight)
		{
			ValidateGrayBuffer(gray, grayWidth, maskWidth, maskHeight);

			var mask = new LcdMask(maskWidth, maskHeight);
			byte[] data = mask.Data;
			for (int py = 0; py < maskHeight; py++)
			{
				int rowStart = py * grayWidth;
				int outRow = py * maskWidth * 3;
				for (int px = 0; px < maskWidth; px++)
				{
					int g = rowStart + (px * 3);

					// grayWidth == maskWidth * 3, so all three subpixels are in-bounds.
					byte coverage = (byte)((gray[g] + gray[g + 1] + gray[g + 2] + 1) / 3);
					int o = outRow + (px * 3);
					data[o] = coverage;
					data[o + 1] = coverage;
					data[o + 2] = coverage;
				}
			}

			return mask;
		}

		/// <summary>
		/// Every filter walks the gray buffer as <c>maskHeight</c> rows of stride <c>grayWidth</c>, so a
		/// short buffer would otherwise surface as a bare <see cref="IndexOutOfRangeException"/> from deep
		/// inside the sampling loop. Fail eagerly with the actual sizes instead.
		/// </summary>
		/// <remarks>
		/// A row has to be at least <c>maskWidth * 3</c> subpixels wide, because that is the mask's own
		/// footprint. Longer rows are accepted - the 5-tap filter's outer taps then read real bytes where
		/// they would otherwise read an implicit 0 - but nothing in production relies on that: the raster
		/// stage always allocates exactly <c>maskWidth * 3</c> (the Rust reference has no notion of a wider
		/// or narrower gray buffer at all). What makes a bbox-sized mask match a full-buffer one is
		/// <see cref="BoundedMaskBuilder"/>'s 2 mask-pixel pad on every side, which guarantees those outer
		/// taps land on genuinely zero coverage either way.
		/// <para>
		/// Anything <i>shorter</i> is a bug that would silently read the next row's coverage in
		/// <see cref="ApplyGrayCollapse"/> - the 5-tap paths bounds-check every tap, so they would instead
		/// quietly drop the right-hand pixels. Both are worth failing loudly for.
		/// </para>
		/// </remarks>
		private static void ValidateGrayBuffer(byte[] gray, int grayWidth, int maskWidth, int maskHeight)
		{
			if (gray == null)
			{
				throw new ArgumentNullException(nameof(gray));
			}

			if (grayWidth < (long)maskWidth * 3)
			{
				throw new ArgumentException(
					$"grayWidth must be at least maskWidth * 3 = {(long)maskWidth * 3}, was {grayWidth}.",
					nameof(grayWidth));
			}

			long required = (long)grayWidth * maskHeight;
			if (gray.Length < required)
			{
				throw new ArgumentException(
					$"gray must hold at least grayWidth * maskHeight = {grayWidth} * {maskHeight} = {required} bytes, was {gray.Length}.",
					nameof(gray));
			}
		}

		/// <summary>
		/// Tap weights for the 5-tap LCD filter, pre-normalised so the five samples always sum to 1.0.
		/// Parameterized on <paramref name="primaryWeight"/>: the middle tap carries
		/// <c>primaryWeight * 9</c> units, the two shoulder taps 2 each, the two outer taps 1 each - a
		/// direct analogue of the AGG <c>LcdDistributionLut(primary, 2/9, 1/9)</c> construction. At the
		/// default 1/3 the output is identical (up to rounding) to the legacy integer
		/// <c>[1, 2, 3, 2, 1] / 9</c> filter.
		/// </summary>
		private static double[] LcdFilterWeights(double primaryWeight)
		{
			double primaryUnits = primaryWeight * 9.0;
			double[] weights = new double[] { 1.0, 2.0, primaryUnits, 2.0, 1.0 };
			double sum = Math.Max(weights[0] + weights[1] + weights[2] + weights[3] + weights[4], 1e-9);
			return new double[]
			{
				weights[0] / sum,
				weights[1] / sum,
				weights[2] / sum,
				weights[3] / sum,
				weights[4] / sum,
			};
		}

		/// <summary>
		/// Reads a subpixel of the current gray row. Out-of-range reads return 0 - the mask bbox is
		/// padded 2 pixels, which makes that the correct neighborhood (no edge clamping).
		/// </summary>
		private static double Sample(byte[] gray, int rowStart, int grayWidth, int position)
		{
			if (position < 0 || position >= grayWidth)
			{
				return 0.0;
			}

			return gray[rowStart + position];
		}

		/// <summary>
		/// Integer twin of <see cref="Sample"/>; out-of-range reads return 0.
		/// </summary>
		private static int SampleInt(byte[] gray, int rowStart, int grayWidth, int position)
		{
			if (position < 0 || position >= grayWidth)
			{
				return 0;
			}

			return gray[rowStart + position];
		}

		private static double ApplyGamma(double coverage, bool needGamma, double invGamma)
		{
			if (!needGamma)
			{
				return coverage;
			}

			double t = Math.Clamp(coverage / 255.0, 0.0, 1.0);
			return Math.Pow(t, invGamma) * 255.0;
		}

		/// <summary>
		/// Rust's <c>f64::round</c> rounds halves away from zero; C#'s default
		/// <see cref="Math.Round(double)"/> rounds halves to even, so the mode has to be spelled out for
		/// byte-exactness.
		/// </summary>
		private static byte RoundAndClamp(double coverage)
		{
			return (byte)Math.Clamp(Math.Round(coverage, MidpointRounding.AwayFromZero), 0.0, 255.0);
		}
	}
}
