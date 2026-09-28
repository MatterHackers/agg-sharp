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
	// The Scrolling window's Appearance tab (agg-gui's demo-ui/src/windows/scrolling/appearance.rs).
	public class AppearanceTabTests
	{
		private static DemoSpec ScrollingSpec => GuiDemoSpecs.All.First(s => s.Title == "Scrolling");

		private static (GuiWidget Page, AppearanceTab Tab) Build()
		{
			var page = new GuiWidget(680 * GuiWidget.DeviceScale, 540 * GuiWidget.DeviceScale);
			var window = (ScrollingWindow)GuiDemoSpecs.CreateContent(ScrollingSpec);
			page.AddChild(window);
			page.PerformLayout();
			return (page, window.Appearance);
		}

		[Test]
		public async Task OpensOnTheFloatingPresetWithEveryControl()
		{
			(GuiWidget page, AppearanceTab tab) = Build();
			await Assert.That(tab.Visible).IsTrue();
			foreach (string name in new[]
			{
				"Scrolling Appearance Presets", "Scrolling Appearance Details", "Scrolling Appearance Type", "Scrolling Appearance Margin Same",
				"Scrolling Appearance Content Margin", "Scrolling Appearance Bar Width", "Scrolling Appearance Floating Width",
				"Scrolling Appearance Handle Min", "Scrolling Appearance Outer Margin", "Scrolling Appearance Color",
				"Scrolling Appearance Inner Margin", "Scrolling Appearance Fade Strength", "Scrolling Appearance Fade Size",
				"Scrolling Appearance Visibility", "Scrolling Appearance Content Length", "Scrolling Appearance View",
			})
			{
				await Assert.That(tab.FindDescendant(name)).IsNotNull().Because(name);
			}

			// agg-gui's defaults: the Floating preset, shown when needed, Details closed, two paragraphs.
			await Assert.That(tab.Presets.SelectedLabel).IsEqualTo("Floating");
			await Assert.That(tab.Details.Expanded).IsFalse();
			await Assert.That(tab.Visibility.SelectedLabel).IsEqualTo("VisibleWhenNeeded");
			await Assert.That(tab.ParagraphCount).IsEqualTo(2);

			ScrollBar bar = tab.View.VerticalScrollBar;
			double s = GuiWidget.DeviceScale;
			await Assert.That(bar.Floating).IsTrue();
			await Assert.That(bar.BarWidth).IsEqualTo(10 * s);
			await Assert.That(bar.FloatingWidth).IsEqualTo(2 * s);
			await Assert.That(bar.HandleMinLength).IsEqualTo(12 * s);
			await Assert.That(tab.View.EdgeFade.Strength).IsEqualTo(0.5);
			await Assert.That(tab.FloatingWidthRow.Visible).IsTrue();
			await Assert.That(tab.InnerMarginRow.Visible).IsFalse();

			var image = new ImageBuffer((int)page.Width, (int)page.Height);
			page.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task PresetsAndDetailsRestyleTheView()
		{
			(GuiWidget page, AppearanceTab tab) = Build();
			ScrollableWidget view = tab.View;
			ScrollBar bar = view.VerticalScrollBar;
			double s = GuiWidget.DeviceScale;

			// Enough content to scroll.
			tab.ContentLength.Value = 40;
			page.PerformLayout();
			await Assert.That(view.MaxScrollFromTop()).IsGreaterThan(0);

			// Solid: the bar takes room, with its inner margin, and swaps the Details rows.
			tab.Presets.SelectedIndex = 0;
			await Assert.That(tab.Type.SelectedLabel).IsEqualTo("Solid");
			await Assert.That(bar.Floating).IsFalse();
			await Assert.That(bar.BarWidth).IsEqualTo(6 * s);
			await Assert.That(bar.InnerMargin).IsEqualTo(4 * s);
			await Assert.That(view.ScrollArea.DeviceMargin.Right).IsEqualTo(10 * s).Within(1e-9);
			await Assert.That(tab.InnerMarginRow.Visible).IsTrue();
			await Assert.That(tab.FloatingWidthRow.Visible).IsFalse();

			// Thin: floating and always visible.
			tab.Presets.SelectedIndex = 1;
			await Assert.That(bar.Floating).IsTrue();
			await Assert.That(bar.Show).IsEqualTo(ScrollBar.ShowState.Always);
			await Assert.That(view.ScrollArea.DeviceMargin.Right).IsEqualTo(0);

			// Details controls write through.
			tab.BarWidth.Value = 20;
			await Assert.That(bar.BarWidth).IsEqualTo(20 * s);
			tab.HandleMin.Value = 50;
			await Assert.That(bar.Thumb.Height).IsGreaterThanOrEqualTo(50 * s - 1e-9);
			tab.OuterMargin.Value = 5;
			await Assert.That(bar.OuterMargin).IsEqualTo(5 * s);
			tab.FadeStrength.Value = 0;
			await Assert.That(view.EdgeFade.FadedEdges().Any()).IsFalse();
			tab.ContentMargin.Value = 8;
			await Assert.That(view.ScrollArea.Children[0].Margin.Left).IsEqualTo(8);
			tab.BarColor.SelectedIndex = 1;
			await Assert.That(bar.TrackColor).IsEqualTo(Color.Transparent);

			// Visibility: hidden hides the bar.
			tab.Visibility.SelectedIndex = 0;
			await Assert.That(bar.Visible).IsFalse();

			// Content length rebuilds the paragraphs.
			tab.ContentLength.Value = 5;
			await Assert.That(view.ScrollArea.Children[0].Children.Count).IsEqualTo(5);

			var image = new ImageBuffer((int)page.Width, (int)page.Height);
			page.OnDraw(image.NewGraphics2D());
		}

		[Test, NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ClickingThePresetsAndOpeningDetailsWorksInAWindow()
		{
			var window = new SystemWindow(ScrollingSpec.DefaultWidth, ScrollingSpec.DefaultHeight) { Name = "Appearance Test Window" };
			var scrolling = (ScrollingWindow)GuiDemoSpecs.CreateContent(ScrollingSpec);
			window.AddChild(scrolling);
			AppearanceTab tab = scrolling.Appearance;

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				testRunner.ClickByName("Details Header");
				testRunner.WaitFor(() => tab.Details.Expanded);

				testRunner.ClickByName("Scrolling Appearance Presets", offset: new Point2D(-(int)(tab.Presets.Width / 3), 0));
				testRunner.WaitFor(() => tab.Presets.SelectedIndex == 0);
				await Assert.That(tab.View.VerticalScrollBar.Floating).IsFalse();
				await Assert.That(tab.Type.SelectedLabel).IsEqualTo("Solid");

				testRunner.MarkTestComplete();
			});
		}
	}
}
