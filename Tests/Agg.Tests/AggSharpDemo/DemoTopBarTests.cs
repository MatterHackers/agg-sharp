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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's menu bar, after agg-gui's top_bar.rs: its menus, their stable ids, and what picking them does.
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class DemoTopBarTests
	{
		private static MenuItemModel Child(IEnumerable<MenuItemModel> items, string id)
		{
			return items.Single(item => item.AutomationName == id);
		}

		[Test]
		public async Task MenuTreeHasAggGuisIds()
		{
			var bar = new DemoTopBar(new DemoTheme());

			await Assert.That(bar.Menus.Select(m => m.Text)).IsEquivalentTo(new[] { "Demos", "View", "Help" }, CollectionOrdering.Matching);

			var demos = bar.Menus[0].SubMenuItems();
			await Assert.That(demos.Select(g => g.Text)).IsEquivalentTo(GuiDemoSpecs.Groups, CollectionOrdering.Matching);
			// A group's rows are the sidebar's: sorted by title, case-insensitively, not in GuiDemoSpecs order
			var widgets = Child(demos, "demos.Widgets").SubMenuItems();
			await Assert.That(widgets.Select(d => d.AutomationName))
				.IsEquivalentTo(GuiDemoSpecs.All.Where(s => s.Group == "Widgets")
					.OrderBy(s => s.Title.ToLowerInvariant(), System.StringComparer.Ordinal)
					.Select(s => "demo." + s.Title), CollectionOrdering.Matching);

			// app_builder.rs builds the menu from the sidebar's groups: sorted, and the Inspector under Tools
			var tools = Child(demos, "demos.Tools").SubMenuItems().Select(d => d.Text).ToList();
			await Assert.That(tools).Contains("Inspector");
			await Assert.That(string.Join("|", tools)).IsEqualTo(string.Join("|", tools.OrderBy(t => t.ToLowerInvariant(), System.StringComparer.Ordinal)));

			var view = bar.Menus[1].SubMenuItems();
			await Assert.That(view.Where(i => !i.IsSeparator).Select(i => i.AutomationName))
				.IsEquivalentTo(new[] { "view.backend", "view.snap", "view.theme", "view.accent" }, CollectionOrdering.Matching);
			await Assert.That(Child(view, "view.theme").SubMenuItems().Select(i => i.AutomationName))
				.IsEquivalentTo(new[] { "view.theme.light", "view.theme.dark", "view.theme.system" }, CollectionOrdering.Matching);
			await Assert.That(Child(view, "view.accent").SubMenuItems().Select(i => i.AutomationName))
				.IsEquivalentTo(new[]
				{
					"view.accent.blue", "view.accent.purple", "view.accent.pink", "view.accent.red",
					"view.accent.orange", "view.accent.yellow", "view.accent.green", "view.accent.teal",
				}, CollectionOrdering.Matching);

			await Assert.That(bar.Menus[2].SubMenuItems().Select(i => i.AutomationName)).IsEquivalentTo(new[] { "help.github" }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task ItemsCarryTopBarRsIconsAndKeepTheMenuOpen()
		{
			var bar = new DemoTopBar(new DemoTheme());
			var view = bar.Menus[1].SubMenuItems();

			await Assert.That(Child(view, "view.backend").IconGlyph).IsEqualTo("\uF109");
			await Assert.That(Child(view, "view.snap").IconGlyph).IsEqualTo("\uF076");
			await Assert.That(Child(view, "view.theme").IconGlyph).IsEqualTo("\uF042");
			await Assert.That(Child(view, "view.accent").IconGlyph).IsEqualTo("\uF53F");
			await Assert.That(Child(bar.Menus[2].SubMenuItems(), "help.github").IconGlyph).IsEqualTo("\uF08E");
			await Assert.That(bar.Menus[0].SubMenuItems().All(g => g.IconGlyph == "\uF009")).IsTrue();
			await Assert.That(bar.Menus[0].IconGlyph).IsEqualTo("\uF009").Because("the Demos title leads with top_bar.rs's th-large icon");
			await Assert.That(bar.Menus[0].IconTypeFace).IsEqualTo(IconFont.TypeFace);
			await Assert.That(string.Join("|", Child(view, "view.theme").SubMenuItems().Select(i => i.IconGlyph)))
				.IsEqualTo("\uF185|\uF186|\uF108");
			await Assert.That(view.Where(i => i.IconGlyph != null).All(i => i.IconTypeFace == IconFont.TypeFace)).IsTrue();

			// Each Color item's icon is a swatch of its accent
			foreach (var accentItem in Child(view, "view.accent").SubMenuItems())
			{
				var accent = System.Enum.Parse<AccentColor>(accentItem.Text);
				var icon = accentItem.Icon;
				await Assert.That(icon).IsNotNull();
				await Assert.That(icon.GetPixel(icon.Width / 2, icon.Height / 2)).IsEqualTo(DemoTheme.ColorOf(accent));
			}

			// agg-gui's keep_open(): every toggle and radio row leaves the menu open
			var toggles = view.Where(i => i.IsChecked != null)
				.Concat(Child(view, "view.theme").SubMenuItems())
				.Concat(Child(view, "view.accent").SubMenuItems());
			await Assert.That(toggles.Any(i => i.CloseMenuOnPick)).IsFalse();
		}

		[Test]
		public async Task PickingWindowSnappingLeavesTheViewMenuOpen()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			var shell = new GuiDemoShell(demoTheme);
			var window = new SystemWindow(800, 500) { Name = "GuiDemo Test Window" };
			window.AddChild(shell);

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				bool snapToggled = false;
				shell.TopBar.SnapToggled += (s, e) => snapToggled = true;
				testRunner.ClickByName("View Menu");
				testRunner.ClickByName("view.snap");
				testRunner.WaitFor(() => snapToggled);

				await Assert.That(shell.TopBar.SnapEnabled).IsFalse();
				await Assert.That(testRunner.NamedWidgetExists("view.backend")).IsTrue();
				testRunner.MarkTestComplete();
			});
		}

		[Test]
		public async Task ActionsReachTheThemeAndRaiseTheirEvents()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			var bar = new DemoTopBar(demoTheme);
			var view = bar.Menus[1].SubMenuItems();

			Child(view, "view.accent").SubMenuItems().Single(i => i.AutomationName == "view.accent.red").Action();
			await Assert.That(demoTheme.Accent).IsEqualTo(AccentColor.Red);

			int snapToggles = 0;
			bar.SnapToggled += (s, e) => snapToggles++;
			await Assert.That(bar.SnapEnabled).IsTrue();
			Child(view, "view.snap").Action();
			await Assert.That(bar.SnapEnabled).IsFalse();
			await Assert.That(snapToggles).IsEqualTo(1);

			int backendToggles = 0;
			bar.BackendPanelToggled += (s, e) => backendToggles++;
			Child(view, "view.backend").Action();
			await Assert.That(bar.BackendPanelOpen).IsTrue();
			await Assert.That(backendToggles).IsEqualTo(1);

			string url = null;
			bar.OpenUrlRequested += u => url = u;
			Child(bar.Menus[2].SubMenuItems(), "help.github").Action();
			await Assert.That(url).IsEqualTo("https://github.com/larsbrubaker/agg-sharp");

			DemoSpec requested = null;
			bar.DemoRequested += spec => requested = spec;
			Child(Child(bar.Menus[0].SubMenuItems(), "demos.Graphics").SubMenuItems(), "demo.Lion").Action();
			await Assert.That(requested?.Title).IsEqualTo("Lion");
		}

		[Test]
		public async Task ViewItemsMarkTheCurrentState()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark, AccentColor.Teal);
			var bar = new DemoTopBar(demoTheme);
			var view = bar.Menus[1].SubMenuItems();

			var theme = Child(view, "view.theme").SubMenuItems();
			await Assert.That(theme.All(i => i.IsRadio)).IsTrue();
			await Assert.That(string.Join("|", theme.Where(i => i.IsChecked()).Select(i => i.AutomationName))).IsEqualTo("view.theme.dark");

			var accents = Child(view, "view.accent").SubMenuItems();
			await Assert.That(accents.All(i => i.IsRadio)).IsTrue();
			await Assert.That(string.Join("|", accents.Where(i => i.IsChecked()).Select(i => i.AutomationName))).IsEqualTo("view.accent.teal");

			// The checks are live: they read the flag when the menu is built, not when the model was
			var snap = Child(view, "view.snap");
			await Assert.That(snap.IsRadio).IsFalse();
			await Assert.That(snap.IsChecked()).IsTrue();
			snap.Action();
			await Assert.That(snap.IsChecked()).IsFalse();

			var backend = Child(view, "view.backend");
			await Assert.That(backend.IsChecked()).IsFalse();
			backend.Action();
			await Assert.That(backend.IsChecked()).IsTrue();
		}

		[Test]
		[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), MatterHackers.Agg.UI.Tests.SystemServicesTests.NotInParallelKey, nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
		public async Task GitHubGoesToTheUrlLauncherWhenTheHostDoesNotListen()
		{
			var saved = UrlLauncher.Provider;
			try
			{
				string launched = null;
				UrlLauncher.Provider = url => launched = url;

				var bar = new DemoTopBar(new DemoTheme());
				Child(bar.Menus[2].SubMenuItems(), "help.github").Action();

				await Assert.That(launched).IsEqualTo(DemoTopBar.GitHubUrl);
			}
			finally
			{
				UrlLauncher.Provider = saved;
			}
		}

		[Test]
		public async Task ClickingViewThemeDarkSwitchesToDark()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var shell = new GuiDemoShell(demoTheme);
			bool themeChanged = false;
			demoTheme.ThemeChanged += (s, e) => themeChanged = true;

			var window = new SystemWindow(800, 500) { Name = "GuiDemo Test Window" };
			window.AddChild(shell);

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				testRunner.ClickByName("View Menu");
				testRunner.ClickByName("view.theme");
				testRunner.ClickByName("view.theme.dark");
				testRunner.WaitFor(() => themeChanged);

				await Assert.That(themeChanged).IsTrue();
				await Assert.That(demoTheme.Preference).IsEqualTo(ThemePreference.Dark);
				await Assert.That(shell.Canvas.BackgroundColor).IsEqualTo(DemoPalette.Dark.BackgroundColor);
				testRunner.MarkTestComplete();
			});
		}
	}
}
