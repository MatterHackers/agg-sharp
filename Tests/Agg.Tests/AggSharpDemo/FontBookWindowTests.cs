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
using MatterHackers.Agg.UI.Tests;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Font Book window (agg-gui's demo-ui/src/windows/font_book).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current), nameof(Clipboard) })]
	public class FontBookWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Font Book");

		private static (FontBookWindow Window, GuiWidget Host) Hosted(DemoTheme demoTheme)
		{
			var window = (FontBookWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth * GuiWidget.DeviceScale, Spec.DefaultHeight * GuiWidget.DeviceScale);
			host.AddChild(window);
			host.PerformLayout();
			return (window, host);
		}

		[Test]
		public async Task ListsTheFontsGlyphsAndDrawsOnlyAScreenful()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var (window, _) = Hosted(demoTheme);

			await Assert.That(window.Name).IsEqualTo("Font Book Content");
			foreach (string name in new[] { "Font Book Source Link", "Font Book Count", "Font Book Font", "Font Book Filter", "Font Book Clear Filter", "Font Book Scroll", "Font Book Grid" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			FontBookGlyphGrid grid = window.Grid;
			await Assert.That(grid.CharacterCount).IsGreaterThan(200);
			await Assert.That(grid.Shown).Contains((int)'A');
			await Assert.That(grid.Shown).DoesNotContain((int)' ');
			await Assert.That(window.CountLabel.Text).IsEqualTo($"The selected font supports {grid.CharacterCount} characters.");

			// The grid is as tall as all its rows, but a draw paints only the rows the viewport shows.
			int rows = (grid.Shown.Count + grid.Columns - 1) / grid.Columns;
			await Assert.That(grid.Height).IsEqualTo(rows * (FontBookGlyphGrid.CellSize + FontBookGlyphGrid.CellGap) * GuiWidget.DeviceScale).Within(.001);
			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			await Assert.That(grid.CellsDrawn).IsGreaterThan(0);
			await Assert.That(grid.CellsDrawn).IsLessThan(grid.Shown.Count / 4);

			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Dark.PanelFill);
			await Assert.That(window.CountLabel.TextColor).IsEqualTo(DemoPalette.Dark.TextColor);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task FiltersByCharacterOrHexAndSwitchesToTheIconAndEmojiFonts()
		{
			var (window, _) = Hosted(new DemoTheme(ThemePreference.Light));
			FontBookGlyphGrid grid = window.Grid;

			window.FilterField.Text = "20AC";
			await Assert.That(grid.Shown.SequenceEqual(new[] { 0x20AC })).IsTrue();
			window.FilterField.Text = "a";
			await Assert.That(grid.Shown).Contains((int)'A');
			await Assert.That(grid.Shown).Contains((int)'a');
			window.FilterField.Text = "no such glyph";
			await Assert.That(grid.Shown.Count).IsEqualTo(0);
			window.ClearFilterButton.InvokeClick();
			await Assert.That(window.FilterField.Text).IsEqualTo(string.Empty);
			await Assert.That(grid.Shown.Count).IsEqualTo(grid.CharacterCount);

			window.FontPicker.SelectedIndex = 2;
			await Assert.That(grid.TypeFace).IsSameReferenceAs(IconFont.TypeFace);
			await Assert.That(grid.Shown).Contains(0xF067);
			window.FontPicker.SelectedIndex = 3;
			await Assert.That(grid.Shown).Contains(0x1F493);
			await Assert.That(window.CountLabel.Text).IsEqualTo($"The selected font supports {grid.CharacterCount} characters.");
			await Assert.That(grid.Tooltip(0x1F493)).StartsWith("💓\nHex: U+1F493\nAdvance: ");
		}

		[Test]
		public async Task ClickingAGlyphSelectsAndCopiesIt()
		{
			var saved = Clipboard.Instance;
			var clipboard = new SimulatedClipboard();
			Clipboard.SetSystemClipboard(clipboard);
			try
			{
				var window = new SystemWindow(Spec.DefaultWidth, Spec.DefaultHeight) { Name = "Font Book Test Window" };
				var content = (FontBookWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
				window.AddChild(content);
				FontBookGlyphGrid grid = content.Grid;

				await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
				{
					// Filtered to the euro sign, the grid is one row and its only cell is at the lower left.
					testRunner.ClickByName("Font Book Filter");
					testRunner.Type("20AC");
					testRunner.WaitFor(() => grid.Shown.Count == 1);
					testRunner.ClickByName("Font Book Grid", offset: new Point2D(5, 5), origin: AutomationRunner.ClickOrigin.LowerLeft);
					testRunner.WaitFor(() => grid.SelectedCodePoint != null);

					await Assert.That(grid.SelectedCodePoint).IsEqualTo(0x20AC);
					await Assert.That(clipboard.GetText()).IsEqualTo("\u20AC");
					testRunner.MarkTestComplete();
				});
			}
			finally
			{
				Clipboard.SetSystemClipboard(saved);
			}
		}
	}
}
