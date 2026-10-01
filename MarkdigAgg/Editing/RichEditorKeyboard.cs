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
	/// The rich editor's keyboard: typing, editing keys, caret movement and the Cmd/Ctrl shortcuts, all going
	/// through one edit pipeline (<see cref="Apply"/>): history Record, the op, Committed, the new selection, then a
	/// relayout of the touched blocks (which raises DocumentChanged).
	/// <para>
	/// Bindings follow <see cref="InternalTextEditWidget"/>: the mac layer folds Command onto Control, so Cmd and
	/// Ctrl chords are the same code. On mac a word step is Option (Alt), Cmd+Left/Right go to the line's ends and
	/// Cmd+Up/Down to the document's; elsewhere Ctrl steps words and Ctrl+Home/End go to the document's ends.
	/// Shift with any caret key extends the selection.
	/// </para>
	/// </summary>
	internal sealed class RichEditorKeyboard
	{
		private readonly RichMarkdownEditWidget editor;
		private readonly RichEditorClipboard clipboard;

		// The x Up/Down aim for, kept while they repeat so the caret comes back to its column after a short line.
		// It belongs to the caret it was measured for; any other caret (a click, an edit) measures afresh.
		private double goalX;
		private DocPosition? goalCaret;

		// A character outside the BMP arrives as two key presses; the first half waits for the second.
		private char? highSurrogate;

		public RichEditorKeyboard(RichMarkdownEditWidget editor)
		{
			this.editor = editor;
			clipboard = new RichEditorClipboard(editor, this);
		}

		private bool Mac => editor.UseMacKeyBindings;

		/// <summary>
		/// One edit: records it in the history, runs <paramref name="edit"/> on the document (it returns the new
		/// selection and whether anything changed), commits, places the selection and re-lays out the blocks from
		/// the old selection's to the new one's. Blocks the edit inserted are laid out and removed ones dropped (with
		/// their Raw hosts) by the relayout itself, and blocks it reshaped without naming them (list children lifted
		/// a level) are noticed there too. With <paramref name="relayoutAll"/> every block is laid out again instead -
		/// the toolbar's contract (<see cref="IRichEditCommands.ApplyEdit"/>), whose ops may touch blocks anywhere.
		/// </summary>
		public void Apply(RichEditKind kind, Func<RichDocument, RichSelection, (RichSelection Selection, bool Changed)> edit, string text = null, bool relayoutAll = false)
		{
			var document = editor.Document;
			var before = editor.Selection;
			int blockCount = document.Blocks.Count;
			editor.History.Record(document, before, kind, text);
			var (after, changed) = edit(document, before);
			editor.History.Committed(after, changed);
			editor.SetSelectionAfterEdit(after);
			if (!changed)
			{
				// An edit that only moved the caret (Tab to the next cell, Backspace selecting a Raw block) is a
				// caret move, which ends the typing run.
				if (after != before)
				{
					editor.History.BreakCoalescing();
				}

				return;
			}

			if (relayoutAll)
			{
				editor.RelayoutAll();
				return;
			}

			int first = Math.Min(before.Start.BlockIndex, after.Start.BlockIndex);

			// After a removal the old selection's end names a block that has moved up unchanged; the edit ends where
			// its new selection does.
			int last = document.Blocks.Count < blockCount
				? after.End.BlockIndex
				: Math.Max(before.End.BlockIndex, after.End.BlockIndex);
			editor.Relayout(first, Math.Max(first, last));
		}

		public void KeyPress(KeyPressEventArgs e)
		{
			char c = e.KeyChar;

			// Control characters (Enter, Tab, Backspace) are KeyDown's.
			if (c < ' ' || c == '\u007f')
			{
				return;
			}

			e.Handled = true;
			if (char.IsHighSurrogate(c))
			{
				highSurrogate = c;
				return;
			}

			string text = c.ToString();
			if (char.IsLowSurrogate(c) && highSurrogate != null)
			{
				text = highSurrogate.Value + text;
			}

			highSurrogate = null;
			Type(text);
			if (text == " " && editor.Selection.IsEmpty)
			{
				// The shortcut is its own step after the typed space, so one undo leaves "# " as typed.
				var caret = editor.Selection.Caret;
				Apply(RichEditKind.Other, (document, selection) =>
				{
					bool applied = RichBlockOperations.TryApplyLineStartShortcut(document, caret, out var newCaret);
					return (RichSelection.At(newCaret), applied);
				});
			}

			ScrollCaretIntoView();
		}

		/// <summary>
		/// Types <paramref name="text"/> at the caret in the pending style, replacing the selection in the same
		/// step.
		/// </summary>
		private void Type(string text)
		{
			var pending = editor.PendingStyle;
			Apply(RichEditKind.Typing, (document, selection) =>
			{
				var caret = selection.IsEmpty ? selection.Caret : RichEditOperations.DeleteSelection(document, selection).Caret;
				var style = pending.IsEmpty || !document.Blocks[caret.BlockIndex].IsTextBlock && document.Blocks[caret.BlockIndex].Kind != RichBlockKind.Table
					? null
					: pending.ApplyPending(RichStyleOperations.TypingStyle(document, caret));
				return (RichEditOperations.InsertText(document, caret, text, style), true);
			}, text);
		}

		public void KeyDown(KeyEventArgs e)
		{
			if (Handle(e))
			{
				e.Handled = true;
				e.SuppressKeyPress = true;
				ScrollCaretIntoView();
			}
		}

		private bool Handle(KeyEventArgs e)
		{
			bool command = e.Control;
			bool word = Mac ? e.Alt : e.Control;
			switch (e.KeyCode)
			{
				case Keys.Left:
				case Keys.Right:
					bool right = e.KeyCode == Keys.Right;
					if (Mac && command)
					{
						return MoveTo(RichCaretNavigator.LineEdge(editor, editor.Selection.Caret, editor.CaretAtLineEnd, right), e.Shift);
					}

					if (!e.Shift && !word && !editor.Selection.IsEmpty)
					{
						// An arrow collapses a selection to the side it points to, as text fields do.
						var side = right ? editor.Selection.End : editor.Selection.Start;
						return MoveTo(new RichCaretTarget(side), false);
					}

					var caret = editor.Selection.Caret;
					return MoveTo(word
						? (right ? RichCaretNavigator.NextWord(editor, caret) : RichCaretNavigator.PreviousWord(editor, caret))
						: (right ? RichCaretNavigator.Next(editor, caret) : RichCaretNavigator.Previous(editor, caret)), e.Shift);

				case Keys.Up:
				case Keys.Down:
					if (Mac && command)
					{
						return MoveTo(e.KeyCode == Keys.Up ? DocumentStart() : DocumentEnd(), e.Shift);
					}

					return MoveVertically(e.KeyCode == Keys.Up ? -1 : 1, e.Shift);

				case Keys.Home:
				case Keys.End:
					bool end = e.KeyCode == Keys.End;
					if (command)
					{
						return MoveTo(end ? DocumentEnd() : DocumentStart(), e.Shift);
					}

					return MoveTo(RichCaretNavigator.LineEdge(editor, editor.Selection.Caret, editor.CaretAtLineEnd, end), e.Shift);

				case Keys.Back:
				case Keys.Delete:
					DeleteKey(e.KeyCode == Keys.Back, word);
					return true;

				case Keys.Enter:
					Enter(e.Shift);
					return true;

				case Keys.Tab:
					return Tab(e.Shift);

				case Keys.Escape:
					if (editor.Selection.IsEmpty)
					{
						return false;
					}

					return MoveTo(new RichCaretTarget(editor.Selection.Caret), false);

				case Keys.A when command:
					editor.SetSelection(new RichSelection(DocumentStart().Position, DocumentEnd().Position), caretAtLineEnd: false);
					return true;

				case Keys.C when command:
					clipboard.Copy();
					return true;

				case Keys.X when command:
					clipboard.Cut();
					return true;

				case Keys.V when command:
					clipboard.Paste();
					return true;

				case Keys.B when command:
					ToggleStyle(RichInlineStyle.Bold);
					return true;

				case Keys.I when command:
					ToggleStyle(RichInlineStyle.Italic);
					return true;

				case Keys.K when command:
					editor.RequestLink();
					return true;

				case Keys.Z when command:
					if (e.Shift)
					{
						Redo();
					}
					else
					{
						Undo();
					}

					return true;

				// Ctrl+Y is Windows' redo; Cmd+Y is not a mac redo.
				case Keys.Y when command && !Mac:
					Redo();
					return true;
			}

			return false;
		}

		private RichCaretTarget DocumentStart() => new RichCaretTarget(new DocPosition(0, 0));

		private RichCaretTarget DocumentEnd() => new RichCaretTarget(RichEditOperations.EndOf(editor.Document, editor.Document.Blocks.Count - 1));

		/// <summary>
		/// Puts the caret at <paramref name="target"/>, extending the selection from its anchor when
		/// <paramref name="extend"/>. A key with nowhere to go (Left at the start) is still handled, so it never
		/// moves focus.
		/// </summary>
		private bool MoveTo(RichCaretTarget? target, bool extend)
		{
			if (target is RichCaretTarget to)
			{
				var anchor = extend ? editor.Selection.Anchor : to.Position;
				editor.SetSelection(new RichSelection(anchor, to.Position), to.AtLineEnd);
			}

			return true;
		}

		private bool MoveVertically(int direction, bool extend)
		{
			var caret = editor.Selection.Caret;
			if (goalCaret != caret)
			{
				var bounds = RichCaretNavigator.CaretBounds(editor, caret, editor.CaretAtLineEnd);
				goalX = bounds?.Center.X ?? 0;
			}

			double x = goalX;
			MoveTo(RichCaretNavigator.Vertical(editor, caret, editor.CaretAtLineEnd, x, direction), extend);

			// SetSelection forgets nothing here: the goal is tied to the caret it produced.
			goalX = x;
			goalCaret = editor.Selection.Caret;
			return true;
		}

		private void DeleteKey(bool backspace, bool word)
		{
			var kind = backspace ? RichEditKind.Backspace : RichEditKind.Delete;
			var selection = editor.Selection;
			if (!selection.IsEmpty)
			{
				// A selected Raw block (WholeBlock) goes as a block; any other selection as text.
				Apply(kind, (document, s) => (RichEditOperations.DeleteSelection(document, s), true));
				return;
			}

			if (word)
			{
				var caret = selection.Caret;
				var target = backspace ? RichCaretNavigator.PreviousWord(editor, caret) : RichCaretNavigator.NextWord(editor, caret);

				// A word delete stays inside its block or cell; at an edge it acts as a plain Backspace or Delete.
				if (target is RichCaretTarget to && SameContainer(to.Position, caret) && to.Position.Offset != caret.Offset)
				{
					Apply(RichEditKind.Other, (document, s) => (RichEditOperations.DeleteRange(document, caret, to.Position), true));
					return;
				}
			}

			Apply(kind, (document, s) =>
			{
				bool changed;
				var after = backspace
					? RichEditOperations.Backspace(document, s.Caret, out changed)
					: RichEditOperations.Delete(document, s.Caret, out changed);
				return (after, changed);
			});
		}

		private static bool SameContainer(DocPosition a, DocPosition b) => a.BlockIndex == b.BlockIndex && a.Row == b.Row && a.Column == b.Column;

		/// <summary>
		/// Enter splits the block. Shift+Enter in a paragraph, list item or quote inserts a hard line break (an atom
		/// written as a backslash and a line ending); anywhere else - headings and table cells cannot hold one,
		/// code takes plain lines - it is Enter.
		/// </summary>
		private void Enter(bool shift)
		{
			Apply(RichEditKind.Other, (document, selection) =>
			{
				var caret = selection.IsEmpty ? selection.Caret : RichEditOperations.DeleteSelection(document, selection).Caret;
				var block = document.Blocks[caret.BlockIndex];
				if (shift && (block.Kind == RichBlockKind.Paragraph || block.Kind == RichBlockKind.ListItem || block.Kind == RichBlockKind.Quote))
				{
					string blankLine = RichEditOperations.BlankLine(document);
					var atom = new InlineAtom(InlineAtomKind.HardBreak, "\\" + blankLine.Substring(0, blankLine.Length / 2));
					block.Inlines.Insert(RichInlines.SplitAt(block.Inlines, caret.Offset), atom);
					block.Dirty = true;
					return (RichSelection.At(caret with { Offset = caret.Offset + 1 }), true);
				}

				return (RichEditOperations.SplitBlock(document, caret), true);
			});
		}

		/// <summary>
		/// Tab moves between table cells (adding a row past the last), indents a list item and types spaces in
		/// code. In a paragraph, heading or quote it is not handled, so Tab still moves focus out of the editor
		/// rather than trapping it.
		/// </summary>
		private bool Tab(bool shift)
		{
			var caret = editor.Selection.Caret;
			var kind = editor.Document.Blocks[caret.BlockIndex].Kind;
			switch (kind)
			{
				case RichBlockKind.Table:
					// Moving between cells changes nothing; only Tab past the last cell adds a row.
					Apply(RichEditKind.Other, (document, selection) =>
					{
						if (shift)
						{
							return (RichTableCodeOperations.PreviousCell(document, selection.Caret), false);
						}

						var after = RichTableCodeOperations.NextCell(document, selection.Caret, out bool addedRow);
						return (after, addedRow);
					});
					return true;

				case RichBlockKind.ListItem:
					Apply(RichEditKind.Other, (document, selection) => (selection, shift
						? RichBlockOperations.Outdent(document, selection)
						: RichBlockOperations.Indent(document, selection)));
					return true;

				case RichBlockKind.CodeBlock:
					// Shift+Tab in code is consumed but does nothing yet: leaving the editor mid-code would surprise.
					if (!shift)
					{
						Type(RichTableCodeOperations.CodeTab);
					}

					return true;
			}

			return false;
		}

		/// <summary>
		/// Cmd/Ctrl+B or I: over a selection it toggles the style; at a caret it flips the style the next typed
		/// text takes, which starts a new undo step.
		/// </summary>
		private void ToggleStyle(RichInlineStyle style)
		{
			if (editor.Selection.IsEmpty)
			{
				editor.PendingStyle = editor.PendingStyle.Toggle(style);
				editor.History.BreakCoalescing();
				return;
			}

			Apply(RichEditKind.Other, (document, selection) => (RichStyleOperations.ToggleStyle(document, selection, style), true));
		}

		private void Undo()
		{
			if (editor.History.Undo(editor.Document, editor.Selection) is var (document, selection))
			{
				editor.SetDocument(document, selection);
				editor.RaiseDocumentChanged();
			}
		}

		private void Redo()
		{
			if (editor.History.Redo(editor.Document, editor.Selection) is var (document, selection))
			{
				editor.SetDocument(document, selection);
				editor.RaiseDocumentChanged();
			}
		}

		/// <summary>
		/// Scrolls the least distance that shows the whole caret line.
		/// </summary>
		public void ScrollCaretIntoView()
		{
			var view = editor.DocumentView;
			if (view.Parent == null || RichCaretNavigator.CaretBounds(editor, editor.Selection.Caret, editor.CaretAtLineEnd) is not { } caret)
			{
				return;
			}

			// At the document's start or end show the page's edge too, not just the caret line inside its margin.
			var position = editor.Selection.Caret;
			if (position == DocumentStart().Position)
			{
				caret.Top = view.Height;
			}
			else if (position == DocumentEnd().Position)
			{
				caret.Bottom = 0;
			}

			var visible = view.TransformFromParentSpace(editor, editor.LocalBounds);
			double scroll = editor.ScrollOffsetFromTop();
			if (caret.Top > visible.Top)
			{
				editor.SetScrollOffsetFromTop(scroll - (caret.Top - visible.Top));
			}
			else if (caret.Bottom < visible.Bottom)
			{
				editor.SetScrollOffsetFromTop(scroll + (visible.Bottom - caret.Bottom));
			}
		}
	}
}
