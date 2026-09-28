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

using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>
	/// The editing state behind a rich-text editor, with no view (agg-gui's RichEditCore): the document, a caret and
	/// selection anchor, a pending caret style, and snapshot undo through <see cref="Undoer{TState}"/>. A view feeds it
	/// input and calls <see cref="FeedUndo"/> each frame so a burst of typing coalesces into one undo step.
	/// </summary>
	public class RichEditCore
	{
		private Undoer<Snapshot> undoer = new Undoer<Snapshot>();

		// The committed state a live colour preview started from, restored by CancelPreview; null when not previewing.
		private Snapshot previewStart;

		// Whether the preview has changed anything yet: a colour dialog dismissed by clicking away keeps a change but
		// leaves no undo step for a look that changed nothing (agg-gui's preview_dirty).
		private bool previewDirty;

		// A toggled format waiting for the next typed character (Ctrl+B with nothing selected).
		private InlineStyle pendingStyle;

		public RichEditCore(RichDoc doc)
		{
			this.Doc = doc ?? new RichDoc();
			this.DocVersion++;
		}

		/// <summary>Raised after any change to the document, caret or pending style, so views can redraw.</summary>
		public event EventHandler Changed;

		public RichDoc Doc { get; private set; }

		/// <summary>Gets the moving end of the selection.</summary>
		public DocPos Caret { get; private set; }

		/// <summary>Gets the fixed end of the selection.</summary>
		public DocPos Anchor { get; private set; }

		public DocRange Selection => new DocRange(this.Anchor, this.Caret);

		public string PlainText => this.Doc.PlainText;

		/// <summary>Gets the style a typed character takes: the pending style if armed, else the style at the caret.</summary>
		public InlineStyle StyleForInsert => this.pendingStyle ?? RichTextCommands.StyleAt(this.Doc, this.Caret);

		/// <summary>
		/// Gets a number that changes whenever the document may have changed through this core - not for caret,
		/// selection or pending-style moves - so a view can keep its layout across those.
		/// </summary>
		public int DocVersion { get; private set; }

		/// <summary>Gets whether a live preview (<see cref="BeginPreview"/>) is in progress.</summary>
		public bool IsPreviewing => this.previewStart != null;

		/// <summary>Gets whether the preview in progress has run a command since <see cref="BeginPreview"/>.</summary>
		public bool IsPreviewDirty => this.IsPreviewing && this.previewDirty;

		public bool CanUndo => this.undoer.HasUndo(this.TakeSnapshot());

		public bool CanRedo => this.undoer.HasRedo(this.TakeSnapshot());

		/// <summary>Replaces the document, resetting the caret and the undo history.</summary>
		public void Load(RichDoc doc)
		{
			this.Doc = doc ?? new RichDoc();

			// A preview's start belongs to the old document.
			this.previewStart = null;
			this.Caret = this.Anchor = default;
			this.pendingStyle = null;
			this.undoer = new Undoer<Snapshot>();
			this.OnChanged();
		}

		/// <summary>Clamps a position onto the document: block in range, offset within the block.</summary>
		public DocPos Clamp(DocPos pos)
		{
			int block = Math.Clamp(pos.Block, 0, this.Doc.Blocks.Count - 1);
			return new DocPos(block, Math.Clamp(pos.Offset, 0, this.Doc.Blocks[block].TextLength));
		}

		/// <summary>Moves the caret; <paramref name="extend"/> keeps the anchor. Drops any pending style.</summary>
		public void SetCaret(DocPos pos, bool extend = false)
		{
			this.Caret = this.Clamp(pos);
			if (!extend)
			{
				this.Anchor = this.Caret;
			}

			this.pendingStyle = null;
			this.OnChanged();
		}

		public void SetSelection(DocPos anchor, DocPos caret)
		{
			this.Anchor = this.Clamp(anchor);
			this.SetCaret(caret, extend: true);
		}

		public void SelectAll() => this.SetSelection(default, this.Doc.EndPos);

		/// <summary>
		/// What the selection's styles agree on, for toolbar state. With a collapsed caret and a pending style, the
		/// pending style (with the caret paragraph's alignment and list) - the format about to be typed.
		/// </summary>
		public CommonStyle CommonStyleOfSelection()
		{
			if (this.Selection.IsEmpty && this.pendingStyle != null)
			{
				var common = CommonStyle.Of(this.pendingStyle);
				common.MergeBlocks(this.Doc, this.Selection);
				return common;
			}

			return RichTextCommands.RangeCommonStyle(this.Doc, this.Selection);
		}

		/// <summary>Inserts <paramref name="text"/> at the caret, replacing the selection; '\n' splits paragraphs.</summary>
		public void Insert(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			var style = this.StyleForInsert;
			this.TakeSelection();
			var lines = text.Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				if (i > 0)
				{
					this.Caret = RichTextEdits.SplitBlock(this.Doc, this.Caret);
				}

				RichTextEdits.InsertText(this.Doc, this.Caret, lines[i], style);
				this.Caret = new DocPos(this.Caret.Block, this.Caret.Offset + lines[i].Length);
			}

			this.CollapseAndNotify();
		}

		/// <summary>
		/// Inserts a styled <paramref name="fragment"/> (from the rich clipboard) at the caret, replacing the selection
		/// and keeping each run's style and the fragment's paragraph structure - unlike <see cref="Insert"/>, which
		/// types plain text in the caret's style.
		/// </summary>
		public void InsertFragment(IReadOnlyList<Block> fragment)
		{
			if (fragment == null || fragment.Count == 0)
			{
				return;
			}

			this.TakeSelection();
			this.Caret = RichTextEdits.SpliceFragment(this.Doc, this.Caret, fragment);
			this.CollapseAndNotify();
		}

		/// <summary>
		/// Enter. On an empty list item it outdents instead, and at indent 0 leaves the list, the way word processors
		/// end a list; anywhere else it splits the paragraph.
		/// </summary>
		public void Split()
		{
			this.TakeSelection();
			var block = this.Doc.Blocks[this.Caret.Block];
			if (block.List != ListKind.None && block.TextLength == 0)
			{
				if (block.Indent > 0)
				{
					block.Indent--;
				}
				else
				{
					block.List = ListKind.None;
				}
			}
			else
			{
				this.Caret = RichTextEdits.SplitBlock(this.Doc, this.Caret);
			}

			this.CollapseAndNotify();
		}

		/// <summary>Deletes the selection, else the character before the caret, else joins onto the previous paragraph.</summary>
		public void Backspace()
		{
			if (!this.Selection.IsEmpty)
			{
				this.TakeSelection();
			}
			else if (this.Caret.Offset > 0)
			{
				var text = this.Doc.Blocks[this.Caret.Block].Text;
				int previous = this.Caret.Offset - (this.Caret.Offset > 1 && char.IsLowSurrogate(text[this.Caret.Offset - 1]) ? 2 : 1);
				this.Caret = RichTextEdits.RemoveRange(this.Doc, new DocRange(new DocPos(this.Caret.Block, previous), this.Caret));
			}
			else if (this.Caret.Block > 0)
			{
				this.Caret = RichTextEdits.MergeBlockWithPrevious(this.Doc, this.Caret.Block);
			}
			else
			{
				return;
			}

			this.CollapseAndNotify();
		}

		/// <summary>Deletes the selection, else the character after the caret, else pulls the next paragraph up.</summary>
		public void DeleteForward()
		{
			if (!this.Selection.IsEmpty)
			{
				this.TakeSelection();
			}
			else if (this.Caret.Offset < this.Doc.Blocks[this.Caret.Block].TextLength)
			{
				var text = this.Doc.Blocks[this.Caret.Block].Text;
				int next = this.Caret.Offset + (char.IsHighSurrogate(text[this.Caret.Offset]) && this.Caret.Offset + 1 < text.Length ? 2 : 1);
				RichTextEdits.RemoveRange(this.Doc, new DocRange(this.Caret, new DocPos(this.Caret.Block, next)));
			}
			else if (this.Caret.Block + 1 < this.Doc.Blocks.Count)
			{
				RichTextEdits.MergeBlockWithPrevious(this.Doc, this.Caret.Block + 1);
			}
			else
			{
				return;
			}

			this.CollapseAndNotify();
		}

		/// <summary>
		/// Applies a formatting command. An inline command on a collapsed caret arms the pending style for the next
		/// typed character instead; anything else changes the document.
		/// </summary>
		public void Exec(RichCommand command)
		{
			this.previewDirty |= this.IsPreviewing;
			if (this.Selection.IsEmpty && command.IsInline)
			{
				this.pendingStyle = command.ApplyTo(this.StyleForInsert);
			}
			else
			{
				RichTextCommands.Apply(this.Doc, this.Selection, command);
				this.pendingStyle = null;
				this.DocVersion++;
			}

			this.OnChanged();
		}

		/// <summary>
		/// Tells the undo history the current state at <paramref name="time"/> seconds (any steady clock). Call it
		/// after each change and every frame while it returns true; a change becomes an undo point once it has held
		/// still for <see cref="Undoer{TState}.StableTime"/>.
		/// </summary>
		public bool FeedUndo(double time)
		{
			if (this.IsPreviewing)
			{
				// Every preview frame changes the document; none of them is an undo step.
				return false;
			}

			this.undoer.FeedState(time, this.TakeSnapshot());
			return this.undoer.IsInFlux;
		}

		/// <summary>Makes the current state an undo point now - for discrete edits such as a toolbar command.</summary>
		public void AddUndoPoint() => this.undoer.AddUndo(this.TakeSnapshot());

		/// <summary>
		/// Starts a live preview (agg-gui's begin_preview): a colour dialog may then <see cref="Exec"/> on every drag
		/// frame without making undo steps, and <see cref="CancelPreview"/> puts back exactly this state. A second call
		/// keeps the first start.
		/// </summary>
		public void BeginPreview()
		{
			if (this.previewStart == null)
			{
				this.AddUndoPoint();
				this.previewStart = this.TakeSnapshot();
				this.previewDirty = false;
			}
		}

		/// <summary>Keeps the previewed result as a single undo step.</summary>
		public void CommitPreview()
		{
			if (this.previewStart != null)
			{
				this.previewStart = null;
				this.AddUndoPoint();
			}
		}

		/// <summary>Restores the document, caret and selection from <see cref="BeginPreview"/>, leaving no undo step.</summary>
		public void CancelPreview()
		{
			if (this.previewStart != null)
			{
				var start = this.previewStart;
				this.previewStart = null;
				this.Restore(start);
			}
		}

		/// <summary>
		/// Undoes one step. Does nothing while a preview is in progress: the preview's start is what Cancel restores,
		/// and undoing past it would leave Cancel restoring a state the history no longer matches.
		/// </summary>
		public bool Undo()
		{
			if (this.IsPreviewing)
			{
				return false;
			}

			if (this.undoer.Undo(this.TakeSnapshot(), out var previous))
			{
				this.Restore(previous);
				return true;
			}

			return false;
		}

		/// <summary>Redoes one step; like <see cref="Undo"/>, not while a preview is in progress.</summary>
		public bool Redo()
		{
			if (this.IsPreviewing)
			{
				return false;
			}

			if (this.undoer.Redo(this.TakeSnapshot(), out var next))
			{
				this.Restore(next);
				return true;
			}

			return false;
		}

		private void TakeSelection()
		{
			if (!this.Selection.IsEmpty)
			{
				this.Caret = RichTextEdits.RemoveRange(this.Doc, this.Selection);
				this.Anchor = this.Caret;
			}
		}

		private void CollapseAndNotify()
		{
			this.DocVersion++;
			this.Anchor = this.Caret;
			this.pendingStyle = null;
			this.OnChanged();
		}

		private void OnChanged() => this.Changed?.Invoke(this, EventArgs.Empty);

		// The undoer compares states by value and keeps them, so each snapshot owns a deep copy of the document.
		private Snapshot TakeSnapshot() => new Snapshot(this.Doc.Clone(), this.Caret, this.Anchor);

		private void Restore(Snapshot snapshot)
		{
			this.Doc = snapshot.Doc.Clone();
			this.DocVersion++;
			this.Caret = snapshot.Caret;
			this.Anchor = snapshot.Anchor;
			this.pendingStyle = null;
			this.OnChanged();
		}

		private sealed record Snapshot(RichDoc Doc, DocPos Caret, DocPos Anchor);
	}
}
