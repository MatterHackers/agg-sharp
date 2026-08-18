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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A two finger scroll on a trackpad arrives as one NSEvent carrying travel on both axes, and agg only
	/// understands wheel units. The event itself needs real fingers on real glass, but the conversion can be
	/// tested, and it is where a lost axis, a flipped sign, or a mismatched scale between the axes would hide.
	/// </summary>
	public class MacTrackpadScrollAxisTests
	{
		[Test]
		public async Task ATrackpadScrollTracksTheFingersOneToOne()
		{
			// Precise deltas are points of travel. ScrollableWidget turns WheelDelta back into pixels by
			// dividing by 5, so 5 x backingScale is what makes the content move exactly as far as the fingers.
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(10, precise: true, backingScale: 2)).IsEqualTo(100);
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(10, precise: true, backingScale: 1)).IsEqualTo(50);
		}

		[Test]
		public async Task ARealWheelStillClicksInDetents()
		{
			// A line based scroll (a mouse wheel) reports whole lines, and every agg consumer was written
			// against Win32's 120 units per detent.
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(1, precise: false, backingScale: 2)).IsEqualTo(120);
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(-3, precise: false, backingScale: 1)).IsEqualTo(-360);
		}

		[Test]
		public async Task TheSignIsCarriedStraightThrough()
		{
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(-10, precise: true, backingScale: 1)).IsLessThan(0);
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(0, precise: true, backingScale: 1)).IsEqualTo(0);
		}

		[Test]
		public async Task ANonsenseDeltaIsNoScroll()
		{
			// (int) of a NaN is a huge negative number rather than nothing, and that would fling the content.
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(double.NaN, precise: true, backingScale: 1)).IsEqualTo(0);
			await Assert.That(MacSystemWindow.ScrollingDeltaToWheelDelta(double.PositiveInfinity, precise: false, backingScale: 1)).IsEqualTo(0);
		}

		[Test]
		public async Task ADiagonalScrollFillsBothAxes()
		{
			var args = new MouseEventArgs(MouseButtons.None, 0, 5, 5, 0);

			MacSystemWindow.ApplyScrollingDeltas(args, scrollingDeltaX: -4, scrollingDeltaY: 10, precise: true, backingScale: 1);

			// Both axes go through the same scale, so a diagonal gesture keeps its angle.
			await Assert.That(args.WheelDelta).IsEqualTo(50);
			await Assert.That(args.WheelDeltaX).IsEqualTo(-20);
		}

		[Test]
		public async Task APurelyVerticalScrollLeavesTheHorizontalAxisAtZero()
		{
			var args = new MouseEventArgs(MouseButtons.None, 0, 5, 5, 0);

			MacSystemWindow.ApplyScrollingDeltas(args, scrollingDeltaX: 0, scrollingDeltaY: 10, precise: true, backingScale: 1);

			await Assert.That(args.WheelDelta).IsEqualTo(50);
			await Assert.That(args.WheelDeltaX).IsEqualTo(0);
		}
	}
}
