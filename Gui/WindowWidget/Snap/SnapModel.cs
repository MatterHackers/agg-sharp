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

// Data model for SnapEngine, ported from agg-gui's snap/model.rs. Free of widget and event types so any
// drag handler (window manager, node graph, diagram editor) can use it.

using System.Collections.Generic;
using System.Threading;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Opaque identity of a snappable rectangle. <see cref="SnapEngine.ComputeSnap"/> uses it to skip the
	/// moving rectangle when the caller passes the whole scene (dragger included) as the target list.
	/// Any scheme that is unique per logical entity works; <see cref="Next"/> mints process-unique ids.
	/// </summary>
	public readonly record struct SnapId(ulong Value)
	{
		private static long counter;

		/// <summary>
		/// Mints a fresh id from a process-wide counter, starting at 1. Cheap enough to call from a
		/// constructor.
		/// </summary>
		public static SnapId Next()
		{
			return new SnapId((ulong)Interlocked.Increment(ref counter));
		}
	}

	/// <summary>
	/// The edge or corner driving a resize, as the eight compass handles of a window. North is the top
	/// (the higher y: agg-sharp, like agg-gui, is y-up).
	/// </summary>
	public enum ResizeEdge
	{
		North,
		NorthEast,
		East,
		SouthEast,
		South,
		SouthWest,
		West,
		NorthWest,
	}

	/// <summary>
	/// Which sides of a rectangle a <see cref="ResizeEdge"/> handle moves.
	/// </summary>
	public static class ResizeEdgeExtensions
	{
		/// <summary>True if the handle moves the left edge.</summary>
		public static bool AffectsLeft(this ResizeEdge edge)
		{
			return edge == ResizeEdge.West || edge == ResizeEdge.NorthWest || edge == ResizeEdge.SouthWest;
		}

		/// <summary>True if the handle moves the right edge.</summary>
		public static bool AffectsRight(this ResizeEdge edge)
		{
			return edge == ResizeEdge.East || edge == ResizeEdge.NorthEast || edge == ResizeEdge.SouthEast;
		}

		/// <summary>True if the handle moves the top edge (the higher y).</summary>
		public static bool AffectsTop(this ResizeEdge edge)
		{
			return edge == ResizeEdge.North || edge == ResizeEdge.NorthEast || edge == ResizeEdge.NorthWest;
		}

		/// <summary>True if the handle moves the bottom edge (the lower y).</summary>
		public static bool AffectsBottom(this ResizeEdge edge)
		{
			return edge == ResizeEdge.South || edge == ResizeEdge.SouthEast || edge == ResizeEdge.SouthWest;
		}
	}

	/// <summary>
	/// The drag that produced the candidate rectangle. A move may snap every edge and runs equal-spacing
	/// for the whole rectangle; a resize may only snap the edges its <see cref="Edge"/> handle controls,
	/// so dragging the right edge can never snap the left side.
	/// </summary>
	public readonly record struct SnapMode
	{
		private SnapMode(bool isResize, ResizeEdge edge)
		{
			IsResize = isResize;
			Edge = edge;
		}

		/// <summary>The whole rectangle is being translated.</summary>
		public static SnapMode Move => new SnapMode(false, default);

		/// <summary>True for a resize, false for a move.</summary>
		public bool IsResize { get; }

		/// <summary>The handle driving a resize; meaningless for a move.</summary>
		public ResizeEdge Edge { get; }

		/// <summary>The rectangle is being resized from <paramref name="edge"/>.</summary>
		public static SnapMode Resize(ResizeEdge edge) => new SnapMode(true, edge);
	}

	/// <summary>
	/// The kind of a <see cref="SnapGuide"/>.
	/// </summary>
	public enum SnapGuideKind
	{
		/// <summary>Vertical alignment line from an edge or centre snap on the x axis.</summary>
		VLine,

		/// <summary>Horizontal alignment line from an edge or centre snap on the y axis.</summary>
		HLine,

		/// <summary>Horizontal dimension line marking a gap matched by equal-spacing on the x axis.</summary>
		HSpacing,

		/// <summary>Vertical dimension line marking a gap matched by equal-spacing on the y axis.</summary>
		VSpacing,
	}

	/// <summary>
	/// One line for the drag overlay to draw, in the same (y-up) space as the rectangles given to
	/// <see cref="SnapEngine.ComputeSnap"/>. A <see cref="SnapGuideKind.VLine"/> or
	/// <see cref="SnapGuideKind.VSpacing"/> runs vertically at x = <see cref="At"/> from y = <see cref="From"/>
	/// to <see cref="To"/>; an <see cref="SnapGuideKind.HLine"/> or <see cref="SnapGuideKind.HSpacing"/> runs
	/// horizontally at y = <see cref="At"/> from x = <see cref="From"/> to <see cref="To"/>.
	/// </summary>
	public readonly record struct SnapGuide(SnapGuideKind Kind, double At, double From, double To)
	{
		/// <summary>Vertical alignment line at x from y0 to y1, spanning the moving rectangle and its target.</summary>
		public static SnapGuide VLine(double x, double y0, double y1) => new SnapGuide(SnapGuideKind.VLine, x, y0, y1);

		/// <summary>Horizontal alignment line at y from x0 to x1, spanning the moving rectangle and its target.</summary>
		public static SnapGuide HLine(double y, double x0, double x1) => new SnapGuide(SnapGuideKind.HLine, y, x0, x1);

		/// <summary>Horizontal gap marker at y from x0 to x1.</summary>
		public static SnapGuide HSpacing(double y, double x0, double x1) => new SnapGuide(SnapGuideKind.HSpacing, y, x0, x1);

		/// <summary>Vertical gap marker at x from y0 to y1.</summary>
		public static SnapGuide VSpacing(double x, double y0, double y1) => new SnapGuide(SnapGuideKind.VSpacing, x, y0, y1);
	}

	/// <summary>
	/// What <see cref="SnapEngine.ComputeSnap"/> returns: the rectangle with any snap applied (the input
	/// unchanged when nothing engaged) and the guides the drag overlay should draw (empty when nothing
	/// engaged).
	/// </summary>
	public sealed class SnapResult
	{
		public SnapResult(RectangleDouble bounds, IReadOnlyList<SnapGuide> guides)
		{
			Bounds = bounds;
			Guides = guides;
		}

		/// <summary>The snapped rectangle, ready to apply to the dragged window.</summary>
		public RectangleDouble Bounds { get; }

		/// <summary>Alignment lines and spacing markers for the overlay.</summary>
		public IReadOnlyList<SnapGuide> Guides { get; }
	}

	/// <summary>
	/// An equal-spacing match from <see cref="SnapSpacing"/>: the offset to apply along the axis and the
	/// dimension lines that explain it (both flanking gaps of a sandwich, or the reference gap and the
	/// matched gap of a chain).
	/// </summary>
	internal sealed class SnapSpacingMatch
	{
		public SnapSpacingMatch(double delta, IReadOnlyList<SnapGuide> guides)
		{
			Delta = delta;
			Guides = guides;
		}

		/// <summary>Offset along the axis that puts the moving rectangle at the matched spacing.</summary>
		public double Delta { get; }

		/// <summary>The dimension lines for the overlay.</summary>
		public IReadOnlyList<SnapGuide> Guides { get; }
	}
}
