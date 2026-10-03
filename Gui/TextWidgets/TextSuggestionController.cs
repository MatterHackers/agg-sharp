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
*/

using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Shows an <see cref="ITextSuggestionProvider"/>'s suggestions in a list just below the caret of a
	/// <see cref="TextEditWidget"/>, the way code editors complete what is being typed.
	/// </summary>
	/// <remarks>
	/// Unlike a <see cref="PopupMenu"/>, where the highlighted row is the focused one, the keyboard focus never
	/// leaves the field - the user keeps typing, and each edit refilters the list. The highlight is
	/// <see cref="HighlightIndex"/>, and the keys that drive it (Up, Down, PageUp, PageDown, Enter, Tab, Escape)
	/// are taken in <see cref="TextEditWidget.PreviewKeyDown"/> before the field can edit, submit, tab away or
	/// scroll for them. The list is agg drawn and parented to the window, so it works in the browser too.
	/// </remarks>
	public class TextSuggestionController : IDisposable
	{
		private readonly TextEditWidget field;
		private readonly ITextSuggestionProvider provider;
		private string lastQueryText;
		private int lastQueryCaret = -1;

		// Set while an accept rewrites the field, so the edit it makes is not taken for the user's own.
		private bool accepting;

		public TextSuggestionController(TextEditWidget field, ITextSuggestionProvider provider, ThemeConfig theme = null)
		{
			this.field = field;
			this.provider = provider;
			this.Popup = new TextSuggestionPopup(this, theme ?? ThemeConfig.Current);

			field.PreviewKeyDown += this.Field_PreviewKeyDown;
			field.KeyPressed += this.Field_KeyPressed;
			field.TextChanged += this.Field_EditedOrCaretMoved;
			field.InternalTextEditWidget.InsertBarPositionChanged += this.Field_EditedOrCaretMoved;
			field.InternalTextEditWidget.FocusChanged += this.Field_FocusChanged;
			field.Closed += this.Field_Closed;
		}

		/// <summary>The list widget, parented to the field's window while it is showing.</summary>
		public TextSuggestionPopup Popup { get; }

		public bool IsOpen { get; private set; }

		/// <summary>What the list is showing; <see cref="TextSuggestionList.Empty"/> while it is closed.</summary>
		public TextSuggestionList Suggestions { get; private set; } = TextSuggestionList.Empty;

		public int HighlightIndex { get; private set; }

		/// <summary>
		/// Asks the provider about the field's text at its caret, showing the answer or closing when there is none.
		/// </summary>
		public void Requery()
		{
			var edit = this.field.InternalTextEditWidget;
			string text = this.field.Text ?? "";
			int caret = edit.CharIndexToInsertBefore;
			if (!edit.Focused)
			{
				this.Close();
				return;
			}

			// Typing moves the caret and changes the text, and both say so - ask once per state, not per event. The
			// later event can still have moved the line (the field scrolls to follow the caret), so place again.
			if (this.IsOpen && text == this.lastQueryText && caret == this.lastQueryCaret)
			{
				this.Popup.Place();
				return;
			}

			this.lastQueryText = text;
			this.lastQueryCaret = caret;
			var list = this.provider.GetSuggestions(text, caret);
			if (list == null || list.Suggestions.Count == 0)
			{
				this.Close();
				return;
			}

			this.Suggestions = list;
			this.HighlightIndex = 0;
			this.IsOpen = true;
			this.Popup.Show(this.field);
		}

		public void Close()
		{
			this.IsOpen = false;
			this.Suggestions = TextSuggestionList.Empty;
			this.lastQueryText = null;
			this.lastQueryCaret = -1;
			this.Popup.Visible = false;
		}

		/// <summary>
		/// Replaces the list's span with suggestion <paramref name="index"/>'s insert text, puts the caret after it
		/// and asks again at once, so an insert ending in something like '.' shows what can follow it.
		/// </summary>
		public void Accept(int index)
		{
			if (!this.IsOpen
				|| index < 0
				|| index >= this.Suggestions.Suggestions.Count)
			{
				return;
			}

			var list = this.Suggestions;
			string insert = list.Suggestions[index].InsertText;
			string text = this.field.Text ?? "";
			int start = Math.Max(0, Math.Min(list.ReplaceStart, text.Length));
			int end = Math.Max(start, Math.Min(start + list.ReplaceLength, text.Length));

			this.accepting = true;
			try
			{
				this.field.Text = text.Substring(0, start) + insert + text.Substring(end);
				this.field.InternalTextEditWidget.SetCursorPosition(start + insert.Length);
			}
			finally
			{
				this.accepting = false;
			}

			this.Close();
			this.Requery();
		}

		public void Dispose()
		{
			this.field.PreviewKeyDown -= this.Field_PreviewKeyDown;
			this.field.KeyPressed -= this.Field_KeyPressed;
			this.field.TextChanged -= this.Field_EditedOrCaretMoved;
			this.field.InternalTextEditWidget.InsertBarPositionChanged -= this.Field_EditedOrCaretMoved;
			this.field.InternalTextEditWidget.FocusChanged -= this.Field_FocusChanged;
			this.field.Closed -= this.Field_Closed;
			this.Close();
			this.Popup.Parent?.RemoveChild(this.Popup);
		}

		/// <summary>Moves the highlight by <paramref name="delta"/> rows, wrapping at the ends when asked.</summary>
		internal void MoveHighlight(int delta, bool wrap)
		{
			int count = this.Suggestions.Suggestions.Count;
			int next = this.HighlightIndex + delta;
			next = wrap ? ((next % count) + count) % count : Math.Max(0, Math.Min(next, count - 1));
			this.HighlightIndex = next;
			this.Popup.ScrollHighlightIntoView();
		}

		/// <summary>
		/// Where the text being replaced starts, in the coordinates of <paramref name="host"/>: one pixel wide, the
		/// height of its line. The list hangs from here, so it lines up with the word being completed and stays put
		/// while it is typed, as code editors do. Measured with the offsets the field draws its selection band with.
		/// </summary>
		internal RectangleDouble ReplaceStartBounds(GuiWidget host)
		{
			var edit = this.field.InternalTextEditWidget;
			int start = Math.Max(0, Math.Min(this.Suggestions.ReplaceStart, edit.Text.Length));
			double fontHeight = edit.Printer.TypeFaceStyle.EmSizeInPixels;
			Vector2 offset = edit.Printer.GetOffsetLeftOfCharacterIndex(start);
			Vector2 top = edit.TransformToParentSpace(host, new Vector2(offset.X, edit.Height + offset.Y));
			Vector2 bottom = edit.TransformToParentSpace(host, new Vector2(offset.X, edit.Height + offset.Y - fontHeight));
			return new RectangleDouble(bottom.X, bottom.Y, bottom.X + 1, top.Y);
		}

		private void Field_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (!this.IsOpen
				|| keyEvent.Control
				|| keyEvent.Alt)
			{
				return;
			}

			int page = Math.Max(1, this.Popup.VisibleRowCount - 1);
			switch (keyEvent.KeyCode)
			{
				case Keys.Down when !keyEvent.Shift:
					this.MoveHighlight(1, wrap: true);
					break;

				case Keys.Up when !keyEvent.Shift:
					this.MoveHighlight(-1, wrap: true);
					break;

				case Keys.PageDown when !keyEvent.Shift:
					this.MoveHighlight(page, wrap: false);
					break;

				case Keys.PageUp when !keyEvent.Shift:
					this.MoveHighlight(-page, wrap: false);
					break;

				case Keys.Enter:
				case Keys.Tab when !keyEvent.Shift:
					this.Accept(this.HighlightIndex);
					break;

				case Keys.Escape:
					this.Close();
					break;

				default:
					return;
			}

			keyEvent.Handled = true;
			keyEvent.SuppressKeyPress = true;
		}

		private void Field_KeyPressed(object sender, KeyPressEventArgs keyPressEvent)
		{
			// Only a typed character opens the list; edits and caret moves refilter one that is already open.
			if (keyPressEvent.KeyChar >= ' '
				&& !this.field.ReadOnly)
			{
				this.Requery();
			}
		}

		private void Field_EditedOrCaretMoved(object sender, EventArgs e)
		{
			if (this.IsOpen && !this.accepting)
			{
				this.Requery();
			}
		}

		private void Field_FocusChanged(object sender, EventArgs e)
		{
			if (!this.field.InternalTextEditWidget.Focused)
			{
				this.Close();
			}
		}

		private void Field_Closed(object sender, EventArgs e)
		{
			this.Dispose();
		}
	}
}
