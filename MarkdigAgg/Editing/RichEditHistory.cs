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

using System;
using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// What kind of edit the rich editor is about to make, so <see cref="RichEditHistory"/> knows which edits
	/// merge into one undo step.
	/// </summary>
	public enum RichEditKind
	{
		/// <summary>One typed character. Runs of it merge (see <see cref="RichEditHistory"/>).</summary>
		Typing,

		/// <summary>Backspace. A run of Backspaces from a caret merges.</summary>
		Backspace,

		/// <summary>Forward Delete. A run of Deletes from a caret merges.</summary>
		Delete,

		/// <summary>Anything else - Enter, paste, a style or block change, deleting a selection: always its own step.</summary>
		Other,
	}

	/// <summary>
	/// The rich editor's undo/redo history. Each step is a snapshot: a deep <see cref="RichDocument.Clone"/> taken
	/// just before the edit, with the selections before and after it. Restoring a snapshot brings back each block's
	/// Dirty flag and group objects as they were, so undoing to the start writes the file back byte for byte.
	/// <para>
	/// Protocol: the editor calls <see cref="Record"/> with the document as it is before mutating it, mutates it,
	/// then calls <see cref="Committed"/> with the resulting selection, passing changed: false when the edit did
	/// nothing (Backspace at the document's start) so no empty step is left. The editor calls
	/// <see cref="BreakCoalescing"/> when the caret or selection moves other than by editing, and when the pending
	/// style changes (picking Bold mid-word starts a new step). A line-start shortcut ("# " and the like) is its
	/// own step: after the space is recorded and committed as typing, the editor calls Record with
	/// <see cref="RichEditKind.Other"/>, applies the shortcut, then Committed - so one undo reverts just the
	/// auto-format and leaves the typed "# ". Loading a new document calls <see cref="Clear"/>.
	/// <see cref="Undo"/> and <see cref="Redo"/> return a fresh document for the editor to adopt in place of its
	/// current one.
	/// </para>
	/// <para>
	/// Merging, as most text editors do it: typing one character at the caret where the previous typing ended
	/// extends that step, except that a word character typed after a space or punctuation starts a new step - so
	/// "hello world" is two steps, "hello " and "world", and each undo removes about a word. Typing over a selection
	/// starts a step that the rest of that word extends, so the first undo removes the replacement's first word and
	/// brings back the replaced text together. Backspace and Delete runs from a caret merge the same way (each with
	/// its own kind); Backspace or Delete of a selection is its own step. Typed text longer than one character (a
	/// paste) or containing a line break counts as <see cref="RichEditKind.Other"/>. Any Other edit, undo, redo or
	/// <see cref="BreakCoalescing"/> ends a run. A merged keystroke takes no snapshot, so typing costs a document
	/// clone per word, not per character.
	/// </para>
	/// <para>
	/// Memory: a step on the undo side holds one whole-document snapshot (Before); an undone step waiting on the
	/// redo side holds two (Before and After), and drops After again once redone. The history keeps at most
	/// <see cref="MaxSteps"/> undo steps (default 200), dropping the oldest. A 2,000-character note is a few KB a
	/// step; a ~50 KB help page tops out around 200 x 50 KB of text plus its inline objects - tens of MB at worst,
	/// and only after 200 separate edits.
	/// </para>
	/// <para>
	/// Built on <see cref="UndoBuffer"/>, which supplies the step cap, redo clearing and the Changed event.
	/// </para>
	/// </summary>
	public class RichEditHistory
	{
		public const int DefaultMaxSteps = 200;

		private readonly UndoBuffer buffer = new UndoBuffer();

		// The Undo or Redo call in progress. IUndoRedoCommand's Do and Undo take and return nothing, so the step
		// reads its input from and writes its result to this object; each call keeps its own in a local, so a
		// Changed handler that undoes again (UndoBuffer raises Changed inside the replay) cannot clobber it.
		private ReplayCall activeCall;

		// Set once the top step must not be extended: after undo, redo, a caret move or Clear.
		private bool coalescingBroken = true;

		// What Committed(changed: false) puts back: the history before the latest Record, or for a merged
		// keystroke the top step's previous LastText.
		private UndoBufferState stateBeforeRecord;
		private bool coalescingBrokenBeforeRecord;
		private SnapshotStep mergedStep;
		private string mergedStepLastText;

		public RichEditHistory(int maxSteps = DefaultMaxSteps)
		{
			if (maxSteps <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxSteps), "The history must keep at least one step.");
			}

			buffer.MaxUndos = maxSteps;
			buffer.Changed += (s, e) => Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Raised when CanUndo or CanRedo may have changed (a new step, undo, redo, Clear), for toolbar enablement.
		/// Not raised when a keystroke merges into the top step, which changes neither.
		/// </summary>
		public event EventHandler Changed;

		public int MaxSteps => buffer.MaxUndos;

		public bool CanUndo => buffer.UndoCount > 0;

		public bool CanRedo => buffer.RedoCount > 0;

		public int UndoCount => buffer.UndoCount;

		public int RedoCount => buffer.RedoCount;

		/// <summary>
		/// Call just before mutating <paramref name="before"/>. Either extends the top step (a continuing typing,
		/// Backspace or Delete run - nothing is copied) or snapshots the document as a new step, which clears redo.
		/// <paramref name="text"/> is the typed text for <see cref="RichEditKind.Typing"/>; it decides word breaks.
		/// </summary>
		public void Record(RichDocument before, RichSelection selectionBefore, RichEditKind kind, string text = null)
		{
			if (kind == RichEditKind.Typing && !IsOneCharacter(text))
			{
				// A paste or a line break (Enter) is always its own step, and typing never merges into it.
				kind = RichEditKind.Other;
			}

			if (buffer.PeekUndo() is SnapshotStep top && Extends(top, selectionBefore, kind, text))
			{
				stateBeforeRecord = null;
				mergedStep = top;
				mergedStepLastText = top.LastText;
				top.LastText = text;
				return;
			}

			mergedStep = null;
			stateBeforeRecord = buffer.CaptureState();
			coalescingBrokenBeforeRecord = coalescingBroken;
			buffer.Add(new SnapshotStep(this)
			{
				Before = before.Clone(),
				SelectionBefore = selectionBefore,
				SelectionAfter = selectionBefore,
				Kind = kind,
				// Backspace or Delete of a selection removes the selection; nothing extends that step.
				Extendable = kind == RichEditKind.Typing || (kind != RichEditKind.Other && selectionBefore.IsEmpty),
				LastText = text,
			});
			coalescingBroken = false;
		}

		/// <summary>
		/// Call after the mutation <see cref="Record"/> announced, with the selection it produced - where the
		/// caret goes back to on redo, and where a following keystroke must be to merge. Pass
		/// <paramref name="changed"/> false when the edit changed nothing: the step just recorded is dropped and the
		/// history (redo included) and typing run are as they were before Record.
		/// </summary>
		public void Committed(RichSelection selectionAfter, bool changed = true)
		{
			if (!changed)
			{
				if (stateBeforeRecord != null)
				{
					buffer.RestoreState(stateBeforeRecord);
					coalescingBroken = coalescingBrokenBeforeRecord;
				}
				else if (mergedStep != null)
				{
					mergedStep.LastText = mergedStepLastText;
				}
			}
			else if (buffer.PeekUndo() is SnapshotStep top)
			{
				top.SelectionAfter = selectionAfter;
			}

			stateBeforeRecord = null;
			mergedStep = null;
		}

		/// <summary>
		/// Ends the current typing, Backspace or Delete run, so the next edit is a new step. The editor calls this
		/// when the user moves the caret or changes the selection other than by editing, and when the pending
		/// style changes.
		/// </summary>
		public void BreakCoalescing()
		{
			coalescingBroken = true;
		}

		/// <summary>
		/// Forgets every step, for a newly loaded document.
		/// </summary>
		public void Clear()
		{
			buffer.ClearHistory();
			coalescingBroken = true;
		}

		/// <summary>
		/// Steps back: returns the document and selection from before the latest step, for the editor to adopt
		/// in place of <paramref name="current"/>. A copy of the current state is kept for <see cref="Redo"/>.
		/// Returns null when there is nothing to undo.
		/// </summary>
		public (RichDocument Document, RichSelection Selection)? Undo(RichDocument current, RichSelection currentSelection)
		{
			return Replay(current, currentSelection, redo: false);
		}

		/// <summary>
		/// Re-applies the latest undone step: returns the document and selection it produced. Returns null when
		/// there is nothing to redo.
		/// </summary>
		public (RichDocument Document, RichSelection Selection)? Redo(RichDocument current, RichSelection currentSelection)
		{
			return Replay(current, currentSelection, redo: true);
		}

		private (RichDocument Document, RichSelection Selection)? Replay(RichDocument current, RichSelection currentSelection, bool redo)
		{
			if (redo ? !CanRedo : !CanUndo)
			{
				return null;
			}

			coalescingBroken = true;
			var call = new ReplayCall { Document = current, Selection = currentSelection };
			var outerCall = activeCall;
			activeCall = call;
			try
			{
				if (redo)
				{
					buffer.Redo();
				}
				else
				{
					buffer.Undo();
				}
			}
			finally
			{
				activeCall = outerCall;
			}

			return (call.Document, call.Selection);
		}

		private bool Extends(SnapshotStep top, RichSelection selectionBefore, RichEditKind kind, string text)
		{
			if (coalescingBroken || !top.Extendable || top.Kind != kind || kind == RichEditKind.Other)
			{
				return false;
			}

			// Only a keystroke at the caret the previous one left behind continues the run.
			if (!selectionBefore.IsEmpty || selectionBefore != top.SelectionAfter)
			{
				return false;
			}

			if (kind == RichEditKind.Typing)
			{
				// A word starts a new step once the previous keystroke ended in a space or punctuation.
				char previous = top.LastText[top.LastText.Length - 1];
				return !(IsWordChar(text[0]) && !IsWordChar(previous));
			}

			return true;
		}

		// One keystroke's text: a single char, or a surrogate pair (an emoji), never a line break.
		private static bool IsOneCharacter(string text)
		{
			if (string.IsNullOrEmpty(text) || text.Contains('\n') || text.Contains('\r'))
			{
				return false;
			}

			return text.Length == 1 || (text.Length == 2 && char.IsSurrogatePair(text, 0));
		}

		// The apostrophe counts so "don't" stays one step.
		private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '\'';

		private class ReplayCall
		{
			public RichDocument Document;
			public RichSelection Selection;
		}

		/// <summary>
		/// One undo step: the document before it, and - only while it waits to be redone - the document after it,
		/// taken from the editor at undo time.
		/// </summary>
		private class SnapshotStep : IUndoRedoCommand
		{
			private readonly RichEditHistory history;

			public SnapshotStep(RichEditHistory history)
			{
				this.history = history;
			}

			public string Name => Kind.ToString();

			public RichDocument Before { get; set; }

			public RichSelection SelectionBefore { get; set; }

			public RichDocument After { get; set; }

			public RichSelection SelectionAfter { get; set; }

			public RichEditKind Kind { get; set; }

			public bool Extendable { get; set; }

			public string LastText { get; set; }

			public void Undo()
			{
				// Read the call before anything can raise Changed. Copy the current state, since the editor may keep
				// editing the document it hands over; hand out a copy of Before, so a later redo then undo still
				// restores the untouched snapshot.
				var call = history.activeCall;
				After = call.Document.Clone();
				SelectionAfter = call.Selection;
				call.Document = Before.Clone();
				call.Selection = SelectionBefore;
			}

			public void Do()
			{
				var call = history.activeCall;
				call.Document = After;
				call.Selection = SelectionAfter;
				// The editor now owns After; the next Undo recaptures it, so the step holds only Before meanwhile.
				After = null;
			}
		}
	}
}
