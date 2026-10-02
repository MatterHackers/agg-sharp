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
using System.Text.RegularExpressions;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Folds the viewer's alignment storage form - <c>&lt;div align="center"&gt;</c>, blank line, paragraphs and
	/// headings, blank line, <c>&lt;/div&gt;</c> - into <see cref="RichBlock.Alignment"/> on the blocks inside.
	/// Markdig reads the two tags as separate HTML blocks (it ends an HTML block at a blank line), so the parser
	/// first sees them as Raw blocks around the content; this pass removes them, keeping their bytes on a shared
	/// <see cref="RichAlignGroup"/> set on every block inside.
	/// </summary>
	internal static class RichAlignGroups
	{
		// Only the bare tag on its own: any other attribute or content in the HTML block is not a form the writer
		// regenerates, so it stays Raw.
		private static readonly Regex OpenTag = new Regex(
			@"^<div\s+align\s*=\s*([""']?)(left|center|right)\1\s*>$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		// The one-line element form: <div align="center">text</div>, <p align=...>...</p> or <h2 align=...>...</h2>
		// on one line, with only the align attribute. Its inner text is read as inline markdown (see FoldOneLine).
		private static readonly Regex OneLineElement = new Regex(
			@"^(?:\s*<(?<tag>div|p|h[1-6])\s+align\s*=\s*(?<q>[""']?)(?<align>left|center|right)\k<q>\s*>)(?<inner>[^\r\n]*?)(?:</\k<tag>\s*>\s*)$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		// Block-level HTML inside a one-line element is not something the rich view can edit as one paragraph.
		private static readonly Regex BlockTag = new Regex(
			@"</?(div|p|h[1-6]|table|ul|ol|li|blockquote|pre|hr)\b",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <param name="htmlBlocks">The Raw blocks that came from a Markdig HtmlBlock. Only they are tags; the same
		/// text as indented code or inside a list is not.</param>
		public static void Fold(List<RichBlock> blocks, HashSet<RichBlock> htmlBlocks)
		{
			// Depth of plain or unmatched <div>s around the current block. A group inside another div stays Raw,
			// so an edit can never regenerate an inner wrapper and leave the outer one unbalanced.
			int divDepth = 0;
			for (int i = 0; i < blocks.Count; i++)
			{
				var block = blocks[i];
				if (!htmlBlocks.Contains(block))
				{
					continue;
				}

				if (IsCloseTag(block, htmlBlocks))
				{
					divDepth = Math.Max(0, divDepth - 1);
					continue;
				}

				string html = block.OriginalSource.Trim();
				var open = OpenTag.Match(html);
				int close = open.Success && divDepth == 0 ? MatchingClose(blocks, i, htmlBlocks) : -1;
				if (close < 0)
				{
					if (html.StartsWith("<div", StringComparison.OrdinalIgnoreCase)
						&& html.IndexOf("</div", StringComparison.OrdinalIgnoreCase) < 0)
					{
						divDepth++;
					}

					continue;
				}

				var first = blocks[i + 1];
				var closeTag = blocks[close];
				var group = new RichAlignGroup
				{
					Alignment = Enum.Parse<RichAlignment>(open.Groups[2].Value, ignoreCase: true),
					OpenSource = block.OriginalSource + first.SeparatorBefore,
					CloseSource = closeTag.SeparatorBefore + closeTag.OriginalSource,
					OriginalMemberCount = close - i - 1,
				};
				first.SeparatorBefore = block.SeparatorBefore;
				for (int inner = i + 1; inner < close; inner++)
				{
					blocks[inner].Alignment = group.Alignment;
					blocks[inner].AlignGroup = group;
				}

				blocks.RemoveAt(close);
				blocks.RemoveAt(i);

				// Resume after the group's last block.
				i = close - 2;
			}

			FoldOneLine(blocks, htmlBlocks);
		}

		/// <summary>
		/// Makes each one-line aligned element (<see cref="OneLineElement"/>) an aligned Paragraph, or a Heading for
		/// h1-h6, instead of a Raw block the rich view would show as nothing. The line goes on a one-member
		/// <see cref="RichAlignGroup"/> (<see cref="RichAlignGroup.OneLineSource"/>), so an untouched block writes back
		/// its exact line; once edited or re-aligned it is written in the regenerated wrapper form like any aligned
		/// block. Runs after the multi-line fold, so an element inside an aligned wrapper is not taken into it.
		/// </summary>
		private static void FoldOneLine(List<RichBlock> blocks, HashSet<RichBlock> htmlBlocks)
		{
			foreach (var block in blocks)
			{
				if (!htmlBlocks.Contains(block))
				{
					continue;
				}

				var element = OneLineElement.Match(block.OriginalSource);
				string inner = element.Groups["inner"].Value.Trim();
				if (!element.Success || inner.Length == 0 || BlockTag.IsMatch(inner))
				{
					continue;
				}


				// The inner text must read as exactly one paragraph of itself ("# x" or "1. x" would not).
				var innerDocument = RichMarkdownParser.Parse(inner);
				if (innerDocument.Blocks.Count != 1
					|| innerDocument.Blocks[0].Kind != RichBlockKind.Paragraph
					|| innerDocument.Blocks[0].OriginalSource != inner)
				{
					continue;
				}

				string tag = element.Groups["tag"].Value;
				bool heading = tag.Length == 2 && char.ToLowerInvariant(tag[0]) == 'h';
				var alignment = Enum.Parse<RichAlignment>(element.Groups["align"].Value, ignoreCase: true);
				block.Kind = heading ? RichBlockKind.Heading : RichBlockKind.Paragraph;
				block.HeadingLevel = heading ? tag[1] - '0' : 0;
				block.Inlines = innerDocument.Blocks[0].Inlines;
				// The block's own source is its markdown, as for any clean block (a heading split off the group is
				// still written from it); the group keeps the exact line for while it is intact.
				string line = block.OriginalSource;
				block.OriginalSource = heading ? new string('#', block.HeadingLevel) + " " + inner : inner;
				block.Alignment = alignment;
				block.AlignGroup = new RichAlignGroup
				{
					Alignment = alignment,
					OneLineSource = line,
					OriginalMemberCount = 1,
				};
			}
		}

		/// <summary>
		/// The index of the &lt;/div&gt; closing the group opened at <paramref name="open"/>, or -1 when the
		/// group is empty, unclosed or holds anything but paragraphs and headings (lists, quotes and Raw blocks
		/// are not aligned in the rich view).
		/// </summary>
		private static int MatchingClose(List<RichBlock> blocks, int open, HashSet<RichBlock> htmlBlocks)
		{
			int index = open + 1;
			while (index < blocks.Count
				&& (blocks[index].Kind == RichBlockKind.Paragraph || blocks[index].Kind == RichBlockKind.Heading))
			{
				index++;
			}

			bool closed = index > open + 1 && index < blocks.Count && IsCloseTag(blocks[index], htmlBlocks);
			return closed ? index : -1;
		}

		private static bool IsCloseTag(RichBlock block, HashSet<RichBlock> htmlBlocks)
		{
			return htmlBlocks.Contains(block)
				&& string.Equals(block.OriginalSource.Trim(), "</div>", StringComparison.OrdinalIgnoreCase);
		}
	}
}
