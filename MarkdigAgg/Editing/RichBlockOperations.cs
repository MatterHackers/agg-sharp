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

namespace Markdig.Agg.Editing
{
	public enum RichListKind
	{
		None,
		Bullet,
		Numbered,

		/// <summary>
		/// Some selected blocks are list items, but they are not all items of one kind.
		/// </summary>
		Mixed,
	}

	/// <summary>
	/// What the formatting toolbar shows for a selection.
	/// </summary>
	/// <param name="HeadingLevel">0 for Normal (list items and quotes read as Normal), 1-6 for a heading, null when the blocks disagree.</param>
	/// <param name="List">Bullet / Numbered only when every selected block is an item of that kind.</param>
	/// <param name="Quote">True when every selected block is a quote paragraph.</param>
	/// <param name="Alignment">The alignment of the selected paragraphs and headings; null when they disagree or there are none.</param>
	/// <param name="CanAlign">False when no paragraph or heading is selected: alignment is not offered in lists and quotes.</param>
	public readonly record struct RichBlockState(int? HeadingLevel, RichListKind List, bool Quote, RichAlignment? Alignment, bool CanAlign);

	/// <summary>
	/// Block-level formatting on a <see cref="RichDocument"/>: Normal/Heading, bullet and numbered lists, quotes,
	/// alignment, list indent/outdent and the line-start shortcuts ("# ", "- ", "1. ", "&gt; "). Each op applies
	/// to every text block the selection touches (a selection ending at the very start of a block does not touch
	/// it, as in other editors), mutates in place and marks changed blocks Dirty. No op adds or removes blocks, so
	/// the caller's selection stays valid.
	/// <para>
	/// Group rules live in <see cref="RichGroupRepair"/>, shared with <see cref="RichEditOperations"/>: a block
	/// that changes kind leaves its list or quote, a re-aligned block leaves its alignment group; whenever a group
	/// stops being one contiguous run, each later run gets a cloned group with OriginalMemberCount 0; lists start
	/// at Depth 0 with no level skipped; two different lists or quotes left side by side around the edit are forced
	/// to regenerate. Turning a list or quote on
	/// joins a touching list of the same kind or a touching quote, since a first-time user sees one list there.
	/// </para>
	/// </summary>
	public static class RichBlockOperations
	{
		/// <summary>
		/// Makes every selected text block a paragraph (<paramref name="headingLevel"/> 0, "Normal") or a heading of
		/// that level. List items and quotes leave their list or quote; paragraphs and headings keep their alignment.
		/// </summary>
		public static void SetBlockKind(RichDocument document, RichSelection selection, int headingLevel)
		{
			if (headingLevel < 0 || headingLevel > 6)
			{
				throw new ArgumentOutOfRangeException(nameof(headingLevel), "0 (Normal) or a heading level 1-6.");
			}

			var (first, last) = BlockRange(document, selection);
			var kind = headingLevel == 0 ? RichBlockKind.Paragraph : RichBlockKind.Heading;
			foreach (var block in TextBlocks(document, first, last))
			{
				if (block.Kind == kind && block.HeadingLevel == headingLevel)
				{
					continue;
				}

				LeaveListAndQuote(block);
				block.Kind = kind;
				block.HeadingLevel = headingLevel;
				block.Dirty = true;
			}

			RepairGroups(document, first, last, join: false);
		}

		/// <summary>
		/// The bullet / numbered toolbar button. When every selected block is already a list item of that kind they
		/// all become paragraphs; otherwise all become list items of that kind: an item of the other kind converts
		/// in place, anything else (headings included - a list item holds one paragraph) becomes a top-level item,
		/// joining a touching list of the same kind above or below.
		/// </summary>
		public static void ToggleList(RichDocument document, RichSelection selection, bool ordered)
		{
			var (first, last) = BlockRange(document, selection);
			var selected = TextBlocks(document, first, last).ToList();
			if (selected.Count == 0)
			{
				return;
			}

			if (selected.All(b => b.Kind == RichBlockKind.ListItem && b.List.Ordered == ordered))
			{
				MakeParagraphs(document, first, last, selected);
				return;
			}

			MakeListItems(document, first, last, ordered, ordered ? '.' : '-', 1);
		}

		/// <summary>
		/// The quote toolbar button: when every selected block is a quote paragraph they all leave the quote;
		/// otherwise all become paragraphs of one quote, joining a quote that touches the selection.
		/// </summary>
		public static void ToggleQuote(RichDocument document, RichSelection selection)
		{
			var (first, last) = BlockRange(document, selection);
			var selected = TextBlocks(document, first, last).ToList();
			if (selected.Count == 0)
			{
				return;
			}

			if (selected.All(b => b.Kind == RichBlockKind.Quote))
			{
				MakeParagraphs(document, first, last, selected);
				return;
			}

			MakeQuotes(document, first, last);
		}

		/// <summary>
		/// Aligns the selected paragraphs and headings. List items and quotes are skipped: alignment is not offered
		/// inside them. A re-aligned block leaves its &lt;div align&gt; group; the writer derives new wrappers.
		/// </summary>
		public static void SetAlignment(RichDocument document, RichSelection selection, RichAlignment alignment)
		{
			var (first, last) = BlockRange(document, selection);
			foreach (var block in TextBlocks(document, first, last))
			{
				if (!CanAlign(block) || block.Alignment == alignment)
				{
					continue;
				}

				block.Alignment = alignment;
				block.AlignGroup = null;
				block.Dirty = true;
			}

			RepairGroups(document, first, last, join: false);
		}

		/// <summary>
		/// Tab in a list: each selected item goes one level deeper with its subtree (the deeper items after it), but
		/// only under an item above it in the same list that is at least as deep - markdown cannot skip a level, and
		/// the first item of a list has nothing to nest under. Returns true when the selection holds list items, so
		/// the caller knows Tab was a list command even when nothing could move.
		/// </summary>
		public static bool Indent(RichDocument document, RichSelection selection)
		{
			var (first, last) = BlockRange(document, selection);
			var blocks = document.Blocks;
			bool anyItem = false;
			int movedThrough = -1;
			for (int i = first; i <= last; i++)
			{
				var block = blocks[i];
				if (block.Kind != RichBlockKind.ListItem)
				{
					continue;
				}

				anyItem = true;

				// An item moved as part of an earlier item's subtree is already one level deeper.
				if (i <= movedThrough)
				{
					continue;
				}

				var previous = i > 0 ? blocks[i - 1] : null;
				if (previous?.ListGroup != block.ListGroup || previous.List.Depth < block.List.Depth)
				{
					continue;
				}

				movedThrough = RichGroupRepair.SubtreeEnd(blocks, i);
				for (int j = i; j <= movedThrough; j++)
				{
					blocks[j].List.Depth++;
					blocks[j].Dirty = true;
				}

				RichGroupRepair.AdoptLevelStyle(blocks, i);
			}

			RepairGroups(document, first, last, join: false);
			return anyItem;
		}

		/// <summary>
		/// Shift-Tab in a list: each selected item comes up one level with its subtree; a top-level item leaves the
		/// list and becomes a paragraph (the items after it become a list of their own, lifted to start at the top
		/// level). Returns true when the selection holds list items.
		/// </summary>
		public static bool Outdent(RichDocument document, RichSelection selection)
		{
			var (first, last) = BlockRange(document, selection);
			var blocks = document.Blocks;
			bool anyItem = false;
			int movedThrough = -1;
			for (int i = first; i <= last; i++)
			{
				var block = blocks[i];
				if (block.Kind != RichBlockKind.ListItem)
				{
					continue;
				}

				anyItem = true;
				if (i <= movedThrough)
				{
					continue;
				}

				block.Dirty = true;
				if (block.List.Depth == 0)
				{
					// Its children are lifted by RepairGroups once they are split off as a list of their own.
					LeaveListAndQuote(block);
					block.Kind = RichBlockKind.Paragraph;
					continue;
				}

				movedThrough = RichGroupRepair.SubtreeEnd(blocks, i);
				for (int j = i; j <= movedThrough; j++)
				{
					blocks[j].List.Depth--;
					blocks[j].Dirty = true;
				}

				RichGroupRepair.AdoptLevelStyle(blocks, i);
			}

			RepairGroups(document, first, last, join: false);
			return anyItem;
		}

		/// <summary>
		/// Called after the user types a space. In a Paragraph whose text before the caret is exactly a marker and
		/// that space - "#", "##", "###" (heading 1-3), "-" or "*" (bullet), "N." (numbered from N) or "&gt;"
		/// (quote) - removes the marker, converts the block (joining a touching list or quote as the toolbar does)
		/// and puts the caret at the block's start. Returns false and changes nothing otherwise.
		/// </summary>
		public static bool TryApplyLineStartShortcut(RichDocument document, DocPosition caret, out DocPosition newCaret)
		{
			newCaret = caret;
			int index = caret.BlockIndex;
			if (index >= document.Blocks.Count)
			{
				return false;
			}

			var block = document.Blocks[index];
			if (block.Kind != RichBlockKind.Paragraph || caret.Offset < 2)
			{
				return false;
			}

			string typed = TextBefore(block.Inlines, caret.Offset);
			if (typed == null || typed[^1] != ' ')
			{
				return false;
			}

			string marker = typed.Substring(0, typed.Length - 1);
			Action convert = null;
			if (marker == "#" || marker == "##" || marker == "###")
			{
				convert = () => SetBlockKind(document, RichSelection.At(new DocPosition(index, 0)), marker.Length);
			}
			else if (marker == "-" || marker == "*")
			{
				convert = () => MakeListItems(document, index, index, false, marker[0], 1);
			}
			else if (marker == ">")
			{
				convert = () => MakeQuotes(document, index, index);
			}
			else if (IsListNumber(marker, out int start))
			{
				convert = () => MakeListItems(document, index, index, true, '.', start);
			}

			if (convert == null)
			{
				return false;
			}

			int end = RichInlines.SplitAt(block.Inlines, caret.Offset);
			block.Inlines.RemoveRange(0, end);
			block.Dirty = true;
			convert();
			newCaret = new DocPosition(index, 0);
			return true;
		}

		/// <summary>
		/// The toolbar state for the text blocks the selection touches (Normal, no list, no quote, alignment
		/// disabled when it touches none).
		/// </summary>
		public static RichBlockState BlockStateAt(RichDocument document, RichSelection selection)
		{
			var (first, last) = BlockRange(document, selection);
			var selected = TextBlocks(document, first, last).ToList();
			if (selected.Count == 0)
			{
				return new RichBlockState(0, RichListKind.None, false, null, false);
			}

			var levels = selected.Select(b => b.Kind == RichBlockKind.Heading ? b.HeadingLevel : 0).Distinct().ToList();
			var listKinds = selected.Select(b => b.Kind != RichBlockKind.ListItem
				? RichListKind.None
				: b.List.Ordered ? RichListKind.Numbered : RichListKind.Bullet).Distinct().ToList();
			var listKind = listKinds.Count == 1 ? listKinds[0]
				: listKinds.Any(k => k != RichListKind.None) ? RichListKind.Mixed : RichListKind.None;
			var alignments = selected.Where(CanAlign).Select(b => b.Alignment).Distinct().ToList();
			return new RichBlockState(
				levels.Count == 1 ? levels[0] : (int?)null,
				listKind,
				selected.All(b => b.Kind == RichBlockKind.Quote),
				alignments.Count == 1 ? alignments[0] : (RichAlignment?)null,
				alignments.Count > 0);
		}

		private static bool CanAlign(RichBlock block) => block.Kind == RichBlockKind.Paragraph || block.Kind == RichBlockKind.Heading;

		/// <summary>
		/// "N." with one to nine digits - CommonMark's limit, which also keeps the number within an int.
		/// </summary>
		private static bool IsListNumber(string marker, out int number)
		{
			number = 0;
			string digits = marker.Length >= 2 && marker[^1] == '.' ? marker.Substring(0, marker.Length - 1) : "";
			return digits.Length > 0 && digits.Length <= 9 && digits.All(c => c >= '0' && c <= '9') && int.TryParse(digits, out number);
		}

		/// <summary>
		/// The blocks a selection touches. A non-empty selection that ends at offset 0 of a later block stops before
		/// it: dragging to the start of the next line selects the lines above, not that one. In an empty document
		/// the range is empty (Last &lt; First), so every op is a no-op there.
		/// </summary>
		private static (int First, int Last) BlockRange(RichDocument document, RichSelection selection)
		{
			var start = selection.Start;
			var end = selection.End;
			int last = end.BlockIndex;
			if (!selection.IsEmpty && last > start.BlockIndex && end.Offset == 0 && end.Row == 0 && end.Column == 0)
			{
				last--;
			}

			return (start.BlockIndex, Math.Min(last, document.Blocks.Count - 1));
		}

		private static IEnumerable<RichBlock> TextBlocks(RichDocument document, int first, int last)
		{
			for (int i = first; i <= last; i++)
			{
				if (document.Blocks[i].IsTextBlock)
				{
					yield return document.Blocks[i];
				}
			}
		}

		/// <summary>
		/// Drops a block's list and quote membership; the caller sets its new kind. The alignment group is kept:
		/// only a paragraph or heading is ever in one, and those stay aligned when they change between the two.
		/// </summary>
		private static void LeaveListAndQuote(RichBlock block)
		{
			block.List = null;
			block.ListGroup = null;
			block.QuoteGroup = null;
		}

		/// <summary>
		/// Strips what a block cannot keep inside a list or quote: heading level, alignment and its wrapper.
		/// </summary>
		private static void ClearForContainer(RichBlock block)
		{
			block.HeadingLevel = 0;
			block.Alignment = RichAlignment.Left;
			block.AlignGroup = null;
			block.Dirty = true;
		}

		private static void MakeParagraphs(RichDocument document, int first, int last, List<RichBlock> selected)
		{
			foreach (var block in selected)
			{
				LeaveListAndQuote(block);
				block.Kind = RichBlockKind.Paragraph;
				block.Dirty = true;
			}

			RepairGroups(document, first, last, join: false);
		}

		/// <summary>
		/// Turns the text blocks first..last into list items of one kind. A nested item converts in place and stays
		/// in its parent's list - taking it out would tear it from its parent. A top-level item and a new item
		/// (headings included; new items are top-level) join the list above when it is of the same kind, otherwise
		/// a new list, and a top-level item brings its unselected subtree along so children stay with their parent.
		/// RepairGroups then splits off whatever no longer lines up and joins a matching list below.
		/// </summary>
		private static void MakeListItems(RichDocument document, int first, int last, bool ordered, char marker, int startNumber)
		{
			var blocks = document.Blocks;
			var above = first > 0 ? blocks[first - 1].ListGroup : null;
			var group = above != null && ListOrdered(document, above) == ordered ? above : new RichListGroup();
			group.OriginalMemberCount = 0;
			int end = last;
			for (int i = first; i <= last; i++)
			{
				var block = blocks[i];
				if (!block.IsTextBlock)
				{
					continue;
				}

				if (block.Kind == RichBlockKind.ListItem)
				{
					if (block.List.Ordered != ordered)
					{
						block.List.Ordered = ordered;
						block.List.Marker = marker;
						block.List.StartNumber = startNumber;
						block.ListGroup.OriginalMemberCount = 0;
						block.Dirty = true;
					}

					if (block.List.Depth > 0)
					{
						continue;
					}

					int subtreeEnd = RichGroupRepair.SubtreeEnd(blocks, i);
					for (int j = i + 1; j <= subtreeEnd; j++)
					{
						if (blocks[j].ListGroup != group)
						{
							blocks[j].ListGroup = group;
							blocks[j].Dirty = true;
						}
					}

					end = Math.Max(end, subtreeEnd);
				}
				else
				{
					block.QuoteGroup = null;
					ClearForContainer(block);
					block.Kind = RichBlockKind.ListItem;
					block.List = new RichListInfo { Ordered = ordered, Marker = marker, StartNumber = startNumber };
				}

				if (block.ListGroup != group)
				{
					block.ListGroup = group;
					block.Dirty = true;
				}
			}

			RepairGroups(document, first, end, join: true);
		}

		/// <summary>
		/// Turns the text blocks first..last into paragraphs of one quote: the quote above when one touches the
		/// selection, otherwise a new one (a quote below is joined by RepairGroups).
		/// </summary>
		private static void MakeQuotes(RichDocument document, int first, int last)
		{
			var group = (first > 0 ? document.Blocks[first - 1].QuoteGroup : null) ?? new RichQuoteGroup();
			group.OriginalMemberCount = 0;
			foreach (var block in TextBlocks(document, first, last))
			{
				if (block.Kind == RichBlockKind.Quote && block.QuoteGroup == group)
				{
					continue;
				}

				block.List = null;
				block.ListGroup = null;
				ClearForContainer(block);
				block.Kind = RichBlockKind.Quote;
				block.QuoteGroup = group;
			}

			RepairGroups(document, first, last, join: true);
		}

		/// <summary>
		/// Whether a list is numbered: the kind of its first (top-level) item. A list has one top-level kind;
		/// RepairGroups splits a list where that changes.
		/// </summary>
		private static bool ListOrdered(RichDocument document, RichListGroup group)
		{
			return document.Blocks.First(b => b.ListGroup == group).List.Ordered;
		}

		/// <summary>
		/// Restores the group invariants after an op changed blocks first..last: each group is one contiguous run,
		/// each list has one top-level kind and starts at Depth 0 with no skipped level, and lists or quotes
		/// touching the edit are joined (<paramref name="join"/>) or, where they stay apart, forced to regenerate.
		/// </summary>
		private static void RepairGroups(RichDocument document, int first, int last, bool join)
		{
			if (!join)
			{
				RichGroupRepair.Repair(document, first, last);
				return;
			}

			RichGroupRepair.SplitAllIntoRuns(document.Blocks);
			for (int i = Math.Max(1, first); i <= Math.Min(document.Blocks.Count - 1, last + 1); i++)
			{
				JoinIfTouching(document, i);
			}

			UnifyTopLevelMarkers(document, first, last);
			RichGroupRepair.NormalizeListDepths(document);
			RichGroupRepair.ForceRegenerationAround(document, first, last);
		}

		/// <summary>
		/// Joins the list or quote starting at block <paramref name="index"/> into a different one ending right above
		/// it - lists only when both are of the same top-level kind.
		/// </summary>
		private static void JoinIfTouching(RichDocument document, int index)
		{
			var blocks = document.Blocks;
			var before = blocks[index - 1];
			var after = blocks[index];
			if (before.QuoteGroup != null && after.QuoteGroup != null && before.QuoteGroup != after.QuoteGroup)
			{
				Absorb(blocks, index, before.QuoteGroup, b => b.QuoteGroup, (b, g) => b.QuoteGroup = (RichQuoteGroup)g);
			}

			if (before.ListGroup != null && after.ListGroup != null && before.ListGroup != after.ListGroup
				&& ListOrdered(document, before.ListGroup) == ListOrdered(document, after.ListGroup))
			{
				Absorb(blocks, index, before.ListGroup, b => b.ListGroup, (b, g) => b.ListGroup = (RichListGroup)g);
			}
		}

		private static void Absorb(
			List<RichBlock> blocks,
			int index,
			RichBlockGroup into,
			Func<RichBlock, RichBlockGroup> getGroup,
			Action<RichBlock, RichBlockGroup> setGroup)
		{
			var absorbed = getGroup(blocks[index]);
			for (int i = index; i < blocks.Count && getGroup(blocks[i]) == absorbed; i++)
			{
				setGroup(blocks[i], into);
				blocks[i].Dirty = true;
			}

			into.OriginalMemberCount = 0;
		}

		/// <summary>
		/// Top-level items of a list share one marker and start number, taken from its first item, so items that
		/// joined a list number on from it rather than restarting.
		/// </summary>
		private static void UnifyTopLevelMarkers(RichDocument document, int first, int last)
		{
			var blocks = document.Blocks;
			var groups = new HashSet<RichListGroup>();
			for (int i = Math.Max(0, first - 1); i <= Math.Min(blocks.Count - 1, last + 1); i++)
			{
				if (blocks[i].ListGroup != null)
				{
					groups.Add(blocks[i].ListGroup);
				}
			}

			foreach (var group in groups)
			{
				var lead = blocks.First(b => b.ListGroup == group).List;
				foreach (var item in blocks.Where(b => b.ListGroup == group && b.List.Depth == 0))
				{
					if (item.List.Marker != lead.Marker || item.List.StartNumber != lead.StartNumber)
					{
						item.List.Marker = lead.Marker;
						item.List.StartNumber = lead.StartNumber;
						item.Dirty = true;
					}
				}
			}
		}

		/// <summary>
		/// The plain text before <paramref name="offset"/>, or null when an atom or inline code is in it: a marker
		/// typed in code or after an image is content, not a shortcut.
		/// </summary>
		private static string TextBefore(List<RichInline> inlines, int offset)
		{
			var text = new StringBuilder();
			foreach (var inline in inlines)
			{
				if (text.Length >= offset)
				{
					break;
				}

				if (inline is not RichRun run || run.Code)
				{
					return null;
				}

				text.Append(run.Text);
			}

			return text.Length >= offset ? text.ToString(0, offset) : null;
		}
	}
}
