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

using System.Threading.Tasks;
using MatterHackers.Agg.UI.RichText;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's word_delete_tests and multi-click selection tests: word motion, word delete, and double/triple click.
	public class RichTextWordTests
	{
		private static RichDoc Doc() => new RichDoc(new[] { Block.Plain("hello big_world, again"), Block.Plain("next line") });

		private static (GuiWidget Root, RichTextEdit Editor) Build(bool mac)
		{
			var root = new GuiWidget(400, 200);
			var editor = new RichTextEdit(Doc()) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch, UseMacKeyBindings = mac };
			root.AddChild(editor);
			root.PerformLayout();
			editor.Focus();
			return (root, editor);
		}

		[Test]
		public async Task WordTargetsAndRanges()
		{
			var doc = Doc();
			await Assert.That(RichTextWords.WordTarget(doc, new DocPos(0, 0), 1)).IsEqualTo(new DocPos(0, 6));
			await Assert.That(RichTextWords.WordTarget(doc, new DocPos(0, 6), 1)).IsEqualTo(new DocPos(0, 17));
			await Assert.That(RichTextWords.WordTarget(doc, new DocPos(0, 17), -1)).IsEqualTo(new DocPos(0, 6));
			await Assert.That(RichTextWords.WordTarget(doc, new DocPos(0, 22), 1)).IsEqualTo(new DocPos(1, 0));
			await Assert.That(RichTextWords.WordTarget(doc, new DocPos(1, 0), -1)).IsEqualTo(new DocPos(0, 22));
			await Assert.That(RichTextWords.WordRange(doc, new DocPos(0, 8))).IsEqualTo(new DocRange(new DocPos(0, 6), new DocPos(0, 15)));
			await Assert.That(RichTextWords.WordRange(doc, new DocPos(0, 15))).IsEqualTo(new DocRange(new DocPos(0, 15), new DocPos(0, 17)));
			await Assert.That(RichTextWords.BlockRange(doc, new DocPos(1, 3))).IsEqualTo(new DocRange(new DocPos(1, 0), new DocPos(1, 9)));
		}

		[Test]
		public async Task CtrlMovesAndDeletesByWordsOptionOnMac()
		{
			var (root, editor) = Build(mac: false);
			root.OnKeyDown(new KeyEventArgs(Keys.Right | Keys.Control));
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(0, 6));
			root.OnKeyDown(new KeyEventArgs(Keys.Delete | Keys.Control));
			await Assert.That(editor.Core.PlainText).StartsWith("hello again");
			root.OnKeyDown(new KeyEventArgs(Keys.Back | Keys.Control));
			await Assert.That(editor.Core.PlainText).StartsWith("again");

			// Each word delete is its own undo step.
			editor.Undo();
			await Assert.That(editor.Core.PlainText).StartsWith("hello again");

			var (macRoot, macEditor) = Build(mac: true);
			macRoot.OnKeyDown(new KeyEventArgs(Keys.Right | Keys.Alt));
			await Assert.That(macEditor.Core.Caret).IsEqualTo(new DocPos(0, 6));
			macRoot.OnKeyDown(new KeyEventArgs(Keys.Right | Keys.Alt | Keys.Shift));
			await Assert.That(macEditor.Core.Selection).IsEqualTo(new DocRange(new DocPos(0, 6), new DocPos(0, 17)));

			// Cmd (Control on the Mac layer) + Left/Right go to the line's ends (here one line), + Up/Down to the document's.
			macRoot.OnKeyDown(new KeyEventArgs(Keys.Right | Keys.Control));
			await Assert.That(macEditor.Core.Caret).IsEqualTo(new DocPos(0, 22));
			macRoot.OnKeyDown(new KeyEventArgs(Keys.Left | Keys.Control | Keys.Shift));
			await Assert.That(macEditor.Core.Selection).IsEqualTo(new DocRange(new DocPos(0, 22), new DocPos(0, 0)));
			macRoot.OnKeyDown(new KeyEventArgs(Keys.Down | Keys.Control));
			await Assert.That(macEditor.Core.Caret).IsEqualTo(new DocPos(1, 9));
		}

		[Test]
		public async Task CmdArrowsStopAtTheWrappedLineEnds()
		{
			var root = new GuiWidget(90, 200);
			var editor = new RichTextEdit(new RichDoc(new[] { Block.Plain("alpha beta gamma delta") })) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch, UseMacKeyBindings = true };
			root.AddChild(editor);
			root.PerformLayout();
			editor.Focus();
			await Assert.That(editor.Layout.Blocks[0].Lines.Count).IsGreaterThan(1);
			var second = editor.Layout.Blocks[0].Lines[1];

			editor.Core.SetCaret(new DocPos(0, second.StartOffset + 1));
			root.OnKeyDown(new KeyEventArgs(Keys.Left | Keys.Control));
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(0, second.StartOffset));
			root.OnKeyDown(new KeyEventArgs(Keys.Right | Keys.Control));
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(0, second.EndOffset));

			// The caret at a line's end stays on that line, drawn after its last word.
			await Assert.That(editor.CaretBounds(editor.Core.Caret).Left).IsGreaterThan(editor.CaretBounds(new DocPos(0, second.StartOffset)).Left);
		}

		[Test]
		public async Task DoubleClickSelectsAWordAndTripleClickAParagraph()
		{
			var (root, editor) = Build(mac: false);
			var inWorld = editor.CaretBounds(new DocPos(0, 8));
			var click2 = new MouseEventArgs(MouseButtons.Left, 2, inWorld.Left + 1, inWorld.Center.Y, 0);
			editor.OnMouseDown(click2);
			await Assert.That(editor.Core.Selection).IsEqualTo(new DocRange(new DocPos(0, 6), new DocPos(0, 15)));

			// Dragging on extends by whole words, keeping the first word selected.
			var inAgain = editor.CaretBounds(new DocPos(0, 19));
			editor.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, inAgain.Left + 1, inAgain.Center.Y, 0));
			await Assert.That(editor.Core.Selection).IsEqualTo(new DocRange(new DocPos(0, 6), new DocPos(0, 22)));
			editor.OnMouseUp(click2);

			var click3 = new MouseEventArgs(MouseButtons.Left, 3, inWorld.Left + 1, inWorld.Center.Y, 0);
			editor.OnMouseDown(click3);
			editor.OnMouseUp(click3);
			await Assert.That(editor.Core.Selection).IsEqualTo(new DocRange(new DocPos(0, 0), new DocPos(0, 22)));
		}
	}
}
