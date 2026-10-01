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
	/// Writes changed groups (see <see cref="RichBlockGroup"/>) for <see cref="RichMarkdownWriter"/>: a whole list,
	/// blockquote or alignment wrapper rebuilt from the model - every member, clean ones too, and the separators
	/// between them - since an original member's bytes hold indentation, numbering and "&gt;" joiners that only
	/// fit the group as it was parsed.
	/// </summary>
	internal static class RichGroupWriter
	{
		/// <summary>
		/// Written after the marker of an empty item that opens a list or sublist. Right below its parent's text a
		/// bare marker cannot start an item (an empty item cannot interrupt a paragraph), so it would read as more
		/// of that text or underline it into a heading; Markdig does not start a top-level list with a bare marker
		/// either. The comment renders as nothing and the parser reads the item back as empty.
		/// </summary>
		public const string EmptyItemComment = "<!-- -->";

		private static readonly char[] Bullets = { '-', '*', '+' };

		public static IEnumerable<RichBlockGroup> GroupsOf(RichBlock block)
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
		/// The groups still exactly as parsed, found in one pass over the blocks: one contiguous run of
		/// OriginalMemberCount members, every member clean and, for alignment, still at the group's alignment.
		/// </summary>
		public static HashSet<RichBlockGroup> UnchangedGroups(List<RichBlock> blocks)
		{
			// Per group: the index of its last member seen, its member count and whether it is still intact.
			var seen = new Dictionary<RichBlockGroup, (int Last, int Count, bool Intact)>();
			for (int i = 0; i < blocks.Count; i++)
			{
				var block = blocks[i];
				foreach (var group in GroupsOf(block))
				{
					var state = seen.TryGetValue(group, out var found) ? found : (Last: i - 1, Count: 0, Intact: true);
					bool aligned = group is not RichAlignGroup align || block.Alignment == align.Alignment;
					bool intact = state.Intact && state.Last == i - 1 && !block.Dirty && aligned;
					seen[group] = (i, state.Count + 1, intact);
				}
			}

			return seen.Where(pair => pair.Value.Intact && pair.Key.OriginalMemberCount > 0 && pair.Value.Count == pair.Key.OriginalMemberCount)
				.Select(pair => pair.Key)
				.ToHashSet();
		}

		/// <summary>
		/// The last index of the regenerated unit starting at <paramref name="first"/>, or -1 when that block is
		/// written on its own (its original bytes or a dirty rewrite). A unit is a changed list or quote, or a run
		/// of paragraphs and headings sharing one non-left alignment outside any unchanged wrapper. A changed
		/// "left" wrapper simply drops: left is how unwrapped blocks render anyway.
		/// </summary>
		public static int RegeneratedUnitEnd(List<RichBlock> blocks, int first, HashSet<RichBlockGroup> unchanged)
		{
			var block = blocks[first];
			Func<RichBlock, bool> member;
			if (block.Kind == RichBlockKind.ListItem && (block.ListGroup == null || !unchanged.Contains(block.ListGroup)))
			{
				// A ListItem with no group (the parser and the ops always give one) is a list of one.
				member = other => block.ListGroup != null && other.Kind == RichBlockKind.ListItem && other.ListGroup == block.ListGroup;
			}
			else if (block.Kind == RichBlockKind.Quote && (block.QuoteGroup == null || !unchanged.Contains(block.QuoteGroup)))
			{
				member = other => block.QuoteGroup != null && other.Kind == RichBlockKind.Quote && other.QuoteGroup == block.QuoteGroup;
			}
			else if (InAlignRun(block, unchanged))
			{
				member = other => InAlignRun(other, unchanged) && other.Alignment == block.Alignment;
			}
			else
			{
				return -1;
			}

			int last = first;
			while (last + 1 < blocks.Count && member(blocks[last + 1]))
			{
				last++;
			}

			return last;
		}

		private static bool InAlignRun(RichBlock block, HashSet<RichBlockGroup> unchanged)
		{
			return (block.Kind == RichBlockKind.Paragraph || block.Kind == RichBlockKind.Heading)
				&& block.Alignment != RichAlignment.Left
				&& (block.AlignGroup == null || !unchanged.Contains(block.AlignGroup));
		}

		/// <summary>
		/// Writes the regenerated unit blocks[first..last]. <paramref name="previousList"/> is the top-level
		/// style of a list written right before this one (null when the block before is not a list item); it is
		/// updated to this unit's top-level style when the unit is a list. <paramref name="listContentColumn"/>
		/// becomes the column the list's last top-level item's text starts at, or -1 for another unit.
		/// </summary>
		public static string WriteUnit(List<RichBlock> blocks, int first, int last, string newline, ref RichListInfo previousList, ref int listContentColumn)
		{
			var block = blocks[first];
			if (block.Kind == RichBlockKind.ListItem)
			{
				return WriteList(blocks, first, last, newline, ref previousList, ref listContentColumn);
			}

			previousList = null;
			listContentColumn = -1;
			if (block.Kind == RichBlockKind.Quote)
			{
				return WriteQuote(blocks, first, last, newline);
			}

			return WriteAlignRun(blocks, first, last, newline);
		}

		/// <summary>
		/// A separator that holds at least one blank line, so a regenerated unit can never run into its neighbour
		/// (a paragraph would continue a list item or quote lazily; two wrappers' tags would share one HTML block).
		/// Indentation after the last line break is kept: it belongs to the block that follows.
		/// </summary>
		public static string WithBlankLine(string separator, string newline)
		{
			if (separator.Count(c => c == '\n') >= 2)
			{
				return separator;
			}

			int lineStart = separator.LastIndexOf('\n') + 1;
			return newline + newline + separator.Substring(lineStart);
		}

		/// <summary>
		/// The separator before a regenerated unit: its line breaks only (the unit writes its own indentation),
		/// at least a blank line unless the unit opens the document.
		/// </summary>
		public static string LeadingSeparator(string separator, string newline, bool firstBlock)
		{
			int lineBreaks = separator.Count(c => c == '\n');
			if (!firstBlock)
			{
				lineBreaks = Math.Max(2, lineBreaks);
			}

			return string.Concat(Enumerable.Repeat(newline, lineBreaks));
		}

		/// <summary>
		/// The column a block starts at: the indentation after the separator's last line break plus any at the
		/// start of its own markdown (a code block's source holds its indentation). A tab advances to a multiple of 4.
		/// </summary>
		public static int Column(string separator, string text)
		{
			int column = 0;
			foreach (char c in separator.Substring(separator.LastIndexOf('\n') + 1) + text)
			{
				if (c == ' ')
				{
					column++;
				}
				else if (c == '\t')
				{
					column += 4 - (column % 4);
				}
				else
				{
					break;
				}
			}

			return column;
		}

		/// <summary>
		/// Where a parsed (clean) list item's text starts: its indentation, its marker and the 1-4 spaces after it
		/// (more than 4 means indented code inside the item, which starts 1 space after the marker, as does an
		/// empty item).
		/// </summary>
		public static int OriginalContentColumn(RichBlock item)
		{
			string source = item.OriginalSource;
			int marker = 0;
			while (marker < source.Length && char.IsAsciiDigit(source[marker]))
			{
				marker++;
			}

			marker = Math.Min(marker + 1, source.Length);
			int spaces = 0;
			while (marker + spaces < source.Length && source[marker + spaces] == ' ')
			{
				spaces++;
			}

			bool lineEnds = marker + spaces >= source.Length || source[marker + spaces] == '\n' || source[marker + spaces] == '\r';
			if (spaces == 0 || spaces > 4 || lineEnds)
			{
				spaces = 1;
			}

			return Column(item.SeparatorBefore, "") + marker + spaces;
		}

		/// <summary>
		/// One list level being written: where its markers start, its style (from its first item) and the
		/// number its next item gets.
		/// </summary>
		private class ListLevel
		{
			public int Indent;
			public bool Ordered;
			public char Marker;
			public bool Loose;
			public int Next;

			/// <summary>
			/// The content column of this level's latest item: where a child list's markers go (CommonMark nests
			/// a list under an item by indenting it to the item's content).
			/// </summary>
			public int ChildIndent;
		}

		/// <summary>
		/// A list rebuilt from each item's Depth and style. Each (sub)list takes its marker, numbering start and
		/// spacing from its first item, so items that disagree still read back as one list; numbers count up from
		/// that start. A change of kind at one depth starts a new sublist, as it does in markdown.
		/// <para>
		/// Spacing: items of a loose (sub)list (<see cref="RichListInfo.Loose"/>) are a blank line apart, all
		/// others a line break, whatever the gaps in the model hold - edits give every new block a blank-line gap,
		/// so the gaps cannot tell.
		/// </para>
		/// <para>
		/// Two lists side by side: a blank line alone would join them into one loose list, so when this list has
		/// the same kind and marker as the one right before it, it switches to another bullet ('-', '*', '+') or
		/// delimiter ('.', ')'), which CommonMark reads as a new list. Unlike an HTML comment between them,
		/// nothing extra shows in the Markdown tab.
		/// </para>
		/// </summary>
		private static string WriteList(List<RichBlock> blocks, int first, int last, string newline, ref RichListInfo previousList, ref int listContentColumn)
		{
			var markdown = new StringBuilder();
			var levels = new List<ListLevel>();
			RichListInfo topStyle = null;
			for (int k = first; k <= last; k++)
			{
				var info = blocks[k].List ?? new RichListInfo();

				// The ops keep depths gapless; a skipped level has no markdown form, so it is clamped.
				int depth = Math.Clamp(info.Depth, 0, levels.Count);
				bool continues = depth < levels.Count && levels[depth].Ordered == info.Ordered;
				string content = RichInlineWriter.Write(blocks[k].Inlines).Replace("\r\n", "\n");
				if (continues)
				{
					levels.RemoveRange(depth + 1, levels.Count - depth - 1);
				}
				else
				{
					levels.RemoveRange(depth, levels.Count - depth);
					levels.Add(NewLevel(info, depth == 0 ? 0 : levels[depth - 1].ChildIndent, depth == 0 && k == first ? previousList : null));
					if (depth == 0)
					{
						topStyle = new RichListInfo { Ordered = info.Ordered, Marker = levels[0].Marker };
					}
				}

				var level = levels[depth];
				if (k > first)
				{
					// A nested list starting at a number other than 1 cannot interrupt its parent's paragraph: right
					// below it the line would continue that paragraph.
					bool cannotInterrupt = !continues && depth > 0 && level.Ordered && level.Next != 1;
					markdown.Append(newline);
					if ((continues && level.Loose) || cannotInterrupt)
					{
						markdown.Append(newline);
					}
				}

				string marker = level.Ordered ? level.Next++ + level.Marker.ToString() : level.Marker.ToString();
				level.ChildIndent = level.Indent + marker.Length + 1;
				var lines = content.Split('\n');
				markdown.Append(' ', level.Indent).Append(marker);
				if (lines[0].Length > 0)
				{
					markdown.Append(' ').Append(lines[0]);
				}
				else if (!continues)
				{
					markdown.Append(' ').Append(EmptyItemComment);
				}

				for (int line = 1; line < lines.Length; line++)
				{
					markdown.Append(newline).Append(' ', level.ChildIndent).Append(lines[line]);
				}
			}

			previousList = topStyle;
			listContentColumn = levels[0].ChildIndent;
			return markdown.ToString();
		}

		private static ListLevel NewLevel(RichListInfo info, int indent, RichListInfo avoid)
		{
			char marker = info.Ordered
				? (info.Marker == ')' ? ')' : '.')
				: (Bullets.Contains(info.Marker) ? info.Marker : '-');
			if (avoid != null && avoid.Ordered == info.Ordered && avoid.Marker == marker)
			{
				marker = info.Ordered ? (marker == '.' ? ')' : '.') : Bullets.First(bullet => bullet != marker);
			}

			return new ListLevel
			{
				Indent = indent,
				Ordered = info.Ordered,
				Marker = marker,
				Loose = info.Loose,
				Next = Math.Max(0, info.StartNumber),
			};
		}

		/// <summary>
		/// One blockquote: every line prefixed "&gt; " (so hard breaks and wrapped lines stay inside it) and
		/// paragraphs joined by a bare "&gt;" line.
		/// </summary>
		private static string WriteQuote(List<RichBlock> blocks, int first, int last, string newline)
		{
			var markdown = new StringBuilder();
			for (int k = first; k <= last; k++)
			{
				if (k > first)
				{
					markdown.Append(newline).Append('>').Append(newline);
				}

				var lines = RichInlineWriter.Write(blocks[k].Inlines).Replace("\r\n", "\n").Split('\n');
				for (int line = 0; line < lines.Length; line++)
				{
					if (line > 0)
					{
						markdown.Append(newline);
					}

					markdown.Append(lines[line].Length > 0 ? "> " + lines[line] : ">");
				}
			}

			return markdown.ToString();
		}

		/// <summary>
		/// Paragraphs and headings sharing one alignment, in the viewer's storage form: the &lt;div align&gt; tag,
		/// a blank line, the blocks a blank line apart, a blank line, &lt;/div&gt;. The blank lines make each tag
		/// its own HTML block so the markdown between them is still read as markdown.
		/// </summary>
		private static string WriteAlignRun(List<RichBlock> blocks, int first, int last, string newline)
		{
			string blankLine = newline + newline;
			var markdown = new StringBuilder("<div align=\"")
				.Append(blocks[first].Alignment == RichAlignment.Center ? "center" : "right")
				.Append("\">");
			for (int k = first; k <= last; k++)
			{
				// An empty paragraph writes nothing, as it does outside a wrapper.
				string block = RichMarkdownWriter.WriteBlock(blocks[k], newline);
				if (block.Length > 0)
				{
					markdown.Append(blankLine).Append(block);
				}
			}

			return markdown.Append(blankLine).Append("</div>").ToString();
		}
	}
}
