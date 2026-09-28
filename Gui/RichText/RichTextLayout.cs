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
using System.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>One styled piece of a laid-out line. X is relative to the line's text origin.</summary>
	public sealed class LineFragment
	{
		public string Text { get; set; }

		public InlineStyle Style { get; set; }

		public StyledTypeFace Face { get; set; }

		public double X { get; set; }

		public double Width { get; set; }

		/// <summary>Gets or sets the character offset into the block where this fragment starts.</summary>
		public int StartOffset { get; set; }

		// Running advances, built once per text so caret and hit tests are not quadratic in the fragment length.
		// Keyed by the text reference because layout grows a fragment by concatenating onto Text.
		private string measuredText;
		private double[] prefixWidths;

		/// <summary>The x (relative to the fragment) just before character <paramref name="count"/> of it.</summary>
		public double PrefixWidth(int count)
		{
			if (!ReferenceEquals(this.measuredText, this.Text))
			{
				this.prefixWidths = new double[this.Text.Length + 1];
				for (int i = 0; i < this.Text.Length; i++)
				{
					this.prefixWidths[i + 1] = this.prefixWidths[i] + this.Face.GetAdvanceForCharacter(this.Text, i);
				}

				this.measuredText = this.Text;
			}

			return this.prefixWidths[Math.Clamp(count, 0, this.Text.Length)];
		}
	}

	/// <summary>A wrapped line of one paragraph. Y values run down from the paragraph's top.</summary>
	public sealed class LineLayout
	{
		public List<LineFragment> Fragments { get; } = new List<LineFragment>();

		public double Width { get; set; }

		public double Height { get; set; }

		public double Ascent { get; set; }

		public double Descent { get; set; }

		/// <summary>Gets or sets the offset alignment adds to every fragment's x.</summary>
		public double AlignDx { get; set; }

		public double BaselineFromTop { get; set; }

		public int StartOffset { get; set; }

		public int EndOffset { get; set; }
	}

	/// <summary>A laid-out paragraph: its lines, where its text starts, and its list marker.</summary>
	public sealed class BlockLayout
	{
		public List<LineLayout> Lines { get; } = new List<LineLayout>();

		public double TextLeft { get; set; }

		/// <summary>Gets or sets the list marker ("•" or "3."), or null for a plain paragraph.</summary>
		public string Marker { get; set; }

		public StyledTypeFace MarkerFace { get; set; }

		public double MarkerX { get; set; }

		public double Height { get; set; }

		public double Width { get; set; }
	}

	/// <summary>A whole document laid out to a width, top-down.</summary>
	public sealed class DocLayout
	{
		public List<BlockLayout> Blocks { get; } = new List<BlockLayout>();

		public double Width { get; set; }

		public double Height { get; set; }
	}

	/// <summary>
	/// Width-constrained, per-run-font layout (agg-gui's rich_text::layout). Paragraphs wrap at whitespace; a word
	/// wider than the line gets a line of its own. A resolver maps each run's style and point size to a face, so the
	/// layout never decides which fonts exist.
	/// </summary>
	public static class RichTextLayout
	{
		public const double IndentPx = 24;

		public const double ListGutterPx = 24;

		public const double MarkerGapPx = 6;

		public const double LineSpacing = 1.35;

		private const int MaxLevels = 16;

		/// <summary>
		/// The default resolver: Liberation Sans, the bold face for bold runs. There is no italic face, so the view
		/// slants italic runs instead. Faces are cached per (bold, size).
		/// </summary>
		public static Func<InlineStyle, double, StyledTypeFace> DefaultResolver()
		{
			var cache = new Dictionary<(bool, double), StyledTypeFace>();
			return (style, size) =>
			{
				if (!cache.TryGetValue((style.Bold, size), out var face))
				{
					face = new StyledTypeFace(style.Bold ? AggContext.DefaultFontBold : AggContext.DefaultFont, size)
					{
						// UI text: follows the System window's typography style.
						ApplyTextStyleSettings = true,
					};
					cache[(style.Bold, size)] = face;
				}

				return face;
			};
		}

		/// <summary>The advance width of <paramref name="text"/> in <paramref name="face"/>, kerning included.</summary>
		public static double Measure(StyledTypeFace face, string text)
		{
			double width = 0;
			for (int i = 0; i < text.Length; i++)
			{
				width += face.GetAdvanceForCharacter(text, i);
			}

			return width;
		}

		/// <summary>
		/// Lays <paramref name="doc"/> out to <paramref name="width"/>. With a <paramref name="cache"/>, paragraphs
		/// unchanged since the cache's last layout (same content, list number, width and size) reuse their old layout,
		/// so an edit only re-measures the paragraphs it touched.
		/// </summary>
		public static DocLayout Layout(RichDoc doc, double width, double defaultFontSize, Func<InlineStyle, double, StyledTypeFace> resolver, RichTextLayoutCache cache = null)
		{
			var numbers = OrderedNumbers(doc.Blocks);
			var layout = new DocLayout();
			cache?.Begin(width, defaultFontSize, resolver);
			for (int i = 0; i < doc.Blocks.Count; i++)
			{
				var block = cache?.Take(doc.Blocks[i], numbers[i]) ?? LayoutBlock(doc.Blocks[i], numbers[i], width, defaultFontSize, resolver);
				cache?.Keep(doc.Blocks[i], numbers[i], block);
				layout.Height += block.Height;
				layout.Blocks.Add(block);
			}

			layout.Width = Math.Max(width, layout.Blocks.Max(b => b.Width));
			cache?.End();
			return layout;
		}

		// Numbered items count up within a run of consecutive ordered items at the same depth; anything else
		// restarts the count, the way word processors number a list.
		private static int[] OrderedNumbers(List<Block> blocks)
		{
			var numbers = new int[blocks.Count];
			var counters = new int[MaxLevels];
			var active = new bool[MaxLevels];
			for (int i = 0; i < blocks.Count; i++)
			{
				int depth = Math.Min(blocks[i].Indent, MaxLevels - 1);
				bool ordered = blocks[i].List == ListKind.Ordered;
				if (ordered)
				{
					counters[depth] = active[depth] ? counters[depth] + 1 : 1;
					numbers[i] = counters[depth];
				}

				for (int k = 0; k < MaxLevels; k++)
				{
					active[k] = ordered && k == depth;
				}
			}

			return numbers;
		}

		private static BlockLayout LayoutBlock(Block block, int ordinal, double width, double defaultFontSize, Func<InlineStyle, double, StyledTypeFace> resolver)
		{
			double baseIndent = block.Indent * IndentPx;
			double textLeft = baseIndent + (block.List != ListKind.None ? ListGutterPx : 0);
			double available = Math.Max(width - textLeft, 1);
			var leadStyle = block.Runs.Count > 0 ? block.Runs[0].Style : InlineStyle.Default;
			var leadFace = resolver(leadStyle, leadStyle.FontSize ?? defaultFontSize);
			var result = new BlockLayout()
			{
				TextLeft = textLeft,
				MarkerFace = leadFace,
				MarkerX = baseIndent,
				Marker = block.List switch
				{
					ListKind.Bullet => "•",
					ListKind.Ordered => $"{ordinal}.",
					_ => null,
				},
			};
			if (result.Marker != null)
			{
				result.MarkerX = Math.Max(textLeft - MarkerGapPx - Measure(leadFace, result.Marker), baseIndent);
			}

			result.Lines.AddRange(Wrap(Tokenize(block, defaultFontSize, resolver), available));
			if (result.Lines.Count == 0)
			{
				// An empty paragraph still takes a line of its lead style's height, and holds the caret.
				result.Lines.Add(new LineLayout() { Ascent = leadFace.AscentInPixels, Descent = Math.Abs(leadFace.DescentInPixels) });
			}

			foreach (var line in result.Lines)
			{
				double content = line.Ascent + line.Descent;
				line.Height = content * LineSpacing;
				line.BaselineFromTop = ((line.Height - content) / 2) + line.Ascent;
				line.AlignDx = block.Align switch
				{
					Justification.Center => Math.Max((available - line.Width) / 2, 0),
					Justification.Right => Math.Max(available - line.Width, 0),
					_ => 0,
				};
				result.Height += line.Height;
				result.Width = Math.Max(result.Width, textLeft + line.Width);
			}

			return result;
		}

		// Splits every run into alternating word and whitespace pieces, each measured in its run's face.
		private static List<(LineFragment Piece, bool IsSpace)> Tokenize(Block block, double defaultFontSize, Func<InlineStyle, double, StyledTypeFace> resolver)
		{
			var pieces = new List<(LineFragment, bool)>();
			int runStart = 0;
			foreach (var run in block.Runs)
			{
				var face = resolver(run.Style, run.Style.FontSize ?? defaultFontSize);
				int start = 0;
				for (int i = 1; i <= run.Text.Length; i++)
				{
					if (i == run.Text.Length || char.IsWhiteSpace(run.Text[i]) != char.IsWhiteSpace(run.Text[start]))
					{
						var text = run.Text.Substring(start, i - start);
						var piece = new LineFragment() { Text = text, Style = run.Style, Face = face, Width = Measure(face, text), StartOffset = runStart + start };
						pieces.Add((piece, char.IsWhiteSpace(text[0])));
						start = i;
					}
				}

				runStart += run.Text.Length;
			}

			return pieces;
		}

		// Greedy wrap: a word goes on the current line if it fits after the pending space, otherwise it starts the
		// next. Whitespace at a break is dropped, so a line never starts with a space.
		private static List<LineLayout> Wrap(List<(LineFragment Piece, bool IsSpace)> pieces, double available)
		{
			var lines = new List<LineLayout>();
			var current = new List<LineFragment>();
			double currentWidth = 0;
			LineFragment pendingSpace = null;
			for (int i = 0; i < pieces.Count;)
			{
				if (pieces[i].IsSpace)
				{
					if (current.Count > 0)
					{
						pendingSpace = pieces[i].Piece;
					}

					i++;
					continue;
				}

				var word = new List<LineFragment>();
				while (i < pieces.Count && !pieces[i].IsSpace)
				{
					word.Add(pieces[i++].Piece);
				}

				double wordWidth = word.Sum(p => p.Width);
				double spaceWidth = pendingSpace?.Width ?? 0;
				if (current.Count > 0 && currentWidth + spaceWidth + wordWidth > available)
				{
					lines.Add(FinishLine(current));
					current = new List<LineFragment>();
					currentWidth = 0;
				}
				else if (pendingSpace != null)
				{
					currentWidth += pendingSpace.Width;
					current.Add(pendingSpace);
				}

				pendingSpace = null;
				currentWidth += wordWidth;
				current.AddRange(word);
			}

			if (pendingSpace != null && current.Count > 0)
			{
				current.Add(pendingSpace);
			}

			if (current.Count > 0)
			{
				lines.Add(FinishLine(current));
			}

			return lines;
		}

		// Positions the pieces left to right and merges neighbours that share a style and face into one fragment.
		private static LineLayout FinishLine(List<LineFragment> pieces)
		{
			var line = new LineLayout();
			double x = 0;
			foreach (var piece in pieces)
			{
				line.Ascent = Math.Max(line.Ascent, piece.Face.AscentInPixels);
				line.Descent = Math.Max(line.Descent, Math.Abs(piece.Face.DescentInPixels));
				var last = line.Fragments.LastOrDefault();
				if (last != null && last.Style == piece.Style && last.Face == piece.Face && last.StartOffset + last.Text.Length == piece.StartOffset)
				{
					last.Text += piece.Text;
					last.Width += piece.Width;
				}
				else
				{
					piece.X = x;
					line.Fragments.Add(piece);
				}

				x += piece.Width;
			}

			line.Width = x;
			line.StartOffset = line.Fragments[0].StartOffset;
			var end = line.Fragments[line.Fragments.Count - 1];
			line.EndOffset = end.StartOffset + end.Text.Length;
			return line;
		}
	}
}
