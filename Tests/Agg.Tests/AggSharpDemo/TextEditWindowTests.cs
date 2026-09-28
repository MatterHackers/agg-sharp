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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's TextEdit window (agg-gui's demo-ui/src/windows/text_edit_demo.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class TextEditWindowTests
	{
		private static DemoSpec TextEditSpec => GuiDemoSpecs.All.First(s => s.Title == "TextEdit");

		private static (GuiWidget page, TextEditWindow window) Build(double width = 420)
		{
			var page = new GuiWidget(width, 520);
			GuiWidget content = GuiDemoSpecs.CreateContent(TextEditSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (page, (TextEditWindow)content);
		}

		/// <summary>A key as the platform delivers it: down to the root, then the press unless suppressed.</summary>
		private static void SendKey(GuiWidget root, Keys keyDown, char keyPressed)
		{
			var down = new KeyEventArgs(keyDown);
			root.OnKeyDown(down);
			if (!down.SuppressKeyPress)
			{
				root.OnKeyPress(new KeyPressEventArgs(keyPressed));
			}
		}

		[Test]
		public async Task BuildsEveryControlNamedWithEguiDefaults()
		{
			(_, TextEditWindow window) = Build();
			await Assert.That(window.Name).IsEqualTo("TextEdit Content");

			string[] names =
			{
				"TextEdit Source Link", "TextEdit HAlign Left", "TextEdit HAlign Center", "TextEdit HAlign Right",
				"TextEdit VAlign Top", "TextEdit VAlign Center", "TextEdit VAlign Bottom", "TextEdit Editor",
				"TextEdit Clear", "TextEdit Selected Text", "TextEdit Cursor Start", "TextEdit Cursor End",
				"TextEdit Read Only",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			await Assert.That(window.Editor.Text).IsEqualTo("Edit this text");
			await Assert.That(window.Editor.HintText).IsEqualTo("Type something!");
			await Assert.That(window.Editor.TextHAnchor).IsEqualTo(HAnchor.Left);
			await Assert.That(window.ReadOnlyField.ReadOnly).IsTrue();
			await Assert.That(window.SelectedTextReadout).IsEqualTo("");

			// The selectors move the text; Clear empties it.
			window.HAlignRadios[2].Checked = true;
			window.VAlignRadios[2].Checked = true;
			await Assert.That(window.Editor.TextHAnchor).IsEqualTo(HAnchor.Right);
			await Assert.That(window.Editor.TextVAnchor).IsEqualTo(VAnchor.Bottom);
			window.Clear();
			await Assert.That(window.Editor.Text).IsEqualTo("");
		}

		[Test]
		public async Task TypedKeysEditAndCtrlYTogglesTheSelectedCase()
		{
			(GuiWidget page, TextEditWindow window) = Build();

			// "end" puts the caret after the text and focuses the editor; typing goes there.
			window.MoveCursor(window.Editor.Text.Length);
			SendKey(page, Keys.OemPeriod, '!');
			await Assert.That(window.Editor.Text).IsEqualTo("Edit this text!");

			// Shift+Left four times selects "ext!", which the readout shows.
			for (int i = 0; i < 4; i++)
			{
				SendKey(page, Keys.Left | Keys.Shift, '\0');
			}

			await Assert.That(window.SelectedTextReadout).IsEqualTo("ext!");

			// Ctrl+Y upper-cases it and keeps it selected, so a second press lowers it again.
			SendKey(page, Keys.Y | Keys.Control, 'y');
			await Assert.That(window.Editor.Text).IsEqualTo("Edit this tEXT!");
			await Assert.That(window.SelectedTextReadout).IsEqualTo("EXT!");
			SendKey(page, Keys.Y | Keys.Control, 'y');
			await Assert.That(window.Editor.Text).IsEqualTo("Edit this text!");
		}

		[Test]
		public async Task LongTextWrapsAndEachRowAlignsOnItsOwn()
		{
			(_, TextEditWindow window) = Build();
			CodeEditor editor = window.Editor;
			await Assert.That(editor.ShowLineNumbers).IsFalse();
			editor.Text = "short\n" + string.Join(" ", Enumerable.Repeat("words that run on", 12));
			await Assert.That(editor.Rows.RowCount).IsGreaterThan(3).Because("the long line wraps to the width");
			await Assert.That(editor.MaxScrollX).IsEqualTo(0);

			// Centred, the short first row starts further in than the full second one.
			window.HAlignRadios[1].Checked = true;
			await Assert.That(editor.PositionOfIndex(0).X).IsGreaterThan(editor.PositionOfIndex(6).X + 20);
		}

		[Test]
		public async Task IconsAreFontAwesomeGlyphsAndTheCaseHintWraps()
		{
			(_, TextEditWindow window) = Build();
			var search = (IconGlyphWidget)window.FindDescendant("TextEdit Search Icon");
			await Assert.That(search.Glyph).IsEqualTo(IconFont.Search);
			await Assert.That(window.FindDescendant("TextEdit Clear")).IsTypeOf<ThemedIconButton>();

			// In a window narrower than the hint's one line, it wraps onto more lines and stays inside.
			var hint = (WrappedTextWidget)window.FindDescendant("TextEdit Case Hint");
			(GuiWidget narrowPage, TextEditWindow narrow) = Build(220);
			var narrowHint = (WrappedTextWidget)narrow.FindDescendant("TextEdit Case Hint");
			await Assert.That(narrowHint.TextWidget.Text.Split('\n').Length).IsGreaterThan(hint.TextWidget.Text.Split('\n').Length);
			RectangleDouble text = narrowHint.TextWidget.TransformToScreenSpace(narrowHint.TextWidget.LocalBounds);
			await Assert.That(text.Right).IsLessThanOrEqualTo(narrowPage.Width);
			await Assert.That(text.Height).IsLessThanOrEqualTo(narrowHint.Height + .5).Because("every line shows, none clipped");
		}

		[Test]
		public async Task ToggleCaseFollowsEgui()
		{
			await Assert.That(TextEditWindow.ToggleCase("abc")).IsEqualTo("ABC");
			await Assert.That(TextEditWindow.ToggleCase("aBc")).IsEqualTo("ABC");
			await Assert.That(TextEditWindow.ToggleCase("ABC")).IsEqualTo("abc");
			await Assert.That(TextEditWindow.ToggleCase("12!")).IsEqualTo("12!");
		}
	}
}
