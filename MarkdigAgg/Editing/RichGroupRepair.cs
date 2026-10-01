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

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Restores the <see cref="RichBlockGroup"/> invariants after an edit changed which blocks belong to which
	/// list, quote or alignment wrapper: each group is one contiguous run, each list has one top-level kind and
	/// starts at Depth 0 with no skipped level, and different groups left side by side are regenerated. Shared by
	/// the text and block edit ops so both repair the model the same way.
	/// </summary>
	internal static class RichGroupRepair
	{
		/// <summary>
		/// The whole repair for an op that changed blocks first..last without adding or removing any: groups split
		/// into contiguous runs, lists lifted and normalized, and different lists or quotes left touching around
		/// the edit forced to regenerate.
		/// </summary>
		public static void Repair(RichDocument document, int first, int last)
		{
			SplitAllIntoRuns(document.Blocks);
			NormalizeListDepths(document);
			ForceRegenerationAround(document, first, last);
		}

		/// <summary>
		/// <see cref="ForceRegenerationIfJoined"/> for each pair of neighbours from first-1..last+1.
		/// </summary>
		public static void ForceRegenerationAround(RichDocument document, int first, int last)
		{
			var blocks = document.Blocks;
			for (int i = Math.Max(1, first); i <= Math.Min(blocks.Count - 1, last + 1); i++)
			{
				ForceRegenerationIfJoined(blocks[i - 1].ListGroup, blocks[i].ListGroup);
				ForceRegenerationIfJoined(blocks[i - 1].QuoteGroup, blocks[i].QuoteGroup);
			}
		}

		/// <summary>
		/// An item that moved to another depth takes that level's style - kind, marker and numbering - from its new
		/// previous sibling, so it continues the list it joined instead of splitting it; the first item of a new
		/// sublist keeps its kind and numbers from 1. Its subtree keeps its own levels' styles.
		/// </summary>
		public static void AdoptLevelStyle(List<RichBlock> blocks, int index)
		{
			var item = blocks[index];
			var list = item.List;
			RichListInfo sibling = null;
			for (int i = index - 1; i >= 0 && blocks[i].ListGroup == item.ListGroup && blocks[i].List.Depth >= list.Depth; i--)
			{
				if (blocks[i].List.Depth == list.Depth)
				{
					sibling = blocks[i].List;
					break;
				}
			}

			list.Ordered = sibling?.Ordered ?? list.Ordered;
			list.Marker = sibling?.Marker ?? list.Marker;
			list.StartNumber = sibling?.StartNumber ?? 1;
		}

		/// <summary>
		/// Makes every alignment, quote and list group one contiguous run (a list also ends where its top-level
		/// kind switches between bullet and number, as markdown's does), then lifts any list whose first item is
		/// nested so it starts at the top level.
		/// </summary>
		public static void SplitAllIntoRuns(List<RichBlock> blocks)
		{
			SplitIntoRuns(blocks, b => b.AlignGroup, (b, g) => b.AlignGroup = (RichAlignGroup)g, null);
			SplitIntoRuns(blocks, b => b.QuoteGroup, (b, g) => b.QuoteGroup = (RichQuoteGroup)g, null);
			SplitIntoRuns(
				blocks,
				b => b.ListGroup,
				(b, g) => b.ListGroup = (RichListGroup)g,
				(runStart, b) => b.List.Depth == 0 && b.List.Ordered != runStart.List.Ordered);
			LiftListRuns(blocks);
		}

		/// <summary>
		/// Gives every run of a group after its first a cloned group so each group object is one contiguous run:
		/// one object spanning a gap would read to the writer as one list or quote with a stranger inside. The
		/// clone has OriginalMemberCount 0, so it is always regenerated (its members need not be Dirty for that).
		/// <paramref name="startsRun"/> (run's first block, block) can also break a run in the middle.
		/// </summary>
		public static void SplitIntoRuns(
			List<RichBlock> blocks,
			Func<RichBlock, RichBlockGroup> getGroup,
			Action<RichBlock, RichBlockGroup> setGroup,
			Func<RichBlock, RichBlock, bool> startsRun)
		{
			var seen = new HashSet<RichBlockGroup>();
			RichBlockGroup previous = null;
			RichBlockGroup current = null;
			RichBlock runStart = null;
			foreach (var block in blocks)
			{
				// Compared against the previous block's group as it was before this pass reassigned it.
				var group = getGroup(block);
				bool continues = group != null && group == previous && !(startsRun?.Invoke(runStart, block) ?? false);
				previous = group;
				if (group == null)
				{
					continue;
				}

				if (!continues)
				{
					runStart = block;
					if (seen.Add(group))
					{
						current = group;
					}
					else
					{
						current = group.Clone();
						current.OriginalMemberCount = 0;
					}
				}

				if (current != group)
				{
					setGroup(block, current);
				}
			}
		}

		/// <summary>
		/// A list whose first item is nested (its parent left the list) has that orphaned subtree - the leading
		/// items at least that deep - lifted by the first item's depth, keeping their depths relative to each other.
		/// Items after it (the next top-level item and its own children) keep their depths.
		/// </summary>
		public static void LiftListRuns(List<RichBlock> blocks)
		{
			for (int i = 0; i < blocks.Count; i++)
			{
				var group = blocks[i].ListGroup;
				if (group == null || (i > 0 && blocks[i - 1].ListGroup == group) || blocks[i].List.Depth == 0)
				{
					continue;
				}

				int shift = blocks[i].List.Depth;
				for (int j = i; j < blocks.Count && blocks[j].ListGroup == group && blocks[j].List.Depth >= shift; j++)
				{
					blocks[j].List.Depth -= shift;
					blocks[j].Dirty = true;
				}
			}
		}

		/// <summary>
		/// Lifts list items so each list starts at Depth 0 and no item is more than one level deeper than the item
		/// before it - deeper has no markdown form. Removing a parent item's line is what usually breaks this.
		/// </summary>
		public static void NormalizeListDepths(RichDocument document)
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
		/// Two different lists or quotes side by side are both forced to regenerate: their clean bytes written back
		/// to back would read as one loose list or one quote. OriginalMemberCount 0 never matches a member count.
		/// </summary>
		public static void ForceRegenerationIfJoined(RichBlockGroup before, RichBlockGroup after)
		{
			if (before != null && after != null && before != after)
			{
				before.OriginalMemberCount = 0;
				after.OriginalMemberCount = 0;
			}
		}

		/// <summary>
		/// The index of the last item in the subtree of the list item at <paramref name="index"/>: the run of deeper
		/// items of the same list right after it (the item itself when it has none).
		/// </summary>
		public static int SubtreeEnd(List<RichBlock> blocks, int index)
		{
			var item = blocks[index];
			int end = index;
			while (end + 1 < blocks.Count && blocks[end + 1].ListGroup == item.ListGroup && blocks[end + 1].List.Depth > item.List.Depth)
			{
				end++;
			}

			return end;
		}
	}
}
