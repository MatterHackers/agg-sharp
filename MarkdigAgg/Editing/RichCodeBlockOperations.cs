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
using System.Text;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The Code block toolbar button: text blocks become one fenced code block and a code block becomes
	/// paragraphs again, one per line. Unlike the other block ops this adds and removes blocks, so it returns the
	/// selection remapped onto the new blocks.
	/// </summary>
	public static class RichCodeBlockOperations
	{
		/// <summary>
		/// When the selection starts in a code block, that block becomes one paragraph per non-blank line. Otherwise
		/// the text blocks the selection touches (a range ending at the very start of a block does not touch it, as
		/// in <see cref="RichBlockOperations"/>) join into one ``` code block, their plain text separated by '\n';
		/// they leave any list or quote first, and character styles are dropped since code has none (an atom keeps
		/// its markdown as text). A range that also touches a table, Raw or code block changes nothing: there is no
		/// single code block a first-time user would expect from that.
		/// </summary>
		public static RichSelection ToggleCodeBlock(RichDocument document, RichSelection selection)
		{
			if (!CanToggleCodeBlock(document, selection))
			{
				return selection;
			}

			int startIndex = selection.Start.BlockIndex;
			if (document.Blocks[startIndex].Kind == RichBlockKind.CodeBlock)
			{
				return CodeToParagraphs(document, startIndex, selection);
			}

			return ParagraphsToCode(document, selection);
		}

		/// <summary>
		/// Whether <see cref="ToggleCodeBlock"/> would change anything: the selection starts in a code block, or
		/// every block it touches is a text block. The toolbar disables its button exactly where this is false.
		/// </summary>
		public static bool CanToggleCodeBlock(RichDocument document, RichSelection selection)
		{
			if (document.Blocks.Count == 0 || selection.Start.BlockIndex >= document.Blocks.Count)
			{
				return false;
			}

			if (document.Blocks[selection.Start.BlockIndex].Kind == RichBlockKind.CodeBlock)
			{
				return true;
			}

			var (first, last) = BlockRange(document, selection);
			for (int i = first; i <= last; i++)
			{
				if (!document.Blocks[i].IsTextBlock)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// One paragraph per non-blank line. Blank lines are dropped: a blank paragraph collapses on save anyway, so
		/// keeping them would only leave empty lines the user cannot keep. All-blank code gives one empty paragraph.
		/// </summary>
		private static RichSelection CodeToParagraphs(RichDocument document, int index, RichSelection selection)
		{
			var code = document.Blocks[index];
			var lines = code.CodeText.Replace("\r\n", "\n").Split('\n');

			// kept[k] is the line paragraph k comes from.
			var kept = new List<int>();
			for (int i = 0; i < lines.Length; i++)
			{
				if (!string.IsNullOrWhiteSpace(lines[i]))
				{
					kept.Add(i);
				}
			}

			string blankLine = RichEditOperations.BlankLine(document);
			var paragraphs = new List<RichBlock>();
			for (int k = 0; k < Math.Max(1, kept.Count); k++)
			{
				var paragraph = new RichBlock
				{
					Kind = RichBlockKind.Paragraph,
					SeparatorBefore = k == 0 ? code.SeparatorBefore : blankLine,
					Dirty = true,
				};

				if (k < kept.Count)
				{
					paragraph.Inlines.Add(new RichRun(lines[kept[k]]));
				}

				paragraphs.Add(paragraph);
			}

			document.Blocks.RemoveAt(index);
			document.Blocks.InsertRange(index, paragraphs);
			RichGroupRepair.Repair(document, index, index + paragraphs.Count - 1);

			DocPosition Map(DocPosition position)
			{
				if (position.BlockIndex < index)
				{
					return position;
				}

				if (position.BlockIndex > index)
				{
					return position with { BlockIndex = position.BlockIndex + paragraphs.Count - 1 };
				}

				if (kept.Count == 0)
				{
					return new DocPosition(index, 0);
				}

				// The code offset counts each '\n' as one character, exactly as the line split consumed them.
				int offset = position.Offset;
				int line = 0;
				while (line < lines.Length - 1 && offset > lines[line].Length)
				{
					offset -= lines[line].Length + 1;
					line++;
				}

				// A position on a dropped blank line moves to the start of the next kept line, or the end of the last.
				int k = kept.FindIndex(l => l >= line);
				if (k < 0)
				{
					k = kept.Count - 1;
					return new DocPosition(index + k, lines[kept[k]].Length);
				}

				return new DocPosition(index + k, kept[k] == line ? Math.Min(offset, lines[line].Length) : 0);
			}

			return new RichSelection(Map(selection.Anchor), Map(selection.Caret));
		}

		private static RichSelection ParagraphsToCode(RichDocument document, RichSelection selection)
		{
			var (first, last) = BlockRange(document, selection);

			// Leaving lists and quotes first lets RichBlockOperations do the group bookkeeping; it adds or removes
			// no blocks, so first..last still names the same blocks.
			RichBlockOperations.SetBlockKind(document, selection, 0);

			var lines = new List<string>();
			var lineStarts = new List<int>();
			int codeLength = 0;
			for (int i = first; i <= last; i++)
			{
				string line = PlainText(document.Blocks[i].Inlines, int.MaxValue);
				lineStarts.Add(codeLength);
				lines.Add(line);
				codeLength += line.Length + 1;
			}

			var codeBlock = new RichBlock
			{
				Kind = RichBlockKind.CodeBlock,
				CodeText = string.Join("\n", lines),
				CodeFence = "```",
				SeparatorBefore = first == 0 ? document.Blocks[0].SeparatorBefore : RichEditOperations.BlankLine(document),
				Dirty = true,
			};

			// Positions are mapped before the blocks go, while their inlines still say how long each atom's text is.
			DocPosition Map(DocPosition position)
			{
				if (position.BlockIndex < first)
				{
					return position;
				}

				if (position.BlockIndex > last)
				{
					return position with { BlockIndex = position.BlockIndex - (last - first) };
				}

				var inlines = document.Blocks[position.BlockIndex].Inlines;
				int line = position.BlockIndex - first;
				return new DocPosition(first, lineStarts[line] + PlainText(inlines, position.Offset).Length);
			}

			var mapped = new RichSelection(Map(selection.Anchor), Map(selection.Caret));
			document.Blocks.RemoveRange(first, last - first + 1);
			document.Blocks.Insert(first, codeBlock);
			RichGroupRepair.Repair(document, first, first);
			return mapped;
		}

		/// <summary>
		/// The code text of the inlines up to caret offset <paramref name="upTo"/>: run text as is, and an atom
		/// (image, line break) as its markdown, so nothing the user wrote is lost when it becomes code.
		/// </summary>
		private static string PlainText(List<RichInline> inlines, int upTo)
		{
			var text = new StringBuilder();
			int offset = 0;
			foreach (var inline in inlines)
			{
				if (offset >= upTo)
				{
					break;
				}

				if (inline is RichRun run)
				{
					text.Append(run.Text, 0, Math.Min(run.Text.Length, upTo - offset));
				}
				else if (inline is InlineAtom atom)
				{
					text.Append(atom.RawMarkdown);
				}

				offset += inline.Length;
			}

			return text.ToString();
		}

		/// <summary>
		/// The blocks a selection touches, by <see cref="RichBlockOperations"/>' rule: a range ending at offset 0 of a
		/// later block stops before it.
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
	}
}
