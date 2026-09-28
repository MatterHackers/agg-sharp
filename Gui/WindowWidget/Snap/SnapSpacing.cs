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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Equal-spacing detection for <see cref="SnapEngine"/>, ported from agg-gui's snap/engine/spacing.rs.
	/// Coordinates are y-up. Internal (visible to Agg.Tests) so the spacing tier can be tested without edge alignment in the way.
	/// </summary>
	/// <remarks>
	/// A move matches one of two patterns per axis. Sandwich: the moving rectangle sits between a neighbour
	/// on each side and fits between them, so it snaps to the centre and gets one guide per flanking gap.
	/// Chain: some pair P, Q in the scene (P directly left of / below Q) has a positive reference gap, and the
	/// moving rectangle snaps so its gap to its own neighbour equals it; the guides are the reference gap and
	/// the matched gap. Reference pairs are tried nearest first (by centre distance to the moving rectangle)
	/// so the guide the user sees is close to where they are looking. A chain match whose matched gap is the
	/// reference gap itself is refused, since it would paint two dimension lines on the same span.
	/// Neighbours only count when they overlap the moving rectangle on the other axis.
	/// </remarks>
	internal static class SnapSpacing
	{
		/// <summary>
		/// Finds a sandwich or chain match on the x axis for a moving rectangle, or null.
		/// </summary>
		internal static SnapSpacingMatch HorizontalEqualSpacing(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets, double threshold)
		{
			var (leftNeighbour, rightNeighbour) = HorizontalNeighbours(moving, targets);

			if (leftNeighbour is RectangleDouble l && rightNeighbour is RectangleDouble r)
			{
				double total = r.Left - l.Right;
				if (total > moving.Width)
				{
					double symmetricLeft = l.Right + (total - moving.Width) * 0.5;
					double symmetricRight = symmetricLeft + moving.Width;
					double delta = symmetricLeft - moving.Left;
					if (Math.Abs(delta) <= threshold)
					{
						double y = moving.Bottom + moving.Height * 0.5;
						return new SnapSpacingMatch(delta, new List<SnapGuide>
						{
							SnapGuide.HSpacing(y, l.Right, symmetricLeft),
							SnapGuide.HSpacing(y, symmetricRight, r.Left),
						});
					}
				}
			}

			foreach (int qi in TargetsSortedByDistance(moving, targets))
			{
				var q = targets[qi];
				if (!(HorizontalLeftNeighbourOf(q, targets) is RectangleDouble p))
				{
					continue;
				}

				double refGap = q.Left - p.Right;
				if (refGap <= 0)
				{
					continue;
				}

				var refGuide = SnapGuide.HSpacing(q.Bottom + q.Height * 0.5, p.Right, q.Left);
				SnapSpacingMatch bestForQ = null;
				double refX0 = p.Right;
				double refX1 = q.Left;
				double movingY = moving.Bottom + moving.Height * 0.5;
				if (leftNeighbour is RectangleDouble a)
				{
					double wantLeft = a.Right + refGap;
					double delta = wantLeft - moving.Left;
					double matchedX0 = a.Right;
					double matchedX1 = wantLeft;
					if (Math.Abs(delta) <= threshold && !RangeEq(refX0, refX1, matchedX0, matchedX1))
					{
						bestForQ = new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.HSpacing(movingY, matchedX0, matchedX1) });
					}
				}

				if (rightNeighbour is RectangleDouble b)
				{
					double wantRight = b.Left - refGap;
					double wantLeft = wantRight - moving.Width;
					double delta = wantLeft - moving.Left;
					double matchedX0 = wantRight;
					double matchedX1 = b.Left;
					if (Math.Abs(delta) <= threshold
						&& !RangeEq(refX0, refX1, matchedX0, matchedX1)
						&& (bestForQ == null || Math.Abs(delta) < Math.Abs(bestForQ.Delta)))
					{
						bestForQ = new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.HSpacing(movingY, matchedX0, matchedX1) });
					}
				}

				if (bestForQ != null)
				{
					return bestForQ;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds a sandwich or chain match on the y axis for a moving rectangle, or null. Mirrors
		/// <see cref="HorizontalEqualSpacing"/>, except a chain match is only refused as degenerate when the
		/// reference pair is in the same column as the moving rectangle.
		/// </summary>
		internal static SnapSpacingMatch VerticalEqualSpacing(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets, double threshold)
		{
			var (bottomNeighbour, topNeighbour) = VerticalNeighbours(moving, targets);

			if (bottomNeighbour is RectangleDouble bn && topNeighbour is RectangleDouble tn)
			{
				double total = tn.Bottom - bn.Top;
				if (total > moving.Height)
				{
					double symmetricBottom = bn.Top + (total - moving.Height) * 0.5;
					double symmetricTop = symmetricBottom + moving.Height;
					double delta = symmetricBottom - moving.Bottom;
					if (Math.Abs(delta) <= threshold)
					{
						double x = moving.Left + moving.Width * 0.5;
						return new SnapSpacingMatch(delta, new List<SnapGuide>
						{
							SnapGuide.VSpacing(x, bn.Top, symmetricBottom),
							SnapGuide.VSpacing(x, symmetricTop, tn.Bottom),
						});
					}
				}
			}

			foreach (int qi in TargetsSortedByDistance(moving, targets))
			{
				var q = targets[qi];
				if (!(VerticalBottomNeighbourOf(q, targets) is RectangleDouble p))
				{
					continue;
				}

				double refGap = q.Bottom - p.Top;
				if (refGap <= 0)
				{
					continue;
				}

				var refGuide = SnapGuide.VSpacing(q.Left + q.Width * 0.5, p.Top, q.Bottom);
				SnapSpacingMatch bestForQ = null;
				double refY0 = p.Top;
				double refY1 = q.Bottom;
				double refX = q.Left + q.Width * 0.5;
				double movingX = moving.Left + moving.Width * 0.5;
				bool sameColumn = Math.Abs(refX - movingX) < 1e-6;
				if (bottomNeighbour is RectangleDouble a)
				{
					double wantBottom = a.Top + refGap;
					double delta = wantBottom - moving.Bottom;
					double matchedY0 = a.Top;
					double matchedY1 = wantBottom;
					bool degenerate = sameColumn && RangeEq(refY0, refY1, matchedY0, matchedY1);
					if (Math.Abs(delta) <= threshold && !degenerate)
					{
						bestForQ = new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.VSpacing(movingX, matchedY0, matchedY1) });
					}
				}

				if (topNeighbour is RectangleDouble b)
				{
					double wantTop = b.Bottom - refGap;
					double wantBottom = wantTop - moving.Height;
					double delta = wantBottom - moving.Bottom;
					double matchedY0 = wantTop;
					double matchedY1 = b.Bottom;
					bool degenerate = sameColumn && RangeEq(refY0, refY1, matchedY0, matchedY1);
					if (Math.Abs(delta) <= threshold
						&& !degenerate
						&& (bestForQ == null || Math.Abs(delta) < Math.Abs(bestForQ.Delta)))
					{
						bestForQ = new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.VSpacing(movingX, matchedY0, matchedY1) });
					}
				}

				if (bestForQ != null)
				{
					return bestForQ;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds a chain match on the x axis for the edge a resize handle drags: the gap from the dragged edge
		/// to the neighbour on that side is matched to a reference gap. Null if none, or if the match would
		/// turn the rectangle inside out.
		/// </summary>
		internal static SnapSpacingMatch HorizontalResizeSpacing(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets, double threshold, ResizeEdge edge)
		{
			var (leftNeighbour, rightNeighbour) = HorizontalNeighbours(moving, targets);
			double movingY = moving.Bottom + moving.Height * 0.5;
			double mLeft = moving.Left;
			double mRight = moving.Right;
			foreach (int qi in TargetsSortedByDistance(moving, targets))
			{
				var q = targets[qi];
				if (!(HorizontalLeftNeighbourOf(q, targets) is RectangleDouble p))
				{
					continue;
				}

				double refGap = q.Left - p.Right;
				if (refGap <= 0)
				{
					continue;
				}

				var refGuide = SnapGuide.HSpacing(q.Bottom + q.Height * 0.5, p.Right, q.Left);
				if (edge.AffectsRight() && rightNeighbour is RectangleDouble r)
				{
					double wantRight = r.Left - refGap;
					double delta = wantRight - mRight;
					if (Math.Abs(delta) <= threshold && wantRight > mLeft
						&& !RangeEq(p.Right, q.Left, wantRight, r.Left))
					{
						return new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.HSpacing(movingY, wantRight, r.Left) });
					}
				}

				if (edge.AffectsLeft() && leftNeighbour is RectangleDouble l)
				{
					double wantLeft = l.Right + refGap;
					double delta = wantLeft - mLeft;
					if (Math.Abs(delta) <= threshold && wantLeft < mRight
						&& !RangeEq(p.Right, q.Left, l.Right, wantLeft))
					{
						return new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.HSpacing(movingY, l.Right, wantLeft) });
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Mirror of <see cref="HorizontalResizeSpacing"/> on the y axis, with the same-column degeneracy rule
		/// of <see cref="VerticalEqualSpacing"/>.
		/// </summary>
		internal static SnapSpacingMatch VerticalResizeSpacing(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets, double threshold, ResizeEdge edge)
		{
			var (bottomNeighbour, topNeighbour) = VerticalNeighbours(moving, targets);
			double movingX = moving.Left + moving.Width * 0.5;
			double mBottom = moving.Bottom;
			double mTop = moving.Top;
			foreach (int qi in TargetsSortedByDistance(moving, targets))
			{
				var q = targets[qi];
				if (!(VerticalBottomNeighbourOf(q, targets) is RectangleDouble p))
				{
					continue;
				}

				double refGap = q.Bottom - p.Top;
				if (refGap <= 0)
				{
					continue;
				}

				var refGuide = SnapGuide.VSpacing(q.Left + q.Width * 0.5, p.Top, q.Bottom);
				bool sameColumn = Math.Abs(q.Left + q.Width * 0.5 - movingX) < 1e-6;
				if (edge.AffectsTop() && topNeighbour is RectangleDouble t)
				{
					double wantTop = t.Bottom - refGap;
					double delta = wantTop - mTop;
					if (Math.Abs(delta) <= threshold && wantTop > mBottom
						&& !(sameColumn && RangeEq(p.Top, q.Bottom, wantTop, t.Bottom)))
					{
						return new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.VSpacing(movingX, wantTop, t.Bottom) });
					}
				}

				if (edge.AffectsBottom() && bottomNeighbour is RectangleDouble b)
				{
					double wantBottom = b.Top + refGap;
					double delta = wantBottom - mBottom;
					if (Math.Abs(delta) <= threshold && wantBottom < mTop
						&& !(sameColumn && RangeEq(p.Top, q.Bottom, b.Top, wantBottom)))
					{
						return new SnapSpacingMatch(delta, new List<SnapGuide> { refGuide, SnapGuide.VSpacing(movingX, b.Top, wantBottom) });
					}
				}
			}

			return null;
		}

		// The nearest target wholly left and wholly right of the moving rectangle among those overlapping it
		// vertically.
		private static (RectangleDouble?, RectangleDouble?) HorizontalNeighbours(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets)
		{
			RectangleDouble? left = null;
			RectangleDouble? right = null;
			foreach (var t in targets)
			{
				if (!(t.Top > moving.Bottom && t.Bottom < moving.Top))
				{
					continue;
				}

				if (t.Right <= moving.Left)
				{
					if (left == null || left.Value.Right < t.Right)
					{
						left = t;
					}
				}
				else if (t.Left >= moving.Right)
				{
					if (right == null || right.Value.Left > t.Left)
					{
						right = t;
					}
				}
			}

			return (left, right);
		}

		// The nearest target wholly left of `of` (other than `of` itself) among those overlapping it vertically.
		private static RectangleDouble? HorizontalLeftNeighbourOf(RectangleDouble of, IReadOnlyList<RectangleDouble> targets)
		{
			RectangleDouble? best = null;
			foreach (var t in targets)
			{
				if (RectEq(t, of) || !(t.Top > of.Bottom && t.Bottom < of.Top))
				{
					continue;
				}

				if (t.Right <= of.Left && (best == null || best.Value.Right < t.Right))
				{
					best = t;
				}
			}

			return best;
		}

		private static (RectangleDouble?, RectangleDouble?) VerticalNeighbours(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets)
		{
			RectangleDouble? bottom = null;
			RectangleDouble? top = null;
			foreach (var t in targets)
			{
				if (!(t.Right > moving.Left && t.Left < moving.Right))
				{
					continue;
				}

				if (t.Top <= moving.Bottom)
				{
					if (bottom == null || bottom.Value.Top < t.Top)
					{
						bottom = t;
					}
				}
				else if (t.Bottom >= moving.Top)
				{
					if (top == null || top.Value.Bottom > t.Bottom)
					{
						top = t;
					}
				}
			}

			return (bottom, top);
		}

		private static RectangleDouble? VerticalBottomNeighbourOf(RectangleDouble of, IReadOnlyList<RectangleDouble> targets)
		{
			RectangleDouble? best = null;
			foreach (var t in targets)
			{
				if (RectEq(t, of) || !(t.Right > of.Left && t.Left < of.Right))
				{
					continue;
				}

				if (t.Top <= of.Bottom && (best == null || best.Value.Top < t.Top))
				{
					best = t;
				}
			}

			return best;
		}

		// Stable, like Rust's sort_by: equally distant targets keep scene order.
		private static IEnumerable<int> TargetsSortedByDistance(RectangleDouble moving, IReadOnlyList<RectangleDouble> targets)
		{
			double cx = moving.Left + moving.Width * 0.5;
			double cy = moving.Bottom + moving.Height * 0.5;
			return Enumerable.Range(0, targets.Count)
				.OrderBy(i => SquaredCenterDistance(targets[i], cx, cy))
				.ToList();
		}

		private static double SquaredCenterDistance(RectangleDouble r, double cx, double cy)
		{
			double dx = r.Left + r.Width * 0.5 - cx;
			double dy = r.Bottom + r.Height * 0.5 - cy;
			return dx * dx + dy * dy;
		}

		private static bool RangeEq(double refStart, double refEnd, double matchedStart, double matchedEnd)
		{
			return Math.Abs(refStart - matchedStart) < 1e-6 && Math.Abs(refEnd - matchedEnd) < 1e-6;
		}

		private static bool RectEq(RectangleDouble a, RectangleDouble b)
		{
			return Math.Abs(a.Left - b.Left) < 1e-9
				&& Math.Abs(a.Bottom - b.Bottom) < 1e-9
				&& Math.Abs(a.Width - b.Width) < 1e-9
				&& Math.Abs(a.Height - b.Height) < 1e-9;
		}
	}
}
