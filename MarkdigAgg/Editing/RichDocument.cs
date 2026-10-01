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
	/// The rich editor's model of a markdown file. Writing it back is Frontmatter, then for each block its
	/// SeparatorBefore and its markdown (OriginalSource unless Dirty), then TrailingText - so a document
	/// nobody edited round-trips byte-identical.
	/// </summary>
	public class RichDocument
	{
		/// <summary>
		/// YAML frontmatter (with its --- fences and trailing newline) the editor hides and never changes, or "".
		/// </summary>
		public string Frontmatter { get; set; } = "";

		public List<RichBlock> Blocks { get; set; } = new List<RichBlock>();

		/// <summary>
		/// Whatever follows the last block (usually the final newline). Like <see cref="RichBlock.SeparatorBefore"/>
		/// it holds only blank/whitespace text, never an alignment wrapper's closing &lt;/div&gt;.
		/// </summary>
		public string TrailingText { get; set; } = "";

		/// <summary>
		/// The largest caret offset at a position's block (and cell, for a table).
		/// </summary>
		public int TextLength(DocPosition position)
		{
			return Blocks[position.BlockIndex].TextLength(position.Row, position.Column);
		}

		/// <summary>
		/// A deep copy sharing no mutable state, so an undo snapshot is unaffected by later edits.
		/// </summary>
		public RichDocument Clone()
		{
			return new RichDocument
			{
				Frontmatter = Frontmatter,
				Blocks = Blocks.ConvertAll(block => block.Clone()),
				TrailingText = TrailingText,
			};
		}
	}

	/// <summary>
	/// A caret position: a block, and a plain-text offset within it (see <see cref="RichInlines"/>). In a Table
	/// the offset is within the cell at Row, Column; elsewhere Row and Column are 0. Positions order in
	/// reading order: block, then row, then column, then offset.
	/// </summary>
	public readonly record struct DocPosition(int BlockIndex, int Offset, int Row = 0, int Column = 0) : IComparable<DocPosition>
	{
		public int CompareTo(DocPosition other)
		{
			int result = BlockIndex.CompareTo(other.BlockIndex);
			if (result == 0)
			{
				result = Row.CompareTo(other.Row);
			}

			if (result == 0)
			{
				result = Column.CompareTo(other.Column);
			}

			if (result == 0)
			{
				result = Offset.CompareTo(other.Offset);
			}

			return result;
		}

		public static bool operator <(DocPosition a, DocPosition b) => a.CompareTo(b) < 0;

		public static bool operator >(DocPosition a, DocPosition b) => a.CompareTo(b) > 0;

		public static bool operator <=(DocPosition a, DocPosition b) => a.CompareTo(b) <= 0;

		public static bool operator >=(DocPosition a, DocPosition b) => a.CompareTo(b) >= 0;

		public static DocPosition Min(DocPosition a, DocPosition b) => a <= b ? a : b;

		public static DocPosition Max(DocPosition a, DocPosition b) => a >= b ? a : b;
	}
}
