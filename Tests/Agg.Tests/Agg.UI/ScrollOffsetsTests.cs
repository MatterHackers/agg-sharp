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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class ScrollOffsetsTests
	{
		private static ScrollableWidget Scrollable(double viewHeight, double contentHeight)
		{
			var container = new GuiWidget(200, viewHeight);
			var scrollable = new ScrollableWidget(200, viewHeight, autoScroll: true) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
			container.AddChild(scrollable);
			scrollable.AddChild(new GuiWidget(100, contentHeight) { HAnchor = HAnchor.Stretch });
			container.PerformLayout();
			return scrollable;
		}

		[Test]
		public async Task OffsetFromTopStartsAtZeroAndClampsToTheTravel()
		{
			ScrollableWidget scrollable = Scrollable(100, 500);
			await Assert.That(scrollable.MaxScrollFromTop()).IsEqualTo(400);
			await Assert.That(scrollable.ScrollOffsetFromTop()).IsEqualTo(0);

			scrollable.SetScrollOffsetFromTop(150);
			await Assert.That(scrollable.ScrollOffsetFromTop()).IsEqualTo(150);

			// 150 down from the top: the content's top is 150 above the view's.
			await Assert.That(scrollable.ScrollArea.BoundsRelativeToParent.Top - scrollable.LocalBounds.Top).IsEqualTo(150);

			scrollable.SetScrollOffsetFromTop(10_000);
			await Assert.That(scrollable.ScrollOffsetFromTop()).IsEqualTo(400);
			scrollable.SetScrollOffsetFromTop(-5);
			await Assert.That(scrollable.ScrollOffsetFromTop()).IsEqualTo(0);
		}

		[Test]
		public async Task ContentShorterThanTheViewHasNoTravel()
		{
			ScrollableWidget scrollable = Scrollable(300, 100);
			scrollable.SetScrollOffsetFromTop(50);
			await Assert.That(scrollable.MaxScrollFromTop()).IsEqualTo(0);
			await Assert.That(scrollable.ScrollOffsetFromTop()).IsEqualTo(0);
		}

		[Test]
		public async Task TargetForSpanAlignsTopCenterAndBottom()
		{
			// Item 10 of 20-pixel rows in a 100-pixel view.
			await Assert.That(ScrollOffsets.TargetForSpan(200, 20, 100, 0, 1000, ScrollAlignment.Top)).IsEqualTo(200);
			await Assert.That(ScrollOffsets.TargetForSpan(200, 20, 100, 0, 1000, ScrollAlignment.Center)).IsEqualTo(160);
			await Assert.That(ScrollOffsets.TargetForSpan(200, 20, 100, 0, 1000, ScrollAlignment.Bottom)).IsEqualTo(120);

			// Clamped at both ends.
			await Assert.That(ScrollOffsets.TargetForSpan(0, 20, 100, 50, 1000, ScrollAlignment.Center)).IsEqualTo(0);
			await Assert.That(ScrollOffsets.TargetForSpan(990, 20, 100, 0, 900, ScrollAlignment.Top)).IsEqualTo(900);
		}

		// agg-gui's scroll_to.rs tests.
		[Test]
		public async Task BringIntoViewScrollsOnlyAsFarAsAHiddenEdgeNeeds()
		{
			await Assert.That(ScrollOffsets.TargetForSpan(160, 20, 100, 100, 1000, ScrollAlignment.BringIntoView)).IsEqualTo(100);
			await Assert.That(ScrollOffsets.TargetForSpan(40, 20, 100, 100, 1000, ScrollAlignment.BringIntoView)).IsEqualTo(40);
			await Assert.That(ScrollOffsets.TargetForSpan(240, 20, 100, 100, 1000, ScrollAlignment.BringIntoView)).IsEqualTo(160);
		}

		[Test]
		public async Task VisibleRowsAreOnlyThoseTheBandTouches()
		{
			// 1000 rows of 10 under a top at 10,000: a band from 9,955 to 9,975 touches rows 2 to 4.
			await Assert.That(ScrollOffsets.VisibleRows(10_000, 9_955, 9_975, 10, 1000)).IsEqualTo((2, 5));

			// Band above and below the content, an empty band, no rows.
			await Assert.That(ScrollOffsets.VisibleRows(10_000, 10_100, 10_200, 10, 1000)).IsEqualTo((0, 0));
			await Assert.That(ScrollOffsets.VisibleRows(10_000, -500, -100, 10, 1000)).IsEqualTo((1000, 1000));
			await Assert.That(ScrollOffsets.VisibleRows(10_000, 50, 50, 10, 1000)).IsEqualTo((0, 0));
			await Assert.That(ScrollOffsets.VisibleRows(10_000, double.MinValue, double.MaxValue, 10, 1000)).IsEqualTo((0, 1000));
		}
	}
}
