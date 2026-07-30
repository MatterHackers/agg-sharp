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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// The drawing surface over an <see cref="LcdBuffer"/>: an ordinary <see cref="Graphics2D"/> as far as
	/// its callers are concerned, but every fill it takes goes through the LCD pipeline (3x horizontal
	/// raster, 5-tap filter, per-channel premultiplied source-over) instead of the scanline renderer.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference's <c>LcdGfxCtx</c> (<c>lcd_gfx_ctx.rs</c>), which its widget
	/// layer hands to a subtree whose backbuffer is in <c>BackbufferMode::LcdCoverage</c>. The load-bearing
	/// property, and the reason this is a whole Graphics2D rather than a text hook, is that <b>every</b>
	/// primitive lands in the same per-channel representation: a widget's background rect, its border stroke
	/// and its text all go through the identical mask pipeline, so nothing painted into the buffer has to
	/// know the buffer is not a normal one. Text is a caller, not a special case.
	/// <para>
	/// <b>Images are the one exception</b>, in the reference too (<c>lcd_gfx_ctx\image.rs</c>): bitmap data
	/// is colour, not coverage, so it composites with its source alpha applied equally to all three channels
	/// rather than through the filter. Running an icon through a 3x horizontal supersample would tint its
	/// sharp edges with R/G/B fringes - the convention every LCD text renderer follows, where subpixel
	/// treatment is for glyph coverage only.
	/// </para>
	/// <para>
	/// The buffer this draws into is transparent in both planes until something paints, so a caller that
	/// wants LCD chroma to be <i>valid</i> owes it an opaque background covering the whole surface first (see
	/// <see cref="Graphics2D.IsTransparentCompositingLayer"/> for the other half of that gate). Painting less
	/// than that is not an error and not corruption - the uncovered pixels simply carry no coverage and leave
	/// the eventual destination alone - it just means the subpixel geometry was computed against pixels that
	/// get blended again later.
	/// </para>
	/// </remarks>
	public class LcdBufferGraphics2D : Graphics2D
	{
		private readonly LcdBuffer buffer;

		/// <summary>
		/// Wraps <paramref name="buffer"/>, clipped to its full extent - the same starting clip
		/// <see cref="ImageBuffer.NewGraphics2D"/> gives an image destination, and one the LCD path needs
		/// because it reads its clip back through <see cref="Graphics2D.GetClippingRect"/>.
		/// </summary>
		public LcdBufferGraphics2D(LcdBuffer buffer)
		{
			this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

			var scanlineRasterizer = new ScanlineRasterizer();
			scanlineRasterizer.SetVectorClipBox(0, 0, buffer.Width, buffer.Height);

			// Assigned rather than passed through Initialize: that overload wants an IImageByte destination,
			// and this one has none - the two planes are the destination. Everything the fill path reads from
			// the rasterizer (the fill rule, the clip box) works the same either way.
			this.rasterizer = scanlineRasterizer;
		}

		/// <summary>The buffer being painted into. Live, not a copy.</summary>
		public LcdBuffer Buffer => this.buffer;

		/// <inheritdoc/>
		public override int Width => this.buffer.Width;

		/// <inheritdoc/>
		public override int Height => this.buffer.Height;

		/// <summary>
		/// Null unless someone assigns one, which nothing here does: the abstract property exists for
		/// destinations that render through the scanline renderer, and every fill this class takes goes
		/// through the LCD pipeline instead. Allocating a cache that no code path reads would be a per
		/// instance allocation to no purpose.
		/// </summary>
		public override IScanlineCache ScanlineCache { get; set; }

		/// <summary>
		/// Always true: the render target <b>is</b> per-channel coverage, so a mask is the most direct thing
		/// this destination can be handed. Mirrors the reference's
		/// <c>LcdGfxCtx::has_lcd_mask_composite</c> returning true unconditionally.
		/// </summary>
		public override bool CanCompositeLcd => true;

		/// <inheritdoc/>
		public override RectangleDouble GetClippingRect()
		{
			return this.rasterizer.GetVectorClipBox();
		}

		/// <inheritdoc/>
		public override void SetClippingRect(RectangleDouble clippingRect)
		{
			this.rasterizer.SetVectorClipBox(clippingRect);
		}

		/// <summary>
		/// Replaces every pixel of the clipping rect with <paramref name="color"/> in both planes.
		/// </summary>
		/// <remarks>
		/// A replace, not a paint, exactly as <see cref="ImageGraphics2D.Clear(IColorType)"/> is - and as the
		/// reference's <c>clear</c> is. A flat clear has no per-subpixel differentiation, so all three
		/// channels take the same alpha and the same premultiplied colour.
		/// </remarks>
		public override void Clear(IColorType color)
		{
			Clear(GetClippingRect(), color);
		}

		/// <inheritdoc cref="Clear(IColorType)"/>
		/// <remarks>
		/// The bounds are intersected with <see cref="GetClippingRect"/>, as
		/// <see cref="ImageGraphics2D.Clear(RectangleDouble, IColorType)"/> intersects its own: a clear
		/// replaces pixels rather than painting them, but it still may not reach outside the clip the caller
		/// set, or a clipped widget clearing its background would wipe out its siblings.
		/// </remarks>
		public override void Clear(RectangleDouble bounds, IColorType color)
		{
			var pixelBounds = new RectangleInt(
				Math.Max(SaturatingMath.Floor(bounds.Left), 0),
				Math.Max(SaturatingMath.Floor(bounds.Bottom), 0),
				Math.Min(SaturatingMath.Ceiling(bounds.Right), this.buffer.Width),
				Math.Min(SaturatingMath.Ceiling(bounds.Top), this.buffer.Height));

			// Through ToPixelClip so the clip rounds outward here exactly as it does for a mask composite -
			// two roundings of the same rect that disagreed would put a boundary pixel in one and out of the
			// other.
			if (LcdBuffer.ToPixelClip(GetClippingRect()) is RectangleInt pixelClip)
			{
				pixelBounds.IntersectWithRectangle(pixelClip);
			}

			if (pixelBounds.Left >= pixelBounds.Right || pixelBounds.Bottom >= pixelBounds.Top)
			{
				return;
			}

			if (pixelBounds.Left == 0
				&& pixelBounds.Bottom == 0
				&& pixelBounds.Right == this.buffer.Width
				&& pixelBounds.Top == this.buffer.Height)
			{
				this.buffer.Clear(color.ToColor());
				return;
			}

			Color fill = color.ToColor();
			byte alpha = fill.alpha;
			byte red = Premultiply(fill.red, alpha);
			byte green = Premultiply(fill.green, alpha);
			byte blue = Premultiply(fill.blue, alpha);

			// This arm writes the planes directly rather than through a method on the buffer, so it owes the
			// change stamp by hand - see LcdBuffer.ChangedCount.
			this.buffer.MarkChanged();

			for (int y = pixelBounds.Bottom; y < pixelBounds.Top; y++)
			{
				int offset = this.buffer.PixelOffset(pixelBounds.Left, y);
				for (int x = pixelBounds.Left; x < pixelBounds.Right; x++, offset += 3)
				{
					this.buffer.ColorPlane[offset] = red;
					this.buffer.ColorPlane[offset + 1] = green;
					this.buffer.ColorPlane[offset + 2] = blue;
					this.buffer.AlphaPlane[offset] = alpha;
					this.buffer.AlphaPlane[offset + 1] = alpha;
					this.buffer.AlphaPlane[offset + 2] = alpha;
				}
			}
		}

		/// <inheritdoc/>
		public override void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor)
		{
			Render(new RoundedRect(left, bottom, right, top, 0), fillColor.ToColor());
		}

		/// <inheritdoc/>
		public override void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth = -1)
		{
			// The half-pixel inset and the stroke are ImageGraphics2D's, so an outline drawn into an LCD
			// backbuffer covers the same pixels it would have on a normal one.
			var rect = new RoundedRect(left + .5, bottom + .5, right - .5, top - .5, 0);

			Render(new Stroke(rect, strokeWidth), color);
		}

		/// <summary>
		/// Every ordinary fill: rasterized through the LCD pipeline and composited per channel, rather than
		/// through the scanline renderer.
		/// </summary>
		/// <remarks>
		/// This is what makes the mode a property of the <i>buffer</i> and not of the caller. It is reached
		/// for everything the cached path in <see cref="Graphics2D.Render(IVertexSource, IColorType)"/> did
		/// not take - a rect, a stroke, an unidentified path - and lands in the same
		/// <see cref="CompositeLcdMask"/> that path uses, so the two differ only in whether the mask came out
		/// of the cache.
		/// <para>
		/// The mask is trimmed to this buffer and the clip as it is built, which is why no clip is passed on
		/// to the composite (see <see cref="Graphics2D.RenderLcd"/>, which does the same).
		/// <see cref="LcdChromaAllowed"/> chooses the LCD filter or its chroma-free sibling, so a buffer
		/// flagged <see cref="Graphics2D.IsTransparentCompositingLayer"/> paints the same coverage with
		/// r == g == b.
		/// </para>
		/// <para>
		/// <b>The effective-scale cap does not apply here</b>, where <see cref="Graphics2D.RenderLcd"/> checks
		/// it (<see cref="LcdRenderSettings.MaxEffectiveScale"/>) before taking the pipeline at all. That is
		/// reference parity, not an oversight: <c>LcdGfxCtx::fill</c> has no scale check, because the mode is
		/// chosen once for the whole buffer and a fill inside it cannot opt out - the buffer has no other
		/// representation to fall back to. The gate that keeps a magnified subtree off this path is one level
		/// up, in <c>GuiWidget.ResolveBackbufferMode</c>, which refuses any transform that is not unit scale.
		/// </para>
		/// </remarks>
		protected override void RenderVertexSource(IVertexSource vertexSource, IColorType colorType)
		{
			if (!BoundedMaskBuilder.TryBuild(
				this.buffer.Width,
				this.buffer.Height,
				vertexSource,
				GetTransform(),
				out LcdMask mask,
				out int originX,
				out int originY,
				GetClippingRect(),
				this.rasterizer.FillingRule,
				LcdRenderSettings.PrimaryWeight,
				LcdRenderSettings.Gamma,
				!this.LcdChromaAllowed))
			{
				// Nothing to paint: off the buffer, entirely clipped away, or an empty path.
				return;
			}

			CompositeLcdMask(mask, colorType.ToColor(), originX, originY);
		}

		/// <inheritdoc/>
		protected override void CompositeLcdMask(LcdMask mask, Color color, int originX, int originY, RectangleDouble? clip = null)
		{
			this.buffer.CompositeMask(mask, color, originX, originY, LcdBuffer.ToPixelClip(clip));
		}

		/// <summary>
		/// Blits a 32 bit-per-pixel image, applying its source alpha equally to all three channels - colour
		/// data, not coverage, so it does <b>not</b> go through the filter.
		/// </summary>
		/// <remarks>
		/// Ported from the reference's <c>lcd_gfx_ctx\image.rs</c>, including its limits, which are the
		/// reference's own and not new ones: the placement is the current transform applied to
		/// (<paramref name="x"/>, <paramref name="y"/>), less the source's hotspot, rounded to whole pixels;
		/// the size is the image scaled by <paramref name="scaleX"/> / <paramref name="scaleY"/> and the
		/// transform's axis scale; and sampling is nearest-neighbour. Rotation -
		/// <paramref name="angleRadians"/>, or shear in the transform - is not applied; the widget backbuffer
		/// path this exists for is axis-aligned at unit scale by construction (see
		/// <c>GuiWidget.ResolveBackbufferMode</c>), so a rotated image blit cannot reach here from a widget.
		/// <para>
		/// <b>The source's alpha convention is read off its blender</b>, in agg-sharp's B, G, R, A byte order:
		/// <see cref="BlenderPreMultBGRA"/> means the colour bytes are already multiplied by their alpha and
		/// are used as they are, <see cref="BlenderBGRA"/> means they are straight and get multiplied here.
		/// Getting this wrong is not subtle in the direction that matters - treating premultiplied bytes as
		/// straight multiplies by alpha twice and paints anti-aliased edges at half their ink - and both of
		/// the callers this method actually has hand it premultiplied data: the hinted glyph images
		/// <c>TypeFacePrinter</c> blits out of its cache, and a nested widget's RGBA backbuffer. The
		/// <see cref="ImageBuffer"/> destinations reached through <see cref="ImageGraphics2D"/> key on the
		/// same distinction, in their blender rather than by hand.
		/// </para>
		/// <para>
		/// A source whose blender is neither of those, or whose bit depth is not 32, is skipped rather than
		/// approximated: there is no defensible per-channel meaning for 8 or 24 bit pixels here, and no way to
		/// tell which convention an unrecognized 32 bit blender's bytes are in. Guessing would paint something
		/// subtly wrong on every frame, which is worse than painting nothing once.
		/// </para>
		/// <para>
		/// <see cref="IImage.OriginOffset"/> is honoured as the hotspot, subtracted after scaling, which is
		/// exactly what <see cref="ImageGraphics2D.Render(IImageByte, double, double, double, double, double)"/>
		/// does through its <c>DrawImageGetDestBounds</c>: an <c>ImageSequence</c> frame with a centered origin
		/// has to land centered here too, or the same animation would jump when a widget's backbuffer changed
		/// mode.
		/// </para>
		/// <para>
		/// There is no row flip, where the reference has one: its image data is top-row-first, and
		/// <see cref="IImageByte"/> is Y-up like the buffer it is being painted into.
		/// </para>
		/// </remarks>
		public override void Render(
			IImageByte imageSource,
			double x,
			double y,
			double angleRadians,
			double scaleX,
			double scaleY)
		{
			if (imageSource == null)
			{
				throw new ArgumentNullException(nameof(imageSource));
			}

			if (imageSource.BitDepth != 32
				|| imageSource.Width <= 0
				|| imageSource.Height <= 0)
			{
				return;
			}

			bool? premultipliedSource = SourceIsPremultiplied(imageSource);
			if (premultipliedSource == null)
			{
				// An unrecognized blender - see the remarks. Skipped on the same grounds as a non-32bpp source.
				return;
			}

			bool sourceIsPremultiplied = premultipliedSource.Value;

			Affine transform = GetTransform();

			// One effective scale for both the painted size and the hotspot: scaling the size by the transform
			// but not the hotspot would slide the image by a fraction of its own dimensions.
			double effectiveScaleX = scaleX * transform.sx;
			double effectiveScaleY = scaleY * transform.sy;

			double placedX = (x * transform.sx) + (y * transform.shx) + transform.tx - (imageSource.OriginOffset.X * effectiveScaleX);
			double placedY = (x * transform.shy) + (y * transform.sy) + transform.ty - (imageSource.OriginOffset.Y * effectiveScaleY);

			// Half away from zero, not Math.Round's default banker's rounding: the reference rounds
			// (lcd_gfx_ctx\image.rs:31-35) with Rust's f64::round, which is half away from zero, and LcdFilter
			// quantizes the same way for the same reason - a .5 that lands on the even neighbour moves an
			// image by a whole pixel depending on where it happens to be.
			int originX = (int)Math.Round(placedX, MidpointRounding.AwayFromZero);
			int originY = (int)Math.Round(placedY, MidpointRounding.AwayFromZero);
			int paintedWidth = (int)Math.Round(Math.Abs(imageSource.Width * effectiveScaleX), MidpointRounding.AwayFromZero);
			int paintedHeight = (int)Math.Round(Math.Abs(imageSource.Height * effectiveScaleY), MidpointRounding.AwayFromZero);
			if (paintedWidth <= 0 || paintedHeight <= 0)
			{
				return;
			}

			RectangleInt? pixelClip = LcdBuffer.ToPixelClip(GetClippingRect());
			int clipLeft = Math.Max(pixelClip?.Left ?? 0, 0);
			int clipBottom = Math.Max(pixelClip?.Bottom ?? 0, 0);
			int clipRight = Math.Min(pixelClip?.Right ?? this.buffer.Width, this.buffer.Width);
			int clipTop = Math.Min(pixelClip?.Top ?? this.buffer.Height, this.buffer.Height);
			if (clipLeft >= clipRight || clipBottom >= clipTop)
			{
				return;
			}

			byte[] source = imageSource.GetBuffer();
			int sourceBytesPerPixel = imageSource.GetBytesBetweenPixelsInclusive();

			// Direct plane writes below, so the change stamp is owed by hand - see LcdBuffer.ChangedCount.
			this.buffer.MarkChanged();

			for (int row = 0; row < paintedHeight; row++)
			{
				int destinationY = originY + row;
				if (destinationY < clipBottom || destinationY >= clipTop)
				{
					continue;
				}

				// Sample the middle of the destination pixel, so a 1:1 blit reads each source row exactly once.
				int sourceY = Math.Min((int)(((row + 0.5) / paintedHeight) * imageSource.Height), imageSource.Height - 1);
				int sourceRowOffset = imageSource.GetBufferOffsetXY(0, sourceY);

				for (int column = 0; column < paintedWidth; column++)
				{
					int destinationX = originX + column;
					if (destinationX < clipLeft || destinationX >= clipRight)
					{
						continue;
					}

					int sourceX = Math.Min((int)(((column + 0.5) / paintedWidth) * imageSource.Width), imageSource.Width - 1);
					int sourceOffset = sourceRowOffset + (sourceX * sourceBytesPerPixel);

					float sourceAlpha = source[sourceOffset + ImageBuffer.OrderA] / 255.0f;
					if (sourceAlpha <= 0.0f)
					{
						continue;
					}

					// Premultiplied colour bytes are already this pixel's contribution; straight ones are the
					// colour it would have at full opacity, so they take the alpha here.
					float colorScale = sourceIsPremultiplied ? 1.0f : sourceAlpha;
					float sourceRed = (source[sourceOffset + ImageBuffer.OrderR] / 255.0f) * colorScale;
					float sourceGreen = (source[sourceOffset + ImageBuffer.OrderG] / 255.0f) * colorScale;
					float sourceBlue = (source[sourceOffset + ImageBuffer.OrderB] / 255.0f) * colorScale;

					int offset = this.buffer.PixelOffset(destinationX, destinationY);
					float bufferRed = this.buffer.ColorPlane[offset] / 255.0f;
					float bufferGreen = this.buffer.ColorPlane[offset + 1] / 255.0f;
					float bufferBlue = this.buffer.ColorPlane[offset + 2] / 255.0f;
					float bufferAlphaRed = this.buffer.AlphaPlane[offset] / 255.0f;
					float bufferAlphaGreen = this.buffer.AlphaPlane[offset + 1] / 255.0f;
					float bufferAlphaBlue = this.buffer.AlphaPlane[offset + 2] / 255.0f;

					this.buffer.ColorPlane[offset] = ToByte(sourceRed + (bufferRed * (1.0f - sourceAlpha)));
					this.buffer.ColorPlane[offset + 1] = ToByte(sourceGreen + (bufferGreen * (1.0f - sourceAlpha)));
					this.buffer.ColorPlane[offset + 2] = ToByte(sourceBlue + (bufferBlue * (1.0f - sourceAlpha)));
					this.buffer.AlphaPlane[offset] = ToByte(sourceAlpha + (bufferAlphaRed * (1.0f - sourceAlpha)));
					this.buffer.AlphaPlane[offset + 1] = ToByte(sourceAlpha + (bufferAlphaGreen * (1.0f - sourceAlpha)));
					this.buffer.AlphaPlane[offset + 2] = ToByte(sourceAlpha + (bufferAlphaBlue * (1.0f - sourceAlpha)));
				}
			}
		}

		/// <summary>
		/// Not supported, as on <see cref="ImageGraphics2D"/>: there is no float composite anywhere in the LCD
		/// pipeline to route this to.
		/// </summary>
		public override void Render(
			IImageFloat imageSource,
			double x,
			double y,
			double angleRadians,
			double scaleX,
			double scaleY)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Whether <paramref name="imageSource"/>'s colour bytes are already multiplied by their own alpha
		/// (true), are straight alpha (false), or say nothing either way (null, which the blit treats as a
		/// refusal).
		/// </summary>
		/// <remarks>
		/// An <see cref="ImageBuffer"/> carries its alpha convention only in the blender it was built with,
		/// which is why this is a type test rather than a property read: the two blenders the library uses for
		/// 32 bit BGRA data are the two answers, and anything else - a gamma blender, an exact-copy blender, a
		/// caller's own - has no documented convention this composite could rely on.
		/// </remarks>
		private static bool? SourceIsPremultiplied(IImageByte imageSource)
		{
			switch (imageSource.GetRecieveBlender())
			{
				case BlenderPreMultBGRA _:
					return true;

				case BlenderBGRA _:
					return false;

				default:
					return null;
			}
		}

		/// <summary>
		/// Premultiplies one channel of a clear colour, rounding half up - the same quantization
		/// <see cref="LcdBuffer.Clear"/> performs, so a partial clear and a full one write identical bytes.
		/// </summary>
		private static byte Premultiply(byte channel, byte alpha)
		{
			return ToByte((channel / 255.0f) * (alpha / 255.0f));
		}

		/// <summary>
		/// Quantizes a 0..1 channel to a byte, rounding half up then clamping - the reference's
		/// <c>(value * 255.0 + 0.5).clamp(0.0, 255.0) as u8</c>, in the same order.
		/// </summary>
		private static byte ToByte(float value)
		{
			return (byte)Math.Clamp((value * 255.0f) + 0.5f, 0.0f, 255.0f);
		}
	}
}
