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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using static System.Math;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The double-buffering half of <see cref="GuiWidget"/>: which representation a widget's cached pixels
	/// live in (<see cref="BackbufferMode"/>), how that buffer is allocated and rastered, and how it is
	/// composited back onto the parent surface.
	/// </summary>
	/// <remarks>
	/// Internal, and owned by exactly one widget: everything a caller outside the assembly ever reached stays
	/// on <see cref="GuiWidget"/> itself (<see cref="GuiWidget.DoubleBuffer"/>,
	/// <see cref="GuiWidget.BackBuffer"/>, <see cref="GuiWidget.ResolveBackbufferMode"/>), so this type is
	/// free to change shape. A widget builds one when double buffering is turned on and drops it when it is
	/// turned off, which keeps the great majority of widgets - the un-buffered ones - carrying nothing but a
	/// null field.
	/// </remarks>
	internal class WidgetBackbuffer
	{
		private readonly GuiWidget widget;

		private ImageBuffer backBuffer;

		/// <summary>
		/// The <see cref="BackbufferMode.LcdCoverage"/> alternative to <see cref="backBuffer"/>: allocated
		/// lazily on the first paint that chooses that mode, and released again when a paint chooses
		/// <see cref="BackbufferMode.Rgba"/>. Only one of the two ever holds live pixels.
		/// </summary>
		private LcdBuffer lcdBackBuffer;

		internal WidgetBackbuffer(GuiWidget widget)
		{
			this.widget = widget;
		}

		/// <summary>
		/// The mode the pixels currently in the backbuffer were painted in. A paint that resolves a different
		/// mode has to re-raster, because the two representations are not convertible in the direction that
		/// matters (an RGBA buffer has no per-channel coverage to recover).
		/// </summary>
		internal BackbufferMode Mode { get; set; } = BackbufferMode.Rgba;

		/// <summary>
		/// <see cref="LcdRenderSettings.Epoch"/> as of the last raster, so a change to the filter's style
		/// parameters reaches a clean backbuffer that would otherwise keep compositing pixels rastered under
		/// the old settings. The reference's <c>typography_epoch</c> on <c>BackbufferCache</c>, for the same
		/// reason.
		/// </summary>
		internal long LcdEpoch { get; set; }

		/// <summary>
		/// The cached pixels as an <see cref="ImageBuffer"/>, or null while they are in
		/// <see cref="BackbufferMode.LcdCoverage"/>. See <see cref="GuiWidget.BackBuffer"/> for why the LCD
		/// case answers null rather than the stale RGBA buffer.
		/// </summary>
		internal ImageBuffer RgbaBuffer => this.Mode == BackbufferMode.Rgba ? this.backBuffer : null;

		/// <summary>
		/// Which backbuffer representation a widget should be painted into, given the surface it will be
		/// composited onto. <see cref="BackbufferMode.Rgba"/> - today's behaviour, byte for byte - unless
		/// every gate opens.
		/// </summary>
		/// <param name="destination">The graphics the backbuffer will be composited onto, <b>with the
		/// transform the composite will happen under already set</b> - which is what
		/// <see cref="GuiWidget.DrawChild"/> has established by the time it asks. Null answers
		/// <see cref="BackbufferMode.Rgba"/>.</param>
		/// <remarks>
		/// The three gates, and why each one is separate:
		/// <list type="number">
		/// <item><description><see cref="LcdRenderSettings.Enabled"/> - the user setting, a lock-free volatile
		/// read, checked first so a process that never turns the feature on pays nothing
		/// else;</description></item>
		/// <item><description><see cref="Graphics2D.CanCompositeLcdBuffer"/> - the destination's capability,
		/// which is what keeps the GL path on the untouched RGBA route until it learns the per-channel
		/// composite. A destination that would have to flatten the planes gains nothing from them and would
		/// pay for the flatten every frame;</description></item>
		/// <item><description>exact unit scale - see below.</description></item>
		/// </list>
		/// <para>
		/// <b>The widget's own opacity is deliberately not a gate</b>, which is a divergence from the
		/// reference - it makes <c>BackbufferMode::LcdCoverage</c> a contract the widget opts into by
		/// promising to cover its bounds with opaque fills. agg-sharp does not need that promise, because an
		/// <see cref="LcdBuffer"/> carries per-channel <i>alpha</i> beside its per-channel colour and starts
		/// out transparent in both planes, so unpainted and part-painted pixels come back out of it exactly as
		/// they went in. Compositing it is a per-channel source-over, and source-over is associative, so a
		/// widget's ink lands where it would have landed painted straight onto the destination whether the
		/// widget covered its bounds or not.
		/// </para>
		/// <para>
		/// Requiring opacity here did not merely lose an edge case: <see cref="TextWidget"/> is
		/// double-buffered by default and draws glyphs over a transparent background, so <b>every label in an
		/// application</b> failed the gate, fell to the RGBA arm - which declares itself
		/// <see cref="Graphics2D.IsTransparentCompositingLayer"/>, and so refuses the mask pipeline
		/// outright - and the user's LCD setting reached no text at all.
		/// </para>
		/// <para>
		/// The unknown-content hazard the opacity rule was guarding against is real, but it belongs to the
		/// <i>RGBA</i> arm, where three coverages have to collapse into one alpha and the phase is genuinely
		/// lost. That arm still refuses chroma, through
		/// <see cref="Graphics2D.IsTransparentCompositingLayer"/>.
		/// </para>
		/// <para>
		/// <b>The scale gate is exact, not near.</b> The RGBA arm runs anywhere in 0.95..1.05 and passes that
		/// scale to its blit, which resamples the buffer to honour it; the LCD composite cannot do the same,
		/// because resampling finished planes smears each channel's phase into its neighbours. Dropping the
		/// scale instead is not a rounding-sized difference: at 1.04 a 300 pixel wide widget composites 12
		/// pixels narrower than it should. So the LCD arm only engages where the composite it can do - whole
		/// pixels, 1:1 - is exactly the composite the RGBA arm would have done, and every other transform,
		/// including a sheared one, falls back.
		/// </para>
		/// <para>
		/// Re-resolved every paint, like the reference's <c>backbuffer_mode()</c>, so the setting takes effect
		/// on the next frame rather than at construction. Static because it reads nothing but the destination
		/// and the global setting, which is what lets a widget answer it with no backbuffer allocated yet.
		/// </para>
		/// </remarks>
		internal static BackbufferMode ResolveMode(Graphics2D destination)
		{
			if (!LcdRenderSettings.Enabled
				|| destination == null
				|| !destination.CanCompositeLcdBuffer)
			{
				return BackbufferMode.Rgba;
			}

			Affine transform = destination.GetTransform();
			if (transform.sx != 1
				|| transform.sy != 1
				|| transform.shx != 0
				|| transform.shy != 0)
			{
				return BackbufferMode.Rgba;
			}

			return BackbufferMode.LcdCoverage;
		}

		/// <summary>
		/// Paints the owning widget into this backbuffer in <paramref name="mode"/>, allocating whichever of
		/// the two representations that takes and releasing the other.
		/// </summary>
		/// <param name="mode">The representation to raster into, as
		/// <see cref="ResolveMode"/> decided it for this paint.</param>
		/// <param name="extraWidth">Extra column for a fractional horizontal placement, 0 or 1.</param>
		/// <param name="extraHeight">Extra row for a fractional vertical placement, 0 or 1.</param>
		/// <param name="transformToBuffer">Widget space to buffer space, including that fractional part.</param>
		/// <remarks>
		/// The two arms are deliberately symmetric about the buffer they do not use: each releases the other,
		/// so a widget never holds two buffers' worth of pixels and can never composite a representation it
		/// stopped painting into.
		/// </remarks>
		internal void Rasterize(BackbufferMode mode, int extraWidth, int extraHeight, Affine transformToBuffer)
		{
			if (mode == BackbufferMode.LcdCoverage)
			{
				this.AllocateLcdBuffer(extraWidth, extraHeight);
				this.backBuffer = null;

				// Cleared to fully transparent in both planes, exactly as the RGBA arm is: the widget's own
				// opaque background is the first thing painted over it, and anywhere it does not reach carries
				// no coverage and leaves the destination alone.
				var lcdBufferGraphics2D = new LcdBufferGraphics2D(this.lcdBackBuffer);
				lcdBufferGraphics2D.Clear(new Color(0, 0, 0, 0));
				lcdBufferGraphics2D.SetTransform(transformToBuffer);
				this.widget.OnDrawBackground(lcdBufferGraphics2D);
				this.widget.OnDraw(lcdBufferGraphics2D);

				// The twin of the RGBA arm's MarkImageChanged below. Defensive rather than load-bearing as it
				// stands: a widget re-rasters into the same LcdBuffer instance whenever its size did not
				// change, and the GPU composite's per-channel texture cache has only the stamp to tell this
				// frame's pixels from last frame's - but the Clear above already bumps it, and so does any
				// paint. It stands as the stamp of record for a repaint that ends up drawing nothing at all.
				this.lcdBackBuffer.MarkChanged();

				return;
			}

			// The buffer can be missing even when no extra row or column is wanted, because the previous paint
			// may have been the LCD arm, which drops it.
			if (this.backBuffer == null
				|| extraWidth > 0
				|| extraHeight > 0)
			{
				this.AllocateRgbaBuffer(extraWidth, extraHeight);
			}

			this.lcdBackBuffer = null;

			Graphics2D backBufferGraphics2D = this.backBuffer.NewGraphics2D();

			// The validity gate (LCD plan section 4): these pixels get blended onto the parent later, so
			// subpixel geometry computed here would be geometry against content the R/G/B phase knows nothing
			// about. Text drawn into this buffer takes the chroma-free arm of the same pipeline automatically.
			backBufferGraphics2D.IsTransparentCompositingLayer = true;
			backBufferGraphics2D.Clear(new Color(0, 0, 0, 0));
			backBufferGraphics2D.SetTransform(transformToBuffer);
			this.widget.OnDrawBackground(backBufferGraphics2D);
			this.widget.OnDraw(backBufferGraphics2D);

			this.backBuffer.MarkImageChanged();
		}

		/// <summary>
		/// Paints the cached pixels onto <paramref name="graphics2D"/>, in whichever representation they were
		/// last rastered in.
		/// </summary>
		/// <param name="graphics2D">The parent surface, with its transform already set to place the buffer.</param>
		/// <param name="offsetToRenderSurface">Where the buffer's bottom-left pixel lands, in whole
		/// destination pixels (the caller has already insisted it is integer).</param>
		/// <param name="scaleX">Horizontal scale for the RGBA blit; ignored by the LCD arm, which only runs at
		/// exact unit scale (see <see cref="ResolveMode"/>).</param>
		/// <param name="scaleY">The vertical twin of <paramref name="scaleX"/>.</param>
		internal void CompositeOnto(Graphics2D graphics2D, Vector2 offsetToRenderSurface, double scaleX, double scaleY)
		{
			// Keyed on what is in the buffer rather than on what this paint resolved: the two agree, because a
			// mode flip forces the re-raster above, and keying on the pixels cannot composite a buffer that was
			// never painted.
			if (this.Mode == BackbufferMode.LcdCoverage)
			{
				// Whole destination pixels, not a transformed blit: the planes are finished pixels, and
				// resampling them would smear each channel's phase into its neighbours. Nothing is lost by
				// dropping the scale, because this arm only runs at exact unit scale.
				graphics2D.CompositeLcdBuffer(this.lcdBackBuffer, (int)offsetToRenderSurface.X, (int)offsetToRenderSurface.Y);
			}
			else
			{
				graphics2D.Render(this.backBuffer, 0, 0, 0, scaleX, scaleY);
			}
		}

		internal void AllocateRgbaBuffer()
		{
			this.AllocateRgbaBuffer(0, 0);
		}

		private void AllocateRgbaBuffer(int extraWidth, int extraHeight)
		{
			this.GetSize(extraWidth, extraHeight, out int intWidth, out int intHeight);
			if (this.backBuffer == null || this.backBuffer.Width != intWidth || this.backBuffer.Height != intHeight)
			{
				this.backBuffer = new ImageBuffer(intWidth, intHeight, 32, new BlenderPreMultBGRA());
			}
		}

		/// <summary>
		/// The <see cref="BackbufferMode.LcdCoverage"/> twin of <see cref="AllocateRgbaBuffer(int, int)"/>:
		/// same size, two coverage planes instead of one premultiplied BGRA image.
		/// </summary>
		private void AllocateLcdBuffer(int extraWidth, int extraHeight)
		{
			this.GetSize(extraWidth, extraHeight, out int intWidth, out int intHeight);
			if (this.lcdBackBuffer == null || this.lcdBackBuffer.Width != intWidth || this.lcdBackBuffer.Height != intHeight)
			{
				this.lcdBackBuffer = new LcdBuffer(intWidth, intHeight);
			}
		}

		/// <summary>
		/// Pixel size of the widget's backbuffer: its bounds rounded out to whole pixels, plus the extra row
		/// and column a fractional screen placement needs.
		/// </summary>
		private void GetSize(int extraWidth, int extraHeight, out int intWidth, out int intHeight)
		{
			RectangleDouble localBounds = this.widget.LocalBounds;
			intWidth = Max((int)(Ceiling(localBounds.Right) - Floor(localBounds.Left)) + extraWidth, 1);
			intHeight = Max((int)(Ceiling(localBounds.Top) - Floor(localBounds.Bottom)) + extraHeight, 1);
		}
	}
}
