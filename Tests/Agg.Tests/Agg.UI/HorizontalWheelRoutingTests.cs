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
	/// A two finger trackpad scroll carries a horizontal component as well as a vertical one, and agg
	/// delivers it as <see cref="MouseEventArgs.WheelDeltaX"/> alongside the wheel. These cover the two
	/// things that have to be true for a widget deep in the tree to ever see it - the per-child clone has
	/// to carry it down, and consuming it has to carry back up - and then what
	/// <see cref="ScrollableWidget"/>, which is where the gesture normally lands, does with it.
	/// </summary>
	public class HorizontalWheelRoutingTests
	{
		[Test]
		public async Task TheClonedEventKeepsBothWheelAxes()
		{
			var original = new MouseEventArgs(MouseButtons.None, 0, 10, 20, 120)
			{
				WheelDeltaX = -45,
			};

			// This is the constructor GuiWidget uses to re-base an event into a child's coordinates; a field
			// it forgets is a field no child can ever act on.
			var moved = new MouseEventArgs(original, 3, 4);

			await Assert.That(moved.WheelDelta).IsEqualTo(120);
			await Assert.That(moved.WheelDeltaX).IsEqualTo(-45);
		}

		[Test]
		public async Task AChildSeesTheHorizontalWheelAndCanConsumeIt()
		{
			var parent = new GuiWidget(200, 200);
			var child = new HorizontalWheelEater()
			{
				LocalBounds = new RectangleDouble(0, 0, 100, 100),
			};
			child.OriginRelativeParent = new VectorMath.Vector2(50, 50);
			parent.AddChild(child);

			var wheelEvent = new MouseEventArgs(MouseButtons.None, 0, 75, 75, 120)
			{
				WheelDeltaX = -30,
			};

			parent.OnMouseWheel(wheelEvent);

			await Assert.That(child.SeenWheelDeltaX).IsEqualTo(-30);

			// The child ate the horizontal component, so the parent (a scroll panel, in the real case) has to
			// be told - otherwise the same gesture would be acted on twice.
			await Assert.That(wheelEvent.WheelDeltaX).IsEqualTo(0);
			await Assert.That(wheelEvent.WheelDelta).IsEqualTo(120);
		}

		[Test]
		public async Task AScrollPanelWiderThanItsViewScrollsSidewaysAndTakesTheDelta()
		{
			var scrollable = WideScrollable();

			var wheelEvent = SidewaysScroll(-30);
			scrollable.OnMouseWheel(wheelEvent);

			// -30 wheel units is 6 pixels of finger travel (the same 5-per-pixel packing the wheel is read with),
			// and negative means the content moves left.
			await Assert.That(scrollable.ScrollPosition.X).IsEqualTo(-6 * GuiWidget.DeviceScale).Within(0.001);

			// it scrolled, so nothing above may scroll on the same gesture
			await Assert.That(wheelEvent.WheelDeltaX).IsEqualTo(0);
		}

		[Test]
		public async Task TheContentFollowsTheFingers()
		{
			// The sign is the whole feature: ScrollPosition.X is where the content sits, so a bigger X is content
			// further right. AppKit's positive scrollingDeltaX is fingers moving right, and the content goes with
			// them - which is why negative (fingers left) has to reveal what was off the right hand edge.
			var scrollable = WideScrollable();

			scrollable.OnMouseWheel(SidewaysScroll(-50));
			var afterFingersLeft = scrollable.ScrollPosition.X;
			await Assert.That(afterFingersLeft).IsLessThan(0);

			scrollable.OnMouseWheel(SidewaysScroll(20));
			await Assert.That(scrollable.ScrollPosition.X).IsGreaterThan(afterFingersLeft);
		}

		[Test]
		public async Task TheContentStopsAtEitherEnd()
		{
			var scrollable = WideScrollable();

			// far past the right hand end
			scrollable.OnMouseWheel(SidewaysScroll(-100000));
			var atTheEnd = scrollable.ScrollPosition.X;

			// the content's right edge has come in to the view's, and no further
			await Assert.That(atTheEnd).IsLessThan(0);
			await Assert.That(scrollable.ScrollArea.BoundsRelativeToParent.Right)
				.IsGreaterThanOrEqualTo(scrollable.LocalBounds.Right - scrollable.ScrollArea.Margin.Right - 0.001);

			// and having run out, the gesture is left for an ancestor rather than swallowed
			var pastTheEnd = SidewaysScroll(-100000);
			scrollable.OnMouseWheel(pastTheEnd);
			await Assert.That(scrollable.ScrollPosition.X).IsEqualTo(atTheEnd).Within(0.001);
			await Assert.That(pastTheEnd.WheelDeltaX).IsEqualTo(-100000);

			// back the other way stops at the start
			scrollable.OnMouseWheel(SidewaysScroll(100000));
			await Assert.That(scrollable.ScrollPosition.X).IsEqualTo(0).Within(0.001);
		}

		[Test]
		public async Task APanelWithNothingHiddenSidewaysLeavesTheDeltaAlone()
		{
			// Nothing is clipped off the sides, so this panel cannot use the gesture - and must not eat it, or a
			// scroll panel outside it that could would never see it.
			var scrollable = new ScrollableWidget(200, 200, autoScroll: true);
			scrollable.AddChild(new GuiWidget(100, 400));
			scrollable.PerformLayout();

			var startingScroll = scrollable.ScrollPosition;

			var wheelEvent = SidewaysScroll(-30);
			scrollable.OnMouseWheel(wheelEvent);

			await Assert.That(scrollable.ScrollPosition.X).IsEqualTo(startingScroll.X).Within(0.001);
			await Assert.That(wheelEvent.WheelDeltaX).IsEqualTo(-30);
		}

		[Test]
		public async Task AnInnerPanelThatOnlyScrollsUpAndDownHandsTheGestureOut()
		{
			// This is the shape the path editor sits in: a tall inner panel inside a wide outer one. The wheel is
			// over the inner panel, which has plenty to scroll vertically and nothing sideways, so the sideways
			// component has to survive it and reach the panel that can act on it.
			var outer = new ScrollableWidget(200, 200, autoScroll: true);
			var inner = new ScrollableWidget(400, 200, autoScroll: true);

			// Stretch rather than the default Fit, which is how a properties panel is built: the content is held
			// to the width left over once the vertical bar has its gutter, so nothing of it is ever off the sides.
			inner.ScrollArea.HAnchor = HAnchor.Stretch;
			inner.AddChild(new GuiWidget(400, 800) { HAnchor = HAnchor.Stretch });
			outer.AddChild(inner);
			outer.PerformLayout();

			var wheelEvent = SidewaysScroll(-30);
			outer.OnMouseWheel(wheelEvent);

			await Assert.That(inner.ScrollPosition.X).IsEqualTo(0).Within(0.001);
			await Assert.That(outer.ScrollPosition.X).IsEqualTo(-6 * GuiWidget.DeviceScale).Within(0.001);
			await Assert.That(wheelEvent.WheelDeltaX).IsEqualTo(0);
		}

		/// <summary>
		/// A panel whose content is twice as wide as the view, so there is always something off the right to
		/// scroll to.
		/// </summary>
		private static ScrollableWidget WideScrollable()
		{
			var scrollable = new ScrollableWidget(200, 200, autoScroll: true);
			scrollable.AddChild(new GuiWidget(400, 100));
			scrollable.PerformLayout();

			return scrollable;
		}

		private static MouseEventArgs SidewaysScroll(int wheelDeltaX)
		{
			return new MouseEventArgs(MouseButtons.None, 0, 100, 100, 0)
			{
				WheelDeltaX = wheelDeltaX,
			};
		}

		private class HorizontalWheelEater : GuiWidget
		{
			public int SeenWheelDeltaX { get; private set; }

			public override void OnMouseWheel(MouseEventArgs mouseEvent)
			{
				this.SeenWheelDeltaX = mouseEvent.WheelDeltaX;
				mouseEvent.WheelDeltaX = 0;

				base.OnMouseWheel(mouseEvent);
			}
		}
	}
}
