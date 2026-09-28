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

using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	/// <summary>What made an edit, which decides whether it joins the one before it in a single undo step.</summary>
	internal enum CodeEditKind
	{
		/// <summary>Never merged: a paste, a new line, an indent.</summary>
		Other,

		/// <summary>A typed character, merged with the typing before it until a space follows a word.</summary>
		Typing,

		/// <summary>Backspace (or a word of it), merged with the backspaces before it.</summary>
		DeleteBackward,

		/// <summary>Delete (or a word of it), merged with the deletes before it.</summary>
		DeleteForward,
	}

	/// <summary>One undo step: text[Start..Start+Removed.Length) became Inserted, with the caret and anchor either
	/// side of it.</summary>
	internal class CodeEdit
	{
		public CodeEditKind Kind;
		public int Start;
		public string Removed;
		public string Inserted;
		public int CaretBefore;
		public int AnchorBefore;
		public int CaretAfter;
		public int AnchorAfter;
	}

	/// <summary>
	/// The undo and redo stacks of a <see cref="CodeDocument"/>. A run of typing, of backspaces or of deletes is one
	/// step, the way editors group them; any caret move, undo or redo between two edits ends the run.
	/// </summary>
	internal class CodeEditHistory
	{
		private readonly List<CodeEdit> undo = new List<CodeEdit>();
		private readonly List<CodeEdit> redo = new List<CodeEdit>();

		// Whether the next edit may merge into the top of the undo stack.
		private bool canMerge;

		public void Clear()
		{
			undo.Clear();
			redo.Clear();
			canMerge = false;
		}

		/// <summary>Ends the current run, so the next edit is a step of its own.</summary>
		public void BreakGroup() => canMerge = false;

		public void Record(CodeEdit edit)
		{
			redo.Clear();
			CodeEdit last = undo.Count > 0 ? undo[undo.Count - 1] : null;
			if (canMerge && last != null && last.Kind == edit.Kind && TryMerge(last, edit))
			{
				last.CaretAfter = edit.CaretAfter;
				last.AnchorAfter = edit.AnchorAfter;
			}
			else
			{
				undo.Add(edit);
			}

			canMerge = edit.Kind != CodeEditKind.Other;
		}

		/// <summary>Takes the newest step off the undo stack onto the redo stack; null when there is none.</summary>
		public CodeEdit PopUndo() => Move(undo, redo);

		/// <summary>Takes the newest undone step back onto the undo stack; null when there is none.</summary>
		public CodeEdit PopRedo() => Move(redo, undo);

		private CodeEdit Move(List<CodeEdit> from, List<CodeEdit> to)
		{
			canMerge = false;
			if (from.Count == 0)
			{
				return null;
			}

			CodeEdit edit = from[from.Count - 1];
			from.RemoveAt(from.Count - 1);
			to.Add(edit);
			return edit;
		}

		private static bool TryMerge(CodeEdit last, CodeEdit edit)
		{
			switch (edit.Kind)
			{
				case CodeEditKind.Typing:
					// A space typed after a word starts a new step, so undo takes back a word at a time.
					bool startsWord = last.Inserted.Length > 0
						&& IsSpace(edit.Inserted[0]) && !IsSpace(last.Inserted[last.Inserted.Length - 1]);
					if (edit.Removed.Length > 0 || edit.Start != last.Start + last.Inserted.Length || startsWord)
					{
						return false;
					}

					last.Inserted += edit.Inserted;
					return true;
				case CodeEditKind.DeleteBackward:
					if (edit.Inserted.Length > 0 || edit.Start + edit.Removed.Length != last.Start)
					{
						return false;
					}

					last.Start = edit.Start;
					last.Removed = edit.Removed + last.Removed;
					return true;
				case CodeEditKind.DeleteForward:
					if (edit.Inserted.Length > 0 || edit.Start != last.Start)
					{
						return false;
					}

					last.Removed += edit.Removed;
					return true;
				default:
					return false;
			}
		}

		private static bool IsSpace(char c) => c == ' ' || c == '\t';
	}
}
