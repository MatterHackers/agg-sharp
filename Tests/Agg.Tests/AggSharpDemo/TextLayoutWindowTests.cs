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
	// The GUI demo's Text Layout window (agg-gui's text_demos/text_layout.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class TextLayoutWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Text Layout");

		private static (TextLayoutWindow Window, GuiWidget Host) Build(DemoTheme demoTheme)
		{
			var window = (TextLayoutWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return (window, host);
		}

		[Test]
		public async Task WrapsToTheWidthAndRecolours()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			(TextLayoutWindow window, GuiWidget _) = Build(demoTheme);
			TextLayoutPreview preview = window.Preview;

			await Assert.That(window.Name).IsEqualTo("Text Layout Content");
			await Assert.That(window.FindDescendant("Text Layout Preview")).IsSameReferenceAs(preview);
			foreach (string name in new[] { "Text Layout Max Rows", "Text Layout Line Break 1", "Text Layout Overflow 3", "Text Layout Letter Spacing", "Text Layout Custom Line Height", "Text Layout Line Height", "Text Layout Align 2", "Text Layout Justify", "Text Layout Text 1" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull().Because(name);
			}

			// Word wrapping: several rows, each within the width, and together the whole text.
			await Assert.That(preview.Lines.Count).IsGreaterThan(3);
			foreach (TextLayoutLine line in preview.Lines)
			{
				await Assert.That(preview.LineWidth(line.Text)).IsLessThanOrEqualTo(preview.ContentWidth);
			}

			await Assert.That(string.Join(" ", preview.Lines.Select(l => l.Text))).IsEqualTo(TextLayoutSettings.LoremIpsum);
			await Assert.That(preview.Height).IsGreaterThanOrEqualTo(preview.Lines.Count * preview.LineHeight);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task RowLimitCutsTheLastRowWithTheOverflowMarker()
		{
			(TextLayoutWindow window, GuiWidget _) = Build(new DemoTheme(ThemePreference.Light));
			TextLayoutPreview preview = window.Preview;

			window.Settings.MaxRows = 2;
			preview.Relayout();
			await Assert.That(preview.Lines.Count).IsEqualTo(2);
			await Assert.That(preview.Lines[1].Text).EndsWith("…");
			await Assert.That(preview.LineWidth(preview.Lines[1].Text)).IsLessThanOrEqualTo(preview.ContentWidth);

			// Breaking anywhere fills each row to the width, so the first row holds more than at word boundaries.
			int wordRow = preview.Lines[0].Text.Length;
			window.Settings.BreakAnywhere = true;
			preview.Relayout();
			await Assert.That(preview.Lines[0].Text.Length).IsGreaterThanOrEqualTo(wordRow);

			// La Pasionaria's blank lines are paragraphs of their own.
			window.Settings.MaxRows = 1000;
			window.Settings.PasionariaText = true;
			preview.Relayout();
			await Assert.That(preview.Lines[0]).IsEqualTo(new TextLayoutLine("Mothers! Women!", true));
			await Assert.That(preview.Lines[1]).IsEqualTo(new TextLayoutLine("", true));
		}

		[Test]
		public async Task ClickingAChoiceRelaysTheText()
		{
			var window = (TextLayoutWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(Spec.DefaultWidth + 200, 700) { Name = "Text Layout Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				testRunner.ClickByName("Text Layout Text 1");
				testRunner.WaitFor(() => window.Preview.Lines.Count > 0 && window.Preview.Lines[0].Text == "Mothers! Women!");
				await Assert.That(window.Settings.PasionariaText).IsTrue();

				testRunner.ClickByName("Text Layout Line Break 1");
				testRunner.WaitFor(() => window.Settings.BreakAnywhere);
				testRunner.ClickByName("Text Layout Line Break 0");
				testRunner.WaitFor(() => !window.Settings.BreakAnywhere);

				double rowHeight = window.Preview.LineHeight;
				testRunner.ClickByName("Text Layout Custom Line Height");
				testRunner.WaitFor(() => window.Preview.LineHeight != rowHeight);
				await Assert.That(window.Preview.LineHeight).IsEqualTo(20 * GuiWidget.DeviceScale);
				testRunner.MarkTestComplete();
			});
		}
	}
}
