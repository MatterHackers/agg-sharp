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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// RectAlign placement and AnchoredPopup's flip, clamp and close behavior, after agg-gui's popup/align.rs and
	// popup/behavior.rs tests. Y-up: the anchor's bottom is 100, its top 120.
	public class AnchoredPopupTests
	{
		private static readonly RectangleDouble Parent = new RectangleDouble(100, 100, 140, 120);
		private static readonly Vector2 Size = new Vector2(60, 30);
		private const double Gap = 4;

		[Test]
		public async Task PresetsPlaceAgainstTheNamedSide()
		{
			// BottomStart hangs below, left aligned.
			RectangleDouble below = RectAlign.BottomStart.PlaceChild(Parent, Size, Gap);
			await Assert.That(below).IsEqualTo(new RectangleDouble(100, 66, 160, 96));

			// Top is centred above.
			RectangleDouble above = RectAlign.Top.PlaceChild(Parent, Size, Gap);
			await Assert.That(above.Bottom).IsEqualTo(Parent.Top + Gap);
			await Assert.That(above.Center.X).IsEqualTo(Parent.Center.X);

			// Right and Left are centred beside it.
			RectangleDouble right = RectAlign.Right.PlaceChild(Parent, Size, Gap);
			await Assert.That(right.Left).IsEqualTo(Parent.Right + Gap);
			await Assert.That(right.Center.Y).IsEqualTo(Parent.Center.Y);
			RectangleDouble left = RectAlign.Left.PlaceChild(Parent, Size, Gap);
			await Assert.That(left.Right).IsEqualTo(Parent.Left - Gap);
		}

		[Test]
		public async Task FlipsMirrorTheNamedPresetsAndLabelsRoundTrip()
		{
			await Assert.That(RectAlign.BottomStart.FlipY()).IsEqualTo(RectAlign.TopStart);
			await Assert.That(RectAlign.Left.FlipX()).IsEqualTo(RectAlign.Right);
			await Assert.That(RectAlign.BottomStart.Flip()).IsEqualTo(RectAlign.TopEnd);
			await Assert.That(RectAlign.Bottom.PresetLabel).IsEqualTo("BOTTOM");
			await Assert.That(new RectAlign(Align2.Center, Align2.Center).PresetIndex).IsEqualTo(-1);
			for (int i = 0; i < Align2.All.Count; i++)
			{
				await Assert.That(Align2.All[i].Align.AllIndex).IsEqualTo(i);
			}
		}

		[Test]
		public async Task OverflowFlipsToAMirrorImageThenClamps()
		{
			// Anchored near the bottom of a 400 x 300 viewport, BottomStart does not fit, so it flips above.
			var popup = new AnchoredPopup
			{
				Anchor = new RectangleDouble(100, 10, 140, 30),
				Size = Size,
				Gap = Gap,
				Align = RectAlign.BottomStart,
			};
			var viewport = new Vector2(400, 300);
			await Assert.That(popup.EffectiveAlign(viewport)).IsEqualTo(RectAlign.TopStart);
			await Assert.That(popup.Rect(viewport).Bottom).IsEqualTo(34);

			// Bigger than the viewport nothing fits; it is clamped in from the margin.
			popup.Size = new Vector2(500, 30);
			await Assert.That(popup.Rect(viewport).Left).IsEqualTo(RectAlign.ViewportMargin);
		}

		[Test]
		public async Task CloseBehaviorsDecideWhichClicksClose()
		{
			var viewport = new Vector2(400, 300);
			var inside = new Vector2(110, 80);
			var outside = new Vector2(300, 250);

			var popup = new AnchoredPopup { Anchor = Parent, Size = Size, IsOpen = true };
			PopupClickOutcome click = popup.OnMouseDown(inside, viewport);
			await Assert.That(click.Inside).IsTrue();
			await Assert.That(click.Closed).IsTrue();

			popup = new AnchoredPopup { Anchor = Parent, Size = Size, IsOpen = true, CloseBehavior = PopupCloseBehavior.CloseOnClickOutside };
			await Assert.That(popup.OnMouseDown(inside, viewport).Closed).IsFalse();
			await Assert.That(popup.IsOpen).IsTrue();
			click = popup.OnMouseDown(outside, viewport);
			await Assert.That(click.Closed).IsTrue();
			await Assert.That(click.Consumed).IsTrue();

			popup = new AnchoredPopup { Anchor = Parent, Size = Size, IsOpen = true, CloseBehavior = PopupCloseBehavior.IgnoreClicks };
			click = popup.OnMouseDown(outside, viewport);
			await Assert.That(click.Consumed).IsFalse();
			await Assert.That(popup.IsOpen).IsTrue();

			// Escape closes even an IgnoreClicks popup, once.
			await Assert.That(popup.OnEscape()).IsTrue();
			await Assert.That(popup.OnEscape()).IsFalse();
		}
	}
}
