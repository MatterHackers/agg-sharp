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

using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	/// <summary>What a click-and-drag selects in: characters, whole words (after a double-click) or whole lines
	/// (after a triple-click).</summary>
	public enum SelectionUnit
	{
		Character,
		Word,
		Line,
	}

	/// <summary>
	/// The text, caret and selection of a <see cref="CodeEditor"/>, and every edit and caret move it makes - kept
	/// apart from the widget so the editing rules can be driven and tested without drawing anything.
	/// </summary>
	/// <remarks>
	/// Positions are UTF-16 indices into <see cref="Text"/>. Lines are split on '\n' only; any "\r\n" or lone '\r'
	/// coming in (<see cref="Text"/>, <see cref="Insert"/>, a paste) is normalised to '\n'. Caret steps never stop
	/// inside a surrogate pair, so an emoji is moved over, selected and deleted as one character.
	/// </remarks>
	public class CodeDocument
	{
		private readonly CodeEditHistory history = new CodeEditHistory();
		private readonly List<int> lineStarts = new List<int> { 0 };
		private string text = "";

		// The column up/down movement aims for, so moving through a short line and on keeps the original column.
		// -1 when the next vertical move should take it from the caret.
		private int preferredColumn = -1;

		public CodeDocument(string text = "")
		{
			Text = text;
		}

		/// <summary>Raised after every change to <see cref="Text"/>.</summary>
		public event EventHandler TextChanged;

		/// <summary>Raised after the caret or the selection anchor moved.</summary>
		public event EventHandler CaretChanged;

		/// <summary>The spaces Tab inserts, and the most Shift+Tab removes from the start of a line.</summary>
		public int TabSize { get; set; } = 4;

		/// <summary>The whole buffer. Setting it puts the caret at the start with nothing selected, and starts the
		/// undo history afresh.</summary>
		public string Text
		{
			get => text;
			set
			{
				text = Normalize(value ?? "");
				history.Clear();
				RebuildLineStarts();
				Caret = Anchor = 0;
				preferredColumn = -1;
				TextChanged?.Invoke(this, EventArgs.Empty);
				CaretChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Where typing goes; the moving end of the selection.</summary>
		public int Caret { get; private set; }

		/// <summary>The fixed end of the selection; equal to <see cref="Caret"/> when nothing is selected.</summary>
		public int Anchor { get; private set; }

		public bool HasSelection => Caret != Anchor;

		public int SelectionStart => Math.Min(Caret, Anchor);

		public int SelectionEnd => Math.Max(Caret, Anchor);

		public string SelectedText => text.Substring(SelectionStart, SelectionEnd - SelectionStart);

		public int LineCount => lineStarts.Count;

		public int LineStart(int line) => lineStarts[line];

		/// <summary>The index of the line's '\n', or the text's end for the last line.</summary>
		public int LineEnd(int line) => line + 1 < lineStarts.Count ? lineStarts[line + 1] - 1 : text.Length;

		public string LineText(int line) => text.Substring(LineStart(line), LineEnd(line) - LineStart(line));

		/// <summary>The line holding <paramref name="index"/> (a line's '\n' belongs to it).</summary>
		public int LineOf(int index)
		{
			int found = lineStarts.BinarySearch(index);
			return found >= 0 ? found : ~found - 1;
		}

		public int ColumnOf(int index) => index - LineStart(LineOf(index));

		/// <summary>The index <paramref name="column"/> chars into <paramref name="line"/>, clamped to the line and
		/// moved off the second half of a surrogate pair.</summary>
		public int IndexAt(int line, int column)
		{
			line = Math.Clamp(line, 0, LineCount - 1);
			int index = LineStart(line) + Math.Clamp(column, 0, LineEnd(line) - LineStart(line));
			return SnapToCharStart(text, index);
		}

		/// <summary>The index one character after <paramref name="index"/>, stepping over a surrogate pair whole.</summary>
		public static int NextCharIndex(string text, int index)
		{
			if (index >= text.Length)
			{
				return text.Length;
			}

			return char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? index + 2 : index + 1;
		}

		/// <summary>The index one character before <paramref name="index"/>, stepping over a surrogate pair whole.</summary>
		public static int PreviousCharIndex(string text, int index)
		{
			if (index <= 0)
			{
				return 0;
			}

			return index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2]) ? index - 2 : index - 1;
		}

		/// <summary>Moves the caret to <paramref name="index"/>; without <paramref name="extend"/> the selection
		/// collapses there, with it the anchor stays and the selection grows or shrinks.</summary>
		public void SetCaret(int index, bool extend = false)
		{
			preferredColumn = -1;
			MoveCaretTo(index, extend);
		}

		public void SelectAll()
		{
			preferredColumn = -1;
			history.BreakGroup();
			Anchor = 0;
			Caret = text.Length;
			CaretChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Left arrow: one character back, or, with a selection and no shift, to its start.</summary>
		public void MoveLeft(bool extend)
		{
			SetCaret(HasSelection && !extend ? SelectionStart : PreviousCharIndex(text, Caret), extend);
		}

		/// <summary>Right arrow: one character on, or, with a selection and no shift, to its end.</summary>
		public void MoveRight(bool extend)
		{
			SetCaret(HasSelection && !extend ? SelectionEnd : NextCharIndex(text, Caret), extend);
		}

		/// <summary>Up/down arrow (-1/+1) and page up/down (the page's line count): <paramref name="lines"/> lines
		/// on at the same column, stopping at the text's start or end on the first or last line.</summary>
		public void MoveLines(int lines, bool extend)
		{
			int line = LineOf(Caret);
			if (preferredColumn < 0)
			{
				preferredColumn = ColumnOf(Caret);
			}

			int target = line + lines;
			int index = target < 0 ? 0
				: target >= LineCount ? text.Length
				: IndexAt(target, preferredColumn);

			// Kept across the move so the next vertical step aims for the same column.
			int column = preferredColumn;
			MoveCaretTo(index, extend);
			preferredColumn = column;
		}

		/// <summary>Home: to the line's first non-blank character, or, when already there, to column 0.</summary>
		public void MoveHome(bool extend)
		{
			int line = LineOf(Caret);
			int start = LineStart(line);
			int firstText = start;
			while (firstText < LineEnd(line) && (text[firstText] == ' ' || text[firstText] == '\t'))
			{
				firstText++;
			}

			SetCaret(Caret == firstText ? start : firstText, extend);
		}

		public void MoveEnd(bool extend) => SetCaret(LineEnd(LineOf(Caret)), extend);

		/// <summary>Ctrl+Left/Right (Option on the Mac): to the previous (-1) or next (+1) word boundary - agg-gui's
		/// whitespace-delimited token - from the caret.</summary>
		public void MoveWord(int direction, bool extend)
		{
			SetCaret(direction < 0 ? PreviousWordBoundary(text, Caret) : NextWordBoundary(text, Caret), extend);
		}

		/// <summary>Ctrl+Backspace/Delete (Option on the Mac): deletes the selection, or back (-1) or on (+1) to the
		/// next word boundary.</summary>
		public void DeleteWord(int direction)
		{
			if (HasSelection)
			{
				Replace(SelectionStart, SelectionEnd, "", CodeEditKind.Other);
			}
			else if (direction < 0 && Caret > 0)
			{
				Replace(PreviousWordBoundary(text, Caret), Caret, "", CodeEditKind.DeleteBackward);
			}
			else if (direction > 0 && Caret < text.Length)
			{
				Replace(Caret, NextWordBoundary(text, Caret), "", CodeEditKind.DeleteForward);
			}
		}

		/// <summary>Double-click: selects the run of word characters (or of anything else) under
		/// <paramref name="index"/>.</summary>
		public void SelectWordAt(int index)
		{
			(int start, int end) = WordRangeAt(text, index);
			Select(start, end);
		}

		/// <summary>Triple-click: selects the line under <paramref name="index"/>, without its newline.</summary>
		public void SelectLineAt(int index)
		{
			int line = LineOf(Math.Clamp(index, 0, text.Length));
			Select(LineStart(line), LineEnd(line));
		}

		/// <summary>
		/// A drag after a click: extends the selection to <paramref name="index"/> in whole <paramref name="unit"/>s,
		/// never letting go of the pivot (pivotStart..pivotEnd) the click selected - agg-gui's granular drag.
		/// </summary>
		public void ExtendSelection(int index, SelectionUnit unit, int pivotStart, int pivotEnd)
		{
			if (unit == SelectionUnit.Character)
			{
				SetCaret(index, extend: true);
				return;
			}

			int line = LineOf(Math.Clamp(index, 0, text.Length));
			(int start, int end) = unit == SelectionUnit.Word ? WordRangeAt(text, index) : (LineStart(line), LineEnd(line));
			if (index >= pivotEnd)
			{
				Select(pivotStart, end);
			}
			else
			{
				Select(pivotEnd, start);
			}
		}

		/// <summary>Sets the anchor and the caret, which may sit either side of it.</summary>
		public void Select(int anchor, int caret)
		{
			preferredColumn = -1;
			history.BreakGroup();
			Anchor = SnapToCharStart(text, Math.Clamp(anchor, 0, text.Length));
			Caret = SnapToCharStart(text, Math.Clamp(caret, 0, text.Length));
			CaretChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Takes back the last edit - a run of typing or deleting counts as one - restoring the text, caret
		/// and selection from before it. False when there is nothing to undo.</summary>
		public bool Undo()
		{
			CodeEdit edit = history.PopUndo();
			if (edit == null)
			{
				return false;
			}

			Apply(edit.Start, edit.Start + edit.Inserted.Length, edit.Removed, edit.CaretBefore, edit.AnchorBefore);
			return true;
		}

		/// <summary>Puts back the last undone edit. False when there is nothing to redo.</summary>
		public bool Redo()
		{
			CodeEdit edit = history.PopRedo();
			if (edit == null)
			{
				return false;
			}

			Apply(edit.Start, edit.Start + edit.Removed.Length, edit.Inserted, edit.CaretAfter, edit.AnchorAfter);
			return true;
		}

		/// <summary>The end of the next token: past any whitespace, then past everything up to the next whitespace
		/// (agg-gui's next_word_boundary).</summary>
		public static int NextWordBoundary(string text, int index)
		{
			while (index < text.Length && char.IsWhiteSpace(text[index]))
			{
				index++;
			}

			while (index < text.Length && !char.IsWhiteSpace(text[index]))
			{
				index++;
			}

			return index;
		}

		/// <summary>The start of the previous token: back over any whitespace, then over everything back to the
		/// whitespace before it (agg-gui's prev_word_boundary).</summary>
		public static int PreviousWordBoundary(string text, int index)
		{
			while (index > 0 && char.IsWhiteSpace(text[index - 1]))
			{
				index--;
			}

			while (index > 0 && !char.IsWhiteSpace(text[index - 1]))
			{
				index--;
			}

			return index;
		}

		/// <summary>The run around <paramref name="index"/> of characters of the same class as the one at it: word
		/// characters (letters, digits, '_') or not (agg-gui's word_range_at).</summary>
		public static (int start, int end) WordRangeAt(string text, int index)
		{
			index = SnapToCharStart(text, Math.Clamp(index, 0, text.Length));
			bool wordClass = index < text.Length && IsWordChar(text, index);
			int start = index;
			while (start > 0)
			{
				int previous = PreviousCharIndex(text, start);
				if (IsWordChar(text, previous) != wordClass)
				{
					break;
				}

				start = previous;
			}

			int end = index;
			while (end < text.Length && IsWordChar(text, end) == wordClass)
			{
				end = NextCharIndex(text, end);
			}

			return (start, end);
		}

		/// <summary>Types <paramref name="value"/> over the selection (or at the caret) and puts the caret after it.</summary>
		public void Insert(string value)
		{
			string normalized = Normalize(value ?? "");

			// One typed character joins the typing before it in one undo step; a paste or a new line stands alone.
			bool typed = normalized.Length == 1 && normalized != "\n";
			Replace(SelectionStart, SelectionEnd, normalized, typed ? CodeEditKind.Typing : CodeEditKind.Other);
		}

		/// <summary>Enter: a new line indented like the current one.</summary>
		public void InsertNewLine()
		{
			int start = LineStart(LineOf(SelectionStart));
			int indentEnd = start;
			while (indentEnd < SelectionStart && (text[indentEnd] == ' ' || text[indentEnd] == '\t'))
			{
				indentEnd++;
			}

			Insert("\n" + text.Substring(start, indentEnd - start));
		}

		/// <summary>Backspace: deletes the selection, or the character before the caret.</summary>
		public void Backspace()
		{
			if (HasSelection)
			{
				Replace(SelectionStart, SelectionEnd, "", CodeEditKind.Other);
			}
			else if (Caret > 0)
			{
				Replace(PreviousCharIndex(text, Caret), Caret, "", CodeEditKind.DeleteBackward);
			}
		}

		/// <summary>Delete: deletes the selection, or the character after the caret.</summary>
		public void Delete()
		{
			if (HasSelection)
			{
				Replace(SelectionStart, SelectionEnd, "", CodeEditKind.Other);
			}
			else if (Caret < text.Length)
			{
				Replace(Caret, NextCharIndex(text, Caret), "", CodeEditKind.DeleteForward);
			}
		}

		/// <summary>
		/// Tab: with a selection over more than one line, indents each of its lines by <see cref="TabSize"/> spaces
		/// and keeps them selected; otherwise inserts spaces up to the next tab stop.
		/// </summary>
		public void Indent()
		{
			int firstLine = LineOf(SelectionStart);
			int lastLine = LineOf(SelectionEnd);
			if (firstLine == lastLine)
			{
				int column = ColumnOf(SelectionStart);
				Insert(new string(' ', TabSize - (column % TabSize)));
				return;
			}

			string indent = new string(' ', TabSize);
			ChangeLines(firstLine, lastLine, line => indent + line);
		}

		/// <summary>Shift+Tab: removes up to <see cref="TabSize"/> leading spaces (or one tab) from each selected
		/// line, or from the caret's line.</summary>
		public void Outdent()
		{
			ChangeLines(LineOf(SelectionStart), LineOf(SelectionEnd), line =>
			{
				if (line.StartsWith("\t"))
				{
					return line.Substring(1);
				}

				int spaces = 0;
				while (spaces < TabSize && spaces < line.Length && line[spaces] == ' ')
				{
					spaces++;
				}

				return line.Substring(spaces);
			});
		}

		private static string Normalize(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');

		private static int SnapToCharStart(string text, int index)
		{
			return index > 0 && index < text.Length && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]) ? index - 1 : index;
		}

		/// <summary>Rewrites whole lines first..last and selects them all, from the first's start to the last's end.</summary>
		private void ChangeLines(int firstLine, int lastLine, Func<string, string> change)
		{
			int start = LineStart(firstLine);
			int end = LineEnd(lastLine);
			var lines = text.Substring(start, end - start).Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				lines[i] = change(lines[i]);
			}

			string replacement = string.Join("\n", lines);
			Replace(start, end, replacement, CodeEditKind.Other, firstLine != lastLine ? start : null);
		}

		private static bool IsWordChar(string text, int index)
		{
			return char.IsLetterOrDigit(text, index) || text[index] == '_';
		}

		/// <summary>The one way the text changes by an edit: records it for undo, then puts the caret after the
		/// replacement with the anchor there too, or at <paramref name="anchorAfter"/> to leave it selected.</summary>
		private void Replace(int start, int end, string replacement, CodeEditKind kind, int? anchorAfter = null)
		{
			int caretAfter = start + replacement.Length;
			string removed = text.Substring(start, end - start);
			if (removed == replacement)
			{
				// Nothing changes (an outdent with no indent to take): no undo step that would undo nothing.
				Apply(start, end, replacement, caretAfter, anchorAfter ?? caretAfter);
				return;
			}

			history.Record(new CodeEdit
			{
				Kind = kind,
				Start = start,
				Removed = removed,
				Inserted = replacement,
				CaretBefore = Caret,
				AnchorBefore = Anchor,
				CaretAfter = caretAfter,
				AnchorAfter = anchorAfter ?? caretAfter,
			});
			Apply(start, end, replacement, caretAfter, anchorAfter ?? caretAfter);
		}

		private void Apply(int start, int end, string replacement, int caret, int anchor)
		{
			text = text.Substring(0, start) + replacement + text.Substring(end);
			RebuildLineStarts();
			preferredColumn = -1;
			Caret = caret;
			Anchor = anchor;
			TextChanged?.Invoke(this, EventArgs.Empty);
			CaretChanged?.Invoke(this, EventArgs.Empty);
		}

		private void MoveCaretTo(int index, bool extend)
		{
			history.BreakGroup();
			Caret = SnapToCharStart(text, Math.Clamp(index, 0, text.Length));
			if (!extend)
			{
				Anchor = Caret;
			}

			CaretChanged?.Invoke(this, EventArgs.Empty);
		}

		private void RebuildLineStarts()
		{
			lineStarts.Clear();
			lineStarts.Add(0);
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n')
				{
					lineStarts.Add(i + 1);
				}
			}
		}
	}
}
