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
	// The GUI demo's Input Event History window (agg-gui's tests/basic/controls.rs input_event_history).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class InputEventHistoryWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Input Event History");

		[Test]
		public async Task RepeatsCoalesceIntoTheNewestRow()
		{
			var history = new EventHistory();
			history.Add("MouseDown Left", "MouseDown Left (1, 2)");
			history.Add("MouseUp Left", "MouseUp Left");
			history.Add("MouseUp Left", "MouseUp Left");
			history.Add("MouseDown Left", "MouseDown Left (5, 6)");

			await Assert.That(history.Entries.Select(e => (e.Summary, e.Count)).ToArray())
				.IsEquivalentTo(new[] { ("MouseDown Left", 1), ("MouseUp Left", 2), ("MouseDown Left", 1) });
			await Assert.That(history.Entries[2].Full).IsEqualTo("MouseDown Left (1, 2)");
		}

		[Test]
		public async Task BuildsTheControlsWithAnEmptyHistory()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (InputEventHistoryWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Input Event History Content");
			await Assert.That(window.FindDescendant("Input Event History Include Movements")).IsSameReferenceAs(window.IncludeMovements);
			await Assert.That(window.FindDescendant("Input Event History Scroll")).IsSameReferenceAs(window.Scroll);
			await Assert.That(window.FindDescendant("Input Event History Recorder")).IsSameReferenceAs(window.Recorder);
			await Assert.That(window.IncludeMovements.Checked).IsFalse();
			await Assert.That(window.Recorder.History.Entries.Count).IsEqualTo(0);

			// The empty box still fills the scroll viewport, so all of it records.
			await Assert.That(window.Recorder.Height).IsGreaterThanOrEqualTo(window.Scroll.Height - 1);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			await Assert.That(window.IncludeMovements.TextColor).IsEqualTo(demoTheme.Theme.TextColor);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task ClicksAndKeysInTheBoxAreRecorded()
		{
			var window = (InputEventHistoryWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var systemWindow = new SystemWindow(420, 360) { Name = "Input Event History Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				testRunner.ClickByName("Input Event History Recorder");
				testRunner.WaitFor(() => window.Recorder.History.Entries.Any(e => e.Summary == "MouseUp Left"));
				await Assert.That(window.Recorder.History.Entries.Select(e => e.Summary).ToArray())
					.IsEquivalentTo(new[] { "MouseUp Left", "MouseDown Left" });

				// Movements are off by default; the click left the box focused, so it records keys.
				testRunner.Type("a");
				testRunner.WaitFor(() => window.Recorder.History.Entries.Any(e => e.Summary.StartsWith("KeyUp")));
				await Assert.That(window.Recorder.History.Entries.Any(e => e.Summary == "MouseMove")).IsFalse();
				testRunner.MarkTestComplete();
			});
		}
	}
}
