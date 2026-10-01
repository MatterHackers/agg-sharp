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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// How <see cref="RichInlineWriter"/> writes what sits inside its delimiters: escaped plain text, code spans
	/// and link tails. Runs hold decoded text, so everything here re-encodes it to read back unchanged.
	/// </summary>
	internal static class RichMarkdownEscaping
	{
		// An entity reference CommonMark would decode; a literal '&' only needs escaping in front of one.
		private static readonly Regex EntityStart = new Regex(@"^&(#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6}|[A-Za-z][A-Za-z0-9]{1,31});");

		// An ordered-list marker at the start of a line: its delimiter is escaped so the text stays a paragraph.
		private static readonly Regex OrderedMarker = new Regex(@"^[0-9]{1,9}[.)]($|[ \t])");

		/// <summary>
		/// The part after a link's text: "](url "title")", or "][label]" for a reference link, whose url and
		/// title stay on its definition.
		/// </summary>
		internal static string LinkTail(string linkUrl, string linkTitle, string linkLabel)
		{
			if (linkLabel != null)
			{
				return "][" + linkLabel + "]";
			}

			var tail = new StringBuilder("](");
			tail.Append(Destination(linkUrl ?? ""));
			if (linkTitle != null)
			{
				tail.Append(" \"");
				string title = linkTitle;
				for (int i = 0; i < title.Length; i++)
				{
					char c = title[i];
					if (c == '\r' || c == '\n')
					{
						// A title cannot hold a blank line; a space keeps it one title.
						tail.Append(' ');
						continue;
					}

					if (c == '&' && EntityStart.IsMatch(title.Substring(i)))
					{
						tail.Append("&amp;");
						continue;
					}

					if (c == '"' || c == '\\')
					{
						tail.Append('\\');
					}

					tail.Append(c);
				}

				tail.Append('"');
			}

			return tail.Append(')').ToString();
		}

		/// <summary>
		/// A link destination as markdown. The url is decoded (escapes and entities resolved), so backslashes and
		/// entity-like text are escaped again; spaces, parentheses or an empty url need the &lt;...&gt; form.
		/// </summary>
		internal static string Destination(string url)
		{
			bool pointy = url.Length == 0 || url.Any(c => c == ' ' || c == '(' || c == ')' || c == '<' || (char.IsControl(c) && c != '\r' && c != '\n'));
			var destination = new StringBuilder(pointy ? "<" : "");
			for (int i = 0; i < url.Length; i++)
			{
				char c = url[i];
				if (c == '\r' || c == '\n')
				{
					// A destination cannot span lines; percent-encoding keeps the same url.
					destination.Append(c == '\r' ? "%0D" : "%0A");
					continue;
				}

				if (c == '&' && EntityStart.IsMatch(url.Substring(i)))
				{
					destination.Append("&amp;");
					continue;
				}

				bool escape = c == '\\' || (pointy && (c == '<' || c == '>'));
				if (escape)
				{
					destination.Append('\\');
				}

				destination.Append(c);
			}

			return destination.Append(pointy ? ">" : "").ToString();
		}

		/// <summary>
		/// A code span. A span closes at the first backtick run of its own length, so the fence differs in length
		/// from every run inside; CommonMark strips one space from each side when both ends have one, and a
		/// leading or trailing backtick would join the fence, so those cases get one space of padding.
		/// </summary>
		internal static void AppendCode(string code, StringBuilder markdown)
		{
			// An empty code span cannot be written; it renders as nothing anyway.
			if (code.Length == 0)
			{
				return;
			}

			var runs = new HashSet<int>();
			for (int i = 0; i < code.Length;)
			{
				int start = i;
				while (i < code.Length && code[i] == '`')
				{
					i++;
				}

				if (i > start)
				{
					runs.Add(i - start);
				}
				else
				{
					i++;
				}
			}

			int fenceLength = 1;
			while (runs.Contains(fenceLength))
			{
				fenceLength++;
			}

			string fence = new string('`', fenceLength);
			bool allSpaces = code.All(c => c == ' ');
			bool pad = code[0] == '`' || code[^1] == '`' || (!allSpaces && code[0] == ' ' && code[^1] == ' ');
			string padding = pad ? " " : "";
			markdown.Append(fence).Append(padding).Append(code).Append(padding).Append(fence);
		}

		/// <summary>
		/// Appends plain text so it reads back as exactly that text: characters that could start markdown syntax
		/// are backslash-escaped where they would (not everywhere, so ordinary prose stays readable). Runs hold
		/// decoded text, so without this "&lt;b&gt;" typed as text would become live HTML.
		/// </summary>
		internal static void AppendEscaped(string text, StringBuilder markdown, bool tableCell)
		{
			if (markdown.Length == 0 || markdown[^1] == '\n')
			{
				// Leading spaces never render, and four of them would make the line an indented code block.
				text = text.TrimStart(' ', '\t');
				if (text.Length == 0)
				{
					return;
				}

				text = AppendLineStart(text, markdown);
			}

			for (int i = 0; i < text.Length; i++)
			{
				char previous = markdown.Length > 0 ? markdown[^1] : '\n';
				char next = i + 1 < text.Length ? text[i + 1] : '\0';
				if (NeedsEscape(text, i, previous, next) || (tableCell && text[i] == '|'))
				{
					markdown.Append('\\');
				}

				markdown.Append(text[i]);
			}
		}

		/// <summary>
		/// Escapes what only matters as a line's first characters (headings, quotes, list markers, setext
		/// underlines) and returns the rest of the text.
		/// </summary>
		private static string AppendLineStart(string text, StringBuilder markdown)
		{
			char first = text[0];
			if (first == '#' || first == '>' || first == '-' || first == '+' || first == '=')
			{
				markdown.Append('\\').Append(first);
				return text.Substring(1);
			}

			var ordered = OrderedMarker.Match(text);
			if (ordered.Success)
			{
				int delimiter = ordered.Value.TrimEnd().Length - 1;
				markdown.Append(text, 0, delimiter).Append('\\').Append(text[delimiter]);
				return text.Substring(delimiter + 1);
			}

			return text;
		}

		private static bool NeedsEscape(string text, int i, char previous, char next)
		{
			switch (text[i])
			{
				case '\\':
				case '*':
				case '`':
				case '[':
				case ']':
				// The emphasis extras: ~sub~ and ^sup^ need only one character each.
				case '~':
				case '^':
					return true;

				case '_':
					// '_' never makes emphasis inside a word, so snake_case stays readable.
					return !char.IsLetterOrDigit(previous) || !char.IsLetterOrDigit(next);

				case '<':
					// Only these can start inline HTML or an autolink.
					return next == '\0' || char.IsLetter(next) || next == '/' || next == '!' || next == '?';

				case '&':
					return EntityStart.IsMatch(text.Substring(i));

				case '=':
				case '+':
					// ==marked== and ++inserted++.
					return previous == text[i] || next == text[i];

				case ':':
					// The AutoLinks extension would turn http://... and mailto:... into links.
					return (next == '/' && i + 2 < text.Length && text[i + 2] == '/') || EndsWithWord(text, i, "mailto");

				case '.':
					return EndsWithWord(text, i, "www");

				default:
					return false;
			}
		}

		/// <summary>
		/// True when the text just before <paramref name="i"/> is <paramref name="word"/> at the start of a word.
		/// </summary>
		private static bool EndsWithWord(string text, int i, string word)
		{
			int start = i - word.Length;
			return start >= 0
				&& string.CompareOrdinal(text, start, word, 0, word.Length) == 0
				&& (start == 0 || !char.IsLetterOrDigit(text[start - 1]));
		}
	}
}
