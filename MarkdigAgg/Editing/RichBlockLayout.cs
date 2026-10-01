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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// One block laid out to a width: wrapped lines of positioned fragments, the list marker, the quote bar and the
	/// code background, plus caret geometry. Coordinates are y-up like every agg widget, with the origin at the
	/// bottom-left of the block's box (spacing above and below included), so the editor widget draws a block by
	/// offsetting it to the block's bottom and stacks blocks by adding <see cref="Height"/>s. Blocks lay out
	/// independently, so an edit re-lays out only the block it touched.
	/// </summary>
	public sealed class RichBlockLayout : IRichBlockLayout
	{
		private int length;

		private RichBlockLayout(RichBlock block)
		{
			Block = block;
		}

		public RichBlock Block { get; }

		public List<RichLayoutLine> Lines { get; } = new List<RichLayoutLine>();

		/// <summary>
		/// The block's whole height, including the style's spacing above and below it.
		/// </summary>
		public double Height { get; private set; }

		/// <summary>
		/// Where the block's text starts: after the list gutter, the quote bar or the code padding.
		/// </summary>
		public double TextLeft { get; private set; }

		/// <summary>
		/// A list item's marker ("-" or "3."), or null.
		/// </summary>
		public string Marker { get; private set; }

		public double MarkerX { get; private set; }

		public double MarkerBaseline { get; private set; }

		public StyledTypeFace MarkerFace { get; private set; }

		/// <summary>
		/// A quote's bar, or null.
		/// </summary>
		public RectangleDouble? QuoteBar { get; private set; }

		/// <summary>
		/// A code block's shaded background, or null.
		/// </summary>
		public RectangleDouble? CodeBackground { get; private set; }

		/// <summary>
		/// Lays a block out to <paramref name="width"/>. <paramref name="listNumber"/> is the number an ordered list
		/// item shows; the caller counts it because numbering depends on the blocks around this one. Text wraps at
		/// spaces and a word wider than the line breaks between characters; code keeps its spaces and breaks only
		/// at its newlines and where a line is wider than the block.
		/// </summary>
		public static RichBlockLayout Layout(RichBlock block, double width, RichLayoutStyle style, int listNumber = 1)
		{
			if (block.Kind == RichBlockKind.Table)
			{
				// A table is a grid of cells, each laid out like a paragraph, with its own hit testing: RichTableLayout.
				throw new NotSupportedException("Table blocks are laid out by RichTableLayout, not RichBlockLayout.");
			}

			var layout = new RichBlockLayout(block);
			var blockFace = style.BlockFace(block);
			var (before, after) = style.BlockSpacing(block);
			bool code = block.Kind == RichBlockKind.CodeBlock;
			double rightInset = 0;
			switch (block.Kind)
			{
				case RichBlockKind.ListItem:
					int depth = block.List?.Depth ?? 0;
					layout.TextLeft = (depth + 1) * style.ListIndent + style.MarkerGutter;
					bool ordered = block.List?.Ordered ?? false;
					char numberMarker = block.List?.Marker == ')' ? ')' : '.';
					layout.Marker = ordered ? $"{listNumber}{numberMarker}" : style.BulletMarker;
					layout.MarkerFace = blockFace;
					double markerWidth = RichLayoutItems.Measure(blockFace, layout.Marker);

					// A wide number ("1000.") grows the gutter rather than hanging past the item's indent.
					layout.TextLeft = Math.Max(layout.TextLeft, depth * style.ListIndent + markerWidth + style.MarkerGap);
					layout.MarkerX = layout.TextLeft - style.MarkerGap - markerWidth;
					break;
				case RichBlockKind.Quote:
					layout.TextLeft = style.QuoteIndent;
					break;
				case RichBlockKind.CodeBlock:
					layout.TextLeft = style.CodePadding;
					rightInset = style.CodePadding;
					before += style.CodePadding;
					after += style.CodePadding;
					break;
			}

			double available = Math.Max(width - layout.TextLeft - rightInset, 1);
			var items = block.Kind switch
			{
				RichBlockKind.CodeBlock => RichLayoutItems.CodeItems(block.CodeText, blockFace),
				RichBlockKind.Raw => RichLayoutItems.RawItems(block, available, style, blockFace),
				_ => RichLayoutItems.InlineItems(block.Inlines, run => style.RunFace(block, run), style, blockFace),
			};

			layout.length = block.TextLength();

			// Lines are stacked top-down first (y down from the block's top), then flipped once the height is known.
			var source = new LineSource(block.Inlines, code ? block.CodeText : null, block.Alignment, blockFace);
			double y = StackLines(layout.Lines, items, source, before, layout.TextLeft, available, layout.length, !code, style.LineGap);

			double linesTop = before;
			double linesBottom = y;
			layout.Height = y + after;

			double Flip(double down) => layout.Height - down;
			FlipLines(layout.Lines, layout.Height);

			layout.MarkerBaseline = layout.Lines[0].Baseline;
			if (block.Kind == RichBlockKind.Quote)
			{
				layout.QuoteBar = new RectangleDouble(0, Flip(linesBottom), style.QuoteBarWidth, Flip(linesTop));
			}
			else if (code)
			{
				layout.CodeBackground = new RectangleDouble(0, Flip(linesBottom + style.CodePadding), width, Flip(linesTop - style.CodePadding));
			}

			return layout;
		}

		/// <summary>
		/// The caret nearest <paramref name="point"/> (block coordinates): the line whose band holds its y (the first
		/// or last line when it is above or below them all), then the stop on it nearest its x. Right of a line that
		/// wraps with no space this is the line's end (<see cref="RichCaret.AtLineEnd"/>).
		/// </summary>
		public RichCaret HitTest(Vector2 point) => HitTestLines(Lines, point);

		/// <summary>
		/// What an inline-bearing text needs besides its items to become lines: the inlines a fragment's text comes
		/// from (or the code text, non-null only for a code block), the alignment and the face every line is at
		/// least as tall as. A table cell is one of these, so cells reuse the paragraph line building.
		/// </summary>
		internal readonly record struct LineSource(List<RichInline> Inlines, string CodeText, RichAlignment Alignment, StyledTypeFace BlockFace);

		/// <summary>
		/// Breaks <paramref name="items"/> into lines of <paramref name="available"/> width and appends them to
		/// <paramref name="lines"/>, stacked top-down from <paramref name="top"/> (y down; flip them with
		/// <see cref="FlipLines"/> once the container's height is known). Returns the y below the last line.
		/// </summary>
		internal static double StackLines(List<RichLayoutLine> lines, LayoutItem[] items, LineSource source, double top, double textLeft, double available, int length, bool wrapAtSpaces, double lineGap)
		{
			var lineStarts = RichLayoutItems.BreakLines(items, available, wrapAtSpaces);
			double y = top;
			for (int i = 0; i < lineStarts.Count; i++)
			{
				int end = i + 1 < lineStarts.Count ? lineStarts[i + 1] : items.Length;
				var line = BuildLine(source, items, lineStarts[i], end, i == lineStarts.Count - 1, textLeft, available, length);
				line.Top = y;
				line.Baseline = y + lineGap / 2 + line.Ascent;
				y += line.Ascent + line.Descent + lineGap;
				line.Bottom = y;
				lines.Add(line);
			}

			return y;
		}

		/// <summary>
		/// Turns lines stacked y-down (see <see cref="StackLines"/>) into y-up coordinates in a box
		/// <paramref name="height"/> tall.
		/// </summary>
		internal static void FlipLines(List<RichLayoutLine> lines, double height)
		{
			foreach (var line in lines)
			{
				line.Top = height - line.Top;
				line.Bottom = height - line.Bottom;
				line.Baseline = height - line.Baseline;
				foreach (var fragment in line.Fragments)
				{
					fragment.Baseline = line.Baseline;
				}
			}
		}

		/// <summary>
		/// <see cref="HitTest"/> over any stack of lines (a block's, or one table cell's).
		/// </summary>
		internal static RichCaret HitTestLines(List<RichLayoutLine> lines, Vector2 point)
		{
			int lineIndex = lines.Count - 1;
			for (int i = 0; i < lines.Count; i++)
			{
				if (point.Y >= lines[i].Bottom)
				{
					lineIndex = i;
					break;
				}
			}

			// A text atom too wide for one line is split over several, with no caret inside it: a click on any piece
			// goes before or after the whole atom by which half of it was clicked. A line holding only a middle piece
			// has no stop at all, so a click anywhere on it is taken to that piece.
			var line = lines[lineIndex];
			var piece = SplitAtomPieceAt(line, point.X, anywhere: line.CaretStops.Count == 0);
			if (piece != null)
			{
				double characters = piece.Width > 0 ? Math.Clamp((point.X - piece.X) / piece.Width, 0, 1) * piece.Text.Length : 0;
				bool secondHalf = piece.StartInInline + characters >= piece.Atom.RawMarkdown.Length / 2.0;
				return new RichCaret(piece.Start + (secondHalf ? 1 : 0));
			}

			while (lineIndex > 0 && lines[lineIndex].CaretStops.Count == 0)
			{
				lineIndex--;
			}

			var best = lines[lineIndex].CaretStops[0];
			foreach (var stop in lines[lineIndex].CaretStops)
			{
				if (Math.Abs(stop.X - point.X) < Math.Abs(best.X - point.X))
				{
					best = stop;
				}
			}

			return best.Caret;
		}

		private static RichLayoutFragment SplitAtomPieceAt(RichLayoutLine line, double x, bool anywhere)
		{
			foreach (var fragment in line.Fragments)
			{
				bool split = fragment.Atom != null && fragment.Face != null && fragment.Text.Length < fragment.Atom.RawMarkdown.Length;
				if (split && (anywhere || (x >= fragment.X && x <= fragment.X + fragment.Width)))
				{
					return fragment;
				}
			}

			return null;
		}

		/// <summary>
		/// The caret at <paramref name="offset"/>: a thin box centred on the caret x, spanning its line's ascent and
		/// descent. Where a line wraps, the offset shows at the start of the next line, or with
		/// <paramref name="atLineEnd"/> at the end of the line before when it wrapped with no space. An offset with no
		/// stop of its own (inside a surrogate pair) shows at the stop before it.
		/// </summary>
		public RectangleDouble CaretRect(int offset, bool atLineEnd = false, double caretWidth = 1)
		{
			var (line, stop) = FindStop(Lines, length, offset, atLineEnd);
			return CaretBox(line, stop, caretWidth);
		}

		internal static RectangleDouble CaretBox(RichLayoutLine line, RichCaretStop stop, double caretWidth)
		{
			return new RectangleDouble(stop.X - caretWidth / 2, line.Baseline - line.Descent, stop.X + caretWidth / 2, line.Baseline + line.Ascent);
		}

		public RectangleDouble CaretRect(RichCaret caret, double caretWidth = 1) => CaretRect(caret.Offset, caret.AtLineEnd, caretWidth);

		/// <summary>
		/// The line that shows the caret at <paramref name="offset"/> (see <see cref="CaretRect(int, bool, double)"/>).
		/// </summary>
		public RichLayoutLine LineOf(int offset, bool atLineEnd = false) => FindStop(Lines, length, offset, atLineEnd).Line;

		/// <summary>
		/// The line and stop that show <paramref name="offset"/> among <paramref name="lines"/>, whose text is
		/// <paramref name="length"/> offsets long.
		/// </summary>
		internal static (RichLayoutLine Line, RichCaretStop Stop) FindStop(List<RichLayoutLine> lines, int length, int offset, bool atLineEnd)
		{
			if (offset < 0 || offset > length)
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			(RichLayoutLine, RichCaretStop)? before = null;
			(RichLayoutLine, RichCaretStop)? plain = null;
			foreach (var line in lines)
			{
				foreach (var stop in line.CaretStops)
				{
					if (stop.Caret.Offset == offset)
					{
						if (stop.Caret.AtLineEnd == atLineEnd)
						{
							return (line, stop);
						}

						if (!stop.Caret.AtLineEnd)
						{
							plain ??= (line, stop);
						}
					}
					else if (stop.Caret.Offset < offset && !stop.Caret.AtLineEnd)
					{
						before = (line, stop);
					}
				}
			}

			return plain ?? before ?? (lines[0], lines[0].CaretStops[0]);
		}

		private static RichLayoutLine BuildLine(LineSource source, LayoutItem[] items, int start, int end, bool isLast, double textLeft, double available, int length)
		{
			var blockFace = source.BlockFace;
			var line = new RichLayoutLine
			{
				Start = start < items.Length ? items[start].Offset : length,
				End = end < items.Length ? items[end].Offset : length,
				IsLast = isLast,
			};

			// Every line is at least as tall as the block's face, so an empty line still holds a caret.
			double ascent = blockFace.AscentInPixels;
			double descent = Math.Abs(blockFace.DescentInPixels);
			var relativeX = new double[end - start + 1];
			int contentEnd = start;
			for (int i = start; i < end; i++)
			{
				var item = items[i];
				relativeX[i - start + 1] = relativeX[i - start] + item.AdvanceAt(relativeX[i - start]);
				if (item.Kind == LayoutItemKind.Char || item.Kind == LayoutItemKind.Box)
				{
					contentEnd = i + 1;
				}

				if (item.Face != null)
				{
					ascent = Math.Max(ascent, item.Face.AscentInPixels);
					descent = Math.Max(descent, Math.Abs(item.Face.DescentInPixels));
				}
				else
				{
					ascent = Math.Max(ascent, item.BoxHeight);
				}
			}

			line.Ascent = ascent;
			line.Descent = descent;
			line.Width = relativeX[contentEnd - start];
			double alignOffset = source.Alignment switch
			{
				RichAlignment.Center => Math.Max((available - line.Width) / 2, 0),
				RichAlignment.Right => Math.Max(available - line.Width, 0),
				_ => 0,
			};
			line.Left = textLeft + alignOffset;

			// Trailing spaces can run past the column; their carets stay at its edge instead of off the block. Those
			// stops then share one x, so a click reaches only the first of them - deliberate: the spaces past the
			// edge are invisible, and the caret still reaches the rest by keyboard.
			double right = textLeft + available;
			double CaretX(int i) => Math.Min(line.Left + relativeX[i - start], right);
			for (int i = start; i < end; i++)
			{
				if (items[i].StopBefore)
				{
					line.CaretStops.Add(new RichCaretStop(new RichCaret(items[i].Offset), CaretX(i)));
				}
			}

			if (isLast)
			{
				line.CaretStops.Add(new RichCaretStop(new RichCaret(length), CaretX(end)));
			}
			else if (end > start
				&& end < items.Length
				&& items[end].StopBefore
				&& (items[end - 1].Kind == LayoutItemKind.Char || items[end - 1].Kind == LayoutItemKind.Box))
			{
				// Wrapped with no space: the line's visual end is a caret position of its own.
				line.CaretStops.Add(new RichCaretStop(new RichCaret(items[end].Offset, AtLineEnd: true), CaretX(end)));
			}

			AddFragments(source, items, start, end, length, line, i => line.Left + relativeX[i - start]);
			return line;
		}

		// Consecutive characters of one inline become one fragment; each box and tab is its own; breaks draw nothing.
		private static void AddFragments(LineSource block, LayoutItem[] items, int start, int end, int length, RichLayoutLine line, Func<int, double> x)
		{
			int i = start;
			while (i < end)
			{
				var item = items[i];
				if (item.Kind == LayoutItemKind.Break)
				{
					i++;
					continue;
				}

				int runEnd = i + 1;
				if (item.Kind != LayoutItemKind.Box && !item.IsTab)
				{
					while (runEnd < end
						&& (items[runEnd].Kind == LayoutItemKind.Char || items[runEnd].Kind == LayoutItemKind.Space)
						&& !items[runEnd].IsTab
						&& items[runEnd].InlineIndex == item.InlineIndex)
					{
						runEnd++;
					}
				}

				var atom = item.InlineIndex >= 0 ? block.Inlines[item.InlineIndex] as InlineAtom : null;
				string source = item.Kind == LayoutItemKind.Box ? ""
					: block.CodeText != null ? block.CodeText
					: atom != null ? atom.RawMarkdown
					: ((RichRun)block.Inlines[item.InlineIndex]).Text;
				int offsetAfter = runEnd < items.Length ? items[runEnd].Offset : length;
				line.Fragments.Add(new RichLayoutFragment
				{
					InlineIndex = item.InlineIndex,
					StartInInline = item.StartInInline,
					Start = item.Offset,
					// Every piece of an atom split over lines stands for the whole atom, as its one offset.
					Length = atom != null ? 1 : offsetAfter - item.Offset,
					Text = source.Length == 0 ? "" : source.Substring(item.StartInInline, runEnd - i),
					Face = item.Face,
					Atom = atom,
					X = x(i),
					Width = x(runEnd) - x(i),
					Ascent = item.Face?.AscentInPixels ?? item.BoxHeight,
					Descent = item.Face == null ? 0 : Math.Abs(item.Face.DescentInPixels),
				});
				i = runEnd;
			}
		}
	}
}