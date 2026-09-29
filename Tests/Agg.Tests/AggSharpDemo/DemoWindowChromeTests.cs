// Copyright (c) 2026, Lars Brubaker
// All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// agg-gui's window chrome and widget styling as the GUI demo composes it: a collapse chevron that folds a
	// window to its title, and accent buttons with white text.
	// new DemoTheme() writes ThemeConfig.Current.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class DemoWindowChromeTests
	{
		[Test]
		public async Task TheTitleBarChevronFoldsTheWindowToItsTitle()
		{
			var canvas = new GuiWidget(1000, 700);
			var host = new DemoWindowHost(canvas);
			WindowWidget window = host.GetWindow(GuiDemoSpecs.All.First(s => s.OpenByDefault));
			double top = window.Position.Y + window.Height;

			await Assert.That(window.Collapsible).IsTrue();
			window.TitleBar.Descendants().First(w => w.Name == "Window Collapse Button").InvokeClick();

			await Assert.That(window.Collapsed).IsTrue();
			await Assert.That(window.ClientArea.Visible).IsFalse();
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top);
		}

		[Test]
		public async Task ButtonsAreAccentWithWhiteTextAndFollowTheAccent()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			ThemedTextButton button = demoTheme.AccentButton(new ThemedTextButton("Click me!", demoTheme.Theme));

			await Assert.That(button.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(AccentColor.Blue));
			await Assert.That(button.TextColor).IsEqualTo(Color.White);

			demoTheme.SetAccent(AccentColor.Green);
			await Assert.That(button.BackgroundColor).IsEqualTo(DemoTheme.ColorOf(AccentColor.Green));
		}
	}
}
