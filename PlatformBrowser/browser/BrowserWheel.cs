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

using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Turns a DOM <c>wheel</c> event into agg's wheel units, over
	/// <see cref="WheelDeltaMath"/>.
	/// </summary>
	/// <remarks>
	/// The browser meets the same two kinds of scroll every desktop host does - a precise device reporting
	/// travel, and a detent device reporting lines - and says which is which with <c>deltaMode</c> rather
	/// than with a flag. Everything past that identification is the shared arithmetic; this class exists to
	/// do the identification, the sign, and the pinch.
	/// <para/>
	/// Pure - no JS interop, no state - so it runs in the desktop test suite.
	/// </remarks>
	public static class BrowserWheel
	{
		/// <summary><c>WheelEvent.DOM_DELTA_PIXEL</c>: the deltas are CSS pixels of travel.</summary>
		public const int DeltaModePixel = 0;

		/// <summary><c>WheelEvent.DOM_DELTA_LINE</c>: the deltas are lines, which is a detent device.</summary>
		public const int DeltaModeLine = 1;

		/// <summary><c>WheelEvent.DOM_DELTA_PAGE</c>: the deltas are pages. Rare, and a detent device too.</summary>
		public const int DeltaModePage = 2;

		/// <summary>
		/// CSS pixels of ctrl-wheel travel that make one unit of pinch magnification.
		/// </summary>
		/// <remarks>
		/// A trackpad pinch reaches the page as a synthetic ctrl+wheel with no scale on it, so the pinch has
		/// to be recovered from the travel. Both Chromium and WebKit synthesize that event from the gesture's
		/// scale as roughly <c>deltaY = -100 * ln(scale)</c>, and for the small per-event steps a pinch is
		/// made of, <c>ln(scale) = scale - 1</c>, which is exactly what AppKit calls the incremental
		/// magnification. So 100 CSS pixels of ctrl-wheel is magnification 1.0 - "twice the size" - which
		/// <see cref="WheelDeltaMath.MagnificationToWheelDelta"/> then turns into about five detents of zoom,
		/// the same order as a comfortable two-finger scroll. Going through magnification rather than
		/// straight to wheel units is what keeps the browser's pinch and the mac's pinch feeling the same;
		/// the constant is the tunable part, the routing is not.
		/// </remarks>
		private const double PinchCssPixelsPerMagnificationUnit = 100;

		/// <summary>
		/// Whether a <c>deltaMode</c> means the event carries real travel rather than accelerated lines.
		/// </summary>
		public static bool IsPreciseScroll(int deltaMode) => deltaMode == DeltaModePixel;

		/// <summary>
		/// Fills a mouse event's wheel axes from one <c>wheel</c> event.
		/// </summary>
		/// <remarks>
		/// <b>The signs are inverted, and that is the whole difference from AppKit.</b> A browser's
		/// <c>deltaY</c> is how far the <em>content</em> should move up, so scrolling away from the user -
		/// the forward wheel, which is agg's positive - reports a negative deltaY. AppKit's
		/// <c>scrollingDeltaY</c> is the other way round: it reports how far the content should move down,
		/// so a forward wheel is positive there and needs no flip. <c>deltaX</c> flips for the same reason:
		/// a browser's positive deltaX scrolls the content left, while agg's
		/// <see cref="MouseEventArgs.WheelDeltaX"/> is AppKit's convention where positive means the content
		/// should move right. Copying the mac host's absence of a flip would invert every scroll and every
		/// zoom in the app.
		/// <para/>
		/// <paramref name="devicePixelRatio"/> is the browser's per-window backing scale, and it is the
		/// right number for the same reason <c>backingScaleFactor</c> is on a mac: a precise deltaY is in
		/// CSS pixels, which are points, and agg's coordinates are device pixels. See
		/// <see cref="WheelDeltaMath.ScrollingDeltaToWheelDelta"/>, which is where DPI is applied and the
		/// only place.
		/// </remarks>
		/// <param name="ctrlKey">The event's <c>ctrlKey</c>. A trackpad pinch arrives as a ctrl+wheel and is
		/// routed to <see cref="ApplyPinch"/> - see there for why that is not a heuristic.</param>
		public static void ApplyWheelEvent(
			MouseEventArgs args,
			double deltaX,
			double deltaY,
			int deltaMode,
			bool ctrlKey,
			double devicePixelRatio)
		{
			if (ctrlKey)
			{
				ApplyPinch(args, deltaY, deltaMode);
				return;
			}

			WheelDeltaMath.ApplyScrollingDeltas(
				args,
				-deltaX,
				-deltaY,
				IsPreciseScroll(deltaMode),
				devicePixelRatio);
		}

		/// <summary>
		/// Fills a mouse event's wheel axes from a ctrl+wheel, which is a pinch.
		/// </summary>
		/// <remarks>
		/// Every engine synthesizes a trackpad pinch as a wheel event with <c>ctrlKey</c> set, and there is
		/// no other signal to tell it from a real Ctrl held over a real wheel - which is fine, because the
		/// two mean the same thing to a browser user (Ctrl+wheel is zoom everywhere) and so should mean the
		/// same thing to agg. What they do not share is a magnitude, which is why the deltaMode still
		/// decides: a pinch is DOM_DELTA_PIXEL and carries recoverable travel, while Ctrl over a real wheel
		/// is DOM_DELTA_LINE in Firefox and carries only an accelerated line count, so it becomes one signed
		/// detent per event exactly as an ordinary line scroll does.
		/// <para/>
		/// Sideways travel is dropped rather than passed through: a pinch is one number, and a two-axis
		/// zoom is not a thing agg's consumers can read.
		/// </remarks>
		public static void ApplyPinch(MouseEventArgs args, double deltaY, int deltaMode)
		{
			// Negated for the same reason a scroll is, and to the same effect: fingers apart, which is zoom
			// in, reports a negative deltaY and has to reach agg as a forward wheel.
			args.WheelDelta = IsPreciseScroll(deltaMode)
				? WheelDeltaMath.MagnificationToWheelDelta(-deltaY / PinchCssPixelsPerMagnificationUnit)
				: WheelDeltaMath.ScrollingDeltaToWheelDelta(-deltaY, precise: false, backingScale: 1);

			args.WheelDeltaX = 0;

			// Never precise, whichever branch ran: these are zoom steps and not a distance to scroll, so a
			// consumer must not scale them by anything. See MouseEventArgs.WheelDeltaIsPreciseScroll.
			args.WheelDeltaIsPreciseScroll = false;
		}
	}
}
