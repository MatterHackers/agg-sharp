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
	public enum RichBlockKind
	{
		Paragraph,
		Heading,
		ListItem,
		Quote,
		CodeBlock,
		Table,

		/// <summary>
		/// Markdown the rich view does not model (raw HTML, thematic breaks, ...). Shown rendered and edited
		/// only in the Markdown tab; always written back as <see cref="RichBlock.OriginalSource"/>.
		/// </summary>
		Raw,
	}

	public enum RichAlignment
	{
		Left,
		Center,
		Right,
	}

	/// <summary>
	/// What makes a block a list item. Each item is its own block so the caret, Enter and Tab treat items like
	/// paragraphs; Depth and the marker are enough for the writer to rebuild the list.
	/// </summary>
	public class RichListInfo
	{
		public bool Ordered { get; set; }

		/// <summary>
		/// Nesting level, 0 for a top-level item.
		/// </summary>
		public int Depth { get; set; }

		/// <summary>
		/// The source marker character ('-', '*', '+' for bullets; '.' or ')' for numbered) so a rewrite keeps
		/// the author's choice.
		/// </summary>
		public char Marker { get; set; } = '-';

		/// <summary>
		/// The number an ordered list (or nested sublist) starts at. Every item of the (sub)list carries it, so
		/// deleting the first item keeps the start.
		/// </summary>
		public int StartNumber { get; set; } = 1;

		public RichListInfo Clone() => (RichListInfo)MemberwiseClone();
	}

	public class RichTableCell
	{
		public List<RichInline> Inlines { get; set; } = new List<RichInline>();

		public RichTableCell Clone() => new RichTableCell { Inlines = RichInlines.Clone(Inlines) };
	}

	/// <summary>
	/// One top-level unit of a <see cref="RichDocument"/>. Which members matter depends on <see cref="Kind"/>:
	/// text-bearing kinds (Paragraph, Heading, ListItem, Quote) use <see cref="Inlines"/>; CodeBlock uses
	/// <see cref="CodeText"/>; Table uses <see cref="TableRows"/>; Raw uses only <see cref="OriginalSource"/>.
	/// </summary>
	public class RichBlock
	{
		public RichBlockKind Kind { get; set; }

		/// <summary>
		/// 1-6 for a Heading, 0 otherwise.
		/// </summary>
		public int HeadingLevel { get; set; }

		/// <summary>
		/// Set only for a ListItem.
		/// </summary>
		public RichListInfo List { get; set; }

		/// <summary>
		/// Paragraph and heading alignment, stored in markdown as a &lt;div align&gt; wrapper. An edit changes
		/// this; <see cref="AlignGroup"/> keeps the wrapper's parsed alignment.
		/// </summary>
		public RichAlignment Alignment { get; set; }

		public List<RichInline> Inlines { get; set; } = new List<RichInline>();

		/// <summary>
		/// A CodeBlock's text, lines joined with '\n' and no trailing newline, without fences or indentation.
		/// </summary>
		public string CodeText { get; set; } = "";

		/// <summary>
		/// A CodeBlock's info string (language), or "" when it has none.
		/// </summary>
		public string CodeLanguage { get; set; } = "";

		/// <summary>
		/// A Table's rows, header row first; every row has one cell per column.
		/// </summary>
		public List<List<RichTableCell>> TableRows { get; set; } = new List<List<RichTableCell>>();

		/// <summary>
		/// One entry per table column; null is a column with no alignment colon (--- rather than :---), which
		/// writes and renders differently from an explicit Left.
		/// </summary>
		public List<RichAlignment?> ColumnAlignments { get; set; } = new List<RichAlignment?>();

		/// <summary>
		/// The exact markdown this block was parsed from; written back verbatim while the block is not Dirty.
		/// A ListItem's source runs from its marker to the end of its paragraph (its indentation and any nested
		/// items' lines are not in it: indentation is whitespace in <see cref="SeparatorBefore"/>, nested items
		/// are blocks of their own). A Quote's source carries its own "&gt; " prefixes; for the second and later
		/// paragraphs of one blockquote it also starts with the "&gt;" line that separates them from the paragraph
		/// before, so the separator between them stays a bare line break. Which quote or list a block belongs to
		/// is <see cref="QuoteGroup"/> / <see cref="ListGroup"/>, never inferred from these bytes.
		/// </summary>
		public string OriginalSource { get; set; } = "";

		/// <summary>
		/// The markdown between the previous block (or the frontmatter, for the first block) and this one,
		/// usually blank lines. Kept on the block so inserting and deleting blocks keeps each gap with its block.
		/// Holds only blank/whitespace text: alignment wrappers (&lt;div align&gt; / &lt;/div&gt;) are never
		/// stored here; an untouched wrapper's original bytes live on <see cref="AlignGroup"/>.
		/// </summary>
		public string SeparatorBefore { get; set; } = "";

		/// <summary>
		/// The &lt;div align&gt; wrapper this paragraph or heading sits in, shared with the other members; null
		/// outside a wrapper. See <see cref="RichBlockGroup"/> for when its original bytes are written.
		/// </summary>
		public RichAlignGroup AlignGroup { get; set; }

		/// <summary>
		/// The blockquote this Quote block is a paragraph of, shared with its other paragraphs.
		/// </summary>
		public RichQuoteGroup QuoteGroup { get; set; }

		/// <summary>
		/// The top-level list this ListItem belongs to, shared with every item at any depth of that list.
		/// </summary>
		public RichListGroup ListGroup { get; set; }

		/// <summary>
		/// True once an edit touched this block; the writer regenerates dirty blocks and copies the rest verbatim.
		/// </summary>
		public bool Dirty { get; set; }

		public bool IsTextBlock => Kind == RichBlockKind.Paragraph
			|| Kind == RichBlockKind.Heading
			|| Kind == RichBlockKind.ListItem
			|| Kind == RichBlockKind.Quote;

		/// <summary>
		/// The inlines a position's offset counts through: the block's own, or a table cell's.
		/// Throws for CodeBlock and Raw, which have no inlines.
		/// </summary>
		public List<RichInline> InlinesAt(int row, int column)
		{
			if (Kind == RichBlockKind.Table)
			{
				return TableRows[row][column].Inlines;
			}

			if (IsTextBlock)
			{
				return Inlines;
			}

			throw new InvalidOperationException($"A {Kind} block has no inlines.");
		}

		/// <summary>
		/// The largest caret offset in the block (in the given cell for a Table). A Raw block is one atom.
		/// </summary>
		public int TextLength(int row = 0, int column = 0)
		{
			switch (Kind)
			{
				case RichBlockKind.CodeBlock:
					return CodeText.Length;
				case RichBlockKind.Raw:
					return 1;
				default:
					return RichInlines.Length(InlinesAt(row, column));
			}
		}

		/// <summary>
		/// A deep copy of the block's own content. Group references are kept, so the copy is still a member of
		/// the same groups; <see cref="RichDocument.Clone"/> remaps them to cloned groups.
		/// </summary>
		public RichBlock Clone()
		{
			var copy = (RichBlock)MemberwiseClone();
			copy.List = List?.Clone();
			copy.Inlines = RichInlines.Clone(Inlines);
			copy.TableRows = new List<List<RichTableCell>>(TableRows.Count);
			foreach (var row in TableRows)
			{
				copy.TableRows.Add(row.ConvertAll(cell => cell.Clone()));
			}

			copy.ColumnAlignments = new List<RichAlignment?>(ColumnAlignments);
			return copy;
		}
	}
}
