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

using System;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The two-finger maths of <see cref="MultiTouchGesture"/>, against agg-gui's touch_state.rs cases.</summary>
	public class MultiTouchGestureTests
	{
		private static MultiTouchInfo? Feed(MultiTouchGesture gesture, params Vector2[] positions)
			=> gesture.Update(new MouseEventArgs(MouseButtons.Left, 0, positions, 0, null));

		[Test]
		public async Task OneFingerIsNoGesture()
		{
			var gesture = new MultiTouchGesture();
			await Assert.That(Feed(gesture, new Vector2(10, 10)).HasValue).IsFalse();
			await Assert.That(gesture.Current.HasValue).IsFalse();
		}

		[Test]
		public async Task TwoFingersLandingIsAZeroedBaseline()
		{
			var gesture = new MultiTouchGesture();
			MultiTouchInfo info = Feed(gesture, new Vector2(100, 100), new Vector2(200, 100)).Value;
			await Assert.That(info.NumTouches).IsEqualTo(2);
			await Assert.That(info.ZoomDelta).IsEqualTo(1.0);
			await Assert.That(info.RotationDelta).IsEqualTo(0.0);
			await Assert.That(info.TranslationDelta).IsEqualTo(Vector2.Zero);
			await Assert.That(info.CenterPosition).IsEqualTo(new Vector2(150, 100));
		}

		[Test]
		public async Task PinchOutReportsTheSpreadRatio()
		{
			var gesture = new MultiTouchGesture();
			Feed(gesture, new Vector2(100, 100), new Vector2(200, 100));
			MultiTouchInfo info = Feed(gesture, new Vector2(50, 100), new Vector2(250, 100)).Value;
			await Assert.That(Math.Abs(info.ZoomDelta - 2)).IsLessThan(1e-9);
			await Assert.That(Math.Abs(info.RotationDelta)).IsLessThan(1e-9);
			await Assert.That(info.TranslationDelta.Length).IsLessThan(1e-9);
		}

		[Test]
		public async Task RotationIsSignedCounterClockwisePositive()
		{
			var gesture = new MultiTouchGesture();
			Feed(gesture, new Vector2(100, 200), new Vector2(100, 0));
			MultiTouchInfo info = Feed(gesture, new Vector2(50, 186.60254), new Vector2(150, 13.39746)).Value;
			await Assert.That(Math.Abs(info.RotationDelta - (Math.PI / 6))).IsLessThan(1e-6);
			await Assert.That(Math.Abs(info.ZoomDelta - 1)).IsLessThan(1e-6);
		}

		[Test]
		public async Task RotationAcrossTheSeamStaysSmall()
		{
			// Just under +180 degrees to just past it is a small turn, not nearly a full one.
			var gesture = new MultiTouchGesture();
			Feed(gesture, new Vector2(0, 1), new Vector2(200, -1));
			MultiTouchInfo info = Feed(gesture, new Vector2(0, -1), new Vector2(200, 1)).Value;
			await Assert.That(Math.Abs(info.RotationDelta)).IsLessThan(0.05);
		}

		[Test]
		public async Task ATwoFingerDragTranslatesByTheCentreMove()
		{
			var gesture = new MultiTouchGesture();
			Feed(gesture, new Vector2(100, 100), new Vector2(200, 100));
			MultiTouchInfo info = Feed(gesture, new Vector2(110, 95), new Vector2(210, 95)).Value;
			await Assert.That(info.TranslationDelta).IsEqualTo(new Vector2(10, -5));
			await Assert.That(Math.Abs(info.ZoomDelta - 1)).IsLessThan(1e-9);
			await Assert.That(info.CenterPosition).IsEqualTo(new Vector2(160, 95));
		}

		[Test]
		public async Task RoutingToAChildMovesEveryFingerNotJustTheFirst()
		{
			// GuiWidget hands a child new MouseEventArgs(parentEvent, childX, childY); the other fingers must
			// come along into the child's space or a nested pinch measures against a finger in the wrong place.
			var parentEvent = new MouseEventArgs(MouseButtons.Left, 0, new[] { new Vector2(110, 120), new Vector2(210, 220) }, 0, null);
			var childEvent = new MouseEventArgs(parentEvent, 10, 20);
			await Assert.That(childEvent.GetPosition(0)).IsEqualTo(new Vector2(10, 20));
			await Assert.That(childEvent.GetPosition(1)).IsEqualTo(new Vector2(110, 120));
		}

		[Test]
		public async Task AFingerCountChangeRebaselines()
		{
			var gesture = new MultiTouchGesture();
			Feed(gesture, new Vector2(100, 100), new Vector2(200, 100));
			MultiTouchInfo info = Feed(gesture, new Vector2(0, 0), new Vector2(300, 100), new Vector2(150, 300)).Value;
			await Assert.That(info.NumTouches).IsEqualTo(3);
			await Assert.That(info.ZoomDelta).IsEqualTo(1.0);
			await Assert.That(info.TranslationDelta).IsEqualTo(Vector2.Zero);

			// Back to one finger ends the gesture; two again is a fresh baseline.
			await Assert.That(Feed(gesture, new Vector2(0, 0)).HasValue).IsFalse();
			info = Feed(gesture, new Vector2(10, 10), new Vector2(90, 10)).Value;
			await Assert.That(info.TranslationDelta).IsEqualTo(Vector2.Zero);
		}
	}
}
