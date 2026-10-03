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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// SVG icons loaded through <see cref="StaticDataBase.LoadIcon(string, int, int, bool, Func{ImageBuffer, ValueTuple{ImageBuffer, string}})"/>
	/// draw with LCD subpixel coverage, as text does, when they land 1:1 on whole pixels of a destination that
	/// takes an LCD buffer - and exactly as before everywhere else.
	/// </summary>
	/// <remarks>
	/// <see cref="LcdRenderSettings"/> is process-wide, so every test that draws is <c>[NotInParallel]</c> and
	/// restores it, as <c>Graphics2DLcdTests</c> does. The icon is asymmetric on both axes (an L with a slanted
	/// foot, in the top left), so a flipped or shifted composite cannot match the plain blit by accident.
	/// </remarks>
	public class SvgIconLcdTests
	{
		private const int IconSize = 32;

		private const int CanvasSize = 48;

		/// <summary>
		/// The largest per-pixel luminance difference the LCD composite may have from the plain blit: the
		/// filter moves up to a third of an edge pixel's ink a subpixel sideways, which is well under this, and
		/// a flip or a shift of even one pixel puts a full 255 somewhere along an edge.
		/// </summary>
		private const int LuminanceTolerance = 96;

		/// <summary>An L whose foot slants, in the top left of the viewBox: no axis of symmetry.</summary>
		private const string Corner = "<path d=\"M8 8 H44 V52 H70 L86 70 H8 Z\" fill=\"#000\"/>";

		[Test]
		[NotInParallel]
		public async Task BlackIconOnWhiteShowsChromaAtItsEdges()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);

				ImageBuffer canvas = Opaque(Color.White);
				canvas.NewGraphics2D().Render(icon, 8, 8);

				await Assert.That(CountChromaPixels(canvas)).IsGreaterThan(0)
					.Because("an icon drawn 1:1 on whole pixels takes the LCD path, so its edges carry R != B");
				await Assert.That(canvas.GetPixel(8 + 4, 8 + 24)).IsEqualTo(Color.Black)
					.Because("the L's fully covered upright is the icon's own colour");
			});
		}

		[Test]
		[NotInParallel]
		public async Task InvertedIconOnBlackShowsChromaAndAWhiteInterior()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: true);

				ImageBuffer canvas = Opaque(Color.Black);
				canvas.NewGraphics2D().Render(icon, 8, 8);

				await Assert.That(CountChromaPixels(canvas)).IsGreaterThan(0)
					.Because("the colour comes from the image at draw time, so a dark-theme inversion keeps the subpixel coverage");
				await Assert.That(canvas.GetPixel(8 + 4, 8 + 24)).IsEqualTo(Color.White);
			});
		}

		/// <summary>
		/// The LCD composite lands where the plain blit does - same footprint, same ink per pixel give or take
		/// the subpixel spread - whether the placement comes from the position, the graphics transform's
		/// translation or the image's hotspot.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(8, 8, 0, 0, 0, 0)]
		[Arguments(3, 4, 5, 3, 0, 0)]
		[Arguments(10, 11, 0, 0, 2, 3)]
		public async Task TheLcdCompositeLandsWhereThePlainBlitDoes(int x, int y, int translateX, int translateY, int hotspotX, int hotspotY)
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);
				icon.OriginOffset = new Vector2(hotspotX, hotspotY);
				ImageBuffer plain = WithoutCoverage(icon);

				ImageBuffer lcd = Opaque(Color.White);
				Graphics2D lcdGraphics = lcd.NewGraphics2D();
				lcdGraphics.SetTransform(Affine.NewTranslation(translateX, translateY));
				lcdGraphics.Render(icon, x, y);

				ImageBuffer expected = Opaque(Color.White);
				Graphics2D expectedGraphics = expected.NewGraphics2D();
				expectedGraphics.SetTransform(Affine.NewTranslation(translateX, translateY));
				expectedGraphics.Render(plain, x, y);

				await Assert.That(CountChromaPixels(lcd)).IsGreaterThan(0)
					.Because("the composite has to have been the LCD one for this to test it");
				await AssertSameFootprint(lcd, expected, Color.White);
			});
		}

		/// <summary>
		/// Anywhere the LCD composite does not apply - LCD off, a fractional position, a scaled draw - the icon
		/// draws byte for byte as the same pixels without coverage do, which is the blit as it always was.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(false, 8.0, 8.0, 1.0)]
		[Arguments(true, 8.5, 8.0, 1.0)]
		[Arguments(true, 8.0, 8.25, 1.0)]
		[Arguments(true, 8.0, 8.0, 1.25)]
		public async Task WithoutTheLcdPathTheIconDrawsAsAPlainBlit(bool lcdEnabled, double x, double y, double scale)
		{
			await WithLcd(lcdEnabled, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);
				await Assert.That(icon.LcdCoverage).IsNotNull();

				ImageBuffer withCoverage = Opaque(Color.White);
				withCoverage.NewGraphics2D().Render(icon, x, y, 0, scale, scale);

				ImageBuffer expected = Opaque(Color.White);
				expected.NewGraphics2D().Render(WithoutCoverage(icon), x, y, 0, scale, scale);

				await AssertPaintedAndIdentical(withCoverage, expected, Color.White);
			});
		}

		/// <summary>
		/// A straight-alpha transparent target is refused: the LCD composite is premultiplied source-over, which
		/// would bake fringed premultiplied edges into an image that is later blended as straight alpha.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AStraightTransparentTargetGetsThePlainBlit()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: true);

				var target = new ImageBuffer(IconSize, IconSize);
				target.NewGraphics2D().Render(icon, 0, 0);

				var expected = new ImageBuffer(IconSize, IconSize);
				expected.NewGraphics2D().Render(WithoutCoverage(icon), 0, 0);

				await AssertPaintedAndIdentical(target, expected, Color.Transparent);
			});
		}

		/// <summary>A mirrored icon has no coverage that lines up with it, so it draws as a plain blit.</summary>
		[Test]
		[NotInParallel]
		public async Task AMirroredIconDrawsAsAPlainBlit()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);
				foreach (ImageBuffer mirrored in new[] { icon.MirrorX(), icon.MirrorY() })
				{
					ImageBuffer canvas = Opaque(Color.White);
					canvas.NewGraphics2D().Render(mirrored, 8, 8);

					ImageBuffer expected = Opaque(Color.White);
					expected.NewGraphics2D().Render(WithoutCoverage(mirrored), 8, 8);

					await AssertPaintedAndIdentical(canvas, expected, Color.White);
				}
			});
		}

		/// <summary>
		/// Ink drawn into the icon after load, where its coverage says there is nothing (a badge), must still
		/// show: such an image falls back to the plain blit rather than losing the badge.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ABadgeDrawnOutsideTheSilhouetteStillShows()
		{
			await WithLcd(true, async () =>
			{
				var badged = new ImageBuffer(LoadSvgIcon(Corner, invert: false));
				badged.NewGraphics2D().FillRectangle(24, 0, 32, 8, Color.Red);

				ImageBuffer canvas = Opaque(Color.White);
				canvas.NewGraphics2D().Render(badged, 8, 8);

				await Assert.That(canvas.GetPixel(8 + 28, 8 + 4)).IsEqualTo(Color.Red);
			});
		}

		/// <summary>A faded icon (a disabled button's) composites faded, matching the faded plain blit.</summary>
		[Test]
		[NotInParallel]
		public async Task AFadedIconCompositesFaded()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer faded = LoadSvgIcon(Corner, invert: false).AjustAlpha(0.2);
				await Assert.That(faded.LcdCoverage).IsNotNull();

				ImageBuffer lcd = Opaque(Color.White);
				lcd.NewGraphics2D().Render(faded, 8, 8);

				ImageBuffer expected = Opaque(Color.White);
				expected.NewGraphics2D().Render(WithoutCoverage(faded), 8, 8);

				Color interior = lcd.GetPixel(8 + 4, 8 + 24);
				Color expectedInterior = expected.GetPixel(8 + 4, 8 + 24);
				await Assert.That((int)expectedInterior.red).IsLessThan(250)
					.Because("the faded blit has to show the fade for this to compare anything");
				await Assert.That(Math.Abs(interior.red - expectedInterior.red)).IsLessThanOrEqualTo(3);
				await Assert.That(Math.Abs(interior.green - expectedInterior.green)).IsLessThanOrEqualTo(3);
				await Assert.That(Math.Abs(interior.blue - expectedInterior.blue)).IsLessThanOrEqualTo(3);
				await AssertSameFootprint(lcd, expected, Color.White);
			});
		}

		/// <summary>The composite is cached per image, and a change to the image's pixels rebuilds it.</summary>
		[Test]
		[NotInParallel]
		public async Task ChangingTheImageRebuildsTheComposite()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);
				Opaque(Color.White).NewGraphics2D().Render(icon, 8, 8);

				// Recolour every pixel red in place, keeping alpha - a colour-only edit.
				byte[] buffer = icon.GetBuffer();
				for (int y = 0; y < icon.Height; y++)
				{
					int offset = icon.GetBufferOffsetY(y);
					for (int x = 0; x < icon.Width; x++, offset += 4)
					{
						buffer[offset + ImageBuffer.OrderR] = 255;
						buffer[offset + ImageBuffer.OrderG] = 0;
						buffer[offset + ImageBuffer.OrderB] = 0;
					}
				}

				icon.MarkImageChanged();

				ImageBuffer canvas = Opaque(Color.White);
				canvas.NewGraphics2D().Render(icon, 8, 8);
				await Assert.That(canvas.GetPixel(8 + 4, 8 + 24)).IsEqualTo(Color.Red);
				await Assert.That(CountChromaPixels(canvas)).IsGreaterThan(0);
			});
		}

		/// <summary>The clipping rect bounds the LCD composite, as it does the plain blit.</summary>
		[Test]
		[NotInParallel]
		public async Task TheClipBoundsTheComposite()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);

				ImageBuffer canvas = Opaque(Color.White);
				Graphics2D graphics = canvas.NewGraphics2D();
				graphics.SetClippingRect(new RectangleDouble(0, 0, 8 + 8, CanvasSize));
				graphics.Render(icon, 8, 8);

				await Assert.That(canvas.GetPixel(8 + 4, 8 + 24)).IsEqualTo(Color.Black)
					.Because("inside the clip the icon paints");
				int paintedOutsideClip = 0;
				for (int y = 0; y < CanvasSize; y++)
				{
					for (int x = 8 + 8; x < CanvasSize; x++)
					{
						if (canvas.GetPixel(x, y) != Color.White)
						{
							paintedOutsideClip++;
						}
					}
				}

				await Assert.That(paintedOutsideClip).IsEqualTo(0);
			});
		}

		/// <summary>Inside an LCD widget backbuffer the icon's coverage merges per channel.</summary>
		[Test]
		[NotInParallel]
		public async Task AnLcdBufferDestinationKeepsTheChroma()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);

				var buffer = new LcdBuffer(CanvasSize, CanvasSize);
				var graphics = new LcdBufferGraphics2D(buffer);
				graphics.Clear(Color.White);
				graphics.Render(icon, 8, 8);

				int chroma = 0;
				for (int i = 0; i < buffer.ColorPlane.Length; i += 3)
				{
					if (buffer.ColorPlane[i] != buffer.ColorPlane[i + 2])
					{
						chroma++;
					}
				}

				await Assert.That(chroma).IsGreaterThan(0);

				ImageBuffer expected = Opaque(Color.White);
				expected.NewGraphics2D().Render(WithoutCoverage(icon), 8, 8);
				await AssertSameFootprint(buffer.ToImageBufferCollapsed(), expected, Color.White);
			});
		}

		/// <summary>
		/// The coverage rides on whole-image copies and the colour-only edits LoadIcon makes, and a geometry
		/// change drops it rather than carrying coverage that no longer lines up.
		/// </summary>
		[Test]
		public async Task CoverageFollowsCopiesButNotGeometryChanges()
		{
			ImageBuffer icon = LoadSvgIcon(Corner, invert: true);
			await Assert.That(icon.LcdCoverage).IsNotNull()
				.Because("the inverted copy LoadIcon hands back still carries the coverage");
			await Assert.That(new ImageBuffer(icon).LcdCoverage).IsSameReferenceAs(icon.LcdCoverage);

			var copied = new ImageBuffer();
			copied.CopyFrom(icon);
			await Assert.That(copied.LcdCoverage).IsSameReferenceAs(icon.LcdCoverage);
			await Assert.That(icon.GrayToColor(Color.Red).LcdCoverage).IsSameReferenceAs(icon.LcdCoverage)
				.Because("theme tinting is colour only, and is how most app icons are drawn");

			await Assert.That(icon.CreateScaledImage(IconSize * 2, IconSize * 2).LcdCoverage).IsNull();
			await Assert.That(icon.MirrorX().LcdCoverage).IsNull();
			await Assert.That(icon.MirrorY().LcdCoverage).IsNull();

			var partial = new ImageBuffer(icon);
			partial.CopyFrom(new ImageBuffer(8, 8), new RectangleInt(0, 0, 8, 8), 0, 0);
			await Assert.That(partial.LcdCoverage).IsNull();

			copied.CopyFrom(new ImageBuffer(IconSize, IconSize));
			await Assert.That(copied.LcdCoverage).IsNull()
				.Because("copying other pixels in must not leave the old icon's coverage behind");
		}

		/// <summary>
		/// WhiteToAlpha only lowers alpha, so the icon's coverage still describes its pixels - the draw-time
		/// a_now / a_orig ratio treats what it cleared as faded out.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task WhiteToAlphaKeepsTheSubpixelEdges()
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: false);
				ImageBuffer cleared = icon.WhiteToAlpha();
				(ImageBuffer tinted, _) = icon.WhiteToAlpha_GreyToColor(Color.Black);

				await Assert.That(cleared.LcdCoverage).IsSameReferenceAs(icon.LcdCoverage);
				await Assert.That(tinted.LcdCoverage).IsSameReferenceAs(icon.LcdCoverage);

				ImageBuffer canvas = Opaque(Color.White);
				canvas.NewGraphics2D().Render(cleared, 8, 8);
				await Assert.That(CountChromaPixels(canvas)).IsGreaterThan(0);
			});
		}

		/// <summary>
		/// A widget backbuffer is labelled premultiplied but holds straight colour, and every consumer reads it
		/// straight (ImageGraphics2D.StraightOverDestination). An LCD icon drawn into its transparent or
		/// half-transparent pixels and then composited onto an opaque surface must look like the icon drawn
		/// straight onto that surface. The per-channel composite is premultiplied source-over, so it stored
		/// premultiplied colour (a white icon's edge at alpha a as grey a) that the straight read darkened again.
		/// </summary>
		/// <remarks>
		/// The expected image is the plain blit straight onto the surface, so the buffered result has to have
		/// left the LCD path: a single alpha per pixel cannot carry three channel coverages, which is why a
		/// non-opaque destination takes the plain blit. The tolerance of 2 per channel is two byte roundings
		/// (into the buffer, then onto the surface); measured worst 1 with the fix (both this test and the LCD
		/// buffer one below), against 64 to 92 when the per-channel composite wrote into these pixels.
		/// </remarks>
		[Test]
		[NotInParallel]
		[Arguments(0)]
		[Arguments(128)]
		public async Task AnLcdIconInAStraightBackbufferCompositesLikeADirectDraw(int backbufferAlpha)
		{
			await WithLcd(true, async () =>
			{
				ImageBuffer icon = LoadSvgIcon(Corner, invert: true);
				var under = new Color(40, 60, 200, backbufferAlpha);

				var backbuffer = new ImageBuffer(CanvasSize, CanvasSize, 32, new BlenderPreMultBGRA());
				Graphics2D backbufferGraphics = backbuffer.NewGraphics2D();
				backbufferGraphics.Clear(under);
				backbufferGraphics.Render(icon, 8, 8);
				ImageBuffer buffered = Opaque(Surface);
				buffered.NewGraphics2D().Render(backbuffer, 0, 0);

				ImageBuffer direct = Opaque(Surface);
				Graphics2D directGraphics = direct.NewGraphics2D();
				directGraphics.FillRectangle(0, 0, CanvasSize, CanvasSize, under);
				directGraphics.Render(WithoutCoverage(icon), 8, 8);

				await Assert.That(PaintedBounds(direct, direct.GetPixel(0, 0)).Width).IsGreaterThan(0)
					.Because("the icon has to have painted, or the comparison proves nothing");
				int worst = WorstChannelDifference(buffered, direct);
				await Assert.That(worst).IsLessThanOrEqualTo(2).Because($"worst channel difference {worst}");
			});
		}

		/// <summary>
		/// The same rule for a whole LCD buffer (a nested LCD widget backbuffer) composited into a transparent
		/// straight-held backbuffer: the composite onto an opaque surface matches the buffer's collapsed
		/// single-alpha draw onto that surface, which is what a pixel with one alpha can hold.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AnLcdBufferInAStraightBackbufferCompositesLikeItsCollapsedDraw()
		{
			await WithLcd(true, async () =>
			{
				var lcd = new LcdBuffer(CanvasSize, CanvasSize);
				var lcdGraphics = new LcdBufferGraphics2D(lcd);
				lcdGraphics.Clear(new Color(0, 0, 0, 0));
				lcdGraphics.Render(new VertexSource.Ellipse(24, 24, 15.3, 11.7), new Color(255, 230, 40));

				var backbuffer = new ImageBuffer(CanvasSize, CanvasSize, 32, new BlenderPreMultBGRA());
				backbuffer.NewGraphics2D().CompositeLcdBuffer(lcd, 0, 0);
				ImageBuffer buffered = Opaque(Color.Black);
				buffered.NewGraphics2D().Render(backbuffer, 0, 0);

				ImageBuffer direct = Opaque(Color.Black);
				direct.NewGraphics2D().Render(lcd.ToImageBufferCollapsed(), 0, 0);

				await Assert.That(PaintedBounds(direct, Color.Black).Width).IsGreaterThan(0);
				int worst = WorstChannelDifference(buffered, direct);
				await Assert.That(worst).IsLessThanOrEqualTo(2).Because($"worst channel difference {worst}");
			});
		}

		/// <summary>
		/// The opacity check covers only the pixels the clip leaves: a destination opaque inside the clipping
		/// rect but transparent outside it (a scrolled panel inside a transparent surround) keeps the
		/// per-channel composite, and nothing lands outside the clip.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AnLcdBufferOpaqueInsideTheClipKeepsItsChroma()
		{
			await WithLcd(true, async () =>
			{
				var lcd = new LcdBuffer(CanvasSize, CanvasSize);
				var lcdGraphics = new LcdBufferGraphics2D(lcd);
				lcdGraphics.Clear(Color.White);
				lcdGraphics.Render(new VertexSource.Ellipse(24, 24, 15.3, 11.7), Color.Black);

				// Transparent everywhere except the left half, which is opaque white and is the clip.
				var destination = new ImageBuffer(CanvasSize, CanvasSize, 32, new BlenderPreMultBGRA());
				Graphics2D graphics = destination.NewGraphics2D();
				graphics.FillRectangle(0, 0, CanvasSize / 2, CanvasSize, Color.White);
				graphics.SetClippingRect(new RectangleDouble(0, 0, CanvasSize / 2, CanvasSize));
				graphics.CompositeLcdBuffer(lcd, 0, 0);

				await Assert.That(CountChromaPixels(destination)).IsGreaterThan(0)
					.Because("pixels outside the clip are never touched, so their transparency must not refuse the LCD path");
				await Assert.That(destination.GetPixel(CanvasSize - 1, CanvasSize / 2).alpha).IsEqualTo((byte)0)
					.Because("nothing lands outside the clip");
			});
		}

		/// <summary>Mid grey, so a white icon shows on it whether the backbuffer under it is transparent or not.</summary>
		private static readonly Color Surface = new Color(128, 128, 128);

		private static int WorstChannelDifference(ImageBuffer actual, ImageBuffer expected)
		{
			int worst = 0;
			for (int y = 0; y < actual.Height; y++)
			{
				for (int x = 0; x < actual.Width; x++)
				{
					Color a = actual.GetPixel(x, y);
					Color e = expected.GetPixel(x, y);
					worst = Math.Max(worst, Math.Max(Math.Abs(a.red - e.red), Math.Max(Math.Abs(a.green - e.green), Math.Abs(a.blue - e.blue))));
				}
			}

			return worst;
		}

		private static async Task WithLcd(bool enabled, Func<Task> test)
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = enabled;
				await test();
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		private static ImageBuffer LoadSvgIcon(string body, bool invert)
		{
			var provider = new InMemorySvgStaticData($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{body}</svg>");
			return provider.LoadIcon("test.svg", IconSize, IconSize, invert);
		}

		/// <summary>The same pixels, blender and hotspot with no coverage: what the blit draws without the LCD path.</summary>
		private static ImageBuffer WithoutCoverage(ImageBuffer icon)
		{
			var plain = new ImageBuffer(icon);
			plain.LcdCoverage = null;
			plain.OriginOffset = icon.OriginOffset;
			return plain;
		}

		private static ImageBuffer Opaque(Color color)
		{
			var image = new ImageBuffer(CanvasSize, CanvasSize);
			image.NewGraphics2D().Clear(color);
			return image;
		}

		private static async Task AssertPaintedAndIdentical(ImageBuffer actual, ImageBuffer expected, Color background)
		{
			await Assert.That(actual.Equals(expected, 0)).IsTrue();
			await Assert.That(PaintedBounds(actual, background).Width).IsGreaterThan(0)
				.Because("the icon has to have painted, or the comparison proves nothing");
		}

		/// <summary>
		/// Same painted bounding box, allowing the LCD filter's one pixel of horizontal spread, and every
		/// pixel's luminance within <see cref="LuminanceTolerance"/> of the plain blit's.
		/// </summary>
		private static async Task AssertSameFootprint(ImageBuffer actual, ImageBuffer expected, Color background)
		{
			RectangleInt actualBounds = PaintedBounds(actual, background);
			RectangleInt expectedBounds = PaintedBounds(expected, background);
			await Assert.That(expectedBounds.Width).IsGreaterThan(0);
			await Assert.That(Math.Abs(actualBounds.Left - expectedBounds.Left)).IsLessThanOrEqualTo(1);
			await Assert.That(Math.Abs(actualBounds.Right - expectedBounds.Right)).IsLessThanOrEqualTo(1);
			await Assert.That(actualBounds.Bottom).IsEqualTo(expectedBounds.Bottom);
			await Assert.That(actualBounds.Top).IsEqualTo(expectedBounds.Top);

			int worst = 0;
			for (int y = 0; y < actual.Height; y++)
			{
				for (int x = 0; x < actual.Width; x++)
				{
					worst = Math.Max(worst, Math.Abs(Luminance(actual.GetPixel(x, y)) - Luminance(expected.GetPixel(x, y))));
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(LuminanceTolerance);
		}

		/// <summary>The bounds of every pixel visibly different from <paramref name="background"/>; half-open.</summary>
		private static RectangleInt PaintedBounds(ImageBuffer image, Color background)
		{
			int left = int.MaxValue, bottom = int.MaxValue, right = int.MinValue, top = int.MinValue;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (Math.Abs(Luminance(pixel) - Luminance(background)) > 32 || Math.Abs(pixel.alpha - background.alpha) > 32)
					{
						left = Math.Min(left, x);
						bottom = Math.Min(bottom, y);
						right = Math.Max(right, x + 1);
						top = Math.Max(top, y + 1);
					}
				}
			}

			return left == int.MaxValue ? default : new RectangleInt(left, bottom, right, top);
		}

		private static int Luminance(Color color)
		{
			return (int)Math.Round((0.2126 * color.red) + (0.7152 * color.green) + (0.0722 * color.blue));
		}

		private static int CountChromaPixels(ImageBuffer image)
		{
			int count = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (pixel.red != pixel.blue)
					{
						count++;
					}
				}
			}

			return count;
		}

		private class InMemorySvgStaticData : StaticDataBase
		{
			private readonly string svg;

			public InMemorySvgStaticData(string svg)
			{
				this.svg = svg;
			}

			public override bool DirectoryExists(string path) => false;

			public override bool FileExists(string path) => true;

			public override IEnumerable<string> GetDirectories(string path) => Array.Empty<string>();

			public override IEnumerable<string> GetFiles(string path) => Array.Empty<string>();

			public override string MapPath(string path) => path;

			public override Stream OpenStream(string path) => new MemoryStream(Encoding.UTF8.GetBytes(svg));

			public override string[] ReadAllLines(string path) => new[] { svg };

			public override string ReadAllText(string path) => svg;
		}
	}
}
