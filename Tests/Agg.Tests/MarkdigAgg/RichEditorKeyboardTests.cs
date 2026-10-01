/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using System.Text;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	public class RichEditorKeyboardTests
	{
		// Windows bindings unless a test asks for mac, set per editor so no test touches the shared static.
		private static RichMarkdownEditWidget Make(string markdown, double width = 400, double height = 300, bool mac = false)
		{
			var container = new GuiWidget(width, height, SizeLimitsToSet.None);
			var editor = new RichMarkdownEditWidget(new ThemeConfig())
			{
				Markdown = markdown,
				UseMacKeyBindings = mac,
			};
			container.AddChild(editor);
			container.PerformLayout();
			return editor;
		}

		private static void Type(RichMarkdownEditWidget editor, string text)
		{
			foreach (char c in text)
			{
				editor.OnKeyPress(new KeyPressEventArgs(c));
			}
		}

		private static KeyEventArgs Key(RichMarkdownEditWidget editor, Keys keys)
		{
			var e = new KeyEventArgs(keys);
			editor.OnKeyDown(e);
			return e;
		}

		private static void Caret(RichMarkdownEditWidget editor, int block, int offset, int row = 0, int column = 0)
		{
			editor.Selection = RichSelection.At(new DocPosition(block, offset, row, column));
		}

		[Test]
		public async Task TypingASentenceAndEnterSplits()
		{
			var editor = Make("");
			Type(editor, "Hello world");
			Key(editor, Keys.Enter);
			Type(editor, "Next");
			await Assert.That(editor.Markdown).IsEqualTo("Hello world\n\nNext");
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(1, 4));
		}

		[Test]
		public async Task HashSpaceMakesAHeadingAndOneUndoKeepsTheTypedMarker()
		{
			var editor = Make("");
			Type(editor, "# ");
			await Assert.That(editor.Document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Heading);
			Type(editor, "Title");
			await Assert.That(editor.Markdown).IsEqualTo("# Title");

			Key(editor, Keys.Z | Keys.Control);
			Key(editor, Keys.Z | Keys.Control);
			await Assert.That(editor.Markdown).IsEqualTo("\\# ");
			await Assert.That(editor.Document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Paragraph);

			Key(editor, Keys.Z | Keys.Shift | Keys.Control);
			await Assert.That(editor.Document.Blocks[0].Kind).IsEqualTo(RichBlockKind.Heading);
		}

		[Test]
		public async Task BackspaceAndDeleteJoinBlocks()
		{
			var editor = Make("one\n\ntwo\n");
			Caret(editor, 1, 0);
			Key(editor, Keys.Back);
			await Assert.That(editor.Markdown).IsEqualTo("onetwo\n");
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(0, 3));

			Key(editor, Keys.Delete);
			await Assert.That(editor.Markdown).IsEqualTo("onewo\n");

			// Backspace at the document's start changes nothing and leaves no empty undo step.
			int steps = editor.History.UndoCount;
			Caret(editor, 0, 0);
			Key(editor, Keys.Back);
			await Assert.That(editor.History.UndoCount).IsEqualTo(steps);
		}

		[Test]
		public async Task DeleteRemovesASelectedRawBlockWhole()
		{
			var editor = Make("before\n\n<table><tr><td>x</td></tr></table>\n\nafter\n");
			int raw = editor.Document.Blocks.FindIndex(b => b.Kind == RichBlockKind.Raw);
			await Assert.That(raw).IsEqualTo(1);

			editor.Selection = RichEditOperations.WholeBlock(editor.Document, raw);
			Key(editor, Keys.Delete);
			await Assert.That(editor.Document.Blocks.Any(b => b.Kind == RichBlockKind.Raw)).IsFalse();
			await Assert.That(editor.Document.Blocks.Count).IsEqualTo(2);
		}

		[Test]
		public async Task WordBackspaceDeletesAWord()
		{
			var editor = Make("hello world");
			Caret(editor, 0, 11);
			Key(editor, Keys.Back | Keys.Control);
			await Assert.That(editor.Markdown).IsEqualTo("hello ");
		}

		[Test]
		public async Task BoldOverASelectionAndAtACaret()
		{
			var editor = Make("make bold");
			editor.Selection = new RichSelection(new DocPosition(0, 5), new DocPosition(0, 9));
			Key(editor, Keys.B | Keys.Control);
			await Assert.That(editor.Markdown).IsEqualTo("make **bold**");

			var plain = Make("a");
			Caret(plain, 0, 1);
			Key(plain, Keys.B | Keys.Control);
			Type(plain, "bc");
			await Assert.That(plain.Markdown).IsEqualTo("a**bc**");

			// The pending style goes when the caret moves.
			Key(plain, Keys.I | Keys.Control);
			Key(plain, Keys.Left);
			await Assert.That(plain.PendingStyle.IsEmpty).IsTrue();
		}

		[Test]
		public async Task ArrowsStepOverAnEmojiWhole()
		{
			var editor = Make("a\U0001F600b");
			Caret(editor, 0, 1);
			Key(editor, Keys.Right);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(3);
			Key(editor, Keys.Left);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(1);

			// Right at a block's end goes to the next block's start.
			var two = Make("a\n\nb");
			Caret(two, 0, 1);
			Key(two, Keys.Right);
			await Assert.That(two.Selection.Caret).IsEqualTo(new DocPosition(1, 0));
		}

		[Test]
		public async Task UpAndDownKeepTheColumnAcrossAShortLine()
		{
			var editor = Make("same words on this line\n\nab\n\nsame words on this line\n");
			Caret(editor, 0, 15);
			Key(editor, Keys.Down);
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(1, 2));
			Key(editor, Keys.Down);
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(2, 15));
			Key(editor, Keys.Up);
			Key(editor, Keys.Up);
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(0, 15));
		}

		[Test]
		public async Task UpAndDownMoveThroughAWrappedParagraph()
		{
			var editor = Make(string.Join(" ", Enumerable.Repeat("word", 40)), width: 200);
			var layout = (RichBlockLayout)editor.BlockLayout(0);
			await Assert.That(layout.Lines.Count).IsGreaterThan(2);

			Caret(editor, 0, 2);
			Key(editor, Keys.Down);
			var caret = editor.Selection.Caret.Offset;
			await Assert.That(caret).IsGreaterThanOrEqualTo(layout.Lines[1].Start);
			await Assert.That(caret).IsLessThan(layout.Lines[2].Start);
			Key(editor, Keys.Up);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(2);
		}

		[Test]
		public async Task HomeAndEndGoToTheDrawnLinesEnds()
		{
			var editor = Make(string.Join(" ", Enumerable.Repeat("word", 40)), width: 200);
			var line = ((RichBlockLayout)editor.BlockLayout(0)).Lines[1];
			Caret(editor, 0, line.Start + 2);
			Key(editor, Keys.Home);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(line.Start);
			Key(editor, Keys.End);
			int end = editor.Selection.Caret.Offset;
			await Assert.That(end).IsGreaterThan(line.Start + 2);
			await Assert.That(end).IsLessThanOrEqualTo(line.End);

			// Shift extends from where the caret was.
			Key(editor, Keys.Home | Keys.Shift);
			await Assert.That(editor.Selection.Anchor.Offset).IsEqualTo(end);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(line.Start);
		}

		[Test]
		public async Task ShiftArrowSelectsAndMacCommandArrowGoesToTheLineEnd()
		{
			var editor = Make("abc def", mac: true);
			Caret(editor, 0, 0);
			Key(editor, Keys.Right | Keys.Shift);
			Key(editor, Keys.Right | Keys.Shift);
			await Assert.That(editor.Selection).IsEqualTo(new RichSelection(new DocPosition(0, 0), new DocPosition(0, 2)));

			// On mac Option steps words and Command goes to the line's end.
			Caret(editor, 0, 0);
			Key(editor, Keys.Right | Keys.Alt);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(4);
			Key(editor, Keys.Right | Keys.Control);
			await Assert.That(editor.Selection.Caret.Offset).IsEqualTo(7);
		}

		[Test]
		public async Task SelectAllThenTypingReplacesEverything()
		{
			var editor = Make("# Title\n\nbody text\n");
			Key(editor, Keys.A | Keys.Control);
			Type(editor, "x");
			await Assert.That(editor.Document.Blocks.Count).IsEqualTo(1);
			await Assert.That(editor.Document.Blocks[0].TextLength()).IsEqualTo(1);
		}

		[Test]
		public async Task TabMovesTableCellsAndIndentsListItems()
		{
			var table = Make("| a | b |\n|---|---|\n| c | d |\n");
			Caret(table, 0, 0);
			var e = Key(table, Keys.Tab);
			await Assert.That(e.Handled).IsTrue();
			await Assert.That(table.Selection.Caret).IsEqualTo(new DocPosition(0, 1, 0, 1));
			Key(table, Keys.Tab | Keys.Shift);
			await Assert.That(table.Selection.Caret).IsEqualTo(new DocPosition(0, 1, 0, 0));

			var list = Make("- one\n- two\n");
			Caret(list, 1, 0);
			Key(list, Keys.Tab);
			await Assert.That(list.Document.Blocks[1].List.Depth).IsEqualTo(1);

			// In a paragraph Tab is left for focus traversal.
			var paragraph = Make("text");
			Caret(paragraph, 0, 0);
			await Assert.That(Key(paragraph, Keys.Tab).Handled).IsFalse();
			await Assert.That(paragraph.Markdown).IsEqualTo("text");
		}

		[Test]
		public async Task UndoAndRedoRestoreTextAndCaret()
		{
			var editor = Make("start");
			Caret(editor, 0, 5);
			// " more" is two undo steps, as in most editors: the space, then the word.
			Type(editor, " more");
			Key(editor, Keys.Z | Keys.Control);
			Key(editor, Keys.Z | Keys.Control);
			await Assert.That(editor.Markdown).IsEqualTo("start");
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(0, 5));

			Key(editor, Keys.Y | Keys.Control);
			Key(editor, Keys.Y | Keys.Control);
			await Assert.That(editor.Markdown).IsEqualTo("start more");
			await Assert.That(editor.Selection.Caret).IsEqualTo(new DocPosition(0, 10));
		}

		[Test]
		public async Task ListChildrenLiftedByAnEditAreLaidOutAtTheirNewDepth()
		{
			// Backspace at the parent's start takes it out of the list; its children lift a level without the
			// edit naming them, and must not keep their indented layouts.
			var editor = Make("- a\n  - b\n  - c\n\npara\n");
			double childIndent = ((RichBlockLayout)editor.BlockLayout(1)).TextLeft;
			var fresh = Make("- b\n- c\n");
			double topIndent = ((RichBlockLayout)fresh.BlockLayout(0)).TextLeft;
			await Assert.That(childIndent).IsGreaterThan(topIndent);

			Caret(editor, 0, 0);
			Key(editor, Keys.Back);
			await Assert.That(editor.Document.Blocks[1].List.Depth).IsEqualTo(0);
			await Assert.That(((RichBlockLayout)editor.BlockLayout(1)).TextLeft).IsEqualTo(topIndent);
			await Assert.That(((RichBlockLayout)editor.BlockLayout(2)).TextLeft).IsEqualTo(topIndent);

			// Tab on an item with a subtree moves the subtree too.
			var tree = Make("- a\n- b\n  - c\n");
			Caret(tree, 1, 0);
			Key(tree, Keys.Tab);
			await Assert.That(tree.Document.Blocks[2].List.Depth).IsEqualTo(2);
			await Assert.That(((RichBlockLayout)tree.BlockLayout(2)).TextLeft).IsGreaterThan(((RichBlockLayout)tree.BlockLayout(1)).TextLeft);
		}

		[Test]
		public async Task AMergingBackspaceLaysOutOnlyTheMergedBlock()
		{
			var text = new StringBuilder();
			for (int i = 0; i < 30; i++)
			{
				text.Append($"Paragraph {i}.\n\n");
			}

			var editor = Make(text.ToString());
			int before = editor.LayoutCount;
			Caret(editor, 10, 0);
			Key(editor, Keys.Back);
			await Assert.That(editor.Document.Blocks.Count).IsEqualTo(29);
			await Assert.That(editor.LayoutCount - before).IsLessThanOrEqualTo(2);
		}

		[Test]
		public async Task ShiftEnterAtABlocksEndSavesNoStrayBackslash()
		{
			var editor = Make("line\n");
			Caret(editor, 0, 4);
			Key(editor, Keys.Enter | Keys.Shift);
			await Assert.That(editor.Markdown.Contains('\\')).IsFalse();

			Type(editor, "next");
			var markdown = editor.Markdown;
			var reloaded = Make(markdown);
			await Assert.That(reloaded.Document.Blocks.Count).IsEqualTo(1);
			await Assert.That(reloaded.Markdown).IsEqualTo(markdown);
			await Assert.That(reloaded.Document.Blocks[0].Inlines.OfType<InlineAtom>().Single().Kind).IsEqualTo(InlineAtomKind.HardBreak);
		}

		[Test]
		public async Task TheCaretIsScrolledIntoView()
		{
			var text = new StringBuilder();
			for (int i = 0; i < 40; i++)
			{
				text.Append($"Paragraph number {i} with a few words.\n\n");
			}

			var editor = Make(text.ToString());
			Key(editor, Keys.End | Keys.Control);
			await Assert.That(editor.ScrollOffsetFromTop()).IsGreaterThan(0);
			var view = editor.DocumentView;
			var visible = view.TransformFromParentSpace(editor, editor.LocalBounds);
			int last = editor.Document.Blocks.Count - 1;
			var caret = editor.BlockLayout(last).CaretRect(new RichCaret(editor.Selection.Caret.Offset));
			await Assert.That(caret.Bottom + editor.BlockOrigin(last)).IsGreaterThanOrEqualTo(visible.Bottom - .001);
			await Assert.That(caret.Top + editor.BlockOrigin(last)).IsLessThanOrEqualTo(visible.Top + .001);

			Key(editor, Keys.Home | Keys.Control);
			await Assert.That(editor.ScrollOffsetFromTop()).IsEqualTo(0);
		}
	}
}
