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
	/// The character styles a toolbar button toggles. Code is independent of the others: applying it keeps any
	/// bold, italic or strike (markdown writes **`x`** fine), so toggling Code off gives the text back unchanged.
	/// </summary>
	[Flags]
	public enum RichInlineStyle
	{
		None = 0,
		Bold = 1,
		Italic = 2,
		Code = 4,
		Strike = 8,
	}

	public enum RichStyleCoverage
	{
		None,
		Mixed,
		All,
	}

	/// <summary>
	/// The style over a selection, for showing toolbar buttons pressed, unpressed or mixed. <see cref="All"/> holds
	/// the flags every character has, <see cref="Any"/> the flags at least one has. LinkUrl and LinkTitle are set
	/// only when the whole range is one link, and for a caret inside or at either edge of a link (the one
	/// SetLink and RemoveLink would act on), so the link button shows pressed and edits that link - even at its
	/// end, where typed text would not extend it.
	/// </summary>
	public readonly record struct RichStyleState(RichInlineStyle All, RichInlineStyle Any, string LinkUrl, string LinkTitle)
	{
		public RichStyleCoverage Coverage(RichInlineStyle style)
		{
			if (style != RichInlineStyle.None && (All & style) == style)
			{
				return RichStyleCoverage.All;
			}

			return (Any & style) != 0 ? RichStyleCoverage.Mixed : RichStyleCoverage.None;
		}
	}

	/// <summary>
	/// Styles toggled at a collapsed caret (Ctrl+B with nothing selected): the next typed text gets them flipped
	/// relative to the style it would otherwise inherit. It is a flip mask rather than absolute flags so pressing
	/// Bold twice cancels out. The widget owns it and clears it (back to default) whenever the caret moves or the
	/// selection changes, because the inherited style it is relative to changes with the caret.
	/// </summary>
	public readonly record struct RichPendingStyle(RichInlineStyle Flipped)
	{
		public bool IsEmpty => Flipped == RichInlineStyle.None;

		public RichPendingStyle Toggle(RichInlineStyle style) => new RichPendingStyle(Flipped ^ style);

		/// <summary>
		/// The template to pass as InsertText's style: <paramref name="baseStyle"/> (normally
		/// <see cref="RichStyleOperations.TypingStyle"/>) with the pending flags flipped. Never mutates the base.
		/// </summary>
		public RichRun ApplyPending(RichRun baseStyle)
		{
			var run = baseStyle.WithText("");
			var current = RichStyleOperations.StyleOf(baseStyle);
			RichStyleOperations.SetStyle(run, Flipped & ~current, true);
			RichStyleOperations.SetStyle(run, Flipped & current, false);
			return run;
		}
	}

	/// <summary>
	/// Character style and link edits over a selection. A range may cross blocks: text blocks take part, Raw and
	/// code blocks are skipped (they have no character styles). A range within one table covers the selected
	/// text of each cell from its start cell to its end cell in reading order (the cell walk of
	/// <see cref="RichTableCodeOperations.DeleteRange"/>); a range spanning several blocks skips tables, because
	/// a table cell is not part of the reading-order text a multi-block selection covers. Atoms (images, breaks)
	/// in the range take bold, italic, strike and links like text, so a bolded selection containing an image
	/// writes as one bold span; they never take inline code. Ops split runs at the range edges, merge equal
	/// neighbours afterwards (no **a****b** fragments) and mark changed blocks Dirty.
	/// </summary>
	public static class RichStyleOperations
	{
		/// <summary>
		/// Applies <paramref name="style"/> to every character in the selection, or removes it when every
		/// character already has it (the word processor rule a first-time user expects). Atoms count and change
		/// for Bold, Italic and Strike; for Code they neither count nor change. A collapsed selection changes
		/// nothing; use <see cref="RichPendingStyle"/>.
		/// Returns the selection unchanged, so the same text stays selected for the next toggle.
		/// </summary>
		public static RichSelection ToggleStyle(RichDocument document, RichSelection selection, RichInlineStyle style)
		{
			var segments = Segments(document, selection);
			var state = Measure(segments);
			if (state.Inlines == 0 || style == RichInlineStyle.None)
			{
				return selection;
			}

			bool apply = (state.All & style) != style;
			foreach (var segment in segments)
			{
				bool changed = false;
				foreach (var inline in SplitInlines(segment))
				{
					changed |= SetStyle(inline, style, apply);
				}

				Finish(segment, changed);
			}

			return selection;
		}

		/// <summary>
		/// The style state over a selection for the toolbar. For a collapsed caret it is the style typed text
		/// would get there (<see cref="TypingStyle"/>) with <paramref name="pending"/> applied, and the link the
		/// caret is in or touches. Never changes the document.
		/// </summary>
		public static RichStyleState StyleAt(RichDocument document, RichSelection selection, RichPendingStyle pending = default)
		{
			if (selection.IsEmpty)
			{
				var typing = TypingStyle(document, selection.Caret);
				if (typing == null)
				{
					return default;
				}

				typing = pending.ApplyPending(typing);
				var flags = StyleOf(typing);
				var inlines = InlinesAt(document, selection.Caret);
				var link = inlines == null ? null : LinkExtent(inlines, selection.Caret.Offset).Link;
				return new RichStyleState(flags, flags, link?.LinkUrl, link?.LinkTitle);
			}

			var state = Measure(Segments(document, selection));
			return new RichStyleState(state.All, state.Any, state.Link?.LinkUrl, state.Link?.LinkTitle);
		}

		/// <summary>
		/// The style text typed at <paramref name="position"/> would get - exactly InsertText's rule (inherit from
		/// the nearest run before the caret; a link or inline code stops at its end), applied to a table cell too.
		/// Plain in an empty document (typing there makes a paragraph). Null in a code block or Raw block, which
		/// have no character style.
		/// </summary>
		public static RichRun TypingStyle(RichDocument document, DocPosition position)
		{
			if (document.Blocks.Count == 0)
			{
				return new RichRun();
			}

			var inlines = InlinesAt(document, position);
			if (inlines == null)
			{
				return null;
			}

			// The one rule InsertText uses for paragraphs and table cells alike; StyleAt may return a live run, so
			// hand back a copy the caller can change.
			return RichEditOperations.StyleAt(inlines, position.Offset).WithText("");
		}

		/// <summary>
		/// Makes the selection a link to <paramref name="url"/>. A range: its text and atoms become the link's
		/// content, so an image in the range stays inside the one link. A caret inside or at the edge of a link:
		/// that whole link's target changes, keeping its title unless <paramref name="title"/> is given. A caret
		/// elsewhere (an empty document included): the url is inserted as linked text - trimmed and on one line,
		/// never inline code - and the caret goes after it. The link is always written inline ([text](url)): any
		/// reference label is dropped, since the user typed a target, not a label. Returns the selection to show next.
		/// </summary>
		public static RichSelection SetLink(RichDocument document, RichSelection selection, string url, string title = null)
		{
			if (string.IsNullOrEmpty(url))
			{
				return selection;
			}

			List<Segment> segments;
			if (selection.IsEmpty)
			{
				var caret = selection.Caret;
				var inlines = document.Blocks.Count == 0 ? null : InlinesAt(document, caret);
				if (inlines == null && document.Blocks.Count > 0)
				{
					return selection;
				}

				var (from, to, link) = inlines == null ? (0, 0, null) : LinkExtent(inlines, caret.Offset);
				if (link == null)
				{
					return InsertLinkText(document, caret, url, title);
				}

				title ??= link.LinkTitle;
				segments = new List<Segment> { new Segment(document.Blocks[caret.BlockIndex], inlines, from, to) };
			}
			else
			{
				segments = Segments(document, selection);
			}

			foreach (var segment in segments)
			{
				bool changed = false;
				foreach (var inline in SplitInlines(segment))
				{
					changed |= SetLinkOn(inline, url, title);
				}

				Finish(segment, changed);
			}

			return selection;
		}

		/// <summary>
		/// Removes links from the selection, keeping their text and other styles. A caret inside or at the edge of
		/// a link removes that whole link; a range unlinks only the text it covers.
		/// </summary>
		public static RichSelection RemoveLink(RichDocument document, RichSelection selection)
		{
			List<Segment> segments;
			if (selection.IsEmpty)
			{
				var caret = selection.Caret;
				var inlines = InlinesAt(document, caret);
				if (inlines == null)
				{
					return selection;
				}

				var (from, to, link) = LinkExtent(inlines, caret.Offset);
				if (link == null)
				{
					return selection;
				}

				segments = new List<Segment> { new Segment(document.Blocks[caret.BlockIndex], inlines, from, to) };
			}
			else
			{
				segments = Segments(document, selection);
			}

			foreach (var segment in segments)
			{
				bool changed = false;
				foreach (var inline in SplitInlines(segment))
				{
					changed |= SetLinkOn(inline, null, null);
				}

				Finish(segment, changed);
			}

			return selection;
		}

		internal static RichInlineStyle StyleOf(RichInline inline)
		{
			var style = RichInlineStyle.None;
			style |= inline.Bold ? RichInlineStyle.Bold : 0;
			style |= inline.Italic ? RichInlineStyle.Italic : 0;
			style |= inline is RichRun { Code: true } ? RichInlineStyle.Code : 0;
			style |= inline.Strike ? RichInlineStyle.Strike : 0;
			return style;
		}

		/// <summary>
		/// Sets (or clears) each flag in <paramref name="style"/> on the inline, leaving the other flags alone.
		/// Code applies to runs only; an atom has no inline-code form. Returns true when anything changed.
		/// </summary>
		internal static bool SetStyle(RichInline inline, RichInlineStyle style, bool on)
		{
			var before = StyleOf(inline);
			if ((style & RichInlineStyle.Bold) != 0)
			{
				inline.Bold = on;
			}

			if ((style & RichInlineStyle.Italic) != 0)
			{
				inline.Italic = on;
			}

			if ((style & RichInlineStyle.Code) != 0 && inline is RichRun run)
			{
				run.Code = on;
			}

			if ((style & RichInlineStyle.Strike) != 0)
			{
				inline.Strike = on;
			}

			return StyleOf(inline) != before;
		}

		/// <summary>
		/// Sets (or, with a null url, clears) the inline's link. Returns true when anything changed.
		/// </summary>
		private static bool SetLinkOn(RichInline inline, string url, string title)
		{
			title = url == null ? null : title;
			bool changed = inline.LinkUrl != url || inline.LinkTitle != title || inline.LinkLabel != null;
			inline.LinkUrl = url;
			inline.LinkTitle = title;
			inline.LinkLabel = null;
			return changed;
		}

		/// <summary>
		/// Inserts the url as linked text at a caret that is not on a link. The shown text is the url trimmed with
		/// control characters (a pasted newline or tab) made spaces, so it stays one line; the link target is the url
		/// as given. The insertion goes through InsertText, which also makes the paragraph an empty document needs.
		/// </summary>
		private static RichSelection InsertLinkText(RichDocument document, DocPosition caret, string url, string title)
		{
			var shown = new string(Array.ConvertAll(url.Trim().ToCharArray(), c => char.IsControl(c) ? ' ' : c));
			if (shown.Length == 0)
			{
				return RichSelection.At(caret);
			}

			var template = TypingStyle(document, caret);
			// Inside inline code the typing style is code, but a link typed there is a new piece of prose.
			template.Code = false;
			SetLinkOn(template, url, title);
			return RichEditOperations.InsertText(document, caret, shown, template);
		}

		/// <summary>
		/// The inlines a caret is in: a text block's, or the table cell's. Null for code and Raw blocks.
		/// </summary>
		private static List<RichInline> InlinesAt(RichDocument document, DocPosition position)
		{
			if (position.BlockIndex < 0 || position.BlockIndex >= document.Blocks.Count)
			{
				return null;
			}

			var block = document.Blocks[position.BlockIndex];
			return block.IsTextBlock || block.Kind == RichBlockKind.Table
				? block.InlinesAt(position.Row, position.Column)
				: null;
		}

		/// <summary>
		/// The offsets of the link a caret is in or touches, and one of its inlines: the inline before the caret is
		/// preferred (so a caret just after a link's text finds it), then the one after. (offset, offset, null)
		/// when there is no link.
		/// </summary>
		private static (int From, int To, RichInline Link) LinkExtent(List<RichInline> inlines, int offset)
		{
			int linkIndex = -1;
			if (offset > 0)
			{
				var (before, _) = RichInlines.Locate(inlines, offset);
				if (IsLink(inlines[before]))
				{
					linkIndex = before;
				}
			}

			if (linkIndex < 0 && offset < RichInlines.Length(inlines))
			{
				var (after, _) = RichInlines.Locate(inlines, offset, preferNext: true);
				if (IsLink(inlines[after]))
				{
					linkIndex = after;
				}
			}

			if (linkIndex < 0)
			{
				return (offset, offset, null);
			}

			// One link can span several inlines (a bold word or an image inside it), so grow over neighbours with the
			// same target.
			var link = inlines[linkIndex];
			int first = linkIndex;
			while (first > 0 && SameLink(inlines[first - 1], link))
			{
				first--;
			}

			int last = linkIndex;
			while (last < inlines.Count - 1 && SameLink(inlines[last + 1], link))
			{
				last++;
			}

			return (RichInlines.OffsetOf(inlines, first, 0), RichInlines.OffsetOf(inlines, last, inlines[last].Length), link);
		}

		private static bool IsLink(RichInline inline)
		{
			return inline.LinkUrl != null || inline.LinkLabel != null;
		}

		private static bool SameLink(RichInline a, RichInline b)
		{
			return a.LinkUrl == b.LinkUrl && a.LinkTitle == b.LinkTitle && a.LinkLabel == b.LinkLabel;
		}

		/// <summary>
		/// One contiguous stretch of a selection inside one inline list.
		/// </summary>
		private readonly record struct Segment(RichBlock Block, List<RichInline> Inlines, int From, int To);

		/// <summary>
		/// Breaks a selection into per-block (and, in a table, per-cell) stretches, skipping blocks with no
		/// character styles and tables a multi-block selection crosses. Empty stretches are left out.
		/// </summary>
		private static List<Segment> Segments(RichDocument document, RichSelection selection)
		{
			var segments = new List<Segment>();
			var start = selection.Start;
			var end = selection.End;
			if (start == end)
			{
				return segments;
			}

			if (start.BlockIndex == end.BlockIndex)
			{
				var block = document.Blocks[start.BlockIndex];
				if (block.Kind == RichBlockKind.Table)
				{
					// The same reading-order cell walk as RichTableCodeOperations.DeleteRange.
					int columns = block.TableRows[0].Count;
					for (int cell = start.Row * columns + start.Column; cell <= end.Row * columns + end.Column; cell++)
					{
						int row = cell / columns;
						int column = cell % columns;
						int from = row == start.Row && column == start.Column ? start.Offset : 0;
						int to = row == end.Row && column == end.Column ? end.Offset : block.TextLength(row, column);
						if (from < to)
						{
							segments.Add(new Segment(block, block.InlinesAt(row, column), from, to));
						}
					}

					return segments;
				}
			}

			for (int i = start.BlockIndex; i <= end.BlockIndex; i++)
			{
				var block = document.Blocks[i];
				if (!block.IsTextBlock)
				{
					continue;
				}

				int from = i == start.BlockIndex ? start.Offset : 0;
				int to = i == end.BlockIndex ? end.Offset : block.TextLength();
				if (from < to)
				{
					segments.Add(new Segment(block, block.Inlines, from, to));
				}
			}

			return segments;
		}

		private readonly record struct Measurement(int Inlines, RichInlineStyle All, RichInlineStyle Any, RichInline Link);

		/// <summary>
		/// The flags common to and present in the selection's text and atoms, and the link when all of it is one
		/// link. An atom counts for Bold, Italic and Strike but not Code (it can never be code, so it neither
		/// clears Code's All nor sets its Any). Reads without splitting, so StyleAt never changes the document.
		/// </summary>
		private static Measurement Measure(List<Segment> segments)
		{
			int inlines = 0;
			bool anyRun = false;
			var all = RichInlineStyle.Bold | RichInlineStyle.Italic | RichInlineStyle.Code | RichInlineStyle.Strike;
			var any = RichInlineStyle.None;
			RichInline link = null;
			bool oneLink = segments.Count == 1;
			foreach (var segment in segments)
			{
				int start = 0;
				foreach (var inline in segment.Inlines)
				{
					int end = start + inline.Length;
					int covered = Math.Min(end, segment.To) - Math.Max(start, segment.From);
					start = end;
					if (covered <= 0)
					{
						continue;
					}

					inlines++;
					var style = StyleOf(inline);
					if (inline is RichRun)
					{
						anyRun = true;
						all &= style;
					}
					else
					{
						all &= style | RichInlineStyle.Code;
					}

					any |= style;
					if (!IsLink(inline) || (link != null && !SameLink(link, inline)))
					{
						oneLink = false;
					}

					link ??= inline;
				}
			}

			if (inlines == 0)
			{
				return default;
			}

			if (!anyRun)
			{
				// Only atoms: none of them is code, so Code is not common to the selection.
				all &= ~RichInlineStyle.Code;
			}

			return new Measurement(inlines, all, any, oneLink ? link : null);
		}

		/// <summary>
		/// Splits the segment's runs at its edges and returns the inlines (runs and atoms) inside it.
		/// </summary>
		private static List<RichInline> SplitInlines(Segment segment)
		{
			int first = RichInlines.SplitAt(segment.Inlines, segment.From);
			int last = RichInlines.SplitAt(segment.Inlines, segment.To);
			return segment.Inlines.GetRange(first, last - first);
		}

		/// <summary>
		/// Merges the runs split at the segment's edges back together, and marks the block dirty only when a style
		/// actually changed, so an op that changed nothing leaves the block writing its original markdown.
		/// </summary>
		private static void Finish(Segment segment, bool changed)
		{
			RichInlines.MergeAdjacent(segment.Inlines);
			if (changed)
			{
				segment.Block.Dirty = true;
			}
		}
	}
}
