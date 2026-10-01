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
	/// <summary>
	/// Turns a <see cref="RichDocument"/> back into markdown. Unedited blocks and unchanged groups (see
	/// <see cref="RichBlockGroup"/>) are copied verbatim, so a document loaded and saved with no edits is
	/// byte-identical.
	/// </summary>
	public static class RichMarkdownWriter
	{
		public static string Write(RichDocument document)
		{
			var blocks = document.Blocks;
			var markdown = new StringBuilder(document.Frontmatter);
			var checkedGroups = new HashSet<RichBlockGroup>();
			for (int i = 0; i < blocks.Count; i++)
			{
				var block = blocks[i];
				foreach (var group in GroupsOf(block))
				{
					if (checkedGroups.Add(group) && !IsOriginal(blocks, i, group))
					{
						// See RichBlockGroup: a changed group is regenerated whole, which is its own step of the plan.
						throw new NotImplementedException($"Writing a changed {group.GetType().Name} is not implemented yet.");
					}
				}

				markdown.Append(block.SeparatorBefore);

				// An original group's wrapper bytes go around its first and last members.
				var align = block.AlignGroup;
				if (align != null && (i == 0 || blocks[i - 1].AlignGroup != align))
				{
					markdown.Append(align.OpenSource);
				}

				markdown.Append(block.Dirty ? WriteBlock(block) : block.OriginalSource);
				if (align != null && (i == blocks.Count - 1 || blocks[i + 1].AlignGroup != align))
				{
					markdown.Append(align.CloseSource);
				}
			}

			markdown.Append(document.TrailingText);
			return markdown.ToString();
		}

		private static IEnumerable<RichBlockGroup> GroupsOf(RichBlock block)
		{
			if (block.AlignGroup != null)
			{
				yield return block.AlignGroup;
			}

			if (block.QuoteGroup != null)
			{
				yield return block.QuoteGroup;
			}

			if (block.ListGroup != null)
			{
				yield return block.ListGroup;
			}
		}

		/// <summary>
		/// True when the group, first met at <paramref name="first"/>, is still exactly as parsed: one contiguous
		/// run of its original member count, every member clean and, for alignment, still at the group's alignment.
		/// </summary>
		private static bool IsOriginal(List<RichBlock> blocks, int first, RichBlockGroup group)
		{
			int members = 0;
			int runEnd = first;
			for (int i = first; i < blocks.Count; i++)
			{
				var block = blocks[i];
				if (!GroupsOf(block).Contains(group))
				{
					continue;
				}

				bool contiguous = i == runEnd;
				bool aligned = group is not RichAlignGroup align || block.Alignment == align.Alignment;
				if (!contiguous || block.Dirty || !aligned)
				{
					return false;
				}

				members++;
				runEnd = i + 1;
			}

			return members == group.OriginalMemberCount;
		}

		/// <summary>
		/// Markdown for an edited block. Raw blocks are never edited in the rich view, so their source stands.
		/// </summary>
		private static string WriteBlock(RichBlock block)
		{
			if (block.Kind == RichBlockKind.Raw)
			{
				return block.OriginalSource;
			}

			// Regenerating edited blocks (delimiters, escaping, lists, tables) is its own step of the editor plan.
			throw new NotImplementedException($"Writing an edited {block.Kind} block is not implemented yet.");
		}
	}
}
