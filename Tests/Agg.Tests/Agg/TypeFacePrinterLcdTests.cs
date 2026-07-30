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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers text reaching the LCD subpixel pipeline the way everything else does - through the ordinary
	/// <see cref="Graphics2D.Render(IVertexSource, IColorType)"/>, because
	/// <see cref="TypeFacePrinter"/> names its own geometry (<see cref="IVertexSourceRenderIdentity"/>) and
	/// nothing more. The same outlines through the text path and through the general vector path produce the
	/// same bytes, one cached mask serves every whole-pixel placement, and every gate that refuses LCD hands
	/// the text back to the renderer it always had.
	/// </summary>
	/// <remarks>
	/// <b>The printer contains no LCD code at all</b>, which is what the last test here proves from the other
	/// end: an ordinary non-text vertex source that names itself gets exactly the same cached treatment.
	/// <para>
	/// The equivalence test is the port of the reference's
	/// <c>test_lcd_mask_builder_matches_legacy_text_path</c> and
	/// <c>test_lcd_buffer_fill_path_matches_text_pipeline_for_glyphs</c> (<c>lcd_coverage\tests.rs</c>) - the
	/// vector-level guarantee the whole design rests on, which in agg-sharp is expressed at the destination
	/// rather than at the mask because the two routes reach it through different entry points.
	/// </para>
	/// <para>
	/// <see cref="LcdRenderSettings"/>, <see cref="LcdMaskCache"/> and
	/// <see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/> are all process-wide, so every test here is
	/// <c>[NotInParallel]</c> and restores what it changed.
	/// </para>
	/// <para>
	/// Every run is placed well inside its destination horizontally. A mask trimmed at the buffer edge loses
	/// the coverage the 5-tap filter would have read from just outside it, so the trimmed (vector) and
	/// untrimmed (cached) routes are only byte-identical where the geometry is not hanging off the left or
	/// right edge - vertically it does not matter, since the filter has no vertical reach.
	/// </para>
	/// </remarks>
	public class TypeFacePrinterLcdTests
	{
		private const int BufferWidth = 80;

		private const int BufferHeight = 24;

		/// <summary>Ascender, x-height and a descender, so the run has ink above and below the baseline.</summary>
		private const string SampleText = "Hxy";

		/// <summary>
		/// The headline guarantee: text through <see cref="TypeFacePrinter.Render(Graphics2D, Color)"/> and the
		/// same outlines through <see cref="Graphics2D.RenderLcd"/> land on the destination byte for byte. The
		/// text path is a caller of the vector pipeline - if these ever diverge, it has started being its own
		/// renderer.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task LcdTextIsTheVectorPipelineOnTheSameOutlines()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = true;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;

				// A whole-pixel origin under an identity transform, so the baseline snap moves nothing and the
				// printer's own vertices are exactly what the vector route has to rasterize.
				ImageBuffer viaText = RenderText(Printer(SampleText, new Vector2(6, 12)));

				ImageBuffer viaVectors = OpaqueWhite();
				viaVectors.NewGraphics2D().RenderLcd(Printer(SampleText, new Vector2(6, 12)), Color.Black);

				await Assert.That(viaText.Equals(viaVectors, 0)).IsTrue()
					.Because("the text path must be the vector pipeline and nothing else");

				// Negative controls: it painted, it carries subpixel chroma, and it is genuinely a different
				// raster from the ordinary anti-aliased text - without which the fallback tests are vacuous.
				await Assert.That(viaText.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("something has to have been painted");
				await Assert.That(HasChroma(viaText)).IsTrue()
					.Because("LCD text is per-channel coverage");
				await Assert.That(viaText.Equals(LegacyText(SampleText, new Vector2(6, 12)), 0)).IsFalse()
					.Because("the LCD raster must differ from the ordinary anti-aliased one");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The backward-compatibility contract: with the toggle off, text is byte for byte what the ordinary
		/// renderer produces. agg-sharp's existing pixel-exact expectations - screenshots, image export, this
		/// test suite - only survive because the LCD path is opt-in all the way down to the fill chokepoint.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task DisabledRendersTheOrdinaryBytes()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = false;

				ImageBuffer painted = RenderText(Printer(SampleText, new Vector2(6, 12)));
				ImageBuffer legacy = LegacyText(SampleText, new Vector2(6, 12));

				await Assert.That(painted.Equals(legacy, 0)).IsTrue()
					.Because("with LCD off the text path must be untouched");
				await Assert.That(painted.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the ordinary path has to actually render");
				await Assert.That(HasChroma(painted)).IsFalse()
					.Because("black on white through the ordinary path is neutral gray");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// Position-independent caching, the property that makes a text mask worth caching at all: the same run
		/// a whole number of device pixels away reuses the mask it already built and lands shifted by exactly
		/// that many pixels, while a fractional move is a different raster and rasterizes again.
		/// </summary>
		/// <remarks>
		/// The move has to be a <b>transform</b> translation, which is how a widget places its text: the
		/// printer's own <see cref="TypeFacePrinter.Origin"/> is baked into the vertices it emits, so moving
		/// that is changing the geometry rather than moving it - see
		/// <see cref="OriginIsPartOfTheGeometryNotThePlacement"/>.
		/// <para>
		/// <see cref="LcdMaskCache.BuildCount"/> is what makes the hit observable - two draws that agree prove
		/// nothing on their own, because rebuilding would also agree.
		/// </para>
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task WholePixelPlacementSharesOneMaskAndAFractionDoesNot()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = true;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				LcdMaskCache.Clear();

				long beforeFirst = LcdMaskCache.BuildCount;
				ImageBuffer atOrigin = RenderText(Printer(SampleText, new Vector2(6, 12)));
				await Assert.That(LcdMaskCache.BuildCount - beforeFirst).IsEqualTo(1L)
					.Because("the first draw of a run has to rasterize it");

				long beforeShifted = LcdMaskCache.BuildCount;
				ImageBuffer shiftedFive = RenderText(Printer(SampleText, new Vector2(6, 12)), Affine.NewTranslation(5, 0));
				await Assert.That(LcdMaskCache.BuildCount - beforeShifted).IsEqualTo(0L)
					.Because("five whole pixels to the right is the same raster at a different origin");

				for (int y = 0; y < BufferHeight; y++)
				{
					for (int x = 0; x + 5 < BufferWidth; x++)
					{
						Color left = atOrigin.GetPixel(x, y);
						Color right = shiftedFive.GetPixel(x + 5, y);
						await Assert.That(right.red).IsEqualTo(left.red).Because($"red at ({x}, {y})");
						await Assert.That(right.green).IsEqualTo(left.green).Because($"green at ({x}, {y})");
						await Assert.That(right.blue).IsEqualTo(left.blue).Because($"blue at ({x}, {y})");
					}
				}

				long beforeFraction = LcdMaskCache.BuildCount;
				ImageBuffer shiftedFraction = RenderText(Printer(SampleText, new Vector2(6, 12)), Affine.NewTranslation(5.5, 0));
				await Assert.That(LcdMaskCache.BuildCount - beforeFraction).IsEqualTo(1L)
					.Because("half a pixel changes each channel's phase, so it is a different mask");
				await Assert.That(shiftedFraction.Equals(shiftedFive, 0)).IsFalse()
					.Because("and it has to actually look different");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// Baseline snapping puts every baseline on a whole <i>device</i> pixel, so with it on the Y half of a
		/// placement is always whole and every Y position of a run collapses onto one cached mask. With it off
		/// the true fraction is rasterized into the mask instead, and each distinct fraction is its own raster.
		/// </summary>
		/// <remarks>
		/// This is the documented interaction between the two features: the snap does for Y, for free and only
		/// when the user asked for it, what the reference's unconditional <c>sy.round()</c> does. It works
		/// through the generic path with no special case anywhere - the printer answers a fractional device Y
		/// by handing <see cref="Graphics2D.Render(IVertexSource, IColorType)"/> its vertices wrapped in the
		/// nudge that cancels the fraction, and a wrapper's translation is placement, so the two draws are one
		/// identity at one phase.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task BaselineSnappingCollapsesEveryYFractionOntoOneMask()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = true;

				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				LcdMaskCache.Clear();
				ImageBuffer snappedAtWhole = RenderText(Printer(SampleText, new Vector2(6, 12)));

				long beforeSnappedFraction = LcdMaskCache.BuildCount;
				ImageBuffer snappedAtFraction = RenderText(Printer(SampleText, new Vector2(6, 12)), Affine.NewTranslation(0, 0.4));
				await Assert.That(LcdMaskCache.BuildCount - beforeSnappedFraction).IsEqualTo(0L)
					.Because("the snap makes a fractional device Y land on the same whole pixel, so it is the same mask");
				await Assert.That(snappedAtFraction.Equals(snappedAtWhole, 0)).IsTrue()
					.Because("and it must be composited at the same place");

				TypeFacePrinter.SnapBaselinesToWholePixels = false;
				LcdMaskCache.Clear();
				ImageBuffer rawAtWhole = RenderText(Printer(SampleText, new Vector2(6, 12)));
				long beforeRawFraction = LcdMaskCache.BuildCount;
				ImageBuffer rawAtFraction = RenderText(Printer(SampleText, new Vector2(6, 12)), Affine.NewTranslation(0, 0.4));
				await Assert.That(LcdMaskCache.BuildCount - beforeRawFraction).IsEqualTo(1L)
					.Because("with no snap the Y fraction is part of the raster");
				await Assert.That(rawAtFraction.Equals(rawAtWhole, 0)).IsFalse()
					.Because("and it has to actually move the text");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// <see cref="TypeFacePrinter.Origin"/> is part of the identity, because
		/// <see cref="TypeFacePrinter.Vertices"/> bakes it into the positions it emits: two origins are two
		/// shapes, not one shape at two places, and the identity contract is about the vertices.
		/// </summary>
		/// <remarks>
		/// Multi-line text is the case where nothing else would do. The per-line baseline snap rounds
		/// <c>lineOffset + Origin.Y</c>, so a fractional origin can change the line <i>spacing</i> and not just
		/// the position - and this is what would happen if the origin ever stopped being part of the identity:
		/// the second draw would be served the first draw's mask and paint the wrong spacing.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task OriginIsPartOfTheGeometryNotThePlacement()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = true;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				LcdMaskCache.Clear();

				// 9.45pt is a 12.6 pixel em, so the second baseline is a fraction away from the first and the
				// per-line snap has something to round.
				var typeFaceStyle = new StyledTypeFace(AggContext.DefaultFont, 9.45);

				RenderText(new TypeFacePrinter("Hx\nHx", typeFaceStyle, new Vector2(6, 20)));

				long beforeFraction = LcdMaskCache.BuildCount;
				ImageBuffer atFraction = RenderText(new TypeFacePrinter("Hx\nHx", typeFaceStyle, new Vector2(6, 20.4)));
				await Assert.That(LcdMaskCache.BuildCount - beforeFraction).IsEqualTo(1L)
					.Because("a run at a different origin emits different vertices, so it cannot share the mask");

				// The proof that the entry it built is the right one: the vector route rasterizes this
				// printer's actual vertices, with no identity and no cache to get wrong.
				ImageBuffer viaVectors = OpaqueWhite();
				viaVectors.NewGraphics2D().RenderLcd(new TypeFacePrinter("Hx\nHx", typeFaceStyle, new Vector2(6, 20.4)), Color.Black);
				await Assert.That(atFraction.Equals(viaVectors, 0)).IsTrue()
					.Because("the cached mask must be this run's own raster");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// A destination that can composite a mask but must not carry subpixel chroma takes the gray arm of the
		/// same path: neutral ink, same pipeline, its own cache entry. The reference's validity gate
		/// (<c>text_render.rs:56-62</c>) switches the collapse, not the renderer.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ChromaFreeDestinationTakesTheGrayArm()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;

				ImageBuffer gray = OpaqueWhite();
				Printer(SampleText, new Vector2(6, 12)).Render(new ChromaFreeGraphics2D(gray), Color.Black);

				await Assert.That(gray.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the gray arm still has to paint");
				await Assert.That(HasChroma(gray)).IsFalse()
					.Because("the gray collapse produces r == g == b everywhere");
				await Assert.That(gray.Equals(LegacyText(SampleText, new Vector2(6, 12)), 0)).IsFalse()
					.Because("it is the mask pipeline's gray collapse, not the ordinary scanline fill");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// Past the effective-scale cap the text goes back to the ordinary renderer, byte for byte - the cap
		/// overrides the toggle, and refusing LCD must never mean refusing to paint.
		/// </summary>
		/// <remarks>
		/// The comparison is against <see cref="LegacyText"/>, which forces the toggle <i>off</i>, so the two
		/// sides genuinely take different code when the cap works and the same code when it does not: comparing
		/// a toggled-on draw against another toggled-on draw would pass whether the cap existed or not. The
		/// chroma control catches the same thing from the other side.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task PastTheScaleCapFallsBackToTheOrdinaryText()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				var pastCap = Affine.NewScaling(1.2501);

				ImageBuffer painted = RenderText(Printer(SampleText, new Vector2(6, 12)), pastCap);
				ImageBuffer legacy = LegacyText(SampleText, new Vector2(6, 12), pastCap);

				await Assert.That(painted.Equals(legacy, 0)).IsTrue()
					.Because("past the cap the text path must be the ordinary one");
				await Assert.That(painted.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the fallback has to actually render");
				await Assert.That(HasChroma(painted)).IsFalse()
					.Because("nothing past the cap may have gone through the subpixel filter");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The destination's clip bounds LCD text. It has to be enforced at composite time here rather than by
		/// trimming the mask, because a trimmed mask could not be shared across positions - so this is the test
		/// that a cached text mask still cannot paint over its widget's siblings.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ClipBoundsTheLcdText()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				LcdMaskCache.Clear();

				const int ClipRight = 16;
				ImageBuffer painted = OpaqueWhite();
				Graphics2D graphics = painted.NewGraphics2D();
				graphics.SetClippingRect(new RectangleDouble(0, 0, ClipRight, BufferHeight));
				Printer(SampleText, new Vector2(6, 12)).Render(graphics, Color.Black);

				for (int y = 0; y < BufferHeight; y++)
				{
					for (int x = ClipRight; x < BufferWidth; x++)
					{
						Color pixel = painted.GetPixel(x, y);
						await Assert.That(pixel.red).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the clip");
						await Assert.That(pixel.green).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the clip");
						await Assert.That(pixel.blue).IsEqualTo((byte)255).Because($"pixel ({x}, {y}) is outside the clip");
					}
				}

				await Assert.That(painted.Equals(OpaqueWhite(), 0)).IsFalse()
					.Because("the clip must not have swallowed the whole run");

				// The unclipped run is the same mask - the clip narrows where it is composited, it does not
				// change what was rasterized.
				long beforeUnclipped = LcdMaskCache.BuildCount;
				RenderText(Printer(SampleText, new Vector2(6, 12)));
				await Assert.That(LcdMaskCache.BuildCount - beforeUnclipped).IsEqualTo(0L)
					.Because("the clip must not be part of the cached raster");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The point of the whole arrangement: there is nothing about text in it. A plain non-text vertex
		/// source that names its own geometry gets the same cached LCD treatment through the same
		/// <see cref="Graphics2D.Render(IVertexSource, IColorType)"/> - the same raster as the vector route,
		/// one mask shared across whole-pixel placements, and no LCD code of its own.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AnyIdentifiedSourceGetsTheSameCachedTreatment()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				LcdMaskCache.Clear();

				// Fractional edges on purpose: a whole-pixel rect has no partial coverage, and therefore no
				// channel variation to tell an LCD raster from an ordinary anti-aliased one.
				var shape = new IdentifiedRectangle(new RectangleDouble(6.3, 4.2, 21.4, 11.6));

				long beforeFirst = LcdMaskCache.BuildCount;
				ImageBuffer painted = RenderShape(shape, Affine.NewIdentity());
				await Assert.That(LcdMaskCache.BuildCount - beforeFirst).IsEqualTo(1L)
					.Because("the first draw of an identified shape has to rasterize it");
				await Assert.That(HasChroma(painted)).IsTrue()
					.Because("an ordinary Render of an identified source is an LCD raster now");

				ImageBuffer viaVectors = OpaqueWhite();
				viaVectors.NewGraphics2D().RenderLcd(shape, Color.Black);
				await Assert.That(painted.Equals(viaVectors, 0)).IsTrue()
					.Because("the cached path must be the same pipeline the explicit entry runs");

				long beforeShifted = LcdMaskCache.BuildCount;
				ImageBuffer shiftedSeven = RenderShape(shape, Affine.NewTranslation(7, 0));
				await Assert.That(LcdMaskCache.BuildCount - beforeShifted).IsEqualTo(0L)
					.Because("seven whole pixels away is the same raster at a different origin");

				for (int y = 0; y < BufferHeight; y++)
				{
					for (int x = 0; x + 7 < BufferWidth; x++)
					{
						Color left = painted.GetPixel(x, y);
						Color right = shiftedSeven.GetPixel(x + 7, y);
						await Assert.That(right.red).IsEqualTo(left.red).Because($"red at ({x}, {y})");
						await Assert.That(right.green).IsEqualTo(left.green).Because($"green at ({x}, {y})");
						await Assert.That(right.blue).IsEqualTo(left.blue).Because($"blue at ({x}, {y})");
					}
				}

				// And with the toggle off it is the fill it always was - the same backward-compatibility
				// contract text gets.
				LcdRenderSettings.Enabled = false;
				ImageBuffer ordinary = RenderShape(shape, Affine.NewIdentity());
				await Assert.That(HasChroma(ordinary)).IsFalse()
					.Because("with LCD off an identified source is an ordinary anti-aliased fill");
				await Assert.That(ordinary.Equals(painted, 0)).IsFalse()
					.Because("which is a different raster from the LCD one");
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

		private static TypeFacePrinter Printer(string text, Vector2 origin)
		{
			return new TypeFacePrinter(text, new StyledTypeFace(AggContext.DefaultFont, 12), origin);
		}

		/// <summary><paramref name="printer"/> painted black through <see cref="TypeFacePrinter.Render(Graphics2D, Color)"/>.</summary>
		private static ImageBuffer RenderText(TypeFacePrinter printer, Affine? transform = null)
		{
			ImageBuffer image = OpaqueWhite();
			Graphics2D graphics = image.NewGraphics2D();
			if (transform != null)
			{
				graphics.SetTransform(transform.Value);
			}

			printer.Render(graphics, Color.Black);

			return image;
		}

		/// <summary><paramref name="source"/> filled black through the ordinary vector chokepoint.</summary>
		private static ImageBuffer RenderShape(IVertexSource source, Affine transform)
		{
			ImageBuffer image = OpaqueWhite();
			Graphics2D graphics = image.NewGraphics2D();
			graphics.SetTransform(transform);
			graphics.Render(source, Color.Black);

			return image;
		}

		/// <summary>
		/// The same run through the ordinary scanline renderer, with the toggle held off so it cannot take the
		/// LCD path - which is the whole point of it: this is what the text looked like before any of this
		/// existed. Under an identity transform the device baseline nudge is zero, so the printer's own
		/// vertices are what <see cref="TypeFacePrinter.Render(Graphics2D, Color)"/> would have handed it - as
		/// they are under any <paramref name="transform"/> that scales, since the nudge only applies at 1:1.
		/// </summary>
		private static ImageBuffer LegacyText(string text, Vector2 origin, Affine? transform = null)
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = false;

				ImageBuffer image = OpaqueWhite();
				Graphics2D graphics = image.NewGraphics2D();
				if (transform != null)
				{
					graphics.SetTransform(transform.Value);
				}

				graphics.Render(Printer(text, origin), Color.Black);

				return image;
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// Whether any pixel's channels differ from each other - the defining mark of subpixel coverage, and
		/// what the gray collapse must not produce. Every fill here is black on white, so nothing else could
		/// introduce a channel difference.
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
		/// A rectangle that can name itself: the rect <b>is</b> the whole of what it emits, so it is exactly
		/// the identity <see cref="IVertexSourceRenderIdentity"/> asks for. Nothing here knows about LCD, which
		/// is the point.
		/// </summary>
		private class IdentifiedRectangle : VertexSourceLegacySupport, IVertexSourceRenderIdentity
		{
			private readonly RectangleDouble rect;

			private readonly VertexStorage path;

			internal IdentifiedRectangle(RectangleDouble rect)
			{
				this.rect = rect;

				this.path = new VertexStorage();
				this.path.MoveTo(rect.Left, rect.Bottom);
				this.path.LineTo(rect.Right, rect.Bottom);
				this.path.LineTo(rect.Right, rect.Top);
				this.path.LineTo(rect.Left, rect.Top);
				this.path.ClosePolygon();
			}

			public object RenderIdentity => this.rect;

			public override IEnumerable<VertexData> Vertices()
			{
				return this.path.Vertices();
			}
		}

		/// <summary>
		/// An <see cref="ImageGraphics2D"/> that refuses subpixel chroma, standing in for the transparent
		/// backbuffer destination that will answer the same way. Built exactly as
		/// <see cref="ImageBuffer.NewGraphics2D"/> builds its own, so the only difference is the one overridden
		/// gate.
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
