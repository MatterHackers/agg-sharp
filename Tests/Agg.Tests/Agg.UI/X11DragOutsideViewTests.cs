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

using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Linux;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The X11 half of the out-of-view drag fix: X11's event numbering and its pixel grid, translated into the
	/// terms the shared filter is written in. The filter's own behaviour is
	/// <see cref="OutOfViewMouseCaptureTests"/>, which runs on every OS; what is left here is everything X11
	/// does differently - the mapping onto <see cref="PointerEventKind"/>, the exclusive-edge bounds test, the
	/// crossings a grab manufactures, and the button-state reconcile that recovers a release the server never
	/// delivered.
	///
	/// <para>
	/// The Y flip and the button numbering live here too, because they are the same question - "where is this
	/// event, and is it ours" - asked of the same geometry.
	/// </para>
	/// </summary>
	public class X11DragOutsideViewTests
	{
		/// <summary>
		/// X11 has one motion event for hover and drag both - the button the state word names is what tells
		/// them apart, where AppKit has separate event types. Mapping a hover onto Drag would hand it to a
		/// widget that never saw a button go down; mapping a drag onto Other would drop the moves that keep a
		/// gesture alive once the pointer has left the window.
		/// </summary>
		[Test]
		[Arguments(X11.ButtonPress, MouseButtons.Left, PointerEventKind.Down)]
		[Arguments(X11.ButtonRelease, MouseButtons.Left, PointerEventKind.Up)]
		[Arguments(X11.MotionNotify, MouseButtons.Left, PointerEventKind.Drag)]
		[Arguments(X11.MotionNotify, MouseButtons.None, PointerEventKind.Other)]
		[Arguments(X11.EnterNotify, MouseButtons.None, PointerEventKind.Other)]
		public async Task XEventTypesMapToTheSharedKinds(int eventType, MouseButtons button, PointerEventKind expected)
		{
			await Assert.That(X11SystemWindow.PointerEventKindFor(eventType, button)).IsEqualTo(expected);
		}

		/// <summary>
		/// The arbitration hook seen through the X11 numbering: a drag whose press landed inside still delivers
		/// its moves and, critically, its release. Dropping that release is what leaves a widget believing its
		/// button is still down. The rule is <see cref="OutOfViewMouseCapture"/>'s and is tested there; what is
		/// checked here is that the X11 event types reach it as the right kinds.
		/// </summary>
		[Test]
		public async Task ADragThatLeavesTheWindowStillDeliversItsMoveAndUp()
		{
			var capture = new OutOfViewMouseCapture();

			await Assert.That(Deliver(capture, X11.ButtonPress, MouseButtons.Left, insideWindow: true)).IsTrue();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Left, insideWindow: false)).IsTrue();
			await Assert.That(Deliver(capture, X11.ButtonRelease, MouseButtons.Left, insideWindow: false)).IsTrue();

			// And with the button up nothing is captured, so the same MotionNotify - now carrying no button -
			// is a plain hover and nobody's business.
			await Assert.That(capture.HasCapturedButtons).IsFalse();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.None, insideWindow: false)).IsFalse();
		}

		/// <summary>
		/// With the grab held, X11 delivers this window every press the pointer makes anywhere on the screen.
		/// None of them is agg's, and a phantom release out of one would be as bad as the missing release
		/// this filter exists to fix.
		/// </summary>
		[Test]
		public async Task APressOutsideTheWindowNeverBecomesAnAggDragOrUp()
		{
			var capture = new OutOfViewMouseCapture();

			await Assert.That(Deliver(capture, X11.ButtonPress, MouseButtons.Left, insideWindow: false)).IsFalse();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Left, insideWindow: false)).IsFalse();
			await Assert.That(Deliver(capture, X11.ButtonRelease, MouseButtons.Left, insideWindow: false)).IsFalse();
		}

		/// <summary>
		/// Taking or dropping a pointer grab makes the server synthesise a LeaveNotify/EnterNotify pair "as
		/// if the pointer warped", even though it has not moved at all. Believing those fires the
		/// pointer-gone sentinel every time a drag begins, which reads to any widget mid-drag as the pointer
		/// having left - MatterCAD's 3D view responds by snapping the dragged part back to where the drag
		/// started. Only the mode and the geometry can tell a real exit from a manufactured one.
		/// </summary>
		[Test]
		public async Task AGrabsSyntheticCrossingIsNotThePointerLeaving()
		{
			await Assert.That(X11SystemWindow.IsRealPointerExit(
				-1, 200, 640, 480, dragInFlight: false, mode: X11.NotifyGrab)).IsFalse();
			await Assert.That(X11SystemWindow.IsRealPointerExit(
				-1, 200, 640, 480, dragInFlight: false, mode: X11.NotifyUngrab)).IsFalse();

			// The same coordinates, arrived at by the pointer actually moving.
			await Assert.That(X11SystemWindow.IsRealPointerExit(
				-1, 200, 640, 480, dragInFlight: false, mode: X11.NotifyNormal)).IsTrue();
		}

		/// <summary>
		/// The pixel grid, which is where X11 and AppKit part company. A window 640 wide has columns 0 to
		/// 639, and a pointer leaving to the right reports exactly 640 - so the far edges are outside, not
		/// inside as they are on macOS where the bounds width is a continuous coordinate.
		/// </summary>
		[Test]
		public async Task TheFarEdgesAreOutsideThePixelGrid()
		{
			await Assert.That(X11SystemWindow.IsInsideBounds(0, 0, 640, 480)).IsTrue();
			await Assert.That(X11SystemWindow.IsInsideBounds(639, 479, 640, 480)).IsTrue();

			await Assert.That(X11SystemWindow.IsInsideBounds(640, 200, 640, 480)).IsFalse();
			await Assert.That(X11SystemWindow.IsInsideBounds(200, 480, 640, 480)).IsFalse();
			await Assert.That(X11SystemWindow.IsInsideBounds(-1, 200, 640, 480)).IsFalse();
			await Assert.That(X11SystemWindow.IsInsideBounds(200, -1, 640, 480)).IsFalse();
		}

		/// <summary>
		/// A drag owns the pointer until its button comes up, so even a genuine exit must not tell the widget
		/// the mouse vanished - that is the same "pointer is gone" that ends the drag by another route.
		/// </summary>
		[Test]
		public async Task ADragInFlightIsNeverToldThePointerLeft()
		{
			await Assert.That(X11SystemWindow.IsRealPointerExit(
				-180, 216, 640, 480, dragInFlight: true, mode: X11.NotifyNormal)).IsFalse();
			await Assert.That(X11SystemWindow.IsRealPointerExit(
				200, 216, 640, 480, dragInFlight: true, mode: X11.NotifyNormal)).IsFalse();
		}

		/// <summary>
		/// The one coordinate conversion X11 needs and macOS does not. X11's origin is the top-left with Y
		/// increasing downwards; agg's is the bottom-left with Y increasing upwards. Copying
		/// <c>MacSystemWindow</c>'s absence of a flip would put every click on the wrong half of the window,
		/// which is the single most consequential line in this whole translation.
		///
		/// <para>
		/// <c>height - y</c>, which is <c>WinformsEventSink</c>'s convention exactly. agg's bounds are a
		/// closed interval and <c>AutomationRunner</c> converts back with the same expression, so a synthetic
		/// click round-trips; a <c>height - 1 - y</c> here would land every automated click a pixel low.
		/// </para>
		/// </summary>
		[Test]
		public async Task TheTopOfTheXWindowIsTheTopOfTheAggWindow()
		{
			// A 640x480 window. The top of the window is X11's 0 and agg's 480.
			await Assert.That(X11SystemWindow.FlipY(0, 480)).IsEqualTo(480.0);

			// The bottom is X11's 480 and agg's 0.
			await Assert.That(X11SystemWindow.FlipY(480, 480)).IsEqualTo(0.0);

			// And the middle stays in the middle, which is the case a missing flip would still pass - hence
			// the two above it.
			await Assert.That(X11SystemWindow.FlipY(100, 480)).IsEqualTo(380.0);
		}

		/// <summary>
		/// A drag past the edge is deliberately not clamped, so the flip has to keep working outside the
		/// window: the widget wants where the pointer really is, so that dragging out and back does not look
		/// like a jump to the edge and stop.
		/// </summary>
		[Test]
		public async Task TheFlipIsNotClampedToTheWindow()
		{
			await Assert.That(X11SystemWindow.FlipY(-20, 480)).IsEqualTo(500.0);
			await Assert.That(X11SystemWindow.FlipY(600, 480)).IsEqualTo(-120.0);
		}

		[Test]
		[Arguments(X11.Button1, MouseButtons.Left)]
		[Arguments(X11.Button2, MouseButtons.Middle)]
		[Arguments(X11.Button3, MouseButtons.Right)]
		public async Task ButtonNumbersMapByPhysicalPosition(uint button, MouseButtons expected)
		{
			// X11 numbers the buttons left to right, so 2 is the middle one and 3 is the right one. Win32 and
			// AppKit both number them by role instead, which is why this pair looks transposed and why
			// copying either of them lands a right-click on the middle button.
			await Assert.That(X11SystemWindow.TranslateButton(button)).IsEqualTo(expected);
		}

		/// <summary>The wheel's synthetic buttons and the thumb buttons are not buttons agg has.</summary>
		[Test]
		public async Task TheButtonsAggHasNoNameForAreNone()
		{
			await Assert.That(X11SystemWindow.TranslateButton(X11.Button4)).IsEqualTo(MouseButtons.None);
			await Assert.That(X11SystemWindow.TranslateButton(X11.Button7)).IsEqualTo(MouseButtons.None);
			await Assert.That(X11SystemWindow.TranslateButton(8)).IsEqualTo(MouseButtons.None);
		}

		/// <summary>
		/// A state word can name several buttons at once, but <see cref="MouseEventArgs.Button"/> is one
		/// value and not a flag set, so one has to win - and which one is not arbitrary, because it is the
		/// button a mid-drag move gets attributed to and so the one the capture filter is asked about.
		/// </summary>
		[Test]
		public async Task TheHeldButtonIsReadOutOfTheStateWordInPriorityOrder()
		{
			await Assert.That(X11SystemWindow.TranslateButtonState(0)).IsEqualTo(MouseButtons.None);
			await Assert.That(X11SystemWindow.TranslateButtonState(X11.Button1Mask)).IsEqualTo(MouseButtons.Left);
			await Assert.That(X11SystemWindow.TranslateButtonState(X11.Button2Mask)).IsEqualTo(MouseButtons.Middle);
			await Assert.That(X11SystemWindow.TranslateButtonState(X11.Button3Mask)).IsEqualTo(MouseButtons.Right);

			// Left beats both; right beats middle.
			await Assert.That(X11SystemWindow.TranslateButtonState(X11.Button1Mask | X11.Button3Mask))
				.IsEqualTo(MouseButtons.Left);
			await Assert.That(X11SystemWindow.TranslateButtonState(X11.Button2Mask | X11.Button3Mask))
				.IsEqualTo(MouseButtons.Right);

			// The modifier half of the same word is not buttons.
			await Assert.That(X11SystemWindow.TranslateButtonState(X11.ShiftMask | X11.ControlMask))
				.IsEqualTo(MouseButtons.None);
		}

		/// <summary>
		/// The recovery path, wired to X11's state-word masks. A captured button is normally cleared by the
		/// release that ends the drag, but that release can genuinely never arrive - the grab was refused
		/// because another client held the pointer, or was broken by a window manager taking one of its own
		/// mid-drag. Left alone the window claims every move on the desktop belongs to a drag that ended
		/// minutes ago, and nothing else would ever clear it.
		/// </summary>
		[Test]
		public async Task ACaptureSurvivingItsLostReleaseIsReconciledAway()
		{
			var capture = new OutOfViewMouseCapture();

			Deliver(capture, X11.ButtonPress, MouseButtons.Left, insideWindow: true);
			await Assert.That(capture.HasCapturedButtons).IsTrue();

			// A move whose state says nothing is held: the release happened somewhere we never heard about.
			X11SystemWindow.ReconcileCaptureWithButtonState(capture, 0);

			await Assert.That(capture.HasCapturedButtons).IsFalse();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Left, insideWindow: false)).IsFalse();
		}

		/// <summary>
		/// Which bit means which button, which is the whole content of the X11 side of the reconcile: reading
		/// the wrong bit would drop a drag that is still running, or keep one that ended.
		/// </summary>
		[Test]
		public async Task AButtonStillHeldIsNotReconciledAway()
		{
			var capture = new OutOfViewMouseCapture();

			Deliver(capture, X11.ButtonPress, MouseButtons.Left, insideWindow: true);
			Deliver(capture, X11.ButtonPress, MouseButtons.Right, insideWindow: true);

			X11SystemWindow.ReconcileCaptureWithButtonState(capture, X11.Button1Mask | X11.Button3Mask);

			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Left, insideWindow: false)).IsTrue();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Right, insideWindow: false)).IsTrue();

			// And only the one that is really gone gets dropped.
			X11SystemWindow.ReconcileCaptureWithButtonState(capture, X11.Button1Mask);

			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Left, insideWindow: false)).IsTrue();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Right, insideWindow: false)).IsFalse();
		}

		/// <summary>
		/// The correction the caller has to make, stated as a test because getting it wrong is silent: a
		/// press reports the state <em>before</em> itself, so the button being captured is not in the word
		/// that captures it, and reconciling on the raw state would drop it on the very event that took it.
		/// </summary>
		[Test]
		public async Task APressIsNotReconciledAwayByItsOwnPrePressState()
		{
			var capture = new OutOfViewMouseCapture();

			// What HandleButton computes: the pre-press state (nothing held) plus this press's own bit.
			uint heldDuringPress = 0u | X11SystemWindow.ButtonStateMaskForButtonNumber(X11.Button1);

			X11SystemWindow.ReconcileCaptureWithButtonState(capture, heldDuringPress);
			Deliver(capture, X11.ButtonPress, MouseButtons.Left, insideWindow: true);

			await Assert.That(capture.HasCapturedButtons).IsTrue();
			await Assert.That(Deliver(capture, X11.MotionNotify, MouseButtons.Left, insideWindow: false)).IsTrue();
		}

		/// <summary>
		/// X11 has no click count of its own - a double click is two presses and the host counts them. Both
		/// thresholds have to be tested: a "double click" at two different places is two clicks, and using
		/// only the clock makes a fast user clicking down a list select the wrong thing.
		/// </summary>
		[Test]
		public async Task TwoQuickPressesInOnePlaceAreADoubleClick()
		{
			var counter = new X11SystemWindow.ClickCounter();

			await Assert.That(counter.CountPress(X11.Button1, 1000, 100, 100)).IsEqualTo(1);
			await Assert.That(counter.CountPress(X11.Button1, 1200, 101, 99)).IsEqualTo(2);
			await Assert.That(counter.CountPress(X11.Button1, 1400, 100, 100)).IsEqualTo(3);

			// The release reports what the press it ends did, which is what WinForms does.
			await Assert.That(counter.LastClickCount).IsEqualTo(3);
		}

		[Test]
		public async Task ASlowOrDistantOrDifferentSecondPressIsANewClick()
		{
			var counter = new X11SystemWindow.ClickCounter();

			// Too slow.
			counter.CountPress(X11.Button1, 1000, 100, 100);
			await Assert.That(counter.CountPress(X11.Button1, 1600, 100, 100)).IsEqualTo(1);

			// Too far.
			counter.CountPress(X11.Button1, 2000, 100, 100);
			await Assert.That(counter.CountPress(X11.Button1, 2100, 140, 100)).IsEqualTo(1);

			// A different button.
			counter.CountPress(X11.Button1, 3000, 100, 100);
			await Assert.That(counter.CountPress(X11.Button3, 3100, 100, 100)).IsEqualTo(1);
		}

		/// <summary>
		/// The server's clock is milliseconds in a 32-bit field, so it wraps every 49.7 days. Left alone the
		/// unsigned subtraction underflows, and a press after the wrap would join whatever click was in
		/// flight before it.
		/// </summary>
		[Test]
		public async Task AClockWrapDoesNotJoinTwoClicks()
		{
			var counter = new X11SystemWindow.ClickCounter();

			counter.CountPress(X11.Button1, uint.MaxValue - 10, 100, 100);

			await Assert.That(counter.CountPress(X11.Button1, 5, 100, 100)).IsEqualTo(1);
		}

		/// <summary>The shared filter asked the way <c>HandleButton</c> and <c>HandleMotion</c> ask it.</summary>
		private static bool Deliver(OutOfViewMouseCapture capture, int eventType, MouseButtons button, bool insideWindow)
			=> capture.ShouldDeliver(X11SystemWindow.PointerEventKindFor(eventType, button), button, insideWindow);
	}
}
