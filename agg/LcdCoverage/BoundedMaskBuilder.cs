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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// What came of an attempt to build an <b>untrimmed</b> mask - the form that is sized to the geometry
	/// rather than to a destination, and can therefore be refused for being too big.
	/// </summary>
	/// <remarks>
	/// Three answers rather than a bool because the two failures oblige the caller to do opposite things:
	/// <see cref="Empty"/> means the ordinary path would have painted nothing either, so painting nothing is
	/// right; <see cref="TooLarge"/> means the fill is real and still has to happen, the ordinary way.
	/// </remarks>
	public enum UnclippedMaskResult
	{
		/// <summary>A mask was produced, with the origin it composites at.</summary>
		Built,

		/// <summary>Nothing to paint: the path carries no coordinate-bearing vertices.</summary>
		Empty,

		/// <summary>
		/// Refused: the padded bbox is past <see cref="BoundedMaskBuilder.MaxUnclippedMaskExtentInPixels"/>
		/// on an axis. The caller must paint this fill some other way.
		/// </summary>
		TooLarge
	}

	/// <summary>
	/// Builds an <see cref="LcdMask"/> sized to a path's bounding box rather than to the whole render
	/// target, and reports where that mask has to be composited.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference (<c>lcd_coverage\mask.rs</c> <c>build_bounded_mask</c>).
	/// <para>
	/// This is a cost optimization, not a different algorithm: a small fill on a large buffer would
	/// otherwise allocate, rasterize and filter a full-buffer mask (measured at ~40 ms for one strip
	/// background rect in the reference), making per-fill cost O(buffer) instead of O(bbox).
	/// </para>
	/// </remarks>
	public static class BoundedMaskBuilder
	{
		/// <summary>
		/// Padding, in mask pixels, added on every side of the transformed path bbox. The 5-tap filter
		/// reaches +/-2 subpixels horizontally and AGG anti-aliasing can touch one pixel past a
		/// fractional edge, so 2 pixels of guaranteed-zero border means the filter reads the same
		/// neighborhood it would in a full-buffer mask (out-of-range taps read 0, they do not clamp).
		/// </summary>
		private const double Pad = 2.0;

		/// <summary>
		/// Largest padded bbox, in pixels on either axis, that <see cref="BuildUnclipped"/> will rasterize;
		/// anything larger is refused with <see cref="UnclippedMaskResult.TooLarge"/>.
		/// </summary>
		/// <remarks>
		/// An untrimmed mask is sized to the geometry rather than to the destination - that is what makes it
		/// cacheable across positions - so without a bound one absurd fill would allocate a mask far larger
		/// than the window it is drawn in, where the ordinary path only ever touches destination pixels. 4096
		/// is generous next to any real label or icon and still bounds one mask to a few tens of megabytes at
		/// worst; past it the caller falls back to the ordinary path, a quality difference nobody can see at
		/// that size.
		/// <para>
		/// It has to be a property of the <b>geometry</b> and not of where it landed, so that the decision
		/// cannot vary between two draws that would otherwise share a cache entry. It is: the bbox is
		/// measured after the mask transform, which by then carries only the sub-pixel phase of the
		/// placement.
		/// </para>
		/// </remarks>
		public const double MaxUnclippedMaskExtentInPixels = 4096;

		/// <summary>
		/// Rasterizes <paramref name="path"/> into a bbox-sized LCD coverage mask.
		/// </summary>
		/// <param name="bufferWidth">Width of the destination the mask will composite onto, in pixels.</param>
		/// <param name="bufferHeight">Height of the destination, in pixels.</param>
		/// <param name="path">The vertex source to fill.</param>
		/// <param name="transform">Path space to destination pixel space (typically the caller's CTM).</param>
		/// <param name="mask">The coverage mask, sized to the padded and clipped bbox.</param>
		/// <param name="originX">X of the mask's left column in destination pixels.</param>
		/// <param name="originY">Y of the mask's bottom row in destination pixels (Y-up).</param>
		/// <param name="clip">Optional clip rect in destination pixel coordinates. Shrinking the mask
		/// here is what makes a clipped fill cheap; the caller still has to enforce the clip at composite
		/// time, because the filter can spread coverage up to 2 subpixels past a clip edge.</param>
		/// <param name="fillRule">Fill rule for the path.</param>
		/// <param name="primaryWeight">Filter center-tap weight; the default takes the byte-exact integer
		/// filter. Ignored when <paramref name="gray"/> is true, which has no filter to weight.</param>
		/// <param name="gamma">Curve applied after the filter sum; the default applies none. Also ignored
		/// when <paramref name="gray"/> is true.</param>
		/// <param name="gray">True for the chroma-free collapse
		/// (<see cref="LcdMaskBuilder.FinalizeGray"/>) instead of the LCD filter - same raster, same layout,
		/// same composite path, no subpixel chroma. This is the fallback wherever LCD geometry is not valid:
		/// inside a transparent compositing layer, or above
		/// <see cref="LcdRenderSettings.MaxEffectiveScale"/>.</param>
		/// <returns>False when the padded, clipped bbox is empty - fully off-buffer, fully clipped away,
		/// or the path carries no vertices. The caller then paints nothing, which is what compositing an
		/// all-zero mask would have done.</returns>
		/// <remarks>
		/// <b>Why a bbox-sized mask is byte-identical to a full-buffer one:</b> the bbox is padded by 2
		/// pixels on every side (see <see cref="Pad"/>), and <paramref name="originX"/> /
		/// <paramref name="originY"/> are <b>whole pixels</b>, so shifting the path into mask-local space
		/// moves it by a multiple of 3 subpixels. The filter kernel is translation-invariant at that
		/// granularity, so the coverage bytes come out the same no matter where the path sat. At buffer
		/// and clip edges both forms read zero beyond the boundary.
		/// </remarks>
		public static bool TryBuild(
			int bufferWidth,
			int bufferHeight,
			IVertexSource path,
			Affine transform,
			out LcdMask mask,
			out int originX,
			out int originY,
			RectangleDouble? clip = null,
			filling_rule_e fillRule = filling_rule_e.fill_non_zero,
			double primaryWeight = LcdFilter.DefaultPrimaryWeight,
			double gamma = LcdFilter.DefaultGamma,
			bool gray = false)
		{
			return BuildCore(
				path,
				transform,
				trimToDestination: true,
				bufferWidth,
				bufferHeight,
				clip,
				out mask,
				out originX,
				out originY,
				fillRule,
				primaryWeight,
				gamma,
				gray) == UnclippedMaskResult.Built;
		}

		/// <summary>
		/// <see cref="TryBuild"/> without a destination: the mask covers the path's whole padded bbox
		/// wherever that lands, so <paramref name="originX"/> and <paramref name="originY"/> may be negative
		/// and the mask may extend past the destination the caller will composite it onto.
		/// </summary>
		/// <remarks>
		/// This is the form the <b>cached</b> callers need, and it is what the reference's text path uses: its
		/// mask is sized from the run's own metrics with no knowledge of the destination
		/// (<c>rasterize_text_mask_cached</c>, <c>mask.rs:119-246</c>), and clipping happens at composite time
		/// instead (<c>composite_mask</c>'s clip argument, and the framebuffer bounds test in
		/// <c>draw_lcd_mask</c>).
		/// <para>
		/// <b>Trimming and caching are mutually exclusive.</b> A trimmed mask's bytes depend on where the path
		/// sat relative to the destination and the clip, so the same run drawn half off the left edge and then
		/// scrolled into view produces different bytes - which is exactly what a cache keyed on the run's
		/// identity must not have. Untrimmed, the bytes depend only on the geometry and its sub-pixel phase,
		/// so one entry serves every position of that run and the caller composites it wherever it belongs.
		/// </para>
		/// <para>
		/// The cost of not trimming is that the mask is sized to the geometry rather than to the window it is
		/// seen through, so this form bounds how big that geometry may be
		/// (<see cref="MaxUnclippedMaskExtentInPixels"/>) and refuses the rest.
		/// </para>
		/// </remarks>
		public static UnclippedMaskResult BuildUnclipped(
			IVertexSource path,
			Affine transform,
			out LcdMask mask,
			out int originX,
			out int originY,
			filling_rule_e fillRule = filling_rule_e.fill_non_zero,
			double primaryWeight = LcdFilter.DefaultPrimaryWeight,
			double gamma = LcdFilter.DefaultGamma,
			bool gray = false)
		{
			return BuildCore(
				path,
				transform,
				trimToDestination: false,
				0,
				0,
				null,
				out mask,
				out originX,
				out originY,
				fillRule,
				primaryWeight,
				gamma,
				gray);
		}

		/// <summary>
		/// The body of <see cref="TryBuild"/> and <see cref="BuildUnclipped"/>; the only difference between
		/// them is whether the padded bbox is trimmed to the destination and clip
		/// (<paramref name="trimToDestination"/>), which the untrimmed form must not do - see the remarks on
		/// <see cref="BuildUnclipped"/>.
		/// </summary>
		private static UnclippedMaskResult BuildCore(
			IVertexSource path,
			Affine transform,
			bool trimToDestination,
			int bufferWidth,
			int bufferHeight,
			RectangleDouble? clip,
			out LcdMask mask,
			out int originX,
			out int originY,
			filling_rule_e fillRule,
			double primaryWeight,
			double gamma,
			bool gray)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			mask = null;
			originX = 0;
			originY = 0;

			if (trimToDestination
				&& (bufferWidth <= 0 || bufferHeight <= 0))
			{
				return UnclippedMaskResult.Empty;
			}

			if (!TryGetTransformedBounds(path, transform, out RectangleDouble bounds))
			{
				return UnclippedMaskResult.Empty;
			}

			// Every double to int conversion here saturates (see SaturatingMath), so a transformed edge past
			// ~2.1e9 - or a double.MaxValue "unbounded clip" sentinel below - widens the region up to the
			// buffer edge instead of inverting the box and dropping the fill entirely.
			int x1 = SaturatingMath.Floor(bounds.Left - Pad);
			int y1 = SaturatingMath.Floor(bounds.Bottom - Pad);
			int x2 = SaturatingMath.Ceiling(bounds.Right + Pad);
			int y2 = SaturatingMath.Ceiling(bounds.Top + Pad);

			if (trimToDestination)
			{
				if (clip != null)
				{
					// Floor on left/bottom and ceil on right/top so any pixel the clip rect touches at all is
					// kept, matching the AGG raster-clip convention.
					RectangleDouble clipRect = clip.Value;
					x1 = Math.Max(x1, SaturatingMath.Floor(clipRect.Left));
					y1 = Math.Max(y1, SaturatingMath.Floor(clipRect.Bottom));
					x2 = Math.Min(x2, SaturatingMath.Ceiling(clipRect.Right));
					y2 = Math.Min(y2, SaturatingMath.Ceiling(clipRect.Top));
				}

				x1 = Math.Max(x1, 0);
				y1 = Math.Max(y1, 0);
				x2 = Math.Min(x2, bufferWidth);
				y2 = Math.Min(y2, bufferHeight);
			}
			else if ((double)x2 - x1 > MaxUnclippedMaskExtentInPixels
				|| (double)y2 - y1 > MaxUnclippedMaskExtentInPixels)
			{
				// Widened to double first: with nothing trimming these, a saturated edge past int range
				// would otherwise wrap the subtraction and read as a comfortably small mask.
				return UnclippedMaskResult.TooLarge;
			}

			if (x1 >= x2 || y1 >= y2)
			{
				return UnclippedMaskResult.Empty;
			}

			originX = x1;
			originY = y1;

			// Apply the caller's transform first, then shift into mask-local space. agg-sharp's operator *
			// is a post-multiply ("a then b"), so this is the caller's CTM followed by the translation.
			Affine local = transform * Affine.NewTranslation(-originX, -originY);

			// The builder's clip is in mask-local pixel coordinates, so it moves with the path.
			RectangleDouble? localClip = null;
			if (clip != null)
			{
				RectangleDouble shifted = clip.Value;
				shifted.Offset(-originX, -originY);
				localClip = shifted;
			}

			var builder = new LcdMaskBuilder(x2 - x1, y2 - y1, localClip, fillRule);
			builder.AddPath(local, path);
			mask = gray ? builder.FinalizeGray() : builder.FinalizeMask(primaryWeight, gamma);
			return UnclippedMaskResult.Built;
		}

		/// <summary>
		/// Destination-space bounding box of <paramref name="path"/> under <paramref name="transform"/>;
		/// false when the path has no coordinate-bearing vertices.
		/// </summary>
		/// <remarks>
		/// Only real vertices contribute - close and end-poly markers carry stale coordinates. Curves are
		/// deliberately <b>not</b> flattened here: a Bezier lies inside the convex hull of its control
		/// points, so the control-point bbox is a conservative superset of the flattened curve the
		/// rasterizer will draw. That keeps this O(vertices) and still never clips painted pixels.
		/// </remarks>
		private static bool TryGetTransformedBounds(IVertexSource path, Affine transform, out RectangleDouble bounds)
		{
			bounds = default;
			bool any = false;
			foreach (VertexData vertex in path.Vertices())
			{
				if (!vertex.IsVertex)
				{
					continue;
				}

				Vector2 position = transform.Transform(vertex.Position);
				if (any)
				{
					bounds.ExpandToInclude(position);
				}
				else
				{
					bounds = new RectangleDouble(position.X, position.Y, position.X, position.Y);
					any = true;
				}
			}

			return any;
		}
	}
}
