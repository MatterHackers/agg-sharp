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

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Everything that takes an <see cref="LcdBuffer"/> back out to a conventional surface: the per-channel
	/// composite onto a 32 bit-per-pixel image, the single-alpha collapse rules, and the row flip for the
	/// texture boundary.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference: <c>lcd_coverage.rs</c>
	/// (<c>to_rgba8_top_down_collapsed</c>, <c>collapse_lcd_pixel</c>, <c>flip_plane</c>),
	/// <c>gfx_ctx\draw_impl.rs</c> (<c>draw_lcd_backbuffer_arc</c>, the CPU screen composite) and
	/// <c>demo-wgpu\src\shaders.rs</c> (<c>LCB_FLATTEN_WGSL</c>, the premultiplied collapse).
	/// </remarks>
	public partial class LcdBuffer
	{
		/// <summary>Rec.709 luminance weight of the red channel.</summary>
		private const float Rec709Red = 0.2126f;

		/// <summary>Rec.709 luminance weight of the green channel.</summary>
		private const float Rec709Green = 0.7152f;

		/// <summary>Rec.709 luminance weight of the blue channel.</summary>
		private const float Rec709Blue = 0.0722f;

		/// <summary>
		/// Collapses one pixel's per-channel (premultiplied color, alpha) triple into a single
		/// <b>straight-alpha</b> color, for the paths that have to hand a conventional single-alpha pixel to
		/// a conventional blend.
		/// </summary>
		/// <remarks>
		/// The collapsed alpha is the <b>Rec.709 luminance-weighted mean</b> of the three channel alphas,
		/// lifted so no channel's unpremultiply can clamp:
		/// <code>
		/// a = max(0.2126 * alphaRed + 0.7152 * alphaGreen + 0.0722 * alphaBlue, max(color))
		/// </code>
		/// <para>
		/// <b>Why the weighted mean and not max.</b> The per-channel composite over a uniform destination
		/// <c>d</c> gives <c>out_c = color_c + d * (1 - alpha_c)</c>; a single-alpha flatten gives
		/// <c>out_c = color_c + d * (1 - a)</c>. The Rec.709-weighted sum of the encoded channels is
		/// preserved exactly when <c>a</c> is the Rec.709-weighted mean of the <c>alpha_c</c>, so perceived
		/// luminance over a neutral background stays correct. <c>max</c> over-weights coverage on the two
		/// channels below the max and so biases <i>every</i> unequal-alpha pixel dark - every glyph edge, and
		/// for a label that paints no opaque background, essentially every text pixel. In the reference that
		/// was the "text inside windows renders ~20% bolder when LCD is on" bug.
		/// </para>
		/// <para>
		/// <b>Why the lift, and why only here.</b> Straight color is <c>color_c / a</c>, so a channel whose
		/// own alpha exceeds the weighted mean (<c>color_c &gt; a</c>, which is what light text on a dark
		/// theme looks like, where <c>color_c ~= alpha_c</c>) would overshoot 1.0 and get clamped - silently
		/// eating that channel's ink on exactly the near-white glyph edges that carry it. Lifting <c>a</c> to
		/// at least <c>max(color)</c> makes the clamp unreachable. The trade: where the lift raises <c>a</c>
		/// above the weighted mean, the residual luminance error is <c>d * (a - weighted)</c>, which scales
		/// with the <i>destination</i> rather than the source - so it vanishes on black, stays small on a
		/// dark theme, and is far cheaper than the clamp loss it replaces. Dark-on-light is untouched:
		/// premultiplied colors are tiny there, so the lift never binds and the collapse stays
		/// luminance-exact.
		/// </para>
		/// <para>
		/// A <b>premultiplied</b> consumer must not apply the lift - see
		/// <see cref="CollapseLcdPixelPremultiplied"/>. Keeping the two rules in one place is deliberate: in
		/// the reference the two CPU collapse sites drifted apart, and the drift is what let the bolder-text
		/// bug survive in one path after being fixed in the other.
		/// </para>
		/// </remarks>
		public static Color CollapseLcdPixel(
			byte colorRed,
			byte colorGreen,
			byte colorBlue,
			byte alphaRed,
			byte alphaGreen,
			byte alphaBlue)
		{
			float weighted = WeightedAlpha(alphaRed, alphaGreen, alphaBlue);
			float lift = Math.Max(colorRed, Math.Max(colorGreen, colorBlue));
			byte collapsed = (byte)Math.Clamp(Math.Max(weighted, lift) + 0.5f, 0.0f, 255.0f);
			if (collapsed == 0)
			{
				// Nothing to encode: the lift guarantees collapsed >= max(color), so a zero alpha here means
				// every premultiplied color byte is zero too. The pixel can only be carrying sub-half-byte
				// coverage of a near-black source, whose contribution rounds away in any case.
				return new Color(0, 0, 0, 0);
			}

			float divisor = collapsed;

			// Divide first, then scale to bytes - the reference's operation order, and not interchangeable
			// with (color * 255) / divisor in float.
			return new Color(
				ToByte(colorRed / divisor),
				ToByte(colorGreen / divisor),
				ToByte(colorBlue / divisor),
				collapsed);
		}

		/// <summary>
		/// Collapses one pixel's per-channel triple into a single <b>premultiplied</b> color: the same
		/// Rec.709 luminance-weighted alpha as <see cref="CollapseLcdPixel"/>, but the color plane passes
		/// through untouched and there is <b>no lift</b>.
		/// </summary>
		/// <remarks>
		/// CPU twin of the reference's <c>lcb_flatten</c> shader (<c>demo-wgpu\src\shaders.rs</c>), used
		/// wherever the consumer stays premultiplied end to end - a premultiplied layer texture or a
		/// premultiplied source-over blit.
		/// <para>
		/// The missing lift is not an inconsistency with <see cref="CollapseLcdPixel"/>, it is the point:
		/// the lift exists only because a straight-alpha consumer has to divide the color by the alpha and
		/// that division can clamp. A premultiplied consumer never divides, so it has no clamp to protect
		/// against - and the lift would cost it a <c>destination * (a - weighted)</c> luminance error it
		/// does not otherwise have.
		/// </para>
		/// </remarks>
		public static Color CollapseLcdPixelPremultiplied(
			byte colorRed,
			byte colorGreen,
			byte colorBlue,
			byte alphaRed,
			byte alphaGreen,
			byte alphaBlue)
		{
			float weighted = WeightedAlpha(alphaRed, alphaGreen, alphaBlue);

			return new Color(
				colorRed,
				colorGreen,
				colorBlue,
				(byte)Math.Clamp(weighted + 0.5f, 0.0f, 255.0f));
		}

		/// <summary>
		/// Flattens both planes into a single 32 bit-per-pixel <b>straight-alpha</b> image, so an LCD buffer
		/// can go through the ordinary blit path (one texture, one alpha, standard source-over).
		/// </summary>
		/// <remarks>
		/// Per pixel this is <see cref="CollapseLcdPixel"/>; see there for the Rec.709 alpha and the lift.
		/// <para>
		/// The conversion is <b>lossy in chroma</b> wherever the three subpixel alphas diverge - necessarily
		/// so, since a flattened pixel has only one alpha to spend. It is the right answer only where LCD
		/// geometry was not valid anyway (a transparent compositing layer, a plain blit); the two-plane
		/// composite paths preserve the full per-channel information.
		/// </para>
		/// <para>
		/// Diverges from the reference's <c>to_rgba8_top_down_collapsed</c> in layout only, in both cases to
		/// match the destination convention rather than the source: the result is B, G, R, A byte order and
		/// Y-up (row 0 = bottom), because that is what <see cref="ImageBuffer"/> is. The reference's flip to
		/// top-row-first belongs at the texture boundary, where <see cref="FlipPlane"/> lives.
		/// </para>
		/// </remarks>
		public ImageBuffer ToImageBufferCollapsed()
		{
			// A straight-alpha blender, because that is what the collapse produces; a premultiplied
			// destination wants CollapseLcdPixelPremultiplied instead.
			var image = new ImageBuffer(this.Width, this.Height, 32, new BlenderBGRA());
			byte[] destination = image.GetBuffer();
			int bytesPerPixel = image.GetBytesBetweenPixelsInclusive();

			for (int y = 0; y < this.Height; y++)
			{
				int rowOffset = image.GetBufferOffsetXY(0, y);
				for (int x = 0; x < this.Width; x++)
				{
					int source = this.PixelOffset(x, y);
					Color collapsed = CollapseLcdPixel(
						this.ColorPlane[source],
						this.ColorPlane[source + 1],
						this.ColorPlane[source + 2],
						this.AlphaPlane[source],
						this.AlphaPlane[source + 1],
						this.AlphaPlane[source + 2]);

					int offset = rowOffset + (x * bytesPerPixel);
					destination[offset + ImageBuffer.OrderR] = collapsed.red;
					destination[offset + ImageBuffer.OrderG] = collapsed.green;
					destination[offset + ImageBuffer.OrderB] = collapsed.blue;
					destination[offset + ImageBuffer.OrderA] = collapsed.alpha;
				}
			}

			return image;
		}

		/// <summary>
		/// Composites this buffer onto a 32 bit-per-pixel <b>premultiplied</b> destination with per-channel
		/// source-over, preserving LCD chroma: each subpixel's alpha drives the source-over of that
		/// subpixel's color independently of the other two.
		/// <code>
		/// dest_c     := color_c + dest_c * (1 - alpha_c)
		/// dest.alpha := max(alpha) + dest.alpha * (1 - max(alpha))
		/// </code>
		/// </summary>
		/// <param name="destination">32 bit-per-pixel destination in agg-sharp's B, G, R, A byte order,
		/// holding <b>premultiplied</b> color - MatterCAD's widget backbuffer convention
		/// (<see cref="BlenderPreMultBGRA"/>). This buffer's color plane is premultiplied, so
		/// <c>source + dest * (1 - sourceAlpha)</c> is only the correct source-over against a premultiplied
		/// destination.</param>
		/// <param name="destX">Destination x of this buffer's left column.</param>
		/// <param name="destY">Destination y of this buffer's bottom row.</param>
		/// <param name="globalAlpha">Fade applied to the whole buffer, 0..1. Scaling the premultiplied color
		/// and the per-channel alpha by the same factor keeps the source premultiplied-consistent, which is
		/// what lets LCD-cached text inside a faded subtree fade with the group.</param>
		/// <param name="clip">Optional clip in the <b>destination's</b> integer pixel coordinates, half-open
		/// on the right and top edges. Lets an over-scan buffer crop its margins to a widget's bounds
		/// instead of painting over its siblings.</param>
		/// <remarks>
		/// <b>The caller owns the transform and the rounding.</b> This takes an already-resolved integer
		/// destination pixel, so a caller working in continuous coordinates has to apply its CTM to the
		/// placement point and <see cref="Math.Round(double)"/> the result before calling - the same
		/// obligation <see cref="FillPath"/> documents, and for the same reason: sub-pixel placement of
		/// finished per-channel planes smears each channel's phase into its neighbors and destroys the
		/// subpixel geometry. The reference does this rounding inside the composite
		/// (<c>gfx_ctx\draw_impl.rs:501</c> - <c>(dst_x * t.sx + dst_y * t.shx + t.tx).round()</c>) because
		/// its draw context owns the CTM; agg-sharp's transform lives on
		/// <see cref="MatterHackers.Agg.Graphics2D"/>, above this layer.
		/// <para>
		/// <b>Destination alpha takes max over the three channel alphas</b>, not the collapse rule: the
		/// question a later source-over blit asks of the destination is "was this pixel drawn on", and the
		/// answer is yes wherever any subpixel was painted. (The Rec.709 collapse answers a different
		/// question - how opaque is this pixel <i>as a single sample</i> - and is what
		/// <see cref="CollapseLcdPixel"/> is for.)
		/// </para>
		/// <para>
		/// Ported from the reference's software <c>draw_lcd_backbuffer_arc</c>
		/// (<c>gfx_ctx\draw_impl.rs</c>), which reads <b>top-row-first</b> cached planes and therefore maps
		/// source row <c>sy</c> to destination row <c>origin + height - 1 - sy</c>. Here the source is a live
		/// Y-up <see cref="LcdBuffer"/> and the destination is a Y-up <see cref="ImageBuffer"/>, so there is
		/// no flip at all; the flip belongs at the texture boundary (<see cref="FlipPlane"/>).
		/// </para>
		/// </remarks>
		public void CompositeOnto(
			ImageBuffer destination,
			int destX,
			int destY,
			double globalAlpha = 1.0,
			RectangleInt? clip = null)
		{
			if (destination == null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			if (destination.BitDepth != 32)
			{
				throw new ArgumentException(
					$"LcdBuffer.CompositeOnto requires a 32 bit-per-pixel destination, was {destination.BitDepth}.",
					nameof(destination));
			}

			if (this.Width == 0 || this.Height == 0)
			{
				return;
			}

			if (!TryResolveClip(
				clip,
				destination.Width,
				destination.Height,
				out int clipLeft,
				out int clipBottom,
				out int clipRight,
				out int clipTop))
			{
				return;
			}

			float fade = (float)Math.Clamp(globalAlpha, 0.0, 1.0);
			byte[] buffer = destination.GetBuffer();
			int bytesPerPixel = destination.GetBytesBetweenPixelsInclusive();

			for (int sourceY = 0; sourceY < this.Height; sourceY++)
			{
				int y = destY + sourceY;
				if (y < clipBottom || y >= clipTop)
				{
					continue;
				}

				// Hoisted per row: the x lookup is linear, so this plus x * bytesPerPixel is exactly
				// GetBufferOffsetXY(x, y), and it keeps a flipped or offset (sub-image) buffer working.
				int destRowOffset = destination.GetBufferOffsetXY(0, y);

				for (int sourceX = 0; sourceX < this.Width; sourceX++)
				{
					int x = destX + sourceX;
					if (x < clipLeft || x >= clipRight)
					{
						continue;
					}

					int source = this.PixelOffset(sourceX, sourceY);
					float sourceAlphaRed = (this.AlphaPlane[source] / 255.0f) * fade;
					float sourceAlphaGreen = (this.AlphaPlane[source + 1] / 255.0f) * fade;
					float sourceAlphaBlue = (this.AlphaPlane[source + 2] / 255.0f) * fade;
					if (sourceAlphaRed == 0.0f && sourceAlphaGreen == 0.0f && sourceAlphaBlue == 0.0f)
					{
						continue;
					}

					float sourceRed = (this.ColorPlane[source] / 255.0f) * fade;
					float sourceGreen = (this.ColorPlane[source + 1] / 255.0f) * fade;
					float sourceBlue = (this.ColorPlane[source + 2] / 255.0f) * fade;

					int offset = destRowOffset + (x * bytesPerPixel);
					float destRed = buffer[offset + ImageBuffer.OrderR] / 255.0f;
					float destGreen = buffer[offset + ImageBuffer.OrderG] / 255.0f;
					float destBlue = buffer[offset + ImageBuffer.OrderB] / 255.0f;
					float destAlpha = buffer[offset + ImageBuffer.OrderA] / 255.0f;

					float sourceAlphaMax = Math.Max(sourceAlphaRed, Math.Max(sourceAlphaGreen, sourceAlphaBlue));

					buffer[offset + ImageBuffer.OrderR] = ToByte(sourceRed + (destRed * (1.0f - sourceAlphaRed)));
					buffer[offset + ImageBuffer.OrderG] = ToByte(sourceGreen + (destGreen * (1.0f - sourceAlphaGreen)));
					buffer[offset + ImageBuffer.OrderB] = ToByte(sourceBlue + (destBlue * (1.0f - sourceAlphaBlue)));
					buffer[offset + ImageBuffer.OrderA] = ToByte(sourceAlphaMax + (destAlpha * (1.0f - sourceAlphaMax)));
				}
			}
		}

		/// <summary>Top-row-first copy of <see cref="ColorPlane"/>; row 0 of the result is the visual top.</summary>
		public byte[] ColorPlaneFlipped()
		{
			return FlipPlane(this.ColorPlane, this.Width, this.Height);
		}

		/// <summary>Top-row-first copy of <see cref="AlphaPlane"/>.</summary>
		public byte[] AlphaPlaneFlipped()
		{
			return FlipPlane(this.AlphaPlane, this.Width, this.Height);
		}

		/// <summary>
		/// Y-flips a 3-byte-per-pixel plane: Y-up (row 0 = bottom) in, top-row-first out. Self-inverse, so
		/// the same call converts back.
		/// </summary>
		/// <remarks>
		/// This is the texture boundary. Both planes live Y-up in memory, matching
		/// <see cref="ImageBuffer"/> and <see cref="LcdMask"/>, but a GL texture upload wants the visual top
		/// row first - so the GPU compositing step (the per-channel color-masked passes that replace
		/// dual-source blending) uploads through here rather than making every producer flip.
		/// </remarks>
		public static byte[] FlipPlane(byte[] plane, int width, int height)
		{
			if (plane == null)
			{
				throw new ArgumentNullException(nameof(plane));
			}

			int rowBytes = width * 3;
			if (plane.Length != rowBytes * height)
			{
				throw new ArgumentException(
					$"Plane must be width * height * 3 bytes ({rowBytes * height}), was {plane.Length}.",
					nameof(plane));
			}

			var flipped = new byte[plane.Length];
			for (int y = 0; y < height; y++)
			{
				Array.Copy(plane, y * rowBytes, flipped, (height - 1 - y) * rowBytes, rowBytes);
			}

			return flipped;
		}

		/// <summary>
		/// Rec.709 luminance-weighted mean of the three channel alphas, in byte scale. Shared by both
		/// collapse rules so they can only ever differ in the lift.
		/// </summary>
		private static float WeightedAlpha(byte alphaRed, byte alphaGreen, byte alphaBlue)
		{
			return (Rec709Red * alphaRed) + (Rec709Green * alphaGreen) + (Rec709Blue * alphaBlue);
		}
	}
}
