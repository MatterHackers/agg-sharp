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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The rich (WYSIWYG) markdown editor: a scrollable view of a <see cref="RichDocument"/> drawn the way the
	/// markdown viewer shows it. Each block keeps its own layout, so an edit re-lays out only the block it touched.
	/// Raw blocks show rendered by the viewer as child widgets; tables draw as an editable grid.
	/// </summary>
	public class RichMarkdownEditWidget : ScrollableWidget, IRichEditCommands
	{
		private readonly RichDocumentView view;
		private RichDocument document = new RichDocument();
		private ThemeConfig theme;
		private RichSelection selection;
		private readonly RichEditorMouse mouse;
		private readonly RichEditorKeyboard keyboard;
		private readonly RichCaretBlink caretBlink;

		public RichMarkdownEditWidget(ThemeConfig theme)
			: base(autoScroll: true)
		{
			this.theme = theme ?? new ThemeConfig();
			Colors = ColorsFor(this.theme);
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;

			// A scrollable widget keeps its first size as a minimum; the editor has to narrow with its panel.
			MinimumSize = Vector2.Zero;
			ScrollArea.HAnchor = HAnchor.Stretch;
			ScrollArea.VAnchor = VAnchor.Fit;

			// Room for the scroll bar, as the viewer leaves it.
			ScrollArea.Margin = new BorderDouble(0, 0, 15, 0);

			view = new RichDocumentView(this);
			AddChild(view);
			keyboard = new RichEditorKeyboard(this);
			caretBlink = new RichCaretBlink(view, () => ContainsFocus && !HasBeenClosed);
			EnsureABlock();
			mouse = new RichEditorMouse(this);
		}

		/// <summary>
		/// Raised with a link's url when it is Cmd/Ctrl+clicked; a plain click on a link only places the caret.
		/// </summary>
		public event Action<string> LinkClicked;

		/// <summary>
		/// Raised when a Raw block is double-clicked, with where its source starts in <see cref="Markdown"/>
		/// (<see cref="RichMarkdownWriter.SourceOffsetOf"/>), so the host can show the markdown there.
		/// </summary>
		public event Action<int> RawBlockActivated;

		/// <summary>
		/// Raised when the user moves the caret or selection with the mouse, so typing after it starts a new undo
		/// step (<see cref="RichEditHistory.BreakCoalescing"/>).
		/// </summary>
		public event EventHandler SelectionChangedByUser;

		/// <summary>
		/// How mouse handling reads Shift and Cmd/Ctrl; tests replace it rather than press the shared keyboard.
		/// </summary>
		internal Func<Keys, bool> MouseKeyState
		{
			get => mouse.IsKeyDown;
			set => mouse.IsKeyDown = value;
		}

		internal void RaiseLinkClicked(string url) => LinkClicked?.Invoke(url);

		internal void RaiseRawBlockActivated(int sourceOffset) => RawBlockActivated?.Invoke(sourceOffset);

		internal void SetSelectionByUser(RichSelection newSelection, bool caretAtLineEnd)
		{
			SetSelection(newSelection, caretAtLineEnd);

			// Even a click where the caret already was ends the typing run, as in other editors.
			History.BreakCoalescing();
			SelectionChangedByUser?.Invoke(this, EventArgs.Empty);
		}

		internal void ShowCursor(Cursors cursor)
		{
			if (Cursor != cursor)
			{
				Cursor = cursor;
				SetCursor(cursor);
			}
		}

		// The document view and Raw hosts are not selectable, so every press over the text lands here, not in them.
		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			mouse.Down(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			mouse.Move(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouse.Up(mouseEvent);
			base.OnMouseUp(mouseEvent);
		}

		/// <summary>
		/// This view's own undo history; loading markdown clears it.
		/// </summary>
		public RichEditHistory History { get; } = new RichEditHistory();

		/// <summary>
		/// Styles Cmd/Ctrl+B or I (or the toolbar) flipped at a collapsed caret for the next typed text; cleared when
		/// the caret moves. A change raises <see cref="SelectionChanged"/> so the toolbar's buttons follow it.
		/// </summary>
		public RichPendingStyle PendingStyle
		{
			get => pendingStyle;
			set
			{
				if (pendingStyle != value)
				{
					pendingStyle = value;

					// As Cmd/Ctrl+B does: the text typed with the new style is its own undo step.
					History.BreakCoalescing();
					SelectionChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		private RichPendingStyle pendingStyle;

		/// <summary>
		/// Raised on every change of the selection, the caret's wrap side or <see cref="PendingStyle"/> - by the
		/// keyboard, the mouse, an edit, an undo or code - after the new selection is stored.
		/// </summary>
		public event EventHandler SelectionChanged;

		/// <summary>
		/// Mac (Option word steps, Cmd+arrows to line and document ends) or Windows/Linux key bindings; defaults to
		/// the plain text field's choice for this OS.
		/// </summary>
		public bool UseMacKeyBindings { get; set; } = InternalTextEditWidget.UseMacKeyBindings;

		/// <summary>
		/// Raised by Cmd/Ctrl+K: the host asks for a link and applies it with <see cref="ApplyEdit"/>.
		/// </summary>
		public event EventHandler LinkRequested;

		/// <summary>
		/// Makes one undoable edit, as the keyboard does: <paramref name="edit"/> mutates the document it is given
		/// (with the current selection) and returns the new selection and whether anything changed; the editor
		/// records it, places the selection, re-lays out and raises <see cref="DocumentChanged"/>.
		/// <paramref name="text"/> is the typed text for <see cref="RichEditKind.Typing"/>.
		/// </summary>
		public void ApplyEdit(RichEditKind kind, Func<RichDocument, RichSelection, (RichSelection Selection, bool Changed)> edit, string text = null)
		{
			keyboard.Apply(kind, edit, text);
		}

		/// <summary>
		/// The formatting toolbar's edit (<see cref="IRichEditCommands"/>): one undo step of kind Other when it
		/// changed something, and every block laid out again, since a toolbar op may add, remove or reshape blocks
		/// anywhere (a table goes after a whole list, a code block replaces several paragraphs).
		/// </summary>
		void IRichEditCommands.ApplyEdit(Func<RichDocument, RichSelection, (RichSelection Selection, bool Changed)> edit)
		{
			keyboard.Apply(RichEditKind.Other, edit, relayoutAll: true);
		}

		/// <summary>
		/// Gives this editor the keyboard focus, so typing continues after a toolbar click.
		/// </summary>
		public void FocusText() => Focus();

		internal bool CaretShowing => caretBlink.Showing;

		internal int LaidOutBlockCount => view.LaidOutCount;

		internal void RequestLink() => LinkRequested?.Invoke(this, EventArgs.Empty);

		internal void RaiseDocumentChanged() => DocumentChanged?.Invoke(this, EventArgs.Empty);

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// Before base, which moves focus on an unhandled Tab: the editor decides first whether Tab is its own.
			keyboard.KeyDown(keyEvent);
			base.OnKeyDown(keyEvent);
		}

		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			keyboard.KeyPress(keyPressEvent);
			base.OnKeyPress(keyPressEvent);
		}

		/// <summary>
		/// Raised after an edit, from <see cref="Relayout(int)"/> and <see cref="RelayoutAll"/>. Loading markdown
		/// through <see cref="Markdown"/> is not an edit and does not raise it.
		/// </summary>
		public event EventHandler DocumentChanged;

		/// <summary>
		/// The document as markdown. Setting it parses and lays out a new document, with the caret at its start; an
		/// untouched document reads back byte-identical.
		/// </summary>
		public string Markdown
		{
			get => RichMarkdownWriter.Write(document);
			set
			{
				ScrollPositionFromTop = Vector2.Zero;
				History.Clear();
				SetDocument(RichMarkdownParser.Parse(value ?? ""), RichSelection.At(new DocPosition(0, 0)));
			}
		}

		/// <summary>
		/// The document shown. Setting it (to adopt an undo or redo result, or to swap documents) lays everything out
		/// again and clamps the selection into the new document; it is not an edit and does not raise
		/// <see cref="DocumentChanged"/>.
		/// </summary>
		public RichDocument Document
		{
			get => document;
			set => SetDocument(value, selection);
		}

		/// <summary>
		/// Shows <paramref name="newDocument"/> with <paramref name="newSelection"/> (clamped into it), as
		/// <see cref="RichEditHistory.Undo"/> and <see cref="RichEditHistory.Redo"/> hand them back.
		/// </summary>
		public void SetDocument(RichDocument newDocument, RichSelection newSelection)
		{
			document = newDocument ?? new RichDocument();
			EnsureABlock();
			SetSelection(new RichSelection(Clamp(newSelection.Anchor), Clamp(newSelection.Caret), newSelection.WholeBlock), caretAtLineEnd: false);
			view.RelayoutAll();
		}

		private DocPosition Clamp(DocPosition position)
		{
			int blockIndex = Math.Clamp(position.BlockIndex, 0, document.Blocks.Count - 1);
			var block = document.Blocks[blockIndex];
			int row = 0;
			int column = 0;
			if (block.Kind == RichBlockKind.Table && block.TableRows.Count > 0)
			{
				row = Math.Clamp(position.Row, 0, block.TableRows.Count - 1);
				column = Math.Clamp(position.Column, 0, Math.Max(block.TableRows[row].Count - 1, 0));
			}

			int offset = Math.Clamp(position.Offset, 0, block.TextLength(row, column));
			return new DocPosition(blockIndex, offset, row, column);
		}

		/// <summary>
		/// The selection. Setting it puts the caret at the start of a line where its offset wraps; use
		/// <see cref="SetSelection"/> to keep a caret at the end of the line before.
		/// </summary>
		public RichSelection Selection
		{
			get => selection;
			set => SetSelection(value, caretAtLineEnd: false);
		}

		/// <summary>
		/// Which side of a wrap the caret shows on (<see cref="RichCaret.AtLineEnd"/>). The model's positions have no
		/// such side - it is purely where the caret is drawn and where up/down/Home/End measure from - so the widget
		/// keeps it beside the selection rather than in <see cref="RichSelection"/>.
		/// </summary>
		public bool CaretAtLineEnd { get; private set; }

		/// <summary>
		/// Sets the selection and which side of a wrap its caret shows on; a click right of a line that wrapped
		/// with no space passes the <see cref="RichCaret.AtLineEnd"/> its hit test returned.
		/// </summary>
		public void SetSelection(RichSelection selection, bool caretAtLineEnd)
		{
			// A move other than by editing ends the typing run, so the next keystroke is its own undo step.
			if (selection != this.selection || caretAtLineEnd != CaretAtLineEnd)
			{
				History.BreakCoalescing();
			}

			SetSelectionAfterEdit(selection, caretAtLineEnd);
		}

		/// <summary>
		/// Places the selection an edit produced without ending the typing run.
		/// </summary>
		internal void SetSelectionAfterEdit(RichSelection selection, bool caretAtLineEnd = false)
		{
			bool changed = selection != this.selection || caretAtLineEnd != CaretAtLineEnd || !pendingStyle.IsEmpty;
			this.selection = selection;
			CaretAtLineEnd = caretAtLineEnd;

			// A pending Bold belongs to the caret it was picked at; typed text has taken it on by now. Cleared through
			// the field so the change raises SelectionChanged once, below.
			pendingStyle = default;
			caretBlink?.Restart();
			view.Invalidate();
			if (changed)
			{
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Shown in a dim colour when the document is empty and the editor does not have focus (e.g. "No notes yet").
		/// </summary>
		public string EmptyHint { get; set; }

		public ThemeConfig Theme
		{
			get => theme;
			set
			{
				theme = value ?? new ThemeConfig();
				Colors = ColorsFor(theme);
				view.InvalidateStyle();
				view.RelayoutAll();
			}
		}

		public RichEditColors Colors { get; set; }

		/// <summary>
		/// The style the current layouts were built with (null before the first layout).
		/// </summary>
		public RichLayoutStyle Style => view.Style;

		/// <summary>
		/// Maps an image's url as written in the markdown to what the viewer's image loader fetches.
		/// </summary>
		public Func<string, string> ResolveImageUrl
		{
			get => view.ResolveImageUrl;
			set => view.ResolveImageUrl = value;
		}

		/// <summary>
		/// True when the document is one empty paragraph.
		/// </summary>
		public bool IsEmpty => document.Blocks.Count == 1
			&& document.Blocks[0].Kind == RichBlockKind.Paragraph
			&& document.Blocks[0].TextLength() == 0;

		/// <summary>
		/// How many blocks have been laid out since the widget was made: lets tests see that an edit re-lays out
		/// only what it touched.
		/// </summary>
		internal int LayoutCount => view.LayoutCount;

		/// <summary>
		/// The first and last block indices painted by the last draw (Last &lt; First when none were).
		/// </summary>
		internal (int First, int Last) DrawnBlocks => view.DrawnBlocks;

		/// <summary>
		/// The widget the document's blocks are drawn in, which scrolls; <see cref="BlockBounds"/> and hit tests are
		/// in its coordinates.
		/// </summary>
		public GuiWidget DocumentView => view;

		/// <summary>
		/// A block's box in <see cref="DocumentView"/> coordinates: the top of its box down to where the next block
		/// starts.
		/// </summary>
		public RectangleDouble BlockBounds(int blockIndex) => view.BlockBounds(blockIndex);

		/// <summary>
		/// The layout a block was last laid out to: a <see cref="RichTableLayout"/> for a table, otherwise a
		/// <see cref="RichBlockLayout"/>.
		/// </summary>
		public IRichBlockLayout BlockLayout(int blockIndex) => view.LayoutOf(blockIndex);

		/// <summary>
		/// The y in <see cref="DocumentView"/> coordinates of a block layout's origin (the bottom of its box, spacing
		/// included): add it to layout coordinates to place them, subtract it before a layout hit test.
		/// </summary>
		public double BlockOrigin(int blockIndex) => view.OriginOf(blockIndex);

		/// <summary>
		/// The child widget showing a Raw block rendered, or null for other blocks.
		/// </summary>
		public GuiWidget RawBlockWidget(int blockIndex) => view.RawHost(blockIndex);

		/// <summary>
		/// Call after editing <see cref="Document"/>: re-lays out that block (and blocks the edit inserted or
		/// renumbered; blocks it removed are dropped), restacks the rest without laying them out, and raises
		/// <see cref="DocumentChanged"/>. Blocks are matched to their layouts by object, so a block an edit changed
		/// in place is stale until it is named here: an edit touching several blocks must relayout their range
		/// (<see cref="Relayout(int, int)"/>) or call <see cref="RelayoutAll"/>.
		/// </summary>
		public void Relayout(int blockIndex) => Relayout(blockIndex, blockIndex);

		/// <summary>
		/// <see cref="Relayout(int)"/> for blocks <paramref name="firstBlock"/> through <paramref name="lastBlock"/>.
		/// </summary>
		public void Relayout(int firstBlock, int lastBlock)
		{
			EnsureABlock();
			var changed = new List<RichBlock>();
			for (int i = Math.Max(firstBlock, 0); i <= lastBlock && i < document.Blocks.Count; i++)
			{
				changed.Add(document.Blocks[i]);
			}

			view.Relayout(changed);
			DocumentChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Call after an edit that touched many blocks: lays them all out again and raises
		/// <see cref="DocumentChanged"/>.
		/// </summary>
		public void RelayoutAll()
		{
			EnsureABlock();
			view.RelayoutAll();
			DocumentChanged?.Invoke(this, EventArgs.Empty);
		}

		public override void OnContainsFocusChanged(FocusChangedArgs e)
		{
			base.OnContainsFocusChanged(e);

			// The caret and the empty hint both depend on focus.
			caretBlink.Restart();
			view.Invalidate();
		}

		// An empty file is one empty paragraph to the user: there is always somewhere for the caret to be. It writes
		// nothing (its source is ""), so an empty file still reads back empty.
		private void EnsureABlock()
		{
			if (document.Blocks.Count == 0)
			{
				document.Blocks.Add(new RichBlock { Kind = RichBlockKind.Paragraph });
			}
		}

		private static RichEditColors ColorsFor(ThemeConfig theme)
		{
			return new RichEditColors
			{
				CodeBackground = theme.MinimalShade,

				// The viewer's quote bar (QuoteBlockX) is the text colour at this alpha.
				QuoteBar = theme.TextColor.WithAlpha(90),
				Selection = theme.PrimaryAccentColor.WithAlpha(100),
				Caret = theme.TextColor,
				Hint = theme.TextColor.WithAlpha(110),
			};
		}
	}
}
