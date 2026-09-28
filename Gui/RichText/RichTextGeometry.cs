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

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>
	/// Caret geometry and hit testing over a <see cref="DocLayout"/> (agg-gui's rich_text editor/geometry). All
	/// coordinates are the layout's own: x from the left edge, y running down from the document top.
	/// </summary>
	public static class RichTextGeometry
	{
		/// <summary>The caret at <paramref name="pos"/>: its x, the top of its line, and the line height.</summary>
		public static (double X, double Top, double Height) CaretRect(DocLayout layout, DocPos pos)
		{
			double top = 0;
			for (int b = 0; b < pos.Block && b < layout.Blocks.Count; b++)
			{
				top += layout.Blocks[b].Height;
			}

			var block = layout.Blocks[Math.Clamp(pos.Block, 0, layout.Blocks.Count - 1)];
			int lineIndex = LineIndexOf(block, pos.Offset);
			for (int l = 0; l < lineIndex; l++)
			{
				top += block.Lines[l].Height;
			}

			var line = block.Lines[lineIndex];
			return (block.TextLeft + line.AlignDx + XInLine(line, pos.Offset), top, line.Height);
		}

		/// <summary>
		/// The start and end offsets, within <paramref name="pos"/>'s paragraph, of the wrapped line holding it. The
		/// space dropped at a wrap is in neither: the end stops before it and the next line starts after it.
		/// </summary>
		public static (int Start, int End) LineRange(DocLayout layout, DocPos pos)
		{
			var block = layout.Blocks[Math.Clamp(pos.Block, 0, layout.Blocks.Count - 1)];
			int index = LineIndexOf(block, pos.Offset);
			return (index == 0 ? 0 : block.Lines[index].StartOffset, block.Lines[index].EndOffset);
		}

		/// <summary>The document position nearest to layout point (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public static DocPos HitTest(DocLayout layout, double x, double y)
		{
			if (y < 0)
			{
				return default;
			}

			double top = 0;
			for (int b = 0; b < layout.Blocks.Count; b++)
			{
				var block = layout.Blocks[b];
				bool lastBlock = b == layout.Blocks.Count - 1;
				if (y >= top + block.Height && !lastBlock)
				{
					top += block.Height;
					continue;
				}

				for (int l = 0; l < block.Lines.Count; l++)
				{
					var line = block.Lines[l];
					if (y < top + line.Height || l == block.Lines.Count - 1)
					{
						return new DocPos(b, OffsetInLine(line, x - block.TextLeft - line.AlignDx));
					}

					top += line.Height;
				}
			}

			return default;
		}

		// The wrapped line holding offset: the first whose end is at or past it. The whitespace dropped at a wrap
		// belongs to the line before the break.
		private static int LineIndexOf(BlockLayout block, int offset)
		{
			for (int l = 0; l < block.Lines.Count; l++)
			{
				if (offset <= block.Lines[l].EndOffset)
				{
					return l;
				}
			}

			return block.Lines.Count - 1;
		}

		private static double XInLine(LineLayout line, int offset)
		{
			foreach (var fragment in line.Fragments)
			{
				if (offset <= fragment.StartOffset + fragment.Text.Length)
				{
					return fragment.X + fragment.PrefixWidth(offset - fragment.StartOffset);
				}
			}

			return line.Width;
		}

		// The character boundary in the line nearest to x (relative to the line's text origin).
		private static int OffsetInLine(LineLayout line, double x)
		{
			if (line.Fragments.Count == 0)
			{
				return line.StartOffset;
			}

			int best = line.StartOffset;
			double bestDistance = double.MaxValue;
			foreach (var fragment in line.Fragments)
			{
				for (int i = 0; i <= fragment.Text.Length; i++)
				{
					double distance = Math.Abs(fragment.X + fragment.PrefixWidth(i) - x);
					if (distance < bestDistance)
					{
						bestDistance = distance;
						best = fragment.StartOffset + i;
					}
				}
			}

			return best;
		}
	}
}
