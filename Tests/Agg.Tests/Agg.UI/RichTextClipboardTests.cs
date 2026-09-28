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
using MatterHackers.Agg.Tests;
using MatterHackers.Agg.UI.RichText;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's rich_clipboard and styled_clipboard_tests: styled copy/paste between RichTextEdits in one process.
	[NotInParallel(new[] { SharedStateKeys.Clipboard })] // Clipboard.Instance and the RichClipboard slot are process-wide
	public class RichTextClipboardTests
	{
		private static RichTextEdit Editor(RichDoc doc)
		{
			var root = new GuiWidget(300, 200);
			var editor = new RichTextEdit(doc) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			root.AddChild(editor);
			root.PerformLayout();
			return editor;
		}

		private static RichDoc StyledDoc() => new RichDoc(new[]
		{
			new Block(TextRun.Plain("plain "), new TextRun("bold", InlineStyle.Default with { Bold = true })),
			new Block(new TextRun("red", InlineStyle.Default with { TextColor = Color.Red })) { List = ListKind.Bullet },
		});

		[Test]
		public async Task MatchingNeedsTheSameTextIgnoringLineEndings()
		{
			try
			{
				RichClipboard.Set("a\nb", new[] { Block.Plain("a"), Block.Plain("b") });
				await Assert.That(RichClipboard.Matching("a\r\nb")?.Count).IsEqualTo(2);
				await Assert.That(RichClipboard.Matching("a b")).IsNull();

				// Each match is a copy, so a paste cannot change what the next paste gets.
				RichClipboard.Matching("a\nb")[0].Runs.Clear();
				await Assert.That(RichClipboard.Matching("a\nb")[0].Text).IsEqualTo("a");
				RichClipboard.Clear();
				await Assert.That(RichClipboard.Matching("a\nb")).IsNull();
			}
			finally
			{
				RichClipboard.Clear();
			}
		}

		[Test]
		public async Task ContextMenuCutsCopiesPastesAndSelectsAll()
		{
			var installed = Clipboard.Instance;
			Clipboard.SetSystemClipboard(new SimulatedClipboard());
			try
			{
				var editor = Editor(StyledDoc());
				PopupMenu.MenuItem Item(PopupMenu menu, string name) => menu.Descendants<PopupMenu.MenuItem>().Single(m => m.Name == name + " Menu Item");

				// Nothing selected and an empty clipboard: only Select All is on.
				var menu = editor.CreateContextMenu();
				await Assert.That(Item(menu, "Cut").Enabled).IsFalse();
				await Assert.That(Item(menu, "Copy").Enabled).IsFalse();
				await Assert.That(Item(menu, "Paste").Enabled).IsFalse();
				Item(menu, "Select All").InvokeClick();
				await Assert.That(editor.Core.Selection).IsEqualTo(new DocRange(default, editor.Core.Doc.EndPos));

				editor.Core.SetSelection(new DocPos(0, 6), new DocPos(0, 10));
				menu = editor.CreateContextMenu();
				Item(menu, "Cut").InvokeClick();
				await Assert.That(editor.Core.PlainText).IsEqualTo("plain \nred");

				editor.Core.SetCaret(new DocPos(1, 3));
				menu = editor.CreateContextMenu();
				await Assert.That(Item(menu, "Paste").Enabled).IsTrue();
				Item(menu, "Paste").InvokeClick();
				await Assert.That(editor.Core.PlainText).IsEqualTo("plain \nredbold");
				await Assert.That(RichTextCommands.StyleAt(editor.Core.Doc, new DocPos(1, 5)).Bold).IsTrue();
			}
			finally
			{
				RichClipboard.Clear();
				Clipboard.SetSystemClipboard(installed);
			}
		}

		[Test]
		public async Task CopyPastesStyledIntoAnotherEditorAndPlainAfterAnExternalCopy()
		{
			var installed = Clipboard.Instance;
			Clipboard.SetSystemClipboard(new SimulatedClipboard());
			try
			{
				var source = Editor(StyledDoc());
				source.Core.SetSelection(new DocPos(0, 6), new DocPos(1, 3));
				await Assert.That(source.Copy()).IsTrue();
				await Assert.That(Clipboard.Instance.GetText()).IsEqualTo("bold\nred");

				var target = Editor(new RichDoc(new[] { Block.Plain("xy") }));
				target.Core.SetCaret(new DocPos(0, 1));
				target.Paste();
				await Assert.That(target.Core.PlainText).IsEqualTo("xbold\nredy");
				await Assert.That(RichTextCommands.StyleAt(target.Core.Doc, new DocPos(0, 3)).Bold).IsTrue();
				await Assert.That(RichTextCommands.StyleAt(target.Core.Doc, new DocPos(1, 2)).TextColor).IsEqualTo(Color.Red);
				await Assert.That(target.Core.Caret).IsEqualTo(new DocPos(1, 3));

				// Paste is one undo step.
				target.Undo();
				await Assert.That(target.Core.PlainText).IsEqualTo("xy");

				// Once another app owns the clipboard, paste is plain text in the caret's style.
				Clipboard.Instance.SetText("bold");
				target.Core.SetCaret(new DocPos(0, 0));
				target.Paste();
				await Assert.That(target.Core.PlainText).IsEqualTo("boldxy");
				await Assert.That(RichTextCommands.StyleAt(target.Core.Doc, new DocPos(0, 2)).Bold).IsFalse();

				// Cut copies styled and removes the selection.
				source.Core.SetSelection(new DocPos(0, 0), new DocPos(0, 6));
				source.Cut();
				await Assert.That(source.Core.PlainText).IsEqualTo("bold\nred");
				await Assert.That(RichClipboard.Matching("plain ")).IsNotNull();
			}
			finally
			{
				RichClipboard.Clear();
				Clipboard.SetSystemClipboard(installed);
			}
		}
	}
}
