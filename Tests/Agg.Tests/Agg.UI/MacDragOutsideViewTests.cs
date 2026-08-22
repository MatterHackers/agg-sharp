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
using MatterHackers.Agg.Platform.Mac;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static MatterHackers.Agg.Platform.Mac.AppKitConstants;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A drag that ends past the window edge must still deliver its mouse up. AppKit routes dragged and up
	/// events to the window that saw the down no matter where the pointer went, so the events arrive; the
	/// only question - and the only thing that can be tested without a real NSEvent - is whether the
	/// out-of-view filter lets them through.
	/// </summary>
	public class MacDragOutsideViewTests
	{
		[Test]
		public async Task ADragThatLeavesTheViewStillDeliversItsMoveAndUp()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDown, MouseButtons.Left, insideView: true)).IsTrue();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: false)).IsTrue();

			// The one that matters: dropping this is what left the widget believing the button was still down.
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: false)).IsTrue();
		}

		[Test]
		public async Task AHoverOutsideTheViewIsStillDropped()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			await Assert.That(capture.ShouldDeliver(NSEventTypeMouseMoved, MouseButtons.None, insideView: false)).IsFalse();
			await Assert.That(capture.ShouldDeliver(NSEventTypeMouseMoved, MouseButtons.None, insideView: true)).IsTrue();
		}

		[Test]
		public async Task ADragEndedOutsideDoesNotCaptureTheNextOne()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			capture.ShouldDeliver(NSEventTypeLeftMouseDown, MouseButtons.Left, insideView: true);
			capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: false);

			// With the button released, a drag whose down agg never saw is not this view's to deliver.
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: false)).IsFalse();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: false)).IsFalse();
		}

		[Test]
		public async Task ATitleBarPressNeverBecomesAnAggDragOrUp()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			// The title bar is part of the same NSWindow, so its events reach this code; none of them are
			// agg's, and a phantom up out of one would be as bad as the missing up this class exists to fix.
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDown, MouseButtons.Left, insideView: false)).IsFalse();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: false)).IsFalse();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: false)).IsFalse();
		}

		[Test]
		public async Task AnOrdinaryInViewDragDeliversEveryEventItsButtonAndPoints()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			// The overwhelmingly common case, and the one a regression is most likely to break: press,
			// drag, release, all of it well inside the view. Nothing here may be filtered.
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDown, MouseButtons.Left, insideView: true)).IsTrue();

			for (int move = 0; move < 5; move++)
			{
				await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: true)).IsTrue();
			}

			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: true)).IsTrue();
		}

		[Test]
		public async Task ADragThatLeavesAndComesBackKeepsDelivering()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			capture.ShouldDeliver(NSEventTypeLeftMouseDown, MouseButtons.Left, insideView: true);

			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: true)).IsTrue();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: false)).IsTrue();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseDragged, MouseButtons.Left, insideView: true)).IsTrue();
			await Assert.That(capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: true)).IsTrue();
		}

		[Test]
		public async Task ACapturedDragOwnsThePointerUntilItsButtonComesUp()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			await Assert.That(capture.HasCapturedButtons).IsFalse();

			capture.ShouldDeliver(NSEventTypeLeftMouseDown, MouseButtons.Left, insideView: true);
			await Assert.That(capture.HasCapturedButtons).IsTrue();

			capture.ShouldDeliver(NSEventTypeLeftMouseUp, MouseButtons.Left, insideView: false);
			await Assert.That(capture.HasCapturedButtons).IsFalse();
		}

		/// <summary>
		/// A mouseExited is a tracking notification, not a statement that the pointer left: the content
		/// view's cursor rect posts one on every rebuild while the mouse sits still. Only the geometry can
		/// tell the two apart, and believing the event type instead is what snapped a dragged 3D part back
		/// to where its drag started. The coordinates below are the ones measured from AppKit.
		/// </summary>
		[Test]
		public async Task ACursorRectRebuildIsNotThePointerLeaving()
		{
			var bounds = new CGRect(0, 0, 400, 400);

			// What -invalidateCursorRectsForView: posts, measured: the pointer has not moved off the centre,
			// so this mouseExited is the cursor rect being rebuilt and nothing more.
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(200, 216), bounds, dragInFlight: false)).IsFalse();

			// What a real exit posts, also measured - out to the left, and up over the title bar.
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(-180, 216), bounds, dragInFlight: false)).IsTrue();
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(200, 437), bounds, dragInFlight: false)).IsTrue();

			// The edges belong to the view, so the last row of pixels is still the view's.
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(0, 0), bounds, dragInFlight: false)).IsFalse();
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(400, 400), bounds, dragInFlight: false)).IsFalse();
		}

		/// <summary>
		/// A drag owns the pointer until its button comes up, so even a genuine exit must not tell the widget
		/// the mouse vanished - that is the same "pointer is gone" that ends the drag by another route.
		/// </summary>
		[Test]
		public async Task ADragInFlightIsNeverToldThePointerLeft()
		{
			var bounds = new CGRect(0, 0, 400, 400);

			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(-180, 216), bounds, dragInFlight: true)).IsFalse();
			await Assert.That(MacSystemWindow.IsRealPointerExit(new CGPoint(200, 216), bounds, dragInFlight: true)).IsFalse();
		}

		[Test]
		public async Task EachButtonIsCapturedOnItsOwn()
		{
			var capture = new MacSystemWindow.OutOfViewMouseCapture();

			capture.ShouldDeliver(NSEventTypeRightMouseDown, MouseButtons.Right, insideView: true);

			await Assert.That(capture.ShouldDeliver(NSEventTypeRightMouseDragged, MouseButtons.Right, insideView: false)).IsTrue();
			await Assert.That(capture.ShouldDeliver(NSEventTypeOtherMouseDragged, MouseButtons.Middle, insideView: false)).IsFalse();
		}
	}
}
