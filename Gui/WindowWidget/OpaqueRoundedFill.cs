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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Fills a styled window's full-window shapes - its shadow layers and its rounded background - without
	/// paying for the flat middle an opaque background covers (see <see cref="WindowWidget"/>).
	/// </summary>
	internal static class OpaqueRoundedFill
	{
		/// <summary>
		/// The whole device pixels an opaque rounded rect of <paramref name="radius"/> over <paramref name="bounds"/>
		/// covers completely, or null when that is not simply worked out (a rotated or flipped transform) or is empty.
		/// </summary>
		/// <remarks>
		/// This is what keeps a styled window's first paint cheap. The shadow layers and the background are each
		/// the size of the whole window, and on an LCD backbuffer every fill is a 3x raster, a 5-tap filter and a
		/// per-pixel composite over its whole clipped extent - a GUI demo window's first paint cost about eight
		/// window-sized fills and froze the browser page for two seconds. Inside this rect the shadow cannot show
		/// and the background is one flat colour, so it is cleared to that colour, and the fills are clipped to
		/// the bands around it (see <see cref="RenderAround"/>). The rect is pulled in by the radius plus a pixel
		/// so the pixels along its edge - where the LCD filter reads across the clip - are fully covered too,
		/// which keeps the result pixel for pixel what the whole fills drew.
		/// </remarks>
		public static RectangleDouble? SolidInterior(Graphics2D graphics2D, RectangleDouble bounds, double radius)
		{
			var transform = graphics2D.GetTransform();
			if (transform.shx != 0
				|| transform.shy != 0
				|| transform.sx <= 0
				|| transform.sy <= 0)
			{
				return null;
			}

			var covered = bounds;
			covered.Inflate(-(radius + 1));
			var bottomLeft = transform.Transform(new Vector2(covered.Left, covered.Bottom));
			var topRight = transform.Transform(new Vector2(covered.Right, covered.Top));
			var interior = new RectangleDouble(
				Math.Ceiling(Math.Min(bottomLeft.X, topRight.X)),
				Math.Ceiling(Math.Min(bottomLeft.Y, topRight.Y)),
				Math.Floor(Math.Max(bottomLeft.X, topRight.X)),
				Math.Floor(Math.Max(bottomLeft.Y, topRight.Y)));
			return interior.Width > 0 && interior.Height > 0 ? interior : null;
		}

		/// <summary>
		/// Fills the rounded rect <paramref name="bounds"/> everywhere but the device rect <paramref name="skip"/>,
		/// one band around it at a time; with no rect to skip, fills all of it.
		/// </summary>
		/// <remarks>
		/// Each band is filled with only the part of the shape it needs - the rect cut short a little past the
		/// band, keeping just the corners on that side - because clipping the whole shape would still make the
		/// rasterizer sweep every row and column of it. Inside the band the piece's edges are the shape's own
		/// edges, so the coverage there is the same to the byte.
		/// <para>
		/// The bands reach a pixel into <paramref name="skip"/>. The LCD filter reads two subpixels either side,
		/// and past a clip it reads nothing, so a band's last pixel comes out partly covered. Inside the skipped
		/// rect that pixel lands on the flat background colour it would have been anyway; at the rect's edge it
		/// would have left a faint seam.
		/// </para>
		/// </remarks>
		public static void RenderAround(Graphics2D graphics2D, RectangleDouble bounds, double radius, Color color, RectangleDouble? skip)
		{
			var hole = skip ?? default;
			hole.Inflate(-1);
			if (skip == null
				|| hole.Width <= 0
				|| hole.Height <= 0)
			{
				graphics2D.Render(new RoundedRect(bounds, radius), color);
				return;
			}

			// SolidInterior only answers for an unrotated, unflipped transform, so the inverse maps device
			// coordinates straight back along each axis. The cuts sit a pixel past each band, in the skipped rect.
			var toLocal = graphics2D.GetTransform();
			toLocal.invert();
			var cutBelowTop = toLocal.Transform(new Vector2(hole.Right - 1, hole.Top - 1));
			var cutAboveBottom = toLocal.Transform(new Vector2(hole.Left + 1, hole.Bottom + 1));

			var savedClip = graphics2D.GetClippingRect();
			var bands = new (RectangleDouble band, RectangleDouble piece, double[] radii)[]
			{
				(new RectangleDouble(savedClip.Left, hole.Top, savedClip.Right, savedClip.Top),
					new RectangleDouble(bounds.Left, cutBelowTop.Y, bounds.Right, bounds.Top), new[] { 0, 0, radius, radius }),
				(new RectangleDouble(savedClip.Left, savedClip.Bottom, savedClip.Right, hole.Bottom),
					new RectangleDouble(bounds.Left, bounds.Bottom, bounds.Right, cutAboveBottom.Y), new[] { radius, radius, 0, 0 }),
				(new RectangleDouble(savedClip.Left, hole.Bottom, hole.Left, hole.Top),
					new RectangleDouble(bounds.Left, bounds.Bottom, cutAboveBottom.X, bounds.Top), new[] { radius, 0, 0, radius }),
				(new RectangleDouble(hole.Right, hole.Bottom, savedClip.Right, hole.Top),
					new RectangleDouble(cutBelowTop.X, bounds.Bottom, bounds.Right, bounds.Top), new[] { 0, radius, radius, 0 }),
			};

			foreach (var (band, piece, radii) in bands)
			{
				var clip = band;
				if (!clip.IntersectWithRectangle(savedClip))
				{
					continue;
				}

				graphics2D.SetClippingRect(clip);

				// A piece too short for its corners would round them differently; the whole shape is always right.
				var shape = new RoundedRect(bounds, radius);
				if (piece.Width >= radius && piece.Height >= radius)
				{
					shape = new RoundedRect(piece, 0);
					shape.radius(radii[0], radii[1], radii[2], radii[3]);
				}

				graphics2D.Render(shape, color);
			}

			graphics2D.SetClippingRect(savedClip);
		}
	}
}
