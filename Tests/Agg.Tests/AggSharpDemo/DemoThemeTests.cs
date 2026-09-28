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
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The demo's theme service against agg-gui's theme.rs values (Visuals::dark / light, AccentColor::color).
	// GuiDemoShell installs its theme as ThemeConfig.Current, which other tests set and read.
	[NotInParallel(nameof(MatterHackers.Agg.UI.ThemeConfig.Current))]
	public class DemoThemeTests
	{
		private static Color Rgb(double r, double g, double b) => new ColorF(r, g, b).ToColor();

		[Test]
		public async Task DarkAndLightUseAggGuiColours()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			ThemeConfig theme = demoTheme.Theme;

			await Assert.That(theme.IsDarkTheme).IsTrue();
			await Assert.That(demoTheme.Palette.BackgroundColor).IsEqualTo(Rgb(0.10, 0.10, 0.12));
			await Assert.That(demoTheme.Palette.PanelFill).IsEqualTo(Rgb(0.13, 0.13, 0.15));
			await Assert.That(demoTheme.Palette.TopBarBackground).IsEqualTo(Rgb(0.15, 0.15, 0.17));
			await Assert.That(theme.BackgroundColor).IsEqualTo(Rgb(0.15, 0.15, 0.18));
			await Assert.That(theme.TextColor).IsEqualTo(Rgb(0.90, 0.90, 0.92));

			demoTheme.SetPreference(ThemePreference.Light);

			await Assert.That(theme.IsDarkTheme).IsFalse();
			await Assert.That(demoTheme.Palette.BackgroundColor).IsEqualTo(Rgb(0.90, 0.90, 0.92));
			await Assert.That(demoTheme.Palette.PanelFill).IsEqualTo(Rgb(0.92, 0.92, 0.95));
			await Assert.That(demoTheme.Palette.TopBarBackground).IsEqualTo(Rgb(0.88, 0.88, 0.91));
			await Assert.That(theme.BackgroundColor).IsEqualTo(Rgb(0.97, 0.97, 0.98));
			await Assert.That(theme.TextColor).IsEqualTo(Rgb(0.08, 0.08, 0.10));
		}

		[Test]
		public async Task SystemFollowsTheOperatingSystemAndFallsBackToDark()
		{
			await Assert.That(new DemoTheme(ThemePreference.System).IsDark).IsTrue();
			await Assert.That(new DemoTheme(ThemePreference.System, systemPrefersDark: () => false).IsDark).IsFalse();
			await Assert.That(new DemoTheme(ThemePreference.System, systemPrefersDark: () => true).IsDark).IsTrue();
		}

		[Test]
		public async Task RefreshSystemPreferenceFollowsAnOsChange()
		{
			bool osDark = true;
			var demoTheme = new DemoTheme(ThemePreference.System, systemPrefersDark: () => osDark);
			int changes = 0;
			demoTheme.ThemeChanged += (s, e) => changes++;

			// Same answer: nothing to recolour
			demoTheme.RefreshSystemPreference();
			await Assert.That(changes).IsEqualTo(0);

			osDark = false;
			demoTheme.RefreshSystemPreference();
			await Assert.That(demoTheme.IsDark).IsFalse();
			await Assert.That(changes).IsEqualTo(1);

			// An explicit preference ignores the OS
			demoTheme.SetPreference(ThemePreference.Dark);
			osDark = true;
			demoTheme.RefreshSystemPreference();
			await Assert.That(changes).IsEqualTo(2);
		}

		[Test]
		[NotInParallel(new[] { MatterHackers.Agg.UI.Tests.SystemServicesTests.NotInParallelKey, nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
		public async Task TheShellsDefaultThemeAsksSystemAppearance()
		{
			var saved = SystemAppearance.Provider;
			try
			{
				SystemAppearance.Provider = () => false;
				await Assert.That(new GuiDemoShell().DemoTheme.IsDark).IsFalse();

				// No provider (a platform that cannot tell): agg-gui's dark fallback
				SystemAppearance.Provider = null;
				await Assert.That(new GuiDemoShell().DemoTheme.IsDark).IsTrue();
			}
			finally
			{
				SystemAppearance.Provider = saved;
			}
		}

		[Test]
		public async Task AccentSetsPrimaryAccentColor()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			await Assert.That(demoTheme.Theme.PrimaryAccentColor).IsEqualTo(Rgb(0.22, 0.45, 0.88));

			demoTheme.SetAccent(AccentColor.Orange);
			await Assert.That(demoTheme.Theme.PrimaryAccentColor).IsEqualTo(Rgb(0.90, 0.46, 0.18));

			demoTheme.SetAccent(AccentColor.Teal);
			await Assert.That(demoTheme.Theme.PrimaryAccentColor).IsEqualTo(Rgb(0.14, 0.62, 0.66));
		}

		[Test]
		public async Task ThemeChangedFiresOnlyForARealChange()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			int changes = 0;
			demoTheme.ThemeChanged += (s, e) => changes++;

			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(changes).IsEqualTo(0);

			demoTheme.SetPreference(ThemePreference.Light);
			await Assert.That(changes).IsEqualTo(1);

			demoTheme.SetAccent(AccentColor.Green);
			await Assert.That(changes).IsEqualTo(2);
		}

		[Test]
		public async Task ShellRecoloursOnThemeChange()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			var shell = new GuiDemoShell(demoTheme);

			await Assert.That(shell.Canvas.BackgroundColor).IsEqualTo(DemoPalette.Dark.BackgroundColor);
			await Assert.That(shell.Sidebar.BackgroundColor).IsEqualTo(DemoPalette.Dark.PanelFill);
			await Assert.That(shell.TopBar.BackgroundColor).IsEqualTo(DemoPalette.Dark.TopBarBackground);

			demoTheme.SetPreference(ThemePreference.Light);

			await Assert.That(shell.Canvas.BackgroundColor).IsEqualTo(DemoPalette.Light.BackgroundColor);
			await Assert.That(shell.Sidebar.BackgroundColor).IsEqualTo(DemoPalette.Light.PanelFill);
			await Assert.That(shell.TopBar.BackgroundColor).IsEqualTo(DemoPalette.Light.TopBarBackground);
			await Assert.That(shell.TopBar.MenuBar.Descendants<TextWidget>().First().TextColor).IsEqualTo(DemoPalette.Light.TextColor);
		}
	}
}
