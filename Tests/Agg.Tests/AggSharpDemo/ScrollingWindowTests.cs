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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout.Scrolling;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Scrolling window (agg-gui's demo-ui/src/windows/scrolling/).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class ScrollingWindowTests
	{
		private static DemoSpec ScrollingSpec => GuiDemoSpecs.All.First(s => s.Title == "Scrolling");

		private static (GuiWidget Page, ScrollingWindow Window) Build()
		{
			var page = new GuiWidget(680 * GuiWidget.DeviceScale, 540 * GuiWidget.DeviceScale);
			GuiWidget content = GuiDemoSpecs.CreateContent(ScrollingSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (page, (ScrollingWindow)content);
		}

		private static void Draw(GuiWidget page)
		{
			var image = new ImageBuffer((int)page.Width, (int)page.Height);
			page.OnDraw(image.NewGraphics2D());
		}

		/// <summary>Presses the tab labelled <paramref name="label"/>, the way a click does.</summary>
		private static void PressTab(ScrollingWindow window, string label)
		{
			GuiWidget tab = window.FindDescendant(ScrollingWindow.TabName(label));
			tab.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, tab.Width / 2, tab.Height / 2, 0));
			tab.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, tab.Width / 2, tab.Height / 2, 0));
		}

		[Test]
		public async Task BuildsSixTabsAndEveryNamedControl()
		{
			(_, ScrollingWindow window) = Build();
			await Assert.That(window.Name).IsEqualTo("Scrolling Content");
			await Assert.That(window.Tabs.Tabs.Count).IsEqualTo(6);
			foreach (string label in ScrollingWindow.TabLabels)
			{
				await Assert.That(window.FindDescendant(ScrollingWindow.TabName(label))).IsNotNull().Because(label);
			}

			foreach (string name in new[]
			{
				"Scrolling Tabs", "Scrolling Track Item", "Scrolling Align", "Scrolling Offset", "Scrolling To Top", "Scrolling To Bottom",
				"Scrolling By Amount", "Scrolling By Down", "Scrolling By Up", "Scrolling Scroll To List",
				"Scrolling Row Count", "Scrolling Many Lines List", "Scrolling Large Canvas", "Scrolling Stick To End List", "Scrolling Bidirectional",
			})
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull().Because(name);
			}

			// Opens on Appearance, as agg-gui's does.
			await Assert.That(window.Tabs.SelectedIndex).IsEqualTo(0);
		}

		[Test]
		public async Task ScrollToTabAlignsTheTrackedItemAndScrollsByButtons()
		{
			(GuiWidget page, ScrollingWindow window) = Build();
			PressTab(window, "Scroll to");
			page.PerformLayout();
			ScrollToTab tab = window.ScrollTo;
			ScrollableWidget scroll = tab.Scroll;
			double s = GuiWidget.DeviceScale;
			await Assert.That(tab.Visible).IsTrue();
			await Assert.That(scroll.Height).IsGreaterThan(100 * s);

			// Opens with item 25 centred.
			double CentreOfItem(int item) => (item - 1 + .5) * ScrollToTab.RowHeight * s - scroll.ScrollOffsetFromTop();
			await Assert.That(Math.Abs(CentreOfItem(25) - scroll.Height / 2)).IsLessThan(1);
			await Assert.That(tab.List.Highlight).IsEqualTo(24);

			// Tracking item 250 centres it; aligning to Top puts it at the top.
			tab.TrackItem.Value = 250;
			await Assert.That(Math.Abs(CentreOfItem(250) - scroll.Height / 2)).IsLessThan(1);
			await Assert.That(tab.List.Highlight).IsEqualTo(249);
			tab.Align.SelectedIndex = (int)ScrollAlignment.Top;
			await Assert.That(Math.Abs(scroll.ScrollOffsetFromTop() - 249 * ScrollToTab.RowHeight * s)).IsLessThan(1);

			((ThemedTextButton)window.FindDescendant("Scrolling To Bottom")).InvokeClick();
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop());
			await Assert.That(tab.ReadoutText).IsEqualTo($"Scroll offset: {scroll.MaxScrollFromTop() / s:0} / {scroll.MaxScrollFromTop() / s:0} px");

			((ThemedTextButton)window.FindDescendant("Scrolling To Top")).InvokeClick();
			window.FindDescendant("Scrolling By Down").InvokeClick();
			await Assert.That(tab.ScrollOffset).IsEqualTo(64).Within(1e-6);
			window.FindDescendant("Scrolling By Up").InvokeClick();
			await Assert.That(tab.ScrollOffset).IsEqualTo(0).Within(1e-6);

			tab.Offset.Value = 300;
			await Assert.That(tab.ScrollOffset).IsEqualTo(300).Within(1e-6);
		}

		[Test]
		public async Task ManyLinesDrawsOnlyTheRowsOnScreen()
		{
			(GuiWidget page, ScrollingWindow window) = Build();
			PressTab(window, "Scroll a lot of lines");
			page.PerformLayout();
			ManyLinesTab tab = window.ManyLines;
			await Assert.That(tab.Visible).IsTrue();

			tab.RowCount.Value = 100_000;
			await Assert.That(tab.List.RowCount).IsEqualTo((int)Math.Round(tab.RowCount.Value)).And.IsGreaterThan(99_000);
			tab.Scroll.SetScrollOffsetFromTop(tab.Scroll.MaxScrollFromTop() / 2);
			Draw(page);

			(int first, int end) = tab.List.LastDrawnRows;
			await Assert.That(first).IsGreaterThan(49_000).And.IsLessThan(51_000);
			await Assert.That(end - first).IsGreaterThan(0).And.IsLessThan(40);
		}

		[Test]
		public async Task LargeCanvasScrollsBothWaysAndDrawsOnlyTheRowsOnScreen()
		{
			(GuiWidget page, ScrollingWindow window) = Build();
			PressTab(window, "Scroll a large canvas");
			page.PerformLayout();
			LargeCanvasTab tab = window.LargeCanvas;
			double s = GuiWidget.DeviceScale;
			await Assert.That(tab.Visible).IsTrue();

			tab.Scroll.SetScrollOffsetFromTop(5_000 * LargeCanvasTab.LargeCanvas.RowHeight * s);
			Draw(page);
			(int first, int end) = tab.Canvas.LastDrawnRows;
			// The clip is widened to whole pixels, so the row above may just be touched.
			await Assert.That(first).IsBetween(4_999, 5_000);
			await Assert.That(end - first).IsGreaterThan(0).And.IsLessThan(40);

			// The canvas is wider than the view, so a sideways swipe scrolls it.
			double left = tab.Scroll.ScrollPosition.X;
			var swipe = new MouseEventArgs(MouseButtons.None, 0, 50, 50, 0) { WheelDeltaX = -100 };
			tab.Scroll.OnMouseWheel(swipe);
			await Assert.That(tab.Scroll.ScrollPosition.X).IsLessThan(left);
		}

		// Holds shift down in the process-wide Keyboard state, which automation tests also drive.
		[Test, NotInParallel(new[] { SharedStateKeys.UiThreadAndKeyboard, SharedStateKeys.ThemeConfigCurrent })]
		public async Task BidirectionalScrollsBothWaysWithBarsAndShiftWheel()
		{
			(GuiWidget page, ScrollingWindow window) = Build();
			PressTab(window, "Bidirectional");
			page.PerformLayout();
			BidirectionalTab tab = window.Bidirectional;
			ScrollableWidget scroll = tab.Scroll;
			await Assert.That(tab.Visible).IsTrue();
			await Assert.That(scroll.HorizontalScroll).IsTrue();
			await Assert.That(scroll.VerticalScrollBar.Visible).IsTrue();
			await Assert.That(scroll.HorizontalScrollBar.Visible).IsTrue();
			await Assert.That(scroll.MaxScrollFromTop()).IsGreaterThan(0);
			await Assert.That(scroll.MaxScrollFromLeft()).IsGreaterThan(0);

			// The wheel scrolls down; with shift held it scrolls right instead.
			scroll.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120));
			await Assert.That(scroll.ScrollOffsetFromTop()).IsGreaterThan(0);
			double down = scroll.ScrollOffsetFromTop();
			Keyboard.SetKeyDownState(Keys.Shift, true);
			try
			{
				scroll.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120));
			}
			finally
			{
				Keyboard.SetKeyDownState(Keys.Shift, false);
			}

			await Assert.That(scroll.ScrollOffsetFromLeft()).IsGreaterThan(0);
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(down);
			Draw(page);
		}

		// Drains UiThread's process-wide idle queue by hand, so it needs UiThread's key as well as the class's:
		// unkeyed, that drain ran other tests' queued work (SwitchToUiThreadTests' parked continuation) and a
		// concurrent ResetForTests could clear the row this test's draw queued.
		[Test, NotInParallel(new[] { SharedStateKeys.UiThreadAndKeyboard, SharedStateKeys.ThemeConfigCurrent })]
		public async Task StickToEndFollowsNewRowsUntilScrolledUp()
		{
			(GuiWidget page, ScrollingWindow window) = Build();
			PressTab(window, "Stick to end");
			page.PerformLayout();
			StickToEndTab tab = window.StickToEnd;
			ScrollableWidget scroll = tab.Scroll;
			await Assert.That(tab.Visible).IsTrue();
			await Assert.That(scroll.StickToBottom).IsTrue();

			// A frame's draw asks for a row; the idle it queued adds it.
			int before = tab.List.RowCount;
			Draw(page);
			UiThread.InvokePendingActions();
			await Assert.That(tab.List.RowCount).IsEqualTo(before + 1);

			for (int i = 0; i < 100; i++)
			{
				tab.AddRow();
			}

			await Assert.That(scroll.MaxScrollFromTop()).IsGreaterThan(0);
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(scroll.MaxScrollFromTop());

			// The wheel up detaches it; new rows no longer move the view.
			scroll.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 50, 50, 120));
			double detached = scroll.ScrollOffsetFromTop();
			await Assert.That(detached).IsLessThan(scroll.MaxScrollFromTop());
			tab.AddRow();
			await Assert.That(scroll.ScrollOffsetFromTop()).IsEqualTo(detached);
		}
	}
}
