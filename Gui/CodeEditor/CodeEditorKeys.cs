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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A <see cref="CodeEditor"/>'s keys: which edit or caret move each makes on its <see cref="CodeDocument"/>, by
	/// wrapped row when <see cref="CodeEditor.WordWrap"/> is on and with agg-gui's plainer Home, Enter and Tab when
	/// <see cref="CodeEditor.AggGuiEditing"/> is.
	/// </summary>
	internal static class CodeEditorKeys
	{
		/// <summary>Applies a key that edits or moves the caret; false for any other key.</summary>
		public static bool Handle(CodeEditor editor, KeyEventArgs keyEvent)
		{
			bool shift = keyEvent.Shift;
			bool control = keyEvent.Control;

			// The Mac platform layer folds Command onto Control, so there Control means Command and Option is the
			// word key; elsewhere agg-gui takes Ctrl or Alt for a word.
			bool word = editor.UseMacKeyBindings ? keyEvent.Alt : control || keyEvent.Alt;
			bool macCommand = editor.UseMacKeyBindings && control;
			CodeDocument document = editor.Document;
			if (editor.ReadOnly)
			{
				// Swallowed, so a read-only view's edit keys change nothing and type nothing; Tab moves the focus on.
				switch (keyEvent.KeyCode)
				{
					case Keys.Back:
					case Keys.Delete:
					case Keys.Enter:
						return true;
					case Keys.Tab:
						return false;
					case Keys.Y:
					case Keys.Z:
						if (control)
						{
							return true;
						}

						break;
				}
			}

			switch (keyEvent.KeyCode)
			{
				case Keys.Left:
					if (macCommand)
					{
						MoveRowStart(editor, shift);
					}
					else if (word)
					{
						document.MoveWord(-1, shift);
					}
					else
					{
						document.MoveLeft(shift);
					}

					return true;
				case Keys.Right:
					if (macCommand)
					{
						MoveRowEnd(editor, shift);
					}
					else if (word)
					{
						document.MoveWord(1, shift);
					}
					else
					{
						document.MoveRight(shift);
					}

					return true;
				case Keys.Up:
					if (macCommand)
					{
						document.SetCaret(0, shift);
					}
					else
					{
						MoveRows(editor, -1, shift);
					}

					return true;
				case Keys.Down:
					if (macCommand)
					{
						document.SetCaret(document.Text.Length, shift);
					}
					else
					{
						MoveRows(editor, 1, shift);
					}

					return true;
				case Keys.PageUp: MoveRows(editor, -editor.PageLines(), shift); return true;
				case Keys.PageDown: MoveRows(editor, editor.PageLines(), shift); return true;
				case Keys.Home:
					if (control)
					{
						document.SetCaret(0, shift);
					}
					else
					{
						MoveRowStart(editor, shift);
					}

					return true;
				case Keys.End:
					if (control)
					{
						document.SetCaret(document.Text.Length, shift);
					}
					else
					{
						MoveRowEnd(editor, shift);
					}

					return true;
				case Keys.Back:
					if (word)
					{
						document.DeleteWord(-1);
					}
					else
					{
						document.Backspace();
					}

					return true;
				case Keys.Delete:
					if (word)
					{
						document.DeleteWord(1);
					}
					else
					{
						document.Delete();
					}

					return true;
				case Keys.Enter:
					if (editor.AggGuiEditing)
					{
						document.Insert("\n");
					}
					else
					{
						document.InsertNewLine();
					}

					return true;
				case Keys.Tab:
					if (control)
					{
						// Ctrl+Tab leaves focus navigation to the containers.
						return false;
					}

					if (shift)
					{
						document.Outdent();
					}
					else if (editor.AggGuiEditing && document.LineOf(document.SelectionStart) == document.LineOf(document.SelectionEnd))
					{
						// agg-gui's Tab within a line: one fixed indent over the selection, not to the next tab stop.
						document.Insert(new string(' ', document.TabSize));
					}
					else
					{
						document.Indent();
					}

					return true;
			}

			if (control)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.A: document.SelectAll(); return true;
					case Keys.C: editor.Copy(); return true;
					case Keys.X: editor.Cut(); return true;
					case Keys.V: editor.Paste(); return true;
					case Keys.Y: document.Redo(); return true;
					case Keys.Z:
						if (shift)
						{
							document.Redo();
						}
						else
						{
							document.Undo();
						}

						return true;
				}
			}

			return false;
		}

		/// <summary>Up/down and page up/down: by source line keeping the column, or, wrapping, by row keeping the x.</summary>
		private static void MoveRows(CodeEditor editor, int rows, bool extend)
		{
			CodeDocument document = editor.Document;
			if (editor.WordWrap)
			{
				document.SetCaret(editor.Rows.MoveVertically(document.Caret, rows, editor.RowShift), extend);
			}
			else
			{
				document.MoveLines(rows, extend);
			}
		}

		/// <summary>Home: to the caret's row's start - but on a line's first row, unless <see cref="CodeEditor.AggGuiEditing"/>,
		/// first to the indent (<see cref="CodeDocument.MoveHome"/>).</summary>
		private static void MoveRowStart(CodeEditor editor, bool extend)
		{
			CodeDocument document = editor.Document;
			CodeRow row = editor.Rows.Row(editor.Rows.RowOfIndex(document.Caret));
			if (row.FirstInLine && !editor.AggGuiEditing)
			{
				document.MoveHome(extend);
			}
			else
			{
				document.SetCaret(row.Start, extend);
			}
		}

		/// <summary>End: to the end of the caret's row - the line's end, unless the line wraps.</summary>
		private static void MoveRowEnd(CodeEditor editor, bool extend)
		{
			CodeDocument document = editor.Document;
			document.SetCaret(editor.Rows.Row(editor.Rows.RowOfIndex(document.Caret)).CaretEnd, extend);
		}
	}
}
