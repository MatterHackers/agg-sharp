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
*/

using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The "safe triangle" of one <see cref="PopupMenu"/>: whether the pointer, crossing a row, is on its way into
	/// the sub menu that is open beside the menu rather than choosing that row. See <see cref="PointerIsAimingAt"/>.
	/// </summary>
	internal class SubMenuAimWedge
	{
		/// <summary>
		/// Where the pointer was the last time it crossed onto a row of the menu, in screen space. The apex
		/// of the wedge <see cref="PointerIsAimingAt"/> tests against.
		/// </summary>
		private Vector2? lastRowHoverPosition;

		/// <summary>Makes <paramref name="pointer"/>, where the pointer just crossed onto a row, the wedge's apex.</summary>
		public void Remember(Vector2 pointer)
		{
			lastRowHoverPosition = pointer;
		}

		/// <summary>
		/// Drops the wedge apex, so the next hover on a row of the menu is judged on its own.
		/// </summary>
		public void Forget()
		{
			lastRowHoverPosition = null;
		}

		/// <summary>
		/// True when the pointer is between where it last was and the near edge of <paramref name="subMenu"/> -
		/// on its way into the open sub menu rather than choosing the row it happens to be over.
		/// </summary>
		/// <remarks>
		/// This is the "safe triangle" every desktop menu needs and for the same reason: a sub menu hangs down
		/// from the row that opened it, so every row of it below the first is reached by moving down and to the
		/// right, and that path crosses the rows underneath the opening row. Windows buys the same forgiveness
		/// with a dwell timer before the crossed row takes over; a wedge does it on geometry alone, which means
		/// it is decided by where the pointer is rather than by how fast it got there - no timer to tune, and
		/// nothing that behaves differently on a loaded machine.
		/// <para>
		/// The apex is where the pointer last entered a row of this menu, so the wedge covers the paths that
		/// start on the opening row. A pointer moving along the menu instead (same x, different row) is never
		/// inside it - the wedge has no width at the apex - so hovering a sibling still closes the sub menu.
		/// </para>
		/// </remarks>
		public bool PointerIsAimingAt(Vector2 pointer, PopupMenu subMenu)
		{
			if (lastRowHoverPosition == null
				|| subMenu == null)
			{
				return false;
			}

			var subMenuBounds = subMenu.TransformToScreenSpace(subMenu.LocalBounds);
			if (subMenuBounds.Width <= 0
				|| subMenuBounds.Height <= 0)
			{
				// Queued to be shown but not laid out yet - there is nothing to aim at
				return false;
			}

			var apex = lastRowHoverPosition.Value;

			// The edge the pointer has to cross to get in. A sub menu that had to open to the left (AltMate,
			// near the right of the screen) is entered through its right edge instead.
			double edgeX = subMenuBounds.Left >= apex.X ? subMenuBounds.Left : subMenuBounds.Right;

			double toEdge = edgeX - apex.X;
			double travelled = pointer.X - apex.X;

			if (toEdge * travelled <= 0
				|| Math.Abs(travelled) > Math.Abs(toEdge))
			{
				// Not headed for the edge at all, or already past it - either way the pointer is not in transit
				return false;
			}

			return PointIsInTriangle(
				pointer,
				apex,
				new Vector2(edgeX, subMenuBounds.Bottom),
				new Vector2(edgeX, subMenuBounds.Top));
		}

		/// <summary>
		/// Standard half-plane test: the point is inside when it is on the same side of all three edges.
		/// Points on an edge count as inside, so a pointer skimming the wedge boundary is not rejected.
		/// </summary>
		private static bool PointIsInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
		{
			double Side(Vector2 from, Vector2 to)
			{
				return (to.X - from.X) * (point.Y - from.Y) - (to.Y - from.Y) * (point.X - from.X);
			}

			double ab = Side(a, b);
			double bc = Side(b, c);
			double ca = Side(c, a);

			return (ab >= 0 && bc >= 0 && ca >= 0)
				|| (ab <= 0 && bc <= 0 && ca <= 0);
		}
	}
}
