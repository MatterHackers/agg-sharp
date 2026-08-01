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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;
using static System.Math;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Covers the widget backbuffer's LCD-coverage mode: which of the two representations a paint resolves
	/// to (<see cref="GuiWidget.ResolveBackbufferMode"/>), that carrying text through an
	/// <see cref="LcdBuffer"/> backbuffer lands the same bytes as painting it straight onto the destination,
	/// and that every refused gate leaves the render exactly as it was before the LCD path existed.
	/// </summary>
	/// <remarks>
	/// Mirrors the reference's <c>BackbufferMode</c> plumbing (<c>widget\backbuffer.rs</c>,
	/// <c>widget\paint\offscreen.rs</c>) and its <c>lcd_backbuffer_collapse</c> regression test.
	/// <para>
	/// <see cref="LcdRenderSettings"/> is process-wide, so every test here is <c>[NotInParallel]</c> and
	/// restores what it changed in a finally block.
	/// </para>
	/// </remarks>
	public class LcdBackbufferTests
	{
		private const int SurfaceWidth = 64;

		private const int SurfaceHeight = 24;

		/// <summary>
		/// Fractional edges on purpose: whole-pixel edges produce coverage with no partial values, and so no
		/// channel variation to tell an LCD raster apart from an ordinary anti-aliased one.
		/// </summary>
		private static readonly RectangleDouble FillRect = new RectangleDouble(4.3, 3.2, 19.4, 10.6);

		/// <summary>
		/// Off by default, and every gate refuses on its own. This is the contract the whole step rests on:
		/// nothing reaches the LCD arm unless the setting, the widget and the destination all agree.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task BackbufferModeIsRgbaUnlessEveryGateOpens()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				var widget = new GuiWidget(20, 10)
				{
					BackgroundColor = Color.White,
					DoubleBuffer = true
				};

				ImageBuffer surface = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				Graphics2D capable = surface.NewGraphics2D();

				LcdRenderSettings.Enabled = false;
				await Assert.That(widget.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.Rgba)
					.Because("the setting is off, which is the default the library ships with");

				LcdRenderSettings.Enabled = true;
				await Assert.That(widget.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("an opaque widget on a capable destination is the case the mode exists for");

				// The widget's own opacity is deliberately not a gate - see ResolveBackbufferMode's remarks.
				// Each of these leaves genuinely transparent pixels in the buffer, and the two planes carry them
				// with their per-channel coverage intact, so the composite reproduces them exactly.
				widget.BackgroundColor = new Color(255, 255, 255, 128);
				await Assert.That(widget.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("a translucent background composites per channel like any other partial coverage");

				widget.BackgroundColor = Color.White;
				widget.BackgroundRadius = new RadiusCorners(3);
				await Assert.That(widget.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("transparent corners are just zero coverage in both planes");

				widget.BackgroundRadius = default(RadiusCorners);
				widget.BackgroundOutlineWidth = 1;
				widget.BorderColor = Color.Black;
				await Assert.That(widget.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("an anti-aliased outline edge is coverage the planes hold as well as any other");

				widget.BackgroundOutlineWidth = 0;

				// Gate two: the destination.
				Graphics2D asLayer = surface.NewGraphics2D();
				asLayer.IsTransparentCompositingLayer = true;
				await Assert.That(widget.ResolveBackbufferMode(asLayer)).IsEqualTo(BackbufferMode.Rgba)
					.Because("a transparent compositing layer cannot be the final word on subpixel geometry");

				var gray = new ImageBuffer(SurfaceWidth, SurfaceHeight, 8, new blender_gray(1));
				await Assert.That(widget.ResolveBackbufferMode(gray.NewGraphics2D())).IsEqualTo(BackbufferMode.Rgba)
					.Because("an 8 bit destination has no three channels to composite into");

				await Assert.That(widget.ResolveBackbufferMode(null)).IsEqualTo(BackbufferMode.Rgba)
					.Because("no destination is not a capable destination");

				// Gate three: the transform the composite would happen under. The LCD composite is a whole-pixel
				// 1:1 blit and cannot honour a scale, so anything but exact unit scale has to stay on the arm
				// that can - a 4% scale is 12 pixels across a 300 pixel widget, not a rounding.
				Graphics2D scaled = surface.NewGraphics2D();
				scaled.SetTransform(Affine.NewScaling(1.04, 1.04));
				await Assert.That(widget.ResolveBackbufferMode(scaled)).IsEqualTo(BackbufferMode.Rgba)
					.Because("a near-unit scale is still a scale the per-channel composite would silently drop");

				Graphics2D sheared = surface.NewGraphics2D();
				sheared.SetTransform(new Affine(1, 0.1, 0, 1, 0, 0));
				await Assert.That(widget.ResolveBackbufferMode(sheared)).IsEqualTo(BackbufferMode.Rgba)
					.Because("a sheared transform is not a 1:1 blit either");

				Graphics2D translated = surface.NewGraphics2D();
				translated.SetTransform(Affine.NewTranslation(7, 3));
				await Assert.That(widget.ResolveBackbufferMode(translated)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("a pure translation is exactly what the whole-pixel composite reproduces");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The load-bearing equivalence: text painted into an LCD-coverage backbuffer and composited onto an
		/// opaque destination is <b>byte-identical</b> to the same text painted straight onto that
		/// destination. If the round trip lost anything - a rounding, a channel, a phase - this is where it
		/// would show.
		/// </summary>
		/// <remarks>
		/// The buffer starts opaque, as the mode's contract requires (a widget paints its background first),
		/// so both paths run the same per-channel source-over over the same background. The composite back is
		/// then a no-op arithmetically - every channel alpha is 1 - which is exactly the property that lets a
		/// widget cache its pixels without the destination ever knowing.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task TextThroughAnLcdBackbufferLandsExactlyAsIfDrawnDirectly()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				LcdMaskCache.Clear();

				var backbuffer = new LcdBuffer(SurfaceWidth, SurfaceHeight);
				var bufferGraphics = new LcdBufferGraphics2D(backbuffer);
				await Assert.That(bufferGraphics.CanCompositeLcd).IsTrue()
					.Because("the render target is per-channel coverage already");
				bufferGraphics.Clear(Color.White);
				bufferGraphics.DrawString("Ag mix", 3, 6, color: Color.Black);

				ImageBuffer throughBackbuffer = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				Graphics2D destination = throughBackbuffer.NewGraphics2D();
				await Assert.That(destination.CanCompositeLcdBuffer).IsTrue()
					.Because("a 32 bit ImageBuffer is the destination the per-channel composite is written for");
				destination.CompositeLcdBuffer(backbuffer, 0, 0);

				ImageBuffer drawnDirectly = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				drawnDirectly.NewGraphics2D().DrawString("Ag mix", 3, 6, color: Color.Black);

				await AssertImagesEqual(drawnDirectly, throughBackbuffer, "text carried through an LCD backbuffer");

				// Negative controls: something was painted, and it really is the subpixel raster rather than a
				// grayscale one that would round trip trivially.
				await Assert.That(throughBackbuffer.Equals(OpaqueWhite(SurfaceWidth, SurfaceHeight), 0)).IsFalse()
					.Because("the text has to actually paint");
				await Assert.That(HasChroma(throughBackbuffer)).IsTrue()
					.Because("per-channel coverage has to survive the round trip, not just luminance");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// Ordinary geometry - a plain rect fill, no text anywhere - painted into an LCD backbuffer goes
		/// through the identical mask pipeline, which is the reference's arrangement
		/// (<c>LcdGfxCtx::fill</c> calls <c>LcdBuffer::fill_path</c>) and the reason this is a vector feature
		/// rather than a font one.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task OrdinaryFillsIntoAnLcdBackbufferGoThroughTheSamePipeline()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				var painted = new LcdBuffer(SurfaceWidth, SurfaceHeight);
				var graphics = new LcdBufferGraphics2D(painted);
				graphics.Clear(Color.White);
				graphics.Render(path, Color.Black);

				// The pipeline by hand: a bbox-sized mask, composited per channel at the origin the builder
				// reports. Nothing about a rect is special-cased into a cheaper path.
				var expected = new LcdBuffer(SurfaceWidth, SurfaceHeight);
				expected.Clear(Color.White);
				bool built = BoundedMaskBuilder.TryBuild(
					SurfaceWidth,
					SurfaceHeight,
					path,
					Affine.NewIdentity(),
					out LcdMask mask,
					out int originX,
					out int originY,
					graphics.GetClippingRect());
				await Assert.That(built).IsTrue();
				expected.CompositeMask(mask, Color.Black, originX, originY);

				await AssertBytesEqual(expected.ColorPlane, painted.ColorPlane, "colour plane of a rect fill");
				await AssertBytesEqual(expected.AlphaPlane, painted.AlphaPlane, "alpha plane of a rect fill");

				// And it is genuinely per-channel: the fractional vertical edges leave the three subpixel
				// alphas of a pixel disagreeing, which a replicated-alpha blit could never produce.
				await Assert.That(HasChannelVariation(painted)).IsTrue()
					.Because("an ordinary fill must carry subpixel coverage, not one alpha copied three times");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The validity gate on a per-channel target: an <see cref="LcdBuffer"/> that will itself be blended
		/// onto something else keeps the whole pipeline - the same raster, the same layout, the same
		/// composite - and only drops the chroma. That is the reference's rule
		/// (<c>text_render.rs:56-62</c>), and it is available here precisely because the two planes can carry
		/// coverage that a single-alpha destination could not.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AnLcdBackbufferInsideALayerKeepsThePipelineAndDropsTheChroma()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				VertexStorage path = Rectangle(FillRect);

				var insideLayer = new LcdBuffer(SurfaceWidth, SurfaceHeight);
				var layerGraphics = new LcdBufferGraphics2D(insideLayer)
				{
					IsTransparentCompositingLayer = true
				};
				await Assert.That(layerGraphics.CanCompositeLcd).IsTrue()
					.Because("two planes can hold coverage wherever it comes from; only its chroma is in doubt");
				layerGraphics.Clear(Color.White);
				layerGraphics.Render(path, Color.Black);

				await Assert.That(HasChannelVariation(insideLayer)).IsFalse()
					.Because("the gray arm produces r == g == b coverage everywhere");

				// The gray mask specifically, not a dropped fill and not the ordinary rasterizer.
				var expected = new LcdBuffer(SurfaceWidth, SurfaceHeight);
				expected.Clear(Color.White);
				bool built = BoundedMaskBuilder.TryBuild(
					SurfaceWidth,
					SurfaceHeight,
					path,
					Affine.NewIdentity(),
					out LcdMask mask,
					out int originX,
					out int originY,
					layerGraphics.GetClippingRect(),
					filling_rule_e.fill_non_zero,
					LcdFilter.DefaultPrimaryWeight,
					LcdFilter.DefaultGamma,
					gray: true);
				await Assert.That(built).IsTrue();
				expected.CompositeMask(mask, Color.Black, originX, originY);

				await AssertBytesEqual(expected.ColorPlane, insideLayer.ColorPlane, "colour plane of a fill inside a layer");
				await AssertBytesEqual(expected.AlphaPlane, insideLayer.AlphaPlane, "alpha plane of a fill inside a layer");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// Images are the documented exception to "everything goes through the pipeline": they carry colour
		/// rather than coverage, so they blit with one alpha applied to all three channels and pick up no
		/// subpixel treatment at all. Running an icon through a 3x horizontal supersample would fringe its
		/// sharp edges, which is why the reference exempts them too (<c>lcd_gfx_ctx\image.rs</c>).
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ImagesBlitIntoAnLcdBackbufferWithoutSubpixelTreatment()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;

				// A different gray per row, so a flipped blit would be caught rather than looking plausible.
				var source = new ImageBuffer(4, 3, 32, new BlenderBGRA());
				byte[] rowValues = { 64, 128, 192 };
				for (int y = 0; y < source.Height; y++)
				{
					for (int x = 0; x < source.Width; x++)
					{
						source.SetPixel(x, y, new Color(rowValues[y], rowValues[y], rowValues[y], 255));
					}
				}

				var backbuffer = new LcdBuffer(8, 6);
				var bufferGraphics = new LcdBufferGraphics2D(backbuffer);
				bufferGraphics.Clear(Color.White);
				bufferGraphics.Render(source, 2, 1);

				ImageBuffer destination = OpaqueWhite(8, 6);
				destination.NewGraphics2D().CompositeLcdBuffer(backbuffer, 0, 0);

				for (int y = 0; y < destination.Height; y++)
				{
					for (int x = 0; x < destination.Width; x++)
					{
						bool insideImage = x >= 2 && x < 6 && y >= 1 && y < 4;
						byte expected = insideImage ? rowValues[y - 1] : (byte)255;
						Color pixel = destination.GetPixel(x, y);
						await Assert.That(pixel.red).IsEqualTo(expected).Because($"red at ({x}, {y})");
						await Assert.That(pixel.green).IsEqualTo(expected).Because($"green at ({x}, {y})");
						await Assert.That(pixel.blue).IsEqualTo(expected).Because($"blue at ({x}, {y})");
					}
				}

				await Assert.That(HasChroma(destination)).IsFalse()
					.Because("a gray image blit that came out with channel variation would be fringing");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The same pixel described two ways - straight alpha and premultiplied - has to land as the same ink.
		/// The blit reads the convention off the source's blender, and getting it wrong for a premultiplied
		/// source means multiplying by alpha twice, which paints every anti-aliased glyph edge at half its
		/// weight. Both of the callers this blit actually has (the hinted glyph cache, and a nested widget's
		/// RGBA backbuffer) hand it premultiplied data.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task PremultipliedAndStraightImageSourcesBlitToTheSameInk()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;

				// Half-covered white, written both ways: premultiplied colour bytes are already scaled by the
				// alpha, straight ones are the colour at full opacity. CopyPixels writes what it is given, so
				// these are the exact bytes on both paths.
				var premultiplied = new ImageBuffer(1, 1, 32, new BlenderPreMultBGRA());
				premultiplied.SetPixel(0, 0, new Color(128, 128, 128, 128));

				var straight = new ImageBuffer(1, 1, 32, new BlenderBGRA());
				straight.SetPixel(0, 0, new Color(255, 255, 255, 128));

				ImageBuffer fromPremultiplied = BlitThroughLcdBuffer(premultiplied);
				ImageBuffer fromStraight = BlitThroughLcdBuffer(straight);

				await AssertImagesEqual(fromStraight, fromPremultiplied, "the same pixel written both ways");

				// The exact bytes, pinned: half-covered white over black is 128, and the rest of the buffer
				// stays black. Reading the premultiplied bytes as straight would give 64 here.
				Color painted = fromPremultiplied.GetPixel(1, 1);
				await Assert.That(painted.red).IsEqualTo((byte)128).Because("red of the composited pixel");
				await Assert.That(painted.green).IsEqualTo((byte)128).Because("green of the composited pixel");
				await Assert.That(painted.blue).IsEqualTo((byte)128).Because("blue of the composited pixel");
				await Assert.That(fromPremultiplied.GetPixel(0, 0).red).IsEqualTo((byte)0)
					.Because("a 1x1 blit must not spill onto the rest of the buffer");

				// A blender that says neither is refused rather than guessed at, which is the same answer a
				// non-32bpp source gets.
				var unknownConvention = new ImageBuffer(1, 1, 32, new BlenderBGRAExactCopy());
				unknownConvention.SetPixel(0, 0, new Color(255, 255, 255, 128));
				ImageBuffer fromUnknown = BlitThroughLcdBuffer(unknownConvention);
				await Assert.That(fromUnknown.GetPixel(1, 1).red).IsEqualTo((byte)0)
					.Because("an unrecognized alpha convention paints nothing rather than something wrong");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// A source's <see cref="ImageBuffer.OriginOffset"/> is its hotspot, and the LCD blit has to place it
		/// exactly where the ordinary blit does - an <c>ImageSequence</c> frame with a centered origin must
		/// land centered, not with its bottom-left corner on the anchor. Otherwise the same animation would
		/// jump the moment a widget's backbuffer changed mode.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AnImageHotspotLandsWhereTheOrdinaryBlitPutsIt()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;

				// A different gray per row again, so a mirrored or flipped placement is caught rather than
				// looking plausible.
				var source = new ImageBuffer(4, 3, 32, new BlenderBGRA());
				byte[] rowValues = { 64, 128, 192 };
				for (int y = 0; y < source.Height; y++)
				{
					for (int x = 0; x < source.Width; x++)
					{
						source.SetPixel(x, y, new Color(rowValues[y], rowValues[y], rowValues[y], 255));
					}
				}

				// Set after the pixels, because SetPixel is relative to the origin too.
				source.OriginOffset = new Vector2(2, 1);

				var backbuffer = new LcdBuffer(8, 6);
				var bufferGraphics = new LcdBufferGraphics2D(backbuffer);
				bufferGraphics.Clear(Color.White);
				bufferGraphics.Render(source, 4, 3);

				ImageBuffer throughLcd = OpaqueWhite(8, 6);
				throughLcd.NewGraphics2D().CompositeLcdBuffer(backbuffer, 0, 0);

				ImageBuffer ordinary = OpaqueWhite(8, 6);
				ordinary.NewGraphics2D().Render(source, 4, 3);

				await AssertImagesEqual(ordinary, throughLcd, "a blit whose source carries a hotspot");

				// Non-vacuous: the hotspot really did move the image, so an implementation that ignored it
				// would have been comparing two identical bottom-left placements.
				source.OriginOffset = Vector2.Zero;
				ImageBuffer withoutHotspot = OpaqueWhite(8, 6);
				withoutHotspot.NewGraphics2D().Render(source, 4, 3);
				await Assert.That(withoutHotspot.Equals(ordinary, 0)).IsFalse()
					.Because("the hotspot has to be the difference between two distinguishable placements");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The two representations are exclusive, and a widget that switches between them keeps neither stale
		/// pixels nor a second buffer's worth of memory. <see cref="GuiWidget.BackBuffer"/> answers null while
		/// the pixels are per-channel, rather than handing back whatever the last RGBA paint left behind.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task SwitchingBackbufferModesReleasesTheOtherRepresentation()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = false;

				GuiWidget container = BuildContainer(Color.White, doubleBuffer: true);
				GuiWidget child = container.Children[0];

				ImageBuffer rgbaRender = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				container.OnDraw(rgbaRender.NewGraphics2D());
				await Assert.That(child.BackBuffer).IsNotNull()
					.Because("with LCD off the widget is in RGBA mode, where the buffer is an ImageBuffer");

				LcdRenderSettings.Enabled = true;
				ImageBuffer lcdRender = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				container.OnDraw(lcdRender.NewGraphics2D());
				await Assert.That(child.BackBuffer).IsNull()
					.Because("per-channel pixels have no ImageBuffer to hand back, and a stale one would be worse");
				await Assert.That(HasChroma(lcdRender)).IsTrue()
					.Because("the LCD arm is the one that ran");

				// And back again, which is the case with no extra row or column to force a reallocation: the
				// RGBA buffer was released on the way in and has to be rebuilt on the way out.
				LcdRenderSettings.Enabled = false;
				ImageBuffer backToRgba = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				container.OnDraw(backToRgba.NewGraphics2D());
				await Assert.That(child.BackBuffer).IsNotNull()
					.Because("the RGBA buffer has to come back when the mode does");
				await AssertImagesEqual(rgbaRender, backToRgba, "a widget painted after a round trip through LCD mode");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// With the setting off, a double-buffered widget paints exactly what the same widget paints with no
		/// backbuffer at all - the property the RGBA arm has always had, pinned here so the LCD seam cannot
		/// quietly move it.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ToggleOffLeavesTheBufferedRenderUnchanged()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = false;

				ImageBuffer buffered = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				BuildContainer(Color.White, doubleBuffer: true).OnDraw(buffered.NewGraphics2D());

				ImageBuffer unbuffered = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				BuildContainer(Color.White, doubleBuffer: false).OnDraw(unbuffered.NewGraphics2D());

				await AssertImagesEqual(unbuffered, buffered, "a double-buffered widget with LCD off");
				await Assert.That(buffered.Equals(OpaqueWhite(SurfaceWidth, SurfaceHeight), 0)).IsFalse()
					.Because("the widget has to actually paint");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The case the feature actually lives or dies on in an application: a double-buffered widget whose
		/// background is <b>not</b> opaque still takes the LCD arm, and its text still comes out with subpixel
		/// chroma.
		/// </summary>
		/// <remarks>
		/// This is not an edge case - it is the common one. <see cref="TextWidget"/> is double-buffered by
		/// default (<see cref="TextWidget.DoubleBufferDefault"/>) and draws glyphs over a transparent
		/// background, so every label in a MatterCAD window is exactly this widget. While a non-opaque
		/// backbuffer was refused the LCD arm, all of them fell to the RGBA arm, whose buffer declares itself
		/// <see cref="Graphics2D.IsTransparentCompositingLayer"/> and so refuses the mask pipeline outright -
		/// and the user's setting reached nothing at all.
		/// <para>
		/// The equality is to a tolerance rather than byte-exact, unlike
		/// <see cref="TextThroughAnLcdBackbufferLandsExactlyAsIfDrawnDirectly"/>, whose buffer is opaque and so
		/// composites back with every channel alpha at 1 - arithmetically a copy, with nothing to round. Both
		/// paths here do the same per-channel source-over in the same order - Porter-Duff over is associative,
		/// which is what makes a non-opaque buffer valid at all - but the buffered one lands each paint in a
		/// byte plane on the way past, and each of those quantizations can move a channel by one level.
		/// </para>
		/// <para>
		/// <b>Which is where the tolerance comes from: one level per buffered composite.</b>
		/// <see cref="BuildContainer"/> paints twice into the planes - the translucent background fill, then
		/// the string over it - so two roundings accumulate and the worst case is 2. Derive it that way rather
		/// than pinning the number observed, so a font, filter or blend change that legitimately costs a second
		/// level does not read as a regression; a widget that painted a third time would want 3.
		/// </para>
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task ANonOpaqueWidgetsBackbufferStillCarriesChroma()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				var translucent = new Color(255, 255, 255, 128);

				LcdRenderSettings.Enabled = true;

				GuiWidget container = BuildContainer(translucent, doubleBuffer: true);
				Graphics2D capable = OpaqueWhite(SurfaceWidth, SurfaceHeight).NewGraphics2D();
				await Assert.That(container.Children[0].ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("a translucent widget's coverage is exactly what the two planes exist to carry");

				ImageBuffer buffered = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				container.OnDraw(buffered.NewGraphics2D());

				await Assert.That(HasChroma(buffered)).IsTrue()
					.Because("this is the widget every label in an application is, and it has to get subpixel text");

				// And the chroma is the right chroma: the same ink the unbuffered widget paints straight onto
				// the destination, which is the render the backbuffer is only supposed to be caching.
				ImageBuffer unbuffered = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				BuildContainer(translucent, doubleBuffer: false).OnDraw(unbuffered.NewGraphics2D());
				// Two buffered composites - the background fill and the string - at one byte level each.
				await AssertImagesClose(unbuffered, buffered, 2, "a non-opaque widget carried through an LCD backbuffer");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The other half of the validity gate, end to end: a widget that lands on the <b>RGBA</b> arm paints
		/// text byte for byte as it did before the LCD path existed - chroma-free - however the user's setting
		/// is set.
		/// </summary>
		/// <remarks>
		/// The widget's own opacity is no longer what routes it there (see
		/// <see cref="GuiWidget.ResolveBackbufferMode"/>), so this drives the arm from the destination
		/// instead: a transparent compositing layer, which is exactly what a widget nested inside another
		/// widget's RGBA backbuffer is painted onto.
		/// <para>
		/// What this pins is the one line in <c>GuiWidget.RasterizeBackbuffer</c> that flags the RGBA buffer
		/// <see cref="Graphics2D.IsTransparentCompositingLayer"/>. Without it the buffer is just a 32 bit
		/// <see cref="ImageBuffer"/>, the mask pipeline accepts it, and the widget's text picks up subpixel
		/// phase computed against pixels that get blended again later against content the phase knows nothing
		/// about - chroma this test would then see. It is checked here rather than at the flag because the
		/// flag has no observable meaning on its own.
		/// </para>
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task AnRgbaBackbufferForcedByTheDestinationStaysChromaFree()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				// Opaque on purpose: the destination has to be the only thing keeping this off the LCD arm.
				LcdRenderSettings.Enabled = true;
				GuiWidget container = BuildContainer(Color.White, doubleBuffer: true);

				ImageBuffer withLcdOn = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				Graphics2D asLayer = withLcdOn.NewGraphics2D();
				asLayer.IsTransparentCompositingLayer = true;
				await Assert.That(container.Children[0].ResolveBackbufferMode(asLayer)).IsEqualTo(BackbufferMode.Rgba)
					.Because("a destination that cannot take the two planes is what sends a widget to the RGBA arm");
				container.OnDraw(asLayer);

				LcdRenderSettings.Enabled = false;
				ImageBuffer baseline = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				Graphics2D baselineAsLayer = baseline.NewGraphics2D();
				baselineAsLayer.IsTransparentCompositingLayer = true;
				BuildContainer(Color.White, doubleBuffer: true).OnDraw(baselineAsLayer);

				await AssertImagesEqual(baseline, withLcdOn, "a widget whose destination refuses the per-channel composite");
				await Assert.That(HasChroma(withLcdOn)).IsFalse()
					.Because("subpixel geometry against pixels that get blended again later is not valid");
				await Assert.That(withLcdOn.Equals(OpaqueWhite(SurfaceWidth, SurfaceHeight), 0)).IsFalse()
					.Because("the widget has to actually paint, or there is no raster to be chroma-free about");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The fallback for a destination that cannot take the two planes: collapse to one alpha and blit,
		/// lossy of chroma but luminance-preserving. This is the base <see cref="Graphics2D"/> body, reached
		/// here through the transparent-layer refusal - the live path the reference tests too
		/// (<c>tests\lcd_backbuffer_collapse.rs</c>).
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task CompositeOntoALayerThatRefusesPerChannelCollapses()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;

				// Black text on an otherwise untouched (fully transparent) buffer, so the three channel alphas
				// diverge at every glyph edge and the collapse has something to lose.
				var backbuffer = new LcdBuffer(SurfaceWidth, SurfaceHeight);
				var bufferGraphics = new LcdBufferGraphics2D(backbuffer);
				bufferGraphics.DrawString("Ag mix", 3, 6, color: Color.Black);
				await Assert.That(HasChannelVariation(backbuffer)).IsTrue()
					.Because("the buffer under test has to have chroma for the collapse to be visible");

				ImageBuffer destination = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				Graphics2D asLayer = destination.NewGraphics2D();
				asLayer.IsTransparentCompositingLayer = true;
				await Assert.That(asLayer.CanCompositeLcdBuffer).IsFalse();
				asLayer.CompositeLcdBuffer(backbuffer, 0, 0);

				ImageBuffer collapsedByHand = OpaqueWhite(SurfaceWidth, SurfaceHeight);
				collapsedByHand.NewGraphics2D().Render(backbuffer.ToImageBufferCollapsed(), 0, 0);

				await AssertImagesEqual(collapsedByHand, destination, "an LCD buffer composited onto a layer");
				await Assert.That(destination.Equals(OpaqueWhite(SurfaceWidth, SurfaceHeight), 0)).IsFalse()
					.Because("the collapse still has to paint the text");
				await Assert.That(HasChroma(destination)).IsFalse()
					.Because("a single alpha per pixel cannot carry three coverages");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// A container holding one double-bufferable child that paints a background and a string. The child is
		/// placed on whole pixels so the backbuffer maps 1:1 onto the destination, which is what makes the
		/// buffered and unbuffered renders comparable at all.
		/// </summary>
		private static GuiWidget BuildContainer(Color background, bool doubleBuffer)
		{
			var container = new GuiWidget(SurfaceWidth, SurfaceHeight);
			var child = new GuiWidget(SurfaceWidth - 8, SurfaceHeight - 8)
			{
				OriginRelativeParent = new Vector2(4, 4),
				BackgroundColor = background,
				DoubleBuffer = doubleBuffer
			};

			// Drawn through the widget's own draw event rather than a TextWidget child, so the test is about
			// one backbuffer rather than about nested ones.
			child.AfterDraw += (s, e) => e.Graphics2D.DrawString("Ag mix", 3, 6, color: Color.Black);
			container.AddChild(child);

			return container;
		}

		/// <summary>
		/// Blits <paramref name="source"/> at (1, 1) into a 2x2 LCD buffer cleared to opaque black, then
		/// composites that onto an opaque destination - the whole round trip a widget's image blit takes,
		/// small enough that the resulting bytes can be reasoned about by hand.
		/// </summary>
		private static ImageBuffer BlitThroughLcdBuffer(ImageBuffer source)
		{
			var backbuffer = new LcdBuffer(2, 2);
			var bufferGraphics = new LcdBufferGraphics2D(backbuffer);
			bufferGraphics.Clear(Color.Black);
			bufferGraphics.Render(source, 1, 1);

			var destination = new ImageBuffer(2, 2, 32, new BlenderPreMultBGRA());
			destination.NewGraphics2D().Clear(Color.Black);
			destination.NewGraphics2D().CompositeLcdBuffer(backbuffer, 0, 0);

			return destination;
		}

		/// <summary>A 32 bit premultiplied BGRA surface, opaque white - the widget backbuffer convention.</summary>
		private static ImageBuffer OpaqueWhite(int width, int height)
		{
			var image = new ImageBuffer(width, height, 32, new BlenderPreMultBGRA());
			image.NewGraphics2D().Clear(Color.White);

			return image;
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
		/// Whether any pixel's channels differ from each other - the defining mark of subpixel coverage, and
		/// what a chroma-free raster must not produce. Both fills here are black on white, so the only thing
		/// that can make the channels disagree is per-channel coverage.
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
		/// The same question one level earlier: does any pixel's three channels disagree, in either plane.
		/// </summary>
		/// <remarks>
		/// Both planes have to be looked at, because which one carries the subpixel information depends on
		/// what the buffer was painted over. Over an opaque background every channel alpha composites to 1
		/// and the coverage survives only in the premultiplied colour; over an untouched (transparent) buffer
		/// the colour is a flat black and the coverage is entirely in the alphas.
		/// </remarks>
		private static bool HasChannelVariation(LcdBuffer buffer)
		{
			for (int offset = 0; offset < buffer.AlphaPlane.Length; offset += 3)
			{
				if (buffer.AlphaPlane[offset] != buffer.AlphaPlane[offset + 1]
					|| buffer.AlphaPlane[offset + 1] != buffer.AlphaPlane[offset + 2]
					|| buffer.ColorPlane[offset] != buffer.ColorPlane[offset + 1]
					|| buffer.ColorPlane[offset + 1] != buffer.ColorPlane[offset + 2])
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// <see cref="AssertImagesEqual"/> with a per-channel tolerance, for the comparisons that cannot be
		/// byte-exact: a composite through a non-opaque backbuffer quantizes to a byte plane on the way past,
		/// once per paint. Callers derive <paramref name="maxDelta"/> from how many of those there were.
		/// </summary>
		private static async Task AssertImagesClose(ImageBuffer expected, ImageBuffer actual, int maxDelta, string what)
		{
			await Assert.That(actual.Width).IsEqualTo(expected.Width).Because($"width of {what}");
			await Assert.That(actual.Height).IsEqualTo(expected.Height).Because($"height of {what}");

			for (int y = 0; y < expected.Height; y++)
			{
				for (int x = 0; x < expected.Width; x++)
				{
					Color expectedPixel = expected.GetPixel(x, y);
					Color actualPixel = actual.GetPixel(x, y);
					if (Abs(expectedPixel.red - actualPixel.red) > maxDelta
						|| Abs(expectedPixel.green - actualPixel.green) > maxDelta
						|| Abs(expectedPixel.blue - actualPixel.blue) > maxDelta
						|| Abs(expectedPixel.alpha - actualPixel.alpha) > maxDelta)
					{
						await Assert.That(actualPixel.ToString()).IsEqualTo(expectedPixel.ToString())
							.Because($"pixel ({x}, {y}) of {what} is more than {maxDelta} from expected");
					}
				}
			}
		}

		/// <summary>Pixel-wise, so a failure reports where and in which channel the images parted.</summary>
		private static async Task AssertImagesEqual(ImageBuffer expected, ImageBuffer actual, string what)
		{
			await Assert.That(actual.Width).IsEqualTo(expected.Width).Because($"width of {what}");
			await Assert.That(actual.Height).IsEqualTo(expected.Height).Because($"height of {what}");

			for (int y = 0; y < expected.Height; y++)
			{
				for (int x = 0; x < expected.Width; x++)
				{
					Color expectedPixel = expected.GetPixel(x, y);
					Color actualPixel = actual.GetPixel(x, y);
					if (expectedPixel != actualPixel)
					{
						await Assert.That(actualPixel.ToString()).IsEqualTo(expectedPixel.ToString())
							.Because($"pixel ({x}, {y}) of {what}");
					}
				}
			}
		}

		/// <summary>Element-wise, so the comparison reports which byte moved.</summary>
		private static async Task AssertBytesEqual(byte[] expected, byte[] actual, string what)
		{
			await Assert.That(actual.Length).IsEqualTo(expected.Length).Because($"length of {what}");
			for (int i = 0; i < expected.Length; i++)
			{
				if (actual[i] != expected[i])
				{
					await Assert.That(actual[i]).IsEqualTo(expected[i]).Because($"byte {i} of {what}");
				}
			}
		}
	}
}
