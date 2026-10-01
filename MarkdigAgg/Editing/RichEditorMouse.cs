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
using System.Linq;
using System.Text;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Mouse handling for <see cref="RichMarkdownEditWidget"/>: a click places the caret, a drag extends the
	/// selection (scrolling while the mouse is held past the view's edge), shift-click extends from the anchor, a
	/// double click selects a word and a triple click the block's (or table cell's) text, and dragging after either
	/// grows the selection by whole words or blocks. A Raw block is selected whole by a click and raises
	/// <see cref="RichMarkdownEditWidget.RawBlockActivated"/> on a double click. Cmd/Ctrl+click on a link (in a
	/// Raw block too) raises <see cref="RichMarkdownEditWidget.LinkClicked"/>; a plain click on one is just a click.
	/// </summary>
	internal sealed class RichEditorMouse
	{
		// An atom (image, inline html) stands as one character that is never part of a word.
		private const char AtomCharacter = '￼';

		// The most one auto-scroll step moves, so a mouse flung far past the edge still scrolls at a readable pace.
		private const double MaxAutoScrollStep = 40;

		private readonly RichMarkdownEditWidget editor;
		private bool dragging;
		private bool autoScrolling;
		private Vector2 lastDrag;

		// 1 by characters, 2 by words, 3 by blocks (cells in a table).
		private int selectClicks = 1;

		// The unit the press selected (a word, a block's text, a Raw block, or one position for a plain click):
		// a drag keeps it selected and grows away from it.
		private DocPosition pivotStart;
		private DocPosition pivotEnd;
		private int pressedRawBlock = -1;

		public RichEditorMouse(RichMarkdownEditWidget editor)
		{
			this.editor = editor;
		}

		/// <summary>
		/// Reads a key's state; tests replace it so modifier clicks do not depend on the shared keyboard state.
		/// </summary>
		internal Func<Keys, bool> IsKeyDown { get; set; } = Keyboard.IsKeyDown;

		// Every host (mac and browser included) reports Command as Control; the Windows keys are not read because
		// they can stay latched on X11 and in the browser.
		private bool LinkModifierDown => IsKeyDown(Keys.Control);

		// Nothing to hit test until the view has a width and its blocks are laid out.
		private bool LaidOut => editor.DocumentView.Width > 0 && editor.Style != null;

		public void Down(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button != MouseButtons.Left || !OverText(mouseEvent.Position) || !LaidOut)
			{
				return;
			}

			editor.Focus();
			var point = ToView(mouseEvent.Position);
			int blockIndex = BlockAt(point.Y);
			var block = editor.Document.Blocks[blockIndex];

			if (LinkModifierDown && LinkAt(blockIndex, point) is string url)
			{
				editor.RaiseLinkClicked(url);
				return;
			}

			dragging = true;
			pressedRawBlock = -1;
			var hit = HitTest(blockIndex, point);
			var position = new DocPosition(blockIndex, hit.Offset, hit.Row, hit.Column);
			if (IsKeyDown(Keys.Shift))
			{
				// The anchor stays where the selection started; only the caret follows the click.
				selectClicks = 1;
				pivotStart = pivotEnd = editor.Selection.Anchor;
				ExtendTo(position, hit.AtLineEnd);
				return;
			}

			if (block.Kind == RichBlockKind.Raw)
			{
				selectClicks = 1;
				pressedRawBlock = blockIndex;
				var whole = RichEditOperations.WholeBlock(editor.Document, blockIndex);
				(pivotStart, pivotEnd) = (whole.Start, whole.End);
				editor.SetSelectionByUser(whole, caretAtLineEnd: false);
				if (mouseEvent.Clicks == 2)
				{
					editor.RaiseRawBlockActivated(RichMarkdownWriter.SourceOffsetOf(editor.Document, blockIndex));
				}

				return;
			}

			selectClicks = Math.Clamp(mouseEvent.Clicks, 1, 3);
			(pivotStart, pivotEnd) = UnitAt(position);
			if (selectClicks == 1)
			{
				editor.SetSelectionByUser(RichSelection.At(position), hit.AtLineEnd);
			}
			else
			{
				editor.SetSelectionByUser(new RichSelection(pivotStart, pivotEnd), caretAtLineEnd: false);
			}
		}

		public void Move(MouseEventArgs mouseEvent)
		{
			if (!LaidOut)
			{
				return;
			}

			if (!dragging)
			{
				UpdateCursor(mouseEvent.Position);
				return;
			}

			lastDrag = mouseEvent.Position;
			if (PastEdge(lastDrag.Y) != 0)
			{
				// Held past the edge, the view keeps scrolling on a timer until the mouse comes back or is released.
				if (!autoScrolling)
				{
					AutoScrollStep();
				}

				return;
			}

			ExtendToPoint(lastDrag);
		}

		public void Up(MouseEventArgs mouseEvent)
		{
			dragging = false;
			pressedRawBlock = -1;
		}

		private void ExtendToPoint(Vector2 editorPosition)
		{
			var point = ToView(editorPosition);
			int blockIndex = BlockAt(point.Y);
			var hit = HitTest(blockIndex, point);
			ExtendTo(new DocPosition(blockIndex, hit.Offset, hit.Row, hit.Column), hit.AtLineEnd);
		}

		/// <summary>
		/// Grows the selection from the pressed unit to the unit at <paramref name="position"/>, so the word, block
		/// or Raw block first selected stays selected whichever way the drag goes.
		/// </summary>
		private void ExtendTo(DocPosition position, bool atLineEnd)
		{
			if (pressedRawBlock >= 0 && position.BlockIndex == pressedRawBlock)
			{
				// Still on the Raw block it started on: it stays selected whole.
				return;
			}

			var (unitStart, unitEnd) = UnitAt(position);
			var selection = position >= pivotEnd
				? new RichSelection(pivotStart, unitEnd)
				: new RichSelection(pivotEnd, unitStart);
			bool lineEnd = selectClicks == 1 && atLineEnd;
			if (selection != editor.Selection || lineEnd != editor.CaretAtLineEnd)
			{
				editor.SetSelectionByUser(selection, lineEnd);
			}
		}

		// The unit a position falls in for the current click count; a Raw block is always one unit.
		private (DocPosition Start, DocPosition End) UnitAt(DocPosition position)
		{
			var block = editor.Document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.Raw)
			{
				return (position with { Offset = 0 }, position with { Offset = block.TextLength() });
			}

			switch (selectClicks)
			{
				case 2:
					var (start, end) = WordAt(block, position);
					return (position with { Offset = start }, position with { Offset = end });
				case 3:
					return (position with { Offset = 0 }, position with { Offset = block.TextLength(position.Row, position.Column) });
				default:
					return (position, position);
			}
		}

		private void AutoScrollStep()
		{
			double past = PastEdge(lastDrag.Y);
			if (!dragging || past == 0 || editor.HasBeenClosed)
			{
				autoScrolling = false;
				return;
			}

			double before = editor.ScrollOffsetFromTop();
			editor.SetScrollOffsetFromTop(before - Math.Clamp(past / 2, -MaxAutoScrollStep, MaxAutoScrollStep));
			ExtendToPoint(new Vector2(lastDrag.X, Math.Clamp(lastDrag.Y, 1, editor.Height - 1)));
			if (editor.ScrollOffsetFromTop() == before)
			{
				// At the top or bottom of the document: nothing more to scroll to.
				autoScrolling = false;
				return;
			}

			autoScrolling = true;
			UiThread.RunOnIdle(AutoScrollStep, 0.05);
		}

		// How far above (positive) or below (negative) the view a y in editor coordinates is.
		private double PastEdge(double y) => y > editor.Height ? y - editor.Height : y < 0 ? y : 0;

		// A press on the scroll bar belongs to it, not to the text.
		private bool OverText(Vector2 position)
		{
			var bar = editor.VerticalScrollBar;
			return !(bar != null && bar.Visible && bar.BoundsRelativeToParent.Contains(position));
		}

		private Vector2 ToView(Vector2 editorPosition) => editor.DocumentView.TransformFromParentSpace(editor, editorPosition);

		/// <summary>
		/// The block at a y in <see cref="RichMarkdownEditWidget.DocumentView"/> coordinates: the one whose box holds
		/// it, else the nearest (above the first block is the first, below the last is the last).
		/// </summary>
		private int BlockAt(double y)
		{
			int nearest = 0;
			double nearestDistance = double.MaxValue;
			for (int i = 0; i < editor.Document.Blocks.Count; i++)
			{
				var bounds = editor.BlockBounds(i);
				double distance = y > bounds.Top ? y - bounds.Top : y < bounds.Bottom ? bounds.Bottom - y : 0;
				if (distance < nearestDistance)
				{
					nearest = i;
					nearestDistance = distance;
				}
			}

			return nearest;
		}

		private RichCaret HitTest(int blockIndex, Vector2 point)
		{
			return editor.BlockLayout(blockIndex).HitTest(new Vector2(point.X, point.Y - editor.BlockOrigin(blockIndex)));
		}

		private void UpdateCursor(Vector2 position)
		{
			if (!OverText(position))
			{
				return;
			}

			var point = ToView(position);
			int blockIndex = BlockAt(point.Y);
			if (LinkModifierDown && LinkAt(blockIndex, point) != null)
			{
				editor.ShowCursor(Cursors.Hand);
			}
			else
			{
				editor.ShowCursor(editor.Document.Blocks[blockIndex].Kind == RichBlockKind.Raw ? Cursors.Arrow : Cursors.IBeam);
			}
		}

		/// <summary>
		/// The url of the link drawn under <paramref name="point"/> (view coordinates), or null. Found from the drawn
		/// fragment rather than the hit-test caret, which can land just past the link's last character.
		/// </summary>
		private string LinkAt(int blockIndex, Vector2 point)
		{
			var block = editor.Document.Blocks[blockIndex];
			if (block.Kind == RichBlockKind.Raw)
			{
				return RawLinkAt(blockIndex, point);
			}

			if (!block.IsTextBlock && block.Kind != RichBlockKind.Table)
			{
				return null;
			}

			var inBlock = new Vector2(point.X, point.Y - editor.BlockOrigin(blockIndex));
			List<RichLayoutLine> lines;
			List<RichInline> inlines;
			if (editor.BlockLayout(blockIndex) is RichTableLayout table)
			{
				var caret = table.HitTest(inBlock);
				lines = table.Cell(caret.Row, caret.Column).Lines;
				inlines = block.InlinesAt(caret.Row, caret.Column);
			}
			else
			{
				lines = ((RichBlockLayout)editor.BlockLayout(blockIndex)).Lines;
				inlines = block.Inlines;
			}

			foreach (var line in lines)
			{
				if (inBlock.Y < line.Bottom || inBlock.Y > line.Top)
				{
					continue;
				}

				foreach (var fragment in line.Fragments)
				{
					if (fragment.InlineIndex < inlines.Count
						&& inBlock.X >= fragment.X
						&& inBlock.X <= fragment.X + fragment.Width
						&& !string.IsNullOrEmpty(inlines[fragment.InlineIndex].LinkUrl))
					{
						return inlines[fragment.InlineIndex].LinkUrl;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// The url of a link the viewer drew in a Raw block's host under <paramref name="point"/> (view coordinates).
		/// The host is not selectable, so its links never see the click themselves.
		/// </summary>
		private string RawLinkAt(int blockIndex, Vector2 point)
		{
			var host = editor.RawBlockWidget(blockIndex);
			var link = host?.DescendantsAndSelf<TextLinkX>().FirstOrDefault(widget =>
			{
				var inLink = widget.TransformFromParentSpace(editor.DocumentView, point);
				return widget.PositionWithinLocalBounds(inLink.X, inLink.Y);
			});
			return link?.Url;
		}

		/// <summary>
		/// The word (or run of spaces, or single other character) at a caret, as offsets in its block or cell. A
		/// caret just after a word, as a click on its last letter's right half gives, selects that word.
		/// </summary>
		internal static (int Start, int End) WordAt(RichBlock block, DocPosition position)
		{
			string text = PlainText(block, position.Row, position.Column);
			if (text.Length == 0)
			{
				return (0, 0);
			}

			int index = Math.Min(position.Offset, text.Length - 1);
			if (position.Offset > 0
				&& ClassOf(text[position.Offset - 1]) == 1
				&& (position.Offset >= text.Length || ClassOf(text[position.Offset]) != 1))
			{
				index = position.Offset - 1;
			}

			int kind = ClassOf(text[index]);
			if (kind >= 3)
			{
				return (index, index + 1);
			}

			int start = index;
			while (start > 0 && ClassOf(text[start - 1]) == kind)
			{
				start--;
			}

			int end = index + 1;
			while (end < text.Length && ClassOf(text[end]) == kind)
			{
				end++;
			}

			return (start, end);
		}

		// 1 word, 2 spaces, 3 anything else (selected one at a time: punctuation, line breaks and atoms).
		private static int ClassOf(char c)
		{
			if (char.IsLetterOrDigit(c) || c == '_')
			{
				return 1;
			}

			return c == ' ' || c == '\t' ? 2 : 3;
		}

		/// <summary>
		/// A block's (or table cell's) text with one character per caret offset: code as written, runs as their
		/// text, each atom as one placeholder.
		/// </summary>
		private static string PlainText(RichBlock block, int row, int column)
		{
			if (block.Kind == RichBlockKind.CodeBlock)
			{
				return block.CodeText;
			}

			if (block.Kind == RichBlockKind.Raw)
			{
				return AtomCharacter.ToString();
			}

			var text = new StringBuilder();
			foreach (var inline in block.InlinesAt(row, column))
			{
				if (inline is RichRun run)
				{
					text.Append(run.Text);
				}
				else
				{
					text.Append(AtomCharacter, inline.Length);
				}
			}

			return text.ToString();
		}
	}
}
