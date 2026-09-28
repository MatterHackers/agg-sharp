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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Undo Redo window (agg-gui's text_demos/dialogs/basic.rs undo_redo).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class UndoRedoWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Undo Redo");

		[Test]
		public async Task BuildsTheControlsWithNothingToUndo()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (UndoRedoWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Undo Redo Content");
			await Assert.That(window.FindDescendant("Undo Redo Checkbox")).IsSameReferenceAs(window.CheckBox);
			await Assert.That(window.FindDescendant("Undo Redo Text")).IsSameReferenceAs(window.TextField);
			await Assert.That(window.FindDescendant("Undo Redo Undo")).IsSameReferenceAs(window.UndoButton);
			await Assert.That(window.FindDescendant("Undo Redo Redo")).IsSameReferenceAs(window.RedoButton);
			await Assert.That(window.State).IsEqualTo(new UndoRedoState(false, UndoRedoWindow.InitialText));
			await Assert.That(window.UndoButton.Enabled).IsFalse();
			await Assert.That(window.RedoButton.Enabled).IsFalse();

			// The heading and checkbox (Absolute children) sit inside the window's padding, as the text field does.
			foreach (GuiWidget child in new GuiWidget[] { window.Children[0], window.CheckBox, window.TextField })
			{
				// At least the padding (the text field's frame draws a unit further in); they touched the edge before.
				await Assert.That(child.TransformToScreenSpace(child.LocalBounds).Left).IsBetween(window.DevicePadding.Left, window.DevicePadding.Left + 1)
					.Because($"'{child.Name}' sits inside the window's padding");
			}

			// agg-gui's ⟲ / ⟳ prefixes, drawn as Font Awesome glyphs beside the labels.
			await Assert.That(window.UndoButton.Text).IsEqualTo("Undo");
			await Assert.That(window.RedoButton.Text).IsEqualTo("Redo");
			await Assert.That(window.UndoButton.ImageWidget.Image.Width).IsGreaterThan(0);
			await Assert.That(window.RedoButton.ImageWidget.Image.Width).IsGreaterThan(0);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task UndoRevertsBothControlsAndRedoRestoresThem()
		{
			var window = (UndoRedoWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));

			// Settle quickly so the test waits on the poll, not on egui's one-second default.
			window.Undoer.StableTime = 0.05;
			var systemWindow = new SystemWindow(420, 360) { Name = "Undo Redo Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				// Both edits in one burst, so they coalesce into one undo point.
				UiThread.RunOnIdle(() =>
				{
					window.CheckBox.Checked = true;
					window.TextField.Text = "edited";
				});
				var edited = new UndoRedoState(true, "edited");
				testRunner.WaitFor(() => window.State == edited);

				// The undo button lights at once; the undo point is kept once the state settles.
				testRunner.WaitFor(() => window.UndoButton.Enabled && !window.Undoer.IsInFlux);
				testRunner.ClickByName("Undo Redo Undo");
				testRunner.WaitFor(() => window.State != edited);
				await Assert.That(window.State).IsEqualTo(new UndoRedoState(false, UndoRedoWindow.InitialText));
				await Assert.That(window.RedoButton.Enabled).IsTrue();

				testRunner.ClickByName("Undo Redo Redo");
				testRunner.WaitFor(() => window.State == edited);
				await Assert.That(window.RedoButton.Enabled).IsFalse();
				testRunner.MarkTestComplete();
			});
		}
	}
}
