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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Window Options window (agg-gui's text_demos/dialogs/basic.rs): its controls steer the live
	// window it is shown in.
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class WindowOptionsWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Window Options");

		private static (WindowOptionsWindow Content, WindowWidget Window) Open(GuiWidget canvas, DemoTheme demoTheme)
		{
			var host = new DemoWindowHost(canvas, demoTheme);

			// Every demo window has a collapse chevron by the same name; closing the first run's windows (About too)
			// leaves this window's as the only one an automation click can find.
			foreach (DemoSpec other in GuiDemoSpecs.DefaultOpen)
			{
				host.SetOpen(other, false);
			}

			host.SetOpen(Spec, true);
			canvas.PerformLayout();
			WindowWidget window = host.GetWindow(Spec);
			return ((WindowOptionsWindow)window.FindDescendant(Spec.ContentName), window);
		}

		[Test]
		public async Task EachControlChangesTheLiveHostWindow()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			(WindowOptionsWindow content, WindowWidget window) = Open(new GuiWidget(1600, 900), demoTheme);

			foreach (string name in new[] { "Window Options Title", "Window Options Resizable", "Window Options Collapsible", "Window Options Auto Size", "Window Options Size" })
			{
				await Assert.That(content.FindDescendant(name)).IsNotNull().Because(name);
			}

			// Attached with agg-gui's WindowOptionCells defaults.
			await Assert.That(content.Host).IsSameReferenceAs(window);
			await Assert.That(content.TitleField.Text).IsEqualTo("Window Options");
			await Assert.That(window.Resizable).IsTrue();
			await Assert.That(window.Collapsible).IsTrue();
			await Assert.That(window.AutoSize).IsFalse();
			await Assert.That(content.SizeLabel.Text).IsEqualTo("Current window size: 360 × 290");

			content.TitleField.Text = "Renamed";
			await Assert.That(window.Title).IsEqualTo("Renamed");

			content.ResizableBox.Checked = false;
			await Assert.That(window.Resizable).IsFalse();

			content.CollapsibleBox.Checked = false;
			await Assert.That(window.Collapsible).IsFalse();
			await Assert.That(window.FindDescendant("Window Collapse Button").Visible).IsFalse();

			// Auto-size fits the window to the content, top edge fixed, and the size label follows.
			double top = window.Position.Y + window.Height;
			content.AutoSizeBox.Checked = true;
			await Assert.That(window.AutoSize).IsTrue();
			await Assert.That(window.ClientArea.Height).IsEqualTo(content.Height).Within(.5);
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top).Within(.001);
			await Assert.That(content.SizeLabel.Text).IsNotEqualTo("Current window size: 360 × 290");

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(content.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			await Assert.That(window.ClientArea.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
		}

		[Test]
		public async Task ClickingCollapsibleOffUnfoldsAndHidesTheChevron()
		{
			var systemWindow = new SystemWindow(900, 700) { Name = "Window Options Test Window" };
			var canvas = new GuiWidget { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			systemWindow.AddChild(canvas);
			(WindowOptionsWindow content, WindowWidget window) = Open(canvas, new DemoTheme(ThemePreference.Light));

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				double height = window.Height;
				testRunner.ClickByName("Window Collapse Button");
				testRunner.WaitFor(() => window.Collapsed);
				await Assert.That(window.Height).IsLessThan(height);

				testRunner.ClickByName("Window Collapse Button");
				testRunner.WaitFor(() => !window.Collapsed);

				testRunner.ClickByName("Window Options Collapsible");
				testRunner.WaitFor(() => !window.Collapsible);
				await Assert.That(content.CollapsibleBox.Checked).IsFalse();
				await Assert.That(window.FindDescendant("Window Collapse Button").Visible).IsFalse();
				testRunner.MarkTestComplete();
			});
		}
	}
}
