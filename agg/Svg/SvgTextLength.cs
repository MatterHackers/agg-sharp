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
*/

using System.Collections.Generic;

namespace MatterHackers.Agg.Svg
{
	/// <summary>textLength with lengthAdjust="spacing", as usvg 0.45.1's apply_length_adjust lays it.</summary>
	internal static class SvgTextLength
	{
		/// <summary>The textLength an element sets on its own characters (it is not inherited), or null; a negative one is ignored.</summary>
		public static double? Read(SvgElement element, double viewportDiagonal, double fontSize)
		{
			double length = SvgLength.Parse(element["textLength"], double.NaN, viewportDiagonal, fontSize);
			return double.IsNaN(length) || length < 0 ? null : length;
		}

		/// <summary>
		/// Each span of a chunk with a textLength spreads its glyphs so their advances sum to it: every glyph's own
		/// width (kerned, without letter- or word-spacing) plus an even share of what is left over, the last glyph
		/// included. One glyph alone keeps its width. spacingAndGlyphs (a horizontal squash) is not done.
		/// </summary>
		public static void Apply(IReadOnlyList<SvgText.Character> characters, int start, int end)
		{
			for (int first = start; first < end;)
			{
				int last = first + 1;
				while (last < end && characters[last].Element == characters[first].Element)
				{
					last++;
				}

				SvgText.Character head = characters[first];
				if (head.TextLength is double target && !head.SpacingAndGlyphs)
				{
					double width = 0;
					for (int i = first; i < last; i++)
					{
						width += characters[i].Width;
					}

					double share = last - first > 1 ? (target - width) / (last - first - 1) : 0;
					for (int i = first; i < last; i++)
					{
						characters[i].Advance = characters[i].Width + share;
					}
				}

				first = last;
			}
		}
	}
}
