/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	/// <summary>What a highlighted run of source is; everything not in a <see cref="SyntaxSpan"/> is plain text.</summary>
	public enum SyntaxTokenKind
	{
		Plain,
		Keyword,
		String,
		Comment,
		Number,
	}

	/// <summary>One highlighted run of a line: <see cref="Length"/> UTF-16 chars from <see cref="Start"/>.</summary>
	public readonly struct SyntaxSpan
	{
		public SyntaxSpan(int start, int length, SyntaxTokenKind kind)
		{
			Start = start;
			Length = length;
			Kind = kind;
		}

		public int Start { get; }

		public int Length { get; }

		public int End => Start + Length;

		public SyntaxTokenKind Kind { get; }

		public override string ToString() => $"{Kind} [{Start}, {End})";
	}

	/// <summary>The keyword set and literal rules the <see cref="SyntaxHighlighter"/> tokenizes one language with.</summary>
	public sealed class SyntaxLanguage
	{
		/// <summary>agg-gui's code_editor_demo.rs keyword list.</summary>
		public static SyntaxLanguage Rust { get; } = new SyntaxLanguage("Rust", charLiterals: false, verbatimStrings: false, new[]
		{
			"as", "async", "await", "break", "const", "continue", "crate", "dyn", "else", "enum", "extern",
			"false", "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod", "move", "mut", "pub",
			"ref", "return", "self", "Self", "static", "struct", "super", "trait", "true", "type",
			"unsafe", "use", "where", "while",
		});

		/// <summary>C#'s reserved keywords plus the contextual ones code most often shows (var, async, ...).</summary>
		public static SyntaxLanguage CSharp { get; } = new SyntaxLanguage("C#", charLiterals: true, verbatimStrings: true, new[]
		{
			"abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
			"class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
			"explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "get", "goto", "if",
			"implicit", "in", "init", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
			"null", "object", "operator", "out", "override", "params", "partial", "private", "protected", "public",
			"readonly", "record", "ref", "return", "sbyte", "sealed", "set", "short", "sizeof", "stackalloc",
			"static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
			"unchecked", "unsafe", "ushort", "using", "value", "var", "virtual", "void", "volatile", "when", "where",
			"while", "yield",
		});

		public SyntaxLanguage(string name, bool charLiterals, bool verbatimStrings, IEnumerable<string> keywords)
		{
			Name = name;
			CharLiterals = charLiterals;
			VerbatimStrings = verbatimStrings;
			Keywords = new HashSet<string>(keywords);
		}

		public string Name { get; }

		/// <summary>Whether '...' is a literal. Off for Rust, whose lifetimes ('a) would open one that never closes.</summary>
		public bool CharLiterals { get; }

		/// <summary>Whether @"..." is a verbatim string: backslashes are literal and "" is an escaped quote.</summary>
		public bool VerbatimStrings { get; }

		public IReadOnlyCollection<string> Keywords { get; }

		public bool IsKeyword(string word) => ((HashSet<string>)Keywords).Contains(word);
	}

	/// <summary>
	/// A line-at-a-time source tokenizer, agg-gui's rust_highlighter (demo-ui code_editor_demo.rs): line comments,
	/// string (and, per language, char and verbatim) literals, numbers and keywords. Every token is resolvable
	/// without state from earlier lines, so an editor re-highlights only the lines it draws.
	/// </summary>
	/// <remarks>
	/// Block comments and strings that span lines are not tracked - they need cross-line state - so a line inside
	/// a /* */ block highlights as ordinary code.
	/// </remarks>
	public static class SyntaxHighlighter
	{
		/// <summary>The highlighted runs of <paramref name="line"/>, in order and not overlapping. Plain text
		/// (identifiers that are not keywords, punctuation, whitespace) gets no span.</summary>
		public static List<SyntaxSpan> HighlightLine(string line, SyntaxLanguage language)
		{
			var spans = new List<SyntaxSpan>();
			int length = line.Length;
			int i = 0;
			while (i < length)
			{
				char c = line[i];

				// A line comment colours the rest of the line.
				if (c == '/' && i + 1 < length && line[i + 1] == '/')
				{
					spans.Add(new SyntaxSpan(i, length - i, SyntaxTokenKind.Comment));
					break;
				}

				if (language.VerbatimStrings && c == '@' && i + 1 < length && line[i + 1] == '"')
				{
					int start = i;
					i = EndOfVerbatimString(line, i + 2);
					spans.Add(new SyntaxSpan(start, i - start, SyntaxTokenKind.String));
					continue;
				}

				if (c == '"' || (c == '\'' && language.CharLiterals))
				{
					int start = i;
					i = EndOfQuoted(line, i + 1, c);
					spans.Add(new SyntaxSpan(start, i - start, SyntaxTokenKind.String));
					continue;
				}

				// A digit-led run of letters, digits, '.' and '_' (1.5, 0xFF, 1_000u32).
				if (char.IsDigit(c))
				{
					int start = i;
					while (i < length && (char.IsLetterOrDigit(line[i]) || line[i] == '.' || line[i] == '_'))
					{
						i++;
					}

					spans.Add(new SyntaxSpan(start, i - start, SyntaxTokenKind.Number));
					continue;
				}

				if (char.IsLetter(c) || c == '_')
				{
					int start = i;
					while (i < length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
					{
						i++;
					}

					if (language.IsKeyword(line.Substring(start, i - start)))
					{
						spans.Add(new SyntaxSpan(start, i - start, SyntaxTokenKind.Keyword));
					}

					continue;
				}

				// Step over a surrogate pair whole, so no span can start or end inside one.
				i += char.IsHighSurrogate(c) && i + 1 < length && char.IsLowSurrogate(line[i + 1]) ? 2 : 1;
			}

			return spans;
		}

		/// <summary>The index just past the closing <paramref name="quote"/>, or the line's end if it never closes.
		/// A backslash escapes the char after it.</summary>
		private static int EndOfQuoted(string line, int i, char quote)
		{
			while (i < line.Length)
			{
				char c = line[i++];
				if (c == '\\')
				{
					i = System.Math.Min(i + 1, line.Length);
				}
				else if (c == quote)
				{
					break;
				}
			}

			return i;
		}

		private static int EndOfVerbatimString(string line, int i)
		{
			while (i < line.Length)
			{
				if (line[i] == '"')
				{
					if (i + 1 < line.Length && line[i + 1] == '"')
					{
						i += 2;
						continue;
					}

					return i + 1;
				}

				i++;
			}

			return i;
		}
	}
}
