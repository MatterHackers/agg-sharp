//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026 Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
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
	public partial class GuiWidget
	{
		private bool doubleBuffer;

		private ImageBuffer backBuffer;

		/// <summary>
		/// The <see cref="BackbufferMode.LcdCoverage"/> alternative to <see cref="backBuffer"/>: allocated
		/// lazily on the first paint that chooses that mode, and released again when a paint chooses
		/// <see cref="BackbufferMode.Rgba"/>. Only one of the two ever holds live pixels.
		/// </summary>
		private LcdBuffer lcdBackBuffer;

		/// <summary>
		/// The mode the pixels currently in the backbuffer were painted in. A paint that resolves a different
		/// mode has to re-raster, because the two representations are not convertible in the direction that
		/// matters (an RGBA buffer has no per-channel coverage to recover).
		/// </summary>
		private BackbufferMode backBufferMode = BackbufferMode.Rgba;

		/// <summary>
		/// <see cref="LcdRenderSettings.Epoch"/> as of the last raster, so a change to the filter's style
		/// parameters reaches a clean backbuffer that would otherwise keep compositing pixels rastered under
		/// the old settings. The reference's <c>typography_epoch</c> on <c>BackbufferCache</c>, for the same
		/// reason.
		/// </summary>
		private long backBufferLcdEpoch;

		/// <summary>
		/// Gets the backBuffer object for widgets that are double buffered.  It will return null if they are not.
		/// </summary>
		/// <remarks>
		/// Also null while the widget's pixels are in <see cref="BackbufferMode.LcdCoverage"/>: those live in
		/// two coverage planes, not in an <see cref="ImageBuffer"/>, and there is no lossless
		/// <see cref="ImageBuffer"/> to hand back. Returning the last RGBA buffer instead would serve pixels
		/// from whenever the widget was last painted the other way, which is worse than nothing. A caller that
		/// wants pixels regardless has to either keep LCD rendering off (the default) or collapse the planes
		/// itself through <see cref="LcdBuffer.ToImageBufferCollapsed"/>.
		/// </remarks>
		public ImageBuffer BackBuffer
		{
			get
			{
				if (DoubleBuffer
					&& backBufferMode == BackbufferMode.Rgba)
				{
					return backBuffer;
				}

				return null;
			}
		}

		public bool DoubleBuffer
		{
			get => doubleBuffer;
			set
			{
				if (this.DoubleBuffer != value)
				{
					doubleBuffer = value;
					if (doubleBuffer)
					{
						AllocateBackBuffer();
					}
					else
					{
						backBuffer = null;
						lcdBackBuffer = null;

						// The recorded mode has to drop with the pixels it describes: leaving it on
						// LcdCoverage would let a later paint that resolves the same mode skip the re-raster
						// and composite a buffer that is no longer there.
						backBufferMode = BackbufferMode.Rgba;
					}

					Invalidate();
				}
			}
		}

		/// <summary>
		/// Which backbuffer representation this widget should be painted into, given the surface it will be
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
		/// on the next frame rather than at construction.
		/// </para>
		/// </remarks>
		public BackbufferMode ResolveBackbufferMode(Graphics2D destination)
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
		/// Paints this widget into its backbuffer in <paramref name="mode"/>, allocating whichever of the two
		/// representations that takes and releasing the other.
		/// </summary>
		/// <param name="mode">The representation to raster into, as
		/// <see cref="ResolveBackbufferMode"/> decided it for this paint.</param>
		/// <param name="extraWidth">Extra column for a fractional horizontal placement, 0 or 1.</param>
		/// <param name="extraHeight">Extra row for a fractional vertical placement, 0 or 1.</param>
		/// <param name="transformToBuffer">Widget space to buffer space, including that fractional part.</param>
		/// <remarks>
		/// The two arms are deliberately symmetric about the buffer they do not use: each releases the other,
		/// so a widget never holds two buffers' worth of pixels and can never composite a representation it
		/// stopped painting into.
		/// </remarks>
		private void RasterizeBackbuffer(BackbufferMode mode, int extraWidth, int extraHeight, Affine transformToBuffer)
		{
			if (mode == BackbufferMode.LcdCoverage)
			{
				AllocateLcdBackBuffer(extraWidth, extraHeight);
				backBuffer = null;

				// Cleared to fully transparent in both planes, exactly as the RGBA arm is: the widget's own
				// opaque background is the first thing painted over it, and anywhere it does not reach carries
				// no coverage and leaves the destination alone.
				var lcdBufferGraphics2D = new LcdBufferGraphics2D(lcdBackBuffer);
				lcdBufferGraphics2D.Clear(new Color(0, 0, 0, 0));
				lcdBufferGraphics2D.SetTransform(transformToBuffer);
				OnDrawBackground(lcdBufferGraphics2D);
				OnDraw(lcdBufferGraphics2D);

				// The twin of the RGBA arm's MarkImageChanged below. Defensive rather than load-bearing as it
				// stands: a widget re-rasters into the same LcdBuffer instance whenever its size did not
				// change, and the GPU composite's per-channel texture cache has only the stamp to tell this
				// frame's pixels from last frame's - but the Clear above already bumps it, and so does any
				// paint. It stands as the stamp of record for a repaint that ends up drawing nothing at all.
				lcdBackBuffer.MarkChanged();

				return;
			}

			// The buffer can be missing even when no extra row or column is wanted, because the previous paint
			// may have been the LCD arm, which drops it.
			if (backBuffer == null
				|| extraWidth > 0
				|| extraHeight > 0)
			{
				AllocateBackBuffer(extraWidth, extraHeight);
			}

			lcdBackBuffer = null;

			Graphics2D backBufferGraphics2D = backBuffer.NewGraphics2D();

			// The validity gate (LCD plan section 4): these pixels get blended onto the parent later, so
			// subpixel geometry computed here would be geometry against content the R/G/B phase knows nothing
			// about. Text drawn into this buffer takes the chroma-free arm of the same pipeline automatically.
			backBufferGraphics2D.IsTransparentCompositingLayer = true;
			backBufferGraphics2D.Clear(new Color(0, 0, 0, 0));
			backBufferGraphics2D.SetTransform(transformToBuffer);
			OnDrawBackground(backBufferGraphics2D);
			OnDraw(backBufferGraphics2D);

			backBuffer.MarkImageChanged();
		}

		/// <summary>
		/// Paints this widget's cached pixels onto <paramref name="graphics2D"/>, in whichever representation
		/// they were last rastered in.
		/// </summary>
		/// <param name="graphics2D">The parent surface, with its transform already set to place the buffer.</param>
		/// <param name="offsetToRenderSurface">Where the buffer's bottom-left pixel lands, in whole
		/// destination pixels (the caller has already insisted it is integer).</param>
		/// <param name="scaleX">Horizontal scale for the RGBA blit; ignored by the LCD arm, which only runs at
		/// exact unit scale (see <see cref="ResolveBackbufferMode"/>).</param>
		/// <param name="scaleY">The vertical twin of <paramref name="scaleX"/>.</param>
		private void CompositeBackbufferOnto(Graphics2D graphics2D, Vector2 offsetToRenderSurface, double scaleX, double scaleY)
		{
			// Keyed on what is in the buffer rather than on what this paint resolved: the two agree, because a
			// mode flip forces the re-raster above, and keying on the pixels cannot composite a buffer that was
			// never painted.
			if (backBufferMode == BackbufferMode.LcdCoverage)
			{
				// Whole destination pixels, not a transformed blit: the planes are finished pixels, and
				// resampling them would smear each channel's phase into its neighbours. Nothing is lost by
				// dropping the scale, because this arm only runs at exact unit scale.
				graphics2D.CompositeLcdBuffer(lcdBackBuffer, (int)offsetToRenderSurface.X, (int)offsetToRenderSurface.Y);
			}
			else
			{
				graphics2D.Render(backBuffer, 0, 0, 0, scaleX, scaleY);
			}
		}

		private void AllocateBackBuffer()
		{
			AllocateBackBuffer(0, 0);
		}

		private void AllocateBackBuffer(int extraWidth, int extraHeight)
		{
			GetBackBufferSize(extraWidth, extraHeight, out int intWidth, out int intHeight);
			if (backBuffer == null || backBuffer.Width != intWidth || backBuffer.Height != intHeight)
			{
				backBuffer = new ImageBuffer(intWidth, intHeight, 32, new BlenderPreMultBGRA());
			}
		}

		/// <summary>
		/// The <see cref="BackbufferMode.LcdCoverage"/> twin of <see cref="AllocateBackBuffer(int, int)"/>:
		/// same size, two coverage planes instead of one premultiplied BGRA image.
		/// </summary>
		private void AllocateLcdBackBuffer(int extraWidth, int extraHeight)
		{
			GetBackBufferSize(extraWidth, extraHeight, out int intWidth, out int intHeight);
			if (lcdBackBuffer == null || lcdBackBuffer.Width != intWidth || lcdBackBuffer.Height != intHeight)
			{
				lcdBackBuffer = new LcdBuffer(intWidth, intHeight);
			}
		}

		/// <summary>
		/// Pixel size of this widget's backbuffer: its bounds rounded out to whole pixels, plus the extra row
		/// and column a fractional screen placement needs.
		/// </summary>
		private void GetBackBufferSize(int extraWidth, int extraHeight, out int intWidth, out int intHeight)
		{
			RectangleDouble localBounds = LocalBounds;
			intWidth = Max((int)(Ceiling(localBounds.Right) - Floor(localBounds.Left)) + extraWidth, 1);
			intHeight = Max((int)(Ceiling(localBounds.Top) - Floor(localBounds.Bottom)) + extraHeight, 1);
		}
	}
}
