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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.LcdCoverage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="LcdFilter"/>: the second stage of the LCD pipeline, which collapses the
	/// 3x-horizontally-supersampled gray coverage buffer into a packed 3-byte-per-pixel
	/// <see cref="LcdMask"/>. Every expected value here is hand-computed from the Rust reference
	/// (<c>agg-gui\src\lcd_coverage\filter.rs</c>) so a divergence in the port shows up as a byte
	/// mismatch rather than a subtle rendering difference.
	/// </summary>
	public class LcdCoverageTests
	{
		/// <summary>
		/// A hand-computed ramp through the integer path. Weights are [1,2,3,2,1] with a truncating
		/// divide by 9, and the three channels sample different windows, so a linear ramp lands on
		/// exact bytes for every channel including the truncated ones at the right edge
		/// (860/9 = 95.5 -&gt; 95, 680/9 = 75.5 -&gt; 75).
		/// </summary>
		[Test]
		public async Task IntegerPathMatchesHandComputedBytes()
		{
			byte[] gray = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };

			LcdMask mask = LcdFilter.Apply5TapFilterLegacy(gray, 12, 4, 1);

			byte[] expected = new byte[]
			{
				11, 20, 30,   // px 0: R (3*10+2*20+30)/9=11, G 180/9, B 270/9
				40, 50, 60,   // px 1: 360/9, 450/9, 540/9
				70, 80, 90,   // px 2: 630/9, 720/9, 810/9
				100, 95, 75,  // px 3: 900/9, 860/9 truncated, 680/9 truncated
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// The reason the integer path is the default (plan section 1, stage 2): a uniform mid-gray
		/// 128 weighted by [1,2,3,2,1] sums to 1152, which is a clean 128 after the integer divide by
		/// 9. The float formulation of the same sum lands at 127.999... and any implementation that
		/// truncates instead of rounding drops it to 127 - sub-LSB drift that accumulates into a
		/// visible fade across a paragraph. Interior pixels (all five taps in bounds) must read 128.
		/// </summary>
		[Test]
		public async Task IntegerPathKeepsMidGrayExact()
		{
			byte[] gray = Enumerable.Repeat((byte)128, 12).ToArray();

			LcdMask mask = LcdFilter.Apply5TapFilterLegacy(gray, 12, 4, 1);

			for (int px = 1; px <= 2; px++)
			{
				mask.GetPixel(px, 0, out byte red, out byte green, out byte blue);
				await Assert.That(red).IsEqualTo((byte)128);
				await Assert.That(green).IsEqualTo((byte)128);
				await Assert.That(blue).IsEqualTo((byte)128);
			}
		}

		/// <summary>
		/// The defining property of the filter: each channel is centered on its own physical subpixel,
		/// so a single lit subpixel spreads asymmetrically across R/G/B. The impulse sits at subpixel 7
		/// (the green subpixel of pixel 2); R samples base+[-2..2], G base+[-1..3], B base+[0..4], so
		/// pixel 1 sees the impulse only in blue and pixel 3 only in red.
		/// </summary>
		[Test]
		public async Task ImpulseSpreadsWithPerChannelPhaseOffsets()
		{
			byte[] gray = new byte[15];
			gray[7] = 255;

			LcdMask mask = LcdFilter.Apply5TapFilterLegacy(gray, 15, 5, 1);

			byte[] expected = new byte[]
			{
				0, 0, 0,      // px 0: impulse is outside every window
				0, 0, 28,     // px 1: blue's +4 tap only, 255/9
				56, 85, 56,   // px 2: weights 2, 3, 2 -> 510/9, 765/9, 510/9
				28, 0, 0,     // px 3: red's -2 tap only
				0, 0, 0,      // px 4
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// Out-of-range taps read 0, never a clamped edge sample (the mask bbox is padded 2 pixels,
		/// which makes reading 0 the correct neighborhood). With the impulse at subpixel 0, red's -2
		/// and -1 taps are out of bounds: reading 0 gives 3*255/9 = 85, whereas clamping to gray[0]
		/// would give (1+2+3)*255/9 = 170.
		/// </summary>
		[Test]
		public async Task OutOfRangeTapsReadZeroRatherThanClamping()
		{
			byte[] gray = new byte[6];
			gray[0] = 255;

			LcdMask mask = LcdFilter.Apply5TapFilterLegacy(gray, 6, 2, 1);

			byte[] expected = new byte[]
			{
				85, 56, 28,   // px 0: weights 3, 2, 1 on the single in-range tap
				0, 0, 0,      // px 1: impulse is three subpixels to the left of every window
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// The gray sibling box-averages each triple with <c>(r + g + b + 1) / 3</c> and replicates it,
		/// so the mask carries no chroma at all - that is what lets it composite through the same
		/// per-channel path with no fringing.
		/// <para>
		/// Note that neither path can actually saturate: the all-255 triple tops out at exactly 255
		/// (766 / 3 = 255 after the truncating divide), and in the legacy 5-tap path the
		/// <c>Math.Min(..., 255)</c> is likewise unreachable because the weights sum to the divisor
		/// (255 * 9 / 9 = 255). Both clamps are belt-and-braces, mirroring the Rust reference.
		/// </para>
		/// </summary>
		[Test]
		public async Task GrayCollapseAveragesTriplesWithNoChroma()
		{
			byte[] gray = new byte[]
			{
				0, 0, 0,          // 1/3 -> 0
				1, 1, 1,          // 4/3 -> 1
				10, 20, 31,       // 62/3 -> 20
				255, 255, 255,    // 766/3 -> 255, the maximum
			};

			LcdMask mask = LcdFilter.ApplyGrayCollapse(gray, 12, 4, 1);

			byte[] expected = new byte[]
			{
				0, 0, 0,
				1, 1, 1,
				20, 20, 20,
				255, 255, 255,
			};
			await AssertMaskBytes(expected, mask);

			for (int i = 0; i < mask.Data.Length; i += 3)
			{
				await Assert.That(mask.Data[i]).IsEqualTo(mask.Data[i + 1]);
				await Assert.That(mask.Data[i + 1]).IsEqualTo(mask.Data[i + 2]);
			}
		}

		/// <summary>
		/// Pins row addressing for the legacy path: rows advance by <c>grayWidth</c> (not by
		/// <c>maskWidth</c>, and not <c>maskWidth * 3</c>), and mask row 0 comes from gray row 0.
		/// <para>
		/// <c>grayWidth</c> is 8 while <c>maskWidth * 3</c> is only 6, so the last two subpixels of each
		/// row are bbox padding that only the right-hand pixel's outer taps reach - an implementation
		/// that assumed <c>grayWidth == maskWidth * 3</c> would both mis-stride row 1 and drop those
		/// taps. The two rows are reverses of each other, so swapping the row order fails too.
		/// </para>
		/// </summary>
		[Test]
		public async Task LegacyPathWalksRowsByGrayStride()
		{
			byte[] gray = new byte[]
			{
				9, 18, 27, 36, 45, 54, 63, 72,   // row 0 (bottom): ascending ramp
				72, 63, 54, 45, 36, 27, 18, 9,   // row 1: the same ramp descending
			};

			LcdMask mask = LcdFilter.Apply5TapFilterLegacy(gray, 8, 2, 2);

			byte[] expected = new byte[]
			{
				// row 0: R (3*9+2*18+27)/9=10, G (2*9+3*18+2*27+36)/9=18, B (9+2*18+3*27+2*36+45)/9=27
				10, 18, 27,
				// row 0 px 1: 324/9=36, 405/9=45, B reaches the padding at 6 and 7 -> 486/9=54
				36, 45, 54,
				// row 1: 396/9=44, 486/9=54, 486/9=54
				44, 54, 54,
				// row 1 px 1: 405/9=45, 324/9=36, 243/9=27
				45, 36, 27,
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// The same row-addressing contract for the gray collapse path, which indexes the buffer
		/// independently of the 5-tap filter. <c>grayWidth</c> is 7 against a <c>maskWidth * 3</c> of 6,
		/// so the trailing padding byte (99) must be skipped rather than folded into row 1's first
		/// triple, and the two rows are far enough apart that reversing them cannot pass.
		/// </summary>
		[Test]
		public async Task GrayCollapseWalksRowsByGrayStride()
		{
			byte[] gray = new byte[]
			{
				10, 20, 30, 40, 50, 60, 99,   // row 0 (bottom), trailing 99 is padding
				0, 0, 1, 255, 255, 255, 99,   // row 1
			};

			LcdMask mask = LcdFilter.ApplyGrayCollapse(gray, 7, 2, 2);

			byte[] expected = new byte[]
			{
				20, 20, 20,       // row 0 px 0: 61/3 -> 20
				50, 50, 50,       // row 0 px 1: 151/3 -> 50
				0, 0, 0,          // row 1 px 0: 2/3 -> 0
				255, 255, 255,    // row 1 px 1: 766/3 -> 255
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// The float path must round halves away from zero (Rust's <c>f64::round</c>), not to even like
		/// C#'s default <see cref="System.Math.Round(double)"/>. A primary weight of 2/9 makes the
		/// normalized taps [1,2,2,2,1]/8, so an isolated sample of 20 under an outer (1/8) tap lands on
		/// exactly 2.5: away-from-zero gives 3, to-even would give 2 (all three values are exact in
		/// binary floating point, so the midpoint is real, not a near-miss).
		/// </summary>
		[Test]
		public async Task FloatPathRoundsHalvesAwayFromZero()
		{
			byte[] gray = new byte[9];
			gray[4] = 20;

			LcdMask mask = LcdFilter.Apply5TapFilter(gray, 9, 3, 1, primaryWeight: 2.0 / 9.0);

			byte[] expected = new byte[]
			{
				// px 0: only blue's +4 tap (1/8) sees the sample -> 20/8 = 2.5 -> 3
				0, 0, 3,
				// px 1: every channel catches it on a 2/8 tap -> 40/8 = 5, no midpoint
				5, 5, 5,
				// px 2: only red's -2 tap (1/8) sees it -> 2.5 -> 3
				3, 0, 0,
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// The dispatch in <see cref="LcdFilter.Apply5TapFilter"/> must route the default parameters
		/// (primary 1/3, gamma 1) to the byte-exact integer path, and must actually be load bearing:
		/// nudging the primary weight past the 1e-6 epsilon takes the float path, whose rounding turns
		/// the truncated 860/9 = 95.5 into 96.
		/// </summary>
		[Test]
		public async Task DefaultParametersDispatchToTheIntegerPath()
		{
			byte[] gray = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };

			LcdMask legacy = LcdFilter.Apply5TapFilterLegacy(gray, 12, 4, 1);
			LcdMask defaulted = LcdFilter.Apply5TapFilter(gray, 12, 4, 1);
			await AssertMaskBytes(legacy.Data, defaulted);

			// Just outside the epsilon: same kernel to five decimal places, different rounding rule.
			LcdMask parameterized = LcdFilter.Apply5TapFilter(gray, 12, 4, 1, primaryWeight: (1.0 / 3.0) + 1e-5);
			parameterized.GetPixel(3, 0, out byte _, out byte green, out byte _);
			await Assert.That(green).IsEqualTo((byte)96);
			await Assert.That(legacy.Data[(3 * 3) + 1]).IsEqualTo((byte)95);
		}

		/// <summary>
		/// Gamma is applied to the per-channel coverage AFTER the filter sum, not to the input samples.
		/// With a lone 255 subpixel, red at pixel 2 filters to 2/9 * 255 = 56.67; the gamma-2 curve
		/// lifts that to sqrt(56.67/255) * 255 = 120. Gamma applied to the samples first would leave
		/// 255 untouched and yield 57, so this pins the ordering.
		/// </summary>
		[Test]
		public async Task GammaIsAppliedAfterTheFilterSum()
		{
			byte[] gray = new byte[15];
			gray[7] = 255;

			LcdMask mask = LcdFilter.Apply5TapFilter(gray, 15, 5, 1, gamma: 2.0);

			mask.GetPixel(2, 0, out byte red, out byte green, out byte _);
			await Assert.That(red).IsEqualTo((byte)120);

			// Green's 3/9 tap: 85 -> sqrt(85/255) * 255 = 147.
			await Assert.That(green).IsEqualTo((byte)147);
		}

		/// <summary>
		/// A gray row narrower than the mask's own <c>maskWidth * 3</c> footprint is a caller bug, and one
		/// that fails quietly: the gray collapse would fold the next row's coverage into the right-hand
		/// pixels, while the bounds-checked 5-tap paths would just drop them. All three entry points must
		/// reject it up front instead. (Rows <i>longer</i> than the footprint are legal - the rest of these
		/// tests use them.)
		/// </summary>
		[Test]
		public async Task GrayRowNarrowerThanTheMaskFootprintIsRejected()
		{
			byte[] gray = new byte[12];

			// grayWidth 5 against a maskWidth * 3 of 6.
			await Assert.That(() => LcdFilter.Apply5TapFilterLegacy(gray, 5, 2, 1)).Throws<ArgumentException>();
			await Assert.That(() => LcdFilter.Apply5TapFilter(gray, 5, 2, 1)).Throws<ArgumentException>();
			await Assert.That(() => LcdFilter.ApplyGrayCollapse(gray, 5, 2, 1)).Throws<ArgumentException>();

			// The parameterized path validates on its own, past the integer fast path's dispatch.
			await Assert.That(() => LcdFilter.Apply5TapFilter(gray, 5, 2, 1, primaryWeight: 2.0 / 9.0))
				.Throws<ArgumentException>();
		}

		/// <summary>
		/// The row stride can be wide enough and the buffer still too short to hold
		/// <c>grayWidth * maskHeight</c> bytes - caught eagerly with the actual sizes rather than as a bare
		/// <see cref="System.IndexOutOfRangeException"/> from inside the sampling loop.
		/// </summary>
		[Test]
		public async Task GrayBufferShorterThanAllItsRowsIsRejected()
		{
			// 6 * 2 = 12 bytes required, 11 supplied.
			await Assert.That(() => LcdFilter.Apply5TapFilterLegacy(new byte[11], 6, 2, 2)).Throws<ArgumentException>();
			await Assert.That(() => LcdFilter.ApplyGrayCollapse(new byte[11], 6, 2, 2)).Throws<ArgumentException>();
			await Assert.That(() => LcdFilter.Apply5TapFilterLegacy(null, 6, 2, 2)).Throws<ArgumentNullException>();
		}

		private static async Task AssertMaskBytes(byte[] expected, LcdMask mask)
		{
			await Assert.That(mask.Data.Length).IsEqualTo(expected.Length);
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(mask.Data[i])
					.IsEqualTo(expected[i])
					.Because($"mask byte {i} (pixel {i / 3}, channel {"RGB"[i % 3]})");
			}
		}
	}
}
