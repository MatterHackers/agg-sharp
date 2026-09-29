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
	/// <summary>
	/// The CSS <c>font</c> shorthand as svgtypes' FontShorthand reads it and usvg applies it: up to four of style,
	/// variant, weight and stretch ("normal" is skipped, as it could be any of them), then a size (a length or a
	/// keyword such as "large"), an optional "/line-height" that is ignored, and the family list (the rest). It is a
	/// CSS property only; as an XML attribute it means nothing.
	/// </summary>
	internal static class SvgFontShorthand
	{
		private static readonly HashSet<string> SizeKeywords = new HashSet<string> { "xx-small", "x-small", "small", "medium", "large", "x-large", "xx-large", "larger", "smaller" };

		/// <summary>
		/// Sets the font properties <paramref name="value"/> declares into <paramref name="attributes"/>, and resets
		/// the rest to their initial values; a value that does not parse changes nothing.
		/// </summary>
		public static void Apply(Dictionary<string, string> attributes, string value)
		{
			string style = null, variant = null, weight = null, stretch = null;
			int pos = SkipSpaces(value, 0);
			for (int i = 0; i < 4; i++)
			{
				int end = IdentEnd(value, pos);
				string ident = value.Substring(pos, end - pos);
				switch (ident)
				{
					case "normal":
						break;
					case "small-caps":
						variant = ident;
						break;
					case "italic" or "oblique":
						style = ident;
						break;
					case "bold" or "bolder" or "lighter" or "100" or "200" or "300" or "400" or "500" or "600" or "700" or "800" or "900":
						weight = ident;
						break;
					case "ultra-condensed" or "extra-condensed" or "condensed" or "semi-condensed" or "semi-expanded" or "expanded" or "extra-expanded" or "ultra-expanded":
						stretch = ident;
						break;
					default:
						end = -1;
						break;
				}

				if (end < 0)
				{
					break;
				}

				pos = SkipSpaces(value, end);
			}

			if (pos >= value.Length)
			{
				return;
			}

			int sizeStart = pos;
			if (char.IsAsciiDigit(value[pos]))
			{
				pos = LengthEnd(value, pos);
				if (pos < 0)
				{
					return;
				}
			}
			else
			{
				pos = IdentEnd(value, pos);
				if (!SizeKeywords.Contains(value.Substring(sizeStart, pos - sizeStart)))
				{
					return;
				}
			}

			string size = value.Substring(sizeStart, pos - sizeStart);
			pos = SkipSpaces(value, pos);
			if (pos >= value.Length)
			{
				return;
			}

			if (value[pos] == '/')
			{
				pos = SkipSpaces(value, pos + 1);
				pos = LengthEnd(value, pos);
				if (pos < 0)
				{
					return;
				}

				pos = SkipSpaces(value, pos);
			}

			if (pos >= value.Length)
			{
				return;
			}

			attributes["font-style"] = style ?? "normal";
			attributes["font-variant"] = variant ?? "normal";
			attributes["font-weight"] = weight ?? "normal";
			attributes["font-stretch"] = stretch ?? "normal";
			attributes["line-height"] = "normal";
			attributes["font-size-adjust"] = "none";
			attributes["font-kerning"] = "auto";
			foreach (string longhand in new[] { "font-variant-caps", "font-variant-ligatures", "font-variant-numeric", "font-variant-east-asian", "font-variant-position" })
			{
				attributes[longhand] = "normal";
			}

			attributes["font-size"] = size;
			attributes["font-family"] = value.Substring(pos);
		}

		private static int SkipSpaces(string text, int pos)
		{
			while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t' || text[pos] == '\n' || text[pos] == '\r' || text[pos] == '\f'))
			{
				pos++;
			}

			return pos;
		}

		/// <summary>The end of the ASCII identifier (letters, digits, '-' and '_') at <paramref name="pos"/>.</summary>
		private static int IdentEnd(string text, int pos)
		{
			while (pos < text.Length && (char.IsAsciiLetterOrDigit(text[pos]) || text[pos] == '-' || text[pos] == '_'))
			{
				pos++;
			}

			return pos;
		}

		/// <summary>The end of the number and its unit (letters or '%') at <paramref name="pos"/>, or -1 for no number.</summary>
		private static int LengthEnd(string text, int pos)
		{
			int start = pos;
			if (pos < text.Length && (text[pos] == '+' || text[pos] == '-'))
			{
				pos++;
			}

			int digits = pos;
			while (pos < text.Length && char.IsAsciiDigit(text[pos]))
			{
				pos++;
			}

			if (pos < text.Length && text[pos] == '.')
			{
				pos++;
				while (pos < text.Length && char.IsAsciiDigit(text[pos]))
				{
					pos++;
				}
			}

			if (pos == digits || (pos == digits + 1 && text[digits] == '.'))
			{
				return -1;
			}

			if (pos < text.Length && (text[pos] == 'e' || text[pos] == 'E') && pos + 1 < text.Length && (char.IsAsciiDigit(text[pos + 1]) || ((text[pos + 1] == '+' || text[pos + 1] == '-') && pos + 2 < text.Length && char.IsAsciiDigit(text[pos + 2]))))
			{
				pos += 2;
				while (pos < text.Length && char.IsAsciiDigit(text[pos]))
				{
					pos++;
				}
			}

			if (pos < text.Length && text[pos] == '%')
			{
				return pos + 1;
			}

			while (pos < text.Length && char.IsAsciiLetter(text[pos]))
			{
				pos++;
			}

			return pos > start ? pos : -1;
		}
	}
}
