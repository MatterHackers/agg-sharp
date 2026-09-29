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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.RichText;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's RichTextEdit window (agg-gui's demo-ui/src/windows/rich_text_demo).
	public class RichTextEditWindowTests
	{
		private static (GuiWidget Page, RichTextEditWindow Window) Build()
		{
			var page = new GuiWidget(640, 520);
			GuiWidget content = GuiDemoSpecs.CreateContent(GuiDemoSpecs.All.First(s => s.Title == "RichTextEdit"));
			page.AddChild(content);
			page.PerformLayout();
			return (page, (RichTextEditWindow)content);
		}

		[Test]
		public async Task BuildsTheToolbarAndSeedDocument()
		{
			var (page, window) = Build();
			await Assert.That(window.Name).IsEqualTo("RichTextEdit Content");
			foreach (string tool in new[] { "Bold", "Italic", "Underline", "Strikethrough", "Highlight color", "Remove highlight", "Align left", "Align center", "Align right", "Numbered list", "Bulleted list", "Decrease indent", "Increase indent" })
			{
				await Assert.That(window.FindDescendant("RichTextEdit " + tool)).IsNotNull();
			}

			await Assert.That(window.Editor.Core.PlainText).StartsWith("Toolbar\nToggle bold");
			await Assert.That(window.Editor.Layout.Blocks[1].Marker).IsEqualTo("1.");
			await Assert.That(window.Editor.Height).IsGreaterThan(200);

			var image = new ImageBuffer(640, 520);
			page.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task ToolbarFormatsTheSelectionAndUndoRestoresIt()
		{
			var (page, window) = Build();
			var editor = window.Editor;
			editor.Focus();
			editor.Core.SetSelection(new DocPos(1, 0), new DocPos(1, 6));

			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Italic").InvokeClick();
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Style.Italic).IsTrue();
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Text).IsEqualTo("Toggle");
			await Assert.That(editor.ContainsFocus).IsTrue();

			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Bulleted list").InvokeClick();
			await Assert.That(editor.Core.Doc.Blocks[1].List).IsEqualTo(ListKind.Bullet);

			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Undo (" + (System.OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl") + "+Z)").InvokeClick();
			await Assert.That(editor.Core.Doc.Blocks[1].List).IsEqualTo(ListKind.Ordered);
			window.Descendants<ThemedIconButton>().Single(b => b.Name.StartsWith("RichTextEdit Undo")).InvokeClick();
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Style.Italic).IsFalse();
		}

		[Test]
		public async Task FamilyAndSizeCombosFormatTheSelectionAndFollowIt()
		{
			var (page, window) = Build();
			var editor = window.Editor;
			await Assert.That(window.SizeCombo.SelectedLabel).IsEqualTo("16");
			await Assert.That(window.FamilyCombo.SelectedLabel).IsEqualTo("Liberation Sans");

			editor.Core.SetSelection(new DocPos(1, 0), new DocPos(1, 6));
			window.SizeCombo.SelectedIndex = System.Array.IndexOf(RichTextEditWindow.FontSizes, 24.0);
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Style.FontSize).IsEqualTo(RichTextEditWindow.EditorSize(24));
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Text).IsEqualTo("Toggle");

			window.FamilyCombo.SelectedIndex = 0;
			editor.Core.SetSelection(new DocPos(1, 0), new DocPos(1, 6));
			window.FamilyCombo.SelectedIndex = -1;
			window.FamilyCombo.SelectedIndex = 0;
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Style.FontFamily).IsEqualTo("Liberation Sans");

			// The size combo picks in agg-gui's sizes: the heading is its 24, the body its 16.
			await Assert.That(editor.Core.Doc.Blocks[0].Runs[0].Style.FontSize).IsEqualTo(RichTextEditWindow.EditorSize(24));
			await Assert.That(RichTextEditWindow.EditorSize(16)).IsEqualTo(editor.DefaultFontSize);
		}

		[Test]
		public async Task UndoAndRedoAreGreyedOutWhenThereIsNothingToTakeBack()
		{
			var (page, window) = Build();
			var editor = window.Editor;
			await Assert.That(window.UndoButton.Enabled).IsFalse();
			await Assert.That(window.RedoButton.Enabled).IsFalse();

			editor.Core.SetSelection(new DocPos(1, 0), new DocPos(1, 6));
			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Italic").InvokeClick();
			await Assert.That(window.UndoButton.Enabled).IsTrue();
			await Assert.That(window.RedoButton.Enabled).IsFalse();

			window.UndoButton.InvokeClick();
			await Assert.That(editor.Core.Doc.Blocks[1].Runs[0].Style.Italic).IsFalse();
			await Assert.That(window.RedoButton.Enabled).IsTrue();
		}

		[Test]
		public async Task ToggleButtonsShowTheSelectionsOnState()
		{
			var (page, window) = Build();
			var editor = window.Editor;
			var bold = window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Bold");

			// The heading is regular weight, as agg-gui's; bolded, it is bold and left aligned. The list item
			// under it is numbered.
			editor.Core.SetSelection(new DocPos(0, 0), new DocPos(0, 7));
			await Assert.That(window.IsToolOn("Bold")).IsFalse();
			bold.InvokeClick();
			await Assert.That(window.IsToolOn("Bold")).IsTrue();
			await Assert.That(window.IsToolOn("Align left")).IsTrue();
			await Assert.That(window.IsToolOn("Numbered list")).IsFalse();
			var onColor = bold.BackgroundColor;

			editor.Core.SetCaret(new DocPos(1, 3));
			await Assert.That(window.IsToolOn("Bold")).IsFalse();
			await Assert.That(window.IsToolOn("Numbered list")).IsTrue();
			await Assert.That(bold.BackgroundColor).IsNotEqualTo(onColor);

			// A selection spanning bold and plain text is mixed, so Bold reads off.
			editor.Core.SetSelection(new DocPos(0, 0), new DocPos(1, 3));
			await Assert.That(window.IsToolOn("Bold")).IsFalse();

			// Formatting updates the state straight away.
			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Align center").InvokeClick();
			await Assert.That(window.IsToolOn("Align center")).IsTrue();
			await Assert.That(window.IsToolOn("Align left")).IsFalse();
			await Assert.That(window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Align center").BackgroundColor).IsEqualTo(onColor);
		}

		[Test]
		public async Task ColorDialogPreviewsLiveAndEveryWayOutSettlesIt()
		{
			var (page, window) = Build();
			var editor = window.Editor;
			editor.Core.SetSelection(new DocPos(1, 0), new DocPos(1, 6));
			Color? ColorAt() => RichTextCommands.StyleAt(editor.Core.Doc, new DocPos(1, 2)).TextColor;
			Color? HighlightAt() => RichTextCommands.StyleAt(editor.Core.Doc, new DocPos(1, 2)).Highlight;
			void PressOutside(ColorDialog d)
			{
				var overlay = (ModalOverlay)d.Parent;
				overlay.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
			}

			// The toolbar button opens agg-gui's colour wheel in a modal window; dragging previews on the selection
			// and Cancel puts the text back.
			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Text color").InvokeClick();
			var dialog = window.ColorDialog;
			await Assert.That(dialog).IsNotNull();
			await Assert.That(dialog.Parent).IsTypeOf<ModalOverlay>();
			await Assert.That(dialog.Picker.AllowNone).IsFalse();
			dialog.Picker.Color = new Color(10, 200, 30);
			await Assert.That(ColorAt()).IsEqualTo(new Color(10, 200, 30));
			await Assert.That(window.SwatchColor(highlight: false)).IsEqualTo(new Color(10, 200, 30));

			// Undo waits for the dialog: it would otherwise move the history under the preview.
			editor.Undo();
			await Assert.That(ColorAt()).IsEqualTo(new Color(10, 200, 30));
			dialog.Picker.CancelButton.InvokeClick();
			await Assert.That(ColorAt()).IsNull();
			await Assert.That(editor.Core.IsPreviewing).IsFalse();
			await Assert.That(window.ColorDialog).IsNull();
			await Assert.That(page.Descendants<ModalOverlay>().Any()).IsFalse().Because("the overlay goes with its dialog");

			// Select keeps the colour as one undo step, and the next dialog opens on it.
			dialog = window.OpenColorDialog(highlight: false);
			dialog.Picker.Color = new Color(1, 2, 3);
			dialog.Picker.Color = new Color(40, 50, 60);
			dialog.Picker.SelectButton.InvokeClick();
			await Assert.That(ColorAt()).IsEqualTo(new Color(40, 50, 60));
			await Assert.That(window.SwatchColor(highlight: false)).IsEqualTo(new Color(40, 50, 60));
			dialog = window.OpenColorDialog(highlight: false);
			await Assert.That(dialog.Picker.OriginalColor).IsEqualTo(new Color(40, 50, 60));

			// Escape puts the text back like Cancel.
			dialog.Picker.Color = new Color(9, 9, 9);
			((ModalOverlay)dialog.Parent).OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(ColorAt()).IsEqualTo(new Color(40, 50, 60));
			editor.Undo();
			await Assert.That(ColorAt()).IsNull();

			// A press outside keeps a change as one undo step...
			dialog = window.OpenColorDialog(highlight: true);
			dialog.Picker.Color = new Color(0, 0, 255, 100);
			PressOutside(dialog);
			await Assert.That(editor.Core.IsPreviewing).IsFalse();
			await Assert.That(HighlightAt()).IsEqualTo(new Color(0, 0, 255, 100));
			await Assert.That(window.SwatchColor(highlight: true)).IsEqualTo(new Color(0, 0, 255, 100));
			editor.Undo();
			await Assert.That(HighlightAt()).IsNull();

			// ...and leaves no undo step when nothing changed.
			editor.Redo();
			bool couldUndo = editor.Core.CanUndo;
			dialog = window.OpenColorDialog(highlight: true);
			PressOutside(dialog);
			await Assert.That(editor.Core.IsPreviewing).IsFalse();
			editor.Undo();
			await Assert.That(HighlightAt()).IsNull();
			await Assert.That(couldUndo).IsTrue();

			window.Descendants<ThemedIconButton>().Single(b => b.Name == "RichTextEdit Remove highlight").InvokeClick();
			await Assert.That(HighlightAt()).IsNull();
		}
	}
}
