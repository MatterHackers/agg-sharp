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
using System.Globalization;
using System.Text;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Text edits on a <see cref="RichDocument"/>: typing, deleting a range, Enter, Backspace and Delete. Each op
	/// mutates the document in place, marks every block it changes Dirty, and returns the selection to show next
	/// (usually a caret; Backspace/Delete next to a Raw block select it whole instead of merging into it).
	/// <para>
	/// Group rules (see <see cref="RichBlockGroup"/>): a split copies the block's groups, a merge keeps the
	/// survivor's, and a block that leaves its list or quote has its reference set to null. When it leaves from
	/// the middle, the members after it move to a new group of the same kind, so every group stays one contiguous
	/// run - the writer sees the changed member counts and regenerates both parts. A removal that leaves two lists
	/// or two quotes side by side forces both to regenerate, and list depths always stay expressible (each list
	/// starts at Depth 0, no item more than one level below the item before it).
	/// </para>
	/// <para>
	/// Edits inside one code block or table go to <see cref="RichTableCodeOperations"/>; across blocks code
	/// blocks and tables behave like Raw blocks (removed whole when any of them is in a deleted range, selected
	/// whole rather than merged into).
	/// </para>
	/// </summary>
	public static class RichEditOperations
	{
		/// <summary>
		/// Types <paramref name="text"/> at <paramref name="position"/>. Each '\n' (or "\r\n") acts like Enter
		/// (<see cref="SplitBlock"/>), so pasting lines into a list makes list items. The text takes
		/// <paramref name="style"/> when given (a pending style the user picked), otherwise the style of the
		/// nearest run before the caret - never from an atom, so typing after an image continues the text before
		/// it; at a block's start it takes the first run's style. A link or inline code does not extend past its
		/// end. At a Raw block the text goes into a new paragraph above (caret before it) or below (caret after
		/// it). Empty text changes nothing.
		/// </summary>
		public static RichSelection InsertText(RichDocument document, DocPosition position, string text, RichRun style = null)
		{
			if (string.IsNullOrEmpty(text))
			{
				return RichSelection.At(position);
			}

			if (IsCodeOrTable(document.Blocks[position.BlockIndex]))
			{
				return RichTableCodeOperations.InsertText(document, position, text, style);
			}

			var caret = TextCaret(document, position);
			style ??= StyleAt(document.Blocks[caret.BlockIndex].Inlines, caret.Offset);
			var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				if (i > 0)
				{
					caret = SplitBlock(document, caret).Caret;
				}

				if (lines[i].Length > 0)
				{
					var block = document.Blocks[caret.BlockIndex];
					int index = RichInlines.SplitAt(block.Inlines, caret.Offset);
					block.Inlines.Insert(index, style.WithText(lines[i]));
					RichInlines.MergeAdjacent(block.Inlines);
					block.Dirty = true;
					caret = caret with { Offset = caret.Offset + lines[i].Length };
				}
			}

			return RichSelection.At(caret);
		}

		/// <summary>
		/// Removes everything between two positions (in either order). Within one text block it removes the text.
		/// Across blocks the blocks in between go whole; if both ends are in text blocks the end block's remainder
		/// joins the start block, which keeps its kind, alignment and groups. A Raw (or code/table) block at either
		/// end is removed whole if any of it is in the range, and then nothing merges across it. Within one code
		/// block or table only the covered text goes (<see cref="RichTableCodeOperations.DeleteRange"/>), even
		/// when that is all of it; removing such a block on its own takes <see cref="DeleteSelection"/> with a
		/// <see cref="RichSelection.WholeBlock"/> selection.
		/// </summary>
		/// <summary>
		/// Deletes a selection: a <see cref="RichSelection.WholeBlock"/> selection removes its block, any other
		/// is a <see cref="DeleteRange"/>. This is what Delete, Backspace and typing over a selection call.
		/// </summary>
		public static RichSelection DeleteSelection(RichDocument document, RichSelection selection)
		{
			if (selection.WholeBlock)
			{
				int index = selection.Anchor.BlockIndex;
				return RemoveBlocks(document, index, index);
			}

			return DeleteRange(document, selection.Anchor, selection.Caret);
		}

		public static RichSelection DeleteRange(RichDocument document, DocPosition a, DocPosition b)
		{
			var start = DocPosition.Min(a, b);
			var end = DocPosition.Max(a, b);
			var blocks = document.Blocks;
			if (start == end)
			{
				return RichSelection.At(start);
			}

			if (start.BlockIndex == end.BlockIndex)
			{
				var block = blocks[start.BlockIndex];
				if (block.IsTextBlock)
				{
					RemoveText(block, start.Offset, end.Offset);
					return RichSelection.At(start);
				}

				if (IsCodeOrTable(block))
				{
					return RichTableCodeOperations.DeleteRange(document, start, end);
				}

				return RemoveBlocks(document, start.BlockIndex, start.BlockIndex);
			}

			int first = start.BlockIndex;
			int last = end.BlockIndex;
			var firstBlock = blocks[first];
			var lastBlock = blocks[last];
			if (firstBlock.IsTextBlock && lastBlock.IsTextBlock)
			{
				RemoveText(firstBlock, start.Offset, firstBlock.TextLength());
				RemoveText(lastBlock, 0, end.Offset);
				AppendInlines(firstBlock, lastBlock);
				RemoveBlockRange(document, first + 1, last - first);
				return RichSelection.At(start);
			}

			// A non-text end is kept only when the range merely touches it (starts at its end or ends at its start).
			bool keepFirst = firstBlock.IsTextBlock || start == EndOf(document, first);
			bool keepLast = lastBlock.IsTextBlock || end == new DocPosition(last, 0);
			if (firstBlock.IsTextBlock)
			{
				RemoveText(firstBlock, start.Offset, firstBlock.TextLength());
			}

			if (lastBlock.IsTextBlock)
			{
				RemoveText(lastBlock, 0, end.Offset);
			}

			int removeFrom = keepFirst ? first + 1 : first;
			int removeTo = keepLast ? last - 1 : last;
			if (removeFrom > removeTo)
			{
				return RichSelection.At(start);
			}

			var afterRemoval = RemoveBlocks(document, removeFrom, removeTo);
			return keepFirst ? RichSelection.At(start) : afterRemoval;
		}

		/// <summary>
		/// Enter at <paramref name="position"/>. Normally splits the block in two: the new block below copies its
		/// kind, heading level, list info, alignment and groups. Special cases a first-time user expects:
		/// Enter in an empty list item outdents it (Depth &gt; 0) or turns it into a paragraph that leaves the
		/// list; Enter in an empty quote paragraph leaves the quote; Enter at the start of a non-empty heading
		/// adds an empty paragraph above it; Enter at the end of a heading starts a paragraph, not another heading.
		/// At a Raw block it adds an empty paragraph above (caret before the block) or below (caret after it).
		/// </summary>
		public static RichSelection SplitBlock(RichDocument document, DocPosition position)
		{
			var blocks = document.Blocks;
			int index = position.BlockIndex;
			var block = blocks[index];
			if (block.Kind == RichBlockKind.Raw)
			{
				return RichSelection.At(InsertParagraphBesideRaw(document, position));
			}

			if (IsCodeOrTable(block))
			{
				return RichTableCodeOperations.SplitBlock(document, position);
			}

			int length = block.TextLength();
			if (length == 0 && block.Kind == RichBlockKind.ListItem)
			{
				OutdentOrLeaveList(document, index);
				return RichSelection.At(position);
			}

			if (length == 0 && block.Kind == RichBlockKind.Quote)
			{
				LeaveQuote(document, index);
				return RichSelection.At(position);
			}

			if (block.Kind == RichBlockKind.Heading && position.Offset == 0 && length > 0)
			{
				var above = NewBlockLike(document, block, RichBlockKind.Paragraph);

				// The new block takes the heading's place, so it takes the gap before it too.
				above.SeparatorBefore = block.SeparatorBefore;
				block.SeparatorBefore = BlankLine(document);
				blocks.Insert(index, above);
				return RichSelection.At(new DocPosition(index + 1, 0));
			}

			var kind = block.Kind == RichBlockKind.Heading && position.Offset == length ? RichBlockKind.Paragraph : block.Kind;
			var below = NewBlockLike(document, block, kind);
			int split = RichInlines.SplitAt(block.Inlines, position.Offset);
			below.Inlines = block.Inlines.GetRange(split, block.Inlines.Count - split);
			block.Inlines.RemoveRange(split, block.Inlines.Count - split);
			block.Dirty = true;
			blocks.Insert(index + 1, below);
			return RichSelection.At(new DocPosition(index + 1, 0));
		}

		/// <summary>
		/// Backspace with a collapsed caret. Inside text it removes one character (a whole emoji or combining
		/// sequence) or atom. At a block's start: a list item outdents, or at Depth 0 becomes a paragraph leaving
		/// the list; a quote paragraph leaves the quote; a heading becomes a paragraph; a paragraph merges into the
		/// text block before it. A Raw (or code/table) block before the caret is selected whole instead, so the
		/// next Backspace deletes it knowingly. Just after a Raw block's own caret stop it selects that block; at
		/// its start the caret moves to the end of a text block before it.
		/// </summary>
		public static RichSelection Backspace(RichDocument document, DocPosition position)
		{
			var blocks = document.Blocks;
			int index = position.BlockIndex;
			var block = blocks[index];
			if (IsCodeOrTable(block))
			{
				return RichTableCodeOperations.Backspace(document, position);
			}

			if (block.Kind == RichBlockKind.Raw)
			{
				if (position.Offset > 0)
				{
					return WholeBlock(document, index);
				}

				if (index == 0)
				{
					return RichSelection.At(position);
				}

				// Nothing merges into a Raw block, so the caret steps back to the end of the block before it.
				return blocks[index - 1].IsTextBlock ? RichSelection.At(EndOf(document, index - 1)) : WholeBlock(document, index - 1);
			}

			if (position.Offset > 0)
			{
				int previous = PreviousStop(block.Inlines, position.Offset);
				RemoveText(block, previous, position.Offset);
				return RichSelection.At(position with { Offset = previous });
			}

			switch (block.Kind)
			{
				case RichBlockKind.ListItem:
					OutdentOrLeaveList(document, index);
					return RichSelection.At(position);

				case RichBlockKind.Quote:
					LeaveQuote(document, index);
					return RichSelection.At(position);

				case RichBlockKind.Heading:
					// The block keeps its alignment group: it is still a paragraph or heading, which the wrapper holds.
					block.Kind = RichBlockKind.Paragraph;
					block.HeadingLevel = 0;
					block.Dirty = true;
					return RichSelection.At(position);
			}

			if (index == 0)
			{
				return RichSelection.At(position);
			}

			var before = blocks[index - 1];
			if (!before.IsTextBlock)
			{
				return WholeBlock(document, index - 1);
			}

			var caret = new DocPosition(index - 1, before.TextLength());
			AppendInlines(before, block);
			RemoveBlockRange(document, index, 1);
			return RichSelection.At(caret);
		}

		/// <summary>
		/// Forward Delete with a collapsed caret: the mirror of <see cref="Backspace"/>. Inside text it removes the
		/// next character or atom; at a text block's end the next text block joins this one (this block keeps its
		/// kind and groups); a Raw (or code/table) block after the caret is selected whole instead. Before a Raw
		/// block's own caret stop it selects that block.
		/// </summary>
		public static RichSelection Delete(RichDocument document, DocPosition position)
		{
			var blocks = document.Blocks;
			int index = position.BlockIndex;
			var block = blocks[index];
			if (IsCodeOrTable(block))
			{
				return RichTableCodeOperations.Delete(document, position);
			}

			if (block.Kind == RichBlockKind.Raw && position.Offset == 0)
			{
				return WholeBlock(document, index);
			}

			int length = block.TextLength();
			if (position.Offset < length)
			{
				int next = NextStop(block.Inlines, position.Offset);
				RemoveText(block, position.Offset, next);
				return RichSelection.At(position);
			}

			if (index == blocks.Count - 1)
			{
				return RichSelection.At(position);
			}

			var after = blocks[index + 1];
			if (!after.IsTextBlock)
			{
				return WholeBlock(document, index + 1);
			}

			if (!block.IsTextBlock)
			{
				// Text cannot join a Raw block; the caret simply stays after it.
				return RichSelection.At(position);
			}

			AppendInlines(block, after);
			RemoveBlockRange(document, index + 1, 1);
			return RichSelection.At(position);
		}

		/// <summary>
		/// A selection of a block as an object, from its first caret stop to its last (for a table, the last
		/// cell's end), flagged <see cref="RichSelection.WholeBlock"/> so deleting it removes the block.
		/// </summary>
		public static RichSelection WholeBlock(RichDocument document, int index)
		{
			return new RichSelection(new DocPosition(index, 0), EndOf(document, index), WholeBlock: true);
		}

		internal static DocPosition EndOf(RichDocument document, int index)
		{
			var block = document.Blocks[index];
			if (block.Kind == RichBlockKind.Table && block.TableRows.Count > 0)
			{
				int row = block.TableRows.Count - 1;
				int column = block.TableRows[row].Count - 1;
				return new DocPosition(index, block.TextLength(row, column), row, column);
			}

			return new DocPosition(index, block.TextLength());
		}

		private static bool IsCodeOrTable(RichBlock block)
		{
			return block.Kind == RichBlockKind.CodeBlock || block.Kind == RichBlockKind.Table;
		}

		/// <summary>
		/// The caret typed text goes to: the position itself in a text block, or a new paragraph beside a Raw one.
		/// </summary>
		private static DocPosition TextCaret(RichDocument document, DocPosition position)
		{
			var block = document.Blocks[position.BlockIndex];
			if (block.Kind == RichBlockKind.Raw)
			{
				return InsertParagraphBesideRaw(document, position);
			}

			return position;
		}

		private static DocPosition InsertParagraphBesideRaw(RichDocument document, DocPosition position)
		{
			var raw = document.Blocks[position.BlockIndex];
			var paragraph = new RichBlock
			{
				Kind = RichBlockKind.Paragraph,
				SeparatorBefore = BlankLine(document),
				Dirty = true,
			};

			if (position.Offset == 0)
			{
				paragraph.SeparatorBefore = raw.SeparatorBefore;
				raw.SeparatorBefore = BlankLine(document);
				document.Blocks.Insert(position.BlockIndex, paragraph);
				return new DocPosition(position.BlockIndex, 0);
			}

			document.Blocks.Insert(position.BlockIndex + 1, paragraph);
			return new DocPosition(position.BlockIndex + 1, 0);
		}

		/// <summary>
		/// The style typed text takes at a caret: the nearest run before it, skipping atoms (an image has no text
		/// style); at the start of the block, the first run after it; plain text in a block with no runs.
		/// A link or inline code stops at its end, as in Google Docs: typing just after one keeps its bold, italic
		/// and strike but not the link or code, so a first-time user does not grow a link by typing after it.
		/// </summary>
		internal static RichRun StyleAt(List<RichInline> inlines, int offset)
		{
			if (offset > 0)
			{
				// Default Locate looks backwards, so for offset > 0 this is the inline just before the caret.
				var (index, inInline) = RichInlines.Locate(inlines, offset);
				for (int i = index; i >= 0; i--)
				{
					if (inlines[i] is RichRun run)
					{
						bool inside = i == index && inInline < run.Length;
						return inside ? run : WithoutLinkOrCode(run);
					}
				}
			}

			foreach (var inline in inlines)
			{
				if (inline is RichRun run)
				{
					return run;
				}
			}

			return new RichRun();
		}

		private static RichRun WithoutLinkOrCode(RichRun run)
		{
			if (run.LinkUrl == null && run.LinkLabel == null && !run.Code)
			{
				return run;
			}

			var style = run.WithText("");
			style.LinkUrl = null;
			style.LinkTitle = null;
			style.LinkLabel = null;
			style.Code = false;
			return style;
		}

		private static void RemoveText(RichBlock block, int from, int to)
		{
			if (from >= to)
			{
				return;
			}

			// SplitAt(to) runs second, so its split lands after the first one and leaves 'start' valid.
			int start = RichInlines.SplitAt(block.Inlines, from);
			int end = RichInlines.SplitAt(block.Inlines, to);
			block.Inlines.RemoveRange(start, end - start);
			RichInlines.MergeAdjacent(block.Inlines);
			block.Dirty = true;
		}

		/// <summary>
		/// Moves <paramref name="source"/>'s inlines onto the end of <paramref name="survivor"/>; the caller
		/// removes the source block, which takes its group membership with it.
		/// </summary>
		private static void AppendInlines(RichBlock survivor, RichBlock source)
		{
			survivor.Inlines.AddRange(source.Inlines);
			RichInlines.MergeAdjacent(survivor.Inlines);
			survivor.Dirty = true;
		}

		/// <summary>
		/// Removes blocks first..last and returns a caret where they were: the start of the block now there, else
		/// the end of the one before. A document left empty gets one empty paragraph so there is somewhere to type.
		/// </summary>
		private static RichSelection RemoveBlocks(RichDocument document, int first, int last)
		{
			var blocks = document.Blocks;
			RemoveBlockRange(document, first, last - first + 1);
			if (blocks.Count == 0)
			{
				blocks.Add(new RichBlock { Kind = RichBlockKind.Paragraph, Dirty = true });
				return RichSelection.At(new DocPosition(0, 0));
			}

			if (first < blocks.Count)
			{
				return RichSelection.At(new DocPosition(first, 0));
			}

			return RichSelection.At(EndOf(document, blocks.Count - 1));
		}

		/// <summary>
		/// An empty, dirty block of <paramref name="kind"/> that sits in the same groups as <paramref name="like"/>
		/// (a split keeps the group) with its alignment, heading level and list info where the kind uses them.
		/// </summary>
		private static RichBlock NewBlockLike(RichDocument document, RichBlock like, RichBlockKind kind)
		{
			return new RichBlock
			{
				Kind = kind,
				HeadingLevel = kind == RichBlockKind.Heading ? like.HeadingLevel : 0,
				List = kind == RichBlockKind.ListItem ? like.List?.Clone() : null,
				ListGroup = kind == RichBlockKind.ListItem ? like.ListGroup : null,
				QuoteGroup = kind == RichBlockKind.Quote ? like.QuoteGroup : null,
				Alignment = like.Alignment,
				AlignGroup = like.AlignGroup,
				SeparatorBefore = BlankLine(document),
				Dirty = true,
			};
		}

		/// <summary>
		/// Removes <paramref name="count"/> blocks at <paramref name="first"/> and repairs what the removal joined:
		/// the leading gap (after the frontmatter) stays with whichever block is now first, two lists or quotes
		/// that became neighbours are both forced to regenerate (their clean bytes back to back would read as one
		/// loose list or one quote), and list depths are brought back into a shape markdown can express.
		/// </summary>
		private static void RemoveBlockRange(RichDocument document, int first, int count)
		{
			var blocks = document.Blocks;
			string leadingGap = first == 0 ? blocks[0].SeparatorBefore : null;
			blocks.RemoveRange(first, count);
			if (leadingGap != null && blocks.Count > 0)
			{
				blocks[0].SeparatorBefore = leadingGap;
			}

			if (first > 0 && first < blocks.Count)
			{
				var before = blocks[first - 1];
				var after = blocks[first];
				ForceRegenerationIfJoined(before.ListGroup, after.ListGroup);
				ForceRegenerationIfJoined(before.QuoteGroup, after.QuoteGroup);
			}

			NormalizeListDepths(document);
		}

		private static void ForceRegenerationIfJoined(RichBlockGroup before, RichBlockGroup after)
		{
			if (before != null && after != null && before != after)
			{
				// OriginalMemberCount 0 never matches a member count, so the writer regenerates the group.
				before.OriginalMemberCount = 0;
				after.OriginalMemberCount = 0;
			}
		}

		/// <summary>
		/// Lifts list items so each list starts at Depth 0 and no item is more than one level deeper than the
		/// item before it - deeper has no markdown form. Removing a parent item's line is what breaks this.
		/// </summary>
		private static void NormalizeListDepths(RichDocument document)
		{
			RichListGroup group = null;
			int previousDepth = -1;
			foreach (var block in document.Blocks)
			{
				if (block.ListGroup == null)
				{
					group = null;
					continue;
				}

				int allowed = block.ListGroup == group ? previousDepth + 1 : 0;
				if (block.List.Depth > allowed)
				{
					block.List.Depth = allowed;
					block.Dirty = true;
				}

				group = block.ListGroup;
				previousDepth = block.List.Depth;
			}
		}

		/// <summary>
		/// Backspace/Enter on a list item at its start: a nested item outdents one level and takes its subtree
		/// (the deeper items right after it) along, so no child is left two levels below its new parent. A
		/// top-level item becomes a paragraph and leaves the list; the items after it are lifted so their list
		/// starts at Depth 0, keeping their depths relative to each other.
		/// </summary>
		private static void OutdentOrLeaveList(RichDocument document, int index)
		{
			var blocks = document.Blocks;
			var block = blocks[index];
			var group = block.ListGroup;
			block.Dirty = true;
			int depth = block.List.Depth;
			if (depth > 0)
			{
				for (int i = index + 1; i < blocks.Count && blocks[i].ListGroup == group && blocks[i].List.Depth > depth; i++)
				{
					blocks[i].List.Depth--;
					blocks[i].Dirty = true;
				}

				block.List.Depth--;
				return;
			}

			var follower = index + 1 < blocks.Count ? blocks[index + 1] : null;
			bool followerWasMember = follower != null && follower.ListGroup == group;
			block.Kind = RichBlockKind.Paragraph;
			block.List = null;
			block.ListGroup = null;
			SplitOffFollowingMembers(blocks, index, group, b => b.ListGroup, (b, g) => b.ListGroup = (RichListGroup)g);
			if (!followerWasMember || follower.List.Depth == 0)
			{
				return;
			}

			// The follower was this item's child (the split-off tail, or the rest of the list when it left from
			// the front); lift the whole run by its depth.
			var followers = follower.ListGroup;
			int shift = follower.List.Depth;
			for (int i = index + 1; i < blocks.Count && blocks[i].ListGroup == followers; i++)
			{
				blocks[i].List.Depth = Math.Max(0, blocks[i].List.Depth - shift);
				blocks[i].Dirty = true;
			}
		}

		private static void LeaveQuote(RichDocument document, int index)
		{
			var block = document.Blocks[index];
			var group = block.QuoteGroup;
			block.Kind = RichBlockKind.Paragraph;
			block.QuoteGroup = null;
			block.Dirty = true;
			SplitOffFollowingMembers(document.Blocks, index, group, b => b.QuoteGroup, (b, g) => b.QuoteGroup = (RichQuoteGroup)g);
		}

		/// <summary>
		/// After the block at <paramref name="index"/> left <paramref name="group"/> from the middle, gives the
		/// members after it a new group of the same kind. One group object spanning a gap would read to the writer
		/// as one list or quote with a stranger inside; two groups write as the two lists or quotes the user sees.
		/// The new group has OriginalMemberCount 0, so it is always regenerated.
		/// </summary>
		private static void SplitOffFollowingMembers(
			List<RichBlock> blocks,
			int index,
			RichBlockGroup group,
			Func<RichBlock, RichBlockGroup> getGroup,
			Action<RichBlock, RichBlockGroup> setGroup)
		{
			if (group == null || index == 0 || getGroup(blocks[index - 1]) != group)
			{
				// Leaving from the front: the remaining members are still one contiguous run.
				return;
			}

			RichBlockGroup tail = null;
			for (int i = index + 1; i < blocks.Count && getGroup(blocks[i]) == group; i++)
			{
				if (tail == null)
				{
					tail = group.Clone();
					tail.OriginalMemberCount = 0;
				}

				setGroup(blocks[i], tail);
			}
		}

		/// <summary>
		/// The gap given to a block an edit creates: one blank line, in the document's line ending (the first line
		/// break found in the frontmatter, the blocks or the trailing text). Inside a group the writer regenerates
		/// separators anyway; outside one this is what a new paragraph needs.
		/// </summary>
		internal static string BlankLine(RichDocument document)
		{
			var samples = new List<string> { document.Frontmatter };
			foreach (var block in document.Blocks)
			{
				samples.Add(block.SeparatorBefore);
				samples.Add(block.OriginalSource);
			}

			samples.Add(document.TrailingText);
			foreach (var sample in samples)
			{
				int lineBreak = sample.IndexOf('\n');
				if (lineBreak >= 0)
				{
					return lineBreak > 0 && sample[lineBreak - 1] == '\r' ? "\r\n\r\n" : "\n\n";
				}
			}

			return "\n\n";
		}

		/// <summary>
		/// The block's caret text: each run's text and one placeholder character per atom. Character stepping walks
		/// this rather than one run, so a combining mark that starts a run still belongs to the letter before it.
		/// </summary>
		private static string PlainText(List<RichInline> inlines)
		{
			var text = new StringBuilder();
			foreach (var inline in inlines)
			{
				if (inline is RichRun run)
				{
					text.Append(run.Text);
				}
				else
				{
					text.Append('￼');
				}
			}

			return text.ToString();
		}

		/// <summary>
		/// The caret stop before <paramref name="offset"/>: one atom, or one text element (a surrogate pair,
		/// combining sequence or emoji cluster is deleted whole, matching the layout's caret stops).
		/// </summary>
		internal static int PreviousStop(List<RichInline> inlines, int offset)
		{
			var starts = StringInfo.ParseCombiningCharacters(PlainText(inlines).Substring(0, offset));
			return starts[starts.Length - 1];
		}

		internal static int NextStop(List<RichInline> inlines, int offset)
		{
			return offset + StringInfo.GetNextTextElementLength(PlainText(inlines), offset);
		}
	}
}
