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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The wheel goes to the topmost widget under the pointer and bubbles up through its ancestors - never sideways
	/// to a sibling that is covered at that point. A window lying over a scrolling window must not scroll the one
	/// behind it.
	/// </summary>
	public class WheelOcclusionRoutingTests
	{
		// wheel turned towards the user - scrolls a view whose content runs off the bottom
		private const int WheelDown = -120;

		[Test]
		public async Task AWindowInFrontKeepsTheWheelFromTheScrollViewBehindIt()
		{
			var (canvas, back) = BackScrollerUnder(new GuiWidget(100, 100));
			var start = back.ScrollPosition;

			// (120, 120) is inside both; the front one is on top there
			canvas.OnMouseWheel(Wheel(120, 120));

			await Assert.That(back.ScrollPosition).IsEqualTo(start);
		}

		[Test]
		public async Task TheScrollViewBehindScrollsWhereNothingCoversIt()
		{
			var (canvas, back) = BackScrollerUnder(new GuiWidget(100, 100));
			var start = back.ScrollPosition;

			// (50, 50) is only over the back one
			canvas.OnMouseWheel(Wheel(50, 50));

			await Assert.That(back.ScrollPosition).IsNotEqualTo(start);
		}

		[Test]
		public async Task AFrontScrollViewAtItsLimitStillKeepsTheWheelFromItsSibling()
		{
			// The front view has nothing more to show in the wheel's direction, so it leaves the delta for its
			// ancestors - but the sibling it covers is not an ancestor and must not take it.
			var front = new ScrollableWidget(100, 100, autoScroll: true);
			front.AddChild(new GuiWidget(80, 400));
			var (canvas, back) = BackScrollerUnder(front);

			front.ScrollPosition = new Vector2(0, 100000);
			var frontAtEnd = front.ScrollPosition;
			var backStart = back.ScrollPosition;

			canvas.OnMouseWheel(Wheel(120, 120));

			await Assert.That(front.ScrollPosition).IsEqualTo(frontAtEnd);
			await Assert.That(back.ScrollPosition).IsEqualTo(backStart);
		}

		[Test]
		public async Task AWidgetThatDeclinesThePointLetsTheWheelThrough()
		{
			// A widget that declines the point through PositionWithinLocalBounds - as a fading-out demo window does -
			// is not in the way.
			var (canvas, back) = BackScrollerUnder(new PointerTransparentWidget(100, 100));
			var start = back.ScrollPosition;

			canvas.OnMouseWheel(Wheel(120, 120));

			await Assert.That(back.ScrollPosition).IsNotEqualTo(start);
		}

		[Test]
		public async Task ANonSelectableOverlayDoesNotBlockTheWheel()
		{
			// Click-through overlays (Selectable = false, like a scroll view's edge fade) are skipped by a mouse down,
			// and the wheel treats them the same way.
			var (canvas, back) = BackScrollerUnder(new GuiWidget(100, 100) { Selectable = false });
			var start = back.ScrollPosition;

			canvas.OnMouseWheel(Wheel(120, 120));

			await Assert.That(back.ScrollPosition).IsNotEqualTo(start);
		}

		[Test]
		public async Task ANestedScrollViewAtItsLimitBubblesToTheOuterOne()
		{
			var outer = new ScrollableWidget(200, 200, autoScroll: true);
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom);
			var inner = new ScrollableWidget(150, 100, autoScroll: true);
			inner.AddChild(new GuiWidget(100, 400));
			column.AddChild(inner);
			column.AddChild(new GuiWidget(150, 600));
			outer.AddChild(column);
			outer.PerformLayout();

			inner.ScrollPosition = new Vector2(0, 100000);
			var innerAtEnd = inner.ScrollPosition;
			var outerStart = outer.ScrollPosition;

			// the pointer over the inner view, wherever layout put it
			var innerCenter = inner.TransformToParentSpace(outer, inner.LocalBounds.Center);
			outer.OnMouseWheel(Wheel(innerCenter.X, innerCenter.Y));

			await Assert.That(inner.ScrollPosition).IsEqualTo(innerAtEnd);
			await Assert.That(outer.ScrollPosition).IsNotEqualTo(outerStart);
		}

		/// <summary>
		/// A 200 x 200 canvas holding a scroll view at (0, 0) - (150, 150) with content to scroll, and
		/// <paramref name="front"/> added after it (so on top) at (100, 100).
		/// </summary>
		private static (GuiWidget canvas, ScrollableWidget back) BackScrollerUnder(GuiWidget front)
		{
			var canvas = new GuiWidget(200, 200);

			var back = new ScrollableWidget(150, 150, autoScroll: true);
			back.AddChild(new GuiWidget(130, 600));
			canvas.AddChild(back);

			front.OriginRelativeParent = new Vector2(100, 100);
			canvas.AddChild(front);

			canvas.PerformLayout();
			return (canvas, back);
		}

		private static MouseEventArgs Wheel(double x, double y) => new MouseEventArgs(MouseButtons.None, 0, x, y, WheelDown);

		private class PointerTransparentWidget : GuiWidget
		{
			public PointerTransparentWidget(double width, double height)
				: base(width, height)
			{
			}

			public override bool PositionWithinLocalBounds(double x, double y) => false;
		}
	}
}
