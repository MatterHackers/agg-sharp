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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Menus window (agg-gui's demo-ui/src/windows/menu_demo.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class MenusWindowTests
	{
		private static DemoSpec MenusSpec => GuiDemoSpecs.All.First(s => s.Title == "Menus");

		[Test]
		public async Task BuildsTheMenuBarContextAreaAndLog()
		{
			var page = new GuiWidget(520, 320);
			var window = (MenusWindow)GuiDemoSpecs.CreateContent(MenusSpec);
			page.AddChild(window);
			page.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Menus Content");
			string[] names =
			{
				"Menus Menu Bar", "Menus File Menu", "Menus Edit Menu", "Menus View Menu",
				"Menus Context Area", "Menus Log 0", "Menus Log 1", "Menus Log 2",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			await Assert.That(window.FindDescendant("Menus Log 0").Text).IsEqualTo(MenusWindow.FirstLogLine);
			await Assert.That(window.IsChecked("file.show-slider")).IsTrue();
			await Assert.That(window.RadioChoice("context")).IsEqualTo("context.option-a");
		}

		[Test]
		public async Task ContextMenuAndMenuBarPicksAreLogged()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (MenusWindow)GuiDemoSpecs.CreateContent(MenusSpec, demoTheme);
			var systemWindow = new SystemWindow(520, 320) { Name = "Menus Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				// Right-click opens the context menu; its disabled row does nothing, New logs.
				testRunner.RightClickByName("Menus Context Area");
				testRunner.WaitForName("menus.context.new");
				await Assert.That(testRunner.GetWidgetByName("menus.context.disabled", out _).Enabled).IsFalse();

				// Rows carry agg-gui's Font Awesome icons: New's plus, drawn in the icon slot.
				await Assert.That(((PopupMenu.MenuItem)testRunner.GetWidgetByName("menus.context.new", out _)).Image).IsNotNull();
				testRunner.ClickByName("menus.context.new");
				testRunner.WaitFor(() => window.Log.Last() == "Action fired: context.new");

				// A radio row in the menu bar moves the choice and shows up at the top of the log.
				testRunner.ClickByName("Menus File Menu");
				testRunner.ClickByName("menus.file.option-b");
				testRunner.WaitFor(() => window.RadioChoice("file") == "file.option-b");

				await Assert.That(window.Log.Last()).IsEqualTo("Action fired: file.option-b");
				await Assert.That(window.FindDescendant("Menus Log 0").Text).IsEqualTo("Action fired: file.option-b");
				await Assert.That(window.FindDescendant("Menus Log 1").Text).IsEqualTo("Action fired: context.new");
				testRunner.MarkTestComplete();
			});
		}

		[Test]
		public async Task LeafTwoClosesTheMenuWhereShowSliderKeepsItOpen()
		{
			var window = (MenusWindow)GuiDemoSpecs.CreateContent(MenusSpec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(520, 320) { Name = "Menus Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				// Show Slider keeps the menu open, as agg-gui's keep_open() row does; Leaf Two closes it.
				testRunner.ClickByName("Menus View Menu");
				testRunner.ClickByName("menus.view.show-slider");
				testRunner.WaitFor(() => !window.IsChecked("view.show-slider"));
				await Assert.That(testRunner.NameExists("menus.view.show-slider", .1)).IsTrue();
				testRunner.ClickByName("menus.view.more");
				testRunner.ClickByName("menus.view.deep");
				testRunner.ClickByName("menus.view.leaf-two");
				testRunner.WaitFor(() => !window.IsChecked("view.leaf-two"));
				await Assert.That(testRunner.WaitForWidgetDisappear("menus.view.show-slider", 5)).IsTrue();
				testRunner.MarkTestComplete();
			});
		}
	}
}
