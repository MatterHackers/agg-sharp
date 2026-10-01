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
			string blankLine = RichEditOperations.BlankLine(document);
			string newline = blankLine.Substring(0, blankLine.Length / 2);
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

				markdown.Append(block.Dirty ? WriteBlock(block, newline) : block.OriginalSource);
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
		private static string WriteBlock(RichBlock block, string newline)
		{
			if (block.Kind == RichBlockKind.Raw)
			{
				return block.OriginalSource;
			}

			if (block.Kind == RichBlockKind.Paragraph)
			{
				return RichInlineWriter.Write(block.Inlines);
			}

			if (block.Kind == RichBlockKind.Heading)
			{
				return WriteHeading(block);
			}

			if (block.Kind == RichBlockKind.CodeBlock)
			{
				return WriteCode(block, newline);
			}

			if (block.Kind == RichBlockKind.Table)
			{
				return WriteTable(block, newline);
			}

			// Regenerating edited lists and quotes is its own step of the editor plan.
			throw new NotImplementedException($"Writing an edited {block.Kind} block is not implemented yet.");
		}

		/// <summary>
		/// An edited heading. ATX ("## text") keeps it one line whatever the text. Hard breaks already write as
		/// spaces; when an atom's own markdown holds a line break, levels 1 and 2 use a setext underline, which
		/// allows several lines, and deeper levels get the break as a space (an attribute value or alt text in
		/// that atom then holds a space instead of a newline, which renders the same).
		/// </summary>
		private static string WriteHeading(RichBlock block)
		{
			int level = Math.Clamp(block.HeadingLevel, 1, 6);
			string content = RichInlineWriter.Write(block.Inlines, RichInlineContext.Heading).TrimEnd();
			if (content.Contains('\n') && level <= 2)
			{
				string lineEnding = content.Contains("\r\n") ? "\r\n" : "\n";
				return content + lineEnding + (level == 1 ? "===" : "---");
			}

			content = content.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

			// A trailing "#" run after a space would read as the optional closing sequence and vanish, unless
			// the writer already escaped it (as it does at the start of the text).
			int backslashes = 0;
			while (backslashes < content.Length - 1 && content[content.Length - 2 - backslashes] == '\\')
			{
				backslashes++;
			}

			if (content.EndsWith('#') && backslashes % 2 == 0)
			{
				content = content.Substring(0, content.Length - 1) + "\\#";
			}

			var hashes = new string('#', level);
			return content.Length == 0 ? hashes : hashes + " " + content;
		}

		/// <summary>
		/// An edited code block, fenced with the author's fence and info string as written, or indented when it
		/// was (CodeFence ""). A fence closes at a run of its character at least as long, so the fence grows past
		/// the longest such run in the code; indented code cannot start or end with a blank line or be empty, so
		/// those write fenced.
		/// </summary>
		private static string WriteCode(RichBlock block, string newline)
		{
			var lines = block.CodeText.Replace("\r\n", "\n").Split('\n');
			bool canIndent = block.CodeText.Length > 0 && lines[0].Trim().Length > 0 && lines[^1].Trim().Length > 0;
			if (block.CodeFence.Length == 0 && canIndent)
			{
				return string.Join(newline, lines.Select(line => "    " + line));
			}

			char fenceChar = block.CodeFence.Length > 0 ? block.CodeFence[0] : '`';
			int fenceLength = Math.Max(3, block.CodeFence.Length);
			int longestRun = 0;
			for (int i = 0; i < block.CodeText.Length;)
			{
				int start = i;
				while (i < block.CodeText.Length && block.CodeText[i] == fenceChar)
				{
					i++;
				}

				longestRun = Math.Max(longestRun, i - start);
				i = Math.Max(i, start + 1);
			}

			var fence = new string(fenceChar, Math.Max(fenceLength, longestRun + 1));
			var markdown = new StringBuilder(fence).Append(block.CodeInfo).Append(newline);
			if (block.CodeText.Length > 0)
			{
				markdown.Append(string.Join(newline, lines)).Append(newline);
			}

			return markdown.Append(fence).ToString();
		}

		/// <summary>
		/// An edited table as a pipe table: header row, delimiter row from the column alignments, then the body.
		/// Cells are one line each, with '|' escaped outside code.
		/// </summary>
		private static string WriteTable(RichBlock block, string newline)
		{
			var lines = new List<string>();
			int columns = block.TableRows.Count > 0 ? block.TableRows[0].Count : 0;
			for (int row = 0; row < block.TableRows.Count; row++)
			{
				// An HTML atom's own line break would end the row; a space renders the same inside a tag.
				var cells = block.TableRows[row].Select(cell => RichInlineWriter.Write(cell.Inlines, RichInlineContext.TableCell)
					.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim());
				lines.Add("| " + string.Join(" | ", cells) + " |");
				if (row == 0)
				{
					var delimiters = Enumerable.Range(0, columns).Select(column =>
						(column < block.ColumnAlignments.Count ? block.ColumnAlignments[column] : null) switch
						{
							RichAlignment.Left => ":---",
							RichAlignment.Center => ":---:",
							RichAlignment.Right => "---:",
							_ => "---",
						});
					lines.Add("| " + string.Join(" | ", delimiters) + " |");
				}
			}

			return string.Join(newline, lines);
		}
	}
}
