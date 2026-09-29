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
using Markdig.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's About window (agg-gui's about in demo-ui/src/windows.rs). MarkdownWidget.Theme and
	// LaunchBrowser are process-wide, so these do not run beside other tests that set them.
	[NotInParallel(new[] { SharedStateKeys.MarkdownWidget, SharedStateKeys.ThemeConfigCurrent })]
	public class AboutWindowTests
	{
		[Test]
		public async Task RendersTheAboutMarkdownAndRecoloursWithTheTheme()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (AboutWindow)GuiDemoSpecs.CreateContent(GuiDemoSpecs.About, demoTheme);
			var host = new GuiWidget(GuiDemoSpecs.About.DefaultWidth, GuiDemoSpecs.About.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("About agg-sharp Content");
			await Assert.That(window.FindDescendant("About Markdown")).IsSameReferenceAs(window.Markdown);
			await Assert.That(window.Markdown.PlainText).Contains("AGG Demos");
			await Assert.That(window.Markdown.PlainText).Contains("GUI Demo");
			await Assert.That(window.FindDescendant("About Repository Link")).IsNotNull();
			await Assert.That(window.Markdown.Descendants<TextWidget>().First().TextColor).IsEqualTo(DemoPalette.Light.TextColor);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());

			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.Markdown.Descendants<TextWidget>().First().TextColor).IsEqualTo(DemoPalette.Dark.TextColor);
			await Assert.That(window.FindDescendant("About Repository Link")).IsNotNull();
			window.OnDraw(image.NewGraphics2D());

			// Closed, it stops following the theme (which outlives it).
			window.Close();
			demoTheme.SetPreference(ThemePreference.Light);
		}

		[Test]
		public async Task ClickingTheRepositoryLinkOpensIt()
		{
			var saved = MarkdownWidget.LaunchBrowser;
			try
			{
				string launched = null;
				MarkdownWidget.LaunchBrowser = url => launched = url;

				var window = new SystemWindow(GuiDemoSpecs.About.DefaultWidth, GuiDemoSpecs.About.DefaultHeight) { Name = "About Test Window" };
				window.AddChild(GuiDemoSpecs.CreateContent(GuiDemoSpecs.About, new DemoTheme(ThemePreference.Light)));

				await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
				{
					// The link is the last line, below the fold of the window's default size, as a reader finds it.
					testRunner.ScrollIntoView("About Repository Link");
					testRunner.ClickByName("About Repository Link");
					testRunner.WaitFor(() => launched != null);

					await Assert.That(launched).IsEqualTo(AboutWindow.RepositoryUrl);
					testRunner.MarkTestComplete();
				});
			}
			finally
			{
				MarkdownWidget.LaunchBrowser = saved;
			}
		}
	}
}
