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
	/// byte-identical; a changed group is regenerated whole by <see cref="RichGroupWriter"/>.
	/// </summary>
	public static class RichMarkdownWriter
	{
		public static string Write(RichDocument document)
		{
			var blocks = document.Blocks;
			var markdown = new StringBuilder(document.Frontmatter);
			string blankLine = RichEditOperations.BlankLine(document);
			string newline = blankLine.Substring(0, blankLine.Length / 2);
			var unchanged = RichGroupWriter.UnchangedGroups(blocks);

			// After a regenerated unit the next block needs a blank line before it; previousList is the top-level
			// style of a list just written, so a regenerated list next to it can keep the two apart, and
			// listContentColumn is where that list's last top-level item's text starts: a block indented that far
			// after it would be read into that item.
			bool blankLineNext = false;
			RichListInfo previousList = null;
			int listContentColumn = -1;
			for (int i = 0; i < blocks.Count;)
			{
				var block = blocks[i];
				int unitEnd = RichGroupWriter.RegeneratedUnitEnd(blocks, i, unchanged);
				if (unitEnd >= 0)
				{
					markdown.Append(RichGroupWriter.LeadingSeparator(block.SeparatorBefore, newline, i == 0));
					markdown.Append(RichGroupWriter.WriteUnit(blocks, i, unitEnd, newline, ref previousList, ref listContentColumn));
					blankLineNext = true;
					i = unitEnd + 1;
					continue;
				}

				// An unchanged wrapper's bytes go around its first and last members; a changed one at Left drops.
				var align = block.AlignGroup != null && unchanged.Contains(block.AlignGroup) ? block.AlignGroup : null;
				string text = block.Dirty ? WriteBlock(block, newline) : block.OriginalSource;
				if (text.Length == 0 && align == null)
				{
					// An empty paragraph writes nothing, gap included, so the lists or quotes around it stay apart
					// exactly as if it were not there.
					i++;
					continue;
				}

				string separator = blankLineNext ? RichGroupWriter.WithBlankLine(block.SeparatorBefore, newline) : block.SeparatorBefore;
				if (block.Kind != RichBlockKind.ListItem && listContentColumn >= 0
					&& RichGroupWriter.Column(separator, text) >= listContentColumn)
				{
					// Only an edit can put an indented block right after a list (the parser would have read it into
					// the list): start it at the margin, and fence indented code, whose indentation is its syntax.
					separator = separator.Substring(0, separator.LastIndexOf('\n') + 1);
					if (block.Kind == RichBlockKind.CodeBlock)
					{
						text = WriteCode(block, newline, fenced: true);
					}
				}

				markdown.Append(separator);
				blankLineNext = false;
				if (align != null && (i == 0 || blocks[i - 1].AlignGroup != align))
				{
					markdown.Append(align.OpenSource);
				}

				markdown.Append(text);
				if (align != null && (i == blocks.Count - 1 || blocks[i + 1].AlignGroup != align))
				{
					markdown.Append(align.CloseSource);
				}

				if (block.Kind != RichBlockKind.ListItem)
				{
					previousList = null;
					listContentColumn = -1;
				}
				else if (block.List?.Depth == 0)
				{
					previousList = block.List;
					listContentColumn = RichGroupWriter.OriginalContentColumn(block);
				}

				i++;
			}

			markdown.Append(document.TrailingText);
			return markdown.ToString();
		}

		/// <summary>
		/// Markdown for an edited block, or for a member of a regenerated alignment wrapper. Raw blocks are never
		/// edited in the rich view, so their source stands. List items and quotes are always written as part of
		/// their whole group (see <see cref="RichGroupWriter"/>).
		/// </summary>
		internal static string WriteBlock(RichBlock block, string newline)
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

			// Unreachable: the writer sends every ListItem and Quote through its group.
			throw new InvalidOperationException($"A {block.Kind} block is written with its group.");
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
		/// those write fenced, as does <paramref name="fenced"/> code (indentation would read as list content).
		/// </summary>
		private static string WriteCode(RichBlock block, string newline, bool fenced = false)
		{
			var lines = block.CodeText.Replace("\r\n", "\n").Split('\n');
			bool canIndent = block.CodeText.Length > 0 && lines[0].Trim().Length > 0 && lines[^1].Trim().Length > 0;
			if (block.CodeFence.Length == 0 && canIndent && !fenced)
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
			var markdown = new StringBuilder(fence).Append(InfoAsWritten(block)).Append(newline);
			if (block.CodeText.Length > 0)
			{
				markdown.Append(string.Join(newline, lines)).Append(newline);
			}

			return markdown.Append(fence).ToString();
		}

		/// <summary>
		/// The code's info string with the author's spacing after the fence ("~~~ c#" stays "~~~ c#") while it
		/// still matches <see cref="RichBlock.CodeInfo"/>; the parser keeps only the trimmed info.
		/// </summary>
		private static string InfoAsWritten(RichBlock block)
		{
			string firstLine = block.OriginalSource.TrimStart(' ', '\t');
			int lineBreak = firstLine.IndexOf('\n');
			firstLine = (lineBreak < 0 ? firstLine : firstLine.Substring(0, lineBreak)).TrimEnd();
			if (block.CodeFence.Length > 0 && firstLine.StartsWith(block.CodeFence, StringComparison.Ordinal))
			{
				string info = firstLine.Substring(block.CodeFence.Length);
				if (info.Trim() == block.CodeInfo)
				{
					return info;
				}
			}

			return block.CodeInfo;
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
