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
	/// The structural edit primitives every higher layer builds on (agg-gui's rich_text::model functions): insert,
	/// remove, split, merge, and styled extract/splice for copy and paste. Each leaves the touched blocks normalized.
	/// </summary>
	public static class RichTextEdits
	{
		/// <summary>Inserts <paramref name="text"/> (no newlines) in <paramref name="style"/> at <paramref name="pos"/>.</summary>
		public static void InsertText(RichDoc doc, DocPos pos, string text, InlineStyle style)
		{
			if (string.IsNullOrEmpty(text) || pos.Block < 0 || pos.Block >= doc.Blocks.Count)
			{
				return;
			}

			var block = doc.Blocks[pos.Block];
			block.Runs.Insert(block.EnsureBoundary(pos.Offset), new TextRun(text, style));
			block.Normalize();
		}

		/// <summary>
		/// Removes everything inside <paramref name="range"/>, keeping the surviving text's styles. A range across
		/// blocks joins the tail of the last block onto the first (Backspace-across-paragraphs). Returns the collapse
		/// position, the range's low end.
		/// </summary>
		public static DocPos RemoveRange(RichDoc doc, DocRange range)
		{
			var a = range.Min;
			var b = range.Max;
			if (a == b)
			{
				return a;
			}

			// Clamp so a select-all made against a longer document never indexes past the end.
			b = new DocPos(Math.Min(b.Block, doc.Blocks.Count - 1), b.Offset);
			var first = doc.Blocks[a.Block];
			if (a.Block == b.Block)
			{
				int start = first.EnsureBoundary(a.Offset);
				int end = first.EnsureBoundary(b.Offset);
				first.Runs.RemoveRange(start, end - start);
				first.Normalize();
				return a;
			}

			first.Runs.RemoveRange(first.EnsureBoundary(a.Offset), first.Runs.Count - first.EnsureBoundary(a.Offset));
			var last = doc.Blocks[b.Block];
			int tailStart = last.EnsureBoundary(b.Offset);
			first.Runs.AddRange(last.Runs.GetRange(tailStart, last.Runs.Count - tailStart));
			first.Normalize();
			doc.Blocks.RemoveRange(a.Block + 1, b.Block - a.Block);
			return a;
		}

		/// <summary>Splits the block at <paramref name="pos"/> (Enter). The new block keeps the attributes. Returns its start.</summary>
		public static DocPos SplitBlock(RichDoc doc, DocPos pos)
		{
			if (pos.Block < 0 || pos.Block >= doc.Blocks.Count)
			{
				return pos;
			}

			var block = doc.Blocks[pos.Block];
			int index = block.EnsureBoundary(pos.Offset);
			var tail = block.CloneAttributes();
			tail.Runs.AddRange(block.Runs.GetRange(index, block.Runs.Count - index));
			block.Runs.RemoveRange(index, block.Runs.Count - index);
			block.Normalize();
			tail.Normalize();
			doc.Blocks.Insert(pos.Block + 1, tail);
			return new DocPos(pos.Block + 1, 0);
		}

		/// <summary>
		/// Joins block <paramref name="index"/> onto the one before it (Backspace at a paragraph start), keeping the
		/// previous block's attributes. Returns the join point; a no-op for the first block or an index out of range.
		/// </summary>
		public static DocPos MergeBlockWithPrevious(RichDoc doc, int index)
		{
			if (index <= 0 || index >= doc.Blocks.Count)
			{
				return new DocPos(index, 0);
			}

			var previous = doc.Blocks[index - 1];
			int join = previous.TextLength;
			previous.Runs.AddRange(doc.Blocks[index].Runs);
			previous.Normalize();
			doc.Blocks.RemoveAt(index);
			return new DocPos(index - 1, join);
		}

		/// <summary>
		/// Copies the content inside <paramref name="range"/> as standalone blocks, keeping run styles and block
		/// attributes - styled Copy, the inverse of <see cref="SpliceFragment"/>. Empty for a collapsed range.
		/// </summary>
		public static List<Block> ExtractRange(RichDoc doc, DocRange range)
		{
			var result = new List<Block>();
			var a = range.Min;
			var b = range.Max;
			if (a == b)
			{
				return result;
			}

			int lastBlock = Math.Min(b.Block, doc.Blocks.Count - 1);
			for (int i = a.Block; i <= lastBlock; i++)
			{
				var block = doc.Blocks[i].Clone();
				int high = i == lastBlock ? b.Offset : block.TextLength;
				int low = i == a.Block ? a.Offset : 0;

				// Trim the tail first so the head offset stays valid.
				int end = block.EnsureBoundary(high);
				block.Runs.RemoveRange(end, block.Runs.Count - end);
				block.Runs.RemoveRange(0, block.EnsureBoundary(low));
				block.Normalize();
				result.Add(block);
			}

			return result;
		}

		/// <summary>
		/// Pastes <paramref name="fragment"/> (from <see cref="ExtractRange"/>) at <paramref name="pos"/>, keeping run
		/// styles. One block splices inline; several split the target paragraph, with the last fragment block joined to
		/// the original tail. Returns the caret position at the end of the pasted content.
		/// </summary>
		public static DocPos SpliceFragment(RichDoc doc, DocPos pos, IReadOnlyList<Block> fragment)
		{
			if (fragment.Count == 0 || pos.Block < 0 || pos.Block >= doc.Blocks.Count)
			{
				return pos;
			}

			var target = doc.Blocks[pos.Block];
			if (fragment.Count == 1)
			{
				// Inline: the target keeps its attributes, unless it is a pristine empty paragraph - then a copied
				// list item pastes as a list item.
				var pristine = new Block();
				bool adopt = target.Runs.Count == 0 && target.Align == pristine.Align && target.List == pristine.List && target.Indent == 0;
				int index = target.EnsureBoundary(pos.Offset);
				target.Runs.InsertRange(index, fragment[0].Runs);
				target.Normalize();
				if (adopt)
				{
					target.Align = fragment[0].Align;
					target.List = fragment[0].List;
					target.Indent = fragment[0].Indent;
				}

				return new DocPos(pos.Block, pos.Offset + fragment[0].TextLength);
			}

			int split = target.EnsureBoundary(pos.Offset);
			var tailRuns = target.Runs.GetRange(split, target.Runs.Count - split);
			target.Runs.RemoveRange(split, target.Runs.Count - split);
			target.Runs.AddRange(fragment[0].Runs);
			target.Normalize();

			var inserted = new List<Block>();
			for (int i = 1; i < fragment.Count - 1; i++)
			{
				inserted.Add(fragment[i].Clone());
			}

			// The final line continues the target paragraph, so it keeps the target's attributes.
			var lastFragment = fragment[fragment.Count - 1];
			var last = target.CloneAttributes();
			last.Runs.AddRange(lastFragment.Runs);
			last.Runs.AddRange(tailRuns);
			last.Normalize();
			inserted.Add(last);
			doc.Blocks.InsertRange(pos.Block + 1, inserted);
			return new DocPos(pos.Block + inserted.Count, lastFragment.TextLength);
		}
	}
}
