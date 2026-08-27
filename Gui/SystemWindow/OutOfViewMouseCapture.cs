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

using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The kind of pointer event <see cref="OutOfViewMouseCapture.ShouldDeliver"/> is being asked about.
	/// </summary>
	/// <remarks>
	/// Each host names these differently - NSEventTypeLeftMouseDragged, WM_MOUSEMOVE, MotionNotify,
	/// pointermove - and the capture rule cares about none of that, only about which of the four a given
	/// event is. Each host maps its own numbering onto this at its event seam.
	/// </remarks>
	public enum PointerEventKind
	{
		/// <summary>Anything that is not a press, a release or a drag: a hover move, a scroll, a pinch.</summary>
		Other,

		/// <summary>A button going down.</summary>
		Down,

		/// <summary>A button coming up.</summary>
		Up,

		/// <summary>A move with a button held.</summary>
		Drag,
	}

	/// <summary>
	/// Keeps a drag that has wandered outside the window's content view alive, so the widget that started it
	/// still gets its moves and, critically, its mouse up.
	/// </summary>
	/// <remarks>
	/// Hosts keep routing dragged and up events to the window that saw the mouse down however far the pointer
	/// has travelled, so the events do arrive; it was the host's bounds test that threw them away. Losing the
	/// up is what left a widget convinced its button was still held after a drag ended past the window edge -
	/// a stuck capture that nothing else would ever clear. WinForms gets this right for free because it
	/// captures the mouse on mouse down and so keeps receiving until the up; this is that same contract
	/// written out by hand for the hosts that do not.
	/// <para/>
	/// A button only becomes ours through a down <em>inside</em> the view, which is what keeps a title bar
	/// drag (whose down agg never saw) from delivering a phantom up. Plain hover moves outside the view are
	/// still dropped: with no button held they really are nobody's business but the window manager's.
	/// </remarks>
	public sealed class OutOfViewMouseCapture
	{
		// Not a bit set: MouseButtons is not [Flags], and more than one button can be held at once.
		private readonly HashSet<MouseButtons> capturedButtons = new HashSet<MouseButtons>();

		/// <summary>
		/// Whether a drag this view owns is in flight, and so the pointer is its business wherever it is.
		/// </summary>
		public bool HasCapturedButtons => this.capturedButtons.Count > 0;

		/// <summary>
		/// Whether a point already converted into the content view's coordinates lies within its bounds.
		/// The edges count as inside, so a click on the last row of pixels still belongs to the view.
		/// </summary>
		public static bool IsInsideBounds(Vector2 inView, RectangleDouble bounds)
			=> inView.X >= bounds.Left
				&& inView.Y >= bounds.Bottom
				&& inView.X <= bounds.Right
				&& inView.Y <= bounds.Top;

		/// <summary>
		/// Whether a "the pointer left" notification means the pointer actually left the content view.
		/// </summary>
		/// <remarks>
		/// The event type on its own does not mean that, which is the trap. On macOS a mouseExited is a
		/// tracking notification, and cursor rects are tracked: every <c>invalidateCursorRectsForView:</c> -
		/// which the host issues on each cursor change, and agg changes the cursor on every
		/// <c>OnMouseEnter</c> - tears the content view's cursor rect down and rebuilds it, and AppKit posts
		/// a mouseExited (immediately followed by a mouseEntered) for the teardown even though the pointer
		/// never moved. Taking those at face value fired the "pointer is nowhere near me" sentinel repeatedly
		/// while the mouse sat still inside the window, which reads to any widget mid-drag as the pointer
		/// having left - MatterCAD's 3D view responds by snapping the dragged part back to where the drag
		/// started.
		/// <para/>
		/// So the geometry is what is believed rather than the event type: a genuine exit reports a location
		/// outside the bounds (measured, including exits over the title bar), an artifact reports one inside.
		/// A drag holding a captured button is exempt as well - it owns the pointer wherever it has gone,
		/// and its mouse up is what ends it.
		/// </remarks>
		public static bool IsRealPointerExit(Vector2 inView, RectangleDouble bounds, bool dragInFlight)
			=> !dragInFlight && !IsInsideBounds(inView, bounds);

		/// <summary>
		/// Decides whether an event should reach agg, and updates the captured-button set.
		/// </summary>
		/// <param name="kind">Which of the four kinds of pointer event this is.</param>
		/// <param name="button">The agg button the event carries, or None for a hover, scroll or pinch.</param>
		/// <param name="insideView">Whether the event's point lies within the content view's bounds.</param>
		public bool ShouldDeliver(PointerEventKind kind, MouseButtons button, bool insideView)
		{
			switch (kind)
			{
				case PointerEventKind.Down:
					if (!insideView)
					{
						return false;
					}

					this.capturedButtons.Add(button);
					return true;

				case PointerEventKind.Up:
					// Removed whether or not it is delivered, so a button can never stay captured.
					bool wasCaptured = this.capturedButtons.Remove(button);
					return insideView || wasCaptured;

				case PointerEventKind.Drag:
					return insideView || this.capturedButtons.Contains(button);

				default:
					return insideView;
			}
		}
	}
}
