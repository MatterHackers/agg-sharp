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

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The seam between the formatting toolbar (<see cref="MarkdownFormatToolbar"/>) and whatever owns the document
	/// being edited. The toolbar never touches the editor widget: it reads <see cref="Document"/>,
	/// <see cref="Selection"/> and <see cref="PendingStyle"/> to show its state and sends every change through
	/// <see cref="ApplyEdit"/>, so toolbar edits go through the editor's one edit pipeline (undo history, relayout,
	/// change events) exactly as typing does. The rich editor's keyboard pipeline implements this when the toolbar
	/// is wired to it; <see cref="RichDocumentCommands"/> is a widget-free implementation over a bare document.
	/// </summary>
	public interface IRichEditCommands
	{
		/// <summary>
		/// The document being edited. The toolbar only reads it; changes go through <see cref="ApplyEdit"/>.
		/// </summary>
		RichDocument Document { get; }

		RichSelection Selection { get; }

		/// <summary>
		/// Styles toggled at a collapsed caret for the next typed text. The toolbar sets it when Bold (etc.) is
		/// pressed with nothing selected; the implementation clears it when the caret moves and raises
		/// <see cref="SelectionChanged"/> when it changes, so the buttons show it.
		/// </summary>
		RichPendingStyle PendingStyle { get; set; }

		/// <summary>
		/// Runs one edit: <paramref name="edit"/> mutates the document in place (the Rich*Operations ops) and returns
		/// the selection to show next and whether it changed the document at all. The implementation:
		/// <list type="number">
		/// <item>records a changed edit as one undo step, and nothing for an unchanged one (a click that did nothing
		/// must not leave an empty undo step);</item>
		/// <item>re-lays out everything (RelayoutAll): a toolbar edit may add, remove or change any blocks - a code
		/// block toggle replaces several, a table insert adds one;</item>
		/// <item>stores the returned selection FIRST, then raises <see cref="DocumentChanged"/> (when changed) and
		/// <see cref="SelectionChanged"/> (when the selection moved), so a handler never reads a selection that
		/// points past the new document.</item>
		/// </list>
		/// Matches RichMarkdownEditWidget.ApplyEdit(kind, edit, text) of the keyboard pipeline.
		/// </summary>
		void ApplyEdit(Func<RichDocument, RichSelection, (RichSelection Selection, bool Changed)> edit);

		/// <summary>
		/// Gives keyboard focus back to the text, so a user who clicks Bold or closes the link row keeps typing.
		/// The toolbar calls it after every toolbar edit, after a pending-style toggle and after the link row closes.
		/// </summary>
		void FocusText();

		/// <summary>
		/// Raised when the caret, the selection or the pending style changes, so the toolbar can refresh.
		/// </summary>
		event EventHandler SelectionChanged;

		/// <summary>
		/// Raised after any edit (toolbar, typing, undo), or when the document is replaced.
		/// </summary>
		event EventHandler DocumentChanged;
	}

	/// <summary>
	/// <see cref="IRichEditCommands"/> over a bare <see cref="RichDocument"/>, with no widget, layout or undo
	/// history: an edit simply runs and the events fire. Drives the toolbar in its tests and in any host that
	/// uses the toolbar without the rich editor widget.
	/// </summary>
	public class RichDocumentCommands : IRichEditCommands
	{
		private RichSelection selection;

		private RichPendingStyle pendingStyle;

		public RichDocumentCommands(RichDocument document, RichSelection selection = default)
		{
			Document = document;
			this.selection = selection;
		}

		public event EventHandler SelectionChanged;

		public event EventHandler DocumentChanged;

		public RichDocument Document { get; }

		/// <summary>
		/// Setting it moves the caret as a click would, so the pending style is cleared as the editor clears it.
		/// </summary>
		public RichSelection Selection
		{
			get => selection;
			set
			{
				selection = value;
				pendingStyle = default;
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public RichPendingStyle PendingStyle
		{
			get => pendingStyle;
			set
			{
				pendingStyle = value;
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public void ApplyEdit(Func<RichDocument, RichSelection, (RichSelection Selection, bool Changed)> edit)
		{
			var (next, changed) = edit(Document, selection);
			bool moved = next != selection;
			selection = next;
			if (moved)
			{
				pendingStyle = default;
			}

			if (changed)
			{
				DocumentChanged?.Invoke(this, EventArgs.Empty);
			}

			if (moved)
			{
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// There is no text widget to focus; virtual so a test can see the toolbar ask for it.
		/// </summary>
		public virtual void FocusText()
		{
		}
	}
}
