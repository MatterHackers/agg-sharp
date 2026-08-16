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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Covers both GPU arms of the LCD composite: the three color-masked passes
	/// <see cref="Graphics2DGpu.CompositeLcdBuffer"/> issues for a whole backbuffer, the per-channel pass
	/// images (<see cref="LcdBufferChannelImages"/>) those passes sample and the cache that keeps them in step
	/// with the buffer they came from - and the same three passes for a single cached
	/// <see cref="LcdMask"/>, which is what an ordinary vector fill (text included) reaches.
	/// </summary>
	/// <remarks>
	/// Nothing here needs a live GL context: <see cref="RecordingGpuContext"/> captures the command stream,
	/// including the pixels that reach <c>glTexImage2D</c>, so the packing, the row order and the pass
	/// configuration are all assertable. What is <b>not</b> covered, and can only be checked against a real
	/// driver, is that the hardware honours <c>glColorMask</c> and the default modulate texture environment
	/// the way the composite assumes - see <c>ThreePassBlendReproducesTheSoftwareComposite</c>, which asserts
	/// against GL's specified blend arithmetic rather than a device's.
	/// </remarks>
	[NotInParallel]
	public class LcdGpuCompositeTests
	{
		private const int BufferWidth = 7;

		private const int BufferHeight = 5;

		/// <summary>Surface the mask arm's tests fill into; big enough to hold the fill with room around it.</summary>
		private const int MaskSurfaceWidth = 24;

		private const int MaskSurfaceHeight = 12;

		/// <summary>
		/// The packing contract: pass <c>c</c>'s image carries channel <c>c</c>'s premultiplied color in its
		/// own color slot, zero in the other two, and channel <c>c</c>'s coverage in alpha - the reference
		/// shader's <c>vec4(col * cc, aa)</c> baked into pixels, because the fixed function GL path has no
		/// uniform to select a channel with.
		/// </summary>
		[Test]
		public async Task ChannelImagesCarryOneChannelEach()
		{
			LcdBuffer buffer = BufferWithDistinctChannels();

			var images = new LcdBufferChannelImages(buffer.Width, buffer.Height);
			images.UpdateFrom(buffer);

			for (int y = 0; y < buffer.Height; y++)
			{
				for (int x = 0; x < buffer.Width; x++)
				{
					int source = buffer.PixelOffset(x, y);

					for (int channel = 0; channel < LcdBufferChannelImages.ChannelCount; channel++)
					{
						Color texel = images[channel].GetPixel(x, y);

						await Assert.That(texel.alpha)
							.IsEqualTo(buffer.AlphaPlane[source + channel])
							.Because($"pass {channel} takes channel {channel}'s coverage as its source alpha");

						await Assert.That(new[] { texel.red, texel.green, texel.blue }[channel])
							.IsEqualTo(buffer.ColorPlane[source + channel])
							.Because($"pass {channel} writes channel {channel}'s premultiplied color");

						// The other two are masked off by the pass's glColorMask, but zeroing them is what
						// keeps every pass image a valid premultiplied image - which is what makes the trip
						// through the texture uploader's premultiplied blit lossless.
						for (int other = 0; other < LcdBufferChannelImages.ChannelCount; other++)
						{
							if (other == channel)
							{
								continue;
							}

							await Assert.That(new[] { texel.red, texel.green, texel.blue }[other])
								.IsEqualTo((byte)0)
								.Because($"pass {channel} must not carry channel {other}'s color");
						}
					}
				}
			}
		}

		/// <summary>
		/// The Y orientation pin (LCD plan section 8, gotcha 1). Both the buffer and the image are Y-up, and
		/// agg-sharp's GL texture path is Y-up end to end, so the pack must not flip - the reference's
		/// <c>flip_plane</c> exists only because its cached planes are stored top-row-first.
		/// </summary>
		[Test]
		public async Task ChannelImagesDoNotFlipTheBuffer()
		{
			// Coverage in the bottom row only, so a flip cannot hide behind symmetry.
			var buffer = new LcdBuffer(BufferWidth, BufferHeight);
			for (int x = 0; x < BufferWidth; x++)
			{
				int offset = buffer.PixelOffset(x, 0);
				for (int channel = 0; channel < 3; channel++)
				{
					buffer.ColorPlane[offset + channel] = 200;
					buffer.AlphaPlane[offset + channel] = 255;
				}
			}

			buffer.MarkChanged();

			var images = new LcdBufferChannelImages(buffer.Width, buffer.Height);
			images.UpdateFrom(buffer);

			for (int channel = 0; channel < LcdBufferChannelImages.ChannelCount; channel++)
			{
				await Assert.That(images[channel].GetPixel(0, 0).alpha)
					.IsEqualTo((byte)255)
					.Because("buffer row 0 is the bottom row, and so is image row 0");
				await Assert.That(images[channel].GetPixel(0, BufferHeight - 1).alpha)
					.IsEqualTo((byte)0)
					.Because("an upside down pack would have put the painted row at the top");
			}
		}

		/// <summary>
		/// The cache stamp. A widget re-rasters into the same <see cref="LcdBuffer"/> instance whenever its
		/// size has not changed, so identity alone would serve last frame's textures forever - and repacking
		/// every frame regardless would throw away a texture upload's worth of work per unchanged widget.
		/// </summary>
		[Test]
		public async Task ChannelImagesRebuildExactlyWhenTheBufferChanges()
		{
			LcdBuffer buffer = BufferWithDistinctChannels();
			var images = new LcdBufferChannelImages(buffer.Width, buffer.Height);

			await Assert.That(images.IsCurrentFor(buffer)).IsFalse()
				.Because("a freshly allocated set holds no pixels yet");
			await Assert.That(images.UpdateFrom(buffer)).IsTrue();

			int changedCountAfterFirstPack = images[0].ChangedCount;

			await Assert.That(images.IsCurrentFor(buffer)).IsTrue();
			await Assert.That(images.UpdateFrom(buffer)).IsFalse()
				.Because("an unchanged buffer must not repack, or every frame would re-upload three textures");
			await Assert.That(images[0].ChangedCount).IsEqualTo(changedCountAfterFirstPack);

			// A repaint into the same instance - exactly what GuiWidget does when the widget's size held.
			buffer.Clear(new Color(0, 0, 0, 0));
			buffer.CompositeMask(SolidMask(buffer.Width, buffer.Height, 255, 128, 64), Color.White, 0, 0);

			await Assert.That(images.IsCurrentFor(buffer)).IsFalse()
				.Because("the buffer was painted again, so the pass images are last frame's pixels");
			await Assert.That(images.UpdateFrom(buffer)).IsTrue();
			await Assert.That(images[0].ChangedCount)
				.IsNotEqualTo(changedCountAfterFirstPack)
				.Because("the repack has to reach the texture cache, which watches ImageBuffer.ChangedCount");

			await Assert.That(images[0].GetPixel(3, 2).alpha).IsEqualTo((byte)255);
			await Assert.That(images[1].GetPixel(3, 2).alpha).IsEqualTo((byte)128);
			await Assert.That(images[2].GetPixel(3, 2).alpha).IsEqualTo((byte)64);
		}

		/// <summary>
		/// Only a live device can prove <c>glColorMask</c> works; what this pins is that the composite asks
		/// for the right thing - three single channel passes with the alpha write mask off, over the
		/// premultiplied blend, and the full mask restored afterwards.
		/// </summary>
		[Test]
		public async Task CompositeLcdBufferDrawsThreeColorMaskedPasses()
		{
			var fake = new RecordingGpuContext(idBase: 40000);
			var graphics = new Graphics2DGpu(new GL(fake), 64, 32, 1);

			await Assert.That(graphics.CanCompositeLcdBuffer).IsTrue()
				.Because("this is the gate GuiWidget.ResolveBackbufferMode consults for a GPU destination");

			fake.ResetCallRecording();
			graphics.CompositeLcdBuffer(BufferWithDistinctChannels(), 10, 4);

			await Assert.That(fake.ColorMasks).IsEquivalentTo(new[]
			{
				(true, false, false, false),
				(false, true, false, false),
				(false, false, true, false),
				(true, true, true, true),
			}).Because("one pass per subpixel channel, none of them writing destination alpha, then a restore");

			await Assert.That(fake.BlendFuncs.Count).IsEqualTo(1);
			await Assert.That(fake.BlendFuncs[0])
				.IsEqualTo(((int)BlendingFactorSrc.One, (int)BlendingFactorDest.OneMinusSrcAlpha))
				.Because("the color plane is premultiplied, so src-over is One / OneMinusSrcAlpha");

			await Assert.That(fake.BeginCount).IsEqualTo(3).Because("the same quad is drawn three times");
			await Assert.That(fake.Vertex2Count).IsEqualTo(12);

			// Where the pixels land. The quad ImageTexturePlugin emits is at the origin, so the composite's
			// only statement of position is this translate - a dropped or transposed one would put a widget's
			// backbuffer somewhere else entirely while every other assertion here still passed.
			await Assert.That(fake.Translates).IsEquivalentTo(new[] { (10.0, 4.0, 0.0, 0) })
				.Because("the buffer is placed at (destX, destY) once, before any of the three passes is drawn");

			// Each pass samples its own image, so each has to be a texture of its own.
			await Assert.That(fake.TextureUploads.Count).IsEqualTo(3);
			await Assert.That(fake.TextureUploads.Select(upload => upload.Texture).Distinct().Count())
				.IsEqualTo(3)
				.Because("three passes reading one texture could only ever produce one channel's coverage");
		}

		/// <summary>
		/// The texture-boundary half of the Y orientation pin: what actually reaches <c>glTexImage2D</c>, for
		/// a buffer painted only along its bottom row. GL's first uploaded row is <c>t = 0</c>, which
		/// <c>ImageTexturePlugin</c>'s quad puts at the bottom of the drawn rectangle.
		/// </summary>
		[Test]
		public async Task CompositeLcdBufferUploadsThePlanesTheRightWayUp()
		{
			var buffer = new LcdBuffer(BufferWidth, BufferHeight);
			buffer.CompositeMask(BottomRowMask(BufferWidth, BufferHeight), Color.White, 0, 0);

			var fake = new RecordingGpuContext(idBase: 41000);
			var graphics = new Graphics2DGpu(new GL(fake), 64, 32, 1);
			graphics.CompositeLcdBuffer(buffer, 0, 0);

			await Assert.That(fake.TextureUploads.Count).IsEqualTo(3);

			foreach (RecordedTextureUpload upload in fake.TextureUploads)
			{
				await Assert.That(upload.Width).IsEqualTo(BufferWidth);
				await Assert.That(upload.Height).IsEqualTo(BufferHeight);

				await Assert.That(upload.Texel(0, 0).Alpha)
					.IsEqualTo((byte)255)
					.Because("the painted bottom row has to be the first row uploaded");
				await Assert.That(upload.Texel(0, BufferHeight - 1).Alpha)
					.IsEqualTo((byte)0)
					.Because("an upside down upload would composite the widget's contents mirrored");
			}
		}

		/// <summary>
		/// Runs GL's specified blend arithmetic over the pixels and pass configuration that actually reached
		/// the context, and requires the result to be byte identical to the software per-channel composite
		/// (<see cref="LcdBuffer.CompositeOnto"/>). This is what makes a mis-packed channel, a swapped color
		/// slot or a flipped row a test failure rather than something to be spotted on screen.
		/// </summary>
		/// <remarks>
		/// The three passes never write destination alpha, so only the color channels are compared - see
		/// <see cref="Graphics2DGpu.CompositeLcdBuffer"/> for why that divergence from the software composite
		/// is the reference's behaviour too.
		/// </remarks>
		[Test]
		public async Task ThreePassBlendReproducesTheSoftwareComposite()
		{
			LcdBuffer buffer = BufferWithLcdText();
			await RequirePerChannelDivergence(buffer);

			// The software answer, from the production per-channel composite.
			ImageBuffer expected = MidGrayPremultiplied(buffer.Width, buffer.Height);
			buffer.CompositeOnto(expected, 0, 0);

			var fake = new RecordingGpuContext(idBase: 42000);
			var graphics = new Graphics2DGpu(new GL(fake), buffer.Width, buffer.Height, 1);
			graphics.CompositeLcdBuffer(buffer, 0, 0);

			await Assert.That(fake.TextureUploads.Count).IsEqualTo(3);

			// The framebuffer the passes blend into, starting where the software destination started.
			ImageBuffer framebuffer = MidGrayPremultiplied(buffer.Width, buffer.Height);
			(int Source, int Destination) blend = fake.BlendFuncs.Single();
			for (int pass = 0; pass < 3; pass++)
			{
				(bool Red, bool Green, bool Blue, bool Alpha) mask = fake.ColorMasks[pass];
				await Assert.That(mask.Alpha).IsFalse();

				BlendOnePass(framebuffer, fake.TextureUploads[pass], mask, blend);
			}

			for (int y = 0; y < buffer.Height; y++)
			{
				for (int x = 0; x < buffer.Width; x++)
				{
					Color want = expected.GetPixel(x, y);
					Color got = framebuffer.GetPixel(x, y);

					await Assert.That((got.red, got.green, got.blue))
						.IsEqualTo((want.red, want.green, want.blue))
						.Because($"the three GPU passes must land the software composite's pixel at {x}, {y}");
				}
			}
		}

		/// <summary>
		/// The wire-up: with the GL destination now reporting the capability, a GPU rendered widget resolves
		/// the LCD arm through the seam that was already there - and every other gate still refuses on its
		/// own, so nothing changes for a process that leaves the setting off.
		/// </summary>
		[Test]
		public async Task GpuDestinationEngagesTheLcdBackbufferMode()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				var widget = new GuiWidget(20, 10)
				{
					BackgroundColor = Color.White,
					DoubleBuffer = true
				};

				var graphics = new Graphics2DGpu(new GL(new RecordingGpuContext(idBase: 43000)), 64, 32, 1);

				LcdRenderSettings.Enabled = false;
				await Assert.That(widget.ResolveBackbufferMode(graphics)).IsEqualTo(BackbufferMode.Rgba)
					.Because("the setting is off, so the GL path stays byte for byte where it was");

				LcdRenderSettings.Enabled = true;
				await Assert.That(widget.ResolveBackbufferMode(graphics)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("the GL destination can composite the planes now, which was the last gate closed");

				// The unit-scale gate still applies: finished planes cannot be resampled without smearing
				// each channel's phase into its neighbours.
				graphics.SetTransform(Affine.NewScaling(1.04, 1.04));
				await Assert.That(widget.ResolveBackbufferMode(graphics)).IsEqualTo(BackbufferMode.Rgba)
					.Because("the LCD arm only runs where a whole pixel 1:1 composite is the right composite");

				graphics.SetTransform(Affine.NewTranslation(3, 7));
				await Assert.That(widget.ResolveBackbufferMode(graphics)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("a whole pixel translation is exactly what the composite places by");

				// And a Graphics2DGpu with no device behind it - what a window destination amounts to before
				// its wgpu device exists or after teardown - cannot composite anything at all.
				var deviceless = new Graphics2DGpu(null, 1);
				await Assert.That(deviceless.CanCompositeLcdBuffer).IsFalse();
				await Assert.That(widget.ResolveBackbufferMode(deviceless)).IsEqualTo(BackbufferMode.Rgba);
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The bug this file's mask arm exists to close: with the setting on, an ordinary vector fill through
		/// <see cref="Graphics2D.Render(IVertexSource, IColorType)"/> on a GL destination has to reach the LCD
		/// <b>mask</b> composite, not the tesselated fill. Every gate in
		/// <c>Graphics2D.TryRenderThroughLcd</c> is checked against this destination, so a regression in any
		/// one of them - the capability, the fill rule it needs to rasterize with - shows up here as three
		/// missing passes.
		/// </summary>
		[Test]
		public async Task GpuVectorFillTakesTheLcdMaskPath()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				LcdMaskCache.Clear();

				var fake = new RecordingGpuContext(idBase: 44000);
				var graphics = new Graphics2DGpu(new GL(fake), MaskSurfaceWidth, MaskSurfaceHeight, 1);

				await Assert.That(graphics.CanCompositeLcd).IsTrue()
					.Because("this is the first gate Graphics2D.Render consults before it builds a mask at all");

				fake.ResetCallRecording();
				long buildsBefore = LcdMaskCache.BuildCount;

				// A clip narrower than the surface, whole pixels as a widget's clip always is by the time it
				// reaches a destination (GuiWidget.DrawChild rounds all four edges out first).
				var clip = new RectangleDouble(2, 1, 21, 11);
				graphics.SetClippingRect(clip);

				// Fractional edges, so the mask carries real per-channel coverage rather than whole pixels.
				graphics.Render(new IdentifiedRectangle(new RectangleDouble(3.4, 2.7, 18.2, 8.35)), Color.Black);

				await Assert.That(LcdMaskCache.BuildCount - buildsBefore).IsEqualTo(1L)
					.Because("a source that names its own geometry has to be rasterized into a cacheable mask");

				// The composite enforces no clip of its own - it draws its quads over the mask's full extent
				// and lets the scissor cut them - so the scissor being exactly the clip the composite was
				// handed is what makes that safe rather than a way to paint outside the caller's clip.
				await Assert.That(graphics.GetClippingRect()).IsEqualTo(clip)
					.Because("this is the rect TryRenderThroughLcd passes to CompositeLcdMask");
				await Assert.That(fake.Scissors).IsEquivalentTo(new[] { (2, 1, 19, 10) })
					.Because("the same rect is live as the GL scissor for all three passes, in GL's x, y, w, h form");

				await Assert.That(fake.ColorMasks).IsEquivalentTo(new[]
				{
					(true, false, false, false),
					(false, true, false, false),
					(false, false, true, false),
					(true, true, true, true),
				}).Because("the mask composite is three single channel passes and a restore, as the buffer one is");

				await Assert.That(fake.TextureUploads.Count).IsEqualTo(3)
					.Because("each pass samples its own channel's coverage, so each needs a texture of its own");
				await Assert.That(fake.BeginCount).IsEqualTo(3)
					.Because("three quads and nothing else - the tesselated fallback fill would add geometry of its own");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// The mask arm's byte-exactness pin, the twin of
		/// <see cref="ThreePassBlendReproducesTheSoftwareComposite"/>: GL's specified blend arithmetic over the
		/// pixels, the draw color and the pass configuration that actually reached the context has to land
		/// exactly what <see cref="LcdComposite.Composite"/> lands.
		/// </summary>
		/// <remarks>
		/// The mask carries coverage and nothing else, so this is also what pins the color's route: it arrives
		/// as the draw color and is applied by the default modulate texture environment, which is the only
		/// reason one set of pass textures can serve every color and position a mask is ever drawn at.
		/// <para>
		/// The color is opaque because that is where the two are byte identical - see
		/// <see cref="MaskPassesMatchTheSoftwareCompositeForATranslucentColor"/> for the case that pays the
		/// premultiplied draw color's rounding.
		/// </para>
		/// </remarks>
		[Test]
		public async Task MaskPassesReproduceTheSoftwareComposite()
		{
			await AssertMaskPassesMatchSoftwareComposite(new Color(220, 130, 40), idBase: 45000, tolerance: 0);
		}

		/// <summary>
		/// The same comparison for a <b>translucent</b> color, within one byte level. This is what holds the
		/// premultiply in <c>Graphics2DGpu.CompositeLcdMask</c> honest: the draw color has to arrive
		/// premultiplied for the modulate to land <c>color_c * cov</c>, and a composite that handed GL the
		/// straight color instead would paint this fill at roughly <c>1 / alpha</c> times its ink - far outside
		/// a level - while still passing the opaque test above.
		/// </summary>
		/// <remarks>
		/// One level of slack, not zero, and the slack is the whole point: <see cref="GL.Color4(Color)"/> takes
		/// bytes, so the premultiplied color is quantized before the GPU ever sees it where the software
		/// composite multiplies in float. That is the documented deviation, and pinning it at ±1 is what would
		/// make it a test failure if it ever grew.
		/// </remarks>
		[Test]
		public async Task MaskPassesMatchTheSoftwareCompositeForATranslucentColor()
		{
			await AssertMaskPassesMatchSoftwareComposite(new Color(220, 130, 40, 137), idBase: 46000, tolerance: 1);
		}

		/// <summary>
		/// Draws one masked fill on a GL destination, replays the recorded passes with GL's specified blend
		/// arithmetic, and requires the result to match <see cref="LcdComposite.Composite"/> within
		/// <paramref name="tolerance"/> byte levels.
		/// </summary>
		private static async Task AssertMaskPassesMatchSoftwareComposite(Color color, int idBase, int tolerance)
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;

				VertexStorage path = Rectangle(new RectangleDouble(3.4, 2.7, 18.2, 8.35));

				var fake = new RecordingGpuContext(idBase);
				var graphics = new Graphics2DGpu(new GL(fake), MaskSurfaceWidth, MaskSurfaceHeight, 1);
				fake.ResetCallRecording();
				graphics.RenderLcd(path, color);

				// The mask the composite was handed, rebuilt from the pipeline's own pieces - the same
				// comparison Graphics2DLcdTests makes for the software destination.
				bool built = BoundedMaskBuilder.TryBuild(
					MaskSurfaceWidth,
					MaskSurfaceHeight,
					path,
					Affine.NewIdentity(),
					out LcdMask mask,
					out int originX,
					out int originY,
					graphics.GetClippingRect());
				await Assert.That(built).IsTrue();
				await RequirePerChannelDivergence(mask);

				ImageBuffer expected = MidGrayPremultiplied(MaskSurfaceWidth, MaskSurfaceHeight);
				LcdComposite.Composite(expected, mask, color, originX, originY);

				await Assert.That(fake.TextureUploads.Count).IsEqualTo(3);
				await Assert.That(fake.Translates).IsEquivalentTo(new[] { ((double)originX, (double)originY, 0.0, 0) })
					.Because("the mask is placed at the builder's whole pixel origin, before any pass is drawn");

				Color modulate = fake.Color4s.Single();
				ImageBuffer framebuffer = MidGrayPremultiplied(MaskSurfaceWidth, MaskSurfaceHeight);
				(int Source, int Destination) blend = fake.BlendFuncs.Single();
				for (int pass = 0; pass < 3; pass++)
				{
					(bool Red, bool Green, bool Blue, bool Alpha) writeMask = fake.ColorMasks[pass];
					await Assert.That(writeMask.Alpha).IsFalse();

					BlendOnePass(framebuffer, fake.TextureUploads[pass], writeMask, blend, originX, originY, modulate);
				}

				for (int y = 0; y < MaskSurfaceHeight; y++)
				{
					for (int x = 0; x < MaskSurfaceWidth; x++)
					{
						Color want = expected.GetPixel(x, y);
						Color got = framebuffer.GetPixel(x, y);

						if (tolerance == 0)
						{
							await Assert.That((got.red, got.green, got.blue))
								.IsEqualTo((want.red, want.green, want.blue))
								.Because($"the three GPU passes must land the software mask composite's pixel at {x}, {y}");
							continue;
						}

						await Assert.That(Math.Abs(got.red - want.red)).IsLessThanOrEqualTo(tolerance)
							.Because($"red at {x}, {y}: GPU {got.red} against software {want.red}");
						await Assert.That(Math.Abs(got.green - want.green)).IsLessThanOrEqualTo(tolerance)
							.Because($"green at {x}, {y}: GPU {got.green} against software {want.green}");
						await Assert.That(Math.Abs(got.blue - want.blue)).IsLessThanOrEqualTo(tolerance)
							.Because($"blue at {x}, {y}: GPU {got.blue} against software {want.blue}");
					}
				}
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>
		/// One pass of the recorded command stream, applied by hand with GL's specified arithmetic, restricted
		/// to the channels the pass's write mask leaves open. The uploaded texels are R, G, B, A with row 0 at
		/// <c>t = 0</c>, and the quad is drawn 1:1 at (<paramref name="destX"/>, <paramref name="destY"/>), so
		/// texel (x, y) lands on framebuffer pixel (destX + x, destY + y).
		/// </summary>
		/// <param name="modulate">
		/// The recorded draw color, applied to the texel as the default modulate texture environment applies
		/// it - per component, alpha included. White is the identity, which is what the buffer composite draws
		/// with; the mask composite is the one that carries its color here.
		/// </param>
		/// <param name="blend">
		/// The pass's blend factors, taken from the recorded <c>glBlendFunc</c> rather than assumed. Hardcoding
		/// premultiplied source over here would let a production swap to, say, <c>SrcAlpha</c> keep producing
		/// the software composite's answer in this test.
		/// </param>
		private static void BlendOnePass(
			ImageBuffer framebuffer,
			RecordedTextureUpload upload,
			(bool Red, bool Green, bool Blue, bool Alpha) mask,
			(int Source, int Destination) blend,
			int destX = 0,
			int destY = 0,
			Color? modulate = null)
		{
			Color drawColor = modulate ?? Color.White;
			byte[] pixels = framebuffer.GetBuffer();
			int bytesPerPixel = framebuffer.GetBytesBetweenPixelsInclusive();

			for (int y = 0; y < upload.Height; y++)
			{
				int destinationY = destY + y;
				if (destinationY < 0 || destinationY >= framebuffer.Height)
				{
					continue;
				}

				int rowOffset = framebuffer.GetBufferOffsetXY(0, destinationY);
				for (int x = 0; x < upload.Width; x++)
				{
					int destinationX = destX + x;
					if (destinationX < 0 || destinationX >= framebuffer.Width)
					{
						continue;
					}

					(byte Red, byte Green, byte Blue, byte Alpha) texel = upload.Texel(x, y);

					// The default modulate texture environment: source = texel * draw color, per component.
					float sourceAlpha = (texel.Alpha / 255.0f) * (drawColor.alpha / 255.0f);
					float sourceRed = (texel.Red / 255.0f) * (drawColor.red / 255.0f);
					float sourceGreen = (texel.Green / 255.0f) * (drawColor.green / 255.0f);
					float sourceBlue = (texel.Blue / 255.0f) * (drawColor.blue / 255.0f);
					int offset = rowOffset + (destinationX * bytesPerPixel);

					if (mask.Red)
					{
						pixels[offset + ImageBuffer.OrderR] = Blend(sourceRed, pixels[offset + ImageBuffer.OrderR], sourceAlpha, blend);
					}

					if (mask.Green)
					{
						pixels[offset + ImageBuffer.OrderG] = Blend(sourceGreen, pixels[offset + ImageBuffer.OrderG], sourceAlpha, blend);
					}

					if (mask.Blue)
					{
						pixels[offset + ImageBuffer.OrderB] = Blend(sourceBlue, pixels[offset + ImageBuffer.OrderB], sourceAlpha, blend);
					}
				}
			}
		}

		/// <summary>
		/// GL's blend equation for one channel: <c>source * sourceFactor + destination * destinationFactor</c>.
		/// </summary>
		private static byte Blend(float source, byte destination, float sourceAlpha, (int Source, int Destination) blend)
		{
			float blended = (source * BlendFactor(blend.Source, sourceAlpha))
				+ ((destination / 255.0f) * BlendFactor(blend.Destination, sourceAlpha));
			return (byte)Math.Clamp((blended * 255.0f) + 0.5f, 0.0f, 255.0f);
		}

		/// <summary>
		/// One <c>glBlendFunc</c> factor as a multiplier. Only the source-alpha family is spelled out - a
		/// composite that reached for a factor outside it would be doing something this test has no model of,
		/// and should say so rather than quietly evaluating to something.
		/// </summary>
		private static float BlendFactor(int glFactor, float sourceAlpha)
		{
			// BlendingFactorSrc and BlendingFactorDest share GL's values, so one table serves both sides.
			switch (glFactor)
			{
				case (int)BlendingFactorSrc.Zero:
					return 0.0f;

				case (int)BlendingFactorSrc.One:
					return 1.0f;

				case (int)BlendingFactorSrc.SrcAlpha:
					return sourceAlpha;

				case (int)BlendingFactorSrc.OneMinusSrcAlpha:
					return 1.0f - sourceAlpha;

				default:
					throw new NotSupportedException($"This test does not model GL blend factor {glFactor}.");
			}
		}

		/// <summary>A buffer whose three channels hold visibly different coverage at every pixel.</summary>
		private static LcdBuffer BufferWithDistinctChannels()
		{
			var buffer = new LcdBuffer(BufferWidth, BufferHeight);
			buffer.CompositeMask(SolidMask(BufferWidth, BufferHeight, 40, 150, 250), new Color(200, 100, 50), 0, 0);
			return buffer;
		}

		/// <summary>
		/// A buffer painted through the real LCD pipeline: a fractionally placed fill on a transparent
		/// buffer, which is what leaves <b>both</b> planes diverging per channel along the edges.
		/// </summary>
		/// <remarks>
		/// Deliberately not cleared to an opaque background first. That would drive every channel's alpha to
		/// 255 (<c>ea_c + 255 * (1 - ea_c)</c>), leaving the alpha plane uniform and the composite unable to
		/// tell one channel's coverage from another's - a fixture that cannot fail on a mis-selected alpha.
		/// <see cref="RequirePerChannelDivergence"/> holds that property.
		/// </remarks>
		private static LcdBuffer BufferWithLcdText()
		{
			var buffer = new LcdBuffer(24, 12);
			var graphics = new LcdBufferGraphics2D(buffer);
			graphics.Render(new RoundedRect(3.4, 2.7, 18.2, 8.35, 0), new Color(220, 130, 40));
			return buffer;
		}

		/// <summary>
		/// Fails unless <paramref name="buffer"/> actually holds pixels whose three channel alphas differ and
		/// pixels whose three premultiplied colors differ. Without both, a composite that read the wrong
		/// channel would still produce the right answer and the test around it would prove nothing.
		/// </summary>
		private static async Task RequirePerChannelDivergence(LcdBuffer buffer)
		{
			bool alphaDiverges = false;
			bool colorDiverges = false;

			for (int y = 0; y < buffer.Height; y++)
			{
				for (int x = 0; x < buffer.Width; x++)
				{
					int offset = buffer.PixelOffset(x, y);
					alphaDiverges |= buffer.AlphaPlane[offset] != buffer.AlphaPlane[offset + 1]
						|| buffer.AlphaPlane[offset + 1] != buffer.AlphaPlane[offset + 2];
					colorDiverges |= buffer.ColorPlane[offset] != buffer.ColorPlane[offset + 1]
						|| buffer.ColorPlane[offset + 1] != buffer.ColorPlane[offset + 2];
				}
			}

			await Assert.That(alphaDiverges).IsTrue()
				.Because("a uniform alpha plane cannot tell a per-channel composite from a single-alpha one");
			await Assert.That(colorDiverges).IsTrue()
				.Because("a uniform color plane cannot tell a per-channel composite from a single-alpha one");
		}

		/// <summary>A mask with flat, per-channel-distinct coverage everywhere.</summary>
		private static LcdMask SolidMask(int width, int height, byte red, byte green, byte blue)
		{
			var mask = new LcdMask(width, height);
			for (int offset = 0; offset < mask.Data.Length; offset += 3)
			{
				mask.Data[offset] = red;
				mask.Data[offset + 1] = green;
				mask.Data[offset + 2] = blue;
			}

			return mask;
		}

		/// <summary>Full coverage along row 0 and nothing above it.</summary>
		private static LcdMask BottomRowMask(int width, int height)
		{
			var mask = new LcdMask(width, height);
			for (int x = 0; x < width; x++)
			{
				int offset = mask.PixelOffset(x, 0);
				mask.Data[offset] = 255;
				mask.Data[offset + 1] = 255;
				mask.Data[offset + 2] = 255;
			}

			return mask;
		}

		/// <summary>
		/// The mask twin of the buffer check above: fails unless <paramref name="mask"/> holds pixels whose
		/// three channel coverages differ, without which a composite that read the wrong channel would still
		/// produce the right answer.
		/// </summary>
		private static async Task RequirePerChannelDivergence(LcdMask mask)
		{
			bool coverageDiverges = false;

			for (int offset = 0; offset < mask.Data.Length; offset += 3)
			{
				coverageDiverges |= mask.Data[offset] != mask.Data[offset + 1]
					|| mask.Data[offset + 1] != mask.Data[offset + 2];
			}

			await Assert.That(coverageDiverges).IsTrue()
				.Because("a mask with no per-channel coverage cannot tell a subpixel composite from a gray one");
		}

		/// <summary>
		/// An opaque mid gray destination in the widget backbuffer's premultiplied convention - a neutral
		/// background where a per-channel composite and a collapsed one visibly disagree.
		/// </summary>
		private static ImageBuffer MidGrayPremultiplied(int width, int height)
		{
			var image = new ImageBuffer(width, height, 32, new BlenderPreMultBGRA());
			image.NewGraphics2D().Clear(new Color(96, 112, 128));
			return image;
		}

		/// <summary>A closed rectangle path, the plainest fill the LCD pipeline takes.</summary>
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
		/// A rectangle that can name itself, so an ordinary <see cref="Graphics2D.Render"/> of it is eligible
		/// for the cached LCD path. Nothing here knows about LCD, which is the point - the same class stands in
		/// for a glyph run in the software tests.
		/// </summary>
		private class IdentifiedRectangle : VertexSourceLegacySupport, IVertexSourceRenderIdentity
		{
			private readonly RectangleDouble rect;

			private readonly VertexStorage path;

			internal IdentifiedRectangle(RectangleDouble rect)
			{
				this.rect = rect;
				this.path = Rectangle(rect);
			}

			public object RenderIdentity => this.rect;

			public override IEnumerable<VertexData> Vertices()
			{
				return this.path.Vertices();
			}
		}
	}
}
