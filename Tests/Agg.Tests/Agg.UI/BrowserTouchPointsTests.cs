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
	/// <summary>How the browser's one-pointer-per-finger events become agg's one mouse with many positions.</summary>
	public class BrowserTouchPointsTests
	{
		[Test]
		public async Task MouseAndPenPassStraightThrough()
		{
			var touches = new BrowserTouchPoints();
			await Assert.That(touches.Update("pointerdown", 1, "mouse", Vector2.Zero)).IsEqualTo(BrowserTouchAction.Forward);
			await Assert.That(touches.Update("pointermove", 2, "pen", Vector2.Zero)).IsEqualTo(BrowserTouchAction.Forward);
			await Assert.That(touches.Count).IsEqualTo(0);
		}

		[Test]
		public async Task TheFirstFingerIsTheMouseAndTheSecondRidesOnItsMoves()
		{
			var touches = new BrowserTouchPoints();
			await Assert.That(touches.Update("pointerdown", 7, "touch", new Vector2(10, 10))).IsEqualTo(BrowserTouchAction.Forward);
			await Assert.That(touches.Update("pointermove", 7, "touch", new Vector2(12, 10))).IsEqualTo(BrowserTouchAction.Forward);

			// The second finger's press is no mouse press - agg has one button - just a move with two positions.
			await Assert.That(touches.Update("pointerdown", 9, "touch", new Vector2(50, 50))).IsEqualTo(BrowserTouchAction.MoveWithAllTouches);
			await Assert.That(touches.Positions).IsEquivalentTo(new[] { new Vector2(12, 10), new Vector2(50, 50) });

			await Assert.That(touches.Update("pointermove", 9, "touch", new Vector2(60, 50))).IsEqualTo(BrowserTouchAction.MoveWithAllTouches);
			await Assert.That(touches.Update("pointermove", 7, "touch", new Vector2(0, 10))).IsEqualTo(BrowserTouchAction.MoveWithAllTouches);
			await Assert.That(touches.Positions).IsEquivalentTo(new[] { new Vector2(0, 10), new Vector2(60, 50) });

			// The second lifting drops back to one position, told as a move.
			await Assert.That(touches.Update("pointerup", 9, "touch", new Vector2(60, 50))).IsEqualTo(BrowserTouchAction.MoveWithAllTouches);
			await Assert.That(touches.Count).IsEqualTo(1);
			await Assert.That(touches.Update("pointerup", 7, "touch", new Vector2(0, 10))).IsEqualTo(BrowserTouchAction.Forward);
			await Assert.That(touches.Count).IsEqualTo(0);
		}

		[Test]
		public async Task AFingerLeftBehindByThePrimaryIsSilent()
		{
			var touches = new BrowserTouchPoints();
			touches.Update("pointerdown", 1, "touch", new Vector2(10, 10));
			touches.Update("pointerdown", 2, "touch", new Vector2(20, 20));

			// The primary's lift is the mouse up; the finger still down has no button to drag with.
			await Assert.That(touches.Update("pointercancel", 1, "touch", new Vector2(10, 10))).IsEqualTo(BrowserTouchAction.Forward);
			await Assert.That(touches.Update("pointermove", 2, "touch", new Vector2(25, 20))).IsEqualTo(BrowserTouchAction.Swallow);
			await Assert.That(touches.Update("pointerup", 2, "touch", new Vector2(25, 20))).IsEqualTo(BrowserTouchAction.Swallow);
		}
	}
}
