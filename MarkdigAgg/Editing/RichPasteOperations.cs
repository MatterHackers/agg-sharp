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

using System.Collections.Generic;
using System.Text;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The model half of copy and paste, free of any clipboard: <see cref="Slice"/> cuts a selection out as a
	/// fragment document, <see cref="VisibleText"/> is that fragment as the plain text a reader sees, and
	/// <see cref="InsertDocument"/> pastes a fragment at a caret the way word processors do.
	/// </summary>
	public static class RichPasteOperations
	{
		/// <summary>
		/// A copy of the selected content as its own document. A range inside one block is a single paragraph of
		/// the selected inlines (styles and links kept, the block's kind not - a few words of a heading paste as
		/// words); a range inside one code block is a paragraph of its plain text. Across blocks the end blocks are
		/// cut at the selection and keep their kind, and a code block, table or Raw block at an end comes whole
		/// unless the range only touches it - the same blocks <see cref="RichEditOperations.DeleteRange"/> removes,
		/// so a cut pastes back what it took. The fragment's groups are its own and every block is Dirty, so it
		/// writes as fresh markdown and editing the source never reaches it.
		/// </summary>
		public static RichDocument Slice(RichDocument document, RichSelection selection)
		{
			var fragment = new RichDocument();
			if (selection.IsEmpty || document.Blocks.Count == 0)
			{
				return fragment;
			}

			var blocks = document.Blocks;
			var start = selection.Start;
			var end = selection.End;
			if (selection.WholeBlock)
			{
				fragment.Blocks.Add(blocks[start.BlockIndex].Clone());
			}
			else if (start.BlockIndex == end.BlockIndex)
			{
				var block = blocks[start.BlockIndex];
				bool sameCell = start.Row == end.Row && start.Column == end.Column;
				if (block.Kind == RichBlockKind.CodeBlock)
				{
					var run = new RichRun(block.CodeText.Substring(start.Offset, end.Offset - start.Offset));
					fragment.Blocks.Add(Paragraph(new List<RichInline> { run }));
				}
				else if (block.IsTextBlock || (block.Kind == RichBlockKind.Table && sameCell))
				{
					fragment.Blocks.Add(Paragraph(SliceInlines(block.InlinesAt(start.Row, start.Column), start.Offset, end.Offset)));
				}
				else if (block.Kind == RichBlockKind.Table)
				{
					fragment.Blocks.Add(SliceTable(block, start, end));
				}
				else
				{
					fragment.Blocks.Add(block.Clone());
				}
			}
			else
			{
				for (int i = start.BlockIndex; i <= end.BlockIndex; i++)
				{
					// A block the range only touches (it starts at a block's end, as Shift+Down from a line's end
					// does, or ends at the next block's start) has nothing selected; copying it would paste an empty
					// list item or paragraph.
					bool touchedOnly = (i == start.BlockIndex && start == RichEditOperations.EndOf(document, i))
						|| (i == end.BlockIndex && end == new DocPosition(i, 0));
					if (touchedOnly)
					{
						continue;
					}

					var block = blocks[i];
					var copy = block.Clone();
					if (block.IsTextBlock)
					{
						int from = i == start.BlockIndex ? start.Offset : 0;
						int to = i == end.BlockIndex ? end.Offset : block.TextLength();
						copy.Inlines = SliceInlines(block.Inlines, from, to);
					}

					fragment.Blocks.Add(copy);
				}
			}

			Detach(fragment.Blocks, "\n\n");
			RichGroupRepair.NormalizeListDepths(fragment);
			return fragment;
		}

		/// <summary>
		/// The fragment as a reader sees it, for the clipboard's plain text: blocks on their own lines, table cells
		/// separated by tabs and rows by lines, a hard break as a line break, an autolink as its address. Images
		/// and inline HTML have no text, and a Raw block gives its markdown since that is all it has.
		/// </summary>
		public static string VisibleText(RichDocument fragment)
		{
			var text = new StringBuilder();
			for (int i = 0; i < fragment.Blocks.Count; i++)
			{
				if (i > 0)
				{
					text.Append('\n');
				}

				var block = fragment.Blocks[i];
				switch (block.Kind)
				{
					case RichBlockKind.CodeBlock:
						text.Append(block.CodeText);
						break;

					case RichBlockKind.Raw:
						text.Append(block.OriginalSource.Trim());
						break;

					case RichBlockKind.Table:
						for (int row = 0; row < block.TableRows.Count; row++)
						{
							if (row > 0)
							{
								text.Append('\n');
							}

							for (int column = 0; column < block.TableRows[row].Count; column++)
							{
								if (column > 0)
								{
									text.Append('\t');
								}

								AppendInlines(text, block.TableRows[row][column].Inlines);
							}
						}

						break;

					default:
						AppendInlines(text, block.Inlines);
						break;
				}
			}

			return text.ToString();
		}

		/// <summary>
		/// Pastes <paramref name="fragment"/> at <paramref name="position"/> and returns the caret after it. A lone
		/// paragraph goes into the text at the caret. Otherwise the caret's block splits: a leading paragraph joins
		/// the text before the caret, a trailing paragraph is joined by the text after it (that part keeps the
		/// caret block's kind), and everything else - headings, list items, quotes, code, tables - comes in as
		/// blocks between, joining the caret's list, quote or alignment wrapper where they fit it. An empty text
		/// before the caret is dropped rather than left as a blank line above a pasted heading or list. In a code
		/// block only the fragment's <see cref="VisibleText"/> goes in, as there is nowhere for blocks there; a table
		/// cell takes a lone paragraph with its styles and anything else as one line of text. At a Raw block the
		/// paste goes into a new paragraph beside it. Verbatim markdown in the paste takes the document's line ending.
		/// The fragment itself is not changed, so one copy pastes any number of times.
		/// </summary>
		public static RichSelection InsertDocument(RichDocument document, DocPosition position, RichDocument fragment)
		{
			if (fragment.Blocks.Count == 0)
			{
				return RichSelection.At(position);
			}

			if (document.Blocks.Count == 0)
			{
				document.Blocks.Add(new RichBlock { Kind = RichBlockKind.Paragraph, Dirty = true });
			}

			var target = document.Blocks[position.BlockIndex];
			if (target.Kind == RichBlockKind.CodeBlock)
			{
				return RichEditOperations.InsertText(document, position, VisibleText(fragment));
			}

			string blankLine = RichEditOperations.BlankLine(document);
			var pasted = fragment.Blocks.ConvertAll(block => block.Clone());
			Detach(pasted, blankLine);
			UseLineEnding(pasted, blankLine.Substring(0, blankLine.Length / 2));
			if (target.Kind == RichBlockKind.Table)
			{
				return InsertIntoCell(document, position, pasted, fragment);
			}

			if (target.Kind == RichBlockKind.Raw)
			{
				position = RichEditOperations.SplitBlock(document, position).Caret;
				target = document.Blocks[position.BlockIndex];
			}


			// Every pasted block follows something here: the text before the caret, or (when that is dropped) whatever
			// preceded the caret block, whose gap the first block then takes over below.
			pasted[0].SeparatorBefore = blankLine;
			target.Dirty = true;
			int split = RichInlines.SplitAt(target.Inlines, position.Offset);
			if (pasted.Count == 1 && pasted[0].Kind == RichBlockKind.Paragraph)
			{
				target.Inlines.InsertRange(split, pasted[0].Inlines);
				RichInlines.MergeAdjacent(target.Inlines);
				return RichSelection.At(position with { Offset = position.Offset + RichInlines.Length(pasted[0].Inlines) });
			}

			var tail = target.Inlines.GetRange(split, target.Inlines.Count - split);
			target.Inlines.RemoveRange(split, target.Inlines.Count - split);
			if (pasted[0].Kind == RichBlockKind.Paragraph)
			{
				target.Inlines.AddRange(pasted[0].Inlines);
				pasted.RemoveAt(0);
			}

			RichBlock tailBlock = null;
			int caretOffset = 0;
			if (pasted.Count > 0 && pasted[pasted.Count - 1].Kind == RichBlockKind.Paragraph)
			{
				var last = pasted[pasted.Count - 1];
				pasted.RemoveAt(pasted.Count - 1);
				caretOffset = RichInlines.Length(last.Inlines);
				tailBlock = LikeTarget(target, last.Inlines, tail, blankLine);
			}
			else if (RichInlines.Length(tail) > 0)
			{
				tailBlock = LikeTarget(target, new List<RichInline>(), tail, blankLine);
			}

			var joinedList = JoinCaretGroups(target, pasted);
			var replacement = new List<RichBlock>();
			if (RichInlines.Length(target.Inlines) > 0 || pasted.Count == 0)
			{
				replacement.Add(target);
			}
			else
			{
				pasted[0].SeparatorBefore = target.SeparatorBefore;
			}

			replacement.AddRange(pasted);
			if (tailBlock != null)
			{
				replacement.Add(tailBlock);
			}

			foreach (var block in replacement)
			{
				RichInlines.MergeAdjacent(block.Inlines);
			}

			int at = position.BlockIndex;
			document.Blocks.RemoveAt(at);
			document.Blocks.InsertRange(at, replacement);
			int lastIndex = at + replacement.Count - 1;
			for (int i = at; i <= lastIndex; i++)
			{
				if (joinedList.Contains(document.Blocks[i]))
				{
					RichGroupRepair.AdoptLevelStyle(document.Blocks, i);
				}
			}

			// A pasted block that did not join the caret's group (a heading in a list) splits it into runs, and
			// pasted list items take the depth rules of their new place.
			RichGroupRepair.Repair(document, at, lastIndex);
			var caret = tailBlock != null
				? new DocPosition(lastIndex, caretOffset)
				: RichEditOperations.EndOf(document, lastIndex);
			return RichSelection.At(caret);
		}

		/// <summary>
		/// The rows a multi-cell selection covers, holding only the covered text: the cells between the ends in
		/// reading order, the end cells cut at the selection, the rest empty - exactly what
		/// <see cref="RichTableCodeOperations.DeleteRange"/> clears, so a cut copies what it removed.
		/// </summary>
		private static RichBlock SliceTable(RichBlock table, DocPosition start, DocPosition end)
		{
			var copy = table.Clone();
			int columns = table.TableRows[0].Count;
			int first = start.Row * columns + start.Column;
			int last = end.Row * columns + end.Column;
			for (int row = 0; row < copy.TableRows.Count; row++)
			{
				for (int column = 0; column < columns; column++)
				{
					int cell = row * columns + column;
					var inlines = table.TableRows[row][column].Inlines;
					int from = cell == first ? start.Offset : 0;
					int to = cell == last ? end.Offset : RichInlines.Length(inlines);
					copy.TableRows[row][column].Inlines = cell < first || cell > last
						? new List<RichInline>()
						: SliceInlines(inlines, from, to);
				}
			}

			copy.TableRows = copy.TableRows.GetRange(start.Row, end.Row - start.Row + 1);
			return copy;
		}

		/// <summary>
		/// Clipboard text from outside the editor as a fragment. It is read as markdown, except that a single line
		/// break stays a line break (a hard break) rather than joining the lines as markdown would, so pasted
		/// plain text keeps its lines; a blank line still starts a paragraph. A paragraph that would hold more than
		/// <see cref="MaxPastedLineBreaks"/> breaks becomes one paragraph per line instead, so a pasted log or
		/// listing stays cheap to edit.
		/// </summary>
		public static RichDocument ParseClipboardText(string text)
		{
			var parsed = RichMarkdownParser.Parse(text, softBreaksAreHard: true);
			var blocks = parsed.Blocks;
			for (int i = blocks.Count - 1; i >= 0; i--)
			{
				var block = blocks[i];
				if (block.Kind != RichBlockKind.Paragraph
					|| block.Inlines.FindAll(inline => inline is InlineAtom { Kind: InlineAtomKind.HardBreak }).Count <= MaxPastedLineBreaks)
				{
					continue;
				}

				var lines = new List<RichBlock>();
				var line = new List<RichInline>();
				foreach (var inline in block.Inlines)
				{
					if (inline is InlineAtom { Kind: InlineAtomKind.HardBreak })
					{
						lines.Add(WithInlines(block, line));
						line = new List<RichInline>();
					}
					else
					{
						line.Add(inline);
					}
				}

				lines.Add(WithInlines(block, line));
				blocks.RemoveAt(i);
				blocks.InsertRange(i, lines);
			}

			return parsed;
		}

		/// <summary>
		/// The most line breaks a pasted paragraph keeps as one paragraph; see <see cref="ParseClipboardText"/>.
		/// </summary>
		public const int MaxPastedLineBreaks = 20;

		private static RichBlock WithInlines(RichBlock block, List<RichInline> inlines)
		{
			var copy = block.Clone();
			copy.Inlines = inlines;
			copy.Dirty = true;
			return copy;
		}

		/// <summary>
		/// A paste into a table cell: a lone paragraph goes in with its styles (a cell has no line breaks, so a
		/// hard break becomes a space); anything else is flattened to one line of its text.
		/// </summary>
		private static RichSelection InsertIntoCell(RichDocument document, DocPosition position, List<RichBlock> pasted, RichDocument fragment)
		{
			if (pasted.Count != 1 || pasted[0].Kind != RichBlockKind.Paragraph)
			{
				string line = VisibleText(fragment).Replace('\n', ' ').Replace('\t', ' ');
				return RichEditOperations.InsertText(document, position, line);
			}

			var inlines = pasted[0].Inlines.ConvertAll(inline => inline is InlineAtom { Kind: InlineAtomKind.HardBreak }
				? new RichRun(" ") { Bold = inline.Bold, Italic = inline.Italic, Strike = inline.Strike }
				: inline);
			var table = document.Blocks[position.BlockIndex];
			var cell = table.InlinesAt(position.Row, position.Column);
			cell.InsertRange(RichInlines.SplitAt(cell, position.Offset), inlines);
			RichInlines.MergeAdjacent(cell);
			table.Dirty = true;
			return RichSelection.At(position with { Offset = position.Offset + RichInlines.Length(inlines) });
		}

		/// <summary>
		/// Blocks pasted inside a list, quote or alignment wrapper join it rather than splitting it in two: pasted
		/// list items join the caret's list (their depths below the caret item's), pasted quote paragraphs its
		/// quote, paragraphs and headings its wrapper, and a plain paragraph becomes an item or quote paragraph
		/// like the caret's block. Returns the blocks that joined a list, whose numbering style is adopted once
		/// they are in place.
		/// </summary>
		private static HashSet<RichBlock> JoinCaretGroups(RichBlock target, List<RichBlock> middle)
		{
			var joinedList = new HashSet<RichBlock>();
			foreach (var block in middle)
			{
				bool plain = block.Kind == RichBlockKind.Paragraph && block.QuoteGroup == null && block.AlignGroup == null;
				if (target.Kind == RichBlockKind.ListItem && block.Kind == RichBlockKind.ListItem)
				{
					block.ListGroup = target.ListGroup;
					block.List.Depth += target.List.Depth;
					joinedList.Add(block);
				}
				else if (target.Kind == RichBlockKind.ListItem && plain)
				{
					block.Kind = RichBlockKind.ListItem;
					block.List = target.List.Clone();
					block.ListGroup = target.ListGroup;
				}
				else if (target.Kind == RichBlockKind.Quote && (plain || block.Kind == RichBlockKind.Quote))
				{
					block.Kind = RichBlockKind.Quote;
					block.QuoteGroup = target.QuoteGroup;
				}

				if (target.AlignGroup != null && (block.Kind == RichBlockKind.Paragraph || block.Kind == RichBlockKind.Heading))
				{
					block.AlignGroup = target.AlignGroup;
					block.Alignment = target.Alignment;
				}
			}

			return joinedList;
		}

		/// <summary>
		/// Puts the document's line ending into what pasted blocks write verbatim (a Raw block's source, an atom's
		/// markdown), so a CRLF file never gains bare LF lines from a paste. Code text is '\n' inside the model.
		/// </summary>
		private static void UseLineEnding(List<RichBlock> blocks, string newline)
		{
			string Fix(string text)
			{
				text = text.Replace("\r\n", "\n");
				return newline == "\n" ? text : text.Replace("\n", newline);
			}

			void FixAtoms(List<RichInline> inlines)
			{
				foreach (var inline in inlines)
				{
					if (inline is InlineAtom atom)
					{
						atom.RawMarkdown = Fix(atom.RawMarkdown);
					}
				}
			}

			foreach (var block in blocks)
			{
				block.OriginalSource = Fix(block.OriginalSource);
				FixAtoms(block.Inlines);
				foreach (var row in block.TableRows)
				{
					foreach (var cell in row)
					{
						FixAtoms(cell.Inlines);
					}
				}
			}
		}

		private static RichBlock Paragraph(List<RichInline> inlines)
		{
			return new RichBlock { Kind = RichBlockKind.Paragraph, Inlines = inlines };
		}

		/// <summary>
		/// The inlines from offset <paramref name="from"/> to <paramref name="to"/>, cloned so the source is not split.
		/// </summary>
		private static List<RichInline> SliceInlines(List<RichInline> inlines, int from, int to)
		{
			var copy = RichInlines.Clone(inlines);
			int first = RichInlines.SplitAt(copy, from);
			int last = RichInlines.SplitAt(copy, to);
			return copy.GetRange(first, last - first);
		}

		/// <summary>
		/// The part of the caret block after the paste: the caret block's kind, level, list info and groups, holding
		/// <paramref name="lead"/> then <paramref name="tail"/>.
		/// </summary>
		private static RichBlock LikeTarget(RichBlock target, List<RichInline> lead, List<RichInline> tail, string blankLine)
		{
			var block = target.Clone();
			block.Inlines = lead;
			block.Inlines.AddRange(tail);
			block.SeparatorBefore = blankLine;
			block.Dirty = true;
			return block;
		}

		/// <summary>
		/// Gives copied blocks groups of their own, marked as made by an edit so they are regenerated rather than
		/// written from the source's bytes; marks them Dirty and spaces them with <paramref name="blankLine"/>.
		/// </summary>
		private static void Detach(List<RichBlock> blocks, string blankLine)
		{
			var groups = new Dictionary<RichBlockGroup, RichBlockGroup>();
			T Remap<T>(T group)
				where T : RichBlockGroup
			{
				if (group == null)
				{
					return null;
				}

				if (!groups.TryGetValue(group, out var copy))
				{
					copy = group.Clone();
					copy.OriginalMemberCount = 0;
					groups.Add(group, copy);
				}

				return (T)copy;
			}

			for (int i = 0; i < blocks.Count; i++)
			{
				var block = blocks[i];
				block.AlignGroup = Remap(block.AlignGroup);
				block.QuoteGroup = Remap(block.QuoteGroup);
				block.ListGroup = Remap(block.ListGroup);
				block.SeparatorBefore = i == 0 ? "" : blankLine;
				block.Dirty = true;
			}
		}

		private static void AppendInlines(StringBuilder text, List<RichInline> inlines)
		{
			foreach (var inline in inlines)
			{
				switch (inline)
				{
					case RichRun run:
						text.Append(run.Text);
						break;

					case InlineAtom { Kind: InlineAtomKind.HardBreak }:
						text.Append('\n');
						break;

					case InlineAtom { Kind: InlineAtomKind.Autolink } atom:
						text.Append(atom.RawMarkdown.Trim('<', '>'));
						break;

					case InlineAtom { Kind: InlineAtomKind.Raw } atom:
						text.Append(atom.RawMarkdown);
						break;
				}
			}
		}
	}
}
