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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Code Editor and Code Example windows (agg-gui's demo-ui/src/windows/code_editor_demo.rs and
	// code_example.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class CodeWindowsTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private static (T Window, GuiWidget Host) Hosted<T>(string title, DemoTheme demoTheme)
			where T : GuiWidget
		{
			DemoSpec spec = Spec(title);
			var window = (T)GuiDemoSpecs.CreateContent(spec, demoTheme);
			var host = new GuiWidget(spec.DefaultWidth * GuiWidget.DeviceScale, spec.DefaultHeight * GuiWidget.DeviceScale);
			host.AddChild(window);
			host.PerformLayout();
			return (window, host);
		}

		[Test]
		public async Task CodeEditorFillsTheWindowHighlightsRustAndRecolours()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var (window, host) = Hosted<CodeEditorWindow>("Code Editor", demoTheme);
			await Assert.That(window.Name).IsEqualTo("Code Editor Content");
			foreach (string name in new[] { "Code Editor Description", "Code Editor Source Link", "Code Editor Editor" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			CodeEditor editor = window.Editor;
			await Assert.That(editor.Text).IsEqualTo(CodeEditorWindow.Sample);
			await Assert.That(editor.Language).IsEqualTo(SyntaxLanguage.Rust);
			await Assert.That(editor.Palette).IsEqualTo(SyntaxPalette.Light);

			// The editor takes the height the header leaves, and follows a resize.
			double before = editor.Height;
			await Assert.That(before).IsGreaterThan(host.Height / 2);
			host.Height += 200;
			host.PerformLayout();
			await Assert.That(editor.Height).IsEqualTo(before + 200).Within(.001);

			var image = new ImageBuffer((int)host.Width, (int)host.Height);
			host.OnDraw(image.NewGraphics2D());
			await Assert.That(editor.RowsDrawn).IsEqualTo(editor.Rows.RowCount);

			// Like agg-gui's TextArea it wraps: narrowed, lines take more rows and nothing scrolls sideways.
			await Assert.That(editor.WordWrap).IsTrue();
			await Assert.That(editor.AggGuiEditing).IsTrue();
			host.Width = 260 * GuiWidget.DeviceScale;
			host.PerformLayout();
			await Assert.That(editor.Rows.RowCount).IsGreaterThan(editor.Document.LineCount);
			await Assert.That(editor.MaxScrollX).IsEqualTo(0);

			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Dark.PanelFill);
			await Assert.That(editor.BackgroundColor).IsEqualTo(DemoPalette.Dark.WidgetBackground);
			await Assert.That(editor.GutterColor).IsEqualTo(DemoPalette.Dark.PanelFill);
			await Assert.That(editor.Palette).IsEqualTo(SyntaxPalette.Dark);
		}

		[Test]
		public async Task ClickingTheCodeEditorAndTypingEditsIt()
		{
			var window = new SystemWindow(Spec("Code Editor").DefaultWidth, Spec("Code Editor").DefaultHeight) { Name = "Code Editor Test Window" };
			var content = (CodeEditorWindow)GuiDemoSpecs.CreateContent(Spec("Code Editor"), new DemoTheme(ThemePreference.Light));
			window.AddChild(content);
			CodeEditor editor = content.Editor;

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				testRunner.ClickByName("Code Editor Editor");
				testRunner.WaitFor(() => editor.ContainsFocus);
				int caret = editor.Document.Caret;
				testRunner.Type("zz");
				testRunner.WaitFor(() => editor.Text.Length == CodeEditorWindow.Sample.Length + 2);

				await Assert.That(editor.Text.Substring(caret, 2)).IsEqualTo("zz");
				testRunner.MarkTestComplete();
			});
		}

		[Test]
		public async Task CodeExampleKeepsTheNameAndAgeInSync()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var (window, host) = Hosted<CodeExampleWindow>("Code Example", demoTheme);
			await Assert.That(window.Name).IsEqualTo("Code Example Content");
			foreach (string name in new[] { "Code Example Name", "Code Example Age", "Code Example Increment", "Code Example Age Readout", "Code Example Theme", "Code Example Debug Readout" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			await Assert.That(window.AgeReadout.Text).IsEqualTo("Arthur is 42");
			await Assert.That(window.DebugReadout.Text).IsEqualTo("CodeExample { name: \"Arthur\", age: 42 }");
			await Assert.That(window.AgeValue.DisplayText).IsEqualTo("42 years");
			await Assert.That(window.ThemeHeader.Expanded).IsFalse();

			window.NameField.Text = "Ford";
			window.AgeValue.Value = 43;
			await Assert.That(window.AgeReadout.Text).IsEqualTo("Ford is 43");
			await Assert.That(window.DebugReadout.Text).IsEqualTo("CodeExample { name: \"Ford\", age: 43 }");

			var image = new ImageBuffer((int)host.Width, (int)host.Height);
			host.OnDraw(image.NewGraphics2D());

			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Dark.PanelFill);
			await Assert.That(window.AgeReadout.TextColor).IsEqualTo(DemoPalette.Dark.TextColor);
		}

		[Test]
		public async Task ClickingIncrementRaisesTheAgeEverywhere()
		{
			var window = new SystemWindow(Spec("Code Example").DefaultWidth, Spec("Code Example").DefaultHeight) { Name = "Code Example Test Window" };
			var content = (CodeExampleWindow)GuiDemoSpecs.CreateContent(Spec("Code Example"), new DemoTheme(ThemePreference.Light));
			window.AddChild(content);

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				// The row is below the fold of the default window size.
				testRunner.ScrollIntoView("Code Example Increment");
				testRunner.ClickByName("Code Example Increment");
				testRunner.WaitFor(() => content.Age == 43);

				await Assert.That(content.AgeReadout.Text).IsEqualTo("Arthur is 43");
				await Assert.That(content.AgeValue.Value).IsEqualTo(43);
				testRunner.MarkTestComplete();
			});
		}
	}
}
