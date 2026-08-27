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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A browser reports scrolling with the opposite sign to AppKit and says which kind of device it is with
	/// deltaMode rather than a flag, so those two decisions are the whole of the browser's wheel handling -
	/// everything past them is <see cref="WheelDeltaMath"/>, which its own tests cover. The expectations here
	/// are written against that shared math rather than against copied constants, so a change to the feel of
	/// scrolling moves both together and only a real regression in the browser's half can fail these.
	/// </summary>
	public class BrowserWheelTests
	{
		/// <summary>
		/// The inversion. A browser's positive deltaY means the content should move up, which is the
		/// backward wheel; AppKit's positive scrollingDeltaY is the forward one. Getting this wrong inverts
		/// every scroll and every zoom in the application, and it is invisible in any test that only checks
		/// magnitudes.
		/// </summary>
		[Test]
		public async Task ScrollingDownIsABackwardWheel()
		{
			await Assert.That(Scroll(BrowserWheel.DeltaModeLine, deltaY: 120).WheelDelta).IsLessThan(0);
			await Assert.That(Scroll(BrowserWheel.DeltaModeLine, deltaY: -120).WheelDelta).IsGreaterThan(0);

			// The same inversion on a precise device, where the magnitude survives as well as the sign.
			await Assert.That(Scroll(BrowserWheel.DeltaModePixel, deltaY: 40).WheelDelta).IsLessThan(0);
			await Assert.That(Scroll(BrowserWheel.DeltaModePixel, deltaY: -40).WheelDelta).IsGreaterThan(0);
		}

		/// <summary>
		/// deltaX inverts too, and for the same reason: a browser's positive deltaX scrolls the content
		/// left, while agg's WheelDeltaX is AppKit's convention where positive means the content moves right.
		/// </summary>
		[Test]
		public async Task SidewaysScrollingInvertsAsWell()
		{
			MouseEventArgs args = Scroll(BrowserWheel.DeltaModePixel, deltaX: 30, deltaY: 0);

			await Assert.That(args.WheelDeltaX).IsLessThan(0);
			await Assert.That(args.WheelDelta).IsEqualTo(0);
		}

		/// <summary>
		/// DOM_DELTA_PIXEL is a trackpad: real travel, in CSS pixels, which is the precise branch of the
		/// shared math - so the whole magnitude has to survive, scaled by the device pixel ratio.
		/// </summary>
		[Test]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task PixelDeltasTakeThePreciseBranch(double devicePixelRatio)
		{
			MouseEventArgs args = Scroll(BrowserWheel.DeltaModePixel, deltaY: -17, deltaX: 3, devicePixelRatio: devicePixelRatio);

			await Assert.That(args.WheelDeltaIsPreciseScroll).IsTrue();
			await Assert.That(args.WheelDelta)
				.IsEqualTo(WheelDeltaMath.ScrollingDeltaToWheelDelta(17, precise: true, devicePixelRatio));
			await Assert.That(args.WheelDeltaX)
				.IsEqualTo(WheelDeltaMath.ScrollingDeltaToWheelDelta(-3, precise: true, devicePixelRatio));
		}

		/// <summary>
		/// DOM_DELTA_LINE and DOM_DELTA_PAGE are a real wheel: an accelerated line or page count that says
		/// nothing about distance, so each becomes one signed detent however large the number is. Claiming
		/// precision here would make one wheel click scroll the content by a hundred pixels.
		/// </summary>
		[Test]
		[Arguments(BrowserWheel.DeltaModeLine)]
		[Arguments(BrowserWheel.DeltaModePage)]
		public async Task LineAndPageDeltasTakeTheDetentBranch(int deltaMode)
		{
			MouseEventArgs oneLine = Scroll(deltaMode, deltaY: 1);
			MouseEventArgs manyLines = Scroll(deltaMode, deltaY: 9);

			await Assert.That(oneLine.WheelDeltaIsPreciseScroll).IsFalse();
			await Assert.That(oneLine.WheelDelta)
				.IsEqualTo(WheelDeltaMath.ScrollingDeltaToWheelDelta(-1, precise: false, backingScale: 1));

			// The magnitude is deliberately discarded: nine accelerated lines is still one turn of the wheel.
			await Assert.That(manyLines.WheelDelta).IsEqualTo(oneLine.WheelDelta);

			await Assert.That(BrowserWheel.IsPreciseScroll(deltaMode)).IsFalse();
			await Assert.That(BrowserWheel.IsPreciseScroll(BrowserWheel.DeltaModePixel)).IsTrue();
		}

		/// <summary>
		/// A trackpad pinch reaches the page as a synthetic ctrl+wheel, so ctrlKey is what routes an event to
		/// the magnification path - through the same conversion the mac host's pinch uses, which is what
		/// keeps the two feeling alike.
		/// </summary>
		[Test]
		public async Task APinchGoesThroughTheMagnificationConversion()
		{
			// Chromium and WebKit synthesize a pinch as deltaY = -100 * ln(scale), so 100 CSS pixels of
			// ctrl-wheel is one whole unit of magnification - "twice the size" in AppKit's terms.
			MouseEventArgs wholeUnit = Scroll(BrowserWheel.DeltaModePixel, deltaY: -100, ctrlKey: true);

			await Assert.That(wholeUnit.WheelDelta).IsEqualTo(WheelDeltaMath.MagnificationToWheelDelta(1));

			// Fingers apart is a negative deltaY and has to come out as a forward wheel, which is zoom in.
			await Assert.That(wholeUnit.WheelDelta).IsGreaterThan(0);
			await Assert.That(Scroll(BrowserWheel.DeltaModePixel, deltaY: 100, ctrlKey: true).WheelDelta)
				.IsEqualTo(WheelDeltaMath.MagnificationToWheelDelta(-1));

			// A single event of a real pinch is a few CSS pixels, and it must not round away to nothing or a
			// slow pinch would do nothing at all.
			await Assert.That(Scroll(BrowserWheel.DeltaModePixel, deltaY: -4, ctrlKey: true).WheelDelta)
				.IsGreaterThan(0);
		}

		/// <summary>
		/// A pinch is one number, so no sideways travel leaks out of it - and it is never a precise scroll,
		/// whichever branch it took: these are zoom steps, not a distance to scroll.
		/// </summary>
		[Test]
		public async Task APinchIsOneAxisAndNeverPrecise()
		{
			MouseEventArgs args = Scroll(BrowserWheel.DeltaModePixel, deltaX: 25, deltaY: -8, ctrlKey: true);

			await Assert.That(args.WheelDeltaX).IsEqualTo(0);
			await Assert.That(args.WheelDeltaIsPreciseScroll).IsFalse();
		}

		/// <summary>
		/// Ctrl held over a real mouse wheel means zoom too, but carries only an accelerated line count -
		/// Firefox reports DOM_DELTA_LINE for it - so it becomes one signed detent rather than a pinch
		/// magnitude read out of a number that is not a distance.
		/// </summary>
		[Test]
		public async Task CtrlOverARealWheelIsADetentOfZoom()
		{
			MouseEventArgs args = Scroll(BrowserWheel.DeltaModeLine, deltaY: -3, ctrlKey: true);

			await Assert.That(args.WheelDelta)
				.IsEqualTo(WheelDeltaMath.ScrollingDeltaToWheelDelta(3, precise: false, backingScale: 1));
			await Assert.That(args.WheelDeltaIsPreciseScroll).IsFalse();
		}

		/// <summary>Feeds one wheel event through the real translation and hands back what agg would see.</summary>
		private static MouseEventArgs Scroll(
			int deltaMode,
			double deltaY = 0,
			double deltaX = 0,
			double devicePixelRatio = 1,
			bool ctrlKey = false)
		{
			var args = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0);

			BrowserWheel.ApplyWheelEvent(args, deltaX, deltaY, deltaMode, ctrlKey, devicePixelRatio);

			return args;
		}
	}
}
