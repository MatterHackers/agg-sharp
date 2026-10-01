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
using System.Globalization;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	internal enum LayoutItemKind
	{
		Char,
		Space,
		Break,

		/// <summary>
		/// An image or a Raw block: a box the caret steps over whole, and a place a line may break before and after.
		/// </summary>
		Box,
	}

	/// <summary>
	/// One drawn piece of a block: a character, a box, a code line's '\n', or one character of a text atom's raw
	/// markdown. A text atom is many items but one caret offset, so items carry their offset rather than being
	/// indexed by it.
	/// </summary>
	internal struct LayoutItem
	{
		public LayoutItemKind Kind;

		// Fixed advance; a tab's depends on where it starts (see Advance).
		public double Advance;
		public StyledTypeFace Face;
		public int InlineIndex;
		public int StartInInline;

		// An image's height; text takes its height from Face.
		public double BoxHeight;

		// The caret offset before this item.
		public int Offset;

		// The caret may sit before this item: false inside a character (a surrogate pair's second half, a combining
		// mark) and inside a text atom.
		public bool StopBefore;

		// A line may never start here: the second half of a surrogate pair or a combining mark.
		public bool NoBreakBefore;
		public bool IsTab;
		public bool InAtom;

		/// <summary>
		/// The item's advance when it starts at <paramref name="x"/> from the line's start. A tab advances to the
		/// next stop four spaces apart, rather than by the font's tab glyph.
		/// </summary>
		public double AdvanceAt(double x)
		{
			if (!IsTab)
			{
				return Advance;
			}

			double tabStop = 4 * Face.GetAdvanceForCharacter(' ');
			return tabStop <= 0 ? 0 : tabStop - (x % tabStop);
		}
	}

	/// <summary>
	/// Turns a block into layout items and breaks them into lines. Advances are summed per character rather than
	/// measured per string with TypeFacePrinter: the faces do no kerning, so the sum is exact, and it gives every
	/// caret x as a by-product.
	/// </summary>
	internal static class RichLayoutItems
	{
		public static double Measure(StyledTypeFace face, string text)
		{
			double width = 0;
			for (int i = 0; i < text.Length; i++)
			{
				width += face.GetAdvanceForCharacter(text, i);
			}

			return width;
		}

		public static LayoutItem[] InlineItems(RichBlock block, RichLayoutStyle style, StyledTypeFace blockFace)
		{
			var items = new List<LayoutItem>();
			int offset = 0;
			for (int inlineIndex = 0; inlineIndex < block.Inlines.Count; inlineIndex++)
			{
				switch (block.Inlines[inlineIndex])
				{
					case RichRun run:
						var face = style.RunFace(block, run);
						for (int i = 0; i < run.Text.Length; i++)
						{
							items.Add(CharItem(run.Text, i, face, inlineIndex, offset++, code: false));
						}

						break;
					case InlineAtom atom when atom.Kind == InlineAtomKind.HardBreak:
						items.Add(new LayoutItem { Kind = LayoutItemKind.Break, Face = blockFace, InlineIndex = inlineIndex, Offset = offset++, StopBefore = true });
						break;
					case InlineAtom atom when atom.Kind == InlineAtomKind.Image || atom.RawMarkdown.Length == 0:
						double lineHeight = blockFace.AscentInPixels + Math.Abs(blockFace.DescentInPixels);
						var size = atom.Kind == InlineAtomKind.Image
							? style.ImageSize?.Invoke(atom) ?? new Vector2(lineHeight, lineHeight)
							: Vector2.Zero;
						items.Add(new LayoutItem { Kind = LayoutItemKind.Box, Advance = size.X, BoxHeight = size.Y, InlineIndex = inlineIndex, Offset = offset++, StopBefore = true });
						break;
					case InlineAtom atom:
						// Inline HTML, autolinks and unmodelled inlines show their markdown as plain text and wrap like a
						// word: no break inside (even at its spaces) unless it is wider than the line.
						for (int i = 0; i < atom.RawMarkdown.Length; i++)
						{
							var item = CharItem(atom.RawMarkdown, i, blockFace, inlineIndex, offset, code: true);
							item.InAtom = true;
							item.StopBefore = i == 0;
							items.Add(item);
						}

						offset++;
						break;
				}
			}

			return Finish(items);
		}

		public static LayoutItem[] CodeItems(string codeText, StyledTypeFace face)
		{
			var items = new List<LayoutItem>(codeText.Length);
			for (int i = 0; i < codeText.Length; i++)
			{
				var item = CharItem(codeText, i, face, -1, i, code: true);
				if (codeText[i] == '\n')
				{
					item.Kind = LayoutItemKind.Break;
					item.Advance = 0;
				}

				items.Add(item);
			}

			return Finish(items);
		}

		/// <summary>
		/// A Raw block is one box (its rendered markdown, drawn by a child widget) with a caret before and after it.
		/// </summary>
		public static LayoutItem[] RawItems(RichBlock block, double available, RichLayoutStyle style, StyledTypeFace blockFace)
		{
			double lineHeight = blockFace.AscentInPixels + Math.Abs(blockFace.DescentInPixels);
			var size = style.RawBlockSize?.Invoke(block, available) ?? new Vector2(available, lineHeight);
			return new[] { new LayoutItem { Kind = LayoutItemKind.Box, Advance = size.X, BoxHeight = size.Y, InlineIndex = -1, StopBefore = true } };
		}

		/// <summary>
		/// Greedy breaking; returns each line's first item. A line breaks after its last space, or before or after a
		/// box, when the next item would cross the width, and between characters when there is no such place
		/// (never inside a surrogate pair or before a combining mark). Spaces never cause a break - they stay at the
		/// end of the line they follow. A line always takes at least one item, so something wider than the line gets
		/// a line of its own instead of looping.
		/// </summary>
		public static List<int> BreakLines(LayoutItem[] items, double available, bool wrapAtSpaces)
		{
			var starts = new List<int> { 0 };
			int lineStart = 0;
			int lastBreak = -1;
			double x = 0;
			int i = 0;
			while (i < items.Length)
			{
				var item = items[i];
				double advance = item.AdvanceAt(x);
				if (item.Kind == LayoutItemKind.Break)
				{
					i++;
					starts.Add(i);
					lineStart = i;
					lastBreak = -1;
					x = 0;
					continue;
				}

				if (wrapAtSpaces && item.Kind == LayoutItemKind.Space)
				{
					x += advance;
					i++;
					lastBreak = CanStartLine(items, i) ? i : lastBreak;
					continue;
				}

				if (wrapAtSpaces && item.Kind == LayoutItemKind.Box && i > lineStart)
				{
					lastBreak = i;
				}

				if (i > lineStart && x + advance > available)
				{
					int breakAt = lastBreak > lineStart ? lastBreak : CharacterBreak(items, lineStart, i);
					if (breakAt > lineStart && breakAt < items.Length)
					{
						starts.Add(breakAt);
						lineStart = breakAt;
						lastBreak = -1;
						x = 0;
						i = breakAt;
						continue;
					}
				}

				x += advance;
				i++;
				if (wrapAtSpaces && item.Kind == LayoutItemKind.Box && CanStartLine(items, i))
				{
					lastBreak = i;
				}
			}

			return starts;
		}

		// A mark attached to a space (or a box) belongs with it, so the line cannot break between them.
		private static bool CanStartLine(LayoutItem[] items, int i) => i >= items.Length || !items[i].NoBreakBefore;

		// Where to break a word wider than the line at item i: back off to the start of the character i is part of,
		// or, when that character began the line, forward past it.
		private static int CharacterBreak(LayoutItem[] items, int lineStart, int i)
		{
			int breakAt = i;
			while (breakAt > lineStart + 1 && items[breakAt].NoBreakBefore)
			{
				breakAt--;
			}

			if (items[breakAt].NoBreakBefore)
			{
				breakAt = i;
				while (breakAt < items.Length && items[breakAt].NoBreakBefore)
				{
					breakAt++;
				}
			}

			return breakAt;
		}

		private static LayoutItem CharItem(string text, int i, StyledTypeFace face, int inlineIndex, int offset, bool code)
		{
			char c = text[i];
			bool attached = IsAttached(text, i);
			bool tab = c == '\t';
			bool space = !code && (c == ' ' || tab);
			return new LayoutItem
			{
				Kind = space ? LayoutItemKind.Space : LayoutItemKind.Char,
				Advance = tab ? 0 : face.GetAdvanceForCharacter(text, i),
				Face = face,
				InlineIndex = inlineIndex,
				StartInInline = i,
				Offset = offset,
				StopBefore = !attached,
				NoBreakBefore = attached,
				IsTab = tab,
			};
		}

		/// <summary>
		/// True when the char at <paramref name="i"/> continues the character before it, so the caret never stops
		/// and a line never breaks before it: a surrogate pair's second half, a combining or spacing mark, a
		/// variation selector, a zero width joiner and whatever it joins on, and the second flag letter of a
		/// regional-indicator pair. Looks only within one run's text.
		/// </summary>
		private static bool IsAttached(string text, int i)
		{
			char c = text[i];
			var category = CharUnicodeInfo.GetUnicodeCategory(c);
			if (char.IsLowSurrogate(c)
				|| category == UnicodeCategory.NonSpacingMark
				|| category == UnicodeCategory.EnclosingMark
				|| category == UnicodeCategory.SpacingCombiningMark
				|| c == ZeroWidthJoiner
				|| (c >= '\uFE00' && c <= '\uFE0F')
				|| (i > 0 && text[i - 1] == ZeroWidthJoiner))
			{
				return true;
			}

			// Flags are pairs of regional indicators: the second, fourth, ... of a run of them joins the one before.
			if (IsRegionalIndicatorAt(text, i))
			{
				int before = 0;
				for (int j = i - 2; j >= 0 && IsRegionalIndicatorAt(text, j); j -= 2)
				{
					before++;
				}

				return before % 2 == 1;
			}

			return false;
		}

		private const char ZeroWidthJoiner = '\u200D';

		// U+1F1E6..U+1F1FF, the regional indicator letters, as a surrogate pair starting at i.
		private static bool IsRegionalIndicatorAt(string text, int i)
		{
			return i + 1 < text.Length
				&& text[i] == '\uD83C'
				&& text[i + 1] >= '\uDDE6'
				&& text[i + 1] <= '\uDDFF';
		}

		// The block's start is always a caret stop, even when it begins with a combining mark.
		private static LayoutItem[] Finish(List<LayoutItem> items)
		{
			if (items.Count > 0)
			{
				var first = items[0];
				first.StopBefore = true;
				first.NoBreakBefore = false;
				items[0] = first;
			}

			return items.ToArray();
		}
	}
}
