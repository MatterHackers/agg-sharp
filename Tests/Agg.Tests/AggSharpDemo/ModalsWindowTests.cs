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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Modals window (agg-gui's modals_demo in demo-ui/src/windows/text_demos/dialogs.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class ModalsWindowTests
	{
		private static DemoSpec ModalsSpec => GuiDemoSpecs.All.First(s => s.Title == "Modals");

		[Test]
		public async Task BuildsTheButtonsAndStacksNamedModals()
		{
			var page = new GuiWidget(400, 300);
			var window = (ModalsWindow)GuiDemoSpecs.CreateContent(ModalsSpec);
			page.AddChild(window);
			page.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Modals Content");
			foreach (string name in new[] { "Modals Open User", "Modals Open Save", "Modals Source Link" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			// With no SystemWindow the overlay hangs on the window itself.
			window.OpenUserModal();
			foreach (string name in new[] { "Modals Overlay", "Modals User Dialog", "Modals Name", "Modals Role", "Modals User Save", "Modals User Cancel" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			window.OpenSaveModal();
			await Assert.That(window.Overlay.TopLayer.Name).IsEqualTo("Modals Save Dialog");
			await Assert.That(window.FindDescendant("Modals Yes Please")).IsNotNull();
			await Assert.That(window.FindDescendant("Modals No Thanks")).IsNotNull();

			// Closing the last modal takes the overlay down.
			window.Overlay.CloseAll();
			await Assert.That(window.Overlay.Parent).IsNull();
		}

		[Test]
		public async Task AnOpenModalTakesAThemeChange()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var page = new GuiWidget(400, 300);
			var window = (ModalsWindow)GuiDemoSpecs.CreateContent(ModalsSpec, demoTheme);
			page.AddChild(window);
			window.OpenUserModal();

			GuiWidget dialog = window.FindDescendant("Modals User Dialog");
			Color lightFill = dialog.BackgroundColor;
			demoTheme.SetPreference(ThemePreference.Dark);

			await Assert.That(dialog.BackgroundColor).IsEqualTo(demoTheme.Palette.WindowFill);
			await Assert.That(dialog.BackgroundColor).IsNotEqualTo(lightFill);
			await Assert.That(dialog.BorderColor).IsEqualTo(demoTheme.Palette.WidgetStroke);
			var role = (DropDownList)window.FindDescendant("Modals Role");
			await Assert.That(role.TextColor).IsEqualTo(demoTheme.Theme.TextColor);
			window.Overlay.CloseAll();
		}

		[Test]
		public async Task ProgressStepsOncePerPaintedFrame()
		{
			var page = new GuiWidget(400, 300);
			var window = (ModalsWindow)GuiDemoSpecs.CreateContent(ModalsSpec);
			page.AddChild(window);
			page.PerformLayout();
			window.OpenSaveModal();
			window.FindDescendant("Modals Yes Please").InvokeClick();
			await Assert.That(window.SaveProgress).IsEqualTo(0);

			// No paint, no progress: it is driven by frames, not a clock.
			var frame = new ImageBuffer(400, 300);
			page.OnDraw(frame.NewGraphics2D());
			await Assert.That(window.SaveProgress.Value).IsEqualTo(ModalsWindow.ProgressStep).Within(1e-9);
			page.OnDraw(frame.NewGraphics2D());
			await Assert.That(window.SaveProgress.Value).IsEqualTo(2 * ModalsWindow.ProgressStep).Within(1e-9);
			window.Overlay.CloseAll();
			await Assert.That(window.SaveProgress).IsNull();
		}

		[Test]
		public async Task SaveStacksOverUserAndProgressClosesEverything()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (ModalsWindow)GuiDemoSpecs.CreateContent(ModalsSpec, demoTheme);
			var systemWindow = new SystemWindow(600, 400) { Name = "Modals Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				testRunner.ClickByName("Modals Open User");
				testRunner.WaitForName("Modals User Dialog");
				await Assert.That(window.Overlay.Parent).IsEqualTo(systemWindow);

				// Escape closes the top modal only.
				testRunner.ClickByName("Modals User Save");
				testRunner.WaitForName("Modals Save Dialog");
				testRunner.Type("{Escape}");
				testRunner.WaitFor(() => window.Overlay.Layers.Count == 1);
				await Assert.That(window.Overlay.TopLayer.Name).IsEqualTo("Modals User Dialog");

				// Save, Yes Please: the progress modal fills and takes every modal with it.
				testRunner.ClickByName("Modals User Save");
				testRunner.ClickByName("Modals Yes Please");
				testRunner.WaitForName("Modals Progress Dialog");
				testRunner.WaitFor(() => window.Overlay.Parent == null);

				await Assert.That(window.Overlay.Layers.Count).IsEqualTo(0);
				await Assert.That(window.SaveProgress).IsNull();
				testRunner.MarkTestComplete();
			});
		}
	}
}
