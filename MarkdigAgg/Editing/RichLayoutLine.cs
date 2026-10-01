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
using MatterHackers.Agg.Font;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// A piece of one line drawn in one face: part of a run, a whole atom, or part of a code block's text.
	/// Coordinates are a <see cref="RichBlockLayout"/>'s: y up from the bottom of the block.
	/// </summary>
	public sealed class RichLayoutFragment
	{
		/// <summary>
		/// The inline this fragment draws, or -1 for a code block's text and a Raw block's box.
		/// </summary>
		public int InlineIndex { get; init; }

		/// <summary>
		/// Where the fragment starts within its inline's text (within CodeText for a code block).
		/// </summary>
		public int StartInInline { get; init; }

		/// <summary>
		/// The block offset of the fragment's first caret position.
		/// </summary>
		public int Start { get; init; }

		/// <summary>
		/// How many block offsets the fragment covers (1 for an atom).
		/// </summary>
		public int Length { get; init; }

		/// <summary>
		/// What to draw: the run's characters, an atom's raw markdown, or "" for an image box.
		/// </summary>
		public string Text { get; init; }

		/// <summary>
		/// The face to draw <see cref="Text"/> in; null for a box (an image or a Raw block) the widget fills.
		/// </summary>
		public StyledTypeFace Face { get; init; }

		/// <summary>
		/// The atom this fragment shows, or null for text.
		/// </summary>
		public InlineAtom Atom { get; init; }

		public double X { get; init; }

		public double Width { get; init; }

		public double Baseline { get; internal set; }

		/// <summary>
		/// Height above the baseline (a box's whole height: boxes sit on the baseline).
		/// </summary>
		public double Ascent { get; init; }

		/// <summary>
		/// Depth below the baseline, positive.
		/// </summary>
		public double Descent { get; init; }

		public double Height => Ascent + Descent;
	}

	/// <summary>
	/// A caret position in a block. <see cref="AtLineEnd"/> picks between the two places an offset can show where a
	/// line wraps with no space (a word broken between characters, or a box pushed to the next line): false is the
	/// start of the next line, true the end of the line before - where a click right of that line or End puts it.
	/// In a table, <see cref="Row"/> and <see cref="Column"/> name the cell <see cref="Offset"/> counts through, as
	/// in <see cref="DocPosition"/>; they are 0 in every other block.
	/// </summary>
	public readonly record struct RichCaret(int Offset, bool AtLineEnd = false, int Row = 0, int Column = 0);

	/// <summary>
	/// A place on a line the caret can sit, with its x (block coordinates, clamped into the text column).
	/// </summary>
	public readonly record struct RichCaretStop(RichCaret Caret, double X);

	/// <summary>
	/// One laid-out line, with the caret stops on it in order. Offsets inside a character (between the halves of a
	/// surrogate pair, before a combining mark) or inside an atom have no stop. The offset where a line wraps is a
	/// plain stop at the start of the next line; the spaces a wrap happens at stay at the end of the line before
	/// (past <see cref="Width"/>), so each of their offsets still has its own stop. A wrap with no space adds an
	/// <see cref="RichCaret.AtLineEnd"/> stop at the end of the line before.
	/// </summary>
	public sealed class RichLayoutLine
	{
		public List<RichLayoutFragment> Fragments { get; } = new List<RichLayoutFragment>();

		public List<RichCaretStop> CaretStops { get; } = new List<RichCaretStop>();

		/// <summary>
		/// The block offset of the line's first item.
		/// </summary>
		public int Start { get; internal set; }

		/// <summary>
		/// The block offset just after the line's last item: the next line's Start.
		/// </summary>
		public int End { get; internal set; }

		public bool IsLast { get; internal set; }

		/// <summary>
		/// The bottom of the line's box (y up), including half the style's line gap.
		/// </summary>
		public double Bottom { get; internal set; }

		public double Top { get; internal set; }

		public double Baseline { get; internal set; }

		public double Ascent { get; internal set; }

		/// <summary>
		/// Depth below the baseline, positive.
		/// </summary>
		public double Descent { get; internal set; }

		/// <summary>
		/// The x of the line's first caret position: the block's text left plus the alignment offset.
		/// </summary>
		public double Left { get; internal set; }

		/// <summary>
		/// The width of the line's content, not counting spaces it wrapped at or a trailing hard break; alignment
		/// centres this width.
		/// </summary>
		public double Width { get; internal set; }
	}
}
