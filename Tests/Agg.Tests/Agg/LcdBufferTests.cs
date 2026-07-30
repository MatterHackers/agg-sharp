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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="LcdBuffer"/>: the two-plane render target that lets per-channel coverage survive a
	/// widget backbuffer, plus the ways back out of it (collapse to single alpha, composite onto an image,
	/// plane flip). Ported from the agg-gui Rust reference's <c>LcdBuffer</c> tests
	/// (<c>lcd_coverage\tests.rs</c>).
	/// </summary>
	/// <remarks>
	/// Every expected byte here is a hand-computed literal, derived in the comment above it from the formula
	/// the reference documents - not a value read back from this port. A test that recomputed the blend
	/// would pass against any arithmetic, including the wrong arithmetic.
	/// </remarks>
	public class LcdBufferTests
	{
		/// <summary>
		/// A fresh buffer is transparent in <b>both</b> planes, which is the property the whole two-plane
		/// design exists for: unpainted regions read as "no paint landed here", not as "intentional black",
		/// so a cached buffer can composite onto any parent without painting a black rectangle over its
		/// margins.
		/// </summary>
		[Test]
		public async Task FreshBufferIsTransparentInBothPlanes()
		{
			var buffer = new LcdBuffer(8, 4);

			await Assert.That(buffer.ColorPlane.Length).IsEqualTo(8 * 4 * 3);
			await Assert.That(buffer.AlphaPlane.Length).IsEqualTo(8 * 4 * 3);
			await Assert.That(buffer.ColorPlane.All(b => b == 0)).IsTrue()
				.Because("a fresh color plane must be zero");
			await Assert.That(buffer.AlphaPlane.All(b => b == 0)).IsTrue()
				.Because("a fresh alpha plane must be zero, which is what 'transparent' means here");
		}

		/// <summary>
		/// A pathological size is clamped to 1x1 rather than attempted or thrown: the frame's paint is lost
		/// but the application survives to clamp the offending widget's bounds on the next layout pass.
		/// </summary>
		[Test]
		public async Task PathologicalSizeClampsInsteadOfAllocating()
		{
			// 100k x 100k x 3 bytes is 30 GB per plane.
			var buffer = new LcdBuffer(100000, 100000);

			await Assert.That(buffer.Width).IsEqualTo(1);
			await Assert.That(buffer.Height).IsEqualTo(1);
			await Assert.That(buffer.ColorPlane.Length).IsEqualTo(3);
		}

		/// <summary>
		/// <see cref="LcdBuffer.Clear"/> writes premultiplied color into every pixel and the same alpha into
		/// all three channels - a flat fill has no per-subpixel differentiation to record. The partial-alpha
		/// arm is the one that pins premultiplication: a straight-alpha clear would leave the color bytes at
		/// full intensity.
		/// </summary>
		[Test]
		public async Task ClearWritesPremultipliedColorAndTheSameAlphaOnEveryChannel()
		{
			var opaque = new LcdBuffer(4, 3);
			opaque.Clear(new Color(255, 128, 64, 255));
			for (int y = 0; y < 3; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					// Alpha 1.0, so premultiplied color is the color itself and every alpha channel is 255.
					await AssertPixel(opaque, x, y, new byte[] { 255, 128, 64 }, new byte[] { 255, 255, 255 });
				}
			}

			// Alpha 137/255: each color channel is scaled by it (137/255 * 255 = 137, * 128/255 = 68.8 -> 69,
			// * 64/255 = 34.4 -> 34) and every alpha channel takes 137.
			var faded = new LcdBuffer(2, 2);
			faded.Clear(new Color(255, 128, 64, 137));
			await AssertPixel(faded, 1, 1, new byte[] { 137, 69, 34 }, new byte[] { 137, 137, 137 });
		}

		/// <summary>
		/// The whole per-channel composite on one pixel, over a buffer that already holds paint, hand
		/// computed and pinned to exact bytes in both planes:
		/// <code>
		/// ea_c     = fill.alpha/255 * mask_c/255
		/// color_c := fill_c * ea_c + color_c * (1 - ea_c)
		/// alpha_c := ea_c          + alpha_c * (1 - ea_c)
		/// </code>
		/// The mask's three coverages differ deliberately, so a channel mixup would swap the results.
		/// </summary>
		[Test]
		public async Task CompositeMaskBlendsEachChannelIntoBothPlanes()
		{
			var buffer = new LcdBuffer(1, 1);
			SetPixel(buffer, 0, 0, new byte[] { 100, 30, 200 }, new byte[] { 150, 60, 255 });
			var mask = new LcdMask(new byte[] { 200, 100, 0 }, 1, 1);

			buffer.CompositeMask(mask, new Color(255, 128, 64, 137), 0, 0);

			// sa = 137/255 = 0.537255.
			// R: ea = 0.537255 * 200/255 = 0.421376.
			//    color = 1.0 * 0.421376 + 100/255 * 0.578624 = 0.648288 -> 165.31 -> 165
			//    alpha = 0.421376 + 150/255 * 0.578624 = 0.761743 -> 194.24 -> 194
			// G: ea = 0.537255 * 100/255 = 0.210688.
			//    color = 128/255 * 0.210688 + 30/255 * 0.789312 = 0.198617 -> 50.65 -> 51
			//    alpha = 0.210688 + 60/255 * 0.789312 = 0.396409 -> 101.08 -> 101
			// B: coverage 0, so both planes survive their own round trip exactly (200 and 255) - the
			//    per-pixel skip only fires when all three coverages are zero.
			await AssertPixel(buffer, 0, 0, new byte[] { 165, 51, 200 }, new byte[] { 194, 101, 255 });
		}

		/// <summary>
		/// Two composites accumulate per channel exactly as the same two applied serially by hand, and
		/// strictly independently: the R channel is saturated by the first paint and untouched by the
		/// second, B the reverse, and only G actually accumulates. Cross-channel bleed (a scalar-alpha
		/// collapse anywhere in the composite) would show up as G's value leaking into R or B.
		/// </summary>
		[Test]
		public async Task RepeatedCompositesAccumulatePerChannel()
		{
			var buffer = new LcdBuffer(1, 1);
			var white = new Color(255, 255, 255, 255);

			buffer.CompositeMask(new LcdMask(new byte[] { 255, 128, 0 }, 1, 1), white, 0, 0);

			// Opaque white through coverage (255, 128, 0) over a transparent buffer: premultiplied color
			// equals coverage, and so does alpha. 128/255 * 255 = 128 exactly.
			await AssertPixel(buffer, 0, 0, new byte[] { 255, 128, 0 }, new byte[] { 255, 128, 0 });

			buffer.CompositeMask(new LcdMask(new byte[] { 0, 128, 255 }, 1, 1), white, 0, 0);

			// R: ea = 0 -> color = 1.0 * 0 + 1.0 * 1 = 1.0 -> 255 (already saturated, unchanged).
			// G: ea = 0.501961 -> color = alpha = 0.501961 + 0.501961 * 0.498039 = 0.751957 -> 192.25 -> 192.
			// B: ea = 1.0 -> color = alpha = 1.0 -> 255.
			await AssertPixel(buffer, 0, 0, new byte[] { 255, 192, 255 }, new byte[] { 255, 192, 255 });
		}

		/// <summary>
		/// The composite clip is half open - left and bottom inclusive, right and top exclusive - and is
		/// intersected with the buffer, so a clip hanging off the edge is not an error. Also pins that a mask
		/// overhanging the buffer composites only its overlap.
		/// </summary>
		[Test]
		public async Task CompositeMaskHonoursAHalfOpenClipRect()
		{
			var mask = FullCoverageMask(4, 4);
			var white = new Color(255, 255, 255, 255);

			var clipped = new LcdBuffer(4, 4);
			clipped.CompositeMask(mask, white, 0, 0, new RectangleInt(1, 1, 3, 3));
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					bool inside = x >= 1 && x < 3 && y >= 1 && y < 3;
					byte expected = inside ? (byte)255 : (byte)0;
					await AssertPixel(
						clipped, x, y, new byte[] { expected, expected, expected }, new byte[] { expected, expected, expected });
				}
			}

			// Clip wider than the buffer: intersected, not trusted. Every pixel is painted, nothing throws.
			var wide = new LcdBuffer(2, 2);
			wide.CompositeMask(mask, white, 0, 0, new RectangleInt(-10, -10, 100, 100));
			await Assert.That(wide.AlphaPlane.All(b => b == 255)).IsTrue();

			// Empty clip paints nothing.
			var empty = new LcdBuffer(2, 2);
			empty.CompositeMask(mask, white, 0, 0, new RectangleInt(1, 1, 1, 3));
			await Assert.That(empty.AlphaPlane.All(b => b == 0)).IsTrue();

			// Mask hanging off the bottom-left corner: only its upper-right 2x2 lands.
			var overhang = new LcdBuffer(4, 4);
			overhang.CompositeMask(mask, white, -2, -2);
			for (int y = 0; y < 4; y++)
			{
				for (int x = 0; x < 4; x++)
				{
					byte expected = x < 2 && y < 2 ? (byte)255 : (byte)0;
					await AssertPixel(
						overhang, x, y, new byte[] { expected, expected, expected }, new byte[] { expected, expected, expected });
				}
			}
		}

		/// <summary>
		/// Black ink on a transparent buffer leaves the color plane at zero (premultiplied black is zero) and
		/// records everything in the alpha plane. This is what a label backbuffer actually looks like - it
		/// paints glyphs and no background - so it is the case that has to stay lossless, and the untouched
		/// corners are what let it blit onto any parent.
		/// </summary>
		[Test]
		public async Task BlackFillOnATransparentBufferRecordsAlphaOnly()
		{
			var buffer = new LcdBuffer(20, 20);

			buffer.FillPath(Rectangle(5.25, 5.5, 15.75, 15.5), new Color(0, 0, 0, 255), Affine.NewIdentity());

			await Assert.That(buffer.ColorPlane.All(b => b == 0)).IsTrue()
				.Because("premultiplied black is zero, so the color plane must stay zero");
			await Assert.That(buffer.AlphaPlane.Any(b => b > 0)).IsTrue()
				.Because("the alpha plane must record coverage where the path was filled");

			// Interior is fully covered on every channel; the corners are outside the padded bbox entirely.
			await AssertPixel(buffer, 10, 10, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });
			await AssertPixel(buffer, 0, 0, new byte[] { 0, 0, 0 }, new byte[] { 0, 0, 0 });
			await AssertPixel(buffer, 19, 19, new byte[] { 0, 0, 0 }, new byte[] { 0, 0, 0 });

			// The fractional left edge lands mid-pixel, so its subpixels must disagree - that per-channel
			// disagreement is the entire point of the pipeline, and a whole-pixel coverage path could not
			// produce it.
			int edge = buffer.PixelOffset(5, 10);
			bool channelsDiffer = buffer.AlphaPlane[edge] != buffer.AlphaPlane[edge + 1]
				|| buffer.AlphaPlane[edge + 1] != buffer.AlphaPlane[edge + 2];
			await Assert.That(channelsDiffer).IsTrue()
				.Because("a fractional edge must produce different coverage per channel");
		}

		/// <summary>
		/// <b>The vector-level guarantee.</b> <see cref="LcdBuffer.FillPath"/> must be exactly
		/// <see cref="BoundedMaskBuilder"/> plus <see cref="LcdBuffer.CompositeMask"/> at the reported
		/// origin - byte for byte in both planes. Everything that ever paints into an LCD buffer goes
		/// through one of those two routes, so if they can disagree, a cached mask and a live fill of the
		/// same geometry can disagree too.
		/// </summary>
		[Test]
		public async Task FillPathIsTheBoundedMaskCompositedAtItsReportedOrigin()
		{
			VertexStorage triangle = LcdMaskBuilderTests.FractionalTriangle();
			Affine transform = Affine.NewRotation(0.3) * Affine.NewTranslation(6.25, 4.5);
			var ink = new Color(220, 40, 90, 200);

			var viaFillPath = new LcdBuffer(32, 32);
			viaFillPath.Clear(new Color(255, 255, 255, 255));
			viaFillPath.FillPath(triangle, ink, transform);

			var viaMask = new LcdBuffer(32, 32);
			viaMask.Clear(new Color(255, 255, 255, 255));
			bool built = BoundedMaskBuilder.TryBuild(
				32, 32, triangle, transform, out LcdMask mask, out int originX, out int originY);
			await Assert.That(built).IsTrue();
			viaMask.CompositeMask(mask, ink, originX, originY);

			// Guard against a vacuous pass before demanding equality.
			await Assert.That(viaMask.AlphaPlane.Any(b => b > 0)).IsTrue();
			await AssertPlanesEqual(viaMask, viaFillPath);
		}

		/// <summary>
		/// A fractional clip rect becomes the pixel rect <c>[floor(left), ceil(right))</c> x
		/// <c>[floor(bottom), ceil(top))</c>, so every pixel the clip touches at all is painted and no
		/// other is. Rounding instead of flooring/ceiling would shrink the painted region by a pixel on
		/// each side, which is why the bounds here are chosen to disagree under rounding.
		/// </summary>
		[Test]
		public async Task FillPathClipsToEveryPixelTheClipRectTouches()
		{
			var buffer = new LcdBuffer(20, 20);

			// A rect covering the whole buffer, so the clip is the only thing bounding the paint.
			buffer.FillPath(
				Rectangle(-5, -5, 25, 25),
				new Color(255, 255, 255, 255),
				Affine.NewIdentity(),
				new RectangleDouble(5.75, 5.75, 9.25, 9.25));

			for (int y = 0; y < 20; y++)
			{
				for (int x = 0; x < 20; x++)
				{
					int offset = buffer.PixelOffset(x, y);
					bool inside = x >= 5 && x < 10 && y >= 5 && y < 10;
					if (inside)
					{
						await Assert.That(buffer.AlphaPlane[offset]).IsGreaterThan((byte)0)
							.Because($"({x}, {y}) is inside the clip");
					}
					else
					{
						await Assert.That(buffer.AlphaPlane[offset]).IsEqualTo((byte)0)
							.Because($"({x}, {y}) is outside the clip");
					}
				}
			}

			// Interior of the clip is fully covered; the mask's own left column loses coverage to the
			// filter's zero reads outside the mask, which is expected and not what this test is about.
			await AssertPixel(buffer, 7, 7, new byte[] { 255, 255, 255 }, new byte[] { 255, 255, 255 });
		}

		/// <summary>
		/// The collapse to a single alpha uses the <b>Rec.709 luminance-weighted mean</b> of the three
		/// channel alphas, and the lift to <c>max(color)</c> applies on the <b>straight-alpha path only</b>.
		/// Both were real bugs in the reference:
		/// <list type="bullet">
		/// <item><description>collapsing with <c>max</c> over-weights the two channels below the max and
		/// biases every unequal-alpha pixel dark - "LCD text renders ~20% bolder";</description></item>
		/// <item><description>omitting the lift makes the straight-alpha unpremultiply clamp for light ink,
		/// eating exactly the near-white glyph edges that carry it.</description></item>
		/// </list>
		/// The coverage triple here is a red-heavy glyph edge, where <c>max</c> (200) and the weighted mean
		/// (96) differ visibly. It is deliberately <b>asymmetric in red versus blue</b>: with a symmetric
		/// triple, transposing the Rec.709 red and blue weights - or swapping the two channels on the way out
		/// - would leave every expected byte here unchanged and the test would pass against the swap.
		/// </summary>
		[Test]
		public async Task CollapseWeightsAlphaByRec709AndLiftsOnlyOnTheStraightPath()
		{
			// Opaque white ink through coverage (200, 60, 150): premultiplied color equals coverage, so
			// color = alpha = (200, 60, 150) - the light-on-dark case where the lift binds.
			var light = new LcdBuffer(1, 1);
			light.CompositeMask(new LcdMask(new byte[] { 200, 60, 150 }, 1, 1), new Color(255, 255, 255, 255), 0, 0);
			await AssertPixel(light, 0, 0, new byte[] { 200, 60, 150 }, new byte[] { 200, 60, 150 });

			// weighted = 0.2126*200 + 0.7152*60 + 0.0722*150 = 42.52 + 42.912 + 10.83 = 96.262 -> 96;
			// lift = max(color) = 200. Straight path takes max(96.262, 200) = 200, then unpremultiplies:
			// 200/200 * 255 -> 255, 60/200 * 255 = 76.5 -> 77, 150/200 * 255 = 191.25 -> 191.
			Color straight = LcdBuffer.CollapseLcdPixel(200, 60, 150, 200, 60, 150);
			await AssertColor(new Color(255, 77, 191, 200), straight, "straight-alpha collapse");

			// max would have been 200 on both paths; the premultiplied path must land on the weighted 96,
			// and must not lift (its color passes through unchanged, because it never divides by alpha).
			// Transposing the red and blue weights would give 0.0722*200 + 0.7152*60 + 0.2126*150 = 89.
			Color premultiplied = LcdBuffer.CollapseLcdPixelPremultiplied(200, 60, 150, 200, 60, 150);
			await AssertColor(new Color(200, 60, 150, 96), premultiplied, "premultiplied collapse");

			// The lift's purpose, asserted directly: the straight color must round trip back to the
			// premultiplied color it came from, i.e. nothing was lost to the unpremultiply clamp.
			await Assert.That((int)System.Math.Round(straight.red * (straight.alpha / 255.0))).IsEqualTo(200);
			await Assert.That((int)System.Math.Round(straight.green * (straight.alpha / 255.0))).IsEqualTo(60);
			await Assert.That((int)System.Math.Round(straight.blue * (straight.alpha / 255.0))).IsEqualTo(150);

			// Dark ink on the same coverage: premultiplied color is zero, so the lift cannot bind and both
			// paths land on the weighted alpha - which is what keeps dark-on-light luminance exact.
			var dark = new LcdBuffer(1, 1);
			dark.CompositeMask(new LcdMask(new byte[] { 200, 60, 150 }, 1, 1), new Color(0, 0, 0, 255), 0, 0);
			await AssertPixel(dark, 0, 0, new byte[] { 0, 0, 0 }, new byte[] { 200, 60, 150 });
			await AssertColor(new Color(0, 0, 0, 96), LcdBuffer.CollapseLcdPixel(0, 0, 0, 200, 60, 150), "dark collapse");

			// Nothing at all collapses to a fully transparent pixel, not to an opaque black one.
			await AssertColor(new Color(0, 0, 0, 0), LcdBuffer.CollapseLcdPixel(0, 0, 0, 0, 0, 0), "empty collapse");
		}

		/// <summary>
		/// The whole-buffer flatten applies <see cref="LcdBuffer.CollapseLcdPixel"/> per pixel and lands each
		/// result at the same Y-up position in a straight-alpha B, G, R, A image. Both halves matter: a
		/// row-flipped or channel-swapped flatten would still collapse "correctly" and still paint text
		/// upside down or in the wrong hue.
		/// </summary>
		[Test]
		public async Task CollapsedImageKeepsPositionAndChannelOrder()
		{
			var buffer = new LcdBuffer(2, 2);

			// Only (1, 0) - bottom-right - is painted, in the same red-heavy light-ink case as the collapse
			// test above, which collapses to (255, 77, 191, 200). Red and blue differ, so a channel-swapped
			// write lands on the wrong byte instead of an identical one.
			buffer.CompositeMask(new LcdMask(new byte[] { 200, 60, 150 }, 1, 1), new Color(255, 255, 255, 255), 1, 0);

			ImageBuffer collapsed = buffer.ToImageBufferCollapsed();

			await Assert.That(collapsed.Width).IsEqualTo(2);
			await Assert.That(collapsed.Height).IsEqualTo(2);
			await AssertColor(new Color(255, 77, 191, 200), ReadPixel(collapsed, 1, 0), "collapsed (1, 0)");
			await AssertColor(new Color(0, 0, 0, 0), ReadPixel(collapsed, 0, 0), "collapsed (0, 0)");
			await AssertColor(new Color(0, 0, 0, 0), ReadPixel(collapsed, 1, 1), "collapsed (1, 1)");
		}

		/// <summary>
		/// Compositing an LCD buffer onto a premultiplied 32 bit image: per channel
		/// <c>dest = source + dest * (1 - source_alpha)</c> - no extra modulation, because the color plane is
		/// already premultiplied - and the destination's single alpha takes <c>max</c> over the three source
		/// alphas, which answers the only question a later source-over blit asks of it ("was this pixel drawn
		/// on"). A destination alpha built from the Rec.709 collapse instead would under-report coverage on
		/// every glyph edge.
		/// </summary>
		[Test]
		public async Task CompositeOntoImageBlendsPerChannelAndTakesMaxAlpha()
		{
			// Source and destination are both asymmetric in red versus blue, so a transposed channel write
			// lands on a different byte rather than an identical one.
			var source = new LcdBuffer(2, 1);
			SetPixel(source, 0, 0, new byte[] { 200, 60, 150 }, new byte[] { 200, 60, 150 });

			ImageBuffer destination = PremultipliedImage(2, 1, new Color(100, 30, 210, 150));
			source.CompositeOnto(destination, 0, 0);

			// R: 200/255 + 100/255 * (1 - 200/255) = 0.868897 -> 221.57 -> 222
			// G: 60/255 + 30/255 * (1 - 60/255) = 0.325260 -> 82.94 -> 83
			// B: 150/255 + 210/255 * (1 - 150/255) = 0.927336 -> 236.47 -> 236
			// A: max source alpha = 200/255, so 200/255 + 150/255 * (1 - 200/255) = 0.911188 -> 232.35 -> 232
			await AssertColor(new Color(222, 83, 236, 232), ReadPixel(destination, 0, 0), "blended pixel");

			// The source's second pixel is untouched (all three alphas zero), so the destination survives.
			await AssertColor(new Color(100, 30, 210, 150), ReadPixel(destination, 1, 0), "unpainted pixel");

			// A global fade scales the premultiplied color and the per-channel alpha by the same factor, so
			// the source stays premultiplied-consistent: opaque white at 0.5 lands as 128 everywhere.
			var opaque = new LcdBuffer(1, 1);
			SetPixel(opaque, 0, 0, new byte[] { 255, 255, 255 }, new byte[] { 255, 255, 255 });
			ImageBuffer faded = PremultipliedImage(1, 1, new Color(0, 0, 0, 0));
			opaque.CompositeOnto(faded, 0, 0, 0.5);
			await AssertColor(new Color(128, 128, 128, 128), ReadPixel(faded, 0, 0), "half-faded pixel");
		}

		/// <summary>
		/// Buffer onto buffer keeps the per-channel alpha instead of collapsing it, so a nested LCD-coverage
		/// widget can flush into an LCD-coverage parent with its chroma intact. Transparent source pixels
		/// leave the destination alone entirely - the property that makes a popped layer safe.
		/// </summary>
		[Test]
		public async Task CompositeBufferBlendsBothPlanesPerChannel()
		{
			// Asymmetric in red versus blue, so a transposed channel write cannot pass unnoticed.
			var source = new LcdBuffer(2, 1);
			SetPixel(source, 0, 0, new byte[] { 200, 60, 150 }, new byte[] { 200, 60, 150 });

			var destination = new LcdBuffer(2, 1);
			SetPixel(destination, 0, 0, new byte[] { 100, 30, 200 }, new byte[] { 150, 60, 255 });
			SetPixel(destination, 1, 0, new byte[] { 10, 20, 30 }, new byte[] { 40, 50, 60 });

			destination.CompositeBuffer(source, 0, 0);

			// Color, same as the premultiplied blend onto an image (the source needs no modulation):
			//   R: 200/255 + 100/255 * 0.215686 = 0.868897 -> 221.57 -> 222
			//   G: 60/255 + 30/255 * 0.764706 = 0.325260 -> 82.94 -> 83
			//   B: 150/255 + 200/255 * 0.411765 = 0.911161 -> 232.35 -> 232
			// Alpha, per channel rather than max:
			//   R: 200/255 + 150/255 * 0.215686 = 0.911188 -> 232.35 -> 232
			//   G: 60/255 + 60/255 * 0.764706 = 0.415225 -> 105.88 -> 106
			//   B: 150/255 + 255/255 * 0.411765 = 1.0 -> 255
			await AssertPixel(destination, 0, 0, new byte[] { 222, 83, 232 }, new byte[] { 232, 106, 255 });

			// Transparent source pixel: destination untouched, both planes.
			await AssertPixel(destination, 1, 0, new byte[] { 10, 20, 30 }, new byte[] { 40, 50, 60 });
		}

		/// <summary>
		/// <see cref="LcdBuffer.FlipPlane"/> turns a Y-up plane into a top-row-first one and is its own
		/// inverse. This is the texture boundary that gotcha 1 of the port plan warns about: both planes live
		/// Y-up in memory, but a GL upload wants the visual top row first, and getting it wrong renders text
		/// upside down rather than failing loudly.
		/// </summary>
		[Test]
		public async Task FlipPlaneReversesRowOrderAndRoundTrips()
		{
			// 2x3 pixels: each byte is (row * 10) + column index within the row, so a row is identifiable.
			var plane = new byte[2 * 3 * 3];
			for (int y = 0; y < 3; y++)
			{
				for (int i = 0; i < 6; i++)
				{
					plane[(y * 6) + i] = (byte)((y * 10) + i);
				}
			}

			byte[] flipped = LcdBuffer.FlipPlane(plane, 2, 3);

			// Row 0 (bottom) must now be last, row 2 (top) first, with the bytes inside each row unmoved.
			var expected = new byte[]
			{
				20, 21, 22, 23, 24, 25,
				10, 11, 12, 13, 14, 15,
				0, 1, 2, 3, 4, 5,
			};
			await AssertBytesEqual(expected, flipped, "flipped plane");
			await AssertBytesEqual(plane, LcdBuffer.FlipPlane(flipped, 2, 3), "round-tripped plane");

			// The instance helpers flip the buffer's own planes, which is what the upload path uses: the
			// bottom-left pixel of a 3-row buffer becomes the first pixel of the last row.
			var buffer = new LcdBuffer(2, 3);
			SetPixel(buffer, 0, 0, new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 });
			await AssertBytesEqual(LcdBuffer.FlipPlane(buffer.ColorPlane, 2, 3), buffer.ColorPlaneFlipped(), "color plane");
			await AssertBytesEqual(LcdBuffer.FlipPlane(buffer.AlphaPlane, 2, 3), buffer.AlphaPlaneFlipped(), "alpha plane");
			await Assert.That(buffer.ColorPlaneFlipped()[2 * 6]).IsEqualTo((byte)1);
			await Assert.That(buffer.AlphaPlaneFlipped()[2 * 6]).IsEqualTo((byte)4);
		}

		/// <summary>An all-255 coverage mask, so a composite through it is a plain fill.</summary>
		private static LcdMask FullCoverageMask(int width, int height)
		{
			var mask = new LcdMask(width, height);
			for (int i = 0; i < mask.Data.Length; i++)
			{
				mask.Data[i] = 255;
			}

			return mask;
		}

		/// <summary>An axis-aligned rectangle path, corners as given.</summary>
		private static VertexStorage Rectangle(double left, double bottom, double right, double top)
		{
			var path = new VertexStorage();
			path.MoveTo(left, bottom);
			path.LineTo(right, bottom);
			path.LineTo(right, top);
			path.LineTo(left, top);
			path.ClosePolygon();

			return path;
		}

		/// <summary>
		/// Seeds one pixel's two planes directly, so a composite can be tested against a known starting
		/// state that <see cref="LcdBuffer.Clear"/> could not produce (Clear cannot make the three channels
		/// disagree, which is exactly the interesting case).
		/// </summary>
		private static void SetPixel(LcdBuffer buffer, int x, int y, byte[] color, byte[] alpha)
		{
			int offset = buffer.PixelOffset(x, y);
			for (int channel = 0; channel < 3; channel++)
			{
				buffer.ColorPlane[offset + channel] = color[channel];
				buffer.AlphaPlane[offset + channel] = alpha[channel];
			}
		}

		/// <summary>Element-wise, so the comparison is order sensitive and reports which byte moved.</summary>
		private static async Task AssertBytesEqual(byte[] expected, byte[] actual, string what)
		{
			await Assert.That(actual.Length).IsEqualTo(expected.Length).Because($"length of {what}");
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(actual[i]).IsEqualTo(expected[i]).Because($"byte {i} of {what}");
			}
		}

		private static async Task AssertPixel(LcdBuffer buffer, int x, int y, byte[] color, byte[] alpha)
		{
			int offset = buffer.PixelOffset(x, y);
			for (int channel = 0; channel < 3; channel++)
			{
				await Assert.That(buffer.ColorPlane[offset + channel]).IsEqualTo(color[channel])
					.Because($"color {"RGB"[channel]} at ({x}, {y})");
				await Assert.That(buffer.AlphaPlane[offset + channel]).IsEqualTo(alpha[channel])
					.Because($"alpha {"RGB"[channel]} at ({x}, {y})");
			}
		}

		/// <summary>
		/// Byte equality of both planes, reporting the first difference with its pixel, plane and channel -
		/// enough to tell "one channel is off by one at an edge" from "the paint landed somewhere else".
		/// </summary>
		private static async Task AssertPlanesEqual(LcdBuffer expected, LcdBuffer actual)
		{
			await Assert.That(actual.Width).IsEqualTo(expected.Width);
			await Assert.That(actual.Height).IsEqualTo(expected.Height);

			string difference = null;
			for (int i = 0; i < expected.ColorPlane.Length && difference == null; i++)
			{
				bool colorDiffers = expected.ColorPlane[i] != actual.ColorPlane[i];
				bool alphaDiffers = expected.AlphaPlane[i] != actual.AlphaPlane[i];
				if (colorDiffers || alphaDiffers)
				{
					int pixel = i / 3;
					string plane = colorDiffers ? "color" : "alpha";
					byte expectedByte = colorDiffers ? expected.ColorPlane[i] : expected.AlphaPlane[i];
					byte actualByte = colorDiffers ? actual.ColorPlane[i] : actual.AlphaPlane[i];
					difference = $"{plane} plane differs at (x {pixel % expected.Width}, y {pixel / expected.Width}, "
						+ $"channel {"RGB"[i % 3]}): expected {expectedByte}, was {actualByte}.";
				}
			}

			await Assert.That(difference).IsNull().Because(difference ?? string.Empty);
		}

		/// <summary>A 32 bit-per-pixel image pre-filled with premultiplied bytes exactly as given.</summary>
		private static ImageBuffer PremultipliedImage(int width, int height, Color fill)
		{
			var image = new ImageBuffer(width, height, 32, new BlenderPreMultBGRA());
			byte[] buffer = image.GetBuffer();
			int bytesPerPixel = image.GetBytesBetweenPixelsInclusive();
			for (int y = 0; y < height; y++)
			{
				int rowOffset = image.GetBufferOffsetXY(0, y);
				for (int x = 0; x < width; x++)
				{
					int offset = rowOffset + (x * bytesPerPixel);
					buffer[offset + ImageBuffer.OrderR] = fill.red;
					buffer[offset + ImageBuffer.OrderG] = fill.green;
					buffer[offset + ImageBuffer.OrderB] = fill.blue;
					buffer[offset + ImageBuffer.OrderA] = fill.alpha;
				}
			}

			return image;
		}

		/// <summary>Reads the raw bytes, not the blender's view of them, so the assertions pin memory layout.</summary>
		private static Color ReadPixel(ImageBuffer image, int x, int y)
		{
			byte[] buffer = image.GetBuffer();
			int offset = image.GetBufferOffsetXY(x, y);

			return new Color(
				buffer[offset + ImageBuffer.OrderR],
				buffer[offset + ImageBuffer.OrderG],
				buffer[offset + ImageBuffer.OrderB],
				buffer[offset + ImageBuffer.OrderA]);
		}

		private static async Task AssertColor(Color expected, Color actual, string what)
		{
			await Assert.That(actual.red).IsEqualTo(expected.red).Because($"red of {what}");
			await Assert.That(actual.green).IsEqualTo(expected.green).Because($"green of {what}");
			await Assert.That(actual.blue).IsEqualTo(expected.blue).Because($"blue of {what}");
			await Assert.That(actual.alpha).IsEqualTo(expected.alpha).Because($"alpha of {what}");
		}
	}
}
