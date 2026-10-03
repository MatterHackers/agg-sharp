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

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Where the caret of a plain text field goes for the word and line keys: word jumps, double-click word
	/// selection, start of line and the extent of the line holding a character. Pure functions of the text, so the
	/// field and the rich-text caret share one notion of a word.
	/// </summary>
	internal static class TextCaretNavigation
	{
		private static readonly HashSet<char> WordBreakChars = new HashSet<char>(new char[] 
		{ 
			' ', '\t', // white space characters
			'\'', '"', '`', // quotes
			',', '.', '?', '!', '@', '&', // punctuation
			'(', ')', '<', '>', '[', ']', '{', '}', // parents (or equivalent)
			'-', '+', '*', '/', '=', '\\', '#', '$', '^', '|', '°', '²', '³'// math symbols
		});

		private static readonly HashSet<char> WordBreakCharsAndCR = new HashSet<char>(WordBreakChars) { '\n' };

		/// <summary>Whether <paramref name="c"/> ends a word for double-click selection and word jumps.</summary>
		public static bool IsWordBreakOrNewLine(char c) => WordBreakCharsAndCR.Contains(c);

		public static int IndexOfNextToken(string text, int cursor)
		{
			var insert = cursor;
			var length = text.Length;
			if (insert == text.Length)
			{
				// If we are already at the end, return.
				return text.Length;
			}

			// if we are starting an a CR
			if (text[insert] == '\n')
			{
				// If we are on a CR advance one (goto next line)
				insert++;
				// and skip ' ' and '\t'
				while (insert < length 
					&& (text[insert] == ' ' || text[insert] == '\t'))
				{
					insert++;
				}

				return insert;
			}
			else if (WordBreakChars.Contains(text[insert]))
			{
				// we are starting on a work break char
				// while we are on the same char advance
				var current = text[insert];
				while (insert < length && text[insert]  == current)
				{
					insert++;
				}
			}
			else
			{
				// we are starting on a normal character
				while (insert < length && !WordBreakCharsAndCR.Contains(text[insert]))
				{
					insert++;
				}

				// and also skip ' ' and '\t'
				while (insert < length
					&& (text[insert] == ' ' || text[insert] == '\t'))
				{
					insert++;
				}
			}

			return insert;
		}

		public static int IndexOfPreviousToken(string text, int cursor)
		{
			if (cursor == 0)
			{
				return 0;
			}

			int prevToken = Math.Max(0, Math.Min(text.Length - 1, cursor - 1));
			var token = text[prevToken];

			if (text[prevToken] == '\n')
			{
				if (prevToken > 0
					&& text[prevToken - 1] == '\n')
				{
					return prevToken;
				}

				prevToken--;
			}
			else if (token == ' ' || token == '\t')
			{
				// the token to the left is a breaking character
				while (--prevToken >= 0
					&& (text[prevToken] == ' ' || text[prevToken] == '\t'))
				{
					// skip back the entire token
				}
			}
			else if (WordBreakChars.Contains(token))
			{
				// the token to the left is a breaking character
				while (--prevToken >= 0 && text[prevToken] == token)
				{
					// skip back the entire token
				}

				return prevToken + 1;
			}

			// the token to the left is normal character skip until a break
			while (prevToken >= 0 && !WordBreakCharsAndCR.Contains(text[prevToken]))
			{
				// skip back until we are on a word break
				prevToken--;
			}

			return prevToken + 1;
		}

        // the '\n' is always considered to be the end of the line.
        // if startIndexInclusive == endIndexInclusive, the line is empty (other than the return)
        public static void GetLineExtents(string actualText, int charToFindLineContaining, out int startIndexOfLineInclusive, out int endIndexOfLineInclusive)
        {
            startIndexOfLineInclusive = 0;
            endIndexOfLineInclusive = actualText.Length;
            if (endIndexOfLineInclusive == 0)
            {
                return;
            }

            charToFindLineContaining = Math.Max(Math.Min(charToFindLineContaining, actualText.Length), 0);

            if (charToFindLineContaining == actualText.Length
                || actualText[charToFindLineContaining] == '\n')
            {
                endIndexOfLineInclusive = charToFindLineContaining;
            }
            else
            {
                int endReturn = actualText.IndexOf('\n', charToFindLineContaining + 1);
                if (endReturn != -1)
                {
                    endIndexOfLineInclusive = endReturn;
                }
            }

            bool isIndex0AndNL = endIndexOfLineInclusive == 0 && actualText[endIndexOfLineInclusive] == '\n';
            if (isIndex0AndNL || actualText[endIndexOfLineInclusive - 1] == '\n')
            {
                startIndexOfLineInclusive = endIndexOfLineInclusive;
            }
            else
            {
                int returnAtStartOfCurrentLine = actualText.LastIndexOf('\n', endIndexOfLineInclusive - 1);
                if (returnAtStartOfCurrentLine != -1)
                {
                    startIndexOfLineInclusive = returnAtStartOfCurrentLine + 1;
                }
            }
        }

        public static int StartOfCurrentLine(string text, int cursor)
		{
			if (cursor > 0)
			{
				int indexOfReturn = text.LastIndexOf('\n', cursor - 1);
				if (indexOfReturn == -1)
				{
					return 0;
				}
				else
				{
					var firstNonWhiteSpaceRegex = new Regex("[^\\t ]");
					Match firstNonWhiteSpace = firstNonWhiteSpaceRegex.Match(text, indexOfReturn + 1);
					if (firstNonWhiteSpace.Success)
					{
						if (firstNonWhiteSpace.Index < cursor
						   || text[cursor - 1] == '\n')
						{
							return firstNonWhiteSpace.Index;
						}
					}

					return indexOfReturn + 1;
				}
			}

			return 0;
		}
	}
}
