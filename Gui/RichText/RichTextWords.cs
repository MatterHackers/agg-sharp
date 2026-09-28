/*
Copyright (c) 2026, Lars Brubaker
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
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>
	/// Word and paragraph boundaries for rich-text editing (agg-gui's word_target, word_range_at_pos and
	/// block_range_at_pos), shared by Ctrl/Alt+arrow motion, word delete and double/triple-click selection so they
	/// all agree on what a word is: letters, digits and '_'.
	/// </summary>
	public static class RichTextWords
	{
		public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

		/// <summary>
		/// One word left (<paramref name="direction"/> &lt; 0) or right from <paramref name="pos"/>: left lands on the
		/// start of the previous word, right past the next word and its trailing gap. At a paragraph edge it crosses
		/// into the neighbouring paragraph.
		/// </summary>
		public static DocPos WordTarget(RichDoc doc, DocPos pos, int direction)
		{
			string text = doc.Blocks[pos.Block].Text;
			int i = pos.Offset;
			if (direction < 0)
			{
				if (i == 0)
				{
					return pos.Block > 0 ? new DocPos(pos.Block - 1, doc.Blocks[pos.Block - 1].TextLength) : pos;
				}

				while (i > 0 && !IsWordChar(text[i - 1]))
				{
					i--;
				}

				while (i > 0 && IsWordChar(text[i - 1]))
				{
					i--;
				}

				return new DocPos(pos.Block, i);
			}

			if (i >= text.Length)
			{
				return pos.Block + 1 < doc.Blocks.Count ? new DocPos(pos.Block + 1, 0) : pos;
			}

			while (i < text.Length && IsWordChar(text[i]))
			{
				i++;
			}

			while (i < text.Length && !IsWordChar(text[i]))
			{
				i++;
			}

			return new DocPos(pos.Block, i);
		}

		/// <summary>
		/// The run of same-class characters (word or gap) under <paramref name="pos"/>, within its paragraph - what a
		/// double-click selects. Past the paragraph end it takes the trailing gap, if any.
		/// </summary>
		public static DocRange WordRange(RichDoc doc, DocPos pos)
		{
			string text = doc.Blocks[pos.Block].Text;
			int at = System.Math.Min(pos.Offset, text.Length);
			bool word = at < text.Length && IsWordChar(text[at]);
			int start = at;
			while (start > 0 && IsWordChar(text[start - 1]) == word)
			{
				start--;
			}

			int end = at;
			while (end < text.Length && IsWordChar(text[end]) == word)
			{
				end++;
			}

			return new DocRange(new DocPos(pos.Block, start), new DocPos(pos.Block, end));
		}

		/// <summary>The whole paragraph holding <paramref name="pos"/> - what a triple-click selects.</summary>
		public static DocRange BlockRange(RichDoc doc, DocPos pos) => new DocRange(new DocPos(pos.Block, 0), new DocPos(pos.Block, doc.Blocks[pos.Block].TextLength));
	}
}
