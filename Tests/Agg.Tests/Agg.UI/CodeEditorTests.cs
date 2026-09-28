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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The CodeEditor widget: keys, mouse and wheel reach its document, and only the lines in view are drawn.</summary>
	// A press reads Shift from the global Keyboard state, which ShiftClickExtendsTheSelection (and the other keyboard
	// test classes sharing this key) hold down; serialized so no click here sees it.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class CodeEditorTests
	{
		private static (GuiWidget root, CodeEditor editor) Build(string text, double height = 120)
		{
			var root = new GuiWidget(300, height);
			var editor = new CodeEditor(text)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Language = SyntaxLanguage.Rust,
			};
			root.AddChild(editor);
			root.PerformLayout();
			editor.Focus();
			return (root, editor);
		}

		/// <summary>A key as the platform delivers it: down to the root, then the press unless suppressed.</summary>
		private static void SendKey(GuiWidget root, Keys keyDown, char keyPressed = '\0')
		{
			var down = new KeyEventArgs(keyDown);
			root.OnKeyDown(down);
			if (!down.SuppressKeyPress && keyPressed != '\0')
			{
				root.OnKeyPress(new KeyPressEventArgs(keyPressed));
			}
		}

		private static MouseEventArgs Mouse(Vector2 position, int wheel = 0) => new MouseEventArgs(MouseButtons.Left, 1, position.X, position.Y, wheel);

		[Test]
		public async Task KeysTypeEditAndMoveWithoutTabLeavingTheEditor()
		{
			(GuiWidget root, CodeEditor editor) = Build("fn main() {}");
			SendKey(root, Keys.End);
			SendKey(root, Keys.Left);
			SendKey(root, Keys.Enter, '\r');
			SendKey(root, Keys.Tab, '\t');
			foreach (char c in "x;")
			{
				SendKey(root, Keys.None, c);
			}

			await Assert.That(editor.Text).IsEqualTo("fn main() {\n    x;}");
			await Assert.That(editor.ContainsFocus).IsTrue().Because("Tab indents rather than moving the focus");

			SendKey(root, Keys.Back, '\b');
			SendKey(root, Keys.Home | Keys.Shift);
			SendKey(root, Keys.Delete);
			await Assert.That(editor.Text).IsEqualTo("fn main() {\n    }");

			// An unused Ctrl chord types nothing.
			SendKey(root, Keys.Q | Keys.Control, 'q');
			await Assert.That(editor.Text).IsEqualTo("fn main() {\n    }");
		}

		[Test]
		public async Task CopyCutAndPasteGoThroughTheClipboard()
		{
			(GuiWidget root, CodeEditor editor) = Build("let a = 1;");
			editor.SystemClipboard = new SimulatedClipboard();
			SendKey(root, Keys.A | Keys.Control, 'a');
			SendKey(root, Keys.C | Keys.Control, 'c');
			SendKey(root, Keys.End);
			SendKey(root, Keys.V | Keys.Control, 'v');
			await Assert.That(editor.Text).IsEqualTo("let a = 1;let a = 1;");

			SendKey(root, Keys.Left | Keys.Shift);
			SendKey(root, Keys.X | Keys.Control, 'x');
			await Assert.That(editor.Text).IsEqualTo("let a = 1;let a = 1");
			await Assert.That(editor.SystemClipboard.GetText()).IsEqualTo(";");
		}

		[Test]
		public async Task ClickPlacesTheCaretAndDragSelects()
		{
			(_, CodeEditor editor) = Build("abcdef\nghijkl");

			// Aim just right of a character's left edge, mid-line.
			Vector2 At(int index) => editor.PositionOfIndex(index) + new Vector2(1, editor.LineHeight / 2);
			editor.OnMouseDown(Mouse(At(2)));
			await Assert.That(editor.Document.Caret).IsEqualTo(2);
			editor.OnMouseMove(Mouse(At(10)));
			editor.OnMouseUp(Mouse(At(10)));
			await Assert.That(editor.Document.SelectedText).IsEqualTo("cdef\nghi");

			// Below the last line lands at the end of the text.
			editor.OnMouseDown(Mouse(new Vector2(editor.Width / 2, 1)));
			await Assert.That(editor.Document.Caret).IsEqualTo(editor.Text.Length);
		}

		[Test]
		public async Task OnlyTheVisibleLinesAreDrawnAndTheWheelAndCaretScroll()
		{
			string text = string.Join("\n", Enumerable.Range(1, 500).Select(i => $"let x{i} = {i}; // line {i}"));
			(GuiWidget root, CodeEditor editor) = Build(text);
			var image = new ImageBuffer(300, 120);
			editor.OnDraw(image.NewGraphics2D());
			await Assert.That(editor.FirstVisibleLine).IsEqualTo(0);
			await Assert.That(editor.RowsDrawn).IsLessThan(12).Because("a 120 pixel view shows a handful of the 500 lines");

			// One notch down scrolls three lines.
			var wheel = Mouse(new Vector2(100, 60), -120);
			editor.OnMouseWheel(wheel);
			await Assert.That(editor.ScrollY).IsEqualTo(3 * editor.LineHeight);
			await Assert.That(wheel.WheelDelta).IsEqualTo(0).Because("the editor used it");

			// Ctrl+End takes the caret, and the view, to the last line.
			SendKey(root, Keys.End | Keys.Control);
			await Assert.That(System.Math.Abs(editor.ScrollY - editor.MaxScrollY)).IsLessThan(1e-6);
			await Assert.That(editor.LastVisibleLine).IsEqualTo(499);
			editor.OnDraw(image.NewGraphics2D());
			await Assert.That(editor.RowsDrawn).IsLessThan(12);

			// Page Up moves a view's worth of lines.
			int before = editor.Document.LineOf(editor.Document.Caret);
			SendKey(root, Keys.PageUp);
			int page = before - editor.Document.LineOf(editor.Document.Caret);
			await Assert.That(page).IsGreaterThan(2);
			await Assert.That(page).IsLessThan(8);
		}

		[Test]
		public async Task UndoRedoAndWordKeysFollowThePlatformsModifiers()
		{
			(GuiWidget root, CodeEditor editor) = Build("let foo = bar;");
			editor.UseMacKeyBindings = false;
			SendKey(root, Keys.End);
			SendKey(root, Keys.Left | Keys.Control);
			await Assert.That(editor.Document.Caret).IsEqualTo(10).Because("Ctrl+Left goes back a word");
			SendKey(root, Keys.Back | Keys.Control, '\b');
			await Assert.That(editor.Text).IsEqualTo("let foo bar;");
			SendKey(root, Keys.Z | Keys.Control, 'z');
			await Assert.That(editor.Text).IsEqualTo("let foo = bar;");
			SendKey(root, Keys.Z | Keys.Control | Keys.Shift, 'Z');
			await Assert.That(editor.Text).IsEqualTo("let foo bar;");
			SendKey(root, Keys.Z | Keys.Control, 'z');
			SendKey(root, Keys.Y | Keys.Control, 'y');
			await Assert.That(editor.Text).IsEqualTo("let foo bar;");

			// On the Mac, Option is the word key and Command+arrows go to the line's ends.
			editor.UseMacKeyBindings = true;
			editor.Document.SetCaret(4);
			SendKey(root, Keys.Right | Keys.Alt | Keys.Shift);
			await Assert.That(editor.Document.SelectedText).IsEqualTo("foo");
			SendKey(root, Keys.Right | Keys.Control);
			await Assert.That(editor.Document.Caret).IsEqualTo(editor.Text.Length);
			SendKey(root, Keys.Left | Keys.Control);
			await Assert.That(editor.Document.Caret).IsEqualTo(0);
			SendKey(root, Keys.Delete | Keys.Alt);
			await Assert.That(editor.Text).IsEqualTo(" foo bar;");
		}

		[Test]
		public async Task DoubleClickSelectsAWordTripleClickALineAndADragGrowsByThem()
		{
			(_, CodeEditor editor) = Build("one two three\nfour five");
			Vector2 At(int index) => editor.PositionOfIndex(index) + new Vector2(1, editor.LineHeight / 2);
			editor.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, At(5).X, At(5).Y, 0));
			await Assert.That(editor.Document.SelectedText).IsEqualTo("two");
			editor.OnMouseMove(Mouse(At(10)));
			editor.OnMouseUp(Mouse(At(10)));
			await Assert.That(editor.Document.SelectedText).IsEqualTo("two three").Because("a drag from a double-click grows by words");

			editor.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 3, At(16).X, At(16).Y, 0));
			await Assert.That(editor.Document.SelectedText).IsEqualTo("four five");
			editor.OnMouseMove(Mouse(At(1)));
			editor.OnMouseUp(Mouse(At(1)));
			await Assert.That(editor.Document.SelectedText).IsEqualTo("one two three\nfour five");
		}

		[Test]
		public async Task ShiftClickExtendsTheSelection()
		{
			(_, CodeEditor editor) = Build("abcdef");
			Vector2 At(int index) => editor.PositionOfIndex(index) + new Vector2(1, editor.LineHeight / 2);
			editor.Document.SetCaret(1);
			Keyboard.SetKeyDownState(Keys.Shift, true);
			try
			{
				editor.OnMouseDown(Mouse(At(4)));
				editor.OnMouseUp(Mouse(At(4)));
			}
			finally
			{
				Keyboard.SetKeyDownState(Keys.Shift, false);
			}

			await Assert.That(editor.Document.SelectedText).IsEqualTo("bcd");
		}

		[Test]
		public async Task DraggingTheScrollbarThumbsScrollsWithoutMovingTheCaret()
		{
			string text = string.Join("\n", Enumerable.Range(1, 200).Select(i => new string('x', i % 90)));
			(_, CodeEditor editor) = Build(text);
			RectangleDouble thumb = editor.VerticalScrollbarThumb;
			await Assert.That(thumb.Top).IsGreaterThan(editor.Height - 20).Because("unscrolled, the thumb is at the top");

			editor.OnMouseDown(Mouse(thumb.Center));
			editor.OnMouseMove(Mouse(new Vector2(thumb.Center.X, -1000)));
			editor.OnMouseUp(Mouse(new Vector2(thumb.Center.X, -1000)));
			await Assert.That(editor.ScrollY).IsEqualTo(editor.MaxScrollY);
			await Assert.That(editor.Document.Caret).IsEqualTo(0);

			thumb = editor.HorizontalScrollbarThumb;
			editor.OnMouseDown(Mouse(thumb.Center));
			editor.OnMouseMove(Mouse(new Vector2(10000, thumb.Center.Y)));
			editor.OnMouseUp(Mouse(new Vector2(10000, thumb.Center.Y)));
			await Assert.That(editor.ScrollX).IsEqualTo(editor.MaxScrollX);
			await Assert.That(editor.Document.Caret).IsEqualTo(0);
		}

		[Test]
		public async Task WordWrapFitsRowsToTheWidthAndMovesByRow()
		{
			string longLine = "let words = alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi;";
			(GuiWidget root, CodeEditor editor) = Build(longLine + "\nshort");
			editor.WordWrap = true;
			CodeLayout rows = editor.Rows;
			await Assert.That(rows.RowCount).IsGreaterThan(3);
			await Assert.That(editor.MaxScrollX).IsEqualTo(0);
			await Assert.That(editor.HorizontalScrollbarVisible).IsFalse();
			for (int row = 0; row < rows.RowCount; row++)
			{
				CodeRow codeRow = rows.Row(row);
				await Assert.That(editor.PositionOfIndex(codeRow.End).X).IsLessThanOrEqualTo(editor.Width - CodeEditor.TextPadding * GuiWidget.DeviceScale + .001);
				await Assert.That(codeRow.FirstInLine).IsEqualTo(row == 0 || row == rows.RowCount - 1);
			}

			var image = new ImageBuffer(300, 120);
			editor.OnDraw(image.NewGraphics2D());
			await Assert.That(editor.RowsDrawn).IsEqualTo(rows.RowCount);

			// Clicks land on the wrapped row under them, and the caret draws there.
			Vector2 At(int index) => editor.PositionOfIndex(index) + new Vector2(1, editor.LineHeight / 2);
			CodeRow second = rows.Row(1);
			int inSecond = second.Start + 3;
			editor.OnMouseDown(Mouse(At(inSecond)));
			editor.OnMouseUp(Mouse(At(inSecond)));
			await Assert.That(editor.Document.Caret).IsEqualTo(inSecond);
			await Assert.That(editor.PositionOfIndex(inSecond).Y).IsEqualTo(editor.RowTop(1) - editor.LineHeight);

			// End and Home go to the row's ends; Down keeps the x on the next row.
			SendKey(root, Keys.End);
			await Assert.That(editor.Document.Caret).IsEqualTo(second.CaretEnd);
			SendKey(root, Keys.Home);
			await Assert.That(editor.Document.Caret).IsEqualTo(second.Start);
			SendKey(root, Keys.Right);
			SendKey(root, Keys.Right);
			double x = editor.PositionOfIndex(editor.Document.Caret).X;
			SendKey(root, Keys.Down);
			await Assert.That(rows.RowOfIndex(editor.Document.Caret)).IsEqualTo(2);
			await Assert.That(System.Math.Abs(editor.PositionOfIndex(editor.Document.Caret).X - x)).IsLessThan(editor.LineHeight / 2);
			SendKey(root, Keys.Up);
			await Assert.That(editor.Document.Caret).IsEqualTo(second.Start + 2);
		}

		[Test]
		public async Task WrappedViewsDrawOnlyTheRowsInView()
		{
			string text = string.Join("\n", Enumerable.Range(1, 500).Select(i => $"let x{i} = {i}; // a comment long enough to wrap over the editor's width"));
			(GuiWidget root, CodeEditor editor) = Build(text);
			editor.WordWrap = true;
			await Assert.That(editor.Rows.RowCount).IsGreaterThan(1000);
			SendKey(root, Keys.End | Keys.Control);
			await Assert.That(editor.LastVisibleLine).IsEqualTo(499);
			var image = new ImageBuffer(300, 120);
			editor.OnDraw(image.NewGraphics2D());
			await Assert.That(editor.RowsDrawn).IsLessThan(12);

			// An edit lays the rows out again.
			int before = editor.Rows.RowCount;
			editor.Document.SetCaret(0);
			editor.Document.Insert("extra words to push the first line onto one more row ");
			await Assert.That(editor.Rows.RowCount).IsGreaterThan(before);
		}

		[Test]
		public async Task AggGuiEditingTakesAggGuisHomeEnterAndTab()
		{
			(GuiWidget root, CodeEditor editor) = Build("    ab");
			editor.AggGuiEditing = true;
			SendKey(root, Keys.End);
			SendKey(root, Keys.Home);
			await Assert.That(editor.Document.Caret).IsEqualTo(0).Because("Home skips the indent stop");

			SendKey(root, Keys.End);
			SendKey(root, Keys.Enter, '\r');
			SendKey(root, Keys.Right);
			SendKey(root, Keys.Tab, '\t');
			await Assert.That(editor.Text).IsEqualTo("    ab\n    ").Because("Enter carries no indent; Tab inserts four spaces");

			SendKey(root, Keys.Left);
			SendKey(root, Keys.Tab, '\t');
			await Assert.That(editor.Text).IsEqualTo("    ab\n        ").Because("Tab is four spaces, not to the next stop");
		}

		[Test]
		public async Task TheCaretBlinksEveryHalfSecondAndShowsSolidAfterAKey()
		{
			await Assert.That(CodeEditor.CaretShowingAt(0)).IsTrue();
			await Assert.That(CodeEditor.CaretShowingAt(499)).IsTrue();
			await Assert.That(CodeEditor.CaretShowingAt(500)).IsFalse();
			await Assert.That(CodeEditor.CaretShowingAt(1000)).IsTrue();

			(GuiWidget root, CodeEditor editor) = Build("abc");
			SendKey(root, Keys.Right);
			await Assert.That(editor.CaretShowing).IsTrue();
		}
	}
}
