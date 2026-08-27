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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Turns a DOM pointer event into the numbers an agg <see cref="MouseEventArgs"/> is made of.
	/// </summary>
	/// <remarks>
	/// Pure - no JS interop, no state - so the coordinate transform and the button mapping run in the
	/// desktop test suite, in the same spirit as <c>X11SystemWindow.FlipY</c>.
	/// </remarks>
	public static class BrowserPointer
	{
		/// <summary>The <c>buttons</c> bit for the primary button.</summary>
		public const int ButtonsMaskLeft = 1;

		/// <summary>The <c>buttons</c> bit for the secondary button. Note it is not <c>button</c>'s 2.</summary>
		public const int ButtonsMaskRight = 2;

		/// <summary>The <c>buttons</c> bit for the middle button. Note it is not <c>button</c>'s 1.</summary>
		public const int ButtonsMaskMiddle = 4;

		/// <summary>
		/// Converts a pointer event's position into agg's coordinate space.
		/// </summary>
		/// <remarks>
		/// Two things happen here. <c>offsetX</c>/<c>offsetY</c> are CSS pixels relative to the target's
		/// padding box, so they are multiplied by <c>devicePixelRatio</c> to reach agg's device pixels - the
		/// same step the mac host makes with <c>backingScaleFactor</c>.
		/// <para/>
		/// And then Y is flipped, which is the step that is <em>not</em> in the mac host. The browser's
		/// origin is the top-left with Y increasing downwards, which is Win32's convention and X11's, not
		/// agg's; a non-flipped NSView is already bottom-left, which is why <c>MacSystemWindow</c> has no
		/// flip at all and copying its absence here would put every click on the wrong half of the window.
		/// The flip is against the canvas's height in <em>device</em> pixels, because that is the space the
		/// answer is in.
		/// <para/>
		/// <c>height - y</c> and not <c>height - 1 - y</c>: agg's bounds are a closed interval, so a window
		/// of height H spans y = 0 through y = H, and <c>AutomationRunner</c> converts back with the same
		/// <c>Height - y</c> so a synthetic click round-trips exactly. See <c>X11SystemWindow.FlipY</c>,
		/// which has the full argument.
		/// <para/>
		/// Deliberately not clamped to the canvas: a drag that ran past the edge should reach the widget
		/// with where the pointer really is - the same coordinates WinForms reports while it holds the
		/// capture - so that dragging out and back does not look like a jump to the edge and stop. See
		/// <see cref="OutOfViewMouseCapture"/> for the rule that keeps those events coming at all.
		/// </remarks>
		/// <param name="pixelHeight">The canvas's height in device pixels - what the backing store is, not
		/// what CSS says the element is.</param>
		public static Vector2 ToAggPosition(double offsetX, double offsetY, double devicePixelRatio, double pixelHeight)
			=> new Vector2(offsetX * devicePixelRatio, pixelHeight - (offsetY * devicePixelRatio));

		/// <summary>
		/// Maps a <c>PointerEvent.button</c> index onto agg's <see cref="MouseButtons"/>.
		/// </summary>
		/// <returns><see cref="MouseButtons.None"/> for a button agg has no name for - the back and forward
		/// buttons (3 and 4), an eraser (5), and the -1 a move reports for "no button changed".</returns>
		public static MouseButtons TranslateButton(int button) => button switch
		{
			0 => MouseButtons.Left,
			1 => MouseButtons.Middle,
			2 => MouseButtons.Right,
			_ => MouseButtons.None,
		};

		/// <summary>
		/// The agg button a drag is being made with, read from the <c>buttons</c> bitmask.
		/// </summary>
		/// <remarks>
		/// A pointermove carries <c>button == -1</c> because no button changed on it, so the held button has
		/// to come from the mask instead - and the mask's bits are not the index's numbering, which is the
		/// trap this exists to contain (right is index 2 but bit 2, middle is index 1 but bit 4).
		/// <para/>
		/// One button is reported even when several are held, because agg's <see cref="MouseEventArgs"/>
		/// carries one; the primary wins, which is what a user dragging with two buttons down means.
		/// </remarks>
		public static MouseButtons HeldButton(int buttons)
		{
			if ((buttons & ButtonsMaskLeft) != 0)
			{
				return MouseButtons.Left;
			}

			if ((buttons & ButtonsMaskMiddle) != 0)
			{
				return MouseButtons.Middle;
			}

			if ((buttons & ButtonsMaskRight) != 0)
			{
				return MouseButtons.Right;
			}

			return MouseButtons.None;
		}

		/// <summary>
		/// Which of <see cref="PointerEventKind"/>'s four kinds a DOM pointer event type is, so the shared
		/// capture rule in <see cref="OutOfViewMouseCapture"/> never has to know the DOM's names.
		/// </summary>
		/// <remarks>
		/// The DOM has one move event for both hover and drag - the <c>buttons</c> mask is what tells them
		/// apart, as X11's button state does and unlike AppKit's separate dragged event types.
		/// </remarks>
		/// <param name="buttons">The event's <c>buttons</c> bitmask; only read for a move.</param>
		public static PointerEventKind PointerEventKindFor(string type, int buttons)
		{
			switch (type)
			{
				case "pointerdown":
					return PointerEventKind.Down;

				// A cancel is the browser taking the pointer away - the page scrolled, another gesture won,
				// the device was unplugged - and no pointerup is coming after it. Calling it an up is what
				// stops a button staying captured forever with no event left that could ever clear it.
				case "pointerup":
				case "pointercancel":
					return PointerEventKind.Up;

				case "pointermove":
					return buttons == 0 ? PointerEventKind.Other : PointerEventKind.Drag;

				default:
					return PointerEventKind.Other;
			}
		}

		/// <summary>
		/// Whether a pointer event should reach agg, and the point at which the capture set is updated.
		/// </summary>
		/// <remarks>
		/// The browser has <c>setPointerCapture</c>, which routes a drag's events to the element that took
		/// the capture however far the pointer travels - so on paper this arbiter is redundant here in a way
		/// it is not on macOS or X11. It is kept anyway because the capture can be lost without warning
		/// (Safari drops it on some gestures, and a pointercancel ends it outright), and because the rule
		/// that a button only becomes ours through a down inside the view is what keeps a stray up from
		/// reaching a widget that never saw its down. Belt and braces on the platform whose pointer model is
		/// the least predictable of the three.
		/// </remarks>
		/// <param name="button">The agg button the event carries: from <see cref="TranslateButton"/> for a
		/// down or an up, and from <see cref="HeldButton"/> for a move.</param>
		public static bool ShouldDeliver(
			OutOfViewMouseCapture capture,
			string type,
			int buttons,
			MouseButtons button,
			bool insideView)
			=> capture.ShouldDeliver(PointerEventKindFor(type, buttons), button, insideView);

		/// <summary>
		/// Composes the agg mouse event a pointer event carries, from the parts of it that determine one.
		/// </summary>
		/// <param name="detail">The event's <c>detail</c>, which is the click count on a down or an up and
		/// zero on a move. Passed straight through: the browser already does the double-click timing, and
		/// second-guessing it would disagree with the platform the user set it on.</param>
		public static MouseEventArgs MakeMouseEventArgs(
			MouseButtons button,
			int detail,
			double offsetX,
			double offsetY,
			double devicePixelRatio,
			double pixelHeight)
		{
			Vector2 position = ToAggPosition(offsetX, offsetY, devicePixelRatio, pixelHeight);

			return new MouseEventArgs(button, detail, position.X, position.Y, 0);
		}
	}
}
