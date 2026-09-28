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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Pure window-snapping math, ported from agg-gui's snap/engine.rs. It knows nothing about widgets,
	/// events or painting: a drag handler passes the raw (unsnapped) candidate rectangle and the
	/// rectangles of the other windows, applies <see cref="SnapResult.Bounds"/>, and hands
	/// <see cref="SnapResult.Guides"/> to an overlay. Coordinates are y-up (Bottom is the lower y).
	/// </summary>
	/// <remarks>
	/// Two tiers, per axis:
	/// 1. Edge and centre alignment: left, right and centre x against every target's left, right and centre
	///    x (likewise bottom, top and centre y); the smallest delta within the threshold wins.
	/// 2. Equal spacing (see <see cref="SnapSpacing"/>), only on an axis where no edge engaged - an edge snap
	///    and a spacing snap on the same axis would paint two competing explanations. The axes are
	///    independent: an x edge snap does not stop y spacing.
	/// A resize only snaps the edges its handle controls, and centre snaps only when both edges of that axis
	/// are free. The engine is stateless; any escape/stickiness logic lives with the caller.
	/// </remarks>
	public static class SnapEngine
	{
		/// <summary>
		/// The pixel distance at which alignment and spacing engage when a caller has no reason to pick
		/// another.
		/// </summary>
		public const double DefaultThreshold = 8.0;

		private enum MovingEdge
		{
			Left,
			Right,
			CenterX,
			Top,
			Bottom,
			CenterY,
		}

		private struct Alignment
		{
			public double Delta;
			public double Position;
			public MovingEdge Edge;
			public RectangleDouble Target;
		}

		/// <summary>
		/// Snaps <paramref name="moving"/> against <paramref name="targets"/>.
		/// </summary>
		/// <param name="moving">The rectangle produced by the raw drag.</param>
		/// <param name="movingId">Identity of the moving rectangle; targets with this id are skipped.</param>
		/// <param name="targets">Every other snappable rectangle in the scene (the mover may be included).</param>
		/// <param name="threshold">Maximum distance for a snap to engage; zero or less disables snapping.</param>
		/// <param name="mode">A move, or a resize from a given handle.</param>
		/// <returns>The possibly adjusted rectangle and the guides to draw.</returns>
		public static SnapResult ComputeSnap(RectangleDouble moving, SnapId movingId, IReadOnlyList<(SnapId Id, RectangleDouble Bounds)> targets, double threshold, SnapMode mode)
		{
			if (targets.Count == 0 || threshold <= 0)
			{
				return new SnapResult(moving, new List<SnapGuide>());
			}

			var neighbours = new List<RectangleDouble>(targets.Count);
			foreach (var (id, bounds) in targets)
			{
				if (id != movingId)
				{
					neighbours.Add(bounds);
				}
			}

			if (neighbours.Count == 0)
			{
				return new SnapResult(moving, new List<SnapGuide>());
			}

			var rect = moving;
			var guides = new List<SnapGuide>();

			// Phase 1: edge alignment.
			bool allowLeft = !mode.IsResize || mode.Edge.AffectsLeft();
			bool allowRight = !mode.IsResize || mode.Edge.AffectsRight();
			bool allowTop = !mode.IsResize || mode.Edge.AffectsTop();
			bool allowBottom = !mode.IsResize || mode.Edge.AffectsBottom();
			// Centre snaps only when both edges on the axis are free - sliding the centre would otherwise
			// move an edge the resize handle must not touch.
			bool allowCenterX = allowLeft && allowRight;
			bool allowCenterY = allowTop && allowBottom;

			bool xEdgeEngaged = false;
			bool yEdgeEngaged = false;
			var xSnap = BestXAlignment(rect, neighbours, threshold, allowLeft, allowRight, allowCenterX);
			if (xSnap is Alignment x)
			{
				if (mode.IsResize)
				{
					ApplyResizeX(ref rect, x.Edge, x.Delta, mode.Edge);
				}
				else
				{
					TranslateX(ref rect, x.Delta);
				}

				var (y0, y1) = YSpan(rect, x.Target);
				guides.Add(SnapGuide.VLine(x.Position, y0, y1));
				xEdgeEngaged = true;
			}

			var ySnap = BestYAlignment(rect, neighbours, threshold, allowTop, allowBottom, allowCenterY);
			if (ySnap is Alignment y)
			{
				if (mode.IsResize)
				{
					ApplyResizeY(ref rect, y.Edge, y.Delta, mode.Edge);
				}
				else
				{
					TranslateY(ref rect, y.Delta);
				}

				var (x0, x1) = XSpan(rect, y.Target);
				guides.Add(SnapGuide.HLine(y.Position, x0, x1));
				yEdgeEngaged = true;
			}

			// Phase 2: equal spacing, only on axes where no edge engaged.
			if (!mode.IsResize)
			{
				if (!xEdgeEngaged && SnapSpacing.HorizontalEqualSpacing(rect, neighbours, threshold) is SnapSpacingMatch h)
				{
					TranslateX(ref rect, h.Delta);
					guides.AddRange(h.Guides);
				}

				if (!yEdgeEngaged && SnapSpacing.VerticalEqualSpacing(rect, neighbours, threshold) is SnapSpacingMatch v)
				{
					TranslateY(ref rect, v.Delta);
					guides.AddRange(v.Guides);
				}
			}
			else
			{
				// Match the dragged edge's gap to a reference gap elsewhere; only the handle's edge moves.
				if (!xEdgeEngaged && SnapSpacing.HorizontalResizeSpacing(rect, neighbours, threshold, mode.Edge) is SnapSpacingMatch h)
				{
					ApplyResizeXSpacing(ref rect, mode.Edge, h.Delta);
					guides.AddRange(h.Guides);
				}

				if (!yEdgeEngaged && SnapSpacing.VerticalResizeSpacing(rect, neighbours, threshold, mode.Edge) is SnapSpacingMatch v)
				{
					ApplyResizeYSpacing(ref rect, mode.Edge, v.Delta);
					guides.AddRange(v.Guides);
				}
			}

			return new SnapResult(rect, guides);
		}

		private static Alignment? BestXAlignment(RectangleDouble moving, List<RectangleDouble> targets, double threshold, bool allowLeft, bool allowRight, bool allowCenterX)
		{
			Alignment? best = null;
			double mLeft = moving.Left;
			double mRight = moving.Right;
			double mCenter = moving.Left + moving.Width * 0.5;
			foreach (var t in targets)
			{
				var targetPoints = new[] { t.Left, t.Right, t.Left + t.Width * 0.5 };
				var movingEdges = new[]
				{
					(MovingEdge.Left, mLeft, allowLeft),
					(MovingEdge.Right, mRight, allowRight),
					(MovingEdge.CenterX, mCenter, allowCenterX),
				};
				foreach (var (edge, movingValue, enabled) in movingEdges)
				{
					if (enabled)
					{
						foreach (var targetPoint in targetPoints)
						{
							Consider(ref best, targetPoint - movingValue, targetPoint, edge, t, threshold);
						}
					}
				}
			}

			return best;
		}

		private static Alignment? BestYAlignment(RectangleDouble moving, List<RectangleDouble> targets, double threshold, bool allowTop, bool allowBottom, bool allowCenterY)
		{
			Alignment? best = null;
			double mBottom = moving.Bottom;
			double mTop = moving.Top;
			double mCenter = moving.Bottom + moving.Height * 0.5;
			foreach (var t in targets)
			{
				var targetPoints = new[] { t.Bottom, t.Top, t.Bottom + t.Height * 0.5 };
				var movingEdges = new[]
				{
					(MovingEdge.Bottom, mBottom, allowBottom),
					(MovingEdge.Top, mTop, allowTop),
					(MovingEdge.CenterY, mCenter, allowCenterY),
				};
				foreach (var (edge, movingValue, enabled) in movingEdges)
				{
					if (enabled)
					{
						foreach (var targetPoint in targetPoints)
						{
							Consider(ref best, targetPoint - movingValue, targetPoint, edge, t, threshold);
						}
					}
				}
			}

			return best;
		}

		/// <summary>
		/// Keeps the candidate if it is within the threshold and strictly closer than the best so far, so the
		/// first of equally close candidates wins, as in agg-gui.
		/// </summary>
		private static void Consider(ref Alignment? best, double delta, double position, MovingEdge edge, RectangleDouble target, double threshold)
		{
			if (Math.Abs(delta) <= threshold
				&& (best == null || Math.Abs(delta) < Math.Abs(best.Value.Delta)))
			{
				best = new Alignment { Delta = delta, Position = position, Edge = edge, Target = target };
			}
		}

		// A move keeps the width exactly, as agg-gui's Rect does (it stores x and width): offsetting both edges
		// by the same delta could round the width by an ulp.
		private static void TranslateX(ref RectangleDouble rect, double delta)
		{
			double width = rect.Width;
			rect.Left += delta;
			rect.Right = rect.Left + width;
		}

		private static void TranslateY(ref RectangleDouble rect, double delta)
		{
			double height = rect.Height;
			rect.Bottom += delta;
			rect.Top = rect.Bottom + height;
		}

		private static void ApplyResizeX(ref RectangleDouble rect, MovingEdge edge, double delta, ResizeEdge active)
		{
			if (edge == MovingEdge.Left && active.AffectsLeft())
			{
				rect.Left += delta;
			}
			else if (edge == MovingEdge.Right && active.AffectsRight())
			{
				rect.Right += delta;
			}
		}

		private static void ApplyResizeY(ref RectangleDouble rect, MovingEdge edge, double delta, ResizeEdge active)
		{
			if (edge == MovingEdge.Bottom && active.AffectsBottom())
			{
				rect.Bottom += delta;
			}
			else if (edge == MovingEdge.Top && active.AffectsTop())
			{
				rect.Top += delta;
			}
		}

		// East-side resizes grow to the right; west-side ones move the left edge and keep the right put.
		private static void ApplyResizeXSpacing(ref RectangleDouble rect, ResizeEdge edge, double delta)
		{
			if (edge.AffectsRight())
			{
				rect.Right += delta;
			}
			else if (edge.AffectsLeft())
			{
				rect.Left += delta;
			}
		}

		private static void ApplyResizeYSpacing(ref RectangleDouble rect, ResizeEdge edge, double delta)
		{
			if (edge.AffectsTop())
			{
				rect.Top += delta;
			}
			else if (edge.AffectsBottom())
			{
				rect.Bottom += delta;
			}
		}

		// Guide lines run end to end across the moving rectangle and its target so the user sees what aligned.
		private static (double, double) YSpan(RectangleDouble a, RectangleDouble b)
		{
			return (Math.Min(a.Bottom, b.Bottom), Math.Max(a.Top, b.Top));
		}

		private static (double, double) XSpan(RectangleDouble a, RectangleDouble b)
		{
			return (Math.Min(a.Left, b.Left), Math.Max(a.Right, b.Right));
		}
	}
}
