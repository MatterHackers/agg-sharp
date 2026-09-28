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
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// ScrollableWidget's opt-in behaviours: stick to bottom, the horizontal bar and shift+wheel, bar appearance.
	public class ScrollableWidgetOptionsTests
	{
		/// <summary>A 200 x 100 view holding content <paramref name="contentWidth"/> x <paramref name="contentHeight"/>,
		/// parented so the scroll clamp runs.</summary>
		private static (ScrollableWidget Scroll, GuiWidget Content) Make(double contentWidth = 100, double contentHeight = 50)
		{
			var container = new GuiWidget(200, 100);
			// No minimum size, so the tests can shrink the view.
			var scroll = new ScrollableWidget(200, 100, autoScroll: true) { MinimumSize = Vector2.Zero };
			container.AddChild(scroll);
			var content = new GuiWidget(contentWidth, contentHeight, SizeLimitsToSet.None);
			scroll.AddChild(content);
			return (scroll, content);
		}

		[Test]
		public async Task StickToBottomFollowsGrowingContentUntilScrolledAway()
		{
			(ScrollableWidget scroll, GuiWidget content) = Make();
			scroll.StickToBottom = true;

			// Grows past the view: follows the tail.
			content.Height = 300;
			await Assert.That(scroll.MaxScrollFromTop()).IsGreaterThan(0);
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop());
			content.Height = 400;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop());

			// Scrolled up: growth leaves it where the user put it.
			scroll.SetScrollOffsetFromTop(50);
			content.Height = 500;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(50);

			// Back at the bottom: attached again.
			scroll.SetScrollOffsetFromTop(scroll.MaxScrollFromTop());
			content.Height = 600;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop());

			// A resize of the view keeps it at the bottom too.
			scroll.Height = 80;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop());
		}

		[Test]
		public async Task HorizontalScrollIsOffAndAddsNoChildrenByDefault()
		{
			(ScrollableWidget scroll, _) = Make(contentWidth: 600);
			await Assert.That(scroll.HorizontalScroll).IsFalse();
			await Assert.That(scroll.HorizontalScrollBar).IsNull();
			await Assert.That(scroll.Children.Count).IsEqualTo(2);
		}

		[Test]
		public async Task HorizontalScrollShowsABarThatScrollsAndReservesItsHeight()
		{
			(ScrollableWidget scroll, GuiWidget content) = Make(contentWidth: 600, contentHeight: 50);
			scroll.HorizontalScroll = true;
			ScrollBar bar = scroll.HorizontalScrollBar;
			await Assert.That(bar.Visible).IsTrue();
			await Assert.That(bar.Height).IsEqualTo(ScrollBar.ScrollBarWidth);
			await Assert.That(scroll.ScrollArea.DeviceMargin.Bottom).IsEqualTo(ScrollBar.ScrollBarWidth).Within(1e-9);
			await Assert.That(scroll.MaxScrollFromLeft()).IsEqualTo(400);

			// Dragging the thumb the whole free track scrolls to the right end.
			bar.MoveThumb(new Vector2(bar.Width, 0));
			await Assert.That(scroll.ScrollOffsetFromLeft()).IsEqualTo(400);

			// Clicking the track to the left of the thumb pages back towards the start.
			bar.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, bar.Height / 2, 0));
			bar.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 1, bar.Height / 2, 0));
			await Assert.That(scroll.ScrollOffsetFromLeft()).IsLessThan(400);

			// Content that fits hides the bar and gives its room back.
			content.Width = 100;
			await Assert.That(content.TransformToParentSpace(scroll, content.LocalBounds).Left).IsEqualTo(0);
			await Assert.That(bar.Visible).IsFalse();
			await Assert.That(scroll.ScrollArea.DeviceMargin.Bottom).IsEqualTo(0);
		}

		// Holds shift down in the process-wide Keyboard state, which automation tests also drive.
		[Test, NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ShiftWheelScrollsSidewaysOnlyWhenOptedIn()
		{
			(ScrollableWidget scroll, _) = Make(contentWidth: 600, contentHeight: 50);
			Keyboard.SetKeyDownState(Keys.Shift, true);
			try
			{
				scroll.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120));
				await Assert.That(scroll.ScrollOffsetFromLeft()).IsEqualTo(0);

				scroll.HorizontalScroll = true;
				scroll.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120));
				await Assert.That(scroll.ScrollOffsetFromLeft()).IsGreaterThan(0);
			}
			finally
			{
				Keyboard.SetKeyDownState(Keys.Shift, false);
			}
		}

		/// <summary>
		/// A resize keeps the scroll where it was, but it used to do that by restoring the old offset after
		/// BoundsChanged had run - silently undoing a scroll a BoundsChanged handler had just asked for.
		/// </summary>
		[Test]
		public async Task AScrollSetDuringBoundsChangedSurvivesTheResize()
		{
			(ScrollableWidget scroll, _) = Make(contentHeight: 500);
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(0);
			scroll.BoundsChanged += (s, e) => scroll.SetScrollOffsetFromTop(120);
			scroll.Height = 90;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(120);
		}

		/// <summary>
		/// MatterCAD's ThemedHorizontalScrollBar resets only X (ScrollPosition = (left, ScrollPosition.Y)) from
		/// BoundsChanged. That sideways request must not cost the view its top-relative vertical scroll.
		/// </summary>
		[Test]
		public async Task AnXOnlyScrollDuringBoundsChangedKeepsTheOffsetFromTop()
		{
			(ScrollableWidget scroll, _) = Make(contentWidth: 600, contentHeight: 500);
			scroll.SetScrollOffsetFromTop(100);
			scroll.SetScrollOffsetFromLeft(50);
			scroll.BoundsChanged += (s, e) => scroll.ScrollPosition = new Vector2(0, scroll.ScrollPosition.Y);
			scroll.LocalBounds = new RectangleDouble(0, 0, 400, 150);
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(100);
			await Assert.That(scroll.ScrollOffsetFromLeft()).IsEqualTo(0);
		}

		// Holds shift down in the process-wide Keyboard state, which automation tests also drive.
		[Test, NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ShiftWheelLeavesTheEventAloneWhenNothingIsHiddenSideways()
		{
			(ScrollableWidget scroll, _) = Make(contentWidth: 100, contentHeight: 500);
			scroll.HorizontalScroll = true;
			Keyboard.SetKeyDownState(Keys.Shift, true);
			try
			{
				var wheel = new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120);
				scroll.OnMouseWheel(wheel);
				// Not turned sideways, so the ordinary wheel scrolled the view down and took the delta.
				await Assert.That(wheel.WheelDeltaX).IsEqualTo(0);
				await Assert.That(scroll.ScrollOffsetFromTop()).IsGreaterThan(0);
			}
			finally
			{
				Keyboard.SetKeyDownState(Keys.Shift, false);
			}
		}

		[Test]
		public async Task AResizeWithNoScrollRequestKeepsTheOffsetFromTop()
		{
			(ScrollableWidget scroll, _) = Make(contentHeight: 500);
			scroll.SetScrollOffsetFromTop(70);
			scroll.Height = 90;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(70);
		}

		[Test]
		public async Task WithoutStickToBottomGrowingContentKeepsTheTopInView()
		{
			(ScrollableWidget scroll, GuiWidget content) = Make();
			content.Height = 300;
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(0);
		}
	}
}
