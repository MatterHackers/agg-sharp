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
	// The GUI demo's Manual Layout Test window (agg-gui's tests/basic/layout.rs manual_layout_test).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class ManualLayoutTestWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Manual Layout Test");

		[Test]
		public async Task ChildIsPlacedFromTheTopLeftInYUpSpace()
		{
			// absolute_place.rs's own cases: a 300 tall canvas puts a 40 tall child 20 from the top at y 240, and
			// a zero offset pins the child's top to the canvas top.
			RectangleDouble rect = ManualLayoutCanvas.PlacedChildRect(300, 10, 20, 50, 40);
			await Assert.That(rect).IsEqualTo(new RectangleDouble(10, 240, 60, 280));
			await Assert.That(ManualLayoutCanvas.PlacedChildRect(200, 0, 0, 30, 30).Top).IsEqualTo(200);
		}

		[Test]
		public async Task BuildsWithAButtonAtTheDefaultRect()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (ManualLayoutTestWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, 520);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Manual Layout Test Content");
			await Assert.That(window.FindDescendant("Manual Layout Canvas")).IsSameReferenceAs(window.Canvas);
			await Assert.That(window.FindDescendant("Manual Layout X")).IsSameReferenceAs(window.XSlider.Slider);
			await Assert.That(window.WidgetType).IsEqualTo(0);
			await Assert.That(window.TypeRadios[0].Checked).IsTrue();
			await Assert.That(window.XSlider.ValueText).IsEqualTo("150");

			// Both sliders of a row share its width.
			await Assert.That(window.XSlider.Width).IsEqualTo(window.YSlider.Width);
			await Assert.That(window.XSlider.Slider.TotalWidthInPixels).IsGreaterThan(40);

			double s = GuiWidget.DeviceScale;
			GuiWidget placed = window.Canvas.Placed;
			await Assert.That(placed).IsTypeOf<ThemedTextButton>();
			RectangleDouble canvasBounds = window.Canvas.LocalBounds;
			await Assert.That(placed.BoundsRelativeToParent).IsEqualTo(new RectangleDouble(
				canvasBounds.Left + 150 * s, canvasBounds.Top - 250 * s, canvasBounds.Left + 350 * s, canvasBounds.Top - 150 * s));

			// The sliders move it live, and the Label radio swaps it.
			window.YSlider.Slider.Value = 0;
			await Assert.That(placed.BoundsRelativeToParent.Top).IsEqualTo(canvasBounds.Top);
			window.TypeRadios[1].Checked = true;
			await Assert.That(window.Canvas.Placed).IsTypeOf<TextWidget>();
			await Assert.That(window.Canvas.Children.Count).IsEqualTo(1);

			// TextEdit is agg-gui's TextArea: the plain-text CodeEditor, wrapping.
			window.TypeRadios[2].Checked = true;
			var textEdit = (CodeEditor)window.Canvas.Placed;
			await Assert.That(textEdit.WordWrap).IsTrue();
			await Assert.That(textEdit.ShowLineNumbers).IsFalse();
			await Assert.That(textEdit.Rows.RowCount).IsGreaterThan(1).Because("the default text wraps in 200 px");
			window.TypeRadios[1].Checked = true;

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			await Assert.That(((TextWidget)window.Canvas.Placed).TextColor).IsEqualTo(demoTheme.Palette.TextColor);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task EditedTextSurvivesATypeSwitchUntilReset()
		{
			var window = (ManualLayoutTestWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(460, 560) { Name = "Manual Layout Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				// The placed button is a real button wherever it sits.
				int clicks = 0;
				testRunner.WaitForName("Manual Layout Placed Button");
				window.Canvas.Placed.Click += (s, e) => clicks++;
				testRunner.ClickByName("Manual Layout Placed Button");
				testRunner.WaitFor(() => clicks == 1);
				await Assert.That(clicks).IsEqualTo(1);

				testRunner.ClickByName("Manual Layout TextEdit");
				testRunner.WaitForName("Manual Layout Placed TextEdit");
				testRunner.ClickByName("Manual Layout Placed TextEdit");
				testRunner.Type("q");
				testRunner.WaitFor(() => window.Text.Contains('q'));
				string edited = window.Text;
				await Assert.That(edited.Length).IsEqualTo(ManualLayoutTestWindow.DefaultText.Length + 1);

				testRunner.ClickByName("Manual Layout Label");
				testRunner.WaitForName("Manual Layout Placed Label");
				testRunner.ClickByName("Manual Layout TextEdit");
				testRunner.WaitForName("Manual Layout Placed TextEdit");
				await Assert.That(window.Canvas.Placed.Text).IsEqualTo(edited);

				testRunner.ClickByName("Manual Layout Reset");
				testRunner.WaitForName("Manual Layout Placed Button");
				await Assert.That(window.Text).IsEqualTo(ManualLayoutTestWindow.DefaultText);
				await Assert.That(window.TypeRadios[0].Checked).IsTrue();
				testRunner.MarkTestComplete();
			});
		}
	}
}
