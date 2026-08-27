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

using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The browser reports pointer positions in CSS pixels from the top-left, and agg wants device pixels
	/// from the bottom-left, so every click in the application passes through one multiply and one flip. A
	/// missing flip puts every click on the wrong half of the window and a missing scale halves every
	/// coordinate on a Retina screen, and neither is visible in a test that only uses a 1x window at the
	/// origin - hence both device pixel ratios below.
	/// </summary>
	public class BrowserPointerTests
	{
		/// <summary>A 400x300 CSS canvas, which is 800x600 device pixels at 2x.</summary>
		private const double CanvasCssHeight = 300;

		/// <summary>
		/// The top-left corner of the canvas is agg's top-left, which is Y = height; the bottom-left corner
		/// is agg's origin. Those two are the whole of the flip, and they hold at either scale.
		/// </summary>
		[Test]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task TheOriginMovesFromTheTopLeftToTheBottomLeft(double devicePixelRatio)
		{
			double pixelHeight = CanvasCssHeight * devicePixelRatio;

			Vector2 topLeft = BrowserPointer.ToAggPosition(0, 0, devicePixelRatio, pixelHeight);
			Vector2 bottomLeft = BrowserPointer.ToAggPosition(0, CanvasCssHeight, devicePixelRatio, pixelHeight);

			await Assert.That(topLeft.X).IsEqualTo(0);
			await Assert.That(topLeft.Y).IsEqualTo(pixelHeight);
			await Assert.That(bottomLeft.X).IsEqualTo(0);
			await Assert.That(bottomLeft.Y).IsEqualTo(0);
		}

		/// <summary>
		/// A point in the middle, where a wrong scale and a wrong flip cannot cancel each other out. At 2x
		/// the CSS point (100, 60) is device (200, 120) from the top, so 480 up from the bottom of a 600
		/// pixel canvas.
		/// </summary>
		[Test]
		[Arguments(1.0, 100.0, 240.0)]
		[Arguments(2.0, 200.0, 480.0)]
		public async Task CssPixelsBecomeDevicePixelsWithYFlipped(double devicePixelRatio, double expectedX, double expectedY)
		{
			Vector2 position = BrowserPointer.ToAggPosition(100, 60, devicePixelRatio, CanvasCssHeight * devicePixelRatio);

			await Assert.That(position.X).IsEqualTo(expectedX);
			await Assert.That(position.Y).IsEqualTo(expectedY);
		}

		/// <summary>
		/// Deliberately unclamped: a drag that ran past the edge has to reach the widget with where the
		/// pointer really is - the coordinates WinForms reports while it holds the capture - or dragging out
		/// and back looks like a jump to the edge and a stop.
		/// </summary>
		[Test]
		public async Task ADragPastTheEdgeKeepsItsRealCoordinates()
		{
			Vector2 aboveTheTop = BrowserPointer.ToAggPosition(-40, -25, 2, CanvasCssHeight * 2);

			await Assert.That(aboveTheTop.X).IsEqualTo(-80);
			await Assert.That(aboveTheTop.Y).IsEqualTo(650);
		}

		/// <summary>
		/// PointerEvent.button is an index and not a mask, and its middle and right are the other way round
		/// from the mask's bits - which is exactly the mix-up this table exists to pin down.
		/// </summary>
		[Test]
		[Arguments(0, MouseButtons.Left)]
		[Arguments(1, MouseButtons.Middle)]
		[Arguments(2, MouseButtons.Right)]
		[Arguments(3, MouseButtons.None)]
		[Arguments(4, MouseButtons.None)]
		[Arguments(-1, MouseButtons.None)]
		public async Task ButtonIndicesMap(int button, MouseButtons expected)
		{
			await Assert.That(BrowserPointer.TranslateButton(button)).IsEqualTo(expected);
		}

		/// <summary>
		/// The held button on a move comes from the bitmask instead, whose bits are not the index numbering.
		/// The primary wins when several are held, because agg's event carries one button.
		/// </summary>
		[Test]
		public async Task TheHeldButtonComesFromTheMask()
		{
			await Assert.That(BrowserPointer.HeldButton(0)).IsEqualTo(MouseButtons.None);
			await Assert.That(BrowserPointer.HeldButton(BrowserPointer.ButtonsMaskLeft)).IsEqualTo(MouseButtons.Left);
			await Assert.That(BrowserPointer.HeldButton(BrowserPointer.ButtonsMaskMiddle)).IsEqualTo(MouseButtons.Middle);
			await Assert.That(BrowserPointer.HeldButton(BrowserPointer.ButtonsMaskRight)).IsEqualTo(MouseButtons.Right);

			await Assert.That(BrowserPointer.HeldButton(BrowserPointer.ButtonsMaskLeft | BrowserPointer.ButtonsMaskRight))
				.IsEqualTo(MouseButtons.Left);
		}

		/// <summary>
		/// The click count is passed straight through from the DOM's detail: the browser already applies the
		/// platform's own double-click timing, and second-guessing it would disagree with the machine the
		/// user set it on.
		/// </summary>
		[Test]
		public async Task TheClickCountAndButtonReachTheEvent()
		{
			MouseEventArgs args = BrowserPointer.MakeMouseEventArgs(
				MouseButtons.Right,
				detail: 2,
				offsetX: 100,
				offsetY: 60,
				devicePixelRatio: 2,
				pixelHeight: CanvasCssHeight * 2);

			await Assert.That(args.Button).IsEqualTo(MouseButtons.Right);
			await Assert.That(args.Clicks).IsEqualTo(2);
			await Assert.That(args.X).IsEqualTo(200);
			await Assert.That(args.Y).IsEqualTo(480);
		}

		/// <summary>
		/// The DOM's one move event is both a hover and a drag, and the buttons mask is what tells them
		/// apart - the shared capture rule cares about nothing else.
		/// </summary>
		[Test]
		[Arguments("pointerdown", 1, PointerEventKind.Down)]
		[Arguments("pointerup", 0, PointerEventKind.Up)]
		[Arguments("pointercancel", 0, PointerEventKind.Up)]
		[Arguments("pointermove", 0, PointerEventKind.Other)]
		[Arguments("pointermove", 1, PointerEventKind.Drag)]
		[Arguments("pointerover", 0, PointerEventKind.Other)]
		[Arguments("wheel", 0, PointerEventKind.Other)]
		public async Task PointerEventTypesMapToTheSharedKinds(string type, int buttons, PointerEventKind expected)
		{
			await Assert.That(BrowserPointer.PointerEventKindFor(type, buttons)).IsEqualTo(expected);
		}

		/// <summary>
		/// The arbitration hook: a drag whose down landed in the canvas keeps getting its moves and its up
		/// after the pointer has left, and a down outside never becomes ours at all. The rule itself is
		/// <see cref="OutOfViewMouseCapture"/>'s and has its own tests; what is checked here is that the DOM
		/// event names reach it as the right kinds.
		/// </summary>
		[Test]
		public async Task ADragKeepsBeingDeliveredAfterItLeavesTheCanvas()
		{
			var capture = new OutOfViewMouseCapture();

			await Assert.That(Deliver(capture, "pointerdown", BrowserPointer.ButtonsMaskLeft, MouseButtons.Left, insideView: true))
				.IsTrue();
			await Assert.That(Deliver(capture, "pointermove", BrowserPointer.ButtonsMaskLeft, MouseButtons.Left, insideView: false))
				.IsTrue();
			await Assert.That(Deliver(capture, "pointerup", 0, MouseButtons.Left, insideView: false))
				.IsTrue();

			// And once the up has come, nothing is captured any more: a hover outside is nobody's business.
			await Assert.That(capture.HasCapturedButtons).IsFalse();
			await Assert.That(Deliver(capture, "pointermove", 0, MouseButtons.None, insideView: false))
				.IsFalse();
		}

		/// <summary>A press that started outside the canvas is not ours, so its drag is not either.</summary>
		[Test]
		public async Task APressOutsideTheCanvasIsNeverDelivered()
		{
			var capture = new OutOfViewMouseCapture();

			await Assert.That(Deliver(capture, "pointerdown", BrowserPointer.ButtonsMaskLeft, MouseButtons.Left, insideView: false))
				.IsFalse();
			await Assert.That(Deliver(capture, "pointermove", BrowserPointer.ButtonsMaskLeft, MouseButtons.Left, insideView: false))
				.IsFalse();
		}

		private static bool Deliver(OutOfViewMouseCapture capture, string type, int buttons, MouseButtons button, bool insideView)
			=> BrowserPointer.ShouldDeliver(capture, type, buttons, button, insideView);
	}
}
