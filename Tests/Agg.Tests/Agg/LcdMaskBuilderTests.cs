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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="LcdMaskBuilder"/> and <see cref="BoundedMaskBuilder"/>: the first stage of the
	/// LCD pipeline, which rasterizes vector paths into a 3x-horizontally-supersampled gray coverage
	/// buffer and then hands that buffer to <see cref="LcdFilter"/>.
	/// </summary>
	public class LcdMaskBuilderTests
	{
		/// <summary>
		/// The raster contract, hand-verifiable end to end: only X is supersampled, so an axis-aligned
		/// rectangle on integer pixel boundaries lands on exact subpixel boundaries (x 1..3 becomes
		/// subpixels 3..8) and covers exactly one gray row. Full coverage writes 255 exactly - the AGG
		/// span for a fully covered run takes the copy path, so no blend rounding is involved.
		/// <para>
		/// A vertical scale bug (multiplying sy or ty by 3 as well as sx/shx/tx) would move the covered
		/// row; a stride bug would smear it across rows.
		/// </para>
		/// </summary>
		[Test]
		public async Task IntegerRectangleFillsWholeSubpixelColumnsOnOneRow()
		{
			var builder = new LcdMaskBuilder(4, 3);

			builder.AddPath(Affine.NewIdentity(), Rectangle(1, 1, 3, 2));

			await Assert.That(builder.GrayWidth).IsEqualTo(12);
			await Assert.That(builder.GrayHeight).IsEqualTo(3);
			await AssertGrayRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, builder, 0);
			await AssertGrayRow(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255, 255, 0, 0, 0 }, builder, 1);
			await AssertGrayRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, builder, 2);
		}

		/// <summary>
		/// The same rectangle through the filter. Every byte is the hand-computed [1,2,3,2,1]/9 sum over
		/// the six lit subpixels, and the result is symmetric about the rectangle's center because the
		/// rectangle is symmetric: the left edge fades in through B, G, R and the right edge fades out
		/// through R, G, B. That asymmetry <i>within</i> a pixel is the whole point of the LCD path.
		/// </summary>
		[Test]
		public async Task IntegerRectangleFiltersToAPerChannelEdgeRamp()
		{
			var builder = new LcdMaskBuilder(4, 3);
			builder.AddPath(Affine.NewIdentity(), Rectangle(1, 1, 3, 2));

			LcdMask mask = builder.FinalizeMask();

			await Assert.That(mask.Width).IsEqualTo(4);
			await Assert.That(mask.Height).IsEqualTo(3);
			byte[] expected = new byte[]
			{
				// row 0 - nothing was rasterized here
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,

				// row 1: px 0 catches the left edge only through G (255/9) and B (765/9)
				0, 28, 85,
				// px 1: R sees 6 of 9 weight units, G 8 (2040/9 truncates to 226), B all 9
				170, 226, 255,
				// px 2: the mirror of px 1
				255, 226, 170,
				// px 3: the mirror of px 0
				85, 28, 0,

				// row 2
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			};
			await AssertMaskBytes(expected, mask);
		}

		/// <summary>
		/// Pins the blend rounding, hand computed. A rectangle from x 1.0 to x 1.5 covers subpixel 3 whole
		/// and exactly half of subpixel 4, over a full row height. AGG's cell area for a half-covered cell
		/// is half of a full one, so the rasterizer hands the blender cover 128, and blending white over the
		/// zeroed buffer at alpha 128 must land on 128.
		/// <para>
		/// This is the case that forced <see cref="BlenderGrayExact"/>: the stock
		/// <see cref="MatterHackers.Agg.Image.blender_gray"/> lerps with a plain <c>&gt;&gt; 8</c>, giving
		/// <c>(255 * 128) &gt;&gt; 8 == 127</c>, one low - and one low on every anti-aliased subpixel, which
		/// the Rust reference's rounding lerp does not do.
		/// </para>
		/// </summary>
		[Test]
		public async Task HalfCoveredSubpixelRoundsToExactlyHalf()
		{
			var builder = new LcdMaskBuilder(4, 1);

			builder.AddPath(Affine.NewIdentity(), Rectangle(1, 0, 1.5, 1));

			await AssertGrayRow(new byte[] { 0, 0, 0, 255, 128, 0, 0, 0, 0, 0, 0, 0 }, builder, 0);
		}

		/// <summary>
		/// The rounding lerp itself, on the cases the raster path above cannot reach. Coverage only ever
		/// blends white up from a zeroed buffer, so the downward direction (<c>p &gt; q</c>, where AGG
		/// subtracts one from the bias) has no other guard, and it is the part of the port most likely to be
		/// transcribed wrong. Both endpoints must be hit exactly in both directions, and half must be half.
		/// </summary>
		[Test]
		public async Task ExactBlenderLerpReachesBothEndpointsAndRoundsHalfUp()
		{
			await Assert.That(BlenderGrayExact.Lerp(0, 255, 0)).IsEqualTo((byte)0);
			await Assert.That(BlenderGrayExact.Lerp(0, 255, 128)).IsEqualTo((byte)128);
			await Assert.That(BlenderGrayExact.Lerp(0, 255, 255)).IsEqualTo((byte)255);

			// Downward: without the "- (p > q)" term this misses 0.
			await Assert.That(BlenderGrayExact.Lerp(255, 0, 255)).IsEqualTo((byte)0);
			await Assert.That(BlenderGrayExact.Lerp(255, 0, 128)).IsEqualTo((byte)127);
			await Assert.That(BlenderGrayExact.Lerp(255, 0, 0)).IsEqualTo((byte)255);

			// Multiply's defining property: scaling by full opacity is the identity, at every cover.
			for (int cover = 0; cover <= 255; cover++)
			{
				await Assert.That(BlenderGrayExact.Multiply(255, cover)).IsEqualTo((byte)cover);
			}
		}

		/// <summary>
		/// Multiple paths share one gray buffer: the rasterizer is reset per path but the buffer is not,
		/// so a second path adds coverage instead of replacing it. This is what lets overlapping glyphs
		/// (or any batch of paths) rasterize as one non-zero fill. An implementation that re-allocated or
		/// cleared per path would lose the first rectangle's left half.
		/// </summary>
		[Test]
		public async Task SecondPathAccumulatesIntoTheSameGrayBuffer()
		{
			var builder = new LcdMaskBuilder(4, 1);

			builder.AddPath(Affine.NewIdentity(), Rectangle(0, 0, 2, 1));
			byte[] afterFirstPath = builder.GrayBuffer.ToArray();

			builder.AddPath(Affine.NewIdentity(), Rectangle(1, 0, 3, 1));

			// x 0..2 -> subpixels 0..5.
			await AssertGrayRow(new byte[] { 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0 }, afterFirstPath, 12, 0);

			// The second rectangle covers x 1..3 -> subpixels 3..8; the union is subpixels 0..8.
			await AssertGrayRow(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0 }, builder, 0);
		}

		/// <summary>
		/// The clip rect is given in mask pixels and has to be mapped onto the 3x-wide gray grid: X bounds
		/// multiply by 3, and because the AGG clip box is <b>inclusive</b> on both ends the right edge is
		/// <c>ceil(right) * 3 - 1</c>. So a clip whose right edge falls at 2.5 keeps subpixel 8 (the last
		/// subpixel of pixel 2) and drops subpixel 9 - dropping the <c>- 1</c> would leak a subpixel of
		/// coverage into pixel 3, and forgetting the ceil would drop all of pixel 2.
		/// </summary>
		[Test]
		public async Task ClipRectDropsCoverageOutsideItOnSubpixelBoundaries()
		{
			var builder = new LcdMaskBuilder(4, 1, new RectangleDouble(1, 0, 2.5, 1));

			builder.AddPath(Affine.NewIdentity(), Rectangle(0, 0, 4, 1));

			await AssertGrayRow(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255, 255, 0, 0, 0 }, builder, 0);

			// Spelled out because this pair is the whole point of the inclusive-box conversion.
			await Assert.That(builder.GrayBuffer[8]).IsEqualTo((byte)255);
			await Assert.That(builder.GrayBuffer[9]).IsEqualTo((byte)0);
		}

		/// <summary>
		/// Geometry entirely outside the clip contributes nothing at all, so the filter has nothing to
		/// spread and the mask is empty.
		/// </summary>
		[Test]
		public async Task GeometryFullyOutsideTheClipProducesNoCoverage()
		{
			var builder = new LcdMaskBuilder(6, 2, new RectangleDouble(0, 0, 2, 2));

			builder.AddPath(Affine.NewIdentity(), Rectangle(3, 0, 5, 2));

			await Assert.That(builder.GrayBuffer.All(b => b == 0)).IsTrue();
			await Assert.That(builder.FinalizeMask().Data.All(b => b == 0)).IsTrue();
		}

		/// <summary>
		/// The bounded-mask equivalence invariant (plan section 2, ported from the reference's
		/// <c>fill_path_bbox_tests</c>): the mask origin is whole pixels, so shifting a path into
		/// mask-local space moves it by a multiple of 3 subpixels, and the filter kernel is
		/// translation-invariant at that granularity. The same path filled at two different integer
		/// offsets therefore produces byte-identical masks, only placed differently. Fractional path
		/// coordinates are used deliberately so anti-aliased edge bytes take part in the comparison.
		/// </summary>
		[Test]
		public async Task BoundedMaskIsIdenticalAtDifferentIntegerOffsets()
		{
			VertexStorage triangle = FractionalTriangle();

			bool builtAtOrigin = BoundedMaskBuilder.TryBuild(
				64, 64, triangle, Affine.NewIdentity(), out LcdMask atOrigin, out int originX, out int originY);
			bool builtShifted = BoundedMaskBuilder.TryBuild(
				64, 64, triangle, Affine.NewTranslation(7, 5), out LcdMask shifted, out int shiftedX, out int shiftedY);

			await Assert.That(builtAtOrigin).IsTrue();
			await Assert.That(builtShifted).IsTrue();

			// Placement moves by exactly the translation; the bbox is padded 2px on every side.
			await Assert.That(shiftedX).IsEqualTo(originX + 7);
			await Assert.That(shiftedY).IsEqualTo(originY + 5);
			await Assert.That(originX).IsEqualTo(8);
			await Assert.That(originY).IsEqualTo(8);

			await Assert.That(shifted.Width).IsEqualTo(atOrigin.Width);
			await Assert.That(shifted.Height).IsEqualTo(atOrigin.Height);

			// Guard against a vacuous pass: the masks must actually carry coverage.
			await Assert.That(atOrigin.Data.Any(b => b > 0)).IsTrue();
			await AssertMaskBytes(atOrigin.Data, shifted);
		}

		/// <summary>
		/// A rotated transform, where <c>shx</c> and <c>shy</c> are both non-zero, must reach the raster
		/// exactly as if the caller had baked the rotation into the path vertices instead. Only X is
		/// supersampled, so <see cref="LcdMaskBuilder.AddPath"/> triples <c>sx, shx, tx</c> and leaves
		/// <c>shy, sy, ty</c> alone - i.e. it post-multiplies by a 3x1 scale. Tripling the wrong row
		/// (<c>shy, sy, ty</c>) is indistinguishable from the right one for a pure translation or a pure
		/// axis-aligned scale, which is all the other tests here use; under a rotation it shears the shape
		/// the other way and this comparison fails outright.
		/// </summary>
		[Test]
		public async Task RotatedTransformMatchesTheSameRotationBakedIntoThePath()
		{
			// 30 degrees about (8, 6) - off both axes so tx and ty are non-zero too.
			Affine rotation = Affine.NewTranslation(-8, -6)
				* Affine.NewRotation(MathHelper.DegreesToRadians(30))
				* Affine.NewTranslation(8, 6);

			// Guard the premise: this test is only meaningful while all four linear terms are populated,
			// and a rotation's two shear terms are equal and opposite (a transposed matrix would not be).
			await Assert.That(Math.Abs(rotation.shx) > 0.4).IsTrue();
			await Assert.That(rotation.shy).IsEqualTo(-rotation.shx).Within(1e-12);

			VertexStorage rect = Rectangle(4.25, 3.5, 11.75, 8.5);

			var viaTransform = new LcdMaskBuilder(16, 12);
			viaTransform.AddPath(rotation, rect);

			// Ground truth: the rotation applied to the vertices, then the identity handed to AddPath (whose
			// supersample then degenerates to a plain x * 3).
			var viaBakedVertices = new LcdMaskBuilder(16, 12);
			viaBakedVertices.AddPath(Affine.NewIdentity(), new VertexSourceApplyTransform(rect, rotation));

			LcdMask rotated = viaTransform.FinalizeMask();
			await AssertMaskBytes(viaBakedVertices.FinalizeMask().Data, rotated);

			// Non-vacuity: the rotation has to actually have moved coverage around, or two empty (or two
			// identical axis-aligned) masks would pass.
			var unrotated = new LcdMaskBuilder(16, 12);
			unrotated.AddPath(Affine.NewIdentity(), rect);
			await Assert.That(rotated.Data.Any(b => b > 0)).IsTrue();
			await Assert.That(rotated.Data.SequenceEqual(unrotated.FinalizeMask().Data)).IsFalse();
		}

		/// <summary>
		/// Nothing to paint when the padded bbox misses the destination entirely - the caller draws
		/// nothing rather than compositing an all-zero mask.
		/// </summary>
		[Test]
		public async Task BoundedMaskReportsNothingToPaintWhenFullyOffBuffer()
		{
			bool built = BoundedMaskBuilder.TryBuild(
				64, 64, Rectangle(-50, -50, -40, -40), Affine.NewIdentity(), out LcdMask mask, out int _, out int _);

			await Assert.That(built).IsFalse();
			await Assert.That(mask).IsNull();
		}

		/// <summary>
		/// A clip rect using <see cref="double.MaxValue"/> as an "unbounded" sentinel has to behave exactly
		/// as no clip at all. The bbox/clip intersection turns doubles into pixel indices, and the whole
		/// region collapses if any of those conversions sends a huge bound the wrong way: <c>x2</c> as the
		/// most negative int inverts the box and makes <c>TryBuild</c> report nothing to paint at all. All of
		/// them saturate (see <c>SaturatingMath</c>), so the sentinel simply never narrows anything.
		/// </summary>
		[Test]
		public async Task BoundedMaskTreatsAnUnboundedClipSentinelAsUnclipped()
		{
			VertexStorage triangle = FractionalTriangle();
			var unbounded = new RectangleDouble(double.MinValue, double.MinValue, double.MaxValue, double.MaxValue);

			bool builtPlain = BoundedMaskBuilder.TryBuild(
				64, 64, triangle, Affine.NewIdentity(), out LcdMask plain, out int plainX, out int plainY);
			bool builtSentinel = BoundedMaskBuilder.TryBuild(
				64, 64, triangle, Affine.NewIdentity(), out LcdMask sentinel, out int sentinelX, out int sentinelY, unbounded);

			await Assert.That(builtPlain).IsTrue();
			await Assert.That(builtSentinel).IsTrue();
			await Assert.That(sentinelX).IsEqualTo(plainX);
			await Assert.That(sentinelY).IsEqualTo(plainY);

			// Guard against a vacuous pass, then demand byte equality with the unclipped mask.
			await Assert.That(plain.Data.Any(b => b > 0)).IsTrue();
			await AssertMaskBytes(plain.Data, sentinel);
		}

		/// <summary>
		/// The conversion every bbox and clip bound in the LCD pipeline goes through, pinned directly: an
		/// out-of-range bound must clamp to the nearest <see cref="int"/> limit, matching Rust's
		/// <c>as i32</c>. Getting the huge-positive direction wrong is what would invert a region instead of
		/// widening it.
		/// <para>
		/// This is also the only coverage the <b>path bbox</b> conversions in
		/// <see cref="BoundedMaskBuilder"/> can get: a coordinate big enough to overflow an int (over ~2.1e9)
		/// is far outside the +/-8.4e6 that AGG's 1/256-pixel <see cref="int"/> rasterizer can represent, so
		/// such a path cannot survive <see cref="BoundedMaskBuilder.TryBuild"/>'s raster stage to be compared
		/// against anything. See <see cref="BoundedMaskTreatsAnUnboundedClipSentinelAsUnclipped"/> for the
		/// clip-side equivalent, which does run end to end.
		/// </para>
		/// </summary>
		[Test]
		public async Task OutOfRangeBoundsSaturateRatherThanInverting()
		{
			// The platform assumption this helper deliberately does not rely on: since .NET Core 3.0 a bare
			// double-to-int cast saturates by itself (it was unspecified before, and int.MinValue in both
			// directions on x86 .NET Framework). Held in a local because the compiler will not fold an
			// out-of-range constant conversion at all.
			double huge = 1e12;
			await Assert.That((int)huge).IsEqualTo(int.MaxValue);

			await Assert.That(SaturatingMath.Ceiling(huge)).IsEqualTo(int.MaxValue);
			await Assert.That(SaturatingMath.Ceiling(double.MaxValue)).IsEqualTo(int.MaxValue);
			await Assert.That(SaturatingMath.Ceiling(double.PositiveInfinity)).IsEqualTo(int.MaxValue);
			await Assert.That(SaturatingMath.Floor(-1e12)).IsEqualTo(int.MinValue);
			await Assert.That(SaturatingMath.Floor(double.MinValue)).IsEqualTo(int.MinValue);
			await Assert.That(SaturatingMath.Floor(double.NegativeInfinity)).IsEqualTo(int.MinValue);

			// In-range values still floor and ceil normally, including across zero.
			await Assert.That(SaturatingMath.Floor(2.5)).IsEqualTo(2);
			await Assert.That(SaturatingMath.Ceiling(2.5)).IsEqualTo(3);
			await Assert.That(SaturatingMath.Floor(-2.5)).IsEqualTo(-3);
			await Assert.That(SaturatingMath.Ceiling(-2.5)).IsEqualTo(-2);

			// NaN has no meaningful pixel index; 0 leaves the caller's box empty rather than inverted.
			await Assert.That(SaturatingMath.Floor(double.NaN)).IsEqualTo(0);
			await Assert.That(SaturatingMath.Ceiling(double.NaN)).IsEqualTo(0);

			// The clip conversion's saturating * 3 (the reference's saturating_mul(3)).
			await Assert.That(SaturatingMath.MultiplyBy3(5)).IsEqualTo(15);
			await Assert.That(SaturatingMath.MultiplyBy3(int.MaxValue)).IsEqualTo(int.MaxValue);
			await Assert.That(SaturatingMath.MultiplyBy3(int.MinValue)).IsEqualTo(int.MinValue);
		}

		/// <summary>
		/// The degenerate early returns, all of which mean "the caller paints nothing": a destination with
		/// no area at all, and a path carrying no coordinate-bearing vertices (so there is no bbox to size
		/// a mask from). None of these may hand back a mask.
		/// </summary>
		[Test]
		public async Task BoundedMaskReportsNothingToPaintForDegenerateInput()
		{
			VertexStorage square = Rectangle(1, 1, 3, 3);

			bool zeroWidth = BoundedMaskBuilder.TryBuild(
				0, 64, square, Affine.NewIdentity(), out LcdMask noWidthMask, out int _, out int _);
			bool zeroHeight = BoundedMaskBuilder.TryBuild(
				64, 0, square, Affine.NewIdentity(), out LcdMask noHeightMask, out int _, out int _);
			bool emptyPath = BoundedMaskBuilder.TryBuild(
				64, 64, new VertexStorage(), Affine.NewIdentity(), out LcdMask emptyPathMask, out int _, out int _);

			await Assert.That(zeroWidth).IsFalse();
			await Assert.That(noWidthMask).IsNull();
			await Assert.That(zeroHeight).IsFalse();
			await Assert.That(noHeightMask).IsNull();
			await Assert.That(emptyPath).IsFalse();
			await Assert.That(emptyPathMask).IsNull();
		}

		/// <summary>
		/// The gray sibling is the same raster in the same layout, only collapsed without phase offsets:
		/// same dimensions, r == g == b everywhere, which is exactly what makes it safe to composite
		/// through the identical per-channel path when LCD geometry is not valid. The LCD mask over the
		/// same gray buffer must by contrast show channel variation at the anti-aliased edge, or the
		/// subpixel stage is not doing anything.
		/// <para>
		/// Note the finalizers do not consume the builder (the Rust reference's do), so both can be taken
		/// from one raster - which is what makes this an apples-to-apples comparison.
		/// </para>
		/// </summary>
		[Test]
		public async Task GrayFinalizeMatchesLcdDimensionsAndCarriesNoChroma()
		{
			var builder = new LcdMaskBuilder(4, 1);
			builder.AddPath(Affine.NewIdentity(), Rectangle(1, 0, 3, 1));

			LcdMask lcd = builder.FinalizeMask();
			LcdMask gray = builder.FinalizeGray();

			await Assert.That(gray.Width).IsEqualTo(lcd.Width);
			await Assert.That(gray.Height).IsEqualTo(lcd.Height);
			await Assert.That(gray.Data.Length).IsEqualTo(lcd.Data.Length);

			for (int i = 0; i < gray.Data.Length; i += 3)
			{
				await Assert.That(gray.Data[i]).IsEqualTo(gray.Data[i + 1]);
				await Assert.That(gray.Data[i + 1]).IsEqualTo(gray.Data[i + 2]);
			}

			bool lcdVariesWithinAPixel = false;
			for (int i = 0; i < lcd.Data.Length; i += 3)
			{
				if (lcd.Data[i] != lcd.Data[i + 1] || lcd.Data[i + 1] != lcd.Data[i + 2])
				{
					lcdVariesWithinAPixel = true;
					break;
				}
			}

			await Assert.That(lcdVariesWithinAPixel).IsTrue();
		}

		/// <summary>
		/// The defining property of the LCD path, on a real anti-aliased path and at the reference's own
		/// thresholds (<c>test_lcd_mask_has_channel_variation</c>): some pixel must carry substantial coverage
		/// (max &gt; 20) with the channels clearly disagreeing (max - min &gt; 10). Without the 5-tap filter's
		/// per-channel phase offset the three channels would be identical at every pixel.
		/// <para>
		/// The gray sibling over the same raster is the control, covering the reference's
		/// <c>test_gray_mask_has_no_chroma</c> (r == g == b everywhere) and
		/// <c>test_gray_mask_is_antialiased</c> (partial-coverage bytes exist, so the edges are not simply
		/// on/off).
		/// </para>
		/// </summary>
		[Test]
		public async Task FractionalPathVariesPerChannelWhileTheGraySiblingDoesNot()
		{
			var builder = new LcdMaskBuilder(20, 20);
			builder.AddPath(Affine.NewIdentity(), FractionalTriangle());

			LcdMask lcd = builder.FinalizeMask();
			bool sawVariation = false;
			for (int i = 0; i < lcd.Data.Length; i += 3)
			{
				int max = Math.Max(lcd.Data[i], Math.Max(lcd.Data[i + 1], lcd.Data[i + 2]));
				int min = Math.Min(lcd.Data[i], Math.Min(lcd.Data[i + 1], lcd.Data[i + 2]));
				if (max > 20 && max - min > 10)
				{
					sawVariation = true;
					break;
				}
			}

			await Assert.That(sawVariation).IsTrue().Because("no per-channel variation at the path edges");

			LcdMask gray = builder.FinalizeGray();
			for (int i = 0; i < gray.Data.Length; i += 3)
			{
				await Assert.That(gray.Data[i]).IsEqualTo(gray.Data[i + 1]);
				await Assert.That(gray.Data[i + 1]).IsEqualTo(gray.Data[i + 2]);
			}

			await Assert.That(gray.Data.Any(b => b > 8 && b < 248)).IsTrue()
				.Because("the gray mask has no partial-coverage bytes - its edges are aliased");
		}

		/// <summary>
		/// The rasterizer's clip box is overflow protection only, so it may not change a single byte of any
		/// geometry that does not need it - and "does not need it" has to include geometry running well off
		/// the buffer, which is the normal case for a glyph or a widget rect. The comparison is against the
		/// same production pipeline with the box removed entirely (the Rust reference's behaviour), so a box
		/// pulled back onto the buffer fails this outright.
		/// <para>
		/// A shallow wedge is the shape that exposes it: both long edges cross the vertical buffer edges at a
		/// non-axis-aligned angle and away from any pixel boundary. AGG's clipper computes the crossing
		/// coordinate through <c>mul_div</c> -&gt; <c>iround</c> and <c>RasterizerCellsAa.line</c> re-seeds its
		/// integer interpolation from that rounded point, which walks the edge up to 1/256 px off along its
		/// <b>whole</b> length - so a clip at the buffer edge moves gray bytes at interior columns too, not
		/// just at the boundary.
		/// </para>
		/// </summary>
		[Test]
		public async Task GeometryCrossingTheBufferEdgeIsRasterizedAsIfUnclipped()
		{
			VertexStorage wedge = ShallowWedge();

			var clipped = new LcdMaskBuilder(8, 8);
			clipped.AddPath(Affine.NewIdentity(), wedge);

			LcdMaskBuilder unclipped = LcdMaskBuilder.CreateWithUnclippedRasterizer(8, 8);
			unclipped.AddPath(Affine.NewIdentity(), wedge);

			// Guard against a vacuous pass: the wedge has to leave anti-aliased coverage to compare.
			await Assert.That(unclipped.GrayBuffer.Any(b => b > 0 && b < 255)).IsTrue()
				.Because("the wedge left no partial coverage, so there is nothing for a clip to shift");

			await AssertGrayBuffer(unclipped.GrayBuffer, clipped);
		}

		/// <summary>
		/// An extreme path coordinate has to clip, not crash. The builder's rasterizer is constructed bare,
		/// so unlike the one <see cref="MatterHackers.Agg.Image.ImageBuffer.NewGraphics2D"/> hands out it has
		/// no vector clip box unless the builder sets one - and without it a coordinate of 1e12 upscales to a
		/// saturated <see cref="int.MaxValue"/> in AGG's 1/256 fixed point, where
		/// <c>RasterizerCellsAa.line</c> computes <c>dx</c> in <see cref="int"/> and recurses forever on the
		/// overflow (a stack overflow, which takes the whole test process with it).
		/// <para>
		/// Both overflow directions are covered, each against the in-bounds rectangle that clips to the same
		/// region: one running off every edge, and one running off only the right edge. Equality is what pins
		/// clipping as clipping - a clamp that folded the far edge back into the buffer instead would move
		/// coverage rather than drop it off the end.
		/// </para>
		/// <para>
		/// Note the all-covering case still does not read 255 at the left and right mask columns: the filter
		/// reads 0 past the end of the gray row (that is what the 2px bbox pad exists for), so those columns
		/// ramp exactly as any clipped edge does.
		/// </para>
		/// </summary>
		[Test]
		public async Task ExtremeCoordinatesClipInsteadOfOverflowingTheRasterizer()
		{
			double huge = 1e12;

			var everywhere = new LcdMaskBuilder(4, 3);
			everywhere.AddPath(Affine.NewIdentity(), Rectangle(-huge, -huge, huge, huge));

			var wholeBuffer = new LcdMaskBuilder(4, 3);
			wholeBuffer.AddPath(Affine.NewIdentity(), Rectangle(-8, -8, 8, 8));

			LcdMask wholeBufferMask = wholeBuffer.FinalizeMask();
			await Assert.That(wholeBufferMask.Data.All(b => b > 0)).IsTrue()
				.Because("a rectangle over the whole buffer must leave coverage in every mask byte");
			await AssertMaskBytes(wholeBufferMask.Data, everywhere.FinalizeMask());

			var offRight = new LcdMaskBuilder(4, 3);
			offRight.AddPath(Affine.NewIdentity(), Rectangle(1, 1, huge, 2));

			var inBounds = new LcdMaskBuilder(4, 3);
			inBounds.AddPath(Affine.NewIdentity(), Rectangle(1, 1, 8, 2));

			LcdMask inBoundsMask = inBounds.FinalizeMask();
			await Assert.That(inBoundsMask.Data.Any(b => b > 0)).IsTrue();
			await AssertMaskBytes(inBoundsMask.Data, offRight.FinalizeMask());
		}

		/// <summary>
		/// A triangle on deliberately fractional coordinates, so every edge produces anti-aliased bytes and
		/// mask comparisons are not carried by whole-coverage runs alone. Shared with
		/// <see cref="LcdCompositeTests"/>, which needs the same anti-aliased path end to end.
		/// </summary>
		internal static VertexStorage FractionalTriangle()
		{
			var triangle = new VertexStorage();
			triangle.MoveTo(10.25, 10.5);
			triangle.LineTo(14.75, 11.25);
			triangle.LineTo(12.5, 14.75);
			triangle.ClosePolygon();

			return triangle;
		}

		/// <summary>
		/// A wedge running clean off both sides of an 8x8 mask on a shallow slope: no vertex sits on a pixel
		/// boundary and no edge is axis-aligned, so its two long edges cross the vertical buffer edges at the
		/// angle that makes AGG's clipped-endpoint rounding observable.
		/// </summary>
		private static VertexStorage ShallowWedge()
		{
			var wedge = new VertexStorage();
			wedge.MoveTo(-40, 2.1);
			wedge.LineTo(40, 5.3);
			wedge.LineTo(40, 5.9);
			wedge.LineTo(-40, 2.7);
			wedge.ClosePolygon();

			return wedge;
		}

		/// <summary>
		/// An axis-aligned rectangle as a closed contour, in the coordinate space the caller's transform
		/// maps to mask pixels.
		/// </summary>
		private static VertexStorage Rectangle(double left, double bottom, double right, double top)
		{
			var storage = new VertexStorage();
			storage.MoveTo(left, bottom);
			storage.LineTo(right, bottom);
			storage.LineTo(right, top);
			storage.LineTo(left, top);
			storage.ClosePolygon();

			return storage;
		}

		private static Task AssertGrayRow(byte[] expected, LcdMaskBuilder builder, int row)
		{
			return AssertGrayRow(expected, builder.GrayBuffer, builder.GrayWidth, row);
		}

		/// <summary>
		/// Compares one row of a gray coverage buffer; row 0 is the bottom row (the buffer is Y-up).
		/// </summary>
		private static async Task AssertGrayRow(byte[] expected, byte[] gray, int grayWidth, int row)
		{
			await Assert.That(expected.Length).IsEqualTo(grayWidth);
			for (int subpixel = 0; subpixel < grayWidth; subpixel++)
			{
				await Assert.That(gray[(row * grayWidth) + subpixel])
					.IsEqualTo(expected[subpixel])
					.Because($"gray row {row} subpixel {subpixel} (pixel {subpixel / 3})");
			}
		}

		/// <summary>
		/// Compares a whole gray coverage buffer, reporting the first mismatch by row and subpixel.
		/// </summary>
		private static async Task AssertGrayBuffer(byte[] expected, LcdMaskBuilder builder)
		{
			byte[] actual = builder.GrayBuffer;
			await Assert.That(actual.Length).IsEqualTo(expected.Length);
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(actual[i])
					.IsEqualTo(expected[i])
					.Because($"gray row {i / builder.GrayWidth} subpixel {i % builder.GrayWidth}");
			}
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
