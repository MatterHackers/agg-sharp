/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The code editor's editing rules, on <see cref="CodeDocument"/> alone - no widget, no drawing.</summary>
	public class CodeDocumentTests
	{
		private const string Emoji = "\U0001F600";

		[Test]
		public async Task LinesAreSplitOnNewlinesAndLineEndingsNormalised()
		{
			var document = new CodeDocument("ab\r\ncd\ref\n");
			await Assert.That(document.Text).IsEqualTo("ab\ncd\nef\n");
			await Assert.That(document.LineCount).IsEqualTo(4);
			await Assert.That(document.LineText(1)).IsEqualTo("cd");
			await Assert.That(document.LineText(3)).IsEqualTo("");
			await Assert.That(document.LineOf(2)).IsEqualTo(0).Because("a line's newline belongs to it");
			await Assert.That(document.LineOf(3)).IsEqualTo(1);
			await Assert.That(document.ColumnOf(4)).IsEqualTo(1);
		}

		[Test]
		public async Task TypingReplacesTheSelectionAndBackspaceDeleteRemoveOneChar()
		{
			var document = new CodeDocument("hello world");
			document.SetCaret(6);
			document.SetCaret(11, extend: true);
			await Assert.That(document.SelectedText).IsEqualTo("world");
			document.Insert("there");
			await Assert.That(document.Text).IsEqualTo("hello there");
			await Assert.That(document.Caret).IsEqualTo(11);

			document.Backspace();
			document.SetCaret(0);
			document.Delete();
			await Assert.That(document.Text).IsEqualTo("ello ther");
		}

		[Test]
		public async Task ArrowsBackspaceAndDeleteTreatASurrogatePairAsOneCharacter()
		{
			var document = new CodeDocument("a" + Emoji + "b");
			document.SetCaret(1);
			document.MoveRight(extend: false);
			await Assert.That(document.Caret).IsEqualTo(3).Because("right steps over both halves");
			document.MoveLeft(extend: false);
			await Assert.That(document.Caret).IsEqualTo(1);

			// A caret put inside the pair snaps to its start.
			document.SetCaret(2);
			await Assert.That(document.Caret).IsEqualTo(1);

			document.Delete();
			await Assert.That(document.Text).IsEqualTo("ab");
			document.Insert(Emoji);
			document.Backspace();
			await Assert.That(document.Text).IsEqualTo("ab");
		}

		[Test]
		public async Task UpDownKeepTheColumnThroughShortLines()
		{
			var document = new CodeDocument("abcdef\nab\nabcdef");
			document.SetCaret(5);
			document.MoveLines(1, extend: false);
			await Assert.That(document.Caret).IsEqualTo(9).Because("the short line's end");
			document.MoveLines(1, extend: false);
			await Assert.That(document.ColumnOf(document.Caret)).IsEqualTo(5).Because("the column comes back");

			// Past the last line goes to the end; past the first to the start (Page Up/Down use the same move).
			document.MoveLines(10, extend: false);
			await Assert.That(document.Caret).IsEqualTo(document.Text.Length);
			document.MoveLines(-10, extend: true);
			await Assert.That(document.Caret).IsEqualTo(0);
			await Assert.That(document.SelectedText).IsEqualTo(document.Text);
		}

		[Test]
		public async Task HomeTogglesBetweenIndentAndColumnZeroAndEndGoesToLineEnd()
		{
			var document = new CodeDocument("x\n    let a;");
			document.SetCaret(document.Text.Length);
			document.MoveHome(extend: false);
			await Assert.That(document.ColumnOf(document.Caret)).IsEqualTo(4);
			document.MoveHome(extend: false);
			await Assert.That(document.ColumnOf(document.Caret)).IsEqualTo(0);
			document.MoveEnd(extend: true);
			await Assert.That(document.SelectedText).IsEqualTo("    let a;");
		}

		[Test]
		public async Task EnterKeepsTheIndentAndTabIndentsOrOutdents()
		{
			var document = new CodeDocument("    a");
			document.SetCaret(5);
			document.InsertNewLine();
			await Assert.That(document.Text).IsEqualTo("    a\n    ");

			// Tab goes to the next tab stop; Shift+Tab takes one level off.
			document.Text = "ab";
			document.SetCaret(1);
			document.Indent();
			await Assert.That(document.Text).IsEqualTo("a   b");
			document.Outdent();
			await Assert.That(document.Text).IsEqualTo("a   b").Because("no leading spaces to take");

			// Over several lines, each line moves and all stay selected.
			document.Text = "a\n  b\nc";
			document.SetCaret(0);
			document.SetCaret(5, extend: true);
			document.Indent();
			await Assert.That(document.Text).IsEqualTo("    a\n      b\nc");
			await Assert.That(document.SelectedText).IsEqualTo("    a\n      b");
			document.Outdent();
			await Assert.That(document.Text).IsEqualTo("a\n  b\nc");
		}

		[Test]
		public async Task ArrowsWithoutShiftCollapseTheSelectionToItsEdge()
		{
			var document = new CodeDocument("abcdef");
			document.SetCaret(2);
			document.SetCaret(4, extend: true);
			document.MoveLeft(extend: false);
			await Assert.That(document.Caret).IsEqualTo(2);
			await Assert.That(document.HasSelection).IsFalse();
			document.SelectAll();
			document.MoveRight(extend: false);
			await Assert.That(document.Caret).IsEqualTo(6);
		}

		[Test]
		public async Task UndoRedoRestoresTextAndCaretAndGroupsTyping()
		{
			var document = new CodeDocument("x");
			document.SetCaret(1);
			foreach (char c in "abc")
			{
				document.Insert(c.ToString());
			}

			document.Insert(" ");
			document.Insert("d");
			await Assert.That(document.Text).IsEqualTo("xabc d");

			// A run of typing undoes as one step; a space starts a new word, and so a new step.
			await Assert.That(document.Undo()).IsTrue();
			await Assert.That(document.Text).IsEqualTo("xabc");
			document.Undo();
			await Assert.That(document.Text).IsEqualTo("x");
			await Assert.That(document.Caret).IsEqualTo(1);
			await Assert.That(document.Undo()).IsFalse().Because("setting Text starts the history");

			await Assert.That(document.Redo()).IsTrue();
			await Assert.That(document.Text).IsEqualTo("xabc");
			await Assert.That(document.Caret).IsEqualTo(4);
			document.Redo();
			await Assert.That(document.Text).IsEqualTo("xabc d");
			await Assert.That(document.Redo()).IsFalse();

			// Backspaces group; a caret move between edits breaks the group; a new edit drops the redo.
			document.Backspace();
			document.Backspace();
			document.SetCaret(0);
			document.Delete();
			await Assert.That(document.Text).IsEqualTo("abc");
			document.Undo();
			await Assert.That(document.Text).IsEqualTo("xabc");
			document.Undo();
			await Assert.That(document.Text).IsEqualTo("xabc d");
			document.Undo();
			document.Insert("q");
			await Assert.That(document.Redo()).IsFalse();
		}

		[Test]
		public async Task UndoRestoresTheSelectionAnIndentReplaced()
		{
			var document = new CodeDocument("a\nb");
			document.SelectAll();
			document.Indent();
			document.Undo();
			await Assert.That(document.Text).IsEqualTo("a\nb");
			await Assert.That(document.SelectedText).IsEqualTo("a\nb");
		}

		[Test]
		public async Task WordMovesAndDeletesSkipSpaceThenAWord()
		{
			var document = new CodeDocument("let  foo.bar = 1;");
			document.MoveWord(1, extend: false);
			await Assert.That(document.Caret).IsEqualTo(3);
			document.MoveWord(1, extend: true);
			await Assert.That(document.SelectedText).IsEqualTo("  foo.bar").Because("agg-gui's word is a whitespace-delimited token");
			document.MoveWord(-1, extend: false);
			await Assert.That(document.Caret).IsEqualTo(5);
			document.MoveWord(-1, extend: false);
			await Assert.That(document.Caret).IsEqualTo(0);

			document.SetCaret(12);
			document.DeleteWord(-1);
			await Assert.That(document.Text).IsEqualTo("let   = 1;");
			document.DeleteWord(1);
			await Assert.That(document.Text).IsEqualTo("let   1;");
			document.Undo();
			await Assert.That(document.Text).IsEqualTo("let   = 1;");

			// A selection is deleted whole, whichever way.
			document.SetCaret(0);
			document.SetCaret(2, extend: true);
			document.DeleteWord(1);
			await Assert.That(document.Text).IsEqualTo("t   = 1;");
		}

		[Test]
		public async Task WordAndLineSelectionAndGranularDragExtension()
		{
			var document = new CodeDocument("one two_2.x\nnext line");
			document.SelectWordAt(5);
			await Assert.That(document.SelectedText).IsEqualTo("two_2").Because("a double-click word is letters, digits and underscore");
			document.SelectWordAt(9);
			await Assert.That(document.SelectedText).IsEqualTo(".");
			document.SelectLineAt(14);
			await Assert.That(document.SelectedText).IsEqualTo("next line");

			// Dragging from a double-clicked word grows by whole words either way, keeping the word selected.
			document.SelectWordAt(5);
			document.ExtendSelection(1, SelectionUnit.Word, 4, 9);
			await Assert.That(document.SelectedText).IsEqualTo("one two_2");
			document.ExtendSelection(15, SelectionUnit.Line, 0, 11);
			await Assert.That(document.SelectedText).IsEqualTo("one two_2.x\nnext line");
		}
	}
}
