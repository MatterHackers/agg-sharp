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
	/// Offset arithmetic over a list of inlines (a paragraph's or a table cell's). A plain-text offset counts
	/// each run's characters and each atom as one.
	/// </summary>
	public static class RichInlines
	{
		/// <summary>
		/// The plain-text length of the inlines: the largest valid caret offset.
		/// </summary>
		public static int Length(IReadOnlyList<RichInline> inlines)
		{
			int length = 0;
			foreach (var inline in inlines)
			{
				length += inline.Length;
			}

			return length;
		}

		/// <summary>
		/// Finds the inline holding a plain-text offset. An offset on the boundary between two inlines is
		/// ambiguous: by default it resolves to the end of the earlier one (where typed text inherits its style);
		/// with <paramref name="preferNext"/> it resolves to the start of the later one. Empty runs are skipped:
		/// offset 0 resolves to the start of the first non-empty inline (so not inline 0 after an empty leading
		/// run), and the end resolves into the last inline, even an empty trailing run. An empty list gives (0, 0)
		/// with index equal to Count. Throws when offset is outside 0..Length.
		/// Because the default looks backwards, an offset just after an atom resolves to the atom's end: an edit
		/// op choosing the style for typed text must take it from the nearest run, not the atom.
		/// </summary>
		public static (int InlineIndex, int OffsetInInline) Locate(IReadOnlyList<RichInline> inlines, int offset, bool preferNext = false)
		{
			if (offset < 0 || offset > Length(inlines))
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			int start = 0;
			for (int i = 0; i < inlines.Count; i++)
			{
				int end = start + inlines[i].Length;
				bool inside = preferNext || offset == 0 ? offset < end : offset <= end;
				if (inside)
				{
					return (i, offset - start);
				}

				start = end;
			}

			// Only reached at the very end (preferNext, or trailing empty runs); there is no later inline to prefer.
			if (inlines.Count == 0)
			{
				return (0, 0);
			}

			int last = inlines.Count - 1;
			return (last, inlines[last].Length);
		}

		/// <summary>
		/// The plain-text offset of a position inside one inline: the inverse of <see cref="Locate"/>.
		/// inlineIndex may equal Count (the end) only with offsetInInline 0; anything else out of range throws.
		/// </summary>
		public static int OffsetOf(IReadOnlyList<RichInline> inlines, int inlineIndex, int offsetInInline)
		{
			if (inlineIndex < 0 || inlineIndex > inlines.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(inlineIndex));
			}

			int inlineLength = inlineIndex < inlines.Count ? inlines[inlineIndex].Length : 0;
			if (offsetInInline < 0 || offsetInInline > inlineLength)
			{
				throw new ArgumentOutOfRangeException(nameof(offsetInInline));
			}

			int offset = 0;
			for (int i = 0; i < inlineIndex; i++)
			{
				offset += inlines[i].Length;
			}

			return offset + offsetInInline;
		}

		/// <summary>
		/// Makes <paramref name="offset"/> fall on an inline boundary, splitting the run it lands in when needed,
		/// and returns the index of the first inline at or after it (Count when offset is the end). Edit ops call
		/// this at both ends of a range so they can restyle or remove whole inlines.
		/// </summary>
		public static int SplitAt(List<RichInline> inlines, int offset)
		{
			var (index, inInline) = Locate(inlines, offset, preferNext: true);
			if (index == inlines.Count)
			{
				return index;
			}

			if (inInline == 0)
			{
				return index;
			}

			if (inInline == inlines[index].Length)
			{
				return index + 1;
			}

			// Atoms are length 1, so a strictly interior offset is always inside a run.
			var run = (RichRun)inlines[index];
			var tail = run.WithText(run.Text.Substring(inInline));
			run.Text = run.Text.Substring(0, inInline);
			inlines.Insert(index + 1, tail);
			return index + 1;
		}

		/// <summary>
		/// Joins neighbouring runs of identical style and drops empty runs, so edits never leave fragments that
		/// would write as needless delimiter pairs (**a****b**).
		/// </summary>
		public static void MergeAdjacent(List<RichInline> inlines)
		{
			for (int i = inlines.Count - 1; i >= 0; i--)
			{
				if (inlines[i] is RichRun run && run.Text.Length == 0)
				{
					inlines.RemoveAt(i);
				}
			}

			for (int i = inlines.Count - 1; i > 0; i--)
			{
				if (inlines[i - 1] is RichRun previous
					&& inlines[i] is RichRun current
					&& previous.HasSameStyle(current))
				{
					previous.Text += current.Text;
					inlines.RemoveAt(i);
				}
			}
		}

		public static List<RichInline> Clone(IReadOnlyList<RichInline> inlines)
		{
			var copy = new List<RichInline>(inlines.Count);
			foreach (var inline in inlines)
			{
				copy.Add(inline.Clone());
			}

			return copy;
		}
	}
}
