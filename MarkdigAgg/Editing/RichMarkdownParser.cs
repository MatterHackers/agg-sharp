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
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Reads markdown into a <see cref="RichDocument"/> whose blocks partition the source exactly, so
	/// <see cref="RichMarkdownWriter.Write"/> of an unedited document gives back the same bytes.
	/// Paragraphs, headings, single-paragraph list items, plain-paragraph quotes, &lt;div align&gt; groups, code
	/// blocks and pipe tables are modelled; every other block is Raw (shown rendered, edited in the Markdown tab).
	/// </summary>
	public class RichMarkdownParser
	{
		// The viewer's extensions, so the rich view reads the same structure the viewer renders, plus precise
		// spans: the partition is built from them.
		private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
			.UseSupportedExtensions()
			.UsePreciseSourceLocation()
			.Build();

		private readonly RichDocument document = new RichDocument();

		private string body;

		// Whitespace seen since the last block; it becomes the next block's SeparatorBefore (or the TrailingText).
		private string pendingSeparator = "";

		// Raw blocks parsed from a Markdig HtmlBlock: only these can be an alignment wrapper's tags (an indented
		// code block holding "<div align>" text must stay code).
		private readonly HashSet<RichBlock> htmlBlocks = new HashSet<RichBlock>();

		private RichMarkdownParser()
		{
		}

		public static RichDocument Parse(string markdown)
		{
			var parser = new RichMarkdownParser();
			parser.Run(markdown ?? "");
			return parser.document;
		}

		private void Run(string markdown)
		{
			document.Frontmatter = FrontmatterOf(markdown);

			// Parse only the body so hidden frontmatter never reads as a thematic break or a setext heading.
			body = markdown.Substring(document.Frontmatter.Length);
			var parsed = Markdown.Parse(body, Pipeline);

			int cursor = 0;
			foreach (var block in parsed)
			{
				// The definition group's span starts where the previous block ended and stops mid-definition, so
				// its definitions are left to the gap handling, which makes them a Raw block of whole lines.
				if (block is LinkReferenceDefinitionGroup)
				{
					continue;
				}

				// A list or quote of plain paragraphs becomes one block per paragraph; anything richer is one Raw block.
				var segments = block switch
				{
					ListBlock list => ListSegments(list, cursor),
					QuoteBlock quote => QuoteSegments(quote, cursor),
					_ => null,
				};
				if (segments != null)
				{
					cursor = AddSegments(cursor, segments);
					continue;
				}

				int start = Math.Max(block.Span.Start, cursor);
				// Markdig span ends are inclusive.
				int end = Math.Min(block.Span.End + 1, body.Length);
				if (end <= start)
				{
					continue;
				}

				if (block is HeadingBlock)
				{
					end = HeadingLineEnd(end);
				}
				else if (RichCodeAndTables.IsCode(block))
				{
					start = RichCodeAndTables.CodeStart((CodeBlock)block, body, start, cursor);
					end = RichCodeAndTables.CodeEnd((CodeBlock)block, end);
				}
				else if (block.GetType() == typeof(ParagraphBlock))
				{
					start = ParagraphTextStart((ParagraphBlock)block, start);
				}

				AddGap(cursor, start);
				document.Blocks.Add(BuildBlock(block, start, end));
				cursor = end;
			}

			AddGap(cursor, body.Length);
			document.TrailingText = pendingSeparator;
			RichAlignGroups.Fold(document.Blocks, htmlBlocks);
		}

		/// <summary>
		/// One modelled block cut out of a list or quote: the source range it owns and the paragraph it reads.
		/// </summary>
		private record Segment(int Start, int End, ParagraphBlock Paragraph, RichBlockKind Kind, RichListInfo List);

		/// <summary>
		/// One ListItem segment per item, nested lists flattened in source order with their Depth, or null when
		/// any item holds more than one paragraph or anything but a paragraph and nested lists. Null makes the
		/// whole top-level list one Raw block: splitting a list around an unmodelled item would let an edit to
		/// its neighbours renumber or re-indent markdown the rich view cannot show.
		/// </summary>
		private List<Segment> ListSegments(ListBlock list, int cursor)
		{
			var segments = new List<Segment>();
			if (!AddListSegments(list, 0, segments) || segments.Count == 0 || segments[0].Start < cursor)
			{
				return null;
			}

			return GapsAreWhitespace(segments) ? segments : null;
		}

		private bool AddListSegments(ListBlock list, int depth, List<Segment> segments)
		{
			// Without the ListExtras extension an ordered list's BulletType is always '1'; anything else is a
			// form the writer could not reproduce.
			int startNumber = 1;
			if (list.IsOrdered && (list.BulletType != '1' || !int.TryParse(list.OrderedStart ?? "1", out startNumber)))
			{
				return false;
			}

			foreach (var child in list)
			{
				if (child is not ListItemBlock item
					|| item.Count == 0
					|| item[0].GetType() != typeof(ParagraphBlock))
				{
					return false;
				}

				var paragraph = (ParagraphBlock)item[0];
				int paragraphStart = paragraph.Span.Start;

				// The item starts at its marker; indentation before it is whitespace for the separator.
				int itemStart = SkipSpaces(item.Span.Start);

				// Reference definitions at the top of the item would sit in its source but not its inlines.
				if (itemStart >= paragraphStart || ParagraphTextStart(paragraph, paragraphStart) != paragraphStart)
				{
					return false;
				}

				segments.Add(new Segment(itemStart, ParagraphEnd(paragraph), paragraph, RichBlockKind.ListItem, new RichListInfo
				{
					Ordered = list.IsOrdered,
					Depth = depth,
					Marker = list.IsOrdered ? list.OrderedDelimiter : list.BulletType,
					StartNumber = startNumber,
				}));

				for (int i = 1; i < item.Count; i++)
				{
					if (item[i] is not ListBlock nested || !AddListSegments(nested, depth + 1, segments))
					{
						return false;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// One Quote segment per paragraph of a blockquote whose children are all paragraphs, or null (Raw) for
		/// nested quotes, lists, code and the like. Each segment's source keeps its "&gt;" prefixes. The "&gt;"
		/// line between two paragraphs starts the second segment, leaving a bare line break as the separator,
		/// and "&gt;" lines closing the quote end the last segment.
		/// </summary>
		private List<Segment> QuoteSegments(QuoteBlock quote, int cursor)
		{
			int quoteStart = SkipSpaces(quote.Span.Start);
			int quoteEnd = Math.Min(quote.Span.End + 1, body.Length);
			if (quote.Count == 0 || quoteStart < cursor || quoteStart >= body.Length || body[quoteStart] != '>')
			{
				return null;
			}

			var segments = new List<Segment>();
			int start = quoteStart;
			foreach (var child in quote)
			{
				if (child.GetType() != typeof(ParagraphBlock))
				{
					return null;
				}

				var paragraph = (ParagraphBlock)child;
				int paragraphStart = paragraph.Span.Start;
				if (segments.Count > 0)
				{
					int lineBreak = body.IndexOf('\n', segments[^1].End);
					if (lineBreak < 0 || lineBreak >= paragraphStart)
					{
						return null;
					}

					start = lineBreak + 1;
				}

				if (start > paragraphStart
					|| !OnlyQuoteMarkers(start, paragraphStart)
					|| ParagraphTextStart(paragraph, paragraphStart) != paragraphStart)
				{
					return null;
				}

				segments.Add(new Segment(start, ParagraphEnd(paragraph), paragraph, RichBlockKind.Quote, null));
			}

			int lastEnd = segments[^1].End;
			if (quoteEnd > lastEnd)
			{
				if (!OnlyQuoteMarkers(lastEnd, quoteEnd))
				{
					return null;
				}

				int lastMarker = body.LastIndexOf('>', quoteEnd - 1, quoteEnd - lastEnd);
				if (lastMarker >= 0)
				{
					segments[^1] = segments[^1] with { End = lastMarker + 1 };
				}
			}

			return GapsAreWhitespace(segments) ? segments : null;
		}

		/// <summary>
		/// Adds the segments' blocks: the text before the first is an ordinary gap, the whitespace between them
		/// separators. Returns where the last one ends.
		/// </summary>
		private int AddSegments(int cursor, List<Segment> segments)
		{
			AddGap(cursor, segments[0].Start);

			// The segments of one call are one top-level list or one blockquote.
			RichListGroup listGroup = segments[0].Kind == RichBlockKind.ListItem ? new RichListGroup { OriginalMemberCount = segments.Count } : null;
			RichQuoteGroup quoteGroup = segments[0].Kind == RichBlockKind.Quote ? new RichQuoteGroup { OriginalMemberCount = segments.Count } : null;
			for (int i = 0; i < segments.Count; i++)
			{
				var segment = segments[i];
				if (i > 0)
				{
					pendingSeparator += body.Substring(segments[i - 1].End, segment.Start - segments[i - 1].End);
				}

				var block = BuildBlock(segment.Paragraph, segment.Start, segment.End);
				block.Kind = segment.Kind;
				block.List = segment.List;
				block.ListGroup = listGroup;
				block.QuoteGroup = quoteGroup;
				document.Blocks.Add(block);
			}

			return segments[^1].End;
		}

		private bool GapsAreWhitespace(List<Segment> segments)
		{
			for (int i = 1; i < segments.Count; i++)
			{
				int gapStart = segments[i - 1].End;
				int gapEnd = segments[i].Start;
				if (gapEnd < gapStart || !string.IsNullOrWhiteSpace(body.Substring(gapStart, gapEnd - gapStart)))
				{
					return false;
				}
			}

			return true;
		}

		private bool OnlyQuoteMarkers(int start, int end)
		{
			for (int i = start; i < end; i++)
			{
				if (body[i] != '>' && !char.IsWhiteSpace(body[i]))
				{
					return false;
				}
			}

			return true;
		}

		private int SkipSpaces(int position)
		{
			while (position < body.Length && (body[position] == ' ' || body[position] == '\t'))
			{
				position++;
			}

			return position;
		}

		// Markdig span ends are inclusive.
		private int ParagraphEnd(ParagraphBlock paragraph) => Math.Min(paragraph.Span.End + 1, body.Length);

		/// <summary>
		/// Markdig ends an ATX heading's span at its text, leaving a closing "#" sequence and trailing spaces
		/// outside every block. They belong to the heading, so the rest of the line joins it when that is all
		/// the line holds.
		/// </summary>
		private int HeadingLineEnd(int end)
		{
			int lineEnd = end;
			while (lineEnd < body.Length && (body[lineEnd] == '#' || body[lineEnd] == ' ' || body[lineEnd] == '\t'))
			{
				lineEnd++;
			}

			bool restOfLine = lineEnd == body.Length || body[lineEnd] == '\r' || body[lineEnd] == '\n';
			return restOfLine ? lineEnd : end;
		}

		/// <summary>
		/// Link reference definitions at the top of a paragraph are pulled out of its inlines but left inside its
		/// span. The paragraph is made to start at its first text line so the definitions fall into the gap before
		/// it and become a Raw block; otherwise rewriting the paragraph would delete them.
		/// </summary>
		private int ParagraphTextStart(ParagraphBlock paragraph, int start)
		{
			var first = paragraph.Inline?.FirstChild;
			if (first == null || first.Span.Start <= start)
			{
				return start;
			}

			int lineStart = body.LastIndexOf('\n', first.Span.Start - 1) + 1;
			if (lineStart <= start)
			{
				return start;
			}

			// The paragraph's own indentation stays in the separator, as it does for any other block.
			while (lineStart < first.Span.Start && (body[lineStart] == ' ' || body[lineStart] == '\t'))
			{
				lineStart++;
			}

			return lineStart;
		}

		/// <summary>
		/// The frontmatter by the viewer's convention (see MarkdownWidget.StripFrontmatter): a first line of "---"
		/// through the next "---" line and its newline. "" when there is none.
		/// </summary>
		private static string FrontmatterOf(string markdown)
		{
			int firstLineEnd = markdown.IndexOf('\n');
			if (firstLineEnd < 0 || markdown.Substring(0, firstLineEnd).Trim() != "---")
			{
				return "";
			}

			int lineStart = firstLineEnd + 1;
			while (lineStart < markdown.Length)
			{
				int lineEnd = markdown.IndexOf('\n', lineStart);
				int contentEnd = lineEnd < 0 ? markdown.Length : lineEnd;
				int next = lineEnd < 0 ? markdown.Length : lineEnd + 1;
				if (markdown.Substring(lineStart, contentEnd - lineStart).Trim() == "---")
				{
					return markdown.Substring(0, next);
				}

				lineStart = next;
			}

			return "";
		}

		/// <summary>
		/// Text between blocks. Whitespace becomes a separator; anything else Markdig left outside every block
		/// (link reference definitions) becomes a Raw block so it stays visible and survives edits around it.
		/// </summary>
		private void AddGap(int start, int end)
		{
			string gap = body.Substring(start, end - start);
			int contentStart = 0;
			while (contentStart < gap.Length && char.IsWhiteSpace(gap[contentStart]))
			{
				contentStart++;
			}

			if (contentStart == gap.Length)
			{
				pendingSeparator += gap;
				return;
			}

			// Indentation on the content's first line belongs to the content, not the separator.
			int lineStart = contentStart == 0 ? 0 : gap.LastIndexOf('\n', contentStart - 1) + 1;
			int contentEnd = gap.Length;
			while (char.IsWhiteSpace(gap[contentEnd - 1]))
			{
				contentEnd--;
			}

			document.Blocks.Add(new RichBlock
			{
				Kind = RichBlockKind.Raw,
				SeparatorBefore = pendingSeparator + gap.Substring(0, lineStart),
				OriginalSource = gap.Substring(lineStart, contentEnd - lineStart),
			});
			pendingSeparator = gap.Substring(contentEnd);
		}

		private RichBlock BuildBlock(Block block, int start, int end)
		{
			var result = new RichBlock
			{
				Kind = RichBlockKind.Raw,
				SeparatorBefore = pendingSeparator,
				OriginalSource = body.Substring(start, end - start),
			};
			pendingSeparator = "";
			if (block is HtmlBlock)
			{
				htmlBlocks.Add(result);
			}

			// Exact types only: extension blocks deriving from these (tables, for one) are not plain text.
			bool isParagraph = block.GetType() == typeof(ParagraphBlock);
			bool isHeading = block.GetType() == typeof(HeadingBlock);
			if (isParagraph || isHeading)
			{
				result.Inlines = InlinesOf((LeafBlock)block);
				result.Kind = isHeading ? RichBlockKind.Heading : RichBlockKind.Paragraph;
				result.HeadingLevel = isHeading ? ((HeadingBlock)block).Level : 0;
			}
			else if (RichCodeAndTables.IsCode(block))
			{
				RichCodeAndTables.ReadCode((CodeBlock)block, body, result);
			}
			else if (block is Markdig.Extensions.Tables.Table table)
			{
				RichCodeAndTables.ReadTable(table, result, InlinesOf);
			}

			return result;
		}

		private List<RichInline> InlinesOf(LeafBlock leaf)
		{
			var inlines = new List<RichInline>();
			if (leaf.Inline != null)
			{
				AddInlines(leaf.Inline, default, inlines);
			}

			RichInlines.MergeAdjacent(inlines);
			return inlines;
		}

		private record struct Style(bool Bold, bool Italic, bool Strike, string LinkUrl, string LinkTitle, string LinkLabel);

		/// <summary>
		/// Appends the model inlines for a container's children. Anything without a model (sub/superscript,
		/// inserted, marked, ...) becomes a Raw atom of its exact source, so the text around it stays editable
		/// and a rewrite of the block reproduces it.
		/// </summary>
		private void AddInlines(ContainerInline container, Style style, List<RichInline> inlines)
		{
			foreach (var inline in container)
			{
				switch (inline)
				{
					case LiteralInline literal:
						inlines.Add(Run(literal.Content.ToString(), style));
						break;

					case HtmlEntityInline entity:
						inlines.Add(Run(entity.Transcoded.ToString(), style));
						break;

					case CodeInline code:
						var codeRun = Run(code.Content, style);
						codeRun.Code = true;
						inlines.Add(codeRun);
						break;

					case LineBreakInline lineBreak when lineBreak.IsHard:
						inlines.Add(new InlineAtom(InlineAtomKind.HardBreak, HardBreakSource(lineBreak)));
						break;

					case LineBreakInline:
						// A soft break renders as a space; the editor wraps lines itself.
						inlines.Add(Run(" ", style));
						break;

					case HtmlInline:
						inlines.Add(new InlineAtom(InlineAtomKind.Html, Slice(inline)));
						break;

					case AutolinkInline:
						inlines.Add(new InlineAtom(InlineAtomKind.Autolink, Slice(inline)));
						break;

					case LinkInline link when link.IsImage:
						inlines.Add(new InlineAtom(InlineAtomKind.Image, Slice(inline)));
						break;

					case LinkInline link when link.IsAutoLink:
						// A bare www. or http link found by the AutoLinks extension.
						inlines.Add(new InlineAtom(InlineAtomKind.Autolink, Slice(inline)));
						break;

					case LinkInline link:
						string label = ReferenceLabel(link);

						// A reference link's title lives on its definition, which stays put; only inline links carry one.
						AddInlines(link, style with
						{
							LinkUrl = link.Url ?? "",
							LinkTitle = label == null ? NullIfEmpty(link.Title) : null,
							LinkLabel = label,
						}, inlines);
						break;

					case EmphasisInline emphasis when emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_':
						// Markdig nests ***x*** as a double inside a single, so both styles accumulate.
						AddInlines(emphasis, emphasis.DelimiterCount >= 2 ? style with { Bold = true } : style with { Italic = true }, inlines);
						break;

					case EmphasisInline emphasis when emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2:
						AddInlines(emphasis, style with { Strike = true }, inlines);
						break;

					default:
						inlines.Add(new InlineAtom(InlineAtomKind.Raw, Slice(inline)));
						break;
				}
			}
		}

		/// <summary>
		/// The label of a reference link ([text][label], [label][], [label]) as written in the source, so a rewrite
		/// still points at the same definition; null for an inline link.
		/// </summary>
		private string ReferenceLabel(LinkInline link)
		{
			if (link.Reference == null && !link.IsShortcut)
			{
				return null;
			}

			var span = link.LabelSpan;
			if (span.Start >= 0 && span.Length > 0 && span.End < body.Length)
			{
				return body.Substring(span.Start, span.Length);
			}

			return link.Label;
		}

		private static string NullIfEmpty(string text) => string.IsNullOrEmpty(text) ? null : text;

		private static RichRun Run(string text, Style style)
		{
			return new RichRun(text)
			{
				Bold = style.Bold,
				Italic = style.Italic,
				Strike = style.Strike,
				LinkUrl = style.LinkUrl,
				LinkTitle = style.LinkTitle,
				LinkLabel = style.LinkLabel,
			};
		}

		/// <summary>
		/// Markdig's hard break span is only the line ending; the trailing spaces that make it hard sit outside
		/// every inline. Without them a rewritten block would turn the break soft.
		/// </summary>
		private string HardBreakSource(LineBreakInline lineBreak)
		{
			int start = lineBreak.Span.Start;
			if (start > 0 && body[start - 1] == '\\')
			{
				start--;
			}
			else
			{
				while (start > 0 && body[start - 1] == ' ')
				{
					start--;
				}
			}

			// For CRLF the span stops at the '\r'.
			int end = lineBreak.Span.End + 1;
			if (end < body.Length && body[end - 1] == '\r' && body[end] == '\n')
			{
				end++;
			}

			return body.Substring(start, end - start);
		}

		private string Slice(Inline inline)
		{
			return body.Substring(inline.Span.Start, inline.Span.Length);
		}
	}
}
