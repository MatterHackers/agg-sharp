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

using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Cut, copy and paste between the rich editor and the system clipboard.
	/// <para>
	/// Copy puts the text a reader sees as the plain text - pasting into a plain field anywhere else gives the
	/// words, not markdown punctuation - and the selection's markdown rendered as styled HTML for rich targets
	/// (mail, documents). The clipboard holds only text and HTML, so the editor also keeps the copied fragment
	/// itself: a paste whose clipboard text is still that copy's text inserts the fragment, styles and all.
	/// </para>
	/// <para>
	/// Any other clipboard text is read as markdown, so text written with **bold**, lists or headings pastes
	/// formatted, except that a single line break stays a line break so pasted plain text keeps its lines. A
	/// single plain line goes in as typing would, in the caret's style. A code block takes only plain text and a
	/// table cell one line. A paste is never merged with typing in the undo history.
	/// </para>
	/// </summary>
	internal sealed class RichEditorClipboard
	{
		private readonly RichMarkdownEditWidget editor;
		private readonly RichEditorKeyboard keyboard;

		// The last copy's fragment and the plain text it put on the clipboard, which identifies it on paste. Shared
		// by every editor in the process, as the clipboard is, so a copy from one notes field pastes formatted into
		// another.
		private static RichDocument copiedFragment;
		private static string copiedText;

		public RichEditorClipboard(RichMarkdownEditWidget editor, RichEditorKeyboard keyboard)
		{
			this.editor = editor;
			this.keyboard = keyboard;
		}

		/// <summary>
		/// Copies the selection; false when there is nothing selected or no clipboard.
		/// </summary>
		public bool Copy()
		{
			var clipboard = Clipboard.Instance;
			if (editor.Selection.IsEmpty || clipboard == null)
			{
				return false;
			}

			var fragment = RichPasteOperations.Slice(editor.Document, editor.Selection);
			if (fragment.Blocks.Count == 0)
			{
				return false;
			}

			string markdown = RichMarkdownWriter.Write(fragment);
			string text = RichPasteOperations.VisibleText(fragment);
			if (text.Length == 0)
			{
				// An image alone has no visible text; its markdown still says what was copied.
				text = markdown.Trim();
			}

			// A light, default theme rather than the editor's: a copy from a dark theme would otherwise carry its
			// dark background and light text into the mail or document it is pasted into.
			var neutral = new ThemeConfig { DefaultFontSize = editor.Theme.DefaultFontSize };
			string html = AggMarkdownDocument.ToStyledHtml(markdown, neutral);
			if (string.IsNullOrWhiteSpace(html))
			{
				clipboard.SetText(text);
			}
			else
			{
				clipboard.SetTextAndHtml(text, html);
			}

			copiedFragment = fragment;
			copiedText = text.Replace("\r\n", "\n");
			return true;
		}

		/// <summary>
		/// Copies the selection, then deletes it as one undo step.
		/// </summary>
		public void Cut()
		{
			if (Copy())
			{
				keyboard.Apply(RichEditKind.Other, (document, selection) => (RichEditOperations.DeleteSelection(document, selection), true));
			}
		}

		/// <summary>
		/// Pastes at the caret, replacing the selection in the same undo step. The host gets the first chance
		/// (<see cref="RichMarkdownEditWidget.PasteRequested"/>), so an application can paste an image itself.
		/// </summary>
		public void Paste()
		{
			if (editor.RaisePasteRequested())
			{
				return;
			}

			var clipboard = Clipboard.Instance;
			if (clipboard == null || !clipboard.ContainsText)
			{
				return;
			}

			string text = clipboard.GetText();
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			text = text.Replace("\r\n", "\n");

			// A system clipboard may hand back line endings of its own, so the copy is recognised by its text alone.
			var fragment = copiedFragment != null && text == copiedText ? copiedFragment : null;
			var pending = editor.PendingStyle;
			keyboard.Apply(RichEditKind.Other, (document, selection) =>
			{
				var caret = selection.IsEmpty ? selection.Caret : RichEditOperations.DeleteSelection(document, selection).Caret;
				var kind = document.Blocks.Count == 0 ? RichBlockKind.Paragraph : document.Blocks[caret.BlockIndex].Kind;
				if (kind == RichBlockKind.CodeBlock)
				{
					string plain = fragment != null ? RichPasteOperations.VisibleText(fragment) : text;
					return (RichEditOperations.InsertText(document, caret, plain), true);
				}

				var pasted = fragment ?? RichPasteOperations.ParseClipboardText(text);
				if (fragment == null && PlainLine(pasted) is string line)
				{
					var style = pending.IsEmpty || kind == RichBlockKind.Raw
						? null
						: pending.ApplyPending(RichStyleOperations.TypingStyle(document, caret));
					return (RichEditOperations.InsertText(document, caret, line, style), true);
				}

				return (RichPasteOperations.InsertDocument(document, caret, pasted), true);
			}, relayoutAll: true);
		}

		/// <summary>
		/// The text of a parsed paste that is one paragraph with no formatting at all, or null.
		/// </summary>
		private static string PlainLine(RichDocument parsed)
		{
			if (parsed.Blocks.Count != 1 || parsed.Blocks[0].Kind != RichBlockKind.Paragraph || parsed.Blocks[0].AlignGroup != null)
			{
				return null;
			}

			foreach (var inline in parsed.Blocks[0].Inlines)
			{
				if (inline is not RichRun run || run.Bold || run.Italic || run.Strike || run.Code || run.LinkUrl != null)
				{
					return null;
				}
			}

			return RichPasteOperations.VisibleText(parsed);
		}
	}
}
