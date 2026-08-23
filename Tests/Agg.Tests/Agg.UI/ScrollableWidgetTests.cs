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

using Agg.Tests.Agg;
using TUnit.Assertions;
using TUnit.Core;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using System.Threading.Tasks;

namespace MatterHackers.Agg.UI.Tests
{
    
    public class ScrollableWidgetTests
	{
		public static bool saveImagesForDebug = false;

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(control.BackBuffer, "image-control.tga");
				ImageTgaIO.Save(test.BackBuffer, "image-test.tga");
			}
		}

        [Test]
        public async Task LimitScrolToContetsTests()
		{
			GuiWidget containerControl = new GuiWidget(200, 200);
			containerControl.DoubleBuffer = true;
			containerControl.BackBuffer.NewGraphics2D().Clear(Color.White);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			ScrollableWidget containerTest = new ScrollableWidget(200, 200);
			containerTest.DoubleBuffer = true;
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);
			containerTest.OnDraw(containerTest.NewGraphics2D());

			OutputImages(containerControl, containerTest);

			await Assert.That(containerControl.BackBuffer != null).IsTrue();
			await Assert.That(containerControl.BackBuffer == containerTest.BackBuffer).IsTrue();
		}

		/// <summary>
		/// Turning the bar back on has to re-decide whether it is needed right then. It used to wait for the next
		/// bounds change, so a host that sized its content before flipping the mode (the popped out sheet editor)
		/// got a permanently hidden bar over content that did not fit.
		/// </summary>
		[Test]
		public async Task ShowStateChangeReevaluatesVisibilityWithNoLayout()
		{
			var scrollable = MakeScrollable(out var container);
			scrollable.AddChild(new GuiWidget(100, 500));
			container.PerformLayout();

			scrollable.VerticalScrollBar.Show = ScrollBar.ShowState.Never;

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsFalse()
				.Because("Never means never, however tall the content is");

			scrollable.VerticalScrollBar.Show = ScrollBar.ShowState.WhenRequired;

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsTrue()
				.Because($"{scrollable.ScrollArea.Height} of content does not fit {scrollable.Height},"
					+ " so the bar is required the moment the mode allows it - without waiting for a layout");
		}

		/// <summary>
		/// The view shrinking under content that used to fit is the other way a bar becomes required.
		/// </summary>
		[Test]
		public async Task ShrinkingTheViewShowsTheBarAndGrowingItBackHidesIt()
		{
			var scrollable = MakeScrollable(out var container);
			scrollable.AddChild(new GuiWidget(100, 150));
			container.PerformLayout();

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsFalse()
				.Because("150 of content fits the 200 tall view");

			scrollable.Height = 100;
			container.PerformLayout();

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsTrue()
				.Because("the same content no longer fits the 100 the view is now");

			scrollable.Height = 200;
			container.PerformLayout();

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsFalse()
				.Because("and it fits again, so the bar has nothing to scroll to");
		}

		/// <summary>
		/// Content going away has to hide the bar. The scrolling area only recalculated its bounds when a child was
		/// added, so a removed child left the area as tall as the content that was no longer there.
		/// </summary>
		[Test]
		public async Task RemovingTheTallContentHidesTheBar()
		{
			var scrollable = MakeScrollable(out var container);
			var tallContent = new GuiWidget(100, 500);
			scrollable.AddChild(tallContent);
			container.PerformLayout();

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsTrue()
				.Because("500 of content in a 200 tall view needs a bar");

			scrollable.ScrollArea.RemoveChild(tallContent);
			container.PerformLayout();

			await Assert.That(scrollable.ScrollArea.Height).IsLessThanOrEqualTo(scrollable.Height)
				.Because("the scrolling area cannot stay as tall as content that has been taken out of it");

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsFalse()
				.Because("there is nothing left to scroll to");
		}

		/// <summary>
		/// The bar's own height has to track the view it scrolls. It was only re-sized when the bar decided it was
		/// showing, so a view that changed height while the bar was off (the sheet editor docked back into the
		/// properties panel) came back with a track shorter than the content it was supposed to reach.
		/// </summary>
		[Test]
		public async Task BarHeightFollowsTheViewAcrossAHiddenPhase()
		{
			var scrollable = MakeScrollable(out var container);
			scrollable.AddChild(new GuiWidget(100, 500));
			container.PerformLayout();

			await Assert.That(scrollable.VerticalScrollBar.Height).IsEqualTo(scrollable.Height)
				.Because("the track spans the view it scrolls");

			scrollable.VerticalScrollBar.Show = ScrollBar.ShowState.Never;

			scrollable.Height = 120;
			container.PerformLayout();

			scrollable.VerticalScrollBar.Show = ScrollBar.ShowState.WhenRequired;

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsTrue()
				.Because("500 of content still does not fit the 120 tall view");

			await Assert.That(scrollable.VerticalScrollBar.Height).IsEqualTo(scrollable.Height)
				.Because("a track sized against the height the view used to be cannot reach the bottom of the content");
		}

		/// <summary>
		/// The same thing without the hidden phase: a visible bar resizes with its view.
		/// </summary>
		[Test]
		public async Task VisibleBarHeightFollowsTheView()
		{
			var scrollable = MakeScrollable(out var container);
			scrollable.AddChild(new GuiWidget(100, 500));
			container.PerformLayout();

			scrollable.Height = 90;
			container.PerformLayout();

			await Assert.That(scrollable.VerticalScrollBar.Visible).IsTrue();

			await Assert.That(scrollable.VerticalScrollBar.Height).IsEqualTo(scrollable.Height)
				.Because("the track follows the view it scrolls");
		}

		/// <summary>
		/// Fit means "as tall as the content", not "as tall as everything my children currently cover". The scrolling
		/// area is deliberately displaced by the scroll position, so fitting around it while the view showed the top of
		/// taller content produced a rect hanging below the origin (B:-300 T:200). A later Height assignment only moves
		/// Top, so Bottom stayed negative for good, and the scroll bar - which is laid out at the local origin - was
		/// then drawn a whole scroll offset away from the view it belonged to.
		/// </summary>
		[Test]
		public async Task FitAroundScrolledContentStaysAnchoredAtTheOrigin()
		{
			var scrollable = MakeScrollable(out var container);
			scrollable.AddChild(new GuiWidget(100, 500));
			container.PerformLayout();

			// show the top of the content, which puts the scrolling area below our own origin
			scrollable.TopLeftOffset = Vector2.Zero;
			await Assert.That(scrollable.ScrollArea.BoundsRelativeToParent.Bottom).IsLessThan(0)
				.Because("the test only means anything while the content is scrolled off the bottom");

			scrollable.VAnchor = VAnchor.Fit;
			container.PerformLayout();

			await Assert.That(scrollable.LocalBounds.Bottom).IsEqualTo(0)
				.Because($"a fitted view is measured from its own origin, not from where the content scrolled to"
					+ $" (got {scrollable.LocalBounds})");

			await Assert.That(scrollable.Height).IsEqualTo(500)
				.Because("fit is as tall as the content it holds");

			// the host then pins the height, the way the docked sheet editor does
			scrollable.Height = 300;
			container.PerformLayout();

			await Assert.That(scrollable.LocalBounds.Bottom).IsEqualTo(0)
				.Because($"assigning a height moves Top, so a bottom left negative by the fit never comes back"
					+ $" (got {scrollable.LocalBounds})");

			await Assert.That(scrollable.VerticalScrollBar.BoundsRelativeToParent.Bottom).IsEqualTo(0)
				.Because($"the track is laid out at the local origin and has to land on the view"
					+ $" (got {scrollable.VerticalScrollBar.BoundsRelativeToParent})");
		}

		private static ScrollableWidget MakeScrollable(out GuiWidget container)
		{
			container = new GuiWidget(400, 400);

			var scrollable = new ScrollableWidget(200, 200, autoScroll: true);

			// the size passed to the constructor is also a minimum, and these tests resize the view
			scrollable.MinimumSize = Vector2.Zero;

			container.AddChild(scrollable);

			return scrollable;
		}
	}
}
