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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Flex Layout Test window (agg-gui's tests/basic/layout.rs layout_test).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class FlexLayoutTestWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Flex Layout Test");

		[Test]
		public async Task BuildsFixedBoxesAndAStretchedOne()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (FlexLayoutTestWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Flex Layout Test Content");
			for (int i = 0; i < FlexLayoutTestWindow.Labels.Length; i++)
			{
				await Assert.That(window.FindDescendant("Flex Layout Test " + FlexLayoutTestWindow.Labels[i])).IsSameReferenceAs(window.Boxes[i]);
			}

			double s = GuiWidget.DeviceScale;
			// A box's Border ring lies outside its bounds, so its drawn width is Width plus the ring: agg-gui's
			// fixed widths for three and the row inside the 14 unit padding for the stretched one.
			double DrawnWidth(GuiWidget box) => box.Width + box.DeviceBorder.Width;
			await Assert.That(DrawnWidth(window.Boxes[0])).IsEqualTo(80 * s);
			await Assert.That(DrawnWidth(window.Boxes[1])).IsEqualTo(120 * s);
			await Assert.That(DrawnWidth(window.Boxes[2])).IsEqualTo(100 * s);
			await Assert.That(DrawnWidth(window.Boxes[3])).IsEqualTo(window.Width - window.DevicePadding.Width);
			for (int i = 0; i < window.Boxes.Count; i++)
			{
				// Every box's drawn left edge sits on the padding's inner edge.
				GuiWidget box = window.Boxes[i];
				await Assert.That(box.BoundsRelativeToParent.Left - box.DeviceBorder.Left).IsEqualTo(window.LocalBounds.Left + window.DevicePadding.Left);
			}

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task OnlyTheStretchedBoxFollowsTheWindowWidth()
		{
			var window = (FlexLayoutTestWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(420, 360) { Name = "Flex Layout Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				testRunner.WaitForName("Flex Layout Test Stretch");
				double stretchBefore = window.Boxes[3].Width;
				double leftBefore = window.Boxes[0].Width;

				UiThread.RunOnIdle(() => systemWindow.Width = 520);
				testRunner.WaitFor(() => window.Boxes[3].Width > stretchBefore);
				await Assert.That(window.Boxes[3].Width).IsEqualTo(stretchBefore + 100);
				await Assert.That(window.Boxes[0].Width).IsEqualTo(leftBefore);
				testRunner.MarkTestComplete();
			});
		}
	}
}
