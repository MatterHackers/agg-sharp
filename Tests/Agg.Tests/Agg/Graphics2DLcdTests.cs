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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="Graphics2D.RenderLcd"/>: the general vector entry into the LCD subpixel pipeline,
	/// its gates (<see cref="LcdRenderSettings.Enabled"/>, the effective-scale cap,
	/// <see cref="Graphics2D.CanCompositeLcd"/>) and the invariants the whole design rests on - the clip is
	/// honoured, and a whole-pixel translation moves a fill without changing a single coverage byte.
	/// </summary>
	/// <remarks>
	/// Mirrors the reference's <c>DrawCtx</c> hooks (<c>draw_lcd_mask</c> /
	/// <c>has_lcd_mask_composite</c>, <c>draw_ctx.rs</c>) and the gates its callers apply
	/// (<c>font_settings.rs</c> <c>lcd_enabled</c>).
	/// <para>
	/// <see cref="LcdRenderSettings"/> is process-wide, so every test here is <c>[NotInParallel]</c> and
	/// restores what it changed in a finally block.
	/// </para>
	/// </remarks>
	public class Graphics2DLcdTests
	{
		private const int BufferWidth = 32;

		private const int BufferHeight = 16;

		/// <summary>
		/// Fractional edges on purpose: whole-pixel edges would produce a mask with no partial coverage, and
		/// therefore no channel variation to tell the LCD raster apart from an ordinary anti-aliased one. The
		/// x edges also stay inside their pixel cells when shifted half a pixel right (4.3 -&gt; 4.8,
		/// 19.4 -&gt; 19.9), so the phase comparison below is over two masks of the same size - a difference
		/// in coverage rather than a difference in bounding box.
		/// </summary>
		private static readonly RectangleDouble FillRect = new RectangleDouble(4.3, 3.2, 19.4, 10.6);

		/// <summary>
		/// With every gate open, the Graphics2D entry produces exactly what the pipeline's own pieces produce
		/// by hand - the bbox-sized mask from <see cref="BoundedMaskBuilder"/> composited by
		/// <see cref="LcdComposite"/> at the origin that builder reports. This is the test that pins the
		/// wiring: the transform that reaches the builder, the clip taken from the destination, and the
		/// integer origin.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task LcdFillMatchesTheMaskPipelineByHand()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				ImageBuffer painted = OpaqueWhite();
				Graphics2D graphics = painted.NewGraphics2D();
				await Assert.That(graphics.CanCompositeLcd).IsTrue()
					.Because("a 32 bit ImageBuffer destination is the case the LCD composite is written for");
				graphics.RenderLcd(path, Color.Black);

				ImageBuffer expected = OpaqueWhite();
				bool built = BoundedMaskBuilder.TryBuild(
					BufferWidth,
					BufferHeight,
					path,
					Affine.NewIdentity(),
					out LcdMask mask,
					out int originX,
					out int originY,
					graphics.GetClippingRect());
				await Assert.That(built).IsTrue();
				LcdComposite.Composite(expected, mask, Color.Black, originX, originY);

				await Assert.That(painted.Equals(expected, 0)).IsTrue()
					.Because("RenderLcd must be the mask pipeline and nothing else");

				// Negative controls: the fill actually painted, and it is genuinely a different raster from
				// the ordinary path - without which every fallback test below would be vacuous.
				await Assert.That(painted.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("something has to have been painted");
				await Assert.That(painted.Equals(PlainRender(path, Affine.NewIdentity()), 0)).IsFalse()
					.Because("the LCD raster must differ from the ordinary anti-aliased fill");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// With the toggle off, <see cref="Graphics2D.RenderLcd"/> is byte-for-byte the ordinary
		/// <see cref="Graphics2D.Render"/> - a real fill, not a dropped one. The reference has the same
		/// property: a caller reaching for LCD on a backend or setting that refuses it still gets its paint.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task DisabledFallsBackToTheOrdinaryFill()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = false;
				VertexStorage path = Rectangle(FillRect);

				ImageBuffer fallback = OpaqueWhite();
				fallback.NewGraphics2D().RenderLcd(path, Color.Black);

				ImageBuffer plain = PlainRender(path, Affine.NewIdentity());
				await Assert.That(fallback.Equals(plain, 0)).IsTrue();
				await Assert.That(fallback.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the fallback has to actually render");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// A destination that cannot composite per-channel coverage reports
		/// <see cref="Graphics2D.CanCompositeLcd"/> false and receives the ordinary fill, exactly as the
		/// reference's backends do through <c>has_lcd_mask_composite</c>. An 8 bit-per-pixel image is the
		/// clearest case: there are no three channels to carry independent coverage.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task DestinationThatCannotCompositeFallsBack()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				var fallback = new ImageBuffer(BufferWidth, BufferHeight, 8, new blender_gray(1));
				Graphics2D fallbackGraphics = fallback.NewGraphics2D();
				await Assert.That(fallbackGraphics.CanCompositeLcd).IsFalse();
				fallbackGraphics.RenderLcd(path, Color.White);

				var plain = new ImageBuffer(BufferWidth, BufferHeight, 8, new blender_gray(1));
				plain.NewGraphics2D().Render(path, Color.White);

				await Assert.That(fallback.Equals(plain, 0)).IsTrue();
				await Assert.That(fallback.Equals(new ImageBuffer(BufferWidth, BufferHeight, 8, new blender_gray(1)), 0)).IsFalse()
					.Because("the fallback has to actually render");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The effective-scale cap overrides the toggle, and its boundary is inclusive: at exactly
		/// <see cref="LcdRenderSettings.MaxEffectiveScale"/> LCD still runs, and the very next representable
		/// double refuses it. The reference's comparison is <c>effective_scale() &gt; 1.25</c>
		/// (<c>font_settings.rs:174</c>), so 125% displays keep subpixel text and anything past it does not.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task EffectiveScaleCapIsExclusiveAtTheBoundary()
		{
			await Assert.That(LcdRenderSettings.EffectiveScaleAllowsLcd(1.0)).IsTrue();
			await Assert.That(LcdRenderSettings.EffectiveScaleAllowsLcd(LcdRenderSettings.MaxEffectiveScale)).IsTrue()
				.Because("the threshold itself is allowed - the reference refuses only what is greater");
			await Assert.That(LcdRenderSettings.EffectiveScaleAllowsLcd(Math.BitIncrement(LcdRenderSettings.MaxEffectiveScale))).IsFalse()
				.Because("one representable step past the cap is already past it");
			await Assert.That(LcdRenderSettings.EffectiveScaleAllowsLcd(2.0)).IsFalse();
			await Assert.That(LcdRenderSettings.EffectiveScaleAllowsLcd(double.NaN)).IsTrue()
				.Because("the gate is a negated > so NaN passes, as in Rust - and a NaN transform paints nothing on either path");

			// The scale is read off the transform's x basis vector, so a scaled sub-render is gated on its own
			// terms; rotation and translation leave it alone.
			await Assert.That(LcdRenderSettings.EffectiveScaleOf(Affine.NewIdentity())).IsEqualTo(1.0).Within(1e-12);
			await Assert.That(LcdRenderSettings.EffectiveScaleOf(Affine.NewScaling(1.25))).IsEqualTo(1.25).Within(1e-12);
			await Assert.That(LcdRenderSettings.EffectiveScaleOf(Affine.NewTranslation(7, 3))).IsEqualTo(1.0).Within(1e-12);
			await Assert.That(LcdRenderSettings.EffectiveScaleOf(Affine.NewRotation(0.7))).IsEqualTo(1.0).Within(1e-12);

			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				// At the cap the LCD raster is used, so the result must differ from the ordinary fill...
				Affine atCap = Affine.NewScaling(LcdRenderSettings.MaxEffectiveScale);
				await Assert.That(LcdFill(path, atCap).Equals(PlainRender(path, atCap), 0)).IsFalse();

				// ...and just past it the gate hands the fill straight to the ordinary path, overriding the
				// toggle that is still on.
				Affine pastCap = Affine.NewScaling(1.2501);
				await Assert.That(LcdFill(path, pastCap).Equals(PlainRender(path, pastCap), 0)).IsTrue();
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The destination's clip bounds the composite: nothing lands outside it, and what lands inside it is
		/// what the pipeline produces with that same clip. <see cref="Graphics2D.RenderLcd"/> reads the clip
		/// from the destination the way every other fill does, so a widget painting inside a clipping parent
		/// cannot paint over its siblings.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ClipBoundsTheComposite()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);
				var clip = new RectangleDouble(8, 0, 16, BufferHeight);

				ImageBuffer painted = OpaqueWhite();
				Graphics2D graphics = painted.NewGraphics2D();
				graphics.SetClippingRect(clip);
				graphics.RenderLcd(path, Color.Black);

				// Left of the clip the fill would otherwise have covered x = 5..7, and above / right of it
				// x = 16..19; all of that must be untouched white.
				for (int y = 0; y < BufferHeight; y++)
				{
					for (int x = 0; x < BufferWidth; x++)
					{
						if (x >= clip.Left && x < clip.Right)
						{
							continue;
						}

						Color pixel = painted.GetPixel(x, y);
						await Assert.That(pixel.red).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the clip");
						await Assert.That(pixel.green).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the clip");
						await Assert.That(pixel.blue).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the clip");
					}
				}

				// Inside the clip it is the ordinary pipeline output, and it did paint.
				ImageBuffer expected = OpaqueWhite();
				BoundedMaskBuilder.TryBuild(
					BufferWidth, BufferHeight, path, Affine.NewIdentity(), out LcdMask mask, out int originX, out int originY, clip);
				LcdComposite.Composite(expected, mask, Color.Black, originX, originY);
				await Assert.That(painted.Equals(expected, 0)).IsTrue();
				await Assert.That(painted.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the clip must not have swallowed the whole fill");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The clip is <b>whole-pixel granular</b> on this path: a clip edge falling inside a pixel snaps to a
		/// pixel boundary, so the partially clipped pixel is either painted in full or dropped in full, where
		/// <see cref="Graphics2D.Render"/> anti-aliases it down to the covered fraction. Both directions are
		/// pinned here, because both are visible: a fractional left edge admits a whole extra pixel of ink, a
		/// fractional right edge drops one.
		/// </summary>
		/// <remarks>
		/// <b>This pins reference-faithful behaviour, not a bug.</b> The mask pipeline has no unit finer than a
		/// pixel to enforce a clip in - <see cref="BoundedMaskBuilder"/> rounds outward the way the reference's
		/// <c>rect_to_pixel_clip</c> does - and by the time a clip reaches it through
		/// <see cref="Graphics2D.GetClippingRect"/> it has already been truncated to whole pixels by the
		/// rasterizer's 24.8 clip box (see the remarks on <see cref="Graphics2D.RenderLcd"/>), which is what
		/// makes both edges floor. Callers needing an exact fractional clip have to snap it themselves or stay
		/// off <see cref="Graphics2D.RenderLcd"/>; if this test ever fails because a clip edge became
		/// anti-aliased, that is a change of contract to make deliberately, not a fix.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task FractionalClipEdgeSnapsToWholePixelsUnlikeRender()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				// Both x edges land mid-pixel, and the fill covers columns 8 and 16 completely, so the only
				// thing deciding their ink is how the clip is rounded.
				var clip = new RectangleDouble(8.5, 0, 16.5, BufferHeight);

				ImageBuffer lcd = OpaqueWhite();
				Graphics2D lcdGraphics = lcd.NewGraphics2D();
				lcdGraphics.SetClippingRect(clip);
				lcdGraphics.RenderLcd(path, Color.Black);

				ImageBuffer plain = OpaqueWhite();
				Graphics2D plainGraphics = plain.NewGraphics2D();
				plainGraphics.SetClippingRect(clip);
				plainGraphics.Render(path, Color.Black);

				// The row is well inside the fill vertically, so only the x clip is in play.
				const int Row = 6;

				// Column 8 is the left clip edge, and it keeps its ink: blue is the subpixel furthest from the
				// mask's left border, so the 5-tap filter sees full coverage there and paints it solid.
				await Assert.That(lcd.GetPixel(8, Row).blue).IsEqualTo((byte)0)
					.Because("the clip snapped down to 8, so column 8 takes the fill at full strength");
				await Assert.That(plain.GetPixel(8, Row).blue).IsGreaterThan((byte)0)
					.Because("Render clips at 1/256 of a pixel, so column 8 is only half covered");

				// Column 16 is the right clip edge, and it loses its ink entirely for the same reason - the
				// snap is a floor on both sides, so on the right it takes a pixel away instead of adding one.
				Color lcdAtRightEdge = lcd.GetPixel(16, Row);
				await Assert.That(lcdAtRightEdge.red).IsEqualTo((byte)255)
					.Because("the clip snapped down to 16, so column 16 is outside it and stays white");
				await Assert.That(lcdAtRightEdge.green).IsEqualTo((byte)255);
				await Assert.That(lcdAtRightEdge.blue).IsEqualTo((byte)255);
				await Assert.That(plain.GetPixel(16, Row).red).IsLessThan((byte)255)
					.Because("Render half covers column 16, which is the pixel this path gives up");

				// The divergence is bounded to those edge pixels: nothing outside the snapped clip is touched.
				for (int y = 0; y < BufferHeight; y++)
				{
					for (int x = 0; x < BufferWidth; x++)
					{
						if (x >= 8 && x < 16)
						{
							continue;
						}

						Color pixel = lcd.GetPixel(x, y);
						await Assert.That(pixel.red).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the snapped clip");
						await Assert.That(pixel.green).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the snapped clip");
						await Assert.That(pixel.blue).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the snapped clip");
					}
				}
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// A destination that can composite a mask but must not carry subpixel chroma takes the gray arm of
		/// the <b>same</b> bounded-mask, cache and composite path: chroma-free ink, identical geometry, and a
		/// cache entry of its own. Mirrors the reference's validity gate
		/// (<c>text_render.rs:56-62</c>), which switches the collapse rather than the pipeline.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ChromaFreeDestinationTakesTheGrayArmOfTheSamePipeline()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				LcdMaskCache.Clear();
				VertexStorage path = Rectangle(FillRect);
				object identity = "ChromaFreeDestinationTakesTheGrayArm";

				// The LCD arm, for contrast: same geometry, same identity, and its ink has chroma.
				long beforeLcd = LcdMaskCache.BuildCount;
				ImageBuffer lcd = OpaqueWhite();
				lcd.NewGraphics2D().RenderLcd(path, Color.Black, identity);
				await Assert.That(LcdMaskCache.BuildCount - beforeLcd).IsEqualTo(1L);
				await Assert.That(HasChroma(lcd)).IsTrue()
					.Because("the LCD arm is the one that paints per-channel coverage");

				// The gray arm. The same path identity under a destination that refuses chroma has to miss -
				// the gray flag is part of the key - and paint neutral ink.
				long beforeGray = LcdMaskCache.BuildCount;
				ImageBuffer gray = OpaqueWhite();
				var grayGraphics = new ChromaFreeGraphics2D(gray);
				await Assert.That(grayGraphics.CanCompositeLcd).IsTrue()
					.Because("chroma validity is a separate question from whether a mask can be composited at all");
				grayGraphics.RenderLcd(path, Color.Black, identity);
				await Assert.That(LcdMaskCache.BuildCount - beforeGray).IsEqualTo(1L)
					.Because("flipping chroma-allowed must miss the cache, not serve the LCD mask");
				await Assert.That(HasChroma(gray)).IsFalse()
					.Because("the gray collapse produces r == g == b everywhere");
				await Assert.That(gray.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the gray arm still has to paint");

				// And it is the gray mask specifically, composited at the builder's origin.
				ImageBuffer expected = OpaqueWhite();
				bool built = BoundedMaskBuilder.TryBuild(
					BufferWidth,
					BufferHeight,
					path,
					Affine.NewIdentity(),
					out LcdMask mask,
					out int originX,
					out int originY,
					grayGraphics.GetClippingRect(),
					filling_rule_e.fill_non_zero,
					LcdFilter.DefaultPrimaryWeight,
					LcdFilter.DefaultGamma,
					gray: true);
				await Assert.That(built).IsTrue();
				LcdComposite.Composite(expected, mask, Color.Black, originX, originY);
				await Assert.That(gray.Equals(expected, 0)).IsTrue()
					.Because("the gray arm must be BoundedMaskBuilder's gray output and nothing else");

				// The gray mask is then cached under its own key, so repainting does not re-rasterize.
				long beforeHit = LcdMaskCache.BuildCount;
				new ChromaFreeGraphics2D(OpaqueWhite()).RenderLcd(path, Color.Black, identity);
				await Assert.That(LcdMaskCache.BuildCount - beforeHit).IsEqualTo(0L)
					.Because("the second chroma-free draw must hit the gray entry");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The mask composite only recognizes a 32 bit <see cref="ImageBuffer"/> behind
		/// <see cref="ImageClippingProxy"/> wrappers. Every other <see cref="ImageProxy"/> exists to
		/// reinterpret the byte writes <see cref="LcdComposite"/> would make, so such a destination reports
		/// <see cref="Graphics2D.CanCompositeLcd"/> false and gets the ordinary fill through the proxy.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ProxyThatReinterpretsWritesFallsBack()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				// An alpha-mask proxy is the case with teeth: writing bytes straight to the buffer behind it
				// would skip the mask the proxy exists to apply, and nothing about the write would look wrong.
				ImageBuffer throughLcd = OpaqueWhite();
				Graphics2D lcdGraphics = MaskedGraphics(throughLcd);
				await Assert.That(lcdGraphics.CanCompositeLcd).IsFalse()
					.Because("a direct byte write behind an alpha-mask proxy would silently bypass the mask");
				lcdGraphics.RenderLcd(path, Color.Black);

				ImageBuffer throughRender = OpaqueWhite();
				MaskedGraphics(throughRender).Render(path, Color.Black);

				await Assert.That(throughLcd.Equals(throughRender, 0)).IsTrue()
					.Because("the fallback has to be the ordinary fill, byte for byte");
				await Assert.That(throughLcd.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the fallback has to actually render");

				// A transposer is the geometric case - it swaps the axes, so a direct write would land in the
				// wrong pixel. It carries only the capability half of this test: it cannot render at all,
				// because the blend_vline its blend_hline forwards to is unimplemented on ImageBuffer.
				await Assert.That(TransposedGraphics(OpaqueWhite()).CanCompositeLcd).IsFalse()
					.Because("a FormatTransposer destination must refuse the mask pipeline too");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The invariant the bbox-sized mask design depends on, now end to end: translating a fill by whole
		/// pixels moves the painted pixels and changes <b>no coverage byte</b>, because the mask origin is
		/// always whole pixels and a whole-pixel shift moves the path by a multiple of 3 subpixels - a
		/// translation the 5-tap kernel is invariant under. A sub-pixel translation is the opposite case: it
		/// changes each channel's phase, so the bytes must differ.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task WholePixelTranslationShiftsWithoutChangingCoverage()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				// Mask level: identical bytes for the whole-pixel shift, different bytes for the sub-pixel one.
				byte[] atOrigin = MaskBytes(path, Affine.NewIdentity(), out int originX, out int originY);
				byte[] shifted = MaskBytes(path, Affine.NewTranslation(3, 2), out int shiftedX, out int shiftedY);
				await Assert.That(shiftedX).IsEqualTo(originX + 3);
				await Assert.That(shiftedY).IsEqualTo(originY + 2);
				await AssertBytesEqual(atOrigin, shifted, "whole-pixel shifted mask");

				byte[] phaseShifted = MaskBytes(path, Affine.NewTranslation(0.5, 0), out _, out _);
				await Assert.That(phaseShifted.Length).IsEqualTo(atOrigin.Length)
					.Because("half a pixel does not change the padded bbox size here");
				await Assert.That(BytesEqual(atOrigin, phaseShifted)).IsFalse()
					.Because("a sub-pixel translation changes each channel's phase");

				// Pixel level: the same fill, three pixels right and two up, reads back identically.
				ImageBuffer paintedAtOrigin = LcdFill(path, Affine.NewIdentity());
				ImageBuffer paintedShifted = LcdFill(path, Affine.NewTranslation(3, 2));
				for (int y = 0; y + 2 < BufferHeight; y++)
				{
					for (int x = 0; x + 3 < BufferWidth; x++)
					{
						Color left = paintedAtOrigin.GetPixel(x, y);
						Color right = paintedShifted.GetPixel(x + 3, y + 2);
						await Assert.That(right.red).IsEqualTo(left.red).Because($"red at ({x}, {y})");
						await Assert.That(right.green).IsEqualTo(left.green).Because($"green at ({x}, {y})");
						await Assert.That(right.blue).IsEqualTo(left.blue).Because($"blue at ({x}, {y})");
					}
				}
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>An opaque white 32 bit destination - the only kind LCD subpixel geometry is valid against.</summary>
		private static ImageBuffer OpaqueWhite()
		{
			var image = new ImageBuffer(BufferWidth, BufferHeight, 32, new BlenderPreMultBGRA());
			image.NewGraphics2D().Clear(new Color(255, 255, 255, 255));

			return image;
		}

		/// <summary><paramref name="path"/> filled black through <see cref="Graphics2D.RenderLcd"/>.</summary>
		private static ImageBuffer LcdFill(IVertexSource path, Affine transform)
		{
			ImageBuffer image = OpaqueWhite();
			Graphics2D graphics = image.NewGraphics2D();
			graphics.SetTransform(transform);
			graphics.RenderLcd(path, Color.Black);

			return image;
		}

		/// <summary><paramref name="path"/> filled black through the ordinary scanline path.</summary>
		private static ImageBuffer PlainRender(IVertexSource path, Affine transform)
		{
			ImageBuffer image = OpaqueWhite();
			Graphics2D graphics = image.NewGraphics2D();
			graphics.SetTransform(transform);
			graphics.Render(path, Color.Black);

			return image;
		}

		private static byte[] MaskBytes(IVertexSource path, Affine transform, out int originX, out int originY)
		{
			BoundedMaskBuilder.TryBuild(
				BufferWidth,
				BufferHeight,
				path,
				transform,
				out LcdMask mask,
				out originX,
				out originY,
				new RectangleDouble(0, 0, BufferWidth, BufferHeight));

			return mask.Data;
		}

		private static bool BytesEqual(byte[] expected, byte[] actual)
		{
			if (expected.Length != actual.Length)
			{
				return false;
			}

			for (int i = 0; i < expected.Length; i++)
			{
				if (expected[i] != actual[i])
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Element-wise, so the comparison reports which byte moved.</summary>
		private static async Task AssertBytesEqual(byte[] expected, byte[] actual, string what)
		{
			await Assert.That(actual.Length).IsEqualTo(expected.Length).Because($"length of {what}");
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(actual[i]).IsEqualTo(expected[i]).Because($"byte {i} of {what}");
			}
		}

		/// <summary>
		/// Whether any pixel's channels differ from each other - the defining mark of subpixel coverage, and
		/// what the gray collapse must not produce. A gray-on-gray fill is the only thing this could false
		/// positive on, and both fills here are black on white.
		/// </summary>
		private static bool HasChroma(ImageBuffer image)
		{
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (pixel.red != pixel.green || pixel.green != pixel.blue)
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// A destination whose outermost wrapper is an <see cref="AlphaMaskAdaptor"/> over a fully opaque
		/// mask, so the mask itself changes nothing and the two fills stay comparable - what is being tested
		/// is that the proxy is in the chain at all, not what its mask does.
		/// </summary>
		private static Graphics2D MaskedGraphics(ImageBuffer destination)
		{
			var maskImage = new ImageBuffer(destination.Width, destination.Height, 8, new blender_gray(1));
			maskImage.NewGraphics2D().Clear(new Color(255, 255, 255, 255));

			var masked = new AlphaMaskAdaptor(new ImageClippingProxy(destination), new AlphaMaskByteUnclipped(maskImage, 1, 0));

			return NewGraphics2DOver(masked);
		}

		/// <summary>A destination whose outermost wrapper is a <see cref="FormatTransposer"/>.</summary>
		private static Graphics2D TransposedGraphics(ImageBuffer destination)
		{
			return NewGraphics2DOver(new FormatTransposer(new ImageClippingProxy(destination)));
		}

		/// <summary>
		/// An <see cref="ImageGraphics2D"/> over <paramref name="destination"/>, wired the way
		/// <see cref="ImageBuffer.NewGraphics2D"/> wires its own - including the clip box, which the LCD path
		/// reads back through <see cref="Graphics2D.GetClippingRect"/>.
		/// </summary>
		private static Graphics2D NewGraphics2DOver(IImageByte destination)
		{
			var rasterizer = new ScanlineRasterizer();
			rasterizer.SetVectorClipBox(0, 0, destination.Width, destination.Height);

			return new ImageGraphics2D(destination, rasterizer, new ScanlineCachePacked8());
		}

		/// <summary>An axis-aligned rectangle path.</summary>
		private static VertexStorage Rectangle(RectangleDouble rect)
		{
			var path = new VertexStorage();
			path.MoveTo(rect.Left, rect.Bottom);
			path.LineTo(rect.Right, rect.Bottom);
			path.LineTo(rect.Right, rect.Top);
			path.LineTo(rect.Left, rect.Top);
			path.ClosePolygon();

			return path;
		}

		/// <summary>
		/// An <see cref="ImageGraphics2D"/> that refuses subpixel chroma, standing in for the transparent
		/// backbuffer destination that will answer the same way. Built exactly as
		/// <see cref="ImageBuffer.NewGraphics2D"/> builds its own, so the only difference from the LCD arm is
		/// the one overridden gate.
		/// </summary>
		private class ChromaFreeGraphics2D : ImageGraphics2D
		{
			internal ChromaFreeGraphics2D(ImageBuffer destination)
			{
				var rasterizer = new ScanlineRasterizer();
				rasterizer.SetVectorClipBox(0, 0, destination.Width, destination.Height);
				Initialize(new ImageClippingProxy(destination), rasterizer);
				ScanlineCache = new ScanlineCachePacked8();
			}

			protected override bool LcdChromaAllowed => false;
		}
	}
}
