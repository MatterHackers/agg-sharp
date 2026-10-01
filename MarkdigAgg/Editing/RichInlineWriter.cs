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
using System.Linq;
using System.Text;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Where a list of inlines is written, which changes what its markdown may contain.
	/// </summary>
	public enum RichInlineContext
	{
		Paragraph,

		/// <summary>
		/// A heading is one line, so a hard break writes as a space.
		/// </summary>
		Heading,

		/// <summary>
		/// A pipe-table cell: one line like a heading, and a '|' in plain text is escaped so it does not end the cell.
		/// </summary>
		TableCell,
	}

	/// <summary>
	/// Writes a list of inlines (a paragraph's, a heading's, a table cell's) as markdown that reads back to the
	/// same inlines: styles become the shortest properly nested delimiters, plain text is escaped, atoms are
	/// written verbatim inside their own style. Every edited text block goes through here, so a rewrite never
	/// changes what renders.
	/// </summary>
	public static class RichInlineWriter
	{
		private static readonly Span BoldSpan = new Span(SpanKind.Bold);
		private static readonly Span ItalicSpan = new Span(SpanKind.Italic);
		private static readonly Span StrikeSpan = new Span(SpanKind.Strike);

		private enum SpanKind
		{
			// Declaration order breaks ties when spans open together: earlier kinds go outside.
			Link,
			Strike,
			Bold,
			Italic,
		}

		private enum TokenKind
		{
			Open,
			Close,
			Text,
			Code,
			Atom,
		}

		/// <summary>
		/// Appends the markdown for the inlines to <paramref name="markdown"/>, which holds whatever precedes them
		/// on the line (or nothing): line-start escaping is decided from it.
		/// </summary>
		public static void Write(IReadOnlyList<RichInline> inlines, StringBuilder markdown, RichInlineContext context = RichInlineContext.Paragraph)
		{
			Write(inlines, markdown, context, out _, out _);
		}

		public static string Write(IReadOnlyList<RichInline> inlines, RichInlineContext context = RichInlineContext.Paragraph)
		{
			var markdown = new StringBuilder();
			Write(inlines, markdown, context);
			return markdown.ToString();
		}

		/// <summary>
		/// <see cref="Write(IReadOnlyList{RichInline}, StringBuilder, RichInlineContext)"/>, also reporting how many
		/// tokens the whitespace hoisting started with and how many steps its scan took, so a test can hold the scan
		/// linear by count rather than by the clock.
		/// </summary>
		internal static void Write(IReadOnlyList<RichInline> inlines, StringBuilder markdown, RichInlineContext context, out int tokenCount, out int hoistSteps)
		{
			var written = Prepare(inlines, context);
			var tokens = Tokenize(written.ConvertAll(inline => new Segment(inline, SpansOf(inline))));
			tokenCount = tokens.Count;
			hoistSteps = HoistWhitespace(tokens);

			string prefix = markdown.ToString();
			bool tableCell = context == RichInlineContext.TableCell;

			// Each pass turns the spans whose delimiters would not read as emphasis into HTML tags; a tag changes
			// its neighbours' flanking, so repeat until nothing changes (each pass converts at least one span).
			string output = Serialize(tokens, prefix, tableCell, out var ranges);
			while (MarkUnflanked(tokens, ranges, output))
			{
				output = Serialize(tokens, prefix, tableCell, out ranges);
			}

			// Flanking is not the whole of CommonMark's delimiter matching (the rule of three, runs that both
			// open and close); if the result still reads back with different styles, every span uses tags.
			if (!ReadsBackAs(output.Substring(prefix.Length), written))
			{
				foreach (var token in tokens.Where(token => token.Use != null))
				{
					token.Use.Html = true;
				}

				output = Serialize(tokens, prefix, tableCell, out _);
			}

			markdown.Append(output, prefix.Length, output.Length - prefix.Length);
		}

		/// <summary>
		/// A span a delimiter pair opens and closes. Links are spans too, so emphasis and links nest properly
		/// around each other; a link's identity is its url, title and label together.
		/// </summary>
		private record Span(SpanKind Kind, string LinkUrl = null, string LinkTitle = null, string LinkLabel = null);

		/// <summary>
		/// One inline with the spans it sits in.
		/// </summary>
		private record Segment(RichInline Inline, List<Span> Spans);

		/// <summary>
		/// One opened span, shared by its Open and Close tokens so both write the same form.
		/// </summary>
		private class SpanUse
		{
			public bool Html;
		}

		private class Token
		{
			public TokenKind Kind;
			public Span Span;
			public string Text;
			public SpanUse Use;
		}

		/// <summary>
		/// The inlines as they will be written: merged so adjacent same-style runs never write as **a****b**,
		/// and with a heading's or table cell's hard breaks made spaces.
		/// </summary>
		private static List<RichInline> Prepare(IReadOnlyList<RichInline> source, RichInlineContext context)
		{
			var inlines = RichInlines.Clone(source);
			if (context != RichInlineContext.Paragraph)
			{
				for (int i = 0; i < inlines.Count; i++)
				{
					if (inlines[i] is InlineAtom { Kind: InlineAtomKind.HardBreak } atom)
					{
						inlines[i] = new RichRun(" ")
						{
							Bold = atom.Bold,
							Italic = atom.Italic,
							Strike = atom.Strike,
							LinkUrl = atom.LinkUrl,
							LinkTitle = atom.LinkTitle,
							LinkLabel = atom.LinkLabel,
						};
					}
				}
			}

			// A hard break with nothing after it (Shift+Enter at a paragraph's end, not yet typed past) means nothing
			// in markdown: written, its backslash would read back as literal text.
			while (inlines.Count > 0 && inlines[^1] is InlineAtom { Kind: InlineAtomKind.HardBreak })
			{
				inlines.RemoveAt(inlines.Count - 1);
			}

			RichInlines.MergeAdjacent(inlines);
			return inlines;
		}

		private static List<Span> SpansOf(RichInline inline)
		{
			var spans = new List<Span>();
			if (inline.LinkUrl != null || inline.LinkLabel != null)
			{
				spans.Add(new Span(SpanKind.Link, inline.LinkUrl, inline.LinkTitle, inline.LinkLabel));
			}

			if (inline.Strike)
			{
				spans.Add(StrikeSpan);
			}

			if (inline.Bold)
			{
				spans.Add(BoldSpan);
			}

			if (inline.Italic)
			{
				spans.Add(ItalicSpan);
			}

			return spans;
		}

		/// <summary>
		/// Opens and closes spans with a stack so delimiters always nest. A span that ends while spans opened
		/// inside it continue forces those to close and reopen; opening the longest-lasting span outermost keeps
		/// that rare.
		/// </summary>
		private static List<Token> Tokenize(List<Segment> segments)
		{
			var tokens = new List<Token>();
			var stack = new List<(Span Span, SpanUse Use)>();
			for (int i = 0; i < segments.Count; i++)
			{
				var wanted = segments[i].Spans;

				// Close down to the deepest span this segment is not in; spans above it that continue reopen below.
				int deepest = stack.FindIndex(open => !wanted.Contains(open.Span));
				if (deepest >= 0)
				{
					for (int s = stack.Count - 1; s >= deepest; s--)
					{
						tokens.Add(new Token { Kind = TokenKind.Close, Span = stack[s].Span, Use = stack[s].Use });
					}

					stack.RemoveRange(deepest, stack.Count - deepest);
				}

				var toOpen = wanted.Where(span => !stack.Any(open => open.Span == span))
					.OrderByDescending(span => Persistence(segments, i, span))
					.ThenBy(span => span.Kind)
					.ToList();
				foreach (var span in toOpen)
				{
					var use = span.Kind == SpanKind.Link ? null : new SpanUse();
					tokens.Add(new Token { Kind = TokenKind.Open, Span = span, Use = use });
					stack.Add((span, use));
				}

				tokens.Add(segments[i].Inline switch
				{
					RichRun run when run.Code => new Token { Kind = TokenKind.Code, Text = run.Text },
					RichRun run => new Token { Kind = TokenKind.Text, Text = run.Text },
					var atom => new Token { Kind = TokenKind.Atom, Text = ((InlineAtom)atom).RawMarkdown },
				});
			}

			for (int s = stack.Count - 1; s >= 0; s--)
			{
				tokens.Add(new Token { Kind = TokenKind.Close, Span = stack[s].Span, Use = stack[s].Use });
			}

			return tokens;
		}

		private static int Persistence(List<Segment> segments, int start, Span span)
		{
			int end = start;
			while (end < segments.Count && segments[end].Spans.Contains(span))
			{
				end++;
			}

			return end - start;
		}

		/// <summary>
		/// CommonMark only reads a delimiter as emphasis when it hugs non-space text ("** a**" is literal), so
		/// whitespace at a styled span's edge moves outside its delimiters. Link brackets have no such rule, and
		/// moving a space across one would change the link's text, so whitespace stops at a link.
		/// </summary>
		/// <returns>How many steps the scan took.</returns>
		private static int HoistWhitespace(List<Token> tokens)
		{
			// Each change only alters what the rules see at its own token and the one before it (a removal or a
			// hoisted space can newly expose an empty pair, an empty text or an edge space to the token before),
			// so the scan steps back one token and carries on: a linear scan with no rescans from the start, and
			// the same rules fire in the same order. (Each list insert or removal still shifts the tokens after it.)
			int steps = 0;
			int i = 0;
			while (i < tokens.Count)
			{
				steps++;
				bool changed = false;
				var token = tokens[i];
				bool emphasis = token.Span != null && token.Span.Kind != SpanKind.Link;
				if (token.Kind == TokenKind.Text && token.Text.Length == 0)
				{
					tokens.RemoveAt(i);
					changed = true;
				}
				else if (token.Kind == TokenKind.Open && i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Close && tokens[i + 1].Span == token.Span)
				{
					// A pair left around nothing (all its text was whitespace) disappears.
					tokens.RemoveRange(i, 2);
					changed = true;
				}
				else if (emphasis && token.Kind == TokenKind.Open && i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Text)
				{
					var next = tokens[i + 1];
					int lead = next.Text.Length - next.Text.TrimStart().Length;
					if (lead > 0)
					{
						tokens.Insert(i, new Token { Kind = TokenKind.Text, Text = next.Text.Substring(0, lead) });
						next.Text = next.Text.Substring(lead);
						changed = true;
					}
				}
				else if (emphasis && token.Kind == TokenKind.Close && i > 0 && tokens[i - 1].Kind == TokenKind.Text)
				{
					var previous = tokens[i - 1];
					string trimmed = previous.Text.TrimEnd();
					if (trimmed.Length < previous.Text.Length)
					{
						tokens.Insert(i + 1, new Token { Kind = TokenKind.Text, Text = previous.Text.Substring(trimmed.Length) });
						previous.Text = trimmed;
						changed = true;
					}
				}

				i = changed ? Math.Max(0, i - 1) : i + 1;
			}

			return steps;
		}

		/// <summary>
		/// The markdown for the tokens after <paramref name="prefix"/>, with where each token landed.
		/// </summary>
		private static string Serialize(List<Token> tokens, string prefix, bool tableCell, out List<(int Start, int End)> ranges)
		{
			var markdown = new StringBuilder(prefix);
			ranges = new List<(int Start, int End)>(tokens.Count);
			foreach (var token in tokens)
			{
				int start = markdown.Length;
				switch (token.Kind)
				{
					case TokenKind.Open when token.Span.Kind == SpanKind.Link:
						// "![" would turn the link into an image.
						if (EndsWithUnescaped(markdown, '!') && markdown.Length > prefix.Length)
						{
							markdown.Insert(markdown.Length - 1, '\\');
							if (ranges.Count > 0 && ranges[^1].End == start)
							{
								ranges[^1] = (ranges[^1].Start, start + 1);
							}

							start++;
						}

						markdown.Append('[');
						break;

					case TokenKind.Close when token.Span.Kind == SpanKind.Link:
						markdown.Append(RichMarkdownEscaping.LinkTail(token.Span.LinkUrl, token.Span.LinkTitle, token.Span.LinkLabel));
						break;

					case TokenKind.Open:
					case TokenKind.Close:
						markdown.Append(Delimiter(token.Span.Kind, token.Use.Html, token.Kind == TokenKind.Close));
						break;

					case TokenKind.Text:
						RichMarkdownEscaping.AppendEscaped(token.Text, markdown, tableCell);
						break;

					case TokenKind.Code:
						RichMarkdownEscaping.AppendCode(token.Text, markdown);
						break;

					case TokenKind.Atom:
						markdown.Append(token.Text);
						break;
				}

				ranges.Add((start, markdown.Length));
			}

			return markdown.ToString();
		}

		// '*' rather than '_' because only '*' makes emphasis inside a word (a*b*c). The tags are the fallback
		// for spans whose delimiters would not read as emphasis; the parser reads them back as the same styles.
		private static string Delimiter(SpanKind kind, bool html, bool close)
		{
			if (!html)
			{
				return kind switch
				{
					SpanKind.Bold => "**",
					SpanKind.Italic => "*",
					_ => "~~",
				};
			}

			string tag = kind switch
			{
				SpanKind.Bold => "strong",
				SpanKind.Italic => "em",
				_ => "del",
			};
			return close ? "</" + tag + ">" : "<" + tag + ">";
		}

		/// <summary>
		/// Applies CommonMark's flanking rules to every delimiter run (adjacent delimiters of one character read
		/// as one run): an opener must be left-flanking and a closer right-flanking, or the delimiters are literal
		/// (a**"b"**c, **`b`**c). Spans that fail switch to HTML tags. Returns true when any span switched.
		/// </summary>
		private static bool MarkUnflanked(List<Token> tokens, List<(int Start, int End)> ranges, string output)
		{
			bool changed = false;
			for (int i = 0; i < tokens.Count;)
			{
				if (!IsMarkdownDelimiter(tokens[i]))
				{
					i++;
					continue;
				}

				char delimiter = Delimiter(tokens[i].Span.Kind, false, false)[0];
				int last = i;
				while (last + 1 < tokens.Count
					&& IsMarkdownDelimiter(tokens[last + 1])
					&& Delimiter(tokens[last + 1].Span.Kind, false, false)[0] == delimiter
					&& ranges[last + 1].Start == ranges[last].End)
				{
					last++;
				}

				int start = ranges[i].Start;
				int end = ranges[last].End;
				char before = start > 0 ? output[start - 1] : '\n';
				char after = end < output.Length ? output[end] : '\n';
				bool leftFlanking = !char.IsWhiteSpace(after) && (!IsPunctuation(after) || char.IsWhiteSpace(before) || IsPunctuation(before));
				bool rightFlanking = !char.IsWhiteSpace(before) && (!IsPunctuation(before) || char.IsWhiteSpace(after) || IsPunctuation(after));
				for (int k = i; k <= last; k++)
				{
					bool opens = tokens[k].Kind == TokenKind.Open;
					if (opens ? !leftFlanking : !rightFlanking)
					{
						tokens[k].Use.Html = true;
						changed = true;
					}
				}

				i = last + 1;
			}

			return changed;
		}

		private static bool IsMarkdownDelimiter(Token token) => token.Use != null && !token.Use.Html;

		private static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

		/// <summary>
		/// True when the markdown parses back to the inlines' styles character by character. Whitespace is not
		/// compared (it moves out of delimiters on purpose), and when the text itself differs (a reference link
		/// whose definition is elsewhere, a trailing hard break) the styles cannot be compared, so this trusts
		/// the flanking pass.
		/// </summary>
		private static bool ReadsBackAs(string markdown, List<RichInline> expected)
		{
			var blocks = RichMarkdownParser.Parse(markdown).Blocks;
			if (blocks.Count != 1 || !blocks[0].IsTextBlock)
			{
				return true;
			}

			var wanted = StyledCharacters(expected);
			var found = StyledCharacters(blocks[0].Inlines);
			if (!wanted.Select(item => item.Key).SequenceEqual(found.Select(item => item.Key)))
			{
				return true;
			}

			return wanted.SequenceEqual(found);
		}

		private static List<(string Key, bool Bold, bool Italic, bool Strike)> StyledCharacters(IReadOnlyList<RichInline> inlines)
		{
			var items = new List<(string Key, bool Bold, bool Italic, bool Strike)>();
			foreach (var inline in inlines)
			{
				if (inline is InlineAtom atom)
				{
					items.Add(("atom:" + atom.RawMarkdown, atom.Bold, atom.Italic, atom.Strike));
					continue;
				}

				var run = (RichRun)inline;
				foreach (char c in run.Text.Where(c => !char.IsWhiteSpace(c)))
				{
					items.Add(((run.Code ? "`" : "") + c, run.Bold, run.Italic, run.Strike));
				}
			}

			return items;
		}

		private static bool EndsWithUnescaped(StringBuilder markdown, char c)
		{
			if (markdown.Length == 0 || markdown[^1] != c)
			{
				return false;
			}

			int backslashes = 0;
			for (int i = markdown.Length - 2; i >= 0 && markdown[i] == '\\'; i--)
			{
				backslashes++;
			}

			return backslashes % 2 == 0;
		}
	}
}
